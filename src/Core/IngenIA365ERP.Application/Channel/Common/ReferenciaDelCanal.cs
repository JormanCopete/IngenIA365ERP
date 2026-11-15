namespace IngenIA365ERP.Application.Channel.Common;

/// <summary>
/// Feature 021 (research R12, T112): la referencia de un intento del canal —{código de canal (3 letras)}{AAMMDD}{secuencia base 36 (8)}{dígito}, 18
/// caracteres— y su dígito de control Luhn módulo 36, igual que <c>canal/api/internal/pagos/referencia.go</c>. La conciliación del archivo de la pasarela
/// la reconoce por su forma para buscar el aviso del canal en vez de leerla como referencia de producto. (nuevo)
/// </summary>
public static class ReferenciaDelCanal
{
    public const int Largo = 18;
    private const string Alfabeto = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>¿Tiene la forma y el dígito de una referencia del canal? Con <paramref name="codigo"/>, además debe empezar por él.</summary>
    public static bool Es(string? referencia, string? codigo = null)
    {
        if (referencia is null || referencia.Length != Largo) return false;
        for (var i = 0; i < Largo; i++)
        {
            var c = referencia[i];
            var valido = i switch
            {
                < 3 => c is >= 'A' and <= 'Z',
                < 9 => c is >= '0' and <= '9',
                _ => Alfabeto.Contains(c, StringComparison.Ordinal),
            };
            if (!valido) return false;
        }
        if (codigo is not null && !referencia.StartsWith(codigo, StringComparison.Ordinal)) return false;
        return DigitoDeControl(referencia[..^1]) == referencia[^1];
    }

    /// <summary>Luhn módulo 36 sobre 0-9A-Z.</summary>
    public static char DigitoDeControl(string cuerpo)
    {
        const int n = 36;
        int factor = 2, suma = 0;
        for (var i = cuerpo.Length - 1; i >= 0; i--)
        {
            var sumando = factor * Alfabeto.IndexOf(cuerpo[i], StringComparison.Ordinal);
            factor = 3 - factor;
            suma += sumando / n + sumando % n;
        }
        return Alfabeto[(n - suma % n) % n];
    }
}
