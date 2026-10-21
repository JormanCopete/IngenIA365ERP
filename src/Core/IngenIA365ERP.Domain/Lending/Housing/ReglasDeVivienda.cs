using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Housing;

/// <summary>Un límite legal vigente (valor nacional <c>LIMITE</c>): su código, su valor y de dónde sale (fuente y <c>PublicId</c>).</summary>
public sealed record LimiteVigente(string Codigo, decimal Valor, string? Fuente = null, Guid? PublicId = null);

/// <summary>Los cinco límites de vivienda vigentes a una fecha (feature 013, K6b, T1051; <see cref="CatalogoDeLimitesLegales"/>).</summary>
public sealed record LimitesDeVivienda(
    LimiteVigente PlazoMinimoMeses,
    LimiteVigente PlazoMaximoMeses,
    LimiteVigente Financiable,
    LimiteVigente FinanciableVis,
    LimiteVigente CuotaIngreso);

/// <summary>El tope de tasa de vivienda vigente a una fecha (valor nacional <c>TOPE_TASA_VIVIENDA</c>, EA como fracción) y su fuente.</summary>
public sealed record TopeDeTasaDeVivienda(decimal Tasa, Guid PublicId, DateOnly VigenteDesde, string? Fuente = null);

/// <summary>Lo de una versión de línea que miran las reglas de vivienda: clase de tasa, la tasa más alta que pacta (la de la versión y sus bandas), plazos y unidad.</summary>
public sealed record LineaDeVivienda(RateKind ClaseDeTasa, decimal? TasaMasAlta, int PlazoMinimoMeses, int PlazoMaximoMeses, DenominationUnit Unidad, bool AdmiteUvr);

/// <summary>
/// Lo de una solicitud de vivienda que miran las reglas: el monto, el plazo en meses, el valor del inmueble (el de la hipoteca), si es de interés
/// social, los ingresos familiares mensuales y la primera cuota del plan simulado (en pesos).
/// </summary>
public sealed record SolicitudDeVivienda(decimal Monto, int PlazoMeses, decimal? ValorDelInmueble, bool EsVis, decimal? IngresosFamiliares, decimal? PrimeraCuota);

/// <summary>Una garantía de la solicitud o del crédito: su clase y, si es hipoteca, su grado.</summary>
public sealed record GarantiaDeVivienda(CollateralKind Clase, byte? GradoDeHipoteca);

/// <summary>
/// Las reglas de la Ley 546 de 1999 para un crédito de vivienda (feature 013, K6b, T1044, T1051, T1055; FR-024, D18), como parámetros con fuente: ningún
/// valor legal vive aquí —los cinco límites llegan del valor nacional <c>LIMITE</c> (<see cref="LimitesDeVivienda"/>), el tope del
/// <c>TOPE_TASA_VIVIENDA</c> y la usura de su valor nacional—.
/// <list type="bullet">
/// <item>Línea: tasa <b>fija</b> (<see cref="CodigoTasaVariable"/>); la tasa pactada más alta no supera el tope del día —el menor entre el tope de
/// vivienda y la usura, si la hay— (<see cref="CodigoTopeExcedido"/>, con el tope y su fuente); el plazo entre el mínimo y el máximo
/// (<see cref="CodigoPlazoFueraDeLimites"/>, nombrando el límite); UVR sólo si el catálogo la admite (<see cref="CodigoUvrNoAdmitida"/>).</item>
/// <item>Solicitud: el plazo dentro de los límites; el monto no pasa del porcentaje financiable del valor del inmueble —el de VIS si es de interés
/// social— (<see cref="CodigoFinanciacionExcedida"/>); la primera cuota no pasa de la fracción de los ingresos familiares
/// (<see cref="CodigoCuotaIngresoExcedida"/>); sin el valor del inmueble o sin los ingresos, se pide el dato.</item>
/// <item>Desembolso: una hipoteca de <b>primer grado</b> (<see cref="CodigoHipotecaDePrimerGrado"/>).</item>
/// </list>
/// Lo demás de la ley —interés vencido sin capitalizar, sin mora sobre intereses, prepago sin penalidad— lo aplica el motor con la versión de reglas
/// de vivienda (<see cref="VersionesDeReglas.Vivienda"/>). Puro. (nuevo)
/// </summary>
public static class ReglasDeVivienda
{
    public const string CodigoTasaVariable = "Lending.Housing.VariableRateNotAllowed";
    public const string CodigoTopeExcedido = "Lending.Housing.RateCapExceeded";
    public const string CodigoPlazoFueraDeLimites = "Lending.Housing.TermOutOfLimits";
    public const string CodigoUvrNoAdmitida = "Lending.Housing.UvrNotAdmitted";
    public const string CodigoFinanciacionExcedida = "Lending.Housing.FinancingExceeded";
    public const string CodigoCuotaIngresoExcedida = "Lending.Housing.InstallmentToIncomeExceeded";
    public const string CodigoDatoRequerido = "Lending.Housing.DataRequired";
    public const string CodigoHipotecaDePrimerGrado = "Lending.Housing.FirstRankMortgageRequired";

