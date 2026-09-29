using FluentAssertions;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.ElectronicInvoicing.Channels;
using IngenIA365ERP.ElectronicInvoicing.Channels.Simulado;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canales;

/// <summary>
/// Feature 012, I5, T806 (contracts/dian.md §3.4 y §14.3): <c>CanalSimulado</c> declara los eventos RADIAN 030 y 032 y los emite con la
/// misma tabla del último dígito, aquí el del <b>proveedor</b> de la factura referida: 1 rechaza la versión 1 y valida la 2; 3 queda en
/// proceso, la consulta no lo encuentra y el reenvío lo valida; 5 no responde; otro valida con CUDE y la respuesta de validación. En
/// producción no emite. (nuevo)
/// </summary>
public class CanalSimuladoEventosTests
{
    private static readonly Guid Cooperativa = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000c093");
    private readonly MemoriaDelCanalSimulado _memoria = new();

    private CanalSimulado Canal() => new(Options.Create(new ElectronicInvoicingOptions()), _memoria);

    private static EventoRadianCanonico Evento(string taxIdDelProveedor, long consecutivo = 1, DianEnvironment ambiente = DianEnvironment.Testing,
        ElectronicDocumentKind tipo = ElectronicDocumentKind.RadianEvent030)
    {
        var configuracion = new ElectronicEmissionSetting
        {
            Mode = EmissionMode.TechnologyProvider, ChannelCode = CanalSimulado.Codigo, Environment = ambiente, IssuerTaxId = "900555111",
            IssuerCheckDigit = "3", IssuerBusinessName = "Cooperativa de prueba", IssuerAddress = "Calle 1", IssuerMunicipalityDaneCode = "76001",
            IssuerEmail = "facturas@coop.co", IsEnabled = true, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
        };
        var entrada = new EntradaDeEventoRadian("INV", Guid.NewGuid(), "SupplierInvoice", "FCP", "FCP1", tipo, "FV4521", new string('c', 96),
            new DateOnly(2026, 9, 20), new ProveedorDelEvento(taxIdDelProveedor, "7", "31", "1", "Distribuidora del Valle S.A.S."),
            tipo == ElectronicDocumentKind.RadianEvent032 ? "REC1" : null, tipo == ElectronicDocumentKind.RadianEvent032 ? new DateOnly(2026, 9, 21) : null,
            "compras@coop");
        var prefijo = tipo == ElectronicDocumentKind.RadianEvent030 ? "EV030" : "EV032";
        var r = ConstructorDelCanonico.ConstruirEvento(entrada,
            new NumeracionDelEvento(configuracion, prefijo, consecutivo, new DateTimeOffset(2026, 9, 25, 15, 0, 0, TimeSpan.Zero)),
            new CondicionTributariaDelEmisor(true, false, false, false));
        if (r.IsFailure) throw new InvalidOperationException(r.Error.Message);
        return r.Value.Evento;
    }

    private static ContextoDeCanal Contexto(EventoRadianCanonico e, int version = 1, DianEnvironment ambiente = DianEnvironment.Testing) =>
        new(Cooperativa, "900555111", "3", EmissionMode.TechnologyProvider, ambiente, null, null, null,
            ContextoDeCanal.ClaveDeIdempotencia(Cooperativa, ambiente, e.Number.Prefix, e.Number.Consecutive, version), null);

    [Fact]
    public void Declara_los_dos_eventos_entre_sus_capacidades()
    {
        Canal().Capacidades.Emite(ElectronicDocumentKind.RadianEvent030).Should().BeTrue();
        Canal().Capacidades.Emite(ElectronicDocumentKind.RadianEvent032).Should().BeTrue();
    }

    [Fact]
    public async Task Valida_con_CUDE_y_respuesta_de_validacion_y_la_consulta_responde_lo_mismo()
    {
        var e = Evento("900123450", tipo: ElectronicDocumentKind.RadianEvent032);
        var r = await Canal().EmitirEventoAsync(e, Contexto(e), default);

        r.Outcome.Should().Be(ChannelOutcome.Validated);
        r.UniqueCode.Should().HaveLength(96);
        r.UniqueCodeKind.Should().Be("CUDE");
        r.TieneRespuestaDeValidacion.Should().BeTrue();
        r.DianDocumentTypeCode.Should().Be("96");
        e.EventCode.Should().Be("032");
        e.Receipt.Should().NotBeNull();

        var consulta = await Canal().ConsultarEstadoAsync(new ReferenciaDeEnvio(e.Number.Prefix, e.Number.Consecutive, e.Environment), Contexto(e), default);
        consulta.Outcome.Should().Be(ChannelOutcome.Validated);
        consulta.UniqueCode.Should().Be(r.UniqueCode);
    }

    [Fact]
    public async Task Rechaza_la_version_1_y_valida_el_reintento()
    {
        var e = Evento("800999111");
        (await Canal().EmitirEventoAsync(e, Contexto(e), default)).Outcome.Should().Be(ChannelOutcome.Rejected);
        (await Canal().EmitirEventoAsync(e, Contexto(e), default)).Outcome.Should().Be(ChannelOutcome.Rejected, "la misma versión responde lo mismo");
        var reintento = await Canal().EmitirEventoAsync(e, Contexto(e, version: 2), default);
        reintento.Outcome.Should().Be(ChannelOutcome.Validated);
        reintento.UniqueCode.Should().HaveLength(96);
    }

    [Fact]
    public async Task Queda_en_proceso_la_consulta_no_lo_encuentra_y_el_reenvio_valida()
    {
        var e = Evento("900123453");
        (await Canal().EmitirEventoAsync(e, Contexto(e), default)).Outcome.Should().Be(ChannelOutcome.InProcess);
        (await Canal().ConsultarEstadoAsync(new ReferenciaDeEnvio(e.Number.Prefix, e.Number.Consecutive, e.Environment), Contexto(e), default))
            .Outcome.Should().Be(ChannelOutcome.NotFound);
        (await Canal().EmitirEventoAsync(e, Contexto(e), default)).Outcome.Should().Be(ChannelOutcome.Validated);
    }

    [Fact]
    public async Task No_responde_con_el_digito_5_y_no_emite_en_produccion()
    {
        var caido = Evento("900123455");
        (await Canal().EmitirEventoAsync(caido, Contexto(caido), default)).Outcome.Should().Be(ChannelOutcome.ChannelUnavailable);

        var produccion = Evento("900123450", ambiente: DianEnvironment.Production);
        (await Canal().EmitirEventoAsync(produccion, Contexto(produccion, ambiente: DianEnvironment.Production), default))
            .Outcome.Should().Be(ChannelOutcome.InvalidData);
    }
}
