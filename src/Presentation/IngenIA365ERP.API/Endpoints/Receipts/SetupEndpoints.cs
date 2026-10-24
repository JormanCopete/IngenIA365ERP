using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Application.Receipts.Vaults;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// La configuración de Recaudo (feature 015, US1, T0076; contracts/api.md §2): puntos, cajas, cajeros, medios por punto, tipos de documento, conceptos varios,
/// talonarios, bóvedas, suplencias y reglas de modalidad por canal. Leer con <c>Receipts.Setup.View</c>, escribir con <c>Receipts.Setup.Manage</c> (las
/// escrituras llevan <c>Idempotency-Key</c>). Es configuración: se admite antes de activar Recaudo. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class SetupEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Receipts.Setup.View";
    public const string PermisoDeConfiguracion = "Receipts.Setup.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        Puntos(ReceiptsGroup.Grupo(app, "/collection-points", "Recaudo Configuración"));
        Cajas(ReceiptsGroup.Grupo(app, "/cash-boxes", "Recaudo Configuración"));
        Catalogos(ReceiptsGroup.Grupo(app, string.Empty, "Recaudo Configuración"));
        Bovedas(ReceiptsGroup.Grupo(app, "/vaults", "Recaudo Configuración"));
    }

    private static void Puntos(RouteGroupBuilder g)
    {
        g.MapGet("/", async (Guid? branch, bool? includeInactive, ISender s, CancellationToken ct) =>
                await s.Send(new ListCollectionPointsQuery(branch, includeInactive ?? false), ct))
            .WithName("Receipts_CollectionPoints_List").RequirePermission(PermisoDeConsulta);

        g.MapPost("/", async (CreateCollectionPointCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/receipts/collection-points/{v.PublicId}"))
            .WithName("Receipts_CollectionPoints_Create").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);

        g.MapPut("/{id:guid}", async (Guid id, PuntoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new UpdateCollectionPointCommand(id, body.Name ?? string.Empty, body.DocumentPrefix, body.IsMainCashBoxVault, body.ValidTo)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_CollectionPoints_Update").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);

        g.MapPost("/{id:guid}/inactivate", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new InactivateCollectionPointCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CollectionPoints_Inactivate").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);

        g.MapGet("/{id:guid}/payment-means", async (Guid id, DateOnly? asOf, ISender s, CancellationToken ct) =>
                await s.Send(new ListPointPaymentMeansQuery(id, asOf), ct))
            .WithName("Receipts_PointPaymentMeans_List").RequirePermission(PermisoDeConsulta);

        g.MapPost("/{id:guid}/payment-means", async (Guid id, OfrecerMedioRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new OfferPaymentMeansCommand(id, body.PaymentMeansPublicId, body.ValidFrom) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_PointPaymentMeans_Offer").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);

        g.MapPost("/{id:guid}/payment-means/{meansId:guid}/retire", async (Guid id, Guid meansId, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RetirePaymentMeansCommand(id, meansId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_PointPaymentMeans_Retire").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);
    }

    private static void Cajas(RouteGroupBuilder g)
    {
        g.MapGet("/", async (Guid? point, bool? includeInactive, ISender s, CancellationToken ct) =>
                await s.Send(new ListCashBoxesQuery(point, includeInactive ?? false), ct))
            .WithName("Receipts_CashBoxes_List").RequirePermission(PermisoDeConsulta);

        g.MapPost("/", async (CreateCashBoxCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/receipts/cash-boxes/{v.PublicId}"))
            .WithName("Receipts_CashBoxes_Create").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);

        g.MapPut("/{id:guid}", async (Guid id, CajaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new UpdateCashBoxCommand(id, body.Name ?? string.Empty, body.BaseMode, body.BaseAmount, body.CashWarnCap, body.CashBlockCap,
                    body.DailyCashOutCap, body.IsMainCashBox, body.ValidTo) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CashBoxes_Update").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);

        g.MapPost("/{id:guid}/inactivate", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new InactivateCashBoxCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_CashBoxes_Inactivate").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);

        g.MapGet("/{id:guid}/cashiers", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new ListCashiersQuery(id), ct))
            .WithName("Receipts_Cashiers_List").RequirePermission(PermisoDeConsulta);

        g.MapPost("/{id:guid}/cashiers", async (Guid id, CajeroRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new AssignCashierCommand(id, body.UserPublicId, body.ValidFrom, body.ValidTo) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Cashiers_Assign").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);

        g.MapPost("/{id:guid}/cashiers/{userId:guid}/retire", async (Guid id, Guid userId, RetiroDeCajeroRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RetireCashierCommand(id, userId, body?.ValidTo) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Cashiers_Retire").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);
    }

    private static void Catalogos(RouteGroupBuilder g)
    {
        g.MapGet("/document-types", async (bool? includeInactive, ISender s, CancellationToken ct) =>
                await s.Send(new ListReceiptDocumentTypesQuery(includeInactive ?? false), ct))
            .WithName("Receipts_DocumentTypes_List").RequirePermission(PermisoDeConsulta);
        g.MapPost("/document-types", async (CreateReceiptDocumentTypeCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/receipts/document-types/{v.PublicId}"))
            .WithName("Receipts_DocumentTypes_Create").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);
        g.MapPut("/document-types/{id:guid}", async (Guid id, TipoDeDocumentoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new UpdateReceiptDocumentTypeCommand(id, body.Name ?? string.Empty, body.UsesPointPrefix, body.Status)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_DocumentTypes_Update").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);

        g.MapGet("/misc-concepts", async (bool? includeInactive, MiscConceptDirection? direction, ISender s, CancellationToken ct) =>
                await s.Send(new ListMiscConceptsQuery(includeInactive ?? false, direction), ct))
            .WithName("Receipts_MiscConcepts_List").RequirePermission(PermisoDeConsulta);
        g.MapPost("/misc-concepts", async (CreateMiscConceptCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/receipts/misc-concepts/{v.PublicId}"))
            .WithName("Receipts_MiscConcepts_Create").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);
        g.MapPut("/misc-concepts/{id:guid}", async (Guid id, ConceptoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new UpdateMiscConceptCommand(id, body.Name ?? string.Empty, body.RequiresThirdParty, body.RequiresCrossDocument, body.RequiresInvoice,
                    body.RequiresApproval, body.MaxAmount) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_MiscConcepts_Update").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);
        g.MapPost("/misc-concepts/{id:guid}/inactivate", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new InactivateMiscConceptCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_MiscConcepts_Inactivate").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);

        g.MapGet("/contingency-books", async (Guid? point, ISender s, CancellationToken ct) => await s.Send(new ListContingencyBooksQuery(point), ct))
            .WithName("Receipts_ContingencyBooks_List").RequirePermission(PermisoDeConsulta);
        g.MapPost("/contingency-books", async (RegisterContingencyBookCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/receipts/contingency-books/{v.PublicId}"))
            .WithName("Receipts_ContingencyBooks_Register").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);
        g.MapPost("/contingency-books/{id:guid}/void", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new VoidContingencyBookCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_ContingencyBooks_Void").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);

        g.MapGet("/supervisor-delegations", async (Guid? point, bool? onlyCurrent, ISender s, CancellationToken ct) =>
                await s.Send(new ListSupervisorDelegationsQuery(point, onlyCurrent ?? false), ct))
            .WithName("Receipts_SupervisorDelegations_List").RequirePermission(PermisoDeConsulta);
        g.MapPost("/supervisor-delegations", async (RegisterSupervisorDelegationCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/receipts/supervisor-delegations/{v.PublicId}"))
            .WithName("Receipts_SupervisorDelegations_Register").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);
        g.MapPost("/supervisor-delegations/{id:guid}/end", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new EndSupervisorDelegationCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_SupervisorDelegations_End").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);

        g.MapGet("/channel-modality-rules", async (bool? includeHistory, ISender s, CancellationToken ct) =>
                await s.Send(new ListChannelModalityRulesQuery(includeHistory ?? false), ct))
            .WithName("Receipts_ChannelModalityRules_List").RequirePermission(PermisoDeConsulta);
        g.MapPut("/channel-modality-rules", async (SetChannelModalityRulesCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_ChannelModalityRules_Set").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);
    }

    private static void Bovedas(RouteGroupBuilder g)
    {
        g.MapGet("/", async (ISender s, CancellationToken ct) => await s.Send(new ListVaultsQuery(), ct))
            .WithName("Receipts_Vaults_List").RequirePermission(PermisoDeConsulta);
        g.MapPost("/", async (CreateVaultCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/receipts/vaults/{v.PublicId}"))
            .WithName("Receipts_Vaults_Create").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);
        g.MapPut("/{id:guid}", async (Guid id, BovedaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new UpdateVaultCommand(id, body.Mode, body.MainCashBoxPublicId, body.Status) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Vaults_Update").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);
        g.MapPost("/{id:guid}/custodians", async (Guid id, CustodiosRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SetVaultCustodiansCommand(id, body.UserPublicIds ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Vaults_Custodians").ConClaveDeOperacion().RequirePermission(PermisoDeConfiguracion);
    }

    public sealed record PuntoRequest(string? Name, string? DocumentPrefix, bool IsMainCashBoxVault, DateOnly? ValidTo);

    public sealed record CajaRequest(string? Name, CashBoxBaseMode BaseMode, decimal BaseAmount, decimal? CashWarnCap, decimal? CashBlockCap,
        decimal? DailyCashOutCap, bool IsMainCashBox, DateOnly? ValidTo);

    public sealed record CajeroRequest(Guid UserPublicId, DateOnly? ValidFrom, DateOnly? ValidTo);

    public sealed record RetiroDeCajeroRequest(DateOnly? ValidTo);

    public sealed record OfrecerMedioRequest(Guid PaymentMeansPublicId, DateOnly? ValidFrom);

    public sealed record TipoDeDocumentoRequest(string? Name, bool UsesPointPrefix, ReceiptsCatalogStatus Status);

    public sealed record ConceptoRequest(string? Name, bool RequiresThirdParty, bool RequiresCrossDocument, bool RequiresInvoice, bool RequiresApproval,
        decimal? MaxAmount);

    public sealed record MotivoRequest(string? Reason);

    public sealed record BovedaRequest(VaultMode Mode, Guid? MainCashBoxPublicId, ReceiptsCatalogStatus Status);

    public sealed record CustodiosRequest(IReadOnlyList<Guid>? UserPublicIds);
}
