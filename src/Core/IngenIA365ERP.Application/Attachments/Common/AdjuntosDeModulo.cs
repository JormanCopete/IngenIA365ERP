using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;

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
/// <b>Destinos de subida habilitados</b>: sólo el comprobante contable. Los tipos de módulo nunca, y
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
        var permiso = ownerEntityType == Comprobante ? LeerComprobantes : De(ownerEntityType)?.PermisoDeLectura;
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
        IApplicationDbContext db, IPermissionChecker permisos, string? ownerEntityType, Guid ownerEntityPublicId, CancellationToken ct)
    {
        if (De(ownerEntityType) is { } deModulo)
            return new Error(AttachmentErrorCodes.OwnerNotAllowed,
                $"No se pueden subir archivos a {deModulo.Descripcion}: lo genera el programa.");
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
        IApplicationDbContext db, string? ownerEntityType, Guid ownerEntityPublicId, CancellationToken ct)
    {
        if (De(ownerEntityType) is { Borrable: false } regla)
            return NoBorrable(regla);
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

    /// <summary>El error de borrar por la ruta genérica un adjunto que su módulo declara inmutable.</summary>
    public static Error NoBorrable(Regla regla) =>
        new(AttachmentErrorCodes.OwnedByModule,
            $"Este adjunto es {regla.Descripcion}: lo generó el programa y no se borra; se conserva con la operación que lo produjo (reverse la operación si hace falta).");
}
