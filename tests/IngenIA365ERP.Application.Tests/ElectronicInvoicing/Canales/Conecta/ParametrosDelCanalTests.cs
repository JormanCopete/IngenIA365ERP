using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Contingencies;
using IngenIA365ERP.Application.ElectronicInvoicing.Settings;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canales.Conecta;

/// <summary>Un canal de prueba que declara parámetros, como los de IngenIA Conecta (feature 028).</summary>
public sealed class CanalConParametrosDePrueba(string codigo, DianEnvironment ambiente, bool produccion, CapacidadesDelCanal? capacidades = null)
    : ICanalDeEmisionElectronica, IDeclaraParametrosDelCanal
{
    private readonly CanalDePrueba _canal = new(codigo, capacidades ?? CanalDePrueba.Completas());

    public string ChannelCode => _canal.ChannelCode;

    public CapacidadesDelCanal Capacidades => _canal.Capacidades;

    public DianEnvironment AmbienteExigido { get; } = ambiente;

    public IReadOnlyList<string> Extremos { get; } = ["QA", "PDN"];

    public bool EsDeProduccion { get; } = produccion;

    public string Nombre => $"Canal {codigo}";

    public Task<ResultadoDeCanal> EmitirAsync(DocumentoElectronicoCanonico documento, ContextoDeCanal contexto, CancellationToken ct) => _canal.EmitirAsync(documento, contexto, ct);

    public Task<ResultadoDeCanal> ConsultarEstadoAsync(ReferenciaDeEnvio referencia, ContextoDeCanal contexto, CancellationToken ct) => _canal.ConsultarEstadoAsync(referencia, contexto, ct);

    public Task<ResultadoDeCanal> EmitirEventoAsync(EventoRadianCanonico evento, ContextoDeCanal contexto, CancellationToken ct) => _canal.EmitirEventoAsync(evento, contexto, ct);

    public Task<ResultadoDeCanal> DescargarArtefactoAsync(ReferenciaDeEnvio referencia, TipoDeArtefacto tipo, ContextoDeCanal contexto, CancellationToken ct) =>
        _canal.DescargarArtefactoAsync(referencia, tipo, contexto, ct);

    public Task<ResultadoDeCanal> ProbarAsync(ContextoDeCanal contexto, CancellationToken ct) => _canal.ProbarAsync(contexto, ct);

    public Task<ResultadoDeCanal> ConsultarRangosAsync(ContextoDeCanal contexto, CancellationToken ct) => _canal.ConsultarRangosAsync(contexto, ct);

    public Task<ResultadoDeCanal> ConsultarAdquirenteAsync(string tipoDeIdentificacionDian, string numero, ContextoDeCanal contexto, CancellationToken ct) =>
        _canal.ConsultarAdquirenteAsync(tipoDeIdentificacionDian, numero, contexto, ct);
}

/// <summary>
/// Feature 028, C1 (contracts/canal-conecta.md §1 y §9; plan.md D4 y D8): los modos de Conecta como canales con parámetros. Un canal de
/// pruebas sólo en <c>Testing</c>, uno de producción sólo en <c>Production</c> y sólo en un proceso de producción, con su tenant, un extremo de la
/// lista blanca, el permiso propio y la confirmación escrita; la guardia dice lo mismo con salida; y un canal que no transmite la 03 nunca
/// tiene evento 03.
/// </summary>
public class ParametrosDelCanalTests
{
    private static readonly Guid TenantConecta = Guid.Parse("266f0ee4-20b4-4676-8d76-946ade8e62b4");

    private readonly CanalConParametrosDePrueba _pruebas = new("CONECTA-PRUEBAS", DianEnvironment.Testing, produccion: false);
    private readonly CanalConParametrosDePrueba _real = new("CONECTA", DianEnvironment.Production, produccion: true);
    private readonly EmisionDePrueba _e;

    public ParametrosDelCanalTests() => _e = new EmisionDePrueba(_pruebas, _real);

    private ConfigureEmissionCommandHandler Configurar(bool procesoDePruebas = false, bool conPermiso = true)
    {
        var permisos = Substitute.For<IPermissionChecker>();
        permisos.HasPermissionAsync(ReglasDeParametrosDelCanal.PermisoDeProduccion, Arg.Any<CancellationToken>()).Returns(conPermiso);
        return new ConfigureEmissionCommandHandler(_e.Db, _e.Canales, _e.Tenant,
            Options.Create(new CanalesExternosOptions { AmbienteDePruebas = procesoDePruebas }), permisos);
    }

    private static ConfigureEmissionCommand Pedido(string canal, DianEnvironment ambiente, Guid? tenant = null, string? extremo = "PDN",
        string? confirmacion = null) =>
        new(EmissionMode.TechnologyProvider, canal, ambiente, null, null, EmailDeliveryBy.Erp, true, new DateOnly(2026, 1, 1), "arranque",
            "900123456", "7", "Cooperativa", "Calle 1", "76001", "facturas@coop.co", tenant ?? TenantConecta, extremo, confirmacion);

