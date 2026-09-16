using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>Qué catálogo institucional representa a la contraparte de un concepto de aporte o provisión (feature 009, FR-088).</summary>
public enum EntidadInstitucional { Ninguna = 0, Eps = 1, Arl = 2, FondoDePensiones = 3, FondoDeCesantias = 4, CajaDeCompensacion = 5 }

/// <summary>
/// Con quién se contabiliza cada línea de nómina (contracts/contabilizacion.md §Nómina): el
/// empleado en devengos y deducciones; en aportes y provisiones, la persona vinculada a la
/// entidad del empleado según el concepto (salud → EPS, pensión → fondo, ARL, caja, cesantías).
/// SENA e ICBF no tienen catálogo en el ERP: van sin tercero.
/// </summary>
public static class TercerosDeNomina
{
    public static EntidadInstitucional EntidadDe(string conceptCode, ConceptNature nature)
    {
        if (nature is not (ConceptNature.EmployerContribution or ConceptNature.Provision)) return EntidadInstitucional.Ninguna;
        var code = conceptCode.Trim().ToUpperInvariant();
        if (code == WellKnownConceptCodes.HealthEmployer || code.StartsWith("SALUD", StringComparison.Ordinal)) return EntidadInstitucional.Eps;
        if (code == WellKnownConceptCodes.PensionEmployer || code.StartsWith("PENSION", StringComparison.Ordinal)) return EntidadInstitucional.FondoDePensiones;
        if (code == WellKnownConceptCodes.WorkRisk || code.StartsWith("ARL", StringComparison.Ordinal)) return EntidadInstitucional.Arl;
        if (code == WellKnownConceptCodes.FamilyCompensation || code.StartsWith("CAJA", StringComparison.Ordinal)) return EntidadInstitucional.CajaDeCompensacion;
        if (code.StartsWith("CESANT", StringComparison.Ordinal)) return EntidadInstitucional.FondoDeCesantias;
        return EntidadInstitucional.Ninguna;
    }

    public static string Nombre(EntidadInstitucional entidad) => entidad switch
    {
        EntidadInstitucional.Eps => "EPS",
        EntidadInstitucional.Arl => "ARL",
        EntidadInstitucional.FondoDePensiones => "fondo de pensiones",
        EntidadInstitucional.FondoDeCesantias => "fondo de cesantías",
        EntidadInstitucional.CajaDeCompensacion => "caja de compensación",
        _ => "entidad",
    };

    /// <summary>Dónde se vincula la persona: la pantalla del catálogo.</summary>
    public static string Pantalla(EntidadInstitucional entidad) => entidad switch
    {
        EntidadInstitucional.Eps => "Nómina › EPS",
        EntidadInstitucional.Arl => "Nómina › ARL",
        EntidadInstitucional.FondoDePensiones => "Nómina › Fondos de pensiones",
        EntidadInstitucional.FondoDeCesantias => "Nómina › Fondos de cesantías",
        EntidadInstitucional.CajaDeCompensacion => "Nómina › Cajas de compensación",
        _ => "Nómina",
    };
}
