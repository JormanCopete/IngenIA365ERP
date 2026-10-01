using System.Globalization;
using System.Text.RegularExpressions;
using IngenIA365ERP.Domain.Enums.Parameters;

namespace IngenIA365ERP.Domain.Common.Parametros;

/// <summary>
/// El tipo de valor de una clave de parámetro (feature 012, T067; contracts/api.md §7, <c>ParameterDto.type</c>).
/// La API lo muestra como texto de presentación; no se guarda en ninguna tabla. (nuevo)
/// </summary>
public enum TipoDeParametro
{
    Bool = 1,
    Int = 2,
    Decimal = 3,
    Text = 4,
    Date = 5,
    Time = 6,
    Choice = 7,
}

/// <summary>
/// Las entregas del comercio (feature 012, decisiones-transversales §5), en el orden en que salen. Un valor de una
/// clave disponible sólo desde una entrega posterior a la vigente (<see cref="CatalogoDeParametros.EntregaVigente"/>)
/// no se admite todavía: <c>Peps</c> de <c>Costeo.Metodo</c> hasta I5. No se guarda en ninguna tabla. (nuevo)
/// </summary>
public enum EntregaDelComercio
{
    I1 = 1,
    I2 = 2,
    I3 = 3,
    IC = 4,
    I4 = 5,
    I5 = 6,
    I6 = 7,
}

/// <summary>
/// Un valor de parámetro ya interpretado: si es admitido, su texto canónico (el que se guarda) y el valor tipado
/// (<see cref="bool"/>, <see cref="int"/>, <see cref="decimal"/>, <see cref="string"/>, <see cref="DateOnly"/>,
/// <see cref="TimeOnly"/>, o nulo si la clave admite vacío y vino vacío). (nuevo)
/// </summary>
public readonly record struct ValorInterpretado(bool Admitido, string? Texto, object? Valor)
{
    public static ValorInterpretado NoAdmitido => new(false, null, null);
}

/// <summary>
/// La definición de una clave de parámetro con vigencia (feature 012, T21, T067; data-model §4.2): módulo, clave,
/// descripción, tipo, valores admitidos, <b>defecto seguro</b>, ámbitos admitidos, permiso adicional, si se sella
/// en el documento al confirmar, si exige fuente legal y la entrega desde la que está disponible. Viven en código,
/// en los catálogos cerrados <c>ParametrosDeInventario</c>, <c>ParametrosTributarios</c> y
/// <c>ParametrosDeFacturacionElectronica</c>; la tabla <c>COR_ParameterVersions</c> sólo guarda vigencias.
///
/// <para>
/// <see cref="Interpretar"/> es la única regla de qué es un valor válido: la usan el lector (que rechaza lo
/// guardado fuera de lo admitido con <c>Parameters.ValueNotAllowed</c>, nunca cae al defecto) y el alta.
/// </para>
/// </summary>
public sealed record DefinicionDeParametro
{
    public required string Modulo { get; init; }
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public required TipoDeParametro Tipo { get; init; }

    /// <summary>Para <see cref="TipoDeParametro.Choice"/>: los valores, en su escritura canónica.</summary>
    public IReadOnlyList<string> ValoresAdmitidos { get; init; } = [];

    /// <summary>Valores de <see cref="ValoresAdmitidos"/> que sólo se admiten desde una entrega posterior.</summary>
    public IReadOnlyDictionary<string, EntregaDelComercio> ValoresDesde { get; init; } =
        new Dictionary<string, EntregaDelComercio>();

    /// <summary>El defecto seguro, en texto canónico ("" = vacío, sólo si <see cref="AdmiteVacio"/>).</summary>
    public required string DefectoSeguro { get; init; }

    public IReadOnlyList<ParameterScopeKind> AmbitosAdmitidos { get; init; } = [ParameterScopeKind.None];

    /// <summary>Permiso que exige registrar una vigencia, además de <c>Inventory.Parameters.Manage</c>.</summary>
    public string? PermisoAdicional { get; init; }

    /// <summary>Se lee a la fecha de confirmación y se copia al documento (modo de paso, cuenta por cobrar).</summary>
    public bool SelladoAlConfirmar { get; init; }

    /// <summary>Registrar una vigencia exige <c>LegalSource</c> (la norma o el acta que la respalda).</summary>
    public bool ExigeFuenteLegal { get; init; }

    /// <summary>La entrega desde la que la clave tiene efecto (informativo: se puede parametrizar antes).</summary>
    public EntregaDelComercio DisponibleDesde { get; init; } = EntregaDelComercio.I1;

    /// <summary>El valor vacío es admitido y significa «sin valor» (p. ej. <c>Cartera.IntegracionHabilitadaDesde</c>).</summary>
    public bool AdmiteVacio { get; init; }

