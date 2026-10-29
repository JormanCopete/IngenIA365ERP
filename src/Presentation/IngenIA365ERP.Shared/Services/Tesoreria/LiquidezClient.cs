using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

public sealed record ComponenteDeLiquidezDto(string Code, string Group, string Name, decimal Amount, int Sign, bool OnlyProjected, Guid? ReferencePublicId);

public sealed record ComponenteFaltanteDto(string Code, string Reason);

public sealed record CuentaDelFondoDto(Guid OwnBankAccountPublicId, string BankCode, string AccountNumber, decimal Balance, DateOnly? StatementDate);

public sealed record PosicionDeLiquidezDto(DateOnly Date, IReadOnlyList<ComponenteDeLiquidezDto> Components, IReadOnlyList<ComponenteFaltanteDto> Missing, decimal Banks,
    decimal Vaults, decimal CashSessions, decimal Commitments, decimal? ExpectedLending, decimal? ExpectedDeposits, decimal Available, decimal Projected,
    decimal LiquidityFund, IReadOnlyList<CuentaDelFondoDto> FundAccounts);

public sealed record FotoDeLiquidezDto(Guid PublicId, string Number, DateOnly Date, DateTime TakenAt, decimal Banks, decimal Vaults, decimal CashSessions, decimal Commitments,
    decimal? ExpectedLending, decimal? ExpectedDeposits, decimal Available, decimal Projected, decimal LiquidityFund, int MissingCount, Guid? FundMessagePublicId);

/// <summary>Direction: 1 entrada, 2 salida. Source: 1 digitada, 2 Cartera, 3 Captaciones, 4 órdenes, 5 Nómina.</summary>
public sealed record CategoriaDeFlujoDto(Guid PublicId, string Code, string Name, int Direction, int Source, int SortOrder, bool IsActive);

public sealed record LineaDeFlujoDto(Guid CategoryPublicId, string Code, string Name, int Direction, int Source, decimal Projected, bool Automatic);

public sealed record PeriodoDeFlujoDto(DateOnly Start, DateOnly End, IReadOnlyList<LineaDeFlujoDto> Lines, decimal ProjectedInflows, decimal ProjectedOutflows,
    decimal? RealInflows, decimal? RealOutflows);

public sealed record FlujoDeCajaDto(int PeriodKind, IReadOnlyList<PeriodoDeFlujoDto> Periods, IReadOnlyList<ComponenteFaltanteDto> Missing);

public sealed record TableroDeTesoreriaDto(DateOnly Date, decimal Available, decimal Projected, decimal Banks, decimal LiquidityFund, int OpenOrders, decimal OpenOrdersAmount,
    int BatchesPendingApproval, int ChecksInTransit, decimal ChecksInTransitAmount, int OpenReconciliations, int BankNotesPendingApproval, DateOnly? LastSnapshotDate,
    int MissingComponents);

public sealed record CategoriaDeFlujoRequest(string Code, string Name, string Direction, string Source, int SortOrder, bool IsActive);

public sealed record ProyeccionDeFlujoRequest(Guid CategoryPublicId, string PeriodKind, DateOnly PeriodStart, decimal Amount, string? Notes);

/// <summary>Cliente tipado de la liquidez de T2 (feature 015, fase 43; api.md §13). (nuevo)</summary>
public sealed class LiquidezClient(HttpClient http, CentralAuthClient auth)
{
    public Task<ResultadoDeInventario<PosicionDeLiquidezDto>> PosicionAsync(DateOnly? fecha, int? dias, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<PosicionDeLiquidezDto>(http, auth, HttpMethod.Get, EnvioDeVentas.ConQuery("/api/treasury/liquidity",
            EnvioDeVentas.Query(("date", EnvioDeVentas.Texto(fecha)), ("days", dias?.ToString(System.Globalization.CultureInfo.InvariantCulture)))), null, null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<FotoDeLiquidezDto>>> FotosAsync(CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<IReadOnlyList<FotoDeLiquidezDto>>(http, auth, HttpMethod.Get, "/api/treasury/liquidity/snapshots", null, null, null, ct);

    public Task<ResultadoDeInventario<FotoDeLiquidezDto>> TomarFotoAsync(DateOnly? fecha, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<FotoDeLiquidezDto>(http, auth, HttpMethod.Post, "/api/treasury/liquidity/snapshots", new { date = fecha }, clave, null, ct);

    public Task<ResultadoDeInventario<FlujoDeCajaDto>> FlujoAsync(string clase, DateOnly? desde, int? periodos, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<FlujoDeCajaDto>(http, auth, HttpMethod.Get, EnvioDeVentas.ConQuery("/api/treasury/cash-flow-projections",
            EnvioDeVentas.Query(("kind", clase), ("from", EnvioDeVentas.Texto(desde)), ("periods", periodos?.ToString(System.Globalization.CultureInfo.InvariantCulture)))),
            null, null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CategoriaDeFlujoDto>>> CategoriasAsync(CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<IReadOnlyList<CategoriaDeFlujoDto>>(http, auth, HttpMethod.Get, "/api/treasury/cash-flow-projections/categories", null, null, null, ct);

    public Task<ResultadoDeInventario<CategoriaDeFlujoDto>> GuardarCategoriaAsync(Guid? id, CategoriaDeFlujoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<CategoriaDeFlujoDto>(http, auth, id is null ? HttpMethod.Post : HttpMethod.Put,
            id is null ? "/api/treasury/cash-flow-projections/categories" : $"/api/treasury/cash-flow-projections/categories/{id}", r, clave, null, ct);

    public Task<ResultadoDeInventario<LineaDeFlujoDto>> GuardarProyeccionAsync(ProyeccionDeFlujoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<LineaDeFlujoDto>(http, auth, HttpMethod.Post, "/api/treasury/cash-flow-projections/entries", r, clave, null, ct);

    public Task<ResultadoDeInventario<TableroDeTesoreriaDto>> TableroAsync(CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<TableroDeTesoreriaDto>(http, auth, HttpMethod.Get, "/api/treasury/dashboard", null, null, null, ct);
}
