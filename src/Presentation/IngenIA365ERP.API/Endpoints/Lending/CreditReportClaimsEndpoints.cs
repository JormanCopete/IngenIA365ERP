using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Collections.Claims;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los reclamos y suplantaciones sobre el reporte a centrales (feature 013, T0313, T69, FR-006, C88; contracts/api.md §14.4). En K1 la lectura de lo
/// migrado, con <c>Lending.CreditReportClaims.Read</c> y el alcance por la oficina del crédito. K4 (T0956) suma registrar, prorrogar (una vez),
/// responder y desistir (<c>Lending.CreditReportClaims.Manage</c>, con <c>Idempotency-Key</c>). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class CreditReportClaimsEndpoints : ICarterModule
{
    public const string Leer = "Lending.CreditReportClaims.Read";
    public const string Administrar = "Lending.CreditReportClaims.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var reclamos = LendingGroup.Grupo(app, "/credit-report-claims", "Cartera Reclamos");

        reclamos.MapGet("/", async ([AsParameters] ListCreditReportClaimsQuery query, ISender sender, CancellationToken ct) => await sender.Send(query, ct))
            .WithName("Lending_CreditReportClaims_List")
            .RequirePermission(Leer);

        reclamos.MapPost("/", async (ReclamoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RegisterCreditReportClaimCommand(body.PersonPublicId, body.Kind, body.ReceivedAt,
                    body.Channel ?? string.Empty, body.Description ?? string.Empty, body.LoanPublicId, body.AttachmentPublicIds, body.BureauCode,
                    body.DataSubjectRequestPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), c => $"/api/lending/credit-report-claims/{c.PublicId}"))
            .WithName("Lending_CreditReportClaims_Register")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        reclamos.MapPost("/{id:guid}/extend", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ExtendCreditReportClaimCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_CreditReportClaims_Extend")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        reclamos.MapPost("/{id:guid}/answer", async (Guid id, RespuestaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new AnswerCreditReportClaimCommand(id, body.Answer ?? string.Empty, body.UpholdsClaim, body.AttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_CreditReportClaims_Answer")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        reclamos.MapPost("/{id:guid}/withdraw", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new WithdrawCreditReportClaimCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_CreditReportClaims_Withdraw")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El reclamo del titular (§14.4): el plazo se calcula en días hábiles nacionales.</summary>
    public sealed record ReclamoRequest(
        Guid PersonPublicId,
        CreditReportClaimKind Kind,
        DateOnly ReceivedAt,
        string? Channel,
        string? Description,
        Guid? LoanPublicId = null,
        IReadOnlyList<Guid>? AttachmentPublicIds = null,
        string? BureauCode = null,
        Guid? DataSubjectRequestPublicId = null);

    /// <summary>La respuesta al titular; <c>upholdsClaim</c> dice si se le da la razón.</summary>
    public sealed record RespuestaRequest(string? Answer, bool UpholdsClaim, Guid? AttachmentPublicId = null);

    /// <summary>Un motivo.</summary>
    public sealed record MotivoRequest(string? Reason);
}
