using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accounts.Parties;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// Las cuentas de captación y de aportes (feature 014, US2, T078; contracts/api.md §4 de la 014), sobre <see cref="LendingGroup"/>:
/// <c>/api/lending/accounts</c> —lista, conteos sin titulares, detalle, apertura, aprobación de una apertura retenida, partes y actualización de datos
/// tras la mayoría de edad—. Leer exige <c>Lending.Accounts.Read</c> (datos del titular) o <c>.View</c> (conteos); abrir y aprobar,
/// <c>Lending.Accounts.Open</c>; las partes, <c>Lending.Accounts.Parties.Manage</c>; toda escritura con <c>Idempotency-Key</c>. Las operaciones, el
/// saldo con su explicación, el extracto, las cuotas, los bloqueos, los cargos, las liquidaciones y la cancelación llegan con sus historias (US3, US4,
/// US5, US11). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class MemberAccountsEndpoints : ICarterModule
{
    public const string Ver = "Lending.Accounts.View";
    public const string Leer = "Lending.Accounts.Read";
    public const string Abrir = "Lending.Accounts.Open";
    public const string AdministrarPartes = "Lending.Accounts.Parties.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var cuentas = LendingGroup.Grupo(app, "/accounts", "Cartera Cuentas de captación y aportes");

        cuentas.MapGet("/", async (string? search, LineNature? nature, DepositClass? depositClass, Guid? line, Guid? office, MemberAccountStatus? status,
                    Guid? personPublicId, string? number, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListMemberAccountsQuery(search, nature, depositClass, line, office, status, personPublicId, number, page ?? 1,
                    pageSize ?? 20), ct))
            .WithName("Lending_Accounts_List")
            .RequirePermission(Leer);

        cuentas.MapGet("/counts", async (LineNature? nature, Guid? line, Guid? office, MemberAccountStatus? status, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMemberAccountCountsQuery(nature, line, office, status), ct))
            .WithName("Lending_Accounts_Counts")
            .RequirePermission(Ver);

        cuentas.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMemberAccountQuery(id), ct))
            .WithName("Lending_Accounts_Get")
            .RequirePermission(Leer);

        cuentas.MapPost("/", async (OpenAccountInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new OpenMemberAccountCommand(body) { OperationKey = http.ClaveDeOperacion() }, ct),
                    c => $"/api/lending/accounts/{c.PublicId}"))
            .WithName("Lending_Accounts_Open")
            .ConClaveDeOperacion()
            .RequirePermission(Abrir);

        cuentas.MapPost("/{id:guid}/approve-opening", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApproveMemberAccountOpeningCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Accounts_ApproveOpening")
            .ConClaveDeOperacion()
            .RequirePermission(Abrir);

        // ------------------------------------------------------------------------------------------------------- partes --
        cuentas.MapGet("/{id:guid}/parties", async (Guid id, DateOnly? date, bool? includeHistory, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMemberAccountPartiesQuery(id, date, includeHistory ?? false), ct))
            .WithName("Lending_Accounts_GetParties")
            .RequirePermission(Leer);

        cuentas.MapPut("/{id:guid}/parties", async (Guid id, CambiarPartesRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetMemberAccountPartiesCommand(id, body.HandlingMode, body.Parties ?? [], body.Beneficiaries ?? [],
                    body.EvidenceAttachmentPublicId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Accounts_SetParties")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarPartes);

        cuentas.MapPost("/{id:guid}/data-update", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ConfirmMemberAccountDataUpdateCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Accounts_ConfirmDataUpdate")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarPartes);
    }

    /// <summary>El cuerpo de las rutas que sólo llevan motivo.</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary>El cuerpo de <c>PUT /{id}/parties</c>: el conjunto completo de partes y beneficiarios, la constancia y el motivo.</summary>
    public sealed record CambiarPartesRequest(
        HandlingMode HandlingMode,
        IReadOnlyList<MemberAccountPartyInput>? Parties,
        IReadOnlyList<MemberAccountBeneficiaryInput>? Beneficiaries,
        Guid? EvidenceAttachmentPublicId,
        string? Reason);
}
