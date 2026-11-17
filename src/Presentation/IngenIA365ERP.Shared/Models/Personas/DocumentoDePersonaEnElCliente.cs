using System.Text;

namespace IngenIA365ERP.Shared.Models.Personas;

/// <summary>
/// Las reglas del documento de la persona en el cliente (2026-11-16). <c>Shared</c> no referencia el Domain, así que se replica lo mínimo de
/// las fuentes, que mandan: el catálogo fijo <c>Domain/Common/Identidad/TiposDeDocumento</c> (códigos de persona, letras y longitudes),
/// <c>UsuarioDeDocumento.NormalizarNumero</c> y <c>UsuarioDeDocumento.Validar</c> (mismos mensajes), el algoritmo del DV de
/// <c>Application/Core/People/Imports/PlantillaDePersonas.DigitoDeVerificacionDian</c> y su uso en
/// <c>Application/Core/People/Contracts/DocumentoDePersona</c>. Si una de ellas cambia, ésta cambia con ella; el servidor vuelve a validar.
/// </summary>
public static class DocumentoDePersonaEnElCliente
{
    private sealed record Tipo(string Nombre, string[] CodigosDePersona, bool AdmiteLetras, int Minima, int Maxima, bool EsNit = false);

    // Copia de TiposDeDocumento.Todos (feature 017, research R2).
    private static readonly Tipo[] Tipos =
    [
        new("Cédula de ciudadanía", ["C", "CC"], false, 3, 10),
        new("Cédula de extranjería", ["CE", "E"], false, 3, 10),
        new("Permiso por protección temporal", ["PT"], false, 3, 15),
        new("Permiso especial de permanencia", ["PE"], false, 3, 15),
        new("Tarjeta de identidad", ["TI", "T"], false, 3, 11),
        new("Pasaporte", ["PA", "P"], true, 3, 16),
        new("Registro civil", ["RC", "R"], false, 3, 11),
        new("NIT", ["NI", "N", "NIT"], false, 5, 15, EsNit: true),
    ];

    /// <summary>¿El código de persona («C», «NI»…) es el NIT?</summary>
    public static bool EsNit(string? idType) => TipoDe(idType)?.EsNit == true;

    /// <summary>
    /// Los avisos del tipo, el número y el dígito de verificación; vacío si pasan. El tipo y el número vacíos no se reportan aquí (los
    /// reporta la obligatoriedad del formulario).
    /// </summary>
    public static IReadOnlyList<string> Avisos(string? idType, string? numero, string? digito)
    {
        var avisos = new List<string>();
        var tipo = TipoDe(idType);
        if (tipo is null)
        {
            avisos.Add("Tipo de documento no válido.");
            return avisos;
        }
        if (tipo.EsNit && numero is not null && numero.Contains('-'))
        {
            avisos.Add("Escriba el NIT sin el dígito de verificación: el dígito va en su propio campo.");
            return avisos;
        }

        var n = Normalizar(numero);
        if (n.Length == 0)
        {
            avisos.Add("Digite el número de documento.");
            return avisos;
        }
        var nombre = tipo.Nombre.ToLowerInvariant();
        if (!tipo.AdmiteLetras && n.Any(char.IsLetter))
        {
            avisos.Add($"El número de {nombre} sólo lleva dígitos.");
            return avisos;
        }
        if (n.Length < tipo.Minima || n.Length > tipo.Maxima)
        {
            avisos.Add($"El número de {nombre} debe tener entre {tipo.Minima} y {tipo.Maxima} caracteres.");
            return avisos;
        }

        var dv = digito?.Trim();
        if (tipo.EsNit && dv is { Length: 1 } && char.IsAsciiDigit(dv[0]) && DigitoDeVerificacion(n) is { } esperado && esperado != dv[0] - '0')
            avisos.Add($"El dígito de verificación del NIT {n} es {esperado}, no {dv}.");
        return avisos;
    }

    /// <summary>¿El DV escrito tiene la forma de un DV? Vacío vale (es opcional).</summary>
    public static bool DigitoConFormato(string? digito) =>
        string.IsNullOrWhiteSpace(digito) || digito.Trim() is { Length: 1 } d && char.IsAsciiDigit(d[0]);

    /// <summary>Copia de <c>UsuarioDeDocumento.NormalizarNumero</c>: mayúsculas y sólo [0-9A-Z].</summary>
    public static string Normalizar(string? numero)
    {
        if (string.IsNullOrEmpty(numero)) return string.Empty;
        var sb = new StringBuilder(numero.Length);
        foreach (var ch in numero.ToUpperInvariant())
            if (ch is >= '0' and <= '9' or >= 'A' and <= 'Z') sb.Append(ch);
        return sb.ToString();
    }

    /// <summary>Copia de <c>PlantillaDePersonas.DigitoDeVerificacionDian</c>: pesos primos de derecha a izquierda, módulo 11.</summary>
    public static int? DigitoDeVerificacion(string numero)
    {
        int[] pesos = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];
        if (numero.Length is 0 or > 15 || !numero.All(char.IsAsciiDigit)) return null;
        var suma = 0;
        for (var i = 0; i < numero.Length; i++)
            suma += (numero[numero.Length - 1 - i] - '0') * pesos[i];
        var residuo = suma % 11;
        return residuo < 2 ? residuo : 11 - residuo;
    }

    private static Tipo? TipoDe(string? idType)
    {
        if (string.IsNullOrWhiteSpace(idType)) return null;
        var c = idType.Trim().ToUpperInvariant();
        return Tipos.FirstOrDefault(t => t.CodigosDePersona.Contains(c));
    }
}
