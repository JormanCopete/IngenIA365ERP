using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Entities.Lending.BulkCharges;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.BulkCharges;

/// <summary>
/// La anulación de la obligación de un cargo masivo (2026-11-25, feature 026; R9 de la investigación). Sólo si el libro del crédito no tiene nada más que
/// su desembolso —ni pagos, ni causación con valor, ni novedades—: la operación <c>DisbursementReversal</c> con el espejo del capital por
/// <see cref="RegistroDeCartera"/>, el crédito <c>Active → Voided</c> con el motivo, la línea de la corrida anulada (libera el período) y el hecho
/// <c>CargoMasivoAnulado</c> con el modo que se le pase. Lo que ya tiene movimientos se corrige con novedad o condonación. Bajo el cerrojo del deudor que
/// tomó quien llama; guarda sin confirmar. (nuevo)
/// </summary>
public sealed class AnulacionDeCargoMasivo(
    IApplicationDbContext db,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    IDateTimeService reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>¿El crédito sólo tiene su desembolso? (sin pagos, causación con valor ni otras operaciones vivas).</summary>
    public async Task<bool> SoloElDesembolsoAsync(int loanId, CancellationToken ct) =>
        !await db.LoanOperations.AsNoTracking().AnyAsync(o => o.LoanId == loanId && o.Kind != LoanOperationKind.Disbursement, ct)
        && !await db.LoanTransactions.AsNoTracking().AnyAsync(t => t.LoanId == loanId && t.Kind != LoanTransactionKind.Disbursement && t.Amount != 0m, ct);

    public async Task<Result> AnularAsync(BulkChargeRun run, BulkChargeLine linea, string motivo, int? usuario, Guid? usuarioCentral, ModoDeEntrega modo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(linea);
        if (linea.Status != BulkChargeLineStatus.Created || linea.LoanId is not { } loanId)
            return Result.Failure(ErroresDeCargoMasivo.LineaDeCorridaInexistente());
        var credito = await db.Loans.FirstAsync(l => l.Id == loanId, ct);
        if (credito.Status != LoanStatus.Active || !await SoloElDesembolsoAsync(loanId, ct))
            return Result.Failure(ErroresDeCargoMasivo.TieneMovimientos(credito.Number));

        var cargo = await db.LoanOperations.FirstAsync(o => o.LoanId == loanId && o.Kind == LoanOperationKind.Disbursement, ct);
        var control = await db.LoanControls.FirstAsync(c => c.LoanId == loanId, ct);
        var condiciones = await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == loanId).OrderByDescending(t => t.VersionNumber).FirstAsync(ct);
        var originales = await db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == loanId && t.OperationId == cargo.Id).ToListAsync(ct);
        var hoy = reloj.HoyLocal;
        var fecha = await fechaContable.CalcularAsync(hoy, ct);
        if (fecha.IsFailure) return Result.Failure(fecha.Error);

        var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.DisbursementReversal, TramoDeDesembolso.PrefijoDeReverso, ct);
        var payload = JsonSerializer.Serialize(new { reverses = cargo.PublicId, bulkChargeRun = run.PublicId, reason = motivo }, Json);
        var reverso = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new LoanOperation
        {
            LoanId = credito.Id,
            Kind = LoanOperationKind.DisbursementReversal,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = hoy < cargo.ValueDate ? cargo.ValueDate : hoy,
            AccountingDate = fecha.Value,
            RegisteredAt = sello.RegisteredAt,
            Amount = cargo.Amount,
            ActorKind = ActorKind.Person,
            ActorUserId = usuario,
            OriginUserCentralId = usuarioCentral,
            ReversesOperationId = cargo.Id,
            DisbursementForm = DisbursementForm.Charge,
            TrancheNumber = cargo.TrancheNumber,
            Reason = motivo,
            Payload = payload,
            InputsSha256 = TramoDeDesembolso.Sha256(payload),
            RulesVersion = cargo.RulesVersion,
            PrevalidationOutcome = PrevalidationOutcome.NotApplicable.ToString(),
        });
        var espejos = registro.Reversar(originales, reverso, fecha.Value);
        if (espejos.IsFailure) return Result.Failure(espejos.Error);
        registro.RegistrarTransacciones(espejos.Value);
        await db.SaveChangesAsync(ct);
        cargo.ReversedByOperationId = reverso.Id;
        RegistroDeCartera.CambiarEstado(credito, LoanStatus.Voided, hoy, motivo);
        linea.Anular(motivo, reloj.UtcNow);

        var persona = await db.People.AsNoTracking().Where(p => p.Id == credito.DebtorPersonId).Select(p => new { p.PublicId, p.IsEmployee }).FirstAsync(ct);
        var sucursal = await emision.SucursalContableAsync(credito.BranchId, ct);
        if (sucursal.IsFailure) return Result.Failure(sucursal.Error);
        var codigo = await db.CreditLines.AsNoTracking().Where(l => l.Id == credito.CreditLineId).Select(l => l.Code).FirstAsync(ct);
        var contraparte = run.CounterpartPersonId is { } cp
            ? await db.People.AsNoTracking().Where(p => p.Id == cp).Select(p => p.PublicId).FirstAsync(ct)
            : persona.PublicId;
        var original = await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginPublicId == cargo.PublicId && m.Type == CargoMasivoRegistradoV1.Type).Select(m => (Guid?)m.PublicId).FirstOrDefaultAsync(ct);
        var hecho = new HechoDeCredito(credito, condiciones, codigo, persona.PublicId, persona.IsEmployee, sucursal.Value, fecha.Value);
        var emitido = await emision.CargoMasivoAnuladoAsync(hecho, reverso, cargo, cargo.Amount, run, contraparte, original, motivo, modo, ct);
        if (emitido.IsFailure) return Result.Failure(emitido.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
