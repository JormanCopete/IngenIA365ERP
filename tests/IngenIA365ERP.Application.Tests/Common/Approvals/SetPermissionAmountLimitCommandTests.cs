using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Approvals.ListPermissionAmountLimits;
using IngenIA365ERP.Application.Common.Approvals.SetPermissionAmountLimit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Common.Approvals;

/// <summary>
/// T015 (feature 012; T34; contracts/api.md §1.3, §15.3, §15.4; data-model §21): el monto máximo de un permiso para un
/// rol. Sólo los permisos limitables, el rol debe conceder el permiso, vigencias sin cruces y moneda COP. Feature 013
/// (T0253, T63, T65; contracts/api.md §2.4, §3.5.2): <c>PermisosLimitables</c> admite códigos por módulo —los cuatro de
/// Inventario y los cuatro operativos de Cartera—, la ruta de Cartera rechaza cualquier otro <c>Lending.*</c> con su lista y la
/// consulta filtra por módulo.
/// </summary>
public class SetPermissionAmountLimitCommandTests
{
    private const string ConfirmarAjuste = "Inventory.Adjustments.Confirm";

    private static readonly DateOnly Octubre = new(2026, 10, 1);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly Role _bodeguero;
    private readonly Role _consulta;

    public SetPermissionAmountLimitCommandTests()
    {
        var confirmar = new Permission { Resource = "Inventory.Adjustments", Action = "Confirm" };
        var credito = new Permission { Resource = "Inventory.Sales", Action = "SellOnCredit" };
        var ver = new Permission { Resource = "Inventory.Adjustments", Action = "View" };
        _bodeguero = new Role { Code = "BODEGA", Name = "Bodeguero" };
        _consulta = new Role { Code = "CONSULTA", Name = "Consulta" };
        _db.AddRange(confirmar, credito, ver, _bodeguero, _consulta);
        _db.SaveChanges();
        _db.RolePermissions.AddRange(
            new RolePermission { RoleId = _bodeguero.Id, PermissionId = confirmar.Id },
            new RolePermission { RoleId = _bodeguero.Id, PermissionId = ver.Id },
            new RolePermission { RoleId = _consulta.Id, PermissionId = ver.Id });
        _db.SaveChanges();
    }

    private Task<Result<PermissionAmountLimitDto>> Enviar(SetPermissionAmountLimitCommand c) =>
        new SetPermissionAmountLimitCommandHandler(_db).Handle(c, CancellationToken.None);

    private static SetPermissionAmountLimitCommand Alta(Guid rol, string permiso, decimal? monto, DateOnly desde, string reason = "Acta 7") =>
        new(rol, permiso, monto, desde, reason) { OperationKey = Guid.NewGuid() };

