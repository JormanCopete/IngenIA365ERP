using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using MediatR;

namespace IngenIA365ERP.Application.Security.Auth.EnrollMfa;

public sealed record EnrollMfaStartCommand(string Password) : IRequest<Result<MfaEnrollmentStartResult>>;
