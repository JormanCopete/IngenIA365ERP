using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 012 (T152; contracts/api.md §8; data-model §5.8; decisiones-transversales §2.14): un tipo de documento por
/// cada clase operable en este despliegue (en I1: compras recibidas, factura, nota y devolución al proveedor, ajustes,
/// consumo interno, baja, saldo inicial, traslados, movimiento entre ubicaciones, conteo, ajuste de costo y la
/// anulación), cada uno con su consecutivo de prefijo vacío desde el primer día del mes y <c>IsSeeded = true</c>. El de
/// <c>OpeningBalance</c> lleva además la política de un nivel, umbral 0 y permiso <c>Inventory.OpeningBalance.Approve</c>
/// en <c>COR_ApprovalPolicies</c>: <b>ésta es la única siembra de esa política</b> (US4 sólo agrega la regla
/// <c>Approvals.Policy.RequiredForClass</c>). Idempotente por código: lo que la cooperativa ya tiene no se toca.
///
/// <para>
/// Las tablas <c>INV_DocumentTypes</c>/<c>INV_DocumentSequences</c> llegan con el par <c>InventarioComercialNucleo</c>
/// (T440). Hasta que esa migración esté aplicada en la base, la semilla no hace nada (si no, el arranque fallaría en
/// toda cooperativa después de <c>PlataformaParaInventario</c>, T186).
/// </para>
/// </summary>
public sealed class InventoryDocumentTypesSeeder : IDataSeeder
{
    public int Order => 80;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    /// <summary>La migración que crea las tablas del documento.</summary>
    public const string MigracionQueCreaLasTablas = "InventarioComercialNucleo";

    public const string PermisoDeAprobacionDelSaldoInicial = "Inventory.OpeningBalance.Approve";

    public const string MotivoDeLaPolitica = "Política por defecto de la semilla (feature 012): el saldo inicial siempre se aprueba.";

    /// <summary>Código y nombre del tipo sembrado de cada clase de I1.</summary>
    public static readonly IReadOnlyDictionary<DocumentClass, (string Codigo, string Nombre)> Sembrados = new Dictionary<DocumentClass, (string, string)>
    {
        [DocumentClass.PurchaseReceipt] = ("REC", "Recepción de compra"),
        [DocumentClass.SupplierInvoice] = ("FCP", "Factura del proveedor"),
        [DocumentClass.SupplierNote] = ("NTP", "Nota del proveedor"),
        [DocumentClass.SupplierReturn] = ("DVP", "Devolución a proveedor"),
        [DocumentClass.PositiveAdjustment] = ("AJP", "Ajuste positivo"),
        [DocumentClass.NegativeAdjustment] = ("AJN", "Ajuste negativo"),
        [DocumentClass.InternalConsumption] = ("CIN", "Consumo interno"),
        [DocumentClass.WriteOff] = ("BAJ", "Baja de inventario"),
        [DocumentClass.OpeningBalance] = ("SIN", "Saldo inicial"),
        [DocumentClass.TransferDispatch] = ("TRD", "Despacho de traslado"),
        [DocumentClass.TransferReceipt] = ("TRR", "Recepción de traslado"),
        [DocumentClass.LocationMove] = ("MUB", "Movimiento entre ubicaciones"),
        [DocumentClass.PhysicalCount] = ("CON", "Conteo físico"),
        [DocumentClass.CostAdjustment] = ("AJC", "Ajuste de costo"),
        [DocumentClass.Voiding] = ("ANU", "Anulación"),
    };

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var aplicadas = await db.Database.GetAppliedMigrationsAsync(ct);
        if (!aplicadas.Any(m => m.EndsWith("_" + MigracionQueCreaLasTablas, StringComparison.Ordinal)))
        {
            context.Logger.LogInformation(
                "[Inventario.TiposSinTablas] La base no tiene todavía la migración {Migracion}: no se siembran los tipos de documento.",
                MigracionQueCreaLasTablas);
            return 0;
        }
        return await AplicarAsync(db, HoyEnColombia(), ct);
    }

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, DateOnly hoy, CancellationToken ct)
    {
        var desde = new DateOnly(hoy.Year, hoy.Month, 1);
        var existentes = (await db.InventoryDocumentTypes.IgnoreQueryFilters().Select(t => t.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var insertadas = 0;
        InventoryDocumentType? saldoInicial = null;
        foreach (var clase in ClasesDeDocumento.Todas.Where(c => c.Operable()))
        {
            if (!Sembrados.TryGetValue(clase.Class, out var sembrado)) continue;
            if (existentes.Contains(sembrado.Codigo))
            {
                if (clase.Class == DocumentClass.OpeningBalance)
                    saldoInicial = await db.InventoryDocumentTypes.FirstOrDefaultAsync(t => t.Code == sembrado.Codigo, ct);
                continue;
            }

            var tipo = new InventoryDocumentType
            {
                Code = sembrado.Codigo,
                Name = sembrado.Nombre,
                Class = clase.Class,
                RequiresReason = clase.Class is DocumentClass.WriteOff or DocumentClass.Voiding,
                RequiresCounterparty = clase.Group == DocumentClassGroup.Purchases,
                AllWarehouses = true,
                IsSeeded = true,
                IsActive = true,
                CreatedBy = SeedContext.ParametricCreatedBy,
            };
            if (clase.NumberedBy == NumberedBy.Sequence)
            {
                tipo.Sequences.Add(new DocumentSequence
                {
                    DocumentType = tipo,
                    Prefix = string.Empty,
                    NextValue = 1,
                    ValidFrom = desde,
                    CreatedBy = SeedContext.ParametricCreatedBy,
                });
            }
            db.InventoryDocumentTypes.Add(tipo);
            insertadas++;
            if (clase.Class == DocumentClass.OpeningBalance) saldoInicial = tipo;
        }

        if (saldoInicial is not null)
        {
            var clave = ApprovalPolicy.ClaveDe(ApprovalPolicy.ModuloInventario, ApprovalSubjects.DocumentConfirmation, saldoInicial.PublicId);
            if (!await db.ApprovalPolicies.IgnoreQueryFilters().AnyAsync(p => p.PolicyKey == clave, ct))
            {
                db.ApprovalPolicies.Add(new ApprovalPolicy
                {
                    Module = ApprovalPolicy.ModuloInventario,
                    Subject = ApprovalSubjects.DocumentConfirmation,
                    DocumentTypePublicId = saldoInicial.PublicId,
                    PolicyKey = clave,
                    Version = 1,
                    ValidFrom = desde,
                    Reason = MotivoDeLaPolitica,
                    CreatedBy = SeedContext.ParametricCreatedBy,
                    Levels = [new ApprovalPolicyLevel { Order = 1, Threshold = 0m, PermissionCode = PermisoDeAprobacionDelSaldoInicial, CreatedBy = SeedContext.ParametricCreatedBy }],
                });
                insertadas++;
            }
        }

        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }

    /// <summary>Hoy en Colombia (−05:00 fijo, como <c>IDateTimeService.HoyLocal</c>).</summary>
    private static DateOnly HoyEnColombia() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
}
