using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Security.Roles.Common;
using IngenIA365ERP.Application.Security.Roles.Plantillas;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Roles;

/// <summary>
/// Feature 012, T116 (contracts/api.md §1.5, FR-093, T48): crear un rol desde un perfil sugerido produce un rol
/// normal y editable con la intersección entre la plantilla y el catálogo sembrado de la cooperativa; lo que la
/// plantilla nombra y el catálogo no tiene vuelve en <c>omitted</c>. Es la única prueba de este comando (US12 no la
/// repite).
/// </summary>
public class CreateRoleFromTemplateCommandHandlerTests
{
    private static (CreateRoleFromTemplateCommandHandler Handler, TestApplicationDbContext Db) Build()
    {
        var db = TestDbContextFactory.Create();
        var cu = Substitute.For<ICurrentUserService>();
        cu.UserId.Returns(99);
        cu.UserName.Returns("admin@test");
        cu.TenantId.Returns("1");
        return (new CreateRoleFromTemplateCommandHandler(db, cu), db);
    }

    private static void Sembrar(TestApplicationDbContext db, params string[] codigos)
    {
        foreach (var codigo in codigos)
        {
            var corte = codigo.LastIndexOf('.');
            db.Permissions.Add(new Permission { Resource = codigo[..corte], Action = codigo[(corte + 1)..] });
        }
        db.SaveChanges();
    }

    [Fact]
    public async Task Crea_el_rol_con_los_codigos_que_existen_y_devuelve_los_omitidos()
    {
        var (handler, db) = Build();
        // Un catálogo parcial: el bodeguero pide más de lo que hay.
        Sembrar(db, "Inventory.Stock.View", "Inventory.Catalog.View", "Inventory.Transfers.View",
            "Inventory.Transfers.Create", "Users.View");

        var result = await handler.Handle(
            new CreateRoleFromTemplateCommand("inventario.bodeguero", "BodegaCentral", "Bodega central", null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : null);
        result.Value.Code.Should().Be("BodegaCentral");
        result.Value.PermissionCodes.Should().BeEquivalentTo(
            ["Inventory.Stock.View", "Inventory.Catalog.View", "Inventory.Transfers.View", "Inventory.Transfers.Create"]);
        result.Value.Omitted.Should().Contain(["Inventory.Transfers.Dispatch", "Inventory.Counts.Capture", "Inventory.Alerts.View"]);
        result.Value.Omitted.Should().NotContain(result.Value.PermissionCodes);

        var rol = db.Roles.Single();
        rol.PublicId.Should().Be(result.Value.RolePublicId);
        rol.IsBuiltIn.Should().BeFalse("una plantilla produce un rol normal y editable");
        rol.TenantId.Should().Be(1);
        db.RolePermissions.Should().HaveCount(4);
    }

    [Fact]
    public async Task Expande_Inventory_View_contra_el_catalogo_sembrado()
    {
        var (handler, db) = Build();
        Sembrar(db, "Inventory.Catalog.View", "Inventory.Catalog.Manage", "Inventory.CashSessions.View",
            "Inventory.CashSessions.ViewAll", "Inventory.Costs.Read", "Inventory.Reports.View",
            "Inventory.Reports.Export", "Inventory.Reconciliation.View", "Inventory.Scope.AllWarehouses");

        var result = await handler.Handle(
            new CreateRoleFromTemplateCommand("inventario.contador", "ContadorInv", "Contador", null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PermissionCodes.Should().BeEquivalentTo(
        [
            "Inventory.Catalog.View", "Inventory.CashSessions.View", "Inventory.Costs.Read", "Inventory.Reports.View",
            "Inventory.Reports.Export", "Inventory.Reconciliation.View", "Inventory.Scope.AllWarehouses",
        ]);
        result.Value.PermissionCodes.Should().NotContain(["Inventory.Catalog.Manage", "Inventory.CashSessions.ViewAll"]);
        result.Value.Omitted.Should().BeEmpty();
    }

    [Fact]
    public async Task Codigo_repetido_responde_CodeAlreadyExists()
    {
        var (handler, db) = Build();
        Sembrar(db, "Inventory.Stock.View");
        db.Roles.Add(new Role { TenantId = 1, Code = "BodegaCentral", Name = "Existente" });
        db.SaveChanges();

        var result = await handler.Handle(
            new CreateRoleFromTemplateCommand("inventario.bodeguero", "BodegaCentral", "Otra", null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(RoleErrorCodes.CodeAlreadyExists);
        db.Roles.Should().HaveCount(1);
    }

    [Fact]
    public async Task Plantilla_inexistente_responde_Generic_NotFound()
    {
        var (handler, db) = Build();
        Sembrar(db, "Inventory.Stock.View");

        var result = await handler.Handle(
            new CreateRoleFromTemplateCommand("inventario.noexiste", "Algo", "Algo", null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Generic.NotFound");
        db.Roles.Should().BeEmpty();
    }

    [Fact]
    public void El_validador_aplica_las_reglas_de_CreateRoleCommand()
    {
        var v = new CreateRoleFromTemplateCommandValidator();

        v.Validate(new CreateRoleFromTemplateCommand("inventario.jefe", "JefeInv", "Jefe", null)).IsValid.Should().BeTrue();
        v.Validate(new CreateRoleFromTemplateCommand("inventario.jefe", "CompanyAdmin", "X", null)).IsValid
            .Should().BeFalse("el código de un rol integrado está reservado");
        v.Validate(new CreateRoleFromTemplateCommand("inventario.jefe", "1malo", "X", null)).IsValid.Should().BeFalse();
        v.Validate(new CreateRoleFromTemplateCommand("inventario.jefe", "Bueno", "", null)).IsValid.Should().BeFalse();
        v.Validate(new CreateRoleFromTemplateCommand("", "Bueno", "Bueno", null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ListRoleTemplatesQuery_filtra_por_modulo_y_expande_contra_el_catalogo()
    {
        var db = TestDbContextFactory.Create();
        Sembrar(db, "Inventory.Catalog.View", "Inventory.Stock.View", "Inventory.Catalog.Manage");
        var handler = new ListRoleTemplatesQueryHandler(db);

        var inventario = await handler.Handle(new ListRoleTemplatesQuery("Inventory"), CancellationToken.None);
        inventario.IsSuccess.Should().BeTrue();
        inventario.Value.Select(t => t.Key).Should().BeEquivalentTo(
        [
            "inventario.administrador", "inventario.jefe", "inventario.bodeguero", "inventario.comprador",
            "inventario.cajero", "inventario.aprobador", "inventario.contador", "inventario.auditor",
        ]);
        inventario.Value.Single(t => t.Key == "inventario.auditor").PermissionCodes
            .Should().BeEquivalentTo(["Inventory.Catalog.View", "Inventory.Stock.View"],
                "los códigos van ya expandidos contra el catálogo de la cooperativa");

        var otro = await handler.Handle(new ListRoleTemplatesQuery("Payroll"), CancellationToken.None);
        otro.Value.Should().BeEmpty();

        var sinFiltro = await handler.Handle(new ListRoleTemplatesQuery(null), CancellationToken.None);
        sinFiltro.Value.Should().HaveCount(8);
    }
}
