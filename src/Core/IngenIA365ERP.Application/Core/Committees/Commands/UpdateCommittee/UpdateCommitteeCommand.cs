using IngenIA365ERP.Application.Common.Catalogos;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Committees.Commands.UpdateCommittee;

public record UpdateCommitteeCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? CommitteeType { get; init; }
}

public class UpdateCommitteeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateCommitteeCommand, Result>
{
    public async Task<Result> Handle(UpdateCommitteeCommand request, CancellationToken ct)
    {
        var entity = await context.Committees
            .FirstOrDefaultAsync(c => c.PublicId == request.PublicId && !c.IsDeleted, ct);
        if (entity is null)
            return Result.Failure(new Error("Committee.NotFound", "Comite no encontrado."));

        var codigo = CodigoDeCatalogo.Normalizar(request.Code);
        if (codigo is not null)
        {
            var repetido = await context.Committees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.LegacyCode == codigo && e.Id != entity.Id && !e.IsDeleted, ct);
            if (repetido is not null)
                return Result.Failure(CodigoDeCatalogo.Duplicado("un comité", codigo, repetido.Name));
        }
        entity.LegacyCode = codigo;

        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.CommitteeType = request.CommitteeType;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class UpdateCommitteeCommandValidator : AbstractValidator<UpdateCommitteeCommand>
{
    public UpdateCommitteeCommandValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage($"El código no puede superar {CodigoDeCatalogo.LargoCorto} caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron)
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.ShortName).MaximumLength(60);
        RuleFor(x => x.CommitteeType).MaximumLength(2);
    }
}
