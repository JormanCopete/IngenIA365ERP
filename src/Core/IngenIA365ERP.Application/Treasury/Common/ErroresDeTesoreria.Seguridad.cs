using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Treasury.Common;

// Feature 015, US12 (T0060, T0061; data-model §21): los errores de segregación y de aprobación diferida de la Tesorería nueva. (nuevo)
public static partial class ErroresDeTesoreria
{
    public const string CodigoLoteAutoaprobado = "Treasury.Batch.SelfApproval";
    public const string CodigoDescargaPorQuienPreparo = "Treasury.File.SameUserDownload";
    public const string CodigoConciliacionConflicto = "Treasury.Reconciliation.SegregationConflict";
    public const string CodigoResultadoPorTerceraPersona = "Treasury.Result.ThirdPersonRequired";
    public const string CodigoAprobacionPendiente = "Treasury.Approval.Pending";

    public static Error LoteAutoaprobado() => new(CodigoLoteAutoaprobado,
        "Usted preparó este lote: no puede aprobarlo. Lo aprueba otra persona con el permiso de aprobar lotes.");

    public static Error DescargaPorQuienPreparo() => new(CodigoDescargaPorQuienPreparo,
        "Usted preparó o aprobó este lote: no puede descargar su archivo. Lo descarga otra persona con el permiso.");

    public static Error ConciliacionConflicto() => new(CodigoConciliacionConflicto,
        "Usted preparó o aprobó pagos de esta cuenta en el período: no puede conciliarla. La concilia otra persona.");

    public static Error ResultadoPorTerceraPersona() => new(CodigoResultadoPorTerceraPersona,
        "La cooperativa exige que el resultado del lote lo registre una persona distinta de quien lo preparó y de quien lo aprobó.");

    public static Error AprobacionPendiente(Guid solicitud, int nivel, decimal monto) => new ErrorConDatos(CodigoAprobacionPendiente,
        "El documento espera su aprobación: cuando los niveles que exige el monto la completen, sigue su curso. Aprobar no mueve dinero.",
        new { approvalRequestPublicId = solicitud, currentLevel = nivel, amount = monto });
}
