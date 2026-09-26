using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Compliance.HabeasData.AcceptConsent;
using IngenIA365ERP.Application.Compliance.HabeasData.PublishPolicyVersion;
using IngenIA365ERP.Application.Compliance.HabeasData.Queries;
using IngenIA365ERP.Application.Compliance.HabeasData.RevokeConsent;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T129 — Endpoints REST de habeas data (US7, Ley 1581/2012 CO).
///
/// <para>
/// Dos sub-recursos: <c>/policies</c> (versiones) y <c>/consents</c>
/// (consentimientos por titular). Cada operación exige el permiso
/// específico catalogado en T066 (<c>Compliance.HabeasData.*</c>).
/// </para>
/// </summary>
public sealed class HabeasDataModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var policies = app.MapGroup("/api/compliance/habeas-data/policies")
            .WithTags("Compliance / Habeas Data")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        policies.MapGet("/", ListPoliciesAsync)
            .WithName("HabeasData_ListPolicies")
            .RequirePermission("Compliance.HabeasData.ViewHistory");

        // Feature 012 (T46, T175): la política que el POS y Compras muestran al crear una persona. La pide quien crea
        // personas, no quien administra el registro de consentimientos. Sin política publicada, 404 Generic.NotFound.
        policies.MapGet("/current", CurrentPolicyAsync)
            .WithName("HabeasData_CurrentPolicy")
            .RequirePermission("Core.People.Create");

        policies.MapGet("/{publicId:guid}", GetPolicyAsync)
            .WithName("HabeasData_GetPolicy")
            .RequirePermission("Compliance.HabeasData.ViewHistory");

        policies.MapPost("/", PublishAsync)
            .WithName("HabeasData_PublishPolicy")
            .RequirePermission("Compliance.HabeasData.Publish");

        var consents = app.MapGroup("/api/compliance/habeas-data/consents")
            .WithTags("Compliance / Habeas Data")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        consents.MapPost("/accept", AcceptAsync)
            .WithName("HabeasData_AcceptConsent")
            .RequirePermission("Compliance.HabeasData.RecordConsent");

        consents.MapPost("/revoke", RevokeAsync)
            .WithName("HabeasData_RevokeConsent")
            .RequirePermission("Compliance.HabeasData.Revoke");

        consents.MapGet("/history/{personId:int}", HistoryAsync)
            .WithName("HabeasData_History")
            .RequirePermission("Compliance.HabeasData.ViewHistory");
    }

    private static async Task<object?> ListPoliciesAsync(ISender sender, CancellationToken ct) =>
        await sender.Send(new ListPoliciesQuery(), ct);

    private static async Task<object?> CurrentPolicyAsync(ISender sender, CancellationToken ct) =>
        await sender.Send(new GetCurrentPolicyQuery(), ct);

    private static async Task<object?> GetPolicyAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new GetPolicyByPublicIdQuery(publicId), ct);

    private static async Task<object?> PublishAsync(
        [FromBody] PublishPolicyBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new PublishPolicyVersionCommand(
            body.Title, body.ContentMarkdown, body.EffectiveFrom), ct);

    private static async Task<object?> AcceptAsync(
        [FromBody] AcceptConsentBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new AcceptConsentCommand(body.PersonId, body.Channel, body.Notes), ct);

    private static async Task<object?> RevokeAsync(
        [FromBody] RevokeConsentBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new RevokeConsentCommand(body.PersonId, body.Notes), ct);

    private static async Task<object?> HistoryAsync(
        int personId, ISender sender, CancellationToken ct) =>
        await sender.Send(new ListHabeasDataHistoryQuery(personId), ct);
}

public sealed record PublishPolicyBody(
    string Title, string ContentMarkdown, DateTime EffectiveFrom);

public sealed record AcceptConsentBody(int PersonId, string Channel, string? Notes);

public sealed record RevokeConsentBody(int PersonId, string? Notes);
