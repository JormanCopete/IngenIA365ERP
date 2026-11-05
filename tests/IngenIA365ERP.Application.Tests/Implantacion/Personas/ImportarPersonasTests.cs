using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Imports;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using P = IngenIA365ERP.Application.Core.People.Imports.PlantillaDePersonas;

namespace IngenIA365ERP.Application.Tests.Implantacion.Personas;

/// <summary>
/// T021 (feature 016, US1; contracts/plantillas.md §2): la plantilla <c>core.people</c> sobre el ejecutor común, con las reglas del alta
/// unitaria. La llave es el usuario derivado de la 017; una eliminada es <c>Person.TaxIdDeleted</c>; actualizar cambia sólo lo que trae
/// valor y deja el cambio campo a campo; las marcas de rol (asociado, empleado, vendedor) no se escriben.
/// </summary>
public class ImportarPersonasTests : IDisposable
{
    private static readonly string[] Encabezados =
    [
        P.TipoDocumento, P.Documento, P.DigitoVerificacion, P.Naturaleza, P.PrimerNombre, P.PrimerApellido, P.SegundoApellido,
        P.RazonSocial, P.Ciudad, P.FechaNacimiento, P.EsProveedor, P.EsCliente, P.Correo, P.Genero,
    ];

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IAuditService _auditoria = Substitute.For<IAuditService>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly List<AuditLogCommand> _eventos = [];
    private TablaLeida _tabla = new([], [], "xlsx");

