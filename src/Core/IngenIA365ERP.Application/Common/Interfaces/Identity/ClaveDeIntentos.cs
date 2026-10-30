namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// La llave con que se cuentan los intentos de contraseña (feature 017, research R5). Desde que se
/// entra con correo <b>o</b> usuario, contar por el texto escrito daba el doble de intentos a quien
/// alternara las dos formas: se cuenta por <b>cuenta</b> cuando el texto corresponde a una, y por el
/// texto normalizado cuando no.
/// <para>
/// El segundo factor (<see cref="AmbitoDeIntentos.Mfa"/>) sigue contando por correo: a él sólo se
/// llega con la contraseña ya acertada y el correo sale del token, no de lo que se escribe.
/// </para>
/// </summary>
public static class ClaveDeIntentos
{
    public static string DeCuenta(Guid centralUserId) => "u:" + centralUserId.ToString("N");

    public static string DeIdentificador(string normalizado) => "i:" + normalizado;
}
