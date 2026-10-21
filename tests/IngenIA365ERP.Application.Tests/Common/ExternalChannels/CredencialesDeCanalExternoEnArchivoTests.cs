using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.ExternalChannels;
using IngenIA365ERP.ExternalChannels.Credentials;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.ExternalChannels;

/// <summary>
/// <c>CredencialesDeCanalExternoEnArchivo</c> (feature 013, T0030; T72; contracts/adaptadores-y-archivos.md §3.4, §10) con dos cooperativas
/// en un directorio temporal: la ruta sale sólo del <c>PublicId</c> de la cooperativa resuelta y de un <c>ChannelCode</c> que cumple
/// <c>^[A-Z0-9-]{2,30}$</c>; un código con <c>..</c> o <c>/</c> se rechaza; la <c>CredentialKey</c> que no coincide con la ruta derivada es
/// <c>Core.ExternalChannel.CredentialMismatch</c>; caché de 5 minutos por huella del archivo; <c>SIMULADO</c> y <c>CARGA-PROPIA</c> no leen
/// archivo. (nuevo)
/// </summary>
public sealed class CredencialesDeCanalExternoEnArchivoTests : IDisposable
{
    private static readonly Guid CooperativaA = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000c091");
    private static readonly Guid CooperativaB = Guid.Parse("7b9e2d10-0000-4000-8000-0000000000b2");
    private const string Canal = "PROV-SMS";

    private readonly string _directorio = Path.Combine(Path.GetTempPath(), "canales-externos-" + Guid.NewGuid().ToString("N"));
    private readonly CacheDeCredencialesExternas _cache = new();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private DateTime _ahora = new(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc);

    public CredencialesDeCanalExternoEnArchivoTests()
    {
        Directory.CreateDirectory(_directorio);
        _reloj.UtcNow.Returns(_ => _ahora);
        Escribir(CooperativaA, Canal, """{ "usuario": "a-usuario", "clave": "a-clave" }""");
        Escribir(CooperativaB, Canal, """{ "usuario": "b-usuario", "clave": "b-clave" }""");
    }

