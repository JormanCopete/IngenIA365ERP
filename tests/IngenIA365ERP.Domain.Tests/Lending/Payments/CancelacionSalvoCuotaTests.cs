using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Payments;

/// <summary>
/// Feature 028 (R4, T059–T062; FR-019, D13-A): la cancelación de <b>todo menos la cuota causada</b>
/// (<see cref="PaymentModality.PayoffExceptCurrentInstallment"/>). La cuota del período en curso queda en el crédito tal como se envió —capital, interés del
/// período completo, seguro y administración— y congelada; lo demás se cancela y las cuotas siguientes salen del plan; cuando su canal la paga por su
/// valor, el crédito queda cancelado al peso. Casos dorados calculados a mano en <c>Lending/Payments/Casos/</c> (causación diaria, al inicio del ciclo,
/// período calendario, con anticipo, con seguro y administración), más el pago tardío, el reverso, la reconstrucción día a día y la vivienda.
/// </summary>
public class CancelacionSalvoCuotaTests
{
    private const string Diaria = "01-ciclo-de-la-cuota-causacion-diaria.json";
    private const string InicioDelCiclo = "02-al-inicio-del-ciclo.json";

    public static IEnumerable<object[]> Escenarios() =>
        CasoDeCancelacionSalvoCuota.Archivos().SelectMany(a =>
            CasoDeCancelacionSalvoCuota.Cargar(a).Escenarios.Select(e => new object[] { Path.GetFileName(a), e.Nombre }));

    [Fact]
    public void Estan_los_casos_dorados_de_la_cancelacion_salvo_la_cuota_causada()
    {
        CasoDeCancelacionSalvoCuota.Archivos().Select(Path.GetFileName).Should().Equal(
            Diaria, InicioDelCiclo, "03-periodo-calendario.json", "04-con-anticipo.json", "05-seguro-y-administracion.json");
        CasoDeCancelacionSalvoCuota.CargarPorNombre(Diaria).Escenarios.Select(e => e.Nombre).Should().Equal(
            "base", "pago-tardio", "reverso", "otra-cuota-pedida", "fondos-insuficientes");
    }

