using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Saas.NationalValues;

/// <summary>
/// T0023 (feature 013, C82; contracts/plantillas.md §10): la plantilla <c>saas.national-values</c> y el paquete <c>.json</c> del
/// registro central. La revisión no escribe; una fila igual a una vigente es «sin cambio»; la misma llave con otro valor y sin
/// <c>corrigeA</c> es <c>NationalValues.ConflictingValue</c> con fila y columna; <c>corrigeA</c> sin motivo es error de fila; cada fila
/// nueva toma su <c>Sequence</c> en el orden del archivo; <c>apply</c> es todo o nada sobre <c>IAdminDbContext</c>; y el paquete de
/// <see cref="ExportNationalValuesQuery"/> se importa en otra base con las mismas filas y la misma huella.
/// </summary>
public class ImportNationalValuesCommandTests
{
    private static readonly string[] Encabezados =
    [
        PlantillaDeValoresNacionales.Codigo, PlantillaDeValoresNacionales.ClaveDelValor, PlantillaDeValoresNacionales.VigenteDesde,
        PlantillaDeValoresNacionales.VigenteHasta, PlantillaDeValoresNacionales.FechaDeReferencia, PlantillaDeValoresNacionales.Valor,
        PlantillaDeValoresNacionales.Unidad, PlantillaDeValoresNacionales.Fuente, PlantillaDeValoresNacionales.Etiqueta,
        PlantillaDeValoresNacionales.CorrigeA, PlantillaDeValoresNacionales.Motivo,
    ];

    private static string?[] Fila(string codigo, string clave, string desde, string valor, string unidad, string fuente = "Norma",
        string? hasta = null, string? corrigeA = null, string? motivo = null, string? etiqueta = null) =>
        [codigo, clave, desde, hasta, null, valor, unidad, fuente, etiqueta, corrigeA, motivo];

