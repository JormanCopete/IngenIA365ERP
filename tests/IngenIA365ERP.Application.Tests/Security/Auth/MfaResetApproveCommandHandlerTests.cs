using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Security.Auth.MfaReset;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Auth;

public class MfaResetApproveCommandHandlerTests
{
    private static (ApproveMfaResetCommandHandler Handler, TestApplicationDbContext Db,
                    ICurrentUserService Cu, ISender Mediator)
        Build(int currentUserId, DateTime? now = null)
    {
        var db = TestDbContextFactory.Create();
        var cu = Substitute.For<ICurrentUserService>();
        cu.UserId.Returns(currentUserId);
        cu.UserName.Returns("approver-" + currentUserId);
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(now ?? new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc));
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>())
            .Returns(IngenIA365ERP.Application.Common.Models.Result.Success());
        return (new ApproveMfaResetCommandHandler(db, cu, clock, mediator), db, cu, mediator);
    }

    private static (MfaResetRequest Entry, User Target) SeedRequest(
        TestApplicationDbContext db, int requesterId = 10, int targetId = 20,
        DateTime? expiresAt = null, bool mfaEnabled = true)
    {
        var target = new User
        {
            Id = targetId,
            Username = "target",
            PasswordHash = "h",
            IsActive = true,
            IsMfaEnabled = mfaEnabled,
            MfaSecret = "protected-secret"
        };
        db.Users.Add(target);

        var entry = new MfaResetRequest
        {
            UserId = targetId,
            RequestedBy = requesterId,
            RequestedAt = new DateTime(2026, 5, 28, 11, 0, 0, DateTimeKind.Utc),
            Reason = "Pérdida del dispositivo",
            ExpiresAt = expiresAt ?? new DateTime(2026, 5, 29, 11, 0, 0, DateTimeKind.Utc),
            Status = MfaResetStatus.Pending
        };
        db.MfaResetRequests.Add(entry);
        db.SaveChanges();
        return (entry, target);
    }

    [Fact]
    public async Task Cannot_approve_own_request()
    {
        var (handler, db, _, _) = Build(currentUserId: 10);
        var (entry, _) = SeedRequest(db, requesterId: 10);

        var result = await handler.Handle(new ApproveMfaResetCommand(entry.PublicId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.MfaResetCannotApproveOwnRequest");
    }

    [Fact]
    public async Task First_approval_marks_first_approver_and_returns_Approved()
    {
        var (handler, db, _, _) = Build(currentUserId: 30);
        var (entry, _) = SeedRequest(db);

        var result = await handler.Handle(new ApproveMfaResetCommand(entry.PublicId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Approved");
        var stored = db.MfaResetRequests.Single();
        stored.FirstApproverId.Should().Be(30);
        stored.SecondApproverId.Should().BeNull();
        stored.Status.Should().Be(MfaResetStatus.Pending);
    }

    [Fact]
    public async Task Same_user_cannot_approve_twice()
    {
        var (handler, db, _, _) = Build(currentUserId: 30);
        var (entry, _) = SeedRequest(db);
        entry.FirstApproverId = 30;
        entry.FirstApprovalAt = DateTime.UtcNow;
        db.SaveChanges();

        var result = await handler.Handle(new ApproveMfaResetCommand(entry.PublicId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.MfaResetAlreadyApprovedBySameUser");
    }

    [Fact]
    public async Task Second_distinct_approval_executes_reset_and_notifies()
    {
        var (handler, db, _, mediator) = Build(currentUserId: 40);
        var (entry, target) = SeedRequest(db);
        entry.FirstApproverId = 30;
        entry.FirstApprovalAt = DateTime.UtcNow;
        db.MfaBackupCodes.Add(new MfaBackupCode
        {
            UserId = target.Id,
            CodeHash = "h",
            BatchId = Guid.NewGuid(),
            GeneratedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        var result = await handler.Handle(new ApproveMfaResetCommand(entry.PublicId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Executed");

        var stored = db.MfaResetRequests.Single();
        stored.Status.Should().Be(MfaResetStatus.Executed);

        var refreshedTarget = db.Users.Single(u => u.Id == target.Id);
        refreshedTarget.IsMfaEnabled.Should().BeFalse();
        refreshedTarget.MfaSecret.Should().BeNull();

        db.MfaBackupCodes.Where(c => !c.IsDeleted).Should().BeEmpty();

        await mediator.Received(1).Send(
            Arg.Is<SendNotificationCommand>(c => c.Payload.Type == NotificationType.MfaReset),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approval_after_24h_returns_expired()
    {
        var (handler, db, _, _) = Build(currentUserId: 30,
            now: new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc));
        var (entry, _) = SeedRequest(db,
            expiresAt: new DateTime(2026, 5, 29, 11, 0, 0, DateTimeKind.Utc));

        var result = await handler.Handle(new ApproveMfaResetCommand(entry.PublicId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.MfaResetExpired");
        db.MfaResetRequests.Single().Status.Should().Be(MfaResetStatus.Expired);
    }
}