    // ------------------------------------------------------------------------------------------ configuración --

    [Fact]
    public async Task Conecta_pruebas_se_configura_en_Testing_con_su_tenant_y_el_extremo_de_la_lista()
    {
        var r = await Configurar(procesoDePruebas: true).Handle(Pedido("CONECTA-PRUEBAS", DianEnvironment.Testing, extremo: "pdn"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.ChannelTenantId.Should().Be(TenantConecta);
        r.Value.ChannelEndpoint.Should().Be("PDN", "se guarda la clave de la lista, tal como está escrita allí");
        r.Value.CredentialVerifiedAt.Should().BeNull("hasta verificar");
    }

    [Theory]
    [InlineData("CONECTA-PRUEBAS", DianEnvironment.Production)]
    [InlineData("CONECTA", DianEnvironment.Testing)]
    public async Task Cada_modo_solo_en_su_ambiente(string canal, DianEnvironment ambiente)
    {
        var r = await Configurar().Handle(Pedido(canal, ambiente, confirmacion: "PRODUCCION"), default);

        r.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ChannelEnvironmentMismatchCode);
    }

    [Fact]
    public async Task Conecta_real_no_se_configura_en_un_proceso_de_pruebas()
    {
        var r = await Configurar(procesoDePruebas: true).Handle(Pedido("CONECTA", DianEnvironment.Production, confirmacion: "PRODUCCION"), default);

        r.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ProductionChannelNotAllowedHereCode);
    }

