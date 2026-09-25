using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace IngenIA365ERP.Application.Common.Imports;

/// <summary>
/// Una plantilla de importación (feature 012, T49, T156; contracts/plantillas.md §0): sus hojas de datos con sus
/// columnas. La misma definición arma el libro vacío o lleno (<c>PlantillaDeImportacion</c> en la API), su hoja
/// «Instrucciones», y es lo que <see cref="EjecutorDeImportacion"/> exige al leer. (nuevo)
/// </summary>
/// <param name="Clave">La que viaja en <see cref="ImportResultDto.Template"/> (<c>inventory.products</c>).</param>
/// <param name="Nombre">El título de la hoja «Instrucciones».</param>
/// <param name="Modulo">El módulo de auditoría del evento de la importación (<c>ModuloDeAuditoria</c>).</param>
/// <param name="Hojas">Las hojas de datos, en el orden del libro. Una sola hoja se llama <c>Datos</c> y admite <c>.csv</c>.</param>
public sealed record DefinicionDePlantilla(string Clave, string Nombre, string Modulo, IReadOnlyList<HojaDePlantilla> Hojas)
{
    /// <summary>Una sola hoja de datos: se lee la primera del libro o el <c>.csv</c>, y los errores no llevan hoja.</summary>
    public bool EsDeUnaHoja => Hojas.Count == 1;

    public HojaDePlantilla? Hoja(string nombre) => Hojas.FirstOrDefault(h => string.Equals(h.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Una hoja de datos (nuevo). <see cref="Permiso"/>: el permiso que exige traer filas en ella (§0.7).</summary>
public sealed record HojaDePlantilla(string Nombre, IReadOnlyList<ColumnaDePlantilla> Columnas, bool Obligatoria = true, string? Permiso = null)
{
    public ColumnaDePlantilla? Columna(string encabezado) =>
        Columnas.FirstOrDefault(c => Interfaces.Files.TablaLeida.Normalizar(c.Nombre) == Interfaces.Files.TablaLeida.Normalizar(encabezado));
}

/// <summary>
/// Una columna (nuevo): encabezado en <c>camelCase</c> español, su tipo de §0.4, si es obligatoria, su largo (códigos y
/// textos), el permiso que exige llenarla (§0.7), sus reglas y un ejemplo para la hoja «Instrucciones».
/// </summary>
public sealed record ColumnaDePlantilla(
    string Nombre,
    TipoDeValor Tipo,
    bool Obligatoria = false,
    int? Largo = null,
    string? Permiso = null,
    string? Reglas = null,
    string? Ejemplo = null);

/// <summary>Los tipos de valor de contracts/plantillas.md §0.4 (nuevo). Deciden la conversión y el formato de la celda.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoDeValor
{
    Codigo = 0,
    Texto = 1,
    Entero = 2,
    /// <summary>Hasta 2 decimales.</summary>
    Monto = 3,
    /// <summary>Hasta 4 decimales.</summary>
    Cantidad = 4,
    /// <summary>Costo o factor: hasta 6 decimales.</summary>
    Costo = 5,
    /// <summary>En puntos, hasta 4 decimales; se guarda como fracción (19 → 0,19).</summary>
    Porcentaje = 6,
    Fecha = 7,
    Hora = 8,
    SiNo = 9,
    /// <summary>Como sí/no, pero vacío = no importa.</summary>
    SiNoIndiferente = 10,
    /// <summary>Valores separados por coma; <c>*</c> = todos.</summary>
    Lista = 11,
    Enumeracion = 12,
    /// <summary>Documento de identidad sin dígito de verificación.</summary>
    Persona = 13,
    Sucursal = 14,
    CentroDeCosto = 15,
    Banco = 16,
    CuentaContable = 17,
}

/// <summary>
/// El archivo que llega a una importación (nuevo). El contenido no viaja al JSON (ni a la auditoría ni a la huella de
/// idempotencia): en su lugar va <see cref="Sha256"/>, así la misma clave con otro archivo es otro contenido
/// (<c>Operation.KeyReused</c>) y el evento nombra el archivo sin guardarlo (§0.5: «el archivo no se guarda»).
/// </summary>
public sealed record ArchivoDeImportacion(string NombreArchivo, [property: JsonIgnore] byte[] Contenido)
{
    /// <summary>La huella SHA-256 del contenido, en hexadecimal minúscula.</summary>
    public string Sha256 => Convert.ToHexStringLower(SHA256.HashData(Contenido ?? []));
}

/// <summary>
/// Lo común de todo comando de importación (nuevo; T49): modo, archivo y motivo, con clave de idempotencia. El motivo
/// sólo se exige cuando la revisión dice <see cref="ImportResultDto.RequiresReason"/>, así que no lleva
/// <c>ValidadorConMotivo</c>; implementa <see cref="Behaviors.IConMotivo"/> para que la auditoría lo copie al evento.
/// </summary>
public interface IComandoDeImportacion : Behaviors.IOperacionIdempotente, Behaviors.IConMotivo
{
    /// <summary>Nulo si la ruta no lo trajo: <see cref="ImportErrors.ModeRequired"/>.</summary>
    ModoDeImportacion? Mode { get; }

    ArchivoDeImportacion File { get; }
}
