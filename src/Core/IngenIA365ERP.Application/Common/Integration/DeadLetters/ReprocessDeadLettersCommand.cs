using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Mensajeria;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration.DeadLetters;

/// <summary>Un trabajo a reprocesar. (nuevo)</summary>
public sealed record DeadLetterRef(DeadLetterKind Kind, Guid PublicId);

/// <summary>Lo que se omitió y por qué. (nuevo)</summary>
public sealed record DeadLetterSkippedDto(Guid PublicId, string Code);

/// <summary>Lo que pasó con el reproceso. (nuevo)</summary>
public sealed record ReprocessDeadLettersResultDto(int Reprocessed, IReadOnlyList<DeadLetterSkippedDto> Skipped);

/// <summary>
/// Reprocesa mensajes muertos (feature 023, D3, D6; contracts/api.md §4; <c>POST /api/platform/dead-letters/reprocess</c>,
/// <c>Platform.DeadLetters.Reprocess</c>). Por cada uno: reinicia la cuenta de intentos transitorios, quita la marca de muerto, lo deja para
/// ya y atiende su alerta; después del commit avisa a la cola. El motivo es obligatorio y el evento va por la auditoría encadenada
/// (<c>AuditBehavior</c>: el espacio de nombres lo pone en el módulo <c>Integration</c>, que sale por <c>COR_AuditOutbox</c>). Lo que ya no está
/// muerto se omite con <c>DeadLetters.NotDead</c> sin fallar el resto. Ningún reproceso cambia el número ni el orden fiscal: sólo vuelve a
/// poner la fila en la fila. (nuevo)
/// </summary>
public sealed record ReprocessDeadLettersCommand(IReadOnlyList<DeadLetterRef> Items, string Reason)
    : IRequest<Result<ReprocessDeadLettersResultDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReprocessDeadLettersCommandValidator : AbstractValidator<ReprocessDeadLettersCommand>
{
    public ReprocessDeadLettersCommandValidator()
    {
        RuleFor(x => x.Items).NotNull().Must(i => i.Count is >= 1 and <= 200).WithMessage("Elija entre uno y doscientos trabajos para reprocesar.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.PublicId).NotEmpty();
            i.RuleFor(x => x.Kind).IsInEnum();
        });
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo del reproceso.")
            .Length(10, 500).WithMessage("El motivo debe tener entre 10 y 500 caracteres.");
    }
}

public sealed class ReprocessDeadLettersCommandHandler(
    IApplicationDbContext db,
    IAlertas alertas,
    IDateTimeService reloj,
    PublicacionDeAvisos publicacion) : IRequestHandler<ReprocessDeadLettersCommand, Result<ReprocessDeadLettersResultDto>>
{
    public async Task<Result<ReprocessDeadLettersResultDto>> Handle(ReprocessDeadLettersCommand request, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        var omitidos = new List<DeadLetterSkippedDto>();
        var hechos = 0;
        var claves = new List<string>();
        var encontrados = 0;

        var ids = request.Items.Where(i => i.Kind != DeadLetterKind.IntegrationDelivery).Select(i => i.PublicId).Distinct().ToList();
        var documentos = ids.Count == 0 ? [] : await db.ElectronicDocuments.Where(d => ids.Contains(d.PublicId)).ToListAsync(ct);
        var idsEntregas = request.Items.Where(i => i.Kind == DeadLetterKind.IntegrationDelivery).Select(i => i.PublicId).Distinct().ToList();
        var entregas = idsEntregas.Count == 0 ? [] : await db.IntegrationMessageDeliveries.Where(d => idsEntregas.Contains(d.PublicId)).ToListAsync(ct);

        foreach (var item in request.Items.DistinctBy(i => (i.Kind, i.PublicId)))
        {
            switch (item.Kind)
            {
                case DeadLetterKind.ElectronicDocument:
                {
                    var d = documentos.FirstOrDefault(x => x.PublicId == item.PublicId);
                    if (d is null) { omitidos.Add(new(item.PublicId, ErroresDeMensajesMuertos.NotFoundCode)); continue; }
                    encontrados++;
                    if (d.DeadAt is null) { omitidos.Add(new(item.PublicId, ErroresDeMensajesMuertos.NotDeadCode)); continue; }
                    d.DeadAt = null;
                    d.DeadAttemptLimit = null;
                    d.TransientFailures = 0;
                    d.NextAttemptAt = ahora;
                    d.EnqueuedAt = null;
                    claves.Add(IntentoAnteElCanal.ClaveDeMuerto(d));
                    publicacion.AvisarTrasElCommit(TiposDeAviso.FeEmitir, d.PublicId, "reproceso");
                    hechos++;
                    break;
                }
                case DeadLetterKind.ElectronicFollowUp:
                {
                    var d = documentos.FirstOrDefault(x => x.PublicId == item.PublicId);
                    if (d is null) { omitidos.Add(new(item.PublicId, ErroresDeMensajesMuertos.NotFoundCode)); continue; }
                    encontrados++;
                    if (d.FollowUpDeadAt is null) { omitidos.Add(new(item.PublicId, ErroresDeMensajesMuertos.NotDeadCode)); continue; }
                    d.FollowUpDeadAt = null;
                    d.FollowUpAttempts = 0;
                    d.FollowUpDueAt = ahora;
                    claves.Add(SeguimientoDelDocumento.ClaveDeAlerta(d));
                    publicacion.AvisarTrasElCommit(TiposDeAviso.FeSeguimiento, d.PublicId, "reproceso");
                    hechos++;
                    break;
                }
                default:
                {
                    var e = entregas.FirstOrDefault(x => x.PublicId == item.PublicId);
                    if (e is null) { omitidos.Add(new(item.PublicId, ErroresDeMensajesMuertos.NotFoundCode)); continue; }
                    encontrados++;
                    if (e.DeadAt is null || e.Status != DeliveryStatus.Pending) { omitidos.Add(new(item.PublicId, ErroresDeMensajesMuertos.NotDeadCode)); continue; }
                    e.DeadAt = null;
                    e.DeadAttemptLimit = null;
                    e.TransientFailures = 0;
                    e.NextAttemptAt = ahora;
                    publicacion.AvisarTrasElCommit(TiposDeAviso.IntegracionEntregar, e.Destination, "reproceso");
                    hechos++;
                    break;
                }
            }
        }

        if (encontrados == 0) return Result.Failure<ReprocessDeadLettersResultDto>(ErroresDeMensajesMuertos.NotFound());
        await db.SaveChangesAsync(ct);
        foreach (var clave in claves)
            await alertas.AtenderPorProcesoAsync(clave, $"Reprocesado: {request.Reason}", ct);
        return Result.Success(new ReprocessDeadLettersResultDto(hechos, omitidos));
    }
}
