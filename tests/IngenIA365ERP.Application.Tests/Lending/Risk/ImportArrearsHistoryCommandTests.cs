using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Application.Tests.Lending.Activation;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using C = IngenIA365ERP.Application.Lending.Risk.PlantillaDeHistorialComplementario.Columnas;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, K6a, T1018 (plantillas.md §7; FR-140; data-model §23.9): la carga complementaria del historial de mora sobre la carga aplicada de
/// <see cref="ActivacionConCarteraDePrueba"/> (corte 2026-12-01; CR-100 desembolsado en septiembre con historial migrado de octubre y noviembre, CR-200
/// en noviembre sin historial). Revisión previa con <c>extra.completeness[]</c> por segmento; todo o nada con errores por fila y columna (crédito fuera
/// de la carga, mes con cierre mensual, fuente distinta); después del primer cierre en pérdida esperada, <c>LoadClosed</c>. Aplicar antes de activar
/// guarda la carga con su SHA-256 y sus filas (sin historial: los créditos no existen) y la activación las lleva al historial con origen
/// <c>ComplementaryLoad</c>; después de activar las escribe la carga misma, sin transacciones ni hechos, y volver a cargar un mes no borra nada.
/// </summary>
public class ImportArrearsHistoryCommandTests : IAsyncLifetime
{
    private static readonly string[] Encabezados = [C.NumeroDeCredito, C.Mes, C.MoraMaximaDias, C.MoraAlCierreDias, C.Fuente];

    private ActivacionConCarteraDePrueba _a = null!;

    public async Task InitializeAsync()
    {
        _a = await ActivacionConCarteraDePrueba.CrearAsync();
        await _a.AplicarAsync();
        await PerdidaEsperadaDePrueba.ReglaAsync(_a.Db, _a.G.UtcNow, new DateOnly(2026, 1, 1));
    }

    public Task DisposeAsync()
    {
        _a.Dispose();
        return Task.CompletedTask;
    }

