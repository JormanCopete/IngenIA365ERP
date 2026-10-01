using System.Text.RegularExpressions;
using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Taxes;

namespace IngenIA365ERP.Application.Core.Taxes;

/// <summary>
/// Los datos de una tarifa que traen el alta, la corrección y la plantilla (feature 012, T165; contracts/api.md §30). Los
/// validadores y el handler los revisan con las mismas reglas: no hay una segunda puerta con reglas propias. (nuevo)
/// </summary>
public interface IDatosDeTarifa
{
    string? Code { get; }
    string Name { get; }
    decimal? Rate { get; }
    decimal? AmountPerUnit { get; }
    string? MunicipalityDaneCode { get; }
    string? ActivityCode { get; }
    decimal? MinimumBaseUvt { get; }
    decimal? MinimumBasePesos { get; }
    TaxRateConditionsDto? Conditions { get; }
    TaxAppliesTo AppliesTo { get; }
    short Priority { get; }
    DateOnly ValidFrom { get; }
    DateOnly? ValidTo { get; }
    string LegalSource { get; }
    string? Notes { get; }
}

/// <summary>El dato de una tarifa que incumple una regla (para la columna del error en la plantilla). (nuevo)</summary>
public enum CampoDeTarifa { Tarifa, ValorPorUnidad, Concepto, Municipio, Actividad, Condiciones }

/// <summary>
/// Las reglas del catálogo tributario (feature 012, T165, T166; data-model §17), en un solo sitio para el alta unitaria y
/// la plantilla: qué exige cada clase y forma de cálculo, que dos vigencias del mismo código no se crucen
/// (<c>Core.TaxRate.Overlaps</c>) y que no haya un empate evidente entre tarifas de retención
/// (<c>Core.TaxRate.Ambiguous</c>). El empate evidente sólo se mira en retenciones: el motor elige esas tarifas por
/// condiciones; los impuestos del producto se citan por código. (nuevo)
/// </summary>
public static class ReglasDelCatalogoTributario
{
    public const int LargoDeNombre = 120;
    public const int LargoDeNotas = 400;
    public const int LargoDeNorma = 200;

    public const string PatronDeMunicipio = "^[0-9]{5}$";
    public const string PatronDeActividad = "^([0-9]{4,6}|\\*)$";
    public const string PatronDeCodigoDian = "^[0-9A-Za-z]{2,4}$";

    /// <summary>Las clases de retención; en <c>Other</c> lo dice quien crea el impuesto.</summary>
    public static bool EsRetencion(TaxKind clase, bool pedido) => clase switch
    {
        TaxKind.ReteFuente or TaxKind.ReteIva or TaxKind.ReteIca => true,
        TaxKind.Other => pedido,
        _ => false,
    };

    /// <summary>
    /// Las reglas de una definición: <c>PercentOfTax</c> exige el impuesto sobre el que se calcula, que no sea una
    /// retención ni el mismo; las demás formas no lo llevan.
    /// </summary>
    public static Error? Definicion(string codigo, TaxCalculationForm forma, TaxDefinition? calculadoSobre)
    {
        if (forma == TaxCalculationForm.PercentOfTax && calculadoSobre is null)
            return TaxErrors.Invalid("Un impuesto que se calcula sobre otro (PercentOfTax) exige el impuesto base (taxedOnTaxPublicId).");
        if (forma != TaxCalculationForm.PercentOfTax && calculadoSobre is not null)
            return TaxErrors.Invalid("Sólo un impuesto PercentOfTax se calcula sobre otro impuesto.");
        if (calculadoSobre is not null && (calculadoSobre.IsWithholding || string.Equals(calculadoSobre.Code, codigo, StringComparison.OrdinalIgnoreCase)))
            return TaxErrors.Invalid("El impuesto base debe ser otro impuesto (el IVA, para ReteIVA), no una retención.");
        return null;
    }

    /// <summary>
    /// Lo que exige la definición a su tarifa: porcentaje o valor por unidad según la forma; concepto en ReteFuente;
    /// municipio en ICA y ReteICA; actividad sólo en ICA y ReteICA; condiciones sólo en retenciones.
    /// </summary>
    public static Error? Tarifa(TaxDefinition impuesto, TaxRate tarifa, WithholdingConcept? concepto) =>
        TarifaConCampo(impuesto, tarifa, concepto)?.Error;

