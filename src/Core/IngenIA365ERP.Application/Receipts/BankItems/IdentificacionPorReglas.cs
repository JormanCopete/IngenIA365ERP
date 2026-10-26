using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Identificacion;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.BankItems;

// Feature 015, R1, US6 (T0379; FR-067, FR-069, FR-071; D11, D15; SC-008): las reglas que proponen el destino de cada abono —referencia, documento del consignante,
// consignación reportada, valor exacto de un exigible— y la consignación reportada en ventanilla. La propuesta la confirma una persona (IdentifyBankItemCommand con la
// regla); lo de referencia exacta se aplica al cargar el extracto si el parámetro lo permite, a nombre de quien lo carga, con la aplicación no interactiva. Ninguna tarea
// programada aplica: la que revisa la antigüedad sólo avisa de la consignación reportada que no aparece.

/// <summary>Una regla de identificación. (nuevo)</summary>
public sealed record IdentificationRuleDto(Guid PublicId, string Code, string Name, IdentificationCriterion Criterion, int Priority, bool ApplyOnLoad, bool IsActive);

/// <summary>La propuesta de destino de un abono. (nuevo)</summary>
public sealed record BankItemProposalDto(
    Guid RulePublicId,
    string RuleCode,
    IdentificationCriterion Criterion,
    Guid PersonPublicId,
    string PersonName,
    Guid? ProductPublicId,
    ApplicationTarget? Target,
    decimal Amount,
    bool ApplyOnLoad,
    Guid? ReportedDepositPublicId,
    string Explanation);

/// <summary>Una consignación reportada en ventanilla. (nuevo)</summary>
public sealed record ReportedDepositDto(
    Guid PublicId,
    string Number,
    Guid PersonPublicId,
    string PersonName,
    Guid OwnBankAccountPublicId,
    DateOnly DepositDate,
    decimal Amount,
    string? Reference,
    ApplicationTarget Target,
    Guid ProductPublicId,
    bool AppliedImmediately,
    ReportedDepositStatus Status,
    string? ReceiptNumber,
    Guid? StatementLinePublicId,
    DateTime RegisteredAt);

/// <summary>Lo que hizo la aplicación al cargar un extracto. (nuevo)</summary>
public sealed record AplicacionAlCargarDto(int Lines, int Proposed, int Applied, int Rejected, bool Enabled);

/// <summary>Los errores de la identificación por reglas y de la consignación reportada. (nuevo)</summary>
public static class ErroresDeIdentificacion
{
    public const string CodigoAplicarSoloConReferencia = "Receipts.IdentificationRule.ApplyOnLoadOnlyByReference";
    public const string CodigoCuentaNoDeRecaudo = "Receipts.ReportedDeposit.NotCollectionAccount";
    public const string CodigoNoPorVerificar = "Receipts.ReportedDeposit.NotToVerify";
    public const string CodigoNoCuadra = "Receipts.ReportedDeposit.AmountMismatch";
    public const string CodigoRechazada = "Receipts.ReportedDeposit.Rejected";

    public static Error AplicarSoloConReferencia() => new(CodigoAplicarSoloConReferencia,
        "Sólo una regla por referencia de pago se aplica al cargar: las demás proponen y una persona confirma.");

    public static Error CuentaNoDeRecaudo() => new(CodigoCuentaNoDeRecaudo, "La consignación se reporta a una cuenta bancaria propia de recaudo activa.");

    public static Error NoPorVerificar(ReportedDepositStatus estado) => new ErrorConDatos(CodigoNoPorVerificar,
        $"La consignación reportada ya no está por verificar (está {estado}).", new { status = estado.ToString() });

    public static Error NoCuadra(decimal pendiente, decimal valor) => new ErrorConDatos(CodigoNoCuadra,
        string.Create(CultureInfo.InvariantCulture, $"Al abono le quedan {pendiente:N2} por identificar y la consignación reportada es de {valor:N2}."),
        new { pending = pendiente, amount = valor });

    public static Error Rechazada(string codigo, string mensaje) => new ErrorConDatos(CodigoRechazada, $"El módulo no aplicó la consignación: {mensaje}",
        new { moduleCode = codigo });
}

