using FluentAssertions;
using IngenIA365ERP.Application.Core.People.Services;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// Feature 008, FR-007: el documento es único entre <b>todas</b> las filas, eliminadas
/// incluidas, y la fábrica nunca guarda por su cuenta.
/// </summary>
public class PersonFactoryTests
{
    [Fact]
    public async Task Documento_libre_agrega_la_persona_sin_guardar()
    {
        var d = new PersonasTestData();

        var r = await d.Personas.PrepareAsync(PersonasTestData.Entrada(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Status.Should().Be("A");
        r.Value.CreatedBy.Should().Be("operador@demo");
        r.Value.IsEmployee.Should().BeFalse();
        r.Value.IsAssociate.Should().BeFalse();
        d.Db.ChangeTracker.Entries().Should().Contain(e => e.Entity == r.Value && e.State == EntityState.Added);
        (await d.Db.People.CountAsync()).Should().Be(0, "todavía no se guardó");
    }

    [Fact]
    public async Task Documento_de_una_persona_viva_es_TaxIdDuplicate_con_el_nombre()
    {
        var d = new PersonasTestData();
        d.Persona("1023456789", "Carlos", "Gómez");

        var r = await d.Personas.PrepareAsync(PersonasTestData.Entrada("1023456789"), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Person.TaxIdDuplicate");
        r.Error.Message.Should().Contain("Carlos Gómez");
        d.Db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Should().BeEmpty();
    }

    [Fact]
    public async Task Documento_de_una_persona_eliminada_es_TaxIdDeleted_con_fecha_y_nunca_segunda_fila()
    {
        var d = new PersonasTestData();
        d.Persona("1023456789", "Carlos", "Gómez", eliminada: true);

        var r = await d.Personas.PrepareAsync(PersonasTestData.Entrada("1023456789"), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Person.TaxIdDeleted");
        r.Error.Message.Should().Contain("eliminada el 2026-03-13").And.Contain("Carlos Gómez");
        d.Db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Should().BeEmpty();
        (await d.Db.People.IgnoreQueryFilters().CountAsync(p => p.TaxId == "1023456789")).Should().Be(1);
    }

    [Fact]
    public async Task Razon_social_manda_sobre_el_nombre_en_el_aviso()
    {
        var d = new PersonasTestData();
        var juridica = d.Persona("900123456", "", "");
        juridica.BusinessName = "Cooperativa Demo";
        await d.Db.SaveChangesAsync();

        var r = await d.Personas.PrepareAsync(PersonasTestData.Entrada("900123456"), CancellationToken.None);

        r.Error.Message.Should().Be("Ya existe Cooperativa Demo con ese documento.");
    }

    [Fact]
    public async Task Ciudad_inexistente_es_CityNotFound_y_no_agrega_nada()
    {
        var d = new PersonasTestData();
        var entrada = PersonasTestData.Entrada() with { CityPublicId = Guid.NewGuid() };

        var r = await d.Personas.PrepareAsync(entrada, CancellationToken.None);

        r.Error.Code.Should().Be("Person.CityNotFound");
        d.Db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Should().BeEmpty();
    }

    [Fact]
    public async Task Ciudad_existente_se_resuelve_a_su_Id()
    {
        var d = new PersonasTestData();
        var cali = new Domain.Entities.Core.City { Name = "Cali", DepartmentId = 1, CreatedAt = PersonasTestData.Ahora };
        d.Db.Cities.Add(cali);
        await d.Db.SaveChangesAsync();

        var r = await d.Personas.PrepareAsync(PersonasTestData.Entrada() with { CityPublicId = cali.PublicId }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.CityId.Should().Be(cali.Id);
    }

    [Fact]
    public void La_colision_del_indice_unico_se_reconoce_en_cualquier_proveedor()
    {
        var postgres = new DbUpdateException("error al guardar",
            new InvalidOperationException("23505: duplicate key value violates unique constraint \"UK_COR_People_TaxId\""));
        var sqlServer = new DbUpdateException("error al guardar",
            new InvalidOperationException("Cannot insert duplicate key row in object 'dbo.COR_People' with unique index 'UK_COR_People_TaxId'."));
        var otra = new DbUpdateException("error al guardar",
            new InvalidOperationException("violates foreign key constraint \"FK_PAY_Employees_People\""));

        PersonFactory.EsColisionDeDocumento(postgres).Should().BeTrue();
        PersonFactory.EsColisionDeDocumento(sqlServer).Should().BeTrue();
        PersonFactory.EsColisionDeDocumento(otra).Should().BeFalse("una FK rota no es un documento repetido y debe seguir siendo 500");
    }

    [Fact]
    public async Task La_colision_se_traduce_al_mismo_error_que_la_comprobacion_previa()
    {
        // Dos usuarios crean la misma persona a la vez: ambos pasan la comprobación y el índice
        // único para al segundo. Se le responde lo que habría visto un segundo después.
        var d = new PersonasTestData();
        d.Persona("1023456789", "Carlos", "Gómez");
        var ex = new DbUpdateException("x", new InvalidOperationException("unique constraint \"UK_COR_People_TaxId\""));

        var error = await d.Personas.TraducirColisionAsync(ex, "1023456789", CancellationToken.None);

        error.Should().NotBeNull();
        error!.Code.Should().Be("Person.TaxIdDuplicate");
        error.Message.Should().Contain("Carlos Gómez");
    }

    [Fact]
    public async Task Otra_excepcion_no_se_traduce()
    {
        var d = new PersonasTestData();
        var ex = new DbUpdateException("x", new InvalidOperationException("FK_algo"));

        (await d.Personas.TraducirColisionAsync(ex, "1", CancellationToken.None)).Should().BeNull();
    }
}
