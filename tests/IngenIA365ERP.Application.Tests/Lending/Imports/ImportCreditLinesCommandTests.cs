using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using C = IngenIA365ERP.Application.Lending.Imports.CatalogoDePlantillasDeCartera.ColumnasDeLineas;
using H = IngenIA365ERP.Application.Lending.Imports.CatalogoDePlantillasDeCartera.HojasDeLineas;

namespace IngenIA365ERP.Application.Tests.Lending.Imports;

/// <summary>
/// Feature 013, T0338 (US1-6; plantillas.md §1, §0.5): la plantilla <c>lending.credit-lines</c> por <see cref="ImportCreditLinesCommandHandler"/>.
/// Seis hojas; <c>review</c> no guarda y <c>apply</c> es todo o nada con <c>Import.Invalid</c> y <c>errors[] { sheet, row, column, code,
/// message }</c>; las reglas son las del alta unitaria (mismo código: <c>Lending.Usury.Exceeded</c> en la hoja, fila y columna de la tasa,
/// <c>NationalValues.Missing</c>, <c>ModalityUnknown</c>); los porcentajes van en puntos con hasta 4 decimales y se guardan como fracción de
/// 6; <c>VIVIENDA</c> es <c>Import.Cell.NotYetAvailable</c>; una aseguradora que no está en el maestro es error de fila; la misma línea
/// con una <c>vigenteDesde</c> posterior crea una versión y reemplaza entera sus hijas; una versión que empieza antes de hoy en una línea
/// con solicitudes se rechaza; crear vigencias pide motivo; las equivalencias de Comercial entran por su hoja.
/// </summary>
public sealed class ImportCreditLinesCommandTests : IDisposable
{
    private readonly LineasDePrueba _l = new();
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly Dictionary<string, List<Dictionary<string, string?>>> _hojas = new(StringComparer.OrdinalIgnoreCase);

