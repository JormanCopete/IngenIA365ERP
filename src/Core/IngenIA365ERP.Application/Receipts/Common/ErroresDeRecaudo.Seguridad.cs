using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Receipts.Common;

// Feature 015, US12 (T0058–T0061; data-model §21; contracts/api.md §1.4; DT §2.7): los errores de alcance y de aprobación presente de Recaudo. Lo que
// está fuera del alcance responde Generic.NotFound, igual que lo inexistente (SC-018): no hay un código propio para eso. (nuevo)
public static partial class ErroresDeRecaudo
{
    public const string CodigoNoEncontrado = "Generic.NotFound";
    public const string CodigoPresenciaRequerida = "Approvals.PresenceRequired";
    public const string CodigoAprobacionRechazada = "Receipts.Approval.Rejected";
    public const string CodigoAprobacionNoCorresponde = "Receipts.Approval.Mismatch";

    /// <summary>Inexistente o fuera del alcance: el mismo 404 (FR-011, SC-018).</summary>
    public static Error NoEncontrado(string recurso = "El registro") => new(CodigoNoEncontrado, $"{recurso} no existe.");

    /// <summary>
    /// 428: la operación necesita el aprobador presente. La pantalla pide el desafío para <paramref name="solicitud"/> y repite la operación con la misma
    /// <c>Idempotency-Key</c> y el encabezado <c>X-Approval-Request</c>.
    /// </summary>
    /// <summary>El 428 de la aprobación presente; <paramref name="huella"/> es lo que el aprobador confirma en su decisión (<c>expectedContentSha256</c>).</summary>
    public static Error PresenciaRequerida(Guid solicitud, string sujeto, decimal monto, string permiso, string? huella = null) => new ErrorConDatos(CodigoPresenciaRequerida,
        "La operación necesita la aprobación de otra persona presente, con su llave o su código: pídale que la apruebe en este equipo.",
        new { approvalRequestPublicId = solicitud, subject = sujeto, amount = monto, permissionCode = permiso, contentSha256 = huella });

    public static Error AprobacionRechazada(Guid solicitud) => new ErrorConDatos(CodigoAprobacionRechazada,
        "La aprobación fue rechazada: corrija la operación o cancélela.", new { approvalRequestPublicId = solicitud });

    /// <summary>La aprobación que trae <c>X-Approval-Request</c> no es de esta operación o cambió lo que se aprueba.</summary>
    public static Error AprobacionNoCorresponde(Guid solicitud) => new ErrorConDatos(CodigoAprobacionNoCorresponde,
        "La aprobación indicada no es de esta operación o lo que se aprobó cambió: pida una aprobación nueva.", new { approvalRequestPublicId = solicitud });
}
