using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using MediatR;

namespace IngenIA365ERP.Application.Security.Auth.RegenerateBackupCodes;

public sealed record RegenerateBackupCodesCommand(string TotpCode) : IRequest<Result<MfaBackupCodesResult>>;
