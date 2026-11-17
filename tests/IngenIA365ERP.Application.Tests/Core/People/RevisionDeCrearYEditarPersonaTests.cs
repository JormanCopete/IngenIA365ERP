using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Core.People.Commands.UpdatePerson;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Imports;
using IngenIA365ERP.Application.Core.People.Queries;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// Revisión del flujo de crear y editar persona (2026-11-16): el documento se valida con la feature 017, el NIT no lleva el DV dentro
/// del número y el DV tiene que ser el de la DIAN, la jurídica guarda siempre los nombres desde la razón social, la ciudad retirada que
/// la persona ya tenía no impide guardar, y los largos que sólo vivían en la base se validan antes de llegar a ella.
/// </summary>
public class RevisionDeCrearYEditarPersonaTests
{
    private readonly PersonInputValidator _validador = new();

    private static PersonInput Con(string idType, string taxId, string? dv = null) =>
        PersonasTestData.Entrada() with { IdType = idType, TaxId = taxId, TaxIdCheckDigit = dv };

    // ---------------------------------------------------------------- 1. documento --

    [Fact]
    public void Una_CC_con_letras_se_rechaza_porque_su_usuario_chocaria_con_el_de_una_CE()
    {
        _validador.TestValidate(Con("C", "CE987654"))
            .ShouldHaveValidationErrorFor(x => x.TaxId).WithErrorMessage("El número de cédula de ciudadanía sólo lleva dígitos.");
    }

    [Fact]
    public void Un_numero_que_normalizado_queda_vacio_se_rechaza()
    {
        _validador.TestValidate(Con("C", "-"))
            .ShouldHaveValidationErrorFor(x => x.TaxId).WithErrorMessage("Digite el número de documento.");
    }

    [Fact]
    public void Un_tipo_fuera_del_catalogo_se_rechaza()
    {
        _validador.TestValidate(Con("X", "123456")).ShouldHaveValidationErrorFor(x => x.IdType);
    }

