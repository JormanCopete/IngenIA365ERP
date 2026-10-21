using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Integration.Contrato;
using IngenIA365ERP.Application.Payroll.Services;

namespace IngenIA365ERP.Application.Tests.Payroll;

/// <summary>
/// Feature 013, fase 9 (defecto hallado por la e2e <c>NominaRecaudaPorElContratoTests</c>): el número del recibo que Nómina le entrega al contrato
/// de obligaciones cabe en los 40 caracteres de <c>LND_LoanOperations.ReceiptNumber</c>. Con la descripción del período en el número, el contrato
/// rechazaba por forma el lote entero de la ordinaria —la nómina se aprobaba sin recaudar— y el reverso de la definitiva terminaba en un 500 de la
/// base porque la forma del reverso no medía el recibo.
/// </summary>
public class RecibosDeNominaParaCarteraTests
{
    [Fact]
    public void El_recibo_de_Nomina_se_recorta_a_lo_que_admite_el_contrato()
    {
        var largo = new ReferenciaDeRecibo("Nómina Primera quincena de noviembre de 2026 v12", new DateOnly(2026, 11, 16), null, null, "NM");
        var corto = new ReferenciaDeRecibo(" Nómina 01/11/2026-15/11/2026 v1 ", new DateOnly(2026, 11, 16), null, null, "NM");

        RecaudoPorNomina.Recibo(largo).Number.Length.Should().BeLessThanOrEqualTo(RecaudoPorNomina.LargoDelRecibo);
        RecaudoPorNomina.Recibo(largo).Number.Should().StartWith("Nómina Primera quincena");
        RecaudoPorNomina.Recibo(corto).Number.Should().Be("Nómina 01/11/2026-15/11/2026 v1");
        RecaudoPorNomina.Recibo(largo).DocumentKind.Should().Be("NM");
    }

    [Fact]
    public void El_reverso_y_la_resolucion_miden_el_recibo_como_la_aplicacion()
    {
        var recibo = new ReferenciaDeRecibo(new string('R', 41), new DateOnly(2026, 11, 16), null, null, "Liquidacion");

        var reverso = new SolicitudDeReversoValidator().Validate(new SolicitudDeReverso(Recaudador.Nomina, $"NOM:{Guid.NewGuid():N}:{Guid.NewGuid():N}",
            "Se equivocó la fecha", recibo));
        reverso.Errors.Should().Contain(e => e.ErrorCode == "ReceiptNumber");

        var resolucion = new SolicitudDeResolucionValidator().Validate(new SolicitudDeResolucion(new Recaudador("TST", ClaseDeRecaudador.Test),
            $"TST:{Guid.NewGuid():N}", Guid.NewGuid(), ConceptoDeObligacion.CreditBalance, 10m, FormaDeResolucion.RefundThroughCollector, recibo));
        resolucion.Errors.Should().Contain(e => e.ErrorCode == "ReceiptNumber");

        new SolicitudDeReversoValidator().Validate(new SolicitudDeReverso(Recaudador.Nomina, $"NOM:{Guid.NewGuid():N}:{Guid.NewGuid():N}",
            "Se equivocó la fecha", RecaudoPorNomina.Recibo(recibo))).Errors.Should().NotContain(e => e.ErrorCode == "ReceiptNumber");
    }
}