    /// <summary>
    /// Como <see cref="Tarifa"/>, con el dato de la tarifa que incumple (<see cref="CampoDeTarifa"/>), para que la
    /// plantilla ponga el error en su columna.
    /// </summary>
    public static (Error Error, CampoDeTarifa Campo)? TarifaConCampo(TaxDefinition impuesto, TaxRate tarifa, WithholdingConcept? concepto)
    {
        if (impuesto.CalculationForm == TaxCalculationForm.AmountPerUnit)
        {
            if (tarifa.AmountPerUnit is not > 0)
                return (TaxErrors.Invalid($"El impuesto {impuesto.Code} es por unidad: la tarifa exige el valor por unidad."), CampoDeTarifa.ValorPorUnidad);
            if (tarifa.Rate is not null) return (TaxErrors.Invalid("Una tarifa por unidad no lleva porcentaje."), CampoDeTarifa.Tarifa);
        }
        else
        {
            if (tarifa.Rate is null)
                return (TaxErrors.Invalid($"El impuesto {impuesto.Code} es un porcentaje: la tarifa exige el porcentaje."), CampoDeTarifa.Tarifa);
            if (tarifa.AmountPerUnit is not null) return (TaxErrors.Invalid("Una tarifa porcentual no lleva valor por unidad."), CampoDeTarifa.ValorPorUnidad);
        }

        if (impuesto.Kind == TaxKind.ReteFuente && concepto is null)
            return (TaxErrors.Invalid("Una tarifa de ReteFuente exige su concepto de retención."), CampoDeTarifa.Concepto);
        var municipal = impuesto.Kind is TaxKind.Ica or TaxKind.ReteIca;
        if (municipal && string.IsNullOrWhiteSpace(tarifa.MunicipalityDaneCode))
            return (TaxErrors.Invalid("Una tarifa de ICA o ReteICA exige el municipio (código DANE)."), CampoDeTarifa.Municipio);
        if (!municipal && !string.IsNullOrWhiteSpace(tarifa.ActivityCode))
            return (TaxErrors.Invalid("La actividad (CIIU) sólo aplica a ICA y ReteICA."), CampoDeTarifa.Actividad);
        if (!impuesto.IsWithholding && !TaxRateConditionsDto.De(tarifa).Vacias)
            return (TaxErrors.Invalid("Las condiciones sobre el sujeto y el agente sólo se admiten en tarifas de retención."), CampoDeTarifa.Condiciones);
        return null;
    }

    /// <summary>La primera otra vigencia viva del mismo código que se cruza con <paramref name="tarifa"/>.</summary>
    public static TaxRate? Cruce(IEnumerable<TaxRate> existentes, TaxRate tarifa) =>
        existentes.FirstOrDefault(o => !ReferenceEquals(o, tarifa) && (tarifa.Id == 0 || o.Id != tarifa.Id) && !o.IsDeleted
            && string.Equals(o.Code, tarifa.Code, StringComparison.OrdinalIgnoreCase)
            && o.SeCruzaCon(tarifa.ValidFrom, tarifa.ValidTo));

    /// <summary>
    /// Empate evidente (sólo retenciones): otra tarifa viva de otro código, del mismo impuesto, concepto, municipio,
    /// actividad, condiciones y prioridad, con vigencias cruzadas.
    /// </summary>
    public static TaxRate? Empate(IEnumerable<TaxRate> existentes, TaxDefinition impuesto, TaxRate tarifa)
    {
        if (!impuesto.IsWithholding) return null;
        return existentes.FirstOrDefault(o => !ReferenceEquals(o, tarifa) && (tarifa.Id == 0 || o.Id != tarifa.Id) && !o.IsDeleted
            && !string.Equals(o.Code, tarifa.Code, StringComparison.OrdinalIgnoreCase)
            && MismoImpuesto(o, tarifa)
            && MismoConcepto(o, tarifa)
            && string.Equals(o.MunicipalityDaneCode ?? string.Empty, tarifa.MunicipalityDaneCode ?? string.Empty, StringComparison.Ordinal)
            && string.Equals(Actividad(o), Actividad(tarifa), StringComparison.Ordinal)
            && o.Priority == tarifa.Priority
            && TaxRateConditionsDto.De(o) == TaxRateConditionsDto.De(tarifa)
            && o.SeCruzaCon(tarifa.ValidFrom, tarifa.ValidTo));
    }