/// <summary>
/// Lo que el motor de propuestas necesita de un abono (feature 015, R1, T0379): las reglas activas, los productos cuyo número es la referencia (por la búsqueda de
/// Cartera, sin consultar tablas suyas), las personas con el documento del consignante, las consignaciones reportadas por verificar de la misma cuenta y lo exigible de
/// hoy de los créditos del consignante. Las cuentas no entran en «valor exacto» en R1: sus exigibles son cuotas que Captaciones resuelve por modalidad. (nuevo)
/// </summary>
public sealed class PropuestasDeIdentificacion(
    IApplicationDbContext db,
    IBusquedaDeObligaciones busqueda,
    IContratoDeObligaciones obligaciones,
    ParametrosDeCaja parametros)
{
    public async Task<(PropuestaDeIdentificacion? Propuesta, IdentificationRule? Regla)> ProponerAsync(LineaDeExtractoDto linea, DateOnly hoy, CancellationToken ct)
    {
        var reglas = await db.IdentificationRules.AsNoTracking().Where(r => r.IsActive).ToListAsync(ct);
        if (reglas.Count == 0 || linea.Amount - linea.Identified <= 0m) return (null, null);
        var criterios = reglas.Select(r => r.Criterion).ToHashSet();
        var referencia = linea.Reference?.Trim();

        var porReferencia = new List<CandidatoPorReferencia>();
        if (criterios.Contains(IdentificationCriterion.PaymentReference) && !string.IsNullOrWhiteSpace(referencia))
            foreach (var o in await busqueda.PorNumeroAsync(new BusquedaPorNumero(referencia), ct))
                porReferencia.Add(new CandidatoPorReferencia(o.PersonPublicId, o.ObligationPublicId,
                    o.Kind == ClaseDeObligacionEncontrada.Loan ? ApplicationTarget.Loan : ApplicationTarget.Account,
                    string.Equals(o.Number.Trim(), referencia, StringComparison.Ordinal)));
        // Feature 015, R2 (fase 38; pedido del dueño): la referencia de pago estable con su dígito —la registrada en RCD_PaymentReferences o, si el dígito corresponde, la
        // del producto cuyo número es su base— también es una coincidencia exacta.
        if (criterios.Contains(IdentificationCriterion.PaymentReference) && !string.IsNullOrWhiteSpace(referencia))
            porReferencia.AddRange(await PorReferenciaDePagoAsync(referencia, porReferencia, hoy, ct));

        var consignantes = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(linea.DepositorTaxId)
            && (criterios.Contains(IdentificationCriterion.DepositorTaxId) || criterios.Contains(IdentificationCriterion.ExactDueAmount)))
        {
            var documento = linea.DepositorTaxId.Trim();
            consignantes = await db.People.AsNoTracking().Where(p => p.TaxId == documento).Select(p => p.PublicId).Take(2).ToListAsync(ct);
        }

        var reportadas = new List<CandidatoReportado>();
        if (criterios.Contains(IdentificationCriterion.ReportedDeposit))
            reportadas = await (from d in db.ReportedDeposits.AsNoTracking()
                                join p in db.People.AsNoTracking() on d.PersonId equals p.Id
                                where d.Status == ReportedDepositStatus.ToVerify && d.OwnBankAccountPublicId == linea.OwnBankAccountPublicId
                                select new CandidatoReportado(d.Id, p.PublicId, d.ProductPublicId, d.Target, d.Amount, d.DepositDate, d.Reference)).ToListAsync(ct);

        var exigibles = new List<CandidatoExigible>();
        if (criterios.Contains(IdentificationCriterion.ExactDueAmount) && consignantes.Count == 1)
        {
            var r = await obligaciones.ListarAsync(new SolicitudDeListado(ClavesDeRecaudo.Recaudador, consignantes[0], hoy), ct);
            exigibles.AddRange((r.Value?.Obligations ?? []).Where(o => o.DueToday > 0m)
                .Select(o => new CandidatoExigible(consignantes[0], o.ObligationPublicId, ApplicationTarget.Loan, o.DueToday)));
        }

        var ventana = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.ConsignacionReportadaDiasDeVentana, hoy, ct) ?? 0;
        var permitido = await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.IdentificacionAplicarAlCargar, hoy, ct);
        var abono = new AbonoAIdentificar(linea.Amount - linea.Identified, linea.MovementDate, referencia, porReferencia, consignantes, reportadas, exigibles, ventana);
        var propuesta = MotorDeIdentificacion.Proponer(abono, reglas.Select(x => new ReglaDeIdentificacion(x.Id, x.Criterion, x.Priority, x.ApplyOnLoad)).ToList(), permitido);
        return (propuesta, propuesta is null ? null : reglas.First(x => x.Id == propuesta.ReglaId));
    }

    /// <summary>Los candidatos por la referencia de pago con su dígito (R2): la registrada, o la base del dígito válido buscada como número de producto.</summary>
    private async Task<IReadOnlyList<CandidatoPorReferencia>> PorReferenciaDePagoAsync(string referencia, IReadOnlyList<CandidatoPorReferencia> yaEncontrados,
        DateOnly hoy, CancellationToken ct)
    {
        var normalizada = Domain.Receipts.Convenios.DigitoDeVerificacion.Normalizar(referencia);
        var registrada = await db.PaymentReferences.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Reference == normalizada && r.Status == PaymentReferenceState.Active && r.ProductPublicId != null, ct);
        if (registrada is not null)
        {
            var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == registrada.PersonId).Select(x => x.PublicId).FirstAsync(ct);
            return yaEncontrados.Any(c => c.ProductoPublicId == registrada.ProductPublicId)
                ? []
                : [new CandidatoPorReferencia(persona, registrada.ProductPublicId!.Value, registrada.Target ?? ApplicationTarget.Loan, true)];
        }
        var algoritmo = Enum.TryParse<CheckDigitAlgorithm>(await parametros.TextoAsync(
            ParametrosDeRecaudoYTesoreria.ReferenciasDigitoDeVerificacion, hoy, ct), out var a)
            ? a
            : CheckDigitAlgorithm.None;
        if (algoritmo == CheckDigitAlgorithm.None) return [];
        var lectura = new Domain.Receipts.Convenios.EstructuraDeReferencia(0, algoritmo).Leer(normalizada);
        if (!lectura.Valida) return [];
        var lista = new List<CandidatoPorReferencia>();
        foreach (var o in await busqueda.PorNumeroAsync(new BusquedaPorNumero(lectura.Base), ct))
        {
            if (!string.Equals(Domain.Receipts.Convenios.DigitoDeVerificacion.Normalizar(o.Number), lectura.Base, StringComparison.Ordinal)) continue;
            if (yaEncontrados.Any(c => c.ProductoPublicId == o.ObligationPublicId)) continue;
            lista.Add(new CandidatoPorReferencia(o.PersonPublicId, o.ObligationPublicId,
                o.Kind == ClaseDeObligacionEncontrada.Loan ? ApplicationTarget.Loan : ApplicationTarget.Account, true));
        }
        return lista;
    }

    public async Task<BankItemProposalDto> DtoAsync(PropuestaDeIdentificacion p, IdentificationRule regla, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(x => x.PublicId == p.PersonaPublicId, ct);
        var reportada = p.ReportadaId is { } r ? await db.ReportedDeposits.AsNoTracking().Where(x => x.Id == r).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct) : null;
        return new BankItemProposalDto(regla.PublicId, regla.Code, p.Criterio, p.PersonaPublicId, OperacionesDeCaja.Nombre(persona), p.ProductoPublicId, p.Destino, p.Valor,
            p.AplicarAlCargar, reportada, p.Explicacion);
    }
}

