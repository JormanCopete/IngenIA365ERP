using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Auth.Login;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Identity.Auth.Common;

/// <summary>
/// Lo que pasa DESPUÉS de superar el segundo factor: cargar membresías y decidir
/// entre emitir la sesión directamente o pedir que se elija cooperativa.
///
/// <para>
/// Vivía dentro de <c>MfaVerifyCommandHandler</c>, con un comentario que decía
/// «duplicación controlada hasta que un tercer caller justifique extraer». El
/// ingreso con passkey es ese tercer llamador: verifica una firma en vez de un
/// código, pero a partir de ahí tiene que pasar exactamente lo mismo. Copiarlo
/// habría significado que arreglar un caso límite en un sitio dejara el otro
/// roto — y el caso límite de aquí es la salida del administrador maestro, que ya
/// se perdió una vez.
/// </para>
/// </summary>
public interface IEmisorDeSesionTrasSegundoFactor
{
    /// <param name="metodoDemostrado">
    /// Con qué acaba de demostrar su identidad. <c>Ninguno</c> para el código de
    /// recuperación: demuestra quién es, pero no es ninguno de los dos métodos que
    /// una cooperativa puede exigir, y por eso no satisface ninguna máscara
    /// restrictiva. Quien entra así a una cooperativa que restringe acaba
    /// inscribiendo — que es exactamente para lo que sirve el código.
    /// </param>
    Task<Result<LoginResult>> EmitirAsync(
        CentralUser user,
        MetodosMfa metodoDemostrado,
        int? codigosDeRecuperacionRestantes,
        CancellationToken ct);
}

