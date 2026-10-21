using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Tests.Lending.Collections;

/// <summary>
/// Feature 013, K4, T0922 (FR-117, FR-119, FR-120, FR-123, US12-3, US12-5; T77): los motores puros de la cobranza, con valores hechos a mano.
/// <list type="bullet">
/// <item><see cref="EtapasDeCobranza"/>: la etapa para unos días de mora (la preventiva con días negativos) y los rangos inválidos.</item>
/// <item><see cref="EstadoDePromesa"/>: <c>Pending</c>, <c>Kept</c> con la tolerancia, <c>Broken</c> al vencer, anulada con su gestión.</item>
/// <item><see cref="CumplimientoDeAcuerdo"/>: <c>Current</c>, <c>Fulfilled</c>, <c>Broken</c>.</item>
/// <item><see cref="AvisoPrevioPorEpisodio"/>: el episodio por la primera cuota impaga, una o dos comunicaciones en días distintos, la fecha
/// más temprana de reporte, <c>SIMULADO</c> en producción, el cierre al ponerse al día y la recaída como episodio nuevo.</item>
/// </list>
/// </summary>
public class MotoresDeCobranzaTests
{
    private static readonly DefinicionDeEtapa[] Defecto =
    [
        new("PREV", 1, -5, 0, false),
        new("ADMIN", 2, 1, 30, false),
        new("PREJ", 3, 31, 90, false),
        new("JUR", 4, 91, null, true),
    ];

    // ------------------------------------------------------------------------------------------------- etapas --

    [Theory]
    [InlineData(-6, null)]
    [InlineData(-5, "PREV")]
    [InlineData(0, "PREV")]
    [InlineData(1, "ADMIN")]
    [InlineData(30, "ADMIN")]
    [InlineData(31, "PREJ")]
    [InlineData(90, "PREJ")]
    [InlineData(91, "JUR")]
    [InlineData(4000, "JUR")]
    public void La_etapa_para_unos_dias_de_mora(int dias, string? etapa)
    {
        EtapasDeCobranza.Validar(Defecto).Should().BeNull();
        EtapasDeCobranza.EtapaPara(Defecto, dias)?.Codigo.Should().Be(etapa);
        if (etapa is null) EtapasDeCobranza.EtapaPara(Defecto, dias).Should().BeNull();
    }

    [Fact]
    public void Un_credito_que_pasa_de_30_dias_cambia_de_etapa()
    {
        EtapasDeCobranza.EtapaPara(Defecto, 30)!.Codigo.Should().Be("ADMIN");
        EtapasDeCobranza.EtapaPara(Defecto, 31)!.Codigo.Should().Be("PREJ");
    }

    [Theory]
    [InlineData("Overlap", 1, 31)]   // ADMIN hasta 31 y PREJ desde 31
    [InlineData("Gap", 1, 29)]       // ADMIN hasta 29 y PREJ desde 31
    public void Rangos_cruzados_o_con_huecos_son_invalidos(string clase, int minimo, int maximo)
    {
        DefinicionDeEtapa[] etapas = [Defecto[0], new("ADMIN", 2, minimo, maximo, false), Defecto[2], Defecto[3]];

        var problema = EtapasDeCobranza.Validar(etapas);

        problema!.Codigo.Should().Be(EtapasDeCobranza.CodigoRangosInvalidos);
        problema.Datos!["kind"].Should().Be(clase);
    }

    [Fact]
    public void Una_etapa_sin_tope_que_no_es_la_ultima_un_orden_que_no_sigue_a_los_dias_o_un_codigo_repetido_son_invalidos()
    {
        Clase([Defecto[0], new("ADMIN", 2, 1, null, false), Defecto[2]]).Should().Be("OpenNotLast");
        Clase([Defecto[0], new("ADMIN", 9, 1, 30, false), Defecto[2], Defecto[3]]).Should().Be("OrderMismatch");
        Clase([Defecto[0], Defecto[1], Defecto[1] with { MinimoDias = 31, MaximoDias = 40 }]).Should().Be("DuplicateCode");
        Clase([new("ADMIN", 2, 30, 1, false)]).Should().Be("Inverted");
        Clase([]).Should().Be("Empty");

        static object? Clase(DefinicionDeEtapa[] etapas) => EtapasDeCobranza.Validar(etapas)!.Datos!["kind"];
    }

    // ----------------------------------------------------------------------------------------------- promesas --

    private static readonly DateTime Gestion = new(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc);
    private static readonly PromesaParaEstado Promesa = new(Gestion, new DateOnly(2026, 11, 13), 500_000m, 1_000m);

