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
            personId = await _db.People.AsNoTracking()
                .Where(p => p.PublicId == personPublicId && !p.IsDeleted)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(ct);
            if (personId is null)
                return Result.Failure("Core.Person.NotFound", "La persona no existe en esta cooperativa.");
        }

        user.IdentificationNumber = request.IdentificationNumber;
        user.PersonId = personId;
        user.UpdatedBy = _currentUser.UserName ?? "SYSTEM";

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
