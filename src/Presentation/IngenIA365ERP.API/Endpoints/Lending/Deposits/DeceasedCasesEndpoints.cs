using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Deceased;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// El fallecimiento del titular (feature 014, US14, T212; contracts/api.md §13; FR-082, FR-083), sobre <see cref="LendingGroup"/>:
/// <c>/api/lending/deceased-cases</c> —listar y ver con cualquiera de los dos permisos; registrar, la vista previa de la entrega (sin clave) y registrar la
/// entrega (<c>Lending.DeceasedCases.Register</c>); aprobarla y anular un registro errado (<c>Lending.DeceasedCases.Approve</c>)—. Toda escritura con
/// <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class DeceasedCasesEndpoints : ICarterModule
{
    public const string Registrar = "Lending.DeceasedCases.Register";
    public const string Aprobar = "Lending.DeceasedCases.Approve";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var casos = LendingGroup.Grupo(app, "/deceased-cases", "Cartera Fallecimientos");

        casos.MapGet("/", async (DeceasedCaseStatus? status, int? page, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDeceasedHolderCasesQuery(status, page ?? 1), ct))
            .WithName("Lending_DeceasedCases_List")
            .RequirePermission(new AlgunPermiso(Registrar, Aprobar));

        casos.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDeceasedHolderCaseQuery(id), ct))
            .WithName("Lending_DeceasedCases_Get")
            .RequirePermission(new AlgunPermiso(Registrar, Aprobar));

        casos.MapPost("/", async (RegistroRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RegisterDeceasedHolderCommand(body.PersonPublicId, body.DateOfDeath,
                    body.DeathCertificateAttachmentPublicId, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), r => $"/api/lending/deceased-cases/{r.PublicId}"))
            .WithName("Lending_DeceasedCases_Register")
            .ConClaveDeOperacion()
            .RequirePermission(Registrar);

        casos.MapPost("/{id:guid}/delivery/preview", async (Guid id, VistaPreviaRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewDeceasedDeliveryQuery(id, body.Mode, body.Recipients), ct))
            .WithName("Lending_DeceasedCases_DeliveryPreview")
            .RequirePermission(Registrar);

        casos.MapPost("/{id:guid}/delivery", async (Guid id, EntregaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RegisterDeceasedDeliveryCommand(id, body.Mode, body.Recipients, body.SupportAttachmentPublicIds,
                    body.ExpectedPreviewSha256 ?? string.Empty, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_DeceasedCases_RegisterDelivery")
            .ConClaveDeOperacion()
            .RequirePermission(Registrar);

        casos.MapPost("/{id:guid}/delivery/approve", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApproveDeceasedDeliveryCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_DeceasedCases_ApproveDelivery")
            .ConClaveDeOperacion()
            .RequirePermission(Aprobar);

        casos.MapPost("/{id:guid}/void", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new VoidDeceasedHolderCaseCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_DeceasedCases_Void")
            .ConClaveDeOperacion()
            .RequirePermission(Aprobar);
    }

    /// <summary>El cuerpo de <c>POST /</c>: la persona, la fecha de la defunción, el registro civil (adjunto) y el motivo.</summary>
    public sealed record RegistroRequest(Guid PersonPublicId, DateOnly DateOfDeath, Guid DeathCertificateAttachmentPublicId, string? Reason);

    /// <summary>El cuerpo de la vista previa: la ruta y los destinatarios (porcentaje en fracción fuera de «beneficiarios»; el medio de cada uno).</summary>
    public sealed record VistaPreviaRequest(DeliveryRoute Mode, IReadOnlyList<DeliveryRecipientInput>? Recipients);

    /// <summary>El cuerpo de <c>/delivery</c>: la ruta, los destinatarios, los soportes, la huella de la vista previa y el motivo.</summary>
    public sealed record EntregaRequest(DeliveryRoute Mode, IReadOnlyList<DeliveryRecipientInput>? Recipients, IReadOnlyList<Guid>? SupportAttachmentPublicIds,
        string? ExpectedPreviewSha256, string? Reason);

    /// <summary>El motivo de una aprobación o una anulación.</summary>
    public sealed record MotivoRequest(string? Reason);
}