    public void Dispose()
    {
        try { Directory.Delete(_directorio, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Escribir(Guid cooperativa, string canal, string contenido)
    {
        var ruta = Path.Combine(_directorio, CredencialDeCanalExterno.ClaveDe(cooperativa, canal));
        File.WriteAllText(ruta, contenido);
        return ruta;
    }

    private CredencialesDeCanalExternoEnArchivo Credenciales(Guid? cooperativa, TestApplicationDbContext? db = null,
        params IDeclaraEsquemaDeCredencialExterna[] esquemas)
    {
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(cooperativa?.ToString());
        return new CredencialesDeCanalExternoEnArchivo(Options.Create(new ExternalChannelsOptions { CredentialsPath = _directorio }), _cache, tenant,
            db ?? TestDbContextFactory.Create(), _reloj, esquemas);
    }

    private static void Configurar(TestApplicationDbContext db, string canal, string? credentialKey)
    {
        db.ExternalChannelSettings.Add(new ExternalChannelSetting
        {
            Kind = ExternalChannelKind.Sms, SettingKey = ExternalChannelSetting.ClaveDe(ExternalChannelKind.Sms, null), ChannelCode = canal,
            ValidFrom = new DateOnly(2026, 1, 1), CredentialKey = credentialKey, Reason = "prueba",
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
        Assert.Equal($"{CooperativaA:D}.{Canal}.json", a.Value.Clave);
        Assert.Equal("a-clave", a.Value.Valores["clave"]);
        Assert.Equal($"{CooperativaB:D}.{Canal}.json", b.Value.Clave);
        Assert.Equal("b-clave", b.Value.Valores["clave"]);
        Assert.DoesNotContain("a-clave", a.Value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_cooperativa_sin_su_archivo_no_toma_el_de_otra()
    {
        var r = await Credenciales(Guid.NewGuid()).ResolverAsync(Canal, default);

        Assert.True(r.IsFailure);
        Assert.Equal(ErroresDeCanalesExternos.CodigoSinCredencial, r.Error.Code);
    }

    [Fact]
    public async Task Sin_cooperativa_resuelta_no_se_lee_nada()
    {
        var r = await Credenciales(null).ResolverAsync(Canal, default);

        Assert.True(r.IsFailure);
        Assert.Equal(ErroresDeCanalesExternos.CodigoSinCredencial, r.Error.Code);
    }

    [Theory]
    [InlineData("../PROV-SMS")]
    [InlineData("PROV/SMS")]
    [InlineData("..")]
    [InlineData("PROV\\SMS")]
    [InlineData("PROV SMS")]
    [InlineData("P")]
    [InlineData("PROVEEDOR-CON-UN-CODIGO-DEMASIADO-LARGO")]
    public async Task Un_codigo_que_podria_salir_del_directorio_se_rechaza(string codigo)
    {
        // Hay un archivo justo fuera del directorio: si el código se usara tal cual, se leería.
        var r = await Credenciales(CooperativaA).ResolverAsync(codigo, default);

        Assert.True(r.IsFailure);
        Assert.Equal(ErroresDeCanalesExternos.CodigoSinCredencial, r.Error.Code);
    }

    [Fact]
    public async Task El_codigo_en_minusculas_se_lee_en_mayusculas()
    {
        var r = await Credenciales(CooperativaA).ResolverAsync("prov-sms", default);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal($"{CooperativaA:D}.{Canal}.json", r.Value.Clave);
    }

    [Fact]
    public async Task Una_clave_guardada_que_no_es_la_derivada_es_CredentialMismatch()
    {
        var db = TestDbContextFactory.Create();
        // Alguien alteró la base para apuntar a la credencial de otra cooperativa.
        Configurar(db, Canal, CredencialDeCanalExterno.ClaveDe(CooperativaB, Canal));

        var r = await Credenciales(CooperativaA, db).ResolverAsync(Canal, default);

        Assert.True(r.IsFailure);
        Assert.Equal(ErroresDeCanalesExternos.CodigoClaveNoCoincide, r.Error.Code);
    }

    [Fact]
    public async Task Una_clave_guardada_igual_a_la_derivada_se_lee()
    {
        var db = TestDbContextFactory.Create();
        Configurar(db, Canal, CredencialDeCanalExterno.ClaveDe(CooperativaA, Canal));

        var r = await Credenciales(CooperativaA, db).ResolverAsync(Canal, default);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
    }

    [Theory]
    [InlineData(CodigosDeCanalExterno.Simulado)]
    [InlineData(CodigosDeCanalExterno.CargaPropia)]
    public async Task Los_canales_sin_credencial_no_leen_archivo(string canal)
    {
        var r = await Credenciales(CooperativaA).ResolverAsync(canal, default);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Empty(r.Value.Valores);
        Assert.Equal(0, _cache.Lecturas);
    }

    [Fact]
    public async Task La_cache_dura_cinco_minutos_y_se_invalida_si_cambia_el_archivo()
    {
        var credenciales = Credenciales(CooperativaA);
        Assert.True((await credenciales.ResolverAsync(Canal, default)).IsSuccess);
        Assert.True((await credenciales.ResolverAsync(Canal, default)).IsSuccess);
        Assert.Equal(1, _cache.Lecturas);

        // El Secret rota: cambia la huella y se relee aunque no hayan pasado los cinco minutos.
        var ruta = Escribir(CooperativaA, Canal, """{ "usuario": "a-usuario", "clave": "a-clave-rotada" }""");
        File.SetLastWriteTimeUtc(ruta, DateTime.UtcNow.AddMinutes(1));
        var rotada = await credenciales.ResolverAsync(Canal, default);
        Assert.Equal("a-clave-rotada", rotada.Value.Valores["clave"]);
        Assert.Equal(2, _cache.Lecturas);

        // Pasados los cinco minutos se relee aunque el archivo no cambie.
        _ahora = _ahora.AddMinutes(6);
        await credenciales.ResolverAsync(Canal, default);
        Assert.Equal(3, _cache.Lecturas);
    }

    [Fact]
    public async Task El_esquema_del_adaptador_se_valida_al_leer_sin_mostrar_valores()
    {
        var esquema = Substitute.For<IDeclaraEsquemaDeCredencialExterna>();
        esquema.ChannelCode.Returns(Canal);
        esquema.CamposDeCredencial.Returns(["usuario", "clave", "remitente"]);

        var r = await Credenciales(CooperativaA, null, esquema).ResolverAsync(Canal, default);

        Assert.True(r.IsFailure);
        Assert.Equal(ErroresDeCanalesExternos.CodigoSinCredencial, r.Error.Code);
        Assert.Contains("remitente", r.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("a-clave", r.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_archivo_que_no_es_json_es_un_fallo_sin_excepcion()
    {
        Escribir(CooperativaA, Canal, "esto no es json: clave-secreta");

        var r = await Credenciales(CooperativaA).ResolverAsync(Canal, default);

        Assert.True(r.IsFailure);
        Assert.DoesNotContain("clave-secreta", r.Error.Message, StringComparison.Ordinal);
    }
}
