using FluentAssertions;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Core.AssociateGroups;
using IngenIA365ERP.Application.Core.Associates.Commands.RegisterAssociate;
using IngenIA365ERP.Application.Core.Associates.Commands.UpdateAssociate;
using IngenIA365ERP.Application.Core.Associates.Queries;
using IngenIA365ERP.Application.Tests.Core.People;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Core.AssociateGroups;

/// <summary>Grupos del asociado: catálogo con código obligatorio y único, y su captura en la ficha del asociado.</summary>
public class GruposDelAsociadoTests
{
    private readonly PersonasTestData _d = new();

    private async Task<AssociateGroupDto> Crear(string codigo, string nombre = "Zona norte") =>
        (await new CreateAssociateGroupCommandHandler(_d.Db, _d.Clock, _d.User)
            .Handle(new CreateAssociateGroupCommand(codigo, nombre, null), default)).Value;

    [Fact]
    public async Task Crear_guarda_el_codigo_en_mayusculas_y_rechaza_el_repetido_nombrando_al_existente()
    {
        var g = await Crear(" zn-01 ", "Zona norte");
        g.Code.Should().Be("ZN-01");
        g.IsActive.Should().BeTrue();

        var repetido = await new CreateAssociateGroupCommandHandler(_d.Db, _d.Clock, _d.User)
            .Handle(new CreateAssociateGroupCommand("zn-01", "Otra", null), default);

        repetido.IsSuccess.Should().BeFalse();
        repetido.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        repetido.Error.Message.Should().Contain("Zona norte");
    }

    [Theory]
    [InlineData("", "Nombre")]
    [InlineData("CON ESPACIO", "Nombre")]
    [InlineData("12345678901", "Nombre")]
    [InlineData("OK", "")]
    public void El_validador_exige_codigo_alfanumerico_de_hasta_10_y_nombre(string codigo, string nombre) =>
        new CreateAssociateGroupCommandValidator().Validate(new CreateAssociateGroupCommand(codigo, nombre, null)).IsValid.Should().BeFalse();

    [Fact]
    public async Task Editar_cambia_nombre_y_descripcion_y_no_el_codigo()
    {
        var g = await Crear("ZN");
        var r = await new UpdateAssociateGroupCommandHandler(_d.Db, _d.Clock, _d.User)
            .Handle(new UpdateAssociateGroupCommand(g.PublicId, "Zona nueva", "Municipios del norte"), default);

        r.Value.Code.Should().Be("ZN");
        r.Value.Name.Should().Be("Zona nueva");
        r.Value.Description.Should().Be("Municipios del norte");
    }

    [Fact]
    public async Task El_listado_busca_por_codigo_y_por_nombre_y_oculta_inactivos()
    {
        await Crear("ZN", "Zona norte");
        var sur = await Crear("ZS", "Zona sur");
        await new SetAssociateGroupActiveCommandHandler(_d.Db, _d.Clock, _d.User)
            .Handle(new SetAssociateGroupActiveCommand(sur.PublicId, false, "ya no se usa"), default);
        var lista = new ListAssociateGroupsQueryHandler(_d.Db);

        (await lista.Handle(new ListAssociateGroupsQuery(), default)).Value.Select(x => x.Code).Should().Equal("ZN");
        (await lista.Handle(new ListAssociateGroupsQuery(true), default)).Value.Select(x => x.Code).Should().Equal("ZN", "ZS");
        (await lista.Handle(new ListAssociateGroupsQuery(true, "zs"), default)).Value.Select(x => x.Code).Should().Equal("ZS");
        (await lista.Handle(new ListAssociateGroupsQuery(true, "NORTE"), default)).Value.Select(x => x.Code).Should().Equal("ZN");
    }

    [Fact]
    public async Task La_consulta_de_codigo_repetido_conoce_el_catalogo()
    {
        var g = await Crear("ZN");
        var r = await new BuscarCodigoDeCatalogoQueryHandler(_d.Db)
            .Handle(new BuscarCodigoDeCatalogoQuery("grupos-del-asociado", "zn"), default);

        r.Value.Existe.Should().BeTrue();
        r.Value.PublicId.Should().Be(g.PublicId);
    }

