using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Documents;
using IngenIA365ERP.Domain.Lending.Housing;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Tests.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Documents;

/// <summary>
/// Feature 013, US13, K5, T0964 (FR-131; US13-3; adaptadores-y-archivos.md §8.6): el certificado tributario suma por crédito los intereses
/// <b>efectivamente pagados</b> en el año —lo aplicado por el contrato (<c>PaymentAllocation</c>, <c>PaymentReallocation</c>) a los cuatro conceptos de
/// interés con fecha valor en el año, neto de los reversos conocidos a R— y el saldo de capital y de intereses al 31 de diciembre con R = la emisión;
/// separa los de vivienda; un crédito cancelado en el año aparece con saldo cero; uno desembolsado después del año o cancelado antes no aparece.
/// </summary>
public class CertificadoTributarioTests
{
    private const short Año = 2027;
    private static readonly DateOnly Cierre = new(2027, 12, 31);
    private static readonly DateTime Emision = CreditosDePrueba.Instante(new DateOnly(2028, 1, 20));

    private static EntradaDeReconstruccion Credito(DateOnly desembolso, params EntradaDeOperacion[] mas)
    {
        var e = CreditosDePrueba.Simple(desembolso: desembolso, cuotas: 24);
        return e with { Operaciones = [.. e.Operaciones, .. mas], InformacionHasta = Emision };
    }

    private static EntradaDeOperacion Pago(string id, DateOnly fecha, decimal valor, long secuencia, PaymentModality modalidad = PaymentModality.CycleInstallment) =>
        new(id, LoanOperationKind.Payment, fecha, CreditosDePrueba.Instante(fecha, 16), secuencia) { Contenido = new ContenidoDePago(modalidad, valor) };

    private static decimal InteresDe(EstadoDelCredito estado, params string[] pagos) =>
        pagos.SelectMany(p => estado.Pagos[p].Lineas)
            .Where(l => l.Concepto is LoanConcept.CurrentInterest or LoanConcept.DefaultInterest or LoanConcept.CurrentInterestInSuspense
                or LoanConcept.DefaultInterestInSuspense)
            .Sum(l => l.Monto);