    /// <summary>
    /// Cada escenario: el pago se aplica (la reconstrucción con él) y se simula (la reconstrucción sin él a su fecha valor y el aplicador) con la misma
    /// distribución y la misma huella; lo que cuesta sin aplicarlo es lo que reparte; el pago posterior la cierra al peso; y los saldos a cada fecha son los
    /// calculados a mano, con el libro que los explica.
    /// </summary>
    [Theory]
    [MemberData(nameof(Escenarios))]
    public void El_escenario_coincide_con_lo_calculado_a_mano(string archivo, string escenario)
    {
        var caso = CasoDeCancelacionSalvoCuota.CargarPorNombre(archivo);
        var e = caso.Escenario(escenario);
        var op = e.Operacion(e.Pago);
        op.Modalidad.Should().Be(PaymentModality.PayoffExceptCurrentInstallment);
        using var _ = new AssertionScope($"{archivo} · {escenario}");

        var entrada = caso.Entrada(e.Operaciones, op.RegistradoEn);
        var aplicada = MotorDeCartera.Reconstruir(entrada, op.FechaValor).Pagos[op.Id];
        Comparar(aplicada, e.Esperado, "aplicada");

        var antes = MotorDeCartera.Reconstruir(entrada with { Operaciones = entrada.Operaciones.Where(o => o.Id != op.Id).ToList() }, op.FechaValor);
        var credito = caso.Condiciones.Credito(caso.Condiciones.RulesVersion);
        var simulada = MotorDeCartera.DistribuirPago(antes, op.Solicitud(), credito.Prelacion, credito.Redondeo);
        Comparar(simulada, e.Esperado, "simulada");
        var id = Guid.Parse("0b7a3c8e-1d1a-4f6e-9a01-000000002800");
        MotorDeCartera.HuellaDeSimulacion(id, credito.RulesVersion, simulada).Should()
            .Be(MotorDeCartera.HuellaDeSimulacion(id, credito.RulesVersion, aplicada), "simular = aplicar (FR-072)");

        if (e.Valor is { } v)
        {
            var valor = AplicadorDePagos.ValorParaCancelarSalvoCuota(antes, op.Cuota, op.CuotaCompleta, op.PeriodoCalendario);
            (valor.Cuota, valor.ParaCancelar, valor.QuedaEnLaCuota).Should().Be((v.Cuota, v.ParaCancelar, v.QuedaEnLaCuota), "lo que cuesta sin aplicar");
            valor.InteresFijado.Sum(l => l.Monto).Should().Be(e.Esperado.InteresFijado.Sum(l => l.Valor), "el interés del período que se causa en la cuota");
            if (v.ParaCancelar > 0m)
            {
                var justo = MotorDeCartera.DistribuirPago(antes, op.Solicitud() with { Valor = v.ParaCancelar }, credito.Prelacion, credito.Redondeo);
                justo.Excedente.Should().Be(0m, "pagar lo que cuesta no deja excedente");
                justo.Aplicado.Should().Be(v.ParaCancelar);
                justo.CuotaDejada.Should().Be(v.Cuota);
            }
        }

        if (e.Cierre is { } c)
        {
            var cierre = e.Operacion(c.Pago);
            var d = MotorDeCartera.Reconstruir(caso.Entrada(e.Operaciones, cierre.RegistradoEn), cierre.FechaValor).Pagos[cierre.Id];
            d.Lineas.Select(l => (l.Cuota, l.Concepto, l.Monto)).Should().BeEquivalentTo(c.Lineas.Select(l => (l.Cuota, l.Concepto, l.Valor)),
                "el pago de la cuota dejada cubre lo que quedó en ella");
            d.Excedente.Should().Be(c.Excedente, "la cuota dejada se cierra al peso");
            d.Anticipo.Should().Be(0m);
            d.SaldoAFavor.Should().Be(0m);
            d.Cancela.Should().Be(c.Cancela);
        }

        foreach (var x in e.Verificaciones)
        {
            var donde = $"a {x.Fecha:yyyy-MM-dd}";
            var conR = caso.Entrada(e.Operaciones, x.InformacionHasta);
            var estado = MotorDeCartera.Reconstruir(conR, x.Fecha);
            estado.Saldos.Select(s => (s.Cuota, s.Concepto, s.Valor)).Should().BeEquivalentTo(x.PorCuota.Select(s => (s.Cuota, s.Concepto, s.Valor)),
                $"{donde}: saldos por cuota y concepto");
            estado.ValorParaCancelar.Should().Be(x.ValorParaCancelar, $"{donde}: valor para cancelar");
            estado.Plan!.Cuotas.Should().HaveCount(x.CuotasDelPlan, $"{donde}: cuotas del plan");
            if (x.Reconocidos is not null)
                estado.Reconocidos.Select(r => (r.Cuota, r.Concepto, r.Desde, r.Reconocido, r.Pendiente)).Should().Equal(
                    x.Reconocidos.Select(r => (r.Cuota, r.Concepto, r.Desde, r.Reconocido, r.Pendiente)), $"{donde}: lo reconocido al inicio del ciclo");
            ElLibroExplicaLosSaldos(conR, x.Fecha, estado.Origen!.Value, donde);
        }
    }

