using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IngenIA365ERP.Domain.Lending;

/// <summary>
/// La huella SHA-256 canónica de Cartera (feature 013, T0276; M15, §2.8; data-model §0.13): el <c>ContentSha256</c> de matrices de
/// atribución, actas y decisiones, y después de versiones de línea y simulaciones (US1, US2). Molde de <c>InputsHasher</c> de Nómina:
/// se arma a mano, campo por campo, y no serializando objetos, para que ni el orden de las propiedades, ni las navegaciones de EF, ni
/// la cultura del hilo cambien el resultado. (nuevo)
///
/// <para>El texto canónico es un JSON sin espacios con las claves de cada objeto en orden <b>ordinal</b> (no el de las llamadas),
/// decimales con punto, fechas <c>yyyy-MM-dd</c>, instantes <c>yyyy-MM-ddTHH:mm:ss[.fffffff]</c> (sin zona: Cartera guarda la hora
/// local de Colombia, T9) e identificadores en minúscula con guiones. Las listas conservan el orden que les da quien llama: si el orden
/// no es dato, quien llama las ordena antes. La huella es el SHA-256 del texto en UTF-8, en hexadecimal minúscula de 64.</para>
///
/// <para>Decimales: sin precisión declarada se escriben normalizados (<c>0.10</c> y <c>0.1</c> son el mismo valor y dan la misma
/// huella, aunque <c>decimal</c> guarde la escala); con precisión declarada se escriben con exactamente esos decimales, y un valor con
/// más decimales que los declarados se rechaza en vez de redondearse, porque una huella que redondea esconde un cambio.</para>
/// </summary>
public sealed class HuellaDeCartera
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly SortedDictionary<string, string> _campos = new(StringComparer.Ordinal);

    public HuellaDeCartera Texto(string clave, string? valor) => Poner(clave, valor is null ? "null" : Cadena(valor));

    public HuellaDeCartera Entero(string clave, long? valor) => Poner(clave, valor?.ToString(Inv) ?? "null");

    public HuellaDeCartera Logico(string clave, bool? valor) => Poner(clave, valor is null ? "null" : valor.Value ? "true" : "false");

    /// <summary>Un decimal; con <paramref name="decimales"/>, escrito con exactamente esa cantidad (y rechazado si trae más).</summary>
    public HuellaDeCartera Decimal(string clave, decimal? valor, int? decimales = null) =>
        Poner(clave, valor is null ? "null" : Numero(clave, valor.Value, decimales));

    public HuellaDeCartera Fecha(string clave, DateOnly? valor) =>
        Poner(clave, valor is null ? "null" : Cadena(valor.Value.ToString("yyyy-MM-dd", Inv)));

    /// <summary>Un instante, tal como viene (hora local de Colombia en Cartera); las fracciones de segundo sólo si las hay.</summary>
    public HuellaDeCartera Instante(string clave, DateTime? valor) =>
        Poner(clave, valor is null ? "null" : Cadena(valor.Value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", Inv)));

    public HuellaDeCartera Hora(string clave, TimeOnly? valor) =>
        Poner(clave, valor is null ? "null" : Cadena(valor.Value.ToString("HH:mm:ss.FFFFFFF", Inv)));

    public HuellaDeCartera Identificador(string clave, Guid? valor) =>
        Poner(clave, valor is null ? "null" : Cadena(valor.Value.ToString("D", Inv)));

    /// <summary>Una enumeración, por su número: los números de Cartera no se renumeran nunca (C65); los nombres podrían cambiar.</summary>
    public HuellaDeCartera Enumeracion<TEnum>(string clave, TEnum? valor) where TEnum : struct, Enum =>
        Poner(clave, valor is null ? "null" : Convert.ToInt64(valor.Value, Inv).ToString(Inv));

    /// <summary>Un objeto anidado.</summary>
    public HuellaDeCartera Objeto(string clave, Action<HuellaDeCartera> llenar)
    {
        var hijo = new HuellaDeCartera();
        llenar(hijo);
        return Poner(clave, hijo.Canonico());
    }

    /// <summary>Una lista de objetos, en el orden recibido.</summary>
    public HuellaDeCartera Lista<T>(string clave, IEnumerable<T> elementos, Action<HuellaDeCartera, T> llenar)
    {
        var partes = elementos.Select(e =>
        {
            var hijo = new HuellaDeCartera();
            llenar(hijo, e);
            return hijo.Canonico();
        });
        return Poner(clave, "[" + string.Join(",", partes) + "]");
    }

    /// <summary>Una lista de textos, en el orden recibido.</summary>
    public HuellaDeCartera Textos(string clave, IEnumerable<string?> valores) =>
        Poner(clave, "[" + string.Join(",", valores.Select(v => v is null ? "null" : Cadena(v))) + "]");

    /// <summary>El JSON canónico.</summary>
    public string Canonico() =>
        "{" + string.Join(",", _campos.Select(c => Cadena(c.Key) + ":" + c.Value)) + "}";

    /// <summary>El SHA-256 del JSON canónico, en hexadecimal minúscula de 64.</summary>
    public string Calcular() => DeTexto(Canonico());

    /// <summary>El SHA-256 de un texto canónico ya armado (para verificar una huella guardada).</summary>
    public static string DeTexto(string canonico) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonico)));

    public override string ToString() => Canonico();

    private HuellaDeCartera Poner(string clave, string valorCanonico)
    {
        ArgumentException.ThrowIfNullOrEmpty(clave);
        if (!_campos.TryAdd(clave, valorCanonico))
            throw new ArgumentException($"La clave «{clave}» ya está en la huella: cada dato entra una sola vez.", nameof(clave));
        return this;
    }

    private static string Numero(string clave, decimal valor, int? decimales)
    {
        if (decimales is not { } d)
        {
            // «0.############################» quita los ceros sobrantes de la escala (0.10m → 0.1); 28 es la escala máxima de decimal.
            var normalizado = valor.ToString("0.############################", Inv);
            return normalizado == "-0" ? "0" : normalizado;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(d, nameof(decimales));
        if (System.Decimal.Round(valor, d) != valor)
            throw new ArgumentException(
                $"El valor {valor.ToString(Inv)} de «{clave}» tiene más de {d} decimales; la huella no redondea.", nameof(valor));
        return valor.ToString("F" + d.ToString(Inv), Inv);
    }

    private static string Cadena(string valor)
    {
        var sb = new StringBuilder(valor.Length + 2);
        sb.Append('"');
        foreach (var c in valor)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                default:
                    if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", Inv));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
