using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Reversos;
using IngenIA365ERP.Domain.Tests.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Feature 013, US3, T0416 (nombre fijo, DT §2.25; T33, C49, C101): la propuesta de espejos del reverso de un desembolso. Con un solo tramo
/// vivo se espeja cada fila propia (el capital del tramo) y cada fila de la causación del crédito; con varios, sólo el capital del tramo y la
/// diferencia de causación se recalcula desde la fecha valor del tramo; un espejo nunca se espeja y lo ya espejado no se vuelve a espejar.
/// Hoy el libro lo arma <c>ReversoDeDesembolso</c> desde <c>LND_LoanTransactions</c>; cuando exista el <c>LibroTeorico</c> de
/// <c>ReconstruccionDeCredito</c> (fase 8) esta misma propuesta corre sobre él, y US5 suma aquí los reversos de pago.
///
/// <para>US5 (T0515): el reverso de un pago espeja cada fila propia del pago y nada más; la redistribución de los pagos posteriores y la mora
/// recalculada son diferencias por familia contra la reconstrucción sin el pago —<c>PaymentReallocation</c> las de los hechos,
/// <c>AccrualAdjustment</c> las de la causación— y libro + espejos + diferencias = la reconstrucción sin el pago.</para>
/// </summary>
public sealed class ElReversoEnlazaCadaFilaConSuOriginalTests
{
    private static readonly DateOnly Valor = new(2026, 12, 1);
    private const long Tramo1 = 100;
    private const long Tramo2 = 200;
    private const int Corrida = 7;

    private static FilaDelLibro Capital(long id, long operacion, decimal importe) =>
        new(id, LoanTransactionKind.Disbursement, LoanConcept.Principal, null, importe, Valor, operacion, null, null);

    private static FilaDelLibro Causacion(long id, decimal importe, int cuota = 1, LoanTransactionKind clase = LoanTransactionKind.Accrual) =>
        new(id, clase, LoanConcept.CurrentInterest, cuota, importe, Valor.AddDays((int)id), null, Corrida, null);

    [Fact]
    public void Con_un_solo_tramo_se_espeja_el_capital_y_toda_la_causacion_del_credito()
    {
        List<FilaDelLibro> libro =
        [
            Capital(1, Tramo1, 10_000_000m),
            Causacion(2, 5_432.10m),
            Causacion(3, 5_432.10m),
            Causacion(4, -12.34m, clase: LoanTransactionKind.AccrualAdjustment),
        ];

        var p = EspejosDelReverso.DelDesembolso(libro, Tramo1, Valor, tramosVivos: 1);

        p.AEspejar.Select(f => f.Id).Should().Equal(1, 2, 3, 4);
        p.ConciliarCausacionDesde.Should().BeNull("con un solo tramo la causación se espeja (C101)");
        p.PorConcepto[LoanConcept.Principal].Should().Be(10_000_000m);
        p.PorConcepto[LoanConcept.CurrentInterest].Should().Be(10_851.86m);
    }

    [Fact]
    public void Con_varios_tramos_solo_el_capital_del_tramo_y_la_causacion_se_concilia_desde_su_fecha_valor()
    {
        var segundo = Valor.AddDays(20);
        List<FilaDelLibro> libro =
        [
            Capital(1, Tramo1, 6_000_000m),
            Causacion(2, 3_000m),
            Capital(3, Tramo2, 4_000_000m) with { FechaValor = segundo },
            Causacion(4, 5_000m),
        ];

        var p = EspejosDelReverso.DelDesembolso(libro, Tramo2, segundo, tramosVivos: 2);

        p.AEspejar.Select(f => f.Id).Should().Equal(3);
        p.ConciliarCausacionDesde.Should().Be(segundo, "la diferencia de causación sale del recálculo desde la fecha valor del tramo (C49)");
    }

    [Fact]
    public void Un_espejo_no_se_espeja()
    {
        List<FilaDelLibro> libro =
        [
            Capital(1, Tramo1, 10_000_000m),
            new(2, LoanTransactionKind.Disbursement, LoanConcept.Principal, null, -10_000_000m, Valor, Tramo2, null, EspejoDe: 1),
        ];

        var reversar = () => EspejosDelReverso.DelDesembolso(libro, Tramo2, Valor, tramosVivos: 1);

        reversar.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(EspejosDelReverso.CodigoReversoDeReverso);
    }

