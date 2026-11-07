using FluentAssertions;
using IngenIA365ERP.Application.Admin.Branches.CreateBranch;
using IngenIA365ERP.Application.Admin.Branches.ListBranches;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Admin;

/// <summary>
/// <c>POST /api/admin/branches</c> trae la cooperativa en el cuerpo y hasta el 2026-11-06 nadie la comparaba con la de
/// quien llama: un administrador creaba oficinas en otra cooperativa con sólo conocer su identificador. La ajena
/// responde igual que una inexistente; sólo el maestro cruza cooperativas.
/// </summary>
public class OficinasDeOtraCooperativaTests
{
    private static readonly Guid Alfa = Guid.NewGuid();
    private static readonly Guid Beta = Guid.NewGuid();

    private static TestAdminDbContext ConDosCooperativas()
    {
        var db = TestAdminDbContext.Create();
        db.Tenants.AddRange(
            new Tenant { Name = "Alfa", PublicId = Alfa, SchemaName = "coop_alfa", IsActive = true },
            new Tenant { Name = "Beta", PublicId = Beta, SchemaName = "coop_beta", IsActive = true });
        db.SaveChanges();
        return db;
    }

    private static ICurrentTenantService EnLaCooperativa(Guid? publica)
    {
        var s = Substitute.For<ICurrentTenantService>();
        s.TenantId.Returns(publica?.ToString("D"));
        return s;
    }

    private static ICurrentCentralUserContext Usuario(bool maestro)
    {
        var u = Substitute.For<ICurrentCentralUserContext>();
        u.IsGlobalMasterAdmin.Returns(maestro);
        return u;
    }

    private static CreateBranchCommandHandler Crear(TestAdminDbContext db, Guid? cooperativa, bool maestro = false) =>
        new(db, Substitute.For<ICurrentUserService>(), EnLaCooperativa(cooperativa), Usuario(maestro));

    private static CreateBranchCommand Oficina(Guid cooperativa) => new(cooperativa, "NTE", "Norte", null, null, null, false);

    [Fact]
    public async Task Un_administrador_crea_oficinas_en_su_cooperativa()
    {
        using var db = ConDosCooperativas();

        var r = await Crear(db, Alfa).Handle(Oficina(Alfa), default);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await db.TenantBranches.AsNoTracking().SingleAsync()).TenantId.Should().Be(db.Tenants.Single(t => t.PublicId == Alfa).Id);
    }

    [Fact]
    public async Task Un_administrador_no_crea_oficinas_en_otra_cooperativa()
    {
        using var db = ConDosCooperativas();

        var ajena = await Crear(db, Alfa).Handle(Oficina(Beta), default);
        var inexistente = await Crear(db, Alfa).Handle(Oficina(Guid.NewGuid()), default);

        ajena.Error.Code.Should().Be("Generic.NotFound");
        ajena.Error.Should().BeEquivalentTo(inexistente.Error, "confirmar que existe pero es ajena ya sería decir de más");
        (await db.TenantBranches.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_cooperativa_activa_se_deniega()
    {
        using var db = ConDosCooperativas();

        var r = await Crear(db, null).Handle(Oficina(Alfa), default);

        r.Error.Code.Should().Be("Session.TenantNotSelected");
        (await db.TenantBranches.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_maestro_crea_oficinas_en_cualquier_cooperativa()
    {
        using var db = ConDosCooperativas();

        var r = await Crear(db, null, maestro: true).Handle(Oficina(Beta), default);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
    }

    [Fact]
    public async Task Listar_una_cooperativa_ajena_responde_igual_que_una_inexistente()
    {
        using var db = ConDosCooperativas();
        var listar = new ListBranchesQueryHandler(db, EnLaCooperativa(Alfa), Usuario(maestro: false));

        var ajena = await listar.Handle(new ListBranchesQuery(Beta, null, false, new PageRequest()), default);
        var inexistente = await listar.Handle(new ListBranchesQuery(Guid.NewGuid(), null, false, new PageRequest()), default);
        var propia = await listar.Handle(new ListBranchesQuery(Alfa, null, false, new PageRequest()), default);

        ajena.Error.Code.Should().Be("Generic.NotFound");
        ajena.Error.Should().BeEquivalentTo(inexistente.Error);
        propia.IsSuccess.Should().BeTrue();
    }
}
