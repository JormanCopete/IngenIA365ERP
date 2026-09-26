using FluentAssertions;
using IngenIA365ERP.Identity.Seed;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Reparto de permisos a los roles built-in.
///
/// <para>
/// Los permisos <c>Admin.Tenants.*</c> y <c>Saas.*</c> operan SOBRE las
/// cooperativas: listar todas, suspender una. No son de nadie dentro de una
/// cooperativa. Los patrones glob del sembrador los repartían solos —
/// <c>"*"</c> se los daba enteros a CompanyAdmin y <c>"*.View"</c> le daba
/// <c>Admin.Tenants.View</c> a ReadOnly— y el aprovisionamiento de cada
/// cooperativa clonaba esos vínculos. Como <c>/api/saas/tenants</c> no filtra
/// por cooperativa, el administrador de la A podía alcanzar la B.
/// </para>
///
/// <para>
/// Estaba dormido sólo porque el token central no lleva claims <c>perm</c>.
/// Estas pruebas existen para que no despierte.
/// </para>
/// </summary>
public class RepartoDePermisosTests
{
    private static readonly string[] Catalogo =
        [.. DomainPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. CorePermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. PayrollPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. AccountingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}")];

    public static TheoryData<string> RolesBuiltIn =>
        [.. BuiltInRolesSeeder.PermissionPatterns.Keys];

    [Theory]
    [MemberData(nameof(RolesBuiltIn))]
    public void NingunRolRecibePermisosSaasGlobales(string rol)
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol(rol, Catalogo);