    [Fact]
    public void Lo_ya_espejado_no_se_vuelve_a_espejar_ni_se_espejan_los_espejos_de_la_causacion()
    {
        List<FilaDelLibro> libro =
        [
            Capital(1, Tramo1, 10_000_000m),
            Causacion(2, 5_000m),
            new(3, LoanTransactionKind.Accrual, LoanConcept.CurrentInterest, 1, -5_000m, Valor.AddDays(2), 300, null, EspejoDe: 2),
            Causacion(4, 4_000m),
        ];

        var p = EspejosDelReverso.DelDesembolso(libro, Tramo1, Valor, tramosVivos: 1);

        p.AEspejar.Select(f => f.Id).Should().Equal(1, 4);
        p.AEspejar.Should().OnlyContain(f => !f.EsEspejo);
    }

    [Fact]
    public void Un_tramo_sin_filas_no_tiene_nada_que_reversar()
    {
        var reversar = () => EspejosDelReverso.DelDesembolso([Causacion(1, 10m)], Tramo1, Valor, tramosVivos: 1);

        reversar.Should().Throw<ReglaDeCarteraException>();
    }

    // ------------------------------------------------------------------------------------------------ US5 (T0515): reverso de un pago

    private const long Pago1 = 301;
    private const long Pago2 = 302;
    private const long ReversoDelPago1 = 400;

    /// <summary>
    /// El libro de un crédito con dos pagos sobre la cuota 1 vencida (a vencidas el 05 y valor libre el 20), tal como lo registraría el
    /// libro: las filas de la reconstrucción, con su Id y la operación de cada pago.
    /// </summary>
    private static (EntradaDeReconstruccion Entrada, List<FilaDelLibro> Libro, DateOnly Hasta, DateOnly FechaDelPago1) LibroConDosPagos()
    {
        var entrada = CreditosDePrueba.Simple();
        var origen = entrada.Operaciones[0].FechaValor;
        var vence = MotorDeCartera.GenerarPlan(entrada.Plan!).Cuotas[0].Vencimiento;
        var v1 = vence.AddDays(4);
        var v2 = vence.AddDays(19);
        var conPagos = entrada with
        {
            Operaciones =
            [
                .. entrada.Operaciones,
                new EntradaDeOperacion("P1", LoanOperationKind.Payment, v1, CreditosDePrueba.Instante(v1), 2)
                    { Contenido = new ContenidoDePago(PaymentModality.PastDue, 70_000m) },
                new EntradaDeOperacion("P2", LoanOperationKind.Payment, v2, CreditosDePrueba.Instante(v2), 3)
                    { Contenido = new ContenidoDePago(PaymentModality.FreeAmount, 300_000m) },
            ],
        };
        var hasta = vence.AddDays(40);
        var teorico = MotorDeCartera.Reconstruir(conPagos, hasta, origen).Libro.Movimientos;
        var operaciones = new Dictionary<string, long> { ["D1"] = Tramo1, ["P1"] = Pago1, ["P2"] = Pago2 };
        var libro = teorico.Select((m, i) => new FilaDelLibro(i + 1, m.Clase, m.Concepto, m.Cuota, m.Monto, m.Dia,
            m.OperacionId is { } op ? operaciones[op] : null, m.OperacionId is null ? Corrida : null, null)).ToList();
        return (conPagos, libro, hasta, v1);
    }

    /// <summary>
    /// Fase 9 (defecto hallado por la e2e de Nómina): un pago que Cartera devolvió entero como excedente (D5) queda con un valor tomado de cero; la
    /// reconstrucción lo deja como constancia sin tocar el crédito, en vez de distribuir cero y lanzar.
    /// </summary>
    [Fact]
    public void Un_pago_devuelto_entero_como_excedente_no_mueve_el_credito()
    {
        var (entrada, _, hasta, _) = LibroConDosPagos();
        var v = entrada.Operaciones[^1].FechaValor.AddDays(1);
        var conDevuelto = entrada with
        {
            Operaciones =
            [
                .. entrada.Operaciones,
                new EntradaDeOperacion("P3", LoanOperationKind.Payment, v, CreditosDePrueba.Instante(v), 4)
                    { Contenido = new ContenidoDePago(PaymentModality.CycleInstallment, 0m) },
            ],
        };

        var con = MotorDeCartera.Reconstruir(conDevuelto, hasta, entrada.Operaciones[0].FechaValor);
        var sin = MotorDeCartera.Reconstruir(entrada, hasta, entrada.Operaciones[0].FechaValor);

        con.Libro.Movimientos.Should().BeEquivalentTo(sin.Libro.Movimientos, o => o.WithStrictOrdering());
        con.Libro.Movimientos.Should().NotContain(m => m.OperacionId == "P3");
    }

