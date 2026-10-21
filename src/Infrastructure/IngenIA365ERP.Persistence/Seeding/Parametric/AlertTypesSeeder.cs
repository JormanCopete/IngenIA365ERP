using System.Text.Json;
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
///
/// <para>
/// Feature 013 (T0091, C68; data-model parte 3 §4.14, §11): siembra también los tipos de Cartera, Contabilidad, Nómina y
/// Cumplimiento con el mismo criterio, y en los tres reutilizados de la 012 (<see cref="TiposDeAlerta.DestinatariosQueSumaCartera"/>)
/// agrega <b>una versión nueva</b> desde el día del despliegue con la unión de los destinatarios de la última versión y los de
/// Cartera, motivo <see cref="MotivoDeCartera"/>, sólo si esa versión no los tiene todos: volver a sembrar no la duplica, y lo que
/// la cooperativa cambió (canales, severidad, umbrales, habilitado) se conserva en la versión nueva.
/// </para>
/// </summary>
public sealed class AlertTypesSeeder : IDataSeeder
{
    public int Order => 83;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    /// <summary>Desde cuándo rige un defecto: antes de cualquier operación de la plataforma.</summary>
    public static readonly DateOnly VigenciaAbierta = new(1900, 1, 1);

    public const string MotivoDeLaSemilla = "Valor por defecto de la semilla (feature 012); la cooperativa lo configura en Inventario › Alertas.";

    /// <summary>El motivo de la versión que suma los destinatarios de Cartera a un tipo reutilizado (C68).</summary>
    public const string MotivoDeCartera = "Feature 013: destinatarios de Cartera";

    public Task<int> SeedAsync(SeedContext context, CancellationToken ct) =>
        // La fecha local de Colombia (UTC−5, sin horario de verano): la versión de Cartera rige desde el día del despliegue.
        AplicarAsync(context.TenantDb!, ct, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)));

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    /// <param name="hoy">El día del despliegue (desde cuándo rige la versión de Cartera); nulo = hoy en Colombia.</param>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct, DateOnly? hoy = null)
    {
        var desdeElDespliegue = hoy ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
        var versiones = await db.AlertTypes.IgnoreQueryFilters().ToListAsync(ct);
        var existentes = versiones.Select(t => t.TypeCode).ToHashSet(StringComparer.Ordinal);

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
                ThresholdsJson = definicion.UmbralesPorDefecto is { Count: > 0 } umbrales ? JsonSerializer.Serialize(umbrales) : null,
                IsEnabled = true,
                ValidFrom = VigenciaAbierta,
                Reason = MotivoDeLaSemilla,
                CreatedBy = SeedContext.ParametricCreatedBy,
            });
            insertadas++;
        }

        foreach (var (typeCode, deCartera) in TiposDeAlerta.DestinatariosQueSumaCartera)
        {
            var ultima = versiones.Where(v => v.TypeCode == typeCode && !v.IsDeleted).OrderByDescending(v => v.ValidFrom).FirstOrDefault();
            // Un tipo que la cooperativa dejó cerrado no se reabre por la semilla.
            if (ultima is null || ultima.ValidTo < desdeElDespliegue) continue;
            var actuales = ultima.Permisos();
            if (deCartera.All(p => actuales.Contains(p, StringComparer.Ordinal))) continue;

            // Desde el despliegue; si la última ya empieza ese día o después, desde el día siguiente a su inicio, para que la
            // anterior cierre la víspera sin quedar con una vigencia vacía.
            var desde = ultima.ValidFrom >= desdeElDespliegue ? ultima.ValidFrom.AddDays(1) : desdeElDespliegue;
            if (ultima.ValidTo is null || ultima.ValidTo >= desde) ultima.ValidTo = desde.AddDays(-1);
            db.AlertTypes.Add(new AlertType
            {
                TypeCode = ultima.TypeCode,
                Module = ultima.Module,
                RecipientPermissions = AlertType.PermisosComoJson(actuales.Concat(deCartera.Where(p => !actuales.Contains(p, StringComparer.Ordinal)))),
                Channels = ultima.Channels,
                Severity = ultima.Severity,
                ThresholdsJson = ultima.ThresholdsJson,
                IsEnabled = ultima.IsEnabled,
                ValidFrom = desde,
                Reason = MotivoDeCartera,
                CreatedBy = SeedContext.ParametricCreatedBy,
            });
            insertadas++;
        }

        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}
