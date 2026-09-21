using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Terminations;

/// <summary>
/// Catálogo de motivos de retiro (feature 010, R7; contracts/api.md §3.4 <c>reasons</c>). Los nueve
/// del programa los siembra <c>TerminationReasonsSeeder</c> con la marca legal de indemnización
/// (CST art. 64); la cooperativa agrega los suyos —siempre <b>sin</b> indemnización, porque esa
/// marca la pone la ley y no un catálogo— y edita nombre y base legal. Un sembrado no cambia de
/// código ni de marca (<c>Payroll.Termination.ReasonSeeded</c>) ni se elimina: se desactiva.
/// </summary>
public static class TerminationReasons
{
    public const string Catalogo = "un motivo de retiro";

    public static TerminationReasonDto Map(TerminationReason r) =>
        new(r.PublicId, r.Code, r.Name, r.GeneratesSeverancePay, r.RequiresContractEndDate, r.LegalBasis, r.IsSeeded, r.IsActive);

    public static readonly Error SeverancePayReserved = new("Payroll.Termination.ReasonSeverancePayReserved",
        "Un motivo propio de la cooperativa no genera indemnización: la marca la pone la ley (CST art. 64) y sólo la lleva el despido sin justa causa sembrado.");
}

// ------------------------------------------------------------------- listar --

public sealed record ListTerminationReasonsQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<TerminationReasonDto>>>;

public sealed class ListTerminationReasonsQueryValidator : AbstractValidator<ListTerminationReasonsQuery>
{
    public ListTerminationReasonsQueryValidator() { }
}

public sealed class ListTerminationReasonsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListTerminationReasonsQuery, Result<IReadOnlyList<TerminationReasonDto>>>
{
    public async Task<Result<IReadOnlyList<TerminationReasonDto>>> Handle(ListTerminationReasonsQuery request, CancellationToken ct)
    {
        var motivos = await db.TerminationReasons.AsNoTracking()
            .Where(r => request.IncludeInactive || r.IsActive)
            .OrderByDescending(r => r.IsSeeded).ThenBy(r => r.Name)
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<TerminationReasonDto>>(motivos.Select(TerminationReasons.Map).ToList());
    }
}

// -------------------------------------------------------------------- crear --

/// <summary>Un motivo propio de la cooperativa. <paramref name="GeneratesSeverancePay"/> tiene que venir en falso.</summary>
public sealed record CreateTerminationReasonCommand(
    string Code,
    string Name,
    bool GeneratesSeverancePay = false,
    bool RequiresContractEndDate = false,
    string? LegalBasis = null) : IRequest<Result<Guid>>;

public sealed class CreateTerminationReasonCommandValidator : AbstractValidator<CreateTerminationReasonCommand>
{
    public CreateTerminationReasonCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("El código es obligatorio.")
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage($"El código no puede superar {CodigoDeCatalogo.LargoCorto} caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre del motivo es obligatorio.").MaximumLength(120);
        RuleFor(x => x.LegalBasis).MaximumLength(120);
        RuleFor(x => x.GeneratesSeverancePay).Equal(false).WithMessage(TerminationReasons.SeverancePayReserved.Message);
    }
}

public sealed class CreateTerminationReasonCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<CreateTerminationReasonCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateTerminationReasonCommand request, CancellationToken ct)
    {
        if (request.GeneratesSeverancePay) return Result.Failure<Guid>(TerminationReasons.SeverancePayReserved);
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var repetido = await db.TerminationReasons.AsNoTracking().FirstOrDefaultAsync(r => r.Code == codigo, ct);
        if (repetido is not null) return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado(TerminationReasons.Catalogo, codigo, repetido.Name));

        var motivo = new TerminationReason
        {
            Code = codigo,
            Name = request.Name.Trim(),
            GeneratesSeverancePay = false,
            RequiresContractEndDate = request.RequiresContractEndDate,
            LegalBasis = string.IsNullOrWhiteSpace(request.LegalBasis) ? null : request.LegalBasis.Trim(),
            IsSeeded = false,
            IsActive = true,
            CreatedAt = clock.UtcNow,
            CreatedBy = user.UserName,
        };
        db.TerminationReasons.Add(motivo);
        await db.SaveChangesAsync(ct);
        return Result.Success(motivo.PublicId);
    }
}

