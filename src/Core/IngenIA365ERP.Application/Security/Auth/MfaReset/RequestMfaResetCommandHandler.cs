using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Auth.MfaReset;

public sealed class RequestMfaResetCommandHandler : IRequestHandler<RequestMfaResetCommand, Result<MfaResetRequestResult>>
{
    private const int ExpiryHours = 24;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public RequestMfaResetCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<MfaResetRequestResult>> Handle(RequestMfaResetCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            return Result.Failure<MfaResetRequestResult>("Generic.Unauthorized", "No autenticado.");
        }

        var target = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == request.TargetUserPublicId, ct);
        if (target is null)
        {
            return Result.Failure<MfaResetRequestResult>(
                "Generic.NotFound", "El usuario objetivo no existe.");
        }

        var now = _clock.UtcNow;
        var entry = new MfaResetRequest
        {
            UserId = target.Id,
            RequestedBy = _currentUser.UserId.Value,
            RequestedAt = now,
            Reason = request.Reason,
            ExpiresAt = now.AddHours(ExpiryHours),
            Status = MfaResetStatus.Pending
        };

        if (request.EvidenceAttachmentPublicId.HasValue)
        {
            var att = await _db.Attachments.AsNoTracking()
                .Where(a => a.PublicId == request.EvidenceAttachmentPublicId.Value)
                .Select(a => new { a.Id })
                .FirstOrDefaultAsync(ct);
            entry.EvidenceAttachmentId = att?.Id;
        }

        _db.MfaResetRequests.Add(entry);
        await _db.SaveChangesAsync(ct);

        return Result.Success(new MfaResetRequestResult(entry.PublicId, entry.ExpiresAt));
    }
}
