using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Replay;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections.Fees;

// Feature 013, K4 (US12, T0948; FR-121, T77; mensajes.md §10.19): el gasto de cobranza. La regla de la etapa (o el honorario o gasto jurídico
// cobrable al deudor) lo <b>propone</b> y lo informa al deudor con un mensaje CollectionFeeNotice; sólo después del Sent o Delivered de ese aviso se
// <b>causa</b>: una operación Charge por RegistroDeCartera y el hecho GastoDeCobranzaCausado (CA) por EmisionDeCartera. Cartera no conoce cuentas.
// (nuevo)

/// <summary>Un gasto propuesto: el valor, la etapa, la entidad de la clave del aviso y lo que lo originó (la entrada de etapa o la actuación). (nuevo)</summary>
public sealed record GastoPropuesto(decimal Valor, string Etapa, string ClaveDeEntidad, Guid? OriginPublicId);

/// <summary>Un gasto de cobranza informado o por informar, como lo muestra la lista de gastos por causar. (nuevo)</summary>
public sealed record PendingCollectionFeeDto(
    Guid NoticePublicId,
    CreditoRefDto Loan,
    PersonaRefDto Debtor,
    decimal Amount,
    string StageCode,
    string Origin,
    DebtorMessageEventKind NoticeStatus,
    DateTimeOffset? InformedAt,
    bool Causable);

/// <summary>Lo que causó el comando: una operación por gasto, con su número y el hecho emitido. (nuevo)</summary>
public sealed record CausedCollectionFeeDto(Guid NoticePublicId, CreditoRefDto Loan, decimal Amount, Guid OperationPublicId, string OperationNumber,
    Guid FactMessagePublicId);

