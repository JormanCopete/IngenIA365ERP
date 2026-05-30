using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using MediatR;

namespace IngenIA365ERP.Application.Security.Auth.EnrollMfa;

public sealed record EnrollMfaConfirmCommand(
    string EnrollmentToken,
    string TotpCode) : IRequest<Result<MfaBackupCodesResult>>;
