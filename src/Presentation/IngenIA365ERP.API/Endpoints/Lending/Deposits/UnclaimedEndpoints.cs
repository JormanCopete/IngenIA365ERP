using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Unclaimed;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// Los saldos no reclamados (feature 014, US14, T212; contracts/api.md §14; FR-081, FR-084), sobre <see cref="LendingGroup"/>: <c>/api/lending/unclaimed</c>
/// —listar (<c>Lending.Accounts.Read</c>), trasladar a mano y pagar (<c>Lending.Unclaimed.Manage</c>)—. Toda escritura con <c>Idempotency-Key</c>. Cada ruta
/// sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class UnclaimedEndpoints : ICarterModule
{
    public const string Leer = "Lending.Accounts.Read";
    public const string Administrar = "Lending.Unclaimed.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var saldos = LendingGroup.Grupo(app, "/unclaimed", "Cartera No reclamados");

        saldos.MapGet("/", async (Guid? personPublicId, UnclaimedStatus? status, int? page, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListUnclaimedBalancesQuery(personPublicId, status, page ?? 1), ct))
            .WithName("Lending_Unclaimed_List")
            .RequirePermission(Leer);

        saldos.MapPost("/transfer", async (TrasladoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new TransferToUnclaimedCommand(body.AccountPublicIds, body.RefundPublicIds, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Unclaimed_Transfer")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        saldos.MapPost("/{id:guid}/pay", async (Guid id, PagoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new PayUnclaimedBalanceCommand(id, body.Form, body.TargetAccountPublicId, body.Payee?.BankPublicId, body.Payee?.AccountType,
                    body.Payee?.AccountNumber, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Unclaimed_Pay")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo de <c>/transfer</c>: las cuentas inactivas, las devoluciones no cobradas (sus órdenes) y el motivo.</summary>
    public sealed record TrasladoRequest(IReadOnlyList<Guid>? AccountPublicIds, IReadOnlyList<Guid>? RefundPublicIds, string? Reason);

    /// <summary>El banco del titular para el giro (sin él, su cuenta de depósito registrada).</summary>
    public sealed record BeneficiarioRequest(Guid? BankPublicId, byte? AccountType, string? AccountNumber);

    /// <summary>El cuerpo de <c>/{id}/pay</c>: el medio, la cuenta de destino o el banco, y el motivo.</summary>
    public sealed record PagoRequest(FormaDePagoDeNoReclamado Form, Guid? TargetAccountPublicId, BeneficiarioRequest? Payee, string? Reason);
}
