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

/// <summary>
/// Un producto cuya línea pide referencia y el rótulo del campo («Placa o lateral»); <see cref="RotuloDelComplemento"/> rotula el segundo
/// dato cuando la referencia lo pide («Placa del vehículo externo»).
/// </summary>
public sealed record ProductoConReferenciaDto(Guid ProductPublicId, string Rotulo, string? RotuloDelComplemento = null);

/// <summary>
/// La referencia resuelta: el valor que se guarda (la placa) y cómo se muestra. Con <see cref="FaltaComplemento"/> la referencia sirve pero
/// pide un segundo dato (el vehículo externo, su placa): la pantalla lo pide y vuelve a resolver «referencia/complemento».
/// </summary>
public sealed record ReferenciaResueltaDto(string Valor, string Etiqueta, string? FaltaComplemento = null);

/// <summary>
/// La referencia guardada con su complemento («618/ABC123», 2026-11-26): la pantalla muestra cada parte en su campo. El separador es el de
/// Comercial (<c>ReferenciaDeLinea.Separador</c>). (nuevo)
/// </summary>
public static class ReferenciaConComplemento
{
    public const char Separador = '/';

    /// <summary>La referencia sin el complemento.</summary>
    public static string? Parte(string? valor) => valor is null ? null : valor.IndexOf(Separador) is var i and >= 0 ? valor[..i] : valor;

    /// <summary>El complemento, o nulo.</summary>
    public static string? Complemento(string? valor) =>
        valor is not null && valor.IndexOf(Separador) is var i and >= 0 && i < valor.Length - 1 ? valor[(i + 1)..] : null;

    /// <summary>Lo que se resuelve con las dos partes.</summary>
    public static string Unir(string parte, string complemento) => $"{parte}{Separador}{complemento}";
}
