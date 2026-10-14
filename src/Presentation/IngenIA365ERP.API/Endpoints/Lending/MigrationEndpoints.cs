using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las cargas de migración de Cartera (feature 013, T0234, C107; US8, T0755; contracts/api.md §19.1, §19.2) bajo
/// <c>/api/lending/migration/loads</c>:
/// <list type="bullet">
/// <item>listar, ver, crear el borrador con su corte y aplicar (<c>Lending.Migration.Load</c>); aplicar <b>sólo guarda la carga</b>;</item>
/// <item>por parte, <c>{id}/parts/{part}/template.xlsx</c> y <c>{id}/parts/{part}/import?mode=review|apply[&amp;replace=true]</c> por
/// <see cref="RutasDePlantilla.MapPlantillaPorRuta"/>: la parte sale de la ruta (<see cref="DefinicionesDeMigracion.Buscar"/>; una desconocida es
/// <c>Lending.Template.NotFound</c>), un archivo por tanda (C64), hasta 16 MB más el multipart. <c>replace</c> se lee de la query o del formulario.
/// La descarga con <c>?withData=true</c> exige además <c>Lending.Reports.ExportPersonalData</c>; como estas partes no tienen consulta de datos
/// todavía, responde el 404 de lo inexistente;</item>
/// <item>el cuadre (<c>{id}/balance-check</c>, <c>Lending.Reconciliation.View</c>, sin documentos ni nombres), la conciliación de ventas
/// (<c>{id}/reconciliation</c>) y sus marcas (<c>{id}/reconciliation/marks</c>), y la aceptación de diferencias con el punto del acta
/// (<c>{id}/differences/accept</c>, <c>Lending.Migration.AcceptDifferences</c>).</item>
/// </list>
/// Las escrituras exigen <c>Idempotency-Key</c>; una carga que no existe responde el 404 genérico (<c>Lending.MigrationLoad.NotFound</c>). Va sobre
/// <see cref="LendingGroup"/> (auditoría de Cartera). Carter registra el módulo solo. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class MigrationEndpoints : ICarterModule
{
    public const string Permiso = "Lending.Migration.Load";
    public const string VerCuadre = "Lending.Reconciliation.View";
    public const string AceptarDiferencias = "Lending.Migration.AcceptDifferences";
    public const string ExportarDatosPersonales = "Lending.Reports.ExportPersonalData";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // US17 (T0312): sobre el grupo base de Cartera, que suma la auditoría del intento denegado y del rechazo de forma.
        var cargas = LendingGroup.Grupo(app, "/migration/loads", "Cartera Migración");

        cargas.MapGet("/", async (ISender sender, CancellationToken ct) => await sender.Send(new ListMigrationLoadsQuery(), ct))
            .WithName("Lending_MigrationLoads_List")
            .RequirePermission(Permiso);

        cargas.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetMigrationLoadQuery(id), ct))
            .WithName("Lending_MigrationLoads_Get")
            .RequirePermission(Permiso);

        cargas.MapPost("/", async (CrearCargaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateMigrationLoadCommand(body.CutoffDate, body.Notes) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/lending/migration/loads/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Lending_MigrationLoads_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Permiso);

        cargas.MapPost("/{id:guid}/apply", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApplyMigrationLoadCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_MigrationLoads_Apply")
            .ConClaveDeOperacion()
            .RequirePermission(Permiso);

        // US8 (T0755): las 17 partes, una plantilla por parte; la clave de la parte sale de la ruta.
        var parte = LendingGroup.Grupo(app, "/migration/loads/{id:guid}/parts/{part}", "Cartera Migración");
        parte.MapPlantillaPorRuta(
            http => Parte(http) is { } p ? Result.Success(p.Definicion) : Result.Failure<IngenIA365ERP.Application.Common.Imports.DefinicionDePlantilla>(
                ErroresDeGobierno.PlantillaInexistente()),
            http => $"plantilla-cartera-migracion-{Parte(http)?.Parte ?? "parte"}",
            Permiso,
            Permiso,
            "Lending_MigrationLoads_Parts",
            ExportarDatosPersonales,
            AuditEventTypes.LendingReportExported,
            importar: (http, modo, archivo, motivo, clave) => new ImportMigrationPartCommand(
                Carga(http), Parte(http)!.Parte, modo, archivo, EsVerdad(http.Request.Query["replace"].ToString()), motivo ?? string.Empty)
            {
                OperationKey = clave,
            },
            datosPersonales: true,
            camposDelFormulario: (comando, formulario) => comando is ImportMigrationPartCommand c && EsVerdad(formulario["replace"].ToString())
                ? c with { Replace = true }
                : comando);

        cargas.MapGet("/{id:guid}/balance-check", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMigrationBalanceCheckQuery(id), ct))
            .WithName("Lending_MigrationLoads_BalanceCheck")
            .RequirePermission(VerCuadre);

        cargas.MapGet("/{id:guid}/reconciliation", async (Guid id, [FromQuery] SaleReconciliationOutcome? classification, [FromQuery] int? page,
                [FromQuery] int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMigrationSalesReconciliationQuery(id, classification, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_MigrationLoads_Reconciliation")
            .RequirePermission(Permiso);

        cargas.MapPost("/{id:guid}/reconciliation/marks", async (Guid id, MarcaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new MarkMigrationSaleCommand(id, body.MessagePublicId, body.Classification, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_MigrationLoads_MarkSale")
            .ConClaveDeOperacion()
            .RequirePermission(Permiso);

        cargas.MapPost("/{id:guid}/differences/accept", async (Guid id, AceptarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new AcceptMigrationDifferencesCommand(id, body.DifferencePublicIds ?? [], body.SessionItemPublicId ?? Guid.Empty,
                    body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_MigrationLoads_AcceptDifferences")
            .ConClaveDeOperacion()
            .RequirePermission(AceptarDiferencias);
    }

    /// <summary>La parte de la ruta (<c>{part}</c>); nula si no es una de las 17.</summary>
    private static ParteDeMigracion? Parte(HttpContext http) => DefinicionesDeMigracion.Buscar(http.GetRouteValue("part")?.ToString());

    private static Guid Carga(HttpContext http) => Guid.TryParse(http.GetRouteValue("id")?.ToString(), out var id) ? id : Guid.Empty;

    private static bool EsVerdad(string? valor) => bool.TryParse(valor, out var b) && b;

    /// <summary>El cuerpo del alta (§19.1, <c>{ cutoffDate, notes? }</c>).</summary>
    public sealed record CrearCargaRequest(DateOnly CutoffDate, string? Notes);

    /// <summary>El cuerpo de aplicar (§19.1, <c>{ reason }</c>).</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary>El cuerpo de la marca (§19.2, <c>{ messagePublicId, classification, reason }</c>).</summary>
    public sealed record MarcaRequest(Guid MessagePublicId, SaleReconciliationOutcome Classification, string? Reason);

    /// <summary>El cuerpo de la aceptación (§19.2, <c>{ differencePublicIds[], sessionItemPublicId, reason }</c>).</summary>
    public sealed record AceptarRequest(IReadOnlyList<Guid>? DifferencePublicIds, Guid? SessionItemPublicId, string? Reason);
}
