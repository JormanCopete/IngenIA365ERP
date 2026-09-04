using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.MultiTenancy;

/// <summary>
/// El despachador de notificaciones recorre lo que devuelve
/// <c>ListActiveAsync</c> y abre la base de cada cooperativa. En QA, el
/// 2026-09-04, recorrió la primera cooperativa real dos segundos después del alta:
/// la base ya existía y sus tablas no, y falló con
/// <c>42P01 relation "dbo.COR_Notifications" does not exist</c>. El alta pone
/// <c>IsActive = true</c> antes de aprovisionar; «activa» no significa «lista».
/// </summary>
public class TenantDirectoryTests
{
    private static TenantDbContext Contexto(string nombre) =>
        new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"directorio-{nombre}-{Guid.NewGuid():N}")
            .Options);

    private static ErpTenantInfo Cooperativa(string id, bool activa, string? estado, string? baseDeDatos = null) => new()
    {
        Identifier = id,
        Name = id.ToUpperInvariant(),
        PublicId = Guid.NewGuid(),
        IsActive = activa,
        ProvisioningState = estado,
        SchemaName = id,
        DatabaseName = baseDeDatos,
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task ListActiveAsync_SoloDevuelveLasActivasYListas()
    {
        await using var db = Contexto(nameof(ListActiveAsync_SoloDevuelveLasActivasYListas));
        db.Tenants.AddRange(
            Cooperativa("lista", activa: true, EstadoDeAprovisionamiento.Listo),
            Cooperativa("a_medio_hacer", activa: true, EstadoDeAprovisionamiento.EnCurso),
            Cooperativa("fallida", activa: true, EstadoDeAprovisionamiento.Fallido),
            Cooperativa("suspendida", activa: false, EstadoDeAprovisionamiento.Listo),
            Cooperativa("sin_estado", activa: true, estado: null));
        await db.SaveChangesAsync();

        var directorio = new TenantDirectory(db, NullLogger<TenantDirectory>.Instance);

        var resultado = await directorio.ListActiveAsync(CancellationToken.None);

        resultado.Select(c => c.Identifier).Should().Equal("lista");
    }

    [Fact]
    public async Task ListActiveAsync_PrefiereDatabaseName_YCaeAlEsquemaSiNoLoHay()
    {
        await using var db = Contexto(nameof(ListActiveAsync_PrefiereDatabaseName_YCaeAlEsquemaSiNoLoHay));
        db.Tenants.AddRange(
            Cooperativa("con_base", activa: true, EstadoDeAprovisionamiento.Listo, baseDeDatos: "coop_prueba"),
            Cooperativa("solo_esquema", activa: true, EstadoDeAprovisionamiento.Listo, baseDeDatos: null));
        await db.SaveChangesAsync();

        var directorio = new TenantDirectory(db, NullLogger<TenantDirectory>.Instance);

        var resultado = await directorio.ListActiveAsync(CancellationToken.None);

        resultado.Single(c => c.Identifier == "con_base").DatabaseName.Should().Be("coop_prueba");
        resultado.Single(c => c.Identifier == "solo_esquema").DatabaseName.Should().Be("solo_esquema",
            "las filas anteriores al modelo de base por cooperativa no tienen DatabaseName y el esquema es el nombre");
    }
}