    /// <summary>
    /// T060: congelada, la cuota dejada no causa más interés corriente hasta su vencimiento aunque su capital siga ahí: el libro no tiene ninguna causación
    /// de la corriente después de la fecha valor, la única fila de interés de ese día es el del período que faltaba, y el plan conserva la cuota tal como
    /// estaba (sin las siguientes).
    /// </summary>
    [Fact]
    public void La_cuota_dejada_no_causa_mas_interes_corriente_hasta_su_vencimiento()
    {
        var caso = CasoDeCancelacionSalvoCuota.CargarPorNombre(Diaria);
        var e = caso.Escenario("base");
        var entrada = caso.Entrada(e.Operaciones, new DateTime(2027, 5, 14, 23, 0, 0, DateTimeKind.Utc));
        var recogida = new DateOnly(2027, 4, 30);

        var estado = MotorDeCartera.Reconstruir(entrada, new DateOnly(2027, 5, 15), recogida);

        estado.Libro.Movimientos.Should().NotContain(m => m.Clase == LoanTransactionKind.Accrual && m.Concepto == LoanConcept.CurrentInterest && m.Dia > recogida,
            "la cuota 2 ya lleva el interés del período completo");
        estado.Libro.Movimientos.Where(m => m.Clase == LoanTransactionKind.UnaccruedInstallmentInterest)
            .Select(m => (m.Dia, m.Concepto, m.Cuota, m.Monto)).Should().Equal((recogida, LoanConcept.CurrentInterest, (int?)2, 6_000m));
        var pactado = GeneradorDePlan.Generar(caso.Condiciones.Entrada()).Cuotas.Single(c => c.Numero == 2);
        estado.Plan!.Cuotas.Select(c => c.Numero).Should().Equal(1, 2);
        estado.Plan.Cuotas.Single(c => c.Numero == 2).Should().BeEquivalentTo(pactado, "la cuota que se deja es la que se envió");
        estado.Saldo(LoanConcept.CurrentInterest, 2).Should().Be(pactado.Interes, "lo causado más lo que faltaba es el interés de la cuota en el plan");

        // La proyección de la cuota completa de un crédito con la cuota congelada no cobra nada más.
        var aMitad = MotorDeCartera.Reconstruir(entrada, new DateOnly(2027, 5, 10));
        aMitad.InteresPorCausarDeLaCuotaEnCurso().Should().BeEmpty();
        AplicadorDePagos.ValorParaCancelar(aMitad, cuotaCompleta: true).Should().Be(412_000m);
    }

    /// <summary>T060: vencida e impaga, la cuota dejada causa mora sobre su capital como cualquier cuota (no sobre su interés: la regla sellada no lo pide).</summary>
    [Fact]
    public void Vencida_e_impaga_la_cuota_dejada_causa_mora_como_cualquier_cuota()
    {
        var caso = CasoDeCancelacionSalvoCuota.CargarPorNombre(Diaria);
        var e = caso.Escenario("pago-tardio");
        var estado = MotorDeCartera.Reconstruir(caso.Entrada(e.Operaciones, new DateTime(2027, 5, 25, 10, 0, 0, DateTimeKind.Utc)), new DateOnly(2027, 5, 25));

        var tasa = estado.TasaDelDia!.TasaDiariaMora;
        tasa.Should().Be(0.000659415791379629m, "la mora topada en la usura de 26,5 % E.A., lineal (la misma del caso 40)");
        estado.Saldo(LoanConcept.DefaultInterest, 2).Should().Be(Math.Round(400_000m * tasa * 10, 2, MidpointRounding.AwayFromZero), "diez días sobre el capital");
        estado.Saldo(LoanConcept.CurrentInterest, 2).Should().Be(12_000m, "vencida no causa más corriente");
        estado.AlturaDeMora.Dias.Should().Be(10);
    }

    /// <summary>T060: pagada por su valor, el crédito queda cancelado al peso: sin anticipo, sin saldo a favor, sin nada que cobrar.</summary>
    [Fact]
    public void Pagada_por_su_valor_el_credito_queda_cancelado_al_peso()
    {
        var caso = CasoDeCancelacionSalvoCuota.CargarPorNombre(Diaria);
        var e = caso.Escenario("base");
        var estado = MotorDeCartera.Reconstruir(caso.Entrada(e.Operaciones, new DateTime(2027, 6, 30, 23, 0, 0, DateTimeKind.Utc)), new DateOnly(2027, 6, 30));

        estado.Pagos["P2"].Cancela.Should().BeTrue();
        estado.Pagos["P2"].Excedente.Should().Be(0m);
        estado.ValorParaCancelar.Should().Be(0m);
        estado.Anticipo.Should().Be(0m);
        estado.SaldoAFavor.Should().Be(0m);
        estado.Saldos.Should().BeEmpty("después de pagar la cuota dejada no se causa nada: las cuotas siguientes salieron del plan");
    }

