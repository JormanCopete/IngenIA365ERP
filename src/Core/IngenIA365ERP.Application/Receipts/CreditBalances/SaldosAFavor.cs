using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.CreditBalances;

/// <summary>Un saldo a favor de pagador para la pantalla. (nuevo)</summary>
public sealed record PayerCreditBalanceDto(
    Guid PublicId,
    Guid PersonPublicId,
    string PersonName,
    string PersonTaxId,
    PayerCreditBalanceKind Kind,
    PayerCreditBalanceOrigin Origin,
    string OriginDocumentNumber,
    decimal Amount,
    decimal Balance,
    PayerCreditBalanceState Status,
    DateOnly OpenedOn,
    int AgeDays,
    IReadOnlyList<PayerCreditBalanceMovementDto> Movements);

public sealed record PayerCreditBalanceMovementDto(Guid PublicId, PayerCreditBalanceMovementKind Kind, decimal Amount, string? DocumentNumber, DateTime RecordedAt, string? Note);

/// <summary>
/// Lo común de los saldos a favor de los pagadores (feature 015, US5, T0177; FR-054; data-model §4.3): nacer con su movimiento <c>Recognized</c>, bajar por una
/// resolución y reabrir cuando Tesorería informa la orden rechazada o anulada. Escribe en la unidad de trabajo de quien llama, sin guardar. (nuevo)
/// </summary>
public static class SaldosAFavor
{
    /// <summary>Reconoce un saldo a favor del pagador por <paramref name="valor"/>. Sin guardar.</summary>
    public static PayerCreditBalance Reconocer(IApplicationDbContext db, int persona, PayerCreditBalanceOrigin origen, Guid documento, string numero, int oficina,
        decimal valor, DateOnly hoy, int? usuario, DateTime ahora, Guid? cuentaPropia = null)
    {
        var saldo = new PayerCreditBalance
        {
            PersonId = persona, Origin = origen, OriginDocumentPublicId = documento, OriginDocumentNumber = numero, BranchId = oficina, Amount = valor, Balance = valor,
            OpenedOn = hoy, OwnBankAccountPublicId = cuentaPropia,
        };
        db.PayerCreditBalances.Add(saldo);
        return saldo;
    }

    /// <summary>El movimiento de reconocimiento, una vez guardado el saldo (necesita su Id).</summary>
    public static PayerCreditBalanceMovement Reconocido(IApplicationDbContext db, PayerCreditBalance saldo, int? usuario, DateTime ahora)
    {
        var m = new PayerCreditBalanceMovement
        {
            PayerCreditBalanceId = saldo.Id, Kind = PayerCreditBalanceMovementKind.Recognized, Amount = saldo.Amount, DocumentPublicId = saldo.OriginDocumentPublicId,
            DocumentNumber = saldo.OriginDocumentNumber, RecordedByUserId = usuario, RecordedAt = ahora,
        };
        db.PayerCreditBalanceMovements.Add(m);
        return m;
    }

    /// <summary>Baja el saldo por una resolución (devuelto por orden, aplicado con recibo). Sin guardar.</summary>
    public static PayerCreditBalanceMovement Bajar(IApplicationDbContext db, PayerCreditBalance saldo, PayerCreditBalanceMovementKind clase, decimal valor,
        Guid? documento, string? numero, int? usuario, DateTime ahora, string? nota)
    {
        saldo.Balance -= valor;
        if (saldo.Balance == 0m && clase != PayerCreditBalanceMovementKind.RefundedByOrder) saldo.Status = PayerCreditBalanceState.Resolved;
        var m = new PayerCreditBalanceMovement
        {
            PayerCreditBalanceId = saldo.Id, Kind = clase, Amount = valor, DocumentPublicId = documento, DocumentNumber = numero, RecordedByUserId = usuario,
            RecordedAt = ahora, Note = nota,
        };
        db.PayerCreditBalanceMovements.Add(m);
        return m;
    }

    public static async Task<PayerCreditBalanceDto> DtoAsync(IApplicationDbContext db, PayerCreditBalance s, DateOnly hoy, CancellationToken ct)
    {
        var p = await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == s.PersonId)
            .Select(x => new { x.PublicId, x.FirstName, x.LastName, x.TaxId }).FirstAsync(ct);
        var movimientos = await db.PayerCreditBalanceMovements.AsNoTracking().Where(m => m.PayerCreditBalanceId == s.Id).OrderBy(m => m.Id)
            .Select(m => new PayerCreditBalanceMovementDto(m.PublicId, m.Kind, m.Amount, m.DocumentNumber, m.RecordedAt, m.Note)).ToListAsync(ct);
        return new PayerCreditBalanceDto(s.PublicId, p.PublicId, $"{p.FirstName} {p.LastName}".Trim(), p.TaxId ?? string.Empty, s.Kind, s.Origin, s.OriginDocumentNumber,
            s.Amount, s.Balance, s.Status, s.OpenedOn, hoy.DayNumber - s.OpenedOn.DayNumber, movimientos);
    }
}
