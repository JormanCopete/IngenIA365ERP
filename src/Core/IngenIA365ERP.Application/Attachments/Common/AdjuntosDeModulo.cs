using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using ClaseDeDocumento = IngenIA365ERP.Domain.Enums.Inventory.DocumentClass;
using EstadoDelDocumentoDeInventario = IngenIA365ERP.Domain.Enums.Inventory.DocumentStatus;

namespace IngenIA365ERP.Application.Attachments.Common;

/// <summary>
/// Quién puede leer, subir y borrar los adjuntos de cada tipo de dueño. Es el único sitio que lo decide.
///
/// <para>
/// <b>Adjuntos que genera un módulo</b> (feature 010, D-36): el PDF para firma de la liquidación
/// definitiva, el archivo de dispersión, la planilla PILA. Los gobierna ese módulo, no el permiso
/// genérico <c>Attachments.*</c>: leerlos exige el permiso del módulo y la ruta genérica no los borra.
/// Antes, quien tuviera <c>Attachments.Download</c> descargaba la definitiva sin
/// <c>Payroll.Settlements.View</c>, y el Operador la borraba con <c>Attachments.Delete</c>.
/// </para>
///
/// <para>
/// <b>Soportes de comprobantes</b> (feature 011, R9): hasta el 2026-09-23 no tenían regla, así que
/// (a) se leían sin permiso de contabilidad, sólo con el genérico de adjuntos (FR-025), (b) se podía
/// colgar un archivo de cualquier documento —incluida la PILA de otro, que después nadie podía borrar—
/// porque subir no miraba el dueño (FR-019), y (c) el soporte de un comprobante contabilizado se podía
/// borrar por la API, porque sólo la pantalla escondía el botón (FR-004). Las tres cosas se deciden ahora
/// aquí, contra el comprobante mismo.
/// </para>
///
/// <para>
/// <b>Destinos de subida habilitados</b>: el comprobante contable y las imágenes del producto de inventario
/// (<see cref="ProductoDeInventario"/>, feature 012, T221: leer con <c>Inventory.Catalog.View</c>, subir y borrar con
/// <c>Inventory.Catalog.Manage</c>, sólo JPEG, PNG y WebP, el producto tiene que existir) y los soportes de un ajuste de
/// inventario (<see cref="SoporteDeAjuste"/>, feature 012, T255: actas de destrucción, denuncias; subir con
/// <c>Inventory.Adjustments.Create</c> mientras el ajuste está en borrador o en aprobación, leer con
/// <c>Inventory.Adjustments.View</c>, y confirmado no se borran: <c>Attachments.OwnerLocked</c>). Los tipos de módulo nunca, y
/// cualquier otro tipo tampoco hasta que tenga su pantalla y su regla (FR-019).
/// </para>
/// </summary>
public static class AdjuntosDeModulo
{
    /// <summary>Qué permiso exige leer el adjunto y si la ruta genérica puede borrarlo.</summary>
    public sealed record Regla(string PermisoDeLectura, bool Borrable, string Descripcion);

    /// <summary>El tipo de dueño de los soportes de un comprobante contable.</summary>
    public const string Comprobante = "AccountingDocument";

    /// <summary>El permiso genérico de borrar adjuntos; además, la regla del dueño (<see cref="PuedeBorrarAsync"/>).</summary>
    public const string PermisoDeBorrar = "Attachments.Delete";

    /// <summary>El tipo de dueño de las imágenes de un producto de inventario (feature 012, T41, T221; FR-029).</summary>
    public const string ProductoDeInventario = "InventoryProduct";

    /// <summary>Los únicos tipos de archivo que admite la imagen de un producto.</summary>
    public static readonly IReadOnlySet<string> TiposDeImagenDeProducto =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    /// <summary>El tipo de dueño de los soportes de un ajuste de inventario (feature 012, T41, T255; FR-037; contracts/api.md §10).</summary>
    public const string SoporteDeAjuste = "InventoryAdjustmentSupport";

    private const string VerAjustes = "Inventory.Adjustments.View";
    private const string CrearAjustes = "Inventory.Adjustments.Create";

