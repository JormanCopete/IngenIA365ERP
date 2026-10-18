using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Deposits.Charges;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// Las operaciones y los cargos de una cuenta (feature 014, US4, T123; contracts/api.md §4 de la 014), sobre <see cref="LendingGroup"/>:
/// <c>/api/lending/accounts/{id}/…</c> —operaciones, su comprobante, el libro, la simulación, el retiro por orden de giro, el traslado, el reverso y los
/// cargos causados—. Leer exige <c>Lending.Accounts.Read</c>; retirar, <c>.Withdraw</c>; trasladar, <c>.Transfer</c>; reversar, <c>.Reverse</c>; los
/// cargos, <c>Lending.Charges.Manage</c> o <c>.Waive</c>; toda escritura con <c>Idempotency-Key</c>. El retiro o el traslado sobre la atribución responde
/// <b>202</b> con la solicitud de aprobación (sin mover saldo). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class MemberAccountOperationsEndpoints : ICarterModule
{
    public const string Leer = MemberAccountsEndpoints.Leer;
    public const string Retirar = "Lending.Accounts.Withdraw";
    public const string Trasladar = "Lending.Accounts.Transfer";
    public const string Reversar = "Lending.Accounts.Reverse";
    public const string AdministrarCargos = "Lending.Charges.Manage";
    public const string CondonarCargos = "Lending.Charges.Waive";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var cuentas = LendingGroup.Grupo(app, "/accounts", "Cartera Movimientos de cuentas");

        cuentas.MapGet("/{id:guid}/operations", async (Guid id, MemberAccountOperationKind? kind, DateOnly? from, DateOnly? to, int? page, int? pageSize,
                    ISender sender, CancellationToken ct) =>
                await sender.Send(new ListMemberAccountOperationsQuery(id, kind, from, to, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_Accounts_Operations")
            .RequirePermission(Leer);

        cuentas.MapGet("/{id:guid}/operations/{operationId:guid}", async (Guid id, Guid operationId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMemberAccountOperationQuery(id, operationId), ct))
            .WithName("Lending_Accounts_Operation")
            .RequirePermission(Leer);

        cuentas.MapGet("/{id:guid}/transactions", async (Guid id, DateOnly? from, DateOnly? to, MemberAccountConcept? concept, string? dateKind, int? page,
                    int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListMemberAccountTransactionsQuery(id, from, to, concept, dateKind ?? "value", page ?? 1, pageSize ?? 100), ct))
            .WithName("Lending_Accounts_Transactions")
            .RequirePermission(Leer);

        cuentas.MapPost("/{id:guid}/operations/simulate", async (Guid id, SimularRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SimulateMemberAccountOperationQuery(id, body.Kind ?? "Withdrawal", body.Amount, body.TargetAccountPublicId, body.MeansClass,
                    body.Signatures ?? []), ct))
            .WithName("Lending_Accounts_SimulateOperation")
            .RequirePermission(Leer);

        cuentas.MapPost("/{id:guid}/operations/withdrawals", async (Guid id, RetiroRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                Responder(await sender.Send(new WithdrawToDisbursementOrderCommand(id, body.Amount, body.ValueDate, body.Payee, body.Signatures ?? [], body.Reason,
                    body.ApprovalPublicId) { OperationKey = http.ClaveDeOperacion() }, ct), id))
            .WithName("Lending_Accounts_Withdraw")
            .ConClaveDeOperacion()
            .RequirePermission(Retirar);

        cuentas.MapPost("/{id:guid}/operations/transfers", async (Guid id, TrasladoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                Responder(await sender.Send(new TransferBetweenMemberAccountsCommand(id, body.TargetAccountPublicId, body.Amount, body.ValueDate,
                    body.Signatures ?? [], body.Reason, body.TargetModality ?? ModalidadDeConsignacion.Installments, body.ApprovalPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), id))
            .WithName("Lending_Accounts_Transfer")
            .ConClaveDeOperacion()
            .RequirePermission(Trasladar);

        cuentas.MapPost("/{id:guid}/operations/{operationId:guid}/reverse", async (Guid id, Guid operationId, MotivoRequest body, HttpContext http, ISender sender,
                    CancellationToken ct) =>
                await sender.Send(new ReverseMemberAccountOperationCommand(id, operationId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Accounts_ReverseOperation")
            .ConClaveDeOperacion()
            .RequirePermission(Reversar);

        // ------------------------------------------------------------------------------------------------------ cargos --
        cuentas.MapGet("/{id:guid}/charges", async (Guid id, AccountChargeStatus? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAccountChargesQuery(id, status, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_Accounts_Charges")
            .RequirePermission(Leer);

        cuentas.MapPost("/{id:guid}/charges", async (Guid id, CargoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RegisterAccountChargeCommand(id, body.ChargeCode ?? string.Empty, body.EventReference,
                    body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct), c => $"/api/lending/accounts/{id}/charges/{c.PublicId}"))
            .WithName("Lending_Accounts_RegisterCharge")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarCargos);

        cuentas.MapPost("/{id:guid}/charges/{chargeId:guid}/waive", async (Guid id, Guid chargeId, MotivoRequest body, HttpContext http, ISender sender,
                    CancellationToken ct) =>
                await sender.Send(new WaiveAccountChargeCommand(id, chargeId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Accounts_WaiveCharge")
            .ConClaveDeOperacion()
            .RequirePermission(CondonarCargos);

        cuentas.MapPost("/{id:guid}/charges/{chargeId:guid}/reverse", async (Guid id, Guid chargeId, MotivoRequest body, HttpContext http, ISender sender,
                    CancellationToken ct) =>
                await sender.Send(new ReverseAccountChargeCommand(id, chargeId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Accounts_ReverseCharge")
            .ConClaveDeOperacion()
            .RequirePermission(AdministrarCargos);

        cuentas.MapPost("/{id:guid}/charges/exemption", async (Guid id, ExoneracionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetAccountChargeExemptionCommand(id, body.ChargeCodes ?? [], body.ValidFrom, body.ValidTo, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Accounts_ChargeExemption")
            .ConClaveDeOperacion()
            .RequirePermission(CondonarCargos);
    }

    /// <summary>201 con el comprobante si se aplicó; 202 con la solicitud de aprobación si espera un aprobador presente.</summary>
    private static object Responder(IngenIA365ERP.Application.Common.Models.Result<MemberAccountOperationResultDto> r, Guid cuenta)
    {
        if (r.IsFailure) return r;
        return r.Value.Status == MemberAccountOperationResultDto.PendienteDeAprobacion
            ? Results.Accepted($"/api/inventory/approvals/{r.Value.ApprovalPublicId}", r.Value)
            : Results.Created($"/api/lending/accounts/{cuenta}/operations/{r.Value.Operation?.OperationPublicId}", r.Value);
    }

    /// <summary>El cuerpo de las rutas que sólo llevan motivo.</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary>La simulación: <c>Withdrawal</c> o <c>Transfer</c>, el valor, la cuenta destino, el medio y quiénes firman.</summary>
    public sealed record SimularRequest(string? Kind, decimal Amount, Guid? TargetAccountPublicId, ClaseDeMedioDeRecaudo? MeansClass, IReadOnlyList<Guid>? Signatures);

    /// <summary>El retiro por orden de giro: el valor, la fecha valor (hoy), el beneficiario, quiénes firman, el motivo y la aprobación ya concedida.</summary>
    public sealed record RetiroRequest(decimal Amount, DateOnly? ValueDate, WithdrawalPayeeInput? Payee, IReadOnlyList<Guid>? Signatures, string? Reason,
        Guid? ApprovalPublicId);

    /// <summary>El traslado: la cuenta destino, el valor, la fecha valor, quiénes firman, el motivo, la modalidad en el destino y la aprobación.</summary>
    public sealed record TrasladoRequest(Guid TargetAccountPublicId, decimal Amount, DateOnly? ValueDate, IReadOnlyList<Guid>? Signatures, string? Reason,
        ModalidadDeConsignacion? TargetModality, Guid? ApprovalPublicId);

    /// <summary>Un cargo por evento de pantalla: el código del catálogo de la línea, la referencia del evento y el motivo.</summary>
    public sealed record CargoRequest(string? ChargeCode, string? EventReference, string? Reason);

    /// <summary>La exoneración individual: los códigos, la vigencia y el motivo.</summary>
    public sealed record ExoneracionRequest(IReadOnlyList<string>? ChargeCodes, DateOnly ValidFrom, DateOnly? ValidTo, string? Reason);
}
