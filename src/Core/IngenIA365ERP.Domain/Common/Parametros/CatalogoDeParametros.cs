using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Taxes;

namespace IngenIA365ERP.Domain.Common.Parametros;

/// <summary>
/// La unión de los tres catálogos cerrados de claves (feature 012, T21, T068; data-model §4.2): lo que no está aquí
/// no se puede registrar (<c>Parameters.KeyNotFound</c>). La consultan <c>LectorDeParametros</c>,
/// <c>AddParameterVersionCommand</c> y las consultas de la pantalla. (nuevo)
/// </summary>
public static class CatalogoDeParametros
{
    /// <summary>
    /// La entrega del comercio en producción en esta versión del programa. Los valores disponibles desde una
    /// posterior (<c>Peps</c>, I5) responden <c>Parameters.ValueNotAllowed</c>; la sube la entrega que los habilita.
    /// </summary>
    public const EntregaDelComercio EntregaVigente = EntregaDelComercio.I1;

    public static IReadOnlyList<string> Modulos { get; } =
        [ParametrosDeInventario.Modulo, ParametrosTributarios.Modulo, ParametrosDeFacturacionElectronica.Modulo];

    public static IReadOnlyList<DefinicionDeParametro> Todas { get; } =
        [.. ParametrosDeInventario.Definiciones, .. ParametrosTributarios.Definiciones, .. ParametrosDeFacturacionElectronica.Definiciones];

    private static readonly Dictionary<(string, string), DefinicionDeParametro> PorClave =
        Todas.ToDictionary(d => (d.Modulo.ToUpperInvariant(), d.Clave.ToUpperInvariant()));

    /// <summary>La definición de (módulo, clave), sin distinguir mayúsculas; nula si no existe.</summary>
    public static DefinicionDeParametro? Buscar(string? modulo, string? clave) =>
        modulo is null || clave is null ? null
            : PorClave.GetValueOrDefault((modulo.Trim().ToUpperInvariant(), clave.Trim().ToUpperInvariant()));

    // Ayudas para declarar los catálogos sin repetir la forma de cada tipo.

    internal static DefinicionDeParametro Eleccion(string modulo, string clave, string descripcion, string[] valores, string defecto,
        EntregaDelComercio desde, params ParameterScopeKind[] ambitos) => new()
        {
            Modulo = modulo, Clave = clave, Descripcion = descripcion, Tipo = TipoDeParametro.Choice,
            ValoresAdmitidos = valores, DefectoSeguro = defecto, DisponibleDesde = desde, AmbitosAdmitidos = Ambitos(ambitos),
        };

    internal static DefinicionDeParametro SiNo(string modulo, string clave, string descripcion, bool defecto,
        EntregaDelComercio desde, params ParameterScopeKind[] ambitos) => new()
        {
            Modulo = modulo, Clave = clave, Descripcion = descripcion, Tipo = TipoDeParametro.Bool,
            DefectoSeguro = defecto ? "true" : "false", DisponibleDesde = desde, AmbitosAdmitidos = Ambitos(ambitos),
        };

    internal static DefinicionDeParametro Entero(string modulo, string clave, string descripcion, string defecto,
        EntregaDelComercio desde, params ParameterScopeKind[] ambitos) => new()
        {
            Modulo = modulo, Clave = clave, Descripcion = descripcion, Tipo = TipoDeParametro.Int,
            DefectoSeguro = defecto, DisponibleDesde = desde, AmbitosAdmitidos = Ambitos(ambitos),
        };

    internal static DefinicionDeParametro NumeroDecimal(string modulo, string clave, string descripcion, string defecto,
        EntregaDelComercio desde, params ParameterScopeKind[] ambitos) => new()
        {
            Modulo = modulo, Clave = clave, Descripcion = descripcion, Tipo = TipoDeParametro.Decimal,
            DefectoSeguro = defecto, DisponibleDesde = desde, AmbitosAdmitidos = Ambitos(ambitos),
        };

    private static ParameterScopeKind[] Ambitos(ParameterScopeKind[] ambitos) =>
        ambitos.Length == 0 ? [ParameterScopeKind.None] : ambitos;
}
