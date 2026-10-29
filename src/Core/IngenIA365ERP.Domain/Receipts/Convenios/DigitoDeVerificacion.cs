using System.Text;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Receipts.Convenios;

/// <summary>Por qué una referencia de pago no se acepta. (nuevo)</summary>
public enum MotivoDeReferenciaInvalida
{
    Ninguno = 0,
    Vacia = 1,
    LongitudInvalida = 2,
    CaracterInvalido = 3,
    DigitoInvalido = 4,
}

/// <summary>Lo que se lee de una referencia de pago: si vale, su base (sin el dígito), su dígito y el motivo si no vale. (nuevo)</summary>
public sealed record LecturaDeReferencia(bool Valida, string Base, string Digito, MotivoDeReferenciaInvalida Motivo)
{
    public static LecturaDeReferencia Invalida(MotivoDeReferenciaInvalida motivo, string texto = "") => new(false, texto, string.Empty, motivo);
}

/// <summary>
/// La estructura de la referencia de pago de un convenio (feature 015, R2, fase 38; FR-088, FR-090): la longitud total con el dígito (cero = la que traiga) y el
/// algoritmo del dígito. La referencia se compone con <see cref="DigitoDeVerificacion.Componer"/> y se lee con <see cref="Leer"/>. Pura: no conoce la base. (nuevo)
/// </summary>
public sealed record EstructuraDeReferencia(int Longitud, CheckDigitAlgorithm Algoritmo)
{
    /// <summary>Lee una referencia tal como llegó en el archivo: quita espacios, exige la longitud si la hay y comprueba el dígito.</summary>
    public LecturaDeReferencia Leer(string? texto)
    {
        var limpia = DigitoDeVerificacion.Normalizar(texto);
        if (limpia.Length == 0) return LecturaDeReferencia.Invalida(MotivoDeReferenciaInvalida.Vacia);
        if (Longitud > 0 && limpia.Length != Longitud)
        {
            // Los archivos de los bancos rellenan con ceros a la izquierda: se aceptan si, sin ellos, la referencia cabe.
            var sinCeros = limpia.TrimStart('0');
            if (sinCeros.Length == 0 || sinCeros.Length > Longitud) return LecturaDeReferencia.Invalida(MotivoDeReferenciaInvalida.LongitudInvalida, limpia);
            if (limpia.Length < Longitud) limpia = limpia.PadLeft(Longitud, '0');
            else limpia = sinCeros.PadLeft(Longitud, '0');
        }
        if (limpia.Any(c => !char.IsAsciiLetterOrDigit(c))) return LecturaDeReferencia.Invalida(MotivoDeReferenciaInvalida.CaracterInvalido, limpia);
        if (Algoritmo == CheckDigitAlgorithm.None) return new LecturaDeReferencia(true, limpia, string.Empty, MotivoDeReferenciaInvalida.Ninguno);
        if (limpia.Length < 2) return LecturaDeReferencia.Invalida(MotivoDeReferenciaInvalida.LongitudInvalida, limpia);
        var baseDeLaReferencia = limpia[..^1];
        var digito = limpia[^1..];
        return DigitoDeVerificacion.Calcular(baseDeLaReferencia, Algoritmo) == digito
            ? new LecturaDeReferencia(true, baseDeLaReferencia, digito, MotivoDeReferenciaInvalida.Ninguno)
            : new LecturaDeReferencia(false, baseDeLaReferencia, digito, MotivoDeReferenciaInvalida.DigitoInvalido);
    }
}

/// <summary>
/// El dígito de verificación de la referencia de pago (feature 015, R2, fase 38; FR-088; pedido del dueño para R2): módulo 10 (Luhn) o módulo 11 (pesos 2 a 7
/// desde la derecha, como el de los bancos colombianos; 10 se escribe «1» y 11 «0»). Las letras de una base alfanumérica valen su posición en el alfabeto más 9
/// (A = 10 … Z = 35), así un número de producto con prefijo también lleva dígito. Pura y determinista: la misma base da siempre la misma referencia. (nuevo)
/// </summary>
public static class DigitoDeVerificacion
{
    /// <summary>La referencia sin espacios, guiones ni puntos, en mayúsculas.</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Trim())
        {
            if (c is ' ' or '-' or '.' or '/' or '_') continue;
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>El dígito de <paramref name="baseDeLaReferencia"/> por <paramref name="algoritmo"/>; vacío sin algoritmo.</summary>
    public static string Calcular(string baseDeLaReferencia, CheckDigitAlgorithm algoritmo)
    {
        var b = Normalizar(baseDeLaReferencia);
        if (algoritmo == CheckDigitAlgorithm.None || b.Length == 0) return string.Empty;
        var digitos = Expandir(b);
        return algoritmo switch
        {
            CheckDigitAlgorithm.Modulo10 => Luhn(digitos).ToString(System.Globalization.CultureInfo.InvariantCulture),
            CheckDigitAlgorithm.Modulo11 => Modulo11(digitos),
            _ => string.Empty,
        };
    }

    /// <summary>La referencia completa: la base (rellena con ceros a la izquierda hasta <paramref name="longitud"/> menos el dígito, si se da) y su dígito.</summary>
    public static string Componer(string baseDeLaReferencia, CheckDigitAlgorithm algoritmo, int longitud = 0)
    {
        var b = Normalizar(baseDeLaReferencia);
        var largoDelDigito = algoritmo == CheckDigitAlgorithm.None ? 0 : 1;
        if (longitud > 0 && b.Length + largoDelDigito < longitud) b = b.PadLeft(longitud - largoDelDigito, '0');
        return b + Calcular(b, algoritmo);
    }

    /// <summary>Si <paramref name="referencia"/> trae el dígito correcto para <paramref name="algoritmo"/>.</summary>
    public static bool EsValida(string referencia, CheckDigitAlgorithm algoritmo) => new EstructuraDeReferencia(0, algoritmo).Leer(referencia).Valida;

    private static List<int> Expandir(string b)
    {
        var lista = new List<int>(b.Length * 2);
        foreach (var c in b)
        {
            if (char.IsAsciiDigit(c)) lista.Add(c - '0');
            else if (char.IsAsciiLetterUpper(c))
            {
                var valor = c - 'A' + 10;
                lista.Add(valor / 10);
                lista.Add(valor % 10);
            }
        }
        return lista;
    }

    private static int Luhn(IReadOnlyList<int> digitos)
    {
        var suma = 0;
        var doblar = true;
        for (var i = digitos.Count - 1; i >= 0; i--)
        {
            var d = digitos[i];
            if (doblar)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            suma += d;
            doblar = !doblar;
        }
        return (10 - suma % 10) % 10;
    }

    private static string Modulo11(IReadOnlyList<int> digitos)
    {
        var suma = 0;
        var peso = 2;
        for (var i = digitos.Count - 1; i >= 0; i--)
        {
            suma += digitos[i] * peso;
            peso = peso == 7 ? 2 : peso + 1;
        }
        var resto = 11 - suma % 11;
        return resto switch { 11 => "0", 10 => "1", _ => resto.ToString(System.Globalization.CultureInfo.InvariantCulture) };
    }
}
