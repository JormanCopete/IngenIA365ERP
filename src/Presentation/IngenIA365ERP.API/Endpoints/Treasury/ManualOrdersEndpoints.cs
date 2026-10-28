using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// Las órdenes de pago manuales y su catálogo cerrado, y los firmantes y tokens de cada cuenta propia (feature 015, T1, fase 34, T0418; contracts/api.md §13; FR-208, FR-211).
/// Las rutas de aprobar, rechazar y retirar son (nuevo), DT §2.7. (nuevo)
/// </summary>
public class ManualOrdersEndpoints : ICarterModule
{
    public const string Crear = "Treasury.ManualOrders.Create";
    public const string Aprobar = "Treasury.ManualOrders.Approve";
    public const string AdministrarConceptos = "Treasury.ManualOrders.ManageConcepts";
    public const string AdministrarFirmantes = "Treasury.Signers.Manage";
    public const string AprobarFirmantes = "Treasury.Signers.Approve";
    public const string VerCuentas = "Treasury.BankAccounts.View";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var c = TreasuryGroup.Grupo(app, "/manual-order-concepts", "Tesorería Órdenes manuales");
        c.MapGet("/", async (bool? includeInactive, ISender s, CancellationToken ct) => await s.Send(new ListManualOrderConceptsQuery(includeInactive ?? false), ct))
            .WithName("Treasury_ManualOrderConcepts_List").RequirePermission(new AlgunPermiso(Crear, Aprobar, AdministrarConceptos));
        c.MapPost("/", async (ConceptoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SaveManualOrderConceptCommand(null, body.Code ?? string.Empty, body.Name ?? string.Empty, body.MaxAmount ?? 0m, body.RequiresWithholding ?? false,
                    body.WithholdingNote, body.Status ?? TreasuryCatalogStatus.Active) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_ManualOrderConcepts_Create").ConClaveDeOperacion().RequirePermission(AdministrarConceptos);
        c.MapPut("/{id:guid}", async (Guid id, ConceptoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SaveManualOrderConceptCommand(id, body.Code ?? string.Empty, body.Name ?? string.Empty, body.MaxAmount ?? 0m, body.RequiresWithholding ?? false,
                    body.WithholdingNote, body.Status ?? TreasuryCatalogStatus.Active) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_ManualOrderConcepts_Update").ConClaveDeOperacion().RequirePermission(AdministrarConceptos);

        var m = TreasuryGroup.Grupo(app, "/manual-orders", "Tesorería Órdenes manuales");
        m.MapGet("/", async (ManualOrderApprovalState? state, ISender s, CancellationToken ct) => await s.Send(new ListManualPaymentOrdersQuery(state), ct))
            .WithName("Treasury_ManualOrders_List").RequirePermission(new AlgunPermiso(Crear, Aprobar));
        m.MapPost("/", async (OrdenManualRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                TreasuryGroup.Creado(await s.Send(new CreateManualPaymentOrderCommand(body.ConceptPublicId ?? Guid.Empty, body.BeneficiaryPersonPublicId ?? Guid.Empty,
                    body.Form ?? PaymentForm.Transfer, body.GrossAmount ?? 0m, body.WithholdingAmount ?? 0m, body.Concept ?? string.Empty, body.SupportAttachmentPublicId ?? Guid.Empty,
                    body.PersonBankAccountPublicId, body.PayDate) { OperationKey = http.ClaveDeOperacion() }, ct), o => $"/api/treasury/payment-orders/{o.PublicId}"))
            .WithName("Treasury_ManualOrders_Create").ConClaveDeOperacion().RequirePermission(Crear);
        m.MapPost("/{id:guid}/approve", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ApproveManualPaymentOrderCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_ManualOrders_Approve").ConClaveDeOperacion().RequirePermission(Aprobar);
        m.MapPost("/{id:guid}/reject", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RejectManualPaymentOrderCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_ManualOrders_Reject").ConClaveDeOperacion().RequirePermission(Aprobar);

        var f = TreasuryGroup.Grupo(app, "/bank-accounts", "Tesorería Firmantes y tokens");
        foreach (var (ruta, clase) in new[] { ("signers", FirmantesYTokens.Firmante), ("tokens", FirmantesYTokens.Token) })
        {
            var nombre = clase == FirmantesYTokens.Firmante ? "Signers" : "Tokens";
            f.MapGet($"/{{id:guid}}/{ruta}", async (Guid id, bool? includeRetired, ISender s, CancellationToken ct) =>
                    await s.Send(new ListBankAccountAuthoritiesQuery(id, clase, includeRetired ?? false), ct))
                .WithName($"Treasury_BankAccounts_{nombre}_List").RequirePermission(new AlgunPermiso(VerCuentas, AdministrarFirmantes, AprobarFirmantes));
            f.MapPost($"/{{id:guid}}/{ruta}/{{authorityId:guid}}/retire", async (Guid id, Guid authorityId, RetiroRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                    await s.Send(new RetireBankAccountAuthorityCommand(authorityId, body.Reason ?? string.Empty, body.MinutesAttachmentPublicId ?? Guid.Empty, body.RetiredOn)
                    {
                        OperationKey = http.ClaveDeOperacion(),
                    }, ct))
                .WithName($"Treasury_BankAccounts_{nombre}_Retire").ConClaveDeOperacion().RequirePermission(AdministrarFirmantes);
            f.MapPost($"/{{id:guid}}/{ruta}/{{authorityId:guid}}/approve", async (Guid id, Guid authorityId, HttpContext http, ISender s, CancellationToken ct) =>
                    await s.Send(new DecideBankAccountAuthorityCommand(authorityId, Approve: true) { OperationKey = http.ClaveDeOperacion() }, ct))
                .WithName($"Treasury_BankAccounts_{nombre}_Approve").ConClaveDeOperacion().RequirePermission(AprobarFirmantes);
            f.MapPost($"/{{id:guid}}/{ruta}/{{authorityId:guid}}/reject", async (Guid id, Guid authorityId, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                    await s.Send(new DecideBankAccountAuthorityCommand(authorityId, Approve: false, body.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
                .WithName($"Treasury_BankAccounts_{nombre}_Reject").ConClaveDeOperacion().RequirePermission(AprobarFirmantes);
        }
        f.MapPost("/{id:guid}/signers", async (Guid id, FirmanteRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterBankAccountSignerCommand(id, body.PersonPublicId ?? Guid.Empty, body.SignatureClass ?? string.Empty, body.MaxAmountAlone,
                    body.CanSignJointly ?? true, body.Role, body.MinutesAttachmentPublicId ?? Guid.Empty, body.ValidFrom) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_BankAccounts_Signers_Register").ConClaveDeOperacion().RequirePermission(AdministrarFirmantes);
        f.MapPost("/{id:guid}/tokens", async (Guid id, TokenRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterBankAccountTokenCommand(id, body.PersonPublicId ?? Guid.Empty, body.Serial ?? string.Empty, body.Purpose,
                    body.MinutesAttachmentPublicId ?? Guid.Empty, body.ValidFrom) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_BankAccounts_Tokens_Register").ConClaveDeOperacion().RequirePermission(AdministrarFirmantes);
    }

    public sealed record ConceptoRequest(string? Code, string? Name, decimal? MaxAmount, bool? RequiresWithholding, string? WithholdingNote, TreasuryCatalogStatus? Status);

    public sealed record OrdenManualRequest(Guid? ConceptPublicId, Guid? BeneficiaryPersonPublicId, PaymentForm? Form, decimal? GrossAmount, decimal? WithholdingAmount,
        string? Concept, Guid? SupportAttachmentPublicId, Guid? PersonBankAccountPublicId, DateOnly? PayDate);

    public sealed record MotivoRequest(string? Reason);

    public sealed record RetiroRequest(string? Reason, Guid? MinutesAttachmentPublicId, DateOnly? RetiredOn = null);

    public sealed record FirmanteRequest(Guid? PersonPublicId, string? SignatureClass, decimal? MaxAmountAlone, bool? CanSignJointly, string? Role,
        Guid? MinutesAttachmentPublicId, DateOnly? ValidFrom);

    public sealed record TokenRequest(Guid? PersonPublicId, string? Serial, string? Purpose, Guid? MinutesAttachmentPublicId, DateOnly? ValidFrom);
}
