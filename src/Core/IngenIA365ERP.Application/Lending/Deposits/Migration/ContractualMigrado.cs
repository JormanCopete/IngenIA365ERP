using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration;

/// <summary>
/// El ahorro contractual de una carga de captaciones (feature 014, US7, T182; pendiente de la fase 10; plantillas §4.1; research §19): el contrato se rehace desde
/// la fila —el plazo son los meses enteros entre la apertura y el vencimiento, el inicio es el vencimiento menos ese plazo, las cuotas vencen cada período de la
/// periodicidad de la línea desde el inicio—, con la tasa base pactada o, sin ella, la de la línea al corte, y la bonificada pactada o la de la línea. (nuevo)
/// </summary>
public static class ContractualMigrado
{
    /// <summary>Los meses enteros del contrato: de la apertura al vencimiento (al menos uno).</summary>
    public static int Meses(DateOnly apertura, DateOnly vencimiento) =>
        Math.Max(1, (vencimiento.Year - apertura.Year) * 12 + vencimiento.Month - apertura.Month);

    /// <summary>El plazo de emisión de una fila contractual (nulo en las demás clases).</summary>
    public static IssueTermBand? PlazoDeEmision(MigrationAccountRow f) =>
        f.DepositClass == DepositClass.Contractual && f.ContractualMaturityDate is { } vence ? AhorroContractual.PlazoDeEmision(Meses(f.OpenedOn, vence)) : null;

    /// <summary>Las condiciones del contrato de la fila con la línea y la tasa base dadas.</summary>
    public static CondicionesDeContractual Condiciones(MigrationAccountRow f, DepositLineVersion linea, decimal tasaBase)
    {
        var vence = f.ContractualMaturityDate ?? f.OpenedOn.AddMonths(1);
        var meses = Meses(f.OpenedOn, vence);
        return new CondicionesDeContractual(f.ContractualInstallment ?? 0m, linea.InstallmentPeriodicity ?? RepaymentPeriodicity.Monthly, meses, vence.AddMonths(-meses),
            tasaBase, f.ContractualBonusAnnualRate ?? linea.BonusAnnualRate, linea.BreachAnnualRate, linea.DayCount, linea.Redondeo());
    }

    /// <summary>
    /// El plan del contrato de la fila: la tasa base pactada o la de la línea para lo pactado (la variable del día del corte, el tramo por saldo o la de la
    /// versión; sin lector, la de la versión).
    /// </summary>
    public static async Task<PlanDeContractualCalculado> PlanAsync(MigrationAccountRow f, DepositLineVersion linea, Contractual.LectorDeContractual? lector,
        DateOnly corte, CancellationToken ct)
    {
        var tasa = f.AgreedAnnualRate;
        if (tasa is null)
        {
            var borrador = Condiciones(f, linea, 0m);
            var pactado = borrador.Cuota * AhorroContractual.Cuotas(borrador.Inicio, borrador.Periodicidad, AhorroContractual.Vencimiento(borrador.Inicio, borrador.PlazoMeses)).Count;
            var leida = lector is null ? null : await lector.TasaBaseAsync(linea, pactado, corte, ct);
            tasa = leida is { IsSuccess: true } ? leida.Value : linea.AnnualRate ?? 0m;
        }
        return AhorroContractual.Plan(Condiciones(f, linea, tasa.Value));
    }

    /// <summary>El plan de la cuenta contractual de la carga con su número (para cotejar sus cuotas atrasadas), con la tasa pactada o cero (las fechas no dependen de la tasa).</summary>
    public static PlanDeContractualCalculado? Plan(FilasDeLaCarga filas, string numero, RepaymentPeriodicity periodicidad)
    {
        var fila = filas.CuentasDeAhorro.Select(x => x.Fila)
            .FirstOrDefault(r => string.Equals(r.SourceAccountNumber, numero, StringComparison.Ordinal) && r.DepositClass == DepositClass.Contractual);
        if (fila?.ContractualMaturityDate is not { } vence || fila.ContractualInstallment is not > 0m) return null;
        var meses = Meses(fila.OpenedOn, vence);
        var inicio = vence.AddMonths(-meses);
        return new PlanDeContractualCalculado(
            new CondicionesDeContractual(fila.ContractualInstallment.Value, RepaymentPeriodicity.Monthly, meses, inicio, 0m, null, null, DayCountConvention.Actual365,
                new Domain.Lending.RedondeoDeCartera(1m, RoundingMode.AwayFromZero, RoundingMoment.PerTotal)),
            vence, AhorroContractual.Cuotas(inicio, periodicidad, vence), AhorroContractual.PlazoDeEmision(meses), 0m, 0m, null, null);
    }
}
