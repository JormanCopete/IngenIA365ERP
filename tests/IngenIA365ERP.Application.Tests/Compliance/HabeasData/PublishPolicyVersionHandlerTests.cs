using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Application.Compliance.HabeasData.PublishPolicyVersion;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Compliance;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Compliance.HabeasData;

/// <summary>
/// T123 — Tests del handler que publica versiones de la política habeas
/// data (US7). Cubre: cierre de la versión anterior, validación de fecha
/// no pasada, cálculo determinístico del SHA-256.
/// </summary>
public class PublishPolicyVersionHandlerTests
{
    private static (PublishPolicyVersionCommandHandler Handler, TestApplicationDbContext Db)
        Build(string? tenantId = "1", DateTime? now = null)
    {
        var db = TestDbContextFactory.Create();
        var cu = Substitute.For<ICurrentUserService>();
        cu.TenantId.Returns(tenantId);
        cu.UserName.Returns("ana@demo");
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(now ?? new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));
        return (new PublishPolicyVersionCommandHandler(db, cu, clock), db);
    }

    [Fact]
    public async Task Publishes_first_version_when_no_prior_exists()
    {
        var (handler, db) = Build();
        var effectiveFrom = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var content = "# Política habeas data v1\n\nContenido inicial.";

        var result = await handler.Handle(
            new PublishPolicyVersionCommand("Política v1", content, effectiveFrom),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var saved = db.HabeasDataPolicyVersions.Single();
        saved.VersionNumber.Should().Be(1);
        saved.TenantId.Should().Be(1);
        saved.EffectiveFrom.Should().Be(effectiveFrom);
        saved.EffectiveTo.Should().BeNull("la primera versión está vigente");
        saved.PublishedBy.Should().Be("ana@demo");
        saved.Sha256Hex.Should().Be(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant());
    }

    [Fact]
    public async Task Closes_previous_version_and_increments_number()
    {
        var (handler, db) = Build();
        db.HabeasDataPolicyVersions.Add(new HabeasDataPolicyVersion
        {
            TenantId = 1,
            VersionNumber = 5,
            Title = "v5",
            ContentMarkdown = "viejo",
            Sha256Hex = "abc",
            EffectiveFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = null,
            PublishedBy = "previous-admin"
        });
        db.SaveChanges();
        var effectiveFrom = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await handler.Handle(
            new PublishPolicyVersionCommand("v6", "contenido v6", effectiveFrom),
            CancellationToken.None);

        var versions = db.HabeasDataPolicyVersions.OrderBy(p => p.VersionNumber).ToList();
        versions.Should().HaveCount(2);
        versions[0].VersionNumber.Should().Be(5);
        versions[0].EffectiveTo.Should().Be(effectiveFrom,
            "la versión anterior debe cerrarse en la fecha de la nueva");
        versions[1].VersionNumber.Should().Be(6);
        versions[1].EffectiveTo.Should().BeNull();
    }

    [Fact]
    public void Validator_rejects_EffectiveFrom_in_the_past()
    {
        var validator = new PublishPolicyVersionCommandValidator();

        var result = validator.Validate(new PublishPolicyVersionCommand(
            "x", "y", DateTime.UtcNow.AddYears(-1)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorCode == HabeasDataErrorCodes.Validation_EffectiveFromInPast);
    }

    [Fact]
    public async Task Fails_with_TenantRequired_when_no_tenant_in_context()
    {
        var (handler, db) = Build(tenantId: null);

        var result = await handler.Handle(
            new PublishPolicyVersionCommand("x", "y", DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.TenantRequired");
        db.HabeasDataPolicyVersions.Should().BeEmpty();
    }
}