    private void Archivo(params string?[][] filas) =>
        _a.Tabla = new TablaLeida(Encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private static string?[] Fila(string numero, string mes, string mora = "0", string fuente = "Reporte mensual del sistema anterior") =>
        [numero, mes, mora, null, fuente];

    private async Task<Result<ImportResultDto>> ImportarAsync(ModoDeImportacion modo = ModoDeImportacion.Apply, string archivo = "historial.xlsx")
    {
        var servicios = new ServiceCollection().AddSingleton(_a.G.Mongo).AddSingleton<IApplicationDbContext>(_a.Db).BuildServiceProvider();
        var r = await new ImportArrearsHistoryCommandHandler(_a.Db, new EjecutorDeImportacion(_a.Db, _a.Lector, _a.G.Permisos, servicios),
                new LectorDeDocumentoNormativo(_a.Db), _a.G.Actor, _a.G.Auditoria, _a.G.Reloj)
            .Handle(new ImportArrearsHistoryCommand(modo, new ArchivoDeImportacion(archivo, Guid.NewGuid().ToByteArray()), "Historial del sistema anterior")
                { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    private static IReadOnlyList<ErrorDeFila> Errores(Result<ImportResultDto> r) =>
        r.IsSuccess ? r.Value.Errors : r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data.Should().BeOfType<ImportResultDto>().Subject.Errors;

    private async Task CierreMensualAsync(int mes, string modelos = "[]")
    {
        _a.Db.MonthlyCloses.Add(new MonthlyClose
        {
            Kind = MonthlyCloseKind.Monthly, PeriodYear = 2026, PeriodMonth = (byte)mes, Version = 1, CloseDate = MonthlyClose.UltimoDiaDe(2026, mes),
            InformationCutoffAt = _a.G.UtcNow, DocumentsUsedJson = "[]", ModelBySegmentJson = modelos, TotalsJson = "{}", LinesSha256 = new string('0', 64),
            OrderedAt = _a.G.UtcNow, ClosedAt = _a.G.UtcNow,
        });
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
    }

    [Fact]
    public void La_plantilla_esta_en_el_registro_desde_K6a_con_su_permiso()
    {
        var registro = CatalogoDePlantillasDeCartera.Buscar(PlantillaDeHistorialComplementario.Clave)!;

        registro.Definicion.Should().BeSameAs(PlantillaDeHistorialComplementario.Definicion);
        registro.ImportPermission.Should().Be("Lending.ArrearsHistory.Import");
        registro.AvailableFrom.Should().Be(EntregaDeCartera.K6a);
        PlantillaDeHistorialComplementario.Definicion.Hojas[0].Columnas.Select(c => c.Nombre).Should().Equal(Encabezados);
        PlantillaDeHistorialComplementario.Definicion.Modulo.Should().Be(ModuloDeAuditoria.Lending);
    }

    [Fact]
    public async Task La_revision_no_guarda_y_trae_la_completitud_por_segmento()
    {
        Archivo(Fila("CR-100", "2026-09"));

        var r = await ImportarAsync(ModoDeImportacion.Review);

        r.IsSuccess.Should().BeTrue();
        r.Value.Valid.Should().BeTrue();
        var completitud = r.Value.Extra["completeness"].Should().BeAssignableTo<IReadOnlyList<CoberturaDeSegmentoDto>>().Subject;
        var consumo = completitud.Should().ContainSingle().Subject;
        (consumo.Segment, consumo.Loans, consumo.Complete).Should().Be(("CONSUMO_SIN_LIBRANZA", 2, 1), "CR-100 queda completo con septiembre; CR-200 no");
        consumo.Incomplete.Should().ContainSingle().Which.Should().BeEquivalentTo(new { LoanNumber = "CR-200", MissingMonths = new[] { "2026-11" } },
            o => o.ExcludingMissingMembers());
        (await _a.Db.ArrearsHistoryLoads.CountAsync()).Should().Be(0, "la revisión no guarda");
    }

    [Fact]
    public async Task Aplicar_antes_de_activar_guarda_la_carga_y_la_activacion_lleva_sus_filas_al_historial()
    {
        Archivo(Fila("CR-100", "2026-09", "15"), Fila("CR-200", "2026-11"));
        var mensajes = await _a.Db.IntegrationMessages.CountAsync();

        var r = await ImportarAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var carga = await _a.Db.ArrearsHistoryLoads.AsNoTracking().SingleAsync();
        carga.Should().BeEquivalentTo(new { RowCount = 2, Source = "Reporte mensual del sistema anterior", MigrationLoadId = _a.Carga.Id },
            o => o.ExcludingMissingMembers());
        carga.FileSha256.Should().HaveLength(64);
        CoberturaDelHistorialMigrado.Filas(carga.RowsJson).Select(f => (f.LoanNumber, f.Month, f.MaxDaysPastDue)).Should().Equal(("CR-100", 9, 15), ("CR-200", 11, 0));
        (await _a.Db.LoanArrearsHistory.CountAsync()).Should().Be(0, "antes de activar los créditos no existen");
        (await _a.Db.LoanTransactions.CountAsync()).Should().Be(0, "información: sin transacciones");
        (await _a.Db.IntegrationMessages.CountAsync()).Should().Be(mensajes, "ni hechos");
        (await _a.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingArrearsHistoryLoadApplied);

        await _a.AceptarTodoAsync();
        var activada = await _a.ActivarAsync(_a.Orden(_a.Acta()));
        activada.IsSuccess.Should().BeTrue(activada.IsFailure ? $"{activada.Error.Code}: {activada.Error.Message}" : string.Empty);

        var complementarias = await _a.Db.LoanArrearsHistory.AsNoTracking().Where(h => h.Origin == ArrearsHistoryOrigin.ComplementaryLoad).ToListAsync();
        complementarias.Should().HaveCount(2).And.OnlyContain(h => h.ArrearsHistoryLoadId == carga.Id);
        var cr100 = await _a.Db.Loans.AsNoTracking().SingleAsync(l => l.Number == "CR-100");
        complementarias.Should().Contain(h => h.LoanId == cr100.Id && h.PeriodMonth == 9 && h.MaxDaysPastDue == 15);
    }

    [Fact]
    public async Task Despues_de_activar_escribe_el_historial_y_volver_a_cargar_un_mes_no_borra_nada()
    {
        await _a.AceptarTodoAsync();
        (await _a.ActivarAsync(_a.Orden(_a.Acta()))).IsSuccess.Should().BeTrue();
        var cr100 = await _a.Db.Loans.AsNoTracking().SingleAsync(l => l.Number == "CR-100");

        Archivo(Fila("CR-100", "2026-09", "10"));
        (await ImportarAsync()).IsSuccess.Should().BeTrue();
        Archivo(Fila("CR-100", "2026-09", "20", "Reporte corregido"));
        (await ImportarAsync(archivo: "historial-corregido.xlsx")).IsSuccess.Should().BeTrue();

        var filas = await _a.Db.LoanArrearsHistory.AsNoTracking().Where(h => h.LoanId == cr100.Id && h.PeriodMonth == 9).OrderBy(h => h.Id).ToListAsync();
        filas.Select(h => (h.Origin, h.MaxDaysPastDue)).Should().Equal((ArrearsHistoryOrigin.ComplementaryLoad, 10), (ArrearsHistoryOrigin.ComplementaryLoad, 20));
        filas.Select(h => h.ArrearsHistoryLoadId).Distinct().Should().HaveCount(2, "cada aplicación es una carga nueva");
        var entradas = await CompletitudDelHistorial.EntradasAsync(_a.Db, [cr100.Id], null, default);
        Domain.Lending.Risk.HistorialDeMora.Construir(new Domain.Lending.Risk.CreditoParaHistorial(cr100.Id, new DateOnly(2026, 9, 1)), entradas,
                CompletitudDelHistorial.CorteParaHistorial(ActivacionConCarteraDePrueba.Corte), ActivacionConCarteraDePrueba.Corte, 36)
            .Meses.Single(m => m.Mes == 9).MaxDiasDeMora.Should().Be(20, "manda la carga más reciente");
    }

    [Fact]
    public async Task Un_credito_fuera_de_la_carga_un_mes_cerrado_y_otra_fuente_son_errores_de_fila_y_columna()
    {
        await CierreMensualAsync(10);
        Archivo(Fila("CR-999", "2026-09"), Fila("CR-100", "2026-10"), Fila("CR-200", "2026-09", fuente: "Otra fuente"));

        var r = await ImportarAsync();

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        var errores = Errores(r);
        errores.Should().Contain(e => e.Row == 2 && e.Column == C.NumeroDeCredito && e.Code == ErroresDeRiesgo.CodigoHistorialFueraDeLaCarga);
        errores.Should().Contain(e => e.Row == 3 && e.Column == C.Mes && e.Code == ErroresDeRiesgo.CodigoHistorialMesCerrado);
        errores.Should().Contain(e => e.Row == 4 && e.Column == C.Fuente);
        (await _a.Db.ArrearsHistoryLoads.CountAsync()).Should().Be(0, "todo o nada");
    }

    [Fact]
    public async Task Despues_del_primer_cierre_en_perdida_esperada_la_carga_se_cierra()
    {
        await CierreMensualAsync(11, """[{"segment":"CONSUMO_SIN_LIBRANZA","model":"ExpectedLossAnnex2","basis":"Obligated"}]""");
        Archivo(Fila("CR-100", "2026-09"));

        (await ImportarAsync()).Error.Code.Should().Be(ErroresDeRiesgo.CodigoHistorialCargaCerrada);
    }

    [Fact]
    public async Task Sin_carga_aplicada_no_hay_que_complementar()
    {
        var carga = await _a.Db.MigrationLoads.SingleAsync(l => l.Id == _a.Carga.Id);
        carga.Status = MigrationLoadStatus.Replaced;
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        Archivo(Fila("CR-100", "2026-09"));

        (await ImportarAsync()).Error.Code.Should().Be(ErroresDeRiesgo.CodigoHistorialSinCarga);
        (await _a.Db.LendingSetups.AsNoTracking().SingleAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica)).ActivationDate.Should().BeNull();
    }
}
