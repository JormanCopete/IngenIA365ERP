using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.CostCenters.Commands.CreateCostCenter;
using IngenIA365ERP.Application.Core.CostCenters.Commands.UpdateCostCenter;
using IngenIA365ERP.Application.Core.CostCenters.Queries;
using IngenIA365ERP.Application.Tests.Common;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Core.CostCenters;

/// <summary>
/// El código del centro de costo: crear lo guarda normalizado, editar sin enviarlo no lo borra
/// (la pantalla no lo mandaba y cada edición lo dejaba nulo), y el listado busca también por código.
/// </summary>
public class CodigoDelCentroDeCostoTests
{
    private static (TestApplicationDbContext Db, IDateTimeService Clock, ICurrentUserService User) Escenario()
    {
        var db = TestDbContextFactory.Create();
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc));
        var user = Substitute.For<ICurrentUserService>();
        user.UserName.Returns("admin@demo");
        return (db, clock, user);
    }

    [Fact]
    public async Task Crear_guarda_el_codigo_recortado_y_en_mayusculas()
    {
        var (db, clock, user) = Escenario();
        var r = await new CreateCostCenterCommandHandler(db, clock, user)
            .Handle(new CreateCostCenterCommand { Code = " cc-01 ", Name = "Administración" }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        var dto = await new GetCostCenterByIdQueryHandler(db).Handle(new GetCostCenterByIdQuery(r.Value), CancellationToken.None);
        dto.Value.Code.Should().Be("CC-01");
    }

    [Fact]
    public async Task Editar_sin_enviar_codigo_conserva_el_que_tenia()
    {
        var (db, clock, user) = Escenario();
        var id = (await new CreateCostCenterCommandHandler(db, clock, user)
            .Handle(new CreateCostCenterCommand { Code = "CC-01", Name = "Administración" }, CancellationToken.None)).Value;

        var r = await new UpdateCostCenterCommandHandler(db, clock, user)
            .Handle(new UpdateCostCenterCommand { PublicId = id, Name = "Administración central" }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        var dto = await new GetCostCenterByIdQueryHandler(db).Handle(new GetCostCenterByIdQuery(id), CancellationToken.None);
        dto.Value.Code.Should().Be("CC-01");
        dto.Value.Name.Should().Be("Administración central");
    }

    [Fact]
    public async Task Editar_con_codigo_vacio_lo_quita_a_proposito()
    {
        var (db, clock, user) = Escenario();
        var id = (await new CreateCostCenterCommandHandler(db, clock, user)
            .Handle(new CreateCostCenterCommand { Code = "CC-01", Name = "Administración" }, CancellationToken.None)).Value;

        await new UpdateCostCenterCommandHandler(db, clock, user)
            .Handle(new UpdateCostCenterCommand { PublicId = id, Code = "", Name = "Administración" }, CancellationToken.None);

        (await new GetCostCenterByIdQueryHandler(db).Handle(new GetCostCenterByIdQuery(id), CancellationToken.None)).Value.Code.Should().BeNull();
    }

    [Fact]
    public async Task El_listado_busca_tambien_por_codigo()
    {
        var (db, clock, user) = Escenario();
        var c = new CreateCostCenterCommandHandler(db, clock, user);
        await c.Handle(new CreateCostCenterCommand { Code = "ZONA9", Name = "Alfa" }, CancellationToken.None);
        await c.Handle(new CreateCostCenterCommand { Code = "OTRO", Name = "Beta" }, CancellationToken.None);

        var r = await new ListCostCentersQueryHandler(db)
            .Handle(new ListCostCentersQuery { SearchTerm = "zona9" }, CancellationToken.None);

        r.Value.Items.Should().ContainSingle().Which.Name.Should().Be("Alfa");
    }
}
