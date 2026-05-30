using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using MediatR;

namespace IngenIA365ERP.Application.Security.Auth.MfaReset;

public sealed record RequestMfaResetCommand(
    Guid TargetUserPublicId,
    string Reason,
    Guid? EvidenceAttachmentPublicId) : IRequest<Result<MfaResetRequestResult>>;
