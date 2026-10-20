using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Assembly;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// La asamblea (feature 014, US12, T205; contracts/api.md §12; FR-045 a FR-047), sobre <see cref="LendingGroup"/>:
/// <c>/api/lending/assembly-distributions</c> —listar y ver con cualquiera de los dos permisos; registrar, simular (sin clave) y descartar un borrador
/// (<c>Lending.Assembly.Register</c>); aplicar con el acta y reversar entera (<c>Lending.Assembly.Approve</c>)—. Toda escritura con <c>Idempotency-Key</c>.
/// Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class AssemblyDistributionsEndpoints : ICarterModule
{
    public const string Registrar = "Lending.Assembly.Register";
    public const string Aprobar = "Lending.Assembly.Approve";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var asamblea = LendingGroup.Grupo(app, "/assembly-distributions", "Cartera Asamblea");

        asamblea.MapGet("/", async (int? year, AssemblyDistributionKind? kind, AssemblyDistributionStatus? status, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAssemblyDistributionsQuery(year, kind, status), ct))
            .WithName("Lending_AssemblyDistributions_List")
            .RequirePermission(new AlgunPermiso(Registrar, Aprobar));

        asamblea.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetAssemblyDistributionQuery(id), ct))
            .WithName("Lending_AssemblyDistributions_Get")
            .RequirePermission(new AlgunPermiso(Registrar, Aprobar));

        asamblea.MapPost("/", async (RegistroRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RegisterAssemblyDistributionCommand(body.Kind, body.Year, body.FundAmount, body.Percent, body.Value,
                    body.Base, body.Destination, body.TargetDepositLineCode, body.CorrectsPublicId, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), r => $"/api/lending/assembly-distributions/{r.PublicId}"))
            .WithName("Lending_AssemblyDistributions_Register")
            .ConClaveDeOperacion()
            .RequirePermission(Registrar);

        asamblea.MapPost("/{id:guid}/simulate", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new SimulateAssemblyDistributionQuery(id), ct))
            .WithName("Lending_AssemblyDistributions_Simulate")
            .RequirePermission(Registrar);

        asamblea.MapPost("/{id:guid}/void", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new VoidAssemblyDistributionCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_AssemblyDistributions_Void")
            .ConClaveDeOperacion()
            .RequirePermission(Registrar);

        asamblea.MapPost("/{id:guid}/apply", async (Guid id, AplicacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApplyAssemblyDistributionCommand(id, body.SessionItemPublicId, body.ExpectedPreviewSha256 ?? string.Empty,
                    body.SupportAttachmentPublicIds, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_AssemblyDistributions_Apply")
            .ConClaveDeOperacion()
            .RequirePermission(Aprobar);

        asamblea.MapPost("/{id:guid}/reverse", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReverseAssemblyDistributionCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_AssemblyDistributions_Reverse")
            .ConClaveDeOperacion()
            .RequirePermission(Aprobar);

        // Respuesta 92 (E258): el giro rechazado de un excedente se regira; mientras tanto el valor sigue por pagar a favor del asociado.
        asamblea.MapPost("/{id:guid}/orders/{orderId:guid}/reissue", async (Guid id, Guid orderId, RegiroRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ReissueSurplusPayoutOrderCommand(id, orderId, body.BankPublicId, body.AccountType, body.AccountNumber,
                    body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_AssemblyDistributions_ReissueOrder")
            .ConClaveDeOperacion()
            .RequirePermission(Aprobar);
    }

    /// <summary>
    /// El cuerpo de <c>POST /</c>: la clase, el ejercicio, el fondo del acta, el porcentaje (fracción) o el valor, la base, el destino de los excedentes,
    /// la línea de ahorro de destino, la decisión reversada que corrige y el motivo.
    /// </summary>
    public sealed record RegistroRequest(AssemblyDistributionKind Kind, int Year, decimal FundAmount, decimal? Percent, decimal? Value, RevaluationBase? Base,
        SurplusDestination? Destination, string? TargetDepositLineCode, Guid? CorrectsPublicId, string? Reason);

    /// <summary>El cuerpo de <c>/apply</c>: el punto del acta cerrada, la huella de la simulación, los soportes y el motivo.</summary>
    public sealed record AplicacionRequest(Guid? SessionItemPublicId, string? ExpectedPreviewSha256, IReadOnlyList<Guid>? SupportAttachmentPublicIds,
        string? Reason);

    /// <summary>El motivo de un descarte o de un reverso.</summary>
    public sealed record MotivoRequest(string? Reason);

    /// <summary>El cuerpo de <c>/orders/{orderId}/reissue</c>: la cuenta de destino nueva (opcional: sin ella, la misma de la orden) y el motivo.</summary>
    public sealed record RegiroRequest(Guid? BankPublicId, byte? AccountType, string? AccountNumber, string? Reason);
}
