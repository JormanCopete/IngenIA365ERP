using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Taxes;

/// <summary>
/// Alta de un concepto de retención (feature 012, T165; contracts/api.md §30, <c>POST /api/core/withholding-concepts</c>).
/// Código repetido, <c>Catalogo.CodigoDuplicado</c>. (nuevo)
/// </summary>
public sealed record CreateWithholdingConceptCommand(string Code, string Name, string? Notes, string Reason)
    : IRequest<Result<Guid>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateWithholdingConceptCommandValidator : ValidadorConMotivo<CreateWithholdingConceptCommand>
{
    public CreateWithholdingConceptCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDelCatalogoTributario.LargoDeNombre);
        RuleFor(x => x.Notes).MaximumLength(ReglasDelCatalogoTributario.LargoDeNotas);
    }
}

public sealed class CreateWithholdingConceptCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateWithholdingConceptCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateWithholdingConceptCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.WithholdingConcepts.Where(c => c.Code == codigo).Select(c => c.Name).FirstOrDefaultAsync(ct);
        if (existente is not null) return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("un concepto de retención", codigo, existente));

        var concepto = new WithholdingConcept
        {
            Code = codigo,
            Name = request.Name.Trim(),
            IsActive = true,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };
        db.WithholdingConcepts.Add(concepto);
        await db.SaveChangesAsync(ct);
        return Result.Success(concepto.PublicId);
    }
}

/// <summary>
/// Edición de un concepto (T165; §30, <c>PUT /api/core/withholding-concepts/{id}</c>): nombre, activo y notas. Un
/// concepto que usan tarifas vigentes no se inactiva (<c>Core.WithholdingConcept.InUse</c>). (nuevo)
/// </summary>
public sealed record UpdateWithholdingConceptCommand(Guid WithholdingConceptPublicId, string Name, bool IsActive, string? Notes, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateWithholdingConceptCommandValidator : ValidadorConMotivo<UpdateWithholdingConceptCommand>
{
    public UpdateWithholdingConceptCommandValidator()
    {
        RuleFor(x => x.WithholdingConceptPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDelCatalogoTributario.LargoDeNombre);
        RuleFor(x => x.Notes).MaximumLength(ReglasDelCatalogoTributario.LargoDeNotas);
    }
}

public sealed class UpdateWithholdingConceptCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<UpdateWithholdingConceptCommand, Result>
{
    public async Task<Result> Handle(UpdateWithholdingConceptCommand request, CancellationToken ct)
    {
        var concepto = await db.WithholdingConcepts.FirstOrDefaultAsync(c => c.PublicId == request.WithholdingConceptPublicId, ct);
        if (concepto is null) return Result.Failure(TaxErrors.ConceptNotFound());

        if (concepto.IsActive && !request.IsActive)
        {
            var enUso = await TarifasVigentesAsync(db, concepto.Id, reloj.HoyLocal, ct);
            if (enUso.Count > 0) return Result.Failure(TaxErrors.ConceptInUse(enUso));
        }

        concepto.Name = request.Name.Trim();
        concepto.IsActive = request.IsActive;
        concepto.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Los códigos de las tarifas vivas del concepto que rigen hoy o después.</summary>
    internal static async Task<IReadOnlyList<string>> TarifasVigentesAsync(IApplicationDbContext db, int conceptoId, DateOnly hoy, CancellationToken ct) =>
        await db.TaxRates.Where(r => r.WithholdingConceptId == conceptoId && (r.ValidTo == null || r.ValidTo >= hoy))
            .Select(r => r.Code).Distinct().OrderBy(c => c).ToListAsync(ct);
}
