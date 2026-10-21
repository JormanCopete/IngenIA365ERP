using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// Un cargo del catálogo de la versión de una línea de captación tal como lo usa el motor (data-model §2.3; FR-128, D35): el código estable, la clase, qué
/// lo dispara, los gratuitos del período, el valor fijo o porcentual con mínimo y máximo, las exoneraciones combinables (cualquiera exonera), qué pasa si
/// el saldo no alcanza y si está gravado con IVA. (nuevo)
/// </summary>
public sealed record DefinicionDeCargo(
    string Code,
    ChargeKind Kind,
    ChargeTrigger Trigger,
    ChargeCalculation CalculationMode,
    decimal? FixedAmount = null,
    decimal? Percentage = null,
    ChargeBase? PercentageBase = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    int? FreeOperationsPerPeriod = null,
    decimal? ExemptIfAverageBalanceAtLeast = null,
    decimal? ExemptIfMinimumBalanceAtLeast = null,
    int? ExemptIfSeniorityMonthsAtLeast = null,
    bool ExemptEmployees = false,
    bool ExemptGmfExemptAccount = false,
    bool ExemptMinors = false,
    IReadOnlyCollection<Guid>? ExemptBranchPublicIds = null,
    ChargeShortfallPolicy ShortfallPolicy = ChargeShortfallPolicy.KeepPending,
    bool IsVatTaxed = false);

/// <summary>
/// Lo que el motor necesita saber de la cuenta y de la operación para causar un cargo (FR-128): la base de un cargo porcentual (el valor de la operación
/// o el saldo promedio del período), los saldos promedio y mínimo del período para las exoneraciones, la antigüedad del titular como asociado (meses;
/// nula si no es asociado), si es empleado, si la cuenta es la marcada exenta del GMF, si el titular es menor, la oficina de la cuenta, si tiene una
/// exoneración individual vigente de ese código, cuántas operaciones del disparador lleva en el período <b>contando ésta</b> (los gratuitos son por
/// cuenta, D35) y lo que el saldo deja cobrar sin quedar bajo el mínimo (FR-128: nunca bajo el mínimo). (nuevo)
/// </summary>
public sealed record ContextoDeCargo(
    decimal MontoDeLaOperacion,
    decimal SaldoPromedio,
    decimal SaldoMinimo,
    int? MesesDeAntiguedad,
    bool TitularEsEmpleado,
    bool CuentaExentaDeGmf,
    bool TitularMenor,
    Guid OficinaPublicId,
    bool ExoneracionIndividual,
    int OperacionesEnElPeriodo,
    decimal CobrableSinBajarDelMinimo);

/// <summary>Cómo queda un cargo: no se causa, se cobra ya o queda pendiente para la siguiente consignación. (nuevo)</summary>
public enum ResultadoDeCargo
{
    NoSeCausa = 0,
    Cobrado = 1,
    Pendiente = 2,
}

/// <summary>
/// El cargo calculado (FR-128): si se causa y cómo, la base, el valor, por qué no se causó (gratuito o la exoneración que aplicó, o la condonación por
/// saldo insuficiente que ordena la línea) y la explicación en español. (nuevo)
/// </summary>
public sealed record CargoCalculadoDeCuenta(ResultadoDeCargo Resultado, decimal Base, decimal Valor, string? Exoneracion, string Explicacion)
{
    public bool SeCausa => Resultado != ResultadoDeCargo.NoSeCausa;
}

