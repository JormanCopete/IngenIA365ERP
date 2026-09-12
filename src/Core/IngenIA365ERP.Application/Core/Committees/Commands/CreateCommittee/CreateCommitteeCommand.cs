using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Committees.Commands.CreateCommittee;

public record CreateCommitteeCommand : IRequest<Result<Guid>>
{
    /// <summary>Código alfanumérico de la cooperativa (hasta 10); se guarda en LegacyCode.</summary>
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? CommitteeType { get; init; }
}

public class CreateCommitteeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCommitteeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCommitteeCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code);
        if (codigo is not null)
        {
            var repetido = await context.Committees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.LegacyCode == codigo && !e.IsDeleted, ct);
            if (repetido is not null)
                return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("un comité", codigo, repetido.Name));
        }

        var entity = new Committee
        {
            LegacyCode = codigo,
            Name = request.Name,
            ShortName = request.ShortName,
            CommitteeType = request.CommitteeType,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Committees.Add(entity);
        await context.SaveChangesAsync(ct);
        return Result.Success(entity.PublicId);
    }
}

public class CreateCommitteeCommandValidator : AbstractValidator<CreateCommitteeCommand>
{
    public CreateCommitteeCommandValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage($"El código no puede superar {CodigoDeCatalogo.LargoCorto} caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron)
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.Name).NotEmpty().WithMessage("Nombre obligatorio.").MaximumLength(80);
        RuleFor(x => x.ShortName).MaximumLength(60);
        RuleFor(x => x.CommitteeType).MaximumLength(2);
    }
}
