using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// <c>ADM_Tenants</c> la mapean DOS contextos con modelos distintos.
///
/// <para>
/// <see cref="AdminDbContext"/> la ve entera, a través de la entidad
/// <see cref="Tenant"/>. <see cref="TenantDbContext"/> ve un subconjunto, a
/// través de <see cref="ErpTenantInfo"/> — y además <b>inserta filas</b>.
/// </para>
///
/// <para>
/// De ahí la trampa: una columna obligatoria que el segundo no conozca y que no
/// tenga valor por defecto en la base hace fallar ese INSERT. No al añadir la
/// columna, sino la próxima vez que alguien registre una cooperativa por ese
/// camino. La configuración vigente ya lo documenta y por eso <c>ContactEmail</c>,
/// <c>StorageLimitMb</c>, <c>IsActive</c> y <c>MaxUsers</c> llevan default.
/// </para>
///
/// <para>
/// Esta prueba convierte esa nota en una regla que se comprueba sola. Si alguien
/// añade una columna obligatoria sin default, falla aquí y no en producción.
/// </para>
/// </summary>
public class DosContextosUnaTablaTests
{
    private const string Cadena = "Host=localhost;Database=x;Username=y;Password=z";

    private static IModel ModeloAdmin() =>
        new AdminDbContext(new DbContextOptionsBuilder<AdminDbContext>()
            .UseNpgsql(Cadena).Options).Model;

    private static IModel ModeloTenant() =>
        new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(Cadena).Options).Model;

    /// <summary>Columnas de ADM_Tenants que un contexto conoce.</summary>
    private static HashSet<string> Columnas(IModel modelo, Type entidad) =>
        [.. modelo.FindEntityType(entidad)!
            .GetProperties()
            .Select(p => p.GetColumnName())];

    [Fact]
    public void TodaColumnaObligatoriaQueElOtroContextoNoConoce_TieneDefaultEnLaBase()
    {
        var conocidasPorElStore = Columnas(ModeloTenant(), typeof(ErpTenantInfo));

        var problematicas = ModeloAdmin()
            .FindEntityType(typeof(Tenant))!
            .GetProperties()
            .Where(p => !p.IsNullable)
            .Where(p => !p.IsPrimaryKey())
            .Where(p => p.ValueGenerated == ValueGenerated.Never)
            .Where(p => p.GetDefaultValue() is null && p.GetDefaultValueSql() is null)
            .Select(p => p.GetColumnName())
            .Where(c => !conocidasPorElStore.Contains(c))
            .ToList();

        problematicas.Should().BeEmpty(
            "TenantDbContext inserta en ADM_Tenants sin conocer estas columnas, así que " +
            "sin valor por defecto en la base el INSERT falla la próxima vez que se " +
            "registre una cooperativa por ese camino");
    }

    [Fact]
    public void LasDosVistasApuntanALaMismaTabla()
    {
        // Si alguien las separa, esta suite deja de tener sentido y hay que saberlo.
        ModeloAdmin().FindEntityType(typeof(Tenant))!.GetTableName().Should().Be("ADM_Tenants");
        ModeloTenant().FindEntityType(typeof(ErpTenantInfo))!.GetTableName().Should().Be("ADM_Tenants");
    }

    [Fact]
    public void ElEstadoDeAprovisionamiento_TieneDefault()
    {
        // Es la unica columna obligatoria que entra en el tramo 3, y la que mas
        // probabilidades tenia de romper el INSERT del store.
        var propiedad = ModeloAdmin()
            .FindEntityType(typeof(Tenant))!
            .FindProperty(nameof(Tenant.ProvisioningState))!;

        propiedad.IsNullable.Should().BeFalse();
        propiedad.GetDefaultValue().Should().Be("Pending");
    }

    [Fact]
    public void DosCooperativasNoPuedenCompartirBase()
    {
        var indices = ModeloAdmin().FindEntityType(typeof(Tenant))!.GetIndexes();

        indices.Should().Contain(
            i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(Tenant.DatabaseName)),
            "dos cooperativas apuntando a la misma base leerían y escribirían lo mismo");
    }
}