    public ImportCreditLinesCommandTests()
    {
        _l.NormativaAsync(0.25m).GetAwaiter().GetResult();
        _l.G.Permisos.EsMaestroGlobal.Returns(true);
        _lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(CatalogoDePlantillasDeCartera.LineasDeCredito.Hojas.Select(h => h.Nombre).ToList())));
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success(Tabla(ci.ArgAt<string?>(2)!))));
    }

    public void Dispose() => _l.Dispose();

    private TablaLeida Tabla(string hoja)
    {
        var definicion = CatalogoDePlantillasDeCartera.LineasDeCredito.Hoja(hoja)!;
        var encabezados = definicion.Columnas.Select(c => c.Nombre).ToList();
        var filas = (_hojas.TryGetValue(hoja, out var lista) ? lista : [])
            .Select((f, i) => new FilaLeida(i + 2, encabezados.Select(e => f.TryGetValue(e, out var v) ? v : null).ToList()))
            .ToList();
        return new TablaLeida(encabezados, filas, "xlsx");
    }

    private void Fila(string hoja, Dictionary<string, string?> fila)
    {
        if (!_hojas.TryGetValue(hoja, out var lista)) _hojas[hoja] = lista = [];
        lista.Add(fila);
    }

    private static Dictionary<string, string?> Linea(string codigo, string desde = "2026-12-01", string tasa = "20", string modalidad = "CONSUMO",
        string? subsegmento = "SinLibranza") => new()
    {
        [C.Codigo] = codigo, [C.VigenteDesde] = desde, [C.Nombre] = $"Línea {codigo}", [C.Destino] = "Libre inversión", [C.Modalidad] = modalidad,
        [C.Subsegmento] = subsegmento, [C.TipoDeTasa] = "Fixed", [C.TasaCorriente] = tasa, [C.TipoDeMora] = "RelativeToCurrent",
        [C.FactorDeMora] = "1,5", [C.ConvencionDeDias] = "Actual365", [C.Sistema] = "LevelPayment", [C.MontoMinimo] = "500000",
        [C.MontoMaximo] = "30000000", [C.PlazoMinimo] = "1", [C.PlazoMaximo] = "60", [C.Periodicidades] = "Monthly, 15",
        [C.PeriodicidadPorDefecto] = "mensual", [C.FormasDePago] = "Cashier, OwnPayroll", [C.FormaDePagoPorDefecto] = "Cashier",
    };

    private async Task<Result<ImportResultDto>> Importar(ModoDeImportacion modo, string motivo = "Parametrización inicial de líneas")
    {
        var servicios = new ServiceCollection().AddSingleton(_l.G.Mongo).BuildServiceProvider();
        var ejecutor = new EjecutorDeImportacion(_l.Db, _lector, _l.G.Permisos, servicios);
        var r = await new ImportCreditLinesCommandHandler(_l.Db, ejecutor, _l.Reglas(), _l.Alta(), _l.G.Actor, _l.G.Reloj)
            .Handle(new ImportCreditLinesCommand(modo, new ArchivoDeImportacion("lineas.xlsx", [1, 2, 3]), motivo) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    private static IReadOnlyList<ErrorDeFila> Errores(Result<ImportResultDto> r) =>
        r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data.Should().BeOfType<ImportResultDto>().Subject.Errors;

    [Fact]
    public void La_plantilla_tiene_las_seis_hojas_de_plantillas_1_y_es_del_modulo_Lending()
    {
        var d = CatalogoDePlantillasDeCartera.LineasDeCredito;

        d.Clave.Should().Be("lending.credit-lines");
        d.Modulo.Should().Be(ModuloDeAuditoria.Lending);
        d.Hojas.Select(h => h.Nombre).Should().Equal(H.Lineas, H.Tasas, H.Documentos, H.Seguros, H.Garantias, H.Equivalencias);
        d.Hoja(H.Lineas)!.Columnas.Select(c => c.Nombre).Should().Contain([C.Codigo, C.VigenteDesde, C.Modalidad, C.TasaCorriente, C.FactorDeMora, C.Periodicidades]);
        d.Hoja(H.Seguros)!.Columnas.Select(c => c.Nombre).Should().Contain([C.Aseguradora, C.Tarifa, C.RetieneParteDeLaPrima, C.PorcentajeRetenido]);
        CatalogoDePlantillasDeCartera.Buscar("lending.credit-lines")!.Definicion.Should().BeSameAs(d);
    }

    [Fact]
    public async Task Review_no_guarda_y_apply_crea_las_lineas_con_sus_hijas_y_porcentajes_como_fraccion()
    {
        Fila(H.Lineas, Linea("LIBRE", tasa: "22,5"));
        Fila(H.Lineas, Linea("LIBRANZA", tasa: "19,8243", subsegmento: "ConLibranza"));
        Fila(H.Tasas, new() { [C.Linea] = "LIBRE", [C.VigenteDesde] = "2026-12-01", [C.MontoDesde] = "0", [C.MontoHasta] = "5000000", [C.TasaCorriente] = "23" });
        Fila(H.Documentos, new() { [C.Linea] = "LIBRE", [C.VigenteDesde] = "2026-12-01", [C.Codigo] = "CEDULA", [C.Nombre] = "Copia de cédula" });
        Fila(H.Seguros, new()
        {
            [C.Linea] = "LIBRE", [C.VigenteDesde] = "2026-12-01", [C.Codigo] = "VIDA", [C.Nombre] = "Vida deudores", [C.Aseguradora] = "860001",
            [C.Base] = "saldo", [C.Tarifa] = "0,045",
        });
        Fila(H.Garantias, new() { [C.Linea] = "LIBRE", [C.VigenteDesde] = "2026-12-01", [C.MontoDesde] = "10000001", [C.CodeudoresMinimos] = "1", [C.TiposAdmitidos] = "CoDebtor, 4" });
        Fila(H.Equivalencias, new() { [C.PeriodicidadComercial] = "Monthly", [C.Periodicidad] = "Monthly", [C.VigenteDesde] = "2026-12-01" });

        var revision = await Importar(ModoDeImportacion.Review);

        revision.IsSuccess.Should().BeTrue(revision.IsFailure ? revision.Error.Message : string.Empty);
        revision.Value.Valid.Should().BeTrue(string.Join(" | ", revision.Value.Errors.Select(e => $"{e.Sheet}:{e.Row}:{e.Column}:{e.Code}:{e.Message}")));
        revision.Value.RequiresReason.Should().BeTrue("crea vigencias");
        (await _l.Db.CreditLines.CountAsync()).Should().Be(0, "review no guarda nada");

        var r = await Importar(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        (await _l.Db.CreditLines.CountAsync()).Should().Be(2);
        (await _l.Db.CreditLines.ToListAsync()).Should().OnlyContain(l => l.Status == CreditLineStatus.Active);
        var libranza = await _l.Db.CreditLineVersions.SingleAsync(v => v.SubsegmentCode == "ConLibranza");
        libranza.AnnualRate.Should().Be(0.198243m, "19,8243 puntos se guardan como fracción de 6 decimales");
        libranza.LeerPeriodicidades().Should().Equal(RepaymentPeriodicity.Biweekly, RepaymentPeriodicity.Monthly);
        libranza.Reason.Should().Be("Parametrización inicial de líneas");
        (await _l.Db.CreditLineRateBands.SingleAsync()).AnnualRate.Should().Be(0.23m);
        var seguro = await _l.Db.CreditLineInsurances.SingleAsync();
        (seguro.Rate, seguro.InsurerPersonId).Should().Be((0.00045m, _l.Aseguradora.Id));
        (await _l.Db.CreditLineCollateralRules.SingleAsync()).AdmittedKinds.Should().Be("[\"CoDebtor\",\"Mortgage\"]");
        (await _l.Db.CreditLineDocumentRequirements.CountAsync()).Should().Be(1);
        (await _l.Db.CommercialTermEquivalences.SingleAsync()).Periodicity.Should().Be(RepaymentPeriodicity.Monthly);
    }

    [Fact]
    public async Task Una_tasa_sobre_la_usura_es_Usury_Exceeded_en_su_hoja_fila_y_columna_y_apply_no_guarda_nada()
    {
        Fila(H.Lineas, Linea("LIBRE"));
        Fila(H.Lineas, Linea("CARA", tasa: "26"));
        Fila(H.Lineas, Linea("BANDA"));
        Fila(H.Tasas, new() { [C.Linea] = "BANDA", [C.VigenteDesde] = "2026-12-01", [C.MontoDesde] = "0", [C.MontoHasta] = "5000000", [C.TasaCorriente] = "27" });

        var r = await Importar(ModoDeImportacion.Apply);

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        var errores = Errores(r);
        errores.Should().Contain(e => e.Sheet == H.Lineas && e.Row == 3 && e.Column == C.TasaCorriente && e.Code == "Lending.Usury.Exceeded");
        errores.Should().Contain(e => e.Sheet == H.Tasas && e.Row == 2 && e.Column == C.TasaCorriente && e.Code == "Lending.Usury.Exceeded");
        (await _l.Db.CreditLines.CountAsync()).Should().Be(0, "apply es todo o nada");
        (await _l.Db.CreditLineVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_modalidad_inexistente_y_vivienda_son_errores_de_la_columna_modalidad()
    {
        Fila(H.Lineas, Linea("RARA", modalidad: "FANTASIA", subsegmento: null));
        Fila(H.Lineas, Linea("VIV", modalidad: "VIVIENDA", subsegmento: null));
        Fila(H.Lineas, Linea("MAL", tasa: "20,00001"));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Valid.Should().BeFalse();
        r.Value.Errors.Should().Contain(e => e.Row == 2 && e.Column == C.Modalidad && e.Code == "Lending.CreditLine.ModalityUnknown");
        r.Value.Errors.Should().Contain(e => e.Row == 3 && e.Column == C.Modalidad && e.Code == ImportErrors.CellNotYetAvailable);
        r.Value.Errors.Should().Contain(e => e.Row == 4 && e.Column == C.TasaCorriente && e.Code == ImportErrors.CellFormat, "más de 4 decimales en puntos");
    }

    [Fact]
    public async Task Sin_usura_de_la_fecha_es_NationalValues_Missing()
    {
        Fila(H.Lineas, Linea("LIBRE", desde: "2026-11-01"));

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Errors.Should().ContainSingle(e => e.Code == "NationalValues.Missing" && e.Column == C.VigenteDesde);
    }

    [Fact]
    public async Task Una_aseguradora_que_no_esta_en_el_maestro_es_error_de_fila()
    {
        Fila(H.Lineas, Linea("LIBRE"));
        Fila(H.Seguros, new()
        {
            [C.Linea] = "LIBRE", [C.VigenteDesde] = "2026-12-01", [C.Codigo] = "VIDA", [C.Nombre] = "Vida", [C.Aseguradora] = "999999",
            [C.Base] = "OutstandingBalance", [C.Tarifa] = "0,05",
        });

        var r = await Importar(ModoDeImportacion.Apply);

        Errores(r).Should().ContainSingle(e => e.Sheet == H.Seguros && e.Row == 2 && e.Column == C.Aseguradora && e.Code == ImportErrors.CellNotFound);
        (await _l.Db.CreditLines.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_misma_linea_con_una_vigenteDesde_posterior_crea_version_y_reemplaza_entera_las_hijas()
    {
        await _l.LineaAsync("LIBRE", LineasDePrueba.Version() with
        {
            Rates = LineasDePrueba.Version().Rates with { Bands = [new Application.Lending.Configuration.RateBandInput(0m, 5_000_000m, EffectiveAnnual: 0.22m)] },
        });
        Fila(H.Lineas, Linea("LIBRE", desde: "2027-01-01", tasa: "19"));
        Fila(H.Documentos, new() { [C.Linea] = "LIBRE", [C.VigenteDesde] = "2027-01-01", [C.Codigo] = "RUT", [C.Nombre] = "RUT" });

        var r = await Importar(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? string.Join(" | ", Errores(r).Select(e => $"{e.Sheet}:{e.Row}:{e.Column}:{e.Code}:{e.Message}")) : string.Empty);
        var versiones = await _l.Db.CreditLineVersions.OrderBy(v => v.VersionNumber).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[1].ValidFrom.Should().Be(new DateOnly(2027, 1, 1));
        versiones[1].AnnualRate.Should().Be(0.19m);
        (await _l.Db.CreditLineRateBands.CountAsync(b => b.CreditLineVersionId == versiones[1].Id)).Should().Be(0, "lo que la versión nueva no trae, no lo tiene");
        (await _l.Db.CreditLineDocumentRequirements.Where(d => d.CreditLineVersionId == versiones[1].Id).Select(d => d.Code).ToListAsync())
            .Should().Equal("RUT");
        (await _l.Db.CreditLineRateBands.CountAsync(b => b.CreditLineVersionId == versiones[0].Id)).Should().Be(1, "la versión anterior no se toca");
    }

    [Fact]
    public async Task Una_version_igual_a_la_registrada_queda_sin_cambio()
    {
        Fila(H.Lineas, Linea("LIBRE"));
        await Importar(ModoDeImportacion.Apply);

        var r = await Importar(ModoDeImportacion.Review);

        r.Value.Sheets.Single(h => h.Sheet == H.Lineas).Unchanged.Should().Be(1);
    }

    [Fact]
    public async Task Antes_de_hoy_solo_en_una_linea_sin_solicitudes_ni_creditos()
    {
        await _l.UsuraAsync(LineasDePrueba.ClaveConsumo, 0.25m, new DateOnly(2026, 11, 1));
        Fila(H.Lineas, Linea("NUEVA", desde: "2026-11-01"));
        var nueva = await Importar(ModoDeImportacion.Apply);
        nueva.IsSuccess.Should().BeTrue(nueva.IsFailure ? string.Join(" | ", Errores(nueva).Select(e => $"{e.Column}:{e.Code}:{e.Message}")) : string.Empty);

        var linea = await _l.Db.CreditLines.SingleAsync();
        var version = await _l.Db.CreditLineVersions.SingleAsync();
        _l.Db.CreditApplications.Add(new CreditApplication
        {
            ApplicantPersonId = _l.Aseguradora.Id, DebtorKind = DebtorKind.Associate, BranchId = 1, CreditLineId = linea.Id, CreditLineVersionId = version.Id,
            RequestedAmount = 1_000_000m, RepaymentChannel = RepaymentChannel.Cashier,
        });
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
        _hojas.Clear();
        Fila(H.Lineas, Linea("NUEVA", desde: "2026-11-15", tasa: "18"));

        var r = await Importar(ModoDeImportacion.Apply);

        Errores(r).Should().ContainSingle(e => e.Column == C.VigenteDesde && e.Code == "Lending.CreditLine.RetroactiveVersion");
    }

    [Fact]
    public async Task Aplicar_sin_motivo_cuando_crea_vigencias_es_un_error()
    {
        Fila(H.Lineas, Linea("LIBRE"));

        var r = await Importar(ModoDeImportacion.Apply, motivo: string.Empty);

        Errores(r).Should().ContainSingle(e => e.Column == ImportErrors.ColumnaDelMotivo && e.Code == ImportErrors.CellRequired);
        (await _l.Db.CreditLines.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Activa_no_cierra_la_linea_a_creditos_nuevos()
    {
        Fila(H.Lineas, Linea("LIBRE"));
        _hojas[H.Lineas][0][C.Activa] = "no";

        var r = await Importar(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var linea = await _l.Db.CreditLines.SingleAsync();
        linea.Status.Should().Be(CreditLineStatus.Closed);
        linea.ClosedReason.Should().Be(CreditLineClosedReason.Manual);
    }
}