    /// <summary>Las clases de un ajuste de I1 cuyo documento admite soportes (el grupo <c>Adjustments</c>).</summary>
    private static readonly ClaseDeDocumento[] ClasesDeAjuste =
    [
        ClaseDeDocumento.PositiveAdjustment, ClaseDeDocumento.NegativeAdjustment, ClaseDeDocumento.InternalConsumption,
        ClaseDeDocumento.WriteOff, ClaseDeDocumento.Assembly, ClaseDeDocumento.LocationMove,
    ];

    private const string VerCatalogo = "Inventory.Catalog.View";
    private const string AdministrarCatalogo = "Inventory.Catalog.Manage";

    private const string LeerComprobantes = "Accounting.Vouchers.View";
    private const string EscribirComprobantes = "Accounting.Vouchers.Create";

    /// <summary>Los tipos que genera un módulo: inmutables y nunca destino de una subida de personas.</summary>
    private static readonly IReadOnlyDictionary<string, Regla> DeModulo = new Dictionary<string, Regla>(StringComparer.Ordinal)
    {
        ["EmploymentTermination"] = new("Payroll.Settlements.View", Borrable: false, "el documento para firma de la liquidación definitiva"),
        ["BankDisbursementFile"] = new("Payroll.Disbursement.View", Borrable: false, "el archivo de dispersión bancaria que se entregó al banco"),
        ["PilaGeneration"] = new("Payroll.Pila.View", Borrable: false, "la planilla PILA tal como se generó y se cargó en el operador"),
    };

    /// <summary>La regla de un tipo que genera un módulo, o nula si no lo es.</summary>
    public static Regla? De(string? ownerEntityType) =>
        ownerEntityType is not null && DeModulo.TryGetValue(ownerEntityType, out var regla) ? regla : null;

    /// <summary>
    /// Verdadero si quien pide puede leer un adjunto de ese dueño. Los tipos de módulo exigen el permiso
    /// del módulo; los soportes de comprobantes, el de consultar comprobantes (FR-025); el resto, sólo el
    /// genérico, que la ruta ya exigió. Sin permiso la respuesta es la misma que si no existiera.
    /// </summary>
    public static async Task<bool> PuedeLeerAsync(IPermissionChecker permisos, string? ownerEntityType, CancellationToken ct)
    {
        var permiso = ownerEntityType switch
        {
            Comprobante => LeerComprobantes,
            ProductoDeInventario => VerCatalogo,
            SoporteDeAjuste => VerAjustes,
            _ => De(ownerEntityType)?.PermisoDeLectura,
        };
        return permiso is null || await permisos.HasPermissionAsync(permiso, ct);
    }

    /// <summary>
    /// Nulo si quien pide puede subir un adjunto a ese dueño; si no, el error (FR-019):
    /// <c>OwnerNotAllowed</c> (422) si el tipo no admite subidas de personas, y <c>Generic.NotFound</c>
    /// (404) si el comprobante no existe en la cooperativa <b>o</b> falta el permiso de escribir
    /// comprobantes —la misma respuesta, para no revelar qué comprobantes existen—. Con una base por
    /// cooperativa, un comprobante de otra simplemente no está.
    /// </summary>
    public static async Task<Error?> PuedeSubirAsync(
        IApplicationDbContext db, IPermissionChecker permisos, string? ownerEntityType, Guid ownerEntityPublicId, CancellationToken ct,
        string? contentType = null)
    {
        if (De(ownerEntityType) is { } deModulo)
            return new Error(AttachmentErrorCodes.OwnerNotAllowed,
                $"No se pueden subir archivos a {deModulo.Descripcion}: lo genera el programa.");
        if (ownerEntityType == ProductoDeInventario)
        {
            if (contentType is not null && !TiposDeImagenDeProducto.Contains(contentType))
                return new Error(AttachmentErrorCodes.Validation_MimeTypeNotAllowed, "La imagen del producto va en JPEG, PNG o WebP.");
            if (!await permisos.HasPermissionAsync(AdministrarCatalogo, ct)
                || !await db.Products.AnyAsync(p => p.PublicId == ownerEntityPublicId, ct))
                return new Error("Generic.NotFound", "Producto no encontrado.");
            return null;
        }
        if (ownerEntityType == SoporteDeAjuste)
        {
            var ajuste = await db.InventoryDocuments.AsNoTracking()
                .Where(d => d.PublicId == ownerEntityPublicId && ClasesDeAjuste.Contains(d.Class))
                .Select(d => (EstadoDelDocumentoDeInventario?)d.Status)
                .FirstOrDefaultAsync(ct);
            if (ajuste is null || !await permisos.HasPermissionAsync(CrearAjustes, ct))
                return new Error("Generic.NotFound", "Ajuste no encontrado.");
            return ajuste is EstadoDelDocumentoDeInventario.Draft or EstadoDelDocumentoDeInventario.PendingApproval
                ? null
                : AjusteBloqueado();
        }
        if (ownerEntityType != Comprobante)
            return new Error(AttachmentErrorCodes.OwnerNotAllowed,
                "Este tipo de documento todavía no admite soportes.");
        if (!await permisos.HasPermissionAsync(EscribirComprobantes, ct)
            || !await db.AccountingDocuments.AnyAsync(d => d.PublicId == ownerEntityPublicId, ct))
            return new Error("Generic.NotFound", "Comprobante no encontrado.");
        return null;
    }