public sealed class EmisorDeSesionTrasSegundoFactor(
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    ICentralJwtIssuer jwtIssuer,
    ICentralRefreshTokenStore refreshStore,
    IPoliticaDePlataforma politicaDePlataforma,
    IDateTimeService clock)
    : IEmisorDeSesionTrasSegundoFactor
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan ChallengeTokenLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<LoginResult>> EmitirAsync(
        CentralUser user,
        MetodosMfa metodoDemostrado,
        int? codigosDeRecuperacionRestantes,
        CancellationToken ct)
    {
        var activas = await memberships.GetActiveMembershipsAsync(user.Id, ct);

        if (activas.Count == 0)
        {
            // El maestro global no tiene membresías por diseño: gobierna el
            // conjunto de cooperativas, no pertenece a ninguna.
            //
            // Esta rama es LA SALIDA. Sin ella el maestro superaba el segundo
            // factor y recibía «no tienes acceso a ninguna empresa» sin token:
            // quedaba encerrado fuera de su propio sistema justo por haber
            // inscrito el MFA.
            if (user.IsGlobalMasterAdmin)
            {
                return await EmitirParaElMaestroAsync(
                    user, metodoDemostrado, codigosDeRecuperacionRestantes, ct);
            }

            // Perdió todas sus membresías entre el login y la verificación.
            return Result.Success(new LoginResult(
                Challenge: LoginChallenges.NoActiveMembership,
                CentralUserId: user.Id,
                Email: user.Email,
                IsGlobalMasterAdmin: user.IsGlobalMasterAdmin,
                Message: "No tienes acceso a ninguna empresa. Solicita una invitación."));
        }

        // Ninguna de sus cooperativas acepta el método con el que acaba de entrar.
        // Se contesta ANTES que nada: ni auto-seleccionar ni enseñar un selector
        // donde toda opción falla sirven de algo, y lo único que le queda es
        // inscribir un método que sí valga. Cubre a quien tiene una sola y a quien
        // tiene varias.
        var algunaLoAdmite = activas.Any(m => Admite(m, metodoDemostrado));
        if (!algunaLoAdmite)
        {
            return PedirInscripcion(user, activas);
        }

        if (activas.Count == 1)
        {
            return await EmitirOperativaAsync(
                user, activas[0], autoSeleccionada: true, metodoDemostrado,
                codigosDeRecuperacionRestantes, ct);
        }

        if (user.DefaultTenantId.HasValue)
        {
            var porDefecto = activas.FirstOrDefault(m => m.TenantId == user.DefaultTenantId.Value);

            if (porDefecto is null)
            {
                // La cooperativa por defecto ya no está entre las suyas: se limpia
                // en vez de dejarla apuntando a un sitio al que no puede entrar.
                await centralIdentity.SetDefaultTenantAsync(user.Id, null, ct);
            }
            else if (Admite(porDefecto, metodoDemostrado))
            {
                return await EmitirOperativaAsync(
                    user, porDefecto, autoSeleccionada: true, metodoDemostrado,
                    codigosDeRecuperacionRestantes, ct);
            }

            // Si existe pero no acepta su método, cae al selector para que elija una
            // que sí — y NO se limpia DefaultTenantId. La política de esa cooperativa
            // puede cambiar mañana, y borrar una preferencia que la persona eligió
            // por un rechazo de hoy es destruir un dato que nadie pidió destruir.
        }

        var desafio = jwtIssuer.IssueChallengeToken(
            centralUserId: user.Id,
            email: user.Email,
            isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            purpose: CentralJwtPurposes.TenantSelect,
            metodoMfa: metodoDemostrado,
            lifetime: ChallengeTokenLifetime);

        return Result.Success(new LoginResult(
            Challenge: LoginChallenges.TenantSelection,
            CentralUserId: user.Id,
            Email: user.Email,
            IsGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            ChallengeToken: desafio.Jwt,
            ChallengeTokenPurpose: CentralJwtPurposes.TenantSelect,
            ExpiresInSeconds: (int)ChallengeTokenLifetime.TotalSeconds,
            // Se listan TODAS, con una marca de cuáles aceptan su método. Filtrarlas
            // escondería cooperativas a las que la persona pertenece, y no saber por
            // qué desapareció una es peor que verla y leer el motivo.
            ActiveTenants: activas
                .Select(m => new ActiveTenantSummary(
                    m.TenantId, m.TenantName, m.IsTenantAdmin, Admite(m, metodoDemostrado)))
                .ToList(),
            DefaultTenantPublicId: null,
            AutoSelected: false,
            RecoveryCodesRemaining: codigosDeRecuperacionRestantes));
    }

    /// <summary>
    /// Sesión del maestro global. El token sale con <c>active_tenant_id=null</c> y
    /// <c>is_global_master_admin=true</c>, igual que el del login; la diferencia es
    /// que aquí sólo se llega tras verificar el segundo factor, que es el punto.
    /// </summary>
    /// <summary>
    /// ¿Esta cooperativa acepta el método con el que entró? Un solo sitio en este
    /// archivo, porque hay cuatro decisiones que dependen de la misma respuesta.
    /// </summary>
    private static bool Admite(ActiveMembershipInfo membresia, MetodosMfa metodoDemostrado) =>
        GuardiaDeMetodos.Evaluar(
            membresia.IsMfaRequiredByTenant,
            membresia.MetodosAceptados,
            metodoDemostrado) == GuardiaDeMetodos.Veredicto.Admite;

    /// <summary>
    /// La salida. Superó el segundo factor —eso ya está probado— pero con un método
    /// que ninguna de sus cooperativas acepta, así que se le da el token de
    /// inscripción y la lista de lo que sí le serviría.
    ///
    /// <para>
    /// Emitir un <c>mfa-enroll</c> AQUÍ es estrictamente más seguro que emitirlo en
    /// el login, que es donde ya se emite hoy: aquí la persona acaba de demostrar su
    /// identidad con un segundo factor real. Lo que no tiene es el que hace falta.
    /// </para>
    /// </summary>
    private Result<LoginResult> PedirInscripcion(
        CentralUser user, IReadOnlyList<ActiveMembershipInfo> activas)
    {
        var leServiria = GuardiaDeMetodos.LoQueLeServiria(
            activas.Select(m => (m.IsMfaRequiredByTenant, m.MetodosAceptados)));

        var desafio = jwtIssuer.IssueChallengeToken(
            centralUserId: user.Id,
            email: user.Email,
            isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            purpose: CentralJwtPurposes.MfaEnroll,
            // El token de inscripción no sella método: todavía no hay ninguno que
            // valga, que es justo el motivo por el que se emite.
            metodoMfa: MetodosMfa.Ninguno,
            lifetime: ChallengeTokenLifetime);

        return Result.Success(new LoginResult(
            Challenge: LoginChallenges.MfaEnrollmentRequired,
            CentralUserId: user.Id,
            Email: user.Email,
            IsGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            ChallengeToken: desafio.Jwt,
            ChallengeTokenPurpose: CentralJwtPurposes.MfaEnroll,
            ExpiresInSeconds: (int)ChallengeTokenLifetime.TotalSeconds,
            TenantsRequiringMfa: activas
                .Where(m => m.IsMfaRequiredByTenant)
                .Select(m => new TenantSummary(m.TenantId, m.TenantName))
                .ToList(),
            MetodosAceptados: ConversionDeMetodosMfa.ALiterales(leServiria)));
    }

    private async Task<Result<LoginResult>> EmitirParaElMaestroAsync(
        CentralUser user, MetodosMfa metodoDemostrado, int? codigosRestantes, CancellationToken ct)
    {
        // El maestro no tiene cooperativas, así que su máscara es la de la
        // plataforma. Si el método no le sirve, se DEGRADA a inscripción y nunca a
        // un fallo: es la única cuenta que no puede rescatarse por otra vía, y un
        // rechazo aquí la deja fuera del sistema entero — con ella, la capacidad de
        // desbloquear a todos los demás.
        var aceptadosPorLaPlataforma = await politicaDePlataforma.MetodosAceptadosAsync(ct);

        if (GuardiaDeMetodos.Evaluar(
                cooperativaExigeMfa: true, aceptadosPorLaPlataforma, metodoDemostrado)
            != GuardiaDeMetodos.Veredicto.Admite)
        {
            var desafioDeAlta = jwtIssuer.IssueChallengeToken(
                centralUserId: user.Id,
                email: user.Email,
                isGlobalMasterAdmin: true,
                purpose: CentralJwtPurposes.MfaEnroll,
                metodoMfa: MetodosMfa.Ninguno,
                lifetime: ChallengeTokenLifetime);

            return Result.Success(new LoginResult(
                Challenge: LoginChallenges.MfaEnrollmentRequired,
                CentralUserId: user.Id,
                Email: user.Email,
                IsGlobalMasterAdmin: true,
                ChallengeToken: desafioDeAlta.Jwt,
                ChallengeTokenPurpose: CentralJwtPurposes.MfaEnroll,
                ExpiresInSeconds: (int)ChallengeTokenLifetime.TotalSeconds,
                MetodosAceptados: ConversionDeMetodosMfa.ALiterales(aceptadosPorLaPlataforma)));
        }

        var access = jwtIssuer.IssueAccessToken(
            centralUserId: user.Id,
            email: user.Email,
            isGlobalMasterAdmin: true,
            activeTenantId: null,
            tenantAdmin: null,
            mfaVerified: true,
            metodoMfa: metodoDemostrado);

        var refresh = jwtIssuer.IssueRefreshToken();
        await refreshStore.StoreAsync(
            tokenHashHex: refresh.HashHex,
            session: new CentralRefreshSession(
                CentralUserId: user.Id,
                ActiveTenantPublicId: null,
                FamilyId: Guid.NewGuid(),
                IssuedAt: clock.UtcNow,
                IpAddress: null,
                UserAgent: null,
                ReplacedByTokenHashHex: null,
                SecurityStamp: user.SecurityStamp),
            ttl: RefreshTokenTtl,
            ct: ct);

        return Result.Success(new LoginResult(
            Challenge: LoginChallenges.None,
            CentralUserId: user.Id,
            Email: user.Email,
            IsGlobalMasterAdmin: true,
            AccessToken: access.Jwt,
            AccessTokenExpiresAt: access.ExpiresAt,
            RefreshToken: refresh.Token,
            RefreshTokenExpiresAt: refresh.ExpiresAt,
            ExpiresInSeconds: (int)(access.ExpiresAt - clock.UtcNow).TotalSeconds,
            AutoSelected: false,
            RecoveryCodesRemaining: codigosRestantes));
    }

    private async Task<Result<LoginResult>> EmitirOperativaAsync(
        CentralUser user,
        ActiveMembershipInfo membresia,
        bool autoSeleccionada,
        MetodosMfa metodoDemostrado,
        int? codigosRestantes,
        CancellationToken ct)
    {
        var access = jwtIssuer.IssueAccessToken(
            centralUserId: user.Id,
            email: user.Email,
            isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            activeTenantId: membresia.TenantId,
            tenantAdmin: membresia.IsTenantAdmin,
            mfaVerified: true,
            metodoMfa: metodoDemostrado);

        var refresh = jwtIssuer.IssueRefreshToken();
        await refreshStore.StoreAsync(
            tokenHashHex: refresh.HashHex,
            session: new CentralRefreshSession(
                CentralUserId: user.Id,
                ActiveTenantPublicId: membresia.TenantId,
                FamilyId: Guid.NewGuid(),
                IssuedAt: clock.UtcNow,
                IpAddress: null,
                UserAgent: null,
                ReplacedByTokenHashHex: null,
                SecurityStamp: user.SecurityStamp),
            ttl: RefreshTokenTtl,
            ct: ct);

        return Result.Success(new LoginResult(
            Challenge: LoginChallenges.None,
            CentralUserId: user.Id,
            Email: user.Email,
            IsGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            AccessToken: access.Jwt,
            AccessTokenExpiresAt: access.ExpiresAt,
            RefreshToken: refresh.Token,
            RefreshTokenExpiresAt: refresh.ExpiresAt,
            ExpiresInSeconds: (int)(access.ExpiresAt - clock.UtcNow).TotalSeconds,
            AutoSelected: autoSeleccionada,
            ActiveTenantPublicId: membresia.TenantId,
            ActiveTenantName: membresia.TenantName,
            DefaultTenantPublicId: user.DefaultTenantId,
            ActiveTenants: new[]
            {
                new ActiveTenantSummary(membresia.TenantId, membresia.TenantName, membresia.IsTenantAdmin)
            },
            RecoveryCodesRemaining: codigosRestantes));
    }
}
