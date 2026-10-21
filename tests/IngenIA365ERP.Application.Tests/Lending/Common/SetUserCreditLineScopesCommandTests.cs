using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Common;

/// <summary>
/// Feature 013, T0251 (US17; T62, FR-004, SC-015; api.md §3.8; data-model §3.14): <see cref="GetLendingUserScopeQuery"/> y
/// <see cref="SetUserCreditLineScopesCommand"/>. El comando reemplaza el conjunto de líneas y da de baja lógica lo retirado; dos por defecto
/// → <c>Lending.Scope.DefaultDuplicate</c>; una línea fuera del alcance de quien asigna → el 404 de la línea (y
/// <c>Lending.Access.OutOfScope</c>); las oficinas salen de sólo lectura desde <c>SEC_UserBranchAssignments</c> con su sucursal contable.
/// </summary>
public sealed class SetUserCreditLineScopesCommandTests : IDisposable
{
    private readonly GobiernoDePrueba _g = new();
    private readonly TestAdminDbContext _admin = TestAdminDbContext.Create();
    private readonly User _asesor;
    private readonly CreditLine _consumo;
    private readonly CreditLine _vivienda;
    private readonly CreditLine _libranza;

    public SetUserCreditLineScopesCommandTests()
    {
        _g.ActuarComo(_g.Usuario(_g.Persona("Ana", "Admin", "1001"), "admin@coop.test"));
        _asesor = _g.Usuario(_g.Persona("Luis", "Asesor", "1002"), "asesor@coop.test");
        _consumo = Linea("CONSUMO", "Consumo");
        _vivienda = Linea("EDUCA", "Educativo");
        _libranza = Linea("LIBRANZA", "Libranza");
    }

    public void Dispose()
    {
        _g.Dispose();
        _admin.Dispose();
    }

    private CreditLine Linea(string codigo, string nombre)
    {
        var l = new CreditLine { Code = codigo, Name = nombre };
        _g.Db.CreditLines.Add(l);
        _g.Db.SaveChanges();
        return l;
    }

    private Task<Result<LendingUserScopeDto>> Asignar(params LineaAsignadaInput[] lineas) =>
        new SetUserCreditLineScopesCommandHandler(_g.Db, _admin, CooperativaDePrueba.Actual, _g.Alcance, _g.Auditoria, _g.Actor, _g.Reloj)
            .Handle(new SetUserCreditLineScopesCommand(_asesor.PublicId, lineas) { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task Reemplaza_el_conjunto_y_da_de_baja_logica_lo_retirado()
    {
        (await Asignar(new(_consumo.PublicId, true), new(_vivienda.PublicId, false))).IsSuccess.Should().BeTrue();

        var r = await Asignar(new(_vivienda.PublicId, true), new(_libranza.PublicId, false));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Lines.Select(l => (l.Code, l.IsDefault)).Should().Equal(("EDUCA", true), ("LIBRANZA", false));
        var filas = await _g.Db.UserCreditLineScopes.IgnoreQueryFilters().ToListAsync();
        filas.Should().HaveCount(3, "la que sigue conserva su fila");
        filas.Single(f => f.CreditLineId == _consumo.Id).IsDeleted.Should().BeTrue("lo retirado queda de baja lógica");
    }

    [Fact]
    public async Task Dos_por_defecto_es_DefaultDuplicate()
    {
        var r = await Asignar(new(_consumo.PublicId, true), new(_vivienda.PublicId, true));

        r.Error.Code.Should().Be("Lending.Scope.DefaultDuplicate");
        (await _g.Db.UserCreditLineScopes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_linea_fuera_del_alcance_de_quien_asigna_es_el_404_de_la_linea()
    {
        _g.AlcanceActual = AlcanceDeCartera.Vacio with { TodasLasOficinas = true, Lineas = new HashSet<int> { _consumo.Id } };

        var r = await Asignar(new(_consumo.PublicId, true), new(_libranza.PublicId, false));

        r.Error.Code.Should().Be("Lending.CreditLine.NotFound");
        (await _g.EventosAsync()).Should().Contain(AuditEventTypes.LendingAccessOutOfScope);
        (await _g.Db.UserCreditLineScopes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_usuario_inexistente_es_Generic_NotFound()
    {
        var r = await new SetUserCreditLineScopesCommandHandler(_g.Db, _admin, CooperativaDePrueba.Actual, _g.Alcance, _g.Auditoria, _g.Actor, _g.Reloj)
            .Handle(new SetUserCreditLineScopesCommand(Guid.NewGuid(), []) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Generic.NotFound");
    }

    [Fact]
    public async Task Las_oficinas_salen_de_solo_lectura_de_Seguridad_con_su_sucursal_contable()
    {
        var tenant = new Tenant { Name = "Coop", SchemaName = "coop", PublicId = Guid.Parse(CooperativaDePrueba.PublicIdN), IsActive = true };
        _admin.Tenants.Add(tenant);
        _admin.SaveChanges();
        var norte = new TenantBranch { TenantId = tenant.Id, Code = "N", Name = "Norte", PublicId = Guid.NewGuid() };
        var sur = new TenantBranch { TenantId = tenant.Id, Code = "S", Name = "Sur", PublicId = Guid.NewGuid() };
        _admin.TenantBranches.AddRange(norte, sur);
        _admin.SaveChanges();
        _g.Db.Branches.Add(new Branch { Name = "Sucursal Norte", LegacyCode = "SN", TenantBranchPublicId = norte.PublicId });
        _g.Db.UserBranchAssignments.AddRange(
            new UserBranchAssignment { UserId = _asesor.Id, TenantId = tenant.Id, BranchId = norte.Id, IsDefault = true, IsActive = true },
            new UserBranchAssignment { UserId = _asesor.Id, TenantId = tenant.Id, BranchId = sur.Id, IsActive = true });
        var todas = new Permission { Resource = "Lending.Scope", Action = "AllLines" };
        var rol = new Role { Code = "CARTERA-JEFE", Name = "Jefe de cartera" };
        _g.Db.AddRange(todas, rol);
        _g.Db.SaveChanges();
        _g.Db.RolePermissions.Add(new RolePermission { RoleId = rol.Id, PermissionId = todas.Id });
        _g.Db.UserRoles.Add(new UserRole { UserId = _asesor.Id, RoleId = rol.Id, AssignedBy = "test" });
        _g.Db.SaveChanges();

        var r = await new GetLendingUserScopeQueryHandler(_g.Db, _admin, CooperativaDePrueba.Actual).Handle(new GetLendingUserScopeQuery(_asesor.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.HasAllOffices.Should().BeFalse();
        r.Value.HasAllLines.Should().BeTrue();
        r.Value.Offices.Select(o => (o.Code, o.IsDefault, o.AccountingBranch?.Name)).Should().Equal(("N", true, "Sucursal Norte"), ("S", false, null));
        r.Value.User.Email.Should().Be("asesor@coop.test");
    }
}