    /// <summary>El tope del día de un crédito de vivienda: el menor entre el tope de vivienda y la usura (los que haya); nulo si no hay ninguno.</summary>
    public static decimal? TopeDelDia(decimal? topeDeVivienda, decimal? usura) =>
        (topeDeVivienda, usura) switch
        {
            ({ } t, { } u) => Math.Min(t, u),
            ({ } t, null) => t,
            (null, { } u) => u,
            _ => null,
        };

    /// <summary>Las reglas de una versión de línea de vivienda (ver la clase). Vacía si cumple.</summary>
    public static IReadOnlyList<ProblemaDeCartera> ValidarLinea(LineaDeVivienda linea, TopeDeTasaDeVivienda? tope, decimal? usura, LimitesDeVivienda limites)
    {
        ArgumentNullException.ThrowIfNull(linea);
        ArgumentNullException.ThrowIfNull(limites);
        var problemas = new List<ProblemaDeCartera>();
        if (linea.ClaseDeTasa != RateKind.Fixed)
            problemas.Add(ProblemaDeCartera.Con(CodigoTasaVariable,
                "Un crédito de vivienda se pacta a tasa fija (Ley 546 de 1999): la línea no puede ser de tasa variable.", "field", "rateKind"));
        if (linea.Unidad == DenominationUnit.Uvr && !linea.AdmiteUvr)
            problemas.Add(ProblemaDeCartera.Con(CodigoUvrNoAdmitida,
                "La combinación de modalidad y sub-segmento no admite UVR en el catálogo de modalidades vigente.", "field", "denominationUnit"));
        if (linea.TasaMasAlta is { } tasa && TopeDelDia(tope?.Tasa, usura) is { } tope_ && tasa > tope_)
            problemas.Add(new ProblemaDeCartera(CodigoTopeExcedido,
                $"La tasa {ExplicacionDeCartera.Numero(tasa)} EA supera el tope de vivienda del día ({ExplicacionDeCartera.Numero(tope_)} EA"
                + (tope?.Fuente is { } f ? $", {f}" : string.Empty) + ").",
                new Dictionary<string, object?>
                {
                    ["field"] = "annualRate", ["rate"] = tasa, ["cap"] = tope_, ["housingRateCap"] = tope?.Tasa, ["usury"] = usura,
                    ["source"] = tope?.Fuente, ["capValuePublicId"] = tope?.PublicId,
                }));
        problemas.AddRange(Plazo(linea.PlazoMinimoMeses, "minTermMonths", limites));
        if (linea.PlazoMaximoMeses != linea.PlazoMinimoMeses) problemas.AddRange(Plazo(linea.PlazoMaximoMeses, "maxTermMonths", limites));
        return problemas;
    }

    /// <summary>Las reglas de una solicitud de vivienda (ver la clase). Vacía si cumple.</summary>
    public static IReadOnlyList<ProblemaDeCartera> ValidarSolicitud(SolicitudDeVivienda s, LimitesDeVivienda limites)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(limites);
        var problemas = new List<ProblemaDeCartera>(Plazo(s.PlazoMeses, "termMonths", limites));

        if (s.ValorDelInmueble is not { } inmueble || inmueble <= 0m)
            problemas.Add(ProblemaDeCartera.Con(CodigoDatoRequerido,
                "El crédito de vivienda necesita el valor del inmueble (el avalúo de la hipoteca) para medir el porcentaje financiable.", "field", "propertyValue"));
        else
        {
            var limite = s.EsVis ? limites.FinanciableVis : limites.Financiable;
            var maximo = decimal.Round(inmueble * limite.Valor, 2, MidpointRounding.ToZero);
            if (s.Monto > maximo)
                problemas.Add(new ProblemaDeCartera(CodigoFinanciacionExcedida,
                    $"El monto {ExplicacionDeCartera.Numero(s.Monto)} supera el {ExplicacionDeCartera.Numero(limite.Valor)} del valor del inmueble "
                    + $"({ExplicacionDeCartera.Numero(maximo)}) que permite el límite {limite.Codigo}" + Fuente(limite) + ".",
                    Datos(limite, "amount", s.Monto, maximo)));
        }

