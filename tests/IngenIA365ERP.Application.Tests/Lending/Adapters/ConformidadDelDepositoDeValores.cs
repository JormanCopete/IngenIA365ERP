using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Tests.Lending.Adapters;

/// <summary>
/// La batería reutilizable de conformidad de los depósitos de valores (feature 013, US3, T0423; T71; contracts/adaptadores-y-archivos.md §3.1,
/// §3.7, §3.9; DT §2.25, §7.6), <c>ConformidadDelDepositoDeValores</c> (fijada allí): se corre contra el simulado y contra cada adaptador real en su
/// ambiente de pruebas. Cada canal provee un pagaré que se firma, uno que se rechaza y uno que no llega (canal caído). Las siete reglas de §3.1,
/// que el resultado traiga su <c>ChannelCode</c> y <c>Simulado</c> sólo en el simulado. (nuevo)
/// </summary>
public abstract class ConformidadDelDepositoDeValores
{
    protected static readonly Guid Cooperativa = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000d013");

    protected abstract IDepositoDeValores Canal { get; }

    protected abstract bool EsSimulado { get; }

    /// <summary>Un pagaré cuyos firmantes firman.</summary>
    protected abstract SolicitudDePagare QueSeFirma();

    /// <summary>Un pagaré cuya firma se rechaza.</summary>
    protected abstract SolicitudDePagare QueSeRechaza();

    /// <summary>Un pagaré que no llega al depósito.</summary>
    protected abstract SolicitudDePagare CanalCaido();

    /// <summary>El mismo canal con un retardo mayor que su tiempo total (regla 2: el transporte no sube como excepción).</summary>
    protected abstract IDepositoDeValores CanalLento { get; }

    /// <summary>Cuántas consultas, como mucho, tarda en informar la firma del pagaré que se firma.</summary>
    protected virtual int ConsultasHastaLaFirma => 3;

    protected ContextoDeCanalExterno Contexto(string? clave = null) =>
        new(Cooperativa, Canal.ChannelCode, null, null, clave ?? Guid.NewGuid().ToString("N"), true);

    /// <summary>Consulta hasta que el depósito informe la firma (o se acaben las consultas admitidas).</summary>
    protected async Task<ResultadoExterno<EstadoDePagare>> ConsultarHastaFirmaAsync(string idExterno)
    {
        ResultadoExterno<EstadoDePagare> r = null!;
        for (var i = 0; i < ConsultasHastaLaFirma; i++)
        {
            r = await Canal.ConsultarPagareAsync(idExterno, Contexto(), default);
            if (r.EsOk && r.Valor!.Firmado) break;
        }
        return r;
    }

    [Fact]
    public async Task Crear_responde_el_id_externo_con_la_firma_pendiente()
    {
        var r = await Canal.CrearPagareAsync(QueSeFirma(), Contexto(), default);

        Assert.Contains(r.Outcome, new[] { ExternalOutcome.Ok, ExternalOutcome.InProcess });
        Assert.NotNull(r.Valor);
        Assert.False(string.IsNullOrWhiteSpace(r.Valor!.IdExterno));
        Assert.Contains(r.Valor.Estado, new[] { PromissoryNoteStatus.Created, PromissoryNoteStatus.PendingSignature });
    }

    [Fact]
    public async Task Consultar_lleva_el_pagare_firmado_a_custodia()
    {
        var creado = await Canal.CrearPagareAsync(QueSeFirma(), Contexto(), default);

        var r = await ConsultarHastaFirmaAsync(creado.Valor!.IdExterno);

        Assert.Equal(ExternalOutcome.Ok, r.Outcome);
        Assert.True(r.Valor!.Firmado);
        Assert.Contains(r.Valor.Estado, new[] { PromissoryNoteStatus.Signed, PromissoryNoteStatus.InCustody });
    }

    [Fact]
    public async Task Regla_1_una_firma_rechazada_es_Rejected_sin_lanzar_y_no_firma()
    {
        var creado = await Canal.CrearPagareAsync(QueSeRechaza(), Contexto(), default);

        var r = await Canal.ConsultarPagareAsync(creado.Valor!.IdExterno, Contexto(), default);

        Assert.Equal(ExternalOutcome.Rejected, r.Outcome);
        Assert.True(r.Valor!.FirmaRechazada);
        Assert.DoesNotContain(r.Valor.Estado, new[] { PromissoryNoteStatus.Signed, PromissoryNoteStatus.InCustody });
    }

    [Fact]
    public async Task Regla_1_un_pagare_sin_documentos_es_InvalidData_sin_lanzar()
    {
        var mala = QueSeFirma() with { Firmantes = [QueSeFirma().Firmantes[0] with { NumeroDeDocumento = "" }] };

        var r = await Canal.CrearPagareAsync(mala, Contexto(), default);

        Assert.Equal(ExternalOutcome.InvalidData, r.Outcome);
        Assert.Null(r.Valor);
    }

