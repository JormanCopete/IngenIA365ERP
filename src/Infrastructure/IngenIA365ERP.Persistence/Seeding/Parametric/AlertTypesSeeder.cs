using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Alerts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 012 (T39, T094; decisiones-transversales §2.13, §2.14; data-model §22): los tipos de alerta del catálogo
/// cerrado (<see cref="TiposDeAlerta"/>) con sus destinatarios, canales y severidad por defecto, en
/// <c>COR_AlertTypes</c>. Inserta un tipo <b>sólo si no tiene ninguna versión</b> (viva o de baja): lo que la cooperativa
/// ya configuró no se pisa. La vigencia abre en <see cref="VigenciaAbierta"/>, «desde siempre»; los tipos de entregas
/// futuras (I2–I6) se siembran igual, así la pantalla los muestra y nadie los levanta hasta que su proceso exista.
/// </summary>
public sealed class AlertTypesSeeder : IDataSeeder
{
    public int Order => 83;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    /// <summary>Desde cuándo rige un defecto: antes de cualquier operación de la plataforma.</summary>
    public static readonly DateOnly VigenciaAbierta = new(1900, 1, 1);

    public const string MotivoDeLaSemilla = "Valor por defecto de la semilla (feature 012); la cooperativa lo configura en Inventario › Alertas.";

    public Task<int> SeedAsync(SeedContext context, CancellationToken ct) => AplicarAsync(context.TenantDb!, ct);

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var existentes = (await db.AlertTypes.IgnoreQueryFilters().Select(t => t.TypeCode).Distinct().ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var insertadas = 0;
        foreach (var definicion in TiposDeAlerta.Todos)
        {
            if (existentes.Contains(definicion.TypeCode)) continue;
            db.AlertTypes.Add(new AlertType
            {
                TypeCode = definicion.TypeCode,
                Module = definicion.Module,
                RecipientPermissions = AlertType.PermisosComoJson(definicion.Destinatarios),
                Channels = definicion.Canales,
                Severity = definicion.Severidad,
                IsEnabled = true,
                ValidFrom = VigenciaAbierta,
                Reason = MotivoDeLaSemilla,
                CreatedBy = SeedContext.ParametricCreatedBy,
            });
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}