        if (s.IngresosFamiliares is not { } ingresos || ingresos <= 0m)
            problemas.Add(ProblemaDeCartera.Con(CodigoDatoRequerido,
                "El crédito de vivienda necesita los ingresos familiares mensuales para medir la relación cuota-ingreso.", "field", "familyIncome"));
        else if (s.PrimeraCuota is { } cuota)
        {
            var maximo = decimal.Round(ingresos * limites.CuotaIngreso.Valor, 2, MidpointRounding.ToZero);
            if (cuota > maximo)
                problemas.Add(new ProblemaDeCartera(CodigoCuotaIngresoExcedida,
                    $"La primera cuota {ExplicacionDeCartera.Numero(cuota)} supera el {ExplicacionDeCartera.Numero(limites.CuotaIngreso.Valor)} de los ingresos "
                    + $"familiares ({ExplicacionDeCartera.Numero(maximo)}) que permite el límite {limites.CuotaIngreso.Codigo}" + Fuente(limites.CuotaIngreso) + ".",
                    Datos(limites.CuotaIngreso, "firstInstallment", cuota, maximo)));
        }
        return problemas;
    }

    /// <summary>
    /// El desembolso de un crédito de vivienda exige una hipoteca de primer grado entre sus garantías (<see cref="CodigoHipotecaDePrimerGrado"/>); nulo si
    /// la tiene.
    /// </summary>
    public static ProblemaDeCartera? ValidarGarantias(IEnumerable<GarantiaDeVivienda> garantias)
    {
        ArgumentNullException.ThrowIfNull(garantias);
        var lista = garantias.ToList();
        if (lista.Any(g => g.Clase == CollateralKind.Mortgage && g.GradoDeHipoteca == 1)) return null;
        var hipotecas = lista.Where(g => g.Clase == CollateralKind.Mortgage).ToList();
        return new ProblemaDeCartera(CodigoHipotecaDePrimerGrado,
            hipotecas.Count == 0
                ? "Un crédito de vivienda se garantiza con hipoteca de primer grado (Ley 546 de 1999): registre la hipoteca antes de desembolsar."
                : $"La hipoteca registrada es de grado {string.Join(", ", hipotecas.Select(h => h.GradoDeHipoteca?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "sin grado"))}: "
                  + "un crédito de vivienda exige una de primer grado (Ley 546 de 1999).",
            new Dictionary<string, object?> { ["mortgageRanks"] = hipotecas.Select(h => h.GradoDeHipoteca).ToList() });
    }

    private static IEnumerable<ProblemaDeCartera> Plazo(int meses, string campo, LimitesDeVivienda limites)
    {
        if (meses < limites.PlazoMinimoMeses.Valor)
            yield return new ProblemaDeCartera(CodigoPlazoFueraDeLimites,
                $"El plazo de {meses} meses es menor que el mínimo de vivienda ({ExplicacionDeCartera.Numero(limites.PlazoMinimoMeses.Valor)} meses, límite "
                + $"{limites.PlazoMinimoMeses.Codigo}{Fuente(limites.PlazoMinimoMeses)}).",
                Datos(limites.PlazoMinimoMeses, campo, meses, limites.PlazoMinimoMeses.Valor));
        if (meses > limites.PlazoMaximoMeses.Valor)
            yield return new ProblemaDeCartera(CodigoPlazoFueraDeLimites,
                $"El plazo de {meses} meses supera el máximo de vivienda ({ExplicacionDeCartera.Numero(limites.PlazoMaximoMeses.Valor)} meses, límite "
                + $"{limites.PlazoMaximoMeses.Codigo}{Fuente(limites.PlazoMaximoMeses)}).",
                Datos(limites.PlazoMaximoMeses, campo, meses, limites.PlazoMaximoMeses.Valor));
    }

    private static string Fuente(LimiteVigente l) => l.Fuente is { Length: > 0 } f ? $", {f}" : string.Empty;

    private static Dictionary<string, object?> Datos(LimiteVigente limite, string campo, decimal valor, decimal permitido) => new()
    {
        ["field"] = campo, ["limitCode"] = limite.Codigo, ["limit"] = limite.Valor, ["value"] = valor, ["allowed"] = permitido,
        ["source"] = limite.Fuente, ["limitValuePublicId"] = limite.PublicId,
    };
}
