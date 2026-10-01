using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Components.Inventario;

// Lo que el componente CatalogoDeInventario (feature 012, T235) sabe de cualquier catálogo simple: la fila común y los campos
// propios de cada uno. Cada página traduce su DTO a FilaDeCatalogo y dice cómo guarda. (nuevo)

/// <summary>Cómo se edita un campo propio del catálogo.</summary>
public enum TipoDeCampoDeCatalogo
{
    Texto = 0,
    Entero = 1,
    SiNo = 2,
    Lista = 3,
}

/// <summary>
/// Un campo propio de un catálogo (<c>decimales</c> y <c>codigoDian</c> de las unidades, <c>padre</c> de las categorías…).
/// <see cref="Opciones"/> arma la lista con las filas cargadas y la que se edita (así el padre no se ofrece a sí mismo).
/// </summary>
public sealed record CampoDeCatalogo(
    string Clave,
    string Etiqueta,
    TipoDeCampoDeCatalogo Tipo = TipoDeCampoDeCatalogo.Texto,
    bool EnLista = true,
    bool SoloAlCrear = false,
    string? PorDefecto = null,
    int? Largo = null,
    string? Ayuda = null,
    string? TextoVacio = null,
    Func<IReadOnlyList<FilaDeCatalogo>, FilaDeCatalogo?, IEnumerable<(string Valor, string Texto)>>? Opciones = null,
    Func<string?, string>? Mostrar = null,
    bool EnFormulario = true);

/// <summary>Una fila de un catálogo simple: lo común y los valores de sus campos propios como texto.</summary>
public sealed record FilaDeCatalogo(
    Guid PublicId,
    string Code,
    string Name,
    bool IsActive,
    bool Sembrado,
    IReadOnlyDictionary<string, string?> Valores,
    string? Detalle = null);

/// <summary>Ayudas para traducir la respuesta de la API al componente.</summary>
public static class CatalogoDeInventarioModelo
{
    public static ResultadoDeInventario<IReadOnlyList<FilaDeCatalogo>> Filas<T>(ResultadoDeInventario<IReadOnlyList<T>> r, Func<T, FilaDeCatalogo> fila) =>
        new(r.IsSuccess, r.Value?.Select(fila).ToList(), r.ErrorCode, r.ErrorMessage, r.StatusCode, r.Data, r.Repetida);

    public static (bool Ok, string? Error) Resultado<T>(ResultadoDeInventario<T> r) => (r.IsSuccess, r.ErrorMessage);

    public static int? Entero(IReadOnlyDictionary<string, string?> valores, string clave) =>
        int.TryParse(valores.GetValueOrDefault(clave), out var n) ? n : null;

    public static bool SiNo(IReadOnlyDictionary<string, string?> valores, string clave) => valores.GetValueOrDefault(clave) == "true";

    public static Guid? Id(IReadOnlyDictionary<string, string?> valores, string clave) =>
        Guid.TryParse(valores.GetValueOrDefault(clave), out var g) ? g : null;

    public static string? Texto(IReadOnlyDictionary<string, string?> valores, string clave) =>
        string.IsNullOrWhiteSpace(valores.GetValueOrDefault(clave)) ? null : valores[clave]!.Trim();

    public static string Bool(bool valor) => valor ? "true" : "false";
}
