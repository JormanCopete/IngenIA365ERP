using System.Diagnostics;
using System.Text;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.ElectronicInvoicing.Channels;
using IngenIA365ERP.ElectronicInvoicing.Channels.Simulado;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canales;

/// <summary>Un reloj que avanza a mano, para la caída simulada de <c>ProbarAsync</c>.</summary>
public sealed class RelojManual(DateTimeOffset inicio) : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = inicio;

    public override DateTimeOffset GetUtcNow() => Ahora;
}

/// <summary>
/// <c>CanalSimulado</c> cumple la batería de conformidad (feature 012, I4, T683) y la tabla del último dígito de la identificación de la
/// contraparte de contracts/dian.md §3.4: 1 <c>Rejected</c>, 2 <c>ValidatedWithNotices</c>, 3 <c>InProcess</c> y <c>NotFound</c> en la
/// primera consulta, 4 <c>DianUnavailable</c> con el documento firmado y su código, 5 <c>ChannelUnavailable</c>, cualquier otro
/// <c>Validated</c>. (nuevo)
/// </summary>
public sealed class CanalSimuladoConformidadTests : ConformidadDelCanal
{
    private readonly MemoriaDelCanalSimulado _memoria = new();
    private readonly RelojManual _reloj = new(new DateTimeOffset(2026, 12, 5, 15, 0, 0, TimeSpan.Zero));
    private readonly ElectronicInvoicingOptions _opciones = new();
    private CanalSimulado? _canal;

    private CanalSimulado Simulado => _canal ??= new CanalSimulado(Options.Create(_opciones), _memoria, _reloj);

    protected override ICanalDeEmisionElectronica Canal => Simulado;

    protected override OpcionesDeCanal Opciones => _opciones.Canal(CanalSimulado.Codigo);

    protected override DocumentoElectronicoCanonico Documento(CasoDeConformidad caso, long consecutivo) => caso switch
    {
        CasoDeConformidad.Rechazado => Factura("16000111", consecutivo),
        CasoDeConformidad.ConNotificaciones => Factura("16000112", consecutivo),
        CasoDeConformidad.Ambiguo => Factura("16000113", consecutivo),
        CasoDeConformidad.DianNoDisponible => Factura("16000114", consecutivo),
        CasoDeConformidad.SinSalir => Factura("16000115", consecutivo),
        _ => Factura("16000110", consecutivo),
    };

    protected override int? PeticionesAlTransporte(DocumentoElectronicoCanonico documento) =>
        _memoria.Recepciones(Cooperativa, documento.Environment, documento.Number.Full);

    // ------------------------------------------------------------------------------------------ la tabla de §3.4 --

    [Theory]
    [InlineData("16000110", ChannelOutcome.Validated)]
    [InlineData("16000111", ChannelOutcome.Rejected)]
    [InlineData("16000112", ChannelOutcome.ValidatedWithNotices)]
    [InlineData("16000113", ChannelOutcome.InProcess)]
    [InlineData("16000114", ChannelOutcome.DianUnavailable)]
    [InlineData("16000115", ChannelOutcome.ChannelUnavailable)]
    [InlineData("16000116", ChannelOutcome.Validated)]
    [InlineData("16000117", ChannelOutcome.Validated)]
    [InlineData("16000118", ChannelOutcome.Validated)]
    [InlineData("16000119", ChannelOutcome.Validated)]
    [InlineData("222222222-1", ChannelOutcome.Rejected)]
    [InlineData("CE-A", ChannelOutcome.Validated)]
    public async Task Decide_por_el_ultimo_digito_de_la_identificacion_de_la_contraparte(string taxId, ChannelOutcome esperado)
    {
        var documento = Factura(taxId, 990000200);

        var resultado = await Simulado.EmitirAsync(documento, Contexto(documento), default);

        Assert.Equal(esperado, resultado.Outcome);
    }