    [Fact]
    public async Task Regla_2_el_canal_caido_o_lento_es_ChannelUnavailable_sin_lanzar()
    {
        var caido = await Canal.CrearPagareAsync(CanalCaido(), Contexto(), default);
        var lento = await CanalLento.CrearPagareAsync(QueSeFirma(), Contexto(), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, caido.Outcome);
        Assert.Equal(ExternalOutcome.ChannelUnavailable, lento.Outcome);
    }

    [Fact]
    public async Task Regla_3_crear_dos_veces_el_mismo_pagare_responde_el_que_ya_existe()
    {
        var solicitud = QueSeFirma();

        var primera = await Canal.CrearPagareAsync(solicitud, Contexto(), default);
        var segunda = await Canal.CrearPagareAsync(solicitud, Contexto(), default);

        Assert.Equal(primera.Valor!.IdExterno, segunda.Valor!.IdExterno);
        Assert.Contains(segunda.Outcome, new[] { ExternalOutcome.Ok, ExternalOutcome.InProcess });
    }

    [Fact]
    public async Task Regla_4_una_escritura_que_no_llego_no_deja_nada_ni_se_reintenta_sola()
    {
        var solicitud = CanalCaido();

        var caido = await Canal.CrearPagareAsync(solicitud, Contexto(), default);
        var desconocido = await Canal.ConsultarPagareAsync("NO-EXISTE-" + solicitud.PromissoryNotePublicId.ToString("N"), Contexto(), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, caido.Outcome);
        Assert.Null(caido.Valor);
        Assert.Equal(ExternalOutcome.NotFound, desconocido.Outcome);
    }

    [Fact]
    public async Task Endosar_y_anular_por_el_adaptador()
    {
        var creado = await Canal.CrearPagareAsync(QueSeFirma(), Contexto(), default);
        await ConsultarHastaFirmaAsync(creado.Valor!.IdExterno);

        var endosado = await Canal.EndosarPagareAsync(creado.Valor.IdExterno, "Fondo de garantías", Contexto(), default);
        var anulado = await Canal.AnularPagareAsync(creado.Valor.IdExterno, "Cancelación en firme", Contexto(), default);

        Assert.Equal(ExternalOutcome.Ok, endosado.Outcome);
        Assert.Equal(PromissoryNoteStatus.Endorsed, endosado.Valor!.Estado);
        Assert.Equal(ExternalOutcome.Ok, anulado.Outcome);
        Assert.Equal(PromissoryNoteStatus.Voided, anulado.Valor!.Estado);
    }

    [Fact]
    public async Task Regla_6_dos_adaptadores_con_el_mismo_codigo_no_arrancan()
    {
        Assert.Throws<InvalidOperationException>(() => new DepositosDeValores([Canal, Canal], new CircuitoSiempreCerrado()));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Regla_7_un_codigo_sellado_sin_adaptador_responde_ChannelUnavailable()
    {
        var registro = new DepositosDeValores([Canal], new CircuitoSiempreCerrado());

        var r = await registro.Resolver("DEPOSITO-QUE-NO-ESTA").ConsultarPagareAsync("X", Contexto(), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, r.Outcome);
        Assert.Contains("no está instalado", r.Detalle, StringComparison.Ordinal);
        Assert.Contains(Canal.ChannelCode, registro.CodigosPara(ExternalChannelKind.SecuritiesDepository));
        Assert.NotEqual(ExternalOutcome.ChannelUnavailable,
            (await registro.Resolver(Canal.ChannelCode.ToLowerInvariant()).CrearPagareAsync(QueSeFirma(), Contexto(), default)).Outcome);
    }

    [Fact]
    public async Task Todo_resultado_trae_el_ChannelCode_y_Simulado_solo_en_el_simulado()
    {
        var resultados = new List<ResultadoExterno<EstadoDePagare>>
        {
            await Canal.CrearPagareAsync(QueSeFirma(), Contexto(), default),
            await Canal.CrearPagareAsync(CanalCaido(), Contexto(), default),
            await Canal.ConsultarPagareAsync("NO-EXISTE", Contexto(), default),
        };
        foreach (var r in resultados)
        {
            Assert.Equal(Canal.ChannelCode, r.ChannelCode);
            Assert.Equal(EsSimulado, r.Simulado);
        }
        var prueba = await Canal.ProbarAsync(Contexto(), default);
        Assert.Equal(ExternalOutcome.Ok, prueba.Outcome);
        Assert.Equal(EsSimulado, prueba.Simulado);
    }
}
