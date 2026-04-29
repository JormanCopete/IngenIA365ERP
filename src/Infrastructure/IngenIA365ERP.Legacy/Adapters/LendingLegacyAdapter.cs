namespace IngenIA365ERP.Legacy.Adapters;

/// <summary>
/// Bridges legacy ERP.Core.CarteraFinanciera.Services.Clscartera calls
/// to the new IngenIA365ERP application services.
///
/// Legacy methods bridged:
///   - Clscartera (13 partial files, 145 methods, 11 enums)
///   - Payment processing: GrabaPago, RecaudoNomina
///   - Portfolio queries: BuscaObligacion, ConsultaCartera
///   - Loan calculations: CalculaInteres, CalculaMora
///   - Restructuring: Reestructurar
/// </summary>
public class LendingLegacyAdapter : ILegacyAdapter
{
    public bool IsAvailable => false; // TODO: Set to true when ERP.Core reference is added

    public Task InitializeAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        // TODO: Initialize legacy Clscartera with OdbcConect struct
        // var varini = new ConectBd.ClsConect.OdbcConect();
        // varini.BaseDatos = connectionString;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Queries the legacy loan portfolio using Clscartera.BuscaObligacion.
    /// </summary>
    public Task<LegacyLoanInfo?> LookupLoanAsync(
        string personCode, int creditLineId, long portfolioNumber,
        CancellationToken cancellationToken = default)
    {
        // TODO: Bridge to legacy call:
        // var cartera = new Clscartera();
        // cartera.varini = _varini;
        // string saldo = "", tasa = "", estado = "";
        // cartera.BuscaObligacion(personCode, creditLineId.ToString(),
        //     portfolioNumber.ToString(), ref saldo, ref tasa, ref estado, ...);
        throw new NotImplementedException(
            "LendingLegacyAdapter.LookupLoanAsync: ERP.Core reference not yet configured.");
    }

    /// <summary>
    /// Processes a payment through the legacy Clscartera.GrabaPago method.
    /// </summary>
    public Task<bool> ProcessPaymentAsync(
        string personCode, int creditLineId, long portfolioNumber,
        decimal amount, string voucherType, long documentNumber,
        CancellationToken cancellationToken = default)
    {
        // TODO: Bridge to legacy call:
        // cartera.GrabaPago(personCode, creditLineId, portfolioNumber,
        //     amount, voucherType, documentNumber, ...);
        throw new NotImplementedException(
            "LendingLegacyAdapter.ProcessPaymentAsync: ERP.Core reference not yet configured.");
    }

    /// <summary>
    /// Calculates interest accrual using the legacy engine.
    /// </summary>
    public Task<LegacyInterestResult> CalculateInterestAsync(
        string personCode, int creditLineId, long portfolioNumber,
        DateTime calculationDate,
        CancellationToken cancellationToken = default)
    {
        // TODO: Bridge to legacy call:
        // cartera.CalculaInteres(personCode, creditLineId, portfolioNumber, calculationDate, ...);
        throw new NotImplementedException(
            "LendingLegacyAdapter.CalculateInterestAsync: ERP.Core reference not yet configured.");
    }
}

/// <summary>
/// DTO for legacy loan lookup results.
/// Maps to the ref string params returned by Clscartera.BuscaObligacion.
/// </summary>
public record LegacyLoanInfo(
    string PersonCode,
    int CreditLineId,
    long PortfolioNumber,
    decimal Balance,
    decimal InterestRate,
    string Status,
    int DaysOverdue,
    string Category
);

/// <summary>
/// DTO for legacy interest calculation results.
/// </summary>
public record LegacyInterestResult(
    decimal InterestAmount,
    decimal PenaltyAmount,
    decimal InsuranceAmount,
    int DaysCalculated
);