    [Fact]
    public async Task Conecta_real_exige_el_permiso_propio_y_la_confirmacion_escrita()
    {
        (await Configurar(conPermiso: false).Handle(Pedido("CONECTA", DianEnvironment.Production, confirmacion: "PRODUCCION"), default))
            .Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ProductionConfirmationRequiredCode);
        (await Configurar().Handle(Pedido("CONECTA", DianEnvironment.Production, confirmacion: "produccion"), default))
            .Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ProductionConfirmationRequiredCode, "se escribe tal cual");

        var r = await Configurar().Handle(Pedido("CONECTA", DianEnvironment.Production, confirmacion: "PRODUCCION"), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
    }

    [Fact]
    public async Task Sin_tenant_o_con_un_extremo_fuera_de_la_lista_no_se_configura()
    {
        (await Configurar().Handle(Pedido("CONECTA-PRUEBAS", DianEnvironment.Testing, tenant: Guid.Empty), default))
            .Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ChannelTenantRequiredCode);
        (await Configurar().Handle(Pedido("CONECTA-PRUEBAS", DianEnvironment.Testing, extremo: "https://otro.example"), default))
            .Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.EndpointUnknownCode, "la URL nunca viene de la petición");
        (await Configurar().Handle(Pedido("CONECTA-PRUEBAS", DianEnvironment.Testing, extremo: null), default))
            .Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.EndpointUnknownCode);
    }

    [Fact]
    public async Task El_simulado_no_guarda_tenant_ni_extremo_aunque_vengan()
    {
        var r = await Configurar().Handle(Pedido(GuardiaDeEmisionFiscal.CanalSimulado, DianEnvironment.Testing), default);

        r.Value.ChannelTenantId.Should().BeNull();
        r.Value.ChannelEndpoint.Should().BeNull();
    }

    // ------------------------------------------------------------------------------------------ guardia --

    private InventoryDocumentType ListaParaFacturar(string canal, DianEnvironment ambiente, Action<Domain.Entities.ElectronicInvoicing.ElectronicEmissionSetting>? ajuste = null)
    {
        _e.Obligada(true);
        _e.Configuracion(canal: canal, ambiente: ambiente, ajuste: s =>
        {
            s.ChannelTenantId = TenantConecta;
            s.ChannelEndpoint = "PDN";
            ajuste?.Invoke(s);
        });
        _e.Resolucion(ResolutionKind.Invoice, "SETT", canal: canal, ambiente: ambiente);
        var tipo = new InventoryDocumentType { Code = "FV", Name = "FV", Class = DocumentClass.SalesInvoice, FiscalPrefix = "SETT", IsActive = true };
        _e.Db.InventoryDocumentTypes.Add(tipo);
        _e.Db.SaveChanges();
        return tipo;
    }

    [Fact]
    public async Task Conecta_pruebas_completa_responde_Electronic()
    {
        var tipo = ListaParaFacturar("CONECTA-PRUEBAS", DianEnvironment.Testing);

        var ev = await _e.Guardia().EvaluarAsync(EmisionDePrueba.Hoy, tipo, null, default);

        ev.Veredicto.Should().Be(VeredictoFiscal.Electronic, string.Join(" | ", ev.Motivos.Select(m => m.Message)));
        ev.Canal.Should().Be("CONECTA-PRUEBAS");
    }

    [Fact]
    public async Task Conecta_real_en_un_proceso_de_pruebas_queda_bloqueado()
    {
        var tipo = ListaParaFacturar("CONECTA", DianEnvironment.Production);

        var enPruebas = await _e.Guardia(procesoDePruebas: true).EvaluarAsync(EmisionDePrueba.Hoy, tipo, null, default);
        var enProduccion = await _e.Guardia(procesoDePruebas: false).EvaluarAsync(EmisionDePrueba.Hoy, tipo, null, default);

        enPruebas.Motivos.Select(m => m.Code).Should().Contain(GuardiaDeEmisionFiscal.ProductionChannelNotAllowedHereCode);
        enProduccion.Veredicto.Should().Be(VeredictoFiscal.Electronic, string.Join(" | ", enProduccion.Motivos.Select(m => m.Message)));
    }

    [Fact]
    public async Task Una_vigencia_alterada_con_otro_ambiente_o_sin_parametros_queda_bloqueada_con_salida()
    {
        var tipo = ListaParaFacturar("CONECTA-PRUEBAS", DianEnvironment.Testing, s => { s.ChannelEndpoint = "OTRO"; });
        _e.Db.ElectronicEmissionSettings.Single().Environment = DianEnvironment.Production;
        _e.Db.SaveChanges();

        var ev = await _e.Guardia().EvaluarAsync(EmisionDePrueba.Hoy, tipo, null, default);

        ev.Veredicto.Should().Be(VeredictoFiscal.Blocked);
        ev.Motivos.Select(m => m.Code).Should().Contain([GuardiaDeEmisionFiscal.ChannelEnvironmentMismatchCode, GuardiaDeEmisionFiscal.ChannelParametersMissingCode]);
        ev.Motivos.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.Page) && !string.IsNullOrWhiteSpace(m.Permission));
    }

    // ------------------------------------------------------------------------------------------ contingencia 03 (D8) --

    [Fact]
    public async Task Un_canal_que_no_transmite_la_03_nunca_abre_evento_03()
    {
        var sin03 = new CanalConParametrosDePrueba("SIN03", DianEnvironment.Testing, false,
            CanalDePrueba.Completas() with { ContingenciaDelFacturador = false });
        var e = new EmisionDePrueba(sin03);
        var contingencias = new ContingenciaDeLaDian(e.Db, e.Lector(), Substitute.For<IAlertas>(), e.Canales);

        var r = await contingencias.AbrirDeFacturacionAsync("SIN03", DateTime.UtcNow, new QuienDeclara(default, null, "Ana"), "caído", default);
        var conCapacidad = await contingencias.AbrirDeFacturacionAsync(GuardiaDeEmisionFiscal.CanalSimulado, DateTime.UtcNow,
            new QuienDeclara(default, null, "Ana"), "caído", default);

        r.Error.Code.Should().Be(ErroresDeContingencias.NotSupportedByChannelCode);
        conCapacidad.IsSuccess.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------ esquema y nombres --

    [Theory]
    [InlineData("CONECTA", 2)]
    [InlineData("conecta-pruebas", 2)]
    [InlineData("SIMULADO", 1)]
    [InlineData(null, 1)]
    public void El_esquema_lo_decide_el_canal_sellado(string? canal, int esquema) =>
        EsquemaDelCanonico.Para(canal).Should().Be(esquema);

    [Theory]
    [InlineData("2", "JUAN CARLOS PÉREZ GÓMEZ", "JUAN", "PÉREZ", "JUAN CARLOS", "PÉREZ GÓMEZ")]
    [InlineData("2", "ANA PÉREZ", "ANA", "PÉREZ", "ANA", "PÉREZ")]
    [InlineData("2", "MARIA  DEL  MAR  ROJAS", "MARIA", "ROJAS", "MARIA DEL MAR", "ROJAS")]
    [InlineData("2", "OTRO NOMBRE", "ANA", "PÉREZ", "ANA", "PÉREZ")]
    [InlineData("2", "PÉREZ PÉREZ", "PÉREZ", "PÉREZ", "PÉREZ", "PÉREZ")]
    public void Separa_nombres_y_apellidos_donde_empieza_el_primer_apellido(string tipo, string completo, string nombre, string apellido,
        string nombres, string apellidos)
    {
        var s = NombresDePersona.Separar(tipo, completo, nombre, apellido);

        s.Should().Be(new NombresDePersona.Separados(nombres, apellidos));
    }

    [Fact]
    public void Una_persona_juridica_o_sin_datos_no_tiene_nombres_separados()
    {
        NombresDePersona.Separar("1", "COOP S.A.", null, null).Should().BeNull();
        NombresDePersona.Separar("2", "ANA", null, null).Should().BeNull();
    }

    [Theory]
    [InlineData("76001", "76")]
    [InlineData("05001", "05")]
    [InlineData("7600", null)]
    [InlineData("7600A", null)]
    [InlineData(null, null)]
    public void El_departamento_son_los_dos_primeros_digitos_del_municipio(string? municipio, string? departamento) =>
        NombresDePersona.Departamento(municipio).Should().Be(departamento);
}
