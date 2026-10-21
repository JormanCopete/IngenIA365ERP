using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Replay;

/// <summary>
/// Feature 013, K6b, fase 24 (defecto de la corrección de una UVR): <see cref="LibroTeorico.DiferenciasSeparandoLaCorreccionMonetaria"/> compara
/// las filas <c>MonetaryCorrection</c> sólo entre sí y marca sus diferencias, para que la corrida de corrección por valor nacional las registre con
/// su clase y su hecho; lo demás sale como en <see cref="LibroTeorico.Diferencias"/>, y en un crédito en pesos el resultado es el mismo.
/// </summary>
public class DiferenciasConCorreccionMonetariaTests
{
    private static readonly DateOnly Dia = new(2027, 3, 10);

    private static MovimientoDeLibro Fila(LoanTransactionKind clase, LoanConcept concepto, int? cuota, decimal monto) => new(Dia, clase, concepto, cuota, monto);

    [Fact]
    public void La_correccion_monetaria_se_compara_aparte_y_sale_marcada()
    {
        var teorico = new LibroTeorico(
        [
            Fila(LoanTransactionKind.Accrual, LoanConcept.CurrentInterest, 1, 120m),
            Fila(LoanTransactionKind.MonetaryCorrection, LoanConcept.CurrentInterest, 1, 3m),
            Fila(LoanTransactionKind.MonetaryCorrection, LoanConcept.Principal, null, 900m),
        ]);
        var libro = new[]
        {
            Fila(LoanTransactionKind.Accrual, LoanConcept.CurrentInterest, 1, 118m),
            Fila(LoanTransactionKind.MonetaryCorrection, LoanConcept.CurrentInterest, 1, 5m),
            Fila(LoanTransactionKind.MonetaryCorrection, LoanConcept.Principal, null, 700m),
        };

        var diferencias = teorico.DiferenciasSeparandoLaCorreccionMonetaria(libro);

        diferencias.Select(d => (d.Concepto, d.Diferencia, d.DeCorreccionMonetaria)).Should().BeEquivalentTo(new[]
        {
            (LoanConcept.CurrentInterest, 2m, false),
            (LoanConcept.Principal, 200m, true),
            (LoanConcept.CurrentInterest, -2m, true),
        });
        teorico.Diferencias(libro).Select(d => (d.Concepto, d.Diferencia)).Should().BeEquivalentTo(new[] { (LoanConcept.Principal, 200m) },
            "sin separar, el interés de la causación y el de la corrección monetaria se anulan entre sí y no se sabría con qué hecho registrarlos");
    }

    [Fact]
    public void En_un_credito_en_pesos_da_lo_mismo_que_las_diferencias_de_siempre()
    {
        var teorico = new LibroTeorico([Fila(LoanTransactionKind.Accrual, LoanConcept.CurrentInterest, 1, 120m)]);
        var libro = new[] { Fila(LoanTransactionKind.Accrual, LoanConcept.CurrentInterest, 1, 100m) };

        teorico.DiferenciasSeparandoLaCorreccionMonetaria(libro).Should().BeEquivalentTo(teorico.Diferencias(libro));
    }
}
