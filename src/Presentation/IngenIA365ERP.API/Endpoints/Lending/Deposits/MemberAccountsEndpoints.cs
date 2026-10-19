using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accounts.Parties;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Contractual;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// Las cuentas de captación y de aportes (feature 014, US2, T078; contracts/api.md §4 de la 014), sobre <see cref="LendingGroup"/>:
/// <c>/api/lending/accounts</c> —lista, conteos sin titulares, detalle, apertura, aprobación de una apertura retenida, partes y actualización de datos
/// tras la mayoría de edad—. Leer exige <c>Lending.Accounts.Read</c> (datos del titular) o <c>.View</c> (conteos); abrir y aprobar,
/// <c>Lending.Accounts.Open</c>; las partes, <c>Lending.Accounts.Parties.Manage</c>; toda escritura con <c>Idempotency-Key</c>. US16 (T164) suma el saldo a
/// (D, R) con su explicación y el extracto en pantalla, lecturas auditadas. Las operaciones, las cuotas, los bloqueos, los cargos, las liquidaciones y la cancelación llegan con sus historias (US3, US4,
/// US5, US11). US7 (T182) suma el contrato del ahorro contractual (<c>GET /{id}/contractual</c>), las versiones de condiciones selladas
/// (<c>GET /{id}/terms</c>) y la instrucción al vencimiento (<c>PUT /{id}/maturity-instruction</c>, con las partes). Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class MemberAccountsEndpoints : ICarterModule
{
    public const string Ver = "Lending.Accounts.View";
    public const string Leer = "Lending.Accounts.Read";
    public const string Abrir = "Lending.Accounts.Open";
    public const string AdministrarPartes = "Lending.Accounts.Parties.Manage";

    /// <summary>US5 (T139): liquidar a mano un período de intereses.</summary>
    public const string Liquidar = "Lending.Interest.Settle";

    /// <summary>US5 (T135): cancelar una cuenta.</summary>
    public const string Cancelar = "Lending.Accounts.Close";

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

        // ----------------------------------------------------------------------- saldo y extracto (US16, T164; lecturas auditadas) --
        cuentas.MapGet("/{id:guid}/balance", async (Guid id, DateOnly? date, DateTime? recordedAsOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMemberAccountBalanceQuery(id, date, recordedAsOf), ct))
            .WithName("Lending_Accounts_Balance")
            .RequirePermission(Leer);

        cuentas.MapGet("/{id:guid}/statement", async (Guid id, DateOnly? from, DateOnly? to, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMemberAccountStatementQuery(id, from, to), ct))
            .WithName("Lending_Accounts_Statement")
            .RequirePermission(Leer);

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

        // --------------------------------------------------------------------------------- ahorro contractual (US7, T182) --
        cuentas.MapGet("/{id:guid}/contractual", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetContractualQuery(id), ct))
            .WithName("Lending_Accounts_Contractual")
            .RequirePermission(Leer);

        cuentas.MapGet("/{id:guid}/terms", async (Guid id, int? version, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMemberAccountTermsQuery(id, version), ct))
            .WithName("Lending_Accounts_Terms")
            .RequirePermission(Leer);

        cuentas.MapPut("/{id:guid}/maturity-instruction", async (Guid id, InstruccionDeContractualRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new SetContractualMaturityInstructionCommand(id,
                    new ContractualMaturityInstructionInput(body.Kind, body.TargetAccountPublicId, body.Payee), body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Accounts_SetContractualMaturityInstruction")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarPartes);

        // ------------------------------------------------------------------------- intereses y cancelación (US5, T135, T139) --
        cuentas.MapGet("/{id:guid}/settlements", async (Guid id, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInterestSettlementsQuery(id, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_Accounts_Settlements")
            .RequirePermission(Leer);

        cuentas.MapPost("/{id:guid}/settlements/simulate", async (Guid id, SimularLiquidacionRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SimulateInterestSettlementQuery(id, body.Date, body.RecordedAsOf), ct))
            .WithName("Lending_Accounts_SimulateSettlement")
            .RequirePermission(Leer);

        // Idempotente por cuenta y período (SC-003): 201 la nueva; 200 la existente.
        cuentas.MapPost("/{id:guid}/settlements", async (Guid id, LiquidarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                Liquidada(await sender.Send(new SettleAccountInterestCommand(id, body.PeriodTo, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() },
                    ct), id))
            .WithName("Lending_Accounts_Settle")
            .ConClaveDeOperacion()
            .RequirePermission(Liquidar);

        cuentas.MapGet("/{id:guid}/close-preview", async (Guid id, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewMemberAccountClosingQuery(id, date), ct))
            .WithName("Lending_Accounts_ClosePreview")
            .RequirePermission(Leer);

        cuentas.MapPost("/{id:guid}/close", async (Guid id, CancelarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CloseMemberAccountCommand(id, body.Date, body.Delivery ?? new CloseDeliveryInput(CloseDeliveryKind.DisbursementOrder,
                    null, null), body.Signatures ?? [], body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct),
                    c => $"/api/lending/accounts/{id}/operations/{c.OperationPublicId}"))
            .WithName("Lending_Accounts_Close")
            .ConClaveDeOperacion()
            .RequirePermission(Cancelar);
    }

    /// <summary>201 con la liquidación nueva; 200 con la existente (SC-003).</summary>
    private static object Liquidada(IngenIA365ERP.Application.Common.Models.Result<InterestSettlementDto> r, Guid cuenta) =>
        r.IsFailure || r.Value.AlreadyExisted ? r : Results.Created($"/api/lending/accounts/{cuenta}/settlements/{r.Value.PublicId}", r.Value);

    /// <summary>La simulación de la liquidación: la fecha D y, si se quiere, el instante R hasta el que se toma lo registrado.</summary>
    public sealed record SimularLiquidacionRequest(DateOnly Date, DateTime? RecordedAsOf);

    /// <summary>La liquidación manual: el fin del período y el motivo.</summary>
    public sealed record LiquidarRequest(DateOnly PeriodTo, string? Reason);

    /// <summary>La cancelación: la fecha (hoy), cómo se entrega el saldo, quiénes firman y el motivo.</summary>
    public sealed record CancelarRequest(DateOnly? Date, CloseDeliveryInput? Delivery, IReadOnlyList<Guid>? Signatures, string? Reason);

    /// <summary>
    /// El cuerpo de <c>PUT /{id}/maturity-instruction</c> del contractual (api.md §4): trasladar a una cuenta a la vista del titular
    /// (<c>targetAccountPublicId</c>) o girar (<c>payee</c>), y el motivo.
    /// </summary>
    public sealed record InstruccionDeContractualRequest(CloseDeliveryKind Kind, Guid? TargetAccountPublicId,
        IngenIA365ERP.Application.Lending.Deposits.Operations.WithdrawalPayeeInput? Payee, string? Reason);

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
