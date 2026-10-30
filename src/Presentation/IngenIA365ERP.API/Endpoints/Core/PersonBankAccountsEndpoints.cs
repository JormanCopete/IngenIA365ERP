using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Core.PersonBankAccounts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// Las cuentas bancarias de una persona con doble control (feature 015, E-06, T0031; contracts/api.md §15; FR-210): leer con
/// <c>Core.PersonBankAccounts.View</c> (lectura sensible auditada), pedir el alta o el cambio con <c>.Request</c> y aprobar, rechazar o retirar con
/// <c>.Approve</c>. Las escrituras exigen <c>Idempotency-Key</c>. Sólo <c>PublicId</c> en rutas y cuerpos; cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class PersonBankAccountsEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Core.PersonBankAccounts.View";
    public const string PermisoDePedir = "Core.PersonBankAccounts.Request";
    public const string PermisoDeAprobar = "Core.PersonBankAccounts.Approve";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var dePersona = app.MapGroup("/api/core/people/{id:guid}/bank-accounts")
            .WithTags("Core Cuentas bancarias de la persona")
            .RequireAuthorization()
            .AuditarIntentoDenegado(ModuloDeAuditoria.Treasury)
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        dePersona.MapGet("/", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new ListPersonBankAccountsQuery(id), ct))
            .WithName("Core_PersonBankAccounts_List")
            .RequirePermission(PermisoDeConsulta);

        dePersona.MapPost("/", async (Guid id, PedirCuentaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new RequestPersonBankAccountCommand(
                    id, body.BankPublicId, body.AccountType ?? string.Empty, body.AccountNumber ?? string.Empty, body.HolderName ?? string.Empty,
                    body.HolderTaxId ?? string.Empty, body.CertificateAttachmentPublicId, body.ReplacesPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/core/people/{id}/bank-accounts", r.Value) : r;
            })
            .WithName("Core_PersonBankAccounts_Request")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDePedir);

        var cuentas = app.MapGroup("/api/core/person-bank-accounts")
            .WithTags("Core Cuentas bancarias de la persona")
            .RequireAuthorization()
            .AuditarIntentoDenegado(ModuloDeAuditoria.Treasury)
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        cuentas.MapPost("/{id:guid}/approve", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApprovePersonBankAccountCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_PersonBankAccounts_Approve")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeAprobar);

        cuentas.MapPost("/{id:guid}/reject", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RejectPersonBankAccountCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_PersonBankAccounts_Reject")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeAprobar);

        cuentas.MapPost("/{id:guid}/retire", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RetirePersonBankAccountCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_PersonBankAccounts_Retire")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeAprobar);
    }

    /// <summary>El cuerpo del alta o el cambio (api.md §15).</summary>
    public sealed record PedirCuentaRequest(
        Guid BankPublicId,
        string? AccountType,
        string? AccountNumber,
        string? HolderName,
        string? HolderTaxId,
        Guid? CertificateAttachmentPublicId,
        Guid? ReplacesPublicId);

    /// <summary>El motivo de un rechazo o un retiro.</summary>
    public sealed record MotivoRequest(string? Reason);
}