    /// <summary>T060: el reverso de la recogida la saca de la reconstrucción: el plan vuelve a tres cuotas, la cuota 2 deja de estar congelada y nada se causó de más.</summary>
    [Fact]
    public void El_reverso_de_la_recogida_lo_deshace_todo()
    {
        var caso = CasoDeCancelacionSalvoCuota.CargarPorNombre(Diaria);
        var e = caso.Escenario("reverso");
        var estado = MotorDeCartera.Reconstruir(caso.Entrada(e.Operaciones, new DateTime(2027, 5, 14, 23, 0, 0, DateTimeKind.Utc)), new DateOnly(2027, 5, 14),
            new DateOnly(2027, 3, 15));

        estado.Pagos.Keys.Should().BeEquivalentTo(["P1"]);
        estado.Plan!.Cuotas.Should().HaveCount(3);
        estado.Libro.Movimientos.Should().NotContain(m => m.Clase == LoanTransactionKind.UnaccruedInstallmentInterest);
        estado.Saldo(LoanConcept.CurrentInterest, 2).Should().Be(11_600m, "29 días sobre los 800.000 de capital por vencer");

        var sinLaRecogida = caso.Entrada(e.Operaciones.Where(o => o.Id is "D1" or "P1").ToList(), new DateTime(2027, 5, 14, 23, 0, 0, DateTimeKind.Utc));
        estado.Huella.Should().Be(MotorDeCartera.Reconstruir(sinLaRecogida, new DateOnly(2027, 5, 14)).Huella, "como si la recogida no hubiera existido");
    }

    /// <summary>
    /// T060 (como <c>CausacionIdempotenteTests</c>): causar día a día desde el origen con la recogida y el pago de la cuota dejada deja en el libro lo mismo que
    /// la reconstrucción, y el libro completo cuadra con ella.
    /// </summary>
    [Theory]
    [InlineData(Diaria)]
    [InlineData(InicioDelCiclo)]
    public void Causar_dia_a_dia_con_la_cuota_dejada_deja_el_libro_de_la_reconstruccion(string archivo)
    {
        var caso = CasoDeCancelacionSalvoCuota.CargarPorNombre(archivo);
        var e = caso.Escenario("base");
        var entrada = caso.Entrada(e.Operaciones, new DateTime(2027, 7, 31, 23, 0, 0, DateTimeKind.Utc));
        var origen = new DateOnly(2027, 3, 15);
        var hasta = new DateOnly(2027, 6, 20);

        var causado = new List<MovimientoDeLibro>();
        for (var d = origen.AddDays(1); d <= hasta; d = d.AddDays(1))
        {
            var r = MotorDeCartera.Causar(entrada, d.AddDays(-1), d);
            r.Causo.Should().BeTrue($"{d:yyyy-MM-dd}: {r.Problema?.Mensaje}");
            causado.AddRange(r.Filas);
        }
        var estado = MotorDeCartera.Reconstruir(entrada, hasta, origen);
        static bool DelCiclo(MovimientoDeLibro m) =>
            m.Clase == LoanTransactionKind.Accrual || (m.Clase == LoanTransactionKind.RecognitionRelease && m.OperacionId is null);
        causado.Should().BeEquivalentTo(estado.Libro.Movimientos.Where(DelCiclo), "de a un día, lo mismo que la reconstrucción");
        causado.Should().NotContain(m => m.Concepto == LoanConcept.CurrentInterest && m.Cuota == 2 && m.Dia > new DateOnly(2027, 4, 30),
            "la cuota dejada no causa después de la recogida");
        MotorDeCartera.Verificar(entrada, hasta, [.. estado.Libro.Movimientos.Where(m => !DelCiclo(m)), .. causado])
            .Should().BeEmpty("el libro causado de a un día cuadra con la reconstrucción");
    }

