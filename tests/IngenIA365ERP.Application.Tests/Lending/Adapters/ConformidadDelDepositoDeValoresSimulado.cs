using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.ExternalChannels;
using IngenIA365ERP.ExternalChannels.Securities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Tests.Lending.Adapters;

/// <summary>
/// <c>DepositoDeValoresSimulado</c> cumple la batería (feature 013, US3, T0423; T71) y la tabla de adaptadores §3.7 / DT §2.22 por el último dígito
/// del documento del deudor: 1 <c>Rejected</c> (firma rechazada), 3 <c>InProcess</c> y firmado a la tercera consulta, 5 <c>ChannelUnavailable</c>,
/// cualquier otro <c>Ok</c> y firmado en la consulta siguiente. Firmado queda en custodia (perfeccionado). Nunca lanza, «ya existe» responde lo que
/// ya creó, no reintenta, su <c>ChannelCode</c> es <c>SIMULADO</c> y marca todo como simulado; recuerda por cooperativa y
/// <c>AddExternalChannels</c> lo registra. (nuevo)
/// </summary>
public sealed class ConformidadDelDepositoDeValoresSimulado : ConformidadDelDepositoDeValores
{
    private static SolicitudDePagare Pagare(string documentoDelDeudor, params string[] codeudores) =>
        new(Guid.NewGuid(), "CR-000123", 3_000_000m, new DateOnly(2026, 12, 1),
            [
                new FirmanteDePagare(Guid.NewGuid(), FirmanteDePagare.Deudor, "CC", documentoDelDeudor, "Ana Solicitante", "ana@correo.test"),
                .. codeudores.Select(c => new FirmanteDePagare(Guid.NewGuid(), FirmanteDePagare.Codeudor, "CC", c, "Codeudor " + c, null)),
            ]);

    private static DepositoDeValoresSimulado Crear(ExternalChannelsOptions? opciones = null) =>
        new(Options.Create(opciones ?? new ExternalChannelsOptions()), new MemoriaDeDepositoSimulado());

    protected override IDepositoDeValores Canal { get; } = Crear();

    protected override bool EsSimulado => true;

    protected override SolicitudDePagare QueSeFirma() => Pagare("52000004", "79000002");

    protected override SolicitudDePagare QueSeRechaza() => Pagare("52000001");

    protected override SolicitudDePagare CanalCaido() => Pagare("52000005");

    protected override IDepositoDeValores CanalLento
    {
        get
        {
            var opciones = new ExternalChannelsOptions();
            opciones.Channels[CodigosDeCanalExterno.Simulado] = new OpcionesDeCanalExterno { TotalTimeoutSeconds = 1, DelayMilliseconds = 1_200 };
            return Crear(opciones);
        }
    }

    [Fact]
    public async Task Con_otro_digito_crea_Ok_y_firma_en_la_consulta_siguiente_quedando_en_custodia()
    {
        var creado = await Canal.CrearPagareAsync(Pagare("1020304"), Contexto(), default);
        var consultado = await Canal.ConsultarPagareAsync(creado.Valor!.IdExterno, Contexto(), default);

        Assert.Equal(ExternalOutcome.Ok, creado.Outcome);
        Assert.Equal(PromissoryNoteStatus.PendingSignature, creado.Valor.Estado);
        Assert.Equal(ExternalOutcome.Ok, consultado.Outcome);
        Assert.Equal(PromissoryNoteStatus.InCustody, consultado.Valor!.Estado);
        Assert.NotNull(consultado.Valor.CustodiaDesde);
        Assert.True(consultado.Valor.Firmado);
    }

    [Fact]
    public async Task Con_el_3_queda_en_proceso_y_firma_a_la_tercera_consulta()
    {
        var creado = await Canal.CrearPagareAsync(Pagare("1020303"), Contexto(), default);
        var primera = await Canal.ConsultarPagareAsync(creado.Valor!.IdExterno, Contexto(), default);
        var segunda = await Canal.ConsultarPagareAsync(creado.Valor.IdExterno, Contexto(), default);
        var tercera = await Canal.ConsultarPagareAsync(creado.Valor.IdExterno, Contexto(), default);

        Assert.Equal(ExternalOutcome.InProcess, creado.Outcome);
        Assert.Equal(ExternalOutcome.InProcess, primera.Outcome);
        Assert.Equal(ExternalOutcome.InProcess, segunda.Outcome);
        Assert.Equal(PromissoryNoteStatus.PendingSignature, segunda.Valor!.Estado);
        Assert.Equal(ExternalOutcome.Ok, tercera.Outcome);
        Assert.Equal(PromissoryNoteStatus.InCustody, tercera.Valor!.Estado);
        Assert.Equal(DepositoDeValoresSimulado.ConsultaQueFirmaConElTres, 3);
    }

