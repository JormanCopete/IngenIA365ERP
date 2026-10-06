using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Compliance.RestrictiveLists;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Compliance;

/// <summary>
/// Las listas restrictivas (feature 013, T70, T0087; contracts/api.md §22.1): cargas y entradas con
/// <c>Compliance.RestrictiveLists.View</c>; la plantilla <c>compliance.restrictive-lists</c> con <c>.View</c> y la importación (revisar o
/// aplicar, con <c>Idempotency-Key</c>) con <c>.Load</c>, por <see cref="RutasDePlantilla.MapPlantillaDePlataforma"/>. Las entradas son de
/// la lista pública, no personas del maestro (C84). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public sealed class RestrictiveListsEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Compliance.RestrictiveLists.View";
    public const string PermisoDeCarga = "Compliance.RestrictiveLists.Load";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var listas = app.MapGroup("/api/compliance/restrictive-lists")
            .WithTags("Compliance / Listas restrictivas")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        listas.MapGet("/", async ([FromQuery] string? listCode, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListRestrictiveListLoadsQuery(listCode), ct))
            .WithName("Compliance_RestrictiveLists_List")
            .RequirePermission(PermisoDeConsulta);

        listas.MapGet("/{id:guid}/entries", async (Guid id, [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize,
                    ISender sender, CancellationToken ct) =>
                await sender.Send(new ListRestrictiveListEntriesQuery(id, search, page ?? 1, pageSize ?? 50), ct))
            .WithName("Compliance_RestrictiveLists_Entries")
            .RequirePermission(PermisoDeConsulta);

        listas.MapPlantillaDePlataforma(PlantillaDeListasRestrictivas.Clave, "Compliance_RestrictiveLists",
            (modo, archivo, motivo, clave) => new ImportRestrictiveListCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });
    }
}
