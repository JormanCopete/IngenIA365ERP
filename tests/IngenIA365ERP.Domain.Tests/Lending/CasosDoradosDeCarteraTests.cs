using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Feature 013, T0330 y T0331 (quickstart §1.2, R45; SC-001, SC-002, SC-017, SC-023): el motor de Cartera coincide con la hoja de
/// cálculo <c>Casos/calcular.py</c> (decimal de 40 dígitos, independiente del código) en los casos dorados. El plan esperado cuota a
/// cuota (vencimiento, días, capital, interés, seguro, saldo), tasas (diaria y nominal exactas a 18 decimales; efectivas con y sin
/// cargos a 6) y veredicto de usura con su holgura, para todo caso que traiga «esperado».
///
/// <para>
/// US4 (T0464) corre además las <c>consultas</c> a (D, R) de cada caso y de cada variante con sus operaciones (fecha valor y
/// <c>registradoEn</c>) y sus series: la reconstrucción por día, cuota y concepto al centavo (saldos, mora, suspensión, tasas del día,
/// libro, plan regenerado), la causación con pagos a tiempo, la causación día a día desde una marca y las diferencias del libro entre
/// dos R. Al final de cada reconstrucción comprueba que Σ libro teórico = saldos del estado por concepto y cuota y que la huella se
/// repite, aun con las operaciones en otro orden. La hoja calcula día por día y el motor por segmentos entre eventos: si al motor le
/// falta un punto de cambio, el caso lo delata. Pendientes de validar por la contadora (D1, C7 §7.7).
/// </para>
/// </summary>
public class CasosDoradosDeCarteraTests
{
    public static IEnumerable<object[]> Casos() =>
        CasoDoradoDeCartera.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_de_plan_usura_y_causacion_de_US1_y_US4()
    {
        // Los demás números de quickstart §1.2 son de US5 (11, 16–21, 31) y US6 (25).
        CasoDoradoDeCartera.Archivos().Select(Path.GetFileNameWithoutExtension).Should().Contain(
        [
            "01-cuota-fija-30-360", "02-cuota-fija-real-365-bisiesto", "03-cuota-fija-real-360-usura", "04-abono-constante",
            "05-cuotas-extraordinarias", "06-pago-unico", "07-gracia-a-capital", "08-gracia-total-primera-cuota",
            "09-gracia-total-distribuida", "27-periodicidad-semanal-y-quincenal", "29-equivalencia-lineal",
            "10-vencimiento-en-festivo", "12-suspension-y-reanudacion", "13-usura-baja", "14-correccion-de-usura", "15-tasa-variable-ibr",
            "22-dos-dias-sin-causar", "23-liquidacion-futura", "24-tolerancia-de-saldo-residual", "26-credito-migrado-con-suspenso",
            "28-seguro-sobre-saldo", "30-desembolso-por-tramos",
        ]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_caso_coincide_con_la_hoja_de_calculo(string archivo)
    {
        var caso = CasoDoradoDeCartera.Cargar(Path.Combine(CasoDoradoDeCartera.DirectorioDeCasos, archivo));

        foreach (var (nombre, condiciones, e) in caso.Escenarios())
        {
            var (plan, usura) = MotorDeCartera.SimularConUsura(condiciones.Entrada(), caso.UsuraDe(condiciones));

            using (new AssertionScope(nombre))
            {
                plan.RulesVersion.Should().Be(VersionesDeReglas.Vigente);
                plan.TasaDiaria.Should().Be(e.Tasas.Diaria, "tasa diaria (18 decimales, la única aproximación)");
                plan.TasaNominalDelPeriodo.Should().Be(e.Tasas.NominalDelPeriodo, "nominal del período");
                plan.CuotaNivelada.Should().Be(e.Tasas.CuotaNivelada, "cuota nivelada");
                plan.InteresDeGracia.Should().Be(e.Plan.InteresDeGracia, "interés de la gracia total");
                plan.ResiduoDeLaUltimaCuota.Should().Be(e.Plan.Residuo, "residuo que absorbe la última cuota");

                CompararCuotas(plan.Cuotas, e.Plan.Cuotas);

                plan.TotalCapital.Should().Be(condiciones.Monto, "Σ capital = monto (SC-017)");
                plan.TotalCapital.Should().Be(e.Plan.Totales.Capital);
                plan.TotalInteres.Should().Be(e.Plan.Totales.Interes, "total de interés");
                plan.TotalSeguro.Should().Be(e.Plan.Totales.Seguro, "total de seguro");

                decimal.Round(usura.EfectivaConCargos, 6).Should().Be(e.Tasas.Efectiva, "efectiva con cargos");
                decimal.Round(usura.EfectivaSinCargos, 6).Should().Be(e.Tasas.EfectivaSinCargos, "efectiva sin cargos");
                usura.Veredicto.Should().Be(e.Usura.Veredicto, "veredicto de usura");
                usura.Usura.Should().Be(e.Usura.Usura, "usura usada");
                (usura.Holgura is { } h ? decimal.Round(h, 6) : (decimal?)null).Should().Be(e.Usura.Holgura, "holgura");
                if (usura.Veredicto != VeredictoDeUsura.SinUsura) usura.UsuraPublicId.Should().NotBeNull();
                plan.Explicacion.Lineas.Should().Contain(l => l.Contains("redondeada a 18 decimales"));
            }
        }

        foreach (var (nombre, condiciones, operaciones, consulta) in caso.TodasLasConsultas())
        {
            var donde = $"{nombre} · {consulta.Modo} a {consulta.Fecha:yyyy-MM-dd} con R {consulta.InformacionHasta:yyyy-MM-ddTHH:mm}";
            var e = consulta.Esperado ?? throw new InvalidOperationException($"{donde}: la consulta no trae «esperado».");
            using (new AssertionScope(donde))
            {
                switch (consulta.Modo)
                {
                    case "PagosATiempo":
                        CompararPagosATiempo(condiciones, consulta, e);
                        break;
                    case "Causacion":
                        CompararCausacion(caso.Entrada(condiciones, operaciones, consulta.InformacionHasta), consulta, e);
                        break;
                    case "Diferencias":
                        CompararDiferencias(caso, condiciones, operaciones, consulta, e);
                        break;
                    default:
                        CompararReconstruccion(caso, condiciones, operaciones, consulta, e);
                        break;
                }
            }
        }
    }

    private static void CompararPagosATiempo(CasoDoradoDeCartera.CondicionesJson condiciones, CasoDoradoDeCartera.ConsultaJson consulta,
        CasoDoradoDeCartera.EsperadoDeConsultaJson e)
    {
        var plan = MotorDeCartera.GenerarPlan(condiciones.Entrada());
        var causado = MotorDeCartera.ProyectarCausacion(plan, consulta.Fecha);
        causado.Select(c => (c.Cuota, c.Concepto, c.Valor)).Should().Equal(
            (e.Causado ?? []).Select(c => (c.Cuota, c.Concepto, c.Valor)), "causación con pagos a tiempo por cuota y concepto");
        // A su vencimiento, lo causado de una cuota es exactamente su interés (T25, T29).
        foreach (var c in plan.Cuotas.Where(c => c.Vencimiento <= consulta.Fecha && c.Interes != 0m))
            causado.Single(x => x.Cuota == c.Numero && x.Concepto == LoanConcept.CurrentInterest).Valor.Should().Be(c.Interes, $"cuota {c.Numero}");
    }

    private static void CompararCausacion(EntradaDeReconstruccion entrada, CasoDoradoDeCartera.ConsultaJson consulta,
        CasoDoradoDeCartera.EsperadoDeConsultaJson e)
    {
        var marca = consulta.Marca ?? throw new InvalidOperationException("La consulta de causación lleva su marca.");
        var filas = new List<MovimientoDeLibro>();
        for (var d = marca.AddDays(1); d <= consulta.Fecha; d = d.AddDays(1))
        {
            var r = MotorDeCartera.Causar(entrada, d.AddDays(-1), d);
            r.Causo.Should().BeTrue($"el {d:yyyy-MM-dd} tiene usura certificada");
            filas.AddRange(r.Filas);
        }
        Filas(filas).Should().Equal(Filas(e.Causacion ?? []), "causación día a día desde la marca");

        // Lo causado de a un día es lo que la reconstrucción pone en el libro esos días.
        var libro = MotorDeCartera.Reconstruir(entrada, consulta.Fecha, marca.AddDays(1)).Libro.Movimientos
            .Where(m => m.Clase == LoanTransactionKind.Accrual);
        Filas(libro).Should().Equal(Filas(filas));
    }

    private static void CompararDiferencias(CasoDoradoDeCartera caso, CasoDoradoDeCartera.CondicionesJson condiciones,
        IReadOnlyList<CasoDoradoDeCartera.OperacionJson> operaciones, CasoDoradoDeCartera.ConsultaJson consulta, CasoDoradoDeCartera.EsperadoDeConsultaJson e)
    {
        var rViejo = consulta.ComparaConR ?? throw new InvalidOperationException("La consulta de diferencias lleva comparaConR.");
        var origen = operaciones.Min(o => o.FechaValor);
        var viejo = MotorDeCartera.Reconstruir(caso.Entrada(condiciones, operaciones, rViejo), consulta.Fecha, origen).Libro.Movimientos;
        var nuevo = caso.Entrada(condiciones, operaciones, consulta.InformacionHasta);
        var diferencias = MotorDeCartera.Verificar(nuevo, consulta.Fecha, viejo);
        diferencias.Select(d => (d.Dia, d.Concepto, d.Cuota, d.Diferencia)).Should().Equal(
            (e.Diferencias ?? []).Select(d => (d.Dia, d.Concepto, d.Cuota, d.Valor)), "ajustes por (día valor, concepto, cuota)");
        MotorDeCartera.Verificar(nuevo, consulta.Fecha, MotorDeCartera.Reconstruir(nuevo, consulta.Fecha, origen).Libro.Movimientos)
            .Should().BeEmpty("el libro de la reconstrucción cuadra consigo mismo");
    }

    private static void CompararReconstruccion(CasoDoradoDeCartera caso, CasoDoradoDeCartera.CondicionesJson condiciones,
        IReadOnlyList<CasoDoradoDeCartera.OperacionJson> operaciones, CasoDoradoDeCartera.ConsultaJson consulta, CasoDoradoDeCartera.EsperadoDeConsultaJson e)
    {
        var entrada = caso.Entrada(condiciones, operaciones, consulta.InformacionHasta);
        var estado = MotorDeCartera.Reconstruir(entrada, consulta.Fecha, consulta.LibroDesde);

        estado.SaldosPorConcepto.ToDictionary(s => s.Key.ToString(), s => s.Value).Should().BeEquivalentTo(e.Saldos ?? [], "saldos por concepto");
        estado.Saldos.Select(s => (s.Cuota, s.Concepto, s.Valor)).Should().Equal(
            (e.PorCuota ?? []).Select(s => (s.Cuota, s.Concepto, s.Valor)), "saldos por cuota y concepto");
        estado.CapitalVencido.Should().Be(e.CapitalVencido, "capital vencido");
        estado.AlturaDeMora.Dias.Should().Be(e.DiasDeMora, "días de mora");
        estado.AlturaDeMora.CuotaMasAntigua.Should().Be(e.CuotaMasAntigua, "cuota impaga más antigua");
        estado.AlturaDeMora.CuotasVencidas.Should().Be(e.CuotasVencidas, "cuotas vencidas que cuentan");
        estado.Suspendida.Should().Be(e.Suspendida ?? false, "suspensión");
        estado.ValorParaCancelar.Should().Be(e.ValorParaCancelar, "valor para cancelar");
        estado.TasaDelDia!.TasaDiariaCorriente.Should().Be(e.TasaDiariaCorriente, "tasa diaria corriente del día");
        estado.TasaDelDia.TasaDiariaMora.Should().Be(e.TasaDiariaMora, "tasa diaria de mora del día");
        estado.UsuraTopada.Should().Be(e.UsuraTopada ?? false, "usura topada");
        estado.ValidaHasta.Should().Be(e.ValidaHasta, "válida hasta (D11)");
        estado.Transiciones.Select(t => (t.Fecha, t.Clase == Domain.Lending.Accrual.TransicionDeCausacion.Suspendida)).Should().Equal(
            (e.Transiciones ?? []).Select(t => (t.Fecha, t.Suspendida)), "transiciones de la causación");
        if (e.Libro is not null)
            estado.Libro.Movimientos.Select(m => (m.Dia, m.Clase, m.Concepto, m.Cuota, m.Monto)).Should().Equal(
                e.Libro.Select(f => (f.Dia, f.Clase ?? LoanTransactionKind.Accrual, f.Concepto, f.Cuota, f.Valor)), "libro teórico por día, cuota y concepto");
        if (e.Plan is not null)
        {
            CompararCuotas(estado.Plan!.Cuotas, e.Plan.Cuotas);
            estado.Plan.TotalCapital.Should().Be(e.Plan.Totales.Capital, "Σ capital del plan regenerado (SC-017)");
            estado.Plan.TotalInteres.Should().Be(e.Plan.Totales.Interes);
        }

        // Σ libro teórico desde el origen = saldos del estado por concepto y cuota (el capital sin cuota, por total).
        var completo = MotorDeCartera.Reconstruir(entrada, consulta.Fecha, estado.Origen);
        foreach (var grupo in completo.Libro.Movimientos.GroupBy(m => m.Concepto))
        {
            if (grupo.Any(m => m.Cuota is null))
                grupo.Sum(m => m.Monto).Should().Be(completo.Saldo(grupo.Key), $"Σ libro de {grupo.Key}");
            else
                foreach (var cuota in grupo.GroupBy(m => m.Cuota))
                    cuota.Sum(m => m.Monto).Should().Be(completo.Saldo(grupo.Key, cuota.Key), $"Σ libro de {grupo.Key} en la cuota {cuota.Key}");
        }
        foreach (var s in completo.Saldos.Where(s => s.Concepto != LoanConcept.Principal))
            completo.Libro.SumaHasta(s.Concepto, consulta.Fecha, s.Cuota).Should().Be(s.Valor, $"saldo de {s.Concepto} en la cuota {s.Cuota} sale del libro");

        // La huella se repite, y no depende del orden en que llegan las operaciones.
        completo.Huella.Should().Be(estado.Huella).And.MatchRegex("^[0-9a-f]{64}$");
        completo.TextoCanonico.Should().StartWith(VersionesDeReglas.Vigente + "|");
        var alReves = entrada with { Operaciones = entrada.Operaciones.Reverse().ToList() };
        MotorDeCartera.Reconstruir(alReves, consulta.Fecha).Huella.Should().Be(estado.Huella, "la huella no depende del orden de EF");
    }

    private static void CompararCuotas(IReadOnlyList<Domain.Lending.Schedules.CuotaDelPlan> reales, IReadOnlyList<CasoDoradoDeCartera.CuotaJson> esperadas)
    {
        reales.Should().HaveCount(esperadas.Count, "número de cuotas");
        foreach (var (real, esperada) in reales.Zip(esperadas))
        {
            var donde = $"cuota {esperada.Numero}";
            real.Numero.Should().Be(esperada.Numero, donde);
            real.Vencimiento.Should().Be(esperada.Vencimiento, $"{donde}: vencimiento");
            real.Dias.Should().Be(esperada.Dias, $"{donde}: días");
            real.Capital.Should().Be(esperada.Capital, $"{donde}: capital");
            real.Interes.Should().Be(esperada.Interes, $"{donde}: interés");
            real.Seguro.Should().Be(esperada.Seguro, $"{donde}: seguro");
            real.SaldoFinal.Should().Be(esperada.Saldo, $"{donde}: saldo");
        }
    }

    private static List<(DateOnly, LoanConcept, int?, decimal)> Filas(IEnumerable<MovimientoDeLibro> filas) =>
        filas.Select(f => (f.Dia, f.Concepto, f.Cuota, f.Monto)).OrderBy(f => f.Dia).ThenBy(f => (int)f.Concepto).ThenBy(f => f.Cuota ?? 0).ToList();

    private static List<(DateOnly, LoanConcept, int?, decimal)> Filas(IEnumerable<CasoDoradoDeCartera.FilaDeLibroJson> filas) =>
        filas.Select(f => (f.Dia, f.Concepto, f.Cuota, f.Valor)).OrderBy(f => f.Dia).ThenBy(f => (int)f.Concepto).ThenBy(f => f.Cuota ?? 0).ToList();
}
