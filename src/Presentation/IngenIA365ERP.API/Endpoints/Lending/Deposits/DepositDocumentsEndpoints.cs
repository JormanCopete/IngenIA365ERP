using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Documents;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// Los documentos al titular de captaciones que tienen ruta propia (feature 014, US13, T198; contracts/api.md §17, documentos.md §2.3, §2.5): el lote de
/// documentos (<c>POST /api/lending/documents/batches</c>: certificados tributarios o de retención del año, o los extractos del último período según la
/// periodicidad de la cooperativa) y la emisión pendiente del título vigente de un CDAT (<c>POST /api/lending/term-deposits/{id}/titles</c>), los dos con
/// <c>Lending.Documents.Issue</c> y <c>Idempotency-Key</c>. Lo demás —emitir, listar (ahora también por <c>accountPublicId</c>), verificar, enviar,
/// reimprimir y anular— son las rutas de la 013 (<see cref="Lending.DocumentsEndpoints"/>), que admiten las clases 7 a 15. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class DepositDocumentsEndpoints : ICarterModule
{
    public const string Emitir = "Lending.Documents.Issue";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var documentos = LendingGroup.Grupo(app, "/documents", "Cartera Documentos");

        documentos.MapPost("/batches", async (LoteRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new IssueLendingDocumentBatchCommand(body.Kind, body.Year,
                    body.Period is { } p ? new PeriodoDeDocumento(p.From, p.To) : null, body.PersonPublicIds,
                    body.Delivery ?? DocumentDeliveryMode.Download)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Documents_Batches")
            .ConClaveDeOperacion()
            .RequirePermission(Emitir);

        var cdat = LendingGroup.Grupo(app, "/term-deposits", "Cartera CDAT");
        cdat.MapPost("/{id:guid}/titles", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new ReissueTermDepositTitleCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct),
                    t => $"/api/lending/documents/{t.DocumentPublicId}"))
            .WithName("Lending_TermDeposits_IssueTitle")
            .ConClaveDeOperacion()
            .RequirePermission(Emitir);
    }

    /// <summary>El pedido del lote: la clase, el año (certificados), el período (retención o extractos), las personas (opcional) y la entrega.</summary>
    public sealed record LoteRequest(IssuedDocumentKind Kind, short? Year, Lending.DocumentsEndpoints.PeriodoRequest? Period, IReadOnlyList<Guid>? PersonPublicIds,
        DocumentDeliveryMode? Delivery);
}
