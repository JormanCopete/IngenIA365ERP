using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Consultas;

// Feature 013, T0649 (contracts/contabilidad.md §4.4 y §15; T59, C33; dueño: pregunta C3, rige registrar con soporte y vigencia de 6 meses):
// el patrimonio técnico y la relación de solvencia que registra el contador, con su soporte. POST y GET /api/accounting/regulatory-indicators
// (Accounting.RegulatoryIndicators.Manage / .View; las rutas son de la API, bloque C). (nuevo)

/// <summary>Un indicador regulatorio registrado, con su unidad, si fue reemplazado y por cuál.</summary>
public sealed record RegulatoryIndicatorDto(
    Guid RegulatoryIndicatorPublicId,
    RegulatoryIndicatorKind Kind,
    DateOnly CutoffDate,
    decimal Value,
    string Unit,
    string Source,
    Guid? SupportAttachmentPublicId,
    Guid? SupersedesPublicId,
    Guid? SupersededByPublicId,
    string? Notes,
    DateTime RegisteredAt,
    string RegisteredBy);

/// <summary>
/// Registra el patrimonio técnico o la relación de solvencia de un corte (§15). Hecho de sólo inserción: corregir es registrar otro del
/// <b>mismo tipo y corte</b> que nombra al anterior en <see cref="SupersedesPublicId"/> (un indicador se reemplaza una sola vez). El soporte es
/// obligatorio: un adjunto de tipo <c>RegulatoryIndicatorSupport</c> ya subido, que desde aquí queda bloqueado. <see cref="PublicId"/> es el que
/// la pantalla reservó para subir el soporte (opcional). Evento <c>Accounting.RegulatoryIndicator.Registered</c>. (nuevo)
/// </summary>
public sealed record RegisterRegulatoryIndicatorCommand(
    RegulatoryIndicatorKind Kind,
    DateOnly CutoffDate,
    decimal Value,
    string Source,
    Guid? SupportAttachmentPublicId,
    Guid? SupersedesPublicId = null,
    string? Notes = null,
    Guid? PublicId = null) : IRequest<Result<Guid>>;

public sealed class RegisterRegulatoryIndicatorCommandValidator : AbstractValidator<RegisterRegulatoryIndicatorCommand>
{
    public RegisterRegulatoryIndicatorCommandValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().WithMessage("Indique si es el patrimonio técnico o la relación de solvencia.");
        RuleFor(x => x.CutoffDate).NotEqual(default(DateOnly)).WithMessage("Indique la fecha de corte.");
        RuleFor(x => x.Value).GreaterThan(0m).WithMessage("El valor tiene que ser mayor que cero.");
        RuleFor(x => x.Value).LessThanOrEqualTo(10m).When(x => x.Kind == RegulatoryIndicatorKind.SolvencyRatio)
            .WithMessage("La relación de solvencia va como fracción (0,12 = 12 %).");
        RuleFor(x => x.Source).NotEmpty().WithMessage("Indique de dónde sale (p. ej. «formato de patrimonio técnico de diciembre»).").MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.SupportAttachmentPublicId).NotNull().WithMessage("Adjunte el soporte del indicador.");
    }
}

