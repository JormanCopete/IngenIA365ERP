using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// La persona genérica «Consumidor final» del maestro (<c>COR_People</c>; feature 012, I3, T587; T52; api.md §18;
/// decisiones-transversales §2.14, Order 87): la contraparte de la venta sin identificar al comprador, dentro de lo que la norma
/// permite. Su identificación —tipo, número y nombre— sale de <see cref="CatalogoDian.ConsumidorFinal"/> vigente a la fecha de la
/// siembra (Res. 202/2025), nunca de un literal: el tipo se guarda como <c>COR_People.IdType</c> por la traducción inversa del
/// catálogo (<see cref="CatalogoDian.IdTypeDe"/>). Idempotente por documento <b>incluidas las eliminadas</b>
/// (<c>UK_COR_People_TaxId</c> no las distingue): si el número ya existe, no se crea otra persona. Sin catálogo vigente no se
/// inventa una identificación. Es la única persona que escribe una semilla: el alta de personas sigue siendo
/// <c>PersonaDialog</c> (FR-011).
///
/// <para>
/// Espera, como las demás semillas de I3, al par <c>VentasYPuntoDeVenta</c> (T586).
/// </para>
/// </summary>
public sealed class ConsumidorFinalSeeder : IDataSeeder
{
    public int Order => 87;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        if (!await CashDenominationsSeeder.TieneLaMigracionAsync(db, context.Logger, "[Ventas.ConsumidorFinalSinTablas]", ct)) return 0;
        return await AplicarAsync(db, context.Logger, DateOnly.FromDateTime(DateTime.UtcNow), ct);
    }

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory), con el catálogo vigente a <paramref name="fecha"/>.</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, ILogger logger, DateOnly fecha, CancellationToken ct)
    {
        var catalogo = CatalogoDian.Embebido;
        var consumidor = catalogo.ConsumidorFinal(fecha);
        var idType = consumidor is null ? null : catalogo.IdTypeDe(consumidor.TipoDeIdentificacion, fecha);
        if (consumidor is null || idType is null)
        {
            logger.LogWarning("[Ventas.ConsumidorFinalSinCatalogo] No hay identificación del consumidor final vigente al {Fecha}: no se siembra.", fecha);
            return 0;
        }

        var existente = await db.People.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.TaxId == consumidor.Numero)
            .Select(p => new { p.IsDeleted })
            .FirstOrDefaultAsync(ct);
        if (existente is not null)
        {
            if (existente.IsDeleted)
                logger.LogWarning(
                    "[Ventas.ConsumidorFinalEliminado] El documento {Numero} del consumidor final pertenece a una persona eliminada: restaurarla en Personas.",
                    consumidor.Numero);
            return 0;
        }

        var (nombre, apellido) = PartirNombre(consumidor.Nombre);
        db.People.Add(new Person
        {
            IdType = idType,
            TaxId = consumidor.Numero,
            FirstName = nombre,
            LastName = apellido,
            PersonType = "01",
            IsCustomer = true,
            ReceivesInvoice = false,
            Status = "A",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedContext.ParametricCreatedBy,
        });
        await db.SaveChangesAsync(ct);
        return 1;
    }

    /// <summary>El nombre visible de la persona sembrada (el mismo que el maestro muestra en listados).</summary>
    public static string NombreVisible(Person persona) =>
        PersonFactory.NombreVisible(persona.FirstName, persona.OtherNames, persona.LastName, persona.SecondLastName, persona.BusinessName);

    /// <summary>«Consumidor final» → nombre «Consumidor», apellido «final»: la primera palabra y el resto.</summary>
    private static (string Nombre, string Apellido) PartirNombre(string completo)
    {
        var partes = completo.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return partes.Length == 2 ? (partes[0], partes[1]) : (partes[0], partes[0]);
    }
}