    [Fact]
    public async Task El_rechazo_trae_una_regla_de_ejemplo_y_la_consulta_lo_confirma()
    {
        var documento = Factura("16000111", 990000201);

        var emitido = await Simulado.EmitirAsync(documento, Contexto(documento), default);
        var consultado = await Simulado.ConsultarEstadoAsync(Referencia(documento), Contexto(documento), default);

        var regla = Assert.Single(emitido.Mensajes, m => m.Tipo == TipoDeMensajeDelCanal.Rechazo);
        Assert.Equal(CanalSimulado.ReglaDeRechazo, regla.Regla);
        Assert.Contains("SIMULADO", regla.Texto, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(regla.Traduccion));
        Assert.Equal(ChannelOutcome.Rejected, consultado.Outcome);
    }

    [Fact]
    public async Task Una_version_nueva_del_rechazado_se_valida_con_el_mismo_numero()
    {
        var documento = Factura("16000111", 990000202);
        await Simulado.EmitirAsync(documento, Contexto(documento, version: 1), default);

        var corregido = await Simulado.EmitirAsync(documento, Contexto(documento, version: 2), default);

        Assert.Equal(ChannelOutcome.Validated, corregido.Outcome);
        Assert.True(corregido.TieneCodigoUnico);
    }

    [Fact]
    public async Task El_digito_3_queda_en_proceso_la_primera_consulta_no_lo_encuentra_y_el_reenvio_lo_valida()
    {
        var documento = Factura("16000113", 990000203);

        var emitido = await Simulado.EmitirAsync(documento, Contexto(documento), default);
        var consulta = await Simulado.ConsultarEstadoAsync(Referencia(documento), Contexto(documento), default);
        var reenvio = await Simulado.EmitirAsync(documento, Contexto(documento), default);
        var despues = await Simulado.ConsultarEstadoAsync(Referencia(documento), Contexto(documento), default);

        Assert.Equal(ChannelOutcome.InProcess, emitido.Outcome);
        Assert.False(emitido.TieneCodigoUnico);
        Assert.Equal(ChannelOutcome.NotFound, consulta.Outcome);
        Assert.Equal(ChannelOutcome.Validated, reenvio.Outcome);
        Assert.Equal(ChannelOutcome.Validated, despues.Outcome);
        Assert.Equal(reenvio.UniqueCode, despues.UniqueCode);
    }

    [Fact]
    public async Task El_digito_4_entrega_el_documento_firmado_con_codigo_y_tipo_04_y_al_transmitirlo_se_valida_con_el_mismo_codigo()
    {
        var documento = Factura("16000114", 990000204);

        var contingencia = await Simulado.EmitirAsync(documento, Contexto(documento), default);
        var transmitido = await Simulado.EmitirAsync(documento, Contexto(documento), default);

        var esperado = CatalogoDian.Embebido.TipoDeDocumento(ElectronicDocumentKind.Invoice, ContingencyType.Dian04, new DateOnly(2026, 12, 5))!.Codigo;
        Assert.Equal(ChannelOutcome.DianUnavailable, contingencia.Outcome);
        Assert.Equal(esperado, contingencia.DianDocumentTypeCode);
        Assert.Equal("CUFE", contingencia.UniqueCodeKind);
        Assert.DoesNotContain(contingencia.Artefactos, a => a.Tipo == TipoDeArtefacto.ApplicationResponse);
        Assert.Equal(ChannelOutcome.Validated, transmitido.Outcome);
        Assert.Equal(contingencia.UniqueCode, transmitido.UniqueCode);
    }

    [Fact]
    public async Task El_digito_5_nunca_llega_y_la_consulta_no_lo_encuentra()
    {
        var documento = Factura("16000115", 990000205);

        var primero = await Simulado.EmitirAsync(documento, Contexto(documento), default);
        var segundo = await Simulado.EmitirAsync(documento, Contexto(documento), default);
        var consulta = await Simulado.ConsultarEstadoAsync(Referencia(documento), Contexto(documento), default);

        Assert.Equal(ChannelOutcome.ChannelUnavailable, primero.Outcome);
        Assert.Equal(ChannelOutcome.ChannelUnavailable, segundo.Outcome);
        Assert.Equal(ChannelOutcome.NotFound, consulta.Outcome);
        Assert.Equal(0, PeticionesAlTransporte(documento));
    }

