using IngenIA365ERP.Domain.Common.Parametros;
using static IngenIA365ERP.Domain.Common.Parametros.CatalogoDeParametros;
using static IngenIA365ERP.Domain.Common.Parametros.EntregaDelComercio;

namespace IngenIA365ERP.Domain.Taxes;

/// <summary>
/// Catálogo cerrado de las claves <c>TAX</c> (feature 012, T21, T068; data-model §4.2; decisiones-transversales
/// §2.8): la condición tributaria de la cooperativa, con vigencia. Todas exigen <see cref="Permiso"/> además de
/// <c>Inventory.Parameters.Manage</c>. Los defectos son configurables con vigencia, no valores legales fijos: por
/// eso el archivo está exceptuado de <c>ElComercioNoTieneValoresLegalesFijos</c>.
/// </summary>
public static class ParametrosTributarios
{
    public const string Modulo = "TAX";

    public const string Permiso = "Core.Taxes.Manage";

    public const string ResponsableIva = "Tributario.ResponsableIva";
    public const string GranContribuyente = "Tributario.GranContribuyente";
    public const string AgenteRetencionIva = "Tributario.AgenteRetencionIva";
    public const string Autorretenedor = "Tributario.Autorretenedor";
    public const string RegimenTributarioEspecial = "Tributario.RegimenTributarioEspecial";
    public const string RedondeoUvtAPesos = "Tributario.RedondeoUvtAPesos";

    public static IReadOnlyList<DefinicionDeParametro> Definiciones { get; } =
    [
        SiNo(Modulo, ResponsableIva, "La cooperativa es responsable de IVA.", true, I1) with { PermisoAdicional = Permiso },
        SiNo(Modulo, GranContribuyente, "La cooperativa es gran contribuyente.", false, I1) with { PermisoAdicional = Permiso },
        SiNo(Modulo, AgenteRetencionIva, "La cooperativa es agente de retención de IVA.", false, I1) with { PermisoAdicional = Permiso },
        SiNo(Modulo, Autorretenedor, "La cooperativa es autorretenedora.", false, I1) with { PermisoAdicional = Permiso },
        SiNo(Modulo, RegimenTributarioEspecial, "La cooperativa pertenece al régimen tributario especial.", true, I1) with { PermisoAdicional = Permiso },
        Eleccion(Modulo, RedondeoUvtAPesos, "Cómo se redondea a pesos un valor expresado en UVT.",
            ["Peso", "Centena", "Mil"], "Peso", I1) with { PermisoAdicional = Permiso },
    ];
}
