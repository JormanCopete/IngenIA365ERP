using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Collections.Legal;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// El cobro jurídico (feature 013, K4, US12, T0956; contracts/api.md §14.8, FR-124): la lista (<c>Lending.Collections.Read</c>) y abrir, registrar
/// actuaciones con adjuntos y cerrar (<c>Lending.LegalProcesses.Manage</c>, con <c>Idempotency-Key</c>). El abogado es una persona del maestro; una
/// insolvencia abierta suspende el proceso. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class LegalProcessesEndpoints : ICarterModule
{
    public const string Leer = "Lending.Collections.Read";
    public const string Administrar = "Lending.LegalProcesses.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var procesos = LendingGroup.Grupo(app, "/legal-processes", "Cartera Cobro jurídico");

        procesos.MapGet("/", async ([AsParameters] ListLegalProcessesQuery query, ISender sender, CancellationToken ct) => await sender.Send(query, ct))
            .WithName("Lending_LegalProcesses_List")
            .RequirePermission(Leer);

        procesos.MapPost("/", async (AbrirRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new OpenLegalProcessCommand(body.LoanPublicIds ?? [], body.LawyerPersonPublicId, body.Court,
                    body.FileNumber, body.StartDate, body.ProcessType, body.FeeAgreementJson)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), "/api/lending/legal-processes"))
            .WithName("Lending_LegalProcesses_Open")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        procesos.MapPost("/{id:guid}/actions", async (Guid id, ActuacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RecordLegalProcessActionCommand(id, body.Date, body.Kind, body.Description ?? string.Empty,
                    body.Amount, body.ChargeableToDebtor, body.AttachmentPublicIds)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), "/api/lending/legal-processes"))
            .WithName("Lending_LegalProcesses_RecordAction")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        procesos.MapPost("/{id:guid}/close", async (Guid id, CerrarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CloseLegalProcessCommand(id, body.Outcome, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_LegalProcesses_Close")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>Abrir el proceso sobre uno o varios créditos del mismo deudor (§14.8).</summary>
    public sealed record AbrirRequest(
        IReadOnlyList<Guid>? LoanPublicIds,
        Guid LawyerPersonPublicId,
        string? Court,
        string? FileNumber,
        DateOnly StartDate,
        string? ProcessType = null,
        string? FeeAgreementJson = null);

    /// <summary>Una actuación; honorarios o gastos <c>ChargeableToDebtor</c> siguen la regla de gastos de cobranza.</summary>
    public sealed record ActuacionRequest(
        DateOnly Date,
        LegalActionKind Kind,
        string? Description,
        decimal? Amount = null,
        bool ChargeableToDebtor = false,
        IReadOnlyList<Guid>? AttachmentPublicIds = null);

    /// <summary>Cerrar el proceso, concluido o retirado.</summary>
    public sealed record CerrarRequest(LegalProcessStatus Outcome, string? Reason);
}
