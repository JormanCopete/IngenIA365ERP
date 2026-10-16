using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Documents;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Tests.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Documents;

/// <summary>
/// Feature 013, US13, K5, T0963 (adaptadores-y-archivos.md §8.1, §8.6; FR-131): el extracto de un período sobre dos estados de la reconstrucción con la
/// misma R —a la víspera del período y a su último día, éste con el libro teórico desde el primer día—: saldo inicial por concepto, las operaciones
/// del período por fecha valor (pagos con su distribución por concepto, reversos informativos, causación agregada por mes y concepto) y saldo final;
/// saldo inicial + movimientos = saldo final al centavo en todos los conceptos; un período sin movimientos y con saldo se extracta; uno posterior a la
/// cancelación, sin movimientos ni saldo, se rechaza con <c>Lending.Document.NothingToCertify</c>.
/// </summary>
public class ExtractoDeCreditoTests
{
    private static readonly DateOnly Desembolso = new(2027, 1, 15);

    private static EntradaDeReconstruccion Credito(params EntradaDeOperacion[] mas)
    {
        var e = CreditosDePrueba.Simple(desembolso: Desembolso, cuotas: 24);
        return e with { Operaciones = [.. e.Operaciones, .. mas] };
    }

    private static EntradaDeOperacion Pago(string id, DateOnly fecha, decimal valor, long secuencia, PaymentModality modalidad = PaymentModality.CycleInstallment) =>
        new(id, LoanOperationKind.Payment, fecha, CreditosDePrueba.Instante(fecha, 16), secuencia) { Contenido = new ContenidoDePago(modalidad, valor) };

    private static ExtractoArmado Extracto(EntradaDeReconstruccion entrada, DateOnly desde, DateOnly hasta, IReadOnlyDictionary<string, OperacionDelExtracto>? ops = null,
        IReadOnlyList<ReversoDelExtracto>? reversos = null) =>
        ExtractoDeCredito.Armar(MotorDeCartera.Reconstruir(entrada, desde.AddDays(-1)), MotorDeCartera.Reconstruir(entrada, hasta, desde), desde, hasta, ops,
            reversos);

    private static void Cuadra(ExtractoArmado x)
    {
        foreach (var concepto in Enum.GetValues<LoanConcept>())
            (x.SaldoInicial[concepto] + x.Movimiento(concepto)).Should().Be(x.SaldoFinal[concepto], $"saldo inicial + movimientos = saldo final en {concepto}");
    }

    [Fact]
    public void Un_trimestre_trae_saldo_inicial_operaciones_por_fecha_valor_y_saldo_final_que_cuadran()
    {
        var entrada = Credito(
            Pago("P1", new DateOnly(2027, 2, 15), 160_000m, 10),
            Pago("P2", new DateOnly(2027, 4, 20), 200_000m, 11, PaymentModality.FreeAmount),
            Pago("P3", new DateOnly(2027, 5, 15), 160_000m, 12),
            Pago("P4", new DateOnly(2027, 6, 15), 160_000m, 13));
        var desde = new DateOnly(2027, 4, 1);
        var hasta = new DateOnly(2027, 6, 30);
        var ops = new Dictionary<string, OperacionDelExtracto>
        {
            ["P2"] = new("P2", "PAG-000002", LoanOperationKind.Payment),
            ["P3"] = new("P3", "PAG-000003", LoanOperationKind.Payment),
        };

        var x = Extracto(entrada, desde, hasta, ops);

        var antes = MotorDeCartera.Reconstruir(entrada, desde.AddDays(-1));
        var despues = MotorDeCartera.Reconstruir(entrada, hasta);
        foreach (var concepto in Enum.GetValues<LoanConcept>())
        {
            x.SaldoInicial[concepto].Should().Be(antes.Saldo(concepto), $"el saldo inicial de {concepto} es el de la víspera");
            x.SaldoFinal[concepto].Should().Be(despues.Saldo(concepto), $"el saldo final de {concepto} es el del último día");
        }
        Cuadra(x);
        x.Lineas.Select(l => l.FechaValor).Should().BeInAscendingOrder("las operaciones van por fecha valor");
        x.Lineas.Should().OnlyContain(l => l.FechaValor >= desde && l.FechaValor <= hasta);

        // Los tres pagos del período, cada uno con su distribución por concepto; el de febrero no.
        var pagos = x.Lineas.Where(l => l.Clase == ClaseDeLineaDeExtracto.Pago).ToList();
        pagos.Select(l => l.OperacionId).Should().Equal("P2", "P3", "P4");
        pagos[0].Descripcion.Should().Be("Pago PAG-000002");
        pagos[2].Descripcion.Should().Be("Pago", "una operación que no se nombró va sólo con su clase");
        foreach (var linea in pagos)
        {
            var distribucion = despues.Pagos[linea.OperacionId!];
            foreach (var concepto in new[] { LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.DefaultInterest })
                linea.De(concepto).Should().Be(-distribucion.Lineas.Where(d => d.Concepto == concepto).Sum(d => d.Monto),
                    $"el pago {linea.OperacionId} muestra lo que aplicó a {concepto}");
        }

        // La causación agregada por mes y concepto: abril, mayo y junio, al último día de cada mes.
        var causacion = x.Lineas.Where(l => l.Clase == ClaseDeLineaDeExtracto.Causacion).ToList();
        causacion.Select(l => l.FechaValor).Should().Equal(new DateOnly(2027, 4, 30), new DateOnly(2027, 5, 31), new DateOnly(2027, 6, 30));
        causacion[0].Descripcion.Should().Be("Causación de abril de 2027");
        causacion.Should().OnlyContain(l => l.De(LoanConcept.CurrentInterest) > 0m);
        x.Explicacion.Texto.Should().Contain("Extracto del 2027-04-01 al 2027-06-30");
    }

