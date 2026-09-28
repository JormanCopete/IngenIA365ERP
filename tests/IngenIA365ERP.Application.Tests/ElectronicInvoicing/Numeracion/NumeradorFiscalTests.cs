using FluentAssertions;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Numeracion;

/// <summary>
/// Feature 012, I4, T677 (T16, FR-038, FR-065; contracts/dian.md §9): el numerador fiscal busca la resolución por (tipo, prefijo, ambiente
/// del canal sellado) vigente <b>y</b> asociada al canal en la fecha; vencida, agotada o no disponible se rechazan con su código;
/// <c>LastIssuedNumber</c> nace en <c>RangeFrom − 1</c>; la vía del caso b devuelve el mismo número sin consumir; la contingencia 03 numera con
/// la resolución <c>Contingency</c> que respalda al tipo; nunca renumera. El bloqueo real entre dos confirmaciones es de las e2e.
/// </summary>
public class NumeradorFiscalTests
{
    private const string Simulado = GuardiaDeEmisionFiscal.CanalSimulado;

    private readonly EmisionDePrueba _e = new();
    private readonly ICerrojoDeInventario _cerrojo = Substitute.For<ICerrojoDeInventario>();

    private NumeradorFiscal Numerador() => new(_e.Db, _cerrojo);

    private static SolicitudDeNumeroFiscal Pedido(ElectronicDocumentKind tipo = ElectronicDocumentKind.Invoice, string prefijo = "SETP",
        DianEnvironment ambiente = DianEnvironment.Testing, string canal = Simulado, DateOnly? fecha = null, bool contingencia = false) =>
        new(tipo, prefijo, ambiente, canal, null, fecha ?? EmisionDePrueba.Hoy, contingencia);

    [Fact]
    public void LastIssuedNumber_nace_en_RangeFrom_menos_uno()
    {
        var r = _e.Resolucion(ResolutionKind.Invoice, "SETP", desde: 990000000, hasta: 995000000);

        r.LastIssuedNumber.Should().Be(989999999);
        r.TieneNumerosEmitidos.Should().BeFalse();
    }

    [Fact]
    public async Task Numera_con_la_resolucion_vigente_y_asociada_al_canal_empezando_en_RangeFrom_y_bloquea_su_fila()
    {
        var r = _e.Resolucion(ResolutionKind.Invoice, "SETP", desde: 990000000, hasta: 995000000);

        var uno = await Numerador().NumerarAsync(Pedido());
        var dos = await Numerador().NumerarAsync(Pedido());

        uno.IsSuccess.Should().BeTrue(uno.IsFailure ? uno.Error.Message : "");
        uno.Value.Consecutivo.Should().Be(990000000);
        uno.Value.Numero.Should().Be("SETP990000000");
        uno.Value.ResolutionPublicId.Should().Be(r.PublicId);
        dos.Value.Consecutivo.Should().Be(990000001, "sin huecos ni repetidos");
        r.LastIssuedNumber.Should().Be(990000001);
        await _cerrojo.Received(2).BloquearResolucionFiscalAsync(r.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Numerar_el_documento_le_copia_prefijo_y_consecutivo()
    {
        _e.Resolucion(ResolutionKind.PosEquivalent, "POS", desde: 1, hasta: 10);
        var doc = new InventoryDocument { Class = DocumentClass.PosEquivalentDocument, OperationDate = EmisionDePrueba.Hoy };

        var r = await Numerador().NumerarDocumentoAsync(doc, Pedido(ElectronicDocumentKind.PosEquivalent, "POS"));

        r.IsSuccess.Should().BeTrue();
        doc.Prefix.Should().Be("POS");
        doc.Number.Should().Be(1);
    }

    [Fact]
    public async Task Nunca_renumera_un_documento_ya_numerado()
    {
        _e.Resolucion(ResolutionKind.Invoice, "SETP");
        var doc = new InventoryDocument { Class = DocumentClass.SalesInvoice, Prefix = "SETP", Number = 7 };

        var accion = () => Numerador().NumerarDocumentoAsync(doc, Pedido());

        await accion.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Vencida_responde_Resolution_Expired()
    {
        _e.Resolucion(ResolutionKind.Invoice, "SETP", validaHasta: new DateOnly(2026, 11, 30));

        var r = await Numerador().NumerarAsync(Pedido());

        r.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionExpiredCode);
    }

    [Fact]
    public async Task Agotada_responde_Resolution_Exhausted_y_no_pasa_del_rango()
    {
        var res = _e.Resolucion(ResolutionKind.Invoice, "SETP", desde: 1, hasta: 2);
        (await Numerador().NumerarAsync(Pedido())).IsSuccess.Should().BeTrue();
        (await Numerador().NumerarAsync(Pedido())).IsSuccess.Should().BeTrue();

        var tercera = await Numerador().NumerarAsync(Pedido());

        tercera.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionExhaustedCode);
        res.LastIssuedNumber.Should().Be(2);
    }

    [Fact]
    public async Task Si_hay_otra_vigente_con_numeros_numera_con_ella_en_vez_de_la_agotada()
    {
        _e.Resolucion(ResolutionKind.Invoice, "SETP", desde: 1, hasta: 1, validaHasta: new DateOnly(2026, 12, 31), numero: "A");
        var nueva = _e.Resolucion(ResolutionKind.Invoice, "SETP", desde: 2, hasta: 50, validaDesde: new DateOnly(2026, 12, 1), numero: "B");
        (await Numerador().NumerarAsync(Pedido())).Value.Consecutivo.Should().Be(1);

        var r = await Numerador().NumerarAsync(Pedido());

        r.Value.ResolutionPublicId.Should().Be(nueva.PublicId);
        r.Value.Consecutivo.Should().Be(2);
    }

    [Fact]
    public async Task Sin_resolucion_responde_ResolutionUnavailable()
    {
        var r = await Numerador().NumerarAsync(Pedido());

        r.Error.Code.Should().Be("Inventory.Numbering.ResolutionUnavailable");
    }

    [Fact]
    public async Task De_otro_ambiente_responde_ResolutionUnavailable()
    {
        _e.Resolucion(ResolutionKind.Invoice, "SETP", ambiente: DianEnvironment.Production);

        var r = await Numerador().NumerarAsync(Pedido(ambiente: DianEnvironment.Testing));

        r.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionUnavailableCode);
    }

