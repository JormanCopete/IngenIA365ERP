using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Migration;
using IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// Migración y activación de aportes y captaciones (feature 014, T055 y US9, T158; contracts/api.md §16 de la 014), sobre <see cref="LendingGroup"/>:
/// <list type="bullet">
/// <item><c>/api/lending/deposits/migration/loads</c>: listar (con <c>?nature=</c>), ver, crear el borrador de una naturaleza con su corte y aplicar
/// (<c>Lending.Deposits.Migration</c>); aplicar <b>sólo guarda la carga</b>;</item>
/// <item>por parte, <c>{id}/parts/{part}/template.xlsx</c> (con <c>?withData=true</c> las filas cargadas, y además
/// <c>&amp;source=contribution-balances</c> en <c>contribution-accounts</c> «Tomar de los saldos cargados»; con datos exige también
/// <c>Lending.Reports.ExportPersonalData</c>) y <c>{id}/parts/{part}/import?mode=review|apply[&amp;replace=true]</c>, por
/// <see cref="RutasDePlantilla.MapPlantillaPorRuta"/>;</item>
/// <item>el cuadre (<c>{id}/balance-check</c>, <c>Lending.Reconciliation.View</c>, sin titulares), las cifras de referencia (<c>{id}/reference-figures</c>,
/// GET y PUT) y la aceptación de diferencias con el punto del acta (<c>{id}/differences/accept</c>, <c>Lending.Migration.AcceptDifferences</c>);</item>
/// <item><c>/api/lending/deposits/activation</c>: el estado (<c>GET ?nature=</c>) y la activación con o sin carga (<c>POST</c>,
/// <c>Lending.Deposits.Activate</c>).</item>
/// </list>
/// Las escrituras exigen <c>Idempotency-Key</c>; una carga que no existe o es de créditos responde el 404 genérico (<c>Lending.MigrationLoad.NotFound</c>).
/// Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class DepositsMigrationEndpoints : ICarterModule
{
    public const string PermisoDeActivacion = "Lending.Deposits.Activate";
    public const string PermisoDeMigracion = DefinicionesDeMigracionDeCaptaciones.Permiso;
    public const string VerCuadre = "Lending.Reconciliation.View";
    public const string AceptarDiferencias = "Lending.Migration.AcceptDifferences";
    public const string ExportarDatosPersonales = DefinicionesDeMigracionDeCaptaciones.PermisoDeDatosPersonales;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var captaciones = LendingGroup.Grupo(app, "/deposits", "Cartera Captaciones: migración y activación");

        captaciones.MapGet("/activation", async ([FromQuery] LineNature nature, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDepositsActivationStatusQuery(nature), ct))
            .WithName("Lending_Deposits_ActivationStatus")
            .RequirePermission(PermisoDeActivacion);

        captaciones.MapPost("/activation", ActivarAsync)
            .WithName("Lending_Deposits_Activate")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeActivacion);

        var cargas = LendingGroup.Grupo(app, "/deposits/migration/loads", "Cartera Captaciones: migración");

        cargas.MapGet("/", async ([FromQuery] LineNature? nature, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDepositMigrationLoadsQuery(nature), ct))
            .WithName("Lending_Deposits_MigrationLoads_List")
            .RequirePermission(PermisoDeMigracion);

        cargas.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetDepositMigrationLoadQuery(id), ct))
            .WithName("Lending_Deposits_MigrationLoads_Get")
            .RequirePermission(PermisoDeMigracion);

        cargas.MapPost("/", CrearAsync)
            .WithName("Lending_Deposits_MigrationLoads_Create")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeMigracion);

        cargas.MapPost("/{id:guid}/apply", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApplyDepositMigrationLoadCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Deposits_MigrationLoads_Apply")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeMigracion);

        // Una plantilla por parte; la clave de la parte sale de la ruta (lending.{part}).
        var parte = LendingGroup.Grupo(app, "/deposits/migration/loads/{id:guid}/parts/{part}", "Cartera Captaciones: migración");
        parte.MapPlantillaPorRuta(
            http => Parte(http) is { Definicion: { } d } ? Result.Success(d) : Result.Failure<DefinicionDePlantilla>(ErroresDeGobierno.PlantillaInexistente()),
            http => $"plantilla-captaciones-migracion-{Parte(http)?.Parte ?? "parte"}",
            PermisoDeMigracion,
            PermisoDeMigracion,
            "Lending_Deposits_MigrationLoads_Parts",
            ExportarDatosPersonales,
            AuditEventTypes.LendingReportExported,
            importar: (http, modo, archivo, motivo, clave) => new ImportDepositMigrationPartCommand(
                Carga(http), Parte(http)!.Parte, modo, archivo, EsVerdad(http.Request.Query["replace"].ToString()), motivo ?? string.Empty)
            {
                OperationKey = clave,
            },
            datos: http => new GetDepositMigrationPartDataQuery(Carga(http), Parte(http)?.Parte ?? string.Empty,
                string.Equals(http.Request.Query["source"].ToString(), "contribution-balances", StringComparison.OrdinalIgnoreCase)),
            datosPersonales: true,
            camposDelFormulario: (comando, formulario) => comando is ImportDepositMigrationPartCommand c && EsVerdad(formulario["replace"].ToString())
                ? c with { Replace = true }
                : comando);

        cargas.MapGet("/{id:guid}/balance-check", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDepositMigrationBalanceCheckQuery(id), ct))
            .WithName("Lending_Deposits_MigrationLoads_BalanceCheck")
            .RequirePermission(VerCuadre);

        cargas.MapGet("/{id:guid}/reference-figures", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDepositReferenceFiguresQuery(id), ct))
            .WithName("Lending_Deposits_MigrationLoads_ReferenceFigures")
            .RequirePermission(PermisoDeMigracion);

        cargas.MapPut("/{id:guid}/reference-figures", async (Guid id, CifrasRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetDepositReferenceFiguresCommand(id, body.Figures ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Deposits_MigrationLoads_SetReferenceFigures")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeMigracion);

        cargas.MapPost("/{id:guid}/differences/accept", async (Guid id, AceptarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new AcceptMigrationDifferencesCommand(id, body.DifferencePublicIds ?? [], body.SessionItemPublicId ?? Guid.Empty,
                    body.Reason ?? string.Empty, DeCaptaciones: true)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Deposits_MigrationLoads_AcceptDifferences")
            .ConClaveDeOperacion()
            .RequirePermission(AceptarDiferencias);
    }

    /// <summary>Crea el borrador de una carga (201 con su ubicación).</summary>
    private static async Task<object> CrearAsync(CrearCargaRequest body, HttpContext http, ISender sender, CancellationToken ct)
    {
        var r = await sender.Send(new CreateDepositMigrationLoadCommand(body.Nature, body.CutoffDate, body.Notes) { OperationKey = http.ClaveDeOperacion() }, ct);
        return r.IsSuccess ? Results.Created($"/api/lending/deposits/migration/loads/{r.Value.PublicId}", r.Value) : r;
    }

    /// <summary>Reenvía al comando de la naturaleza: captaciones (<c>Deposit</c>) o aportes (<c>Contribution</c>).</summary>
    private static async Task<object> ActivarAsync(ActivarRequest body, HttpContext http, ISender sender, CancellationToken ct)
    {
        Result<DepositsActivationDto> r = body.Nature switch
        {
            LineNature.Deposit => await sender.Send(new ActivateDepositsCommand(body.ActivationDate, body.Reason ?? string.Empty,
                body.MigrationLoadPublicId, body.SessionItemPublicId, body.AcknowledgeWarnings) { OperationKey = http.ClaveDeOperacion() }, ct),
            LineNature.Contribution => await sender.Send(new ActivateContributionsCommand(body.ActivationDate, body.Reason ?? string.Empty,
                body.MigrationLoadPublicId, body.SessionItemPublicId, body.AcknowledgeWarnings) { OperationKey = http.ClaveDeOperacion() }, ct),
            _ => Result.Failure<DepositsActivationDto>(new ErrorConDatos("Validation.Invalid",
                "Indique qué naturaleza activa: Deposit (captaciones) o Contribution (aportes).", new { field = "nature" })),
        };
        return r.IsSuccess ? Results.Created("/api/lending/deposits/activation", r.Value) : r;
    }

    /// <summary>La parte de la ruta (<c>{part}</c>); nula si no es una de las partes de captaciones.</summary>
    private static ParteDeMigracionDeCaptaciones? Parte(HttpContext http) => DefinicionesDeMigracionDeCaptaciones.Buscar(http.GetRouteValue("part")?.ToString());

    private static Guid Carga(HttpContext http) => Guid.TryParse(http.GetRouteValue("id")?.ToString(), out var id) ? id : Guid.Empty;

    private static bool EsVerdad(string? valor) => bool.TryParse(valor, out var b) && b;

    /// <summary>El cuerpo de la activación (api.md §16): <c>{ nature, migrationLoadPublicId?, activationDate, sessionItemPublicId?, acknowledgeWarnings, reason }</c>.</summary>
    public sealed record ActivarRequest(
        LineNature Nature,
        Guid? MigrationLoadPublicId,
        DateOnly ActivationDate,
        Guid? SessionItemPublicId,
        bool AcknowledgeWarnings,
        string? Reason);

    /// <summary>El cuerpo del alta (§16, <c>{ nature, cutoffDate, notes? }</c>).</summary>
    public sealed record CrearCargaRequest(LineNature Nature, DateOnly CutoffDate, string? Notes);

    /// <summary>El cuerpo de aplicar (§16, <c>{ reason }</c>).</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary>El cuerpo de las cifras de referencia (§16, <c>{ figures: [{ lineCode, concept, amount, source }] }</c>).</summary>
    public sealed record CifrasRequest(IReadOnlyList<CifraDeReferenciaDto>? Figures);

    /// <summary>El cuerpo de la aceptación (§16, <c>{ differencePublicIds[], sessionItemPublicId, reason }</c>).</summary>
    public sealed record AceptarRequest(IReadOnlyList<Guid>? DifferencePublicIds, Guid? SessionItemPublicId, string? Reason);
}
