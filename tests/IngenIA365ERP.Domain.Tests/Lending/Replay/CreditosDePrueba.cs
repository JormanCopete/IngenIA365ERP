using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Replay;

/// <summary>
/// Créditos sintéticos para las propiedades del motor de Cartera (feature 013, US4, T0468): uno simple con valores fijos y uno al azar
/// con semilla fija (montos, plazos, convenciones, periodicidades, redondeos, tramos, reajustes y una usura mensual), para que una
/// falla se reproduzca. No usan valores legales: la usura y los festivos de prueba van en la entrada.
/// </summary>
internal static class CreditosDePrueba
{
    /// <summary>Un R muy posterior a todo lo registrado.</summary>
    public static readonly DateTime Siempre = new(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static DateTime Instante(DateOnly dia, int hora = 15) => dia.ToDateTime(new TimeOnly(hora, 0), DateTimeKind.Utc);

    /// <summary>Usura mensual certificada desde <paramref name="desde"/> durante <paramref name="meses"/> meses, cada mes con su «hasta».</summary>
    public static IReadOnlyList<ValorDeUsura> UsuraMensual(DateOnly desde, int meses, decimal ea, decimal paso = 0m)
    {
        var primero = new DateOnly(desde.Year, desde.Month, 1);
        return Enumerable.Range(0, meses).Select(i =>
        {
            var inicio = primero.AddMonths(i);
            return new ValorDeUsura(inicio, ea + paso * (i % 3), Instante(inicio.AddDays(-1)), i + 1, new Guid(i + 1, 0, 0, new byte[8]))
            {
                ValidTo = inicio.AddMonths(1).AddDays(-1),
            };
        }).ToList();
    }

    /// <summary>Un crédito de cuota fija mensual 30/360 de un solo desembolso.</summary>
    public static EntradaDeReconstruccion Simple(decimal monto = 3_000_000m, DateOnly? desembolso = null, int cuotas = 12, decimal ea = 0.24m,
        int reestructuraciones = 0, DateOnly? insolvencia = null)
    {
        var fecha = desembolso ?? new DateOnly(2027, 1, 15);
        var redondeo = RedondeoDeCartera.AlCentavo;
        var plan = new EntradaDePlan(monto, fecha, cuotas, RepaymentPeriodicity.Monthly, AmortizationSystem.LevelPayment, ea,
            DayCountConvention.Thirty360, RateEquivalence.Compound, redondeo);
        var condiciones = new CondicionesDeCredito(VersionesDeReglas.Vigente, ea, DefaultRateKind.RelativeToCurrent, DayCountConvention.Thirty360,
            RateEquivalence.Compound, RepaymentPeriodicity.Monthly, redondeo)
        {
            FactorDeMora = 1.5m,
            Reestructuraciones = reestructuraciones,
            InsolvenciaDesde = insolvencia,
        };
        return new EntradaDeReconstruccion(condiciones, plan,
            [new EntradaDeOperacion("D1", LoanOperationKind.Disbursement, fecha, Instante(fecha), 1) { Monto = monto }], Siempre)
        {
            Usura = UsuraMensual(fecha, cuotas + 3, 0.30m),
            UmbralesDeSuspension = [new ValorConVigencia<int>(new DateOnly(2020, 1, 1), 61)],
        };
    }

    private static readonly RepaymentPeriodicity[] Periodicidades =
        [RepaymentPeriodicity.Weekly, RepaymentPeriodicity.Biweekly, RepaymentPeriodicity.Monthly, RepaymentPeriodicity.Quarterly];

    /// <summary>
    /// Un crédito al azar: sistema, convención, periodicidad, redondeo, plazo, tasa y usura mensual que a veces topa; a veces un tramo
    /// y un reajuste con fecha valor dentro del plazo, registrados al día siguiente de su fecha valor.
    /// </summary>
    public static EntradaDeReconstruccion AlAzar(Random azar, DateOnly? desembolso = null, bool conOperaciones = true)
    {
        var fecha = desembolso ?? new DateOnly(2027, 1, 1).AddDays(azar.Next(0, 700));
        var monto = azar.Next(500_000, 40_000_000) + azar.Next(0, 100) / 100m;
        var ea = azar.Next(80_000, 300_000) / 1_000_000m;
        var convencion = (DayCountConvention)azar.Next(1, 4);
        var periodicidad = Periodicidades[azar.Next(Periodicidades.Length)];
        var cuotas = azar.Next(2, 25);
        var sistema = azar.Next(0, 3) == 0 ? AmortizationSystem.ConstantPrincipal : AmortizationSystem.LevelPayment;
        var redondeo = new RedondeoDeCartera(
            RedondeoDeCartera.UnidadesAdmitidas[azar.Next(0, 3)], (RoundingMode)azar.Next(1, 5), (RoundingMoment)azar.Next(1, 4));
        var plan = new EntradaDePlan(monto, fecha, cuotas, periodicidad, sistema, ea, convencion, RateEquivalence.Compound, redondeo);
        var condiciones = new CondicionesDeCredito(VersionesDeReglas.Vigente, ea, DefaultRateKind.RelativeToCurrent, convencion,
            RateEquivalence.Compound, periodicidad, redondeo)
        {
            FactorDeMora = 1.5m,
            Margen = 0.05m,
            Piso = 0.10m,
            Techo = 0.30m,
        };
        var operaciones = new List<EntradaDeOperacion>
        {
            new("D1", LoanOperationKind.Disbursement, fecha, Instante(fecha), 1) { Monto = monto },
        };
        var vencimientos = FechasDeVencimiento.Generar(fecha, periodicidad, cuotas);
        if (conOperaciones && azar.Next(0, 3) == 0)
        {
            var v = fecha.AddDays(azar.Next(1, Math.Max(2, vencimientos[^1].DayNumber - fecha.DayNumber)));
            if (v < vencimientos[^1])
                operaciones.Add(new EntradaDeOperacion("T2", LoanOperationKind.Disbursement, v, Instante(v.AddDays(1)), 2)
                {
                    Monto = azar.Next(100_000, 5_000_000),
                });
        }
        if (conOperaciones && azar.Next(0, 3) == 0)
        {
            var v = fecha.AddDays(azar.Next(1, Math.Max(2, vencimientos[^1].DayNumber - fecha.DayNumber)));
            operaciones.Add(new EntradaDeOperacion("R1", LoanOperationKind.RateReset, v, Instante(v.AddDays(1)), 3)
            {
                Indice = new Domain.Lending.Rates.IndiceDeReferencia(azar.Next(10_000, 200_000) / 1_000_000m, 12),
            });
        }
        return new EntradaDeReconstruccion(condiciones, plan, operaciones, Siempre)
        {
            Usura = UsuraMensual(fecha, 40, azar.Next(180_000, 320_000) / 1_000_000m, 0.01m),
            UmbralesDeSuspension = [new ValorConVigencia<int>(new DateOnly(2020, 1, 1), 61)],
            Tolerancias = [new ValorConVigencia<decimal>(new DateOnly(2020, 1, 1), 1000m)],
        };
    }
}
