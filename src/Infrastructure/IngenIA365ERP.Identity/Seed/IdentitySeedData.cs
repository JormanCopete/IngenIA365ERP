using IngenIA365ERP.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

public static class IdentitySeedData
{
    // ═══════════════════════════════════════════════════
    // Roles base del sistema
    // ═══════════════════════════════════════════════════
    private static readonly (string Name, string Description, bool IsSystem)[] SystemRoles =
    [
        ("Administrator", "Acceso total al sistema", true),
        ("Auditor", "Solo lectura + reportes de auditoría", true),
        ("Accountant", "Módulo contabilidad completo", true),
        ("LoanOfficer", "Módulo cartera financiera", true),
        ("Cashier", "Módulo depósitos/caja", true),
        ("PayrollManager", "Módulo nómina", true),
        ("ReadOnly", "Solo consultas en todos los módulos", true),
    ];

    // ═══════════════════════════════════════════════════
    // Permisos base (~100 permisos cubriendo todos los módulos)
    // ═══════════════════════════════════════════════════
    private static readonly (string Module, string Feature, string Action)[] SystemPermissions =
    [
        // === Accounting (Contabilidad) ===
        ("Accounting", "ChartOfAccounts", "Read"),
        ("Accounting", "ChartOfAccounts", "Create"),
        ("Accounting", "ChartOfAccounts", "Update"),
        ("Accounting", "ChartOfAccounts", "Delete"),
        ("Accounting", "ChartOfAccounts", "Print"),
        ("Accounting", "ChartOfAccounts", "Export"),
        ("Accounting", "JournalEntries", "Read"),
        ("Accounting", "JournalEntries", "Create"),
        ("Accounting", "JournalEntries", "Update"),
        ("Accounting", "JournalEntries", "Delete"),
        ("Accounting", "JournalEntries", "Print"),
        ("Accounting", "AccountingPeriods", "Read"),
        ("Accounting", "AccountingPeriods", "Create"),
        ("Accounting", "AccountingPeriods", "Close"),
        ("Accounting", "FinancialReports", "Read"),
        ("Accounting", "FinancialReports", "Print"),
        ("Accounting", "FinancialReports", "Export"),
        ("Accounting", "CostCenters", "Read"),
        ("Accounting", "CostCenters", "Create"),
        ("Accounting", "CostCenters", "Update"),

        // === Lending (Cartera Financiera) ===
        ("Lending", "LoanPortfolios", "Read"),
        ("Lending", "LoanPortfolios", "Create"),
        ("Lending", "LoanPortfolios", "Update"),
        ("Lending", "LoanPortfolios", "Delete"),
        ("Lending", "LoanPortfolios", "Print"),
        ("Lending", "LoanApplications", "Read"),
        ("Lending", "LoanApplications", "Create"),
        ("Lending", "LoanApplications", "Approve"),
        ("Lending", "LoanApplications", "Reject"),
        ("Lending", "Transactions", "Read"),
        ("Lending", "Transactions", "Create"),
        ("Lending", "Collections", "Read"),
        ("Lending", "Collections", "Create"),
        ("Lending", "Collections", "Update"),
        ("Lending", "Restructuring", "Read"),
        ("Lending", "Restructuring", "Create"),
        ("Lending", "Guarantees", "Read"),
        ("Lending", "Guarantees", "Create"),
        ("Lending", "Guarantees", "Update"),
        ("Lending", "LoanReports", "Read"),
        ("Lending", "LoanReports", "Print"),
        ("Lending", "LoanReports", "Export"),

        // === Deposits (Depósitos/Caja) ===
        ("Deposits", "SavingsAccounts", "Read"),
        ("Deposits", "SavingsAccounts", "Create"),
        ("Deposits", "SavingsAccounts", "Update"),
        ("Deposits", "Transactions", "Read"),
        ("Deposits", "Transactions", "Create"),
        ("Deposits", "CashRegister", "Read"),
        ("Deposits", "CashRegister", "Open"),
        ("Deposits", "CashRegister", "Close"),
        ("Deposits", "Reconciliation", "Read"),
        ("Deposits", "Reconciliation", "Create"),
        ("Deposits", "DepositReports", "Read"),
        ("Deposits", "DepositReports", "Print"),

        // === Payroll (Nómina) ===
        ("Payroll", "Employees", "Read"),
        ("Payroll", "Employees", "Create"),
        ("Payroll", "Employees", "Update"),
        ("Payroll", "Employees", "Delete"),
        ("Payroll", "PayrollPeriods", "Read"),
        ("Payroll", "PayrollPeriods", "Create"),
        ("Payroll", "PayrollPeriods", "Process"),
        ("Payroll", "PayrollPeriods", "Close"),
        ("Payroll", "Deductions", "Read"),
        ("Payroll", "Deductions", "Create"),
        ("Payroll", "Deductions", "Update"),
        ("Payroll", "Benefits", "Read"),
        ("Payroll", "Benefits", "Create"),
        ("Payroll", "Benefits", "Update"),
        ("Payroll", "PayrollReports", "Read"),
        ("Payroll", "PayrollReports", "Print"),
        ("Payroll", "PayrollReports", "Export"),

        // === CDT (Certificados) ===
        ("CDT", "Certificates", "Read"),
        ("CDT", "Certificates", "Create"),
        ("CDT", "Certificates", "Update"),
        ("CDT", "Certificates", "Renew"),
        ("CDT", "Certificates", "Print"),

        // === Treasury (Tesorería) ===
        ("Treasury", "BankAccounts", "Read"),
        ("Treasury", "BankAccounts", "Create"),
        ("Treasury", "BankAccounts", "Update"),
        ("Treasury", "Payments", "Read"),
        ("Treasury", "Payments", "Create"),
        ("Treasury", "Payments", "Approve"),

        // === Core (Maestros) ===
        ("Core", "People", "Read"),
        ("Core", "People", "Create"),
        ("Core", "People", "Update"),
        ("Core", "People", "Delete"),
        ("Core", "Associates", "Read"),
        ("Core", "Associates", "Create"),
        ("Core", "Associates", "Update"),
        ("Core", "Branches", "Read"),
        ("Core", "Branches", "Create"),
        ("Core", "Branches", "Update"),
        ("Core", "Companies", "Read"),
        ("Core", "Companies", "Update"),

        // === Security (Seguridad) ===
        ("Security", "Users", "Read"),
        ("Security", "Users", "Create"),
        ("Security", "Users", "Update"),
        ("Security", "Users", "Delete"),
        ("Security", "Roles", "Read"),
        ("Security", "Roles", "Create"),
        ("Security", "Roles", "Update"),
        ("Security", "Roles", "Delete"),
        ("Security", "Permissions", "Read"),
        ("Security", "Permissions", "Assign"),
        ("Security", "AuditLog", "Read"),
        ("Security", "AuditLog", "Export"),
    ];

