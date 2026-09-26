using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Alerts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Alerts.AttendAlert;

/// <summary>
/// Atiende una alerta (feature 012, T39, T093; FR-022; contracts/api.md §16.1,
/// <c>POST /api/inventory/alerts/{id}/attend</c>, <c>Inventory.Alerts.Attend</c>, con <c>Idempotency-Key</c>). El estado
/// es compartido: queda atendida para todos, con quién, cuándo y la nota. Atender no corrige la causa.
/// </summary>
public sealed record AttendAlertCommand(Guid AlertPublicId, string Note) : IRequest<Result<AlertDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

/// <summary>La nota es obligatoria (hasta 1000, el largo de la columna).</summary>
public sealed class AttendAlertCommandValidator : AbstractValidator<AttendAlertCommand>
{
    public AttendAlertCommandValidator()
    {
        RuleFor(x => x.AlertPublicId).NotEmpty();
        RuleFor(x => x.Note)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Contá qué hiciste con la alerta.")
            .MaximumLength(1000).WithMessage("La nota admite hasta 1000 caracteres.");
    }
}

/// <summary>
/// Sólo quien la ve puede atenderla (<see cref="VisibilidadDeAlertas"/>; si no, 404 <c>Alerts.Alert.NotFound</c>). Ya
/// atendida: 422 <c>Alerts.Alert.AlreadyAttended</c> con quién y cuándo. La persona es la de <see cref="IActorActual"/>
/// (<c>SEC_Users.Id</c>), nunca el entero del token.
/// </summary>
public sealed class AttendAlertCommandHandler(
    IApplicationDbContext db,
    VisibilidadDeAlertas visibilidad,
    IActorActual actorActual,
    IDateTimeService reloj)
    : IRequestHandler<AttendAlertCommand, Result<AlertDto>>
{
    public async Task<Result<AlertDto>> Handle(AttendAlertCommand request, CancellationToken ct)
    {
        var visibles = await visibilidad.VisiblesAsync(ct);
        var alerta = await visibles.FirstOrDefaultAsync(a => a.PublicId == request.AlertPublicId, ct);
        if (alerta is null) return Result.Failure<AlertDto>(ErroresDeAlertas.AlertaInexistente());

        if (alerta.Status == AlertStatus.Attended)
            return Result.Failure<AlertDto>(ErroresDeAlertas.YaAtendida(alerta.AttendedByName, alerta.AttendedAt));

        var actor = await actorActual.ObtenerAsync(ct);
        var nombre = actor.Name.Length > 150 ? actor.Name[..150] : actor.Name;
        alerta.Atender(actor.Kind, actor.UserId, nombre, request.Note.Trim(), reloj.UtcNow);
        await db.SaveChangesAsync(ct);

        return Result.Success(ProyeccionDeAlertas.ADto(alerta));
    }
}