/// <summary>
/// La decisión de un cargo que guarda la operación <c>Charge</c> en su <c>Payload</c> (K4, T0948) y que la reconstrucción lee
/// (<see cref="ContenidoDeCargo"/>): el aviso que lo informó, el concepto, el valor, la etapa y el tercero. (nuevo)
/// </summary>
public sealed record DecisionDeCargo
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Guid NoticePublicId { get; init; }

    public string Concept { get; init; } = nameof(LoanConcept.CollectionFee);

    public decimal Amount { get; init; }

    public string StageCode { get; init; } = string.Empty;

    public Guid? PayeePersonPublicId { get; init; }

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    public ContenidoDeCargo Contenido() => new(Enum.TryParse<LoanConcept>(Concept, out var c) ? c : LoanConcept.CollectionFee, Amount);

    public static DecisionDeCargo? Leer(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Trim() == "{}") return null;
        try
        {
            var d = JsonSerializer.Deserialize<DecisionDeCargo>(payload, Json);
            return d is null || d.Amount <= 0m ? null : d;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Lo común del gasto de cobranza (K4, T0943, T0947, T0948): informar al deudor (el aviso <c>CollectionFeeNotice</c> con la plantilla <c>GASTOCOBR</c>),
/// leer el valor de un aviso y decidir si ya se puede causar.
/// <para>
/// <b>Dónde vive el valor propuesto</b>: data-model §26 no trae una tabla de gastos propuestos. El aviso es la propuesta: su clave de idempotencia lleva
/// el valor de la regla de la etapa (<c>CollectionFee:{crédito}:{etapa}:{valor}:CollectionFeeNotice:{fecha}</c>) y el de un honorario o gasto jurídico lo
/// lleva su actuación (<c>OriginPublicId</c> = la actuación, clave <c>CollectionFee:{crédito}:juridico:{actuación}:…</c>). El mensaje es inmutable, así
/// que el valor que se causa es el que se informó.
/// </para>
/// </summary>
public static class GastosDeCobranza
{
    public const string Plantilla = "GASTOCOBR";
    public const string Origen = "CollectionFee";
    public const string OrigenEtapa = "Stage";
    public const string OrigenJuridico = "LegalAction";
    private const string MarcaJuridica = "juridico";

    /// <summary>El código del participante que causa: Cartera (la clave de idempotencia de la operación es única con él).</summary>
    public const string CodigoDeQuienCausa = "CAR";

    /// <summary>El prefijo del comprobante de la operación <c>Charge</c>.</summary>
    public const string PrefijoDeOperacion = "GCO-";

    public static string ClaveDeEtapa(Guid loanPublicId, string etapa, decimal valor) =>
        string.Create(CultureInfo.InvariantCulture, $"{loanPublicId:N}:{etapa}:{valor:0.00}");

    public static string ClaveDeActuacion(Guid loanPublicId, Guid actuacionPublicId) => $"{loanPublicId:N}:{MarcaJuridica}:{actuacionPublicId:N}";

    /// <summary>
    /// El tope del gasto de cobranza vigente el <paramref name="dia"/> (K4, bloque D): <c>Cobranza.TopeGasto.Porcentaje</c> y
    /// <c>Cobranza.TopeGasto.ValorMaximo</c>; vacíos, sin tope.
    /// </summary>
    public static async Task<Result<TopeDelGastoDeCobranza>> TopeAsync(ReglasDeParametrosDeCartera parametros, DateOnly dia, CancellationToken ct)
    {
        var porcentaje = await parametros.LeerAsync(ParametrosDeCartera.CobranzaTopeGastoPorcentaje, dia, ct: ct);
        if (porcentaje.IsFailure) return Result.Failure<TopeDelGastoDeCobranza>(porcentaje.Error);
        var valor = await parametros.LeerAsync(ParametrosDeCartera.CobranzaTopeGastoValorMaximo, dia, ct: ct);
        if (valor.IsFailure) return Result.Failure<TopeDelGastoDeCobranza>(valor.Error);
        return Result.Success(new TopeDelGastoDeCobranza(porcentaje.Value.Valor as decimal?, valor.Value.Valor as decimal?));
    }

    /// <summary>La clave de idempotencia de la operación <c>Charge</c> que causa el gasto de un aviso (una sola por aviso).</summary>
    public static string ClaveDeOperacion(Guid avisoPublicId) => $"{Origen}:{avisoPublicId:N}";

    /// <summary>
    /// Informa el gasto al deudor por el primer canal que la ley y el titular permiten (<see cref="EnvioDeCobranza"/>): el aviso nace en la unidad de
    /// trabajo de quien lo motiva, sin guardar; la clave lo hace único.
    /// </summary>
    public static Task<Result<MensajeEncolado>> InformarAsync(ColaDeMensajesAlDeudor cola, Loan credito, GastoPropuesto gasto, IDateTimeService reloj,
        ActorKind actorKind, int? actorUserId, CancellationToken ct) =>
        EnvioDeCobranza.EncolarAsync(cola, canal => new PedidoDeMensajeAlDeudor(credito.DebtorPersonId, credito.Id, DebtorMessagePurpose.CollectionFeeNotice, canal,
            Plantilla, Origen, gasto.ClaveDeEntidad, actorKind, actorUserId)
        {
            OriginPublicId = gasto.OriginPublicId,
            Valores = new Dictionary<string, string?>
            {
                ["ValorDelGasto"] = ErroresDeCobranza.Pesos(gasto.Valor),
                ["Etapa"] = gasto.Etapa,
                ["FechaDeCausacion"] = ErroresDeCobranza.Fecha(reloj.HoyLocal),
            },
        }, reloj.UtcNow, ct);

    /// <summary>
    /// El valor y la etapa de un aviso de gasto: de la regla de la etapa, leídos de su clave; de una actuación jurídica, de la actuación. Nulo si el aviso
    /// no es un gasto de Cartera.
    /// </summary>
    public static (decimal Valor, string Etapa, string Origen)? Leer(DebtorMessage aviso, IReadOnlyDictionary<Guid, LegalProcessAction> actuaciones,
        string? etapaVigente)
    {
        if (aviso.Purpose != DebtorMessagePurpose.CollectionFeeNotice || aviso.OriginKind != Origen) return null;
        var partes = aviso.IdempotencyKey.Split(':');
        // {origen}:{crédito}:{etapa|juridico}:{valor|actuación}:{propósito}:{fecha}
        if (partes.Length < 4) return null;
        if (partes[2] == MarcaJuridica)
            return aviso.OriginPublicId is { } a && actuaciones.TryGetValue(a, out var actuacion) && actuacion.Amount is { } monto
                ? (monto, etapaVigente ?? string.Empty, OrigenJuridico)
                : null;
        return decimal.TryParse(partes[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) && valor > 0m
            ? (valor, partes[2], OrigenEtapa)
            : null;
    }

    /// <summary>¿El aviso ya salió? Su último evento es <c>Sent</c> o <c>Delivered</c> (fuera de producción, también <c>Simulated</c>).</summary>
    public static bool Informado(DebtorMessageEventKind? ultimo, bool enProduccion) =>
        ultimo is DebtorMessageEventKind.Sent or DebtorMessageEventKind.Delivered || (!enProduccion && ultimo == DebtorMessageEventKind.Simulated);

    /// <summary>Los avisos de gasto de los créditos (o del deudor), con su último evento, su valor y si ya se causaron.</summary>
    public static async Task<IReadOnlyList<GastoInformado>> GastosAsync(IApplicationDbContext db, IReadOnlyCollection<int> loanIds, bool enProduccion,
        CancellationToken ct)
    {
        var ids = loanIds.ToArray();
        var avisos = await db.DebtorMessages.AsNoTracking()
            .Where(m => m.LoanId != null && ids.Contains(m.LoanId.Value) && m.Purpose == DebtorMessagePurpose.CollectionFeeNotice && m.OriginKind == Origen)
            .ToListAsync(ct);
        if (avisos.Count == 0) return [];
        var avisoIds = avisos.Select(a => a.Id).ToList();
        var eventos = await db.DebtorMessageEvents.AsNoTracking().Where(e => avisoIds.Contains(e.DebtorMessageId)).ToListAsync(ct);
        var claves = avisos.Select(a => ClaveDeOperacion(a.PublicId)).ToList();
        var causados = (await db.LoanOperations.AsNoTracking()
            .Where(o => o.Kind == LoanOperationKind.Charge && o.CollectorCode == CodigoDeQuienCausa && o.IdempotencyKey != null && claves.Contains(o.IdempotencyKey)
                        && o.ReversedByOperationId == null)
            .Select(o => o.IdempotencyKey!).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var origenes = avisos.Where(a => a.OriginPublicId != null).Select(a => a.OriginPublicId!.Value).ToList();
        var actuaciones = await db.LegalProcessActions.AsNoTracking().Where(a => origenes.Contains(a.PublicId)).ToDictionaryAsync(a => a.PublicId, ct);
        var etapas = await CobranzaDeCartera.EtapaVigenteAsync(db, ids, ct);
        var salida = new List<GastoInformado>();
        foreach (var a in avisos)
        {
            var leido = Leer(a, actuaciones, etapas.GetValueOrDefault(a.LoanId!.Value)?.StageCode);
            if (leido is not { } g) continue;
            var historia = eventos.Where(e => e.DebtorMessageId == a.Id).OrderBy(e => e.Sequence).ToList();
            var ultimo = historia.LastOrDefault();
            var salio = historia.FirstOrDefault(e => e.Kind is DebtorMessageEventKind.Sent or DebtorMessageEventKind.Delivered or DebtorMessageEventKind.Simulated);
            salida.Add(new GastoInformado(a, g.Valor, g.Etapa, g.Origen, ultimo?.Kind ?? DebtorMessageEventKind.Queued,
                Informado(ultimo?.Kind, enProduccion) ? salio?.OccurredAt : null, causados.Contains(ClaveDeOperacion(a.PublicId)),
                a.OriginPublicId is { } o && actuaciones.TryGetValue(o, out var actuacion) ? actuacion : null));
        }
        return salida;
    }
}

/// <summary>Un aviso de gasto con su valor, su etapa, su origen, su último evento, cuándo salió (si salió), si ya se causó y su actuación jurídica. (nuevo)</summary>
public sealed record GastoInformado(DebtorMessage Aviso, decimal Valor, string Etapa, string Origen, DebtorMessageEventKind Estado, DateTime? InformadoEn,
    bool Causado, LegalProcessAction? Actuacion)
{
    public bool Causable => InformadoEn is not null && !Causado;
}

// ------------------------------------------------------------------------------------------------------------------ lista --

/// <summary>
/// Los gastos de cobranza por causar (feature 013, K4, T0948; ruta nueva de T0956, <c>GET /api/lending/collection-fees/pending?personPublicId=&amp;loanPublicId=</c>,
/// <c>Lending.Collections.Read</c>; FR-121): los informados o por informar que todavía no se causaron, con su valor, su etapa, su origen (regla de la
/// etapa o actuación jurídica), el estado del aviso y si ya se pueden causar (el aviso salió). Filtrados en silencio por el alcance. (nuevo)
/// </summary>
public sealed record ListPendingCollectionFeesQuery(Guid? PersonPublicId = null, Guid? LoanPublicId = null)
    : IRequest<Result<IReadOnlyList<PendingCollectionFeeDto>>>;

public sealed class ListPendingCollectionFeesQueryValidator : AbstractValidator<ListPendingCollectionFeesQuery>;

public sealed class ListPendingCollectionFeesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, ColaDeMensajesAlDeudor cola,
    IDateTimeService reloj) : IRequestHandler<ListPendingCollectionFeesQuery, Result<IReadOnlyList<PendingCollectionFeeDto>>>
{
    public async Task<Result<IReadOnlyList<PendingCollectionFeeDto>>> Handle(ListPendingCollectionFeesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.Loans.AsNoTracking().Where(l => !l.IsDeleted && l.Status == LoanStatus.Active)
            .PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        if (request.PersonPublicId is { } persona)
        {
            var id = await db.People.AsNoTracking().Where(p => p.PublicId == persona).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(l => l.DebtorPersonId == id);
        }
        if (request.LoanPublicId is { } loan) consulta = consulta.Where(l => l.PublicId == loan);
        var creditos = await consulta.ToDictionaryAsync(l => l.Id, ct);
        var gastos = await GastosDeCobranza.GastosAsync(db, creditos.Keys.ToList(), cola.EnProduccion, ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, creditos.Values.Select(c => c.DebtorPersonId), ct);
        var desfase = reloj.AhoraLocal.Offset;
        return Result.Success<IReadOnlyList<PendingCollectionFeeDto>>(gastos
            .Where(g => !g.Causado && g.Estado is not (DebtorMessageEventKind.Cancelled or DebtorMessageEventKind.Excluded or DebtorMessageEventKind.Failed))
            .Select(g =>
            {
                var c = creditos[g.Aviso.LoanId!.Value];
                return new PendingCollectionFeeDto(g.Aviso.PublicId, new CreditoRefDto(c.PublicId, c.Number), personas[c.DebtorPersonId], g.Valor, g.Etapa,
                    g.Origen, g.Estado, g.InformadoEn is { } en ? new DateTimeOffset(CobranzaDeCartera.ALocal(en, desfase), desfase) : null, g.Causable);
            }).OrderBy(g => g.Loan.Number, StringComparer.Ordinal).ToList());
    }
}

// ------------------------------------------------------------------------------------------------------------------ causar --

/// <summary>
/// Causa los gastos de cobranza informados de una persona (feature 013, K4, T0948; ruta nueva de T0956, <c>POST /api/lending/collection-fees/cause</c>,
/// <c>Lending.CollectionStages.Manage</c>, con clave; FR-121, T77; mensajes.md §10.19). Toma los gastos de los créditos vigentes del alcance de la persona
/// cuyo aviso <c>CollectionFeeNotice</c> ya salió (<c>Sent</c> o <c>Delivered</c>) y no se causaron —o sólo los avisos de <see cref="NoticePublicIds"/>: uno
/// que no salió responde <c>Lending.CollectionFee.NoticeNotSent</c>, uno ya causado <c>.AlreadyCaused</c>—. Por cada gasto, en una transacción: la
/// validación previa contable de pantalla (A8), la operación <c>Charge</c> (comprobante <c>GCO-</c>, clave única por aviso) con sus filas <c>Charge</c>
/// <c>+CollectionFee</c> por <see cref="RegistroDeCartera"/>, y el hecho <c>GastoDeCobranzaCausado</c> por <see cref="EmisionDeCartera"/> —sin cuentas:
/// las pone Contabilidad—; un honorario jurídico es cuenta por pagar al abogado (<c>payeePersonPublicId</c>). Lo origina la persona: <c>mensajes.md</c>
/// §10.19 no admite proceso, así que el ciclo diario sólo propone e informa. (nuevo)
/// </summary>
public sealed record CauseCollectionFeesCommand(Guid PersonPublicId, IReadOnlyList<Guid>? NoticePublicIds = null)
    : IRequest<Result<IReadOnlyList<CausedCollectionFeeDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CauseCollectionFeesCommandValidator : AbstractValidator<CauseCollectionFeesCommand>
{
    public CauseCollectionFeesCommandValidator() => RuleFor(x => x.PersonPublicId).NotEmpty();
}

public sealed class CauseCollectionFeesCommandHandler(
    IApplicationDbContext db,
    ColaDeMensajesAlDeudor cola,
    CargadorDeReconstruccion cargador,
    PuestaAlDiaDeCredito puestaAlDia,
    PartesDeHechosDeCartera partes,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ValidacionPreviaContableDeCartera validacionPrevia,
    FechaContable fechaContable,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcanceDeLaPeticion,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CauseCollectionFeesCommand, Result<IReadOnlyList<CausedCollectionFeeDto>>>
{
    public async Task<Result<IReadOnlyList<CausedCollectionFeeDto>>> Handle(CauseCollectionFeesCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is null) return Falla(ErroresDeGobierno.SinUsuario());
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = await db.Loans.AsNoTracking().Where(l => l.DebtorPersonId == persona && !l.IsDeleted && l.Status == LoanStatus.Active)
            .PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).ToDictionaryAsync(l => l.Id, ct);
        var gastos = await GastosDeCobranza.GastosAsync(db, creditos.Keys.ToList(), cola.EnProduccion, ct);

        List<GastoInformado> aCausar;
        if (request.NoticePublicIds is { Count: > 0 } pedidos)
        {
            aCausar = [];
            foreach (var pedido in pedidos.Distinct())
            {
                var g = gastos.FirstOrDefault(x => x.Aviso.PublicId == pedido);
                if (g is null) return Falla(ErroresDeCobranza.GastoInexistente());
                var numero = creditos[g.Aviso.LoanId!.Value].Number;
                if (g.Causado) return Falla(ErroresDeCobranza.GastoYaCausado(numero));
                if (g.InformadoEn is null) return Falla(ErroresDeCobranza.AvisoDeGastoSinEnviar(numero));
                aCausar.Add(g);
            }
        }
        else
        {
            aCausar = gastos.Where(g => g.Causable).ToList();
        }
        if (aCausar.Count == 0) return Result.Success<IReadOnlyList<CausedCollectionFeeDto>>([]);

        var v = reloj.HoyLocal;
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([persona.Value], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync(aCausar.Select(g => g.Aviso.LoanId!.Value).Distinct().ToList(), ModoDeEspera.Esperar, ct);
            var salida = new List<CausedCollectionFeeDto>();
            foreach (var g in aCausar.OrderBy(g => g.Aviso.Id))
            {
                var causado = await CausarAsync(g, creditos[g.Aviso.LoanId!.Value], v, actor, ct);
                if (causado.IsFailure) return Falla(causado.Error);
                salida.Add(causado.Value);
            }
            return Result.Success<IReadOnlyList<CausedCollectionFeeDto>>(salida);
        }, ct);
    }

    private async Task<Result<CausedCollectionFeeDto>> CausarAsync(GastoInformado g, Loan credito, DateOnly v, IngenIA365ERP.Application.Common.Execution.Actor actor, CancellationToken ct)
    {
        var vivo = await db.Loans.FirstAsync(l => l.Id == credito.Id, ct);
        if (vivo.Status != LoanStatus.Active)
            return Result.Failure<CausedCollectionFeeDto>(Novelties.ErroresDeNovedades.CreditoNoActivo(vivo.Number, vivo.Status.ToString()));
        var control = await db.LoanControls.FirstAsync(c => c.LoanId == credito.Id, ct);
        var marca = control.LastAccruedDate;
        var cargado = await cargador.CargarAsync(credito.Id, null, marca > v ? marca : v, ct);
        if (cargado is not { Listo: true }) return Result.Failure<CausedCollectionFeeDto>(cargado?.Error ?? Settlement.LecturaDelCredito.Inexistente());

        var contable = await fechaContable.CalcularAsync(v, ct);
        if (contable.IsFailure) return Result.Failure<CausedCollectionFeeDto>(contable.Error);
        var datos = (await partes.DatosAsync([cargado], ct))[credito.Id];
        var hecho = datos.Hecho(contable.Value);
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(credito.Id, contable.Value, ct);
        Guid? beneficiario = null;
        if (g.Actuacion is { Kind: LegalActionKind.Fee } honorario)
        {
            var abogado = await db.LegalProcesses.AsNoTracking().Where(p => p.Id == honorario.LegalProcessId).Select(p => p.LawyerPersonId).FirstAsync(ct);
            beneficiario = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == abogado).Select(p => (Guid?)p.PublicId).FirstAsync(ct);
        }
        var previo = EmisionDeCartera.ContenidoDeGasto(hecho, categoria, v, reloj.UtcNow, g.Valor, g.Aviso.PublicId, g.Etapa, beneficiario);
        var decision = await validacionPrevia.DecidirAsync([new MensajeContableDto(new IntegrationEnvelopeV1
        {
            MessageId = Guid.Empty, Type = GastoDeCobranzaCausadoV1.Type, Kind = IntegrationMessageKind.Business, OriginModule = ModulosDeOrigen.Cartera,
            OriginEventKey = ClavesDeEvento.Confirmacion, BranchPublicId = hecho.SucursalPublicId,
        }, previo)], OrigenDeValidacionPrevia.Pantalla, $"el gasto de cobranza del crédito {credito.Number}", ct);
        if (decision.Bloqueo is { } bloqueo) return Result.Failure<CausedCollectionFeeDto>(bloqueo);

        await puestaAlDia.PrepararAsync([credito.Id], v, ct);
        var cargo = new DecisionDeCargo { NoticePublicId = g.Aviso.PublicId, Amount = g.Valor, StageCode = g.Etapa, PayeePersonPublicId = beneficiario };
        var payload = cargo.Serializar();
        var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.Charge, GastosDeCobranza.PrefijoDeOperacion, ct);
        var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new LoanOperation
        {
            LoanId = credito.Id,
            Kind = LoanOperationKind.Charge,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = v,
            AccountingDate = contable.Value,
            RegisteredAt = sello.RegisteredAt,
            Amount = g.Valor,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorCode = GastosDeCobranza.CodigoDeQuienCausa,
            IdempotencyKey = GastosDeCobranza.ClaveDeOperacion(g.Aviso.PublicId),
            Reason = $"Gasto de cobranza informado ({g.Etapa})",
            Payload = payload,
            InputsSha256 = TramoDeDesembolso.Sha256(payload),
            RulesVersion = cargado.Vigentes.RulesVersion,
            PrevalidationOutcome = decision.Outcome.ToString(),
        });
        var id = CargadorDeReconstruccion.IdEnLaEntrada(operacion);
        var libro = marca >= v ? (await cargador.LibroAsync([credito.Id], marca, ct))[credito.Id] : [];
        EfectoEnElLibro efecto;
        try
        {
            efecto = EfectoEnElLibro.Calcular(cargado.Entrada!, CargadorDeReconstruccion.ConOperacion(cargado.Entrada!, operacion, cargo.Contenido()), id, v, marca,
                libro, []);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<CausedCollectionFeeDto>(ErroresDeGobierno.De(ex.Problema));
        }

        var emitido = await emision.GastoDeCobranzaAsync(new GastoDeCobranzaAEmitir(hecho, operacion, g.Valor, g.Aviso.PublicId, g.Etapa, beneficiario,
            decision.Outcome), ct);
        if (emitido.IsFailure) return Result.Failure<CausedCollectionFeeDto>(emitido.Error);
        var mensaje = emitido.Value.PublicId;
        registro.RegistrarTransacciones(efecto.Propias.Select(f => RegistroDeCartera.FilaDeHecho(credito.Id, f, operacion, null, contable.Value,
            operacion.RegisteredAt, mensaje)));
        registro.RegistrarTransacciones(RegistroDeCartera.DiferenciasPorFamilia(credito.Id, efecto.Diferencias, operacion, contable.Value,
            operacion.RegisteredAt, (_, _) => mensaje));
        await db.SaveChangesAsync(ct);
        if (g.Actuacion is { } actuacion)
        {
            var viva = await db.LegalProcessActions.FirstAsync(a => a.Id == actuacion.Id, ct);
            viva.ChargeOperationId = operacion.Id;
        }

        var puesta = await puestaAlDia.RegistrarAsync(operacion, ct);
        if (puesta.IsFailure) return Result.Failure<CausedCollectionFeeDto>(puesta.Error);
        await db.SaveChangesAsync(ct);
        var u = (await PosicionesDeCartera.UltimasAsync(db, [credito.Id], v, v, ct)).GetValueOrDefault((credito.Id, v));
        registro.PosicionVersionada(control, efecto.Despues, vivo, cargado.Vigentes, u.Version, u.Huella, reloj.UtcNow);
        await db.SaveChangesAsync(ct);
        await auditoria.EmitirOperacionRegistradaAsync(operacion.PublicId, operacion.Number, nameof(LoanOperationKind.Charge), new
        {
            notice = g.Aviso.PublicId, stage = g.Etapa, amount = g.Valor, valueDate = v, payee = beneficiario,
        }, ct);
        return Result.Success(new CausedCollectionFeeDto(g.Aviso.PublicId, new CreditoRefDto(credito.PublicId, credito.Number), g.Valor, operacion.PublicId,
            operacion.Number, mensaje));
    }

    private static Result<IReadOnlyList<CausedCollectionFeeDto>> Falla(Error e) => Result.Failure<IReadOnlyList<CausedCollectionFeeDto>>(e);
}
