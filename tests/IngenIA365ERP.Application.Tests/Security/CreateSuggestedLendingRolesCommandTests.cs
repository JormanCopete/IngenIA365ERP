using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Roles.Plantillas;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Identity.Seed;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security;

/// <summary>
/// Feature 013, T0252 (US17; T61, C86; api.md §2.5, §19.4): <see cref="CreateSuggestedLendingRolesCommand"/> crea las trece plantillas
/// <c>cartera.*</c> por defecto o las pedidas, sin <c>Lending.Decisions.*</c> ni <c>Lending.Sessions.Vote</c> en <c>cartera.administrador</c>
/// (C86); repetir no duplica y las informa en <c>alreadyExisting[]</c>; una clave desconocida va en <c>omitted[]</c>; cada rol creado es de
/// la cooperativa y editable.
/// </summary>
public sealed class CreateSuggestedLendingRolesCommandTests : IDisposable
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();

    public CreateSuggestedLendingRolesCommandTests()
    {
        _usuario.TenantId.Returns("1");
        _usuario.UserName.Returns("admin@coop.test");
        var catalogo = LendingPermissionCatalogSeeder.Catalog.Select(p => (p.Resource, p.Action))
            .Concat(CorePermissionCatalogSeeder.Catalog.Select(p => (p.Resource, p.Action)))
            .Concat(DomainPermissionCatalogSeeder.Catalog.Select(p => (p.Resource, p.Action)))
            .Distinct();
        foreach (var (recurso, accion) in catalogo) _db.Permissions.Add(new Permission { Resource = recurso, Action = accion });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private Task<Result<SuggestedLendingRolesDto>> Crear(params string[] claves) =>
        new CreateSuggestedLendingRolesCommandHandler(_db, _usuario)
            .Handle(new CreateSuggestedLendingRolesCommand(claves.Length == 0 ? null : claves) { OperationKey = Guid.NewGuid() }, default);

    private async Task<IReadOnlyList<string>> PermisosDe(string codigo) =>
        await (from r in _db.Roles
               join rp in _db.RolePermissions on r.Id equals rp.RoleId
               join p in _db.Permissions on rp.PermissionId equals p.Id
               where r.Code == codigo
               select p.Resource + "." + p.Action).ToListAsync();

    [Fact]
    public async Task Sin_claves_crea_las_trece_plantillas_de_Cartera_como_roles_editables_de_la_cooperativa()
    {
        var r = await Crear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Created.Should().HaveCount(13);
        r.Value.AlreadyExisting.Should().BeEmpty();
        r.Value.Omitted.Should().BeEmpty();
        var roles = await _db.Roles.ToListAsync();
        roles.Should().HaveCount(13).And.OnlyContain(x => !x.IsBuiltIn && x.IsAssignable && x.TenantId == 1);
        roles.Select(x => x.Code).Should().Contain(["CARTERA-ADMINISTRADOR", "CARTERA-COMITE-CREDITO", "CARTERA-REVISOR-FISCAL"]);
    }

    [Fact]
    public async Task El_administrador_de_Cartera_no_decide_ni_vota()
    {
        await Crear("cartera.administrador");

        var permisos = await PermisosDe("CARTERA-ADMINISTRADOR");

        permisos.Should().NotBeEmpty();
        permisos.Should().NotContain(p => p.StartsWith("Lending.Decisions.", StringComparison.Ordinal));
        permisos.Should().NotContain("Lending.Sessions.Vote");
        permisos.Should().Contain(["Lending.Sessions.Close", "Lending.Scope.AllOffices"]);
    }

    [Fact]
    public async Task Repetir_no_duplica_y_las_informa_en_alreadyExisting()
    {
        await Crear("cartera.asesor", "cartera.analista");

        var r = await Crear("cartera.asesor", "cartera.jefe");

        r.Value.Created.Select(c => c.TemplateKey).Should().Equal("cartera.jefe");
        r.Value.AlreadyExisting.Should().Equal("cartera.asesor");
        (await _db.Roles.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Una_clave_desconocida_o_que_no_es_de_Cartera_va_en_omitted()
    {
        var r = await Crear("cartera.no-existe", "inventario.bodeguero", "cartera.gerente");

        r.Value.Omitted.Should().Equal("cartera.no-existe", "inventario.bodeguero");
        r.Value.Created.Should().ContainSingle().Which.Code.Should().Be("CARTERA-GERENTE");
    }
}
