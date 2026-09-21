using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.LegalParameters;

public sealed record LegalParameterRangeInput(decimal FromValue, decimal? ToValue, decimal? Rate, decimal? FixedValue);

/// <summary>
/// Nueva vigencia de un parámetro legal (FR-010, SC-003): una fila nueva desde
/// <c>ValidFrom</c>; la anterior se cierra el día antes. Un código nuevo trae nombre y
/// tipo; uno existente los hereda. Las tablas por rangos deben cubrir sin huecos ni
/// solapes y terminar en un tramo abierto.
/// </summary>
public sealed record AddLegalParameterVersionCommand(
    string Code,
    DateTime ValidFrom,
    decimal? Value,
    IReadOnlyList<LegalParameterRangeInput>? Ranges,
    string Source,
    string? Name = null,
    LegalParameterKind? Kind = null,
    string? RangeUnitParameterCode = null,
    bool? RangeIsMarginal = null) : IRequest<Result<Guid>>;

public sealed class AddLegalParameterVersionCommandValidator : AbstractValidator<AddLegalParameterVersionCommand>
{
    public AddLegalParameterVersionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("El código es obligatorio.")
            .Matches("^[A-Za-z0-9_]{2,40}$").WithMessage("El código admite letras, dígitos y guion bajo (2 a 40).");
        RuleFor(x => x.ValidFrom).NotEmpty().WithMessage("La vigencia desde es obligatoria.");
        RuleFor(x => x.Source).NotEmpty().WithMessage("Indique la norma que fija el valor (decreto, resolución, ley).").MaximumLength(200);
        RuleFor(x => x.Name).MaximumLength(120);
        RuleFor(x => x.RangeUnitParameterCode).MaximumLength(40);
        RuleFor(x => x).Must(x => x.Value is not null || (x.Ranges is { Count: > 0 }))
            .WithMessage("Indique el valor o los tramos de la tabla.");
        RuleForEach(x => x.Ranges).ChildRules(r =>
        {
            r.RuleFor(x => x.FromValue).GreaterThanOrEqualTo(0m);
            r.RuleFor(x => x.ToValue).GreaterThan(x => x.FromValue).When(x => x.ToValue is not null)
                .WithMessage("El «hasta» de un tramo debe ser mayor que su «desde».");
        });
    }
}

