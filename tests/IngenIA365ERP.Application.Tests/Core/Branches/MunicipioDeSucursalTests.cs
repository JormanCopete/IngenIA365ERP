using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.Branches.Commands.CreateBranch;
using IngenIA365ERP.Application.Core.Branches.Commands.UpdateBranch;
using IngenIA365ERP.Application.Core.Branches.Queries;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Core.Branches;

/// <summary>
/// Feature 012, T113 (T24, data-model §0.2): la sucursal lleva su municipio DIVIPOLA (<c>MunicipalityDaneCode</c>), que
/// ReteICA de compras propone y que la carga de plantillas exige. Se valida contra <c>COR_Cities.DaneCode</c>
/// (<c>Branch.MunicipalityUnknown</c>), y <see cref="DivipolaSeeder"/> pone el código a las ciudades que ya existen
/// (por nombre) y agrega las que faltan, sin duplicar.
/// </summary>
public class MunicipioDeSucursalTests
{
    private static readonly DateTime Ahora = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private static (TestApplicationDbContext Db, IDateTimeService Clock, ICurrentUserService User) Escenario()
    {
        var db = TestDbContextFactory.Create();
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(Ahora);
        var user = Substitute.For<ICurrentUserService>();
        user.UserName.Returns("admin@demo");
        return (db, clock, user);
    }

    private static void Ciudad(TestApplicationDbContext db, string nombre, string? dane, string departamento = "Valle del Cauca", string codigoDepartamento = "76")
    {
        var pais = db.Countries.FirstOrDefault() ?? db.Countries.Add(new Country { Name = "Colombia", CreatedBy = "seed" }).Entity;
        var depto = db.Departments.FirstOrDefault(d => d.Code == codigoDepartamento)
                    ?? db.Departments.Add(new Department { Country = pais, Code = codigoDepartamento, Name = departamento, CreatedBy = "seed" }).Entity;
        db.Cities.Add(new City { Department = depto, Name = nombre, DaneCode = dane, CreatedBy = "seed" });
        db.SaveChanges();
    }

