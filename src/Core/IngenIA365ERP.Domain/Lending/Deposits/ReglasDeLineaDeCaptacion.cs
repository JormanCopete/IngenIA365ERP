using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// Las invariantes de las versiones de las líneas de captación y de aportes (feature 014, US1, T064; data-model §2.1, §2.2, §2.5; FR-007, FR-014,
/// FR-018, FR-020, FR-028, FR-029, FR-038, FR-043): puras y sin consultar nada fuera de lo que reciben. La aplicación les pasa lo que lee (el perfil
/// regulatorio vigente, los topes con su fuente, la anticipación del aviso) y traduce el <see cref="ProblemaDeCartera"/> al contrato.
/// <list type="bullet">
/// <item><b>Sin retroactivas</b>: una versión no rige antes de hoy (<see cref="CodigoCaptacionRetroactiva"/>, <see cref="CodigoAportesRetroactiva"/>).</item>
/// <item><b>Clase permitida por el perfil</b>: una línea de captación sólo se activa si el perfil vigente autoriza captar
/// (<see cref="CodigoNoPermitidaPorElPerfil"/>, FR-018); la de aportes, siempre.</item>
/// <item><b>Fondos de empleados</b>: la cuota obligatoria (aportes y ahorro permanente) como fracción del ingreso no supera el tope legal y la parte que
/// va a aportes no baja de la fracción mínima (<see cref="CodigoCuotaSobreElTope"/>, <see cref="CodigoFraccionBajoLaLey"/>).</item>
/// <item><b>Tramos sin solape</b>: los de saldo son contiguos y no se cruzan (<see cref="CodigoTramosCruzados"/>).</item>
/// <item>Las columnas de una clase sólo se llenan en esa clase; las del CDAT y del contractual son obligatorias en la suya
/// (<see cref="CodigoLineaIncompleta"/>); toda tasa con a lo sumo seis decimales de fracción (<c>Lending.Rate.TooPrecise</c>).</item>
/// </list>
/// Nunca consulta la usura: las captaciones no la usan (R01). (nuevo)
/// </summary>
public static class ReglasDeLineaDeCaptacion
{
    public const string CodigoCaptacionInvalida = "Lending.DepositLine.Invalid";
    public const string CodigoAportesInvalida = "Lending.ContributionLine.Invalid";
    public const string CodigoCaptacionRetroactiva = "Lending.DepositLine.Retroactive";
    public const string CodigoAportesRetroactiva = "Lending.ContributionLine.Retroactive";
    public const string CodigoClaseInmutable = "Lending.DepositLine.ClassImmutable";
    public const string CodigoTramosCruzados = "Lending.Savings.RateTiersOverlap";
    public const string CodigoLineaIncompleta = "Lending.TermDeposit.LineIncomplete";
    public const string CodigoNoPermitidaPorElPerfil = "Lending.Savings.LineNotAllowedByProfile";
    public const string CodigoCuotaSobreElTope = "Lending.Contributions.InstallmentCapExceeded";
    public const string CodigoFraccionBajoLaLey = "Lending.Contributions.FractionBelowLegal";
    public const string CodigoAvisoCorto = "Lending.Savings.RateChangeNoticeTooShort";
    public const string CodigoMenosEstrictoQueLaLey = "Lending.Parameter.BelowLegalFloor";

    /// <summary>Las clases que un perfil que autoriza captar admite: todas (el perfil de la 013 no distingue clases).</summary>
    public static IReadOnlyList<DepositClass> ClasesDeCaptacion { get; } =
        [DepositClass.Demand, DepositClass.Contractual, DepositClass.Permanent, DepositClass.TermDeposit];

