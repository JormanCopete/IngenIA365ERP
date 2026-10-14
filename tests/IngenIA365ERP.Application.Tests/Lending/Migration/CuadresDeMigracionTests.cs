using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using P = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Partes;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;

namespace IngenIA365ERP.Application.Tests.Lending.Migration;

/// <summary>
/// Feature 013, US8, T0718 (plantillas.md §5.3.2–§5.3.4, §5.3.9; FR-139): los cuadres de la migración, puros. El plan pendiente suma el capital
/// (si no, <c>Lending.Schedule.PrincipalMismatch</c>); los conceptos de <c>balances</c> cuadran con las cuotas (<c>BalanceMismatch</c>); la ecuación
/// de la venta; la mora declarada frente a la de las cuotas y el episodio vigente (avisos); fechas crecientes; interés corriente sólo en cuotas
/// vencidas o en curso. Y, por la importación y la aplicación, dónde caen esos errores. (nuevo)
/// </summary>
public class CuadresDeMigracionTests
{
    private static readonly DateOnly Corte = new(2026, 11, 30);

    private static readonly CuotaMigrada[] Plan =
    [
        new(2, 1, new DateOnly(2026, 10, 15), 300_000m, InteresCorriente: 8_000m, InteresDeMora: 1_500m, Seguro: 2_000m, Otros: 500m),
        new(3, 2, new DateOnly(2026, 11, 15), 300_000m, InteresCorriente: 7_000m, InteresDeMora: 300m),
        new(4, 3, new DateOnly(2026, 12, 15), 300_000m, InteresCorriente: 4_000m),
        new(5, 4, new DateOnly(2027, 1, 15), 100_000m),
    ];

    [Fact]
    public void El_plan_pendiente_suma_el_capital()
    {
        CuadresDeMigracion.CapitalDelPlan(Plan).Should().Be(1_000_000m);
        CuadresDeMigracion.PlanDescuadrado(Plan, 1_000_000m).Should().BeNull();
        CuadresDeMigracion.PlanDescuadrado(Plan, 1_000_100m).Should().BeEquivalentTo(new { Concepto = "Principal", Declarado = 1_000_100m, Calculado = 1_000_000m });
    }

    [Fact]
    public void Los_conceptos_de_balances_cuadran_con_las_cuotas()
    {
        SaldoMigrado[] saldos =
        [
            new(LoanConcept.Principal, 1_000_000m),
            new(LoanConcept.CurrentInterest, 15_000m), new(LoanConcept.CurrentInterestInSuspense, 4_000m),
            new(LoanConcept.DefaultInterest, 1_800m),
            new(LoanConcept.Insurance, 2_000m),
            new(LoanConcept.CollectionFee, 200m), new(LoanConcept.OtherCharge, 300m),
            new(LoanConcept.Advance, 50_000m), new(LoanConcept.CreditBalance, 7_000m),
        ];

        CuadresDeMigracion.ConceptosDescuadrados(Plan, saldos).Should().BeEmpty("corriente + suspenso, mora, seguro y cobranza + otros cuadran; anticipos y saldos a favor sólo vienen en balances");

        var malos = CuadresDeMigracion.ConceptosDescuadrados(Plan, [new(LoanConcept.CurrentInterest, 19_000m), new(LoanConcept.Insurance, 2_500m)]);
        malos.Should().BeEquivalentTo(new[]
        {
            new DescuadreDeConcepto("DefaultInterest", 0m, 1_800m),
            new DescuadreDeConcepto("Insurance", 2_500m, 2_000m),
            new DescuadreDeConcepto("OtherCharge", 0m, 500m),
        });
    }

    [Theory]
    [InlineData(900_000, 100_000, 0, 800_000, true)]
    [InlineData(900_000, 100_000, -50_000, 750_000, true)]
    [InlineData(900_000, 900_000, 0, 0, true)]
    [InlineData(900_000, 100_000, 0, 900_000, false)]
    public void La_venta_cuadra_con_su_ecuacion(int original, int cobros, int ajustes, int saldo, bool cuadra) =>
        CuadresDeMigracion.VentaCuadra(original, cobros, ajustes, saldo).Should().Be(cuadra);

    [Fact]
    public void La_mora_y_el_episodio_salen_de_las_cuotas_vencidas_e_impagas()
    {
        CuadresDeMigracion.PrimerVencimientoImpago(Plan, Corte).Should().Be(new DateOnly(2026, 10, 15));
        CuadresDeMigracion.MoraSegunCuotas(Plan, Corte).Should().Be(46);
        CuadresDeMigracion.MoraDistinta(46, Plan, Corte).Should().BeFalse();
        CuadresDeMigracion.MoraDistinta(30, Plan, Corte).Should().BeTrue("diferencia → aviso ArrearsMismatch");
        CuadresDeMigracion.EpisodioVigente(new DateOnly(2026, 10, 15), Plan, Corte).Should().BeTrue();
        CuadresDeMigracion.EpisodioVigente(new DateOnly(2026, 6, 15), Plan, Corte).Should().BeFalse("aviso EpisodeNotCurrent y la fila no se migra");
        CuadresDeMigracion.MoraSegunCuotas([new CuotaMigrada(2, 1, new DateOnly(2026, 12, 15), 1_000m)], Corte).Should().Be(0, "al día");
    }