    /// <summary>
    /// T061 (caso 02): lo reconocido al inicio del ciclo de la cuota 2 es su interés del período completo (lo que proyecta la causación desde el 16-04): el pago
    /// que la deja lo consume del todo con lo que le causa.
    /// </summary>
    [Fact]
    public void Lo_reconocido_de_la_cuota_dejada_es_su_interes_del_periodo()
    {
        var caso = CasoDeCancelacionSalvoCuota.CargarPorNombre(InicioDelCiclo);
        var e = caso.Escenario("base");
        var hastaP1 = caso.Entrada(e.Operaciones.Where(o => o.Id is "D1" or "P1").ToList(), new DateTime(2027, 4, 16, 5, 0, 0, DateTimeKind.Utc)) with
        {
            Reconocimientos = [],
        };
        ReconstruccionDeCredito.CuotaPorReconocer(hastaP1, 2, new DateOnly(2027, 4, 16), [LoanConcept.CurrentInterest])
            .Should().Equal((LoanConcept.CurrentInterest, 12_000m));
    }

    /// <summary>T059: la cuota causada es la del período en curso a la fecha valor; el día de un vencimiento, o antes de que empiece su tramo, no hay.</summary>
    [Fact]
    public void La_cuota_causada_es_la_del_periodo_en_curso()
    {
        var caso = CasoDeCancelacionSalvoCuota.CargarPorNombre(Diaria);
        var e = caso.Escenario("base");
        var entrada = caso.Entrada(e.Operaciones.Where(o => o.Id is "D1" or "P1").ToList(), new DateTime(2027, 5, 31, 23, 0, 0, DateTimeKind.Utc));
        int? Causada(DateOnly fecha, bool calendario) => AplicadorDePagos.CuotaCausada(MotorDeCartera.Reconstruir(entrada, fecha), fecha, calendario);

        Causada(new DateOnly(2027, 3, 15), false).Should().BeNull("el día del desembolso el período de la cuota 1 no ha empezado");
        Causada(new DateOnly(2027, 3, 16), false).Should().Be(1);
        Causada(new DateOnly(2027, 4, 15), false).Should().BeNull("el día del vencimiento de la 1 el período de la 2 no ha empezado");
        Causada(new DateOnly(2027, 4, 16), false).Should().Be(2);
        Causada(new DateOnly(2027, 4, 30), true).Should().BeNull("por período calendario el tramo de mayo no ha empezado");
        Causada(new DateOnly(2027, 5, 1), true).Should().Be(2);
    }

    /// <summary>T059: el plan que deja: las cuotas hasta la dejada, idénticas, y el monto de la entrada en Σ capital de las que quedan.</summary>
    [Fact]
    public void Cortar_el_plan_deja_las_cuotas_tal_como_estaban()
    {
        var plan = GeneradorDePlan.Generar(CasoDeCancelacionSalvoCuota.CargarPorNombre(Diaria).Condiciones.Entrada());

        var cortado = GeneradorDePlan.CortarDespuesDe(plan, 2, new DateOnly(2027, 4, 30));

        cortado.Cuotas.Should().BeEquivalentTo(plan.Cuotas.Take(2), o => o.WithStrictOrdering());
        cortado.Entrada.Monto.Should().Be(800_000m);
        cortado.TotalCapital.Should().Be(800_000m);
        cortado.PrimeraCuotaRegenerada.Should().Be(2);
        GeneradorDePlan.CortarDespuesDe(plan, 3, new DateOnly(2027, 4, 30)).Should().BeSameAs(plan, "sin cuotas después no hay nada que cortar");
    }

