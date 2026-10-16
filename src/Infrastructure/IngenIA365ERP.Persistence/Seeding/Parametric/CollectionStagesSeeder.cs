using IngenIA365ERP.Application.Lending.Collections.Stages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Las cuatro etapas de cobranza por defecto de Cartera, «pendiente de validar» (<c>LND_CollectionStages</c>; feature 013, K4, T0941; data-model
/// §26.4; FR-117; Order 92): preventiva, administrativa, prejurídica y jurídica, con el texto en <c>Data/lending-collection-stages.json</c>. La lógica
/// es <see cref="EtapasPorDefecto"/>: idempotente, no hace nada si la cooperativa ya tiene alguna etapa. Sólo siembra las etapas: el registro de la
/// etapa migrada de los créditos vivos (C64) es una corrida aparte, no esta semilla. La tabla llega con la migración <c>CarteraCobranza</c>: hasta que
/// esté aplicada en la base, la semilla no hace nada. <b>Order 92</b> y no 91 como decía T0941: el 91 ya es de <c>LendingMessageTemplatesSeeder</c>, y
/// las etapas citan sus plantillas.
/// </summary>
public sealed class CollectionStagesSeeder : IDataSeeder
{
    public const string MigracionQueCreaLaTabla = "CarteraCobranza";
    public const string Archivo = "lending-collection-stages.json";

    public int Order => 92;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var aplicadas = await db.Database.GetAppliedMigrationsAsync(ct);
        if (!aplicadas.Any(m => m.EndsWith("_" + MigracionQueCreaLaTabla, StringComparison.Ordinal)))
        {
            context.Logger.LogInformation("[Cartera.EtapasSinTabla] La base no tiene todavía la migración {Migracion}: no se siembran las etapas de cobranza.",
                MigracionQueCreaLaTabla);
            return 0;
        }
        return await EtapasPorDefecto.SembrarAsync(db, Leer(), ct);
    }

    /// <summary>Las etapas del archivo.</summary>
    public static IReadOnlyList<CollectionStageInput> Leer() => RecursoJson.Leer<List<CollectionStageInput>>(Archivo);
}
