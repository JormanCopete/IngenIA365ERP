using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Demo;

/// <summary>
/// Datos de demostracion (feature 004 — T048, FR-017/FR-020). Personas de
/// ejemplo con flags de rol (clientes/asociado/proveedor) para probar y
/// capacitar. TODO registro demo lleva <c>CreatedBy = "system:seed-demo"</c> —
/// la marca reconocible que permite localizarlos y depurarlos si llegan a un
/// ambiente equivocado. Solo INSERTA (idempotente por TaxId); jamas edita ni
/// borra — los movimientos contables demo quedan excluidos a proposito
/// (principio XI: los asientos se generan por los flujos normales del ERP
/// durante la capacitacion, no por seed directo).
/// </summary>
public sealed class DemoDataSeeder : IDataSeeder
{
    public int Order => 900;
    public SeedCategory Category => SeedCategory.Test;
    public SeedScope Scope => SeedScope.Tenant;

    private static readonly (string TaxId, string First, string Last, string Email, bool IsAssociate, bool IsCustomer, bool IsSupplier)[] DemoPeople =
    [
        ("900000001", "Demo",    "Asociado Uno",  "demo.asociado1@demo.ingenia365.test", true,  true,  false),
        ("900000002", "Demo",    "Asociada Dos",  "demo.asociada2@demo.ingenia365.test", true,  true,  false),
        ("900000003", "Demo",    "Cliente Tres",  "demo.cliente3@demo.ingenia365.test",  false, true,  false),
        ("900000004", "Demo SAS","Proveedor",     "demo.proveedor@demo.ingenia365.test", false, false, true),
    ];

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var inserted = 0;

        foreach (var p in DemoPeople)
        {
            if (await db.People.AnyAsync(x => x.TaxId == p.TaxId, ct)) continue;

            db.People.Add(new Person
            {
                TaxId = p.TaxId,
                IdType = p.IsSupplier ? "N" : "C",
                PersonType = p.IsSupplier ? "02" : "01",
                FirstName = p.First,
                LastName = p.Last,
                BusinessName = p.IsSupplier ? $"{p.First} {p.Last}" : null,
                Email = p.Email,
                IsAssociate = p.IsAssociate,
                IsCustomer = p.IsCustomer,
                IsSupplier = p.IsSupplier,
                CreatedBy = SeedContext.DemoCreatedBy
            });
            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(ct);
            context.Logger.LogInformation(
                "DemoDataSeeder: {Count} persona(s) demo insertadas en {Schema} (marca {Mark}).",
                inserted, context.Tenant?.Schema ?? "dbo", SeedContext.DemoCreatedBy);
        }

        return inserted;
    }
}
