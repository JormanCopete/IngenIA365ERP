using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Salespeople;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.CreateSalesperson;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.DeleteSalesperson;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.UpdateSalesperson;
using IngenIA365ERP.Application.Inventory.Salespeople.Import;
using IngenIA365ERP.Application.Inventory.Salespeople.Queries;
using IngenIA365ERP.Application.Tests.Core.People;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using P = IngenIA365ERP.Application.Inventory.Salespeople.Import.PlantillaDeVendedores;

namespace IngenIA365ERP.Application.Tests.Inventory.Salespeople;

/// <summary>
/// Feature 012, T412 (US12; FR-031, FR-092; data-model §11; contracts/api.md §31, contracts/plantillas.md §9): el rol vendedor
/// se da, se restaura y se retira sólo por <see cref="RolDeVendedor"/>, que escribe la fila de <c>INV_Salespeople</c> y la
/// marca <c>IsSalesperson</c> de la persona en el mismo <c>SaveChangesAsync</c>. Restaurar conserva el <c>PublicId</c>; la
/// importación revisa sin guardar, aplica todo o nada, rechaza la fila de una persona que no existe y nunca crea ni
/// modifica personas.
/// </summary>
public class SalespeopleCommandsTests
{
    private readonly PersonasTestData _d = new();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private TablaLeida _tabla = new([], [], "xlsx");

