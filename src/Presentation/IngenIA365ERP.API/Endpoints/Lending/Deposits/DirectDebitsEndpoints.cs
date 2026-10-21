using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// El débito automático (feature 014, US4, T123; contracts/api.md §9 de la 014; FR-090, R16), sobre <see cref="LendingGroup"/>:
/// <c>/api/lending/direct-debits</c> —las autorizaciones, registrar una y revocarla—. Leer exige <c>Lending.Accounts.Read</c>; registrar y revocar,
/// <c>Lending.DirectDebits.Manage</c> con <c>Idempotency-Key</c>. El débito lo hace el ciclo diario, sin ruta. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class DirectDebitsEndpoints : ICarterModule
{
    public const string Leer = MemberAccountsEndpoints.Leer;
    public const string Administrar = "Lending.DirectDebits.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var debitos = LendingGroup.Grupo(app, "/direct-debits", "Cartera Débito automático");

        debitos.MapGet("/", async (Guid? personPublicId, Guid? accountPublicId, Guid? loanPublicId, string? status, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDirectDebitMandatesQuery(personPublicId, accountPublicId, loanPublicId, status), ct))
            .WithName("Lending_DirectDebits_List")
            .RequirePermission(Leer);

        debitos.MapPost("/", async (RegistrarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RegisterDirectDebitMandateCommand(body.AccountPublicId, body.Scope, body.LoanPublicId, body.AllowsPartial,
                    body.ValidFrom, body.ValidTo, body.AuthorizationAttachmentPublicId, body.AuthorizedByPersonPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), m => $"/api/lending/direct-debits/{m.PublicId}"))
            .WithName("Lending_DirectDebits_Register")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        debitos.MapPost("/{id:guid}/revoke", async (Guid id, RevocarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RevokeDirectDebitMandateCommand(id, body.ValidTo, body.Reason ?? string.Empty, body.EvidenceAttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_DirectDebits_Revoke")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo de <c>POST /</c>: la cuenta, el alcance, el crédito (si es uno), el débito parcial, la vigencia, el soporte y quién autorizó.</summary>
    public sealed record RegistrarRequest(Guid AccountPublicId, DirectDebitScope Scope, Guid? LoanPublicId, bool AllowsPartial, DateOnly? ValidFrom,
        DateOnly? ValidTo, Guid AuthorizationAttachmentPublicId, Guid? AuthorizedByPersonPublicId);

    /// <summary>El cuerpo de <c>POST /{id}/revoke</c>: el último día (hoy si no viene), el motivo y el soporte.</summary>
    public sealed record RevocarRequest(DateOnly? ValidTo, string? Reason, Guid? EvidenceAttachmentPublicId);
}
