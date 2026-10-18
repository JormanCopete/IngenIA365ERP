using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Operations;

/// <summary>
/// La fuente de aprobación de los movimientos de cuenta sobre la atribución (feature 014, US4, T111; api.md §1.6 de la 014; FR-051, FR-059, US4-5;
/// <c>SourceType = MemberAccountOperation</c>). Aprobar <b>no mueve saldo</b>: la operación la repite el recaudador (o la pantalla) con la misma clave y el
/// <c>approvalPublicId</c> aprobado, y el contrato compara la huella. Quien aprueba —un presente con segundo factor, por la mecánica de la plataforma— tiene
/// que tener el mismo permiso con un monto que alcance (<c>SEC_PermissionAmountLimits</c>). El rechazo o el retiro no dejan nada que devolver: la operación
/// nunca se escribió. La cuenta es la fuente (<c>SourcePublicId</c>): la mayoría de edad del titular anula las pendientes del representante (FR-033). (nuevo)
/// </summary>
public sealed class FuenteDeAprobacionDeCuenta(IApplicationDbContext db) : IFuenteDeAprobacion, IFuenteConAprobador
{
    public string SourceType => ApprovalSourceTypes.MemberAccountOperation;

    public async Task<Result> AlAprobarElNivelAsync(ApprovalRequest solicitud, int aprobadorUserId, ApprovalMethod metodo, bool esElUltimo, CancellationToken ct)
    {
        var permiso = solicitud.Subject == ApprovalSubjects.AccountWithdrawal ? ReglasDeMovimientos.PermisoDeRetiro : ReglasDeMovimientos.PermisoDeConsignacion;
        if (await LimitesDePermisoPorUsuario.DeAsync(db, aprobadorUserId, permiso, solicitud.OperationDate, ct) is { } maximo && maximo < solicitud.Amount)
            return Result.Failure(new ErrorConDatos("Lending.Savings.ApproverLimitTooLow",
                $"Quien aprueba tiene una atribución de {maximo:N2} y la operación es de {solicitud.Amount:N2}: debe aprobar alguien con un monto que alcance.",
                new { permissionCode = permiso, amount = solicitud.Amount, maxAmount = maximo }));
        return Result.Success();
    }

    public async Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct) =>
        await db.MemberAccounts.AsNoTracking().AnyAsync(a => a.PublicId == solicitud.SourcePublicId && !a.IsDeleted, ct)
            ? Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, SourceType, ApprovalRequestStatus.Approved.ToString(), solicitud.SourceLabel))
            : Result.Failure<EstadoDeFuenteDto>(Error.NotFound);

    public Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct) =>
        Task.FromResult(Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, SourceType, "NotApplied", solicitud.SourceLabel)));

    /// <summary>La aprobación no lleva bodega ni punto de venta: el motor no pregunta el alcance de Inventario.</summary>
    public Task<bool> EnAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct) => Task.FromResult(true);

    public async Task<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>> DescribirAsync(IReadOnlyCollection<Guid> sourcePublicIds, CancellationToken ct)
    {
        var ids = sourcePublicIds.ToArray();
        var cuentas = await db.MemberAccounts.AsNoTracking().Where(a => ids.Contains(a.PublicId))
            .Select(a => new { a.PublicId, a.Number, a.OpenedOn }).ToListAsync(ct);
        return cuentas.ToDictionary(c => c.PublicId, c => new OrigenDeAprobacionDto(c.PublicId, "MemberAccount", null, c.Number, c.OpenedOn, null, null,
            $"Movimiento de la cuenta {c.Number} sobre la atribución de quien lo registra", $"/cartera/ahorros/{c.PublicId}"));
    }
}
