using FluentAssertions;
using IngenIA365ERP.Application.Security.Roles.Plantillas;
using IngenIA365ERP.Identity.Seed;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 012, T114 (decisiones-transversales §2.10, T48; contracts/api.md §1): el catálogo que siembra la API
/// para el comercio, el reparto a los roles integrados —sólo lectura, nunca escritura de inventario— y las ocho
/// plantillas de rol sugeridas. En el molde de <see cref="RepartoDePermisosTests"/>.
/// </summary>
public class PermisosDeInventarioTests
{
    private static readonly string[] Catalogo =
        [.. DomainPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. CorePermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. PayrollPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. AccountingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. InventoryPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. ElectronicInvoicingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}")];

    /// <summary>§2.10, tal cual: los códigos <c>Inventory.*</c>.</summary>
    private static readonly string[] Inventario =
    [
        "Catalog.View", "Catalog.Manage", "Catalog.Import", "Catalog.Export", "Catalog.ReclassifyAccountingGroup",
        "Salespeople.View", "Salespeople.Manage",
        "Warehouses.View", "Warehouses.Manage", "Warehouses.Activate", "Warehouses.AcceptActivationDifference",
        "Stock.View", "Costs.Read", "Costing.Manage",
        "Integrity.Verify", "Integrity.Rebuild",
        "Periods.View", "Periods.Close", "Periods.Reopen", "Periods.AcceptUnbilledShipments",
        "DocumentTypes.View", "DocumentTypes.Manage", "DocumentTypes.DisableFiscalPosting",
        "Documents.View", "Documents.Reprint",
        "Parameters.View", "Parameters.Manage",
        "ApprovalPolicies.View", "ApprovalPolicies.Manage",
        "Approvals.View", "Approvals.Supervisor", "Approvals.Management",
        "Scopes.Manage", "Scope.AllWarehouses", "Scope.AllPointsOfSale",
        "Adjustments.View", "Adjustments.Create", "Adjustments.Confirm", "Adjustments.Approve", "Adjustments.Void", "Adjustments.SetUnitCost",
        "Transfers.View", "Transfers.Create", "Transfers.Dispatch", "Transfers.Receive", "Transfers.Approve", "Transfers.Void",
        "Counts.View", "Counts.Open", "Counts.Capture", "Counts.Close", "Counts.Approve",
        "Purchases.View", "Purchases.Create", "Purchases.Confirm", "Purchases.Approve", "Purchases.Void",
        "Purchases.RegisterRadianEvent", "Purchases.EmitRadianEvent",
        "Sales.View", "Sales.Create", "Sales.Confirm", "Sales.Approve", "Sales.Void", "Sales.SellOnCredit", "Sales.RefundOtherMeans",
        "Pos.Sell", "Discounts.Authorize", "Prices.View", "Prices.Manage", "DiscountCaps.Manage",
        "PointsOfSale.View", "PointsOfSale.Manage",
        "CashSessions.View", "CashSessions.ViewAll", "CashSessions.Open", "CashSessions.Close",
        "CashMovements.Create", "CashMovements.Approve", "CashDifferences.Approve",
        "DayClose.Execute", "DayClose.Reopen",
        "OpeningBalance.Load", "OpeningBalance.Approve", "LegacyFigures.Import",
        "Messages.View", "Messages.Reprocess", "Messages.SendNotApplicable",
        "Reconciliation.View", "Dashboard.View",
        "Reports.View", "Reports.Export", "Reports.ExportPersonalData",
        "Alerts.View", "Alerts.Attend", "Alerts.Manage",
    ];

    private static readonly string[] FacturacionElectronica =
    [
        "Settings.View", "Settings.Manage", "Resolutions.View", "Resolutions.Manage", "Documents.View",
        "Documents.Transmit", "Documents.Correct", "Documents.TransmitByCurrentChannel", "Contingencies.View",
        "Contingencies.Declare",
    ];

    private static bool DelComercio(string c) =>
        c.StartsWith("Inventory.", StringComparison.Ordinal)
        || c.StartsWith("ElectronicInvoicing.", StringComparison.Ordinal)
        || c.StartsWith("Core.Taxes.", StringComparison.Ordinal)
        || c.StartsWith("Core.PaymentMeans.", StringComparison.Ordinal)
        || c.StartsWith("Accounting.InventoryRules.", StringComparison.Ordinal)
        || c.StartsWith("Accounting.InventoryBatches.", StringComparison.Ordinal);

