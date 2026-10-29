using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IngenIA365ERP.Domain.Receipts.Huellas;

/// <summary>Un movimiento del extracto tal como lo leyó el formato, con lo que la huella necesita (feature 015, T0190; FR-197). (nuevo)</summary>
/// <param name="Linea">El número de la línea en el archivo (o en la captura), para nombrar los duplicados.</param>
/// <param name="CodigoDeBanco">El código del banco de la cuenta (su <c>TransferCode</c>, o el Id si no lo tiene).</param>
/// <param name="Cuenta">El número de la cuenta propia.</param>
/// <param name="Fecha">La fecha del movimiento en el banco.</param>
/// <param name="Valor">El valor con signo: positivo es un abono, negativo un cargo.</param>
/// <param name="Referencia">La referencia del movimiento, si la trae.</param>
/// <param name="SecuenciaDelBanco">La secuencia o el identificador único del banco, si el formato la trae.</param>
public sealed record MovimientoParaHuella(int Linea, string CodigoDeBanco, string Cuenta, DateOnly Fecha, decimal Valor, string? Referencia, string? SecuenciaDelBanco);

/// <summary>La huella de un movimiento y si viene del identificador del banco (con identificador, el duplicado se omite; sin él, una persona decide). (nuevo)</summary>
public sealed record HuellaCalculada(string Huella, bool ConIdentificadorDelBanco);

/// <summary>Qué pasa con una línea al cargar el extracto (FR-066, FR-197). (nuevo)</summary>
public enum DestinoDeLaLinea
{
    /// <summary>Movimiento nuevo: se carga.</summary>
    Nueva = 1,

    /// <summary>Su huella con identificador del banco ya está cargada en la cuenta: se omite y se cuenta.</summary>
    OmitidaPorHuella = 2,

    /// <summary>Sin identificador del banco y con la misma huella que otro (ya cargado o del mismo archivo): se carga marcada «posible duplicado» para que una persona decida.</summary>
    PosibleDuplicado = 3,
}

/// <summary>La clasificación de una línea: su huella, su destino y, si es posible duplicado, la línea del mismo archivo con que coincide. (nuevo)</summary>
public sealed record LineaClasificada(int Linea, HuellaCalculada Huella, DestinoDeLaLinea Destino, int? DuplicaLineaDelArchivo);

/// <summary>
/// El motor puro de la huella de un movimiento bancario (feature 015, T0190; R-06; FR-066, FR-196, FR-197; DT §2.4), sin IO: lo usa Tesorería al cargar el extracto
/// (y Recaudo en R2 con los archivos de convenio), por eso vive en <c>Domain/Receipts/Huellas</c>.
/// <list type="bullet">
/// <item>con la <b>secuencia del banco</b>, la huella es banco + cuenta + secuencia: un movimiento ya cargado se reconoce y se <b>omite</b> (se cuenta);</item>
/// <item>sin ella, banco + cuenta + fecha + valor + referencia (normalizada: sin espacios sobrantes y en mayúsculas): dos movimientos iguales —en el mismo
/// archivo o contra lo cargado— no se omiten, se marcan <b>posibles duplicados</b> para que una persona decida;</item>
/// <item>el archivo entero tiene su propia huella (<see cref="DelArchivo"/>, SHA-256 de los bytes): un archivo ya cargado se reconoce.</item>
/// </list>
/// Las huellas son SHA-256 en hexadecimal minúscula del texto canónico, con la cultura invariante. (nuevo)
/// </summary>
public static class HuellaDeMovimientoBancario
{
    /// <summary>La huella de un movimiento.</summary>
    public static HuellaCalculada Calcular(MovimientoParaHuella m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var banco = Normalizar(m.CodigoDeBanco);
        var cuenta = Normalizar(m.Cuenta);
        if (!string.IsNullOrWhiteSpace(m.SecuenciaDelBanco))
            return new HuellaCalculada(Sha256($"SEQ|{banco}|{cuenta}|{Normalizar(m.SecuenciaDelBanco)}"), true);
        var texto = string.Create(CultureInfo.InvariantCulture,
            $"MOV|{banco}|{cuenta}|{m.Fecha:yyyy-MM-dd}|{decimal.Round(m.Valor, 2):0.00}|{Normalizar(m.Referencia)}");
        return new HuellaCalculada(Sha256(texto), false);
    }

    /// <summary>La huella del archivo entero (FR-197): un archivo ya cargado se reconoce por ella.</summary>
    public static string DelArchivo(byte[] contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        return Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();
    }

    /// <summary>
    /// Clasifica las líneas de un extracto contra las huellas ya cargadas en la cuenta: con identificador del banco, la que ya está se omite; sin él, la repetida
    /// (contra lo cargado o contra otra línea del mismo archivo) queda como posible duplicado. Dos líneas con el mismo identificador del banco en el mismo archivo:
    /// la segunda se omite (el banco no repite su secuencia).
    /// </summary>
    public static IReadOnlyList<LineaClasificada> Clasificar(IReadOnlyList<MovimientoParaHuella> movimientos, IReadOnlySet<string> yaCargadas)
    {
        ArgumentNullException.ThrowIfNull(movimientos);
        ArgumentNullException.ThrowIfNull(yaCargadas);
        var vistasEnElArchivo = new Dictionary<string, int>(StringComparer.Ordinal);
        var resultado = new List<LineaClasificada>(movimientos.Count);
        foreach (var m in movimientos)
        {
            var huella = Calcular(m);
            var enElArchivo = vistasEnElArchivo.TryGetValue(huella.Huella, out var primera);
            if (huella.ConIdentificadorDelBanco)
            {
                resultado.Add(new LineaClasificada(m.Linea, huella,
                    yaCargadas.Contains(huella.Huella) || enElArchivo ? DestinoDeLaLinea.OmitidaPorHuella : DestinoDeLaLinea.Nueva, null));
            }
            else if (yaCargadas.Contains(huella.Huella) || enElArchivo)
            {
                resultado.Add(new LineaClasificada(m.Linea, huella, DestinoDeLaLinea.PosibleDuplicado, enElArchivo ? primera : null));
            }
            else
            {
                resultado.Add(new LineaClasificada(m.Linea, huella, DestinoDeLaLinea.Nueva, null));
            }
            if (!enElArchivo) vistasEnElArchivo[huella.Huella] = m.Linea;
        }
        return resultado;
    }

    private static string Normalizar(string? texto) =>
        string.Join(' ', (texto ?? string.Empty).Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string Sha256(string texto) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();
}