/// <summary>
/// El motor de los cargos de las cuentas de captación (feature 014, US4, T115; research R14; data-model §2.3, §4.8, §4.9; FR-128, D35). Puro: recibe la
/// definición del cargo y el contexto de la cuenta y devuelve si se causa, el valor y si se cobra o queda pendiente. Sin valores legales ni tarifas: el
/// valor, los gratuitos y las exoneraciones los fija la cooperativa en la línea. En orden:
/// <list type="number">
/// <item><b>Gratuitos</b>: un cargo por evento sobre los gratuitos (<c>WithdrawalOverFree</c>, <c>DepositOverFree</c>) no se causa mientras las
/// operaciones del período (contando ésta) no pasen de los gratuitos.</item>
/// <item><b>Exoneraciones</b>, la primera que aplica: individual aprobada, saldo promedio, saldo mínimo, antigüedad, titular empleado, cuenta exenta del
/// GMF, titular menor, oficina.</item>
/// <item><b>Valor</b>: fijo, o porcentaje de la base (la operación o el saldo promedio), al peso, entre el mínimo y el máximo.</item>
/// <item><b>Cobro</b>: si el saldo lo deja sin bajar del mínimo se cobra; si no, queda pendiente (<c>KeepPending</c>, se cobra con la siguiente
/// consignación) o la línea lo condona y no se causa (<c>Waive</c>).</item>
/// </list>
/// (nuevo)
/// </summary>
public static class CargosDeCuenta
{
    /// <summary>
    /// El período (desde, hasta) de una periodicidad que contiene <paramref name="fecha"/>: los de meses anclados al año calendario (el mes, el bimestre, el
    /// trimestre…); el semanal de lunes a domingo; el decadal y el quincenal, tramos del mes con el último hasta su fin. Sirve para los gratuitos del período
    /// y para los cargos periódicos (FR-128, D35).
    /// </summary>
    public static (DateOnly Desde, DateOnly Hasta) PeriodoDe(RepaymentPeriodicity periodicidad, DateOnly fecha)
    {
        var dias = (int)periodicidad;
        var mensual = (int)RepaymentPeriodicity.Monthly;
        if (dias <= 0) throw new ArgumentOutOfRangeException(nameof(periodicidad), periodicidad, "Un cargo periódico tiene periodicidad.");
        if (dias >= mensual)
        {
            var meses = dias / mensual;
            var indice = (fecha.Month - 1) / meses;
            var desde = new DateOnly(fecha.Year, indice * meses + 1, 1);
            return (desde, desde.AddMonths(meses).AddDays(-1));
        }
        if (periodicidad == RepaymentPeriodicity.Weekly)
        {
            var lunes = fecha.AddDays(-(((int)fecha.DayOfWeek + dias - 1) % dias));
            return (lunes, lunes.AddDays(dias - 1));
        }
        var tramos = mensual / dias;
        var tramo = Math.Min((fecha.Day - 1) / dias, tramos - 1);
        var inicio = new DateOnly(fecha.Year, fecha.Month, tramo * dias + 1);
        var fin = tramo == tramos - 1 ? new DateOnly(fecha.Year, fecha.Month, DateTime.DaysInMonth(fecha.Year, fecha.Month)) : inicio.AddDays(dias - 1);
        return (inicio, fin);
    }

    /// <summary>¿Lo dispara un evento sobre los gratuitos del período?</summary>
    public static bool EsSobreGratuitos(ChargeTrigger disparador) => disparador is ChargeTrigger.WithdrawalOverFree or ChargeTrigger.DepositOverFree;

    /// <summary>El cargo calculado (ver la clase).</summary>
    public static CargoCalculadoDeCuenta Calcular(DefinicionDeCargo cargo, ContextoDeCargo c)
    {
        ArgumentNullException.ThrowIfNull(cargo);
        ArgumentNullException.ThrowIfNull(c);
        CargoCalculadoDeCuenta No(string porque, string? exoneracion = null) => new(ResultadoDeCargo.NoSeCausa, 0m, 0m, exoneracion, porque);

        if (EsSobreGratuitos(cargo.Trigger) && cargo.FreeOperationsPerPeriod is { } gratuitos && c.OperacionesEnElPeriodo <= gratuitos)
            return No(string.Create(CultureInfo.InvariantCulture,
                $"Operación {c.OperacionesEnElPeriodo} de {gratuitos} gratuitas del período: el cargo {cargo.Code} no se causa."), "FreeOperation");

        if (Exoneracion(cargo, c) is { } exonerada)
            return No($"El cargo {cargo.Code} no se causa: {exonerada.Texto}.", exonerada.Codigo);

        var (base_, valor) = Valor(cargo, c);
        if (valor <= 0m) return No($"El cargo {cargo.Code} vale cero con estas condiciones.");

        if (c.CobrableSinBajarDelMinimo >= valor)
            return new CargoCalculadoDeCuenta(ResultadoDeCargo.Cobrado, base_, valor, null,
                string.Create(CultureInfo.InvariantCulture, $"Cargo {cargo.Code} de {valor:N2}: se debita de la cuenta."));
        return cargo.ShortfallPolicy == ChargeShortfallPolicy.Waive
            ? No(string.Create(CultureInfo.InvariantCulture,
                $"El saldo no alcanza para el cargo {cargo.Code} de {valor:N2} sin bajar del mínimo y la línea lo condona."), "ShortfallWaived")
            : new CargoCalculadoDeCuenta(ResultadoDeCargo.Pendiente, base_, valor, null,
                string.Create(CultureInfo.InvariantCulture,
                    $"El saldo no alcanza para el cargo {cargo.Code} de {valor:N2} sin bajar del mínimo: queda pendiente y se cobra con la siguiente consignación."));
    }

