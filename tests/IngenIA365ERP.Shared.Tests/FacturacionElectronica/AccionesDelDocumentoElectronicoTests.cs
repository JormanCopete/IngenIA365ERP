using FluentAssertions;
using IngenIA365ERP.Shared.Services.FacturacionElectronica;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.FacturacionElectronica;

/// <summary>
/// T755, T758 (feature 012, I4; contracts/dian.md §5.1, §8; api.md §24.4, §24.5): qué acciones habilita la pantalla para cada estado del
/// documento electrónico. El servidor decide siempre (una acción mal habilitada responde su 422); esto sólo evita ofrecer lo que no
/// procede: «Reintentar ahora» sólo en pendiente o contingencia, los casos b y c sólo con el rechazo confirmado por una consulta posterior
/// al último envío, y la entrega sólo validado o en contingencia. Además, la ruta de API del borrador de reemplazo se traduce a la
/// pantalla que lo edita. (nuevo)
/// </summary>
public class AccionesDelDocumentoElectronicoTests
{
    private const int Pending = 0, Sent = 1, Validated = 2, WithNotices = 3, Rejected = 4, Issuer03 = 5, Dian04 = 6, Cancelled = 7;
    private const int Emit = 1, TransmitContingency = 2, QueryStatus = 3;
    private const int OutValidated = 1, OutRejected = 3, OutInProcess = 4, OutNotFound = 5, OutInvalidData = 8;

    [Theory]
    [InlineData(Pending, true)]
    [InlineData(Issuer03, true)]
    [InlineData(Dian04, true)]
    [InlineData(Sent, false)]
    [InlineData(Validated, false)]
    [InlineData(Rejected, false)]
    [InlineData(Cancelled, false)]
    public void Reintentar_sólo_en_pendiente_o_contingencia(int estado, bool esperado) =>
        AccionesDelDocumentoElectronico.PuedeReintentar(estado).Should().Be(esperado);

    [Theory]
    [InlineData(Pending, true)]
    [InlineData(Sent, true)]
    [InlineData(Rejected, true)]
    [InlineData(Validated, false)]
    [InlineData(WithNotices, false)]
    [InlineData(Cancelled, false)]
    public void Consultar_a_la_DIAN_mientras_no_haya_respuesta_definitiva_o_para_confirmar_un_rechazo(int estado, bool esperado) =>
        AccionesDelDocumentoElectronico.PuedeConsultar(estado).Should().Be(esperado);

    [Theory]
    [InlineData(Validated, true)]
    [InlineData(WithNotices, true)]
    [InlineData(Issuer03, true)]
    [InlineData(Dian04, true)]
    [InlineData(Pending, false)]
    [InlineData(Sent, false)]
    [InlineData(Rejected, false)]
    [InlineData(Cancelled, false)]
    public void Se_entrega_validado_o_en_contingencia(int estado, bool esperado) =>
        AccionesDelDocumentoElectronico.Entregable(estado).Should().Be(esperado);

    [Fact]
    public void El_rechazo_se_confirma_con_una_consulta_posterior_al_ultimo_envio()
    {
        AccionesDelDocumentoElectronico.RechazoConfirmado(Rejected, [(Emit, OutRejected)]).Should().BeFalse("la respuesta del envío sola no confirma");
        AccionesDelDocumentoElectronico.RechazoConfirmado(Rejected, [(Emit, OutRejected), (QueryStatus, OutRejected)]).Should().BeTrue();
        AccionesDelDocumentoElectronico.RechazoConfirmado(Rejected, [(Emit, OutInProcess), (QueryStatus, OutNotFound)]).Should().BeTrue();
        AccionesDelDocumentoElectronico.RechazoConfirmado(Rejected, [(Emit, OutRejected), (QueryStatus, OutInvalidData)]).Should().BeTrue();
        AccionesDelDocumentoElectronico.RechazoConfirmado(Rejected, [(QueryStatus, OutRejected), (TransmitContingency, OutRejected)])
            .Should().BeFalse("la consulta es anterior al último envío");
        AccionesDelDocumentoElectronico.RechazoConfirmado(Validated, [(Emit, OutValidated), (QueryStatus, OutRejected)])
            .Should().BeFalse("sólo un rechazado");
    }

    [Theory]
    [InlineData(Rejected, true, true)]
    [InlineData(Rejected, false, false)]
    [InlineData(Sent, true, false)]
    [InlineData(Validated, true, false)]
    public void Los_casos_b_y_c_exigen_el_rechazo_confirmado(int estado, bool confirmado, bool esperado) =>
        AccionesDelDocumentoElectronico.PuedeReemplazarOCancelar(estado, confirmado).Should().Be(esperado);

    [Fact]
    public void El_caso_a_solo_sobre_un_rechazado()
    {
        AccionesDelDocumentoElectronico.PuedeCorregirCasoA(Rejected).Should().BeTrue();
        AccionesDelDocumentoElectronico.PuedeCorregirCasoA(Sent).Should().BeFalse();
        AccionesDelDocumentoElectronico.PuedeCorregirCasoA(Validated).Should().BeFalse();
    }

    [Theory]
    [InlineData(Pending, true)]
    [InlineData(Rejected, true)]
    [InlineData(Issuer03, true)]
    [InlineData(Dian04, true)]
    [InlineData(Sent, false)]
    [InlineData(Validated, false)]
    [InlineData(Cancelled, false)]
    public void Transmitir_por_el_canal_vigente_sólo_lo_que_sigue_por_transmitir(int estado, bool esperado) =>
        AccionesDelDocumentoElectronico.PuedeTransmitirPorElCanalVigente(estado).Should().Be(esperado);

    [Theory]
    [InlineData("/api/inventory/sales/invoices/{0}", "/ventas/facturas/nueva?documento={0}")]
    [InlineData("/api/inventory/sales/credit-notes/{0}", "/ventas/notas-credito/nueva?documento={0}")]
    [InlineData("/api/inventory/purchases/support-documents/{0}", "/compras/documentos-soporte/{0}")]
    public void La_ruta_del_borrador_de_reemplazo_abre_su_pantalla(string api, string pantalla)
    {
        var id = Guid.NewGuid();
        AccionesDelDocumentoElectronico.PantallaDelBorrador(string.Format(api, id)).Should().Be(string.Format(pantalla, id));
    }

    [Fact]
    public void Una_ruta_desconocida_no_inventa_pantalla() =>
        AccionesDelDocumentoElectronico.PantallaDelBorrador("/api/otra/cosa/1").Should().BeNull();

    [Theory]
    [InlineData("/compras/documentos-soporte?documento=7a0f3c1e-2b4d-4e8f-9a01-23456789abcd", "/compras/documentos-soporte/7a0f3c1e-2b4d-4e8f-9a01-23456789abcd")]
    [InlineData("/ventas/documentos/abc", "/ventas/documentos/abc")]
    [InlineData(null, null)]
    public void La_ruta_del_documento_comercial_se_normaliza(string? ruta, string? esperada) =>
        AccionesDelDocumentoElectronico.PantallaDelOrigen(ruta).Should().Be(esperada);
}
