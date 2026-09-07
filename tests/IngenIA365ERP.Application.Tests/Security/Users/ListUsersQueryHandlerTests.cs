using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Security.Users.ListUsers;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Users;

/// <summary>
/// El listado de usuarios de una cooperativa.
///
/// <para>
/// <b>Lo que fija.</b> Tres columnas —MFA, Bloqueado y Último acceso— se leían
/// de <c>SEC_Users</c>, donde ya no las escribe nadie: el segundo factor se
/// inscribe sobre la identidad central, el último acceso lo sella ella, y el
/// bloqueo por intentos vive en Redis con clave por correo. La pantalla decía
/// «MFA: No» y último acceso en blanco para todo el mundo, con aspecto de dato
/// real. Quien administra usuarios tomaba decisiones con eso.
/// </para>
/// </summary>
public class ListUsersQueryHandlerTests
{
    private static readonly Guid IdentidadDeAna = Guid.NewGuid();

    private readonly ICentralIdentityProvider _central = Substitute.For<ICentralIdentityProvider>();
    private readonly ILoginAttemptCounter _intentos = Substitute.For<ILoginAttemptCounter>();

    private static readonly DateTime UltimoAcceso = new(2026, 8, 20, 9, 30, 0, DateTimeKind.Utc);

    /// <summary>Ana con identidad central; Beto sin ella (fila heredada).</summary>
    private IApplicationDbContextAndHandler Preparar()
    {
        var db = TestDbContextFactory.Create();
        db.Users.AddRange(
            new User { Id = 1, PublicId = Guid.NewGuid(), Username = "ana", Email = "ana@demo.test", IsActive = true, CentralUserId = IdentidadDeAna },
            new User { Id = 2, PublicId = Guid.NewGuid(), Username = "beto", Email = "beto@demo.test", IsActive = true, CentralUserId = null });
        ((DbContext)db).SaveChanges();

        _central.GetSecuritySnapshotsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, CentralUserSecuritySnapshot>
            {
                [IdentidadDeAna] = new(TwoFactorEnabled: true, LastLoginAt: UltimoAcceso),
            });

        _intentos.CheckAsync(Arg.Any<AmbitoDeIntentos>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LoginLockoutState(IsLocked: false, RetryAfterSeconds: 0, FailureCount: 0));

        return new IApplicationDbContextAndHandler(
            db, new ListUsersQueryHandler(db, _central, _intentos));
    }

    private sealed record IApplicationDbContextAndHandler(
        IngenIA365ERP.Application.Common.Interfaces.IApplicationDbContext Db,
        ListUsersQueryHandler Handler);

    private static ListUsersQuery Todos() =>
        new(Search: null, RoleCode: null, IncludeDisabled: false, Paging: new PageRequest());

    [Fact]
    public async Task El_segundo_factor_y_el_ultimo_acceso_salen_de_la_identidad_central()
    {
        var (_, handler) = Preparar();

        var result = await handler.Handle(Todos(), default);

        var ana = result.Value!.Items.Single(u => u.Username == "ana");
        ana.IsMfaEnabled.Should().BeTrue("Ana tiene MFA inscrito en la identidad central");
        ana.LastLoginAt.Should().Be(UltimoAcceso);
    }

    [Fact]
    public async Task Sin_identidad_central_se_dice_que_no_se_sabe_y_no_que_NO()
    {
        // El punto entero. Devolver false aquí es exactamente lo que hacía la
        // versión anterior con TODAS las filas: afirmaba que nadie tenía segundo
        // factor, que es una afirmación, no un hueco.
        var (_, handler) = Preparar();

        var result = await handler.Handle(Todos(), default);

        var beto = result.Value!.Items.Single(u => u.Username == "beto");
        beto.IsMfaEnabled.Should().BeNull();
        beto.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public async Task El_bloqueo_se_consulta_al_contador_real_por_correo_normalizado()
    {
        var (_, handler) = Preparar();
        _intentos.CheckAsync(AmbitoDeIntentos.Password, "ANA@DEMO.TEST", Arg.Any<CancellationToken>())
            .Returns(new LoginLockoutState(IsLocked: true, RetryAfterSeconds: 42, FailureCount: 5));

        var result = await handler.Handle(Todos(), default);

        result.Value!.Items.Single(u => u.Username == "ana").IsLocked.Should().BeTrue(
            "el bloqueo vive en Redis; SEC_Users.LockoutEndAt no lo rellena nadie");
        result.Value.Items.Single(u => u.Username == "beto").IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Solo_se_consulta_la_identidad_central_de_quien_la_tiene()
    {
        // Un id nulo en la consulta por lote traería filas ajenas o ninguna;
        // en cualquier caso es una consulta que no hay que hacer.
        var (_, handler) = Preparar();

        await handler.Handle(Todos(), default);

        await _central.Received(1).GetSecuritySnapshotsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(IdentidadDeAna)),
            Arg.Any<CancellationToken>());
    }
}