    [Fact]
    public async Task Con_el_1_crea_pero_toda_consulta_rechaza_la_firma()
    {
        var creado = await Canal.CrearPagareAsync(Pagare("1020301"), Contexto(), default);
        var primera = await Canal.ConsultarPagareAsync(creado.Valor!.IdExterno, Contexto(), default);
        var otra = await Canal.ConsultarPagareAsync(creado.Valor.IdExterno, Contexto(), default);

        Assert.Equal(ExternalOutcome.Ok, creado.Outcome);
        Assert.Equal(ExternalOutcome.Rejected, primera.Outcome);
        Assert.Equal(ExternalOutcome.Rejected, otra.Outcome);
        Assert.Equal(EstadoDeFirmaDePagare.Rechazada, otra.Valor!.Firmas[0].Estado);
    }

    [Fact]
    public async Task El_5_es_ChannelUnavailable_y_no_crea_nada()
    {
        var r = await Canal.CrearPagareAsync(Pagare("1020305"), Contexto(), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, r.Outcome);
        Assert.Null(r.Valor);
    }

    [Fact]
    public async Task Marca_SIMULADO_en_el_id_externo()
    {
        var r = await Canal.CrearPagareAsync(Pagare("52000004"), Contexto(), default);

        Assert.StartsWith("SIM-", r.Valor!.IdExterno, StringComparison.Ordinal);
        Assert.Equal(r.Valor.IdExterno, r.ReferenciaExterna);
        Assert.Equal(CodigosDeCanalExterno.Simulado, r.ChannelCode);
    }

    [Fact]
    public async Task Recuerda_por_cooperativa_lo_que_creo()
    {
        var solicitud = Pagare("77000004");
        var otra = Contexto() with { TenantPublicId = Guid.NewGuid() };

        var aqui = await Canal.CrearPagareAsync(solicitud, Contexto(), default);
        var alla = await Canal.CrearPagareAsync(solicitud, otra, default);
        var desdeOtra = await Canal.ConsultarPagareAsync(aqui.Valor!.IdExterno, otra, default);

        Assert.NotEqual(aqui.Valor.IdExterno, alla.Valor!.IdExterno);
        Assert.Equal(ExternalOutcome.NotFound, desdeOtra.Outcome);
    }

    [Fact]
    public async Task No_endosa_un_pagare_que_no_esta_en_custodia()
    {
        var creado = await Canal.CrearPagareAsync(Pagare("52000004"), Contexto(), default);

        var r = await Canal.EndosarPagareAsync(creado.Valor!.IdExterno, "Fondo", Contexto(), default);

        Assert.Equal(ExternalOutcome.Rejected, r.Outcome);
        Assert.Equal(PromissoryNoteStatus.PendingSignature, r.Valor!.Estado);
    }

    [Fact]
    public void AddExternalChannels_registra_el_simulado_en_el_registro_de_depositos()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddExternalChannels(new ConfigurationBuilder().Build());
        using var proveedor = servicios.BuildServiceProvider();

        var registro = new DepositosDeValores(proveedor.GetServices<IDepositoDeValores>(), new CircuitoSiempreCerrado());

        Assert.Contains(CodigosDeCanalExterno.Simulado, registro.Codigos);
        Assert.Contains(proveedor.GetServices<CanalExternoRegistrado>(),
            c => c.Puerto == PuertosExternos.DepositoDeValores && c.ChannelCode == CodigosDeCanalExterno.Simulado);
    }

    [Fact]
    public void Dos_adaptadores_del_deposito_con_el_mismo_codigo_hacen_fallar_la_composicion()
    {
        var servicios = new ServiceCollection();
        global::IngenIA365ERP.ExternalChannels.DependencyInjection.AgregarCanal(servicios, PuertosExternos.DepositoDeValores, "DECEVAL", typeof(DepositoDeValoresSimulado));

        Assert.Throws<InvalidOperationException>(() =>
            global::IngenIA365ERP.ExternalChannels.DependencyInjection.AgregarCanal(servicios, PuertosExternos.DepositoDeValores, "deceval", typeof(DepositoDeValoresSimulado)));
    }
}
