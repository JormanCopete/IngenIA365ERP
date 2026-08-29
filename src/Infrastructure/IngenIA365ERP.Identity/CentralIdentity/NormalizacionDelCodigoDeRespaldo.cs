namespace IngenIA365ERP.Identity.CentralIdentity;

/// <summary>
/// Deja el código de respaldo como lo emitió Identity, antes de compararlo.
///
/// <para>
/// <c>RedeemTwoFactorRecoveryCodeAsync</c> compara <b>ordinal</b> contra la lista
/// guardada, así que <c>bwjrf-p88vc</c> no canjea el código <c>BWJRF-P88VC</c>:
/// falla por cómo se tecleó, no por ser incorrecto. Y el validador de entrada
/// acepta minúsculas (<c>^[A-Za-z0-9-]+$</c>), de modo que pasan la validación y
/// mueren en la comparación — el peor sitio donde morir, porque el mensaje que
/// sale es «código inválido».
/// </para>
///
/// <para>
/// Que además <b>cuesta caro</b>: cada canje fallido llama a
/// <c>RecordFailureAsync</c> sobre el ámbito del segundo factor, y ese contador
/// sólo baja al acertar. Cinco intentos en minúsculas bloquean un minuto, diez
/// bloquean cinco, y a partir de veinte es una hora por intento. Quien tiene el
/// papel correcto en la mano se va bloqueando solo, sin entender por qué.
/// </para>
///
/// <para>
/// Se normaliza únicamente lo que <b>no cambia la identidad del código</b>: los
/// espacios que pega cualquier copiado, las mayúsculas, y el guion separador
/// cuando falta. El alfabeto que genera Identity
/// (<c>23456789BCDFGHJKMNPQRTVWXY</c>) no tiene minúsculas ni caracteres
/// ambiguos, así que subir a mayúsculas no puede colisionar con otro código: es
/// una conversión sin pérdida, no una comparación laxa. Un código realmente
/// equivocado sigue fallando, y sigue sumando al contador — que es lo correcto.
/// </para>
/// </summary>
public static class NormalizacionDelCodigoDeRespaldo
{
    /// <summary>
    /// Identity emite <c>XXXXX-XXXXX</c>: cinco, guion, cinco.
    /// <c>UserManager.CreateTwoFactorRecoveryCode</c> lo fija así.
    /// </summary>
    private const int LongitudDeCadaMitad = 5;

    public static string Normalizar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return string.Empty;

        Span<char> limpio = stackalloc char[codigo.Length];
        var n = 0;
        foreach (var c in codigo)
        {
            if (char.IsWhiteSpace(c)) continue;
            limpio[n++] = char.ToUpperInvariant(c);
        }

        var texto = new string(limpio[..n]);

        // Quien lo teclea de corrido escribe los diez caracteres sin guion. Es el
        // mismo código: se lo devolvemos puesto en vez de rechazarlo.
        if (texto.Length == LongitudDeCadaMitad * 2 && !texto.Contains('-'))
        {
            texto = string.Concat(
                texto[..LongitudDeCadaMitad], "-", texto[LongitudDeCadaMitad..]);
        }

        return texto;
    }
}
