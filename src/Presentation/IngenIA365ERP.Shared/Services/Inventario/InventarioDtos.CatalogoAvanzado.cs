namespace IngenIA365ERP.Shared.Services.Inventario;

// DTO espejo del catálogo avanzado (feature 012, I6, T936; US15; contracts/api.md §3, §5, §10, §17.1): atributos y variantes, componentes de
// combos y kits, lotes con existencia y series, y el cuerpo del ensamble. Mismos nombres de propiedad que la API; `ProductKind` llega como
// número y el estado de un lote como texto (`Current`, `ExpiringSoon`, `Expired`, `NoExpiry`, `TextosDeInventario.EstadoDeLote`). (nuevo)

// ------------------------------------------------------------------------------------------------- atributos --

/// <summary>Un valor de un atributo de variante.</summary>
public sealed record ValorDeAtributoDto(Guid PublicId, string Code, string Name, int SortOrder);

/// <summary>Un atributo de variante —talla, color— con sus valores y cuántas variantes lo usan.</summary>
public sealed record AtributoDeVarianteDto(Guid PublicId, string Code, string Name, bool IsActive, IReadOnlyList<ValorDeAtributoDto> Values, int VariantsUsing);

/// <summary>Un valor pedido del atributo.</summary>
public sealed record ValorDeAtributoRequest(string Code, string Name, int SortOrder = 0);

/// <summary>El cuerpo de <c>POST/PUT /variant-attributes</c>: los valores que no vienen se retiran (salvo los que usan variantes).</summary>
public sealed record AtributoDeVarianteRequest(string Code, string Name, IReadOnlyList<ValorDeAtributoRequest> Values, bool IsActive = true);

// ------------------------------------------------------------------------------------------------- variantes --

/// <summary>Un par atributo–valor de una variante.</summary>
public sealed record ValorDeVarianteDto(string AttributeCode, string AttributeName, string ValueCode, string ValueName);

/// <summary>Una variante de una plantilla.</summary>
public sealed record VarianteDto(Guid PublicId, string Code, string Name, string VariantKey, int Status, IReadOnlyList<ValorDeVarianteDto> Values, string? Barcode);

/// <summary>Lo que dejó la generación: las creadas y las combinaciones que ya existían.</summary>
public sealed record VariantesGeneradasDto(Guid TemplatePublicId, IReadOnlyList<VarianteDto> Created, IReadOnlyList<string> AlreadyExisting);

/// <summary>Un atributo elegido y sus valores.</summary>
public sealed record AtributoElegidoRequest(Guid AttributePublicId, IReadOnlyList<Guid> ValuePublicIds);

/// <summary>Lo que se cambia de una variante propuesta, por su <c>variantKey</c>; vacío = lo propuesto.</summary>
public sealed record VarianteAjustadaRequest(string VariantKey, string? Code = null, string? Name = null, string? Barcode = null);

/// <summary>El cuerpo de <c>POST /products/{id}/variants</c>.</summary>
public sealed record GenerarVariantesRequest(IReadOnlyList<AtributoElegidoRequest> Attributes, IReadOnlyList<VarianteAjustadaRequest>? Adjustments = null);

// ----------------------------------------------------------------------------------------------- componentes --

/// <summary>Un componente de un combo o kit: el producto, su clase, su unidad base y la cantidad por unidad.</summary>
public sealed record ComponenteDelProductoDto(Guid PublicId, ReferenciaDeInventarioDto Component, int Kind, UnidadDelProductoDto BaseUnit, decimal Quantity);

/// <summary>Los componentes vigentes de un combo o kit.</summary>
public sealed record ComponentesDelProductoDto(Guid ProductPublicId, string Code, int Kind, IReadOnlyList<ComponenteDelProductoDto> Components);

/// <summary>Un componente pedido.</summary>
public sealed record ComponentePedidoRequest(Guid ComponentProductPublicId, decimal Quantity);

/// <summary>El cuerpo de <c>PUT /products/{id}/components</c>: la lista nueva completa.</summary>
public sealed record ComponentesRequest(IReadOnlyList<ComponentePedidoRequest> Components);

// ------------------------------------------------------------------------------------------ lotes y series --

/// <summary>Un lote con existencia, en orden FEFO; <see cref="Suggested"/> es el que la salida toma primero.</summary>
public sealed record LoteDto(Guid PublicId, string Code, DateOnly? ExpiryDate, DateOnly? ManufactureDate, decimal Quantity, string State, bool Suggested)
{
    public bool Vencido => State == TextosDeInventario.LoteVencido;
    public bool ProximoAVencer => State == TextosDeInventario.LoteProximoAVencer;
}

/// <summary>Una serie: dónde está si está en existencia y su lote.</summary>
public sealed record SerieDto(Guid PublicId, string SerialNumber, string? LotCode, BodegaDeExistenciaDto? Warehouse, UbicacionDeExistenciaDto? Location, bool InStock);

// ------------------------------------------------------------------------------------------------ ensamble --

/// <summary>El <c>assembly</c> del borrador de un ensamble (§10): el kit y cuántos; el servidor propone los componentes.</summary>
public sealed record EnsambleRequest(Guid KitProductPublicId, decimal Quantity);
