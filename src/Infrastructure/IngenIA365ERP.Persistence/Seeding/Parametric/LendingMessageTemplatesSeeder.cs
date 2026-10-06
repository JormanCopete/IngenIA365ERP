using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Las nueve plantillas de mensaje de Cartera «pendiente de validar» (<c>COR_NotificationTemplates</c>; feature 013, T0085; T76; C90;
/// contracts/adaptadores-y-archivos.md §4.7; data-model parte 3 §4.12; Order 91): <c>AVISOPREV</c>, <c>RECORDPREV</c>, <c>MORAADMIN</c>,
/// <c>PREJURID</c>, <c>NOAPLICPAG</c>, <c>PASOACAJA</c>, <c>ABONOCAP</c>, <c>GASTOCOBR</c> y <c>ENVIODOC</c>, una fila por canal (correo,
/// SMS y WhatsApp), con <c>Module = Lending</c>, <c>Version = 1</c>, <c>IsSeeded</c> y <c>ReviewPending</c>. El texto vive en
/// <c>Data/lending-message-templates.json</c>. Idempotente por (<c>Code</c>, <c>Channel</c>, <c>Version</c>) <b>incluidas las de baja</b>:
/// una plantilla que la cooperativa cambió (versión 2) o dio de baja no vuelve. Las columnas llegan con la migración
/// <c>PlataformaParaCartera</c>: hasta que esté aplicada en la base, la semilla no hace nada.
/// </summary>
public sealed class LendingMessageTemplatesSeeder : IDataSeeder
{
    public const string MigracionQueCreaLasColumnas = "PlataformaParaCartera";
    public const string Archivo = "lending-message-templates.json";

    /// <summary>Desde cuándo rigen: «desde siempre», como las demás semillas.</summary>
    public static readonly DateOnly VigenciaDeLaSemilla = new(2000, 1, 1);

    public int Order => 91;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var aplicadas = await db.Database.GetAppliedMigrationsAsync(ct);
        if (!aplicadas.Any(m => m.EndsWith("_" + MigracionQueCreaLasColumnas, StringComparison.Ordinal)))
        {
            context.Logger.LogInformation("[Cartera.PlantillasSinColumnas] La base no tiene todavía la migración {Migracion}: no se siembra.",
                MigracionQueCreaLasColumnas);
            return 0;
        }
        return await AplicarAsync(db, ct);
    }

    /// <summary>Las plantillas del archivo, ya validadas contra la lista blanca de su propósito.</summary>
    public static IReadOnlyList<PlantillaSembrada> Leer()
    {
        var codigos = RecursoJson.Leer<List<CodigoSembrado>>(Archivo);
        var plantillas = new List<PlantillaSembrada>();
        foreach (var codigo in codigos)
        {
            foreach (var t in codigo.Templates)
            {
                var revision = RenderizadorDePlantillasDeMensaje.Revisar(codigo.Purpose, t.Channel, t.Subject, t.Body, null);
                if (!revision.EsValida)
                    throw new InvalidOperationException($"La plantilla sembrada {codigo.Code} ({t.Channel}) no es válida: {revision.Errores[0].Message}");
                plantillas.Add(new PlantillaSembrada(codigo.Code, codigo.Purpose, t.Channel, t.Subject, t.Body));
            }
        }
        return plantillas;
    }

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var existentes = (await db.NotificationTemplates.IgnoreQueryFilters()
                .Where(t => t.Module == CreateMessageTemplateVersionCommandHandler.Modulo)
                .Select(t => new { t.Code, t.Channel, t.Version })
                .ToListAsync(ct))
            .Select(t => (t.Code, t.Channel, t.Version))
            .ToHashSet();

        var insertadas = 0;
        foreach (var p in Leer())
        {
            var canal = p.Channel.ToString();
            if (existentes.Contains((p.Code, canal, 1))) continue;
            db.NotificationTemplates.Add(new NotificationTemplate
            {
                TemplateName = $"{p.Code} v1",
                Channel = canal,
                Subject = p.Subject,
                BodyTemplate = p.Body,
                IsActive = true,
                Code = p.Code,
                Module = CreateMessageTemplateVersionCommandHandler.Modulo,
                Purpose = p.Purpose.ToString(),
                Version = 1,
                ValidFrom = VigenciaDeLaSemilla,
                Variables = RenderizadorDePlantillasDeMensaje.VariablesComoJson(p.Purpose),
                IsSeeded = true,
                ReviewPending = true,
                CreatedBy = SeedContext.ParametricCreatedBy,
            });
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }

    /// <summary>Una plantilla del archivo.</summary>
    public sealed record PlantillaSembrada(string Code, DebtorMessagePurpose Purpose, MessageChannel Channel, string? Subject, string Body);

    private sealed record CodigoSembrado(string Code, DebtorMessagePurpose Purpose, List<TextoSembrado> Templates);

    private sealed record TextoSembrado(MessageChannel Channel, string? Subject, string Body);
}
