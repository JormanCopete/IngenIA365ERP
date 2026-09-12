using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Professions.Commands.CreateProfession;

public record CreateProfessionCommand : IRequest<Result<Guid>>
{
    /// <summary>Código alfanumérico de la cooperativa (hasta 10); se guarda en LegacyCode.</summary>
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public class CreateProfessionCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateProfessionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateProfessionCommand request,
        CancellationToken cancellationToken)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code);
        if (codigo is not null)
        {
            var repetido = await context.Professions.AsNoTracking()
                .FirstOrDefaultAsync(e => e.LegacyCode == codigo && !e.IsDeleted, cancellationToken);
            if (repetido is not null)
                return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("una profesión", codigo, repetido.Name));
        }

        var entity = new Profession
        {
            LegacyCode = codigo,
            Name = request.Name,
            ShortName = request.ShortName,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Professions.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateProfessionCommandValidator : AbstractValidator<CreateProfessionCommand>
{
    public CreateProfessionCommandValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage($"El código no puede superar {CodigoDeCatalogo.LargoCorto} caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron)
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must not exceed 80 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(40).WithMessage("Short name must not exceed 40 characters.");
    }
}
