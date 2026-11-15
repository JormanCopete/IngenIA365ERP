using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.SetUserPerson;

/// <summary>
/// <c>PUT /api/admin/users/{id}/person</c>: vincula el usuario con su persona del maestro, la cambia o la quita (<paramref name="PersonPublicId"/>
/// nulo), y no toca nada más. Cartera exige <c>SEC_Users.PersonId</c> a quien radica, decide, vota, desembolsa o carga la migración
/// (<c>Lending.Security.UserWithoutPerson</c>) y manda a Seguridad › Usuarios, donde hasta el 2026-11-06 no había ese campo: la carga de
/// créditos de una cooperativa se bloqueó por eso. Va aparte de <see cref="UpdateUser.UpdateUserCommand"/> porque aquél reemplaza el usuario
/// entero. La invitación con documento ya vincula sola (<c>TenantUserProvisioner</c>). (nuevo)
/// </summary>
public sealed record SetUserPersonCommand(Guid UserPublicId, Guid? PersonPublicId) : IRequest<Result>;

public sealed class SetUserPersonCommandValidator : AbstractValidator<SetUserPersonCommand>
{
    public SetUserPersonCommandValidator() => RuleFor(x => x.UserPublicId).NotEmpty();
}

public sealed class SetUserPersonCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<SetUserPersonCommand, Result>
{
    public async Task<Result> Handle(SetUserPersonCommand request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null) return Result.Failure("Generic.NotFound", "El usuario no existe.");

        int? personId = null;
        if (request.PersonPublicId is { } persona && persona != Guid.Empty)
        {
            var resuelta = await PersonaDelUsuario.ResolverAsync(db, user.Id, persona, ct);
            if (resuelta.IsFailure) return Result.Failure(resuelta.Error);
            personId = resuelta.Value;
        }

        if (user.PersonId == personId) return Result.Success();
        user.PersonId = personId;
        user.UpdatedBy = currentUser.UserName ?? "SYSTEM";
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
