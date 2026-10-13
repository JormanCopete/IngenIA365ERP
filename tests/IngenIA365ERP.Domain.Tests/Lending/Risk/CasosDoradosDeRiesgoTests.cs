using FluentAssertions;
using FluentAssertions.Execution;
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
    public void Estan_los_casos_de_K1()
    {
        CasoDoradoDeRiesgo.Archivos().Select(Path.GetFileNameWithoutExtension).Should().BeEquivalentTo(
        [
            "r01-rangos-por-modalidad", "r02-subcategorias", "r03-arrastre-por-modalidad", "r04-arrastre-no-alcanza-codeudas",
            "r05-reestructurado", "r06-pago-unico", "r07-credito-nuevo", "r08-analisis-solo-peor", "r09-insolvencia",
            "r10-deterioro-individual-anexo1", "r11-deterioro-general", "r12-politica-mas-estricta", "r13-redondeo-del-deterioro",
            "r14-homologacion-contable", "r15-selector-sin-k6a", "r16-d19-sin-descuento-de-aportes",
        ]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_caso_coincide_con_el_calculo_a_mano(string archivo)
    {
        var caso = CasoDoradoDeRiesgo.Cargar(Path.Combine(CasoDoradoDeRiesgo.Directorio, "Casos", archivo));
        foreach (var escenario in caso.Escenarios())
            Comprobar(caso, escenario);
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
            Insolvencia = c.Insolvencia is { } i ? new(i.Estado, i.CategoriaAlIniciar) : null,
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
                    c.Suspendido ?? cal.Categoria >= segmento.CategoriaDeSuspension)
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
