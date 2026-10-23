using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Identity.Seed;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 013, T0138 (T60, T61; DT §2.14; contracts/api.md §2.1–§2.4; C85, C86): el catálogo <c>Lending.*</c> que siembra la API, el
/// reparto a los roles integrados —<c>CompanyAdmin</c> sin decisiones ni voto, Operator, ReadOnly y Auditor sólo con sus
/// <c>*.View</c>— y los códigos que admiten límite de monto. Molde: <see cref="PermisosDeInventarioTests"/>.
/// </summary>
public class PermisosDeCarteraTests
{
    private static readonly string[] Catalogo =
        [.. DomainPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. CorePermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. PayrollPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. AccountingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. InventoryPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. ElectronicInvoicingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. LendingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         // Feature 015 (T0044): Recaudo, Tesorería y sus piezas de plataforma.
         .. ReceiptsAndTreasuryPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}")];

    /// <summary>DT §2.14 y api.md §2.1, tal cual, más <c>PrepaymentPreferences.Manage</c> (C85): 123; con los 39 de la feature 014, 162.</summary>
    private static readonly string[] Cartera =
    [
        "CreditLines.View", "CreditLines.Manage", "Parameters.View", "Parameters.Manage", "Scoring.View", "Scoring.Manage",
        "Attributions.View", "Attributions.Manage", "Segregation.Manage", "GoverningBodies.View", "GoverningBodies.Manage",
        "RelatedParties.Read", "RelatedParties.Manage", "RiskPolicy.Manage", "RiskPolicy.Validate", "Scopes.Manage", "Scope.AllOffices",
        "Scope.AllLines", "Templates.View", "MessageTemplates.View", "MessageTemplates.Manage",
        "Simulations.Run", "Applications.Read", "Applications.Create", "Applications.Submit", "Applications.Analyze", "Applications.Cancel",
        "PrepaymentPreferences.Manage", "Decisions.Analyst", "Decisions.Coordinator", "Decisions.Management", "Decisions.Exceptions",
        "CreditBureau.Query", "CreditBureau.ReadReports", "Sessions.Read", "Sessions.Manage", "Sessions.RecordVotes", "Sessions.Vote",
        "Sessions.Close", "Guarantees.Read", "Guarantees.Manage", "Guarantees.Release", "PromissoryNotes.Manage", "CreditLimits.Read",
        "CreditLimits.Assign", "Blocks.Manage", "NumberReservations.Read",
        "Disbursements.Execute", "Disbursements.Void", "DisbursementOrders.Void", "DisbursementFiles.Generate", "Loans.Read", "Payoffs.Read",
        "Payments.Simulate", "Payments.Reverse", "Payments.BackdateBeforeClose", "ValidationFailures.Confirm", "Integrity.Verify",
        "Messages.Read", "Messages.Reprocess", "Messages.SendNotApplicable", "Processes.View", "Processes.Run", "Reconciliation.View",
        "TestDoubles.Operate",
        "Ratings.Read", "Ratings.Override", "MonthlyClose.Execute", "MonthlyClose.Reopen", "ImpairmentModel.Manage", "ArrearsHistory.Import",
        "PortfolioEvaluations.Prepare", "PortfolioEvaluations.Apply",
        "PayrollAgreements.View", "PayrollAgreements.Manage", "PayrollDeductionFiles.Read", "PayrollDeductionFiles.Generate",
        "PayrollNotApplied.Manage", "PayrollTransfers.Manage",
        "Novelties.Read", "Novelties.Request", "Novelties.Apply", "Condonations.Request", "Condonations.Apply", "WriteOffs.Prepare",
        "WriteOffs.Apply", "DebtorEvents.Manage", "Insolvency.Manage",
        "DebtorMessages.Read", "DebtorMessages.Send", "DebtorMessages.Cancel", "Campaigns.Manage", "Collections.Read", "Collections.Assign",
        "Collections.RecordActivity", "CollectionStages.Manage", "PaymentAgreements.Create", "PaymentAgreements.Approve",
        "LegalProcesses.Manage", "PriorNotices.Read", "PriorNotices.Send", "PriorNotices.RegisterEvidence", "CreditReportClaims.Read",
        "CreditReportClaims.Manage",
        "Documents.Read", "Documents.Issue", "Documents.Reprint", "Documents.Void", "Documents.Verify", "Reports.View", "Reports.Export",
        "Reports.ExportPersonalData", "Dashboard.View", "RegulatoryFiles.Read", "RegulatoryFiles.MarkUploaded",
        "RegulatoryReports.Generate", "CreditBureauFiles.Generate",
        "Migration.Load", "Migration.AcceptDifferences", "Activation.Execute", "Alerts.View", "Alerts.Attend", "Alerts.Manage",
        // Feature 014 (T018; contracts/api.md §2): los 39 de captaciones y aportes.
        "DepositLines.View", "DepositLines.Manage", "ContributionLines.View", "ContributionLines.Manage", "Accounts.View", "Accounts.Read",
        "Accounts.Open", "Accounts.Close", "Accounts.Deposit", "Accounts.Withdraw", "Accounts.Transfer", "Accounts.Reverse",
        "Accounts.Parties.Manage", "TermDeposits.Constitute", "TermDeposits.Renew", "TermDeposits.Redeem", "TermDeposits.PayThirdParty", "NegotiatedRates.Request",
        "NegotiatedRates.Approve", "Contributions.Manage", "Holds.Manage", "Garnishments.Register", "Garnishments.Release",
        "DirectDebits.Manage", "GmfExemptions.Manage", "Charges.Manage", "Charges.Waive", "Interest.Settle", "MembershipWithdrawals.File",
        "MembershipWithdrawals.Approve", "MembershipWithdrawals.Settle", "Assembly.Register", "Assembly.Approve", "DeceasedCases.Register",
        "DeceasedCases.Approve", "Unclaimed.Manage", "LiquidityFund.View", "LiquidityFund.Manage", "Deposits.Migration", "Deposits.Activate",
        // Decisión del dueño del 2026-10-18 (D44): el reverso tardío de un pago del cruce al retiro.
        "MembershipWithdrawals.ReverseAfterPayment",
    ];

