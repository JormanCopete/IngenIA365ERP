using System.Text.RegularExpressions;

namespace IngenIA365ERP.Application.Common.ExternalChannels;

/// <summary>
/// Los códigos de canal externo que conoce la plataforma (feature 013, T0079; T71, T72; contracts/adaptadores-y-archivos.md §3.3, §3.4) y
/// la forma de un código. El código también arma la ruta del archivo de credencial, por eso es sólo mayúsculas, dígitos y guiones
/// (<c>^[A-Z0-9-]{2,30}$</c>): no puede salir del directorio del Secret. (nuevo)
/// </summary>
public static partial class CodigosDeCanalExterno
{
    /// <summary>El adaptador de prueba de cada puerto. Existe en todo ambiente y nunca tiene efecto legal en producción (T73).</summary>
    public const string Simulado = "SIMULADO";

    /// <summary>Las listas restrictivas que carga el oficial de cumplimiento. <b>No</b> es simulado: vale en producción.</summary>
    public const string CargaPropia = "CARGA-PROPIA";

    /// <summary>El correo de la plataforma (<c>CanalDeCorreo</c> sobre <c>IEmailSender</c>). No se configura por cooperativa.</summary>
    public const string Correo = "CORREO";

    [GeneratedRegex("^[A-Z0-9-]{2,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex Forma();

    /// <summary>¿<paramref name="codigo"/> (ya en mayúsculas) es un código de canal válido?</summary>
    public static bool EsValido(string? codigo) => codigo is not null && Forma().IsMatch(codigo);

    /// <summary>El código en mayúsculas y sin espacios alrededor, o nulo si no es válido.</summary>
    public static string? Normalizar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        var normal = codigo.Trim().ToUpperInvariant();
        return EsValido(normal) ? normal : null;
    }

    /// <summary>¿El canal no pide archivo de credencial? (<c>SIMULADO</c> y <c>CARGA-PROPIA</c>, §3.4).</summary>
    public static bool NoPideCredencial(string? codigo) =>
        string.Equals(codigo, Simulado, StringComparison.OrdinalIgnoreCase)
        || string.Equals(codigo, CargaPropia, StringComparison.OrdinalIgnoreCase);

    /// <summary>¿Es el canal simulado?</summary>
    public static bool EsSimulado(string? codigo) => string.Equals(codigo, Simulado, StringComparison.OrdinalIgnoreCase);
}
