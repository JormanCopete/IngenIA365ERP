using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.ElectronicInvoicing.Channels;
using IngenIA365ERP.ElectronicInvoicing.Credentials;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canales;

/// <summary>
/// <c>CredencialesEnArchivo</c> (feature 012, I4, T684; contracts/dian.md §11): dos cooperativas en un directorio temporal, cada una lee
/// sólo <c>{su PublicId}.{ChannelCode}.json</c>; la <c>CredentialKey</c> alterada en la base es <c>ElectronicInvoicing.CredentialMismatch</c>;
/// el archivo ausente es un fallo sin excepción (la emisión lo traduce a <c>ChannelUnavailable</c>); la caché de 5 minutos se invalida por
/// la huella del archivo. (nuevo)
/// </summary>
public sealed class CredencialesEnArchivoTests : IDisposable
{
    private static readonly Guid CooperativaA = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000c091");
    private static readonly Guid CooperativaB = Guid.Parse("7b9e2d10-0000-4000-8000-0000000000b2");
    private const string Canal = "PROV";

    private readonly string _directorio = Path.Combine(Path.GetTempPath(), "fe-credenciales-" + Guid.NewGuid().ToString("N"));
    private readonly CacheDeCredenciales _cache = new();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private DateTime _ahora = new(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc);

    public CredencialesEnArchivoTests()
    {
        Directory.CreateDirectory(_directorio);
        _reloj.UtcNow.Returns(_ => _ahora);
        Escribir(CooperativaA, Canal, """{ "tokenEmpresa": "a-empresa", "tokenPassword": "a-clave" }""");
        Escribir(CooperativaB, Canal, """{ "tokenEmpresa": "b-empresa", "tokenPassword": "b-clave" }""");
    }

