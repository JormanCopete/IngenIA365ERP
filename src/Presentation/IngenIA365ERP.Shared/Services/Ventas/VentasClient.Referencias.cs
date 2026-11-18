using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// La referencia de la línea de venta (2026-11-18, feature 020): qué productos piden la placa o el lateral del vehículo y la resolución de lo
/// digitado. La usan el POS y la factura de oficina. (nuevo)
/// </summary>
public sealed partial class VentasClient
{
    private const string RutaDeReferencias = "/api/inventory/sales/line-references";

    public Task<ResultadoDeInventario<IReadOnlyList<ProductoConReferenciaDto>>> ProductosConReferenciaAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ProductoConReferenciaDto>>(HttpMethod.Get, $"{RutaDeReferencias}/products", null, null, ct);

    public Task<ResultadoDeInventario<ReferenciaResueltaDto>> ResolverReferenciaAsync(Guid producto, string texto, CancellationToken ct = default) =>
        Enviar<ReferenciaResueltaDto>(HttpMethod.Get, $"{RutaDeReferencias}/resolve?product={producto}&q={Uri.EscapeDataString(texto)}", null, null, ct);
}

/// <summary>Un producto cuya línea pide referencia y el rótulo del campo («Placa o lateral»).</summary>
public sealed record ProductoConReferenciaDto(Guid ProductPublicId, string Rotulo);

/// <summary>La referencia resuelta: el valor que se guarda (la placa) y cómo se muestra.</summary>
public sealed record ReferenciaResueltaDto(string Valor, string Etiqueta);
