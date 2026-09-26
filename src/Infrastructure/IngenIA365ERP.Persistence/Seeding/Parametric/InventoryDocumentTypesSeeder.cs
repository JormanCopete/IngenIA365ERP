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
/// anulación), cada uno con su consecutivo de prefijo vacío vigente desde <see cref="VigenciaDeLaSemilla"/> y <c>IsSeeded = true</c>. El de
/// <c>OpeningBalance</c> lleva además la política de un nivel, umbral 0 y permiso <c>Inventory.OpeningBalance.Approve</c>
/// en <c>COR_ApprovalPolicies</c>: <b>ésta es la única siembra de esa política</b> (US4 sólo agrega la regla
/// <c>Approvals.Policy.RequiredForClass</c>). El de <c>TransferReceipt</c> lleva la política <c>Subject = TransferDiscrepancy</c>
/// de un nivel, umbral 0 y <c>Inventory.Transfers.Approve</c> (US10, T373). US11 (T397): además de un tipo por clase, los dos tipos de
/// <b>ajuste de conteo</b> (<c>CONP</c> positivo, <c>CONN</c> negativo) con su consecutivo y su política <c>DocumentConfirmation</c> de un
/// nivel, umbral 0 y <c>Inventory.Counts.Approve</c> —así los reconocen <c>GenerateCountAdjustmentCommand</c> y la regla
/// <c>Approvals.Policy.RequiredForClass</c>—; el de conteo (<c>CON</c>) ya va con su consecutivo. Idempotente por código: lo que la
/// cooperativa ya tiene no se toca.
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

    /// <summary>US10 (T373): el permiso del nivel de la política de diferencias de traslado sembrada.</summary>
    public const string PermisoDeAprobacionDeDiferencias = "Inventory.Transfers.Approve";

    public const string MotivoDeLaPoliticaDeDiferencias =
        "Política por defecto de la semilla (feature 012, US10): resolver un faltante o sobrante de traslado siempre se aprueba.";

    /// <summary>US11 (T397): el permiso del nivel de la política de los tipos de ajuste de conteo.</summary>
    public const string PermisoDeAprobacionDeConteo = "Inventory.Counts.Approve";

    public const string MotivoDeLaPoliticaDeConteo =
        "Política por defecto de la semilla (feature 012, US11): el ajuste de un conteo siempre lo aprueba alguien ajeno al conteo.";

    /// <summary>US11 (T397): los tipos de ajuste de conteo, uno por clase de ajuste.</summary>
    public static readonly IReadOnlyDictionary<DocumentClass, (string Codigo, string Nombre)> AjustesDeConteo = new Dictionary<DocumentClass, (string, string)>
    {
        [DocumentClass.PositiveAdjustment] = ("CONP", "Ajuste de conteo (sobrante)"),
        [DocumentClass.NegativeAdjustment] = ("CONN", "Ajuste de conteo (faltante)"),
    };

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
        return await AplicarAsync(db, ct);
    }

    /// <summary>
    /// Desde cuándo rigen los consecutivos y las políticas sembrados: «desde siempre». Hasta el 2026-09-26 regían desde el primer
    /// día del mes en que corrió la semilla (el despliegue), y una bodega con fecha de corte anterior —la cooperativa de ensayo, o
    /// una puesta en marcha que despliega después del corte— no podía confirmar su saldo inicial (<c>Inventory.Numbering.SequenceMissing</c>)
    /// ni lo mandaba a aprobación, porque la política tampoco regía a esa fecha (lo destapó la e2e del cierre de I1, T443). El
    /// inicio real del módulo lo pone <c>INV_Setup.StartDate</c>, que ya impide documentos anteriores.
    /// </summary>
    public static readonly DateOnly VigenciaDeLaSemilla = new(2000, 1, 1);

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var desde = VigenciaDeLaSemilla;
        var existentes = (await db.InventoryDocumentTypes.IgnoreQueryFilters().Select(t => t.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var insertadas = 0;
        InventoryDocumentType? saldoInicial = null;
        InventoryDocumentType? recepcionDeTraslado = null;
        foreach (var clase in ClasesDeDocumento.Todas.Where(c => c.Operable()))
        {
            if (!Sembrados.TryGetValue(clase.Class, out var sembrado)) continue;
            if (existentes.Contains(sembrado.Codigo))
            {
                if (clase.Class == DocumentClass.OpeningBalance)
                    saldoInicial = await db.InventoryDocumentTypes.FirstOrDefaultAsync(t => t.Code == sembrado.Codigo, ct);
                if (clase.Class == DocumentClass.TransferReceipt)
                    recepcionDeTraslado = await db.InventoryDocumentTypes.FirstOrDefaultAsync(t => t.Code == sembrado.Codigo, ct);
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
            if (clase.Class == DocumentClass.TransferReceipt) recepcionDeTraslado = tipo;
        }

        // US11 (T397): los tipos de ajuste de conteo, con su consecutivo; su política va abajo.
        var ajustesDeConteo = new List<InventoryDocumentType>();
        foreach (var (clase, (codigo, nombre)) in AjustesDeConteo)
        {
            if (existentes.Contains(codigo))
            {
                if (await db.InventoryDocumentTypes.FirstOrDefaultAsync(t => t.Code == codigo, ct) is { } existente) ajustesDeConteo.Add(existente);
                continue;
            }
            var tipo = new InventoryDocumentType
            {
                Code = codigo,
                Name = nombre,
                Class = clase,
                AllWarehouses = true,
                IsSeeded = true,
                IsActive = true,
                CreatedBy = SeedContext.ParametricCreatedBy,
            };
            tipo.Sequences.Add(new DocumentSequence
            {
                DocumentType = tipo,
                Prefix = string.Empty,
                NextValue = 1,
                ValidFrom = desde,
                CreatedBy = SeedContext.ParametricCreatedBy,
            });
            db.InventoryDocumentTypes.Add(tipo);
            ajustesDeConteo.Add(tipo);
            insertadas++;
        }

        if (saldoInicial is not null
            && await PoliticaAsync(db, ApprovalSubjects.DocumentConfirmation, saldoInicial, PermisoDeAprobacionDelSaldoInicial, MotivoDeLaPolitica, desde, ct))
        {
            insertadas++;
        }
        // US10 (T373): resolver una diferencia de traslado se aprueba con la política del tipo de la recepción de traslado.
        if (recepcionDeTraslado is not null
            && await PoliticaAsync(db, ApprovalSubjects.TransferDiscrepancy, recepcionDeTraslado, PermisoDeAprobacionDeDiferencias, MotivoDeLaPoliticaDeDiferencias, desde, ct))
        {
            insertadas++;
        }

        foreach (var tipo in ajustesDeConteo)
        {
            if (await PoliticaAsync(db, ApprovalSubjects.DocumentConfirmation, tipo, PermisoDeAprobacionDeConteo, MotivoDeLaPoliticaDeConteo, desde, ct))
                insertadas++;
        }

        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }

    /// <summary>Una política de un nivel, umbral 0, para el sujeto y el tipo, si la serie no existe. Devuelve si la agregó.</summary>
    private static async Task<bool> PoliticaAsync(IApplicationDbContext db, string sujeto, InventoryDocumentType tipo, string permiso, string motivo,
        DateOnly desde, CancellationToken ct)
    {
        var clave = ApprovalPolicy.ClaveDe(ApprovalPolicy.ModuloInventario, sujeto, tipo.PublicId);
        if (await db.ApprovalPolicies.IgnoreQueryFilters().AnyAsync(p => p.PolicyKey == clave, ct)) return false;
        db.ApprovalPolicies.Add(new ApprovalPolicy
        {
            Module = ApprovalPolicy.ModuloInventario,
            Subject = sujeto,
            DocumentTypePublicId = tipo.PublicId,
            PolicyKey = clave,
            Version = 1,
            ValidFrom = desde,
            Reason = motivo,
            CreatedBy = SeedContext.ParametricCreatedBy,
            Levels = [new ApprovalPolicyLevel { Order = 1, Threshold = 0m, PermissionCode = permiso, CreatedBy = SeedContext.ParametricCreatedBy }],
        });
        return true;
    }

}
