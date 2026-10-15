using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.WriteOffs;

/// <summary>
/// Registra el reporte de los castigos de un lote al supervisor (feature 013, K3, US14, T0909; ruta nueva
/// <c>POST /api/lending/write-off-batches/{id}/supervisor-report</c>, <c>Lending.WriteOffs.Apply</c>, con clave; data-model §25.6; FR-095): una sola vez,
/// en un lote aplicado que debe reportarse (si no, o si ya se registró, <c>Lending.WriteOffBatch.InvalidStatus</c>), con la referencia del radicado y,
/// si viene, el soporte —un adjunto <c>LendingWriteOffSupervisorReport</c> del lote ya disponible; si no lo es, <c>Lending.WriteOff.AttachmentInvalid</c>—.
/// Da por atendida la alerta <c>Cartera.PlazoRegulatorioPorVencer</c> del lote. (nuevo)
/// </summary>
public sealed record RegisterWriteOffSupervisorReportCommand(Guid WriteOffBatchPublicId, string Reference, Guid? AttachmentPublicId = null)
    : IRequest<Result<WriteOffBatchDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterWriteOffSupervisorReportCommandValidator : AbstractValidator<RegisterWriteOffSupervisorReportCommand>
{
    public RegisterWriteOffSupervisorReportCommandValidator()
    {
        RuleFor(x => x.WriteOffBatchPublicId).NotEmpty();
        RuleFor(x => x.Reference).NotEmpty().WithMessage("Diga la referencia o el radicado del reporte al supervisor.").MaximumLength(60);
    }
}

public sealed class RegisterWriteOffSupervisorReportCommandHandler(
    IApplicationDbContext db,
    CastigosDeCartera castigos,
    IAlertas alertas,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterWriteOffSupervisorReportCommand, Result<WriteOffBatchDto>>
{
    public async Task<Result<WriteOffBatchDto>> Handle(RegisterWriteOffSupervisorReportCommand request, CancellationToken ct)
    {
        var leido = await CastigosDeCartera.LeerAsync(db, alcance, auditoria, request.WriteOffBatchPublicId, seguir: true, ct);
        if (leido.IsFailure) return Result.Failure<WriteOffBatchDto>(leido.Error);
        var (lote, items) = leido.Value;
        if (request.AttachmentPublicId is { } adjunto
            && !await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == adjunto && a.OwnerEntityType == AdjuntosDeModulo.ReporteDeCastigos
                && a.OwnerEntityPublicId == lote.PublicId && a.Status == EstadoDeAdjunto.Available, ct))
            return Result.Failure<WriteOffBatchDto>(ErroresDeCastigo.SoporteInvalido());
        var clave = ApplyWriteOffBatchCommandHandler.ClaveDeAlerta(lote);
        try
        {
            lote.RegistrarReporteAlSupervisor(reloj.UtcNow, request.Reference, request.AttachmentPublicId);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<WriteOffBatchDto>(ErroresDeGobierno.De(ex.Problema));
        }
        await db.SaveChangesAsync(ct);
        await alertas.AtenderPorProcesoAsync(clave, $"Reporte al supervisor registrado: {request.Reference.Trim()}.", ct);
        return Result.Success(await castigos.DtoAsync(lote, items, ct));
    }
}
