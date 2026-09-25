using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Imports;

/// <summary>
/// Feature 012, T106 (T49; contracts/plantillas.md §0.3–§0.5): la infraestructura común de importación. Se ejercita
/// <see cref="EjecutorDeImportacion"/> con una plantilla de prueba sobre <c>COR_Banks</c> (código y nombre) y un lector
/// sustituto que entrega las hojas ya leídas: lo que se prueba es lo común —hojas, encabezados, conversión de celdas,
/// errores con hoja, fila y columna, revisión sin guardar, todo o nada, motivo, permisos—, no las reglas de un catálogo.
/// </summary>
public class ImportacionComunTests
{
    private const string Codigo = "codigo";
    private const string Nombre = "nombre";
    private const string Tasa = "tasa";
    private const string Pais = "pais";
    private const string Especial = "especial";
    private const string Vigencia = "vigencia";

    private static readonly HojaDePlantilla HojaDeBancos = new("Bancos",
    [
        new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: 10),
        new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 20),
        new(Tasa, TipoDeValor.Porcentaje),
        new(Pais, TipoDeValor.Codigo),
        new(Especial, TipoDeValor.Texto, Permiso: "Core.Banks.Special"),
        new(Vigencia, TipoDeValor.Fecha),
    ]);

    private static readonly HojaDePlantilla HojaDeNotas = new("Notas", [new("nota", TipoDeValor.Texto)], Obligatoria: false);

    private static readonly DefinicionDePlantilla DeUnaHoja = new("test.banks", "Bancos", "Test",
        [HojaDeBancos with { Nombre = "Datos" }]);

    private static readonly DefinicionDePlantilla DeDosHojas = new("test.banks2", "Bancos y notas", "Test",
        [HojaDeBancos, HojaDeNotas]);

    private sealed record ImportarBancos(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
        : IComandoDeImportacion
    {
        public Guid OperationKey { get; init; } = Guid.NewGuid();
    }

    private sealed class Escenario
    {
        public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
        public ITabularFileReader Lector { get; } = Substitute.For<ITabularFileReader>();
        public ICurrentUserPermissions Permisos { get; } = Substitute.For<ICurrentUserPermissions>();
        public IAuditService Auditoria { get; } = Substitute.For<IAuditService>();
        public List<string> Hojas { get; } = [];
        private readonly Dictionary<string, TablaLeida> _tablas = new(StringComparer.OrdinalIgnoreCase);

        public Escenario(params string[] permisos)
        {
            Permisos.EsMaestroGlobal.Returns(false);
            Permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<string>>(permisos));
            Lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(Hojas.ToList())));
            Lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var hoja = ci.ArgAt<string?>(2) ?? Hojas.FirstOrDefault() ?? "Datos";
                    return Task.FromResult(_tablas.TryGetValue(hoja, out var t)
                        ? Result.Success(t)
                        : Result.Failure<TablaLeida>(ArchivosTabulares.HojaFaltante(hoja)));
                });
        }

        public void Hoja(string nombre, string[] encabezados, params string?[][] filas)
        {
            Hojas.Add(nombre);
            _tablas[nombre] = new TablaLeida(encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");
        }

        public void Banco(string codigo, string nombre, decimal tasa = 0m)
        {
            Db.Banks.Add(new Bank { LegacyCode = codigo, Name = nombre, FinancialTaxRate = tasa, CreatedBy = "test" });
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
        }

        public EjecutorDeImportacion Ejecutor()
        {
            var servicios = new ServiceCollection().AddSingleton(Auditoria).BuildServiceProvider();
            return new EjecutorDeImportacion(Db, Lector, Permisos, servicios);
        }

        public Task<Result<ImportResultDto>> Correr(DefinicionDePlantilla plantilla, ModoDeImportacion? modo, string motivo = "") =>
            Ejecutor().EjecutarAsync(plantilla, new ImportarBancos(modo, new ArchivoDeImportacion("bancos.xlsx", [1, 2, 3]), motivo),
                (ctx, ct) => ProcesarBancos(Db, ctx, ct), CancellationToken.None);
    }

    /// <summary>Una plantilla mínima con las mismas piezas que usarán las reales.</summary>
    private static async Task ProcesarBancos(IApplicationDbContext db, ContextoDeImportacion ctx, CancellationToken ct)
    {
        var existentes = await db.Banks.ToListAsync(ct);
        var porCodigo = CatalogoCitado<Bank>.Desde(existentes, b => b, b => b.LegacyCode);
        var paises = new CatalogoCitado<string>().Agregar("CO", "CO").Agregar("Colombia", "CO");
        var hoja = ctx.Hojas[0];

        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(Codigo);
            var nombre = fila.Texto(Nombre);
            var tasa = fila.Porcentaje(Tasa) ?? 0m;
            fila.Referencia(Pais, paises, "un país", "en Maestros → Países");
            if (fila.Fecha(Vigencia) is not null) ctx.PedirMotivo();
            if (!hoja.LlaveUnica(fila, codigo, Codigo) || fila.TieneErrores || codigo is null || nombre is null) continue;

            if (porCodigo.Buscar(codigo, out var banco))
            {
                var campos = new List<CampoCambiadoDto>();
                if (banco.Name != nombre) campos.Add(new(Nombre, banco.Name, nombre));
                if (banco.FinancialTaxRate != tasa) campos.Add(new(Tasa, banco.FinancialTaxRate.ToString(), tasa.ToString()));
                banco.Name = nombre;
                banco.FinancialTaxRate = tasa;
                ctx.Registrar(fila, codigo, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
            }
            else
            {
                db.Banks.Add(new Bank { LegacyCode = codigo, Name = nombre, FinancialTaxRate = tasa, CreatedBy = "import" });
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create, [new(Nombre, null, nombre)]);
            }
        }
    }

    private static readonly string[] Encabezados = [Codigo, Nombre, Tasa, Pais, Especial, Vigencia];

    private static string?[] Fila(string? codigo, string? nombre, string? tasa = null, string? pais = null, string? especial = null, string? vigencia = null) =>
        [codigo, nombre, tasa, pais, especial, vigencia];

    // ------------------------------------------------------------ ErrorDeFila --

    [Fact]
    public void ErrorDeFila_lleva_la_hoja_y_sin_hoja_responde_lo_mismo_que_la_009()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var sinHoja = JsonSerializer.Serialize(new ErrorDeFila(3, "cuenta", "X", "m"), opciones);
        sinHoja.Should().Be("{\"row\":3,\"column\":\"cuenta\",\"code\":\"X\",\"message\":\"m\"}");

        var conHoja = JsonSerializer.Serialize(new ErrorDeFila(3, "codigo", "X", "m", "Productos"), opciones);
        conHoja.Should().Contain("\"sheet\":\"Productos\"");
    }

    // ----------------------------------------------------------------- modo --

    [Fact]
    public async Task Sin_modo_responde_Import_ModeRequired_sin_leer_el_archivo()
    {
        var e = new Escenario();
        var r = await e.Correr(DeUnaHoja, null);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ImportErrors.ModeRequiredCode);
        await e.Lector.DidNotReceiveWithAnyArgs().LeerHojaAsync(default!, default!, default, default, default);
    }

    // ---------------------------------------------------------------- hojas --

    [Fact]
    public async Task Lee_cada_hoja_por_su_nombre_y_los_errores_llevan_la_hoja()
    {
        var e = new Escenario();
        e.Hoja("Notas", ["nota"], ["una"]);
        e.Hoja("Bancos", Encabezados, Fila("B01", null));

        var r = await e.Correr(DeDosHojas, ModoDeImportacion.Review);

        r.IsSuccess.Should().BeTrue();
        await e.Lector.Received().LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Is<string?>("Bancos"), 1, Arg.Any<CancellationToken>());
        await e.Lector.Received().LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Is<string?>("Notas"), 1, Arg.Any<CancellationToken>());
        r.Value.Sheets.Select(s => s.Sheet).Should().Equal("Bancos", "Notas");
        var error = r.Value.Errors.Should().ContainSingle().Subject;
        error.Should().Be(new ErrorDeFila(2, Nombre, ImportErrors.CellRequired, error.Message, "Bancos"));
    }

    [Fact]
    public async Task Una_hoja_obligatoria_que_falta_es_Archivo_HojaFaltante_y_una_opcional_no()
    {
        var e = new Escenario();
        e.Hoja("Notas", ["nota"], ["una"]);
        var r = await e.Correr(DeDosHojas, ModoDeImportacion.Review);
        r.Error.Code.Should().Be("Archivo.HojaFaltante");
        r.Error.Message.Should().Contain("Bancos");

        var sinNotas = new Escenario();
        sinNotas.Hoja("Bancos", Encabezados, Fila("B01", "Uno"));
        (await sinNotas.Correr(DeDosHojas, ModoDeImportacion.Review)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task En_una_plantilla_de_una_hoja_se_lee_la_primera_y_los_errores_no_llevan_hoja()
    {
        var e = new Escenario();
        e.Hoja("Hoja1", Encabezados, Fila("B01", null));

        var r = await e.Correr(DeUnaHoja, ModoDeImportacion.Review);

        await e.Lector.Received().LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Is<string?>(h => h == null), 1, Arg.Any<CancellationToken>());
        r.Value.Errors.Should().ContainSingle().Which.Sheet.Should().BeNull();
    }

    // ----------------------------------------------------------- encabezados --

    [Fact]
    public async Task Falta_una_columna_obligatoria_es_Archivo_ColumnaFaltante()
    {
        var e = new Escenario();
        e.Hoja("Bancos", [Codigo, Tasa], ["B01", "1"]);

        var r = await e.Correr(DeDosHojas, ModoDeImportacion.Review);

        r.Error.Code.Should().Be("Archivo.ColumnaFaltante");
        r.Error.Message.Should().Contain(Nombre).And.Contain("Bancos");
    }

    [Fact]
    public async Task Una_columna_desconocida_se_ignora_con_un_aviso()
    {
        var e = new Escenario();
        e.Hoja("Datos", [Codigo, Nombre, "nombreCorto", "resultado", "errores"], ["B01", "Uno", "U", null, null]);

        var r = await e.Correr(DeUnaHoja, ModoDeImportacion.Review);

        r.Value.Valid.Should().BeTrue();
        var aviso = r.Value.Warnings.Should().ContainSingle().Subject;
        aviso.Code.Should().Be(ImportErrors.ColumnUnknown);
        aviso.Column.Should().Be("nombreCorto");
        aviso.Row.Should().Be(1);
    }

    [Fact]
    public async Task Los_encabezados_se_comparan_sin_mayusculas_tildes_espacios_ni_guiones_bajos()
    {
        var e = new Escenario();
        e.Hoja("Datos", [" CÓDIGO ", "Nom_bre", "T a s a"], ["B01", "Uno", "19"]);

        var r = await e.Correr(DeUnaHoja, ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue();
        r.Value.Warnings.Should().BeEmpty();
        (await e.Db.Banks.SingleAsync(b => b.LegacyCode == "B01")).FinancialTaxRate.Should().Be(0.19m);
    }

    [Fact]
    public async Task Las_filas_vacias_se_ignoran()
    {
        var e = new Escenario();
        e.Hoja("Datos", Encabezados, Fila("B01", "Uno"), Fila(null, null), Fila(" ", ""), Fila("B02", "Dos"));

        var r = await e.Correr(DeUnaHoja, ModoDeImportacion.Review);

        r.Value.Sheets.Single().Rows.Should().Be(2);
        r.Value.Sheets.Single().Created.Should().Be(2);
    }

    // ---------------------------------------------------------------- celdas --

    [Fact]
    public async Task Cada_celda_invalida_deja_su_error_con_fila_de_Excel_y_columna()
    {
        var e = new Escenario("Core.Banks.Special");
        e.Hoja("Datos", Encabezados,
            Fila("B 01", "Uno"),                         // 2: código con espacio
            Fila("B02", null),                           // 3: nombre obligatorio
            Fila("B03", "Tres", tasa: "19,5.2"),         // 4: número mal escrito
            Fila("B04", "Cuatro", tasa: "1,23456"),      // 5: más de 4 decimales
            Fila("B05", "Cinco", pais: "XX"),            // 6: país inexistente
            Fila("B06", "Seis"),                         // 7
            Fila("b06", "Seis bis"),                     // 8: llave repetida (sin mayúsculas)
            Fila("B08", "Ocho", vigencia: "31/12/2026")); // 9: fecha mal escrita

        var r = await e.Correr(DeUnaHoja, ModoDeImportacion.Review);

        r.IsSuccess.Should().BeTrue("la revisión responde 200 aunque haya errores");
        r.Value.Valid.Should().BeFalse();
        r.Value.Errors.Select(x => (x.Row, x.Column, x.Code)).Should().Equal(
            (2, Codigo, ImportErrors.CellFormat),
            (3, Nombre, ImportErrors.CellRequired),
            (4, Tasa, ImportErrors.CellFormat),
            (5, Tasa, ImportErrors.CellFormat),
            (6, Pais, ImportErrors.CellNotFound),
            (8, Codigo, ImportErrors.RowDuplicate),
            (9, Vigencia, ImportErrors.CellFormat));
        r.Value.TotalErrors.Should().Be(7);
        r.Value.Errors.Single(x => x.Code == ImportErrors.RowDuplicate).Message.Should().Contain("fila 7");
        r.Value.Errors.Single(x => x.Code == ImportErrors.CellNotFound).Message.Should().Contain("XX").And.Contain("Países");
        r.Value.Sheets.Single().Created.Should().Be(1, "sólo B06 queda sin errores");
    }

    [Fact]
    public async Task Una_columna_con_permiso_propio_exige_el_permiso_si_trae_datos()
    {
        var e = new Escenario();
        e.Hoja("Datos", Encabezados, Fila("B01", "Uno", especial: "x"), Fila("B02", "Dos"));

        var r = await e.Correr(DeUnaHoja, ModoDeImportacion.Review);

        var error = r.Value.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(ImportErrors.CellPermissionRequired);
        (error.Row, error.Column).Should().Be((2, Especial));
        error.Message.Should().Contain("Core.Banks.Special");
    }

    [Fact]
    public async Task Una_hoja_con_permiso_propio_exige_el_permiso_si_trae_filas()
    {
        var conPermiso = new DefinicionDePlantilla("test.p", "P", "Test", [HojaDeBancos, HojaDeNotas with { Permiso = "Core.Notes.Manage" }]);
        var e = new Escenario();
        e.Hoja("Bancos", Encabezados, Fila("B01", "Uno"));
        e.Hoja("Notas", ["nota"], ["una"]);

        var r = await e.Correr(conPermiso, ModoDeImportacion.Review);

        var error = r.Value.Errors.Should().ContainSingle().Subject;
        (error.Code, error.Sheet, error.Row).Should().Be((ImportErrors.CellPermissionRequired, "Notas", 0));
    }

    [Fact]
    public async Task El_porcentaje_se_escribe_en_puntos_y_se_guarda_como_fraccion()
    {
        var e = new Escenario();
        e.Hoja("Datos", Encabezados, Fila("IVA", "IVA", tasa: "19"), Fila("ICA", "ICA", tasa: "0,966"));

        (await e.Correr(DeUnaHoja, ModoDeImportacion.Apply)).IsSuccess.Should().BeTrue();

        var tasas = await e.Db.Banks.ToDictionaryAsync(b => b.LegacyCode!, b => b.FinancialTaxRate);
        tasas["IVA"].Should().Be(0.19m);
        tasas["ICA"].Should().Be(0.00966m);
    }

    // ------------------------------------------------------- revisar y aplicar --

    [Fact]
    public async Task La_revision_corre_las_mismas_reglas_y_no_guarda_nada()
    {
        var e = new Escenario();
        e.Banco("B01", "Uno");
        e.Banco("B02", "Dos");
        e.Hoja("Datos", Encabezados, Fila("B01", "Uno"), Fila("B02", "Dos cambiado"), Fila("B03", "Tres"));

        var r = await e.Correr(DeUnaHoja, ModoDeImportacion.Review);

        r.Value.Valid.Should().BeTrue();
        r.Value.Applied.Should().BeFalse();
        r.Value.Mode.Should().Be(ModoDeImportacion.Review);
        r.Value.Sheets.Single().Should().Be(new ResumenDeHojaDto("Datos", 3, 1, 1, 1));
        r.Value.Changes.Select(c => (c.Key, c.Action)).Should().Equal(("B02", AccionDeImportacion.Update), ("B03", AccionDeImportacion.Create));
        r.Value.Changes[0].Fields.Should().ContainSingle().Which.Should().Be(new CampoCambiadoDto(Nombre, "Dos", "Dos cambiado"));
        r.Value.FileName.Should().Be("bancos.xlsx");
        r.Value.FileSha256.Should().HaveLength(64);

        e.Db.ChangeTracker.Entries().Should().OnlyContain(x => x.State == EntityState.Unchanged);
        e.Db.ChangeTracker.Clear();
        (await e.Db.Banks.CountAsync()).Should().Be(2);
        (await e.Db.Banks.SingleAsync(b => b.LegacyCode == "B02")).Name.Should().Be("Dos");
        await e.Auditoria.Received(1).LogAsync(Arg.Is<AuditLogCommand>(a => a.Action == EjecutorDeImportacion.EventoRevisada
            && a.Metadata!["Template"] == "test.banks" && a.Metadata["FileSha256"] == r.Value.FileSha256), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Aplicar_sin_errores_guarda_todo_y_es_idempotente_al_repetir_el_archivo()
    {
        var e = new Escenario();
        e.Banco("B01", "Uno");
        e.Hoja("Datos", Encabezados, Fila("B01", "Uno nuevo"), Fila("B02", "Dos"));

        var r = await e.Correr(DeUnaHoja, ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue();
        r.Value.Applied.Should().BeTrue();
        e.Db.ChangeTracker.Clear();
        (await e.Db.Banks.OrderBy(b => b.LegacyCode).Select(b => b.Name).ToListAsync()).Should().Equal("Uno nuevo", "Dos");

        var otraVez = await e.Correr(DeUnaHoja, ModoDeImportacion.Apply);
        otraVez.Value.Sheets.Single().Unchanged.Should().Be(2);
        (await e.Db.Banks.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Aplicar_con_un_error_no_guarda_nada_y_responde_Import_Invalid_con_el_mismo_cuerpo()
    {
        var e = new Escenario();
        e.Banco("B01", "Uno");
        e.Hoja("Datos", Encabezados, Fila("B01", "Uno cambiado"), Fila("B02", "Dos"), Fila("B03", "Tres", tasa: "abc"));

        var r = await e.Correr(DeUnaHoja, ModoDeImportacion.Apply);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        var cuerpo = ((ErrorConDatos)r.Error).Data.Should().BeOfType<ImportResultDto>().Subject;
        cuerpo.Valid.Should().BeFalse();
        cuerpo.Applied.Should().BeFalse();
        cuerpo.Errors.Should().ContainSingle().Which.Row.Should().Be(4);

        e.Db.ChangeTracker.Entries().Should().OnlyContain(x => x.State == EntityState.Unchanged);
        e.Db.ChangeTracker.Clear();
        (await e.Db.Banks.CountAsync()).Should().Be(1);
        (await e.Db.Banks.SingleAsync()).Name.Should().Be("Uno");
    }

    [Fact]
    public async Task Si_la_revision_pide_motivo_aplicar_sin_el_es_un_error_sin_fila_en_reason()
    {
        var e = new Escenario();
        e.Hoja("Datos", Encabezados, Fila("B01", "Uno", vigencia: "2026-10-01"));

        var revision = await e.Correr(DeUnaHoja, ModoDeImportacion.Review);
        revision.Value.RequiresReason.Should().BeTrue();
        revision.Value.Valid.Should().BeTrue();

        var sinMotivo = await e.Correr(DeUnaHoja, ModoDeImportacion.Apply);
        sinMotivo.Error.Code.Should().Be(ImportErrors.InvalidCode);
        var error = ((ImportResultDto)((ErrorConDatos)sinMotivo.Error).Data).Errors.Should().ContainSingle().Subject;
        (error.Row, error.Column, error.Code).Should().Be((0, ImportErrors.ColumnaDelMotivo, ImportErrors.CellRequired));
        (await e.Db.Banks.CountAsync()).Should().Be(0);

        var conMotivo = await e.Correr(DeUnaHoja, ModoDeImportacion.Apply, "Tarifa nueva de octubre");
        conMotivo.IsSuccess.Should().BeTrue();
        (await e.Db.Banks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Los_errores_devueltos_se_topan_en_mil_pero_se_cuentan_todos()
    {
        var e = new Escenario();
        var filas = Enumerable.Range(1, EjecutorDeImportacion.MaximoDeErrores + 5).Select(i => Fila($"B{i}", null)).ToArray();
        e.Hoja("Datos", Encabezados, filas);

        var r = await e.Correr(DeUnaHoja, ModoDeImportacion.Review);

        r.Value.Errors.Should().HaveCount(EjecutorDeImportacion.MaximoDeErrores);
        r.Value.TotalErrors.Should().Be(EjecutorDeImportacion.MaximoDeErrores + 5);
        r.Value.Rows.Should().HaveCount(EjecutorDeImportacion.MaximoDeErrores + 5);
        r.Value.Rows.Should().OnlyContain(f => f.Action == null && f.Errors.Count == 1);
    }

    [Fact]
    public void El_archivo_no_viaja_en_el_json_pero_su_huella_si()
    {
        var comando = new ImportarBancos(ModoDeImportacion.Review, new ArchivoDeImportacion("a.csv", [1, 2, 3]));
        var json = JsonSerializer.Serialize(comando, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().NotContain("contenido");
        json.Should().Contain(comando.File.Sha256);
        HuellaDeOperacion.Calcular("X", comando).Should().NotBe(
            HuellaDeOperacion.Calcular("X", comando with { File = new ArchivoDeImportacion("a.csv", [1, 2, 4]) }),
            "la misma clave con otro archivo es otro contenido");
    }

    // ------------------------------------------------------ lista de plantillas --

    [Fact]
    public void El_catalogo_tiene_las_dieciseis_plantillas_en_el_orden_de_carga()
    {
        CatalogoDePlantillas.Todas.Select(p => p.Numero).Should().Equal(Enumerable.Range(1, 16));
        CatalogoDePlantillas.Todas.Select(p => p.Clave).Should().OnlyHaveUniqueItems();
        CatalogoDePlantillas.Por(CatalogoDePlantillas.ProductosClave).Definicion.Hojas.Select(h => h.Nombre)
            .Should().Equal("Productos", "CodigosDeBarras", "Unidades", "ImpuestosAdicionales");
        CatalogoDePlantillas.Todas.Where(p => p.Definicion.EsDeUnaHoja).Should()
            .OnlyContain(p => p.Definicion.Hojas[0].Nombre == "Datos");
    }

    [Fact]
    public async Task La_lista_dice_que_puede_descargar_e_importar_quien_pregunta_y_que_se_importa_despues()
    {
        var permisos = Substitute.For<ICurrentUserPermissions>();
        permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<string>>(
            ["Inventory.Catalog.View", "Inventory.Catalog.Import", "Inventory.PointsOfSale.View", "Inventory.PointsOfSale.Manage"]));

        var r = await new ListImportTemplatesQueryHandler(permisos).Handle(new ListImportTemplatesQuery(), CancellationToken.None);

        var lista = r.Value;
        lista.Should().HaveCount(16);
        var productos = lista.Single(p => p.Key == CatalogoDePlantillas.ProductosClave);
        (productos.CanDownload, productos.CanImport, productos.Note).Should().Be((true, true, null));
        var impuestos = lista.Single(p => p.Number == 1);
        (impuestos.CanDownload, impuestos.CanImport).Should().Be((false, false));
        var puntos = lista.Single(p => p.Number == 10);
        (puntos.CanDownload, puntos.CanImport).Should().Be((true, false));
        puntos.Note.Should().Be("Se importa con I3.");
        var matriz = lista.Single(p => p.Number == 16);
        matriz.CanDownload.Should().BeFalse();
        matriz.Note.Should().Contain("I2");
    }
}
