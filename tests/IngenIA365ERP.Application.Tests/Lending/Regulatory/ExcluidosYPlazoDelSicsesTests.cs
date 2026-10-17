using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Processes.Tareas;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Application.Tests.Lending.Reports;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using static IngenIA365ERP.Application.Tests.Lending.Regulatory.RegulatoriosDePrueba;

namespace IngenIA365ERP.Application.Tests.Lending.Regulatory;

/// <summary>
/// T1006 y T1007 (feature 013, K5, US16; api.md §17; DT §2.13; adaptadores-y-archivos.md §7.4): la vista <c>bureau-exclusions</c> lista los titulares
/// excluidos de la generación vigente de una central y período, con su motivo y la gestión que lo corrige, dentro del alcance de oficina y línea (con
/// datos del deudor, <c>AvailableFrom = K5</c>); y la vigilancia levanta <c>Cartera.PlazoRegulatorioPorVencer</c> para el SICSES desde los días de aviso
/// antes del día límite que fija el perfil, si el mes no tiene una generación <c>Generated</c> ni <c>Uploaded</c>.
/// </summary>
public sealed class ExcluidosYPlazoDelSicsesTests : IAsyncLifetime
{
    private RegulatoriosDePrueba _r = null!;

    public async Task InitializeAsync()
    {
        _r = await CrearAsync();
        await _r.CierreAsync([Linea(_r.Credito, capital: 3_000_000m)]);
    }

    public Task DisposeAsync()
    {
        _r.Dispose();
        return Task.CompletedTask;
    }

    private BureauExclusionsReportQueryHandler Vista() => new(_r.Db, _r.G.Alcance, CatalogoDeLayoutsDeCartera.Embebido);

    private Task<IngenIA365ERP.Application.Common.Models.Result<VigilanciaRealizada>> VigilarAsync() =>
        new WatchLendingProcessesCommandHandler(_r.Db, _r.D.Alertas, _r.G.Reloj, [], null, _r.G.Perfil, _r.D.O.L.Normativa())
            .Handle(new WatchLendingProcessesCommand(), default);

