namespace IngenIA365ERP.Application.ElectronicInvoicing.Canonical;

/// <summary>
/// Qué versión del canónico se construye (feature 028, D6/T010–T012): la decide el <b>canal sellado</b> en la configuración del
/// documento, que no cambia después de numerar. Así reconstruir una versión ya emitida da siempre los mismos bytes y el mismo SHA-256:
/// lo sellado a <c>SIMULADO</c> sigue en v1, lo sellado a <c>CONECTA-PRUEBAS</c> o <c>CONECTA</c> sale en v2. (nuevo)
/// </summary>
public static class EsquemaDelCanonico
{
    /// <summary>El canal de IngenIA Conecta en habilitación (sólo <c>Environment = Testing</c>).</summary>
    public const string CanalConectaPruebas = "CONECTA-PRUEBAS";

    /// <summary>El canal de IngenIA Conecta en producción (sólo <c>Environment = Production</c>).</summary>
    public const string CanalConecta = "CONECTA";

    /// <summary>¿El código es uno de los dos canales de Conecta?</summary>
    public static bool EsConecta(string? channelCode) =>
        string.Equals(channelCode?.Trim(), CanalConecta, StringComparison.OrdinalIgnoreCase)
        || string.Equals(channelCode?.Trim(), CanalConectaPruebas, StringComparison.OrdinalIgnoreCase);

    /// <summary>La versión del esquema para el canal sellado.</summary>
    public static int Para(string? channelCode) =>
        EsConecta(channelCode) ? DocumentoElectronicoCanonico.VersionConDatosDeConecta : DocumentoElectronicoCanonico.VersionDelEsquema;
}

/// <summary>
/// Nombres y apellidos separados de una persona natural (feature 028): algunos proveedores (Factible, por Conecta) los exigen aparte.
/// La copia fiscal guarda el nombre completo (<c>LegalName</c>, «nombres apellidos» en ese orden) y el primer nombre y el primer
/// apellido sueltos; se parte el nombre completo donde empieza el primer apellido. Sin él, o si no se encuentra, se usan los sueltos.
/// Una persona jurídica no tiene nombres ni apellidos. Puro y determinista. (nuevo)
/// </summary>
public static class NombresDePersona
{
    /// <summary>Código DIAN de persona natural (tabla de tipos de organización).</summary>
    public const string PersonaNatural = "2";

    /// <summary>Nombres y apellidos.</summary>
    public sealed record Separados(string? Nombres, string? Apellidos);

    /// <summary>Separa el nombre completo; nulo si la persona es jurídica o no hay con qué.</summary>
    public static Separados? Separar(string? tipoDeOrganizacion, string? nombreCompleto, string? primerNombre, string? primerApellido)
    {
        if (!string.Equals(tipoDeOrganizacion?.Trim(), PersonaNatural, StringComparison.Ordinal)) return null;
        var completo = Normalizar(nombreCompleto);
        var nombre = Normalizar(primerNombre);
        var apellido = Normalizar(primerApellido);

        if (completo is not null && apellido is not null)
        {
            // El apellido como palabra completa, después de al menos un nombre.
            var buscado = " " + apellido;
            var desde = completo.IndexOf(buscado + " ", StringComparison.OrdinalIgnoreCase);
            if (desde < 0 && completo.EndsWith(buscado, StringComparison.OrdinalIgnoreCase)) desde = completo.Length - buscado.Length;
            if (desde > 0) return new Separados(completo[..desde].Trim(), completo[(desde + 1)..].Trim());
        }

        return nombre is null && apellido is null ? null : new Separados(nombre, apellido);
    }

    /// <summary>
    /// El código del departamento: los dos primeros dígitos del código DIVIPOLA del municipio (cinco dígitos: dos del departamento y tres
    /// del municipio). Nulo si el código no tiene esa forma.
    /// </summary>
    public static string? Departamento(string? codigoMunicipio)
    {
        var c = codigoMunicipio?.Trim();
        return c is { Length: 5 } && c.All(char.IsAsciiDigit) ? c[..2] : null;
    }

    private static string? Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return string.Join(' ', texto.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