    private static ITabularFileReader Lector(params string?[][] filas)
    {
        var lector = Substitute.For<ITabularFileReader>();
        var tabla = new TablaLeida(Encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");
        lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(tabla)));
        return lector;
    }

    private static Task<Result<ImportResultDto>> Importar(RegistroCentralDePrueba e, ITabularFileReader lector, ModoDeImportacion modo, string archivo = "valores.xlsx", byte[]? contenido = null) =>
        new ImportNationalValuesCommandHandler(e.Db, lector, e.Maestro, e.Reloj, e.Validadores)
            .Handle(new ImportNationalValuesCommand(modo, new ArchivoDeImportacion(archivo, contenido ?? [1, 2, 3])), CancellationToken.None);

    [Fact]
    public async Task La_revision_valida_sin_escribir_nada()
    {
        using var e = new RegistroCentralDePrueba();

        var r = await Importar(e, Lector(
            Fila("SMMLV", "", "2026-01-01", "1750905", "Pesos"),
            Fila("FESTIVO", "2026-12-08", "2026-12-08", "1", "Dias", "Ley 51 de 1983", etiqueta: "Inmaculada Concepción")), ModoDeImportacion.Review);

        r.IsSuccess.Should().BeTrue();
        r.Value.Template.Should().Be(PlantillaDeValoresNacionales.Clave);
        r.Value.Valid.Should().BeTrue();
        r.Value.Applied.Should().BeFalse();
        r.Value.Sheets.Single().Created.Should().Be(2);
        (await e.Db.PlatformNationalValues.AnyAsync()).Should().BeFalse("la revisión no escribe");
        (await e.Db.PlatformNationalValueCounters.AnyAsync()).Should().BeFalse("ni consume números");
    }

    [Fact]
    public async Task Aplicar_numera_en_el_orden_del_archivo_y_una_fila_igual_a_una_vigente_es_sin_cambio()
    {
        using var e = new RegistroCentralDePrueba();
        await e.RegistrarAsync(RegistroCentralDePrueba.Smmlv());

        var r = await Importar(e, Lector(
            Fila("UVT", "", "2026-01-01", "52374", "Pesos", "Resolución DIAN 000238"),
            Fila("SMMLV", "", "2026-01-01", "1750905", "Pesos"),
            Fila("LIMITE", "AVISO_PREVIO_DIAS", "2026-01-01", "20", "Dias", "Ley 2157 de 2021")), ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue();
        r.Value.Applied.Should().BeTrue();
        var hoja = r.Value.Sheets.Single();
        (hoja.Created, hoja.Unchanged).Should().Be((2, 1));
        var filas = await e.Db.PlatformNationalValues.OrderBy(v => v.Sequence).Select(v => new { v.Code, v.Sequence }).ToListAsync();
        filas.Select(f => (f.Code, f.Sequence)).Should().Equal(("SMMLV", 1L), ("UVT", 2L), ("LIMITE", 3L));
    }

    [Fact]
    public async Task La_misma_llave_con_otro_valor_y_sin_corrigeA_es_ConflictingValue_con_fila_y_columna()
    {
        using var e = new RegistroCentralDePrueba();
        await e.RegistrarAsync(RegistroCentralDePrueba.Smmlv());

        var r = await Importar(e, Lector(
            Fila("UVT", "", "2026-01-01", "52374", "Pesos"),
            Fila("SMMLV", "", "2026-01-01", "1800000", "Pesos")), ModoDeImportacion.Review);

        var error = r.Value.Errors.Should().ContainSingle().Which;
        (error.Row, error.Column, error.Code).Should().Be((3, PlantillaDeValoresNacionales.Valor, ErroresDeValoresNacionales.ConflictingValueCode));
        error.Message.Should().Contain("corrigeA");
    }

    [Fact]
    public async Task corrigeA_sin_motivo_es_error_de_fila_y_con_motivo_registra_la_correccion()
    {
        using var e = new RegistroCentralDePrueba();
        var original = (await e.RegistrarAsync(RegistroCentralDePrueba.Smmlv(1_700_000m))).Value;

        var sinMotivo = await Importar(e, Lector(Fila("SMMLV", "", "2026-01-01", "1750905", "Pesos", corrigeA: original.ToString())), ModoDeImportacion.Review);
        sinMotivo.Value.Errors.Should().ContainSingle(x => x.Row == 2 && x.Column == PlantillaDeValoresNacionales.Motivo && x.Code == ImportErrors.CellRequired);

        var conMotivo = await Importar(e, Lector(Fila("SMMLV", "", "2026-01-01", "1750905", "Pesos", corrigeA: original.ToString(), motivo: "Valor del decreto")),
            ModoDeImportacion.Apply);
        conMotivo.Value.Applied.Should().BeTrue();
        var correccion = await e.Db.PlatformNationalValues.SingleAsync(v => v.SupersedesPublicId == original);
        (correccion.Value, correccion.Reason).Should().Be((1_750_905m, "Valor del decreto"));
    }

    [Fact]
    public async Task Aplicar_es_todo_o_nada_y_responde_Import_Invalid_con_el_resultado()
    {
        using var e = new RegistroCentralDePrueba();

        var r = await Importar(e, Lector(
            Fila("SMMLV", "", "2026-01-01", "1750905", "Pesos"),
            Fila("DTF", "", "2026-01-01", "0,1", "EA"),
            Fila("UVR", "", "2026-01-01", "400", "Pesos")), ModoDeImportacion.Apply);

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        var resultado = (ImportResultDto)((ErrorConDatos)r.Error).Data;
        resultado.Errors.Should().Contain(x => x.Row == 3 && x.Column == PlantillaDeValoresNacionales.Codigo && x.Code == ErroresDeValoresNacionales.CodeUnknownCode);
        resultado.Errors.Should().Contain(x => x.Row == 4 && x.Code == ImportErrors.CellNotYetAvailable, "UVR se habilita con K6b");
        (await e.Db.PlatformNationalValues.AnyAsync()).Should().BeFalse("la fila buena tampoco se guarda");
    }

    [Fact]
    public async Task El_valor_admite_hasta_diez_decimales_como_fraccion()
    {
        using var e = new RegistroCentralDePrueba();
        await e.CatalogoDeModalidadesAsync();

        var r = await Importar(e, Lector(Fila("USURA", "CONSUMO_Y_ORDINARIO", "2026-11-01", "0,2650000001", "EA", "Superfinanciera", hasta: "2026-11-30")),
            ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue();
        var usura = await e.Db.PlatformNationalValues.SingleAsync(v => v.Code == "USURA");
        (usura.Value, usura.ValidTo).Should().Be((0.2650000001m, (DateOnly?)new DateOnly(2026, 11, 30)));
    }

    [Fact]
    public async Task El_paquete_exportado_se_importa_en_otra_base_con_las_mismas_filas_y_la_misma_huella()
    {
        using var origen = new RegistroCentralDePrueba();
        await PlatformNationalValuesSeeder.AplicarAsync(origen.Db, origen.Validadores, RegistroCentralDePrueba.Ahora, NullLogger.Instance, CancellationToken.None);
        var smmlv = await origen.Db.PlatformNationalValues.SingleAsync(v => v.Code == CodigosDeValorNacional.Smmlv);
        await origen.CorregirAsync(new CorrectNationalValueCommand(smmlv.PublicId, 1_750_906m, null, null, "Decreto", "Prueba de la cadena"));

        var exportado = await new ExportNationalValuesQueryHandler(origen.Db, origen.Reloj).Handle(new ExportNationalValuesQuery(), CancellationToken.None);
        exportado.IsSuccess.Should().BeTrue();
        exportado.Value.NombreArchivo.Should().EndWith(".json");

        using var destino = new RegistroCentralDePrueba();
        var r = await Importar(destino, Substitute.For<ITabularFileReader>(), ModoDeImportacion.Apply, exportado.Value.NombreArchivo, exportado.Value.Contenido);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Applied.Should().BeTrue();
        var a = await origen.Db.PlatformNationalValues.OrderBy(v => v.Sequence).ToListAsync();
        var b = await destino.Db.PlatformNationalValues.OrderBy(v => v.Sequence).ToListAsync();
        b.Select(v => (v.PublicId, v.Code, v.Key, v.ValidFrom, v.ValidTo, v.Value, v.Unit, v.SupersedesPublicId))
            .Should().Equal(a.Select(v => (v.PublicId, v.Code, v.Key, v.ValidFrom, v.ValidTo, v.Value, v.Unit, v.SupersedesPublicId)));
        var da = await origen.Db.PlatformRegulatoryDocuments.OrderBy(d => d.Sequence).Select(d => new { d.PublicId, d.ContentSha256, d.Content }).ToListAsync();
        var db = await destino.Db.PlatformRegulatoryDocuments.OrderBy(d => d.Sequence).Select(d => new { d.PublicId, d.ContentSha256, d.Content }).ToListAsync();
        db.Should().BeEquivalentTo(da, o => o.WithStrictOrdering());

        var otraVez = await Importar(destino, Substitute.For<ITabularFileReader>(), ModoDeImportacion.Apply, exportado.Value.NombreArchivo, exportado.Value.Contenido);
        otraVez.Value.Sheets.Sum(h => h.Created).Should().Be(0, "importar dos veces el mismo paquete no duplica");
    }

    [Fact]
    public async Task Un_paquete_alterado_se_rechaza_por_su_huella()
    {
        using var origen = new RegistroCentralDePrueba();
        await PlatformNationalValuesSeeder.AplicarAsync(origen.Db, origen.Validadores, RegistroCentralDePrueba.Ahora, NullLogger.Instance, CancellationToken.None);
        var exportado = (await new ExportNationalValuesQueryHandler(origen.Db, origen.Reloj).Handle(new ExportNationalValuesQuery(), CancellationToken.None)).Value;
        var texto = System.Text.Encoding.UTF8.GetString(exportado.Contenido).Replace("Consumo", "Consumos");

        using var destino = new RegistroCentralDePrueba();
        var r = await Importar(destino, Substitute.For<ITabularFileReader>(), ModoDeImportacion.Review, "paquete.json", System.Text.Encoding.UTF8.GetBytes(texto));

        r.Value.Errors.Should().Contain(x => x.Sheet == ImportNationalValuesCommandHandler.HojaDeDocumentos && x.Code == ErroresDeValoresNacionales.DocumentInvalidCode);
    }
}
