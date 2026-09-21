using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PensionProviders.Commands.CreatePensionProvider;

public record CreatePensionProviderCommand : IRequest<Result<Guid>>
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public int CheckDigit { get; init; }

    /// <summary>Feature 010 (US5): código de la administradora en el listado del operador de la PILA (6 posiciones).</summary>
    public string? PilaCode { get; init; }
    /// <summary>Administradora de ahorro individual (ACCAI, Ley 2381/2024).</summary>
    public bool IsAccai { get; init; }
    /// <summary>Feature 009 (FR-088): persona de Personas que la representa como tercero; null = sin vínculo.</summary>
    public Guid? PersonPublicId { get; init; }
}

public class CreatePensionProviderCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreatePensionProviderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePensionProviderCommand request,
        CancellationToken cancellationToken)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var persona = await PersonaVinculada.ResolverAsync(context, request.PersonPublicId, cancellationToken);
        if (persona.IsFailure) return Result.Failure<Guid>(persona.Error);
        var repetido = await context.PensionProviders.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Code == codigo, cancellationToken);
        if (repetido is not null)
            return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("un fondo de pensiones", codigo, repetido.Name));

        var entity = new PensionProvider
        {
            Code = CodigoDeCatalogo.Normalizar(request.Code)!,
            Name = request.Name,
            ShortName = request.ShortName ?? string.Empty,
            TaxId = request.TaxId,
            CheckDigit = request.CheckDigit,
            PilaCode = string.IsNullOrWhiteSpace(request.PilaCode) ? null : request.PilaCode.Trim().ToUpperInvariant(),
            IsAccai = request.IsAccai,
            PersonId = persona.Value,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.PensionProviders.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreatePensionProviderCommandValidator : AbstractValidator<CreatePensionProviderCommand>
{
    public CreatePensionProviderCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código es obligatorio.")
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage($"El código no puede superar {CodigoDeCatalogo.LargoCorto} caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(50).WithMessage("Short name must not exceed 50 characters.");

        RuleFor(x => x.TaxId)
            .MaximumLength(20).WithMessage("TaxId must not exceed 20 characters.");

        RuleFor(x => x.PilaCode)
            .MaximumLength(6).WithMessage("El código PILA tiene hasta 6 posiciones.")
            .Matches("^[A-Za-z0-9-]*$").WithMessage("El código PILA es alfanumérico (admite guion), sin espacios.");
    }
}