    /// <summary>
    /// T062: un crédito de vivienda —en UVR (caso 63) o en pesos (caso 61)— se recoge completo: la modalidad se aplica como la cancelación total, con el
    /// aviso, sin dejar ni congelar nada; simular y reconstruir dicen lo mismo y lo que cuesta es lo de la cancelación total.
    /// </summary>
    [Theory]
    [InlineData("63-vivienda-uvr-pago-y-reverso.json", "2026-12-20")]
    [InlineData("61-vivienda-pesos-tasa-fija.json", "")]
    public void Un_credito_de_vivienda_se_recoge_completo(string archivo, string fechaTexto)
    {
        var caso = CasoDoradoDeCartera.Cargar(Path.Combine(CasoDoradoDeCartera.DirectorioDeCasos, archivo));
        var c = caso.Condiciones;
        var fecha = fechaTexto.Length > 0 ? DateOnly.Parse(fechaTexto, System.Globalization.CultureInfo.InvariantCulture) : c.FechaDesembolso.AddDays(17);
        var desembolso = new List<CasoDoradoDeCartera.OperacionJson>
        {
            new() { Id = "D1", Tipo = nameof(LoanOperationKind.Disbursement), Monto = c.Monto, FechaValor = c.FechaDesembolso,
                RegistradoEn = c.FechaDesembolso.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc) },
        };
        var r = fecha.ToDateTime(new TimeOnly(16, 0), DateTimeKind.Utc);
        var estado = MotorDeCartera.Reconstruir(caso.Entrada(c, desembolso, r), fecha);
        var credito = c.Credito(c.RulesVersion);
        var valor = c.Monto * 2;

        var salvo = MotorDeCartera.DistribuirPago(estado, new SolicitudDeDistribucion(PaymentModality.PayoffExceptCurrentInstallment, valor, fecha),
            credito.Prelacion, credito.Redondeo);
        var total = MotorDeCartera.DistribuirPago(estado, new SolicitudDeDistribucion(PaymentModality.FullPayoff, valor, fecha), credito.Prelacion,
            credito.Redondeo);

        salvo.Cancela.Should().BeTrue("se recoge completo");
        salvo.CuotaDejada.Should().BeNull();
        salvo.InteresDeLaCuotaDejada.Should().BeEmpty();
        salvo.PlanNuevo.Should().BeNull();
        salvo.Avisos.Should().Contain(AplicadorDePagos.AvisoViviendaSeCancelaCompleto);
        salvo.Lineas.Should().BeEquivalentTo(total.Lineas);
        salvo.Aplicado.Should().Be(total.Aplicado);
        AplicadorDePagos.ValorParaCancelarSalvoCuota(estado, null, cuotaCompleta: false).Should().Match<ValorSalvoCuota>(x =>
            x.Cuota == null && x.QuedaEnLaCuota == 0m && x.ParaCancelar == AplicadorDePagos.ValorParaCancelar(estado, false, false));

