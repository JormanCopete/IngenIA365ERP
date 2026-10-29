using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Invitations.IssueMasterInvitation;
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
            Email: "nuevo.miembro@cooperativa.co",
            DocumentType: "CC", DocumentNumber: "1234567", DocumentNumberConfirmation: "1.234.567",
            FirstName: "Ana", LastName: "Pérez");

        var result = _sut.TestValidate(cmd);

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Decisión del dueño del 2026-10-28: la API ya no emite invitaciones sólo por correo.</summary>
    [Fact]
    public void Rechaza_la_invitacion_sin_documento()
    {
        var result = _sut.TestValidate(new IssueTenantInvitationCommand(Guid.NewGuid(), "solo.correo@cooperativa.co"));

        result.ShouldHaveValidationErrorFor(x => x.DocumentType).WithErrorCode("Invitation.DocumentRequired");
        result.ShouldHaveValidationErrorFor(x => x.DocumentNumber).WithErrorCode("Invitation.DocumentRequired");
        result.ShouldHaveValidationErrorFor(x => x.DocumentNumberConfirmation);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Rechaza_la_invitacion_del_maestro_sin_documento()
    {
        var sut = new IssueMasterInvitationCommandValidator();

        var sinDocumento = sut.TestValidate(new IssueMasterInvitationCommand(Guid.NewGuid(), "solo.correo@cooperativa.co", false));
        var conDocumento = sut.TestValidate(new IssueMasterInvitationCommand(Guid.NewGuid(), "con.documento@cooperativa.co", false,
            DocumentType: "CE", DocumentNumber: "987654", DocumentNumberConfirmation: "987654", FirstName: "Luz", LastName: "Extranjera"));

        sinDocumento.ShouldHaveValidationErrorFor(x => x.DocumentNumber).WithErrorCode("Invitation.DocumentRequired");
        conDocumento.ShouldNotHaveAnyValidationErrors();
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
