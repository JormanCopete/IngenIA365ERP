using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Recaudo;

/// <summary>Un cheque recibido. Estado: 1 recibido, 2 en custodia, 3 consignado, 4 en canje, 5 pagado, 6 devuelto. (nuevo)</summary>
public sealed record ChequeRecibidoDto(Guid PublicId, Guid DraweeBankPublicId, string DraweeBankName, string CheckNumber, string AccountNumber, Guid? DrawerPersonPublicId,
    string? DrawerName, Guid PayerPersonPublicId, string PayerName, DateOnly IssueDate, decimal Amount, bool IsThirdParty, bool SamePlace, DateOnly PresentationDeadline,
    bool PastDeadline, int Status, string? ReceiptNumber, string? ReturnCauseCode, string? PayerReceivableNumber, DateTime CreatedAt);

public sealed record GiradorConDevolucionesDto(Guid? DrawerPersonPublicId, string? DrawerName, string AccountNumber, int Returned, DateOnly LastReturnedOn);

public sealed record CausalDeDevolucionDto(string Code, string Description, bool CountsForDrawerList);

public sealed record DevolverChequeRequest(string CauseCode, Guid? SupportAttachmentPublicId, string? Note);

public sealed record NotaDeChequeRequest(string? Note);

/// <summary>Cliente tipado de los cheques recibidos (feature 015, US15, T0219). Las escrituras llevan <see cref="ClaveDeOperacion"/>. (nuevo)</summary>
/// <summary>R2 (fase 37; FR-084): un lote de canje. Estado: 1 en canje, 2 pagado en parte, 3 pagado, 4 cerrado. (nuevo)</summary>
public sealed record LoteDeCanjeDto(Guid PublicId, string Number, Guid OwnBankAccountPublicId, DateOnly DepositedOn, int CheckCount, decimal TotalAmount, decimal PaidAmount,
    decimal ReturnedAmount, decimal InClearingAmount, int Status, IReadOnlyList<ChequeRecibidoDto> Checks);

public sealed record ArmarLoteDeCanjeRequest(Guid OwnBankAccountPublicId, IReadOnlyList<Guid> CheckPublicIds, Guid? RemittancePublicId, DateOnly? DepositedOn);

public sealed record CruzarLoteDeCanjeRequest(Guid StatementLinePublicId, IReadOnlyList<Guid>? CheckPublicIds);

public sealed class ChequesRecibidosClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/receipts/received-checks";

    public Task<ResultadoDeInventario<IReadOnlyList<ChequeRecibidoDto>>> ChequesAsync(int? estado = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ChequeRecibidoDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery(Base, EnvioDeVentas.Query(("state", EnvioDeVentas.Texto(estado)))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<GiradorConDevolucionesDto>>> GiradoresConDevolucionesAsync(Guid? persona = null, string? cuenta = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<GiradorConDevolucionesDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/drawers-with-returns",
            EnvioDeVentas.Query(("person", EnvioDeVentas.Texto(persona)), ("account", cuenta))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CausalDeDevolucionDto>>> CausalesAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CausalDeDevolucionDto>>(HttpMethod.Get, $"{Base}/return-causes", null, null, ct);

    public Task<ResultadoDeInventario<ChequeRecibidoDto>> ConsignarAsync(Guid cheque, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ChequeRecibidoDto>(HttpMethod.Post, $"{Base}/{cheque}/deposit", new NotaDeChequeRequest(null), clave, ct);

    public Task<ResultadoDeInventario<ChequeRecibidoDto>> EnCanjeAsync(Guid cheque, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ChequeRecibidoDto>(HttpMethod.Post, $"{Base}/{cheque}/clearing", new NotaDeChequeRequest(null), clave, ct);

    public Task<ResultadoDeInventario<ChequeRecibidoDto>> PagadoAsync(Guid cheque, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ChequeRecibidoDto>(HttpMethod.Post, $"{Base}/{cheque}/paid", new NotaDeChequeRequest(null), clave, ct);

    public Task<ResultadoDeInventario<ChequeRecibidoDto>> DevolverAsync(Guid cheque, DevolverChequeRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ChequeRecibidoDto>(HttpMethod.Post, $"{Base}/{cheque}/return", r, clave, ct);

    // R2 (fase 37): el canje completo.
    public Task<ResultadoDeInventario<IReadOnlyList<LoteDeCanjeDto>>> LotesDeCanjeAsync(int? estado = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<LoteDeCanjeDto>>(HttpMethod.Get, "/api/receipts/clearing-batches" + (estado is { } e ? $"?state={e}" : string.Empty), null, null, ct);

    public Task<ResultadoDeInventario<LoteDeCanjeDto>> ArmarLoteDeCanjeAsync(ArmarLoteDeCanjeRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<LoteDeCanjeDto>(HttpMethod.Post, "/api/receipts/clearing-batches", r, clave, ct);

    public Task<ResultadoDeInventario<LoteDeCanjeDto>> CruzarLoteDeCanjeAsync(Guid lote, CruzarLoteDeCanjeRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<LoteDeCanjeDto>(HttpMethod.Post, $"/api/receipts/clearing-batches/{lote}/statement-match", r, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeRecaudo.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, null, ct);
}
