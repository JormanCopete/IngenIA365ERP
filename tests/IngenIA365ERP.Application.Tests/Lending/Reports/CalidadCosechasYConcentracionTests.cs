using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Origination.ConnectedGroups;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Reports;

/// <summary>
/// T0977 (feature 013, US15, K5; FR-134; US15-2; contracts/api.md §17): las vistas de K5 sobre el escenario de <see cref="InformesDePrueba"/> (hoy
/// 2026-12-01, el crédito de Ana de 3.000.000 desembolsado hoy en su oficina), con créditos y cierres sembrados como hechos para fijar las cifras a
/// mano:
/// <list type="bullet">
/// <item><c>quality</c>: bruta, vencida (más de <c>Informes.DiasCarteraVencida</c> días), en riesgo (B–E de la calificación oficial del último
/// cierre vigente al corte), deterioro individual más general y cobertura, cuadrando con el cierre; el reabierto no cuenta; sin cierre, vacía; con
/// alcance o filtro por línea, sin el general.</item>
/// <item><c>vintages</c>: seis meses de desembolso (enero a junio de 2026) con la mora a 30, 60 y 90 días por mes de maduración contra un cálculo
/// a mano (la prueba independiente de US15).</item>
/// <item><c>concentration</c>: por deudor con el porcentaje del límite individual (10 % de un patrimonio técnico de 1.000 millones) y la señal,
/// por grupo conectado, línea, sector y pagaduría; sin patrimonio técnico vigente, sin porcentaje y con la advertencia.</item>
/// <item><c>issued-documents</c>: filtros propios inválidos con los admitidos.</item>
/// </list>
/// Definiciones a validar por la contadora (las fija el XML de cada vista y el manual cartera-informes-y-tablero.md).
/// </summary>
public class CalidadCosechasYConcentracionTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = InformesDePrueba.Hoy;

    private InformesDePrueba _e = null!;
    private int _corrida = 9000;
    private Branch _otra = null!;

    public async Task InitializeAsync()
    {
        _e = await InformesDePrueba.CrearAsync();
        _otra = new Branch { Name = "Oficina Sur", CreatedBy = "test" };
        _e.Db.Branches.Add(_otra);
        await _e.Db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _e.Dispose();
        return Task.CompletedTask;
    }

    private QualityReportQueryHandler Calidad() => new(_e.Db, _e.G.Alcance, _e.D.Parametros(), _e.G.Reloj);
    private VintagesReportQueryHandler Cosechas() => new(_e.Db, _e.G.Alcance, _e.G.Reloj);

    private ConcentrationReportQueryHandler Concentracion() => new(_e.Db, _e.G.Alcance, _e.D.Parametros(),
        new LectorDeIndicadoresDeLaEntidad(_e.D.Contabilidad, _e.D.Parametros()), new GrupoConectadoDelDeudor(_e.Db), _e.G.Reloj);

    private static object? V(TablaExportable t, FilaExportable f, string columna) => InformesDePrueba.Valor(t, f, columna);

    private static string Nombre(Person p) => IngenIA365ERP.Application.Lending.Common.ReferenciasDeGobierno.Nombre(p);

    private static FiltrosDeInformeDeCartera Filtros(DateOnly? corte = null, DateOnly? desde = null, DateOnly? hasta = null) =>
        new() { AsOf = corte, From = desde, To = hasta };

    // -------------------------------------------------------------------------------------------------------- siembra --

    /// <summary>Un crédito sembrado como hecho: la fila, su desembolso y (si se pide) su posición diaria al corte.</summary>
    private async Task<Loan> CreditoAsync(string numero, Person deudor, DateOnly desembolso, decimal monto, int? oficina = null, decimal? saldo = null,
        int dias = 0, DateOnly? posicion = null)
    {
        var credito = new Loan
        {
            Number = numero, DebtorPersonId = deudor.Id, DebtorKind = DebtorKind.Associate, BranchId = oficina ?? _e.Credito.BranchId,
            CreditLineId = _e.Credito.CreditLineId, Origin = LoanOrigin.Origination, Status = LoanStatus.Active, ApprovedAmount = monto, OpenedOn = desembolso,
        };
        _e.Db.Loans.Add(credito);
        await _e.Db.SaveChangesAsync();
        _e.Db.LoanOperations.Add(new LoanOperation
        {
            LoanId = credito.Id, Kind = LoanOperationKind.Disbursement, Number = $"OP-{numero}", Sequence = 1, ValueDate = desembolso, AccountingDate = desembolso,
            RegisteredAt = desembolso.ToDateTime(TimeOnly.MinValue), Amount = monto,
        });
        if (saldo is { } s)
        {
            _e.Db.LoanDailyPositions.Add(new LoanDailyPosition
            {
                LoanId = credito.Id, PositionDate = posicion ?? Hoy, Version = 1, Status = LoanStatus.Active, BranchId = credito.BranchId,
                CreditLineId = credito.CreditLineId, ModalityCode = "CONSUMO", Principal = s, DaysPastDue = dias, InputsSha256 = "x",
            });
        }
        await _e.Db.SaveChangesAsync();
        _e.Db.ChangeTracker.Clear();
        return credito;
    }

    /// <summary>Un cierre mensual vigente del mes con sus líneas (crédito, días, calificación, capital, deterioro, castigado).</summary>
    private async Task<MonthlyClose> CierreAsync(int anio, int mes, IEnumerable<(Loan Credito, int Dias, RatingCategory Cal, decimal Capital, decimal Deterioro, bool Castigado)> lineas,
        int version = 1, (int Oficina, decimal Monto)[]? generales = null)
    {
        var corrida = ++_corrida;
        var cierre = new MonthlyClose
        {
            Kind = MonthlyCloseKind.Monthly, PeriodYear = (short)anio, PeriodMonth = (byte)mes, Version = version,
            CloseDate = MonthlyClose.UltimoDiaDe(anio, mes), ProcessRunId = corrida,
        };
        _e.Db.MonthlyCloses.Add(cierre);
        await _e.Db.SaveChangesAsync();
        foreach (var l in lineas)
        {
            _e.Db.MonthlyCloseLoans.Add(new MonthlyCloseLoan
            {
                ProcessRunId = corrida, LoanId = l.Credito.Id, PersonId = l.Credito.DebtorPersonId, BranchId = l.Credito.BranchId,
                CreditLineId = l.Credito.CreditLineId, ModalityCode = "CONSUMO", SarcSegmentCode = "CONSUMO", DaysPastDueAtClose = l.Dias, FinalRating = l.Cal,
                AccountingRating = l.Cal, ArrearsRating = l.Cal, PrincipalBalance = l.Capital, TotalImpairment = l.Deterioro, IsWrittenOff = l.Castigado,
            });
        }
        foreach (var (oficina, monto) in generales ?? [])
            _e.Db.MonthlyCloseGeneralImpairments.Add(new MonthlyCloseGeneralImpairment
            {
                MonthlyCloseId = cierre.Id, BranchId = oficina, ModalityCode = "CONSUMO", TotalAmount = monto,
            });
        await _e.Db.SaveChangesAsync();
        _e.Db.ChangeTracker.Clear();
        return cierre;
    }

    private async Task ReabrirAsync(MonthlyClose cierre)
    {
        _e.Db.MonthlyCloseReopenings.Add(new MonthlyCloseReopening { MonthlyCloseId = cierre.Id, ProcessRunId = cierre.ProcessRunId ?? 0, Reason = "prueba" });
        await _e.Db.SaveChangesAsync();
        _e.Db.ChangeTracker.Clear();
    }

    // -------------------------------------------------------------------------------------------------------- quality --

    /// <summary>
    /// Noviembre de 2026: L1 (A, al día, 1.000.000, deterioro 10.000) y L2 (B, 45 días, 500.000, 50.000) en la oficina de Ana; L3 (C, 95 días, 300.000,
    /// 90.000) y L4 castigado (E, 200.000) en la otra; general 20.000 y 5.000.
    /// </summary>
    private async Task<(MonthlyClose Cierre, Loan L1, Loan L2, Loan L3)> NoviembreAsync()
    {
        var l1 = await CreditoAsync("Q-1", _e.D.O.Beto, new DateOnly(2026, 1, 15), 1_200_000m);
        var l2 = await CreditoAsync("Q-2", _e.D.O.Carla, new DateOnly(2026, 2, 15), 600_000m);
        var l3 = await CreditoAsync("Q-3", _e.D.O.Dario, new DateOnly(2026, 3, 15), 400_000m, _otra.Id);
        var l4 = await CreditoAsync("Q-4", _e.D.O.Dario, new DateOnly(2026, 3, 15), 300_000m, _otra.Id);
        var cierre = await CierreAsync(2026, 11,
        [
            (l1, 0, RatingCategory.A, 1_000_000m, 10_000m, false),
            (l2, 45, RatingCategory.B, 500_000m, 50_000m, false),
            (l3, 95, RatingCategory.C, 300_000m, 90_000m, false),
            (l4, 400, RatingCategory.E, 200_000m, 200_000m, true),
        ], generales: [(_e.Credito.BranchId, 20_000m), (_otra.Id, 5_000m)]);
        return (cierre, l1, l2, l3);
    }

    [Fact]
    public async Task Quality_toma_el_ultimo_cierre_vigente_y_cuadra_con_sus_lineas()
    {
        await NoviembreAsync();

        var t = InformesDePrueba.Tabla(await Calidad().Handle(new QualityReportQuery(Filtros()), default));

        var fila = t.Filas.Should().ContainSingle().Subject;
        V(t, fila, "Dimensión").Should().Be("Total");
        V(t, fila, "Cartera bruta").Should().Be(1_800_000m, "sin el castigado");
        V(t, fila, "Vencida").Should().Be(800_000m, "más de 30 días: 45 y 95");
        V(t, fila, "Vencida/bruta").Should().Be(44.44m);
        V(t, fila, "En riesgo (B–E)").Should().Be(800_000m);
        V(t, fila, "Riesgo/bruta").Should().Be(44.44m);
        V(t, fila, "Deterioro").Should().Be(175_000m, "150.000 individual + 25.000 general");
        V(t, fila, "Cobertura").Should().Be(21.88m, "175.000 / 800.000 = 21,875 %");
        t.Notas.Should().Contain(n => n.Contains("2026-11") && n.Contains("versión 1"));
        t.Notas.Should().Contain(n => n.Contains("a validar por la contadora"));
        t.Columnas.Should().NotContain(c => c.EsOculta, "agregada: sin datos del deudor");
    }

    [Fact]
    public async Task Quality_usa_Informes_DiasCarteraVencida_y_abre_por_oficina_linea_y_modalidad()
    {
        await NoviembreAsync();
        await _e.ParametroAsync(ParametrosDeCartera.InformesDiasCarteraVencida, "60");

        var total = InformesDePrueba.Tabla(await Calidad().Handle(new QualityReportQuery(Filtros()), default));
        V(total, total.Filas.Single(), "Vencida").Should().Be(300_000m, "con 60 días sólo cuenta la de 95");
        V(total, total.Filas.Single(), "Vencida/bruta").Should().Be(16.67m);

        var porOficina = InformesDePrueba.Tabla(await Calidad().Handle(new QualityReportQuery(Filtros(), "office"), default));
        porOficina.Filas.Should().HaveCount(2);
        var sur = porOficina.Filas.Single(f => (string)V(porOficina, f, "Dimensión")! == "Oficina Sur");
        V(porOficina, sur, "Cartera bruta").Should().Be(300_000m);
        V(porOficina, sur, "Deterioro").Should().Be(95_000m, "90.000 individual + 5.000 general de la oficina");
        V(porOficina, porOficina.Totales!, "Deterioro").Should().Be(175_000m);

        var porLinea = InformesDePrueba.Tabla(await Calidad().Handle(new QualityReportQuery(Filtros(), "line"), default));
        porLinea.Filas.Should().Contain(f => (string)V(porLinea, f, "Dimensión")! == QualityReportQueryHandler.FilaDelGeneral
                                              && (decimal)V(porLinea, f, "Deterioro")! == 25_000m, "el general no se reparte por línea");

        var porModalidad = InformesDePrueba.Tabla(await Calidad().Handle(new QualityReportQuery(Filtros(), "modality"), default));
        V(porModalidad, porModalidad.Filas.Single(), "Dimensión").Should().Be("CONSUMO");

        var invalida = await Calidad().Handle(new QualityReportQuery(Filtros(), "producto"), default);
        invalida.Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
    }

    [Fact]
    public async Task Quality_respeta_el_alcance_ignora_el_reabierto_y_sin_cierre_sale_vacia()
    {
        var sinCierre = InformesDePrueba.Tabla(await Calidad().Handle(new QualityReportQuery(Filtros()), default));
        sinCierre.Filas.Should().BeEmpty();
        sinCierre.Notas.Should().Contain(n => n.Contains("no hay un cierre"));

        var (noviembre, l1, _, _) = await NoviembreAsync();
        await ReabrirAsync(noviembre);
        var octubre = InformesDePrueba.Tabla(await Calidad().Handle(new QualityReportQuery(Filtros()), default));
        octubre.Filas.Should().BeEmpty("noviembre está reabierto y no hay otro cierre");
        await CierreAsync(2026, 11, [(l1, 0, RatingCategory.A, 999_000m, 9_990m, false)], version: 2);
        var v2 = InformesDePrueba.Tabla(await Calidad().Handle(new QualityReportQuery(Filtros()), default));
        V(v2, v2.Filas.Single(), "Cartera bruta").Should().Be(999_000m, "la versión 2 reemplaza a la reabierta");
        V(v2, v2.Filas.Single(), "Deterioro").Should().Be(9_990m);

        var antes = InformesDePrueba.Tabla(await Calidad().Handle(new QualityReportQuery(Filtros(new DateOnly(2026, 11, 29))), default));
        antes.Filas.Should().BeEmpty("al 29 de noviembre todavía no había cierre");
    }

    [Fact]
    public async Task Quality_con_alcance_de_una_oficina_solo_suma_la_suya_y_por_linea_sin_el_general()
    {
        await NoviembreAsync();

        _e.G.AlcanceActual = InformesDePrueba.SoloLaOficina(_e.Credito.BranchId);
        var oficina = InformesDePrueba.Tabla(await Calidad().Handle(new QualityReportQuery(Filtros()), default));
        V(oficina, oficina.Filas.Single(), "Cartera bruta").Should().Be(1_500_000m);
        V(oficina, oficina.Filas.Single(), "Deterioro").Should().Be(80_000m, "60.000 individual + 20.000 general de su oficina");

        _e.G.AlcanceActual = IngenIA365ERP.Application.Common.Interfaces.Security.AlcanceDeCartera.Total;
        var linea = await _e.Db.CreditLines.AsNoTracking().SingleAsync(l => l.Id == _e.Credito.CreditLineId);
        var porLinea = InformesDePrueba.Tabla(await Calidad().Handle(new QualityReportQuery(new FiltrosDeInformeDeCartera { Line = linea.PublicId }), default));
        V(porLinea, porLinea.Filas.Single(), "Deterioro").Should().Be(150_000m, "con filtro por línea sólo el individual");
        porLinea.Notas.Should().Contain(n => n.Contains("no se reparte por línea"));
    }

    // ------------------------------------------------------------------------------------------------------- vintages --

    [Fact]
    public async Task Vintages_seis_meses_de_desembolso_con_la_mora_a_30_60_y_90_contra_un_calculo_a_mano()
    {
        // Enero: dos créditos (1.000.000 y 500.000); febrero a junio, uno de 1.000.000 cada mes.
        var a = await CreditoAsync("V-ENE-A", _e.D.O.Beto, new DateOnly(2026, 1, 10), 1_000_000m);
        var b = await CreditoAsync("V-ENE-B", _e.D.O.Carla, new DateOnly(2026, 1, 20), 500_000m);
        var meses = new List<Loan>();
        for (var m = 2; m <= 6; m++) meses.Add(await CreditoAsync($"V-{m:D2}", _e.D.O.Dario, new DateOnly(2026, m, 5), 1_000_000m));
        var junio = meses[^1];

        // Cierres de febrero a noviembre. Enero: B entra en mora en febrero (35 días), sigue en marzo (65) y abril (95), A cae en marzo (31).
        await CierreAsync(2026, 2, [(a, 0, RatingCategory.A, 950_000m, 0m, false), (b, 35, RatingCategory.B, 480_000m, 0m, false)]);
        var marzo = await CierreAsync(2026, 3, [(a, 0, RatingCategory.A, 900_000m, 0m, false), (b, 65, RatingCategory.C, 480_000m, 0m, false)]);
        await ReabrirAsync(marzo);
        await CierreAsync(2026, 3, [(a, 31, RatingCategory.B, 900_000m, 0m, false), (b, 65, RatingCategory.C, 480_000m, 0m, false)], version: 2);
        await CierreAsync(2026, 4, [(a, 61, RatingCategory.C, 900_000m, 0m, false), (b, 95, RatingCategory.D, 480_000m, 0m, false)]);
        for (var m = 5; m <= 11; m++)
        {
            var lineas = new List<(Loan, int, RatingCategory, decimal, decimal, bool)>();
            if (m == 7) lineas.Add((junio, 30, RatingCategory.B, 990_000m, 0m, false));
            if (m == 8) lineas.Add((junio, 92, RatingCategory.D, 990_000m, 0m, false));
            await CierreAsync(2026, m, lineas);
        }

        var t = InformesDePrueba.Tabla(await Cosechas().Handle(new VintagesReportQuery(Filtros(desde: new DateOnly(2026, 1, 1), hasta: new DateOnly(2026, 6, 30))), default));

        t.Filas.Should().HaveCount(10 + 9 + 8 + 7 + 6 + 5, "cada cosecha tiene una fila por cada cierre posterior, hasta noviembre");
        FilaExportable Celda(string cosecha, int k) => t.Filas.Single(f => (string)V(t, f, "Mes de desembolso")! == cosecha && (int?)V(t, f, "Mes de maduración") == k);

        var feb = Celda("2026-01", 1);
        V(t, feb, "Monto").Should().Be(1_500_000m);
        V(t, feb, "Mes del cierre").Should().Be("2026-02");
        V(t, feb, "Mora 30").Should().Be(32.00m, "480.000 / 1.500.000");
        V(t, feb, "Mora 60").Should().Be(0.00m);
        V(t, feb, "Mora 90").Should().Be(0.00m);

        var mar = Celda("2026-01", 2);
        V(t, mar, "Mora 30").Should().Be(92.00m, "la versión 2 de marzo: (900.000 + 480.000) / 1.500.000");
        V(t, mar, "Mora 60").Should().Be(32.00m);
        V(t, mar, "Mora 90").Should().Be(0.00m);

        var abr = Celda("2026-01", 3);
        V(t, abr, "Mora 30").Should().Be(92.00m);
        V(t, abr, "Mora 60").Should().Be(92.00m);
        V(t, abr, "Mora 90").Should().Be(32.00m);

        V(t, Celda("2026-06", 1), "Mora 30").Should().Be(99.00m, "junio a julio: 30 días es «30 o más»: 990.000 / 1.000.000");
        V(t, Celda("2026-06", 1), "Mora 60").Should().Be(0.00m);
        V(t, Celda("2026-06", 2), "Mora 90").Should().Be(99.00m);
        V(t, Celda("2026-06", 3), "Mora 30").Should().Be(0.00m, "sin línea en el cierre de septiembre, no suma");
        V(t, Celda("2026-03", 1), "Monto").Should().Be(1_000_000m);
        t.Notas.Should().Contain(n => n.Contains("a validar por la contadora"));

        _e.G.AlcanceActual = InformesDePrueba.OtraOficina(_e.Credito.BranchId);
        var ajena = InformesDePrueba.Tabla(await Cosechas().Handle(new VintagesReportQuery(Filtros(desde: new DateOnly(2026, 1, 1), hasta: new DateOnly(2026, 6, 30))), default));
        ajena.Filas.Should().BeEmpty("los créditos son de una oficina fuera del alcance");
    }

    // -------------------------------------------------------------------------------------------------- concentration --

    [Fact]
    public async Task Concentration_por_deudor_senala_al_cercano_al_limite_y_al_que_lo_supera()
    {
        await CreditoAsync("C-BETO", _e.D.O.Beto, new DateOnly(2026, 6, 1), 90_000_000m, saldo: 85_000_000m);
        await CreditoAsync("C-CARLA", _e.D.O.Carla, new DateOnly(2026, 6, 1), 130_000_000m, saldo: 120_000_000m);

        var t = InformesDePrueba.Tabla(await Concentracion().Handle(new ConcentrationReportQuery(Filtros()), default));

        t.Filas.Select(f => V(t, f, "Dimensión")).Should().ContainInOrder(Nombre(_e.D.O.Carla), Nombre(_e.D.O.Beto), Nombre(_e.D.O.Ana));
        var beto = t.Filas.Single(f => (string)V(t, f, "Dimensión")! == Nombre(_e.D.O.Beto));
        V(t, beto, "% del límite individual").Should().Be(85.00m, "85 millones de un límite de 100 (10 % de 1.000 millones)");
        V(t, beto, "Señal").Should().Be(ConcentrationReportQueryHandler.SenalCerca);
        V(t, beto, "_persona").Should().Be(_e.D.O.Beto.PublicId.ToString());
        V(t, t.Filas.First(), "Señal").Should().Be(ConcentrationReportQueryHandler.SenalExcede);
        V(t, t.Filas.Single(f => (string)V(t, f, "Dimensión")! == Nombre(_e.D.O.Ana)), "Señal").Should().Be(string.Empty);
        V(t, beto, "Participación").Should().Be(Math.Round(85_000_000m / 208_000_000m * 100m, 2, MidpointRounding.AwayFromZero));
        t.Totales!.Valores[InformesDePrueba.Indice(t, "Saldo")].Should().Be(208_000_000m);

        var conAviso = InformesDePrueba.Tabla(await Concentracion().Handle(new ConcentrationReportQuery(Filtros(), WarnAt: "90"), default));
        V(conAviso, conAviso.Filas.Single(f => (string)V(conAviso, f, "Dimensión")! == Nombre(_e.D.O.Beto)), "Señal").Should().Be(string.Empty);
        var top = InformesDePrueba.Tabla(await Concentracion().Handle(new ConcentrationReportQuery(Filtros(), Top: "1"), default));
        top.Filas.Should().ContainSingle();

        (await Concentracion().Handle(new ConcentrationReportQuery(Filtros(), "producto"), default)).Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
        (await Concentracion().Handle(new ConcentrationReportQuery(Filtros(), Top: "0"), default)).Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
        (await Concentracion().Handle(new ConcentrationReportQuery(Filtros(), WarnAt: "150"), default)).Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
    }

    [Fact]
    public async Task Concentration_por_grupo_linea_sector_y_pagaduria()
    {
        await CreditoAsync("G-BETO", _e.D.O.Beto, new DateOnly(2026, 6, 1), 50_000_000m, saldo: 50_000_000m);
        await CreditoAsync("G-CARLA", _e.D.O.Carla, new DateOnly(2026, 6, 1), 40_000_000m, saldo: 40_000_000m, oficina: _otra.Id);
        var grupo = new ConnectedGroup { Name = "Familia Beto-Carla", Basis = ConnectedGroupBasis.EconomicLink, Grounds = "prueba", ValidFrom = new DateOnly(2026, 1, 1) };
        _e.Db.ConnectedGroups.Add(grupo);
        await _e.Db.SaveChangesAsync();
        _e.Db.ConnectedGroupMembers.Add(ConnectedGroupMember.Nuevo(grupo.Id, _e.D.O.Beto.Id, new DateOnly(2026, 1, 1)));
        _e.Db.ConnectedGroupMembers.Add(ConnectedGroupMember.Nuevo(grupo.Id, _e.D.O.Carla.Id, new DateOnly(2026, 1, 1)));
        await _e.Db.SaveChangesAsync();
        _e.Db.ChangeTracker.Clear();

        var porGrupo = InformesDePrueba.Tabla(await Concentracion().Handle(new ConcentrationReportQuery(Filtros(), "group"), default));
        var fila = porGrupo.Filas.Should().ContainSingle("Ana no tiene grupo: los grupos de una persona no se listan").Subject;
        ((string)V(porGrupo, fila, "Dimensión")!).Should().StartWith("Familia Beto-Carla");
        V(porGrupo, fila, "Saldo").Should().Be(90_000_000m);
        V(porGrupo, fila, "% del límite individual").Should().Be(90.00m);
        V(porGrupo, fila, "Señal").Should().Be(ConcentrationReportQueryHandler.SenalCerca);

        var porLinea = InformesDePrueba.Tabla(await Concentracion().Handle(new ConcentrationReportQuery(Filtros(), "line"), default));
        V(porLinea, porLinea.Filas.Single(), "Saldo").Should().Be(93_000_000m);
        V(porLinea, porLinea.Filas.Single(), "% del límite individual").Should().BeNull("el límite se mide por deudor y por grupo");
        V(porLinea, porLinea.Filas.Single(), "_persona").Should().BeNull();

        var porSector = InformesDePrueba.Tabla(await Concentracion().Handle(new ConcentrationReportQuery(Filtros(), "sector"), default));
        porSector.Filas.Should().Contain(f => (string)V(porSector, f, "Dimensión")! == ConcentrationReportQueryHandler.SinSector);

        var porPagaduria = InformesDePrueba.Tabla(await Concentracion().Handle(new ConcentrationReportQuery(Filtros(), "payer"), default));
        V(porPagaduria, porPagaduria.Filas.Single(), "Dimensión").Should().Be(ConcentrationReportQueryHandler.SinPagaduria);

        _e.G.AlcanceActual = InformesDePrueba.SoloLaOficina(_e.Credito.BranchId);
        var deUnaOficina = InformesDePrueba.Tabla(await Concentracion().Handle(new ConcentrationReportQuery(Filtros(), "group"), default));
        V(deUnaOficina, deUnaOficina.Filas.Single(), "Saldo").Should().Be(50_000_000m, "del grupo sólo suma lo del alcance");
    }

    [Fact]
    public async Task Concentration_sin_patrimonio_tecnico_vigente_sale_sin_porcentaje_y_con_la_advertencia()
    {
        _e.D.Contabilidad.IndicadoresDeLaEntidadAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success(new IndicadoresDeLaEntidadDto(ci.Arg<DateOnly>(), 0m, 0m, null, null))));

        var t = InformesDePrueba.Tabla(await Concentracion().Handle(new ConcentrationReportQuery(Filtros()), default));

        V(t, t.Filas.Single(), "% del límite individual").Should().BeNull();
        V(t, t.Filas.Single(), "Señal").Should().Be(string.Empty);
        t.Notas.Should().Contain(n => n.StartsWith("Advertencia") && n.Contains("patrimonio técnico"));
    }

    [Fact]
    public void La_concentracion_trae_datos_del_deudor_salvo_por_linea_sector_y_pagaduria()
    {
        var v = ConcentrationReportQueryHandler.Vista;
        v.PersonalData.Should().BeTrue();
        v.RequiredPermission.Should().Be(VistaDeInformeDeCartera.PermisoDeDatosDelDeudor);
        v.TraeDatosDelDeudor(null).Should().BeTrue("el defecto es por deudor");
        v.TraeDatosDelDeudor("group").Should().BeTrue();
        v.TraeDatosDelDeudor("LINE").Should().BeFalse();
        v.TraeDatosDelDeudor("sector").Should().BeFalse();
        v.TraeDatosDelDeudor("payer").Should().BeFalse();
        AgingReportQueryHandler.Vista.TraeDatosDelDeudor("line").Should().BeTrue("sin excepción la marca vale siempre");
        foreach (var vista in new[] { QualityReportQueryHandler.Vista, VintagesReportQueryHandler.Vista, v, IssuedDocumentsReportQueryHandler.Vista })
            vista.AvailableFrom.Should().Be(IngenIA365ERP.Domain.Lending.EntregaDeCartera.K5);
        IssuedDocumentsReportQueryHandler.Vista.PersonalData.Should().BeTrue();
        QualityReportQueryHandler.Vista.PersonalData.Should().BeFalse();
        VintagesReportQueryHandler.Vista.PersonalData.Should().BeFalse();
    }

    // ----------------------------------------------------------------------------------------------- issued-documents --

    [Fact]
    public async Task Issued_documents_sin_documentos_sale_vacia_y_rechaza_clase_o_estado_desconocidos()
    {
        var h = new IssuedDocumentsReportQueryHandler(_e.Db, _e.G.Alcance, _e.G.Reloj);
        var t = InformesDePrueba.Tabla(await h.Handle(new IssuedDocumentsReportQuery(Filtros()), default));
        t.Filas.Should().BeEmpty();
        t.Columnas.Select(c => c.Nombre).Should().ContainInOrder("Fecha", "Clase", "Número", "Titular", "Estado", "Código", "Emitido por", "_documento");

        (await h.Handle(new IssuedDocumentsReportQuery(Filtros(), Kind: "Receipt"), default)).Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
        (await h.Handle(new IssuedDocumentsReportQuery(Filtros(), Status: "Lost"), default)).Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
        (await h.Handle(new IssuedDocumentsReportQuery(Filtros(), Kind: "1"), default)).Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
        IssuedDocumentsReportQueryHandler.Clase("TaxCertificate").Should().Be("Certificado tributario");
        IssuedDocumentsReportQueryHandler.Estado("SubjectToFinality").Should().Be("Sujeto a firmeza");
    }
}
