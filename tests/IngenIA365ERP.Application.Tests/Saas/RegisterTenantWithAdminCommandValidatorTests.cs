using FluentAssertions;
using IngenIA365ERP.Application.Saas.RegisterTenantWithAdmin;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Saas;

/// <summary>
/// Feature 017 (D-20, decisión del dueño del 2026-10-29): el alta de una cooperativa con su primer administrador
/// exige el documento de esa persona, como cualquier invitación nueva. Antes era opcional.
/// </summary>
public class RegisterTenantWithAdminCommandValidatorTests
{
    private readonly RegisterTenantWithAdminCommandValidator _validator = new();

    private static RegisterTenantWithAdminCommand Alta(
        string? tipo = "CC", string? numero = "63171001", string? confirmacion = "63171001",
        string? nombre = "Ana", string? apellido = "Pérez") =>
        new("Coop Uno", "coop_uno", null, "900171001", "Coop Uno SAS", null, null, "contacto@coop.test", null,
            "Basic", 50, 5120, "admin@coop.test", tipo, numero, confirmacion, nombre, null, apellido, null, null);

    [Fact]
    public void Con_documento_nombre_y_apellido_es_valida()
    {
        _validator.Validate(Alta()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Sin_documento_no_pasa_y_dice_que_falta()
    {
        var r = _validator.Validate(Alta(tipo: null, numero: null, confirmacion: null));

        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterTenantWithAdminCommand.FirstAdminDocumentNumber)
            && e.ErrorCode == "Invitation.DocumentRequired");
        r.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterTenantWithAdminCommand.FirstAdminDocumentType));
        r.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterTenantWithAdminCommand.FirstAdminDocumentNumberConfirmation));
    }

    [Fact]
    public void Sin_nombre_ni_apellido_no_pasa()
    {
        var r = _validator.Validate(Alta(nombre: null, apellido: null));

        r.Errors.Select(e => e.PropertyName).Should().Contain(
            [nameof(RegisterTenantWithAdminCommand.FirstAdminFirstName), nameof(RegisterTenantWithAdminCommand.FirstAdminLastName)]);
    }
}
