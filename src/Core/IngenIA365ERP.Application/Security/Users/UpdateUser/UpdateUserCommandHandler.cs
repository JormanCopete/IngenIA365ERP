using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.UpdateUser;

public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateUserCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null)
        {
            return Result.Failure("Generic.NotFound", "El usuario no existe.");
        }

        // Email único global (excepto el propio).
        if (user.Email != request.Email
            && await _db.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Email == request.Email && u.Id != user.Id, ct))
        {
            return Result.Failure(UserErrorCodes.EmailTaken,
                $"Ya existe un usuario con el correo '{request.Email}'.");
        }

        if (user.Email != request.Email)
        {
            user.Email = request.Email;
            user.IsEmailVerified = false;
        }
        var personId = request.PersonId;
        if (request.PersonPublicId is { } personPublicId && personPublicId != Guid.Empty)
        {
            var resuelta = await PersonaDelUsuario.ResolverAsync(_db, user.Id, personPublicId, ct);
            if (resuelta.IsFailure) return Result.Failure(resuelta.Error);
            personId = resuelta.Value;
        }
        else if (personId is { } entero && await PersonaDelUsuario.OtroUsuarioConLaPersonaAsync(_db, user.Id, entero, ct) is { } otro)
        {
            return Result.Failure(UserErrorCodes.PersonAlreadyLinked, $"Esa persona ya está vinculada al usuario {otro}.");
        }

        // Nulo = no vino. La ficha ya no lo edita (feature 017), y un PUT de otra cosa lo borraba.
        if (request.IdentificationNumber is not null) user.IdentificationNumber = request.IdentificationNumber;
        user.PersonId = personId;
        user.UpdatedBy = _currentUser.UserName ?? "SYSTEM";

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
