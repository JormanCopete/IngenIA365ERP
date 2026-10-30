using System.Text;

namespace IngenIA365ERP.Domain.Common.Identidad;

/// <summary>Resultado de validar un documento digitado: el tipo, el número normalizado y el usuario.</summary>
public sealed record DocumentoValidado(TipoDeDocumento Tipo, string Numero, string Usuario);

/// <summary>
/// El <b>único</b> sitio donde se normaliza un número de documento y se deriva el usuario (feature 017,
/// D-02, D-13, research R2). La cédula de ciudadanía lleva sólo el número; los demás tipos, su
/// prefijo y el número (<c>CE987654</c>, <c>PPT…</c>, <c>PAAB123</c>). Lo vigila
/// <c>ElUsuarioSeDerivaEnUnSoloSitio</c>: nadie más concatena tipo y número.
/// </summary>
public static class UsuarioDeDocumento
{
    /// <summary>Largo máximo de un usuario: el prefijo más largo (3) más el número más largo (20).</summary>
    public const int LongitudMaxima = 24;

    /// <summary>
    /// Mayúsculas y sólo <c>[0-9A-Z]</c>: se van espacios, puntos, guiones y cualquier otro signo.
    /// Una letra con tilde no es parte de ningún documento y también se va.
    /// </summary>
    public static string NormalizarNumero(string? numero)
    {
        if (string.IsNullOrEmpty(numero))
            return string.Empty;
        var sb = new StringBuilder(numero.Length);
        foreach (var ch in numero.ToUpperInvariant())
        {
            if (ch is >= '0' and <= '9' or >= 'A' and <= 'Z')
                sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>El usuario de un tipo y un número <b>ya normalizado</b>.</summary>
    public static string Derivar(TipoDeDocumento tipo, string numeroNormalizado) => tipo.Prefijo + numeroNormalizado;

    /// <summary>
    /// Valida un documento digitado para identidad de acceso o para una persona nueva: tipo conocido,
    /// número no vacío, sin letras si el tipo no las admite y dentro de las longitudes. Devuelve el
    /// mensaje para la persona en <paramref name="error"/>.
    /// </summary>
    public static DocumentoValidado? Validar(string? codigoTipo, string? numero, bool soloDeAcceso, out string? error)
    {
        var tipo = TiposDeDocumento.PorCodigo(codigoTipo);
        if (tipo is null || (soloDeAcceso && !tipo.SirveParaEntrar))
        {
            error = "Tipo de documento no válido.";
            return null;
        }

        var n = NormalizarNumero(numero);
        if (n.Length == 0)
        {
            error = "Digite el número de documento.";
            return null;
        }
        if (!tipo.AdmiteLetras && n.Any(char.IsLetter))
        {
            error = $"El número de {tipo.Nombre.ToLowerInvariant()} sólo lleva dígitos.";
            return null;
        }
        if (n.Length < tipo.LongitudMinima || n.Length > tipo.LongitudMaxima)
        {
            error = $"El número de {tipo.Nombre.ToLowerInvariant()} debe tener entre {tipo.LongitudMinima} y {tipo.LongitudMaxima} caracteres.";
            return null;
        }

        error = null;
        return new DocumentoValidado(tipo, n, Derivar(tipo, n));
    }

    /// <summary>
    /// El usuario de una persona de cooperativa a partir de lo que tiene guardado (<c>IdType</c>,
    /// <c>TaxId</c>), sin validar: sirve para filas viejas con datos imperfectos. Un código de tipo
    /// que el catálogo no conoce («X»…) usa el propio código y un guion como prefijo, que no puede
    /// coincidir con ningún usuario del catálogo.
    /// </summary>
    public static string DerivarParaPersona(string? idType, string? taxId)
    {
        var n = NormalizarNumero(taxId);
        var tipo = TiposDeDocumento.PorCodigoDePersona(idType);
        if (tipo is not null)
            return Derivar(tipo, n);
        var codigo = NormalizarNumero(idType);
        return (codigo.Length == 0 ? "SIN" : codigo) + "-" + n;
    }

    /// <summary>
    /// Lo que la persona escribió en «Correo o usuario» cuando no lleva <c>@</c>, llevado a la forma
    /// del usuario: normalizado y sin una <c>CC</c> inicial si lo que sigue son sólo dígitos
    /// (la cédula no lleva prefijo, pero la gente lo escribe).
    /// </summary>
    public static string NormalizarIdentificador(string? texto)
    {
        var n = NormalizarNumero(texto);
        if (n.Length > 2 && n.StartsWith("CC", StringComparison.Ordinal) && n.Skip(2).All(char.IsAsciiDigit))
            return n[2..];
        return n;
    }

    /// <summary>
    /// La pista fija que acompaña a «Credenciales inválidas»: no revela nada y sale de los prefijos del
    /// catálogo.
    /// </summary>
    public static string Pista() =>
        "Si tu documento no es cédula, antepón el tipo: "
        + string.Join(", ", TiposDeDocumento.DeAcceso.Where(t => t.Prefijo.Length > 0).Select(t => t.Prefijo))
        + ".";
}
