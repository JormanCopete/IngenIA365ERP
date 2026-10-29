using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

/// <summary>Una chequera. Estado: 1 activa, 2 agotada, 3 anulada. (nuevo)</summary>
public sealed record ChequeraDto(Guid PublicId, Guid BankAccountPublicId, string BankAccountCode, long FromNumber, long ToNumber, long NextNumber, int Remaining, int Status,
    string CustodianName, Guid? CustodyAttachmentPublicId, DateTime RegisteredAt);

/// <summary>Un cheque girado. Estado: 1 emitido, 2 entregado, 3 cobrado, 4 anulado, 5 no cobrado, 6 en blanco dañado. (nuevo)</summary>
public sealed record ChequeGiradoDto(Guid PublicId, Guid CheckbookPublicId, string BankAccountCode, long CheckNumber, Guid? PaymentOrderPublicId, string? OrderNumber,
    Guid? BeneficiaryPersonPublicId, string? BeneficiaryName, decimal Amount, DateOnly IssuedOn, bool Crossed, bool FirstBeneficiaryOnly, int Status, DateOnly? DeliveredOn,
    DateOnly? CashedOn, string? VoidReason, int DaysWithoutCashing);

/// <summary>Una remesa de efectivo. Dirección: 1 bóveda → banco, 2 banco → bóveda. Estado: 1 en tránsito, 2 confirmada, 3 vencida, 4 anulada. (nuevo)</summary>
public sealed record RemesaDto(Guid PublicId, string Number, int Direction, Guid VaultPublicId, Guid BankAccountPublicId, string BankAccountCode, decimal Amount, string? BagSeal,
    Guid? MinutesAttachmentPublicId, string? SourceDocumentNumber, DateOnly DispatchDate, int Status, DateOnly? ConfirmedOn, Guid? ConfirmedByStatementLinePublicId,
    int DaysInTransit);

public sealed record RegistrarChequeraRequest(Guid BankAccountPublicId, long FromNumber, long ToNumber, Guid? CustodianUserPublicId, Guid? CustodyAttachmentPublicId);

public sealed record MotivoDeTesoreriaRequest(string Reason);

public sealed record EmitirChequesRequest(Guid CheckbookPublicId, bool? Crossed, bool? FirstBeneficiaryOnly);

public sealed record EntregarChequeRequest(Guid DeliveryReceiptAttachmentPublicId, DateOnly? DeliveredOn);

public sealed record ConfirmarRemesaRequest(Guid? SupportAttachmentPublicId, Guid? StatementLinePublicId, DateOnly? ConfirmedOn);

public sealed record CuentaDeRecaudoDto(Guid PublicId, string Code, string BankName, string? BankTransferCode, string AccountType, string AccountNumberMasked,
    Guid? BranchPublicId, bool IsCollection, bool IsActive);

public sealed record RemesarDesdeBovedaRequest(Guid OwnBankAccountPublicId, IReadOnlyList<DenominacionContadaRequest> Denominations, string BagSeal,
    Guid MinutesAttachmentPublicId, Guid? SecondCustodianUserPublicId, string? Reason);

public sealed record RemesaDeBovedaDto(MovimientoDeBovedaDto Movement, Guid RemittancePublicId, string RemittanceNumber);

/// <summary>
/// Cliente tipado de las chequeras, los cheques girados y las remesas (feature 015, US19, T0230; api.md §11, §12) y de la remesa que despacha la bóveda de Recaudo. Las
/// escrituras llevan <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class ChequesYRemesasClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/treasury";

    public Task<ResultadoDeInventario<IReadOnlyList<ChequeraDto>>> ChequerasAsync(Guid? cuenta = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ChequeraDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/checkbooks", EnvioDeVentas.Query(("account", EnvioDeVentas.Texto(cuenta)))), null, null, ct);

    public Task<ResultadoDeInventario<ChequeraDto>> RegistrarChequeraAsync(RegistrarChequeraRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ChequeraDto>(HttpMethod.Post, $"{Base}/checkbooks", r, clave, ct);

    public Task<ResultadoDeInventario<ChequeraDto>> AnularChequeraAsync(Guid chequera, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ChequeraDto>(HttpMethod.Post, $"{Base}/checkbooks/{chequera}/void", new MotivoDeTesoreriaRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<ChequeGiradoDto>> DanadoAsync(Guid chequera, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ChequeGiradoDto>(HttpMethod.Post, $"{Base}/checkbooks/{chequera}/spoiled", new MotivoDeTesoreriaRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ChequeGiradoDto>>> ChequesAsync(Guid? chequera = null, int? estado = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ChequeGiradoDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/issued-checks",
            EnvioDeVentas.Query(("checkbook", EnvioDeVentas.Texto(chequera)), ("state", EnvioDeVentas.Texto(estado)))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ChequeGiradoDto>>> EmitirDelLoteAsync(Guid lote, EmitirChequesRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ChequeGiradoDto>>(HttpMethod.Post, $"{Base}/issued-checks/batches/{lote}", r, clave, ct);

    public Task<ResultadoDeInventario<ChequeGiradoDto>> EntregarAsync(Guid cheque, EntregarChequeRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ChequeGiradoDto>(HttpMethod.Post, $"{Base}/issued-checks/{cheque}/deliver", r, clave, ct);

    public Task<ResultadoDeInventario<ChequeGiradoDto>> CobradoAsync(Guid cheque, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ChequeGiradoDto>(HttpMethod.Post, $"{Base}/issued-checks/{cheque}/cashed", null, clave, ct);

    public Task<ResultadoDeInventario<ChequeGiradoDto>> AnularChequeAsync(Guid cheque, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ChequeGiradoDto>(HttpMethod.Post, $"{Base}/issued-checks/{cheque}/void", new MotivoDeTesoreriaRequest(motivo), clave, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DocumentoAsync(Guid cheque, CancellationToken ct = default) =>
        EnvioDeVentas.DescargarAsync(http, auth, $"{Base}/issued-checks/{cheque}/document", "cheque.pdf", ct);

    public Task<ResultadoDeInventario<IReadOnlyList<RemesaDto>>> RemesasAsync(int? estado = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<RemesaDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/remittances", EnvioDeVentas.Query(("state", EnvioDeVentas.Texto(estado)))), null, null, ct);

    public Task<ResultadoDeInventario<RemesaDto>> ConfirmarRemesaAsync(Guid remesa, ConfirmarRemesaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<RemesaDto>(HttpMethod.Post, $"{Base}/remittances/{remesa}/confirm", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CuentaDeRecaudoDto>>> CuentasDeRecaudoAsync(Guid? oficina = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CuentaDeRecaudoDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery("/api/receipts/own-bank-accounts",
            EnvioDeVentas.Query(("branch", EnvioDeVentas.Texto(oficina)))), null, null, ct);

    public Task<ResultadoDeInventario<RemesaDeBovedaDto>> RemesarAsync(Guid boveda, RemesarDesdeBovedaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<RemesaDeBovedaDto>(HttpMethod.Post, $"/api/receipts/vaults/{boveda}/remittances", r, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeRecaudo.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, null, ct);
}
