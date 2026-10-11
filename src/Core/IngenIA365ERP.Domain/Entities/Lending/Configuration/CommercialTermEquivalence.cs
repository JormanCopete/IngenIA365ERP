using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_CommercialTermEquivalences</c> (feature 013, T0210; data-model §1.9; FR-022, FR-050, T17): traduce a Cartera las
/// periodicidades y unidades de plazo que llegan de Comercial como texto. Versión propia de Cartera (hecho, C42) leída a la fecha
/// de la venta.
/// </summary>
public class CommercialTermEquivalence : AuditableEntity, IHechoInmutable, IVersionDeCartera
{
    public CommercialTermKind Kind { get; init; }

    /// <summary>El texto que manda Comercial, en mayúsculas.</summary>
    public string CommercialCode { get; init; } = string.Empty;

    public RepaymentPeriodicity? Periodicity { get; init; }

    /// <summary>Días por unidad de plazo (días = 1, meses = 30, años = 360).</summary>
    public int? DaysPerUnit { get; init; }

    public DateOnly ValidFrom { get; init; }

    public int VersionNumber { get; init; }

    public DateTime RegisteredAt { get; init; }

    public const string CodigoPeriodicidadDesconocida = "Lending.CommercialTermEquivalence.PeriodicityUnknown";
    public const string CodigoDuplicada = "Lending.CommercialTermEquivalence.Duplicate";
    public const string CodigoInvalida = "Validation.Invalid";

    /// <summary>Largo máximo del texto de Comercial (<c>nvarchar(20)</c>).</summary>
    public const int LargoDelCodigo = 20;

    /// <summary>El texto de Comercial normalizado: sin espacios alrededor y en mayúsculas.</summary>
    public static string Normalizar(string codigo) => codigo.Trim().ToUpperInvariant();

    /// <summary>
    /// Lo que está mal en una equivalencia, o nulo (data-model §1.9; T0337): el texto de Comercial no vacío y de hasta 20; una de
    /// periodicidad lleva una <see cref="RepaymentPeriodicity"/> conocida (<see cref="CodigoPeriodicidadDesconocida"/>) y no lleva días;
    /// una de unidad de plazo lleva los días por unidad (mayor que cero) y no lleva periodicidad. Los duplicados dentro de un lote los
    /// mira <see cref="ValidarLote"/>.
    /// </summary>
    public static ProblemaDeCartera? Validar(CommercialTermKind kind, string commercialCode, RepaymentPeriodicity? periodicity, int? daysPerUnit)
    {
        if (string.IsNullOrWhiteSpace(commercialCode) || Normalizar(commercialCode).Length > LargoDelCodigo)
            return ProblemaDeCartera.Con(CodigoInvalida, "El texto de Comercial va de 1 a 20 caracteres.", "field", "commercialCode");
        switch (kind)
        {
            case CommercialTermKind.Periodicity:
                if (periodicity is not { } p || !Enum.IsDefined(p))
                    return ProblemaDeCartera.Con(CodigoPeriodicidadDesconocida,
                        $"«{Normalizar(commercialCode)}» no se traduce a una periodicidad de Cartera conocida.", "commercialCode", Normalizar(commercialCode));
                if (daysPerUnit is not null)
                    return ProblemaDeCartera.Con(CodigoInvalida, "Una equivalencia de periodicidad no lleva días por unidad.", "field", "daysPerUnit");
                return null;
            case CommercialTermKind.TermUnit:
                if (daysPerUnit is not > 0)
                    return ProblemaDeCartera.Con(CodigoInvalida, "Una unidad de plazo lleva sus días por unidad (días = 1, meses = 30, años = 360).", "field", "daysPerUnit");
                if (periodicity is not null)
                    return ProblemaDeCartera.Con(CodigoInvalida, "Una unidad de plazo no lleva periodicidad.", "field", "periodicity");
                return null;
            default:
                return ProblemaDeCartera.Con(CodigoInvalida, "Clase de equivalencia desconocida.", "field", "kind");
        }
    }

    /// <summary>Un lote que trae dos veces la misma clase y texto se rechaza entero (<see cref="CodigoDuplicada"/>).</summary>
    public static ProblemaDeCartera? ValidarLote(IEnumerable<(CommercialTermKind Kind, string CommercialCode)> lote)
    {
        var repetida = lote.GroupBy(x => (x.Kind, Normalizar(x.CommercialCode))).FirstOrDefault(g => g.Count() > 1);
        return repetida is null
            ? null
            : ProblemaDeCartera.Con(CodigoDuplicada, $"«{repetida.Key.Item2}» está dos veces en las equivalencias de {repetida.Key.Kind}.",
                "commercialCode", repetida.Key.Item2);
    }

    /// <summary>Una versión nueva de la equivalencia (hecho de sólo inserción, C42); valida y lanza si quien llama no lo hizo.</summary>
    public static CommercialTermEquivalence Nueva(CommercialTermKind kind, string commercialCode, RepaymentPeriodicity? periodicity, int? daysPerUnit,
        DateOnly validFrom, int versionNumber, DateTime registeredAt)
    {
        ReglaDeCarteraException.SiHay(Validar(kind, commercialCode, periodicity, daysPerUnit));
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNumber, 1);
        return new CommercialTermEquivalence
        {
            Kind = kind,
            CommercialCode = Normalizar(commercialCode),
            Periodicity = periodicity,
            DaysPerUnit = daysPerUnit,
            ValidFrom = validFrom,
            VersionNumber = versionNumber,
            RegisteredAt = registeredAt,
        };
    }
}
