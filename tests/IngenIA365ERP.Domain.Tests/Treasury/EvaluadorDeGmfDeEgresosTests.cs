using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Common.Taxation;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Gmf;

namespace IngenIA365ERP.Domain.Tests.Treasury;

/// <summary>
/// Feature 015, T0125 (R-25; FR-100, FR-205, FR-206): el evaluador del GMF de los egresos coincide con los casos de <c>Casos/gmf-*.json</c>: tarifa como fracción
/// (incluida la cero), redondeo por parámetro, hecho generador por clase de orden, exención del desembolso por operación (abono en cuenta del deudor o cheque de
/// primer beneficiario), la cuenta exenta y lo que el origen ya liquidó, que no se evalúa. Prueba escrita, sin correr.
/// </summary>
public class EvaluadorDeGmfDeEgresosTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Treasury", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "gmf-*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_minimos_de_T0125() =>
        Directory.EnumerateFiles(Directorio, "gmf-*.json").Should().HaveCountGreaterThanOrEqualTo(8, "≈8 casos dorados del GMF (T0125)");

    [Theory]
    [MemberData(nameof(Casos))]
    public void La_evaluacion_coincide_con_el_calculo_manual(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var e = caso.Egreso;
        var p = caso.Parametros;
        var r = EvaluadorDeGmfDeEgresos.Evaluar(
            new EgresoAEvaluar(Enum.Parse<PaymentOrderOrigin>(e.Origen), e.Clase, Enum.Parse<PaymentForm>(e.Forma), e.Valor, e.Deudor, e.CuentaExenta, e.OrigenLiquido),
            new ParametrosDeGmf(p.Tarifa, ParametrosDeGmf.Hechos(p.Hechos), p.ExencionDesembolsos, CalculoDeGmf.Redondeo(p.Redondeo)));

        using (new AssertionScope($"{archivo} — {caso.Nombre}"))
        {
            r.Evaluado.Should().Be(caso.Esperado.Evaluado);
            r.Gravado.Should().Be(caso.Esperado.Gravado);
            r.Exento.Should().Be(caso.Esperado.Exento);
            r.Motivo.Should().Be(caso.Esperado.Motivo);
            r.Base.Should().Be(caso.Esperado.Base);
            r.Gmf.Should().Be(caso.Esperado.Gmf);
            r.EmiteHecho.Should().Be(caso.Esperado.EmiteHecho);
        }
    }

    [Fact]
    public void El_estimado_del_lote_suma_solo_lo_gravado()
    {
        var p = new ParametrosDeGmf(0.004m, HechosGeneradoresDeGmf.DevolucionesYPartidas, true, RedondeoDeGmf.AlPeso);
        var evaluaciones = new[]
        {
            EvaluadorDeGmfDeEgresos.Evaluar(new EgresoAEvaluar(PaymentOrderOrigin.Receipts, "PayerCreditBalanceRefund", PaymentForm.Transfer, 1_000_000m, false, false, false), p),
            EvaluadorDeGmfDeEgresos.Evaluar(new EgresoAEvaluar(PaymentOrderOrigin.LendingDisbursement, "LoanDisbursement", PaymentForm.Transfer, 9_000_000m, true, false, false), p),
        };
        EvaluadorDeGmfDeEgresos.EstimadoDelLote(evaluaciones).Should().Be(4_000m);
    }

    private sealed record Caso(string Nombre, string Descripcion, Egreso Egreso, Parametros Parametros, Esperado Esperado);

    private sealed record Egreso(string Origen, string Clase, string Forma, decimal Valor, bool Deudor, bool CuentaExenta, bool OrigenLiquido);

    private sealed record Parametros(decimal Tarifa, string Hechos, bool ExencionDesembolsos, string Redondeo);

    private sealed record Esperado(bool Evaluado, bool Gravado, bool Exento, string Motivo, decimal Base, decimal Gmf, bool EmiteHecho);
}