// ------------------------------------------------------------------------------------------------------------------ reglas --

/// <summary><c>GET /api/receipts/identification-rules</c> (<c>Receipts.Setup.View</c>) (nuevo).</summary>
public sealed record ListIdentificationRulesQuery : IRequest<Result<IReadOnlyList<IdentificationRuleDto>>>;

public sealed class ListIdentificationRulesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListIdentificationRulesQuery, Result<IReadOnlyList<IdentificationRuleDto>>>
{
    public async Task<Result<IReadOnlyList<IdentificationRuleDto>>> Handle(ListIdentificationRulesQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<IdentificationRuleDto>>(await db.IdentificationRules.AsNoTracking().OrderBy(r => r.Priority).ThenBy(r => r.Code)
            .Select(r => new IdentificationRuleDto(r.PublicId, r.Code, r.Name, r.Criterion, r.Priority, r.ApplyOnLoad, r.IsActive)).ToListAsync(ct));
}

/// <summary>
/// <c>POST /api/receipts/identification-rules</c> y <c>PUT …/{id}</c> (<c>Receipts.Setup.Manage</c>) (nuevo): crea o edita una regla. El código es la nomenclatura de la
/// cooperativa (<c>CodigoDeCatalogo</c>); sólo la de referencia de pago puede aplicar al cargar.
/// </summary>
public sealed record SaveIdentificationRuleCommand(Guid? PublicId, string Code, string Name, IdentificationCriterion Criterion, int Priority, bool ApplyOnLoad,
    bool IsActive = true) : IRequest<Result<IdentificationRuleDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveIdentificationRuleCommandValidator : AbstractValidator<SaveIdentificationRuleCommand>
{
    public SaveIdentificationRuleCommandValidator()
    {
        ReglasDeCodigo.Codigo(RuleFor(x => x.Code));
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre.").MaximumLength(120);
        RuleFor(x => x.Criterion).IsInEnum();
        RuleFor(x => x.Priority).GreaterThan(0);
    }
}

public sealed class SaveIdentificationRuleCommandHandler(IApplicationDbContext db, AuditoriaDeRecaudo auditoria)
    : IRequestHandler<SaveIdentificationRuleCommand, Result<IdentificationRuleDto>>
{
    public async Task<Result<IdentificationRuleDto>> Handle(SaveIdentificationRuleCommand request, CancellationToken ct)
    {
        if (request.ApplyOnLoad && request.Criterion != IdentificationCriterion.PaymentReference)
            return Result.Failure<IdentificationRuleDto>(ErroresDeIdentificacion.AplicarSoloConReferencia());
        var codigo = IngenIA365ERP.Application.Common.Catalogos.CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.IdentificationRules.FirstOrDefaultAsync(r => r.Code == codigo && r.PublicId != request.PublicId, ct);
        if (existente is not null)
            return Result.Failure<IdentificationRuleDto>(IngenIA365ERP.Application.Common.Catalogos.CodigoDeCatalogo.Duplicado("una regla de identificación", codigo, existente.Name, existente.PublicId));
        IdentificationRule regla;
        if (request.PublicId is { } id)
        {
            var seguida = await db.IdentificationRules.FirstOrDefaultAsync(r => r.PublicId == id, ct);
            if (seguida is null) return Result.Failure<IdentificationRuleDto>(ErroresDeRecaudo.NoEncontrado("La regla de identificación"));
            regla = seguida;
        }
        else
        {
            regla = new IdentificationRule();
            db.IdentificationRules.Add(regla);
        }
        (regla.Code, regla.Name, regla.Criterion, regla.Priority, regla.ApplyOnLoad, regla.IsActive) =
            (codigo, request.Name.Trim(), request.Criterion, request.Priority, request.ApplyOnLoad, request.IsActive);
        await auditoria.AgregarAsync("Receipts.IdentificationRule.Saved", "IdentificationRule", regla.PublicId, null,
            new { code = codigo, criterion = request.Criterion.ToString(), request.Priority, request.ApplyOnLoad, request.IsActive }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new IdentificationRuleDto(regla.PublicId, regla.Code, regla.Name, regla.Criterion, regla.Priority, regla.ApplyOnLoad, regla.IsActive));
    }
}

// ------------------------------------------------------------------------------------------------------------ propuestas --

/// <summary><c>GET /api/receipts/bank-items/{lineId}/proposal</c> (<c>Receipts.BankItems.View</c>): la propuesta de destino del abono, o nada. (nuevo)</summary>
public sealed record GetBankItemProposalQuery(Guid LinePublicId) : IRequest<Result<BankItemProposalDto?>>;

public sealed class GetBankItemProposalQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IMovimientosBancariosParaRecaudo movimientos,
    PropuestasDeIdentificacion propuestas, IDateTimeService reloj) : IRequestHandler<GetBankItemProposalQuery, Result<BankItemProposalDto?>>
{
    public async Task<Result<BankItemProposalDto?>> Handle(GetBankItemProposalQuery request, CancellationToken ct)
    {
        var visto = await AbonosDeRecaudo.VisibleAsync(db, alcance, movimientos, request.LinePublicId, ct, admitirDuplicado: true);
        if (visto.IsFailure) return Result.Failure<BankItemProposalDto?>(visto.Error);
        var (p, regla) = await propuestas.ProponerAsync(visto.Value.Linea, reloj.HoyLocal, ct);
        return Result.Success(p is null || regla is null ? null : await propuestas.DtoAsync(p, regla, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/bank-items/apply-on-load</c> (<c>Receipts.BankItems.Identify</c>) (nuevo; FR-067, D15): lo que la pantalla de extractos pide <b>enseguida</b> de
/// cargar uno, como la misma persona: si <c>Identificacion.AplicarAlCargar</c> está encendido, aplica cada abono pendiente del extracto que una regla marcada propone con la
/// referencia exacta, con la aplicación no interactiva y la fecha valor del movimiento (por la v2 si el contrato la adoptó). Va en su propia transacción, después de la
/// carga, para respetar el orden de cerrojos Recaudo → Tesorería. Repetir no aplica dos veces: cada abono lleva su clave.
/// </summary>
public sealed record ApplyIdentificationRulesOnLoadCommand(Guid OwnBankAccountPublicId, string StatementNumber) : IRequest<Result<AplicacionAlCargarDto>>,
    IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyIdentificationRulesOnLoadCommandValidator : AbstractValidator<ApplyIdentificationRulesOnLoadCommand>
{
    public ApplyIdentificationRulesOnLoadCommandValidator()
    {
        RuleFor(x => x.OwnBankAccountPublicId).NotEmpty();
        RuleFor(x => x.StatementNumber).NotEmpty().MaximumLength(30);
    }
}

public sealed class ApplyIdentificationRulesOnLoadCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IMovimientosBancariosParaRecaudo movimientos,
    PropuestasDeIdentificacion propuestas,
    ParametrosDeCaja parametros,
    AplicacionNoInteractiva aplicacion,
    PartidasPorIdentificar partidas,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<ApplyIdentificationRulesOnLoadCommand, Result<AplicacionAlCargarDto>>
{
    public async Task<Result<AplicacionAlCargarDto>> Handle(ApplyIdentificationRulesOnLoadCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AplicacionAlCargarDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;
        if (!await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.IdentificacionAplicarAlCargar, hoy, ct))
            return Result.Success(new AplicacionAlCargarDto(0, 0, 0, 0, Enabled: false));
        var pagina = await movimientos.AbonosPorIdentificarAsync(new FiltroDeAbonos(request.OwnBankAccountPublicId, null, null, null, 1, 5000, SoloPendientes: true), ct);
        var lineas = pagina.Items.Where(l => l.StatementNumber == request.StatementNumber && !l.PossibleDuplicate && l.Amount - l.Identified > 0m).ToList();
        int propuestasHechas = 0, aplicadas = 0, rechazadas = 0;
        foreach (var l in lineas)
        {
            var visto = await AbonosDeRecaudo.VisibleAsync(db, alcance, movimientos, l.PublicId, ct);
            if (visto.IsFailure) continue;
            var (linea, oficina) = visto.Value;
            var (p, _) = await propuestas.ProponerAsync(linea, hoy, ct);
            if (p is null) continue;
            propuestasHechas++;
            if (!p.AplicarAlCargar || p.ProductoPublicId is null || p.Destino is null) continue;
            var corrida = await CorridasNoInteractivas.AbrirAsync(db, ProcessingRunKind.BankItem, linea.PublicId, $"{linea.StatementNumber}·{linea.MovementDate:yyyyMMdd}",
                null, usuario, reloj.UtcNow, ct);
            await db.SaveChangesAsync(ct);
            var secuencia = await AbonosDeRecaudo.SiguienteSecuenciaAsync(db, linea.PublicId, ct);
            var pedido = new PedidoDeAbono(linea.PublicId, secuencia, p.PersonaPublicId,
                [new ParteDeAbono(p.Destino.Value, p.PersonaPublicId, p.Valor, p.ProductoPublicId)], false, PaymentMeansClass.Transfer,
                "Aplicado al cargar el extracto por la regla de referencia exacta", p.ReglaId);
            var r = await AbonosDeRecaudo.ProcesarAsync(db, aplicacion, partidas, movimientos, auditoria, corrida, linea, oficina, pedido, usuario, reloj, ct);
            if (r.IsSuccess && r.Value.Identifications.Count > 0) aplicadas++;
            else rechazadas++;
        }
        await auditoria.AgregarAsync("Receipts.BankItems.AppliedOnLoad", "BankStatement", request.OwnBankAccountPublicId, null,
            new { statement = request.StatementNumber, lines = lineas.Count, proposed = propuestasHechas, applied = aplicadas, rejected = rechazadas, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new AplicacionAlCargarDto(lineas.Count, propuestasHechas, aplicadas, rechazadas, Enabled: true));
    }
}

// --------------------------------------------------------------------------------------------------- consignación reportada --

/// <summary><c>GET /api/receipts/reported-deposits?state=</c> (<c>Receipts.BankItems.View</c> o <c>Receipts.Receipt.Register</c>) (nuevo).</summary>
public sealed record ListReportedDepositsQuery(ReportedDepositStatus? State = null) : IRequest<Result<IReadOnlyList<ReportedDepositDto>>>;

public sealed class ListReportedDepositsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, ParametrosDeCaja parametros, IDateTimeService reloj)
    : IRequestHandler<ListReportedDepositsQuery, Result<IReadOnlyList<ReportedDepositDto>>>
{
    public async Task<Result<IReadOnlyList<ReportedDepositDto>>> Handle(ListReportedDepositsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var filas = (await db.ReportedDeposits.AsNoTracking().OrderByDescending(d => d.Id).Take(500).ToListAsync(ct)).Where(d => mio.IncluyeOficina(d.BranchId)).ToList();
        var lista = await ConsignacionesReportadas.DtosAsync(db, parametros, filas, reloj.HoyLocal, ct);
        return Result.Success<IReadOnlyList<ReportedDepositDto>>(request.State is { } e ? lista.Where(d => d.Status == e).ToList() : lista);
    }
}

/// <summary>Lo común de la consignación reportada. (nuevo)</summary>
public static class ConsignacionesReportadas
{
    public const string ClaseDeNumeracion = "CRP";

    /// <summary>¿Lleva más de <c>ConsignacionReportada.DiasParaVerificar</c> días sin aparecer? (se muestra vencida; se puede verificar igual).</summary>
    public static bool Vencida(ReportedDeposit d, int? dias, DateOnly hoy) =>
        d.Status == ReportedDepositStatus.ToVerify && dias is > 0 && hoy.DayNumber - d.DepositDate.DayNumber > dias.Value;

    public static async Task<IReadOnlyList<ReportedDepositDto>> DtosAsync(IApplicationDbContext db, ParametrosDeCaja parametros, IReadOnlyList<ReportedDeposit> filas,
        DateOnly hoy, CancellationToken ct)
    {
        var dias = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.ConsignacionReportadaDiasParaVerificar, hoy, ct);
        var personas = filas.Select(f => f.PersonId).Distinct().ToList();
        var porPersona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var recibos = filas.Where(f => f.ReceiptId is not null).Select(f => f.ReceiptId!.Value).Distinct().ToList();
        var numeros = await db.IssuedReceipts.AsNoTracking().Where(r => recibos.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Number, ct);
        return filas.Select(f => new ReportedDepositDto(f.PublicId, f.Number, porPersona[f.PersonId].PublicId, OperacionesDeCaja.Nombre(porPersona[f.PersonId]),
            f.OwnBankAccountPublicId, f.DepositDate, f.Amount, f.Reference, f.Target, f.ProductPublicId, f.AppliedImmediately,
            Vencida(f, dias, hoy) ? ReportedDepositStatus.Expired : f.Status, f.ReceiptId is { } r ? numeros.GetValueOrDefault(r) : null, f.StatementLinePublicId,
            f.RegisteredAt)).ToList();
    }
}

/// <summary>
/// <c>POST /api/receipts/reported-deposits</c> (<c>Receipts.Receipt.Register</c>) (nuevo; FR-069, D11): el asociado trae el soporte de su consignación. Con
/// <c>ConsignacionReportada.Modo = PorVerificar</c> (el defecto) queda por verificar hasta encontrarla en el extracto; con <c>AplicarDeInmediato</c> se aplica ya, en forma
/// no interactiva con medio «consignación» y la fecha de la consignación, y queda pendiente de cruzar.
/// </summary>
public sealed record RegisterReportedDepositCommand(
    Guid PersonPublicId,
    Guid OwnBankAccountPublicId,
    DateOnly DepositDate,
    decimal Amount,
    Guid SupportAttachmentPublicId,
    ApplicationTarget Target,
    Guid ProductPublicId,
    string? Reference = null) : IRequest<Result<ReportedDepositDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterReportedDepositCommandValidator : AbstractValidator<RegisterReportedDepositCommand>
{
    public RegisterReportedDepositCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.OwnBankAccountPublicId).NotEmpty();
        RuleFor(x => x.SupportAttachmentPublicId).NotEmpty().WithMessage("La consignación reportada lleva su soporte.");
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Target).Must(t => t is ApplicationTarget.Loan or ApplicationTarget.Account).WithMessage("La consignación va a un crédito o a una cuenta.");
        RuleFor(x => x.Reference).MaximumLength(100);
    }
}

