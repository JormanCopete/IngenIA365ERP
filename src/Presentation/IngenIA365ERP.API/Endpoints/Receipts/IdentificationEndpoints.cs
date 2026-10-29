using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// La identificación por reglas y la consignación reportada (feature 015, R1, US6, T0380; contracts/api.md §5): las reglas (con la configuración de Recaudo), la
/// propuesta de un abono, la aplicación al cargar un extracto y la consignación reportada en ventanilla con su verificación. Las escrituras llevan
/// <c>Idempotency-Key</c>; cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class IdentificationEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var reglas = ReceiptsGroup.Grupo(app, "/identification-rules", "Recaudo Reglas de identificación");
        reglas.MapGet("/", async (ISender s, CancellationToken ct) => await s.Send(new ListIdentificationRulesQuery(), ct))
            .WithName("Receipts_IdentificationRules_List")
            .RequirePermission(new AlgunPermiso(SetupEndpoints.PermisoDeConsulta, SetupEndpoints.PermisoDeConfiguracion, BankItemsEndpoints.Identificar));
        reglas.MapPost("/", async (ReglaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SaveIdentificationRuleCommand(null, body.Code ?? string.Empty, body.Name ?? string.Empty, body.Criterion, body.Priority, body.ApplyOnLoad,
                    body.IsActive ?? true) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_IdentificationRules_Create").ConClaveDeOperacion().RequirePermission(SetupEndpoints.PermisoDeConfiguracion);
        reglas.MapPut("/{id:guid}", async (Guid id, ReglaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SaveIdentificationRuleCommand(id, body.Code ?? string.Empty, body.Name ?? string.Empty, body.Criterion, body.Priority, body.ApplyOnLoad,
                    body.IsActive ?? true) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_IdentificationRules_Update").ConClaveDeOperacion().RequirePermission(SetupEndpoints.PermisoDeConfiguracion);

        var abonos = ReceiptsGroup.Grupo(app, "/bank-items", "Recaudo Abonos");
        abonos.MapGet("/{lineId:guid}/proposal", async (Guid lineId, ISender s, CancellationToken ct) => await s.Send(new GetBankItemProposalQuery(lineId), ct))
            .WithName("Receipts_BankItems_Proposal")
            .RequirePermission(new AlgunPermiso(BankItemsEndpoints.Ver, BankItemsEndpoints.Identificar, BankItemsEndpoints.Clasificar));
        abonos.MapPost("/apply-on-load", async (AplicarAlCargarRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ApplyIdentificationRulesOnLoadCommand(body.OwnBankAccountPublicId, body.StatementNumber ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_BankItems_ApplyOnLoad").ConClaveDeOperacion().RequirePermission(BankItemsEndpoints.Identificar);

        var reportadas = ReceiptsGroup.Grupo(app, "/reported-deposits", "Recaudo Consignaciones reportadas");
        reportadas.MapGet("/", async (ReportedDepositStatus? state, ISender s, CancellationToken ct) => await s.Send(new ListReportedDepositsQuery(state), ct))
            .WithName("Receipts_ReportedDeposits_List")
            .RequirePermission(new AlgunPermiso(BankItemsEndpoints.Ver, BankItemsEndpoints.Identificar, CashDeskEndpoints.Registrar));
        reportadas.MapPost("/", async (RegisterReportedDepositCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/receipts/reported-deposits/{v.PublicId}"))
            .WithName("Receipts_ReportedDeposits_Register").ConClaveDeOperacion().RequirePermission(CashDeskEndpoints.Registrar);
        reportadas.MapPost("/{id:guid}/verify", async (Guid id, VerificarRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new VerifyReportedDepositCommand(id, body.LinePublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_ReportedDeposits_Verify").ConClaveDeOperacion().RequirePermission(BankItemsEndpoints.Identificar);
    }

    public sealed record ReglaRequest(string? Code, string? Name, IdentificationCriterion Criterion, int Priority, bool ApplyOnLoad, bool? IsActive);

    public sealed record AplicarAlCargarRequest(Guid OwnBankAccountPublicId, string? StatementNumber);

    public sealed record VerificarRequest(Guid LinePublicId);
}
