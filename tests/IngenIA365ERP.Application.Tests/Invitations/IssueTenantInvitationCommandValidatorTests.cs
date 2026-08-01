using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Invitations.IssueTenantInvitation;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Invitations;

public class IssueTenantInvitationCommandValidatorTests
{
    private readonly IssueTenantInvitationCommandValidator _sut = new();

    [Fact]
    public void Acepta_caso_happy_path()
    {
        var cmd = new IssueTenantInvitationCommand(
            TenantPublicId: Guid.NewGuid(),
            Email: "nuevo.miembro@cooperativa.co");

        var result = _sut.TestValidate(cmd);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rechaza_TenantPublicId_Empty()
    {
        var cmd = new IssueTenantInvitationCommand(
            TenantPublicId: Guid.Empty,
            Email: "x@y.com");

        var result = _sut.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.TenantPublicId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Rechaza_email_vacio(string? email)
    {
        var cmd = new IssueTenantInvitationCommand(Guid.NewGuid(), email!);
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@cooperativa.co")]
    [InlineData("usuario@")]
    [InlineData("usuario sin arroba")]
    public void Rechaza_email_formato_invalido(string email)
    {
        var cmd = new IssueTenantInvitationCommand(Guid.NewGuid(), email);
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Rechaza_email_demasiado_largo()
    {
        var longLocal = new string('a', 260);
        var cmd = new IssueTenantInvitationCommand(
            Guid.NewGuid(),
            $"{longLocal}@x.com"); // > 256 chars

        var result = _sut.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