    public void Dispose()
    {
        try { Directory.Delete(_directorio, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Escribir(Guid cooperativa, string canal, string contenido)
    {
        var ruta = Path.Combine(_directorio, CredencialesDeCanal.ClaveDe(cooperativa, canal));
        File.WriteAllText(ruta, contenido);
        return ruta;
    }

    private CredencialesEnArchivo Credenciales(Guid? cooperativa, TestApplicationDbContext? db = null, ICanalesDeEmision? canales = null)
    {
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(cooperativa?.ToString());
        return new CredencialesEnArchivo(Options.Create(new ElectronicInvoicingOptions { CredentialsPath = _directorio }), _cache, tenant,
            db ?? TestDbContextFactory.Create(), _reloj, canales);
    }

    private static void Configurar(TestApplicationDbContext db, string credentialKey)
    {
        db.ElectronicEmissionSettings.Add(new ElectronicEmissionSetting
        {
            Mode = EmissionMode.TechnologyProvider, ChannelCode = Canal, Environment = DianEnvironment.Testing, CredentialKey = credentialKey,
            IssuerTaxId = "900123456", IssuerCheckDigit = "7", IssuerBusinessName = "Cooperativa", IsEnabled = true,
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Cada_cooperativa_lee_solo_su_archivo()
    {
        var a = await Credenciales(CooperativaA).ResolverAsync(Canal, default);
        var b = await Credenciales(CooperativaB).ResolverAsync(Canal, default);

        Assert.True(a.IsSuccess, a.IsFailure ? a.Error.Message : null);
        Assert.True(b.IsSuccess, b.IsFailure ? b.Error.Message : null);
        Assert.Equal(CredencialesDeCanal.ClaveDe(CooperativaA, Canal), a.Value.Clave);
        Assert.Equal("a-clave", a.Value.Valores["tokenPassword"]);
        Assert.Equal(CredencialesDeCanal.ClaveDe(CooperativaB, Canal), b.Value.Clave);
        Assert.Equal("b-clave", b.Value.Valores["tokenPassword"]);
        Assert.DoesNotContain("a-clave", a.Value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_cooperativa_sin_su_archivo_no_toma_el_de_otra()
    {
        var tercera = Guid.NewGuid();

        var r = await Credenciales(tercera).ResolverAsync(Canal, default);

        Assert.True(r.IsFailure);
        Assert.Equal(CredencialesEnArchivo.CodigoNoDisponible, r.Error.Code);
    }

    [Fact]
    public async Task El_archivo_ausente_es_un_fallo_sin_excepcion()
    {
        var r = await Credenciales(CooperativaA).ResolverAsync("OTRO", default);

        Assert.True(r.IsFailure);
        Assert.Equal(CredencialesEnArchivo.CodigoNoDisponible, r.Error.Code);
    }

    [Fact]
    public async Task Sin_cooperativa_resuelta_no_lee_nada()
    {
        var r = await Credenciales(null).ResolverAsync(Canal, default);

        Assert.True(r.IsFailure);
    }

    [Theory]
    [InlineData("../PROV")]
    [InlineData("PROV/../../etc")]
    [InlineData("")]
    [InlineData("PR OV")]
    public async Task Un_canal_que_no_es_un_codigo_no_arma_ninguna_ruta(string canal)
    {
        var r = await Credenciales(CooperativaA).ResolverAsync(canal, default);

        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task La_CredentialKey_alterada_en_la_base_es_CredentialMismatch()
    {
        var db = TestDbContextFactory.Create();
        Configurar(db, CredencialesDeCanal.ClaveDe(CooperativaB, Canal));

        var r = await Credenciales(CooperativaA, db).ResolverAsync(Canal, default);

        Assert.True(r.IsFailure);
        Assert.Equal("ElectronicInvoicing.CredentialMismatch", r.Error.Code);
    }

    [Fact]
    public async Task La_CredentialKey_de_la_cooperativa_resuelta_se_acepta()
    {
        var db = TestDbContextFactory.Create();
        Configurar(db, CredencialesDeCanal.ClaveDe(CooperativaA, Canal));

        var r = await Credenciales(CooperativaA, db).ResolverAsync(Canal, default);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
    }

    [Fact]
    public async Task Valida_el_esquema_que_declara_el_adaptador()
    {
        var canales = new CanalesConEsquema(new CanalConEsquema(Canal, "tokenEmpresa", "tokenPassword", "cuenta"));

        var r = await Credenciales(CooperativaA, canales: canales).ResolverAsync(Canal, default);

        Assert.True(r.IsFailure);
        Assert.Equal(CredencialesEnArchivo.CodigoNoDisponible, r.Error.Code);
        Assert.Contains("cuenta", r.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("a-clave", r.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_JSON_ilegible_es_un_fallo_sin_excepcion_y_sin_mostrar_el_contenido()
    {
        Escribir(CooperativaA, "ROTO", "{ esto no es json: secreto-123");

        var r = await Credenciales(CooperativaA).ResolverAsync("ROTO", default);

        Assert.True(r.IsFailure);
        Assert.DoesNotContain("secreto-123", r.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_cache_dura_cinco_minutos_mientras_la_huella_no_cambie()
    {
        var ruta = Path.Combine(_directorio, CredencialesDeCanal.ClaveDe(CooperativaA, Canal));
        var original = File.GetLastWriteTimeUtc(ruta);
        await Credenciales(CooperativaA).ResolverAsync(Canal, default);

        // Mismo largo y misma fecha: la huella no cambia y la caché responde lo viejo.
        File.WriteAllText(ruta, """{ "tokenEmpresa": "a-empresa", "tokenPassword": "a-CLAVE" }""");
        File.SetLastWriteTimeUtc(ruta, original);
        var enCache = await Credenciales(CooperativaA).ResolverAsync(Canal, default);

        _ahora = _ahora.AddMinutes(6);
        var vencida = await Credenciales(CooperativaA).ResolverAsync(Canal, default);

        Assert.Equal("a-clave", enCache.Value.Valores["tokenPassword"]);
        Assert.Equal("a-CLAVE", vencida.Value.Valores["tokenPassword"]);
    }

    [Fact]
    public async Task Un_cambio_de_huella_invalida_la_cache_en_el_acto()
    {
        var ruta = Path.Combine(_directorio, CredencialesDeCanal.ClaveDe(CooperativaA, Canal));
        await Credenciales(CooperativaA).ResolverAsync(Canal, default);

        File.WriteAllText(ruta, """{ "tokenEmpresa": "a-empresa", "tokenPassword": "rotada-2026" }""");
        File.SetLastWriteTimeUtc(ruta, DateTime.UtcNow.AddMinutes(1));
        var nueva = await Credenciales(CooperativaA).ResolverAsync(Canal, default);

        Assert.Equal("rotada-2026", nueva.Value.Valores["tokenPassword"]);
    }

    [Fact]
    public async Task Si_el_archivo_desaparece_deja_de_resolver_aunque_estuviera_en_cache()
    {
        var ruta = Path.Combine(_directorio, CredencialesDeCanal.ClaveDe(CooperativaA, Canal));
        await Credenciales(CooperativaA).ResolverAsync(Canal, default);

        File.Delete(ruta);
        var r = await Credenciales(CooperativaA).ResolverAsync(Canal, default);

        Assert.True(r.IsFailure);
    }

    private sealed class CanalConEsquema(string codigo, params string[] campos) : ICanalDeEmisionElectronica, IDeclaraEsquemaDeCredenciales
    {
        public string ChannelCode { get; } = codigo;

        public IReadOnlyList<string> CamposDeCredencial { get; } = campos;

        public CapacidadesDelCanal Capacidades { get; } = CanalDePrueba.Completas();

        public Task<ResultadoDeCanal> EmitirAsync(Application.ElectronicInvoicing.Canonical.DocumentoElectronicoCanonico documento, ContextoDeCanal contexto, CancellationToken ct) => throw new NotSupportedException();

        public Task<ResultadoDeCanal> ConsultarEstadoAsync(ReferenciaDeEnvio referencia, ContextoDeCanal contexto, CancellationToken ct) => throw new NotSupportedException();

        public Task<ResultadoDeCanal> EmitirEventoAsync(EventoRadianCanonico evento, ContextoDeCanal contexto, CancellationToken ct) => throw new NotSupportedException();

        public Task<ResultadoDeCanal> DescargarArtefactoAsync(ReferenciaDeEnvio referencia, TipoDeArtefacto tipo, ContextoDeCanal contexto, CancellationToken ct) => throw new NotSupportedException();

        public Task<ResultadoDeCanal> ProbarAsync(ContextoDeCanal contexto, CancellationToken ct) => throw new NotSupportedException();

        public Task<ResultadoDeCanal> ConsultarRangosAsync(ContextoDeCanal contexto, CancellationToken ct) => throw new NotSupportedException();

        public Task<ResultadoDeCanal> ConsultarAdquirenteAsync(string tipoDeIdentificacionDian, string numero, ContextoDeCanal contexto, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CanalesConEsquema(params ICanalDeEmisionElectronica[] canales) : ICanalesDeEmision
    {
        public ICanalDeEmisionElectronica Resolver(string channelCode) => canales.First(c => c.ChannelCode == channelCode);

        public IReadOnlyCollection<string> Codigos => canales.Select(c => c.ChannelCode).ToList();
    }
}
