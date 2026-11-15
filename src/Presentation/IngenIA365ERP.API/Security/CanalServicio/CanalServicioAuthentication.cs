using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace IngenIA365ERP.API.Security.CanalServicio;

/// <summary>
/// Feature 021 (research R2; T017): la configuración del token de servicio. Todo llega por configuración del ambiente (Secrets);
/// nada de esto va en archivos del repositorio.
/// </summary>
public sealed class CanalServicioOpciones
{
    public const string Seccion = "CanalServicio";

    /// <summary>Llaves públicas ES256 del canal en PEM, por <c>kid</c> (varias para rotar).</summary>
    public Dictionary<string, string> LlavesPublicas { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Llave privada ES256 del ERP en PEM, con la que firma hacia el canal.</summary>
    public string? LlavePrivadaErp { get; set; }

    /// <summary>El <c>kid</c> de la llave privada del ERP.</summary>
    public string KidErp { get; set; } = "erp1";

    /// <summary>La URL interna del servicio del canal (la API interna <c>/internal/v1</c>).</summary>
    public string? UrlInterna { get; set; }
}

/// <summary>
/// Feature 021 (T017): registra el esquema <see cref="PeticionDelCanal.Esquema"/> (JwtBearer ES256) junto al de usuarios. Un esquema
/// de política elige por ruta: <c>/api/canal/v1</c> autentica con el del canal y todo lo demás con el de usuarios; así ninguna ruta
/// de servicio acepta el JWT de una persona y ninguna otra ruta acepta el token del canal.
/// </summary>
public static class CanalServicioAuthentication
{
    private const string EsquemaSelector = "ErpOCanal";

    public static IServiceCollection AddCanalServicio(this IServiceCollection services, IConfiguration configuration)
    {
        var opciones = configuration.GetSection(CanalServicioOpciones.Seccion).Get<CanalServicioOpciones>() ?? new CanalServicioOpciones();
        services.Configure<CanalServicioOpciones>(configuration.GetSection(CanalServicioOpciones.Seccion));
        services.AddMemoryCache();
        services.AddSingleton<IAlmacenDeJti, AlmacenDeJti>();

        var llaves = LlavesPublicas(opciones);

        services.AddAuthentication()
            .AddJwtBearer(PeticionDelCanal.Esquema, o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    AuthenticationType = PeticionDelCanal.Esquema,
                    ValidateIssuer = true,
                    ValidIssuer = PeticionDelCanal.EmisorDelCanal,
                    ValidateAudience = true,
                    ValidAudience = PeticionDelCanal.AudienciaDelErp,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    IssuerSigningKeys = llaves,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidarAsync,
                    OnAuthenticationFailed = ctx =>
                    {
                        ctx.HttpContext.Items[IngenIA365ERP.Identity.DependencyInjection.ClaveDelFalloDeAutenticacion] = ctx.Exception;
                        return Task.CompletedTask;
                    },
                };
            })
            .AddPolicyScheme(EsquemaSelector, "Usuarios del ERP o servicio del canal", o =>
            {
                o.ForwardDefaultSelector = http => PeticionDelCanal.EsRutaDelCanal(http.Request.Path.Value)
                    ? PeticionDelCanal.Esquema
                    : JwtBearerDefaults.AuthenticationScheme;
            });

        // Se aplica después de la configuración de AddIdentityServices: el selector pasa a ser el esquema por defecto.
        services.Configure<AuthenticationOptions>(o =>
        {
            o.DefaultAuthenticateScheme = EsquemaSelector;
            o.DefaultChallengeScheme = EsquemaSelector;
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(PeticionDelCanal.Politica, p => p
                .AddAuthenticationSchemes(PeticionDelCanal.Esquema)
                .RequireAuthenticatedUser()
                .RequireClaim(JwtRegisteredClaimNames.Sub)
                .RequireClaim(JwtRegisteredClaimNames.Jti));

        return services;
    }

    /// <summary>Vida máxima de 60 s, <c>jti</c> presente y de un solo uso.</summary>
    private static async Task ValidarAsync(TokenValidatedContext ctx)
    {
        var token = ctx.SecurityToken as Microsoft.IdentityModel.JsonWebTokens.JsonWebToken;
        var emitido = token?.IssuedAt ?? DateTime.MinValue;
        var vence = token?.ValidTo ?? DateTime.MaxValue;
        if (emitido == DateTime.MinValue || vence - emitido > PeticionDelCanal.VidaMaxima + TimeSpan.FromSeconds(5))
        {
            ctx.Fail("Channel.Service.TokenLifetime: el token de servicio debe declarar iat y durar a lo sumo 60 segundos.");
            return;
        }

        var jti = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(jti) || jti.Length > 128)
        {
            ctx.Fail("Channel.Service.MissingJti: el token de servicio no trae jti.");
            return;
        }

        var almacen = ctx.HttpContext.RequestServices.GetRequiredService<IAlmacenDeJti>();
        if (!await almacen.RegistrarAsync(PeticionDelCanal.EmisorDelCanal, jti, ctx.HttpContext.RequestAborted))
        {
            ctx.Fail("Channel.Service.ReplayedToken: el token de servicio ya se usó.");
        }
    }

    /// <summary>Las llaves públicas del canal por <c>kid</c>. Sin llaves, ningún token del canal valida (el ERP funciona sin canal).</summary>
    private static List<SecurityKey> LlavesPublicas(CanalServicioOpciones opciones)
    {
        var llaves = new List<SecurityKey>();
        foreach (var (kid, pem) in opciones.LlavesPublicas)
        {
            if (string.IsNullOrWhiteSpace(pem)) continue;
            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(pem);
            llaves.Add(new ECDsaSecurityKey(ecdsa) { KeyId = kid });
        }
        return llaves;
    }
}