public sealed class AddLegalParameterVersionCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, IPayrollRunStaleMarker stale)
    : IRequestHandler<AddLegalParameterVersionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddLegalParameterVersionCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var desde = request.ValidFrom.Date;
        var versiones = await db.PayrollLegalParameters.Include(p => p.Ranges).Where(p => p.Code == code).OrderByDescending(p => p.ValidFrom).ToListAsync(ct);
        var actual = versiones.FirstOrDefault();

        LegalParameterKind kind;
        string name;
        if (actual is null)
        {
            if (request.Kind is null || string.IsNullOrWhiteSpace(request.Name))
                return Result.Failure<Guid>(new Error("Payroll.LegalParameterNew", $"El parámetro {code} no existe: indique nombre y tipo (Amount, Percent, RangeTable o DateInYear) para crearlo."));
            kind = request.Kind.Value;
            name = request.Name.Trim();
        }
        else
        {
            kind = actual.Kind;
            name = string.IsNullOrWhiteSpace(request.Name) ? actual.Name : request.Name.Trim();
            if (desde <= actual.ValidFrom)
                return Result.Failure<Guid>(new Error("Payroll.LegalParameterOverlap",
                    $"La vigencia nueva debe empezar después del {actual.ValidFrom:dd/MM/yyyy}, inicio de la vigencia actual de {code}."));
            if (versiones.Any(v => v.ValidFrom == desde))
                return Result.Failure<Guid>(new Error("Payroll.LegalParameterOverlap", $"Ya hay una vigencia de {code} que empieza el {desde:dd/MM/yyyy}."));
        }

        var nueva = new PayrollLegalParameter
        {
            Code = code,
            Name = name,
            Kind = kind,
            ValidFrom = desde,
            Source = request.Source.Trim(),
            CreatedAt = clock.UtcNow,
            CreatedBy = user.UserName,
        };

        if (kind == LegalParameterKind.RangeTable)
        {
            var tramos = request.Ranges;
            if (tramos is null || tramos.Count == 0)
                return Result.Failure<Guid>(new Error("Payroll.RangeTableInvalid", "Una tabla por rangos necesita al menos un tramo."));
            var error = ValidarTramos(tramos);
            if (error is not null) return Result.Failure<Guid>(new Error("Payroll.RangeTableInvalid", error));

            nueva.RangeUnitParameterCode = string.IsNullOrWhiteSpace(request.RangeUnitParameterCode)
                ? actual?.RangeUnitParameterCode : request.RangeUnitParameterCode.Trim().ToUpperInvariant();
            nueva.RangeIsMarginal = request.RangeIsMarginal ?? actual?.RangeIsMarginal ?? false;
            if (nueva.RangeUnitParameterCode is { } unit && !await db.PayrollLegalParameters.AnyAsync(p => p.Code == unit, ct))
                return Result.Failure<Guid>(new Error("Payroll.LegalParameterNew", $"El parámetro de unidad {unit} no existe."));

            var orden = 0;
            foreach (var t in tramos.OrderBy(t => t.FromValue))
                nueva.Ranges.Add(new PayrollLegalParameterRange
                {
                    FromValue = t.FromValue, ToValue = t.ToValue, Rate = t.Rate, FixedValue = t.FixedValue, Order = ++orden,
                    CreatedAt = clock.UtcNow, CreatedBy = user.UserName,
                });
        }
        else
        {
            if (request.Value is null)
                return Result.Failure<Guid>(new Error("Payroll.RangeTableInvalid", $"El parámetro {code} es de tipo {kind}: indique el valor."));
            if (request.Value < 0m)
                return Result.Failure<Guid>(new Error("Payroll.RangeTableInvalid", "El valor no puede ser negativo."));
            nueva.Value = request.Value;
        }

        if (actual is not null && (actual.ValidTo is null || actual.ValidTo >= desde))
        {
            actual.ValidTo = desde.AddDays(-1);
            actual.UpdatedAt = clock.UtcNow;
            actual.UpdatedBy = user.UserName;
        }

        db.PayrollLegalParameters.Add(nueva);
        await stale.MarkAllDraftsStaleAsync($"parámetro legal {code} con vigencia nueva desde {desde:yyyy-MM-dd}", ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(nueva.PublicId);
    }

    /// <summary>Tramos ordenados, contiguos (el «hasta» de uno es el «desde» del siguiente), sin huecos ni solapes y el último abierto.</summary>
    public static string? ValidarTramos(IReadOnlyList<LegalParameterRangeInput> tramos)
    {
        var ordenados = tramos.OrderBy(t => t.FromValue).ToList();
        for (var i = 0; i < ordenados.Count; i++)
        {
            var t = ordenados[i];
            if (t.Rate is null && t.FixedValue is null) return $"El tramo desde {t.FromValue} no tiene tarifa ni valor fijo.";
            if (i < ordenados.Count - 1)
            {
                if (t.ToValue is null) return $"Sólo el último tramo puede quedar abierto; el tramo desde {t.FromValue} no tiene «hasta».";
                var siguiente = ordenados[i + 1];
                if (t.ToValue != siguiente.FromValue)
                    return t.ToValue < siguiente.FromValue
                        ? $"Hay un hueco entre {t.ToValue} y {siguiente.FromValue}."
                        : $"Los tramos se solapan entre {siguiente.FromValue} y {t.ToValue}.";
            }
            else if (t.ToValue is not null)
            {
                return $"El último tramo (desde {t.FromValue}) debe quedar abierto («hasta» vacío).";
            }
        }
        return null;
    }
}
