using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Security.Users.DisableUser;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Security.Users;

/// <summary>
/// Feature 017 (T075, US8): retirar a alguien de una cooperativa corta esa membresía y no las demás, encuentra
/// la cuenta central por el vínculo de la fila (no por el correo, que pudo cambiar) y anota como autor a
/// quien retira. Que la renovación rechace después del retiro lo prueba <c>RefreshTokenCommandHandlerTests</c>
/// (sin membresía activa, <c>Membership.NotActive</c>) y la e2e de retiro con sesión abierta.
/// </summary>
public class RetiroDeCooperativaTests
{
    private static readonly DateTime Ahora = new(2026, 11, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Coop = Guid.NewGuid();
    private static readonly Guid Otra = Guid.NewGuid();
    private static readonly Guid Retirada = Guid.NewGuid();
    private static readonly Guid Administrador = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly TestAdminDbContext _admin = TestAdminDbContext.Create();
    private readonly ICentralIdentityProvider _identidad = Substitute.For<ICentralIdentityProvider>();

    private DisableUserCommandHandler Handler()
    {
        var usuario = Substitute.For<ICurrentUserService>();
        usuario.UserName.Returns("admin@coop.test");
        usuario.UserId.Returns(-1);
        var cooperativa = Substitute.For<ICurrentTenantService>();
        cooperativa.TenantId.Returns(Coop.ToString());
        var reloj = Substitute.For<IDateTimeService>();
        reloj.UtcNow.Returns(Ahora);
        var sesion = Substitute.For<ICurrentCentralUserContext>();
        sesion.CentralUserId.Returns(Administrador);
        return new DisableUserCommandHandler(_db, _admin, usuario, cooperativa, _identidad, reloj, sesionCentral: sesion);
    }

    private User UsuarioConVinculo(string correoDeLaFila)
    {
        var u = new User { Username = correoDeLaFila, Email = correoDeLaFila, PasswordHash = "x", CentralUserId = Retirada };
        _db.Users.Add(u);
        _db.SaveChanges();
        return u;
    }

    [Fact]
    public async Task Se_encuentra_por_el_vinculo_aunque_el_correo_haya_cambiado_y_el_autor_es_quien_retira()
    {
        _admin.TenantMemberships.Add(TenantMembership.CreateActive(Retirada, Coop, false, Administrador, Ahora));
        await _admin.SaveChangesAsync();
        var fila = UsuarioConVinculo("correo.viejo@coop.test");

        var r = await Handler().Handle(new DisableUserCommand(fila.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var m = await _admin.TenantMemberships.SingleAsync();
        m.Status.Should().Be(MembershipStatus.Revoked);
        m.RevokedByUserId.Should().Be(Administrador, "el autor del retiro es quien retira, no el retirado");
        await _identidad.DidNotReceive().FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retirar_de_una_cooperativa_no_toca_la_membresia_de_las_demas()
    {
        _admin.TenantMemberships.Add(TenantMembership.CreateActive(Retirada, Coop, false, Administrador, Ahora));
        _admin.TenantMemberships.Add(TenantMembership.CreateActive(Retirada, Otra, false, Administrador, Ahora));
        await _admin.SaveChangesAsync();
        var fila = UsuarioConVinculo("ana@coop.test");

        await Handler().Handle(new DisableUserCommand(fila.PublicId), default);

        var porCooperativa = await _admin.TenantMemberships.ToDictionaryAsync(x => x.TenantId, x => x.Status);
        porCooperativa[Coop].Should().Be(MembershipStatus.Revoked);
        porCooperativa[Otra].Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task Una_fila_de_antes_del_vinculo_se_encuentra_por_el_correo()
    {
        _admin.TenantMemberships.Add(TenantMembership.CreateActive(Retirada, Coop, false, Administrador, Ahora));
        await _admin.SaveChangesAsync();
        var fila = new User { Username = "ana@coop.test", Email = "ana@coop.test", PasswordHash = "x" };
        _db.Users.Add(fila);
        await _db.SaveChangesAsync();
        _identidad.FindByEmailAsync("ana@coop.test", Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = Retirada, Email = "ana@coop.test" });

        await Handler().Handle(new DisableUserCommand(fila.PublicId), default);

        (await _admin.TenantMemberships.SingleAsync()).Status.Should().Be(MembershipStatus.Revoked);
    }
}
