using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// La fuente de aprobación de las operaciones de Recaudo con aprobador presente (feature 015, T0061; US12; FR-012, FR-013; <c>SourceType =
/// ReceiptsOperation</c>). Molde: <c>FuenteDeAprobacionDeCuenta</c> de la 014. Aprobar <b>no mueve dinero</b>: la operación la repite la caja con la misma
/// clave y <c>X-Approval-Request</c> (<see cref="AprobacionPresenteEnCaja"/>). En cada nivel:
/// <list type="bullet">
/// <item>quien aprueba no es la misma <b>persona</b> que pidió ni que los excluidos (el cajero de la sesión, quien registró): el motor compara usuarios y
/// esta fuente compara personas (<see cref="PersonasDeLosUsuarios"/>);</item>
/// <item>con la regla fija (sin política de la cooperativa), un permiso con monto (<c>Receipts.Count.ApproveDifference</c>, <c>Receipts.Receipt.Backdate</c>,
/// <c>Receipts.Voiding.Approve</c>) exige que la atribución del aprobador alcance (<c>SEC_PermissionAmountLimits</c>; sin valor sembrado, sin límite).</item>
/// </list>
/// El rechazo o el retiro no dejan nada que devolver: la operación nunca se escribió. (nuevo)
/// </summary>
public sealed class FuenteDeAprobacionDeRecaudo(IApplicationDbContext db, PersonasDeLosUsuarios personas) : IFuenteDeAprobacion, IFuenteConAprobador
{
    public const string CodigoAutoaprobacion = "Receipts.Approval.SelfApproval";
    public const string CodigoAtribucionInsuficiente = "Receipts.Approval.ApproverLimitTooLow";

    private static readonly string[] SujetosConMonto =
        [ApprovalSubjects.CashCountDifference, ApprovalSubjects.ReceiptBackdate, ApprovalSubjects.ReceiptVoiding,
            ApprovalSubjects.CashDisbursementApproval];

    public string SourceType => ApprovalSourceTypes.ReceiptsOperation;

    public async Task<Result> AlAprobarElNivelAsync(ApprovalRequest solicitud, int aprobadorUserId, ApprovalMethod metodo, bool esElUltimo, CancellationToken ct)
    {
        if (await personas.AlgunaEsLaMismaAsync(aprobadorUserId, [solicitud.RequestedByUserId, solicitud.CreatedByUserId, .. solicitud.Excluidos()], ct))
            return Result.Failure(new Error(CodigoAutoaprobacion,
                "Quien aprueba es la misma persona que registró la operación: debe aprobarla otra persona con el permiso del nivel."));

        if (solicitud.PolicyId is not null || !SujetosConMonto.Contains(solicitud.Subject, StringComparer.Ordinal)) return Result.Success();
        var permiso = EvaluadorDePolitica.ReglaFija(solicitud.Subject)!.PermissionCode;
        if (await LimitesDePermisoPorUsuario.DeAsync(db, aprobadorUserId, permiso, solicitud.OperationDate, ct) is { } maximo && maximo < solicitud.Amount)
            return Result.Failure(new ErrorConDatos(CodigoAtribucionInsuficiente,
                $"Quien aprueba tiene una atribución de {maximo:N2} y la operación es de {solicitud.Amount:N2}: debe aprobar alguien con un monto que alcance.",
                new { permissionCode = permiso, amount = solicitud.Amount, maxAmount = maximo }));
        return Result.Success();
    }

    public Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct) =>
        Task.FromResult(Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, SourceType, ApprovalRequestStatus.Approved.ToString(), solicitud.SourceLabel)));

    public Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct) =>
        Task.FromResult(Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, SourceType, "NotApplied", solicitud.SourceLabel)));

    /// <summary>La aprobación no lleva bodega ni punto de venta: el motor no pregunta el alcance de Inventario.</summary>
    public Task<bool> EnAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct) => Task.FromResult(true);

    public Task<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>> DescribirAsync(IReadOnlyCollection<Guid> sourcePublicIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>>(new Dictionary<Guid, OrigenDeAprobacionDto>());
}
