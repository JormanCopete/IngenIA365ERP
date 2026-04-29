namespace IngenIA365ERP.Legacy.Adapters;

/// <summary>
/// Base interface for all legacy adapters that bridge ERP.Core (the migrated VB.NET codebase)
/// to the new IngenIA365ERP application services.
/// </summary>
public interface ILegacyAdapter
{
    /// <summary>
    /// Indicates whether the legacy module is available and can be called.
    /// Returns false if ERP.Core is not loaded or the module is not initialized.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Initializes the legacy adapter with the ODBC connection information
    /// needed by the legacy ERP.Core module.
    /// </summary>
    Task InitializeAsync(string connectionString, CancellationToken cancellationToken = default);
}