    [Theory]
    [InlineData("C", "1.023.456.789")]
    [InlineData("CE", "987654")]
    [InlineData("PA", "AB123")]
    [InlineData("NI", "900123456")]
    public void Los_documentos_validos_pasan_normalizados(string tipo, string numero)
    {
        _validador.TestValidate(Con(tipo, numero)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void La_longitud_es_la_del_tipo()
    {
        _validador.TestValidate(Con("C", "12345678901"))
            .ShouldHaveValidationErrorFor(x => x.TaxId).WithErrorMessage("El número de cédula de ciudadanía debe tener entre 3 y 10 caracteres.");
    }

    [Fact]
    public async Task Al_editar_un_documento_viejo_invalido_que_no_cambia_no_bloquea()
    {
        var d = new PersonasTestData();
        var vieja = d.Persona("1"); // fila heredada: CC de un dígito
        var handler = new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas);

        var r = await handler.Handle(new UpdatePersonCommand
        {
            PublicId = vieja.PublicId, IdType = "C", TaxId = "1", FirstName = "Ana", LastName = "Pérez", Email = "otra@demo.co",
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
    }

    [Fact]
    public async Task Al_editar_un_documento_que_cambia_se_aplica_la_regla()
    {
        var d = new PersonasTestData();
        var p = d.Persona("1023456789");
        var handler = new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas);

        var r = await handler.Handle(new UpdatePersonCommand
        {
            PublicId = p.PublicId, IdType = "C", TaxId = "CE987654", FirstName = "Ana", LastName = "Pérez",
        }, CancellationToken.None);

        r.Error.Code.Should().Be(DocumentoDePersona.CodigoInvalido);
        r.Error.Message.Should().Contain("sólo lleva dígitos");
    }

    [Fact]
    public void La_edicion_no_aplica_la_regla_en_el_validador_sino_en_el_handler()
    {
        new UpdatePersonCommandValidator().TestValidate(new UpdatePersonCommand
        {
            PublicId = Guid.NewGuid(), IdType = "C", TaxId = "1", FirstName = "Ana", LastName = "Pérez",
        }).ShouldNotHaveAnyValidationErrors();
    }

    // ---------------------------------------------------------------- 2. NIT y DV --

    [Fact]
    public void El_NIT_con_guion_se_rechaza_y_dice_que_el_DV_va_en_su_campo()
    {
        _validador.TestValidate(Con("NI", "900123456-8"))
            .ShouldHaveValidationErrorFor(x => x.TaxId).WithErrorMessage("Escriba el NIT sin el dígito de verificación: el dígito va en su propio campo.");
    }

    [Fact]
    public void Un_DV_distinto_del_de_la_DIAN_se_rechaza_con_el_correcto()
    {
        PlantillaDePersonas.DigitoDeVerificacionDian("900123456").Should().Be(8);

        _validador.TestValidate(Con("NI", "900123456", "7"))
            .ShouldHaveValidationErrorFor(x => x.TaxIdCheckDigit).WithErrorMessage("El dígito de verificación del NIT 900123456 es 8, no 7.");
        _validador.TestValidate(Con("NI", "900123456", "8")).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("12")]
    [InlineData("A")]
    public void El_DV_es_un_solo_digito(string dv)
    {
        _validador.TestValidate(Con("NI", "900123456", dv)).ShouldHaveValidationErrorFor(x => x.TaxIdCheckDigit);
    }

    [Fact]
    public async Task Al_editar_solo_el_DV_se_valida_el_DV()
    {
        var d = new PersonasTestData();
        var p = d.Persona("900123456");
        p.IdType = "NI";
        d.Db.SaveChanges();
        var handler = new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas);

        var r = await handler.Handle(new UpdatePersonCommand
        {
            PublicId = p.PublicId, IdType = "NI", TaxId = "900123456", TaxIdCheckDigit = "3", FirstName = "Ana", LastName = "Pérez",
        }, CancellationToken.None);

        r.Error.Code.Should().Be(DocumentoDePersona.CodigoInvalido);
        r.Error.Message.Should().Contain("es 8, no 3");
    }

    // ---------------------------------------------------------------- 5. jurídica --

    [Fact]
    public async Task Editar_la_razon_social_de_una_juridica_actualiza_nombre_y_apellido()
    {
        var d = new PersonasTestData();
        var p = d.Persona("900123456", "VIEJA S.A.S.", "VIEJA S.A.S.");
        p.IdType = "NI";
        p.PersonType = "02";
        p.BusinessName = "VIEJA S.A.S.";
        d.Db.SaveChanges();
        var handler = new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas);

        var r = await handler.Handle(new UpdatePersonCommand
        {
            PublicId = p.PublicId, IdType = "NI", TaxId = "900123456", PersonType = "02", BusinessName = " NUEVA S.A.S. ",
            // Lo que mandan los campos ocultos del formulario.
            FirstName = "VIEJA S.A.S.", LastName = "VIEJA S.A.S.",
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var guardada = await d.Db.People.SingleAsync(x => x.PublicId == p.PublicId);
        guardada.FirstName.Should().Be("NUEVA S.A.S.");
        guardada.LastName.Should().Be("NUEVA S.A.S.");
        guardada.BusinessName.Should().Be("NUEVA S.A.S.", "la razón social se guarda recortada");
    }

    [Fact]
    public async Task Pasar_de_natural_a_juridica_cambia_los_nombres_por_la_razon_social()
    {
        var d = new PersonasTestData();
        var p = d.Persona("1023456789", "Ana", "Pérez");
        p.SecondLastName = "Gómez";
        p.OtherNames = "María";
        d.Db.SaveChanges();
        var handler = new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas);

        var r = await handler.Handle(new UpdatePersonCommand
        {
            PublicId = p.PublicId, IdType = "C", TaxId = "1023456789", PersonType = "02", BusinessName = "ANA PÉREZ E.U.",
            FirstName = "Ana", LastName = "Pérez", SecondLastName = "Gómez", OtherNames = "María",
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var guardada = await d.Db.People.SingleAsync(x => x.PublicId == p.PublicId);
        guardada.FirstName.Should().Be("ANA PÉREZ E.U.");
        guardada.LastName.Should().Be("ANA PÉREZ E.U.");
        guardada.SecondLastName.Should().BeNull();
        guardada.OtherNames.Should().BeNull();
    }

    [Fact]
    public void Al_crear_una_juridica_se_guarda_con_la_razon_social_aunque_traiga_nombres()
    {
        var d = new PersonasTestData();
        var p = d.Personas.Construir(new PersonInput
        {
            IdType = "NI", TaxId = "900123456", PersonType = "02", BusinessName = "ACME S.A.", FirstName = "Contacto", LastName = "X",
        }, null);

        p.FirstName.Should().Be("ACME S.A.");
        p.LastName.Should().Be("ACME S.A.");
    }

    // ---------------------------------------------------------------- 6. ciudad --

    private static (City Viva, City Retirada) Ciudades(PersonasTestData d)
    {
        var pais = d.Db.Countries.Add(new Country { Name = "Colombia", CreatedBy = "seed" }).Entity;
        var depto = d.Db.Departments.Add(new Department { Country = pais, Code = "76", Name = "Valle del Cauca", CreatedBy = "seed" }).Entity;
        var viva = d.Db.Cities.Add(new City { Department = depto, Name = "Cali", DaneCode = "76001", CreatedBy = "seed" }).Entity;
        var retirada = d.Db.Cities.Add(new City { Department = depto, Name = "La Unión", DaneCode = "76400", CreatedBy = "seed", IsDeleted = true }).Entity;
        d.Db.SaveChanges();
        return (viva, retirada);
    }

    [Fact]
    public async Task La_ciudad_retirada_que_la_persona_ya_tenia_se_acepta_sin_cambio()
    {
        var d = new PersonasTestData();
        var (_, retirada) = Ciudades(d);
        var p = d.Persona("1023456789");
        p.CityId = retirada.Id;
        d.Db.SaveChanges();
        var handler = new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas);

        var r = await handler.Handle(new UpdatePersonCommand
        {
            PublicId = p.PublicId, IdType = "C", TaxId = "1023456789", FirstName = "Ana", LastName = "Pérez",
            CityPublicId = retirada.PublicId, Email = "nuevo@demo.co",
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await d.Db.People.SingleAsync(x => x.PublicId == p.PublicId)).CityId.Should().Be(retirada.Id);
    }

    [Fact]
    public async Task Una_ciudad_retirada_nueva_sigue_rechazandose()
    {
        var d = new PersonasTestData();
        var (viva, retirada) = Ciudades(d);
        var p = d.Persona("1023456789");
        p.CityId = viva.Id;
        d.Db.SaveChanges();
        var handler = new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas);

        var r = await handler.Handle(new UpdatePersonCommand
        {
            PublicId = p.PublicId, IdType = "C", TaxId = "1023456789", FirstName = "Ana", LastName = "Pérez", CityPublicId = retirada.PublicId,
        }, CancellationToken.None);

        r.Error.Code.Should().Be("Person.CityNotFound");
    }

    [Fact]
    public void La_lectura_para_editar_dice_si_la_ciudad_esta_retirada()
    {
        typeof(PersonEditDto).GetProperty(nameof(PersonEditDto.CityRetired)).Should().NotBeNull();
    }

    // ---------------------------------------------------------------- 7. largos y recortes --

    [Fact]
    public void Los_largos_que_solo_vivian_en_la_base_se_validan()
    {
        var r = _validador.TestValidate(PersonasTestData.Entrada() with
        {
            IdIssuedAt = new string('x', 41), PersonType = "001", Status = "ABC",
        });

        r.ShouldHaveValidationErrorFor(x => x.IdIssuedAt);
        r.ShouldHaveValidationErrorFor(x => x.PersonType);
        r.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void La_fecha_de_expedicion_no_puede_ser_futura()
    {
        _validador.TestValidate(PersonasTestData.Entrada() with { IdIssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)) })
            .ShouldHaveValidationErrorFor(x => x.IdIssueDate).WithErrorMessage("La fecha de expedición no puede ser futura.");
        _validador.TestValidate(PersonasTestData.Entrada() with { IdIssueDate = new DateOnly(2010, 5, 1) })
            .ShouldNotHaveValidationErrorFor(x => x.IdIssueDate);
    }

    [Fact]
    public void Al_crear_se_recortan_razon_social_correo_y_direccion()
    {
        var d = new PersonasTestData();
        var p = d.Personas.Construir(PersonasTestData.Entrada() with
        {
            BusinessName = "  Tienda Ana  ", Email = " ana@demo.co ", Address = "  Calle 1 # 2-3 ",
        }, null);

        p.BusinessName.Should().Be("Tienda Ana");
        p.Email.Should().Be("ana@demo.co");
        p.Address.Should().Be("Calle 1 # 2-3");
    }
}
