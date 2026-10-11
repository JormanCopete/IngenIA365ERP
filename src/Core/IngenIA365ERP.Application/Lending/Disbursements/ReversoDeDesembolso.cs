using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Reversos;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>Quién reversa: el proceso (consumo del resultado del giro, con el usuario de Tesorería como origen) o una persona. (nuevo)</summary>
public sealed record ActorDelReverso(ActorKind Kind, int? UserId, Guid? OriginUserCentralId, UserRefV1? UsuarioDeOrigen);

/// <summary>Lo que quedó del reverso: la operación, los mensajes y el estado del crédito. (nuevo)</summary>
public sealed record ReversoRegistrado(LoanOperation Reverso, IReadOnlyList<IntegrationMessage> Mensajes, LoanStatus EstadoDelCredito);

/// <summary>
/// El reverso de un desembolso <b>desde su origen</b> (feature 013, US3, T0447; FR-002, FR-048; data-model §7.11; T33, C08, C49, C101), que
/// usan el consumidor del resultado del giro y la anulación. Bajo el cerrojo que ya tomó quien llama:
/// <list type="bullet">
/// <item>la operación <c>DisbursementReversal</c> con <c>ReversesOperationId</c> = el tramo y, en el tramo, <c>ReversedByOperationId</c> (escritura
/// única);</item>
/// <item>los espejos de <see cref="EspejosDelReverso"/> por <see cref="RegistroDeCartera.Espejar"/>: con un solo tramo vivo, el capital y su
/// causación (C101); con varios, sólo el capital y la diferencia de causación desde la fecha valor del tramo como <c>AccrualAdjustment</c>
/// (<see cref="IMotorDeCreditoEnDesembolso.ConciliarCausacionAsync"/>, C49);</item>
/// <item>el crédito <c>PendingDisbursement</c> si no le queda otro tramo vivo;</item>
/// <item><c>DesembolsoReversado</c> con <c>cause</c>, <c>loanStatusAfter</c> y <c>payoffReversals</c> (vacía: las recogidas se conservan mientras el
/// crédito espera el giro nuevo, C11), que hereda el modo del desembolso (C08), con su acompañante <c>AjusteDeCausacionPorFechaValor</c> si hubo
/// diferencia.</item>
/// </list>
/// Si el dinero ya salió (orden <c>Disbursed</c> y la causa no es el cheque no cobrado): <c>Lending.Reversal.DisbursementPaidOut</c>. Guarda
/// dentro de la transacción de quien llama para tener el Id del reverso. (nuevo)
/// </summary>
public sealed class ReversoDeDesembolso(
    IApplicationDbContext db,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    IMotorDeCreditoEnDesembolso motor,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    IDateTimeService reloj)
{
    public const string CausaRechazada = nameof(DisbursementOrderStatus.Rejected);
    public const string CausaChequeNoCobrado = nameof(DisbursementOrderStatus.CheckNotCashed);
    public const string CausaAnulada = nameof(DisbursementOrderStatus.Voided);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<ReversoRegistrado>> ReversarAsync(DisbursementOrder orden, string causa, Guid? mensajeDelResultado, int? recibo, string? motivo,
        ActorDelReverso actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(orden);
        ArgumentNullException.ThrowIfNull(actor);
        if (orden.Status == DisbursementOrderStatus.Disbursed && causa != CausaChequeNoCobrado)
            return Result.Failure<ReversoRegistrado>(ErroresDeDesembolso.DineroYaSalio(orden.Number));

        var desembolso = await db.LoanOperations.FirstAsync(o => o.Id == orden.DisbursementOperationId, ct);
        if (desembolso.ReversedByOperationId is not null)
            return Result.Failure<ReversoRegistrado>(new ErrorConDatos(EspejosDelReverso.CodigoReversoDeReverso,
                "Este desembolso ya está reversado: un reverso no se repite.", new { operationNumber = desembolso.Number }));
        var credito = await db.Loans.FirstAsync(l => l.Id == desembolso.LoanId, ct);
        var control = await db.LoanControls.FirstAsync(c => c.LoanId == credito.Id, ct);
        var condiciones = await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == credito.Id).OrderByDescending(t => t.VersionNumber).FirstAsync(ct);

        var libro = await db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == credito.Id).ToListAsync(ct);
        var vivos = await db.LoanOperations.AsNoTracking()
            .CountAsync(o => o.LoanId == credito.Id && o.Kind == LoanOperationKind.Disbursement && o.ReversedByOperationId == null, ct);
        PropuestaDeEspejos propuesta;
        try
        {
            propuesta = EspejosDelReverso.DelDesembolso(libro.Select(Fila).ToList(), desembolso.Id, desembolso.ValueDate, Math.Max(1, vivos));
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<ReversoRegistrado>(ErroresDeGobierno.De(ex.Problema));
        }

        var fecha = await fechaContable.CalcularAsync(reloj.HoyLocal, ct);
        if (fecha.IsFailure) return Result.Failure<ReversoRegistrado>(fecha.Error);
        var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.DisbursementReversal, TramoDeDesembolso.PrefijoDeReverso, ct);
        var payload = JsonSerializer.Serialize(new { reverses = desembolso.PublicId, order = orden.PublicId, cause = causa, reason = motivo, result = mensajeDelResultado }, Json);
        var reverso = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new LoanOperation
        {
            LoanId = credito.Id,
            Kind = LoanOperationKind.DisbursementReversal,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = desembolso.ValueDate,
            AccountingDate = fecha.Value,
            RegisteredAt = sello.RegisteredAt,
            Amount = desembolso.Amount,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.OriginUserCentralId,
            ReversesOperationId = desembolso.Id,
            DisbursementForm = desembolso.DisbursementForm,
            TrancheNumber = desembolso.TrancheNumber,
            InboundMessageId = recibo,
            Reason = motivo ?? $"Resultado del giro: {causa}",
            Payload = payload,
            InputsSha256 = TramoDeDesembolso.Sha256(payload),
            RulesVersion = desembolso.RulesVersion,
            PrevalidationOutcome = PrevalidationOutcome.NotApplicable.ToString(),
        });

        var espejos = new List<LoanTransaction>();
        var originales = libro.ToDictionary(t => t.Id);
        foreach (var fila in propuesta.AEspejar)
        {
            var espejo = RegistroDeCartera.Espejar(originales[fila.Id], reverso, fecha.Value);
            if (espejo.IsFailure) return Result.Failure<ReversoRegistrado>(espejo.Error);
            espejos.Add(espejo.Value);
        }
        var ajuste = propuesta.ConciliarCausacionDesde is { } desde
            ? await motor.ConciliarCausacionAsync(credito.Id, desde, reverso, fecha.Value, ct)
            : [];
        registro.RegistrarTransacciones(espejos.Concat(ajuste));

        var estado = credito.Status;
        if (vivos <= 1)
        {
            RegistroDeCartera.CambiarEstado(credito, LoanStatus.PendingDisbursement, reloj.HoyLocal);
            estado = LoanStatus.PendingDisbursement;
        }
        await db.SaveChangesAsync(ct);
        desembolso.ReversedByOperationId = reverso.Id;

        var sucursal = await emision.SucursalContableAsync(credito.BranchId, ct);
        if (sucursal.IsFailure) return Result.Failure<ReversoRegistrado>(sucursal.Error);
        var deudor = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == credito.DebtorPersonId)
            .Select(p => new { p.PublicId, p.IsEmployee }).FirstAsync(ct);
        var linea = await db.CreditLines.AsNoTracking().Where(l => l.Id == credito.CreditLineId).Select(l => l.Code).FirstAsync(ct);
        var beneficiario = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == orden.BeneficiaryPersonId).Select(p => (Guid?)p.PublicId).FirstAsync(ct);
        var forma = desembolso.DisbursementForm ?? DisbursementForm.Transfer;
        var valores = propuesta.AEspejar.GroupBy(f => (f.Concepto, f.Cuota))
            .Select(g => new ConceptAmountV1
            {
                Concept = g.Key.Concepto.ToString(),
                InstallmentNumber = g.Key.Cuota,
                Amount = g.Sum(f => f.Importe),
                ReasonCode = g.Key.Concepto == LoanConcept.Principal ? forma.ToString() : null,
            })
            .ToList();
        var ajustes = ajuste.Select(t => new ConceptAmountV1 { Concept = t.Concept.ToString(), InstallmentNumber = t.InstallmentNumber, Amount = t.Amount }).ToList();
        var hecho = new HechoDeCredito(credito, condiciones, linea, deudor.PublicId, deudor.IsEmployee, sucursal.Value, fecha.Value);
        var mensajes = await emision.DesembolsoReversadoAsync(new ReversoAEmitir(hecho, reverso, desembolso, causa, mensajeDelResultado, motivo, valores, estado,
            forma, beneficiario, ajustes, [], actor.UsuarioDeOrigen), ct);
        return Result.Success(new ReversoRegistrado(reverso, mensajes, estado));
    }

    private static FilaDelLibro Fila(LoanTransaction t) =>
        new(t.Id, t.Kind, t.Concept, t.InstallmentNumber, t.Amount, t.ValueDate, t.OperationId, t.ProcessRunId, t.ReversesTransactionId);
}
