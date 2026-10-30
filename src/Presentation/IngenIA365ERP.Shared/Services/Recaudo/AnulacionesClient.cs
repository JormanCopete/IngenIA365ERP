using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Recaudo;

/// <summary>Una anulación de recibo (los enums llegan como número). (nuevo)</summary>
public sealed record AnulacionDto(Guid PublicId, string? Number, Guid ReceiptPublicId, string ReceiptNumber, decimal Amount, string Reason, int Status, bool InSameSession,
    string RequestedByName, DateTime RequestedAt, string? ApprovedByName, DateTime? DecidedAt, string? DecisionReason, Guid? ApprovalRequestPublicId,
    IReadOnlyList<string> OmittedApplications, Guid? PayerCreditBalancePublicId, string VerificationCode);

/// <summary>Un reverso del módulo. (nuevo)</summary>
public sealed record ReversoDeModuloDto(Guid PublicId, Guid ReceiptPublicId, string ReceiptNumber, string? ProductNumber, string Module, string MessageType,
    string ModuleReversalNumber, decimal Amount, string Reason, int Cause, string? ConfirmedByName, DateTime? ConfirmedAt, string? ConfirmationNote,
    Guid? PayerCreditBalancePublicId, DateTime CreatedAt);

public sealed record MovimientoDeSaldoDto(Guid PublicId, int Kind, decimal Amount, string? DocumentNumber, DateTime RecordedAt, string? Note);

/// <summary>Un saldo a favor de pagador. (nuevo)</summary>
public sealed record SaldoAFavorDto(Guid PublicId, Guid PersonPublicId, string PersonName, string PersonTaxId, int Kind, int Origin, string OriginDocumentNumber,
    decimal Amount, decimal Balance, int Status, DateOnly OpenedOn, int AgeDays, IReadOnlyList<MovimientoDeSaldoDto> Movements);

public sealed record DevolucionDeCarteraDto(Guid PaymentOrderPublicId, string PaymentOrderNumber, string LendingResolutionNumber, decimal Amount);

public sealed record MotivoDeAnulacionRequest(string? Reason);

public sealed record CausaDelReversoRequest(int Cause, string Note);

public sealed record DevolucionPorOrdenRequest(decimal Amount, Guid? PersonBankAccountPublicId, string Form);

public sealed record AplicarSaldoRequest(decimal Amount, Guid ReceiptPublicId);

public sealed record DevolverSaldoDeCarteraRequest(Guid ObligationPublicId, decimal Amount, Guid BeneficiaryPersonPublicId, Guid BranchPublicId,
    Guid? PersonBankAccountPublicId, string Form);

/// <summary>
/// Cliente tipado de la anulación de recibos, los reversos de los módulos y los saldos a favor (feature 015, US5, T0181; contracts/api.md §4). (nuevo)
/// </summary>
public sealed class AnulacionesClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/receipts";

    public Task<ResultadoDeInventario<AnulacionDto>> PedirAsync(Guid recibo, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<AnulacionDto>(HttpMethod.Post, $"{Base}/receipts/{recibo}/voiding", new MotivoDeAnulacionRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<AnulacionDto>>> AnulacionesAsync(int? estado = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<AnulacionDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/voidings", EnvioDeVentas.Query(("state", EnvioDeVentas.Texto(estado)))), null, null, ct);

    public Task<ResultadoDeInventario<AnulacionDto>> AprobarAsync(Guid anulacion, string? motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<AnulacionDto>(HttpMethod.Post, $"{Base}/voidings/{anulacion}/approve", new MotivoDeAnulacionRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<AnulacionDto>> RechazarAsync(Guid anulacion, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<AnulacionDto>(HttpMethod.Post, $"{Base}/voidings/{anulacion}/reject", new MotivoDeAnulacionRequest(motivo), clave, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DocumentoAsync(Guid anulacion, CancellationToken ct = default) =>
        EnvioDeVentas.DescargarAsync(http, auth, $"{Base}/voidings/{anulacion}/document", "anulacion.pdf", ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ReversoDeModuloDto>>> ReversosAsync(int? causa = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ReversoDeModuloDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/module-reversals", EnvioDeVentas.Query(("state", EnvioDeVentas.Texto(causa)))),
            null, null, ct);

    public Task<ResultadoDeInventario<ReversoDeModuloDto>> ConfirmarCausaAsync(Guid reverso, CausaDelReversoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ReversoDeModuloDto>(HttpMethod.Post, $"{Base}/module-reversals/{reverso}/confirm-cause", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<SaldoAFavorDto>>> SaldosAsync(int? estado = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<SaldoAFavorDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/payer-credit-balances", EnvioDeVentas.Query(("state", EnvioDeVentas.Texto(estado)))),
            null, null, ct);

    public Task<ResultadoDeInventario<SaldoAFavorDto>> DevolverPorOrdenAsync(Guid saldo, DevolucionPorOrdenRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<SaldoAFavorDto>(HttpMethod.Post, $"{Base}/payer-credit-balances/{saldo}/refund-by-order", r, clave, ct);

    public Task<ResultadoDeInventario<SaldoAFavorDto>> AplicarAsync(Guid saldo, AplicarSaldoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<SaldoAFavorDto>(HttpMethod.Post, $"{Base}/payer-credit-balances/{saldo}/apply", r, clave, ct);

    public Task<ResultadoDeInventario<DevolucionDeCarteraDto>> DevolverSaldoDeCarteraAsync(DevolverSaldoDeCarteraRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DevolucionDeCarteraDto>(HttpMethod.Post, $"{Base}/lending-credit-balances/refund-by-order", r, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
