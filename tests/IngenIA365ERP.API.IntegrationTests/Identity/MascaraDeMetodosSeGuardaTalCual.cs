using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Ida y vuelta contra PostgreSQL real de lo que
/// <c>MascaraDeMetodosMfaSinAsignarTests</c> fija en el modelo: una política que
/// no exige segundo factor y no acepta ningún método se guarda con máscara cero,
/// no con el default de la columna. Antes del sentinel, EF tomaba el cero por
/// «no asignado», lo omitía del INSERT y la base ponía «todos».
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class MascaraDeMetodosSeGuardaTalCual(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Una_politica_sin_metodos_y_sin_exigencia_vuelve_con_cero()
    {
        var tenantId = Guid.NewGuid();

        using (var scope = fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
            var politica = TenantMfaPolicy.CreateForTenant(tenantId);
            politica.PermitirMetodos(MetodosMfa.Ninguno); // legítimo: no exige segundo factor
            db.TenantMfaPolicies.Add(politica);
            await db.SaveChangesAsync();
        }

        using (var scope = fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
            var leida = await db.TenantMfaPolicies.AsNoTracking().SingleAsync(p => p.TenantId == tenantId);

            Assert.Equal(MetodosMfa.Ninguno, leida.AllowedMethodsMask);
        }
    }

    [Fact]
    public async Task Una_politica_nueva_sin_tocar_la_mascara_sigue_aceptando_todo()
    {
        // El default de la entidad y el de la columna coinciden; esto vigila que el
        // sentinel no haya roto el camino normal.
        var tenantId = Guid.NewGuid();

        using (var scope = fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
            db.TenantMfaPolicies.Add(TenantMfaPolicy.CreateForTenant(tenantId));
            await db.SaveChangesAsync();
        }

        using (var scope = fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
            var leida = await db.TenantMfaPolicies.AsNoTracking().SingleAsync(p => p.TenantId == tenantId);

            Assert.Equal(ConversionDeMetodosMfa.Todos, leida.AllowedMethodsMask);
        }
    }
}