    [Fact]
    public void Promesa_pendiente_hasta_su_fecha_y_rota_al_dia_siguiente_sin_pago()
    {
        EstadoDePromesa.Calcular(Promesa, [], new DateOnly(2026, 11, 13)).Estado.Should().Be(PaymentPromiseStatus.Pending);
        EstadoDePromesa.Calcular(Promesa, [], new DateOnly(2026, 11, 14)).Estado.Should().Be(PaymentPromiseStatus.Broken);
    }

    [Fact]
    public void Promesa_cumplida_sola_con_el_pago_dentro_de_la_tolerancia()
    {
        // 499.000 = 500.000 − 1.000 de tolerancia: alcanza. Pagado el jueves 12 con fecha valor el 12.
        var pago = new PagoParaCobranza(new DateOnly(2026, 11, 12), 499_000m, new DateTime(2026, 11, 12, 14, 0, 0, DateTimeKind.Utc));

        var estado = EstadoDePromesa.Calcular(Promesa, [pago], new DateOnly(2026, 11, 20));

        estado.Should().Be(new EstadoDePromesaCalculado(PaymentPromiseStatus.Kept, false, 499_000m, new DateOnly(2026, 11, 12)));
    }

    [Fact]
    public void Promesa_rota_si_falta_mas_que_la_tolerancia_o_el_pago_llega_tarde_o_es_anterior()
    {
        var corto = new PagoParaCobranza(new DateOnly(2026, 11, 12), 498_999m, new DateTime(2026, 11, 12, 14, 0, 0, DateTimeKind.Utc));
        var tarde = new PagoParaCobranza(new DateOnly(2026, 11, 14), 500_000m, new DateTime(2026, 11, 14, 14, 0, 0, DateTimeKind.Utc));
        var anterior = new PagoParaCobranza(new DateOnly(2026, 11, 9), 500_000m, new DateTime(2026, 11, 9, 14, 59, 0, DateTimeKind.Utc));

        var estado = EstadoDePromesa.Calcular(Promesa, [corto, tarde, anterior], new DateOnly(2026, 11, 20));

        estado.Estado.Should().Be(PaymentPromiseStatus.Broken);
        estado.Abonado.Should().Be(498_999m, "sólo cuenta lo registrado después de la promesa con fecha valor hasta la prometida");
    }

    [Fact]
    public void Dos_pagos_parciales_cumplen_la_promesa_el_dia_del_segundo()
    {
        var p1 = new PagoParaCobranza(new DateOnly(2026, 11, 10), 200_000m, new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc));
        var p2 = new PagoParaCobranza(new DateOnly(2026, 11, 13), 300_000m, new DateTime(2026, 11, 13, 14, 0, 0, DateTimeKind.Utc));

