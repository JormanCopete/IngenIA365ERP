using System.Text;

namespace IngenIA365ERP.Domain.Lending.Documents;

/// <summary>
/// El código de verificación de un documento emitido de Cartera (feature 013, US13, T0777; T80; adaptadores-y-archivos.md §8.4; data-model §27.1):
/// 80 bits aleatorios en <b>Crockford base32</b> —16 caracteres de <see cref="Alfabeto"/>, sin <c>I</c>, <c>L</c>, <c>O</c> ni <c>U</c>—, mostrados
/// <c>XXXX-XXXX-XXXX-XXXX</c>. Motor puro: la aleatoriedad la pone quien llama (<c>RandomNumberGenerator</c> en producción, bytes fijos en las
/// pruebas), así la colisión con un código ya emitido se resuelve generando otro antes de guardar. Al verificar se normaliza lo que digitó la
/// persona: mayúsculas, sin guiones ni espacios, <c>I</c> y <c>L</c> → <c>1</c>, <c>O</c> → <c>0</c>. (nuevo)
/// </summary>
public static class CodigoDeVerificacion
{
    /// <summary>El alfabeto de Crockford base32: dígitos y letras sin <c>I</c>, <c>L</c>, <c>O</c> ni <c>U</c>.</summary>
    public const string Alfabeto = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Caracteres del código guardado (<c>char(16)</c>).</summary>
    public const int Largo = 16;

    /// <summary>Bytes aleatorios que lo forman: 80 bits = 16 grupos de 5.</summary>
    public const int BytesAleatorios = 10;

    /// <summary>Caracteres por grupo al mostrarlo.</summary>
    public const int CaracteresPorGrupo = 4;

    private const int BitsPorCaracter = 5;
    private const int MascaraDeCaracter = 0b1_1111;

    /// <summary>
    /// Un código nuevo con los <see cref="BytesAleatorios"/> que entrega <paramref name="fuente"/> (se le pide esa cantidad). Lanza si la fuente
    /// entrega otra cantidad: un código con menos entropía no es un código.
    /// </summary>
    public static string Generar(Func<int, byte[]> fuente)
    {
        ArgumentNullException.ThrowIfNull(fuente);
        var bytes = fuente(BytesAleatorios);
        if (bytes is null || bytes.Length != BytesAleatorios)
            throw new ArgumentException($"La fuente de aleatoriedad debe entregar {BytesAleatorios} bytes.", nameof(fuente));

        var texto = new StringBuilder(Largo);
        var acumulado = 0;
        var bits = 0;
        foreach (var b in bytes)
        {
            acumulado = (acumulado << 8) | b;
            bits += 8;
            while (bits >= BitsPorCaracter)
            {
                bits -= BitsPorCaracter;
                texto.Append(Alfabeto[(acumulado >> bits) & MascaraDeCaracter]);
            }
            acumulado &= (1 << bits) - 1;
        }
        return texto.ToString();
    }

    /// <summary>El código en cuatro grupos de cuatro: <c>XXXX-XXXX-XXXX-XXXX</c>. Acepta el guardado o uno ya normalizable.</summary>
    public static string Formatear(string codigo)
    {
        var normal = Normalizar(codigo) ?? throw new ArgumentException("No es un código de verificación válido.", nameof(codigo));
        var grupos = Enumerable.Range(0, Largo / CaracteresPorGrupo).Select(i => normal.Substring(i * CaracteresPorGrupo, CaracteresPorGrupo));
        return string.Join('-', grupos);
    }

    /// <summary>
    /// Lo que digitó o leyó la persona, en la forma guardada (16 caracteres del alfabeto), o nulo si no puede ser un código: mayúsculas, sin
    /// guiones ni espacios, <c>I</c>/<c>L</c> → <c>1</c> y <c>O</c> → <c>0</c> (las confusiones que Crockford perdona).
    /// </summary>
    public static string? Normalizar(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada)) return null;
        var texto = new StringBuilder(Largo);
        foreach (var c in entrada.Trim().ToUpperInvariant())
        {
            if (c == '-' || char.IsWhiteSpace(c)) continue;
            var normal = c switch
            {
                'I' or 'L' => '1',
                'O' => '0',
                _ => c,
            };
            if (!Alfabeto.Contains(normal, StringComparison.Ordinal)) return null;
            texto.Append(normal);
            if (texto.Length > Largo) return null;
        }
        return texto.Length == Largo ? texto.ToString() : null;
    }
}
