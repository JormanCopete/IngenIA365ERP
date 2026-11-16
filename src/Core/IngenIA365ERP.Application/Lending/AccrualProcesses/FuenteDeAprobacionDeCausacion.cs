using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.AccrualProcesses;

/// <summary>
/// La fuente de aprobación del proceso de causación (2026-11-16, decisión del dueño 6: «borrador libre; en firme con cuatro ojos»; <c>SourceType =
/// LendingAccrualProcess</c>). La pide quien pone en firme o reprocesa (<c>POST /api/lending/accrual-processes/{id}/confirm</c>, con motivo); la decide
/// <b>otra persona</b> con <c>Lending.AccrualProcesses.Confirm</c>, <b>presente</b> en el equipo de quien pidió con su passkey o su TOTP
/// (<c>/approvals/{id}/presence-challenge</c> y <c>/decide</c>), nunca desde la bandeja con su propia sesión. En cada nivel compara personas, no sólo
/// usuarios. La última aprobación pone el proceso en firme <b>en la misma transacción</b> por <see cref="ProcesosDeCausacion.PonerEnFirmeAsync"/>: si lo
/// calculado cambió, la decisión no queda. Rechazar o retirar devuelve el proceso a borrador. (nuevo)
/// </summary>
public sealed class FuenteDeAprobacionDeCausacion(IApplicationDbContext db, PersonasDeLosUsuarios personas, ProcesosDeCausacion procesos)
    : IFuenteDeAprobacion, IFuenteConAprobador
{
    private int? _aprobador;
    private ApprovalMethod _metodo = ApprovalMethod.OwnSession;

    public string SourceType => ApprovalSourceTypes.LendingAccrualProcess;

    public async Task<Result> AlAprobarElNivelAsync(ApprovalRequest solicitud, int aprobadorUserId, ApprovalMethod metodo, bool esElUltimo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (metodo is not (ApprovalMethod.InPersonPasskey or ApprovalMethod.InPersonTotp))
            return Result.Failure(ErroresDeProcesosDeCausacion.PresenciaRequerida());
        if (await personas.AlgunaEsLaMismaAsync(aprobadorUserId, [solicitud.RequestedByUserId, solicitud.CreatedByUserId, .. solicitud.Excluidos()], ct))
            return Result.Failure(ErroresDeProcesosDeCausacion.Autoaprobacion());
        _aprobador = aprobadorUserId;
        _metodo = metodo;
        return Result.Success();
    }

    public async Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (_aprobador is not { } aprobador) return Result.Failure<EstadoDeFuenteDto>(ErroresDeProcesosDeCausacion.PresenciaRequerida());
        var nombre = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == aprobador)
            .Select(u => u.Email ?? u.Username).FirstOrDefaultAsync(ct) ?? string.Empty;
        var enFirme = await procesos.PonerEnFirmeAsync(solicitud, aprobador, nombre, _metodo, ct);
        return enFirme.IsFailure
            ? Result.Failure<EstadoDeFuenteDto>(enFirme.Error)
            : Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, SourceType, AccrualProcessStatus.Confirmed.ToString(), enFirme.Value.Number));
    }

    public async Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var proceso = await db.AccrualProcesses.FirstOrDefaultAsync(p => p.PublicId == solicitud.SourcePublicId && !p.IsDeleted, ct);
        if (proceso is { Status: AccrualProcessStatus.PendingApproval } && proceso.ApprovalRequestPublicId == solicitud.PublicId)
            proceso.DevolverABorrador();
        return Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, SourceType, (proceso?.Status ?? AccrualProcessStatus.Draft).ToString(),
            proceso?.Number ?? solicitud.SourceLabel));
    }

    public Task<bool> EnAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct) => Task.FromResult(true);

    public async Task<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>> DescribirAsync(IReadOnlyCollection<Guid> sourcePublicIds, CancellationToken ct)
    {
        var filas = await db.AccrualProcesses.AsNoTracking().Where(p => sourcePublicIds.Contains(p.PublicId))
            .Select(p => new { p.PublicId, p.Number, p.DateFrom, p.DateTo, p.IncludedAmount, p.IncludedFactCount })
            .ToListAsync(ct);
        return filas.ToDictionary(f => f.PublicId, f => new OrigenDeAprobacionDto(f.PublicId, "LendingAccrualProcess", null, f.Number, f.DateTo, null, null,
            $"Proceso de causación {f.Number} del {f.DateFrom:yyyy-MM-dd} al {f.DateTo:yyyy-MM-dd}: {f.IncludedFactCount} hechos por {f.IncludedAmount:N2}. "
            + "Lo aprueba otra persona presente, en el equipo de quien lo pidió.", "/cartera/causacion"));
    }
}
