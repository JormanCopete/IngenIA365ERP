using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;

namespace IngenIA365ERP.Application.Attachments.Common;

/// <summary>
/// Los adjuntos que <b>genera un módulo</b> los gobierna ese módulo, no el permiso genérico
/// <c>Attachments.*</c>. El PDF para firma de la liquidación definitiva (feature 010, D-36) vive en
/// <c>COR_Attachments</c> con <c>OwnerEntityType = "EmploymentTermination"</c>; sin esta tabla, quien
/// tuviera <c>Attachments.Download</c> lo descargaba sin <c>Payroll.Settlements.View</c> y el Operador
/// —que por diseño no aprueba ni ajusta descuentos— lo borraba con <c>Attachments.Delete</c>, dejando la
/// terminación apuntando a un adjunto eliminado. Aquí se declara, por tipo de dueño, qué permiso exige
/// leerlo y si se puede borrar por la ruta genérica; los tipos que no figuran (adjuntos que sube una
/// persona: <c>User</c>, <c>Loan</c>, <c>Transaction</c>…) siguen con <c>Attachments.*</c>. Los archivos
/// de N2–N4 (PILA, XML DIAN, plano bancario) entran aquí cuando existan.
/// </summary>
public static class AdjuntosDeModulo
{
    /// <summary>Qué permiso exige leer el adjunto y si la ruta genérica puede borrarlo.</summary>
    public sealed record Regla(string PermisoDeLectura, bool Borrable, string Descripcion);

    private static readonly IReadOnlyDictionary<string, Regla> Reglas = new Dictionary<string, Regla>(StringComparer.Ordinal)
    {
        ["EmploymentTermination"] = new("Payroll.Settlements.View", Borrable: false, "el documento para firma de la liquidación definitiva"),
        ["BankDisbursementFile"] = new("Payroll.Disbursement.View", Borrable: false, "el archivo de dispersión bancaria que se entregó al banco"),
    };

    /// <summary>La regla del tipo de dueño, o nula si el adjunto es de una persona y lo gobierna <c>Attachments.*</c>.</summary>
    public static Regla? De(string? ownerEntityType) =>
        ownerEntityType is not null && Reglas.TryGetValue(ownerEntityType, out var regla) ? regla : null;

    /// <summary>
    /// Verdadero si quien pide puede leer un adjunto de ese dueño: sin regla, siempre (la ruta ya exigió
    /// <c>Attachments.Download</c>); con regla, sólo con el permiso del módulo. Sin él la respuesta es la
    /// misma que si no existiera (FR-017 de la 002).
    /// </summary>
    public static async Task<bool> PuedeLeerAsync(IPermissionChecker permisos, string? ownerEntityType, CancellationToken ct)
    {
        var regla = De(ownerEntityType);
        return regla is null || await permisos.HasPermissionAsync(regla.PermisoDeLectura, ct);
    }

    /// <summary>El error de borrar por la ruta genérica un adjunto que su módulo declara inmutable.</summary>
    public static Error NoBorrable(Regla regla) =>
        new(AttachmentErrorCodes.OwnedByModule,
            $"Este adjunto es {regla.Descripcion}: lo generó el programa y no se borra; se conserva con la operación que lo produjo (reverse la operación si hace falta).");
}
