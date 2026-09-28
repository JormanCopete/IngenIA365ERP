using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>PurchaseRequest</c> (feature 012, I5, T787; FR-036, FR-048; contracts/api.md §14.9). (nuevo)
/// <list type="bullet">
/// <item>la forma de §14.9: bodega que pide (la exige el ciclo común), <c>neededBy</c> (en <c>ExpectedDate</c>), quién la pide
/// (<c>requestedByPersonPublicId</c>, opcional, su contraparte) y líneas con producto, unidad y cantidad, <b>sin precios</b> (el borrador
/// ya rechaza precios, proveedor y orígenes: <see cref="BorradorDeCompra"/>);</item>
/// <item>los productos, activos y no bloqueados; nunca el tránsito;</item>
/// <item>se aprueba por la política de su tipo (el ciclo común, paso 2) con su <c>Total</c>, que es cero: la solicitud no compromete
/// dinero;</item>
/// <item><b>no</b> escribe kardex ni emite mensajes (FR-036): hereda de la base el efecto y los mensajes vacíos; su anulación tampoco
/// mueve nada. Una solicitud con órdenes vigentes no se anula (<c>Inventory.Document.HasDependents</c>, el ciclo común).</item>
/// </list>
/// </summary>
public sealed class EfectoDeSolicitudDeCompra(IMaestrosDelDocumento maestros) : EfectoDeClaseBase
{
    public override DocumentClass Clase => DocumentClass.PurchaseRequest;

    public override async Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        await ReglasDeCompra.ProductosComprablesAsync(contexto.Documento, maestros, ct);

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return Result.Success();
        var documento = contexto.Documento;
        if (documento.ExpectedDate is null) return Result.Failure(InventoryErrors.FieldRequired(BorradorDeCompra.CampoNecesarioPara));
        if (await ReglasDeCompra.TransitoAsync(documento, maestros, ct) is { } transito) return Result.Failure(transito);
        var productos = await ReglasDeCompra.ProductosComprablesAsync(documento, maestros, ct);
        return productos.Count > 0 ? Result.Failure(productos[0]) : Result.Success();
    }

    /// <summary>El <c>Total</c> (§1.3): cero, porque la solicitud no lleva precios.</summary>
    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;
}
