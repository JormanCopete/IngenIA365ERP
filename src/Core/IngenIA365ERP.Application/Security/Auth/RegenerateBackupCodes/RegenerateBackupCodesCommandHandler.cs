using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Auth.RegenerateBackupCodes;

public sealed class RegenerateBackupCodesCommandHandler : IRequestHandler<RegenerateBackupCodesCommand, Result<MfaBackupCodesResult>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITotpService _totp;
    private readonly IMfaBackupCodeGenerator _backupCodes;
    private readonly IDateTimeService _clock;

    public RegenerateBackupCodesCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ITotpService totp,
        IMfaBackupCodeGenerator backupCodes,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _totp = totp;
        _backupCodes = backupCodes;
        _clock = clock;
    }

    public async Task<Result<MfaBackupCodesResult>> Handle(RegenerateBackupCodesCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            return Result.Failure<MfaBackupCodesResult>("Generic.Unauthorized", "No autenticado.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, ct);
        if (user is null || !user.IsMfaEnabled || string.IsNullOrWhiteSpace(user.MfaSecret))
        {
            return Result.Failure<MfaBackupCodesResult>(
                "Auth.MfaNotEnrolled",
                "El usuario no tiene MFA inscrito.");
        }

        var plainSecret = _totp.UnprotectSecret(user.MfaSecret);
        if (!_totp.Verify(plainSecret, request.TotpCode))
        {
            return Result.Failure<MfaBackupCodesResult>(
                "Auth.InvalidMfaCode",
                "El código TOTP es inválido.");
        }

        // Invalida en bloque los códigos del batch anterior (soft-delete).
        var existing = await _db.MfaBackupCodes
            .Where(c => c.UserId == user.Id && c.UsedAt == null)
            .ToListAsync(ct);
        var now = _clock.UtcNow;
        foreach (var c in existing)
        {
            c.IsDeleted = true;
            c.DeletedAt = now;
            c.DeletedBy = _currentUser.UserName;
        }

        var batch = Guid.NewGuid();
        var generated = _backupCodes.Generate(10);
        foreach (var pair in generated)
        {
            _db.MfaBackupCodes.Add(new MfaBackupCode
            {
                UserId = user.Id,
                CodeHash = pair.Hash,
                BatchId = batch,
                GeneratedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);

        return Result.Success(new MfaBackupCodesResult(
            generated.Select(g => g.PlainCode).ToList()));
    }
}
