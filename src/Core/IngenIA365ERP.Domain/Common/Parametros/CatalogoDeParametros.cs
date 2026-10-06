using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Taxes;

namespace IngenIA365ERP.Domain.Common.Parametros;

/// <summary>
/// La unión de los catálogos cerrados de claves (feature 012, T21, T068; data-model §4.2): lo que no está aquí
/// no se puede registrar (<c>Parameters.KeyNotFound</c>). La consultan <c>LectorDeParametros</c>,
/// <c>AddParameterVersionCommand</c> y las consultas de la pantalla. (nuevo)
///
/// <para>Feature 013 (T43, R17): suma el catálogo de Cartera (<see cref="ParametrosDeCartera"/>, módulo <c>LND</c>) y la entrega
/// pasa a ser una por línea (<see cref="EntregaDeModulo"/>, <see cref="EntregasVigentes"/>).</para>
/// </summary>
public static class CatalogoDeParametros
{
    /// <summary>
    /// La entrega del comercio en producción en esta versión del programa. Los valores disponibles desde una
    /// posterior (<c>Peps</c>, I5) responden <c>Parameters.ValueNotAllowed</c>; la sube la entrega que los habilita.
    /// </summary>
    /// <remarks>
    /// Feature 013 (T43): es la entrega vigente de la línea del comercio (<c>INV</c>), y sigue siendo constante porque el código
    /// del comercio la usa como valor por defecto de sus constructores (<c>EfectosDeClase</c>, <c>RegistroDeKardex</c>…). La de
    /// cada línea, incluida ésta, sale de <see cref="EntregasVigentes"/>.
    /// </remarks>
    public const EntregaDelComercio EntregaVigente = EntregaDelComercio.I6;

    /// <summary>
    /// La entrega vigente de cada línea (feature 013, T43): el comercio en <see cref="EntregaVigente"/> y Cartera en
    /// <see cref="CatalogoDeCartera.EntregaVigente"/>. Una línea nueva se suma aquí.
    /// </summary>
    public static IReadOnlyDictionary<string, EntregaDeModulo> EntregasVigentes { get; } = new Dictionary<string, EntregaDeModulo>(StringComparer.Ordinal)
    {
        [EntregaDeModulo.LineaDelComercio] = EntregaVigente,
        [EntregaDeModulo.LineaDeCartera] = CatalogoDeCartera.EntregaVigente,
    };

    /// <summary>La entrega vigente de la línea <paramref name="linea"/> (<c>INV</c>, <c>LND</c>).</summary>
    public static EntregaDeModulo EntregaVigenteDe(string linea) =>
        EntregasVigentes.TryGetValue(linea, out var entrega)
            ? entrega
            : throw new ArgumentException($"«{linea}» no es una línea de entregas conocida.", nameof(linea));

    /// <summary>
    /// La entrega con que se juzga algo de la línea <paramref name="linea"/>: la <paramref name="fijada"/> si es de esa línea (las
    /// pruebas de una entrega anterior la fijan), si no la vigente de la línea.
    /// </summary>
    public static EntregaDeModulo EntregaParaLaLinea(string linea, EntregaDeModulo? fijada) =>
        fijada is { } f && string.Equals(f.Modulo, linea, StringComparison.Ordinal) ? f : EntregaVigenteDe(linea);

    public static IReadOnlyList<string> Modulos { get; } =
        [ParametrosDeInventario.Modulo, ParametrosTributarios.Modulo, ParametrosDeFacturacionElectronica.Modulo, ParametrosDeCartera.Modulo];

    public static IReadOnlyList<DefinicionDeParametro> Todas { get; } =
        [.. ParametrosDeInventario.Definiciones, .. ParametrosTributarios.Definiciones, .. ParametrosDeFacturacionElectronica.Definiciones,
         .. ParametrosDeCartera.Definiciones];

    private static readonly Dictionary<(string, string), DefinicionDeParametro> PorClave =
        Todas.ToDictionary(d => (d.Modulo.ToUpperInvariant(), d.Clave.ToUpperInvariant()));

    /// <summary>La definición de (módulo, clave), sin distinguir mayúsculas; nula si no existe.</summary>
    public static DefinicionDeParametro? Buscar(string? modulo, string? clave) =>
        modulo is null || clave is null ? null
            : PorClave.GetValueOrDefault((modulo.Trim().ToUpperInvariant(), clave.Trim().ToUpperInvariant()));

    // Ayudas para declarar los catálogos sin repetir la forma de cada tipo.

    internal static DefinicionDeParametro Eleccion(string modulo, string clave, string descripcion, string[] valores, string defecto,
        EntregaDeModulo desde, params ParameterScopeKind[] ambitos) => new()
        {
            Modulo = modulo, Clave = clave, Descripcion = descripcion, Tipo = TipoDeParametro.Choice,
            ValoresAdmitidos = valores, DefectoSeguro = defecto, DisponibleDesde = desde, AmbitosAdmitidos = Ambitos(ambitos),
        };

    internal static DefinicionDeParametro SiNo(string modulo, string clave, string descripcion, bool defecto,
        EntregaDeModulo desde, params ParameterScopeKind[] ambitos) => new()
        {
            Modulo = modulo, Clave = clave, Descripcion = descripcion, Tipo = TipoDeParametro.Bool,
            DefectoSeguro = defecto ? "true" : "false", DisponibleDesde = desde, AmbitosAdmitidos = Ambitos(ambitos),
        };

    internal static DefinicionDeParametro Entero(string modulo, string clave, string descripcion, string defecto,
        EntregaDeModulo desde, params ParameterScopeKind[] ambitos) => new()
        {
            Modulo = modulo, Clave = clave, Descripcion = descripcion, Tipo = TipoDeParametro.Int,
            DefectoSeguro = defecto, DisponibleDesde = desde, AmbitosAdmitidos = Ambitos(ambitos),
        };

    internal static DefinicionDeParametro NumeroDecimal(string modulo, string clave, string descripcion, string defecto,
        EntregaDeModulo desde, params ParameterScopeKind[] ambitos) => new()
        {
            Modulo = modulo, Clave = clave, Descripcion = descripcion, Tipo = TipoDeParametro.Decimal,
            DefectoSeguro = defecto, DisponibleDesde = desde, AmbitosAdmitidos = Ambitos(ambitos),
        };

    private static ParameterScopeKind[] Ambitos(ParameterScopeKind[] ambitos) =>
        ambitos.Length == 0 ? [ParameterScopeKind.None] : ambitos;
}
