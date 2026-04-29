namespace IngenIA365ERP.Legacy.Adapters;

/// <summary>
/// Bridges legacy ERP.Core.Contabilidad.Services.ClsContabilidad calls
/// to the new IngenIA365ERP application services.
///
/// Legacy methods bridged:
///   - ClsContabilidad.GrabaMovimiento (32 params) -> JournalEntry creation
///   - ClsContabilidad.BuscarTercero -> Person lookup
///   - ClsContabilidad.BuscarComprobante -> VoucherType lookup
///   - ClsContabilidad.Promedios -> AccountBalance calculations
/// </summary>
public class AccountingLegacyAdapter : ILegacyAdapter
{
    public bool IsAvailable => false; // TODO: Set to true when ERP.Core reference is added

    public Task InitializeAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        // TODO: Initialize legacy ClsContabilidad with OdbcConect struct
        // var varini = new ConectBd.ClsConect.OdbcConect();
        // varini.BaseDatos = connectionString;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Translates a new-system journal entry creation request into legacy
    /// ClsContabilidad.GrabaMovimiento call with its 32 positional parameters.
    /// </summary>
    public Task<bool> PostJournalEntryAsync(
        string voucherTypeCode, long documentNumber, string accountCode,
        string thirdPartyNit, decimal debitAmount, decimal creditAmount,
        string description, DateTime transactionDate,
        CancellationToken cancellationToken = default)
    {
        // TODO: Bridge to legacy call:
        // var contabilidad = new ClsContabilidad();
        // contabilidad.varini = _varini;
        // contabilidad.GrabaMovimiento(
        //     voucherTypeCode, documentNumber.ToString(), thirdPartyNit,
        //     accountCode, debitAmount.ToString(), creditAmount.ToString(),
        //     /* ... remaining 26 params with defaults ... */
        // );
        throw new NotImplementedException(
            "AccountingLegacyAdapter.PostJournalEntryAsync: ERP.Core reference not yet configured.");
    }

    /// <summary>
    /// Looks up a third party (person) using the legacy BuscarTercero method.
    /// </summary>
    public Task<LegacyThirdPartyInfo?> LookupThirdPartyAsync(
        string nit, CancellationToken cancellationToken = default)
    {
        // TODO: Bridge to legacy call:
        // string nombre = "", direccion = "", telefono = "", ciudad = "";
        // contabilidad.BuscarTercero(nit, ref nombre, ref direccion, ...);
        throw new NotImplementedException(
            "AccountingLegacyAdapter.LookupThirdPartyAsync: ERP.Core reference not yet configured.");
    }
}

/// <summary>
/// DTO for legacy third-party lookup results.
/// Maps to the ref string params returned by ClsContabilidad.BuscarTercero.
/// </summary>
public record LegacyThirdPartyInfo(
    string Nit,
    string Name,
    string? Address,
    string? Phone,
    string? City
);