    public SalespeopleCommandsTests()
    {
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(
            ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(), "Jefe de ventas", "jefe@coop.test", ExecutionChannel.Web, "/api/inventory/salespeople", "10.0.0.1", null));
        _permisos.EsMaestroGlobal.Returns(true);
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(_tabla)));
    }

    private RolDeVendedor Rol() => new(_d.Db, _d.Clock, _actor);

    private Task<Result<CreateSalespersonResultDto>> Crear(Person p, int? tipo = null, bool? comision = null) =>
        new CreateSalespersonCommandHandler(_d.Db, Rol()).Handle(
            new CreateSalespersonCommand { PersonPublicId = p.PublicId, SalespersonType = tipo, AppliesCommission = comision, OperationKey = Guid.NewGuid() },
            CancellationToken.None);

    private Task<Result> Retirar(Guid vendedor, string motivo = "Dejó de vender") =>
        new DeleteSalespersonCommandHandler(_d.Db, Rol()).Handle(new DeleteSalespersonCommand(vendedor, motivo) { OperationKey = Guid.NewGuid() }, CancellationToken.None);

    private async Task<Person> PersonaAsync(Guid id) => await _d.Db.People.AsNoTracking().IgnoreQueryFilters().SingleAsync(p => p.PublicId == id);

    // ------------------------------------------------------------------------------------------------ alta --

    [Fact]
    public async Task Dar_el_rol_crea_la_fila_y_enciende_IsSalesperson_en_el_mismo_guardado()
    {
        var p = _d.Persona();

        var r = await Crear(p, tipo: 2, comision: true);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Restored.Should().BeFalse();
        var fila = await _d.Db.Salespeople.AsNoTracking().SingleAsync();
        fila.PublicId.Should().Be(r.Value.SalespersonPublicId);
        fila.SalespersonType.Should().Be(2);
        fila.AppliesCommission.Should().BeTrue();
        (await PersonaAsync(p.PublicId)).IsSalesperson.Should().BeTrue();
        _d.Db.ChangeTracker.HasChanges().Should().BeFalse("un solo SaveChangesAsync");
    }

    [Fact]
    public async Task Sobre_una_persona_con_fila_retirada_la_restaura_con_el_mismo_PublicId()
    {
        var p = _d.Persona();
        var primera = await Crear(p, tipo: 1);
        (await Retirar(primera.Value.SalespersonPublicId)).IsSuccess.Should().BeTrue();

        var r = await Crear(p, comision: true);

        r.Value.Restored.Should().BeTrue();
        r.Value.SalespersonPublicId.Should().Be(primera.Value.SalespersonPublicId);
        var filas = await _d.Db.Salespeople.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        filas.Should().ContainSingle("restaurar no crea otra fila").Which.Should().Match<Domain.Entities.Inventory.Salesperson>(s =>
            !s.IsDeleted && s.DeletedAt == null && s.SalespersonType == 1 && s.AppliesCommission);
        (await PersonaAsync(p.PublicId)).IsSalesperson.Should().BeTrue();
    }

    [Fact]
    public async Task Con_el_rol_vivo_es_AlreadyActive()
    {
        var p = _d.Persona(vendedora: true);

        var r = await Crear(p);

        r.Error.Code.Should().Be("Inventory.Salesperson.AlreadyActive");
    }

    [Fact]
    public async Task Una_persona_eliminada_o_inexistente_es_Core_Person_NotFound()
    {
        var eliminada = _d.Persona(eliminada: true);

        (await Crear(eliminada)).Error.Code.Should().Be("Core.Person.NotFound");
        (await Crear(new Person { PublicId = Guid.NewGuid() })).Error.Code.Should().Be("Core.Person.NotFound");
        (await _d.Db.Salespeople.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------- retiro --

    [Fact]
    public async Task Retirar_exige_motivo_da_de_baja_la_fila_y_apaga_la_marca()
    {
        var p = _d.Persona(vendedora: true, asociada: true);
        var fila = await _d.Db.Salespeople.SingleAsync();
        new DeleteSalespersonCommandValidator().Validate(new DeleteSalespersonCommand(fila.PublicId, " ")).IsValid.Should().BeFalse();

        var r = await Retirar(fila.PublicId);

        r.IsSuccess.Should().BeTrue();
        var guardada = await _d.Db.Salespeople.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        guardada.IsDeleted.Should().BeTrue();
        guardada.DeletedAt.Should().Be(PersonasTestData.Ahora);
        guardada.DeletedBy.Should().Be("Jefe de ventas");
        var persona = await PersonaAsync(p.PublicId);
        persona.IsSalesperson.Should().BeFalse();
        persona.IsAssociate.Should().BeTrue("sólo se apaga la marca del rol que se retira");
        (await Retirar(fila.PublicId)).Error.Code.Should().Be("Generic.NotFound", "ya retirado");
    }

    [Fact]
    public async Task Actualizar_solo_cambia_tipo_y_comision()
    {
        _d.Persona(vendedora: true);
        var fila = await _d.Db.Salespeople.SingleAsync();

        var r = await new UpdateSalespersonCommandHandler(_d.Db).Handle(
            new UpdateSalespersonCommand { PublicId = fila.PublicId, SalespersonType = 3, AppliesCommission = true, OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue();
        var guardada = await _d.Db.Salespeople.AsNoTracking().SingleAsync();
        (guardada.SalespersonType, guardada.AppliesCommission).Should().Be((3, true));
    }

    [Fact]
    public async Task La_lista_trae_la_persona_y_con_includeRetired_tambien_los_retirados()
    {
        _d.Persona(taxId: "16000111", nombre: "María", apellido: "Pérez", vendedora: true);
        var juan = _d.Persona(taxId: "94500222", nombre: "Juan", apellido: "Gómez");
        var creado = await Crear(juan);
        await Retirar(creado.Value.SalespersonPublicId);

        var vivos = await new ListSalespeopleQueryHandler(_d.Db).Handle(new ListSalespeopleQuery(), default);
        var todos = await new ListSalespeopleQueryHandler(_d.Db).Handle(new ListSalespeopleQuery(IncludeRetired: true), default);
        var buscados = await new ListSalespeopleQueryHandler(_d.Db).Handle(new ListSalespeopleQuery(Search: "16000"), default);

        vivos.Value.Items.Should().ContainSingle().Which.Person.Should().Be(
            new SalespersonPersonDto(_d.Db.People.Single(p => p.TaxId == "16000111").PublicId, "María Pérez", "16000111"));
        todos.Value.TotalCount.Should().Be(2);
        todos.Value.Items.Single(v => v.Person.IdNumber == "94500222").IsActive.Should().BeFalse();
        buscados.Value.Items.Should().ContainSingle();
    }

    // -------------------------------------------------------------------------------------- importación --

    private void Datos(params string?[][] filas) =>
        _tabla = new TablaLeida([P.Documento, P.Nombre, P.TipoDeVendedor, P.AplicaComision, P.Activo],
            filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private async Task<Result<ImportResultDto>> Importar(ModoDeImportacion modo, string motivo = "")
    {
        var ejecutor = new EjecutorDeImportacion(_d.Db, _lector, _permisos,
            new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider());
        var r = await new ImportSalespeopleCommandHandler(_d.Db, ejecutor, Rol())
            .Handle(new ImportSalespeopleCommand(modo, new ArchivoDeImportacion("vendedores.xlsx", [1, 2, 3]), motivo), default);
        _d.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task La_revision_no_guarda_nada()
    {
        _d.Persona(taxId: "16000111", nombre: "María", apellido: "Pérez");
        Datos(["16000111", "MARÍA PÉREZ", "1", "no", "sí"]);

        var r = await Importar(ModoDeImportacion.Review);

        r.IsSuccess.Should().BeTrue();
        r.Value.Valid.Should().BeTrue();
        r.Value.Sheets.Single().Created.Should().Be(1);
        (await _d.Db.Salespeople.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await _d.Db.People.SingleAsync()).IsSalesperson.Should().BeFalse();
    }

    [Fact]
    public async Task Aplicar_crea_restaura_y_retira_con_la_misma_operacion()
    {
        _d.Persona(taxId: "16000111", nombre: "María", apellido: "Pérez");
        var juan = _d.Persona(taxId: "94500222", nombre: "Juan", apellido: "Gómez", vendedora: true);
        var ana = _d.Persona(taxId: "30111222", nombre: "Ana", apellido: "Ruiz");
        var deAna = await Crear(ana, tipo: 1);
        await Retirar(deAna.Value.SalespersonPublicId);
        _d.Db.ChangeTracker.Clear();
        Datos(
            ["16000111", "MARÍA PÉREZ", "1", "no", "sí"],
            ["94500222", "JUAN GÓMEZ", "", "sí", "no"],
            ["30111222", "ANA RUIZ", "2", "sí", "sí"]);

        var sinMotivo = await Importar(ModoDeImportacion.Apply);
        var r = await Importar(ModoDeImportacion.Apply, "Carga inicial de COOFLOPAL");

        sinMotivo.Error.Code.Should().Be("Import.Invalid", "retirar pide motivo");
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var filas = await _d.Db.Salespeople.IgnoreQueryFilters().AsNoTracking().Include(s => s.Person).ToListAsync();
        filas.Should().HaveCount(3);
        filas.Single(s => s.Person.TaxId == "16000111").IsDeleted.Should().BeFalse();
        filas.Single(s => s.Person.TaxId == "94500222").IsDeleted.Should().BeTrue();
        var restaurada = filas.Single(s => s.Person.TaxId == "30111222");
        restaurada.PublicId.Should().Be(deAna.Value.SalespersonPublicId);
        (restaurada.IsDeleted, restaurada.SalespersonType, restaurada.AppliesCommission).Should().Be((false, 2, true));
        var personas = await _d.Db.People.AsNoTracking().ToDictionaryAsync(p => p.TaxId);
        personas["16000111"].IsSalesperson.Should().BeTrue();
        personas["94500222"].IsSalesperson.Should().BeFalse();
        personas["30111222"].IsSalesperson.Should().BeTrue();
        personas["94500222"].PublicId.Should().Be(juan.PublicId);
    }

    [Fact]
    public async Task Aplicar_es_todo_o_nada_y_rechaza_la_persona_que_no_existe_sin_crearla()
    {
        _d.Persona(taxId: "16000111", nombre: "María", apellido: "Pérez");
        _d.Persona(taxId: "55000333", nombre: "Eliminada", apellido: "X", eliminada: true);
        Datos(
            ["16000111", "MARÍA PÉREZ", "1", "no", "sí"],
            ["99999999", "NADIE", "", "", ""],
            ["55000333", "", "", "", ""]);

        var r = await Importar(ModoDeImportacion.Apply);

        r.Error.Code.Should().Be("Import.Invalid");
        var dto = (ImportResultDto)r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data!;
        dto.Errors.Select(e => (e.Row, e.Column, e.Code)).Should().BeEquivalentTo(new[]
        {
            (3, P.Documento, "Import.Cell.NotFound"),
            (4, P.Documento, "Core.Person.NotFound"),
        });
        (await _d.Db.Salespeople.IgnoreQueryFilters().CountAsync()).Should().Be(0, "nada se guarda a medias");
        (await _d.Db.People.IgnoreQueryFilters().CountAsync()).Should().Be(2, "la plantilla no crea personas");
    }

    [Fact]
    public async Task Un_nombre_que_no_coincide_es_un_aviso_y_no_modifica_la_persona()
    {
        _d.Persona(taxId: "16000111", nombre: "María", apellido: "Pérez");
        Datos(["16000111", "MARIO PÉREZ", "", "", ""]);

        var r = await Importar(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue();
        r.Value.Warnings.Should().ContainSingle(w => w.Column == P.Nombre);
        (await _d.Db.People.SingleAsync()).FirstName.Should().Be("María");
    }
}
