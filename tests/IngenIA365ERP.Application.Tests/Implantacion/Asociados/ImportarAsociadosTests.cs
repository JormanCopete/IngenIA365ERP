using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.Associates.Imports;
using IngenIA365ERP.Application.Core.Associates.Services;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using P = IngenIA365ERP.Application.Core.Associates.Imports.PlantillaDeAsociados;

namespace IngenIA365ERP.Application.Tests.Implantacion.Asociados;

/// <summary>
/// T029 (feature 016, US3; contracts/plantillas.md §3): la plantilla <c>core.associates</c> sobre el ejecutor común. Nunca crea personas;
/// crea por <see cref="AssociateRegistrar"/>; actualiza sin duplicar; un retirado sólo nace retirado, el retiro de un vigente es de Cartera.
/// </summary>
public class ImportarAsociadosTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 10, 30);

    private static readonly string[] Encabezados =
    [
        P.TipoDocumento, P.Documento, P.FechaAfiliacion, P.Oficina, P.Estado, P.Grupo, P.Clase, P.FechaRetiro, P.MotivoRetiro,
        P.FechaReingreso, P.PeriodicidadDescuento, P.FormaDescuento, P.TasaAporte,
    ];

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IAuditService _auditoria = Substitute.For<IAuditService>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private TablaLeida _tabla = new([], [], "xlsx");

    public ImportarAsociadosTests()
    {
        _permisos.EsMaestroGlobal.Returns(true);
        _reloj.UtcNow.Returns(new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc));
        _reloj.HoyLocal.Returns(Hoy);
        _usuario.UserName.Returns("implantacion");
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(_tabla)));
        _db.Branches.Add(new Branch { Id = 1, LegacyCode = "PRIN", Name = "PRINCIPAL" });
        _db.AssociateGroups.AddRange(
            new AssociateGroup { Id = 1, Code = "ZONA1", Name = "ZONA UNO" },
            new AssociateGroup { Id = 2, Code = "ZONA2", Name = "ZONA DOS" },
            new AssociateGroup { Id = 3, Code = "VIEJO", Name = "VIEJO", IsActive = false });
        _db.WithdrawalReasons.AddRange(new WithdrawalReason { Id = 1, LegacyCode = "OTRO", Name = "OTRO" }, new WithdrawalReason { Id = 2, LegacyCode = "PERS", Name = "PERSONAL" });
        _db.SaveChanges();
        Persona("1010101010");
        Persona("2020202020");
    }

    public void Dispose() => _db.Dispose();

    private Person Persona(string numero)
    {
        var p = new Person
        {
            IdType = "C", TaxId = numero, Username = Domain.Common.Identidad.UsuarioDeDocumento.DerivarParaPersona("C", numero),
            FirstName = "ANA", LastName = "MUÑOZ", PersonType = "01", Status = "A",
        };
        _db.People.Add(p);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return p;
    }

    private void Archivo(params string?[][] filas) =>
        _tabla = new TablaLeida(Encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private static string?[] Fila(string documento, string estado = "ACTIVO", string fecha = "2015-03-01", string? grupo = null, string? clase = "ASOCIADO",
        string? retiro = null, string? motivo = null, string? reingreso = null, string? periodicidad = "MENSUAL", string? forma = "CAJA", string? tasa = null) =>
        ["CC", documento, fecha, "PRINCIPAL", estado, grupo, clase, retiro, motivo, reingreso, periodicidad, forma, tasa];

    private async Task<Result<ImportResultDto>> Importar(ModoDeImportacion modo, SiYaExiste existentes = SiYaExiste.Actualizar)
    {
        var servicios = new ServiceCollection().AddSingleton(_auditoria).BuildServiceProvider();
        var ejecutor = new EjecutorDeImportacion(_db, _lector, _permisos, servicios);
        var r = await new ImportAssociatesCommandHandler(_db, ejecutor, new AssociateRegistrar(_db, _reloj, _usuario), _reloj, _usuario)
            .Handle(new ImportAssociatesCommand(modo, new ArchivoDeImportacion("asociados.xlsx", [1, 2, 3]), Existentes: existentes), CancellationToken.None);
        _db.ChangeTracker.Clear();
        return r;
    }

    private static string Errores(ImportResultDto r) => string.Join("; ", r.Errors.Select(e => $"{e.Row}/{e.Column} {e.Code} {e.Message}"));

    private async Task Aplicar(params string?[][] filas)
    {
        Archivo(filas);
        var r = await Importar(ModoDeImportacion.Apply);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
    }

    [Fact]
    public void La_plantilla_es_de_plataforma_con_los_permisos_de_Asociados()
    {
        var plantilla = CatalogoDePlantillasDePlataforma.Por(P.Clave);

        plantilla.RutaBase.Should().Be("/api/core/associates");
        plantilla.PermisoDeDescarga.Should().Be("Core.Associates.View");
        plantilla.PermisoDeImportacion.Should().Be("Core.Associates.Create");
        plantilla.Definicion.Modulo.Should().Be(ModuloDeAuditoria.Core);
    }

    [Fact]
    public async Task Un_documento_que_no_existe_es_error_y_con_un_error_no_se_guarda_ninguno()
    {
        Archivo(Fila("1010101010"), Fila("9999999999"));

        var revision = await Importar(ModoDeImportacion.Review);

        revision.Value.Errors.Should().ContainSingle(e => e.Code == ImportErrors.CellNotFound && e.Column == P.Documento && e.Row == 3
            && e.Message.Contains("esta plantilla no crea personas"), Errores(revision.Value));
        (await _db.Associates.CountAsync()).Should().Be(0, "revisar no guarda");

        var aplicada = await Importar(ModoDeImportacion.Apply);

        aplicada.IsFailure.Should().BeTrue();
        aplicada.Error.Code.Should().Be(ImportErrors.InvalidCode);
        (await _db.Associates.CountAsync()).Should().Be(0);
        (await _db.People.CountAsync()).Should().Be(2, "nunca crea personas");
        (await _db.People.AnyAsync(p => p.IsAssociate)).Should().BeFalse();
    }

    [Fact]
    public async Task Crea_como_el_alta_unitaria_con_marca_y_datos_del_archivo()
    {
        await Aplicar(Fila("1010101010", estado: "ACTIVO", grupo: "ZONA1", clase: "AFILIADO", periodicidad: "QUINCENAL", forma: "NÓMINA", tasa: "5"));

        var persona = await _db.People.SingleAsync(p => p.TaxId == "1010101010");
        persona.IsAssociate.Should().BeTrue();
        persona.UpdatedBy.Should().Be("implantacion");
        var a = await _db.Associates.SingleAsync();
        a.PersonId.Should().Be(persona.Id);
        a.JoinDate.Should().Be(new DateOnly(2015, 3, 1));
        a.BranchId.Should().Be(1);
        a.AssociateGroupId.Should().Be(1);
        a.Status.Should().Be("A");
        a.AssociateClass.Should().Be("AFIL");
        a.DeductionPeriod.Should().Be("Q");
        a.DeductionType.Should().Be("N");
        a.ContributionRate.Should().Be(5m);
        a.CreatedBy.Should().Be("implantacion");
    }

    [Theory]
    [InlineData("ASOCIADO", "ASOC")]
    [InlineData("AFILIADO", "AFIL")]
    [InlineData("EMPLEADO", "EMPL")]
    public async Task Las_tres_clases_dan_fila_de_asociado(string clase, string codigo)
    {
        await Aplicar(Fila("1010101010", clase: clase));

        (await _db.Associates.SingleAsync()).AssociateClass.Should().Be(codigo);
    }

    [Fact]
    public async Task Aplicar_otro_grupo_actualiza_sin_duplicar_y_lo_vacio_no_cambia()
    {
        await Aplicar(Fila("1010101010", grupo: "ZONA1", tasa: "5"));
        Archivo(Fila("1010101010", grupo: "ZONA2", clase: null, periodicidad: null, forma: null));

        var r = await Importar(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Sheets.Single().Updated.Should().Be(1);
        r.Value.Changes.Single().Fields.Should().ContainSingle(f => f.Column == P.Grupo && f.After == "ZONA2");
        var a = await _db.Associates.SingleAsync();
        a.AssociateGroupId.Should().Be(2);
        a.ContributionRate.Should().Be(5m, "una celda vacía no cambia nada");
        a.AssociateClass.Should().Be("ASOC");
    }

    [Fact]
    public async Task La_misma_fila_otra_vez_queda_sin_cambio()
    {
        var fila = Fila("1010101010", grupo: "ZONA1", tasa: "5");
        await Aplicar(fila);
        Archivo(fila);

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Sheets.Single().Unchanged.Should().Be(1, Errores(r.Value));
    }

    [Fact]
    public async Task Un_retirado_nace_retirado_con_fecha_y_motivo_y_no_cuenta_como_vigente()
    {
        await Aplicar(Fila("1010101010", estado: "RETIRADO", retiro: "2024-06-30", motivo: "PERSONAL"));

        var a = await _db.Associates.SingleAsync();
        a.Status.Should().Be("R");
        a.WithdrawalDate.Should().Be(new DateOnly(2024, 6, 30));
        a.WithdrawalReasonId.Should().Be(2);
        (await _db.People.SingleAsync(p => p.Id == a.PersonId)).IsAssociate.Should().BeTrue();
        PersonaDeOriginacion.Retirado(a.Status, a.WithdrawalDate, a.RejoinDate, Hoy).Should().BeTrue();
    }

    [Fact]
    public async Task Un_vigente_con_reingreso_posterior_al_retiro_guarda_las_dos_fechas_y_cuenta_como_vigente()
    {
        await Aplicar(Fila("1010101010", retiro: "2020-01-31", motivo: "OTRO", reingreso: "2022-02-01"));

        var a = await _db.Associates.SingleAsync();
        a.Status.Should().Be("A");
        a.WithdrawalDate.Should().Be(new DateOnly(2020, 1, 31));
        a.WithdrawalReasonId.Should().Be(1);
        a.RejoinDate.Should().Be(new DateOnly(2022, 2, 1));
        PersonaDeOriginacion.Retirado(a.Status, a.WithdrawalDate, a.RejoinDate, Hoy).Should().BeFalse();
    }

    [Fact]
    public async Task Pasar_a_retirado_a_un_vigente_es_de_Cartera()
    {
        await Aplicar(Fila("1010101010"));
        Archivo(Fila("1010101010", estado: "RETIRADO", retiro: "2024-06-30", motivo: "OTRO"));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Errors.Should().ContainSingle(e => e.Code == "Associate.WithdrawalOwnedByLending" && e.Column == P.Estado, Errores(r.Value));
    }

    [Fact]
    public async Task Un_retirado_no_se_reactiva_por_archivo_y_otra_fecha_de_retiro_solo_avisa()
    {
        await Aplicar(Fila("1010101010", estado: "RETIRADO", retiro: "2024-06-30", motivo: "OTRO"));

        Archivo(Fila("1010101010"));
        var reactivar = await Importar(ModoDeImportacion.Review);
        reactivar.Value.Errors.Should().ContainSingle(e => e.Code == "Associate.WithdrawalOwnedByLending" && e.Column == P.Estado, Errores(reactivar.Value));

        Archivo(Fila("1010101010", estado: "RETIRADO", retiro: "2024-07-31", motivo: "OTRO"));
        var otraFecha = await Importar(ModoDeImportacion.Apply);
        otraFecha.IsSuccess.Should().BeTrue(otraFecha.IsFailure ? otraFecha.Error.Message : null);
        otraFecha.Value.Warnings.Should().ContainSingle(w => w.Code == ImportAssociatesCommandHandler.CodigoSoloAlCrear && w.Column == P.FechaRetiro);
        (await _db.Associates.SingleAsync()).WithdrawalDate.Should().Be(new DateOnly(2024, 6, 30));
    }

    [Fact]
    public async Task La_afiliacion_futura_da_el_error_del_alta_en_su_columna()
    {
        Archivo(Fila("1010101010", fecha: "2090-01-01"));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Errors.Should().ContainSingle(e => e.Code == ImportAssociatesCommandHandler.CodigoInvalido && e.Column == P.FechaAfiliacion, Errores(r.Value));
    }

    [Fact]
    public async Task Estado_y_fechas_que_se_contradicen_son_error()
    {
        Archivo(
            Fila("1010101010", estado: "RETIRADO"),
            Fila("2020202020", retiro: "2024-06-30", motivo: "OTRO"));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Errors.Select(e => (e.Row, e.Column)).Should().BeEquivalentTo(
            [(2, P.FechaRetiro), (2, P.MotivoRetiro), (3, P.FechaRetiro)], Errores(r.Value));
    }

    [Fact]
    public async Task Valores_y_catalogos_que_no_existen_dicen_fila_y_columna()
    {
        Archivo(
            Fila("1010101010", clase: "SOCIO", forma: "BANCO"),
            Fila("2020202020", grupo: "VIEJO"));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Errors.Select(e => (e.Row, e.Column)).Should().BeEquivalentTo(
            [(2, P.Clase), (2, P.FormaDescuento), (3, P.Grupo)], Errores(r.Value));
    }

    [Fact]
    public async Task Solo_agregar_deja_al_asociado_como_esta_aunque_su_fila_diga_otra_cosa()
    {
        Persona("2020202020");
        await Aplicar(Fila("1010101010", tasa: "5"));
        _permisos.EsMaestroGlobal.Returns(false);
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(["Core.Associates.Create"]);
        Archivo(Fila("1010101010", estado: "RETIRADO", tasa: "9"), Fila("2020202020"));

        var r = await Importar(ModoDeImportacion.Apply, SiYaExiste.Conservar);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var hoja = r.Value.Sheets.Single();
        (hoja.Created, hoja.Updated, hoja.Unchanged).Should().Be((1, 0, 1));
        var existente = await _db.Associates.Include(a => a.Person).SingleAsync(a => a.Person!.TaxId == "1010101010");
        existente.Status.Should().Be("A");
        existente.ContributionRate.Should().Be(5m);
    }

    [Fact]
    public async Task Sin_permiso_de_actualizar_un_asociado_existente_es_error()
    {
        await Aplicar(Fila("1010101010"));
        _permisos.EsMaestroGlobal.Returns(false);
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(["Core.Associates.Create"]);
        Archivo(Fila("1010101010", grupo: "ZONA2"));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Errors.Should().ContainSingle(e => e.Code == ImportErrors.CellPermissionRequired && e.Column == P.Documento, Errores(r.Value));
    }
}