// ------------------------------------------------------------------- editar --

/// <summary>Edita un motivo. En uno sembrado sólo cambian nombre, base legal y «exige fecha de fin»; código y marca de indemnización son de la ley.</summary>
public sealed record UpdateTerminationReasonCommand(
    Guid PublicId,
    string Code,
    string Name,
    bool GeneratesSeverancePay,
    bool RequiresContractEndDate,
    string? LegalBasis,
    bool IsActive = true) : IRequest<Result>;

public sealed class UpdateTerminationReasonCommandValidator : AbstractValidator<UpdateTerminationReasonCommand>
{
    public UpdateTerminationReasonCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().WithMessage("El código es obligatorio.")
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage($"El código no puede superar {CodigoDeCatalogo.LargoCorto} caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre del motivo es obligatorio.").MaximumLength(120);
        RuleFor(x => x.LegalBasis).MaximumLength(120);
    }
}

public sealed class UpdateTerminationReasonCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<UpdateTerminationReasonCommand, Result>
{
    public async Task<Result> Handle(UpdateTerminationReasonCommand request, CancellationToken ct)
    {
        var motivo = await db.TerminationReasons.FirstOrDefaultAsync(r => r.PublicId == request.PublicId, ct);
        if (motivo is null) return Result.Failure(SettlementErrors.TerminationReasonNotFound);

        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (motivo.IsSeeded)
        {
            if (!codigo.Equals(motivo.Code, StringComparison.OrdinalIgnoreCase) || request.GeneratesSeverancePay != motivo.GeneratesSeverancePay)
                return Result.Failure(SettlementErrors.TerminationReasonSeeded);
        }
        else if (request.GeneratesSeverancePay)
        {
            return Result.Failure(TerminationReasons.SeverancePayReserved);
        }

        if (!codigo.Equals(motivo.Code, StringComparison.OrdinalIgnoreCase))
        {
            var repetido = await db.TerminationReasons.AsNoTracking().FirstOrDefaultAsync(r => r.Code == codigo && r.Id != motivo.Id, ct);
            if (repetido is not null) return Result.Failure(CodigoDeCatalogo.Duplicado(TerminationReasons.Catalogo, codigo, repetido.Name));
            motivo.Code = codigo;
        }

        motivo.Name = request.Name.Trim();
        motivo.RequiresContractEndDate = request.RequiresContractEndDate;
        motivo.LegalBasis = string.IsNullOrWhiteSpace(request.LegalBasis) ? null : request.LegalBasis.Trim();
        motivo.IsActive = request.IsActive;
        motivo.UpdatedAt = clock.UtcNow;
        motivo.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// --------------------------------------------------------------- desactivar --

/// <summary>Un motivo desactivado no se ofrece al registrar; las terminaciones que lo usaron lo conservan.</summary>
public sealed record DeactivateTerminationReasonCommand(Guid PublicId) : IRequest<Result>;

public sealed class DeactivateTerminationReasonCommandValidator : AbstractValidator<DeactivateTerminationReasonCommand>
{
    public DeactivateTerminationReasonCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class DeactivateTerminationReasonCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<DeactivateTerminationReasonCommand, Result>
{
    public async Task<Result> Handle(DeactivateTerminationReasonCommand request, CancellationToken ct)
    {
        var motivo = await db.TerminationReasons.FirstOrDefaultAsync(r => r.PublicId == request.PublicId, ct);
        if (motivo is null) return Result.Failure(SettlementErrors.TerminationReasonNotFound);
        motivo.IsActive = false;
        motivo.UpdatedAt = clock.UtcNow;
        motivo.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
