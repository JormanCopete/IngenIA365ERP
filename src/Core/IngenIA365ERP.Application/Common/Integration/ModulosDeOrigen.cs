namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Los módulos que emiten mensajes a la bandeja (feature 013, T10; DT §2.12; contracts/mensajes.md §13.1): 3 caracteres,
/// guardados en <c>COR_IntegrationMessages.OriginModule</c>. Lo declara el tipo en el catálogo (<see cref="CatalogoDeMensajes"/>),
/// nunca la solicitud de emisión. <c>CAR</c> y <c>TES</c> coinciden con <c>ModuloContable</c>. (nuevo)
/// </summary>
public static class ModulosDeOrigen
{
    /// <summary>Comercial (Inventario, ventas y compras).</summary>
    public const string Inventario = "INV";

    /// <summary>Cartera financiera.</summary>
    public const string Cartera = "CAR";

    /// <summary>Tesorería.</summary>
    public const string Tesoreria = "TES";

    /// <summary>Recaudo.</summary>
    public const string Recaudo = "REC";

    public static readonly IReadOnlyList<string> Todos = [Inventario, Cartera, Tesoreria, Recaudo];
}