    [Fact]
    public void El_catalogo_de_inventario_es_exactamente_el_de_la_seccion_2_10()
    {
        InventoryPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}")
            .Should().BeEquivalentTo(Inventario.Select(c => "Inventory." + c));
        InventoryPermissionCatalogSeeder.Catalog.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Description));
    }

    [Fact]
    public void El_catalogo_de_facturacion_electronica_es_exactamente_el_de_la_seccion_2_10()
    {
        ElectronicInvoicingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}")
            .Should().BeEquivalentTo(FacturacionElectronica.Select(c => "ElectronicInvoicing." + c));
    }

    [Fact]
    public void Los_codigos_de_otros_modulos_van_en_su_seeder_de_siempre()
    {
        CorePermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}").Should().Contain(
            ["Core.Taxes.View", "Core.Taxes.Manage", "Core.PaymentMeans.View", "Core.PaymentMeans.Manage"]);
        AccountingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}").Should().Contain(
            ["Accounting.InventoryRules.View", "Accounting.InventoryRules.Manage",
             "Accounting.InventoryBatches.View", "Accounting.InventoryBatches.Run"]);
        DomainPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}").Should().Contain("AuditLog.VerifyIntegrity");
    }

    [Fact]
    public void Los_catalogos_no_se_pisan()
    {
        Catalogo.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("Operator")]
    [InlineData("ReadOnly")]
    [InlineData("Auditor")]
    public void Los_roles_integrados_solo_leen_el_comercio(string rol)
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol(rol, Catalogo).Where(DelComercio).ToList();

        concedidos.Should().NotBeEmpty().And.OnlyContain(c => c.EndsWith(".View", StringComparison.Ordinal),
            "a ningún rol integrado se le agrega escritura de inventario (contracts/api.md §1.2)");
        concedidos.Should().NotContain(["Inventory.Costs.Read", "Inventory.Scope.AllWarehouses", "Inventory.CashSessions.ViewAll",
                                        "Inventory.Reports.Export"], "las lecturas sensibles usan otra acción que View");
    }

    [Fact]
    public void El_auditor_recibe_la_verificacion_de_integridad_por_su_glob()
    {
        BuiltInRolesSeeder.CodigosParaRol("Auditor", Catalogo).Should().Contain("AuditLog.VerifyIntegrity");
        BuiltInRolesSeeder.CodigosParaRol("Operator", Catalogo).Should().NotContain("AuditLog.VerifyIntegrity");
    }

    [Fact]
    public void CompanyAdmin_recibe_todo_el_comercio()
    {
        var todo = Catalogo.Where(DelComercio).ToList();
        BuiltInRolesSeeder.CodigosParaRol("CompanyAdmin", Catalogo).Where(DelComercio).Should().BeEquivalentTo(todo);
    }

    // ---------------------------------------------------------------------------- perfiles sugeridos --

    [Fact]
    public void Hay_ocho_plantillas_de_inventario()
    {
        PerfilesSugeridos.Todos.Where(p => p.Module == "Inventory").Select(p => p.Key).Should().BeEquivalentTo(
        [
            "inventario.administrador", "inventario.jefe", "inventario.bodeguero", "inventario.comprador",
            "inventario.cajero", "inventario.aprobador", "inventario.contador", "inventario.auditor",
        ]);
        PerfilesSugeridos.Todos.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Actor) && !string.IsNullOrWhiteSpace(p.Notes));
    }

    [Fact]
    public void Con_el_catalogo_completo_ninguna_plantilla_omite_codigos()
    {
        foreach (var plantilla in PerfilesSugeridos.Todos)
        {
            var (codigos, omitidos) = PerfilesSugeridos.Expandir(plantilla, Catalogo);
            omitidos.Should().BeEmpty($"«{plantilla.Key}» sólo nombra códigos del catálogo sembrado");
            codigos.Should().NotBeEmpty().And.OnlyHaveUniqueItems();
            codigos.Should().OnlyContain(c => Catalogo.Contains(c));
        }
    }

    [Fact]
    public void El_aprobador_no_crea_ni_confirma()
    {
        var (codigos, _) = PerfilesSugeridos.Expandir(PerfilesSugeridos.Buscar("inventario.aprobador")!, Catalogo);

        codigos.Should().NotContain(c => c.EndsWith(".Create", StringComparison.Ordinal) || c.EndsWith(".Confirm", StringComparison.Ordinal));
        codigos.Should().Contain(["Inventory.Adjustments.Approve", "Inventory.Approvals.Supervisor", "Inventory.Stock.View"]);
    }

    [Fact]
    public void El_administrador_trae_todo_el_modulo_con_impuestos_medios_de_pago_y_facturacion()
    {
        var (codigos, _) = PerfilesSugeridos.Expandir(PerfilesSugeridos.Buscar("inventario.administrador")!, Catalogo);

        codigos.Should().Contain(Inventario.Select(c => "Inventory." + c));
        codigos.Should().Contain(FacturacionElectronica.Select(c => "ElectronicInvoicing." + c));
        codigos.Should().Contain(["Core.Taxes.Manage", "Core.PaymentMeans.Manage"]);
        codigos.Should().NotContain(c => c.StartsWith("Accounting.", StringComparison.Ordinal));
    }

    [Fact]
    public void Contador_y_auditor_no_escriben()
    {
        var permitidasFueraDeView = new[]
        {
            "Inventory.Costs.Read", "Inventory.Reports.Export", "Inventory.Integrity.Verify", "Inventory.CashSessions.ViewAll",
            "Inventory.Scope.AllWarehouses", "Inventory.Scope.AllPointsOfSale", "AuditLog.Export", "AuditLog.VerifyIntegrity",
        };
        foreach (var clave in new[] { "inventario.contador", "inventario.auditor" })
        {
            var (codigos, _) = PerfilesSugeridos.Expandir(PerfilesSugeridos.Buscar(clave)!, Catalogo);
            codigos.Where(c => !c.EndsWith(".View", StringComparison.Ordinal))
                .Should().OnlyContain(c => permitidasFueraDeView.Contains(c), $"«{clave}» es de sólo consulta");
        }
    }

    [Fact]
    public void Inventory_View_no_alcanza_las_lecturas_sensibles()
    {
        var (codigos, _) = PerfilesSugeridos.Expandir(
            new PerfilSugerido("prueba", "Inventory", "x", "x", ["Inventory.*.View"], "x"), Catalogo);

        codigos.Should().Contain("Inventory.CashSessions.View").And.NotContain("Inventory.CashSessions.ViewAll");
        codigos.Should().OnlyContain(c => c.StartsWith("Inventory.") && c.EndsWith(".View"));
    }
}
