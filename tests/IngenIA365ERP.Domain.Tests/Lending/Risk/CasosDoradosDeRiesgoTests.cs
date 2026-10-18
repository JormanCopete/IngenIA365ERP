using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Lending.Accrual;
using IngenIA365ERP.Domain.Lending.Delinquency;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9 (T0672; quickstart §1.3; SC-021, SC-023): los motores de riesgo coinciden con los casos dorados r01–r16, calculados
/// a mano y repasados con <c>Casos/riesgo.py</c> (decimal de 40 dígitos, sin el C#). Por cada caso y variante: política efectiva
/// (<see cref="PoliticaSarcEfectiva"/>), altura de mora con el calendario hábil cuando el crédito trae cuotas
/// (<see cref="FechasDeVencimiento.FechaExigible"/> + <see cref="AlturaDeMora"/>), calificación (<see cref="MotorDeCalificacion"/>),
/// deterioro individual (<see cref="MotorDeDeterioroIndividual"/>) y general (<see cref="MotorDeDeterioroGeneral"/>), y el selector
/// (<see cref="SelectorDeModeloDeDeterioro"/>). Mientras rija D19: sin excepción de aportes en el arrastre ni descuento de aportes en
/// el deterioro. Pendientes de validar por la contadora (E8, E9).
/// </summary>
public class CasosDoradosDeRiesgoTests
{
    public static IEnumerable<object[]> Casos() => CasoDoradoDeRiesgo.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_de_K1_K3_K6a_y_K6b()
    {
        CasoDoradoDeRiesgo.Archivos().Select(Path.GetFileNameWithoutExtension).Should().BeEquivalentTo(
        [
            // K3 (T0859, T0905): novación y reestructurado dos veces con el historial que sigue el enlace, insolvencia con sus cierres y
            // la evaluación de cartera.
            "r-k3-novacion-hereda", "r-k3-insolvencia-cierres", "r-k3-evaluacion",
            "r01-rangos-por-modalidad", "r02-subcategorias", "r03-arrastre-por-modalidad", "r04-arrastre-no-alcanza-codeudas",
            "r05-reestructurado", "r06-pago-unico", "r07-credito-nuevo", "r08-analisis-solo-peor", "r09-insolvencia",
            "r10-deterioro-individual-anexo1", "r11-deterioro-general", "r12-politica-mas-estricta", "r13-redondeo-del-deterioro",
            "r14-homologacion-contable", "r15-selector-sin-k6a", "r16-d19-sin-descuento-de-aportes",
            // K6a (T1013): la pérdida esperada del Anexo 2, repasada con Casos/riesgo-pe.py.
            "pe01-consumo-con-libranza", "pe02-consumo-sin-libranza", "pe03-comercial-persona-natural", "pe04-historial-parcial-mayor-observada",
            "pe05-historial-parcial-del-corte", "pe06-historial-parcial-fija", "pe07-pi-voluntaria", "pe08-homologacion",
            "pe09-segmento-fuera-del-modelo", "pe10-novacion-hereda-historial",
            // K6b (T1043): vivienda con el Anexo 1 sembrado —E1/E2 por meses en E y la garantía hipotecaria por meses de mora—.
            "r21-vivienda-e1-e2-por-tiempo-en-e",
        ]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_caso_coincide_con_el_calculo_a_mano(string archivo)
    {
        var caso = CasoDoradoDeRiesgo.Cargar(Path.Combine(CasoDoradoDeRiesgo.Directorio, "Casos", archivo));
        foreach (var escenario in caso.Escenarios())
            Comprobar(caso, escenario);
        foreach (var historial in caso.Historiales)
            ComprobarHistorial(caso, historial);
        if (caso.PerdidaEsperada is { } pe)
            ComprobarPerdidaEsperada(caso, pe);
    }

    /// <summary>
    /// K6a (T1013): la pérdida esperada encadenada como en el cierre: historial (dado mes a mes, o construido y completado con su regla),
    /// puntaje (<see cref="MotorDePerdidaEsperada.Puntuar"/>), calificación con la candidata del modelo y el Anexo 1 sembrado
    /// (<see cref="MotorDeCalificacion"/>, con arrastre), alineación del deudor y cálculo (<see cref="MotorDePerdidaEsperada.Calcular"/>). La
    /// suspensión sale de la categoría por mora del Anexo 1 (lectura del equipo, a validar), salvo que el caso la dé.
    /// </summary>
    private static void ComprobarPerdidaEsperada(CasoDoradoDeRiesgo caso, CasoDoradoDeRiesgo.PerdidaEsperadaJson pe)
    {
        var anexo2 = CasoDoradoDeRiesgo.LeerAnexo2(pe.Anexo2);
        var anexo1 = CasoDoradoDeRiesgo.Anexo1Sembrado();
        var contexto = new ContextoDelModelo(pe.Contexto.TipoDeOrganizacion, pe.Contexto.FondoDeEmpleados, pe.Contexto.Smmlv, pe.Contexto.Voluntaria);
        var seleccion = caso.Selector is { } s
            ? SelectorDeModeloDeDeterioro.Seleccionar(CasoDoradoDeRiesgo.LeerRegla(s.Regla), new PerfilParaModelo(s.Tipo, s.CategoriaDeFondo),
                s.Eleccion is { } clase ? new EleccionDeModelo(clase, s.SegmentosDeLaEleccion) : null, s.Entrega)
            : null;

        var enModelo = pe.Creditos.Where(c => c.Segmento is not null).ToList();
        var historiales = enModelo.ToDictionary(c => c.Id, c => Historial(c));
        var puntajes = enModelo.ToDictionary(c => c.Id, c => MotorDePerdidaEsperada.Puntuar(anexo2,
            new CreditoParaPuntaje(c.Id, c.Segmento!, c.DiasDeMora, historiales[c.Id].Completado.Meses)
            {
                AsociadoActivo = c.AsociadoActivo,
                ValorDeLaCuota = c.Cuota,
                PlazoEnMeses = c.PlazoEnMeses,
                PagoUnico = c.PagoUnico,
            }, contexto));
        string Sarc(CasoDoradoDeRiesgo.CreditoPeJson c) => anexo2.SegmentoObligatorio(c.Segmento!).SegmentoSarc;
        var calificaciones = MotorDeCalificacion.Calificar(anexo1, enModelo.Select(c => new CreditoParaCalificar(c.Id, c.Deudor, Sarc(c), c.DiasDeMora)
        {
            PagoUnico = c.PagoUnico,
            CalificacionDelModelo = puntajes[c.Id].CandidataParaCalificar,
            Analisis = c.Analisis is { } an ? new(an.Categoria, an.Subcategoria) : null,
        }), OpcionesDeCalificacion.Cierre).ToDictionary(c => c.LoanId);
        var alinear = enModelo.Select(c => new CreditoParaAlinear(c.Id, c.Deudor, Sarc(c), c.DiasDeMora, puntajes[c.Id].Incumplido)).ToList();
        var porArrastre = MotorDePerdidaEsperada.IncumplidosPorArrastre(alinear);
        var moraActual = MotorDePerdidaEsperada.MoraActualPorDeudor(alinear);

        using var _ = new AssertionScope($"{caso.Nombre} · pérdida esperada");
        foreach (var esp in pe.Esperado)
        {
            var c = pe.Creditos.Single(x => x.Id == esp.Id);
            var quien = $"{caso.Nombre} crédito {esp.Id}";
            if (esp.Modelo is { } modelo)
            {
                var deSeleccion = seleccion ?? throw new InvalidOperationException($"{quien}: espera modelo y el caso no configura el selector.");
                deSeleccion.ParaCredito(c.Segmento).Modelo.Should().Be(modelo, $"{quien}: modelo");
            }
            if (c.Segmento is null) continue;

            var h = historiales[c.Id];
            var p = puntajes[c.Id];
            var cal = calificaciones[c.Id];
            var sarc = anexo1.SegmentoObligatorio(Sarc(c));
            var suspendido = c.Suspendido ?? anexo1.CategoriaPorMora(sarc.Codigo, c.DiasDeMora) >= sarc.CategoriaDeSuspension;
            var r = MotorDePerdidaEsperada.Calcular(anexo2, new CreditoParaPerdidaEsperada(p, cal.Categoria,
                new SaldosParaDeterioro(c.Saldos.Capital, c.Saldos.InteresCorriente, c.Saldos.InteresDeMora, c.Saldos.Otros), suspendido, c.Garantia)
            {
                IncumplidoPorArrastre = porArrastre.Contains(c.Id),
                MoraActualDelDeudor = moraActual[c.Id],
            }, contexto, ContextoDeAportes.D19SinIndicadores, pe.Redondeo);
            var texto = r.Explicacion.Texto + "\n" + cal.Explicacion.Texto + "\n" + h.Completado.Explicacion.Texto;

            if (esp.Faltantes is { } f) h.Faltantes.Should().Be(f, $"{quien}: meses que faltan\n{texto}");
            if (esp.CompletadosCon is { } con)
                h.Completado.Completados.Should().NotBeEmpty().And.OnlyContain(m => m.MaxDiasDeMora == con, $"{quien}: meses completados");
            if (esp.Incumplido is { } inc) r.Incumplido.Should().Be(inc, $"{quien}: incumplido\n{texto}");
            if (esp.DiasEnIncumplimiento is { } dias) r.DiasEnIncumplimiento.Should().Be(dias, $"{quien}: días en incumplimiento");
            if (esp.Z is { } z) p.Z.Should().Be(z, $"{quien}: Z\n{texto}");
            if (esp.Puntaje is { } puntaje) p.Puntaje.Should().Be(puntaje, $"{quien}: puntaje");
            if (esp.CalificacionDelModelo is { } delModelo) p.Calificacion.Should().Be(delModelo, $"{quien}: calificación del modelo");
            if (esp.Variables is { } vs) p.Variables.Select(v => $"{v.Codigo}={v.Valor}").Should().Equal(vs, $"{quien}: variables\n{texto}");
            if (esp.ClaveDePi is { } clave) r.ClaveDePi.Should().Be(clave, $"{quien}: clave de la PI");
            if (esp.CalificacionFinal is { } final) cal.Categoria.Should().Be(final, $"{quien}: calificación final\n{texto}");
            if (esp.Causa is { } causa) cal.Causa.Should().Be(causa, $"{quien}: causa");
            if (esp.Pi is { } pi) r.ProbabilidadDeIncumplimiento.Should().Be(pi, $"{quien}: PI\n{texto}");
            if (esp.Pdi is { } pdi) r.PerdidaDadoElIncumplimiento.Should().Be(pdi, $"{quien}: PDI\n{texto}");
            if (esp.Exposicion is { } vea) r.Exposicion.Should().Be(vea, $"{quien}: exposición");
            if (esp.Suspendido is { } sus) suspendido.Should().Be(sus, $"{quien}: suspensión");
            if (esp.DeterioroCapital is { } dc) r.DeterioroCapital.Should().Be(dc, $"{quien}: deterioro de capital\n{texto}");
            if (esp.DeterioroIntereses is { } di) r.DeterioroIntereses.Should().Be(di, $"{quien}: deterioro de intereses");
            if (esp.DeterioroOtros is { } dO) r.DeterioroOtros.Should().Be(dO, $"{quien}: deterioro de otros");
            if (esp.Total is { } t) r.Total.Should().Be(t, $"{quien}: total");
            if (esp.Contable is { } contable) r.CalificacionContable.Should().Be(contable, $"{quien}: contable\n{texto}");
            if (esp.Homologada is { } homologada) r.Homologada.Should().Be(homologada, $"{quien}: homologada");
            foreach (var linea in esp.ExplicacionContiene ?? []) texto.Should().Contain(linea, $"{quien}: explicación");
        }
    }

    /// <summary>El historial completo del crédito: dado mes a mes o construido con sus filas y transacciones y completado con su regla.</summary>
    private static (HistorialDeMoraCompletado Completado, int Faltantes) Historial(CasoDoradoDeRiesgo.CreditoPeJson c)
    {
        if (c.MoraMensual is { } m)
        {
            var hasta = Mes(m.Hasta);
            var meses = Enumerable.Range(0, m.Meses).Select(i => hasta.AddMonths(i - m.Meses + 1))
                .Select(d => new MoraMensualCompleta(d.Year, d.Month, m.Dias.GetValueOrDefault(d.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)), false))
                .ToList();
            return (new HistorialDeMoraCompletado(meses, null, new IngenIA365ERP.Domain.Lending.ExplicacionDeCartera()), 0);
        }
        var h = c.Historial ?? throw new InvalidOperationException($"El crédito {c.Id} no trae historial.");
        var entradas = h.Filas.SelectMany((f, i) => Meses(f.Desde, f.Hasta)
            .Select(d => new EntradaDeHistorialDeMora(f.Credito, d.Year, d.Month, f.Dias, f.Origen, i + 1))).ToList();
        var construido = HistorialDeMora.Construir(new CreditoParaHistorial(c.Id, h.InicioDeLaCadena) { Antecesores = h.Antecesores }, entradas,
            h.CorteDeMigracion, h.FechaDeReferencia, h.MesesExigidos);
        var transacciones = h.Transacciones.SelectMany(t => Meses(t.Desde, t.Hasta).Select(d => (d, t.Dias)))
            .ToDictionary(x => (x.d.Year, x.d.Month), x => x.Dias);
        return (HistorialDeMora.Completar(construido, transacciones, h.Regla, h.DiasDelCorte, h.DiasFijos), construido.Faltantes.Count);
    }

    private static DateOnly Mes(string aaaaMm) =>
        DateOnly.ParseExact(aaaaMm + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static IEnumerable<DateOnly> Meses(string desde, string? hasta)
    {
        var fin = Mes(hasta ?? desde);
        for (var d = Mes(desde); d <= fin; d = d.AddMonths(1)) yield return d;
    }

    /// <summary>K3 (T0859): el historial de mora con la cadena de la novación, mes a mes como texto y los que faltan.</summary>
    private static void ComprobarHistorial(CasoDoradoDeRiesgo caso, CasoDoradoDeRiesgo.HistorialJson h)
    {
        var construido = HistorialDeMora.Construir(new CreditoParaHistorial(h.Credito, h.InicioDeLaCadena) { Antecesores = h.Antecesores },
            h.Entradas.Select(e => new EntradaDeHistorialDeMora(e.Credito, e.Anio, e.Mes, e.Dias, e.Origen, e.Secuencia) { Vigente = e.Vigente }),
            h.CorteDeMigracion, h.FechaDeReferencia, h.MesesExigidos);
        static string Mes(MesDeHistorialDeMora m) => $"{m.Anio:0000}-{m.Mes:00}";
        using var _ = new AssertionScope($"{caso.Nombre} · historial del crédito {h.Credito}");
        construido.Meses.Select(m => m.MaxDiasDeMora is { } d
                ? $"{Mes(m)} {d} {m.Origen} {m.LoanIdDeLaFuente}"
                : m.CubiertoPorTransacciones ? $"{Mes(m)} transacciones" : $"{Mes(m)} falta")
            .Should().Equal(h.Esperado.Meses, "mes a mes: la mora que manda, su fuente y su crédito");
        construido.Faltantes.Select(Mes).Should().Equal(h.Esperado.Faltantes);
        construido.Completo.Should().Be(h.Esperado.Faltantes.Count == 0);
    }

    private static void Comprobar(CasoDoradoDeRiesgo caso, CasoDoradoDeRiesgo.Escenario e)
    {
        var norma = CasoDoradoDeRiesgo.LeerNorma(e.Norma);
        var efectiva = PoliticaSarcEfectiva.Combinar(norma, CasoDoradoDeRiesgo.LeerPolitica(e.Politica));
        efectiva.Errores.Should().BeEmpty($"{e.Nombre}: la política de un caso sólo toca celdas que la norma trae");
        var aportes = new ContextoDeAportes(e.Opciones.RigeD19, e.Opciones.IndicadoresDisponibles);

        var dias = caso.Creditos.ToDictionary(c => c.Id, c => DiasDeMora(caso, c));
        var calificaciones = MotorDeCalificacion.Calificar(efectiva.Efectiva, caso.Creditos.Select(c => new CreditoParaCalificar(c.Id, c.Deudor, c.Segmento, dias[c.Id])
        {
            MesesEnE = c.MesesEnE,
            Codeudores = c.Codeudores,
            PagoUnico = c.PagoUnico,
            Nuevo = c.Nuevo,
            Reestructuracion = c.Reestructuracion is { } r ? new(r.CategoriaAlReestructurar, r.CuotasPagadasSeguidas) : null,
            Insolvencia = Insolvencia(caso, c.Insolvencia),
            CalificacionDelModelo = c.Modelo,
            Evaluacion = c.Evaluacion is { } ev ? new(ev.Categoria, ev.Subcategoria) : null,
            Analisis = c.Analisis is { } an ? new(an.Categoria, an.Subcategoria) : null,
        }), new OpcionesDeCalificacion(e.Opciones.ConArrastre, aportes)).ToDictionary(c => c.LoanId);

        var deterioros = caso.Creditos.ToDictionary(c => c.Id, c =>
        {
            var cal = calificaciones[c.Id];
            var segmento = efectiva.Efectiva.SegmentoObligatorio(c.Segmento);
            return MotorDeDeterioroIndividual.Calcular(efectiva.Efectiva,
                new CreditoParaDeteriorar(c.Id, c.Segmento, cal.Categoria, cal.Subcategoria, dias[c.Id],
                    new SaldosParaDeterioro(c.Saldos.Capital, c.Saldos.InteresCorriente, c.Saldos.InteresDeMora, c.Saldos.Otros),
                    c.Suspendido ?? (cal.Categoria >= segmento.CategoriaDeSuspension
                        || ReglaDeSuspension.Evaluar(new EntradaDeSuspension(0, null, c.Reestructuraciones, false, false)) is not null))
                {
                    MesesEnE = c.MesesEnE,
                    Garantias = c.Garantias.Select(g => new GarantiaParaDeterioro(g.Naturaleza, g.Valor)).ToList(),
                    AportesAProrrata = c.Aportes,
                }, aportes, e.Opciones.Redondeo);
        });

        using var _ = new AssertionScope(e.Nombre);
        foreach (var esp in e.Esperado.Creditos ?? [])
        {
            var cal = calificaciones[esp.Id];
            var det = deterioros[esp.Id];
            var quien = $"{e.Nombre} crédito {esp.Id}";
            if (esp.DiasDeMora is { } d) dias[esp.Id].Should().Be(d, $"{quien}: días de mora");
            if (esp.CategoriaPorMora is { } m) cal.CategoriaPorMora.Should().Be(m, $"{quien}: categoría por mora");
            cal.Categoria.Should().Be(esp.Categoria, $"{quien}: categoría\n{cal.Explicacion.Texto}");
            cal.Subcategoria.Should().Be(esp.Subcategoria, $"{quien}: subcategoría");
            cal.Causa.Should().Be(esp.Causa, $"{quien}: causa\n{cal.Explicacion.Texto}");
            if (esp.Contable is { } k) cal.Contable.Should().Be(k, $"{quien}: contable");
            if (esp.Candidatas is { } cs) cal.Candidatas.Select(x => $"{x.Causa}:{x.Categoria}").Should().Equal(cs, $"{quien}: candidatas");
            if (esp.GarantiaAdmisible is { } g) det.ValorAdmisibleDeGarantias.Should().Be(g, $"{quien}: garantía admisible");
            if (esp.Aportes is { } a) det.AportesDescontados.Should().Be(a, $"{quien}: aportes");
            if (esp.Base is { } b) det.Base.Should().Be(b, $"{quien}: base\n{det.Explicacion.Texto}");
            if (esp.Porcentaje is { } p) det.Porcentaje.Should().Be(p, $"{quien}: porcentaje");
            if (esp.DeterioroCapital is { } dc) det.DeterioroCapital.Should().Be(dc, $"{quien}: deterioro de capital\n{det.Explicacion.Texto}");
            if (esp.DeterioroIntereses is { } di) det.DeterioroIntereses.Should().Be(di, $"{quien}: deterioro de intereses");
            if (esp.DeterioroOtros is { } dO) det.DeterioroOtros.Should().Be(dO, $"{quien}: deterioro de otros");
            if (esp.DeterioroTotal is { } dt) det.Total.Should().Be(dt, $"{quien}: deterioro total");
            foreach (var texto in esp.ExplicacionContiene ?? [])
                (cal.Explicacion.Texto + "\n" + det.Explicacion.Texto).Should().Contain(texto, $"{quien}: explicación");
        }

        if (e.Esperado.Celdas is { } celdas)
            efectiva.Celdas.Select(c => (c.Ruta, c.Norma, c.Propuesta, c.Efectiva)).Should()
                .BeEquivalentTo(celdas.Select(c => (c.Ruta, c.Norma, c.Propuesta, c.Efectiva)), $"{e.Nombre}: celdas de la política");
        if (e.Esperado.MenosEstrictas is { } menos)
            efectiva.MenosEstrictas.Select(c => c.Ruta).Should().BeEquivalentTo(menos, $"{e.Nombre}: celdas menos estrictas");

        if (e.Esperado.General is { } general)
        {
            var g = e.General ?? throw new InvalidOperationException($"{e.Nombre}: espera general y no lo configura.");
            var lineas = g.Lineas.Select(l => new LineaParaGeneral(l.Dimension(), caso.Creditos.Single(c => c.Id == l.Credito).Saldos.Capital,
                deterioros[l.Credito].Total));
            var resultado = MotorDeDeterioroGeneral.Calcular(lineas,
                ParametrosDeDeterioroGeneral.Desde(efectiva, g.Perfil, g.LimiteLegalMinimo),
                g.Anteriores.ToDictionary(a => a.Dimension(), a => a.Monto), g.ModelosConGeneral.ToHashSet(), e.Opciones.Redondeo);
            resultado.Select(r => (r.Dimension, r.CarteraBruta, r.PorcentajeMinimo, r.PorcentajeAdicional, r.MontoMinimo, r.MontoAdicional,
                    r.CompensacionDelIndividual, r.Total, r.Anterior, r.Delta)).Should()
                .BeEquivalentTo(general.Select(x => (x.Dimension(), x.CarteraBruta, x.PorcentajeMinimo, x.PorcentajeAdicional, x.MontoMinimo,
                    x.MontoAdicional, x.Compensacion, x.Total, x.Anterior, x.Delta)), $"{e.Nombre}: deterioro general");
        }

        if (e.Esperado.Selector is { } modelos)
        {
            var s = e.Selector ?? throw new InvalidOperationException($"{e.Nombre}: espera selector y no lo configura.");
            var seleccion = SelectorDeModeloDeDeterioro.Seleccionar(CasoDoradoDeRiesgo.LeerRegla(s.Regla),
                new PerfilParaModelo(s.Tipo, s.CategoriaDeFondo),
                s.Eleccion is { } clase ? new EleccionDeModelo(clase, s.SegmentosDeLaEleccion) : null, s.Entrega);
            seleccion.Segmentos.Select(m => (m.Segmento, m.Modelo, m.Fundamento, m.ModeloObligatorioNoDisponible)).Should()
                .BeEquivalentTo(modelos.Select(m => (m.Segmento, m.Modelo, m.Fundamento, m.Marca)), $"{e.Nombre}: modelo por segmento");
            if (e.Esperado.Marca is { } marca) seleccion.ModeloObligatorioNoDisponible.Should().Be(marca, $"{e.Nombre}: marca del cierre");
        }
    }

    /// <summary>
    /// La insolvencia del crédito: tal como viene o, si trae su inicio y sus cierres (K3), la que rige al corte del caso
    /// (<see cref="CalificacionDeInsolvencia"/>).
    /// </summary>
    private static InsolvenciaParaCalificar? Insolvencia(CasoDoradoDeRiesgo caso, CasoDoradoDeRiesgo.InsolvenciaJson? i)
    {
        if (i is null) return null;
        if (i.Inicio is not { } inicio) return new(i.Estado, i.CategoriaAlIniciar);
        var corte = caso.Corte ?? throw new InvalidOperationException($"{caso.Nombre}: una insolvencia con inicio necesita el corte.");
        return CalificacionDeInsolvencia.Para(new ProcesoParaCalificar(i.Estado, inicio, i.FracasoEl), corte,
            i.Cierres.Select(x => new CierreParaInsolvencia(x.Fecha, x.Categoria)), i.SinCierreAnterior ?? i.CategoriaAlIniciar);
    }

    /// <summary>Los días que trae el caso o, con cuotas, la altura de mora al corte con la fecha exigible del calendario del art. 829.</summary>
    private static int DiasDeMora(CasoDoradoDeRiesgo caso, CasoDoradoDeRiesgo.CreditoJson c)
    {
        if (c.Cuotas is not { Count: > 0 } cuotas) return c.DiasDeMora ?? 0;
        var corte = caso.Corte ?? throw new InvalidOperationException($"{caso.Nombre}: un crédito con cuotas necesita el corte.");
        var calendario = FechasDeVencimiento.CalendarioDelArticulo829(caso.Festivos);
        return AlturaDeMora.Calcular(corte, cuotas.Select(q => new CuotaParaMora(q.Numero, FechasDeVencimiento.FechaExigible(q.Vencimiento, calendario), q.SaldoVencido)),
            c.Tolerancia).Dias;
    }
}
