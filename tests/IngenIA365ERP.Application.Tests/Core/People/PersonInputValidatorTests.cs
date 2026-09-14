using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Core.Associates.Commands.RegisterAssociate;
using IngenIA365ERP.Application.Core.People.Commands.CreatePerson;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.RegisterEmployee;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// Feature 008: las reglas de la persona viven en <see cref="PersonInputValidator"/> y los
/// comandos las incluyen; el contrato no tiene banderas derivadas.
/// </summary>
public class PersonInputValidatorTests
{
    private readonly PersonInputValidator _validador = new();

    [Fact]
    public void Exige_documento_tipo_nombres_y_apellidos()
    {
        var r = _validador.TestValidate(new PersonInput { IdType = "", TaxId = "", FirstName = "", LastName = "" });

        r.ShouldHaveValidationErrorFor(x => x.TaxId);
        r.ShouldHaveValidationErrorFor(x => x.IdType);
        r.ShouldHaveValidationErrorFor(x => x.FirstName);
        r.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Correo_invalido_y_fecha_de_nacimiento_futura_fallan()
    {
        var r = _validador.TestValidate(PersonasTestData.Entrada() with
        {
            Email = "no-es-correo",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
        });

        r.ShouldHaveValidationErrorFor(x => x.Email);
        r.ShouldHaveValidationErrorFor(x => x.DateOfBirth);
    }

    [Fact]
    public void Una_entrada_completa_pasa()
    {
        _validador.TestValidate(PersonasTestData.Entrada()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void El_contrato_no_tiene_banderas_derivadas()
    {
        // Sacarlas del tipo es lo que hace imposible que un módulo las pise (Principio V).
        var propiedades = typeof(PersonInput).GetProperties().Select(p => p.Name);

        propiedades.Should().NotContain(["IsEmployee", "IsAssociate", "IsSalesperson"]);
        propiedades.Should().Contain(["IsCustomer", "IsSupplier", "IsAdvisor", "IsThirdParty", "ReceivesInvoice"]);
    }

    [Fact]
    public void CreatePerson_incluye_las_reglas_de_la_persona()
    {
        var r = new CreatePersonCommandValidator().TestValidate(new CreatePersonCommand { TaxId = "", FirstName = "", LastName = "" });

        r.ShouldHaveValidationErrorFor(x => x.TaxId);
        r.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void RegisterEmployee_incluye_las_reglas_laborales_y_exige_persona()
    {
        var r = new RegisterEmployeeCommandValidator().TestValidate(new RegisterEmployeeCommand { BaseSalary = 0, ContractType = 99 });

        r.ShouldHaveValidationErrorFor(x => x.PersonPublicId);
        r.ShouldHaveValidationErrorFor(x => x.BaseSalary);
        r.ShouldHaveValidationErrorFor(x => x.ContractType);
    }

    [Fact]
    public void RegisterAssociate_incluye_las_reglas_de_afiliacion_y_exige_persona()
    {
        var r = new RegisterAssociateCommandValidator().TestValidate(new RegisterAssociateCommand { ContributionRate = -1 });

        r.ShouldHaveValidationErrorFor(x => x.PersonPublicId);
        r.ShouldHaveValidationErrorFor(x => x.ContributionRate);
    }
}
