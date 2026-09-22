using FluentValidation;
using MediatR;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio VIII — Validación dual: cada Command/Query
/// <strong>de los módulos cubiertos por la Fase 0 en adelante</strong>
/// que sea <see cref="IRequest{TResponse}"/> tiene un
/// <see cref="AbstractValidator{T}"/> hermano en el mismo assembly.
///
/// Scope intencional: los módulos legacy migrados desde SOLIDO
/// (Accounting, Lending, Payroll, Inventory, CDT, Debit, Treasury) tienen
/// commands sin validator — deuda técnica conocida que se documenta
/// fuera del alcance de esta fase. Cuando un módulo legacy entre a una
/// fase nueva, se añade a la lista de namespaces cubiertos.
/// </summary>
public class PrincipioVIII_DualValidation
{
    // Namespaces creados por Fase 0 (Cimientos) y posteriores fases planeadas
    // que deben respetar el principio. Cualquier módulo legacy se mantiene
    // fuera para no romper el pipeline por la deuda heredada.
    private static readonly string[] InScopeNamespacePrefixes =
    [
        "IngenIA365ERP.Application.Security.Auth",
        "IngenIA365ERP.Application.Security.Users",
        "IngenIA365ERP.Application.Security.Roles",
        "IngenIA365ERP.Application.Security.Permissions",
        "IngenIA365ERP.Application.Security.PasswordPolicy",
        "IngenIA365ERP.Application.Admin",
        "IngenIA365ERP.Application.Audit",
        "IngenIA365ERP.Application.Attachments",
        "IngenIA365ERP.Application.Compliance",
        // Notifications cubre el contrato cross-cutting; el stub SendNotificationCommand
        // se exenta porque su validation lo hace el handler real (T118, US6).

        // Feature 005 — nomina nueva (novedades y liquidacion). El modulo Payroll
        // legado sigue fuera; estos namespaces son los que nacen con la feature y
        // entran con validador desde el primer comando.
        "IngenIA365ERP.Application.Payroll.Plans",
        "IngenIA365ERP.Application.Payroll.Novelties",
        "IngenIA365ERP.Application.Payroll.Runs",
        "IngenIA365ERP.Application.Payroll.Payments",
        "IngenIA365ERP.Application.Payroll.Payslips",
        "IngenIA365ERP.Application.Payroll.Concepts",
        "IngenIA365ERP.Application.Payroll.LegalParameters",
        "IngenIA365ERP.Application.Payroll.EmployeeTax",

        // Feature 009 - contabilidad reescrita: nace con validador en cada request.
        "IngenIA365ERP.Application.Accounting",

        // Feature 010 - nomina completa (R12): los namespaces entran aqui en el primer commit,
        // antes de que exista el primer comando, para que ninguno nazca sin validador.
        "IngenIA365ERP.Application.Payroll.Settlements",
        "IngenIA365ERP.Application.Payroll.Vacations",
        "IngenIA365ERP.Application.Payroll.Terminations",
        "IngenIA365ERP.Application.Payroll.WithholdingRates",
        "IngenIA365ERP.Application.Payroll.Pila",
        "IngenIA365ERP.Application.Payroll.ElectronicPayroll",
        "IngenIA365ERP.Application.Payroll.Dispersion",
        "IngenIA365ERP.Application.Payroll.Policies",
        "IngenIA365ERP.Application.Payroll.Holidays",
        "IngenIA365ERP.Application.Payroll.OpeningBalances",
    ];

    private static readonly HashSet<string> AllowedWithoutValidator =
    [
        "IngenIA365ERP.Application.Notifications.Contracts.SendNotificationCommand"
    ];

    [Fact]
    public void Every_request_in_phase0_scope_has_a_FluentValidation_validator()
    {
        var asm = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly;

        var requests = asm.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType
                && (i.GetGenericTypeDefinition() == typeof(IRequest<>) || i.GetGenericTypeDefinition() == typeof(IRequest))))
            .Where(t => InScopeNamespacePrefixes.Any(p =>
                t.Namespace?.StartsWith(p, StringComparison.Ordinal) == true))
            .Where(t => !AllowedWithoutValidator.Contains(t.FullName ?? string.Empty))
            .ToList();

        var validatorTargets = asm.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.BaseType is { IsGenericType: true }
                     && t.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            .Select(t => t.BaseType!.GetGenericArguments()[0])
            .ToHashSet();

        var missing = requests
            .Where(r => !validatorTargets.Contains(r))
            .Select(r => r.FullName!)
            .ToList();

        Assert.True(missing.Count == 0,
            "Requests Phase 0+ sin AbstractValidator hermano:\n  " + string.Join("\n  ", missing));
    }
}
