using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.RestrictiveLists;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using P = IngenIA365ERP.Application.Compliance.RestrictiveLists.PlantillaDeListasRestrictivas;

namespace IngenIA365ERP.Application.Tests.Compliance;

/// <summary>
/// T0033 (feature 013; T70, F-18; data-model parte 3 §4.8; contracts/plantillas.md §4, §0.5, §0.8): la plantilla
/// <c>compliance.restrictive-lists</c> sobre el ejecutor común. <c>review</c> no guarda; un documento normalizado repetido es
/// <c>Import.Row.Duplicate</c>; cada carga es nueva, con <c>Version</c> consecutiva por lista, y la anterior queda <c>Replaced</c> por
/// derivación; la auditoría es un evento por carga (conteo y SHA-256), no por fila; y <c>CARGA-PROPIA</c> lee la carga vigente.
/// </summary>
public class ImportRestrictiveListCommandTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 10, 6);
    private static readonly string[] Encabezados =
        [P.Lista, P.Fuente, P.PublicadaEl, P.VigenteDesde, P.TipoDeEntidad, P.TipoDeDocumento, P.NumeroDeDocumento, P.NombreCompleto, P.Alias, P.Programa, P.Notas];

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IAuditService _auditoria = Substitute.For<IAuditService>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly List<AuditLogCommand> _eventos = [];
    private TablaLeida _tabla = new([], [], "xlsx");

    public ImportRestrictiveListCommandTests()
    {
        _permisos.EsMaestroGlobal.Returns(true);
        _reloj.HoyLocal.Returns(Hoy);
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(_tabla)));
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(), "Oficial de cumplimiento",
            null, ExecutionChannel.App, "POST /api/compliance/restrictive-lists/import", null, null));
        _auditoria.LogAsync(Arg.Do<AuditLogCommand>(_eventos.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _db.Users.Add(new Domain.Entities.Security.User { Id = 7, Username = "oficial", Email = "oficial@coop.test" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private void Archivo(params string?[][] filas) =>
        _tabla = new TablaLeida(Encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private static string?[] Fila(string? documento, string nombre, string lista = "OFAC", string desde = "2026-10-06", string? alias = null, string tipo = "Person") =>
        [lista, "https://sanctionssearch.ofac.treas.gov", "2026-10-01", desde, tipo, documento is null ? null : "CC", documento, nombre, alias, "SDNTK", null];

    private async Task<Result<ImportResultDto>> Importar(ModoDeImportacion modo, string archivo = "ofac.xlsx")
    {
        var servicios = new ServiceCollection().AddSingleton(_auditoria).BuildServiceProvider();
        var ejecutor = new EjecutorDeImportacion(_db, _lector, _permisos, servicios);
        var r = await new ImportRestrictiveListCommandHandler(_db, ejecutor, _actor)
            .Handle(new ImportRestrictiveListCommand(modo, new ArchivoDeImportacion(archivo, [1, 2, 3, (byte)archivo.Length])), CancellationToken.None);
        _db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task La_plantilla_es_de_plataforma_de_una_hoja_y_del_modulo_Compliance()
    {
        var plantilla = CatalogoDePlantillasDePlataforma.Por(P.Clave);

        plantilla.Definicion.Modulo.Should().Be(ModuloDeAuditoria.Compliance);
        plantilla.Definicion.EsDeUnaHoja.Should().BeTrue();
        plantilla.PermisoDeDescarga.Should().Be("Compliance.RestrictiveLists.View");
        plantilla.PermisoDeImportacion.Should().Be("Compliance.RestrictiveLists.Load");
        plantilla.RutaBase.Should().Be("/api/compliance/restrictive-lists");
        CatalogoDePlantillasDePlataforma.Por("saas.national-values").PermisoDeImportacion.Should().BeNull("lo protege el maestro");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task La_revision_cuenta_y_no_guarda()
    {
        Archivo(Fila("79.123.456", "Juan Pérez Gómez", alias: "Juancho; El Mono"), Fila(null, "Inversiones Oscuras SAS", tipo: "2"));

        var r = await Importar(ModoDeImportacion.Review);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Valid.Should().BeTrue(string.Join("; ", r.Value.Errors.Select(e => $"{e.Code} {e.Message}")));
        r.Value.Sheets.Single().Created.Should().Be(2);
        (await _db.RestrictiveListLoads.CountAsync()).Should().Be(0);
        (await _db.RestrictiveListEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_documento_normalizado_repetido_es_Import_Row_Duplicate_y_apply_no_guarda_nada()
    {
        Archivo(Fila("79.123.456", "Juan Pérez"), Fila("79123456", "Juan Perez Gomez"));

        var revision = await Importar(ModoDeImportacion.Review);
        var aplicada = await Importar(ModoDeImportacion.Apply);

        revision.Value.Errors.Should().ContainSingle(e => e.Code == ImportErrors.RowDuplicate && e.Row == 3);
        aplicada.IsFailure.Should().BeTrue();
        aplicada.Error.Code.Should().Be(ImportErrors.InvalidCode);
        (await _db.RestrictiveListLoads.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Cada_carga_es_nueva_con_version_consecutiva_y_la_anterior_queda_Replaced()
    {
        Archivo(Fila("79.123.456", "Juan Pérez Gómez", desde: "2026-10-01"));
        (await Importar(ModoDeImportacion.Apply, "ofac-1.xlsx")).IsSuccess.Should().BeTrue();
        Archivo(Fila("79.123.456", "Juan Pérez Gómez", desde: "2026-10-05"), Fila("123", "Pedro Ruiz", desde: "2026-10-05"));

        var segunda = await Importar(ModoDeImportacion.Apply, "ofac-2.xlsx");

        segunda.IsSuccess.Should().BeTrue(segunda.IsFailure ? segunda.Error.Message : null);
        var cargas = await _db.RestrictiveListLoads.OrderBy(c => c.Version).ToListAsync();
        cargas.Select(c => c.Version).Should().Equal(1, 2);
        cargas[1].EntryCount.Should().Be(2);
        cargas[1].LoadedByUserId.Should().Be(7);
        cargas[1].FileSha256.Should().HaveLength(64);
        (await _db.RestrictiveListEntries.CountAsync(e => e.LoadId == cargas[0].Id)).Should().Be(1, "las entradas de la anterior siguen ahí");

        var lista = await new ListRestrictiveListLoadsQueryHandler(_db, _reloj).Handle(new ListRestrictiveListLoadsQuery(), CancellationToken.None);
        lista.Value.Single(c => c.Version == 2).Status.Should().Be(EstadosDeCargaDeLista.Active);
        lista.Value.Single(c => c.Version == 1).Status.Should().Be(EstadosDeCargaDeLista.Replaced);

        var vigente = await new CargaDeListasRestrictivasEnBase(_db).LeerVigenteAsync(Hoy, CancellationToken.None);
        vigente.Versiones.Should().ContainSingle().Which.Version.Should().Be(2);
        vigente.Entradas.Should().HaveCount(2).And.Contain(e => e.DocumentNumberNormalized == "79123456" && e.NameNormalized.Length > 0);
        (await new CargaDeListasRestrictivasEnBase(_db).LeerVigenteAsync(new DateOnly(2026, 10, 3), CancellationToken.None))
            .Versiones.Single().Version.Should().Be(1, "el 3 de octubre regía la primera");
    }

    [Fact]
    public async Task Una_carga_no_puede_empezar_antes_que_la_vigente_de_su_lista()
    {
        Archivo(Fila("1", "Uno", desde: "2026-10-05"));
        await Importar(ModoDeImportacion.Apply);
        Archivo(Fila("2", "Dos", desde: "2026-10-01"));

        var r = await Importar(ModoDeImportacion.Apply);

        r.IsFailure.Should().BeTrue();
        ((ImportResultDto)((ErrorConDatos)r.Error).Data).Errors.Should().Contain(e => e.Code == ErroresDeListasRestrictivas.CodigoVigenciaAnterior);
    }

    [Fact]
    public async Task El_encabezado_de_la_carga_es_el_mismo_en_todas_las_filas()
    {
        Archivo(Fila("1", "Uno"), Fila("2", "Dos", lista: "ONU"));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Errors.Should().ContainSingle(e => e.Code == ErroresDeListasRestrictivas.CodigoEncabezadoDistinto && e.Row == 3);
    }

    [Fact]
    public async Task Se_audita_un_evento_por_carga_con_conteo_y_SHA_256_no_uno_por_fila()
    {
        Archivo(Fila("1", "Uno"), Fila("2", "Dos"), Fila("3", "Tres"));

        var r = await Importar(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue();
        var aplicada = _eventos.Should().ContainSingle(e => e.Action == EjecutorDeImportacion.EventoAplicada).Subject;
        aplicada.Module.Should().Be(ModuloDeAuditoria.Compliance);
        aplicada.Metadata!["FileSha256"].Should().Be(r.Value.FileSha256);
        aplicada.Metadata["Template"].Should().Be(P.Clave);
        System.Text.Json.JsonSerializer.Serialize(aplicada.NewValues).Should().Contain("\"Created\":3");
        _eventos.Should().HaveCount(1, "las entradas no dejan un evento cada una");
        typeof(Domain.Entities.Compliance.RestrictiveListEntry).IsDefined(typeof(Domain.Common.SinDiffDeAuditoriaAttribute), false).Should().BeTrue();
    }

    [Fact]
    public async Task Las_entradas_se_consultan_por_carga_y_una_carga_inexistente_es_NotFound()
    {
        Archivo(Fila("79.123.456", "Juan Pérez Gómez"), Fila(null, "Inversiones Oscuras SAS", tipo: "Organization"));
        await Importar(ModoDeImportacion.Apply);
        var carga = await _db.RestrictiveListLoads.SingleAsync();
        var handler = new ListRestrictiveListEntriesQueryHandler(_db);

        var todas = await handler.Handle(new ListRestrictiveListEntriesQuery(carga.PublicId), CancellationToken.None);
        var buscada = await handler.Handle(new ListRestrictiveListEntriesQuery(carga.PublicId, "79123456"), CancellationToken.None);
        var ninguna = await handler.Handle(new ListRestrictiveListEntriesQuery(Guid.NewGuid()), CancellationToken.None);

        todas.Value.TotalCount.Should().Be(2);
        buscada.Value.Items.Should().ContainSingle().Which.EntityKind.Should().Be(RestrictiveListEntityKind.Person);
        ninguna.Error.Code.Should().Be(ErroresDeListasRestrictivas.CodigoNoEncontrada);
    }
}