    public ImportarPersonasTests()
    {
        _permisos.EsMaestroGlobal.Returns(true);
        _reloj.UtcNow.Returns(new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc));
        _usuario.UserName.Returns("implantacion");
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(_tabla)));
        _auditoria.LogAsync(Arg.Do<AuditLogCommand>(_eventos.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _db.Cities.Add(new City { Id = 1, Name = "Cali", DaneCode = "76001", LegacyCode = "CALI", DepartmentId = 1 });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private void Archivo(params string?[][] filas) =>
        _tabla = new TablaLeida(Encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private static string?[] Natural(string documento, string nombre = "ANA", string apellido = "MUÑOZ", string tipo = "CC",
        string? ciudad = "76001", string? proveedor = null, string? correo = null, string? fecha = "1980-01-15") =>
        [tipo, documento, null, "natural", nombre, apellido, null, null, ciudad, fecha, proveedor, null, correo, null];

    private async Task<Result<ImportResultDto>> Importar(ModoDeImportacion modo, SiYaExiste existentes = SiYaExiste.Actualizar)
    {
        var servicios = new ServiceCollection().AddSingleton(_auditoria).BuildServiceProvider();
        var ejecutor = new EjecutorDeImportacion(_db, _lector, _permisos, servicios);
        var factory = new PersonFactory(_db, _reloj, _usuario);
        var r = await new ImportPeopleCommandHandler(_db, ejecutor, factory, _reloj, _usuario)
            .Handle(new ImportPeopleCommand(modo, new ArchivoDeImportacion("personas.xlsx", [1, 2, 3]), Existentes: existentes), CancellationToken.None);
        _db.ChangeTracker.Clear();
        return r;
    }

    private static string Errores(ImportResultDto r) => string.Join("; ", r.Errors.Select(e => $"{e.Row}/{e.Column} {e.Code} {e.Message}"));

    private Person Existente(string tipo, string numero, bool eliminada = false, string nombre = "LUIS")
    {
        var p = new Person
        {
            IdType = tipo, TaxId = numero, Username = Domain.Common.Identidad.UsuarioDeDocumento.DerivarParaPersona(tipo, numero),
            FirstName = nombre, LastName = "GÓMEZ", PersonType = P.TipoNatural, Status = "A", IsAssociate = true,
            IsDeleted = eliminada, DeletedAt = eliminada ? new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc) : null,
        };
        _db.People.Add(p);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return p;
    }

    [Fact]
    public void La_plantilla_es_de_plataforma_con_los_permisos_de_Personas()
    {
        var plantilla = CatalogoDePlantillasDePlataforma.Por(P.Clave);

        plantilla.RutaBase.Should().Be("/api/core/people");
        plantilla.PermisoDeDescarga.Should().Be("Core.People.View");
        plantilla.PermisoDeImportacion.Should().Be("Core.People.Create");
        plantilla.Definicion.Modulo.Should().Be(ModuloDeAuditoria.Core);
        plantilla.Definicion.Hojas.Single().Columnas.Select(c => c.Nombre)
            .Should().NotContain(["banco", "tipoCuenta", "numeroCuenta"], "las cuentas van por la plantilla de la 015 (D10)");
    }

    [Fact]
    public async Task Tres_buenas_y_dos_malas_la_revision_no_guarda_y_dice_fila_y_columna()
    {
        Archivo(
            Natural("1010101010"),
            Natural("2020202020", nombre: "LUIS", apellido: "GÓMEZ"),
            ["NIT", "900123456", "7", "juridica", null, null, null, "DISTRIBUIDORA EJEMPLO SAS", "76001", null, "sí", null, null, null],
            Natural("123456", tipo: "CE", fecha: "2090-01-01"),
            Natural("3030303030", ciudad: "99999"));

        var r = await Importar(ModoDeImportacion.Review);

        r.IsSuccess.Should().BeTrue();
        r.Value.Valid.Should().BeFalse();
        r.Value.Errors.Select(e => (e.Row, e.Column)).Should().BeEquivalentTo([(5, P.FechaNacimiento), (6, P.Ciudad)], Errores(r.Value));
        r.Value.Sheets.Single().Created.Should().Be(3);
        (await _db.People.CountAsync()).Should().Be(0);
        _eventos.Should().ContainSingle(e => e.Action == EjecutorDeImportacion.EventoRevisada);
    }

    [Fact]
    public async Task Aplicar_crea_como_el_alta_unitaria_con_usuario_naturaleza_y_marca_de_proveedor()
    {
        Archivo(
            Natural("1.010.101.010", correo: "ana@correo.test"),
            ["NIT", "900123456", "7", "juridica", null, null, null, "DISTRIBUIDORA EJEMPLO SAS", "CALI", null, "sí", "no", null, null]);

        var r = await Importar(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var ana = await _db.People.SingleAsync(p => p.Username == "1010101010");
        ana.IdType.Should().Be("C");
        ana.TaxId.Should().Be("1010101010", "el número se guarda normalizado");
        ana.PersonType.Should().Be(P.TipoNatural);
        ana.CityId.Should().Be(1);
        ana.DateOfBirth.Should().Be(new DateOnly(1980, 1, 15));
        ana.Status.Should().Be("A");
        ana.IsSupplier.Should().BeFalse();
        var nit = await _db.People.SingleAsync(p => p.Username == "NIT900123456");
        nit.IdType.Should().Be("NI");
        nit.BusinessName.Should().Be("DISTRIBUIDORA EJEMPLO SAS");
        nit.FirstName.Should().Be("DISTRIBUIDORA EJEMPLO SAS", "la jurídica sin nombres toma la razón social, que es lo que exige el alta");
        nit.TaxIdCheckDigit.Should().Be("7");
        nit.IsSupplier.Should().BeTrue();
        new[] { ana, nit }.Should().AllSatisfy(p => (p.IsAssociate || p.IsEmployee || p.IsSalesperson).Should().BeFalse());
        _eventos.Should().ContainSingle(e => e.Action == EjecutorDeImportacion.EventoAplicada);
    }

    [Fact]
    public async Task Un_documento_existente_se_actualiza_solo_con_lo_que_trae_y_el_cambio_campo_a_campo()
    {
        Existente("C", "2020202020");
        Archivo(Natural("2020202020", nombre: "LUIS", apellido: "GÓMEZ", ciudad: null, proveedor: "si", correo: "luis@correo.test", fecha: null));

        var r = await Importar(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Sheets.Single().Updated.Should().Be(1);
        r.Value.Changes.Single().Fields.Should().BeEquivalentTo(
        [
            new CampoCambiadoDto(P.Correo, null, "luis@correo.test"),
            new CampoCambiadoDto(P.EsProveedor, "no", "sí"),
        ], "nombre, apellido y naturaleza vienen iguales y no cuentan como cambio");
        var luis = await _db.People.SingleAsync();
        luis.Email.Should().Be("luis@correo.test");
        luis.IsSupplier.Should().BeTrue();
        luis.IsAssociate.Should().BeTrue("la plantilla nunca toca las marcas de rol");
        luis.CityId.Should().BeNull("una celda vacía no cambia nada");
    }

    [Fact]
    public async Task La_misma_fila_otra_vez_queda_sin_cambio()
    {
        Existente("C", "2020202020");
        Archivo(Natural("2020202020", nombre: "LUIS", apellido: "GÓMEZ", ciudad: null, fecha: null));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Sheets.Single().Unchanged.Should().Be(1);
    }

    [Fact]
    public async Task Una_eliminada_es_TaxIdDeleted_en_la_columna_del_documento()
    {
        Existente("C", "4040404040", eliminada: true);
        Archivo(Natural("4040404040"));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Errors.Should().ContainSingle(e => e.Code == "Person.TaxIdDeleted" && e.Column == P.Documento && e.Row == 2);
    }

    [Fact]
    public async Task CC_y_CE_con_el_mismo_numero_son_dos_personas_y_la_misma_llave_repetida_es_error()
    {
        Archivo(Natural("123456"), Natural("123456", tipo: "CE"), Natural("123.456"));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Errors.Should().ContainSingle(e => e.Code == ImportErrors.RowDuplicate && e.Row == 4, Errores(r.Value));
        r.Value.Sheets.Single().Created.Should().Be(2);
    }

    [Fact]
    public async Task Los_codigos_de_tipo_de_la_persona_tambien_se_aceptan()
    {
        Archivo(Natural("5050505050", tipo: "C"), Natural("123456789", tipo: "PT"));

        var r = await Importar(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        (await _db.People.Select(p => p.Username).ToListAsync()).Should().BeEquivalentTo(["5050505050", "PPT123456789"]);
    }

    [Fact]
    public async Task Solo_agregar_deja_la_existente_como_esta_y_crea_las_nuevas()
    {
        _permisos.EsMaestroGlobal.Returns(false);
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(["Core.People.Create"]);
        Existente("C", "2020202020");
        Archivo(Natural("2020202020", nombre: "OTRO", correo: "esto no es un correo"), Natural("3030303030"));

        var r = await Importar(ModoDeImportacion.Apply, SiYaExiste.Conservar);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var hoja = r.Value.Sheets.Single();
        (hoja.Created, hoja.Updated, hoja.Unchanged).Should().Be((1, 0, 1));
        var luis = await _db.People.SingleAsync(p => p.TaxId == "2020202020");
        luis.FirstName.Should().Be("LUIS");
        luis.Email.Should().BeNull();
    }

    [Fact]
    public async Task Solo_agregar_no_perdona_una_eliminada_ni_una_fila_repetida()
    {
        Existente("C", "2020202020", eliminada: true);
        Existente("C", "4040404040");
        Archivo(Natural("2020202020"), Natural("4040404040"), Natural("4040404040"));

        var r = await Importar(ModoDeImportacion.Review, SiYaExiste.Conservar);

        r.Value.Errors.Select(e => e.Code).Should().BeEquivalentTo(["Person.TaxIdDeleted", ImportErrors.RowDuplicate]);
    }

    [Fact]
    public async Task Sin_permiso_de_actualizar_una_existente_es_error()
    {
        _permisos.EsMaestroGlobal.Returns(false);
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(["Core.People.Create"]);
        Existente("C", "2020202020");
        Archivo(Natural("2020202020", correo: "otro@correo.test"));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Errors.Should().ContainSingle(e => e.Code == ImportErrors.CellPermissionRequired && e.Column == P.Documento);
    }

    [Fact]
    public async Task Natural_sin_apellido_y_correo_invalido_dan_el_error_del_alta_en_su_columna()
    {
        Archivo(Natural("6060606060", apellido: ""), Natural("7070707070", correo: "no-es-correo"));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Errors.Select(e => (e.Row, e.Column)).Should().BeEquivalentTo([(2, P.PrimerApellido), (3, P.Correo)], Errores(r.Value));
    }

    [Fact]
    public async Task El_digito_de_un_NIT_que_no_corresponde_es_aviso_no_error()
    {
        Archivo(["NIT", "900123456", "3", "juridica", null, null, null, "EJEMPLO SAS", null, null, null, null, null, null]);

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Valid.Should().BeTrue(Errores(r.Value));
        r.Value.Warnings.Should().ContainSingle(w => w.Code == ImportPeopleCommandHandler.CodigoDigitoDistinto);
        P.DigitoDeVerificacionDian("900123456").Should().Be(8);
    }
}
