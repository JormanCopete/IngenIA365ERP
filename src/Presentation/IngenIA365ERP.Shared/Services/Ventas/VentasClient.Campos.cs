using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// Los campos adicionales de venta (2026-11-15, pedido del dueño: la placa del vehículo): sus definiciones y los valores de un documento en
/// borrador. (nuevo)
/// </summary>
public sealed partial class VentasClient
{
    private const string RutaDeCampos = "/api/inventory/document-fields";

    public Task<ResultadoDeInventario<IReadOnlyList<CampoAdicionalDto>>> CamposAdicionalesAsync(bool soloActivos = false, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CampoAdicionalDto>>(HttpMethod.Get, soloActivos ? $"{RutaDeCampos}?activeOnly=true" : RutaDeCampos, null, null, ct);

    /// <summary>Crea (sin <paramref name="id"/>) o cambia un campo adicional; el código no cambia al editar.</summary>
    public Task<ResultadoDeInventario<CampoAdicionalDto>> GuardarCampoAdicionalAsync(Guid? id, CampoAdicionalRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        id is { } existente
            ? Enviar<CampoAdicionalDto>(HttpMethod.Put, $"{RutaDeCampos}/{existente}", request, clave, ct)
            : Enviar<CampoAdicionalDto>(HttpMethod.Post, RutaDeCampos, request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ValorDeCampoDto>>> ValoresDeCamposAsync(Guid documento, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ValorDeCampoDto>>(HttpMethod.Get, $"/api/inventory/documents/{documento}/fields", null, null, ct);

    /// <summary>Fija los valores de los campos del documento en borrador; uno vacío borra el valor.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<ValorDeCampoDto>>> FijarValoresDeCamposAsync(Guid documento, IReadOnlyList<ValorDeCampoRequest> valores,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ValorDeCampoDto>>(HttpMethod.Put, $"/api/inventory/documents/{documento}/fields", new ValoresDeCamposRequest(valores), clave, ct);
}

/// <summary>Un campo adicional. <see cref="DataType"/>: 1 texto, 2 número, 3 fecha.</summary>
public sealed record CampoAdicionalDto(Guid PublicId, string Code, string Name, int DataType, int MaxLength, bool IsRequired, bool ShowInSales,
    bool ShowInQuotes, bool PrintOnInvoice, int SortOrder, bool IsActive)
{
    /// <summary>Las clases de venta (factura, factura desde remisiones, documento equivalente POS, comprobante) y la cotización (23).</summary>
    public bool AplicaA(int clase) => (ShowInSales && clase is 26 or 27 or 28 or 29) || (ShowInQuotes && clase == 23);

    public string Tipo => DataType switch { 2 => "Número", 3 => "Fecha", _ => "Texto" };
}

public sealed record CampoAdicionalRequest(string Code, string Name, int DataType, int MaxLength, bool IsRequired, bool ShowInSales, bool ShowInQuotes,
    bool PrintOnInvoice, int SortOrder, bool IsActive);

public sealed record ValorDeCampoDto(Guid DefinitionPublicId, string Code, string Name, string Value);

public sealed record ValorDeCampoRequest(string Code, string? Value);

public sealed record ValoresDeCamposRequest(IReadOnlyList<ValorDeCampoRequest> Values);
