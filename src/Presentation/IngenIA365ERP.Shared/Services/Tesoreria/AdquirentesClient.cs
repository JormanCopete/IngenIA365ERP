using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

/// <summary>Source: 1 Recaudo, 2 punto de venta; nulo, sin lote cobrado.</summary>
public sealed record LineaDeLiquidacionDto(int LineNumber, string TerminalCode, string? BatchNumber, DateOnly BatchDate, decimal Gross, decimal Commission, decimal Vat,
    decimal Withholding, decimal Net, int? Source, decimal? Collected, decimal Difference);

/// <summary>Status: 1 explicada, 2 por aprobar, 3 aprobada, 4 rechazada.</summary>
public sealed record LiquidacionDeAdquirenteDto(Guid PublicId, string Number, Guid AcquirerPublicId, string AcquirerCode, Guid BankAccountPublicId, string FileName,
    DateOnly SettlementDate, Guid? StatementLinePublicId, decimal? StatementAmount, decimal Gross, decimal Commission, decimal Vat, decimal WithholdingTax,
    decimal WithholdingVat, decimal WithholdingIca, decimal Net, decimal Collected, decimal Difference, decimal Tolerance, int Status, string? DecisionReason,
    Guid? FactMessagePublicId, IReadOnlyList<LineaDeLiquidacionDto> Lines);

/// <summary>Cliente tipado de la conciliación de adquirentes de T2 (feature 015, fase 44; api.md §13). (nuevo)</summary>
public sealed class AdquirentesClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/treasury/acquirer-settlements";

    public Task<ResultadoDeInventario<IReadOnlyList<LiquidacionDeAdquirenteDto>>> LiquidacionesAsync(Guid? cuenta, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<IReadOnlyList<LiquidacionDeAdquirenteDto>>(http, auth, HttpMethod.Get,
            EnvioDeVentas.ConQuery(Base, EnvioDeVentas.Query(("account", EnvioDeVentas.Texto(cuenta)))), null, null, null, ct);

    /// <summary>Los formatos activos de liquidación de adquirentes (ámbito 11).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<FormatoBancarioResumenDto>>> FormatosAsync(CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<IReadOnlyList<FormatoBancarioResumenDto>>(http, auth, HttpMethod.Get, EnvioDeVentas.ConQuery("/api/core/bank-file-formats",
            EnvioDeVentas.Query(("scope", "AcquirerSettlement"), ("onlyActive", "true"))), null, null, null, ct);

    public Task<ResultadoDeInventario<LiquidacionDeAdquirenteDto>> CargarAsync(Guid red, Guid cuenta, Guid formato, string nombre, byte[] contenido, DateOnly? fecha,
        Guid? lineaDelExtracto, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.MultipartAsync<LiquidacionDeAdquirenteDto>(http, auth, Base, nombre, contenido, new Dictionary<string, string?>
        {
            ["acquirerPublicId"] = red.ToString(), ["bankAccountPublicId"] = cuenta.ToString(), ["formatPublicId"] = formato.ToString(),
            ["settlementDate"] = fecha?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), ["statementLinePublicId"] = lineaDelExtracto?.ToString(),
        }, clave, ct);

    public Task<ResultadoDeInventario<LiquidacionDeAdquirenteDto>> DecidirAsync(Guid id, bool aprobar, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<LiquidacionDeAdquirenteDto>(http, auth, HttpMethod.Post, $"{Base}/{id}/{(aprobar ? "approve" : "reject")}", new { reason = motivo }, clave,
            null, ct);
}