    /// <summary>La clave del parámetro de tarifa de retención que sugiere la clase (§9.1, research E15), salvo que la versión diga otra.</summary>
    public static string TarifaDeRetencionDe(BorradorDeVersionDeCaptacion b)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (!string.IsNullOrWhiteSpace(b.WithholdingRateParameterCode)) return b.WithholdingRateParameterCode.Trim();
        return b.DepositClass switch
        {
            DepositClass.TermDeposit => ParametrosDeCartera.RetencionTarifaCdat,
            DepositClass.Contractual => ParametrosDeCartera.RetencionTarifaContractual,
            DepositClass.Permanent => ParametrosDeCartera.RetencionTarifaPermanente,
            _ => ParametrosDeCartera.RetencionTarifaAhorro,
        };
    }

    /// <summary>Las claves de tarifa de retención que una versión puede nombrar.</summary>
    public static IReadOnlyList<string> TarifasDeRetencion { get; } =
    [
        ParametrosDeCartera.RetencionTarifaAhorro, ParametrosDeCartera.RetencionTarifaCdat, ParametrosDeCartera.RetencionTarifaContractual,
        ParametrosDeCartera.RetencionTarifaPermanente,
    ];

    // ------------------------------------------------------------------------------------------------------- captación --

    /// <summary>Lo que está mal en la versión de captación (sin consultar nada fuera de ella), o nulo si se puede registrar.</summary>
    public static ProblemaDeCartera? ValidarVersion(BorradorDeVersionDeCaptacion b, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (b.ValidFrom < hoy)
            return ProblemaDeCartera.Con(CodigoCaptacionRetroactiva,
                $"La versión no puede regir desde {Fecha(b.ValidFrom)}, antes de hoy ({Fecha(hoy)}): registre una desde hoy o después.",
                "validFrom", Fecha(b.ValidFrom));
        if (string.IsNullOrWhiteSpace(b.Reason)) return Captacion("reason", "Diga el motivo de la versión.");
        if (!Enum.IsDefined(b.DepositClass)) return Captacion("depositClass", "Clase de captación desconocida.");

        foreach (var (campo, tasa) in TasasPactadas(b))
        {
            if (tasa is not { } t) continue;
            if (!EquivalenciaDeTasas.EsTasaPactable(t))
                return ProblemaDeCartera.Con(EquivalenciaDeTasas.CodigoTasaDemasiadoPrecisa,
                    $"La tasa de «{campo}» tiene más de seis decimales de fracción; escríbala con seis como máximo.", "field", campo);
            if (t < 0m) return Captacion(campo, "Una tasa no puede ser negativa.");
        }

        var problemaDeTasa = ValidarTasa(b);
        if (problemaDeTasa is not null) return problemaDeTasa;
        var problemaDeTramos = ValidarTramos(b);
        if (problemaDeTramos is not null) return problemaDeTramos;

        if (!Enum.IsDefined(b.BalanceMethod)) return Captacion("balanceMethod", "Método de saldo desconocido.");
        if (!Enum.IsDefined(b.DayCount)) return Captacion("dayCount", "Convención de días desconocida.");
        if (!Enum.IsDefined(b.RateEquivalence)) return Captacion("rateEquivalence", "Equivalencia de tasas desconocida.");
        if (!RedondeoDeCartera.UnidadesAdmitidas.Contains(b.RoundingUnit))
            return Captacion("roundingUnit", "La unidad de redondeo es 0,01; 1; 10; 100 o 1.000.");
        if (!Enum.IsDefined(b.RoundingMode) || !Enum.IsDefined(b.RoundingMoment)) return Captacion("roundingMode", "Redondeo desconocido.");
        if (!Enum.IsDefined(b.SettlementPeriodicity)) return Captacion("settlementPeriodicity", "Periodicidad de liquidación desconocida.");
        if (b.SettlementDay is < 1 or > 31) return Captacion("settlementDay", "El día de liquidación va de 1 a 31.");
        foreach (var (campo, monto) in Minimos(b))
            if (monto < 0m || !EsMonto(monto)) return Captacion(campo, "Los mínimos son pesos no negativos con hasta dos decimales.");
        foreach (var (campo, monto) in Limites(b))
            if (monto is { } m && (m <= 0m || !EsMonto(m))) return Captacion(campo, "Los límites son pesos mayores que cero con hasta dos decimales.");
        if (b.MaxWithdrawalPerOperation > b.MaxWithdrawalPerDay)
            return Captacion("maxWithdrawalPerOperation", "El retiro máximo por operación no supera el diario.");
        if (b.MaxCashPerOperation > b.MaxCashPerDay) return Captacion("maxCashPerOperation", "El efectivo máximo por operación no supera el diario.");
        if (b.InactivityDays is < 1) return Captacion("inactivityDays", "Los días para inactividad son al menos uno (o vacío: no se inactiva).");
        if (b.RateChangeNoticeDays < 0) return Captacion("rateChangeNoticeDays", "La anticipación del aviso no puede ser negativa.");
        var tarifa = TarifaDeRetencionDe(b);
        if (!TarifasDeRetencion.Contains(tarifa, StringComparer.Ordinal))
            return Captacion("withholdingRateParameterCode", $"«{tarifa}» no es una tarifa de retención de captaciones. Use una de: {string.Join(", ", TarifasDeRetencion)}.");
        if (b.BranchPublicIds.Any(o => o == Guid.Empty)) return Captacion("branchPublicIds", "Una oficina no puede ir vacía.");
        var problemaDeCargos = ValidarCargos(b.Charges);
        if (problemaDeCargos is not null) return problemaDeCargos;

        return ValidarBloquesDeClase(b);
    }

    /// <summary>
    /// US4 (T114; data-model §2.3; FR-128): el catálogo de cargos de una versión. Código de catálogo único en la versión (hasta 10, en mayúsculas, sin
    /// espacios), nombre, clase y disparador conocidos; la periodicidad es obligatoria con <c>Periodic</c> e <c>Inactivity</c>; el valor fijo o el
    /// porcentaje con su base según el modo; mínimo ≤ máximo; los gratuitos sólo en los cargos por evento sobre gratuitos; gravado exige el código del
    /// servicio en Comercial (R14). Montos con hasta dos decimales y porcentajes como fracción de hasta seis.
    /// </summary>
    public static ProblemaDeCartera? ValidarCargos(IReadOnlyList<BorradorDeCargoDeCaptacion> cargos)
    {
        ArgumentNullException.ThrowIfNull(cargos);
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in cargos)
        {
            var codigo = c.Code?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!System.Text.RegularExpressions.Regex.IsMatch(codigo, "^[A-Z0-9]{1,10}$"))
                return Captacion("charges.code", $"«{c.Code}» no es un código de cargo: hasta 10 letras o números, sin espacios.");
            if (!vistos.Add(codigo)) return Captacion("charges.code", $"El cargo {codigo} está dos veces en la versión.");
            if (string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > 120) return Captacion("charges.name", $"El cargo {codigo} lleva nombre (hasta 120).");
            if (!Enum.IsDefined(c.Kind) || !Enum.IsDefined(c.Trigger) || !Enum.IsDefined(c.CalculationMode) || !Enum.IsDefined(c.ShortfallPolicy))
                return Captacion("charges.kind", $"El cargo {codigo} tiene una clase, un disparador, un modo o una política desconocidos.");
            var periodico = c.Trigger is ChargeTrigger.Periodic or ChargeTrigger.Inactivity;
            if (periodico && (c.Periodicity is not { } p || !Enum.IsDefined(p) || p == RepaymentPeriodicity.SinglePayment))
                return Captacion("charges.periodicity", $"El cargo {codigo} es periódico: diga su periodicidad.");
            if (!periodico && c.Periodicity is not null) return Captacion("charges.periodicity", $"El cargo {codigo} no es periódico: no lleva periodicidad.");
            if (CargosDeCuenta.EsSobreGratuitos(c.Trigger))
            {
                if (c.FreeOperationsPerPeriod is < 0) return Captacion("charges.freeOperationsPerPeriod", $"Los gratuitos del cargo {codigo} no son negativos.");
                if (c.FreeOperationsPerPeriod is not null && c.FreePeriod is null)
                    return Captacion("charges.freePeriod", $"Los gratuitos del cargo {codigo} son por período: diga cuál.");
            }
            else if (c.FreeOperationsPerPeriod is not null || c.FreePeriod is not null)
            {
                return Captacion("charges.freeOperationsPerPeriod", $"Sólo un cargo sobre los gratuitos de retiros o consignaciones lleva gratuitos ({codigo}).");
            }
            if (c.CalculationMode == ChargeCalculation.FixedAmount)
            {
                if (c.FixedAmount is not { } fijo || fijo <= 0m || !EsMonto(fijo))
                    return Captacion("charges.fixedAmount", $"El cargo {codigo} es de valor fijo: diga el valor (mayor que cero, hasta dos decimales).");
                if (c.Percentage is not null || c.PercentageBase is not null)
                    return Captacion("charges.percentage", $"El cargo {codigo} es de valor fijo: no lleva porcentaje.");
            }
            else
            {
                if (c.Percentage is not { } pct || pct <= 0m || pct > 1m || !EsFraccion(pct) || c.PercentageBase is not { } pb || !Enum.IsDefined(pb))
                    return Captacion("charges.percentage", $"El cargo {codigo} es un porcentaje: diga la fracción (mayor que 0 y hasta 1) y su base.");
                if (c.FixedAmount is not null) return Captacion("charges.fixedAmount", $"El cargo {codigo} es un porcentaje: no lleva valor fijo.");
            }
            foreach (var (campo, monto) in new[] { ("charges.minAmount", c.MinAmount), ("charges.maxAmount", c.MaxAmount),
                         ("charges.exemptIfAverageBalanceAtLeast", c.ExemptIfAverageBalanceAtLeast), ("charges.exemptIfMinimumBalanceAtLeast", c.ExemptIfMinimumBalanceAtLeast) })
                if (monto is { } m && (m < 0m || !EsMonto(m))) return Captacion(campo, $"Los montos del cargo {codigo} son pesos no negativos con hasta dos decimales.");
            if (c.MinAmount is { } min && c.MaxAmount is { } max && min > max) return Captacion("charges.minAmount", $"El mínimo del cargo {codigo} supera su máximo.");
            if (c.ExemptIfSeniorityMonthsAtLeast is < 0) return Captacion("charges.exemptIfSeniorityMonthsAtLeast", $"La antigüedad del cargo {codigo} no es negativa.");
            if (c.ExemptBranchPublicIds.Any(o => o == Guid.Empty)) return Captacion("charges.exemptBranchPublicIds", "Una oficina exonerada no puede ir vacía.");
            if (c.IsVatTaxed && string.IsNullOrWhiteSpace(c.CommercialItemCode))
                return Captacion("charges.commercialItemCode", $"El cargo {codigo} está gravado con IVA: diga el código del servicio en Comercial, que lo factura.");
            if (c.CommercialItemCode is { Length: > 30 }) return Captacion("charges.commercialItemCode", "El código del servicio en Comercial tiene hasta 30 caracteres.");
        }
        return null;
    }

    /// <summary>La clase no cambia entre versiones de la línea (data-model §2.1).</summary>
    public static ProblemaDeCartera? ValidarClaseInmutable(DepositClass anterior, DepositClass nueva) => anterior == nueva
        ? null
        : new ProblemaDeCartera(CodigoClaseInmutable,
            $"La clase de una línea de captación no cambia: la línea es de {NombreDeClase(anterior)} y la versión dice {NombreDeClase(nueva)}. Cree otra línea.",
            new Dictionary<string, object?> { ["depositClass"] = anterior.ToString(), ["requested"] = nueva.ToString(), ["field"] = "depositClass" });

    /// <summary>
    /// FR-018 (US1-1): una línea de captación se activa sólo si el perfil regulatorio vigente autoriza captar; si no, sólo puede ofrecer aportes.
    /// </summary>
    public static ProblemaDeCartera? ValidarClaseAutorizada(DepositClass clase, bool perfilAutorizaCaptar, string? tipoDeOrganizacion, Guid? perfilPublicId) =>
        perfilAutorizaCaptar
            ? null
            : new ProblemaDeCartera(CodigoNoPermitidaPorElPerfil,
                tipoDeOrganizacion is null
                    ? $"La cooperativa no tiene un perfil regulatorio vigente que la autorice a captar: no puede activar una línea de {NombreDeClase(clase)}. Sólo puede ofrecer aportes."
                    : $"El perfil regulatorio vigente ({tipoDeOrganizacion}) no autoriza a la cooperativa a captar ahorro: no puede activar una línea de {NombreDeClase(clase)}. Sólo puede ofrecer aportes.",
                new Dictionary<string, object?>
                {
                    ["depositClass"] = clase.ToString(),
                    ["profilePublicId"] = perfilPublicId,
                    ["organizationTypeCode"] = tipoDeOrganizacion,
                    ["allowedClasses"] = Array.Empty<string>(),
                });

    /// <summary>
    /// FR-038: un cambio de tasa de una línea a la vista que no declara tasa pactada se avisa a los titulares con <paramref name="diasDeAnticipacion"/>;
    /// la versión no puede regir antes de hoy más esos días.
    /// </summary>
    public static ProblemaDeCartera? ValidarAnticipacionDelAviso(DateOnly validFrom, DateOnly hoy, int diasDeAnticipacion)
    {
        if (diasDeAnticipacion <= 0) return null;
        var primera = hoy.AddDays(diasDeAnticipacion);
        return validFrom >= primera
            ? null
            : new ProblemaDeCartera(CodigoAvisoCorto,
                $"El cambio de tasa se avisa a los titulares con {diasDeAnticipacion} días de anticipación: la versión puede regir desde el {Fecha(primera)}.",
                new Dictionary<string, object?> { ["requiredDays"] = diasDeAnticipacion, ["earliestValidFrom"] = Fecha(primera), ["field"] = "validFrom" });
    }

    // --------------------------------------------------------------------------------------------------------- aportes --

    /// <summary>Lo que está mal en la versión de aportes (sin consultar nada fuera de ella), o nulo si se puede registrar.</summary>
    public static ProblemaDeCartera? ValidarVersionDeAportes(BorradorDeVersionDeAportes b, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (b.ValidFrom < hoy)
            return ProblemaDeCartera.Con(CodigoAportesRetroactiva,
                $"La versión no puede regir desde {Fecha(b.ValidFrom)}, antes de hoy ({Fecha(hoy)}): registre una desde hoy o después.",
                "validFrom", Fecha(b.ValidFrom));
        if (string.IsNullOrWhiteSpace(b.Reason)) return Aportes("reason", "Diga el motivo de la versión.");

        var minimo = ValidarValor(b.MinimumMode, b.MinimumAmount, b.MinimumFraction, "minimum", "el mínimo no reducible", admiteCero: true);
        if (minimo is not null) return minimo;
        var cuota = ValidarValor(b.InstallmentMode, b.InstallmentAmount, b.InstallmentFraction, "installment", "la cuota periódica", admiteCero: false);
        if (cuota is not null) return cuota;
        if (!RedondeoDeCartera.UnidadesAdmitidas.Contains(b.InstallmentRoundingUnit))
            return Aportes("installmentRoundingUnit", "La unidad de redondeo de la cuota es 0,01; 1; 10; 100 o 1.000.");
        if (!Enum.IsDefined(b.InstallmentRoundingMode)) return Aportes("installmentRoundingMode", "Modo de redondeo desconocido.");
        if (!Enum.IsDefined(b.InstallmentPeriodicity) || b.InstallmentPeriodicity == RepaymentPeriodicity.SinglePayment)
            return Aportes("installmentPeriodicity", "La cuota de aportes es periódica: elija su periodicidad.");
        if (b.InstallmentDueDay is < 1 or > 31) return Aportes("installmentDueDay", "El día de vencimiento de la cuota va de 1 a 31.");

        if (b.PermanentSavingsLineId is null != b.PermanentSavingsFraction is null)
            return Aportes("permanentSavings", "El reparto con ahorro permanente dice la línea y la parte de la cuota que va a ella.");
        if (b.PermanentSavingsFraction is { } parte && (parte <= 0m || parte >= 1m || !EsFraccion(parte)))
            return Aportes("permanentSavingsFraction", "La parte de la cuota que va a ahorro permanente es una fracción entre 0 y 1.");
        if (b.MaxParticipationFraction is { } limite && (limite <= 0m || limite > 1m || !EsFraccion(limite)))
            return Aportes("maxParticipationFraction", "El límite de participación es una fracción del total de aportes, mayor que 0 y hasta 1.");
        if (!Enum.IsDefined(b.ExcessAction)) return Aportes("excessAction", "Qué hacer con el exceso: rechazarlo o devolverlo por decisión.");
        if (b.RefundTermDays < 0) return Aportes("refundTermDays", "El plazo de devolución no puede ser negativo.");
        return null;
    }

    /// <summary>
    /// US1-3 (FR-014; Ley 2496 de 2025, a validar): en un fondo de empleados la cuota obligatoria —aportes más ahorro permanente— como fracción del
    /// ingreso no supera <paramref name="tope"/>, y la parte que va a aportes no baja de <paramref name="fraccionMinima"/>. Un tope nulo no aplica.
    /// Con cuota en valor fijo o en salarios mínimos, el tope se controla por persona al generar la cuota (US3).
    /// </summary>
    public static ProblemaDeCartera? ValidarTopesDeFondoDeEmpleados(BorradorDeVersionDeAportes b, decimal? tope, string? fuenteDelTope,
        decimal? fraccionMinima, string? fuenteDeLaFraccion)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (tope is { } t && b.InstallmentMode == ContributionAmountMode.IncomeFraction && b.InstallmentFraction is { } f && f > t)
            return new ProblemaDeCartera(CodigoCuotaSobreElTope,
                $"La cuota obligatoria de aportes y ahorro permanente ({Porcentaje(f)} del ingreso) supera el tope de un fondo de empleados "
                + $"({Porcentaje(t)}, {fuenteDelTope}). Bájela.",
                new Dictionary<string, object?>
                {
                    ["capPercent"] = t, ["installmentFraction"] = f, ["contributionsShareMin"] = fraccionMinima, ["source"] = fuenteDelTope,
                    ["field"] = "installment",
                });
        if (fraccionMinima is { } minima && b.FraccionAAportes < minima)
            return new ProblemaDeCartera(CodigoFraccionBajoLaLey,
                $"La parte de la cuota que va a aportes ({Porcentaje(b.FraccionAAportes)}) es menor que la mínima de un fondo de empleados "
                + $"({Porcentaje(minima)}, {fuenteDeLaFraccion}). Baje la parte del ahorro permanente.",
                new Dictionary<string, object?>
                {
                    ["contributionsShare"] = b.FraccionAAportes, ["contributionsShareMin"] = minima, ["source"] = fuenteDeLaFraccion,
                    ["capPercent"] = tope, ["field"] = "permanentSavingsFraction",
                });
        return null;
    }

    /// <summary>FR-043: el límite propio de participación nunca es menos estricto que el legal vigente (<paramref name="limiteLegal"/>; nulo = no hay).</summary>
    public static ProblemaDeCartera? ValidarLimiteDeParticipacion(BorradorDeVersionDeAportes b, decimal? limiteLegal, string? clave, string? fuente)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (limiteLegal is not { } legal || b.MaxParticipationFraction is not { } propio || propio <= legal) return null;
        return new ProblemaDeCartera(CodigoMenosEstrictoQueLaLey,
            $"El límite de participación de la línea ({Porcentaje(propio)}) es menos estricto que el legal vigente ({Porcentaje(legal)}, {fuente}). "
            + "La cooperativa sólo puede ser más estricta.",
            new Dictionary<string, object?>
            {
                ["key"] = clave, ["value"] = propio, ["limitValue"] = legal, ["source"] = fuente, ["field"] = "maxParticipationFraction",
            });
    }

    // ------------------------------------------------------------------------------------------------------- privados --

    private static ProblemaDeCartera? ValidarTasa(BorradorDeVersionDeCaptacion b)
    {
        switch (b.RateKind)
        {
            case RateKind.Fixed:
                if (b.AnnualRate is null && b.RateTiers.Count == 0) return Captacion("annualRate", "La tasa fija necesita su efectiva anual o sus tramos.");
                if (b.IndexCode is not null || b.Margin is not null) return Captacion("indexCode", "La tasa fija no lleva índice ni margen.");
                if (b.RateTiers.Any(t => t.AnnualRate is null || t.Margin is not null))
                    return Captacion("rateTiers", "Con tasa fija, cada tramo dice su efectiva anual (no un margen).");
                break;
            case RateKind.Variable:
                if (string.IsNullOrWhiteSpace(b.IndexCode) || string.IsNullOrWhiteSpace(b.IndexKey) || (b.Margin is null && b.RateTiers.Count == 0))
                    return Captacion("indexCode", "La tasa variable necesita el índice, su clave y el margen (o los márgenes de sus tramos).");
                if (b.AnnualRate is not null) return Captacion("annualRate", "La tasa variable no lleva efectiva anual fija.");
                if (b.RateTiers.Any(t => t.Margin is null || t.AnnualRate is not null))
                    return Captacion("rateTiers", "Con tasa variable, cada tramo dice su margen (no una efectiva anual).");
                break;
            default:
                return Captacion("rateKind", "Clase de tasa desconocida.");
        }
        if (b.RateFloor is { } piso && b.RateCeiling is { } techo && piso > techo) return Captacion("rateFloor", "El piso de la tasa supera el techo.");
        if (b.RateTierMode is { } modo && !Enum.IsDefined(modo)) return Captacion("rateTierMode", "Modo de tramos desconocido.");
        if (b.ResetPeriodicity is { } reajuste && !Enum.IsDefined(reajuste)) return Captacion("resetPeriodicity", "Periodicidad de reajuste desconocida.");
        return null;
    }

    private static ProblemaDeCartera? ValidarTramos(BorradorDeVersionDeCaptacion b)
    {
        if (b.RateTiers.Count == 0) return null;
        if (b.RateTiers.Select(t => t.Order).Distinct().Count() != b.RateTiers.Count) return Captacion("rateTiers", "Cada tramo tiene un orden distinto.");
        if (b.RateTiers.Any(t => t.MinBalance < 0m || t.MinAmount < 0m || t.MinTermDays < 1
                || t.MinBalance > t.MaxBalance || t.MinAmount > t.MaxAmount || t.MinTermDays > t.MaxTermDays))
            return Captacion("rateTiers", "Cada tramo tiene su desde no mayor que su hasta, y saldos, montos y plazos no negativos.");

        if (b.DepositClass == DepositClass.TermDeposit)
        {
            // CDAT: por plazo y monto constituido; gana el primero por orden que casa (pueden cruzarse a propósito).
            if (b.RateTiers.Any(t => t.MinBalance is not null || t.MaxBalance is not null || (t.MinTermDays is null && t.MaxTermDays is null
                    && t.MinAmount is null && t.MaxAmount is null)))
                return Captacion("rateTiers", "En el CDAT los tramos son por plazo y monto, no por saldo.");
            return null;
        }

        // A la vista, contractual y permanente: por saldo, contiguos y sin cruzarse (data-model §2.2).
        if (b.RateTiers.Any(t => !t.EsPorSaldo)) return Captacion("rateTiers", "En esta clase los tramos son por saldo, no por plazo ni monto.");
        var ordenados = b.RateTiers.OrderBy(t => t.MinBalance ?? 0m).ToList();
        if ((ordenados[0].MinBalance ?? 0m) != 0m)
            return Cruzados("El primer tramo de saldo empieza en cero: si no, un saldo menor no tendría tasa.", ordenados[0]);
        for (var i = 1; i < ordenados.Count; i++)
        {
            var anterior = ordenados[i - 1];
            var este = ordenados[i];
            if (anterior.MaxBalance is not { } hasta)
                return Cruzados($"El tramo {anterior.Order} no tiene hasta y hay otro después: sólo el último queda abierto.", anterior);
            var desde = este.MinBalance ?? 0m;
            if (desde <= hasta) return Cruzados($"Los tramos {anterior.Order} y {este.Order} se cruzan: el segundo empieza antes de que termine el primero.", este);
            if (desde - hasta > RedondeoDeCartera.Centavo)
                return Cruzados($"Entre los tramos {anterior.Order} y {este.Order} queda un hueco: el segundo empieza un centavo después de que termina el primero.", este);
        }
        return null;
    }

    private static ProblemaDeCartera Cruzados(string mensaje, BorradorDeTramoDeCaptacion tramo) =>
        new(CodigoTramosCruzados, mensaje, new Dictionary<string, object?> { ["order"] = tramo.Order, ["field"] = "rateTiers" });

    private static ProblemaDeCartera? ValidarBloquesDeClase(BorradorDeVersionDeCaptacion b)
    {
        var esCdat = b.DepositClass == DepositClass.TermDeposit;
        var esContractual = b.DepositClass == DepositClass.Contractual;
        var esPermanente = b.DepositClass == DepositClass.Permanent;

        if (!esCdat && TieneAlgoDeCdat(b)) return Captacion("termDeposit", "Las condiciones del CDAT sólo van en una línea de CDAT.");
        if (!esContractual && TieneAlgoDeContractual(b)) return Captacion("contractual", "Las condiciones del ahorro contractual sólo van en una línea contractual.");
        if (!esPermanente && b.WithdrawableOnlyOnMembershipWithdrawal is not null)
            return Captacion("permanent", "«Sólo se retira al retiro del asociado» va sólo en una línea de ahorro permanente.");
        if ((esCdat || esPermanente) && b.AllowsPartialWithdrawals)
            return Captacion("allowsPartialWithdrawals", "El CDAT y el ahorro permanente no admiten retiros parciales.");

        if (esCdat)
        {
            if (b.BalanceMethod != BalanceMethod.DailyBalance) return Captacion("balanceMethod", "El CDAT causa siempre por saldo diario.");
            if (b.RateChangeNoticeDays != 0) return Captacion("rateChangeNoticeDays", "El CDAT sella su tasa: no lleva aviso de cambio de tasa.");
            if (b.MinTermDays is not >= 1 || b.MaxTermDays is not { } maximo) return Incompleta("minTermDays", "El CDAT dice su plazo mínimo y máximo en días.");
            if (b.MinTermDays > maximo) return Captacion("minTermDays", "El plazo mínimo no supera el máximo.");
            if (b.InterestPeriodicities.Count == 0) return Incompleta("interestPeriodicities", "El CDAT dice cómo paga sus intereses (periódicos o al vencimiento).");
            if (b.InterestPeriodicities.Any(p => !Enum.IsDefined(p)) || b.InterestPeriodicities.Distinct().Count() != b.InterestPeriodicities.Count)
                return Captacion("interestPeriodicities", "Cada periodicidad de intereses se nombra una sola vez y existe.");
            if (b.InterestTimings.Count == 0) return Incompleta("interestTimings", "El CDAT dice si sus intereses son vencidos, anticipados o ambos.");
            if (b.InterestTimings.Any(t => !Enum.IsDefined(t)) || b.InterestTimings.Distinct().Count() != b.InterestTimings.Count)
                return Captacion("interestTimings", "Cada modalidad de intereses se nombra una sola vez y existe.");
            if (b.AutoRollover is null || b.RolloverRateRule is null || b.CapitalizesOnRollover is null)
                return Incompleta("rollover", "El CDAT dice si se prorroga solo, con qué tasa y si capitaliza los intereses al prorrogar.");
            if (!Enum.IsDefined(b.RolloverRateRule.Value)) return Captacion("rolloverRateRule", "Regla de tasa de la prórroga desconocida.");
            if (b.MaxRollovers is < 1) return Captacion("maxRollovers", "El máximo de prórrogas es al menos una (o vacío: sin máximo).");
            if (b.GraceBusinessDays is not >= 0 || b.GraceAccrues is null)
                return Incompleta("graceBusinessDays", "El CDAT dice sus días hábiles de gracia y si causan.");
            if (b.AfterMaturityTreatment is not { } despues) return Incompleta("afterMaturityTreatment", "El CDAT dice qué pasa si no se prorroga.");
            if (!Enum.IsDefined(despues)) return Captacion("afterMaturityTreatment", "Tratamiento al vencimiento desconocido.");
            if (despues == Enums.Lending.AfterMaturityTreatment.MoveToDemand && b.MoveToDemandLineId is null)
                return Incompleta("moveToDemandLineId", "Pasar a la vista al vencer necesita la línea a la vista destino.");
            if (despues != Enums.Lending.AfterMaturityTreatment.MoveToDemand && b.MoveToDemandLineId is not null)
                return Captacion("moveToDemandLineId", "La línea a la vista destino sólo va si el CDAT pasa a la vista al vencer.");
            if (b.EarlyRedemptionAllowed is not { } redime) return Incompleta("earlyRedemption", "El CDAT dice si admite redención anticipada.");
            if (redime)
            {
                switch (b.EarlyRedemptionPenaltyMode)
                {
                    case Enums.Lending.EarlyRedemptionPenaltyMode.PenaltyRate when b.PenaltyAnnualRate is null:
                        return Incompleta("penaltyAnnualRate", "La redención anticipada a tasa de penalidad dice la tasa.");
                    case Enums.Lending.EarlyRedemptionPenaltyMode.Discount when b.PenaltyDiscountFraction is not { } descuento || descuento <= 0m || descuento > 1m
                            || !EsFraccion(descuento):
                        return Incompleta("penaltyDiscountFraction", "La redención anticipada con descuento dice qué fracción del interés causado pierde (de 0 a 1).");
                    case null:
                        return Incompleta("earlyRedemptionPenaltyMode", "La redención anticipada dice su penalidad: tasa de penalidad o descuento.");
                    case { } modo when !Enum.IsDefined(modo):
                        return Captacion("earlyRedemptionPenaltyMode", "Penalidad de redención desconocida.");
                }
                if (b.MinDaysBeforeRedemption is < 0) return Captacion("minDaysBeforeRedemption", "Los días mínimos antes de redimir no pueden ser negativos.");
            }
            else if (b.EarlyRedemptionPenaltyMode is not null || b.PenaltyAnnualRate is not null || b.PenaltyDiscountFraction is not null
                     || b.MinDaysBeforeRedemption is not null)
            {
                return Captacion("earlyRedemption", "Sin redención anticipada no hay penalidad ni días mínimos.");
            }
        }

        if (esContractual)
        {
            if (b.InstallmentPeriodicity is not { } periodicidad || b.MinInstallmentAmount is not { } cuota)
                return Incompleta("installmentPeriodicity", "El ahorro contractual dice la periodicidad y la cuota mínima.");
            if (!Enum.IsDefined(periodicidad) || periodicidad == RepaymentPeriodicity.SinglePayment)
                return Captacion("installmentPeriodicity", "La cuota del contractual es periódica.");
            if (cuota <= 0m || !EsMonto(cuota)) return Captacion("minInstallmentAmount", "La cuota mínima es un valor en pesos mayor que cero.");
            if (b.MinTermMonths is not >= 1 || b.MaxTermMonths is not { } maximo)
                return Incompleta("minTermMonths", "El ahorro contractual dice su plazo mínimo y máximo en meses.");
            if (b.MinTermMonths > maximo) return Captacion("minTermMonths", "El plazo mínimo no supera el máximo.");
            if (b.BreachEffect is not { } efecto) return Incompleta("breachEffect", "El ahorro contractual dice qué pasa al incumplirlo.");
            if (!Enum.IsDefined(efecto)) return Captacion("breachEffect", "Efecto del incumplimiento desconocido.");
            if (efecto == ContractualBreachEffect.BreachRate && b.BreachAnnualRate is null)
                return Incompleta("breachAnnualRate", "El incumplimiento a tasa castigo dice la tasa.");
            if (b.ToleratedLateInstallments is < 0) return Captacion("toleratedLateInstallments", "La tolerancia de atrasos no puede ser negativa.");
            if (b.BonusRecognition is { } reconocimiento && !Enum.IsDefined(reconocimiento))
                return Captacion("bonusRecognition", "Reconocimiento de la bonificación desconocido.");
            // E248: la línea dice a dónde puede ir lo ahorrado al vencer (traslado a la vista, giro, ambos o ninguno: a disposición).
            if (b.MaturityDestinations is not { } destinos)
                return Incompleta("maturityDestinations", "El ahorro contractual dice a dónde puede ir lo ahorrado al vencer.");
            if ((destinos & ~ContractualMaturityDestinations.All) != 0)
                return Captacion("maturityDestinations", "Destino al vencimiento desconocido.");
        }
        return null;
    }

    private static bool TieneAlgoDeCdat(BorradorDeVersionDeCaptacion b) =>
        b.MinTermDays is not null || b.MaxTermDays is not null || b.InterestPeriodicities.Count > 0 || b.InterestTimings.Count > 0
        || b.AutoRollover is not null || b.MaxRollovers is not null || b.RolloverRateRule is not null || b.CapitalizesOnRollover is not null
        || b.GraceBusinessDays is not null || b.GraceAccrues is not null || b.AfterMaturityTreatment is not null || b.MoveToDemandLineId is not null
        || b.EarlyRedemptionAllowed is not null || b.EarlyRedemptionPenaltyMode is not null || b.PenaltyAnnualRate is not null
        || b.PenaltyDiscountFraction is not null || b.MinDaysBeforeRedemption is not null;

    private static bool TieneAlgoDeContractual(BorradorDeVersionDeCaptacion b) =>
        b.InstallmentPeriodicity is not null || b.MinInstallmentAmount is not null || b.MinTermMonths is not null || b.MaxTermMonths is not null
        || b.BonusAnnualRate is not null || b.BreachAnnualRate is not null || b.ToleratedLateInstallments is not null || b.BreachEffect is not null
        || b.BonusRecognition is not null || b.MaturityDestinations is not null;

    private static ProblemaDeCartera? ValidarValor(ContributionAmountMode modo, decimal? monto, decimal? fraccion, string campo, string que, bool admiteCero)
    {
        switch (modo)
        {
            case ContributionAmountMode.FixedAmount:
                if (monto is not { } m || (admiteCero ? m < 0m : m <= 0m) || !EsMonto(m))
                    return Aportes(campo + "Amount", $"En valor fijo, {que} dice su valor en pesos.");
                if (fraccion is not null) return Aportes(campo + "Fraction", $"En valor fijo, {que} no lleva fracción.");
                return null;
            case ContributionAmountMode.IncomeFraction:
                if (fraccion is not { } f || f <= 0m || f > 1m || !EsFraccion(f))
                    return Aportes(campo + "Fraction", $"Como porcentaje del ingreso, {que} es una fracción mayor que 0 y hasta 1.");
                if (monto is not null) return Aportes(campo + "Amount", $"Como porcentaje del ingreso, {que} no lleva valor fijo.");
                return null;
            case ContributionAmountMode.MinimumWageFraction:
                if (fraccion is not { } s || s <= 0m || !EsFraccion(s))
                    return Aportes(campo + "Fraction", $"En salarios mínimos, {que} dice cuántos (una fracción mayor que cero).");
                if (monto is not null) return Aportes(campo + "Amount", $"En salarios mínimos, {que} no lleva valor fijo.");
                return null;
            default:
                return Aportes(campo + "Mode", $"Modo de {que} desconocido.");
        }
    }

    private static IEnumerable<(string Campo, decimal? Tasa)> TasasPactadas(BorradorDeVersionDeCaptacion b)
    {
        yield return ("annualRate", b.AnnualRate);
        yield return ("margin", b.Margin);
        yield return ("rateFloor", b.RateFloor);
        yield return ("rateCeiling", b.RateCeiling);
        yield return ("penaltyAnnualRate", b.PenaltyAnnualRate);
        yield return ("bonusAnnualRate", b.BonusAnnualRate);
        yield return ("breachAnnualRate", b.BreachAnnualRate);
        foreach (var t in b.RateTiers)
        {
            yield return ($"rateTiers[{t.Order}].annualRate", t.AnnualRate);
            yield return ($"rateTiers[{t.Order}].margin", t.Margin);
        }
    }

    private static IEnumerable<(string Campo, decimal Monto)> Minimos(BorradorDeVersionDeCaptacion b)
    {
        yield return ("minOpeningAmount", b.MinOpeningAmount);
        yield return ("minBalance", b.MinBalance);
        yield return ("minBalanceForInterest", b.MinBalanceForInterest);
        foreach (var t in b.RateTiers)
        {
            if (t.MinBalance is { } a) yield return ("rateTiers", a);
            if (t.MaxBalance is { } c) yield return ("rateTiers", c);
            if (t.MinAmount is { } d) yield return ("rateTiers", d);
            if (t.MaxAmount is { } e) yield return ("rateTiers", e);
        }
    }

    private static IEnumerable<(string Campo, decimal? Monto)> Limites(BorradorDeVersionDeCaptacion b)
    {
        yield return ("maxWithdrawalPerOperation", b.MaxWithdrawalPerOperation);
        yield return ("maxWithdrawalPerDay", b.MaxWithdrawalPerDay);
        yield return ("maxCashPerOperation", b.MaxCashPerOperation);
        yield return ("maxCashPerDay", b.MaxCashPerDay);
    }

    private static bool EsMonto(decimal valor) => decimal.Round(valor, 2) == valor;

    private static bool EsFraccion(decimal valor) => decimal.Round(valor, EquivalenciaDeTasas.DecimalesDeTasaPactada) == valor;

    private static ProblemaDeCartera Captacion(string campo, string mensaje) => ProblemaDeCartera.Con(CodigoCaptacionInvalida, mensaje, "field", campo);

    private static ProblemaDeCartera Aportes(string campo, string mensaje) => ProblemaDeCartera.Con(CodigoAportesInvalida, mensaje, "field", campo);

    private static ProblemaDeCartera Incompleta(string campo, string mensaje) => ProblemaDeCartera.Con(CodigoLineaIncompleta, mensaje, "field", campo);

    /// <summary>El nombre de una clase de captación para los mensajes.</summary>
    public static string NombreDeClase(DepositClass clase) => clase switch
    {
        DepositClass.Demand => "ahorro a la vista",
        DepositClass.Contractual => "ahorro contractual",
        DepositClass.Permanent => "ahorro permanente",
        DepositClass.TermDeposit => "CDAT",
        _ => clase.ToString(),
    };

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Porcentaje(decimal fraccion) => (fraccion * 100m).ToString("0.##", CultureInfo.GetCultureInfo("es-CO")) + " %";
}
