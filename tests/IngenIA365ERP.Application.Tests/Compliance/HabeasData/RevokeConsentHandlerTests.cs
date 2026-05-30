using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Application.Compliance.HabeasData.Events;
using IngenIA365ERP.Application.Compliance.HabeasData.RevokeConsent;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Compliance;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Compliance.HabeasData;

/// <summary>
/// T124 — Tests del handler de revocación (US7). Exige que la última
/// acción del titular sea <c>Accepted</c>, publica
/// <see cref="HabeasDataRevokedEvent"/> tras commit.
/// </summary>
public class RevokeConsentHandlerTests
{
    private static (RevokeConsentCommandHandler Handler, TestApplicationDbContext Db,
                    IPublisher Publisher)
        Build(string? tenantId = "1")
    {
        var db = TestDbContextFactory.Create();
        var cu = Substitute.For<ICurrentUserService>();
        cu.TenantId.Returns(tenantId);
        cu.UserName.Returns("ana@demo");
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        var publisher = Substitute.For<IPublisher>();
        return (new RevokeConsentCommandHandler(db, cu, clock, publisher), db, publisher);
    }

    private static HabeasDataPolicyVersion SeedPolicy(TestApplicationDbContext db, int tenantId = 1)
    {
        var policy = new HabeasDataPolicyVersion
        {
            TenantId = tenantId,
            VersionNumber = 1,
            Title = "v1",
            ContentMarkdown = "contenido",
            Sha256Hex = "hash",
            EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PublishedBy = "admin"
        };
        db.HabeasDataPolicyVersions.Add(policy);
        db.SaveChanges();
        return policy;
    }

    [Fact]
    public async Task Revokes_when_last_action_is_Accepted_and_publishes_event()
    {
        var (handler, db, publisher) = Build();
        var policy = SeedPolicy(db);
        db.HabeasDataConsents.Add(new HabeasDataConsent
        {
            TenantId = 1, PersonId = 100, PolicyVersionId = policy.Id,
            Action = "Accepted",
            ActionAt = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc),
            ActionBy = "prior-admin", Channel = "Portal"
        });
        db.SaveChanges();

        var result = await handler.Handle(
            new RevokeConsentCommand(PersonId: 100, Notes: "El titular solicitó revocación."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var consents = db.HabeasDataConsents.OrderBy(c => c.ActionAt).ToList();
        consents.Should().HaveCount(2);
        consents[1].Action.Should().Be("Revoked");
        consents[1].PolicyVersionId.Should().Be(policy.Id,
            "la revocación se vincula a la misma versión del consentimiento original");

        await publisher.Received(1).Publish(
            Arg.Is<HabeasDataRevokedEvent>(e =>
                e.TenantId == 1 &&
                e.PersonId == 100 &&
                e.PolicyVersionPublicId == policy.PublicId &&
                e.RevokedBy == "ana@demo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_with_NoActiveConsent_when_titular_never_accepted()
    {
        var (handler, db, publisher) = Build();
        SeedPolicy(db);

        var result = await handler.Handle(
            new RevokeConsentCommand(PersonId: 999, Notes: null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(HabeasDataErrorCodes.NoActiveConsent);
        await publisher.DidNotReceive().Publish(
            Arg.Any<HabeasDataRevokedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_with_AlreadyRevoked_when_last_action_is_Revoked()
    {
        var (handler, db, publisher) = Build();
        var policy = SeedPolicy(db);
        db.HabeasDataConsents.AddRange(
            new HabeasDataConsent
            {
                TenantId = 1, PersonId = 100, PolicyVersionId = policy.Id,
                Action = "Accepted",
                ActionAt = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc),
                ActionBy = "x"
            },
            new HabeasDataConsent
            {
                TenantId = 1, PersonId = 100, PolicyVersionId = policy.Id,
                Action = "Revoked",
                ActionAt = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc),
                ActionBy = "x"
            });
        db.SaveChanges();

        var result = await handler.Handle(
            new RevokeConsentCommand(PersonId: 100, Notes: null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(HabeasDataErrorCodes.AlreadyRevoked);
        await publisher.DidNotReceive().Publish(
            Arg.Any<HabeasDataRevokedEvent>(), Arg.Any<CancellationToken>());
    }
}
