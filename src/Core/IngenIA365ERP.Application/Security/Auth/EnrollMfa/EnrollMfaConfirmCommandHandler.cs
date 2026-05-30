using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Auth.EnrollMfa;

public sealed class EnrollMfaConfirmCommandHandler : IRequestHandler<EnrollMfaConfirmCommand, Result<MfaBackupCodesResult>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITotpService _totp;
    private readonly IMfaEnrollmentStore _enrollments;
    private readonly IMfaBackupCodeGenerator _backupCodes;
    private readonly IDateTimeService _clock;

    public EnrollMfaConfirmCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ITotpService totp,
        IMfaEnrollmentStore enrollments,
        IMfaBackupCodeGenerator backupCodes,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _totp = totp;
        _enrollments = enrollments;
        _backupCodes = backupCodes;
        _clock = clock;
    }

    public async Task<Result<MfaBackupCodesResult>> Handle(EnrollMfaConfirmCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            return Result.Failure<MfaBackupCodesResult>("Generic.Unauthorized", "No autenticado.");
        }

        var enrollment = await _enrollments.GetAsync(request.EnrollmentToken, ct);
        if (enrollment is null || enrollment.UserId != _currentUser.UserId.Value)
        {
            return Result.Failure<MfaBackupCodesResult>(
                "Auth.EnrollmentExpired",
                "La inscripción MFA expiró. Vuelve a iniciar el proceso.");
        }

        if (!_totp.Verify(enrollment.Base32Secret, request.TotpCode))
        {
            return Result.Failure<MfaBackupCodesResult>(
                "Auth.InvalidMfaCode",
                "El código TOTP es inválido.");
        }

        var user = await _db.Users.FirstAsync(u => u.Id == enrollment.UserId, ct);
        user.MfaSecret = _totp.ProtectSecret(enrollment.Base32Secret);
        user.IsMfaEnabled = true;

        var batch = Guid.NewGuid();
        var generated = _backupCodes.Generate(10);
        var now = _clock.UtcNow;
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
        await _enrollments.RemoveAsync(request.EnrollmentToken, ct);

        return Result.Success(new MfaBackupCodesResult(
            generated.Select(g => g.PlainCode).ToList()));
    }
}
