using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using P = IngenIA365ERP.Application.Core.Taxes.PlantillaDeImpuestos;

namespace IngenIA365ERP.Application.Tests.Core.Taxes;

/// <summary>
/// Feature 012, T105 (T22, T49; contracts/plantillas.md §1 y §0.5): la plantilla 1 —hojas <c>Conceptos</c>,
/// <c>Impuestos</c> y <c>Tarifas</c>— con la mecánica común: <c>review</c> no guarda y cuenta creados, actualizados y
/// sin cambio; <c>apply</c> es todo o nada (422 <c>Import.Invalid</c> con el mismo cuerpo); los errores llevan hoja,
/// fila de Excel y columna; el porcentaje en puntos se guarda como fracción; una vigencia nueva exige motivo; y las
/// reglas y códigos son los del alta unitaria.
/// </summary>
public class ImportTaxCatalogCommandTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 3, 16);

    private static readonly string[] EncabezadosConceptos = [P.Codigo, P.Nombre, P.Activo];
    private static readonly string[] EncabezadosImpuestos = [P.Codigo, P.Nombre, P.Clase, P.FormaDeCalculo, P.CalculadoSobre, P.EsRetencion, P.CodigoDian, P.Activo];
    private static readonly string[] EncabezadosTarifas =
    [
        P.Codigo, P.Impuesto, P.Nombre, P.TarifaPorcentaje, P.ValorPorUnidad, P.ConceptoRetencion, P.Municipio, P.Actividad,
        P.BaseMinimaUvt, P.BaseMinimaPesos, P.AplicaA, P.Prioridad, P.SujetoTipoPersona, P.SujetoDeclarante, P.SujetoAutorretenedor,
        P.VigenteDesde, P.VigenteHasta, P.Norma, P.Notas,
    ];

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly Dictionary<string, TablaLeida> _hojas = new(StringComparer.OrdinalIgnoreCase);

    public ImportTaxCatalogCommandTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _permisos.EsMaestroGlobal.Returns(true);
        _lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(_hojas.Keys.ToList())));
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(_hojas.TryGetValue(ci.ArgAt<string?>(2) ?? string.Empty, out var t)
                ? Result.Success(t)
                : Result.Failure<TablaLeida>(ArchivosTabulares.HojaFaltante(ci.ArgAt<string?>(2) ?? "?"))));
    }

    public void Dispose() => _db.Dispose();

    private void Hoja(string nombre, string[] encabezados, params string?[][] filas) =>
        _hojas[nombre] = new TablaLeida(encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private void Libro(string?[][] conceptos, string?[][] impuestos, string?[][] tarifas)
    {
        Hoja(P.HojaConceptos, EncabezadosConceptos, conceptos);
        Hoja(P.HojaImpuestos, EncabezadosImpuestos, impuestos);
        Hoja(P.HojaTarifas, EncabezadosTarifas, tarifas);
    }

    private static string?[] Tarifa(string codigo, string impuesto, string? porcentaje, string desde = "2026-01-01", string? concepto = null,
        string? porUnidad = null, string? declarante = null, string? hasta = null, string? minimoUvt = null, string nombre = "Tarifa", string? prioridad = null) =>
        [codigo, impuesto, nombre, porcentaje, porUnidad, concepto, null, null, minimoUvt, null, "Both", prioridad, null, declarante, null, desde, hasta, "ET art. X", null];

    private async Task<Result<ImportResultDto>> ImportarAsync(ModoDeImportacion modo, string motivo = "")
    {
        var servicios = new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider();
        var ejecutor = new EjecutorDeImportacion(_db, _lector, _permisos, servicios);
        var r = await new ImportTaxCatalogCommandHandler(_db, ejecutor, _reloj)
            .Handle(new ImportTaxCatalogCommand(modo, new ArchivoDeImportacion("impuestos.xlsx", [1, 2, 3]), motivo), default);
        _db.ChangeTracker.Clear();
        return r;
    }

    private static readonly string?[][] LibroBasicoConceptos = [["COMPRAS", "Compras generales", "sí"]];

    private static readonly string?[][] LibroBasicoImpuestos =
    [
        ["IVA", "IVA", "Iva", "PercentOfBase", null, null, "01", null],
        ["RETEIVA", "Retención de IVA", "ReteIva", "sobre otro impuesto", "IVA", null, "05", null],
        ["RTF", "ReteFuente", "ReteFuente", "PercentOfBase", null, null, "06", null],
    ];

    private static readonly string?[][] LibroBasicoTarifas =
    [
        Tarifa("IVA19", "IVA", "19", "2017-01-01"),
        Tarifa("RIVA15", "RETEIVA", "15"),
        Tarifa("RF966", "RTF", "0,966", concepto: "COMPRAS", declarante: "sí", minimoUvt: "27"),
    ];

    // --------------------------------------------------------------------------------------------- la plantilla --

    [Fact]
    public void La_plantilla_1_declara_sus_tres_hojas_con_columnas_y_es_la_de_CatalogoDePlantillas()
    {
        P.Definicion.Hojas.Select(h => h.Nombre).Should().Equal(P.HojaConceptos, P.HojaImpuestos, P.HojaTarifas);
        P.Definicion.Hojas.Should().OnlyContain(h => h.Columnas.Count > 0);
        CatalogoDePlantillas.Por("core.taxes").Definicion.Should().BeSameAs(P.Definicion);
        P.Definicion.Hoja(P.HojaTarifas)!.Columna(P.TarifaPorcentaje)!.Tipo.Should().Be(TipoDeValor.Porcentaje);
    }

    // ------------------------------------------------------------------------------------------------- revisión --

    [Fact]
    public async Task Revisar_no_guarda_y_cuenta_lo_que_crearia()
    {
        Libro(LibroBasicoConceptos, LibroBasicoImpuestos, LibroBasicoTarifas);

        var r = await ImportarAsync(ModoDeImportacion.Review);

        r.IsSuccess.Should().BeTrue();
        r.Value.Valid.Should().BeTrue(string.Join(" | ", r.Value.Errors.Select(e => $"{e.Sheet} {e.Row} {e.Column}: {e.Message}")));
        r.Value.Applied.Should().BeFalse();
        r.Value.Template.Should().Be("core.taxes");
        r.Value.Sheets.Should().BeEquivalentTo(new[]
        {
            new ResumenDeHojaDto(P.HojaConceptos, 1, 1, 0, 0),
            new ResumenDeHojaDto(P.HojaImpuestos, 3, 3, 0, 0),
            new ResumenDeHojaDto(P.HojaTarifas, 3, 3, 0, 0),
        });
        r.Value.RequiresReason.Should().BeTrue("el archivo crea vigencias de tarifa");
        (await _db.TaxDefinitions.CountAsync()).Should().Be(0, "la revisión no guarda nada (FR-030)");
        (await _db.TaxRates.CountAsync()).Should().Be(0);
        (await _db.WithholdingConcepts.CountAsync()).Should().Be(0);
    }

    // ------------------------------------------------------------------------------------------------ aplicación --

    [Fact]
    public async Task Aplicar_guarda_el_porcentaje_como_fraccion_y_resuelve_las_referencias_del_mismo_libro()
    {
        Libro(LibroBasicoConceptos, LibroBasicoImpuestos, LibroBasicoTarifas);

        var r = await ImportarAsync(ModoDeImportacion.Apply, "Carga inicial de la contadora");

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Applied.Should().BeTrue();
        var tarifas = await _db.TaxRates.Include(t => t.TaxDefinition).Include(t => t.WithholdingConcept).ToListAsync();
        tarifas.Single(t => t.Code == "IVA19").Rate.Should().Be(0.19m);
        tarifas.Single(t => t.Code == "RF966").Rate.Should().Be(0.00966m, "0,966 puntos = 9,66 por mil");
        tarifas.Single(t => t.Code == "RF966").WithholdingConcept!.Code.Should().Be("COMPRAS");
        tarifas.Single(t => t.Code == "RF966").MinimumBaseUvt.Should().Be(27m);
        tarifas.Single(t => t.Code == "RF966").SubjectIsIncomeTaxFiler.Should().BeTrue();
        tarifas.Should().OnlyContain(t => !t.ReviewPending, "lo que carga la cooperativa no queda pendiente de validar");
        var reteiva = await _db.TaxDefinitions.Include(d => d.TaxedOnDefinition).SingleAsync(d => d.Code == "RETEIVA");
        reteiva.TaxedOnDefinition!.Code.Should().Be("IVA");
        reteiva.IsWithholding.Should().BeTrue();
    }

    [Fact]
    public async Task Crear_vigencias_sin_motivo_no_guarda_nada()
    {
        Libro(LibroBasicoConceptos, LibroBasicoImpuestos, LibroBasicoTarifas);

        var r = await ImportarAsync(ModoDeImportacion.Apply);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Import.Invalid");
        var cuerpo = (ImportResultDto)((ErrorConDatos)r.Error).Data;
        cuerpo.Errors.Should().ContainSingle(e => e.Row == 0 && e.Column == "reason" && e.Code == "Import.Cell.Required");
        (await _db.TaxDefinitions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Volver_a_subir_el_mismo_libro_queda_sin_cambio_y_no_pide_motivo()
    {
        Libro(LibroBasicoConceptos, LibroBasicoImpuestos, LibroBasicoTarifas);
        (await ImportarAsync(ModoDeImportacion.Apply, "Carga inicial")).IsSuccess.Should().BeTrue();

        var otra = await ImportarAsync(ModoDeImportacion.Apply);

        otra.IsSuccess.Should().BeTrue(otra.IsFailure ? otra.Error.Message : null);
        otra.Value.RequiresReason.Should().BeFalse();
        otra.Value.Sheets.Should().OnlyContain(h => h.Created == 0 && h.Updated == 0 && h.Unchanged == h.Rows);
        (await _db.TaxRates.CountAsync()).Should().Be(3, "el archivo es idempotente");
    }

    [Fact]
    public async Task Un_error_en_una_fila_no_deja_guardar_nada_y_viaja_con_hoja_fila_y_columna()
    {
        Libro(LibroBasicoConceptos, LibroBasicoImpuestos,
        [
            Tarifa("IVA19", "IVA", "19"),
            Tarifa("RFX", "RTF", "2,5", concepto: "NOEXISTE"),
            Tarifa("IVA19", "IVA", "19"),
        ]);

        var r = await ImportarAsync(ModoDeImportacion.Apply, "Carga");

        r.Error.Code.Should().Be("Import.Invalid");
        var cuerpo = (ImportResultDto)((ErrorConDatos)r.Error).Data;
        cuerpo.Errors.Should().Contain(new ErrorDeFila(3, P.ConceptoRetencion, "Import.Cell.NotFound", cuerpo.Errors.Single(e => e.Row == 3).Message, P.HojaTarifas));
        cuerpo.Errors.Should().Contain(e => e.Row == 4 && e.Code == "Import.Row.Duplicate" && e.Sheet == P.HojaTarifas);
        (await _db.TaxDefinitions.CountAsync()).Should().Be(0, "todo o nada");
        (await _db.WithholdingConcepts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Las_reglas_y_los_codigos_son_los_del_alta_unitaria()
    {
        Libro(LibroBasicoConceptos, LibroBasicoImpuestos,
        [
            Tarifa("RF25", "RTF", "2,5"),                                               // ReteFuente sin concepto
            Tarifa("IVA19", "IVA", "19", "2017-01-01"),
            Tarifa("IVA19", "IVA", "20", "2020-01-01"),                                  // se cruza con la anterior
            Tarifa("RFA", "RTF", "2,5", concepto: "COMPRAS", declarante: "sí"),
            Tarifa("RFB", "RTF", "3", concepto: "COMPRAS", declarante: "sí"),           // empate evidente con RFA
        ]);

        var r = await ImportarAsync(ModoDeImportacion.Review);

        r.Value.Valid.Should().BeFalse();
        r.Value.Errors.Should().Contain(e => e.Row == 2 && e.Code == "Validation.Invalid" && e.Column == P.ConceptoRetencion);
        r.Value.Errors.Should().Contain(e => e.Code == "Core.TaxRate.Overlaps" && e.Sheet == P.HojaTarifas);
        r.Value.Errors.Should().Contain(e => e.Code == "Core.TaxRate.Ambiguous");
    }

    [Fact]
    public async Task Codigo_de_concepto_o_de_impuesto_existente_actualiza_y_la_clase_no_cambia()
    {
        Libro(LibroBasicoConceptos, LibroBasicoImpuestos, []);
        (await ImportarAsync(ModoDeImportacion.Apply)).IsSuccess.Should().BeTrue("sin tarifas no hay vigencias: no pide motivo");

        Libro([["COMPRAS", "Compras de bienes", "sí"]], [["IVA", "IVA", "Inc", "PercentOfBase", null, null, "01", null]], []);
        var r = await ImportarAsync(ModoDeImportacion.Review);

        r.Value.Sheets.Single(s => s.Sheet == P.HojaConceptos).Updated.Should().Be(1);
        r.Value.Changes.Should().Contain(c => c.Key == "COMPRAS" && c.Fields.Any(f => f.Column == P.Nombre && f.After == "Compras de bienes"));
        r.Value.Errors.Should().ContainSingle(e => e.Code == "Core.Tax.Immutable" && e.Column == P.Clase);
    }

    [Fact]
    public async Task Una_tarifa_en_vigencia_solo_cambia_nombre_notas_y_fin_y_cerrar_pide_motivo()
    {
        Libro(LibroBasicoConceptos, LibroBasicoImpuestos, [Tarifa("IVA19", "IVA", "19", "2017-01-01")]);
        (await ImportarAsync(ModoDeImportacion.Apply, "Carga")).IsSuccess.Should().BeTrue();

        Libro(LibroBasicoConceptos, LibroBasicoImpuestos, [Tarifa("IVA19", "IVA", "18", "2017-01-01")]);
        var otroValor = await ImportarAsync(ModoDeImportacion.Review);
        otroValor.Value.Errors.Should().ContainSingle(e => e.Code == "Core.TaxRate.InEffect");

        Libro(LibroBasicoConceptos, LibroBasicoImpuestos,
        [
            Tarifa("IVA19", "IVA", "19", "2017-01-01", hasta: "2026-12-31", nombre: "IVA general"),
            Tarifa("IVA19", "IVA", "20", "2027-01-01", nombre: "IVA general"),
        ]);
        var cierre = await ImportarAsync(ModoDeImportacion.Review);
        cierre.Value.Valid.Should().BeTrue(string.Join(" | ", cierre.Value.Errors.Select(e => e.Message)));
        cierre.Value.RequiresReason.Should().BeTrue();
        cierre.Value.Sheets.Single(s => s.Sheet == P.HojaTarifas).Should().Be(new ResumenDeHojaDto(P.HojaTarifas, 2, 1, 1, 0));

        (await ImportarAsync(ModoDeImportacion.Apply, "Cambio de tarifa")).IsSuccess.Should().BeTrue();
        var vigencias = await _db.TaxRates.Where(t => t.Code == "IVA19").OrderBy(t => t.ValidFrom).ToListAsync();
        vigencias.Should().HaveCount(2);
        vigencias[0].ValidTo.Should().Be(new DateOnly(2026, 12, 31));
        vigencias[0].Name.Should().Be("IVA general");
        vigencias[1].Rate.Should().Be(0.20m);
    }

    [Fact]
    public async Task Un_concepto_en_uso_no_se_inactiva_por_plantilla()
    {
        Libro(LibroBasicoConceptos, LibroBasicoImpuestos, LibroBasicoTarifas);
        (await ImportarAsync(ModoDeImportacion.Apply, "Carga")).IsSuccess.Should().BeTrue();

        Libro([["COMPRAS", "Compras generales", "no"]], [], []);
        var r = await ImportarAsync(ModoDeImportacion.Review);

        r.Value.Errors.Should().ContainSingle(e => e.Code == "Core.WithholdingConcept.InUse" && e.Column == P.Activo && e.Sheet == P.HojaConceptos);
    }

    [Fact]
    public async Task Formato_de_celdas_con_la_mecanica_comun()
    {
        Libro(LibroBasicoConceptos, LibroBasicoImpuestos,
        [
            Tarifa("IVA19", "IVA", "diecinueve"),
            Tarifa("BOL", "NOHAY", "19"),
        ]);

        var r = await ImportarAsync(ModoDeImportacion.Review);

        r.Value.Errors.Should().Contain(e => e.Row == 2 && e.Column == P.TarifaPorcentaje && e.Code == "Import.Cell.Format");
        r.Value.Errors.Should().Contain(e => e.Row == 3 && e.Column == P.Impuesto && e.Code == "Import.Cell.NotFound");
    }

    [Fact]
    public async Task Los_datos_de_la_plantilla_vuelven_con_los_mismos_valores_que_se_leen()
    {
        Libro(LibroBasicoConceptos, LibroBasicoImpuestos, LibroBasicoTarifas);
        (await ImportarAsync(ModoDeImportacion.Apply, "Carga")).IsSuccess.Should().BeTrue();

        var datos = (await new GetTaxCatalogTemplateDataQueryHandler(_db).Handle(new GetTaxCatalogTemplateDataQuery(), default)).Value;

        datos.De(P.HojaConceptos).Should().ContainSingle().Which.Should().Equal("COMPRAS", "Compras generales", true);
        var tarifas = datos.De(P.HojaTarifas);
        tarifas.Should().HaveCount(3);
        var rf = tarifas.Single(f => (string?)f[0] == "RF966");
        rf.Count.Should().Be(P.Definicion.Hoja(P.HojaTarifas)!.Columnas.Count, "un valor por columna, en su orden");
        rf[3].Should().Be(0.00966m, "el exportador lo escribe en puntos");
        rf[5].Should().Be("COMPRAS");
        datos.De(P.HojaImpuestos).Single(f => (string?)f[0] == "RETEIVA")[4].Should().Be("IVA");
    }
}
