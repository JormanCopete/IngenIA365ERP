using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.WriteOffs;

/// <summary>
/// La decisión de un castigo que guarda la operación <c>WriteOff</c> en su <c>Payload</c> (feature 013, K3, US14, T0908): el lote y lo que salió del
/// activo, contra el deterioro y del suspenso a control. La reconstrucción no la lee: el castigo no cambia lo que el deudor debe. (nuevo)
/// </summary>
public sealed record DecisionDeCastigo(
    Guid WriteOffBatchPublicId,
    string WriteOffBatchNumber,
    Guid SessionItemPublicId,
    IReadOnlyList<ConceptAmountV1> Amounts,
    IReadOnlyList<ConceptAmountV1> ImpairmentApplied,
    IReadOnlyList<ConceptAmountV1> SuspenseWrittenOff)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    public static DecisionDeCastigo? Leer(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try
        {
            return JsonSerializer.Deserialize<DecisionDeCastigo>(payload, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Aplica un lote de castigo aprobado por el consejo (feature 013, K3, US14, T0908; api.md §11.3, <c>POST /api/lending/write-off-batches/{id}/apply</c>,
/// <c>Lending.WriteOffs.Apply</c>, con clave; FR-095, FR-096, US14-2; mensajes.md §10.22):
/// <list type="bullet">
/// <item>sin la aprobación del consejo, <c>Lending.WriteOff.ApprovalRequired</c>; un punto que no es el que lo aprobó (cerrado, que decide, aprobatorio,
/// sobre este lote), <c>Lending.Session.ItemMismatch</c>; quien armó el lote no lo aplica si el par <c>ArmarLoteDeCastigo × AplicarCastigo</c> está
/// activo (<c>Lending.Segregation.Conflict</c>);</item>
/// <item>en una transacción, con los créditos bloqueados: los requisitos se <b>vuelven a verificar</b> a hoy; el que ya no cumple (o ya no está vigente)
/// se <b>excluye</b> con su motivo —ítem <c>Rejected</c>, evento <c>Lending.WriteOffItem.Excluded</c>, <c>excluded[]</c> en la respuesta— y el resto se
/// castiga; si no queda ninguno, el lote no se aplica (<c>Lending.WriteOff.NoLoansLeft</c> con <c>data.excluded[]</c>) (bloque D, decisión del dueño
/// del 2026-10-15); y la validación previa contable de pantalla de todos los <c>CreditoCastigado</c> (A8);</item>
/// <item>por crédito: la puesta al día hasta hoy, la operación <c>WriteOff</c> por <see cref="RegistroDeCartera"/> (sin filas propias: el castigo no
/// cambia lo que se debe; la causación pasa a suspenso desde mañana por la fecha del castigo), el crédito <c>WrittenOff</c> con <c>WrittenOffOn</c>, el
/// ítem castigado, <c>CreditoCastigado</c> en línea (<c>PV</c>) con lo que sale contra el deterioro individual del último cierre
/// (<c>impairmentApplied</c>), el faltante al gasto (<c>impairmentShortfall</c>) y el suspenso a control (<c>suspenseWrittenOff</c>, C81) y, si el
/// crédito ya estaba suspendido, <c>CausacionSuspendida</c> con causa <c>WriteOff</c> (si causaba normal, la emite el ciclo de mañana);</item>
/// <item>el lote <c>Applied</c> con quién y cuándo, el conteo y los totales de lo castigado, y del perfil regulatorio vigente si el tipo de entidad debe reportar los castigos al supervisor y su
/// plazo (documento <c>TiposDeOrganizacion</c>); si debe, la alerta <c>Cartera.PlazoRegulatorioPorVencer</c>.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record ApplyWriteOffBatchCommand(Guid WriteOffBatchPublicId, Guid SessionItemPublicId, bool AcknowledgeWarnings = false)
    : IRequest<Result<ApplyWriteOffBatchResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyWriteOffBatchCommandValidator : AbstractValidator<ApplyWriteOffBatchCommand>
{
    public ApplyWriteOffBatchCommandValidator()
    {
        RuleFor(x => x.WriteOffBatchPublicId).NotEmpty();
        RuleFor(x => x.SessionItemPublicId).NotEmpty().WithMessage("Diga el punto del acta del consejo que aprobó el lote.");
    }
}

public sealed class ApplyWriteOffBatchCommandHandler(
    IApplicationDbContext db,
    CastigosDeCartera castigos,
    PuntoDeActaCerrado puntoDeActa,
    PuestaAlDiaDeCredito puestaAlDia,
    PartesDeHechosDeCartera partes,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    RegistroDeIntervenciones intervenciones,
    EmisionDeCartera emision,
    ValidacionPreviaContableDeCartera validacionPrevia,
    FechaContable fechaContable,
    IPerfilRegulatorio perfiles,
    INormativaVigente normativa,
    IAlertas alertas,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ApplyWriteOffBatchCommand, Result<ApplyWriteOffBatchResultDto>>
{
    /// <summary>La clave de la alerta del plazo del reporte de un lote (la atiende el registro del reporte).</summary>
    public static string ClaveDeAlerta(WriteOffBatch lote) =>
        $"{TiposDeAlerta.PlazoRegulatorioPorVencer}:WriteOffBatch:{lote.Number}:{lote.SupervisorReportDueDate:yyyy-MM-dd}";

    public async Task<Result<ApplyWriteOffBatchResultDto>> Handle(ApplyWriteOffBatchCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leido = await CastigosDeCartera.LeerAsync(db, alcance, auditoria, request.WriteOffBatchPublicId, seguir: true, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var (lote, items) = leido.Value;
        if (lote.PuedeIrA(WriteOffBatchStatus.Applied) is { } sinAprobar) return Falla(ErroresDeGobierno.De(sinAprobar));
        var punto = await puntoDeActa.ValidarAsync(request.SessionItemPublicId, LendingDecisionSubjects.WriteOffBatch, lote.PublicId, ct);
        if (punto.IsFailure) return Falla(punto.Error);
        if (punto.Value.ItemId != lote.SessionItemId)
            return Falla(ErroresDeGobierno.PuntoNoCorresponde(LendingDecisionSubjects.WriteOffBatch, LendingDecisionSubjects.WriteOffBatch,
                punto.Value.Outcome.ToString()));
        var v = reloj.HoyLocal;
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(CastigosDeCartera.Expediente, lote.PublicId),
            AccionesDeSegregacion.AplicarCastigo, v, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        var incluidos = items.Where(i => i.Status == WriteOffItemStatus.Included).OrderBy(i => i.LoanId).ToList();
        var ids = incluidos.Select(i => i.LoanId).ToList();

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var creditos = await db.Loans.Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(creditos.Values.Select(c => c.DebtorPersonId).Distinct().ToList(), ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync(ids, ModoDeEspera.Esperar, ct);
            // Los requisitos se vuelven a verificar a hoy con los soportes con que entró cada crédito. Bloque D (decisión del dueño del 2026-10-15): el
            // que ya no está vigente o ya no cumple se excluye con su motivo (ítem rechazado, auditado) y el resto se castiga; si no queda ninguno, el
            // lote no se aplica.
            var excluidos = new List<WriteOffExclusionDto>();
            foreach (var item in incluidos.Where(i => creditos[i.LoanId].Status != LoanStatus.Active).OrderBy(i => creditos[i.LoanId].Number, StringComparer.Ordinal))
            {
                var c = creditos[item.LoanId];
                var motivo = $"Al aplicar el lote el crédito ya no está vigente ({c.Status}): se excluye del castigo.";
                item.Excluir(motivo);
                excluidos.Add(new WriteOffExclusionDto(c.PublicId, c.Number, [], motivo));
            }
            var vigentes = incluidos.Where(i => creditos[i.LoanId].Status == LoanStatus.Active).ToList();
            var soportes = vigentes.ToDictionary(i => creditos[i.LoanId].PublicId, i => CastigosDeCartera.SoportesDe(i, creditos[i.LoanId].PublicId));
            var verificados = vigentes.Count == 0
                ? Result.Success<IReadOnlyList<CreditoVerificadoParaCastigo>>([])
                : await castigos.VerificarAsync(vigentes.Select(i => creditos[i.LoanId]).ToList(), soportes, v, ct);
            if (verificados.IsFailure) return Falla(verificados.Error);
            foreach (var noCumple in verificados.Value.Where(x => !x.Verificacion.Cumple).OrderBy(x => x.Credito.Number, StringComparer.Ordinal))
            {
                var motivo = $"Al aplicar el lote ya no cumple: falta {string.Join(", ", noCumple.Verificacion.Faltantes.Select(ErroresDeCastigo.Requisito))}.";
                vigentes.First(i => i.LoanId == noCumple.Credito.Id).Excluir(motivo, noCumple.Verificacion.RequisitosJson);
                excluidos.Add(new WriteOffExclusionDto(noCumple.Credito.PublicId, noCumple.Credito.Number, noCumple.Verificacion.Faltantes, motivo));
            }
            if (excluidos.Count == incluidos.Count) return Falla(ErroresDeCastigo.NingunoCumple(lote.Number, excluidos));
            var cumplen = verificados.Value.Where(x => x.Verificacion.Cumple).ToList();
            var porCredito = cumplen.ToDictionary(x => x.Credito.Id);
            var aCastigarItems = incluidos.Where(i => porCredito.ContainsKey(i.LoanId)).ToList();
            var idsACastigar = aCastigarItems.Select(i => i.LoanId).ToList();

            // Lo que sale de cada crédito y la validación previa contable de pantalla de todos los castigos (A8).
            var contable = await fechaContable.CalcularAsync(v, ct);
            if (contable.IsFailure) return Falla(contable.Error);
            var datos = await partes.DatosAsync(cumplen.Select(x => x.Cargado), ct);
            var categorias = await new CategoriaContableDeCartera(db).VigentesAsync(idsACastigar, contable.Value, ct);
            var ahora = reloj.UtcNow;
            var aCastigar = new List<(WriteOffBatchItem Item, CreditoVerificadoParaCastigo Credito, HechoDeCredito Hecho, IReadOnlyList<ConceptAmountV1> Castigado,
                IReadOnlyList<ConceptAmountV1> Deterioro, IReadOnlyList<ConceptAmountV1> Suspenso)>();
            var previos = new List<MensajeContableDto>();
            foreach (var item in aCastigarItems)
            {
                var c = porCredito[item.LoanId];
                var hecho = datos[item.LoanId].Hecho(contable.Value);
                var castigado = PorConcepto(c.Estado, CastigosDeCartera.DelActivo, EmisionDeCartera.MotivoCastigo);
                var suspenso = PorConcepto(c.Estado, CastigosDeCartera.DeSuspenso, null);
                var deterioro = await castigos.DeterioroAplicableAsync(item.LoanId, castigado, v, ct);
                aCastigar.Add((item, c, hecho, castigado, deterioro, suspenso));
                previos.Add(new MensajeContableDto(new IntegrationEnvelopeV1
                {
                    MessageId = Guid.Empty, Type = CreditoCastigadoV1.Type, Kind = IntegrationMessageKind.Business, OriginModule = ModulosDeOrigen.Cartera,
                    OriginEventKey = ClavesDeEvento.Confirmacion, BranchPublicId = hecho.SucursalPublicId,
                }, EmisionDeCartera.ContenidoDeCastigo(hecho, categorias[item.LoanId], v, ahora, castigado, deterioro, suspenso, lote.PublicId,
                    punto.Value.ItemPublicId)));
            }
            var decision = await validacionPrevia.DecidirAsync(previos, OrigenDeValidacionPrevia.Pantalla, $"el lote de castigo {lote.Number}", ct);
            if (decision.Bloqueo is { } bloqueo) return Falla(bloqueo);

            // La puesta al día hasta hoy (el día del castigo se causa normal) y, por crédito, la operación, el estado y el hecho.
            await puestaAlDia.PrepararAsync(idsACastigar, v, ct);
            var controles = await db.LoanControls.Where(c => idsACastigar.Contains(c.LoanId)).ToDictionaryAsync(c => c.LoanId, ct);
            var hechos = new List<(LoanOperation Operacion, WriteOffBatchItem Item, Guid Mensaje, decimal Castigado, decimal Deterioro, decimal Suspenso)>();
            foreach (var (item, c, hecho, castigado, deterioro, suspenso) in aCastigar)
            {
                var payload = new DecisionDeCastigo(lote.PublicId, lote.Number, punto.Value.ItemPublicId, castigado, deterioro, suspenso).Serializar();
                var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.WriteOff, CastigosDeCartera.PrefijoDeOperacion, ct);
                var operacion = registro.RegistrarOperacion(controles[item.LoanId], ahora, sello => new LoanOperation
                {
                    LoanId = item.LoanId,
                    Kind = LoanOperationKind.WriteOff,
                    Number = numero,
                    Sequence = sello.Sequence,
                    ValueDate = v,
                    AccountingDate = contable.Value,
                    RegisteredAt = sello.RegisteredAt,
                    Amount = castigado.Sum(a => a.Amount),
                    ActorKind = actor.Kind,
                    ActorUserId = actor.UserId,
                    OriginUserCentralId = actor.CentralUserId,
                    WriteOffBatchItemId = item.Id,
                    Reason = Recortar($"Castigo del lote {lote.Number}: {lote.Reason}"),
                    Payload = payload,
                    InputsSha256 = TramoDeDesembolso.Sha256(payload),
                    RulesVersion = c.Cargado.Vigentes.RulesVersion,
                    PrevalidationOutcome = decision.Outcome.ToString(),
                });
                var emitido = await emision.CastigoAsync(new CastigoAEmitir(hecho, operacion, castigado, deterioro, suspenso, lote.PublicId,
                    punto.Value.ItemPublicId, decision.Outcome, SuspensionPorElCastigo: c.Estado.Suspendida, c.DiasDeMora), ct);
                if (emitido.IsFailure) return Falla(emitido.Error);
                try
                {
                    item.Castigar(operacion);
                }
                catch (ReglaDeCarteraException ex)
                {
                    return Falla(ErroresDeGobierno.De(ex.Problema));
                }
                RegistroDeCartera.CambiarEstado(creditos[item.LoanId], LoanStatus.WrittenOff, v);
                hechos.Add((operacion, item, emitido.Value.PublicId, castigado.Sum(a => a.Amount), deterioro.Sum(a => a.Amount), suspenso.Sum(a => a.Amount)));
            }
            await db.SaveChangesAsync(ct);
            foreach (var h in hechos)
            {
                var puesta = await puestaAlDia.RegistrarAsync(h.Operacion, ct, [h.Item.LoanId]);
                if (puesta.IsFailure) return Falla(puesta.Error);
            }

            // El lote aplicado y, si el tipo de entidad debe reportar los castigos, su plazo.
            var (reporte, plazo) = await ReporteAlSupervisorAsync(v, ct);
            try
            {
                lote.RecalcularAlAplicar(items);
                lote.Aplicar(usuario, ahora, reporte, plazo);
            }
            catch (ReglaDeCarteraException ex)
            {
                return Falla(ErroresDeGobierno.De(ex.Problema));
            }
            intervenciones.Registrar(verificada.Value, LendingDecisionSubjects.WriteOffBatch, lote.PublicId);
            await db.SaveChangesAsync(ct);

            if (reporte)
                await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.PlazoRegulatorioPorVencer,
                    $"Reporte de castigos del lote {lote.Number} al supervisor",
                    string.Create(CultureInfo.InvariantCulture,
                        $"El lote {lote.Number} castigó {hechos.Count} crédito(s) por {ErroresDeOriginacion.Pesos(hechos.Sum(h => h.Castigado))}. ")
                    + $"El tipo de entidad debe reportarlo a la Superintendencia a más tardar el {plazo:yyyy-MM-dd}; registre el reporte en el lote con su soporte.",
                    EntityType: CastigosDeCartera.Expediente, EntityPublicId: lote.PublicId, DedupKey: ClaveDeAlerta(lote),
                    RecipientPermissions: ["Lending.WriteOffs.Apply"], ModuloDeOrigen: "Lending"), ct);

            foreach (var e in excluidos)
                await auditoria.EmitirCreditoExcluidoDelCastigoAsync(lote.PublicId, lote.Number, e.LoanNumber, e.Missing, e.Reason, ct);
            foreach (var h in hechos)
                await auditoria.EmitirOperacionRegistradaAsync(h.Operacion.PublicId, h.Operacion.Number, nameof(LoanOperationKind.WriteOff), new
                {
                    batch = lote.Number, valueDate = v, amount = h.Castigado, impairmentApplied = h.Deterioro, suspenseWrittenOff = h.Suspenso,
                }, ct);
            var operaciones = hechos.Select(h => new WriteOffOperationDto(creditos[h.Item.LoanId].PublicId, creditos[h.Item.LoanId].Number, h.Operacion.PublicId,
                h.Operacion.Number, h.Castigado, h.Deterioro, h.Castigado - h.Deterioro, h.Suspenso, h.Mensaje)).ToList();
            return Result.Success(new ApplyWriteOffBatchResultDto(operaciones, hechos.Select(h => h.Mensaje.ToString()).ToList(), lote.SupervisorReportRequired,
                lote.SupervisorReportDueDate, decision.Avisos, excluidos));
        }, ct);
    }

    /// <summary>
    /// Si el tipo de entidad del perfil regulatorio vigente debe reportar los castigos al supervisor (documento <c>TiposDeOrganizacion</c>, dato
    /// <c>writeOffReport</c>) y su plazo: hoy más los días del documento. Sin perfil, sin documento o sin el dato, no se exige.
    /// </summary>
    private async Task<(bool Requerido, DateOnly? Plazo)> ReporteAlSupervisorAsync(DateOnly v, CancellationToken ct)
    {
        var perfil = await perfiles.VigenteAsync(v, null, ct);
        if (perfil is null) return (false, null);
        var tipos = await normativa.TiposDeOrganizacionAsync(v, null, ct);
        if (tipos.IsFailure) return (false, null);
        var reporte = tipos.Value.Documento.Buscar(perfil.OrganizationTypeCode)?.WriteOffReport;
        return reporte is { Required: true, DueDays: { } dias } ? (true, v.AddDays(dias)) : (false, null);
    }

    /// <summary>Los saldos de hoy de los conceptos dados, por concepto (sin cuota), con el motivo.</summary>
    private static IReadOnlyList<ConceptAmountV1> PorConcepto(Domain.Lending.Replay.EstadoDelCredito estado, IReadOnlyList<LoanConcept> conceptos, string? motivo) =>
        conceptos.Select(c => new ConceptAmountV1 { Concept = c.ToString(), Amount = estado.Saldo(c), ReasonCode = motivo }).Where(a => a.Amount > 0m).ToList();

    private static string Recortar(string texto) => texto.Length <= 500 ? texto : texto[..500];

    private static Result<ApplyWriteOffBatchResultDto> Falla(Error e) => Result.Failure<ApplyWriteOffBatchResultDto>(e);
}
