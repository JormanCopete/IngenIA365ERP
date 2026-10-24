using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

/// <summary>Un concepto del catálogo de órdenes manuales. Estado: 1 activo, 2 inactivo. (nuevo)</summary>
public sealed record ConceptoDeOrdenManualDto(Guid PublicId, string Code, string Name, decimal MaxAmount, bool RequiresWithholding, string? WithholdingNote, int Status);

/// <summary>
/// Una orden manual. Forma: 1 transferencia, 2 cheque, 3 pago a tercero. Aprobación: 1 espera la primera, 2 espera la segunda, 3 aprobada, 4 rechazada.
/// Estado de la orden: el de la bandeja (1 recibida, 2 pendiente, 3 lista, 4 en lote, … 10 anulada). (nuevo)
/// </summary>
public sealed record OrdenManualDto(Guid PublicId, string Number, Guid ConceptPublicId, string ConceptCode, Guid BeneficiaryPersonPublicId, string BeneficiaryName, int Form,
    decimal GrossAmount, decimal WithholdingAmount, decimal NetAmount, string Concept, DateOnly PayDate, Guid SupportAttachmentPublicId, int ApprovalState, int Status,
    string PreparedBy, string? FirstApprovedBy, string? SecondApprovedBy, string? RejectionReason);

/// <summary>Un firmante o un token. Estado: 1 por aprobar, 2 activo, 3 retiro por aprobar, 4 retirado, 5 rechazado. (nuevo)</summary>
public sealed record FirmanteOTokenDto(Guid PublicId, string Kind, Guid BankAccountPublicId, string BankAccountCode, Guid PersonPublicId, string PersonName,
    string? SignatureClass, decimal? MaxAmountAlone, bool CanSignJointly, string? Role, string? Serial, string? Purpose, DateOnly ValidFrom, DateOnly? ValidTo, int Status,
    Guid MinutesAttachmentPublicId, Guid? PendingApprovalRequestPublicId, string? RetirementReason);

public sealed record GuardarConceptoDeOrdenManualRequest(string Code, string Name, decimal MaxAmount, bool RequiresWithholding, string? WithholdingNote, int Status);

public sealed record PrepararOrdenManualRequest(Guid ConceptPublicId, Guid BeneficiaryPersonPublicId, int Form, decimal GrossAmount, decimal WithholdingAmount, string Concept,
    Guid SupportAttachmentPublicId, Guid? PersonBankAccountPublicId, DateOnly? PayDate);

public sealed record RegistrarFirmanteRequest(Guid PersonPublicId, string SignatureClass, decimal? MaxAmountAlone, bool CanSignJointly, string? Role, Guid MinutesAttachmentPublicId,
    DateOnly? ValidFrom);

public sealed record RegistrarTokenRequest(Guid PersonPublicId, string Serial, string? Purpose, Guid MinutesAttachmentPublicId, DateOnly? ValidFrom);

public sealed record RetirarFirmanteRequest(string Reason, Guid MinutesAttachmentPublicId);

/// <summary>
/// Cliente tipado de las órdenes manuales, su catálogo y los firmantes y tokens de las cuentas (feature 015, T1, fase 34, T0418; api.md §13). Las escrituras llevan
/// <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class OrdenesManualesClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/treasury";

    public Task<ResultadoDeInventario<IReadOnlyList<ConceptoDeOrdenManualDto>>> ConceptosAsync(bool incluirInactivos = false, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ConceptoDeOrdenManualDto>>(HttpMethod.Get, $"{Base}/manual-order-concepts?includeInactive={(incluirInactivos ? "true" : "false")}", null, null, ct);

    public Task<ResultadoDeInventario<ConceptoDeOrdenManualDto>> GuardarConceptoAsync(Guid? concepto, GuardarConceptoDeOrdenManualRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        concepto is { } id
            ? Enviar<ConceptoDeOrdenManualDto>(HttpMethod.Put, $"{Base}/manual-order-concepts/{id}", r, clave, ct)
            : Enviar<ConceptoDeOrdenManualDto>(HttpMethod.Post, $"{Base}/manual-order-concepts", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<OrdenManualDto>>> OrdenesAsync(int? aprobacion = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<OrdenManualDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/manual-orders", EnvioDeVentas.Query(("state", EnvioDeVentas.Texto(aprobacion)))),
            null, null, ct);

    public Task<ResultadoDeInventario<OrdenManualDto>> PrepararAsync(PrepararOrdenManualRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<OrdenManualDto>(HttpMethod.Post, $"{Base}/manual-orders", r, clave, ct);

    public Task<ResultadoDeInventario<OrdenManualDto>> AprobarAsync(Guid orden, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<OrdenManualDto>(HttpMethod.Post, $"{Base}/manual-orders/{orden}/approve", null, clave, ct);

    public Task<ResultadoDeInventario<OrdenManualDto>> RechazarAsync(Guid orden, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<OrdenManualDto>(HttpMethod.Post, $"{Base}/manual-orders/{orden}/reject", new MotivoDeTesoreriaRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<FirmanteOTokenDto>>> FirmantesAsync(Guid cuenta, bool tokens, bool conRetirados = false, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<FirmanteOTokenDto>>(HttpMethod.Get,
            $"{Base}/bank-accounts/{cuenta}/{(tokens ? "tokens" : "signers")}?includeRetired={(conRetirados ? "true" : "false")}", null, null, ct);

    public Task<ResultadoDeInventario<FirmanteOTokenDto>> RegistrarFirmanteAsync(Guid cuenta, RegistrarFirmanteRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<FirmanteOTokenDto>(HttpMethod.Post, $"{Base}/bank-accounts/{cuenta}/signers", r, clave, ct);

    public Task<ResultadoDeInventario<FirmanteOTokenDto>> RegistrarTokenAsync(Guid cuenta, RegistrarTokenRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<FirmanteOTokenDto>(HttpMethod.Post, $"{Base}/bank-accounts/{cuenta}/tokens", r, clave, ct);

    public Task<ResultadoDeInventario<FirmanteOTokenDto>> RetirarAsync(Guid cuenta, FirmanteOTokenDto a, RetirarFirmanteRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<FirmanteOTokenDto>(HttpMethod.Post, $"{Base}/bank-accounts/{cuenta}/{Ruta(a)}/{a.PublicId}/retire", r, clave, ct);

    public Task<ResultadoDeInventario<FirmanteOTokenDto>> DecidirAsync(Guid cuenta, FirmanteOTokenDto a, bool aprobar, string? motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<FirmanteOTokenDto>(HttpMethod.Post, $"{Base}/bank-accounts/{cuenta}/{Ruta(a)}/{a.PublicId}/{(aprobar ? "approve" : "reject")}",
            aprobar ? null : new MotivoDeTesoreriaRequest(motivo ?? string.Empty), clave, ct);

    private static string Ruta(FirmanteOTokenDto a) => a.Kind == "Token" ? "tokens" : "signers";

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeRecaudo.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, null, ct);
}
