namespace IngenIA365ERP.Domain.Common.Identidad;

/// <summary>
/// Un tipo de documento de identidad del catálogo fijo (feature 017, D-12, research R2).
/// </summary>
/// <param name="Codigo">Código de acceso que ve la persona: CC, CE, PPT, PEP, TI, PA, RC, NIT.</param>
/// <param name="CodigosDePersona">
/// Códigos con que la persona de cada cooperativa lo guarda en <c>COR_People.IdType</c>. El primero
/// es el que se escribe; los demás son los que dejó la migración de datos («E», «T», «P»…) y sólo
/// se leen.
/// </param>
/// <param name="Prefijo">Lo que antecede al número en el usuario. Vacío sólo en la cédula de ciudadanía.</param>
/// <param name="AdmiteLetras">Si el número puede llevar letras (sólo el pasaporte).</param>
/// <param name="SirveParaEntrar">Si una persona puede tenerlo como documento de acceso. RC y NIT no.</param>
/// <param name="CodigoDian">Código del tipo de identificación de la DIAN.</param>
public sealed record TipoDeDocumento(
    string Codigo,
    IReadOnlyList<string> CodigosDePersona,
    string Nombre,
    string Prefijo,
    bool AdmiteLetras,
    int LongitudMinima,
    int LongitudMaxima,
    bool SirveParaEntrar,
    string CodigoDian)
{
    /// <summary>El código que se escribe en <c>COR_People.IdType</c>.</summary>
    public string CodigoDePersona => CodigosDePersona[0];
}

/// <summary>
/// Catálogo <b>fijo</b> de tipos de documento (D-12: sin pantalla de mantenimiento; un tipo nuevo es
/// una versión nueva del producto). Las longitudes son la propuesta de research R2, pendiente de
/// confirmar por el dueño (T001).
/// <para>
/// El usuario derivado no es ambiguo porque el prefijo vacío de la cédula sólo admite dígitos y
/// ningún otro prefijo es el comienzo de otro (lo vigila <c>LosPrefijosDelUsuarioNoSonPrefijoDeOtro</c>).
/// </para>
/// </summary>
public static class TiposDeDocumento
{
    public static readonly TipoDeDocumento CedulaDeCiudadania =
        new("CC", ["C", "CC"], "Cédula de ciudadanía", "", false, 3, 10, true, "13");

    public static readonly TipoDeDocumento CedulaDeExtranjeria =
        new("CE", ["CE", "E"], "Cédula de extranjería", "CE", false, 3, 10, true, "22");

    public static readonly TipoDeDocumento PermisoPorProteccionTemporal =
        new("PPT", ["PT"], "Permiso por protección temporal", "PPT", false, 3, 15, true, "48");

    public static readonly TipoDeDocumento PermisoEspecialDePermanencia =
        new("PEP", ["PE"], "Permiso especial de permanencia", "PEP", false, 3, 15, true, "47");

    public static readonly TipoDeDocumento TarjetaDeIdentidad =
        new("TI", ["TI", "T"], "Tarjeta de identidad", "TI", false, 3, 11, true, "12");

    public static readonly TipoDeDocumento Pasaporte =
        new("PA", ["PA", "P"], "Pasaporte", "PA", true, 3, 16, true, "41");

    public static readonly TipoDeDocumento RegistroCivil =
        new("RC", ["RC", "R"], "Registro civil", "RC", false, 3, 11, false, "11");

    public static readonly TipoDeDocumento Nit =
        new("NIT", ["NI", "N", "NIT"], "NIT", "NIT", false, 5, 15, false, "31");

    public static readonly IReadOnlyList<TipoDeDocumento> Todos =
    [
        CedulaDeCiudadania, CedulaDeExtranjeria, PermisoPorProteccionTemporal, PermisoEspecialDePermanencia,
        TarjetaDeIdentidad, Pasaporte, RegistroCivil, Nit,
    ];

    /// <summary>Los que sirven para entrar, en el orden en que se ofrecen.</summary>
    public static readonly IReadOnlyList<TipoDeDocumento> DeAcceso = Todos.Where(t => t.SirveParaEntrar).ToList();

    /// <summary>El tipo por su código de acceso (CC, CE, PPT…), sin distinguir mayúsculas.</summary>
    public static TipoDeDocumento? PorCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return null;
        var c = codigo.Trim().ToUpperInvariant();
        return Todos.FirstOrDefault(t => t.Codigo == c);
    }

    /// <summary>El tipo por el código de <c>COR_People.IdType</c> («C», «CE», «PT», «E»…).</summary>
    public static TipoDeDocumento? PorCodigoDePersona(string? idType)
    {
        if (string.IsNullOrWhiteSpace(idType))
            return null;
        var c = idType.Trim().ToUpperInvariant();
        return Todos.FirstOrDefault(t => t.CodigosDePersona.Contains(c));
    }
}
