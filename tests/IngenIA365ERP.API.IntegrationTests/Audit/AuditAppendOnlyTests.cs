using System.Reflection;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Audit;

namespace IngenIA365ERP.API.IntegrationTests.Audit;

/// <summary>
/// T082 — Append-only constraint del contrato canónico de escritura.
///
/// El spec exige que el writer NO exponga métodos de mutación
/// (<c>Update*</c>, <c>Delete*</c>, <c>Replace*</c>) — es la garantía
/// estructural FR-025 (audit log inmutable). En producción esto se refuerza
/// con un rol Mongo <c>audit_appendOnly</c> (T027), pero la primera línea
/// de defensa es que el código simplemente NO PUEDE mutar porque la
/// superficie del contrato <see cref="IAuditAppendOnlyWriter"/> no lo
/// permite.
///
/// El test introspecciona la <b>interfaz pública</b> (no la implementación
/// interna) porque es el único contrato que ven los consumidores via DI.
/// Si mañana un dev introduce <c>DeleteAsync</c> en la interfaz, este test
/// se pone rojo.
/// </summary>
public class AuditAppendOnlyTests
{
    private static readonly string[] ForbiddenMethodPrefixes =
        ["Update", "Delete", "Replace", "Remove", "Drop", "Overwrite", "Set"];

    [Fact]
    public void IAuditAppendOnlyWriter_does_not_expose_any_mutation_method()
    {
        var contract = typeof(IAuditAppendOnlyWriter);
        var members = contract
            .GetMethods(BindingFlags.Public | BindingFlags.Instance);

        var offenders = members
            .Where(m => ForbiddenMethodPrefixes.Any(p =>
                m.Name.StartsWith(p, StringComparison.Ordinal)))
            .Select(m => m.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "el writer canónico solo debe exponer Append*; cualquier método " +
            "Update*/Delete*/Replace*/Set* viola la promesa append-only (FR-025).");
    }

    [Fact]
    public void IAuditAppendOnlyWriter_surface_is_limited_to_append_operations()
    {
        var contract = typeof(IAuditAppendOnlyWriter);
        var methodNames = contract
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .Distinct()
            .ToList();

        methodNames.Should().OnlyContain(name =>
            name.StartsWith("Append", StringComparison.Ordinal),
            because: "la superficie del contrato debe limitarse a Append*.");
    }
}