    // ═══════════════════════════════════════════════════
    // Role → Permission assignments
    // ═══════════════════════════════════════════════════
    private static readonly Dictionary<string, string[]> RolePermissionMap = new()
    {
        ["Administrator"] = ["*"], // All permissions
        ["Auditor"] = ["*.Read", "*.Print", "*.Export", "Security.AuditLog.*"],
        ["Accountant"] = ["Accounting.*", "Core.*.Read"],
        ["LoanOfficer"] = ["Lending.*", "Core.*.Read", "Deposits.*.Read"],
        ["Cashier"] = ["Deposits.*", "Core.*.Read"],
        ["PayrollManager"] = ["Payroll.*", "Core.*.Read"],
        ["ReadOnly"] = ["*.Read"],
    };

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ErpIdentityDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationUser>>();

        await SeedRolesAsync(roleManager, logger);
        await SeedPermissionsAsync(dbContext, logger);
        await SeedRolePermissionsAsync(dbContext, roleManager, logger);
        await SeedAdminUserAsync(userManager, logger);
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager, ILogger logger)
    {
        foreach (var (name, description, isSystem) in SystemRoles)
        {
            if (!await roleManager.RoleExistsAsync(name))
            {
                var role = new ApplicationRole
                {
                    Name = name,
                    Description = description,
                    IsSystemRole = isSystem,
                    TenantId = "system"
                };
                var result = await roleManager.CreateAsync(role);
                if (result.Succeeded)
                    logger.LogInformation("Created role: {Role}", name);
                else
                    logger.LogError("Failed to create role {Role}: {Errors}", name, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedPermissionsAsync(ErpIdentityDbContext dbContext, ILogger logger)
    {
        var existingCodes = await dbContext.Permissions
            .Select(p => p.PermissionCode)
            .ToHashSetAsync();

        var newPermissions = new List<IdentityPermission>();

        foreach (var (module, feature, action) in SystemPermissions)
        {
            var code = $"{module}.{feature}.{action}";
            if (!existingCodes.Contains(code))
            {
                newPermissions.Add(new IdentityPermission
                {
                    Module = module,
                    Feature = feature,
                    Action = action,
                    PermissionCode = code,
                    Description = $"{action} en {module}/{feature}"
                });
            }
        }

        if (newPermissions.Count > 0)
        {
            dbContext.Permissions.AddRange(newPermissions);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} permissions", newPermissions.Count);
        }
    }

    private static async Task SeedRolePermissionsAsync(
        ErpIdentityDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        ILogger logger)
    {
        var allPermissions = await dbContext.Permissions.ToListAsync();
        var existingRolePermissions = await dbContext.RolePermissions.ToListAsync();

        foreach (var (roleName, patterns) in RolePermissionMap)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null) continue;

            var matchingPermissions = GetMatchingPermissions(allPermissions, patterns);

            foreach (var permission in matchingPermissions)
            {
                var exists = existingRolePermissions.Any(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id);
                if (!exists)
                {
                    dbContext.RolePermissions.Add(new IdentityRolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permission.Id
                    });
                }
            }
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Role-permission assignments seeded");
    }

    private static List<IdentityPermission> GetMatchingPermissions(
        List<IdentityPermission> allPermissions, string[] patterns)
    {
        var result = new List<IdentityPermission>();

        foreach (var pattern in patterns)
        {
            if (pattern == "*")
            {
                return allPermissions;
            }

            foreach (var perm in allPermissions)
            {
                if (MatchesPattern(perm.PermissionCode, pattern) && !result.Contains(perm))
                    result.Add(perm);
            }
        }

        return result;
    }

    private static bool MatchesPattern(string permissionCode, string pattern)
    {
        // Patterns: "Accounting.*", "*.Read", "Security.AuditLog.*"
        var parts = pattern.Split('.');
        var codeParts = permissionCode.Split('.');

        if (parts.Length > codeParts.Length) return false;

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "*") continue;
            if (i >= codeParts.Length || parts[i] != codeParts[i]) return false;
        }

        // If pattern has fewer parts, last must be wildcard
        if (parts.Length < codeParts.Length && parts[^1] != "*") return false;

        return true;
    }

    private static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager, ILogger logger)
    {
        const string adminEmail = "admin@ingenia365.com";
        const string adminPassword = "Admin@Temporal2024!";

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin is not null) return;

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FullName = "Administrador del Sistema",
            TenantId = "dev_tenant",
            IsActive = true,
            MustChangePassword = false,
            CreatedBy = "Seed"
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Administrator");
            logger.LogInformation("Admin user seeded: {Email} (must change password on first login)", adminEmail);
        }
        else
        {
            logger.LogError("Failed to seed admin: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