public sealed class RegisterReportedDepositCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICuentasBancariasPropias cuentasPropias,
    NumeradorDeRecaudo numerador,
    ICerrojoDeRecaudo cerrojo,
    ParametrosDeCaja parametros,
    AplicacionNoInteractiva aplicacion,
    AuditoriaDeRecaudo auditoria) : IRequestHandler<RegisterReportedDepositCommand, Result<ReportedDepositDto>>
{
    public async Task<Result<ReportedDepositDto>> Handle(RegisterReportedDepositCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReportedDepositDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var clave = request.OperationKey == Guid.Empty ? Guid.NewGuid() : request.OperationKey;
        var llave = string.Create(CultureInfo.InvariantCulture, $"{ClavesDeRecaudo.Codigo}:consignacion:{clave:N}");
        var hoy = reloj.HoyLocal;
        if (await db.ReportedDeposits.AsNoTracking().FirstOrDefaultAsync(d => d.IdempotencyKey == llave, ct) is { } ya)
            return Result.Success((await ConsignacionesReportadas.DtosAsync(db, parametros, [ya], hoy, ct))[0]);
        if (request.DepositDate > hoy) return Result.Failure<ReportedDepositDto>(ErroresDeRecaudo.MovimientoInvalido("La fecha de la consignación no puede ser futura."));
        var cuenta = await cuentasPropias.PorPublicIdAsync(request.OwnBankAccountPublicId, ct);
        if (cuenta is null || !cuenta.IsCollection || !cuenta.IsActive || cuenta.BranchPublicId is null)
            return Result.Failure<ReportedDepositDto>(ErroresDeIdentificacion.CuentaNoDeRecaudo());
        var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == cuenta.BranchPublicId).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (oficina is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(oficina.Value))
            return Result.Failure<ReportedDepositDto>(ErroresDeRecaudo.NoEncontrado("La cuenta bancaria propia"));
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<ReportedDepositDto>(ErroresDeRecaudo.NoEncontrado("La persona"));

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearPagadoresAsync([persona.Value], ct);
        var deposito = new ReportedDeposit
        {
            Number = await numerador.AsignarAsync(ConsignacionesReportadas.ClaseDeNumeracion, ConsignacionesReportadas.ClaseDeNumeracion, ct), PersonId = persona.Value,
            BranchId = oficina.Value, RegisteredByUserId = usuario, OwnBankAccountPublicId = request.OwnBankAccountPublicId, DepositDate = request.DepositDate,
            Amount = request.Amount, Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            SupportAttachmentPublicId = request.SupportAttachmentPublicId, Target = request.Target, ProductPublicId = request.ProductPublicId, RegisteredAt = reloj.UtcNow,
            IdempotencyKey = llave,
        };
        var inmediata = string.Equals(await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.ConsignacionReportadaModo, hoy, ct), "AplicarDeInmediato",
            StringComparison.OrdinalIgnoreCase);
        if (inmediata)
        {
            var r = await aplicacion.AplicarAsync(new LineaNoInteractiva(llave, ReceiptChannel.Bank, request.PersonPublicId, request.DepositDate, request.Amount, oficina.Value,
                [new ParteNoInteractiva(request.Target, request.ProductPublicId, request.PersonPublicId, request.Amount)], ClaseDeMedioDeRecaudo.BankDeposit,
                PaymentMeansClass.BankDeposit, request.OwnBankAccountPublicId, deposito.Reference ?? deposito.Number, CerrojosTomados: true), ct);
            if (r.Outcome is not (ProcessingLineOutcome.Applied or ProcessingLineOutcome.AlreadyApplied) || r.Receipt is null)
                return Result.Failure<ReportedDepositDto>(ErroresDeIdentificacion.Rechazada(r.Code ?? "Receipts.Receipt.ContractUnavailable", r.Message ?? "El módulo no respondió."));
            deposito.ReceiptId = r.Receipt.Id;
            deposito.AppliedImmediately = true;
        }
        db.ReportedDeposits.Add(deposito);
        await auditoria.AgregarAsync("Receipts.ReportedDeposit.Registered", "ReportedDeposit", deposito.PublicId, null,
            new { number = deposito.Number, amount = deposito.Amount, date = deposito.DepositDate, immediately = inmediata, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success((await ConsignacionesReportadas.DtosAsync(db, parametros, [deposito], hoy, ct))[0]);
    }
}