    /// <summary>Las condiciones de la entidad en el tipo del motor.</summary>
    public static CondicionesDeTarifa Condiciones(TaxRate t) => new(t.SubjectPersonType, t.SubjectIsIncomeTaxFiler, t.SubjectIsVatResponsible,
        t.SubjectIsLargeContributor, t.SubjectIsSelfWithholder, t.SubjectIsSimpleTaxRegime, t.AgentIsLargeContributor, t.AgentIsVatWithholdingAgent);

    /// <summary>Copia los datos del alta o la corrección a la entidad (el código sólo al crear).</summary>
    public static void Copiar(IDatosDeTarifa datos, TaxRate tarifa)
    {
        tarifa.Name = datos.Name.Trim();
        tarifa.Rate = datos.Rate;
        tarifa.AmountPerUnit = datos.AmountPerUnit;
        tarifa.MunicipalityDaneCode = Nulo(datos.MunicipalityDaneCode);
        tarifa.ActivityCode = Nulo(datos.ActivityCode);
        tarifa.MinimumBaseUvt = datos.MinimumBaseUvt;
        tarifa.MinimumBasePesos = datos.MinimumBasePesos;
        var c = datos.Conditions ?? new TaxRateConditionsDto();
        tarifa.SubjectPersonType = Nulo(c.SubjectPersonType);
        tarifa.SubjectIsIncomeTaxFiler = c.SubjectIsIncomeTaxFiler;
        tarifa.SubjectIsVatResponsible = c.SubjectIsVatResponsible;
        tarifa.SubjectIsLargeContributor = c.SubjectIsLargeContributor;
        tarifa.SubjectIsSelfWithholder = c.SubjectIsSelfWithholder;
        tarifa.SubjectIsSimpleTaxRegime = c.SubjectIsSimpleTaxRegime;
        tarifa.AgentIsLargeContributor = c.AgentIsLargeContributor;
        tarifa.AgentIsVatWithholdingAgent = c.AgentIsVatWithholdingAgent;
        tarifa.AppliesTo = datos.AppliesTo;
        tarifa.Priority = datos.Priority;
        tarifa.ValidFrom = datos.ValidFrom;
        tarifa.ValidTo = datos.ValidTo;
        tarifa.LegalSource = datos.LegalSource.Trim();
        tarifa.Notes = Nulo(datos.Notes);
    }

    /// <summary>¿Cambia algo de lo que queda fijo desde que la tarifa entra en vigencia (todo salvo nombre, notas y fin)?</summary>
    public static bool CambiaLoFijo(TaxRate actual, TaxRate propuesta) =>
        actual.Rate != propuesta.Rate || actual.AmountPerUnit != propuesta.AmountPerUnit
        || !string.Equals(actual.MunicipalityDaneCode, propuesta.MunicipalityDaneCode, StringComparison.Ordinal)
        || !string.Equals(actual.ActivityCode, propuesta.ActivityCode, StringComparison.Ordinal)
        || actual.MinimumBaseUvt != propuesta.MinimumBaseUvt || actual.MinimumBasePesos != propuesta.MinimumBasePesos
        || TaxRateConditionsDto.De(actual) != TaxRateConditionsDto.De(propuesta)
        || actual.AppliesTo != propuesta.AppliesTo || actual.Priority != propuesta.Priority
        || actual.ValidFrom != propuesta.ValidFrom
        || !string.Equals(actual.LegalSource, propuesta.LegalSource, StringComparison.Ordinal)
        || !MismoConcepto(actual, propuesta) || !MismoImpuesto(actual, propuesta);