    private async Task GenerarCentralAsync()
    {
        var r = await new GenerateCreditBureauFileCommandHandler(_r.Generacion())
            .Handle(new GenerateCreditBureauFileCommand("DATACREDITO", Anio, Mes, true) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        _r.Db.ChangeTracker.Clear();
    }

    [Fact]
    public void La_vista_se_registra_en_K5_con_datos_del_deudor_y_sus_filtros_propios()
    {
        var v = BureauExclusionsReportQueryHandler.Vista;

        (v.Key, v.AvailableFrom, v.PersonalData, v.RequiredPermission).Should().Be(("bureau-exclusions", EntregaDeCartera.K5, true, "Lending.Loans.Read"));
        v.OwnFilters.Should().Equal("bureau", "year", "month");
        BureauExclusionsReportQueryHandler.Columnas.Select(c => c.Clave ?? c.Nombre).Should().Equal("Crédito", "Deudor", "Motivo", "Gestión sugerida", "_credito");
    }

    [Fact]
    public async Task Lista_los_excluidos_de_la_generacion_vigente_con_su_motivo_y_su_gestion()
    {
        var moroso = _r.Persona("Moroso", "Prueba", "71000002");
        var sinPermiso = _r.Persona("Reservada", "Prueba", "71000003");
        var a = await _r.CreditoAsync("CR-VIS-0001", moroso, dias: 60);
        var b = await _r.CreditoAsync("CR-VIS-0002", sinPermiso);
        _r.D.O.QuitarAutorizacion(sinPermiso, IngenIA365ERP.Domain.Enums.Compliance.DataAuthorizationPurpose.CreditBureauReport);
        await GenerarCentralAsync();

        var t = InformesDePrueba.Tabla(await Vista().Handle(new BureauExclusionsReportQuery(new FiltrosDeInformeDeCartera(), "datacredito", Anio, Mes), default));

        t.Filas.Should().HaveCount(2);
        var fila = t.Filas.Single(f => (string?)InformesDePrueba.Valor(t, f, "Crédito") == "CR-VIS-0001");
        InformesDePrueba.Valor(t, fila, "Motivo").Should().Be("Sin aviso previo con constancia");
        ((string)InformesDePrueba.Valor(t, fila, "Gestión sugerida")!).Should().Contain("aviso previo");
        InformesDePrueba.Valor(t, fila, "_credito").Should().Be(a.PublicId.ToString());
        t.Filas.Should().Contain(f => (string?)InformesDePrueba.Valor(t, f, "Motivo") == "Sin autorización de reporte"
                                      && (string?)InformesDePrueba.Valor(t, f, "Crédito") == b.Number);

        // Sin year ni month, la última generada; fuera del alcance de la oficina, nada.
        InformesDePrueba.Tabla(await Vista().Handle(new BureauExclusionsReportQuery(new FiltrosDeInformeDeCartera(), "DATACREDITO"), default)).Filas.Should().HaveCount(2);
        _r.G.AlcanceActual = InformesDePrueba.OtraOficina(_r.Credito.BranchId);
        InformesDePrueba.Tabla(await Vista().Handle(new BureauExclusionsReportQuery(new FiltrosDeInformeDeCartera(), "DATACREDITO", Anio, Mes), default))
            .Filas.Should().BeEmpty("la vista respeta el alcance de oficina y línea");
    }

    [Fact]
    public async Task Una_central_sin_layout_o_sin_bureau_es_un_filtro_invalido_y_sin_generacion_la_tabla_va_vacia()
    {
        var sin = await Vista().Handle(new BureauExclusionsReportQuery(new FiltrosDeInformeDeCartera()), default);
        sin.Error.Code.Should().Be("Lending.Report.FilterInvalid");
        (await Vista().Handle(new BureauExclusionsReportQuery(new FiltrosDeInformeDeCartera(), "CIFIN"), default)).Error.Code.Should().Be("Lending.Report.FilterInvalid");

        var vacia = InformesDePrueba.Tabla(await Vista().Handle(new BureauExclusionsReportQuery(new FiltrosDeInformeDeCartera(), "TRANSUNION", Anio, Mes), default));
        vacia.Filas.Should().BeEmpty();
        vacia.Notas.Should().ContainSingle(n => n.Contains("No hay una generación"));
    }

    [Fact]
    public async Task La_vigilancia_avisa_el_plazo_del_SICSES_sin_generacion()
    {
        await _r.PerfilConSicsesAsync();
        _r.G.HoyLocal = new DateOnly(2027, 1, 10);

        (await VigilarAsync()).Value.SicsesPorVencer.Should().NotContain("2026-12", "el aviso empieza cinco días antes del 20 de enero");

        _r.G.HoyLocal = new DateOnly(2027, 1, 16);
        var r = await VigilarAsync();

        r.Value.SicsesPorVencer.Should().Contain("2026-12");
        var alerta = _r.Alertas.Single(a => a.DedupKey == $"{TiposDeAlerta.PlazoRegulatorioPorVencer}:SICSES:2026-12");
        alerta.RecipientPermissions.Should().Equal("Lending.RegulatoryReports.Generate");
        alerta.Body.Should().Contain("2027-01-20");
    }

    [Fact]
    public async Task Con_el_SICSES_generado_o_un_mes_que_no_se_reporta_no_hay_aviso()
    {
        await _r.PerfilConSicsesAsync();
        _r.G.HoyLocal = new DateOnly(2027, 1, 16);
        var g = await new GenerateSicsesFileCommandHandler(_r.Generacion())
            .Handle(new GenerateSicsesFileCommand(Anio, Mes, true) { OperationKey = Guid.NewGuid() }, default);
        g.Value.File.Status.Should().Be(RegulatoryFileStatus.Generated);
        _r.Db.ChangeTracker.Clear();

        (await VigilarAsync()).Value.SicsesPorVencer.Should().NotContain("2026-12");
        _r.Alertas.Should().NotContain(a => a.DedupKey == $"{TiposDeAlerta.PlazoRegulatorioPorVencer}:SICSES:2026-12");
    }

    [Fact]
    public void La_semilla_trae_el_plazo_del_formato_9027_por_nivel_de_supervision()
    {
        // Decisión del dueño del 2026-10-16: nivel 1 mensual a 20 días, diciembre al último día hábil de enero; nivel 2 trimestral a 30 días, diciembre
        // en febrero (conservador: el 1); nivel 3 anual en febrero, salvo fondos de empleados y mutuales, que no lo traen todavía.
        var leido = IngenIA365ERP.Domain.Common.NationalValues.TiposDeOrganizacion.Leer(
            IngenIA365ERP.Persistence.Seeding.Parametric.PlatformNationalValuesSeeder.Contenido("tipos-de-organizacion.json"), 1);
        leido.Errores.Should().BeEmpty();
        var tipos = leido.Documento!;
        var coop = tipos.Buscar("COOP_AHORRO_CREDITO")!;

        coop.SicsesDelNivel(1)!.Limite(2026, 12).Should().Be(new DateOnly(2027, 1, 29), "el 31 de enero de 2027 es domingo");
        coop.SicsesDelNivel(1)!.Limite(2027, 1).Should().Be(new DateOnly(2027, 2, 20));
        coop.SicsesDelNivel(2)!.SeReporta(5).Should().BeFalse();
        coop.SicsesDelNivel(2)!.Limite(2027, 3).Should().Be(new DateOnly(2027, 4, 30));
        coop.SicsesDelNivel(2)!.Limite(2026, 12).Should().Be(new DateOnly(2027, 2, 1));
        coop.SicsesDelNivel(3)!.CutoffMonths.Should().Equal(12);
        coop.SicsesDelNivel(3)!.Limite(2026, 12).Should().Be(new DateOnly(2027, 2, 1));
        tipos.Buscar("FONDO_EMPLEADOS")!.SicsesDelNivel(3).Should().BeNull();
        tipos.Buscar("ASOCIACION_MUTUAL")!.SicsesDelNivel(3).Should().BeNull();
        tipos.Tipos.Should().OnlyContain(t => t.SicsesDelNivel(1) != null && t.SicsesDelNivel(2) != null);
    }

    [Fact]
    public async Task El_corte_de_diciembre_del_nivel_1_vence_el_ultimo_dia_habil_de_enero()
    {
        await _r.PerfilConSicsesAsync(1, """
            [ { "supervisionLevel": 1, "cutoffMonths": [1,2,3,4,5,6,7,8,9,10,11,12], "dueDays": 20, "warningDays": 5,
                "dueExceptions": [ { "cutoffMonth": 12, "monthsAfterCutoff": 1, "lastBusinessDay": true } ] } ]
            """);
        _r.G.HoyLocal = new DateOnly(2027, 1, 16);

        (await VigilarAsync()).Value.SicsesPorVencer.Should().NotContain("2026-12", "vence el viernes 29 de enero: se avisa desde el 24");

        _r.G.HoyLocal = new DateOnly(2027, 1, 24);
        (await VigilarAsync()).Value.SicsesPorVencer.Should().Contain("2026-12");
        _r.Alertas.Single(a => a.DedupKey == $"{TiposDeAlerta.PlazoRegulatorioPorVencer}:SICSES:2026-12").Body.Should().Contain("2027-01-29");
        (await new ProyeccionSicsesCartera(_r.Db, _r.D.Contabilidad, _r.G.Perfil, _r.D.O.L.Normativa()).ProyectarAsync(Anio, Mes, default))
            .Value.Plazo!.Limite.Should().Be(new DateOnly(2027, 1, 29));
    }

    [Fact]
    public async Task Un_nivel_que_no_reporta_el_mes_o_un_perfil_sin_el_dato_no_avisan()
    {
        await _r.PerfilConSicsesAsync(2, """[ { "supervisionLevel": 2, "cutoffMonths": [3, 6, 9], "dueDays": 30, "warningDays": 5 } ]""");
        _r.G.HoyLocal = new DateOnly(2027, 1, 25);

        (await VigilarAsync()).Value.SicsesPorVencer.Should().BeEmpty();
        _r.Alertas.Should().NotContain(a => a.TypeCode == TiposDeAlerta.PlazoRegulatorioPorVencer);
    }
}
