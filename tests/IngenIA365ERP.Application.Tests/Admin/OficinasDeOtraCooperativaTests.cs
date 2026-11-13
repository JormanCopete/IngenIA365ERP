using FluentAssertions;
using IngenIA365ERP.Application.Admin.Branches.CreateBranch;
using IngenIA365ERP.Application.Admin.Branches.ListBranches;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

using CrearAgencia = IngenIA365ERP.Application.Core.Branches.Commands.CreateBranch.CreateBranchCommand;

namespace IngenIA365ERP.Application.Tests.Admin;

/// <summary>
/// <c>POST /api/admin/branches</c> trae la cooperativa en el cuerpo y hasta el 2026-11-06 nadie la comparaba con la de
/// quien llama: un administrador creaba oficinas en otra cooperativa con sólo conocer su identificador. Desde la
/// feature 018 la cooperativa es siempre la activa, también para el maestro, y el comando ya no la recibe.
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

    private readonly IApplicationDbContext _agencias = TestDbContextFactory.Create();
    private readonly ISender _sender = Substitute.For<ISender>();

    private CreateBranchCommandHandler Crear(TestAdminDbContext db, Guid? cooperativa, bool maestro = false) =>
        new(db, _agencias, Substitute.For<ICurrentUserService>(), EnLaCooperativa(cooperativa), Usuario(maestro), _sender);

    private static readonly CreateBranchCommand Oficina = new("NTE", "Norte", null, null, null, false);
    private static readonly CreateBranchCommand OficinaConAgencia = Oficina with { CrearAgencia = true };

    [Fact]
    public async Task Un_administrador_crea_oficinas_en_su_cooperativa()
    {
        using var db = ConDosCooperativas();

        var r = await Crear(db, Alfa).Handle(Oficina, default);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await db.TenantBranches.AsNoTracking().SingleAsync()).TenantId.Should().Be(db.Tenants.Single(t => t.PublicId == Alfa).Id);
    }

    [Fact]
    public async Task Sin_cooperativa_activa_se_deniega()
    {
        using var db = ConDosCooperativas();

        var r = await Crear(db, null).Handle(Oficina, default);

        r.Error.Code.Should().Be("Session.TenantNotSelected");
        (await db.TenantBranches.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_maestro_sin_cooperativa_activa_no_crea_oficinas()
    {
        using var db = ConDosCooperativas();

        var r = await Crear(db, null, maestro: true).Handle(Oficina, default);

        r.Error.Code.Should().Be("Session.TenantNotSelected");
        (await db.TenantBranches.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_maestro_con_cooperativa_activa_crea_en_ella()
    {
        using var db = ConDosCooperativas();

        var r = await Crear(db, Beta, maestro: true).Handle(Oficina, default);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await db.TenantBranches.AsNoTracking().SingleAsync()).TenantId.Should().Be(db.Tenants.Single(t => t.PublicId == Beta).Id);
    }

    [Fact]
    public async Task El_maestro_con_cooperativa_activa_lista_solo_esa()
    {
        using var db = ConDosCooperativas();
        await Crear(db, Alfa).Handle(Oficina, default);
        await Crear(db, Beta).Handle(Oficina, default);

        var r = await new ListBranchesQueryHandler(db, EnLaCooperativa(Alfa), Usuario(maestro: true))
            .Handle(new ListBranchesQuery(null, null, false, new PageRequest()), default);

        r.Value.Items.Should().ContainSingle().Which.TenantPublicId.Should().Be(Alfa);
    }

    [Fact]
    public async Task Crear_agencia_la_vincula_a_la_oficina_nueva()
    {
        using var db = ConDosCooperativas();
        _sender.Send(Arg.Any<CrearAgencia>(), Arg.Any<CancellationToken>()).Returns(Result.Success(Guid.NewGuid()));

        var r = await Crear(db, Alfa).Handle(OficinaConAgencia, default);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        await _sender.Received(1).Send(
            Arg.Is<CrearAgencia>(c => c.Code == "NTE" && c.Name == "Norte" && c.TenantBranchPublicId == r.Value),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_pedirla_no_crea_agencia()
    {
        using var db = ConDosCooperativas();

        await Crear(db, Alfa).Handle(Oficina, default);

        await _sender.DidNotReceive().Send(Arg.Any<CrearAgencia>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_codigo_de_agencia_repetido_se_avisa_antes_de_crear_la_oficina()
    {
        using var db = ConDosCooperativas();
        _agencias.Branches.Add(new Branch { LegacyCode = "NTE", Name = "Agencia Norte" });
        await _agencias.SaveChangesAsync(default);

        var r = await Crear(db, Alfa).Handle(OficinaConAgencia, default);

        r.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        (await db.TenantBranches.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Si_la_agencia_falla_dice_que_la_oficina_quedo_creada()
    {
        using var db = ConDosCooperativas();
        _sender.Send(Arg.Any<CrearAgencia>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Guid>("Branch.OfficeAlreadyLinked", "Ya está vinculada."));

        var r = await Crear(db, Alfa).Handle(OficinaConAgencia, default);

        r.Error.Code.Should().Be("Branch.AgencyNotCreated");
        r.Error.Message.Should().Contain("La oficina quedó creada");
        (await db.TenantBranches.CountAsync()).Should().Be(1);
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
