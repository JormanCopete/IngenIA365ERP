using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Treasury.Common;

/// <summary>Los errores de la base de la Tesorería nueva (feature 015, T0019; data-model §21). Cada historia agrega los suyos. (nuevo)</summary>
public static partial class ErroresDeTesoreria
{
    public const string CodigoNoActivado = "Treasury.NotActivated";

    public static Error NoActivado() => new(CodigoNoActivado,
        "Tesorería todavía no está activada en la cooperativa: complete la puesta en marcha y actívela antes de registrar operaciones.");
}

// Feature 015, US8 (pendiente de T0250, 2026-10-24; contabilidad.md §6): la validación previa contable de las pantallas de Tesorería. (nuevo, DT §2.7)
public static partial class ErroresDeTesoreria
{
    public const string CodigoNoContabilizable = "Treasury.Prevalidation.NotPostable";

    public static Error NoContabilizable(IReadOnlyList<IngenIA365ERP.Application.Common.Integration.Accounting.CombinacionSinReglaDto> faltan) => new ErrorConDatos(
        CodigoNoContabilizable,
        "Falta la regla contable de lo que se va a registrar: pida a Contabilidad que complete la matriz de Tesorería.",
        new
        {
            errors = faltan.Select(f => new { factType = f.FactType, role = f.Role, combination = f.Detail, whoFixes = "Contabilidad" }).ToList(),
        });
}
