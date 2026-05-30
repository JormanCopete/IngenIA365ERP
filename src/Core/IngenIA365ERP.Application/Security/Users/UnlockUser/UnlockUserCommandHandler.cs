using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.UnlockUser;

public sealed class UnlockUserCommandHandler : IRequestHandler<UnlockUserCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public UnlockUserCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(UnlockUserCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null) return Result.Failure("Generic.NotFound", "El usuario no existe.");

        var hadLockout = user.LockoutEndAt is not null && user.LockoutEndAt > _clock.UtcNow;
        var hadFailures = user.FailedLoginAttempts > 0;

        if (!hadLockout && !hadFailures)
        {
            return Result.Failure(UserErrorCodes.NotLocked,
                "El usuario no está bloqueado ni tiene intentos fallidos pendientes.");
        }

        user.LockoutEndAt = null;
        user.FailedLoginAttempts = 0;
        user.UpdatedBy = _currentUser.UserName ?? "SYSTEM";

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
