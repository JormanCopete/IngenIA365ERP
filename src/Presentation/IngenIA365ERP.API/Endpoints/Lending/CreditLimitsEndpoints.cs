using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los cupos (feature 013, US2, T0408; contracts/api.md §7.1, FR-049, FR-058, T34): la lista y el disponible calculado al leer
/// (<c>Lending.CreditLimits.Read</c>), otorgar, terminar y revalidar (<c>.Assign</c>, que admite monto operativo por rol) y la preferencia del
/// rotativo (<c>Lending.PrepaymentPreferences.Manage</c>). Otorgar responde 201 con el cupo <c>Active</c>, o <c>PendingDecision</c> con su
/// enrutamiento si supera la atribución o la matriz lo lleva a otro nivel (sin error), o <c>Blocked</c> con el aviso
/// <c>Lending.Screening.Blocked</c>. La decisión vive en <see cref="DecisionsEndpoints"/>. Toda escritura lleva <c>Idempotency-Key</c>. Cada
/// ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class CreditLimitsEndpoints : ICarterModule
{
    public const string Leer = "Lending.CreditLimits.Read";
    public const string Asignar = "Lending.CreditLimits.Assign";
    public const string Preferencia = "Lending.PrepaymentPreferences.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var cupos = LendingGroup.Grupo(app, "/credit-limits", "Cartera Cupos");

        cupos.MapGet("/", async (Guid? personPublicId, CreditLimitKind? kind, string? status, Guid? line, int? page, int? pageSize, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ListCreditLimitsQuery(personPublicId, kind, status, line, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_CreditLimits_List")
            .RequirePermission(Leer);

        cupos.MapGet("/availability", async (Guid personPublicId, Guid? creditLinePublicId, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCreditLimitAvailabilityQuery(personPublicId, creditLinePublicId, date), ct))
            .WithName("Lending_CreditLimits_Availability")
            .RequirePermission(Leer);

        cupos.MapPost("/", async (OtorgarCupoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AssignCreditLimitCommand(
                    body.PersonPublicId, body.Kind, body.CreditLinePublicId, body.Origin, body.Amount, body.ValidFrom, body.ValidTo,
                    body.PrepaymentPreference, body.Reason ?? string.Empty, body.ScreeningResultPublicId, body.BureauQueryPublicId, body.Authorizations)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), c => $"/api/lending/credit-limits/{c.PublicId}"))
            .WithName("Lending_CreditLimits_Assign")
            .ConClaveDeOperacion()
            .RequirePermission(Asignar);

        cupos.MapPost("/{id:guid}/prepayment-preference", async (Guid id, PreferenciaRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new ChangePrepaymentPreferenceCommand(id, body.Choice, body.Reason, body.EvidenceAttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), c => $"/api/lending/credit-limits/{c.PublicId}"))
            .WithName("Lending_CreditLimits_PrepaymentPreference")
            .ConClaveDeOperacion()
            .RequirePermission(Preferencia);

        cupos.MapPost("/{id:guid}/end", async (Guid id, TerminarCupoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new EndCreditLimitCommand(id, body.ValidTo, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_CreditLimits_End")
            .ConClaveDeOperacion()
            .RequirePermission(Asignar);

        cupos.MapPost("/{id:guid}/revalidate", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RevalidateCreditLimitCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_CreditLimits_Revalidate")
            .ConClaveDeOperacion()
            .RequirePermission(Asignar);
    }

    /// <summary>El otorgamiento (§7.1). <c>amount</c> sólo en el asignado; el calculado sale de los aportes por la relación de la línea.</summary>
    public sealed record OtorgarCupoRequest(
        Guid PersonPublicId,
        CreditLimitKind Kind,
        Guid? CreditLinePublicId,
        CreditLimitOrigin Origin,
        decimal? Amount,
        DateOnly ValidFrom,
        DateOnly? ValidTo,
        PrepaymentChoice? PrepaymentPreference,
        string? Reason,
        Guid? ScreeningResultPublicId,
        Guid? BureauQueryPublicId,
        IReadOnlyList<AuthorizationInput>? Authorizations);

    /// <summary>La preferencia del rotativo (§7.1): la heredan los créditos que nazcan después.</summary>
    public sealed record PreferenciaRequest(PrepaymentChoice Choice, string? Reason, Guid? EvidenceAttachmentPublicId);

    /// <summary>El fin del cupo con su motivo.</summary>
    public sealed record TerminarCupoRequest(DateOnly ValidTo, string? Reason);

    /// <summary>El motivo de una revalidación.</summary>
    public sealed record MotivoRequest(string? Reason);
}