/// <summary>
/// <c>POST /api/receipts/reported-deposits/{id}/verify</c> (<c>Receipts.BankItems.Identify</c>) (nuevo; FR-069): la cruza con la línea del extracto que la confirma. Si se
/// aplicó de inmediato, la línea queda identificada con el mismo recibo (no se aplica otra vez); si estaba por verificar, se aplica ahora como cualquier abono identificado,
/// con la fecha valor del movimiento (por la v2 si el contrato la adoptó, FR-071).
/// </summary>
public sealed record VerifyReportedDepositCommand(Guid DepositPublicId, Guid LinePublicId) : IRequest<Result<ReportedDepositDto>>, IOperacionIdempotente,
    IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class VerifyReportedDepositCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IMovimientosBancariosParaRecaudo movimientos,
    ParametrosDeCaja parametros,
    AplicacionNoInteractiva aplicacion,
    PartidasPorIdentificar partidas,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<VerifyReportedDepositCommand, Result<ReportedDepositDto>>
{
    public async Task<Result<ReportedDepositDto>> Handle(VerifyReportedDepositCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReportedDepositDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;
        var deposito = await db.ReportedDeposits.FirstOrDefaultAsync(d => d.PublicId == request.DepositPublicId, ct);
        if (deposito is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(deposito.BranchId))
            return Result.Failure<ReportedDepositDto>(ErroresDeRecaudo.NoEncontrado("La consignación reportada"));
        if (deposito.Status == ReportedDepositStatus.Verified && deposito.StatementLinePublicId == request.LinePublicId)
            return Result.Success((await ConsignacionesReportadas.DtosAsync(db, parametros, [deposito], hoy, ct))[0]);
        if (deposito.Status != ReportedDepositStatus.ToVerify) return Result.Failure<ReportedDepositDto>(ErroresDeIdentificacion.NoPorVerificar(deposito.Status));
        var visto = await AbonosDeRecaudo.VisibleAsync(db, alcance, movimientos, request.LinePublicId, ct);
        if (visto.IsFailure) return Result.Failure<ReportedDepositDto>(visto.Error);
        var (linea, oficina) = visto.Value;
        var pendiente = linea.Amount - linea.Identified;
        if (pendiente < deposito.Amount || linea.OwnBankAccountPublicId != deposito.OwnBankAccountPublicId)
            return Result.Failure<ReportedDepositDto>(ErroresDeIdentificacion.NoCuadra(pendiente, deposito.Amount));
        var persona = await db.People.AsNoTracking().Where(p => p.Id == deposito.PersonId).Select(p => p.PublicId).FirstAsync(ct);
        var ahora = reloj.UtcNow;

        if (deposito.ReceiptId is { } recibo)
        {
            // Ya aplicada al registrarla: la línea queda identificada con ese mismo recibo.
            db.BankItemIdentifications.Add(new BankItemIdentification
            {
                BankStatementLinePublicId = linea.PublicId, Sequence = await AbonosDeRecaudo.SiguienteSecuenciaAsync(db, linea.PublicId, ct),
                OwnBankAccountPublicId = linea.OwnBankAccountPublicId, MovementDate = linea.MovementDate, Amount = deposito.Amount, Outcome = BankItemOutcome.Receipt,
                ReceiptId = recibo, IdentifiedByUserId = usuario, IdentifiedAt = ahora, Note = $"Consignación reportada {deposito.Number}",
            });
            await movimientos.MarcarIdentificacionAsync(linea.PublicId, linea.Identified + deposito.Amount, ct);
        }
        else
        {
            var corrida = await CorridasNoInteractivas.AbrirAsync(db, ProcessingRunKind.BankItem, linea.PublicId, $"{linea.StatementNumber}·{linea.MovementDate:yyyyMMdd}",
                null, usuario, ahora, ct);
            await db.SaveChangesAsync(ct);
            var pedido = new PedidoDeAbono(linea.PublicId, await AbonosDeRecaudo.SiguienteSecuenciaAsync(db, linea.PublicId, ct), persona,
                [new ParteDeAbono(deposito.Target, persona, deposito.Amount, deposito.ProductPublicId)], false, PaymentMeansClass.BankDeposit,
                $"Consignación reportada {deposito.Number}");
            var r = await AbonosDeRecaudo.ProcesarAsync(db, aplicacion, partidas, movimientos, auditoria, corrida, linea, oficina, pedido, usuario, reloj, ct);
            if (r.IsFailure) return Result.Failure<ReportedDepositDto>(r.Error);
            var identificacion = r.Value.Identifications.FirstOrDefault();
            if (identificacion?.ReceiptNumber is null)
                return Result.Failure<ReportedDepositDto>(ErroresDeIdentificacion.Rechazada("Receipts.ReportedDeposit.LineRejected",
                    "la línea quedó rechazada en la corrida; reprocésela desde el extracto."));
            deposito.ReceiptId = await db.IssuedReceipts.AsNoTracking().Where(x => x.Number == identificacion.ReceiptNumber).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
        }
        deposito.Status = ReportedDepositStatus.Verified;
        deposito.StatementLinePublicId = linea.PublicId;
        deposito.VerifiedAt = ahora;
        deposito.VerifiedByUserId = usuario;
        await auditoria.AgregarAsync("Receipts.ReportedDeposit.Verified", "ReportedDeposit", deposito.PublicId, null,
            new { number = deposito.Number, line = linea.PublicId, statement = linea.StatementNumber, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success((await ConsignacionesReportadas.DtosAsync(db, parametros, [deposito], hoy, ct))[0]);
    }
}