    [Fact]
    public async Task Crear_con_un_municipio_que_existe_lo_guarda_y_la_consulta_lo_devuelve()
    {
        var (db, clock, user) = Escenario();
        Ciudad(db, "Florida", "76275");

        var r = await new CreateBranchCommandHandler(db, clock, user)
            .Handle(new CreateBranchCommand { Name = "Florida", MunicipalityDaneCode = "76275" }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var dto = await new GetBranchByIdQueryHandler(db).Handle(new GetBranchByIdQuery(r.Value), CancellationToken.None);
        dto.Value.MunicipalityDaneCode.Should().Be("76275");
    }

    [Fact]
    public async Task Un_municipio_que_no_esta_en_COR_Cities_es_Branch_MunicipalityUnknown()
    {
        var (db, clock, user) = Escenario();
        Ciudad(db, "Florida", "76275");

        var crear = await new CreateBranchCommandHandler(db, clock, user)
            .Handle(new CreateBranchCommand { Name = "Norte", MunicipalityDaneCode = "99999" }, CancellationToken.None);

        crear.Error.Code.Should().Be("Branch.MunicipalityUnknown");
        (await db.Branches.CountAsync()).Should().Be(0);

        var alta = await new CreateBranchCommandHandler(db, clock, user).Handle(new CreateBranchCommand { Name = "Norte" }, CancellationToken.None);
        var editar = await new UpdateBranchCommandHandler(db, clock, user)
            .Handle(new UpdateBranchCommand { PublicId = alta.Value, Name = "Norte", MunicipalityDaneCode = "11001" }, CancellationToken.None);

        editar.Error.Code.Should().Be("Branch.MunicipalityUnknown");
        (await db.Branches.AsNoTracking().SingleAsync()).MunicipalityDaneCode.Should().BeNull();
    }

    [Fact]
    public async Task Editar_pone_conserva_y_quita_el_municipio()
    {
        var (db, clock, user) = Escenario();
        Ciudad(db, "Florida", "76275");
        var alta = await new CreateBranchCommandHandler(db, clock, user).Handle(new CreateBranchCommand { Name = "Principal" }, CancellationToken.None);
        var editar = new UpdateBranchCommandHandler(db, clock, user);

        (await editar.Handle(new UpdateBranchCommand { PublicId = alta.Value, Name = "Principal", MunicipalityDaneCode = "76275" }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await db.Branches.AsNoTracking().SingleAsync()).MunicipalityDaneCode.Should().Be("76275");

        (await editar.Handle(new UpdateBranchCommand { PublicId = alta.Value, Name = "Principal Florida" }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await db.Branches.AsNoTracking().SingleAsync()).MunicipalityDaneCode.Should().Be("76275", "nulo = no cambia");

        (await editar.Handle(new UpdateBranchCommand { PublicId = alta.Value, Name = "Principal Florida", MunicipalityDaneCode = "" }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await db.Branches.AsNoTracking().SingleAsync()).MunicipalityDaneCode.Should().BeNull("vacío lo quita");
    }

    [Fact]
    public void El_validador_exige_cinco_digitos()
    {
        new CreateBranchCommandValidator().Validate(new CreateBranchCommand { Name = "X", MunicipalityDaneCode = "7627" }).IsValid.Should().BeFalse();
        new CreateBranchCommandValidator().Validate(new CreateBranchCommand { Name = "X", MunicipalityDaneCode = "76275" }).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DivipolaSeeder_actualiza_por_nombre_y_agrega_las_faltantes_sin_duplicar()
    {
        var (db, _, _) = Escenario();
        // Como las deja la semilla heredada de SOLIDO: sin código y con el nombre sin tildes.
        Ciudad(db, "Bogota", null, "Bogota D.C.", "11");
        Ciudad(db, "Cali", null);
        Ciudad(db, "Buga", null); // el DANE la escribe «Guadalajara de Buga»: la semilla trae el nombre corto como alias
        var semilla = DivipolaSeeder.Semilla();

        var primera = await DivipolaSeeder.AplicarAsync(db, CancellationToken.None);
        var segunda = await DivipolaSeeder.AplicarAsync(db, CancellationToken.None);

        primera.Should().BeGreaterThan(0);
        segunda.Should().Be(0, "es idempotente");
        var ciudades = await db.Cities.AsNoTracking().ToListAsync();
        (await db.Cities.AsNoTracking().SingleAsync(c => c.Name == "Bogota")).DaneCode.Should().Be("11001");
        (await db.Cities.AsNoTracking().SingleAsync(c => c.Name == "Cali")).DaneCode.Should().Be("76001");
        (await db.Cities.AsNoTracking().SingleAsync(c => c.Name == "Buga")).DaneCode.Should().Be("76111", "el nombre de la ciudad existente no se cambia");
        ciudades.Count(c => c.DaneCode == "76001").Should().Be(1);
        ciudades.Where(c => c.DaneCode != null).Select(c => c.DaneCode).Should().OnlyHaveUniqueItems();
        ciudades.Count(c => c.DaneCode != null).Should().Be(semilla.Municipios.Count);
        ciudades.Should().Contain(c => c.DaneCode == "76275" && c.Name == "Florida");
        (await db.Departments.CountAsync(d => d.Code == "76")).Should().Be(1, "el departamento que ya existía no se duplica");
    }

    [Fact]
    public void La_semilla_DIVIPOLA_tiene_codigos_de_cinco_digitos_unicos_y_de_su_departamento()
    {
        var semilla = DivipolaSeeder.Semilla();

        semilla.Municipios.Should().NotBeEmpty();
        semilla.Municipios.Should().HaveCount(1122, "DIVIPOLA completa del DANE (datos.gov.co gdxc-w37w, 2026-09-25)");
        semilla.Municipios.Should().Contain(m => m.Codigo == "76111" && m.Alias != null && m.Alias.Contains("Buga"), "los alias de la versión parcial se conservan");
        semilla.Municipios.Should().Contain(m => m.Codigo == "91263" && m.Nombre == "El Encanto", "las áreas no municipalizadas también tienen código");
        semilla.Municipios.Select(m => m.Codigo).Should().OnlyHaveUniqueItems();
        semilla.Municipios.Should().OnlyContain(m => m.Codigo.Length == 5 && m.Codigo.All(char.IsDigit));
        semilla.Municipios.Should().OnlyContain(m => semilla.Departamentos.Any(d => d.Codigo == m.Codigo.Substring(0, 2)));
        semilla.Departamentos.Should().HaveCount(33, "32 departamentos y Bogotá D.C.");
    }
}
