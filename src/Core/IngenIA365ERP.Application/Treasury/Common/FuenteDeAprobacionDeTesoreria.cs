using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;

namespace IngenIA365ERP.Application.Treasury.Common;

/// <summary>
/// Lo que un documento de Tesorería hace cuando su aprobación se completa o se devuelve (feature 015, T0061): el lote pasa a <c>Approved</c> (US10, fase 7),
/// el uso del fondo de liquidez a <c>Approved</c>. Uno por sujeto; lo registra la historia que crea el documento. (nuevo)
/// </summary>
public interface IAprobableDeTesoreria
{
    string Sujeto { get; }

    Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct);

    Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct);
}

/// <summary>
/// La fuente de aprobación de los documentos de la Tesorería nueva (feature 015, T0061; US12; FR-012, FR-013; <c>SourceType = TreasuryDocument</c>). En cada
/// nivel, quien aprueba no es la misma <b>persona</b> que pidió ni que los participantes (quien preparó el lote: <c>Treasury.Batch.SelfApproval</c>), y con la
/// regla fija (sin política) su atribución del permiso con monto tiene que alcanzar el monto. La última aprobación la confirma el
/// <see cref="IAprobableDeTesoreria"/> del sujeto; sin uno registrado (antes de su fase) queda aprobada sin más. Aprobar no mueve dinero. (nuevo)
/// </summary>
public sealed class FuenteDeAprobacionDeTesoreria(IApplicationDbContext db, PersonasDeLosUsuarios personas, IEnumerable<IAprobableDeTesoreria> aprobables)
    : IFuenteDeAprobacion, IFuenteConAprobador
{
    public const string CodigoAtribucionInsuficiente = "Treasury.Approval.ApproverLimitTooLow";

    public string SourceType => ApprovalSourceTypes.TreasuryDocument;

    public async Task<Result> AlAprobarElNivelAsync(ApprovalRequest solicitud, int aprobadorUserId, ApprovalMethod metodo, bool esElUltimo, CancellationToken ct)
    {
        if (await personas.AlgunaEsLaMismaAsync(aprobadorUserId, [solicitud.RequestedByUserId, solicitud.CreatedByUserId, .. solicitud.Excluidos()], ct))
            return Result.Failure(solicitud.Subject switch
            {
                ApprovalSubjects.LiquidityFundUse => ErroresDeTesoreria.UsoDelFondoAutoaprobado(),
                // T1 (fase 33): el traslado entre cuentas propias.
                ApprovalSubjects.TreasuryTransfer => ErroresDeTesoreria.TrasladoAutoaprobado(),
                _ => ErroresDeTesoreria.LoteAutoaprobado(),
            });
        if (solicitud.PolicyId is not null || EvaluadorDePolitica.ReglaFija(solicitud.Subject) is not { } fija) return Result.Success();
        if (await LimitesDePermisoPorUsuario.DeAsync(db, aprobadorUserId, fija.PermissionCode, solicitud.OperationDate, ct) is { } maximo && maximo < solicitud.Amount)
            return Result.Failure(new ErrorConDatos(CodigoAtribucionInsuficiente,
                $"Quien aprueba tiene una atribución de {maximo:N2} y el documento es de {solicitud.Amount:N2}: debe aprobar alguien con un monto que alcance, o la cooperativa configura niveles sucesivos.",
                new { permissionCode = fija.PermissionCode, amount = solicitud.Amount, maxAmount = maximo }));
        return Result.Success();
    }

    public Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct) =>
        Del(solicitud.Subject) is { } aprobable
            ? aprobable.AlAprobarAsync(solicitud, ct)
            : Task.FromResult(Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, SourceType, ApprovalRequestStatus.Approved.ToString(), solicitud.SourceLabel)));

    public Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct) =>
        Del(solicitud.Subject) is { } aprobable
            ? aprobable.AlDevolverAsync(solicitud, motivo, ct)
            : Task.FromResult(Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, SourceType, "Draft", solicitud.SourceLabel)));

    public Task<bool> EnAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct) => Task.FromResult(true);

    public Task<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>> DescribirAsync(IReadOnlyCollection<Guid> sourcePublicIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>>(new Dictionary<Guid, OrigenDeAprobacionDto>());

    private IAprobableDeTesoreria? Del(string sujeto) => aprobables.FirstOrDefault(a => a.Sujeto == sujeto);
}