    /// <summary>
    /// Las reglas de forma de una tarifa para el validador (400): código, nombre, porcentaje entre 0 y 1 (fracción),
    /// base mínima en UVT o en pesos (no las dos), municipio de cinco dígitos, actividad CIIU o <c>*</c> con municipio,
    /// vigencia y norma.
    /// </summary>
    public static void ReglasDeForma<T>(AbstractValidator<T> v, bool exigeCodigo) where T : IDatosDeTarifa
    {
        if (exigeCodigo)
            v.RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
                .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        v.RuleFor(x => x.Name).NotEmpty().MaximumLength(LargoDeNombre);
        v.RuleFor(x => x.Rate).InclusiveBetween(0m, 1m).When(x => x.Rate is not null)
            .WithMessage("La tarifa es una fracción entre 0 y 1 (0,19 para el 19 %).");
        v.RuleFor(x => x.AmountPerUnit).GreaterThan(0m).When(x => x.AmountPerUnit is not null);
        v.RuleFor(x => x).Must(x => x.Rate is null || x.AmountPerUnit is null)
            .WithMessage("Una tarifa es porcentual o por unidad, no las dos.");
        v.RuleFor(x => x.MinimumBaseUvt).GreaterThanOrEqualTo(0m).When(x => x.MinimumBaseUvt is not null);
        v.RuleFor(x => x.MinimumBasePesos).GreaterThanOrEqualTo(0m).When(x => x.MinimumBasePesos is not null);
        v.RuleFor(x => x).Must(x => x.MinimumBaseUvt is null || x.MinimumBasePesos is null)
            .WithMessage("La base mínima va en UVT o en pesos, no en las dos.");
        v.RuleFor(x => x.MunicipalityDaneCode).Matches(PatronDeMunicipio).When(x => !string.IsNullOrWhiteSpace(x.MunicipalityDaneCode))
            .WithMessage("El municipio es el código DANE (DIVIPOLA) de cinco dígitos.");
        v.RuleFor(x => x.ActivityCode).Matches(PatronDeActividad).When(x => !string.IsNullOrWhiteSpace(x.ActivityCode))
            .WithMessage("La actividad es el código CIIU (4 a 6 dígitos) o * para la tarifa general del municipio.");
        v.RuleFor(x => x).Must(x => string.IsNullOrWhiteSpace(x.ActivityCode) || x.ActivityCode == MotorTributario.ActividadGeneral || !string.IsNullOrWhiteSpace(x.MunicipalityDaneCode))
            .WithMessage("Una actividad distinta de * exige el municipio.");
        v.RuleFor(x => x.Conditions!.SubjectPersonType).Must(t => t is "01" or "02").When(x => x.Conditions?.SubjectPersonType is not null)
            .WithMessage("El tipo de persona del sujeto es 01 (natural) o 02 (jurídica).");
        v.RuleFor(x => x.AppliesTo).IsInEnum();
        v.RuleFor(x => x.Priority).GreaterThanOrEqualTo((short)0);
        v.RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null)
            .WithMessage("La vigencia no puede terminar antes de empezar.");
        v.RuleFor(x => x.LegalSource).NotEmpty().MaximumLength(LargoDeNorma).WithMessage("Escriba la norma que respalda la tarifa (hasta 200 caracteres).");
        v.RuleFor(x => x.Notes).MaximumLength(LargoDeNotas);
    }

    private static string Actividad(TaxRate t) => string.IsNullOrWhiteSpace(t.ActivityCode) ? MotorTributario.ActividadGeneral : t.ActivityCode!;

    private static bool MismoImpuesto(TaxRate a, TaxRate b) =>
        a.TaxDefinition is not null && b.TaxDefinition is not null
            ? ReferenceEquals(a.TaxDefinition, b.TaxDefinition) || (a.TaxDefinition.Id != 0 && a.TaxDefinition.Id == b.TaxDefinition.Id)
            : a.TaxDefinitionId != 0 && a.TaxDefinitionId == b.TaxDefinitionId;

    private static bool MismoConcepto(TaxRate a, TaxRate b)
    {
        if (a.WithholdingConcept is not null && b.WithholdingConcept is not null)
            return ReferenceEquals(a.WithholdingConcept, b.WithholdingConcept) || (a.WithholdingConcept.Id != 0 && a.WithholdingConcept.Id == b.WithholdingConcept.Id);
        var ida = a.WithholdingConcept?.Id ?? a.WithholdingConceptId;
        var idb = b.WithholdingConcept?.Id ?? b.WithholdingConceptId;
        return ida == idb;
    }

    private static string? Nulo(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>Normaliza un municipio o actividad para comparar.</summary>
    internal static bool EsCodigoDianValido(string? codigo) => codigo is null || Regex.IsMatch(codigo, PatronDeCodigoDian);
}
