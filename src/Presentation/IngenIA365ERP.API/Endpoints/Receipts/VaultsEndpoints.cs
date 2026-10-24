using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Application.Receipts.Vaults;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// La bóveda en operación (feature 015, US2, T0097; contracts/api.md §3): su saldo por denominación y sus movimientos propios —la remesa que vuelve, el
/// conteo, el billete retenido con acta— con doble custodia. La configuración de la bóveda (alta, modo, custodios) está en <see cref="SetupEndpoints"/>;
/// la remesa al banco, con Tesorería (fase 7). (nuevo)
/// </summary>
public class VaultsEndpoints : ICarterModule
{
    public const string Ver = "Receipts.Vault.View";
    public const string Mover = "Receipts.Vault.Move";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = ReceiptsGroup.Grupo(app, "/vaults", "Recaudo Bóveda");
        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetVaultDetailQuery(id), ct))
            .WithName("Receipts_Vaults_Detail").RequirePermission(Ver);
        g.MapPost("/{id:guid}/movements", async (Guid id, MovimientoDeBovedaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterVaultMovementCommand(id, body.Kind, body.Denominations ?? [], body.Reason ?? string.Empty, body.SecondCustodianUserPublicId,
                    body.BagSeal, body.MinutesAttachmentPublicId, body.RemittancePublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Vaults_Movement").ConClaveDeOperacion().RequirePermission(Mover);
    }

    public sealed record MovimientoDeBovedaRequest(VaultMovementKind Kind, IReadOnlyList<DenominacionContadaDto>? Denominations, string? Reason,
        Guid? SecondCustodianUserPublicId, string? BagSeal, Guid? MinutesAttachmentPublicId, Guid? RemittancePublicId);
}
