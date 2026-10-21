using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// El tipo de comprobante y el documento cruce por defecto de cada hecho de Cartera que pasa a Contabilidad
/// (<c>ACC_LendingVoucherMappings</c>; feature 013, T0637, T54; contracts/contabilidad.md §4.5 y §5; data-model parte 3 §5.2; DT §2.21,
/// <b>Order 89</b> —la 88 es de <c>NationalValuesReplicaSeeder</c>, CH36—), desde <c>lending-voucher-mappings.json</c>: una fila por hecho de
/// negocio y las excepciones de lo que recauda Nómina en <c>DN</c> (C79). Todo queda con <c>IsSeeded = true</c> y <c>ValidatedAt</c> nulo:
/// «pendiente de validar» por la contadora (C6).
///
/// <para>
/// Idempotente por <c>MappingKey</c> y <b>sin pisar</b>: si la clave ya existe —aunque la cooperativa la haya llevado a otro tipo, o la haya
/// dado de baja— no se toca. Un tipo de comprobante que falte o que la cooperativa tenga con otro uso (no <c>Module</c>/<c>CAR</c> o inactivo,
/// p. ej. un <c>CN</c> manual que <c>VoucherTypesSeeder</c> ya avisó en el log) deja el hecho sin mapeo y un aviso en el log: el consumidor
/// responderá <c>Accounting.VoucherType.NotFound</c>, que se corrige en la pantalla del mapeo. Un documento cruce que falte deja la fila sin
/// cruce, también con aviso.
/// </para>
///
/// <para>
/// La tabla llega con el par <c>IntegracionContableDeCartera</c> (T0639): hasta que esa migración esté aplicada en la base, la semilla no hace
/// nada. (nuevo)
/// </para>
/// </summary>
public sealed class LendingVoucherMappingsSeeder : IDataSeeder
{
    public const string Recurso = "lending-voucher-mappings.json";

    /// <summary>La migración que crea la tabla del mapeo.</summary>
    public const string MigracionQueCreaLasTablas = "IntegracionContableDeCartera";

    /// <summary>El módulo dueño de los tipos de comprobante que admite el mapeo.</summary>
    public const string ModuloCartera = "CAR";

    public int Order => 89;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public sealed record Semilla(string Operation, string? CollectorClass, string VoucherTypeCode, string? CrossDocumentTypeCode);

    public static IReadOnlyList<Semilla> Semillas() => RecursoJson.Leer<List<Semilla>>(Recurso);

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var aplicadas = await db.Database.GetAppliedMigrationsAsync(ct);
        if (!aplicadas.Any(m => m.EndsWith("_" + MigracionQueCreaLasTablas, StringComparison.Ordinal)))
        {
            context.Logger.LogInformation(
                "[Contabilidad.MapeoDeCarteraSinTablas] La base no tiene todavía la migración {Migracion}: no se siembra el mapeo de Cartera.",
                MigracionQueCreaLasTablas);
            return 0;
        }
        return await AplicarAsync(db, context.Logger, ct);
    }

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, ILogger logger, CancellationToken ct)
    {
        var existentes = (await db.LendingVoucherMappings.IgnoreQueryFilters().Select(m => m.MappingKey).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var tipos = (await db.VoucherTypes.Where(v => !v.IsDeleted)
                .Select(v => new { v.Id, v.Code, v.Usage, v.ModuleCode, v.IsActive })
                .ToListAsync(ct))
            .ToDictionary(v => v.Code, StringComparer.OrdinalIgnoreCase);
        var cruces = (await db.CrossDocumentTypes.Where(c => !c.IsDeleted).Select(c => new { c.Id, c.Code }).ToListAsync(ct))
            .ToDictionary(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase);

        var insertadas = 0;
        foreach (var semilla in Semillas())
        {
            if (existentes.Contains(LendingVoucherMapping.ClaveDe(semilla.Operation, semilla.CollectorClass))) continue;

            if (!tipos.TryGetValue(semilla.VoucherTypeCode, out var tipo)
                || tipo.Usage != VoucherUsage.Module
                || !string.Equals(tipo.ModuleCode, ModuloCartera, StringComparison.OrdinalIgnoreCase)
                || !tipo.IsActive)
            {
                logger.LogWarning(
                    "[Semilla.MapeoDeCarteraSinTipo] El hecho {Hecho} ({Clase}) no se mapea: el tipo de comprobante {Codigo} no existe o no es un tipo activo del módulo {Modulo} en esta cooperativa. Asígnele un tipo en Contabilidad › Cartera › Tipos de comprobante.",
                    semilla.Operation, semilla.CollectorClass ?? LendingVoucherMapping.Cualquiera, semilla.VoucherTypeCode, ModuloCartera);
                continue;
            }

            int? cruce = null;
            if (semilla.CrossDocumentTypeCode is { } codigoCruce)
            {
                if (cruces.TryGetValue(codigoCruce, out var idCruce)) cruce = idCruce;
                else
                    logger.LogWarning(
                        "[Semilla.MapeoDeCarteraSinCruce] El hecho {Hecho} se mapea sin documento cruce: la cooperativa no tiene el tipo de documento cruce {Codigo}.",
                        semilla.Operation, codigoCruce);
            }

            db.LendingVoucherMappings.Add(new LendingVoucherMapping(semilla.Operation, semilla.CollectorClass, tipo.Id, cruce, isSeeded: true)
            {
                CreatedBy = SeedContext.ParametricCreatedBy,
            });
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}