    [Fact]
    public async Task El_validado_trae_codigo_de_96_caracteres_QR_y_los_tres_artefactos_marcados_SIMULADO()
    {
        var documento = Factura("16000110", 990000206);

        var r = await Simulado.EmitirAsync(documento, Contexto(documento), default);

        Assert.Equal(96, r.UniqueCode!.Length);
        Assert.True(r.UniqueCode.All(Uri.IsHexDigit));
        Assert.Equal("CUFE", r.UniqueCodeKind);
        Assert.Contains("SIMULADO", r.QrContent, StringComparison.Ordinal);
        Assert.Contains(r.UniqueCode, r.QrContent, StringComparison.Ordinal);
        Assert.NotNull(r.ValidatedAt);
        Assert.Equal("01", r.DianDocumentTypeCode);
        Assert.Equal(3, r.Artefactos.Count);
        foreach (var tipo in new[] { TipoDeArtefacto.XmlFirmado, TipoDeArtefacto.ApplicationResponse, TipoDeArtefacto.AttachedDocument })
        {
            var a = Assert.Single(r.Artefactos, x => x.Tipo == tipo);
            Assert.Equal("application/xml", a.ContentType);
            Assert.Contains("SIMULADO", Encoding.UTF8.GetString(a.Bytes), StringComparison.Ordinal);
            Assert.Contains(documento.Number.Full, a.NombreDeArchivo, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(ElectronicDocumentKind.Invoice, "CUFE")]
    [InlineData(ElectronicDocumentKind.CreditNote, "CUDE")]
    [InlineData(ElectronicDocumentKind.PosEquivalent, "CUDE")]
    [InlineData(ElectronicDocumentKind.PosAdjustmentNote, "CUDE")]
    [InlineData(ElectronicDocumentKind.SupportDocument, "CUDS")]
    [InlineData(ElectronicDocumentKind.SupportDocumentAdjustmentNote, "CUDS")]
    public async Task El_tipo_de_codigo_unico_sale_del_tipo_de_documento(ElectronicDocumentKind tipo, string esperado)
    {
        var documento = Factura("16000110", 990000207, tipo: tipo);

        var r = await Simulado.EmitirAsync(documento, Contexto(documento), default);

        Assert.Equal(esperado, r.UniqueCodeKind);
    }

    [Fact]
    public async Task Solo_admite_el_ambiente_de_pruebas()
    {
        var documento = Factura("16000110", 990000208, ambiente: DianEnvironment.Production);

        var r = await Simulado.EmitirAsync(documento, Contexto(documento), default);

        Assert.Equal(ChannelOutcome.InvalidData, r.Outcome);
        Assert.Contains(r.Mensajes, m => m.Regla == CanalSimulado.ReglaDeAmbiente && m.Tipo == TipoDeMensajeDelCanal.Rechazo);
    }

    [Fact]
    public async Task Recuerda_por_cooperativa_ambiente_y_numero()
    {
        var documento = Factura("16000110", 990000209);
        await Simulado.EmitirAsync(documento, Contexto(documento), default);

        var otra = Guid.NewGuid();
        var contextoDeOtra = Contexto(documento) with { TenantPublicId = otra };
        var deOtra = await Simulado.ConsultarEstadoAsync(Referencia(documento), contextoDeOtra, default);
        var propia = await Simulado.ConsultarEstadoAsync(Referencia(documento), Contexto(documento), default);

        Assert.Equal(ChannelOutcome.NotFound, deOtra.Outcome);
        Assert.Equal(ChannelOutcome.Validated, propia.Outcome);
    }

    [Fact]
    public async Task Descarga_un_artefacto_de_lo_validado_y_NotFound_de_lo_que_no_conoce()
    {
        var documento = Factura("16000110", 990000210);
        await Simulado.EmitirAsync(documento, Contexto(documento), default);

        var xml = await Simulado.DescargarArtefactoAsync(Referencia(documento), TipoDeArtefacto.AttachedDocument, Contexto(documento), default);
        var otro = Factura("16000110", 990000211);
        var nada = await Simulado.DescargarArtefactoAsync(Referencia(otro), TipoDeArtefacto.AttachedDocument, Contexto(otro), default);

        var a = Assert.Single(xml.Artefactos);
        Assert.Equal(TipoDeArtefacto.AttachedDocument, a.Tipo);
        Assert.Equal(ChannelOutcome.NotFound, nada.Outcome);
    }

    [Fact]
    public async Task El_retardo_configurable_hace_esperar_cada_llamada()
    {
        _opciones.Channels[CanalSimulado.Codigo] = new OpcionesDeCanal { DelayMilliseconds = 120 };
        var documento = Factura("16000110", 990000212);

        var cronometro = Stopwatch.StartNew();
        await Simulado.EmitirAsync(documento, Contexto(documento), default);
        cronometro.Stop();

        Assert.True(cronometro.ElapsedMilliseconds >= 100, $"tardó {cronometro.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task El_retardo_respeta_la_cancelacion_de_quien_espera()
    {
        _opciones.Channels[CanalSimulado.Codigo] = new OpcionesDeCanal { DelayMilliseconds = 10_000 };
        var documento = Factura("16000110", 990000213);
        using var cancelar = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Simulado.EmitirAsync(documento, Contexto(documento), cancelar.Token));
    }

    [Fact]
    public async Task El_sondeo_falla_mientras_dura_la_caida_simulada_despues_de_un_ChannelUnavailable()
    {
        var documento = Factura("16000115", 990000214);
        var antes = await Simulado.ProbarAsync(Contexto(documento), default);
        await Simulado.EmitirAsync(documento, Contexto(documento), default);

        var durante = await Simulado.ProbarAsync(Contexto(documento), default);
        _reloj.Ahora = _reloj.Ahora.AddSeconds(Opciones.SimulatedOutageSeconds + 1);
        var despues = await Simulado.ProbarAsync(Contexto(documento), default);

        Assert.Equal(ChannelOutcome.Validated, antes.Outcome);
        Assert.Equal(ChannelOutcome.ChannelUnavailable, durante.Outcome);
        Assert.Equal(ChannelOutcome.Validated, despues.Outcome);
    }

    [Fact]
    public async Task Las_capacidades_son_las_de_I4()
    {
        var c = Simulado.Capacidades;

        Assert.Equal("SIMULADO", Simulado.ChannelCode);
        foreach (var tipo in new[]
                 {
                     ElectronicDocumentKind.Invoice, ElectronicDocumentKind.CreditNote, ElectronicDocumentKind.DebitNote,
                     ElectronicDocumentKind.PosEquivalent, ElectronicDocumentKind.PosAdjustmentNote, ElectronicDocumentKind.SupportDocument,
                     ElectronicDocumentKind.SupportDocumentAdjustmentNote,
                 })
            Assert.True(c.Emite(tipo), $"no emite {tipo}");
        Assert.True(c.AceptaNumeroDelErp);
        Assert.True(c.ContingenciaDelFacturador);
        Assert.True(c.InformaContingenciaDian);
        Assert.True(c.EsAsincrono);
        Assert.True(c.AdmiteClaveDeIdempotencia);
        Assert.True(c.ConsultaAdquirente);

        var documento = Factura("16000110", 990000215);
        var adquirente = await Simulado.ConsultarAdquirenteAsync("13", "16000110", Contexto(documento), default);
        Assert.Equal(ChannelOutcome.Validated, adquirente.Outcome);
        Assert.Contains(adquirente.Mensajes, m => m.Texto.Contains("SIMULADO", StringComparison.Ordinal));
    }
}
