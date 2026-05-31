using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.ValueObjects;

namespace IngenIA365ERP.Domain.Tests.Entities.Admin;

/// <summary>
/// Invariantes mínimas de <see cref="CentralUser"/>: defaults, soft-delete inicial,
/// y comportamiento de los value objects asociados (<see cref="Email"/>, <see cref="InvitationToken"/>).
/// </summary>
public class CentralUserTests
{
    [Fact]
    public void NewCentralUser_ShouldHaveSensibleDefaults()
    {
        var user = new CentralUser
        {
            Id = Guid.NewGuid(),
            Email = "ana@coop.test",
            NormalizedEmail = "ANA@COOP.TEST",
            PasswordHash = "bcrypt$hash",
        };

        user.Status.Should().Be(CentralUserStatus.Active, "default es Active");
        user.IsGlobalMasterAdmin.Should().BeFalse();
        user.TwoFactorEnabled.Should().BeFalse();
        user.IsDeleted.Should().BeFalse();
        user.LockoutEnabled.Should().BeFalse("lockout interno deshabilitado por T043; Redis es la fuente de verdad");
        user.AccessFailedCount.Should().Be(0);
        user.SecurityStamp.Should().NotBeNullOrEmpty();
        user.ConcurrencyStamp.Should().NotBeNullOrEmpty();
        user.Memberships.Should().BeEmpty();
    }

    [Fact]
    public void CentralUser_SoftDeleteFields_ShouldStartUnset()
    {
        var user = new CentralUser();

        user.IsDeleted.Should().BeFalse();
        user.DeletedAt.Should().BeNull();
        user.DeletedBy.Should().BeNull();
    }

    // -------------------- Email value object --------------------

    [Fact]
    public void Email_Create_ShouldNormalizeToUpperInvariant()
    {
        var email = Email.Create(" Ana.Perez@Coop.Coop ");

        email.Value.Should().Be("Ana.Perez@Coop.Coop");
        email.Normalized.Should().Be("ANA.PEREZ@COOP.COOP");
        email.ToString().Should().Be("Ana.Perez@Coop.Coop");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-an-email")]
    [InlineData("@no-local-part")]
    [InlineData("no-at-sign.com")]
    public void Email_Create_WithInvalidInput_ShouldThrow(string raw)
    {
        var act = () => Email.Create(raw);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Email_Equality_ShouldBeCaseInsensitive()
    {
        var a = Email.Create("ana@coop.test");
        var b = Email.Create("ANA@coop.test");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Email_Normalize_StaticHelper_ShouldHandleNullAndWhitespace()
    {
        Email.Normalize(null!).Should().Be(string.Empty);
        Email.Normalize("   ").Should().Be(string.Empty);
        Email.Normalize("  Foo@Bar.Com  ").Should().Be("FOO@BAR.COM");
    }

    // -------------------- InvitationToken value object --------------------

    [Fact]
    public void InvitationToken_Generate_ShouldProducePlainAndHash()
    {
        var token = InvitationToken.Generate();

        token.PlainText.Should().NotBeNullOrEmpty();
        token.PlainText!.Length.Should().BeGreaterThanOrEqualTo(40, "32 bytes en Base64Url ≈ 43 chars");
        token.Hash.Should().HaveCount(InvitationToken.HashBytes);
    }

    [Fact]
    public void InvitationToken_HashOf_ShouldMatchGenerateHash()
    {
        var token = InvitationToken.Generate();

        var hash = InvitationToken.HashOf(token.PlainText!);

        hash.Should().BeEquivalentTo(token.Hash);
    }

    [Fact]
    public void InvitationToken_GenerateTwice_ShouldProduceDifferentTokens()
    {
        var a = InvitationToken.Generate();
        var b = InvitationToken.Generate();

        a.PlainText.Should().NotBe(b.PlainText);
        a.Hash.Should().NotBeEquivalentTo(b.Hash);
    }

    [Fact]
    public void InvitationToken_FromHash_ShouldRoundTripWithoutPlainText()
    {
        var original = InvitationToken.Generate();

        var reconstructed = InvitationToken.FromHash(original.Hash);

        reconstructed.PlainText.Should().BeNull();
        reconstructed.Hash.Should().BeEquivalentTo(original.Hash);
        reconstructed.Should().Be(original, "la igualdad se basa en el hash, no en el plano");
    }

    [Fact]
    public void InvitationToken_FromHash_WithWrongSize_ShouldThrow()
    {
        var act = () => InvitationToken.FromHash(new byte[16]);

        act.Should().Throw<ArgumentException>();
    }
}