    private static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data);

    [Fact]
    public async Task Registra_el_monto_en_COP_con_el_rol()
    {
        var r = await Enviar(Alta(_bodeguero.PublicId, ConfirmarAjuste, 2_000_000m, Octubre));

        r.IsSuccess.Should().BeTrue();
        r.Value.Currency.Should().Be("COP");
        r.Value.Role.Should().Be(new RolDto(_bodeguero.PublicId, "Bodeguero"));
        r.Value.MaxAmount.Should().Be(2_000_000m);
        (await _db.PermissionAmountLimits.SingleAsync()).RoleId.Should().Be(_bodeguero.Id);
    }

    private static readonly string[] LosDeInventario =
        ["Inventory.Purchases.Confirm", "Inventory.Adjustments.Confirm", "Inventory.Sales.Confirm", "Inventory.Sales.SellOnCredit"];

    // Feature 013 (T0220, T0253; data-model parte 3 §6; contracts/api.md §2.4, §3.5.2): las cuatro atribuciones operativas de Cartera.
    private static readonly string[] LosDeCartera =
    [
        "Lending.Disbursements.Execute", "Lending.CreditLimits.Assign", "Lending.ValidationFailures.Confirm", "Lending.Payments.BackdateBeforeClose",
        // Feature 014 (decisión del dueño del 2026-10-18, D53).
        "Lending.Accounts.Withdraw", "Lending.Accounts.Deposit",
        // Feature 014, US6 (T173): la redención anticipada del CDAT.
        "Lending.TermDeposits.Redeem",
    ];

    private static IEnumerable<string?> Limitables(Error error) =>
        Datos(error).GetProperty("limitable").EnumerateArray().Select(e => e.GetString());

    [Theory]
    [InlineData("Inventory.Adjustments.View")]
    [InlineData("Inventory.Catalog.Manage")]
    public async Task Un_permiso_de_Inventario_sin_valor_es_NotLimitable_con_la_lista_de_su_modulo(string permiso)
    {
        var r = await Enviar(Alta(_bodeguero.PublicId, permiso, 10m, Octubre));

        r.Error.Code.Should().Be("Approvals.AmountLimit.NotLimitable");
        Limitables(r.Error).Should().Equal(LosDeInventario);
    }

    [Fact]
    public async Task Un_permiso_de_un_modulo_sin_limitables_es_NotLimitable_con_la_lista_completa()
    {
        var r = await Enviar(Alta(_bodeguero.PublicId, "Payroll.Runs.Approve", 10m, Octubre));

        r.Error.Code.Should().Be("Approvals.AmountLimit.NotLimitable");
        Limitables(r.Error).Should().Equal(LosDeInventario.Concat(LosDeCartera));
    }

    [Theory]
    [InlineData("Lending.Loans.Read")]
    [InlineData("Lending.Decisions.Analyst")]
    [InlineData("Lending.Disbursements.Void")]
    public async Task Cualquier_otro_permiso_de_Cartera_es_NotLimitable_con_los_cuatro_de_Cartera(string permiso)
    {
        var r = await Enviar(Alta(_bodeguero.PublicId, permiso, 10m, Octubre) with { Module = "Lending" });

        r.Error.Code.Should().Be("Approvals.AmountLimit.NotLimitable");
        Limitables(r.Error).Should().Equal(LosDeCartera);
    }

    [Fact]
    public void PermisosLimitables_admite_por_modulo_los_cuatro_de_Inventario_y_los_cuatro_de_Cartera()
    {
        PermisosLimitables.DelModulo("Inventory").Should().Equal(LosDeInventario);
        PermisosLimitables.DelModulo("Lending").Should().Equal(LosDeCartera);
        PermisosLimitables.Todos.Should().Equal(LosDeInventario.Concat(LosDeCartera));
        foreach (var p in LosDeCartera)
        {
            PermisosLimitables.Admite(p, "Lending").Should().BeTrue();
            PermisosLimitables.Admite(p, "Inventory").Should().BeFalse("la ruta de Inventario no fija montos de Cartera");
        }
        foreach (var p in LosDeInventario) PermisosLimitables.Admite(p).Should().BeTrue("los cuatro de Inventario siguen igual");
    }

    [Fact]
    public async Task Por_la_ruta_de_Cartera_registra_una_atribucion_operativa_de_Cartera()
    {
        var desembolsar = new Permission { Resource = "Lending.Disbursements", Action = "Execute" };
        _db.Add(desembolsar);
        _db.SaveChanges();
        _db.RolePermissions.Add(new RolePermission { RoleId = _bodeguero.Id, PermissionId = desembolsar.Id });
        _db.SaveChanges();

        var r = await Enviar(Alta(_bodeguero.PublicId, "Lending.Disbursements.Execute", 30_000_000m, Octubre) with { Module = "Lending" });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.PermissionCode.Should().Be("Lending.Disbursements.Execute");
    }

    [Fact]
    public async Task Por_la_ruta_de_Cartera_un_permiso_de_Inventario_es_NotLimitable_con_los_de_Cartera()
    {
        var r = await Enviar(Alta(_bodeguero.PublicId, ConfirmarAjuste, 10m, Octubre) with { Module = "Lending" });

        r.Error.Code.Should().Be("Approvals.AmountLimit.NotLimitable");
        Limitables(r.Error).Should().Equal(LosDeCartera);
    }

    [Fact]
    public async Task Un_rol_que_no_concede_el_permiso_es_RoleLacksPermission()
    {
        var sinConfirmar = await Enviar(Alta(_consulta.PublicId, ConfirmarAjuste, 10m, Octubre));
        var sinCredito = await Enviar(Alta(_bodeguero.PublicId, "Inventory.Sales.SellOnCredit", 10m, Octubre));

        sinConfirmar.Error.Code.Should().Be("Approvals.AmountLimit.RoleLacksPermission");
        sinCredito.Error.Code.Should().Be("Approvals.AmountLimit.RoleLacksPermission");
    }

    [Fact]
    public async Task Un_rol_inexistente_es_Generic_NotFound()
    {
        var r = await Enviar(Alta(Guid.NewGuid(), ConfirmarAjuste, 10m, Octubre));

        r.Error.Code.Should().Be("Generic.NotFound");
    }

    [Fact]
    public async Task Una_vigencia_posterior_o_del_mismo_dia_es_Overlaps()
    {
        await Enviar(Alta(_bodeguero.PublicId, ConfirmarAjuste, 1_000m, Octubre));

        var mismoDia = await Enviar(Alta(_bodeguero.PublicId, ConfirmarAjuste, 2_000m, Octubre));
        var antes = await Enviar(Alta(_bodeguero.PublicId, ConfirmarAjuste, 2_000m, new DateOnly(2026, 9, 1)));

        mismoDia.Error.Code.Should().Be("Approvals.AmountLimit.Overlaps");
        antes.Error.Code.Should().Be("Approvals.AmountLimit.Overlaps");
        Datos(antes.Error).GetProperty("existingValidFrom").GetString().Should().Be("2026-10-01");
    }

    [Fact]
    public async Task La_nueva_vigencia_cierra_la_anterior_la_vispera_y_nulo_es_sin_limite()
    {
        await Enviar(Alta(_bodeguero.PublicId, ConfirmarAjuste, 1_000m, Octubre));

        var r = await Enviar(Alta(_bodeguero.PublicId, ConfirmarAjuste, null, new DateOnly(2027, 1, 1)));

        r.Value.MaxAmount.Should().BeNull();
        var filas = await _db.PermissionAmountLimits.OrderBy(l => l.ValidFrom).ToListAsync();
        filas[0].ValidTo.Should().Be(new DateOnly(2026, 12, 31));
        filas[1].ValidTo.Should().BeNull();
    }

    [Fact]
    public void El_validador_exige_motivo_y_monto_no_negativo_con_dos_decimales()
    {
        var v = new SetPermissionAmountLimitCommandValidator();

        v.Validate(Alta(_bodeguero.PublicId, ConfirmarAjuste, 10m, Octubre, reason: "")).IsValid.Should().BeFalse();
        v.Validate(Alta(_bodeguero.PublicId, ConfirmarAjuste, -1m, Octubre)).IsValid.Should().BeFalse();
        v.Validate(Alta(_bodeguero.PublicId, ConfirmarAjuste, 10.005m, Octubre)).IsValid.Should().BeFalse();
        v.Validate(Alta(Guid.Empty, ConfirmarAjuste, 10m, Octubre)).IsValid.Should().BeFalse();
        v.Validate(Alta(_bodeguero.PublicId, ConfirmarAjuste, null, Octubre)).IsValid.Should().BeTrue();
        v.Validate(Alta(_bodeguero.PublicId, ConfirmarAjuste, 10.25m, Octubre)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task La_consulta_filtra_por_rol_permiso_y_fecha()
    {
        await Enviar(Alta(_bodeguero.PublicId, ConfirmarAjuste, 1_000m, Octubre));
        await Enviar(Alta(_bodeguero.PublicId, ConfirmarAjuste, 5_000m, new DateOnly(2026, 11, 1)));
        var consulta = new ListPermissionAmountLimitsQueryHandler(_db);

        var todo = await consulta.Handle(new ListPermissionAmountLimitsQuery(), CancellationToken.None);
        var octubre = await consulta.Handle(new ListPermissionAmountLimitsQuery(AsOf: new DateOnly(2026, 10, 20)), CancellationToken.None);
        var otroRol = await consulta.Handle(new ListPermissionAmountLimitsQuery(RolePublicId: _consulta.PublicId), CancellationToken.None);

        todo.Value.Select(l => l.MaxAmount).Should().Equal(1_000m, 5_000m);
        octubre.Value.Should().ContainSingle().Which.MaxAmount.Should().Be(1_000m);
        otroRol.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task La_consulta_filtra_por_modulo()
    {
        var desembolsar = new Permission { Resource = "Lending.Disbursements", Action = "Execute" };
        _db.Add(desembolsar);
        _db.SaveChanges();
        _db.RolePermissions.Add(new RolePermission { RoleId = _bodeguero.Id, PermissionId = desembolsar.Id });
        _db.SaveChanges();
        await Enviar(Alta(_bodeguero.PublicId, ConfirmarAjuste, 1_000m, Octubre));
        await Enviar(Alta(_bodeguero.PublicId, "Lending.Disbursements.Execute", 9_000m, Octubre) with { Module = "Lending" });
        var consulta = new ListPermissionAmountLimitsQueryHandler(_db);

        var cartera = await consulta.Handle(new ListPermissionAmountLimitsQuery(Module: "Lending"), CancellationToken.None);
        var inventario = await consulta.Handle(new ListPermissionAmountLimitsQuery(Module: "Inventory"), CancellationToken.None);
        var todo = await consulta.Handle(new ListPermissionAmountLimitsQuery(), CancellationToken.None);

        cartera.Value.Should().ContainSingle().Which.PermissionCode.Should().Be("Lending.Disbursements.Execute");
        inventario.Value.Should().ContainSingle().Which.PermissionCode.Should().Be(ConfirmarAjuste);
        todo.Value.Should().HaveCount(2);
    }
}