    /// <summary>La base y el valor del cargo: fijo, o el porcentaje de su base al peso, acotado por el mínimo y el máximo.</summary>
    public static (decimal Base, decimal Valor) Valor(DefinicionDeCargo cargo, ContextoDeCargo c)
    {
        ArgumentNullException.ThrowIfNull(cargo);
        ArgumentNullException.ThrowIfNull(c);
        if (cargo.CalculationMode == ChargeCalculation.FixedAmount)
            return (c.MontoDeLaOperacion, Acotar(cargo, cargo.FixedAmount ?? 0m));
        var base_ = cargo.PercentageBase == ChargeBase.AverageBalance ? c.SaldoPromedio : c.MontoDeLaOperacion;
        var valor = Math.Round(Math.Max(0m, base_) * (cargo.Percentage ?? 0m), 0, MidpointRounding.AwayFromZero);
        return (base_, Acotar(cargo, valor));
    }

    private static decimal Acotar(DefinicionDeCargo cargo, decimal valor)
    {
        if (cargo.MinAmount is { } minimo && valor < minimo) valor = minimo;
        if (cargo.MaxAmount is { } maximo && valor > maximo) valor = maximo;
        return valor;
    }

    /// <summary>La primera exoneración que aplica (código y texto), o nula.</summary>
    public static (string Codigo, string Texto)? Exoneracion(DefinicionDeCargo cargo, ContextoDeCargo c)
    {
        ArgumentNullException.ThrowIfNull(cargo);
        ArgumentNullException.ThrowIfNull(c);
        if (c.ExoneracionIndividual) return ("IndividualExemption", "la cuenta tiene una exoneración individual aprobada");
        if (cargo.ExemptIfAverageBalanceAtLeast is { } promedio && c.SaldoPromedio >= promedio)
            return ("AverageBalanceAtLeast", string.Create(CultureInfo.InvariantCulture, $"el saldo promedio ({c.SaldoPromedio:N2}) alcanza {promedio:N2}"));
        if (cargo.ExemptIfMinimumBalanceAtLeast is { } minimo && c.SaldoMinimo >= minimo)
            return ("MinimumBalanceAtLeast", string.Create(CultureInfo.InvariantCulture, $"el saldo mínimo ({c.SaldoMinimo:N2}) alcanza {minimo:N2}"));
        if (cargo.ExemptIfSeniorityMonthsAtLeast is { } meses && c.MesesDeAntiguedad is { } antiguedad && antiguedad >= meses)
            return ("SeniorityAtLeast", string.Create(CultureInfo.InvariantCulture, $"la antigüedad como asociado ({antiguedad} meses) alcanza {meses}"));
        if (cargo.ExemptEmployees && c.TitularEsEmpleado) return ("Employee", "el titular es empleado de la cooperativa");
        if (cargo.ExemptGmfExemptAccount && c.CuentaExentaDeGmf) return ("GmfExemptAccount", "la cuenta es la marcada exenta del GMF");
        if (cargo.ExemptMinors && c.TitularMenor) return ("MinorHolder", "el titular es menor de edad");
        if (cargo.ExemptBranchPublicIds is { Count: > 0 } oficinas && oficinas.Contains(c.OficinaPublicId))
            return ("Branch", "la oficina de la cuenta está exonerada");
        return null;
    }
}
