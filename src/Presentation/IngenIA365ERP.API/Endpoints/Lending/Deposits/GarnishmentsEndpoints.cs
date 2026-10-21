using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Holds;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// Los embargos y los bloqueos de las cuentas (feature 014, US11, T191; contracts/api.md §8 y §4 <c>/holds</c> de la 014; research E161), sobre
/// <see cref="LendingGroup"/>: <c>/api/lending/garnishments</c> —lista, detalle con la explicación del límite inembargable, registrar, borrador y registro de
/// respuestas al juzgado, consignar y levantar— y <c>/api/lending/accounts/{id}/holds</c> —los bloqueos de una cuenta, poner y levantar—. Leer exige
/// <c>Lending.Accounts.Read</c>; registrar, responder y consignar <c>Lending.Garnishments.Register</c>; levantar <c>Lending.Garnishments.Release</c>
/// (quien registró no levanta); los bloqueos <c>Lending.Holds.Manage</c>. Toda escritura con <c>Idempotency-Key</c>. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class GarnishmentsEndpoints : ICarterModule
{
    public const string Leer = MemberAccountsEndpoints.Leer;
    public const string Registrar = "Lending.Garnishments.Register";
    public const string Levantar = "Lending.Garnishments.Release";
    public const string AdministrarBloqueos = "Lending.Holds.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var embargos = LendingGroup.Grupo(app, "/garnishments", "Cartera Embargos");

        embargos.MapGet("/", async (Guid? personPublicId, GarnishmentStatus? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListGarnishmentsQuery(personPublicId, status, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_Garnishments_List")
            .RequirePermission(Leer);

        embargos.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetGarnishmentQuery(id), ct))
            .WithName("Lending_Garnishments_Get")
            .RequirePermission(Leer);

        embargos.MapPost("/", async (RegisterGarnishmentInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RegisterGarnishmentCommand(body) { OperationKey = http.ClaveDeOperacion() }, ct),
                    g => $"/api/lending/garnishments/{g.PublicId}"))
            .WithName("Lending_Garnishments_Register")
            .ConClaveDeOperacion()
            .RequirePermission(Registrar);

        embargos.MapGet("/{id:guid}/responses/draft", async (Guid id, GarnishmentResponseKind kind, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetGarnishmentResponseDraftQuery(id, kind), ct))
            .WithName("Lending_Garnishments_ResponseDraft")
            .RequirePermission(Leer);

        embargos.MapPost("/{id:guid}/responses", async (Guid id, RespuestaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RegisterGarnishmentResponseCommand(id, body.Kind, body.AttachmentPublicId, body.SentOn, body.Text)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Garnishments_RegisterResponse")
            .ConClaveDeOperacion()
            .RequirePermission(Registrar);

        embargos.MapPost("/{id:guid}/consign", async (Guid id, ConsignarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ConsignGarnishmentCommand(id, body.Amount, body.Payee?.BankPublicId, body.Payee?.AccountNumber, body.Payee?.AccountType,
                    body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Garnishments_Consign")
            .ConClaveDeOperacion()
            .RequirePermission(Registrar);

        embargos.MapPost("/{id:guid}/release", async (Guid id, LevantarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReleaseGarnishmentCommand(id, body.ReleaseOfficeReference ?? string.Empty, body.ReleaseOfficeDate,
                    body.SupportAttachmentPublicId, body.Amount, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Garnishments_Release")
            .ConClaveDeOperacion()
            .RequirePermission(Levantar);

        var cuentas = LendingGroup.Grupo(app, "/accounts", "Cartera Bloqueos de cuentas");

        cuentas.MapGet("/{id:guid}/holds", async (Guid id, bool? includeReleased, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAccountHoldsQuery(id, includeReleased ?? false), ct))
            .WithName("Lending_Accounts_Holds_List")
            .RequirePermission(Leer);

        cuentas.MapPost("/{id:guid}/holds", async (Guid id, BloquearRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new PlaceAccountHoldCommand(id, body.Reason, body.Amount, body.ValidTo, body.LoanPublicId, body.SupportAttachmentPublicId,
                    body.Notes ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Accounts_Holds_Place")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarBloqueos);

        cuentas.MapPost("/{id:guid}/holds/{holdId:guid}/release", async (Guid id, Guid holdId, MotivoRequest body, HttpContext http, ISender sender,
                    CancellationToken ct) =>
                await sender.Send(new ReleaseAccountHoldCommand(id, holdId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Accounts_Holds_Release")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarBloqueos);
    }

    /// <summary>El cuerpo de <c>POST /{id}/responses</c>: la clase, el soporte firmado, cuándo se envió y el texto (vacío = el de la plantilla).</summary>
    public sealed record RespuestaRequest(GarnishmentResponseKind Kind, Guid? AttachmentPublicId, DateOnly SentOn, string? Text);

    /// <summary>La cuenta de depósitos judiciales a la que se consigna (sin ella, la del embargo).</summary>
    public sealed record BeneficiarioRequest(Guid? BankPublicId, string? AccountNumber, byte? AccountType);

    /// <summary>El cuerpo de <c>POST /{id}/consign</c>.</summary>
    public sealed record ConsignarRequest(decimal Amount, BeneficiarioRequest? Payee, string? Reason);

    /// <summary>El cuerpo de <c>POST /{id}/release</c>: el oficio del desembargo, su fecha, el soporte, el valor (parcial) y el motivo.</summary>
    public sealed record LevantarRequest(string? ReleaseOfficeReference, DateOnly? ReleaseOfficeDate, Guid? SupportAttachmentPublicId, decimal? Amount, string? Reason);

    /// <summary>El cuerpo de <c>POST /accounts/{id}/holds</c>.</summary>
    public sealed record BloquearRequest(HoldReason Reason, decimal? Amount, DateOnly? ValidTo, Guid? LoanPublicId, Guid? SupportAttachmentPublicId, string? Notes);

    /// <summary>El cuerpo de un levantamiento: el motivo.</summary>
    public sealed record MotivoRequest(string? Reason);
}
