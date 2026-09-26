using Carter;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Ajustes, consumos internos, bajas y movimientos entre ubicaciones (feature 012, T262; contracts/api.md §9.3, §10): el ciclo
/// común del grupo <c>Adjustments</c> sobre <c>/api/inventory/adjustments</c> —lista y detalle, crear y reemplazar el borrador,
/// descartar, confirmar (<c>ConfirmInventoryDocumentCommand(DocumentPublicId, ExpectedGroup: Adjustments)</c>) y anular— con
/// <c>Inventory.Adjustments.{View, Create, Confirm, Void}</c>, <c>Idempotency-Key</c> en toda escritura
/// (<c>LosComandosDeInventarioLlevanClave</c>) y el 404 fuera del alcance. Lo publica <see cref="CicloDeDocumentoRutas"/>: las
/// reglas de cada clase las ponen sus estrategias (<c>EfectoDeAjustePositivo</c>, <c>EfectoDeSalidaPorAjuste</c>). El movimiento
/// entre ubicaciones (US10) y el ensamble (I6) se montan sobre esta misma ruta. (nuevo)
/// </summary>
public class AdjustmentsEndpoints : ICarterModule
{
    public const string Ruta = "/api/inventory/adjustments";

    public void AddRoutes(IEndpointRouteBuilder app) =>
        app.MapGroup(Ruta)
            .WithTags("Inventory Adjustments")
            .RequireAuthorization()
            .MapCicloDeDocumento(DocumentClassGroup.Adjustments, "Inventory.Adjustments", "Inventory_Adjustments");
}