    [Fact]
    public async Task No_asociada_al_canal_sellado_responde_ResolutionUnavailable()
    {
        _e.Resolucion(ResolutionKind.Invoice, "SETP", canal: "PROV");
        _e.Resolucion(ResolutionKind.Invoice, "FE", canal: null);

        (await Numerador().NumerarAsync(Pedido(canal: Simulado))).Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionUnavailableCode);
        (await Numerador().NumerarAsync(Pedido(prefijo: "FE"))).Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionUnavailableCode);
        (await Numerador().NumerarAsync(Pedido(canal: "PROV"))).IsSuccess.Should().BeTrue("con el canal sellado sí numera");
    }

    [Fact]
    public async Task La_asociacion_se_mira_a_la_fecha_de_operacion()
    {
        var r = _e.Resolucion(ResolutionKind.Invoice, "SETP", canal: null);
        r.Channels.Add(new() { Resolution = r, ChannelCode = Simulado, ValidFrom = new DateOnly(2026, 12, 10) });
        _e.Db.SaveChanges();

        (await Numerador().NumerarAsync(Pedido())).Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionUnavailableCode);
        (await Numerador().NumerarAsync(Pedido(fecha: new DateOnly(2026, 12, 10)))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task En_contingencia_03_numera_con_la_resolucion_Contingency_que_respalda_al_tipo()
    {
        _e.Resolucion(ResolutionKind.PosEquivalent, "POS");
        _e.Resolucion(ResolutionKind.Contingency, "CPOS", respalda: ResolutionKind.Invoice, numero: "DE-FACTURA");
        var respaldo = _e.Resolucion(ResolutionKind.Contingency, "CPOS", respalda: ResolutionKind.PosEquivalent, desde: 500, hasta: 600,
            numero: "DEL-POS", validaDesde: new DateOnly(2026, 1, 1));

        var r = await Numerador().NumerarAsync(Pedido(ElectronicDocumentKind.PosEquivalent, "CPOS", contingencia: true));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.ResolutionPublicId.Should().Be(respaldo.PublicId);
        r.Value.ResolutionKind.Should().Be(ResolutionKind.Contingency);
        r.Value.Consecutivo.Should().Be(500);
    }

    [Fact]
    public async Task En_contingencia_sin_resolucion_que_respalde_al_tipo_no_numera()
    {
        _e.Resolucion(ResolutionKind.Contingency, "CFE", respalda: ResolutionKind.Invoice);

        var r = await Numerador().NumerarAsync(Pedido(ElectronicDocumentKind.PosEquivalent, "CFE", contingencia: true));

        r.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionUnavailableCode);
    }

    [Fact]
    public async Task Las_notas_no_numeran_con_resolucion()
    {
        var accion = () => Numerador().NumerarAsync(Pedido(ElectronicDocumentKind.CreditNote, "NC"));

        await accion.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task La_via_del_caso_b_devuelve_el_mismo_numero_sin_consumir_la_resolucion()
    {
        var res = _e.Resolucion(ResolutionKind.Invoice, "SETP", desde: 1, hasta: 100);
        var original = await Numerador().NumerarAsync(Pedido());
        var reemplazo = new InventoryDocument { Class = DocumentClass.SalesInvoice };

        NumeradorFiscal.ReutilizarNumeroParaReemplazo(reemplazo, original.Value.Prefijo, original.Value.Consecutivo);

        reemplazo.Prefix.Should().Be("SETP");
        reemplazo.Number.Should().Be(1);
        res.LastIssuedNumber.Should().Be(1, "reemplazar no consume número");
        var otroNumerado = new InventoryDocument { Class = DocumentClass.SalesInvoice, Prefix = "SETP", Number = 2 };
        var renumerar = () => NumeradorFiscal.ReutilizarNumeroParaReemplazo(otroNumerado, "SETP", 1);
        renumerar.Should().Throw<InvalidOperationException>();
    }
}