        EstadoDePromesa.Calcular(Promesa, [p2, p1], new DateOnly(2026, 11, 13))
            .Should().Be(new EstadoDePromesaCalculado(PaymentPromiseStatus.Kept, false, 500_000m, new DateOnly(2026, 11, 13)));
    }

    [Fact]
    public void Promesa_anulada_si_su_gestion_lo_fue()
    {
        var estado = EstadoDePromesa.Calcular(Promesa with { GestionAnulada = true }, [], new DateOnly(2026, 11, 20));

        estado.Anulada.Should().BeTrue();
        estado.Estado.Should().BeNull();
    }

    // ----------------------------------------------------------------------------------------------- acuerdos --

    private static readonly CuotaDeAcuerdo[] Calendario =
    [
        new(1, new DateOnly(2026, 11, 30), 300_000m),
        new(2, new DateOnly(2026, 12, 30), 300_000m),
        new(3, new DateOnly(2027, 1, 30), 400_000m),
    ];

    private static readonly DateOnly Aprobado = new(2026, 11, 15);

    private static PagoParaCobranza Pago(int a, int m, int d, decimal monto) => new(new DateOnly(a, m, d), monto, new DateTime(a, m, d, 15, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Acuerdo_al_dia_con_la_primera_cuota_pagada()
    {
        var r = CumplimientoDeAcuerdo.Calcular(Calendario, Aprobado, [Pago(2026, 11, 28, 300_000m)], new DateOnly(2026, 12, 15));

        r.Should().Be(new ResultadoDeAcuerdo(PaymentAgreementPerformance.Current, 300_000m, 1_000_000m, 1, null, null));
    }

    [Fact]
    public void Acuerdo_incumplido_el_dia_siguiente_al_vencimiento_no_cubierto()
    {
        // La cuota 2 vence el 30/12 y al 30/12 sólo hay 300.000 + 200.000 = 500.000 de 600.000.
        var pagos = new[] { Pago(2026, 11, 28, 300_000m), Pago(2026, 12, 29, 200_000m) };

        CumplimientoDeAcuerdo.Calcular(Calendario, Aprobado, pagos, new DateOnly(2026, 12, 30)).Estado
            .Should().Be(PaymentAgreementPerformance.Current, "el día del vencimiento todavía se puede pagar");
        CumplimientoDeAcuerdo.Calcular(Calendario, Aprobado, pagos, new DateOnly(2026, 12, 31))
            .Should().Be(new ResultadoDeAcuerdo(PaymentAgreementPerformance.Broken, 500_000m, 1_000_000m, 1, 2, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Acuerdo_cumplido_con_todo_pagado_a_tiempo_y_un_pago_anterior_a_la_aprobacion_no_cuenta()
    {
        var pagos = new[] { Pago(2026, 11, 10, 50_000m), Pago(2026, 11, 30, 600_000m), Pago(2027, 1, 20, 400_000m) };

        CumplimientoDeAcuerdo.Calcular(Calendario, Aprobado, pagos, new DateOnly(2027, 2, 1))
            .Should().Be(new ResultadoDeAcuerdo(PaymentAgreementPerformance.Fulfilled, 1_000_000m, 1_000_000m, 3, null, null));
    }

    [Fact]
    public void Un_incumplimiento_se_queda_aunque_despues_se_pague_todo()
    {
        var pagos = new[] { Pago(2026, 12, 5, 1_000_000m) };

        var r = CumplimientoDeAcuerdo.Calcular(Calendario, Aprobado, pagos, new DateOnly(2027, 2, 1));

        r.Estado.Should().Be(PaymentAgreementPerformance.Broken);
        r.CuotaIncumplida.Should().Be(1);
        r.IncumplidoEl.Should().Be(new DateOnly(2026, 12, 1));
    }

    // ------------------------------------------------------------------------------------------- aviso previo --

    [Fact]
    public void El_episodio_empieza_en_el_vencimiento_de_la_primera_cuota_impaga_y_recaer_es_otro()
    {
        // Cuota vencida el 10/10 sin pagar: el 11/10 lleva 1 día; se pone al día el 20/11; la del 10/12 no se paga y el 15/12 lleva 5.
        MoraDelDia[] serie =
        [
            new(new DateOnly(2026, 10, 10), 0), new(new DateOnly(2026, 10, 11), 1), new(new DateOnly(2026, 11, 19), 40),
            new(new DateOnly(2026, 11, 20), 0), new(new DateOnly(2026, 12, 15), 5),
        ];

        var episodios = AvisoPrevioPorEpisodio.Episodios(serie);

        episodios.Should().Equal(
            new EpisodioDeMora(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 11), new DateOnly(2026, 11, 20)),
            new EpisodioDeMora(new DateOnly(2026, 12, 10), new DateOnly(2026, 12, 15), null));
        AvisoPrevioPorEpisodio.EpisodioVigente(serie)!.Inicio.Should().Be(new DateOnly(2026, 12, 10));
    }

    [Theory]
    [InlineData(100_000, 150_000, 2)]
    [InlineData(150_000, 150_000, 2)]
    [InlineData(150_001, 150_000, 1)]
    public void Bajo_el_umbral_se_exigen_dos_comunicaciones(int saldo, int umbral, int exigidas) =>
        AvisoPrevioPorEpisodio.ComunicacionesExigidas(saldo, umbral).Should().Be(exigidas);

    private static readonly EpisodioDeMora Abierto = new(new DateOnly(2026, 12, 10), new DateOnly(2026, 12, 11), null);

    [Fact]
    public void Con_una_comunicacion_exigida_la_fecha_mas_temprana_es_la_constancia_mas_los_dias_minimos()
    {
        var aviso = new AvisoParaEvaluar(Abierto.Inicio, 1, 20);

        AvisoPrevioPorEpisodio.Evaluar(aviso, [], Abierto, true).Estado.Should().Be(PriorNoticeStatus.Pending);
        var r = AvisoPrevioPorEpisodio.Evaluar(aviso, [new ConstanciaDeAviso(new DateOnly(2026, 12, 15))], Abierto, true);

        r.Estado.Should().Be(PriorNoticeStatus.Valid);
        r.FechaMasTempranaDeReporte.Should().Be(new DateOnly(2027, 1, 4), "15/12 + 20 días calendario");
        r.PermiteReportar(new DateOnly(2027, 1, 3)).Should().BeFalse();
        r.PermiteReportar(new DateOnly(2027, 1, 4)).Should().BeTrue();
    }

    [Fact]
    public void Con_dos_exigidas_cuentan_dias_distintos_y_la_fecha_sale_de_la_segunda()
    {
        var aviso = new AvisoParaEvaluar(Abierto.Inicio, 2, 20);
        var mismoDia = new[] { new ConstanciaDeAviso(new DateOnly(2026, 12, 15)), new ConstanciaDeAviso(new DateOnly(2026, 12, 15)) };

        AvisoPrevioPorEpisodio.Evaluar(aviso, mismoDia, Abierto, true).Estado.Should().Be(PriorNoticeStatus.InProgress);

        var tres = mismoDia.Append(new ConstanciaDeAviso(new DateOnly(2026, 12, 18))).Append(new ConstanciaDeAviso(new DateOnly(2026, 12, 22)));
        var r = AvisoPrevioPorEpisodio.Evaluar(aviso, tres, Abierto, true);
        r.Estado.Should().Be(PriorNoticeStatus.Valid);
        r.FechaMasTempranaDeReporte.Should().Be(new DateOnly(2027, 1, 7), "la segunda exigida es el 18/12: + 20 días");
    }

    [Fact]
    public void Simulado_en_produccion_no_es_constancia_y_fuera_de_produccion_si()
    {
        var aviso = new AvisoParaEvaluar(Abierto.Inicio, 1, 20);
        var simulada = new[] { new ConstanciaDeAviso(new DateOnly(2026, 12, 15), Simulada: true) };

        AvisoPrevioPorEpisodio.Evaluar(aviso, simulada, Abierto, enProduccion: true).Estado.Should().Be(PriorNoticeStatus.Pending);
        AvisoPrevioPorEpisodio.Evaluar(aviso, simulada, Abierto, enProduccion: false).Estado.Should().Be(PriorNoticeStatus.Valid);
    }

    [Fact]
    public void Al_ponerse_al_dia_el_episodio_se_cierra_y_la_recaida_necesita_un_aviso_nuevo()
    {
        var primero = new EpisodioDeMora(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 11), new DateOnly(2026, 11, 20));
        var avisoDelPrimero = new AvisoParaEvaluar(primero.Inicio, 1, 20);
        var constancia = new[] { new ConstanciaDeAviso(new DateOnly(2026, 10, 20)) };

        var cerrado = AvisoPrevioPorEpisodio.Evaluar(avisoDelPrimero, constancia, primero, true);
        cerrado.Estado.Should().Be(PriorNoticeStatus.Closed);
        cerrado.PermiteReportar(new DateOnly(2026, 12, 31)).Should().BeFalse();

        // US12-5: la recaída del 10/12 es otro episodio; su aviso nace sin constancias.
        AvisoPrevioPorEpisodio.Evaluar(new AvisoParaEvaluar(Abierto.Inicio, 1, 20), [], Abierto, true).Estado.Should().Be(PriorNoticeStatus.Pending);
        var cruzado = () => AvisoPrevioPorEpisodio.Evaluar(avisoDelPrimero, constancia, Abierto, true);
        cruzado.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Lo_migrado_cuenta_las_fechas_de_envio_y_el_reporte_vigente()
    {
        var conEnvios = new AvisoParaEvaluar(Abierto.Inicio, 2, 20)
        {
            MigradoPrimerEnvio = new DateOnly(2026, 12, 12), MigradoSegundoEnvio = new DateOnly(2026, 12, 14),
        };
        AvisoPrevioPorEpisodio.Evaluar(conEnvios, [], Abierto, true).FechaMasTempranaDeReporte.Should().Be(new DateOnly(2027, 1, 3));

        var reportado = new AvisoParaEvaluar(Abierto.Inicio, 2, 20) { ReportadoNegativoDesde = new DateOnly(2026, 12, 31) };
        var r = AvisoPrevioPorEpisodio.Evaluar(reportado, [], Abierto, true);
        r.Estado.Should().Be(PriorNoticeStatus.Valid);
        r.PermiteReportar(new DateOnly(2026, 12, 31)).Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------------ historial --

    [Fact]
    public void El_historial_suma_las_gestiones_directas_no_anuladas_en_orden()
    {
        var mensaje = ContactoRegistrado.DeMensaje(new DateTime(2026, 11, 10, 9, 0, 0), MessageChannel.Sms, DebtorMessagePurpose.Collection);
        GestionParaHistorial[] gestiones =
        [
            new(1, new DateTime(2026, 11, 9, 10, 0, 0), CollectionActionChannel.Phone, true),
            new(2, new DateTime(2026, 11, 9, 11, 0, 0), CollectionActionChannel.Visit, false),
            new(3, new DateTime(2026, 11, 11, 8, 0, 0), CollectionActionChannel.InPerson, true),
            new(4, new DateTime(2026, 11, 11, 9, 0, 0), CollectionActionChannel.InPerson, false, AnulaA: 3),
        ];

        var historial = HistorialDeContactos.Componer([mensaje], gestiones);

        historial.Contactos.Should().Equal(
            ContactoRegistrado.DeGestion(new DateTime(2026, 11, 9, 10, 0, 0), CollectionActionChannel.Phone, true),
            mensaje);
    }
}
