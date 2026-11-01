using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Security.Users.AssignBranch;
using IngenIA365ERP.Application.Security.Users.Common;
using IngenIA365ERP.Application.Security.Users.GetUserByPublicId;
using IngenIA365ERP.Application.Security.Users.RemoveBranch;
using IngenIA365ERP.Application.Security.Users.SetDefaultBranch;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Users;

/// <summary>
/// Las oficinas en la ficha del usuario (2026-10-31): asignar sólo oficinas de la cooperativa de la petición (antes se
/// buscaba sólo por PublicId y se podía asignar una de otra), quitar con baja lógica, marcar la de por defecto, y el
/// detalle dice qué agencia contable da alcance cada oficina. (nuevo)
/// </summary>
public class OficinasDelUsuarioEnLaFichaTests
{
    private static readonly DateTime Ahora = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly TestAdminDbContext _admin = TestAdminDbContext.Create();
    private readonly ICurrentUserService _usuarioActual = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly int _cooperativaId;
    private readonly TenantBranch _norte, _sur, _ajena;
    private readonly User _ana;

    public OficinasDelUsuarioEnLaFichaTests()
    {
        _usuarioActual.UserName.Returns("admin@coop");
        _reloj.UtcNow.Returns(Ahora);

        var propia = new Tenant { Name = "Coop", SchemaName = "coop", PublicId = Guid.Parse(CooperativaDePrueba.PublicIdN), IsActive = true };
        var otra = new Tenant { Name = "Otra", SchemaName = "otra", PublicId = Guid.NewGuid(), IsActive = true };
        _admin.Tenants.AddRange(propia, otra);
        _admin.SaveChanges();
        _cooperativaId = propia.Id;

        _norte = new TenantBranch { TenantId = propia.Id, Code = "N", Name = "Norte", PublicId = Guid.NewGuid() };
        _sur = new TenantBranch { TenantId = propia.Id, Code = "S", Name = "Sur", PublicId = Guid.NewGuid() };
        _ajena = new TenantBranch { TenantId = otra.Id, Code = "X", Name = "De la otra", PublicId = Guid.NewGuid() };
        _admin.TenantBranches.AddRange(_norte, _sur, _ajena);
        _admin.SaveChanges();

        _ana = new User { Username = "ana", Email = "ana@coop", PasswordHash = "h", IsActive = true };
        _db.Users.Add(_ana);
        // Norte tiene agencia contable; Sur no.
        _db.Branches.Add(new Branch { Name = "Agencia Norte", TenantBranchPublicId = _norte.PublicId });
        _db.SaveChanges();
    }

    private AssignBranchCommandHandler Asignar() => new(_db, _admin, CooperativaDePrueba.Actual, _usuarioActual, _reloj);
    private RemoveBranchCommandHandler Quitar() => new(_db, _admin, CooperativaDePrueba.Actual, _usuarioActual, _reloj);
    private SetDefaultBranchCommandHandler PorDefecto() => new(_db, _admin, CooperativaDePrueba.Actual, _usuarioActual);

    private GetUserByPublicIdQueryHandler Detalle() =>
        new(_db, _admin, Substitute.For<ICentralIdentityProvider>(), CooperativaDePrueba.Actual);

    private UserBranchAssignment Fila(TenantBranch oficina) =>
        _db.UserBranchAssignments.Single(a => a.UserId == _ana.Id && a.BranchId == oficina.Id);