    [Fact]
    public void El_reverso_de_un_pago_espeja_cada_fila_propia_del_pago_y_nada_mas()
    {
        var (_, libro, _, v1) = LibroConDosPagos();

        var p = EspejosDelReverso.DelPago(libro, Pago1, v1);

        p.AEspejar.Should().NotBeEmpty().And.OnlyContain(f => f.OperacionId == Pago1);
        p.AEspejar.Select(f => f.Id).Should().Equal(libro.Where(f => f.OperacionId == Pago1).Select(f => f.Id));
        p.ConciliarCausacionDesde.Should().Be(v1, "la diferencia con los pagos posteriores sale de reconstruir sin el pago desde su fecha valor");
        p.AEspejar.Should().OnlyContain(f => f.Clase == LoanTransactionKind.PaymentAllocation);
    }

    [Fact]
    public void Los_espejos_y_las_diferencias_por_familia_dejan_el_libro_como_si_el_pago_no_hubiera_existido()
    {
        var (entrada, libro, hasta, v1) = LibroConDosPagos();
        var espejos = EspejosDelReverso.DelPago(libro, Pago1, v1).AEspejar
            .Select((f, i) => f with { Id = 10_000 + i, Importe = -f.Importe, OperacionId = ReversoDelPago1, CorridaId = null, EspejoDe = f.Id }).ToList();
        var conEspejos = libro.Concat(espejos).ToList();

        var sinElPago = entrada with { Operaciones = entrada.Operaciones.Where(o => o.Id != "P1").ToList() };
        var teoricoSin = MotorDeCartera.Reconstruir(sinElPago, hasta, entrada.Operaciones[0].FechaValor).Libro;
        var diferencias = teoricoSin.DiferenciasPorFamilia(conEspejos.Select(f => new MovimientoDeLibro(f.FechaValor, f.Clase, f.Concepto, f.Cuota, f.Importe)));

        // Cada espejo apunta a su original (ReversesTransactionId) y lo anula.
        espejos.Should().OnlyContain(e => libro.Single(f => f.Id == e.EspejoDe).Importe == -e.Importe);
        // El pago posterior se redistribuye (PaymentReallocation) y la mora cambia desde la fecha valor (AccrualAdjustment).
        var clases = diferencias.Select(d => EspejosDelReverso.ClaseDeLaDiferencia(d, diferencias)).ToList();
        clases.Should().Contain(LoanTransactionKind.PaymentReallocation).And.Contain(LoanTransactionKind.AccrualAdjustment);
        diferencias.Where(d => d.DeHechos).Should().OnlyContain(d => d.Dia > v1, "lo del pago reversado ya lo quitaron sus espejos");
        diferencias.Should().OnlyContain(d => d.Dia >= v1, "antes de la fecha valor del pago nada cambia");

        // Libro + espejos + diferencias = la reconstrucción sin el pago, por (día valor, concepto, cuota).
        var ajustes = diferencias.Select(d => new MovimientoDeLibro(d.Dia, EspejosDelReverso.ClaseDeLaDiferencia(d, diferencias), d.Concepto, d.Cuota, d.Diferencia));
        var final = conEspejos.Select(f => new MovimientoDeLibro(f.FechaValor, f.Clase, f.Concepto, f.Cuota, f.Importe)).Concat(ajustes).ToList();
        teoricoSin.Diferencias(final).Should().BeEmpty();
    }

    [Fact]
    public void El_reverso_de_un_reverso_de_pago_se_rechaza()
    {
        var (_, libro, _, v1) = LibroConDosPagos();
        var espejos = EspejosDelReverso.DelPago(libro, Pago1, v1).AEspejar
            .Select((f, i) => f with { Id = 10_000 + i, Importe = -f.Importe, OperacionId = ReversoDelPago1, EspejoDe = f.Id }).ToList();

        var reversar = () => EspejosDelReverso.DelPago([.. libro, .. espejos], ReversoDelPago1, v1);

        reversar.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(EspejosDelReverso.CodigoReversoDeReverso);
        ((Action)(() => EspejosDelReverso.DelPago(libro, 999, v1))).Should().Throw<ReglaDeCarteraException>();
    }
}
