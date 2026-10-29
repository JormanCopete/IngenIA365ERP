using System.Globalization;
using System.Text;

namespace IngenIA365ERP.Domain.Receipts.Cumplimiento;

/// <summary>Los datos de una persona como los pide el Anexo 2 TRSO: tipo e identificación, dígito de verificación y nombres, apellidos o razón social. (nuevo)</summary>
public sealed record PersonaParaLaUiaf(int TipoDeIdentificacion, string NumeroDeIdentificacion, int DigitoDeVerificacion, string Nombres, string Apellidos, string RazonSocial);

/// <summary>
/// Las reglas puras del Anexo 2 TRSO de la Supersolidaria (feature 015, segunda ronda del 2026-10-27) para escribir los datos de las personas y los productos (nuevo):
/// <list type="bullet">
/// <item>el tipo de identificación es el código numérico de la tabla 7.4 (13 cédula, 22 cédula de extranjería, 31 NIT, 41 pasaporte, 12 tarjeta de identidad…; lo
/// demás, 00 «otro»);</item>
/// <item>la identificación va sin guiones, puntos, comas ni espacios, y sin el dígito de verificación, que va aparte: el del NIT, o -1 si no se conoce o no es NIT;</item>
/// <item>la persona natural lleva nombres y apellidos y -1 en la razón social; la jurídica, al revés;</item>
/// <item>el texto alfanumérico va en mayúsculas, sin puntos ni guiones, con sólo los caracteres que admite el anexo en CP1252 (A–Z, 0–9, espacio, Œ, Ž y las
/// mayúsculas acentuadas de À a Ý salvo ×), y lo demás se vuelve espacio;</item>
/// <item>el código de producto sale del mapa del parámetro <c>Sarlaft.Reporte.TiposDeProducto</c>, por clase y, si hay, por el prefijo más largo del número.</item>
/// </list>
/// </summary>
public static class DatosParaLaUiaf
{
    /// <summary>El valor del anexo para «no aplica» o «no se conoce».</summary>
    public const string NoAplica = "-1";

    /// <summary>El código de persona jurídica de <c>COR_People.PersonType</c>.</summary>
    public const string PersonaJuridica = "02";

    /// <summary>Tabla 7.4 desde el <c>IdType</c> de la persona.</summary>
    public static int TipoDeIdentificacion(string? idType) => (idType ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "" or "C" or "CC" => 13,
        "E" or "CE" => 22,
        "N" or "NI" or "NIT" => 31,
        "P" or "PA" or "PP" => 41,
        "T" or "TI" => 12,
        "R" or "RC" => 11,
        "TE" => 21,
        "DE" or "X" => 42,
        "SE" => 7,
        _ => 0,
    };

    /// <summary>La identificación sin separadores, en mayúsculas.</summary>
    public static string SoloAlfanumerico(string? texto) =>
        new((texto ?? string.Empty).ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());

    /// <summary>El dígito de verificación del NIT, o -1 si no es NIT o no se conoce.</summary>
    public static int DigitoDeVerificacion(int tipo, string? digito) =>
        tipo == 31 && int.TryParse(digito?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var d) && d is >= 0 and <= 9 ? d : -1;

    /// <summary>El texto alfanumérico del anexo: mayúsculas, sólo los caracteres admitidos, sin espacios repetidos.</summary>
    public static string Texto(string? texto)
    {
        var sb = new StringBuilder();
        foreach (var c in (texto ?? string.Empty).ToUpperInvariant())
            sb.Append(Admitido(c) ? c : ' ');
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool Admitido(char c) => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or 'Œ' or 'Ž' || c is >= 'À' and <= 'Ý' && c != '×';

    /// <summary>La persona como la pide el anexo.</summary>
    public static PersonaParaLaUiaf Persona(string? idType, string? numero, string? digito, string? personType, string? primerNombre, string? otrosNombres,
        string? primerApellido, string? segundoApellido, string? razonSocial)
    {
        var tipo = TipoDeIdentificacion(idType);
        var juridica = string.Equals(personType?.Trim(), PersonaJuridica, StringComparison.Ordinal) || tipo == 31 && !string.IsNullOrWhiteSpace(razonSocial);
        var id = SoloAlfanumerico(numero);
        var dv = DigitoDeVerificacion(tipo, digito);
        if (juridica)
        {
            var razon = Texto(string.IsNullOrWhiteSpace(razonSocial) ? $"{primerNombre} {otrosNombres} {primerApellido} {segundoApellido}" : razonSocial);
            return new PersonaParaLaUiaf(tipo, id, dv, NoAplica, NoAplica, razon);
        }
        return new PersonaParaLaUiaf(tipo, id, dv, Texto($"{primerNombre} {otrosNombres}"), Texto($"{primerApellido} {segundoApellido}"), NoAplica);
    }

    /// <summary>
    /// El código de producto (tabla 7.1) de una clase de aplicación y su número desde el mapa «Clase=código;Clase:prefijo=código»: gana el prefijo más largo que
    /// empiece el número sin separadores; si ninguno, la clase sola; si tampoco, nulo (el archivo lo bloquea como obligatorio vacío).
    /// </summary>
    public static int? TipoDeProducto(string? mapa, string? clase, string? numero)
    {
        if (string.IsNullOrWhiteSpace(mapa) || string.IsNullOrWhiteSpace(clase)) return null;
        var limpio = SoloAlfanumerico(numero);
        int? porClase = null;
        (int Largo, int Codigo)? porPrefijo = null;
        foreach (var entrada in mapa.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var igual = entrada.IndexOf('=', StringComparison.Ordinal);
            if (igual <= 0 || !int.TryParse(entrada[(igual + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var codigo)) continue;
            var llave = entrada[..igual];
            var dos = llave.IndexOf(':', StringComparison.Ordinal);
            var deClase = dos < 0 ? llave : llave[..dos];
            if (!string.Equals(deClase, clase, StringComparison.OrdinalIgnoreCase)) continue;
            if (dos < 0)
            {
                porClase = codigo;
                continue;
            }
            var prefijo = SoloAlfanumerico(llave[(dos + 1)..]);
            if (prefijo.Length > 0 && limpio.StartsWith(prefijo, StringComparison.Ordinal) && (porPrefijo is null || prefijo.Length > porPrefijo.Value.Largo))
                porPrefijo = (prefijo.Length, codigo);
        }
        return porPrefijo?.Codigo ?? porClase;
    }

    /// <summary>
    /// El nombre del archivo del anexo, <c>TRSOSSTTTCCCCCMMAA.txt</c>: la sigla, el código de la organización, el mes y el año a dos dígitos del período.
    /// </summary>
    public static string NombreDelArchivo(string codigoDeOrganizacion, int anio, int mes) =>
        string.Create(CultureInfo.InvariantCulture, $"TRSO{codigoDeOrganizacion}{mes:00}{anio % 100:00}.txt");
}