        concedidos.Where(BuiltInRolesSeeder.EsSaasGlobal)
            .Should().BeEmpty($"«{rol}» vive dentro de una cooperativa y no puede " +
                              "administrar el conjunto de cooperativas");
    }

    [Fact]
    public void ElGlobPorSiSolo_SiConcederiaLosGlobales()
    {
        // Documenta POR QUE hace falta la lista de retencion. Si alguien la quita
        // pensando que el glob ya es seguro, esta prueba dice que no lo es.
        BuiltInRolesSeeder.MatchesGlob("*.View", "Admin.Tenants.View").Should().BeTrue();
        BuiltInRolesSeeder.MatchesGlob("*", "Admin.Tenants.Suspend").Should().BeTrue();
    }

    [Fact]
    public void CompanyAdmin_ConservaLoQueSiEsSuyo()
    {
        // La lista de retencion no puede pasarse de frenada: las sucursales son
        // de la cooperativa pese a llamarse Admin.Branches.
        var concedidos = BuiltInRolesSeeder.CodigosParaRol("CompanyAdmin", Catalogo);

        concedidos.Should().Contain("Admin.Branches.Create");
        concedidos.Should().Contain("Security.Users.AssignRole");
        concedidos.Should().NotContain("Admin.Tenants.Suspend");
    }

    [Fact]
    public void ReadOnly_SoloLectura_YSinLaDeCooperativas()
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol("ReadOnly", Catalogo);

        concedidos.Should().Contain("Admin.Branches.View");
        concedidos.Should().NotContain("Admin.Tenants.View");
        concedidos.Should().OnlyContain(c => c.EndsWith(".View"));
    }

    // ------------------------------------------------------ feature 008: maestros de persona --

    [Fact]
    public void Operator_CreaYEdita_PeroNoDaDeBaja()
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol("Operator", Catalogo);

        concedidos.Should().Contain(["Core.People.View", "Core.People.Create", "Core.People.Update",
                                     "Core.Associates.View", "Core.Associates.Create", "Core.Associates.Update",
                                     "Payroll.Employees.View", "Payroll.Employees.Create", "Payroll.Employees.Update"]);
        concedidos.Should().NotContain("Core.People.Delete", "eliminar y restaurar personas es del administrador");
        concedidos.Should().NotContain("Payroll.Employees.Terminate", "terminar contratos es del administrador");
    }

    [Theory]
    [InlineData("ReadOnly")]
    [InlineData("Auditor")]
    public void ReadOnlyYAuditor_SoloLeenLosMaestros(string rol)
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol(rol, Catalogo);

        concedidos.Should().Contain(BuiltInRolesSeeder.LecturaDeMaestros);
        concedidos.Should().NotContain(c =>
            (c.StartsWith("Core.People.") || c.StartsWith("Core.Associates.") || c.StartsWith("Payroll.Employees."))
            && !c.EndsWith(".View"));
    }

    [Fact]
    public void CompanyAdmin_PuedeTodoEnLosMaestros()
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol("CompanyAdmin", Catalogo);

        concedidos.Should().Contain(["Core.People.Delete", "Payroll.Employees.Terminate"]);
    }

    [Fact]
    public void LaLecturaDeMaestros_ExisteEnElCatalogo()
    {
        // Es lo que se concede a los roles personalizados el día del despliegue (FR-010):
        // una errata en la lista la volvería inerte sin avisar.
        foreach (var codigo in BuiltInRolesSeeder.LecturaDeMaestros)
        {
            Catalogo.Should().Contain(codigo);
        }
    }

    [Fact]
    public void ElCatalogoContableTieneLosTreintaPermisosDelContrato()
    {
        // Feature 009 (contracts/api.md §1): 30 códigos Accounting.*; la compuerta T046 los cuenta en SEC_Permissions.
        // Feature 012 (T115): +4 del lado contable del inventario (InventoryRules.View/Manage, InventoryBatches.View/Run).
        AccountingPermissionCatalogSeeder.Catalog.Should().HaveCount(34);
        AccountingPermissionCatalogSeeder.Catalog.Should().OnlyContain(p => p.Resource.StartsWith("Accounting."));
    }

    // ------------------------------------------------------------ feature 009: contabilidad --

    [Fact]
    public void Operator_RegistraBorradoresYExporta_PeroNoContabilizaNiAnulaNiCierraNiParametriza()
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol("Operator", Catalogo);

        concedidos.Should().Contain(["Accounting.Vouchers.Create", "Accounting.Reports.Export", "Accounting.Vouchers.View", "Accounting.Accounts.View"]);
        concedidos.Should().NotContain(["Accounting.Vouchers.Post", "Accounting.Vouchers.Void", "Accounting.Periods.Close", "Accounting.Periods.Reopen",
                                        "Accounting.Accounts.Manage", "Accounting.Setup.Manage", "Accounting.VoucherTypes.Manage"],
            "segregación de funciones (Q3:C): quien digita no contabiliza; cuatro ojos es opcional por empresa");
    }

    [Fact]
    public void ReadOnly_SoloVeLaContabilidad()
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol("ReadOnly", Catalogo).Where(c => c.StartsWith("Accounting.")).ToList();

        concedidos.Should().NotBeEmpty().And.OnlyContain(c => c.EndsWith(".View"));
        concedidos.Should().NotContain("Accounting.Reports.Export");
    }

    [Fact]
    public void Auditor_ExportaLosLibros_YNoEscribeNada()
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol("Auditor", Catalogo).Where(c => c.StartsWith("Accounting.")).ToList();

        concedidos.Should().Contain("Accounting.Reports.Export");
        concedidos.Where(c => !c.EndsWith(".View")).Should().BeEquivalentTo(["Accounting.Reports.Export"]);
    }

    [Fact]
    public void TodoRol_LeeElPlanDeCuentasYLosTiposDeComprobante()
    {
        // Los buscadores de cuenta de los demás módulos (cuentas por concepto, bancos…) los usa cualquier rol.
        BuiltInRolesSeeder.LecturaDeMaestros.Should().Contain(["Accounting.Accounts.View", "Accounting.VoucherTypes.View"]);
    }

    [Fact]
    public void TodaLecturaContable_LaRecibenLosCuatroRolesBuiltIn()
    {
        // Se cuenta contra el catálogo, no contra un número: la apertura sólo tiene Manage y eso está bien.
        var lecturas = AccountingPermissionCatalogSeeder.Catalog.Where(p => p.Action == "View").Select(p => $"{p.Resource}.{p.Action}").ToList();
        lecturas.Should().NotBeEmpty();

        foreach (var rol in BuiltInRolesSeeder.PermissionPatterns.Keys)
        {
            var concedidos = BuiltInRolesSeeder.CodigosParaRol(rol, Catalogo);
            concedidos.Should().Contain(lecturas, $"«{rol}» conserva la lectura de toda la contabilidad");
        }
    }

    // ------------------------------------------------------------ feature 010: nómina completa --

    /// <summary>Los once recursos de contracts/api.md §1 (feature 010).</summary>
    private static readonly string[] RecursosNomina010 =
    [
        "Payroll.ServiceBonus", "Payroll.Severance", "Payroll.Vacations", "Payroll.Settlements",
        "Payroll.BenefitBalances", "Payroll.WithholdingRate", "Payroll.Pila", "Payroll.ElectronicPayroll",
        "Payroll.Disbursement", "Payroll.CompanyPolicies", "Payroll.Holidays",
    ];

    private static List<string> Del010(IEnumerable<string> codigos) =>
        codigos.Where(c => RecursosNomina010.Any(r => c.StartsWith(r + ".", StringComparison.Ordinal))).ToList();

    [Fact]
    public void ElCatalogoDeNominaTieneLosOnceRecursosDelContrato()
    {
        // Se cuenta contra el seeder, no contra un número fijo: cada recurso tiene sus acciones
        // (§1) y todas tienen View.
        var recursos = PayrollPermissionCatalogSeeder.Catalog.Select(p => p.Resource).Distinct().ToList();
        recursos.Should().Contain(RecursosNomina010);

        foreach (var recurso in RecursosNomina010)
        {
            PayrollPermissionCatalogSeeder.Catalog.Should().Contain(p => p.Resource == recurso && p.Action == "View",
                $"«{recurso}» tiene que poder verse");
        }

        var acciones = PayrollPermissionCatalogSeeder.Catalog.ToLookup(p => p.Resource, p => p.Action);
        acciones["Payroll.ServiceBonus"].Should().BeEquivalentTo(["View", "Calculate", "Approve", "Reverse"]);
        acciones["Payroll.Severance"].Should().BeEquivalentTo(["View", "Calculate", "Approve", "Reverse", "MarkDeposited"]);
        acciones["Payroll.Vacations"].Should().BeEquivalentTo(["View", "Register", "Calculate", "Approve", "Reverse"]);
        acciones["Payroll.Settlements"].Should().BeEquivalentTo(["View", "Calculate", "Approve", "Reverse", "AdjustDeduction", "Manage"]);
        acciones["Payroll.BenefitBalances"].Should().BeEquivalentTo(["View", "Manage"]);
        acciones["Payroll.WithholdingRate"].Should().BeEquivalentTo(["View", "Calculate", "Approve"]);
        acciones["Payroll.Pila"].Should().BeEquivalentTo(["View", "Generate", "MarkUploaded", "Manage"]);
        acciones["Payroll.ElectronicPayroll"].Should().BeEquivalentTo(["View", "Generate", "Transmit", "Manage"]);
        acciones["Payroll.Disbursement"].Should().BeEquivalentTo(["View", "Generate", "MarkSent", "Manage"]);
        acciones["Payroll.CompanyPolicies"].Should().BeEquivalentTo(["View", "Manage"]);
        acciones["Payroll.Holidays"].Should().BeEquivalentTo(["View", "Manage"]);
    }

    [Fact]
    public void Operator_CalculaRegistraYGenera_PeroNoApruebaReversaTransmiteMarcaNiAdministra()
    {
        var concedidos = Del010(BuiltInRolesSeeder.CodigosParaRol("Operator", Catalogo));

        concedidos.Should().Contain([
            "Payroll.ServiceBonus.Calculate", "Payroll.Severance.Calculate",
            "Payroll.Vacations.Register", "Payroll.Vacations.Calculate",
            "Payroll.Settlements.Calculate", "Payroll.BenefitBalances.Manage",
            "Payroll.WithholdingRate.Calculate", "Payroll.Pila.Generate",
            "Payroll.ElectronicPayroll.Generate", "Payroll.Disbursement.Generate"]);

        // Segregación (FR-006): lo que decide, marca o parametriza es de otra persona.
        var prohibidas = new[] { ".Approve", ".Reverse", ".Transmit", ".MarkDeposited", ".MarkUploaded", ".MarkSent", ".AdjustDeduction" };
        concedidos.Should().NotContain(c => prohibidas.Any(p => c.EndsWith(p, StringComparison.Ordinal)),
            "quien calcula no aprueba, reversa, transmite, marca ni ajusta descuentos");
        concedidos.Should().NotContain(["Payroll.Settlements.Manage", "Payroll.Pila.Manage", "Payroll.ElectronicPayroll.Manage",
                                        "Payroll.Disbursement.Manage", "Payroll.CompanyPolicies.Manage", "Payroll.Holidays.Manage"],
            "administrar políticas, festivos, formatos, habilitación, aportante y motivos es del administrador; el único Manage del operador es el de saldos iniciales");
    }

    [Theory]
    [InlineData("Auditor")]
    [InlineData("ReadOnly")]
    public void AuditorYReadOnly_SoloVenLaNominaCompleta(string rol)
    {
        var concedidos = Del010(BuiltInRolesSeeder.CodigosParaRol(rol, Catalogo));

        concedidos.Should().NotBeEmpty().And.OnlyContain(c => c.EndsWith(".View"));
        concedidos.Should().HaveCount(RecursosNomina010.Length, "una lectura por recurso");
    }

    [Fact]
    public void CompanyAdmin_RecibeTodaLaNominaCompleta()
    {
        var todos = Del010(PayrollPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"));
        var concedidos = Del010(BuiltInRolesSeeder.CodigosParaRol("CompanyAdmin", Catalogo));

        concedidos.Should().BeEquivalentTo(todos);
    }

    [Fact]
    public void LosCatalogosNoSePisan()
    {
        Catalogo.Should().OnlyHaveUniqueItems("un código repetido entre seeders se insertaría dos veces");
    }

    [Fact]
    public void UnRolDesconocido_NoRecibeNada()
    {
        BuiltInRolesSeeder.CodigosParaRol("NoExiste", Catalogo).Should().BeEmpty();
    }

    [Fact]
    public void LosCodigosRetenidos_ExistenEnElCatalogo()
    {
        // Una errata en la lista de retencion la volveria inerte sin avisar.
        foreach (var codigo in Catalogo.Where(BuiltInRolesSeeder.EsSaasGlobal))
        {
            Catalogo.Should().Contain(codigo);
        }

        Catalogo.Count(BuiltInRolesSeeder.EsSaasGlobal)
            .Should().Be(6, "son los 5 Admin.Tenants.* mas Saas.AuditLog.Verify");
    }
}
