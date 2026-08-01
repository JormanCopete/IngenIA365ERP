using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Common;
using System.Reflection;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio VII — Soft-delete universal:
/// <list type="bullet">
///   <item>Toda entidad concreta del dominio hereda de <see cref="AuditableEntity"/>
///         o <see cref="AuditableEntityLong"/>, o lleva el atributo
///         <see cref="LookupAttribute"/> (catálogos inmutables).</item>
///   <item>El <c>SoftDeleteInterceptor</c> está registrado en el DI de
///         Persistence (T098) — la promesa solo se cumple si EF realmente
///         lo invoca al guardar cambios.</item>
/// </list>
/// </summary>
public class PrincipioVII_SoftDeleteAndAuditable
{
    /// <summary>
    /// Excepciones documentadas: entidades append-only que el data-model
    /// declara explícitamente fuera del modelo auditable.
    /// </summary>
    private static readonly HashSet<string> AllowedAppendOnly =
    [
        // FR-010 — historial inmutable, FIFO; no participa de soft-delete.
        "IngenIA365ERP.Domain.Entities.Security.PasswordHistory",

        // Feature 002-identidad-central-federada — Complexity Tracking del plan.md:
        // CentralUser usa Guid Id por convención de ASP.NET Core Identity
        // (IdentityUser<Guid>) y por eso NO puede heredar AuditableEntity (int Id).
        // Los campos de audit + soft-delete están declarados manualmente; cumple el
        // espíritu del principio VII aunque no la firma estructural.
        "IngenIA365ERP.Domain.Entities.Admin.CentralUser",

        // Feature 002 — data-model §5: telemetría append-only de intentos de login.
        // No participa de soft-delete por diseño (FR-035, FR-042); su retención se
        // gestiona por job de limpieza (1 año) — separado del modelo auditable.
        "IngenIA365ERP.Domain.Entities.Admin.CentralUserLoginAttempt",
    ];

    [Fact]
    public void Every_concrete_entity_inherits_an_auditable_base_or_is_marked_Lookup()
    {
        var asm = typeof(BaseEntity).Assembly;
        var offenders = asm.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.Namespace?.StartsWith("IngenIA365ERP.Domain.Entities", StringComparison.Ordinal) == true)
            .Where(t => !typeof(AuditableEntity).IsAssignableFrom(t)
                     && !typeof(AuditableEntityLong).IsAssignableFrom(t))
            // Allow [Lookup] (T099) — catálogos puros como Permission o monedas.
            .Where(t => t.GetCustomAttribute<LookupAttribute>() is null)
            .Where(t => !AllowedAppendOnly.Contains(t.FullName ?? string.Empty))
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Entidades concretas no auditables y sin [Lookup]:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void SoftDeleteInterceptor_is_registered_in_Persistence_DI()
    {
        // T098 — la promesa de soft-delete solo se cumple si EF dispara el
        // interceptor. Si alguien lo des-registra (refactor mal hecho), este
        // test se pone rojo antes que un DELETE físico llegue a producción.
        var sourcePath = System.IO.Path.Combine(
            RepoPath.FindRepoRoot(),
            "src", "Infrastructure", "IngenIA365ERP.Persistence", "DependencyInjection.cs");
        var text = System.IO.File.ReadAllText(sourcePath);
        Assert.Contains("SoftDeleteInterceptor", text);
        Assert.Contains("AddScoped<ISaveChangesInterceptor", text);
    }
}