    [Fact]
    public void Suma_los_intereses_pagados_con_fecha_valor_en_el_año_y_el_saldo_al_31_de_diciembre()
    {
        var entrada = Credito(new DateOnly(2027, 1, 15),
            Pago("P1", new DateOnly(2027, 2, 15), 160_000m, 10),
            Pago("P2", new DateOnly(2027, 3, 15), 160_000m, 11),
            Pago("P3", new DateOnly(2028, 1, 15), 160_000m, 12));
        var alCierre = MotorDeCartera.Reconstruir(entrada, Cierre);

        var c = CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "Libre inversión", false, alCierre)]);

        var credito = c.Creditos.Should().ContainSingle().Subject;
        credito.InteresesPagados.Should().Be(InteresDe(alCierre, "P1", "P2")).And.BeGreaterThan(0m);
        credito.SaldoDeCapital.Should().Be(alCierre.Saldo(LoanConcept.Principal));
        credito.SaldoDeIntereses.Should().Be(alCierre.Saldo(LoanConcept.CurrentInterest) + alCierre.Saldo(LoanConcept.DefaultInterest)
                                             + alCierre.Saldo(LoanConcept.CurrentInterestInSuspense) + alCierre.Saldo(LoanConcept.DefaultInterestInSuspense));
        credito.SaldoTotal.Should().Be(alCierre.ValorParaCancelar, "capital, intereses y lo demás cobrable menos el anticipo");
        c.Vivienda.Should().BeEmpty();
        c.TotalInteresesPagados.Should().Be(credito.InteresesPagados);
        c.Explicacion.Texto.Should().Contain("CR-1");
    }

    [Fact]
    public void Los_intereses_son_netos_de_los_reversos_conocidos_a_la_emision()
    {
        var reverso = new EntradaDeOperacion("R2", LoanOperationKind.PaymentReversal, new DateOnly(2027, 3, 15),
            CreditosDePrueba.Instante(new DateOnly(2028, 1, 10)), 13) { Reversa = "P2" };
        var entrada = Credito(new DateOnly(2027, 1, 15),
            Pago("P1", new DateOnly(2027, 2, 15), 160_000m, 10),
            Pago("P2", new DateOnly(2027, 3, 15), 160_000m, 11),
            reverso);

        var conocido = MotorDeCartera.Reconstruir(entrada, Cierre);
        var desconocido = MotorDeCartera.Reconstruir(entrada with { InformacionHasta = CreditosDePrueba.Instante(new DateOnly(2028, 1, 5)) }, Cierre);

        CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, conocido)]).Creditos.Single().InteresesPagados
            .Should().Be(InteresDe(conocido, "P1"), "el reverso registrado antes de la emisión quita el pago");
        CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, desconocido)]).Creditos.Single().InteresesPagados
            .Should().Be(InteresDe(desconocido, "P1", "P2"), "con R anterior al reverso el pago todavía cuenta");
    }

    [Fact]
    public void Separa_los_de_vivienda_y_ordena_por_numero()
    {
        var uno = MotorDeCartera.Reconstruir(Credito(new DateOnly(2027, 1, 15), Pago("P1", new DateOnly(2027, 2, 15), 160_000m, 10)), Cierre);
        var dos = MotorDeCartera.Reconstruir(Credito(new DateOnly(2027, 2, 1)), Cierre);

        var c = CertificadoTributario.Armar(Año,
        [
            new CreditoParaCertificar("CR-9", "Vivienda", true, dos),
            new CreditoParaCertificar("CR-3", "Consumo", false, uno),
        ]);

        c.Creditos.Select(x => x.Numero).Should().Equal("CR-3");
        c.Vivienda.Select(x => x.Numero).Should().Equal("CR-9");
        c.TotalInteresesDeVivienda.Should().Be(0m, "sin pagos no hay intereses pagados");
        c.Vivienda[0].SaldoDeCapital.Should().BeGreaterThan(0m);
    }

    /// <summary>K6b (T1046): una serie UVR diaria sintética (valores DE PRUEBA, no publicados) que crece un poco cada día de 2027.</summary>
    private static SerieUvr SerieDePrueba()
    {
        var valores = new List<ValorDeUvr>();
        var valor = 400.0000m;
        for (var d = new DateOnly(2027, 1, 1); d <= new DateOnly(2028, 1, 31); d = d.AddDays(1))
        {
            valores.Add(new ValorDeUvr(d, valor));
            valor = Math.Round(valor * 1.00015m, 4, MidpointRounding.AwayFromZero);
        }
        return new SerieUvr(valores);
    }

    [Fact]
    public void Un_credito_de_vivienda_en_UVR_va_a_la_seccion_de_vivienda_con_los_intereses_en_pesos_a_la_UVR_de_cada_pago()
    {
        var serie = SerieDePrueba();
        var entrada = Credito(new DateOnly(2027, 1, 15),
            Pago("P1", new DateOnly(2027, 2, 15), 160_000m, 10),
            Pago("P2", new DateOnly(2027, 3, 15), 160_000m, 11)) with { Uvr = serie };
        var alCierre = MotorDeCartera.Reconstruir(entrada, Cierre);
        alCierre.EnUvr.Should().BeTrue();

        var c = CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-V", "Vivienda UVR", true, alCierre)]);

        c.Creditos.Should().BeEmpty();
        var vivienda = c.Vivienda.Should().ContainSingle().Subject;
        vivienda.InteresesPagados.Should().Be(InteresDe(alCierre, "P1", "P2")).And.BeGreaterThan(0m);
        c.TotalInteresesDeVivienda.Should().Be(vivienda.InteresesPagados);
        // En pesos a la UVR de la fecha valor de cada pago, no a la del cierre.
        var pagosDelLibro = alCierre.Libro.Movimientos.Where(m => CertificadoTributario.ClasesDePago.Contains(m.Clase)
            && (CertificadoTributario.Corrientes.Contains(m.Concepto) || CertificadoTributario.DeMora.Contains(m.Concepto))).ToList();
        pagosDelLibro.Should().NotBeEmpty().And.OnlyContain(m => m.ValorUvr == serie.Requerida(m.Dia));
        (-pagosDelLibro.Sum(m => m.Monto)).Should().Be(vivienda.InteresesPagados);
        vivienda.SaldoDeCapital.Should().Be(alCierre.Saldo(LoanConcept.Principal), "el saldo al 31 de diciembre, en pesos a la UVR de ese día");
        alCierre.Uvr!.Valor.Should().Be(serie.Requerida(Cierre));
    }

    [Fact]
    public void Un_credito_cancelado_en_el_año_aparece_con_saldo_cero()
    {
        var entrada = Credito(new DateOnly(2027, 1, 15), Pago("P1", new DateOnly(2027, 6, 15), 4_000_000m, 10, PaymentModality.FullPayoff));
        var alCierre = MotorDeCartera.Reconstruir(entrada, Cierre);

        var credito = CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, alCierre)]).Creditos.Should().ContainSingle().Subject;

        credito.SaldoTotal.Should().Be(0m);
        credito.InteresesPagados.Should().Be(InteresDe(alCierre, "P1")).And.BeGreaterThan(0m);
    }

    [Fact]
    public void Uno_desembolsado_despues_del_año_o_cancelado_antes_no_aparece()
    {
        var anterior = CreditosDePrueba.Simple(desembolso: new DateOnly(2026, 1, 15), cuotas: 24);
        anterior = anterior with
        {
            Operaciones = [.. anterior.Operaciones, Pago("P1", new DateOnly(2026, 3, 15), 4_000_000m, 10, PaymentModality.FullPayoff)],
            InformacionHasta = Emision,
        };

        var c = CertificadoTributario.Armar(Año,
        [
            new CreditoParaCertificar("CR-DESPUES", "L", false, null),
            new CreditoParaCertificar("CR-ANTES", "L", false, MotorDeCartera.Reconstruir(anterior, Cierre)),
        ]);

        c.Vacio.Should().BeTrue();
    }

    [Fact]
    public void El_estado_tiene_que_ser_al_31_de_diciembre()
    {
        var estado = MotorDeCartera.Reconstruir(Credito(new DateOnly(2027, 1, 15)), new DateOnly(2027, 12, 30));

        var armar = () => CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, estado)]);

        armar.Should().Throw<ArgumentException>();
    }

    // ------------------------------------------------------------------ decisiones del dueño del 2026-10-16: anticipo y saldo --

    private static readonly LoanConcept[] Intereses =
        [LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense, LoanConcept.DefaultInterestInSuspense];

    private static decimal InteresDeAnticipos(EstadoDelCredito estado, int año) =>
        estado.AnticiposAplicados.Where(a => a.Fecha.Year == año).SelectMany(a => a.Lineas).Where(l => Intereses.Contains(l.Concepto)).Sum(l => l.Monto);

    /// <summary>Desembolsado el 20 de noviembre: un anticipo de 500.000 el 1 de diciembre cubre la cuota del 20 de diciembre y le sobra al cierre.</summary>
    private static EstadoDelCredito ConAnticipoDeDiciembre() =>
        MotorDeCartera.Reconstruir(Credito(new DateOnly(2027, 11, 20), Pago("A1", new DateOnly(2027, 12, 1), 500_000m, 10, PaymentModality.Advance)), Cierre);

    [Fact]
    public void El_interes_que_cubre_un_anticipo_ingresado_en_el_año_cuenta_como_pagado()
    {
        var alCierre = ConAnticipoDeDiciembre();
        var conAnticipo = InteresDeAnticipos(alCierre, Año);
        conAnticipo.Should().BeGreaterThan(0m, "el anticipo cubrió el interés de la cuota del 20 de diciembre");
        InteresDe(alCierre, "A1").Should().Be(0m, "el pago que dejó el anticipo no cubrió intereses: su dinero quedó como anticipo");

        var credito = CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, alCierre)]).Creditos.Single();

        credito.InteresesPagados.Should().Be(conAnticipo, "sólo una vez: por la aplicación del anticipo, no por el pago que lo dejó");
        credito.InteresesConAnticipo.Should().Be(conAnticipo);
    }

    [Fact]
    public void El_interes_que_cubre_un_anticipo_ingresado_el_año_anterior_no_cuenta_aunque_se_aplique_en_el_año()
    {
        // Desembolso del 15 de noviembre de 2026; el 20 de diciembre paga la cuota vencida y deja anticipo, que cubre cuotas de 2027.
        var entrada = Credito(new DateOnly(2026, 11, 15),
            Pago("A1", new DateOnly(2026, 12, 20), 400_000m, 10, PaymentModality.Advance),
            Pago("P2", new DateOnly(2027, 3, 15), 160_000m, 11));
        var alCierre = MotorDeCartera.Reconstruir(entrada, Cierre);
        InteresDeAnticipos(alCierre, Año).Should().BeGreaterThan(0m, "el anticipo de 2026 cubrió intereses de cuotas de 2027");

        var credito = CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, alCierre)]).Creditos.Single();

        credito.InteresesPagados.Should().Be(InteresDe(alCierre, "P2"), "sólo el pago de 2027: el anticipo ingresó en 2026");
        credito.InteresesConAnticipo.Should().Be(0m);
    }

    // ------------------------------------------------- decisiones del dueño del 2026-10-18: anticipo recibido y corrección monetaria --

    [Fact]
    public void El_anticipo_recibido_en_el_año_y_pendiente_al_cierre_va_en_su_renglon_sin_sumar_a_los_intereses()
    {
        var alCierre = ConAnticipoDeDiciembre();
        alCierre.Anticipo.Should().BeGreaterThan(0m, "le sobró anticipo al 31 de diciembre");

        var c = CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, alCierre)]);
        var credito = c.Creditos.Single();

        credito.AnticipoRecibido.Should().Be(alCierre.Anticipo, "todo el anticipo pendiente ingresó en el año: se aplicará a intereses el siguiente");
        credito.InteresesPagados.Should().Be(InteresDeAnticipos(alCierre, Año), "el renglón es informativo: no suma a los intereses pagados");
        c.TotalAnticipoRecibido.Should().Be(alCierre.Anticipo);
        c.Explicacion.Lineas.Should().Contain(l => l.Contains("anticipo recibido en 2027"));
    }

    [Fact]
    public void El_anticipo_ingresado_el_año_anterior_va_en_el_certificado_de_ese_año_y_no_en_el_siguiente()
    {
        // El 20 de diciembre de 2026 paga la cuota vencida y deja anticipo, que se aplica a cuotas de 2027.
        var entrada = Credito(new DateOnly(2026, 11, 15), Pago("A1", new DateOnly(2026, 12, 20), 400_000m, 10, PaymentModality.Advance));
        var alCierreDe2026 = MotorDeCartera.Reconstruir(entrada, new DateOnly(2026, 12, 31));
        alCierreDe2026.Anticipo.Should().BeGreaterThan(0m);

        var de2026 = CertificadoTributario.Armar(2026, [new CreditoParaCertificar("CR-1", "L", false, alCierreDe2026)]).Creditos.Single();
        var de2027 = CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, MotorDeCartera.Reconstruir(entrada, Cierre))]);

        de2026.AnticipoRecibido.Should().Be(alCierreDe2026.Anticipo, "el certificado de 2026 muestra el anticipo recibido que se aplicará en 2027");
        de2027.Todos.Sum(x => x.AnticipoRecibido).Should().Be(0m, "en 2027 no ingresó anticipo");
        de2027.Todos.Sum(x => x.InteresesConAnticipo).Should().Be(0m, "y el interés que cubrió el de 2026 no cuenta en 2027");
    }

    [Fact]
    public void La_correccion_monetaria_UVR_del_año_va_en_su_renglon_y_un_credito_en_pesos_no_la_tiene()
    {
        var serie = SerieDePrueba();
        var entrada = Credito(new DateOnly(2027, 1, 15), Pago("P1", new DateOnly(2027, 2, 15), 160_000m, 10)) with { Uvr = serie };
        var enUvr = MotorDeCartera.Reconstruir(entrada, Cierre);
        var conLibroDelAño = MotorDeCartera.Reconstruir(entrada, Cierre, new DateOnly(2027, 1, 15));
        var enPesos = MotorDeCartera.Reconstruir(Credito(new DateOnly(2027, 1, 15), Pago("P1", new DateOnly(2027, 2, 15), 160_000m, 10)), Cierre);
        var esperada = conLibroDelAño.Libro.Movimientos.Where(m => m.Dia.Year == Año && m.Clase == LoanTransactionKind.MonetaryCorrection
                                                          && m.Concepto is not (LoanConcept.Advance or LoanConcept.CreditBalance)).Sum(m => m.Monto);
        esperada.Should().BeGreaterThan(0m, "la serie de prueba sube cada día");

        var c = CertificadoTributario.Armar(Año,
            [new CreditoParaCertificar("CR-V", "Vivienda UVR", true, enUvr) { ConLibroDelAño = conLibroDelAño }, new CreditoParaCertificar("CR-P", "L", false, enPesos)]);

        c.Vivienda.Single().CorreccionMonetaria.Should().Be(esperada);
        c.Vivienda.Single().InteresesPagados.Should().Be(InteresDe(enUvr, "P1"), "la corrección no suma a los intereses pagados");
        c.Creditos.Single().CorreccionMonetaria.Should().Be(0m);
        c.TotalCorreccionMonetaria.Should().Be(esperada);
    }

    [Fact]
    public void De_una_cancelacion_que_usa_un_anticipo_del_año_anterior_se_resta_el_interes_que_cubrio_ese_anticipo()
    {
        var entrada = Credito(new DateOnly(2026, 11, 15),
            Pago("A1", new DateOnly(2026, 12, 20), 300_000m, 10, PaymentModality.Advance),
            Pago("P2", new DateOnly(2027, 1, 10), 4_000_000m, 11, PaymentModality.FullPayoff));
        var alCierre = MotorDeCartera.Reconstruir(entrada, Cierre);
        var cancelacion = alCierre.Pagos["P2"];
        var usado = cancelacion.AnticipoUsado.Sum(u => u.Monto);
        usado.Should().BeGreaterThan(0m, "la cancelación usó el anticipo de diciembre");
        // El anticipo cubre primero, en el orden de las líneas (la prelación): lo que de él fue a intereses no cuenta en 2027.
        var restante = usado;
        var excluido = 0m;
        foreach (var l in cancelacion.Lineas)
        {
            var t = Math.Min(restante, l.Monto);
            if (Intereses.Contains(l.Concepto)) excluido += t;
            restante -= t;
        }
        excluido.Should().BeGreaterThan(0m);

        var credito = CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, alCierre)]).Creditos.Single();

        credito.InteresesPagados.Should().Be(InteresDe(alCierre, "P2") - excluido);
        credito.SaldoTotal.Should().Be(0m);
    }

    [Fact]
    public void El_saldo_por_defecto_es_el_de_siempre_y_cada_cooperativa_elige_sus_conceptos()
    {
        var alCierre = ConAnticipoDeDiciembre();
        alCierre.Anticipo.Should().BeGreaterThan(0m, "le sobró anticipo al 31 de diciembre");
        var c = new CreditoParaCertificar("CR-1", "L", false, alCierre);

        var defecto = CertificadoTributario.Armar(Año, [c]).Creditos.Single();
        defecto.SaldoTotal.Should().Be(alCierre.ValorParaCancelar, "el defecto reproduce el saldo de antes del parámetro");
        CertificadoTributario.Armar(Año, [c], ConceptosDelSaldoCertificado.Todos).Creditos.Single().Should().BeEquivalentTo(defecto);

        var soloCapital = CertificadoTributario.Armar(Año, [c], ConceptosDelSaldoCertificado.Capital).Creditos.Single();
        soloCapital.SaldoTotal.Should().Be(alCierre.Saldo(LoanConcept.Principal));
        soloCapital.SaldoDeIntereses.Should().Be(0m);
        soloCapital.SaldoDeOtros.Should().Be(0m);
        soloCapital.InteresesPagados.Should().Be(defecto.InteresesPagados, "el parámetro sólo cambia el saldo");

        var sinRestarAnticipo = CertificadoTributario.Armar(Año, [c], ConceptosDelSaldoCertificado.Todos & ~ConceptosDelSaldoCertificado.MenosAnticipo)
            .Creditos.Single();
        sinRestarAnticipo.SaldoTotal.Should().Be(alCierre.ValorParaCancelar + alCierre.Anticipo);

        var sinMora = CertificadoTributario.Armar(Año, [c], ConceptosDelSaldoCertificado.Capital | ConceptosDelSaldoCertificado.InteresCorriente)
            .Creditos.Single();
        sinMora.SaldoDeIntereses.Should().Be(alCierre.Saldo(LoanConcept.CurrentInterest) + alCierre.Saldo(LoanConcept.CurrentInterestInSuspense));
    }

    [Theory]
    [InlineData("Capital,InteresCorriente,InteresDeMora,OtrosCargos,MenosAnticipo", ConceptosDelSaldoCertificado.Todos)]
    [InlineData("Capital", ConceptosDelSaldoCertificado.Capital)]
    [InlineData("interesdemora, Capital", ConceptosDelSaldoCertificado.Capital | ConceptosDelSaldoCertificado.InteresDeMora)]
    [InlineData("", null)]
    [InlineData("Capital,Seguros", null)]
    public void El_texto_del_parametro_se_lee_por_nombre(string texto, ConceptosDelSaldoCertificado? esperado)
    {
        SaldoDelCertificadoTributario.Leer(texto).Should().Be(esperado);
        SaldoDelCertificadoTributario.Defecto.Should().MatchRegex(SaldoDelCertificadoTributario.Patron);
        SaldoDelCertificadoTributario.Leer(SaldoDelCertificadoTributario.Defecto).Should().Be(ConceptosDelSaldoCertificado.Todos);
    }
}
