using FluentAssertions;
using IngenIA365ERP.Shared.Models.Personas;
using IngenIA365ERP.Shared.Services.Core;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Personas;

/// <summary>Feature 008, US4: un solo modelo de formulario para las tres pantallas.</summary>
public class PersonaFormularioModeloTests
{
    private static PersonaDto Dto() => new()
    {
        PublicId = Guid.NewGuid(), IdType = "C", TaxId = "1023456789", TaxIdCheckDigit = "7", IdIssuedAt = "Cali",
        IdIssueDate = new DateOnly(2010, 5, 1), FirstName = "Ana", LastName = "Pérez", BusinessName = null, PersonType = "01",
        Address = "Cra 1", Phone1 = "555", Phone2 = "556", Mobile = "3001234567", Email = "ana@demo.co",
        CityPublicId = Guid.NewGuid(), CityName = "Cali", Gender = "F", MaritalStatus = "S",
        DateOfBirth = new DateOnly(1990, 1, 1), EducationLevel = "U", Status = "A",
        IsAssociate = true, IsEmployee = false, IsSalesperson = true,
        IsThirdParty = true, IsAdvisor = false, IsCustomer = true, IsSupplier = false, ReceivesInvoice = true,
    };

    [Fact]
    public void Ida_y_vuelta_conserva_todo_lo_editable()
    {
        var dto = Dto();

        var modelo = PersonaFormularioModelo.DesdeDto(dto);
        var entrada = modelo.AInput();

        modelo.PublicId.Should().Be(dto.PublicId);
        entrada.TaxId.Should().Be("1023456789");
        entrada.TaxIdCheckDigit.Should().Be("7");
        entrada.IdIssueDate.Should().Be(new DateOnly(2010, 5, 1));
        entrada.DateOfBirth.Should().Be(new DateOnly(1990, 1, 1));
        entrada.CityPublicId.Should().Be(dto.CityPublicId);
        entrada.MaritalStatus.Should().Be("S");
        entrada.EducationLevel.Should().Be("U");
        entrada.Phone2.Should().Be("556");
        entrada.IsThirdParty.Should().BeTrue();
        entrada.IsCustomer.Should().BeTrue();
        entrada.ReceivesInvoice.Should().BeTrue();
        entrada.IsSupplier.Should().BeFalse();
        entrada.Status.Should().Be("A");
    }

    [Fact]
    public void Las_derivadas_se_leen_pero_no_viajan()
    {
        var modelo = PersonaFormularioModelo.DesdeDto(Dto());

        modelo.EsAsociado.Should().BeTrue();
        modelo.EsVendedor.Should().BeTrue();
        modelo.EsEmpleado.Should().BeFalse();
        typeof(PersonaFormularioModelo).GetProperty(nameof(PersonaFormularioModelo.EsAsociado))!.SetMethod!.IsPublic
            .Should().BeFalse("el formulario no las edita: las escribe el módulo que crea la fila hija");
        typeof(PersonaEntradaDto).GetProperties().Select(p => p.Name)
            .Should().NotContain(["IsEmployee", "IsAssociate", "IsSalesperson"]);
    }

    [Fact]
    public void Nuevo_trae_los_valores_por_defecto_y_el_documento_buscado()
    {
        var modelo = PersonaFormularioModelo.Nuevo(" 1023456789 ");

        modelo.IdType.Should().Be("C");
        modelo.PersonType.Should().Be("01");
        modelo.Status.Should().Be("A");
        modelo.TaxId.Should().Be("1023456789");
        modelo.PublicId.Should().BeNull();
    }

    [Fact]
    public void Validar_exige_documento_nombres_apellidos_y_correo_valido()
    {
        var modelo = PersonaFormularioModelo.Nuevo();
        modelo.Email = "no-es-correo";
        modelo.DateOfBirthDt = DateTime.UtcNow.AddYears(1);

        var avisos = modelo.Validar();

        avisos.Should().Contain(a => a.Contains("documento"));
        avisos.Should().Contain(a => a.Contains("nombres"));
        avisos.Should().Contain(a => a.Contains("apellidos"));
        avisos.Should().Contain(a => a.Contains("correo"));
        avisos.Should().Contain(a => a.Contains("nacimiento"));
    }

    [Fact]
    public void Validar_pasa_con_lo_minimo_y_los_blancos_viajan_como_nulos()
    {
        var modelo = PersonaFormularioModelo.Nuevo("1");
        modelo.FirstName = " Ana "; modelo.LastName = "Pérez"; modelo.Email = "  "; modelo.BusinessName = "";

        modelo.Validar().Should().BeEmpty();
        var entrada = modelo.AInput();
        entrada.FirstName.Should().Be("Ana");
        entrada.Email.Should().BeNull();
        entrada.BusinessName.Should().BeNull();
    }

    [Fact]
    public void Los_catalogos_tienen_los_codigos_que_guarda_la_base()
    {
        CatalogosDePersona.TiposDeDocumento.Select(o => o.Codigo).Should().Contain(["C", "CE", "NI", "PA"]);
        CatalogosDePersona.Estados.Select(o => o.Codigo).Should().Equal("A", "I", "R");
        CatalogosDePersona.Nombre(CatalogosDePersona.Generos, "F").Should().Be("Femenino");
        CatalogosDePersona.Nombre(CatalogosDePersona.Generos, "X").Should().Be("X", "un código migrado que no está en el catálogo se muestra tal cual");
    }
}