    [Fact]
    public void Las_fechas_crecen_y_el_interes_corriente_va_solo_en_cuotas_vencidas_o_en_curso()
    {
        CuadresDeMigracion.FechasNoCrecientes(Plan).Should().BeEmpty();
        CuadresDeMigracion.InteresFueraDeLaCuotaEnCurso(Plan, Corte).Should().BeEmpty("la de diciembre es la cuota en curso");

        CuotaMigrada[] malo =
        [
            new(2, 1, new DateOnly(2026, 12, 15), 500_000m),
            new(3, 2, new DateOnly(2026, 12, 10), 300_000m),
            new(4, 3, new DateOnly(2027, 2, 15), 200_000m, InteresCorriente: 1_000m),
        ];
        CuadresDeMigracion.FechasNoCrecientes(malo).Select(c => c.Fila).Should().Equal(3);
        CuadresDeMigracion.InteresFueraDeLaCuotaEnCurso(malo, Corte).Select(c => c.Fila).Should().Equal(4);
    }

    // ------------------------------------------------------------------------------------- dónde caen los errores --

    [Fact]
    public async Task Al_importar_las_cuotas_las_fechas_que_no_crecen_y_el_interes_futuro_son_error_de_fila_y_columna()
    {
        using var m = await MigracionDePrueba.CrearAsync();
        m.Archivo(P.Installments,
            MigracionDePrueba.Cuota("CR-1", 1, "2026-12-15", "500000"),
            MigracionDePrueba.Cuota("CR-1", 2, "2026-12-10", "300000"),
            MigracionDePrueba.Cuota("CR-1", 3, "2027-02-15", "200000", "1000"));

        var r = await m.ImportarAsync(P.Installments);

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        MigracionDePrueba.Errores(r).Select(e => (e.Row, e.Column)).Should().BeEquivalentTo(new[] { (3, C.FechaDeVencimiento), (4, C.InteresCorriente) });
    }

    [Fact]
    public async Task Al_importar_las_ventas_la_ecuacion_y_el_resultado_son_error_de_fila_y_columna()
    {
        using var m = await MigracionDePrueba.CrearAsync();
        m.Archivo(P.Sales,
            MigracionDePrueba.Fila((C.Factura, "FE-1"), (C.FechaDeVenta, "2026-10-01"), (C.Deudor, "52000001"), (C.ValorOriginal, "900000"),
                (C.CobrosExternos, "100000"), (C.SaldoAlCorte, "900000"), (C.Resultado, "Migrated"), (C.NumeroDeCredito, "CR-9")),
            MigracionDePrueba.Fila((C.Factura, "FE-2"), (C.FechaDeVenta, "2026-10-01"), (C.Deudor, "52000001"), (C.ValorOriginal, "900000"),
                (C.CobrosExternos, "900000"), (C.SaldoAlCorte, "0"), (C.Resultado, "Migrated"), (C.NumeroDeCredito, "CR-9")),
            MigracionDePrueba.Fila((C.Factura, "FE-3"), (C.FechaDeVenta, "2026-10-01"), (C.Deudor, "52000001"), (C.ValorOriginal, "900000"),
                (C.SaldoAlCorte, "900000"), (C.Resultado, "CancelledBeforeCutoff")));

        var r = await m.ImportarAsync(P.Sales);

        var errores = MigracionDePrueba.Errores(r);
        errores.Should().Contain(e => e.Row == 2 && e.Column == C.SaldoAlCorte && e.Code == ErroresDeMigracion.CodigoSaldoDescuadrado);
        errores.Should().Contain(e => e.Row == 3 && e.Column == C.SaldoAlCorte, "una migrada exige saldo mayor que cero");
        errores.Should().Contain(e => e.Row == 4 && e.Column == C.SaldoAlCorte, "una cancelada antes del corte tiene saldo cero");
    }

    [Fact]
    public async Task Al_aplicar_el_plan_descuadrado_y_los_conceptos_son_errores_entre_partes_con_fila_y_columna_y_la_mora_distinta_un_aviso()
    {
        using var m = await MigracionDePrueba.CrearAsync();
        await m.CargarAsync(P.Loans, MigracionDePrueba.Credito("CR-1"));
        await m.CargarAsync(P.Installments, MigracionDePrueba.Cuota("CR-1", 1, "2026-10-15", "600000", "10000"), MigracionDePrueba.Cuota("CR-1", 2, "2026-12-15", "300000"));
        await m.CargarAsync(P.Balances, MigracionDePrueba.Saldo("CR-1", "Principal", "1000000"), MigracionDePrueba.Saldo("CR-1", "CurrentInterest", "9000"));
        await m.CargarAsync(P.RiskData, MigracionDePrueba.Riesgo("CR-1", dias: "10"));

        var r = await m.AplicarAsync();

        r.Error.Code.Should().Be(ErroresDeMigracion.CodigoErroresEntrePartes);
        var datos = MigracionDePrueba.Errores2(r.Error);
        datos.Should().Contain(e => e.Part == P.Installments && e.Row == 3 && e.Column == C.Capital && e.Code == ErroresDeMigracion.CodigoPlanDescuadrado);
        datos.Should().Contain(e => e.Part == P.Balances && e.Column == C.Valor && e.Code == ErroresDeMigracion.CodigoSaldoDescuadrado && e.Message.Contains("CurrentInterest"));
    }
}
