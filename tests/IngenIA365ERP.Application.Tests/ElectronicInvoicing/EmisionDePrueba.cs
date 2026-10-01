using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing;

/// <summary>
/// Un canal de prueba (feature 012, I4): capacidades a medida y una respuesta fija para <c>ProbarAsync</c> y <c>ConsultarRangosAsync</c>.
/// No emite: la emisión la prueban las baterías de conformidad (T683) y de emisión (T679).
/// </summary>
public sealed class CanalDePrueba(string codigo, CapacidadesDelCanal capacidades) : ICanalDeEmisionElectronica
{
    public string ChannelCode { get; } = codigo;

    public CapacidadesDelCanal Capacidades { get; } = capacidades;

    public ResultadoDeCanal RespuestaDePrueba { get; set; } = ResultadoDeCanal.Sin(ChannelOutcome.Validated);

    public ResultadoDeCanal RespuestaDeRangos { get; set; } = ResultadoDeCanal.Sin(ChannelOutcome.Validated);

    public ContextoDeCanal? UltimoContexto { get; private set; }

    public Task<ResultadoDeCanal> EmitirAsync(DocumentoElectronicoCanonico documento, ContextoDeCanal contexto, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<ResultadoDeCanal> ConsultarEstadoAsync(ReferenciaDeEnvio referencia, ContextoDeCanal contexto, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<ResultadoDeCanal> EmitirEventoAsync(EventoRadianCanonico evento, ContextoDeCanal contexto, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<ResultadoDeCanal> DescargarArtefactoAsync(ReferenciaDeEnvio referencia, TipoDeArtefacto tipo, ContextoDeCanal contexto, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<ResultadoDeCanal> ProbarAsync(ContextoDeCanal contexto, CancellationToken ct)
    {
        UltimoContexto = contexto;
        return Task.FromResult(RespuestaDePrueba);
    }

    public Task<ResultadoDeCanal> ConsultarRangosAsync(ContextoDeCanal contexto, CancellationToken ct)
    {
        UltimoContexto = contexto;
        return Task.FromResult(RespuestaDeRangos);
    }

    public Task<ResultadoDeCanal> ConsultarAdquirenteAsync(string tipoDeIdentificacionDian, string numero, ContextoDeCanal contexto, CancellationToken ct) =>
        throw new NotSupportedException();

    /// <summary>Todo lo de I4: los siete tipos, número del ERP, contingencias, correo y rangos.</summary>
    public static CapacidadesDelCanal Completas(bool aceptaNumero = true, bool correo = true, params ElectronicDocumentKind[] sin) => new(
        new HashSet<ElectronicDocumentKind>(Enum.GetValues<ElectronicDocumentKind>()
            .Where(t => t is not (ElectronicDocumentKind.RadianEvent030 or ElectronicDocumentKind.RadianEvent032))
            .Where(t => !sin.Contains(t))),
        aceptaNumero, true, true, true, false, correo, true, false, true);
}

/// <summary>El registro de canales de prueba. (Los códigos son los que se pasan.)</summary>
public sealed class CanalesDePrueba(params CanalDePrueba[] canales) : ICanalesDeEmision
{
    public ICanalDeEmisionElectronica Resolver(string channelCode) =>
        canales.FirstOrDefault(c => string.Equals(c.ChannelCode, channelCode, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Canal sin adaptador: {channelCode}");

    public IReadOnlyCollection<string> Codigos => canales.Select(c => c.ChannelCode).ToList();
}

/// <summary>
/// El escenario de facturación electrónica de las pruebas de numeración, guardia y configuración (T677, T678, T682): una cooperativa con
/// su <c>PublicId</c>, el canal simulado y un canal «PROV» de proveedor, un reloj fijo y atajos para configurar, registrar y asociar.
/// </summary>
public sealed class EmisionDePrueba
{
    public static readonly DateOnly Hoy = new(2026, 12, 5);

    public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();

    public Guid Cooperativa { get; } = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000c091");

    public CanalDePrueba Simulado { get; } = new(GuardiaDeEmisionFiscal.CanalSimulado, CanalDePrueba.Completas());

    public CanalDePrueba Proveedor { get; } = new("PROV", CanalDePrueba.Completas());

    public CanalesDePrueba Canales { get; }

    public IDateTimeService Reloj { get; } = Substitute.For<IDateTimeService>();

    public ICurrentTenantService Tenant { get; } = Substitute.For<ICurrentTenantService>();

    public ICredencialesDeCanal Credenciales { get; } = Substitute.For<ICredencialesDeCanal>();

    public EmisionDePrueba(params CanalDePrueba[] otros)
    {
        Canales = new CanalesDePrueba([Simulado, Proveedor, .. otros]);
        Reloj.HoyLocal.Returns(Hoy);
        Reloj.TodayUtc.Returns(Hoy);
        Reloj.UtcNow.Returns(new DateTime(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc));
        Tenant.TenantId.Returns(Cooperativa.ToString());
        Credenciales.ResolverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(c => Result.Success(new CredencialesDeCanal(CredencialesDeCanal.ClaveDe(Cooperativa, c.Arg<string>()),
                new Dictionary<string, string> { ["token"] = "x" })));
    }

    public LectorDeParametros Lector() => new(Db);

    public GuardiaDeEmisionFiscal Guardia(EntregaDelComercio entrega = EntregaDelComercio.I4, bool conCanales = true) =>
        new(Lector(), Db, conCanales ? Canales : null, entrega);

    /// <summary>Fija <c>Dian.ObligadaAFacturar</c> desde el 1 de enero.</summary>
    public void Obligada(bool obligada)
    {
        Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeFacturacionElectronica.Modulo, Key = ParametrosDeFacturacionElectronica.ObligadaAFacturar,
            ScopeKind = ParameterScopeKind.None, Value = obligada ? "true" : "false", ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
        });
        Db.SaveChanges();
    }

    /// <summary>Una configuración completa, habilitada y con la credencial verificada.</summary>
    public ElectronicEmissionSetting Configuracion(string canal = GuardiaDeEmisionFiscal.CanalSimulado, DianEnvironment ambiente = DianEnvironment.Testing,
        DateOnly? desde = null, Action<ElectronicEmissionSetting>? ajuste = null)
    {
        var s = new ElectronicEmissionSetting
        {
            Mode = EmissionMode.TechnologyProvider, ChannelCode = canal, Environment = ambiente,
            CredentialKey = CredencialesDeCanal.ClaveDe(Cooperativa, canal), CredentialVerifiedAt = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            IssuerTaxId = "900123456", IssuerCheckDigit = "7", IssuerBusinessName = "Cooperativa de prueba", IssuerAddress = "Calle 1",
            IssuerMunicipalityDaneCode = "76001", IssuerEmail = "facturas@coop.co", IsEnabled = true,
            ValidFrom = desde ?? new DateOnly(2026, 1, 1), Reason = "prueba",
        };
        ajuste?.Invoke(s);
        Db.ElectronicEmissionSettings.Add(s);
        Db.SaveChanges();
        return s;
    }

    /// <summary>Una resolución de 1 a 100 vigente todo 2026, asociada a <paramref name="canal"/> desde el 1 de enero (nula = sin asociar).</summary>
    public DianNumberingResolution Resolucion(ResolutionKind tipo, string prefijo, string? canal = GuardiaDeEmisionFiscal.CanalSimulado,
        DianEnvironment ambiente = DianEnvironment.Testing, ResolutionKind? respalda = null, long desde = 1, long hasta = 100,
        DateOnly? validaDesde = null, DateOnly? validaHasta = null, string? numero = null)
    {
        var r = new DianNumberingResolution
        {
            Kind = tipo, BacksUpKind = respalda, ResolutionNumber = numero ?? $"18764{prefijo}{(int)tipo}{desde}", ResolutionDate = new DateOnly(2025, 12, 20),
            Prefix = prefijo, RangeTo = hasta, ValidFrom = validaDesde ?? new DateOnly(2026, 1, 1), ValidTo = validaHasta ?? new DateOnly(2026, 12, 31),
            Environment = ambiente, IsActive = true,
        };
        r.RangeFrom = desde;
        if (canal is not null)
            r.Channels.Add(new DianResolutionChannel { Resolution = r, ChannelCode = canal, ValidFrom = new DateOnly(2026, 1, 1) });
        Db.DianNumberingResolutions.Add(r);
        Db.SaveChanges();
        return r;
    }

    /// <summary>Una contingencia 03 abierta en <paramref name="canal"/>.</summary>
    public DianContingencyEvent Contingencia03(string canal = GuardiaDeEmisionFiscal.CanalSimulado)
    {
        var e = new DianContingencyEvent
        {
            Type = ContingencyType.Issuer03, ChannelCode = canal, StartedAt = new DateTime(2026, 12, 5, 13, 0, 0, DateTimeKind.Utc),
            DetectedByName = "Ana", Reason = "sin internet", Status = ContingencyEventStatus.Open,
        };
        Db.DianContingencyEvents.Add(e);
        Db.SaveChanges();
        return e;
    }
}