        var conElPago = desembolso.Append(new CasoDoradoDeCartera.OperacionJson
        {
            Id = "R", Tipo = nameof(LoanOperationKind.Payment), Modalidad = PaymentModality.PayoffExceptCurrentInstallment, Valor = salvo.Aplicado,
            FechaValor = fecha, RegistradoEn = r,
        }).ToList();
        var despues = MotorDeCartera.Reconstruir(caso.Entrada(c, conElPago, r.AddDays(10)), fecha.AddDays(10));
        despues.Pagos["R"].Cancela.Should().BeTrue("la reconstrucción repite la cancelación total");
        despues.Pagos["R"].Avisos.Should().Contain(AplicadorDePagos.AvisoViviendaSeCancelaCompleto);
        despues.ValorParaCancelar.Should().Be(0m);
    }

    // ------------------------------------------------------------------------------------------------------------- ayudas --

    private static void Comparar(DistribucionDePago d, CasoDeCancelacionSalvoCuota.EsperadoJson e, string donde)
    {
        d.Modalidad.Should().Be(PaymentModality.PayoffExceptCurrentInstallment, donde);
        d.Cuota.Should().Be(e.Cuota, $"{donde}: la cuota que sella la decisión");
        d.CuotaDejada.Should().Be(e.CuotaDejada, $"{donde}: la cuota dejada");
        d.Lineas.Select(l => (l.Cuota, l.Concepto, l.Monto)).Should().BeEquivalentTo(e.Lineas.Select(l => (l.Cuota, l.Concepto, l.Valor)),
            $"{donde}: lo que cubre por cuota y concepto");
        d.InteresDeLaCuotaDejada.Select(l => (l.Cuota, l.Concepto, l.Monto)).Should().Equal(e.InteresFijado.Select(l => (l.Cuota, l.Concepto, l.Valor)),
            $"{donde}: el interés del período que se causa en la cuota dejada");
        d.InteresNoCobrado.Select(l => (l.Cuota, l.Concepto, l.Monto)).Should().Equal(e.InteresNoCobrado.Select(l => (l.Cuota, l.Concepto, l.Valor)),
            $"{donde}: lo de un tramo sin empezar que no se cobra");
        d.InteresPorCausar.Should().BeEmpty($"{donde}: la cuota dejada no cobra su cuota completa");
        d.AnticipoUsado.Select(l => (l.Cuota, l.Monto)).Should().BeEquivalentTo(e.AnticipoUsado.Select(l => (l.Cuota, l.Valor)), $"{donde}: anticipo usado");
        d.Anticipo.Should().Be(e.Anticipo, $"{donde}: anticipo que queda");
        d.CuotaDelAnticipo.Should().BeNull($"{donde}: lo que sobra queda como anticipo general");
        d.SaldoAFavor.Should().Be(0m, $"{donde}: la cancelación nunca deja saldo a favor");
        d.Excedente.Should().Be(e.Excedente, $"{donde}: excedente");
        d.Aplicado.Should().Be(e.Aplicado, $"{donde}: aplicado");
        d.Cancela.Should().Be(e.Cancela, $"{donde}: cancela");
        d.Avisos.Should().Equal(e.Avisos, $"{donde}: avisos");
        (d.Valor - d.Lineas.Sum(l => l.Monto) - d.Anticipo - d.SaldoAFavor - d.Excedente + d.AnticipoUsado.Sum(l => l.Monto))
            .Should().Be(0m, $"{donde}: lo recibido = lo distribuido + anticipo + excedente − anticipo usado");
        if (e.CuotasDelPlanNuevo is null)
            d.PlanNuevo.Should().BeNull($"{donde}: el plan no cambia");
        else
            d.PlanNuevo!.Cuotas.Select(c => c.Numero).Should().Equal(e.CuotasDelPlanNuevo, $"{donde}: las cuotas siguientes salen del plan");
    }

    /// <summary>Σ libro teórico desde el origen = saldos del estado (como los casos dorados): lo reconocido, por lo que queda por causar.</summary>
    private static void ElLibroExplicaLosSaldos(EntradaDeReconstruccion entrada, DateOnly fecha, DateOnly origen, string donde)
    {
        var completo = MotorDeCartera.Reconstruir(entrada, fecha, origen);
        foreach (var grupo in completo.Libro.Movimientos.Where(m => ConceptosReconocidos.Es(m.Concepto)).GroupBy(m => (m.Concepto, m.Cuota)))
            grupo.Sum(m => m.Monto).Should().Be(completo.Reconocidos.Where(r => r.Concepto == grupo.Key.Concepto && r.Cuota == grupo.Key.Cuota).Sum(r => r.Pendiente),
                $"{donde}: Σ libro de {grupo.Key.Concepto} en la cuota {grupo.Key.Cuota} es lo reconocido por causar");
        foreach (var grupo in completo.Libro.Movimientos.Where(m => !ConceptosReconocidos.Es(m.Concepto)).GroupBy(m => m.Concepto))
        {
            if (grupo.Any(m => m.Cuota is null))
                grupo.Sum(m => m.Monto).Should().Be(completo.Saldo(grupo.Key), $"{donde}: Σ libro de {grupo.Key}");
            else
                foreach (var cuota in grupo.GroupBy(m => m.Cuota))
                    cuota.Sum(m => m.Monto).Should().Be(completo.Saldo(grupo.Key, cuota.Key), $"{donde}: Σ libro de {grupo.Key} en la cuota {cuota.Key}");
        }
    }
}