    [Fact]
    public async Task Registrar_un_asociado_con_grupo_lo_guarda_y_el_listado_filtra_por_grupo()
    {
        var g = await Crear("ZN");
        var otro = await Crear("ZS");
        var p1 = _d.Persona("1", "Ana", "Ruiz");
        var p2 = _d.Persona("2", "Beto", "Paz");
        var registrar = new RegisterAssociateCommandHandler(_d.Db, _d.Asociados);

        (await registrar.Handle(new RegisterAssociateCommand { PersonPublicId = p1.PublicId, AssociateGroupPublicId = g.PublicId }, default)).IsSuccess.Should().BeTrue();
        (await registrar.Handle(new RegisterAssociateCommand { PersonPublicId = p2.PublicId, AssociateGroupPublicId = otro.PublicId }, default)).IsSuccess.Should().BeTrue();

        var lista = await new ListAssociatesQueryHandler(_d.Db)
            .Handle(new ListAssociatesQuery { AssociateGroupPublicId = g.PublicId }, default);

        lista.Value.Items.Should().ContainSingle().Which.AssociateGroupCode.Should().Be("ZN");
    }

    [Fact]
    public async Task Un_grupo_inexistente_o_inactivo_no_se_asigna()
    {
        var g = await Crear("ZN");
        await new SetAssociateGroupActiveCommandHandler(_d.Db, _d.Clock, _d.User)
            .Handle(new SetAssociateGroupActiveCommand(g.PublicId, false, "sin uso"), default);
        var p = _d.Persona("1");
        var registrar = new RegisterAssociateCommandHandler(_d.Db, _d.Asociados);

        (await registrar.Handle(new RegisterAssociateCommand { PersonPublicId = p.PublicId, AssociateGroupPublicId = g.PublicId }, default))
            .Error.Code.Should().Be("AssociateGroup.Inactive");
        (await registrar.Handle(new RegisterAssociateCommand { PersonPublicId = p.PublicId, AssociateGroupPublicId = Guid.NewGuid() }, default))
            .Error.Code.Should().Be("AssociateGroup.NotFound");
        (await _d.Db.Associates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Editar_al_asociado_cambia_y_quita_el_grupo_y_conserva_el_inactivo_que_ya_tenia()
    {
        var g = await Crear("ZN");
        var p = _d.Persona("1");
        var id = (await new RegisterAssociateCommandHandler(_d.Db, _d.Asociados)
            .Handle(new RegisterAssociateCommand { PersonPublicId = p.PublicId, AssociateGroupPublicId = g.PublicId }, default)).Value;
        var editar = new UpdateAssociateCommandHandler(_d.Db, _d.Clock, _d.User);

        // En uso: no se inactiva.
        var enUso = await new SetAssociateGroupActiveCommandHandler(_d.Db, _d.Clock, _d.User)
            .Handle(new SetAssociateGroupActiveCommand(g.PublicId, false, "prueba"), default);
        enUso.Error.Code.Should().Be("AssociateGroup.InUse");

        // Quitar el grupo y luego inactivarlo; el asociado no lo vuelve a tomar.
        (await editar.Handle(new UpdateAssociateCommand { PublicId = id }, default)).IsSuccess.Should().BeTrue();
        (await _d.Db.Associates.AsNoTracking().SingleAsync()).AssociateGroupId.Should().BeNull();
        (await new SetAssociateGroupActiveCommandHandler(_d.Db, _d.Clock, _d.User)
            .Handle(new SetAssociateGroupActiveCommand(g.PublicId, false, "prueba"), default)).IsSuccess.Should().BeTrue();
        (await editar.Handle(new UpdateAssociateCommand { PublicId = id, AssociateGroupPublicId = g.PublicId }, default))
            .Error.Code.Should().Be("AssociateGroup.Inactive");
    }
}