    /// <summary>Para <see cref="TipoDeParametro.Int"/> y <see cref="TipoDeParametro.Decimal"/>. Por defecto, cero.</summary>
    public decimal? Minimo { get; init; } = decimal.Zero;

    /// <summary>Para <see cref="TipoDeParametro.Int"/> y <see cref="TipoDeParametro.Decimal"/>; una fracción lleva 1.</summary>
    public decimal? Maximo { get; init; }

    /// <summary>Para <see cref="TipoDeParametro.Text"/>: expresión regular que el texto debe cumplir.</summary>
    public string? Patron { get; init; }

    public bool AdmiteAmbito(ParameterScopeKind ambito) => AmbitosAdmitidos.Contains(ambito);

    /// <summary>
    /// Lo admitido, para <c>data.allowed</c> y la pantalla: los valores de una elección disponibles en la entrega, o
    /// el formato del tipo.
    /// </summary>
    public IReadOnlyList<string> Admitidos(EntregaDelComercio entrega)
    {
        List<string> lista = Tipo switch
        {
            TipoDeParametro.Choice => ValoresAdmitidos.Where(v => DisponibleEn(v, entrega)).ToList(),
            TipoDeParametro.Bool => ["true", "false"],
            TipoDeParametro.Int => [Rango("entero")],
            TipoDeParametro.Decimal => [Rango("decimal con punto")],
            TipoDeParametro.Date => ["yyyy-MM-dd"],
            TipoDeParametro.Time => ["HH:mm"],
            _ => [Patron is null ? "texto" : $"texto con la forma {Patron}"],
        };
        if (AdmiteVacio) lista.Add(string.Empty);
        return lista;
    }

    /// <summary>
    /// Valida y tipa un texto según la definición, en la entrega dada. Puro: no lee reloj ni base. Un valor de
    /// elección o un booleano se reconoce sin distinguir mayúsculas y se devuelve en su escritura canónica.
    /// </summary>
    public ValorInterpretado Interpretar(string? texto, EntregaDelComercio entrega)
    {
        var t = texto?.Trim() ?? string.Empty;
        if (t.Length == 0)
            return AdmiteVacio ? new ValorInterpretado(true, string.Empty, null) : ValorInterpretado.NoAdmitido;

        switch (Tipo)
        {
            case TipoDeParametro.Bool:
                if (string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)) return new(true, "true", true);
                if (string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)) return new(true, "false", false);
                return ValorInterpretado.NoAdmitido;

            case TipoDeParametro.Int:
                if (!int.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var entero) || !EnRango(entero))
                    return ValorInterpretado.NoAdmitido;
                return new(true, entero.ToString(CultureInfo.InvariantCulture), entero);

            case TipoDeParametro.Decimal:
                if (!decimal.TryParse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var dec)
                    || !EnRango(dec))
                    return ValorInterpretado.NoAdmitido;
                return new(true, dec.ToString(CultureInfo.InvariantCulture), dec);

            case TipoDeParametro.Date:
                if (!DateOnly.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
                    return ValorInterpretado.NoAdmitido;
                return new(true, fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), fecha);

            case TipoDeParametro.Time:
                if (!TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora))
                    return ValorInterpretado.NoAdmitido;
                return new(true, hora.ToString("HH:mm", CultureInfo.InvariantCulture), hora);

            case TipoDeParametro.Choice:
                var canonico = ValoresAdmitidos.FirstOrDefault(v => string.Equals(v, t, StringComparison.OrdinalIgnoreCase));
                if (canonico is null || !DisponibleEn(canonico, entrega)) return ValorInterpretado.NoAdmitido;
                return new(true, canonico, canonico);

            case TipoDeParametro.Text:
                if (Patron is not null && !Regex.IsMatch(t, Patron, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                    return ValorInterpretado.NoAdmitido;
                return new(true, t, t);

            default:
                return ValorInterpretado.NoAdmitido;
        }
    }

    private bool DisponibleEn(string valor, EntregaDelComercio entrega) =>
        !ValoresDesde.TryGetValue(valor, out var desde) || desde <= entrega;

    private bool EnRango(decimal valor) => (Minimo is null || valor >= Minimo) && (Maximo is null || valor <= Maximo);

    private string Rango(string tipo) => (Minimo, Maximo) switch
    {
        ({ } min, { } max) => $"{tipo} entre {min.ToString(CultureInfo.InvariantCulture)} y {max.ToString(CultureInfo.InvariantCulture)}",
        ({ } min, null) => $"{tipo} mayor o igual a {min.ToString(CultureInfo.InvariantCulture)}",
        _ => tipo,
    };
}
