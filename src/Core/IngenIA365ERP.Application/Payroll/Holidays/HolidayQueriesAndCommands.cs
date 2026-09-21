using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Holidays;

/// <summary>
/// Calendario de festivos (feature 010, R6; contracts/api.md §10.2). La lista de la Ley 51 de
/// 1983 la siembra <c>HolidaysSeeder</c> y aquí no se toca: la cooperativa agrega un puente
/// decretado o un festivo propio y retira sólo esos. Todo queda auditado
/// (<c>Payroll.Holiday.Changed</c>): un festivo cambia cuántos días hábiles tienen unas
/// vacaciones.
/// </summary>
public sealed record HolidayDto(Guid HolidayPublicId, DateOnly Date, string Name, HolidayOrigin Origin, bool IsSeeded, string? CreatedBy, DateTime CreatedAt);

public static class HolidayErrors
{
    public static readonly Error Seeded = new("Payroll.Holiday.Seeded",
        "Un festivo de la Ley 51 de 1983 no se retira desde aquí: viene de la semilla y el cálculo lo necesita. Sólo los decretados y los manuales se retiran.");

    public static Error DateDuplicate(DateOnly fecha, string nombre) =>
        new("Payroll.Holiday.DateDuplicate", $"El {fecha:dd/MM/yyyy} ya es festivo: {nombre}.");

    public static readonly Error NotFound = new("Payroll.Holiday.NotFound", "No existe el festivo indicado.");

    public static readonly Error OriginInvalid = new("Payroll.Holiday.OriginInvalid",
        "Desde la pantalla sólo se registran festivos decretados (Decreed) o manuales (Manual); los de la Ley 51 los pone la semilla.");
}

// ------------------------------------------------------------------ listado --

/// <summary>Los festivos de un año (el actual si no viene), en orden de fecha.</summary>
public sealed record ListHolidaysQuery(int? Year = null) : IRequest<Result<IReadOnlyList<HolidayDto>>>;

public sealed class ListHolidaysQueryValidator : AbstractValidator<ListHolidaysQuery>
{
    public ListHolidaysQueryValidator() =>
        RuleFor(x => x.Year).InclusiveBetween(1900, 2200).When(x => x.Year is not null).WithMessage("Año fuera de rango.");
}

public sealed class ListHolidaysQueryHandler(IApplicationDbContext db, IDateTimeService clock)
    : IRequestHandler<ListHolidaysQuery, Result<IReadOnlyList<HolidayDto>>>
{
    public async Task<Result<IReadOnlyList<HolidayDto>>> Handle(ListHolidaysQuery request, CancellationToken ct)
    {
        var year = (short)(request.Year ?? clock.TodayUtc.Year);
        var festivos = await db.Holidays.AsNoTracking()
            .Where(h => h.Year == year && !h.IsDeleted)
            .OrderBy(h => h.Date)
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<HolidayDto>>(festivos.Select(Map).ToList());
    }

    public static HolidayDto Map(Holiday h) => new(h.PublicId, h.Date, h.Name, h.Origin, h.EsSembrado, h.CreatedBy, h.CreatedAt);
}

// ------------------------------------------------------------------- crear --

/// <summary>
/// Un festivo nuevo: <c>Manual</c> por defecto, o <c>Decreed</c> cuando es un puente decretado
/// por el Gobierno (queda dicho de dónde salió). Nunca uno de la Ley 51: eso es la semilla.
/// </summary>
public sealed record CreateHolidayCommand(DateOnly Date, string Name, HolidayOrigin Origin = HolidayOrigin.Manual) : IRequest<Result<Guid>>;

public sealed class CreateHolidayCommandValidator : AbstractValidator<CreateHolidayCommand>
{
    public CreateHolidayCommandValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("La fecha es obligatoria.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre del festivo es obligatorio.").MaximumLength(80);
        RuleFor(x => x.Origin).Must(o => o is HolidayOrigin.Manual or HolidayOrigin.Decreed).WithMessage(HolidayErrors.OriginInvalid.Message);
    }
}

public sealed class CreateHolidayCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<CreateHolidayCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateHolidayCommand request, CancellationToken ct)
    {
        if (request.Origin is not (HolidayOrigin.Manual or HolidayOrigin.Decreed))
            return Result.Failure<Guid>(HolidayErrors.OriginInvalid);

        var existente = await db.Holidays.AsNoTracking().FirstOrDefaultAsync(h => h.Date == request.Date && !h.IsDeleted, ct);
        if (existente is not null)
            return Result.Failure<Guid>(HolidayErrors.DateDuplicate(request.Date, existente.Name));

        var festivo = new Holiday
        {
            Date = request.Date,
            Name = request.Name.Trim(),
            Origin = request.Origin,
            Year = (short)request.Date.Year,
            CreatedAt = clock.UtcNow,
            CreatedBy = user.UserName,
        };
        db.Holidays.Add(festivo);
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollHolidayChanged, nameof(Holiday), festivo.PublicId, null,
            new { action = "Created", date = festivo.Date, name = festivo.Name, origin = festivo.Origin.ToString() }, ct);
        return Result.Success(festivo.PublicId);
    }
}

// ------------------------------------------------------------------ retirar --

/// <summary>Retiro suave de un festivo decretado o manual; uno sembrado responde <c>Payroll.Holiday.Seeded</c>.</summary>
public sealed record DeleteHolidayCommand(Guid HolidayPublicId) : IRequest<Result>;

public sealed class DeleteHolidayCommandValidator : AbstractValidator<DeleteHolidayCommand>
{
    public DeleteHolidayCommandValidator() => RuleFor(x => x.HolidayPublicId).NotEmpty();
}

public sealed class DeleteHolidayCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<DeleteHolidayCommand, Result>
{
    public async Task<Result> Handle(DeleteHolidayCommand request, CancellationToken ct)
    {
        var festivo = await db.Holidays.FirstOrDefaultAsync(h => h.PublicId == request.HolidayPublicId && !h.IsDeleted, ct);
        if (festivo is null) return Result.Failure(HolidayErrors.NotFound);
        if (festivo.EsSembrado) return Result.Failure(HolidayErrors.Seeded);

        var antes = new { date = festivo.Date, name = festivo.Name, origin = festivo.Origin.ToString() };
        festivo.IsDeleted = true;
        festivo.DeletedAt = clock.UtcNow;
        festivo.DeletedBy = user.UserName;
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollHolidayChanged, nameof(Holiday), festivo.PublicId, antes, new { action = "Deleted" }, ct);
        return Result.Success();
    }
}
