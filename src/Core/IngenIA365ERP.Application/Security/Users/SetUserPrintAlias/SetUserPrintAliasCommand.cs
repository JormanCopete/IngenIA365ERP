using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.SetUserPrintAlias;

/// <summary>
/// <c>PUT /api/admin/users/{id}/print-alias</c> (<c>Security.Users.Update</c>): el sobrenombre, alias, iniciales o cargo abreviado con que el usuario sale
/// en los documentos impresos cuando la cooperativa elige «Sobrenombre» (pedido del dueño del 2026-11-15). Vacío lo borra y el usuario sale con su nombre.
/// Sólo toca esa columna. (nuevo)
/// </summary>
public sealed record SetUserPrintAliasCommand(Guid UserPublicId, string? PrintAlias) : IRequest<Result>;

public sealed class SetUserPrintAliasCommandValidator : AbstractValidator<SetUserPrintAliasCommand>
{
    public SetUserPrintAliasCommandValidator()
    {
        RuleFor(x => x.UserPublicId).NotEmpty();
        RuleFor(x => x.PrintAlias).MaximumLength(60).WithMessage("El sobrenombre admite hasta 60 caracteres.");
    }
}

public sealed class SetUserPrintAliasCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<SetUserPrintAliasCommand, Result>
{
    public async Task<Result> Handle(SetUserPrintAliasCommand request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null) return Result.Failure("Generic.NotFound", "El usuario no existe.");

        var alias = string.IsNullOrWhiteSpace(request.PrintAlias) ? null : request.PrintAlias.Trim();
        if (user.PrintAlias == alias) return Result.Success();
        user.PrintAlias = alias;
        user.UpdatedBy = currentUser.UserName ?? "SYSTEM";
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
