using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Accounts.GmfExemption;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// La exención del GMF (feature 014, US2, T078; contracts/api.md §10 de la 014), sobre <see cref="LendingGroup"/>: <c>/api/lending/gmf-exemptions</c>
/// —las marcas de una persona, marcar una cuenta de ahorro (o reemplazar la vigente) y terminar una marca—. Leer exige <c>Lending.Accounts.Read</c>;
/// marcar y terminar, <c>Lending.GmfExemptions.Manage</c> con <c>Idempotency-Key</c>. Desde K8 (US4, T123), el uso exento del mes de una persona
/// (<c>/usage</c>).
/// Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class GmfExemptionsEndpoints : ICarterModule
{
    public const string Leer = MemberAccountsEndpoints.Leer;
    public const string Administrar = "Lending.GmfExemptions.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var marcas = LendingGroup.Grupo(app, "/gmf-exemptions", "Cartera Exención del GMF");

        marcas.MapGet("/", async (Guid? personPublicId, bool? active, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListGmfExemptAccountsQuery(personPublicId, active), ct))
            .WithName("Lending_GmfExemptions_List")
            .RequirePermission(Leer);

        marcas.MapGet("/usage", async (Guid personPublicId, int? year, int? month, ISender sender, CancellationToken ct) =>
                await sender.Send(new IngenIA365ERP.Application.Lending.Deposits.Operations.GetGmfMonthlyUsageQuery(personPublicId, year, month), ct))
            .WithName("Lending_GmfExemptions_Usage")
            .RequirePermission(Leer);

        marcas.MapPost("/", async (MarcarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new MarkGmfExemptAccountCommand(body.AccountPublicId, body.ValidFrom, body.Reason ?? string.Empty,
                    body.ReplaceCurrent) { OperationKey = http.ClaveDeOperacion() }, ct), m => $"/api/lending/gmf-exemptions/{m.PublicId}"))
            .WithName("Lending_GmfExemptions_Mark")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        marcas.MapPost("/{id:guid}/end", async (Guid id, TerminarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new EndGmfExemptAccountCommand(id, body.ValidTo, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_GmfExemptions_End")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo de <c>POST /</c>: la cuenta, desde cuándo (hoy si no viene), el motivo y si reemplaza la marca vigente del titular.</summary>
    public sealed record MarcarRequest(Guid AccountPublicId, DateOnly? ValidFrom, string? Reason, bool ReplaceCurrent = false);

    /// <summary>El cuerpo de <c>POST /{id}/end</c>: el último día exento (hoy si no viene) y el motivo.</summary>
    public sealed record TerminarRequest(DateOnly? ValidTo, string? Reason);
}