    /// <summary>
    /// Nulo si el adjunto se puede borrar; si no, el error. Los de módulo nunca (<c>OwnedByModule</c>).
    /// Los soportes de un comprobante sólo mientras esté en borrador (<c>OwnerLocked</c>, FR-004): uno
    /// contabilizado o reversado conserva sus soportes, como conserva sus movimientos (Principio XI). Si
    /// el comprobante ya no existe —un borrador descartado antes de esta regla—, no hay nada que proteger.
    /// </summary>
    public static async Task<Error?> PuedeBorrarAsync(
        IApplicationDbContext db, string? ownerEntityType, Guid ownerEntityPublicId, CancellationToken ct, IPermissionChecker? permisos = null)
    {
        if (De(ownerEntityType) is { Borrable: false } regla)
            return NoBorrable(regla);
        // La imagen de un producto la borra quien administra el catálogo (T221); el borrado queda auditado por el comando.
        if (ownerEntityType == ProductoDeInventario)
            return permisos is not null && await permisos.HasPermissionAsync(AdministrarCatalogo, ct)
                ? null
                : new Error("Generic.NotFound", "Adjunto no encontrado.");
        // Los soportes de un ajuste se borran sólo antes de confirmarlo (T255): confirmado o anulado, se conservan con él.
        if (ownerEntityType == SoporteDeAjuste)
        {
            var ajuste = await db.InventoryDocuments.AsNoTracking()
                .Where(d => d.PublicId == ownerEntityPublicId)
                .Select(d => (EstadoDelDocumentoDeInventario?)d.Status)
                .FirstOrDefaultAsync(ct);
            if (permisos is not null && !await permisos.HasPermissionAsync(CrearAjustes, ct))
                return new Error("Generic.NotFound", "Adjunto no encontrado.");
            return ajuste is EstadoDelDocumentoDeInventario.Confirmed or EstadoDelDocumentoDeInventario.Voided ? AjusteBloqueado() : null;
        }
        if (ownerEntityType != Comprobante)
            return null;

        var estado = await db.AccountingDocuments
            .Where(d => d.PublicId == ownerEntityPublicId)
            .Select(d => (DocumentStatus?)d.Status)
            .FirstOrDefaultAsync(ct);
        return estado is DocumentStatus.Posted or DocumentStatus.Reversed
            ? new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
                "Es soporte de un comprobante contabilizado: se conserva con el comprobante y no se puede borrar.",
                new { ownerEntityType })
            : null;
    }

    private static Error AjusteBloqueado() => new ErrorConDatos(AttachmentErrorCodes.OwnerLocked,
        "Es soporte de un ajuste confirmado: se conserva con el ajuste y no se puede cambiar.",
        new { ownerEntityType = SoporteDeAjuste });

    /// <summary>El error de borrar por la ruta genérica un adjunto que su módulo declara inmutable.</summary>
    public static Error NoBorrable(Regla regla) =>
        new(AttachmentErrorCodes.OwnedByModule,
            $"Este adjunto es {regla.Descripcion}: lo generó el programa y no se borra; se conserva con la operación que lo produjo (reverse la operación si hace falta).");
}