    [Fact]
    public async Task Asignar_una_oficina_de_otra_cooperativa_responde_como_inexistente()
    {
        var r = await Asignar().Handle(new AssignBranchCommand(_ana.PublicId, _ajena.PublicId, IsDefault: true), default);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("Generic.NotFound");
        _db.UserBranchAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task Asignar_una_oficina_propia_la_guarda_con_la_cooperativa()
    {
        var r = await Asignar().Handle(new AssignBranchCommand(_ana.PublicId, _norte.PublicId, IsDefault: true), default);

        r.IsSuccess.Should().BeTrue();
        var fila = Fila(_norte);
        fila.TenantId.Should().Be(_cooperativaId);
        fila.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Quitar_da_de_baja_la_fila_y_volver_a_asignar_la_revive()
    {
        await Asignar().Handle(new AssignBranchCommand(_ana.PublicId, _norte.PublicId, IsDefault: true), default);

        var r = await Quitar().Handle(new RemoveBranchCommand(_ana.PublicId, _norte.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        var fila = Fila(_norte);
        fila.IsDeleted.Should().BeTrue();
        fila.DeletedAt.Should().Be(Ahora);
        fila.DeletedBy.Should().Be("admin@coop");
        fila.IsDefault.Should().BeFalse("una fila de baja no puede seguir ocupando el índice de la por defecto");

        var otraVez = await Asignar().Handle(new AssignBranchCommand(_ana.PublicId, _norte.PublicId, IsDefault: false), default);
        otraVez.IsSuccess.Should().BeTrue();
        _db.UserBranchAssignments.Count(a => a.UserId == _ana.Id).Should().Be(1);
        Fila(_norte).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Quitar_una_oficina_no_asignada_o_ya_quitada_falla()
    {
        var sinAsignar = await Quitar().Handle(new RemoveBranchCommand(_ana.PublicId, _sur.PublicId), default);
        sinAsignar.Error!.Code.Should().Be(UserErrorCodes.NotAssignedBranch);

        await Asignar().Handle(new AssignBranchCommand(_ana.PublicId, _sur.PublicId, IsDefault: false), default);
        await Quitar().Handle(new RemoveBranchCommand(_ana.PublicId, _sur.PublicId), default);
        var otraVez = await Quitar().Handle(new RemoveBranchCommand(_ana.PublicId, _sur.PublicId), default);
        otraVez.Error!.Code.Should().Be(UserErrorCodes.NotAssignedBranch);
    }

    [Fact]
    public async Task Quitar_con_una_oficina_de_otra_cooperativa_responde_como_inexistente()
    {
        // Una fila que dejó el defecto anterior: no se toca desde esta cooperativa.
        _db.UserBranchAssignments.Add(new UserBranchAssignment { UserId = _ana.Id, TenantId = _ajena.TenantId, BranchId = _ajena.Id, IsActive = true });
        _db.SaveChanges();

        var r = await Quitar().Handle(new RemoveBranchCommand(_ana.PublicId, _ajena.PublicId), default);

        r.Error!.Code.Should().Be("Generic.NotFound");
        Fila(_ajena).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Marcar_por_defecto_rebaja_la_anterior()
    {
        await Asignar().Handle(new AssignBranchCommand(_ana.PublicId, _norte.PublicId, IsDefault: true), default);
        await Asignar().Handle(new AssignBranchCommand(_ana.PublicId, _sur.PublicId, IsDefault: false), default);

        var r = await PorDefecto().Handle(new SetDefaultBranchCommand(_ana.PublicId, _sur.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        Fila(_sur).IsDefault.Should().BeTrue();
        Fila(_norte).IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Marcar_por_defecto_una_oficina_no_asignada_falla()
    {
        var r = await PorDefecto().Handle(new SetDefaultBranchCommand(_ana.PublicId, _sur.PublicId), default);

        r.Error!.Code.Should().Be(UserErrorCodes.NotAssignedBranch);
    }

    [Fact]
    public async Task El_detalle_dice_la_agencia_de_cada_oficina_y_omite_las_quitadas_y_las_ajenas()
    {
        await Asignar().Handle(new AssignBranchCommand(_ana.PublicId, _norte.PublicId, IsDefault: true), default);
        await Asignar().Handle(new AssignBranchCommand(_ana.PublicId, _sur.PublicId, IsDefault: false), default);
        _db.UserBranchAssignments.Add(new UserBranchAssignment { UserId = _ana.Id, TenantId = _ajena.TenantId, BranchId = _ajena.Id, IsActive = true });
        _db.SaveChanges();

        var antes = await Detalle().Handle(new GetUserByPublicIdQuery(_ana.PublicId), default);

        antes.Value!.Branches.Select(b => (b.Code, b.IsDefault, b.AgencyName)).Should().Equal(
            ("N", true, "Agencia Norte"),
            ("S", false, (string?)null));

        await Quitar().Handle(new RemoveBranchCommand(_ana.PublicId, _sur.PublicId), default);
        var despues = await Detalle().Handle(new GetUserByPublicIdQuery(_ana.PublicId), default);
        despues.Value!.Branches.Select(b => b.Code).Should().Equal("N");
    }
}