    [Fact]
    public void El_periodo_que_contiene_el_desembolso_parte_de_cero_y_lo_muestra()
    {
        var entrada = Credito();
        var desde = new DateOnly(2027, 1, 1);
        var hasta = new DateOnly(2027, 1, 31);

        var x = ExtractoDeCredito.Armar(null, MotorDeCartera.Reconstruir(entrada, hasta, desde), desde, hasta);

        x.SaldoInicial.Values.Should().OnlyContain(v => v == 0m);
        var desembolso = x.Lineas.First();
        desembolso.Clase.Should().Be(ClaseDeLineaDeExtracto.Desembolso);
        desembolso.FechaValor.Should().Be(Desembolso);
        desembolso.De(LoanConcept.Principal).Should().Be(3_000_000m);
        Cuadra(x);
    }

    [Fact]
    public void Un_periodo_sin_operaciones_y_con_saldo_se_extracta()
    {
        var entrada = Credito(Pago("P1", new DateOnly(2027, 2, 15), 160_000m, 10));
        var desde = new DateOnly(2027, 2, 16);
        var hasta = new DateOnly(2027, 3, 10);

        var x = Extracto(entrada, desde, hasta);

        x.Lineas.Should().NotContain(l => l.Clase != ClaseDeLineaDeExtracto.Causacion, "no hubo operaciones en el período");
        x.TotalFinal.Should().BeGreaterThan(0m);
        Cuadra(x);
    }

    [Fact]
    public void Un_periodo_posterior_a_la_cancelacion_sin_movimientos_se_rechaza()
    {
        var entrada = Credito(Pago("P1", new DateOnly(2027, 3, 10), 4_000_000m, 10, PaymentModality.FullPayoff));

        var armar = () => Extracto(entrada, new DateOnly(2027, 5, 1), new DateOnly(2027, 5, 31));

        armar.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be("Lending.Document.NothingToCertify");

        // El período de la cancelación sí se extracta y termina en cero.
        var conLaCancelacion = Extracto(entrada, new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 31));
        conLaCancelacion.TotalFinal.Should().Be(0m);
        conLaCancelacion.Lineas.Should().Contain(l => l.Clase == ClaseDeLineaDeExtracto.Pago && l.OperacionId == "P1");
        Cuadra(conLaCancelacion);
    }

    [Fact]
    public void Un_reverso_conocido_se_informa_con_lo_que_reverso_y_el_pago_no_mueve_saldos()
    {
        var fecha = new DateOnly(2027, 2, 15);
        var reverso = new EntradaDeOperacion("R1", LoanOperationKind.PaymentReversal, fecha, CreditosDePrueba.Instante(fecha.AddDays(3), 10), 11) { Reversa = "P1" };
        var entrada = Credito(Pago("P1", fecha, 160_000m, 10), reverso);

        var x = Extracto(entrada, new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 28),
            reversos: [new ReversoDelExtracto(fecha, "REV-000001", "PAG-000001", LoanOperationKind.Payment, 160_000m)]);

        x.Lineas.Should().NotContain(l => l.Clase == ClaseDeLineaDeExtracto.Pago, "lo reversado ya no es parte del crédito");
        var informativa = x.Lineas.Should().ContainSingle(l => l.Clase == ClaseDeLineaDeExtracto.Reverso).Subject;
        informativa.Descripcion.Should().Be("Pago PAG-000001 reversado por REV-000001");
        informativa.ValorInformativo.Should().Be(160_000m);
        informativa.PorConcepto.Should().BeEmpty();
        Cuadra(x);
    }

    [Fact]
    public void Los_estados_tienen_que_ser_a_la_vispera_y_al_ultimo_dia_con_la_misma_informacion()
    {
        var entrada = Credito();
        var desde = new DateOnly(2027, 3, 1);
        var hasta = new DateOnly(2027, 3, 31);
        var final = MotorDeCartera.Reconstruir(entrada, hasta, desde);

        var otraVispera = () => ExtractoDeCredito.Armar(MotorDeCartera.Reconstruir(entrada, desde), final, desde, hasta);
        otraVispera.Should().Throw<ArgumentException>();
        var otroFinal = () => ExtractoDeCredito.Armar(null, MotorDeCartera.Reconstruir(entrada, hasta.AddDays(-1), desde), desde, hasta);
        otroFinal.Should().Throw<ArgumentException>();
        var otraR = () => ExtractoDeCredito.Armar(MotorDeCartera.Reconstruir(entrada with { InformacionHasta = CreditosDePrueba.Instante(hasta) }, desde.AddDays(-1)),
            final, desde, hasta);
        otraR.Should().Throw<ArgumentException>();
    }
}