public sealed class RegisterRegulatoryIndicatorCommandHandler(
    IApplicationDbContext db, ICurrentUserService usuario, IDateTimeService reloj, AccountingAuditEmitter auditoria)
    : IRequestHandler<RegisterRegulatoryIndicatorCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterRegulatoryIndicatorCommand request, CancellationToken ct)
    {
        if (request.SupportAttachmentPublicId is not { } soporte) return Result.Failure<Guid>(AccountingErrors.RegulatoryIndicatorSupportRequired);
        var adjunto = await db.Attachments.AsNoTracking()
            .AnyAsync(a => a.PublicId == soporte && !a.IsDeleted && a.Status == EstadoDeAdjunto.Available
                           && a.OwnerEntityType == AdjuntosDeModulo.SoporteDeIndicadorRegulatorio, ct);
        if (!adjunto) return Result.Failure<Guid>(AccountingErrors.RegulatoryIndicatorSupportInvalid);

        var vigentesDelCorte = await db.RegulatoryIndicators.AsNoTracking()
            .Where(r => !r.IsDeleted && r.Kind == request.Kind && r.CutoffDate == request.CutoffDate
                        && !db.RegulatoryIndicators.Any(o => o.SupersedesPublicId == r.PublicId))
            .Select(r => r.PublicId)
            .ToListAsync(ct);
        if (request.SupersedesPublicId is { } reemplaza)
        {
            if (!vigentesDelCorte.Contains(reemplaza)) return Result.Failure<Guid>(AccountingErrors.RegulatoryIndicatorSupersedesInvalid);
        }
        else if (vigentesDelCorte.Count > 0)
        {
            return Result.Failure<Guid>(AccountingErrors.RegulatoryIndicatorAlreadyRegistered(request.CutoffDate));
        }

        var ahora = reloj.UtcNow;
        var quien = usuario.UserName ?? "(sin usuario)";
        var indicador = new RegulatoryIndicator
        {
            Kind = request.Kind,
            CutoffDate = request.CutoffDate,
            Value = request.Value,
            Source = request.Source.Trim(),
            SupportAttachmentPublicId = soporte,
            SupersedesPublicId = request.SupersedesPublicId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            RegisteredByUserId = usuario.UserId ?? 0,
            RegisteredByName = quien,
            CreatedAt = ahora,
            CreatedBy = quien,
        };
        if (request.PublicId is { } reservado && reservado != Guid.Empty) indicador.PublicId = reservado;
        db.RegulatoryIndicators.Add(indicador);
        await db.SaveChangesAsync(ct);

        await auditoria.EmitirIndicadorRegulatorioRegistradoAsync(indicador.PublicId, new
        {
            kind = indicador.Kind.ToString(), cutoffDate = indicador.CutoffDate, value = indicador.Value,
            unit = GetEntityIndicatorsQueryHandler.UnidadDe(indicador.Kind), source = indicador.Source,
            supportAttachmentPublicId = soporte, supersedesPublicId = indicador.SupersedesPublicId,
        }, ct);
        return Result.Success(indicador.PublicId);
    }
}

/// <summary>Los indicadores registrados, del más reciente al más antiguo, con quién reemplazó a quién (§15). (nuevo)</summary>
public sealed record ListRegulatoryIndicatorsQuery(RegulatoryIndicatorKind? Kind = null) : IRequest<Result<IReadOnlyList<RegulatoryIndicatorDto>>>;

public sealed class ListRegulatoryIndicatorsQueryValidator : AbstractValidator<ListRegulatoryIndicatorsQuery>
{
    public ListRegulatoryIndicatorsQueryValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
    }
}

public sealed class ListRegulatoryIndicatorsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListRegulatoryIndicatorsQuery, Result<IReadOnlyList<RegulatoryIndicatorDto>>>
{
    public async Task<Result<IReadOnlyList<RegulatoryIndicatorDto>>> Handle(ListRegulatoryIndicatorsQuery request, CancellationToken ct)
    {
        var q = db.RegulatoryIndicators.AsNoTracking().Where(r => !r.IsDeleted);
        if (request.Kind is { } tipo) q = q.Where(r => r.Kind == tipo);
        var filas = await q.OrderByDescending(r => r.CutoffDate).ThenByDescending(r => r.Id).ToListAsync(ct);
        var reemplazos = filas.Where(r => r.SupersedesPublicId is not null).ToDictionary(r => r.SupersedesPublicId!.Value, r => r.PublicId);
        return Result.Success<IReadOnlyList<RegulatoryIndicatorDto>>(filas.Select(r => new RegulatoryIndicatorDto(
                r.PublicId, r.Kind, r.CutoffDate, r.Value, GetEntityIndicatorsQueryHandler.UnidadDe(r.Kind), r.Source, r.SupportAttachmentPublicId,
                r.SupersedesPublicId, reemplazos.TryGetValue(r.PublicId, out var por) ? por : null, r.Notes, r.CreatedAt, r.RegisteredByName))
            .ToList());
    }
}