    private static bool DeCartera(string c) => c.StartsWith("Lending.", StringComparison.Ordinal);

    [Fact]
    public void El_catalogo_de_Cartera_es_exactamente_el_de_DT_2_14_mas_la_preferencia_de_abono()
    {
        Cartera.Should().HaveCount(164).And.OnlyHaveUniqueItems();
        LendingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}")
            .Should().BeEquivalentTo(Cartera.Select(c => "Lending." + c));
        LendingPermissionCatalogSeeder.Catalog.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Description));
    }

    [Fact]
    public void Las_lecturas_con_datos_del_deudor_no_usan_View()
    {
        string[] conDatosDelDeudor =
        [
            "Lending.RelatedParties", "Lending.Applications", "Lending.CreditBureau", "Lending.Sessions", "Lending.Guarantees",
            "Lending.CreditLimits", "Lending.NumberReservations", "Lending.Loans", "Lending.Payoffs", "Lending.Messages", "Lending.Ratings",
            "Lending.PayrollDeductionFiles", "Lending.Novelties", "Lending.DebtorMessages", "Lending.Collections", "Lending.PriorNotices",
            "Lending.CreditReportClaims", "Lending.Documents", "Lending.RegulatoryFiles",
            // Feature 014 (T018): las garantías de captaciones con titular (las cuentas tienen View sin titular y Read con él).
            "Lending.Accounts.Parties", "Lending.TermDeposits", "Lending.Garnishments", "Lending.MembershipWithdrawals", "Lending.DeceasedCases",
        ];

        LendingPermissionCatalogSeeder.Catalog.Where(p => conDatosDelDeudor.Contains(p.Resource))
            .Should().NotContain(p => p.Action == "View", "View sólo para catálogos, parámetros, tableros y bandejas sin datos personales");
    }

    [Fact]
    public void Los_catalogos_no_se_pisan()
    {
        Catalogo.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void CompanyAdmin_recibe_todo_Lending_salvo_decidir_votar_y_aprobar_tasa()
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol("CompanyAdmin", Catalogo).Where(DeCartera).ToList();

        concedidos.Should().NotContain(c => c.StartsWith("Lending.Decisions.", StringComparison.Ordinal), "administrar no es aprobar crédito (T61)");
        concedidos.Should().NotContain("Lending.Sessions.Vote");
        concedidos.Should().NotContain("Lending.NegotiatedRates.Approve", "administrar no es aprobar tasa (decisión del dueño del 2026-10-18, feature 014 D17)");
        concedidos.Should().HaveCount(164 - 4 - 1 - 1);
        concedidos.Should().Contain(["Lending.Sessions.Close", "Lending.Disbursements.Execute", "Lending.Activation.Execute"]);
    }

    [Theory]
    [InlineData("Operator")]
    [InlineData("ReadOnly")]
    [InlineData("Auditor")]
    public void Los_roles_integrados_solo_leen_Cartera_por_View(string rol)
    {
        var concedidos = BuiltInRolesSeeder.CodigosParaRol(rol, Catalogo).Where(DeCartera).ToList();

        concedidos.Should().NotBeEmpty().And.OnlyContain(c => c.EndsWith(".View", StringComparison.Ordinal),
            "a ningún rol integrado se le agrega escritura ni lectura con datos del deudor de Cartera");
    }

    [Fact]
    public void Las_atribuciones_operativas_de_Cartera_admiten_limite_de_monto()
    {
        PermisosLimitables.Admite("Lending.Disbursements.Execute").Should().BeTrue();
        PermisosLimitables.Admite("Lending.CreditLimits.Assign").Should().BeTrue();
        PermisosLimitables.Admite("Lending.ValidationFailures.Confirm").Should().BeTrue();
        PermisosLimitables.Admite("Lending.Payments.BackdateBeforeClose").Should().BeTrue();
        PermisosLimitables.Admite("Lending.Loans.Read").Should().BeFalse();
        // Feature 014 (decisión del dueño del 2026-10-18, D53): y el retiro y la consignación en efectivo de captaciones.
        PermisosLimitables.Admite("Lending.Accounts.Withdraw").Should().BeTrue();
        PermisosLimitables.Admite("Lending.Accounts.Deposit").Should().BeTrue();
        // Feature 014, US6 (T173): y la redención anticipada del CDAT.
        PermisosLimitables.Admite("Lending.TermDeposits.Redeem").Should().BeTrue();
        PermisosLimitables.Todos.Where(DeCartera).Should().HaveCount(7, "los cuatro de la 013 (api.md §2.4) y los tres de captaciones");
    }
}
