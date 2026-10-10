using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las personas vinculadas (feature 013, T0313, FR-040; contracts/api.md §3.7, plantillas.md §2): ver (auditado como
/// <c>Lending.RelatedParty.Viewed</c>, trae documentos: <c>Lending.RelatedParties.Read</c>, nunca un <c>.View</c>), registrar y terminar
/// vínculos y la plantilla <c>lending.related-parties</c> por <c>RutasDePlantilla.MapPlantilla</c>
/// (la vacía con <c>.Read</c>; importar con <c>.Manage</c>, <c>Idempotency-Key</c> y todo o nada; sin descarga con datos en K1). Las
/// escrituras exigen <c>Lending.RelatedParties.Manage</c> e <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class RelatedPartiesEndpoints : ICarterModule
{
    public const string Leer = "Lending.RelatedParties.Read";
    public const string Administrar = "Lending.RelatedParties.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var vinculados = LendingGroup.Grupo(app, "/related-parties", "Cartera Vinculados");

        vinculados.MapGet("/", async ([AsParameters] ListRelatedPartiesQuery query, ISender sender, CancellationToken ct) => await sender.Send(query, ct))
            .WithName("Lending_RelatedParties_List")
            .RequirePermission(Leer);

        vinculados.MapPost("/", async (RegistrarVinculoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RegisterRelatedPartyCommand(
                    body.PersonPublicId, body.Basis, body.ValidFrom, body.LinkedToPersonPublicId, body.KinshipKind, body.KinshipDegree,
                    body.ContributionPercentage, body.ValidTo, body.Source, body.SupportAttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), v => $"/api/lending/related-parties?personPublicId={body.PersonPublicId}"))
            .WithName("Lending_RelatedParties_Register")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        vinculados.MapPost("/{id:guid}/end", async (Guid id, TerminarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new EndRelatedPartyCommand(id, body.ValidTo, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_RelatedParties_End")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        // La plantilla 2 (plantillas.md §2): GET /template.xlsx y POST /import?mode=review|apply, con Idempotency-Key.
        vinculados.MapPlantilla(
            () => CatalogoDePlantillasDeCartera.Vinculados,
            "plantilla-cartera-vinculados",
            Leer,
            Administrar,
            "Lending_RelatedParties",
            "Lending.Reports.ExportPersonalData",
            AuditEventTypes.LendingReportExported,
            importar: (modo, archivo, motivo, clave) =>
                new ImportRelatedPartiesCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });
    }

    /// <summary>
    /// El cuerpo del alta (§3.7). <c>source</c> es el soporte descrito (acta de elección, certificado, registro civil); el pariente lleva
    /// clase y grado de parentesco.
    /// </summary>
    public sealed record RegistrarVinculoRequest(
        Guid PersonPublicId,
        RelatedPartyBasis Basis,
        DateOnly ValidFrom,
        Guid? LinkedToPersonPublicId,
        KinshipKind? KinshipKind,
        byte? KinshipDegree,
        decimal? ContributionPercentage,
        DateOnly? ValidTo,
        string? Source,
        Guid? SupportAttachmentPublicId);

    /// <summary>El fin de un vínculo con su motivo.</summary>
    public sealed record TerminarRequest(DateOnly ValidTo, string? Reason);
}
