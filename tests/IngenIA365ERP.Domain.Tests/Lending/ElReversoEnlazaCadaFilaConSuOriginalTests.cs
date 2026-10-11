using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Reversos;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Feature 013, US3, T0416 (nombre fijo, DT §2.25; T33, C49, C101): la propuesta de espejos del reverso de un desembolso. Con un solo tramo
/// vivo se espeja cada fila propia (el capital del tramo) y cada fila de la causación del crédito; con varios, sólo el capital del tramo y la
/// diferencia de causación se recalcula desde la fecha valor del tramo; un espejo nunca se espeja y lo ya espejado no se vuelve a espejar.
/// Hoy el libro lo arma <c>ReversoDeDesembolso</c> desde <c>LND_LoanTransactions</c>; cuando exista el <c>LibroTeorico</c> de
/// <c>ReconstruccionDeCredito</c> (fase 8) esta misma propuesta corre sobre él, y US5 suma aquí los reversos de pago.
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
}
