using FluentAssertions;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Tests.Lending.Accrual;

/// <summary>
/// Feature 013, US4 (bloque 8C; defecto que destaparon las e2e en los dos motores): el rótulo de una parte es el <c>origin.number</c> de sus
/// mensajes, una columna de 30 caracteres. Llevaba el nombre del tipo (<c>AjusteDeCausacionPorFechaValor-20261014-1</c>, 41) y la corrección por
/// valor nacional no se podía guardar; <c>DimensionContableCambiada</c>, <c>SaldoAFavorReconocido</c> y la décima parte de
/// <c>CausacionSuspendida</c> tampoco cabían. InMemory no mide el largo: aquí se fija la forma de mensajes.md §3 y que cabe siempre. (nuevo)
/// </summary>
public class RotuloDeLasPartesTests
{
    private static readonly DateOnly Dia = new(2026, 12, 14);

    private static OrigenDePartes Origen(AccountingFactPartSource fuente, LoanOperation? disparadora = null) =>
        new(fuente, new LendingProcessRun { Id = 1 }, disparadora, new Dictionary<DateOnly, int>(), null);

    [Theory]
    [InlineData(AccountingFactPartSource.DailyCycleBatch, "CAU-20261214-0007")]
    [InlineData(AccountingFactPartSource.NationalValueCorrectionBatch, "VNC-20261214-0007")]
    [InlineData(AccountingFactPartSource.MonthlyClose, "CIE-20261214-0007")]
    [InlineData(AccountingFactPartSource.CloseReopening, "REA-20261214-0007")]
    public void Cada_fuente_tiene_su_prefijo(AccountingFactPartSource fuente, string esperado) =>
        PartesDeHechosDeCartera.Rotulo(Origen(fuente), Dia, 7).Should().Be(esperado);

    [Fact]
    public void La_puesta_al_dia_nombra_la_operacion_que_la_pidio()
    {
        var operacion = new LoanOperation { Number = "PAG-2026-004811" };

        PartesDeHechosDeCartera.Rotulo(Origen(AccountingFactPartSource.LoanCatchUp, operacion), Dia, 1).Should().Be("ALD-PAG-2026-004811-1");
    }

    [Theory]
    [InlineData(AccountingFactPartSource.DailyCycleBatch)]
    [InlineData(AccountingFactPartSource.NationalValueCorrectionBatch)]
    [InlineData(AccountingFactPartSource.LoanCatchUp)]
    public void Cabe_en_la_columna_aun_con_el_numero_mas_largo(AccountingFactPartSource fuente)
    {
        // El número de operación es de 20 (UK_LND_LoanOperations_Number) y una corrida grande pasa de diez mil partes.
        var operacion = new LoanOperation { Number = new string('9', 20) };

        PartesDeHechosDeCartera.Rotulo(Origen(fuente, operacion), Dia, 1_234_567).Length.Should().BeLessThanOrEqualTo(PartesDeHechosDeCartera.LargoDelRotulo);
    }
}
