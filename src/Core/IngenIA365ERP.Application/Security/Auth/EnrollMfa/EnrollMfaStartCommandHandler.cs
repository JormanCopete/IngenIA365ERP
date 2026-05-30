using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Auth.EnrollMfa;

public sealed class EnrollMfaStartCommandHandler : IRequestHandler<EnrollMfaStartCommand, Result<MfaEnrollmentStartResult>>
{
    private const string Issuer = "IngenIA365ERP";
    private const int EnrollmentTtlMinutes = 10;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordPolicyEnforcer _passwords;
    private readonly ITotpService _totp;
    private readonly IMfaEnrollmentStore _enrollments;
    private readonly IDateTimeService _clock;

    public EnrollMfaStartCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPasswordPolicyEnforcer passwords,
        ITotpService totp,
        IMfaEnrollmentStore enrollments,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _passwords = passwords;
        _totp = totp;
        _enrollments = enrollments;
        _clock = clock;
    }

    public async Task<Result<MfaEnrollmentStartResult>> Handle(EnrollMfaStartCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            return Result.Failure<MfaEnrollmentStartResult>("Generic.Unauthorized", "No autenticado.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, ct);
        if (user is null)
        {
            return Result.Failure<MfaEnrollmentStartResult>("Generic.NotFound", "Usuario no encontrado.");
        }

        if (!_passwords.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<MfaEnrollmentStartResult>(
                "Auth.WrongCurrentPassword",
                "La contraseña ingresada no es correcta.");
        }

        var secret = _totp.GenerateSecret();
        var otpAuthUri = _totp.BuildOtpAuthUri(secret, user.Username, Issuer);
        var qrSvg = _totp.BuildQrCodeSvg(otpAuthUri);

        var token = await _enrollments.IssueAsync(
            new MfaEnrollmentContext(user.Id, user.PublicId, secret, _clock.UtcNow),
            TimeSpan.FromMinutes(EnrollmentTtlMinutes),
            ct);

        return Result.Success(new MfaEnrollmentStartResult(secret, qrSvg, token));
    }
}
