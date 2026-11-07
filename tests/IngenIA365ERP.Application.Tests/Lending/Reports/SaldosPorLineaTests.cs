using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Application.Tests.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Reports;

/// <summary>
/// La vista <c>line-balances</c> antes de activar: sale de la carga aplicada, a su corte, con la cuenta que responda la matriz; lo que no tiene regla
/// queda vacío y lo dice. Es lo que se usa para cuadrar cada implantación contra el informe del sistema anterior.
/// </summary>
public sealed class SaldosPorLineaTests : IAsyncLifetime
{
    private static readonly DateOnly Corte = OriginacionDePrueba.Hoy.AddDays(-1);

    private OriginacionDePrueba _o = null!;
    private int _numero;

    public async Task InitializeAsync() => _o = await OriginacionDePrueba.CrearAsync(activada: false);

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private LineBalancesReportQueryHandler Handler()
    {
        var contabilidad = _o.Contabilidad;
        return new LineBalancesReportQueryHandler(_o.Db, _o.G.Alcance, new CargaVigenteDeCartera(_o.Db), new CuadreDeMigracion(_o.Db, contabilidad),
            new SaldosContablesDeCartera(_o.Db, contabilidad), new SaldosContablesDeCaptaciones(_o.Db), contabilidad, _o.G.Reloj);
    }

    private async Task<TablaExportable> InformeAsync()
    {
        var r = await Handler().Handle(new LineBalancesReportQuery(new FiltrosDeInformeDeCartera()), default);
        return InformesDePrueba.Tabla(r);
    }

    private void Cuentas(Func<IReadOnlyList<CombinacionDeSaldo>, Result<SaldosMapeadosDeCarteraDto>> respuesta) =>
        _o.Contabilidad.SaldosDeCuentasMapeadasAsync(Arg.Any<DateOnly>(), Arg.Any<IReadOnlyList<CombinacionDeSaldo>>(), Arg.Any<CancellationToken>())
            .Returns(c => respuesta(c.Arg<IReadOnlyList<CombinacionDeSaldo>>()));

    /// <summary>Capital a la 144205 e interés sin regla.</summary>
    private void SoloCapitalTieneRegla() => Cuentas(combinaciones =>
    {
        var capital = Enumerable.Range(0, combinaciones.Count).Where(i => combinaciones[i].Role == "Capital").ToList();
        var resto = Enumerable.Range(0, combinaciones.Count).Except(capital).ToList();
        var cuenta = new CuentaDelConjuntoDto("144205", "Consumo sin libranza", "Capital", []);
        return Result.Success(new SaldosMapeadosDeCarteraDto([new ConjuntoDeCuentasDeCarteraDto("Capital", capital, [cuenta], 0m)], resto));
    });

    private MigrationLoadPart Carga(MigrationLoadStatus estado = MigrationLoadStatus.Applied, MigrationPartStatus tanda = MigrationPartStatus.Imported)
    {
        var carga = new MigrationLoad { Number = ++_numero, CutoffDate = Corte, Status = estado, AppliedAt = _o.G.UtcNow, CreatedByUserId = _o.Asesora.Id };
        _o.Db.MigrationLoads.Add(carga);
        _o.Db.SaveChanges();
        var parte = new MigrationLoadPart { MigrationLoadId = carga.Id, PartKey = "loans", BatchNumber = 1, FileName = "c.xlsx", FileSha256 = Guid.NewGuid().ToString("N"),
            ImportedAt = _o.G.UtcNow, ImportedByUserId = _o.Asesora.Id, RowCount = 1, Status = tanda };
        _o.Db.MigrationLoadParts.Add(parte);
        _o.Db.SaveChanges();
        return parte;
    }

    private void Credito(MigrationLoadPart parte, string numero, params (LoanConcept Concepto, decimal Monto)[] saldos)
    {
        _o.Db.MigrationLoanRows.Add(new MigrationLoanRow { MigrationLoadPartId = parte.Id, RowNumber = 2, SourceLoanNumber = numero, PersonId = _o.Ana.Id,
            CreditLineCode = "CONLIB", OfficePublicId = _o.Norte.PublicId, DisbursementDate = OriginacionDePrueba.Hoy.AddMonths(-6), OriginalAmount = 1_000m });
        foreach (var (concepto, monto) in saldos)
            _o.Db.MigrationBalanceRows.Add(new MigrationBalanceRow { MigrationLoadPartId = parte.Id, RowNumber = 2, SourceLoanNumber = numero, Concept = concepto, Amount = monto });
        _o.Db.SaveChanges();
        _o.Db.ChangeTracker.Clear();
    }

    private static object? V(TablaExportable t, FilaExportable f, string columna) => InformesDePrueba.Valor(t, f, columna);

    private static FilaExportable Fila(TablaExportable t, string concepto) =>
        t.Filas.Should().ContainSingle(f => (string?)V(t, f, "Concepto") == concepto).Subject;

    [Fact]
    public async Task Antes_de_activar_sale_de_la_carga_con_el_numero_de_obligaciones_y_el_total()
    {
        SoloCapitalTieneRegla();
        var parte = Carga();
        Credito(parte, "CR-1", (LoanConcept.Principal, 100.50m), (LoanConcept.CurrentInterest, 7m));
        Credito(parte, "CR-2", (LoanConcept.Principal, 200m));

        var t = await InformeAsync();

        var capital = Fila(t, "Capital");
        capital.Seccion.Should().Be(LineBalancesReportQueryHandler.SeccionDeCreditos);
        V(t, capital, "Cuenta contable").Should().Be("144205");
        V(t, capital, "Línea").Should().Be("CONLIB");
        V(t, capital, "No. a cargo").Should().Be(2);
        V(t, capital, "Saldo a cargo").Should().Be(300.50m);
        V(t, capital, "No. a favor").Should().Be(0);

        V(t, t.Totales!, "No. a cargo").Should().Be(2, "una obligación con capital e interés cuenta una vez");
        V(t, t.Totales!, "Saldo a cargo").Should().Be(307.50m);
        t.Subtitulo.Should().Contain(Corte.ToString("yyyy-MM-dd"));
        t.Notas.Should().Contain(n => n.StartsWith("De la carga"));
        t.Notas.Should().Contain(n => n.Contains("Capital: 2 por 300,50"));
    }

    [Fact]
    public async Task Lo_que_no_tiene_regla_queda_sin_cuenta_resaltado_y_lo_dice()
    {
        SoloCapitalTieneRegla();
        Credito(Carga(), "CR-1", (LoanConcept.Principal, 100m), (LoanConcept.CurrentInterest, 7m));

        var t = await InformeAsync();

        var interes = Fila(t, "Interés corriente");
        V(t, interes, "Cuenta contable").Should().Be(string.Empty);
        interes.Resaltada.Should().BeTrue();
        Fila(t, "Capital").Resaltada.Should().BeFalse();
        t.Notas.Should().Contain(n => n.StartsWith("1 fila queda sin cuenta contable"));
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_el_informe_sale_igual_y_lo_dice()
    {
        Cuentas(_ => Result.Failure<SaldosMapeadosDeCarteraDto>(new Error("Accounting.NotInitialized", "sin iniciar")));
        Credito(Carga(), "CR-1", (LoanConcept.Principal, 100m));

        var t = await InformeAsync();

        V(t, t.Totales!, "Saldo a cargo").Should().Be(100m);
        t.Notas.Should().Contain("La contabilidad no está iniciada: la cuenta contable queda vacía.");
    }

    [Fact]
    public async Task Lo_que_es_del_asociado_va_a_favor_y_las_cuentas_de_orden_quedan_fuera_del_total()
    {
        SoloCapitalTieneRegla();
        Credito(Carga(), "CR-1", (LoanConcept.Principal, 100m), (LoanConcept.CreditBalance, 5m), (LoanConcept.CurrentInterestInSuspense, 9m));

        var t = await InformeAsync();

        var favor = Fila(t, "Saldo a favor");
        V(t, favor, "No. a favor").Should().Be(1);
        V(t, favor, "Saldo a favor").Should().Be(5m);
        V(t, favor, "Saldo a cargo").Should().Be(0m);

        Fila(t, "Interés corriente en suspenso").Seccion.Should().Be(LineBalancesReportQueryHandler.SeccionDeOrden);
        V(t, t.Totales!, "Saldo a cargo").Should().Be(100m);
        V(t, t.Totales!, "Saldo a favor").Should().Be(5m);
    }

    [Fact]
    public async Task Un_borrador_o_una_tanda_reemplazada_no_cuentan_y_sin_fuente_lo_dice()
    {
        SoloCapitalTieneRegla();
        Credito(Carga(MigrationLoadStatus.Draft), "CR-1", (LoanConcept.Principal, 100m));

        var sinFuente = await InformeAsync();
        sinFuente.Filas.Should().BeEmpty();
        sinFuente.Notas.Should().Contain(LineBalancesReportQueryHandler.NotaSinFuente);

        Credito(Carga(tanda: MigrationPartStatus.Superseded), "CR-2", (LoanConcept.Principal, 100m));
        (await InformeAsync()).Filas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_alcance_parcial_recorta_la_carga()
    {
        SoloCapitalTieneRegla();
        Credito(Carga(), "CR-1", (LoanConcept.Principal, 100m));
        _o.G.AlcanceActual = InformesDePrueba.OtraOficina(_o.Norte.Id);

        (await InformeAsync()).Filas.Should().BeEmpty();
    }

    [Fact]
    public async Task Los_aportes_de_su_carga_van_a_favor_del_asociado_y_en_su_seccion()
    {
        SoloCapitalTieneRegla();
        var carga = new MigrationLoad { Number = ++_numero, CutoffDate = Corte, Status = MigrationLoadStatus.Applied, AppliedAt = _o.G.UtcNow, CreatedByUserId = _o.Asesora.Id,
            Nature = LineNature.Contribution };
        _o.Db.MigrationLoads.Add(carga);
        _o.Db.SaveChanges();
        var parte = new MigrationLoadPart { MigrationLoadId = carga.Id, PartKey = DefinicionesDeMigracionDeCaptaciones.Partes.ContributionAccounts, BatchNumber = 1, FileName = "a.xlsx",
            FileSha256 = Guid.NewGuid().ToString("N"), ImportedAt = _o.G.UtcNow, ImportedByUserId = _o.Asesora.Id, RowCount = 1, Status = MigrationPartStatus.Imported };
        _o.Db.MigrationLoadParts.Add(parte);
        _o.Db.SaveChanges();
        _o.Db.MigrationContributionAccountRows.Add(new MigrationContributionAccountRow { MigrationLoadPartId = parte.Id, RowNumber = 2, PersonId = _o.Ana.Id, CreditLineCode = "APORTES",
            OfficePublicId = _o.Norte.PublicId, OpenedOn = Corte.AddYears(-1), OrdinaryBalance = 500m, RevaluedBalance = 50m });
        _o.Db.SaveChanges();
        _o.Db.ChangeTracker.Clear();

        var t = await InformeAsync();

        var ordinarios = Fila(t, "Aportes ordinarios");
        ordinarios.Seccion.Should().Be(LineBalancesReportQueryHandler.SeccionDeAportes);
        V(t, ordinarios, "No. a favor").Should().Be(1);
        V(t, ordinarios, "Saldo a favor").Should().Be(500m);
        V(t, t.Totales!, "No. a favor").Should().Be(1, "es una sola cuenta aunque tenga dos clases de aporte");
        V(t, t.Totales!, "Saldo a favor").Should().Be(550m);
    }

    [Fact]
    public async Task Activada_ya_no_lee_la_carga_sino_el_libro()
    {
        SoloCapitalTieneRegla();
        Credito(Carga(), "CR-1", (LoanConcept.Principal, 100m));
        _o.Activar();
        _o.Db.ChangeTracker.Clear();

        var t = await InformeAsync();

        t.Filas.Should().BeEmpty("el libro de esta cooperativa no tiene movimientos");
        t.Notas.Should().Contain(n => n.StartsWith("Del libro: créditos"));
    }

    [Fact]
    public async Task Activada_suma_el_libro_hasta_el_corte()
    {
        using var e = await InformesDePrueba.CrearAsync();
        e.D.Contabilidad.SaldosDeCuentasMapeadasAsync(Arg.Any<DateOnly>(), Arg.Any<IReadOnlyList<CombinacionDeSaldo>>(), Arg.Any<CancellationToken>())
            .Returns(c => Result.Success(new SaldosMapeadosDeCarteraDto([], Enumerable.Range(0, c.Arg<IReadOnlyList<CombinacionDeSaldo>>().Count).ToList())));
        var handler = new LineBalancesReportQueryHandler(e.Db, e.G.Alcance, new CargaVigenteDeCartera(e.Db), new CuadreDeMigracion(e.Db, e.D.Contabilidad),
            new SaldosContablesDeCartera(e.Db, e.D.Contabilidad), new SaldosContablesDeCaptaciones(e.Db), e.D.Contabilidad, e.G.Reloj);

        var t = InformesDePrueba.Tabla(await handler.Handle(new LineBalancesReportQuery(new FiltrosDeInformeDeCartera { AsOf = InformesDePrueba.Hoy }), default));

        var capital = Fila(t, "Capital");
        V(t, capital, "Línea").Should().Be("CONLIB");
        V(t, capital, "No. a cargo").Should().Be(1);
        V(t, capital, "Saldo a cargo").Should().Be(3_000_000m);

        var antes = InformesDePrueba.Tabla(await handler.Handle(new LineBalancesReportQuery(new FiltrosDeInformeDeCartera { AsOf = InformesDePrueba.Hoy.AddDays(-1) }), default));
        antes.Filas.Should().BeEmpty("el crédito se desembolsó hoy");
    }

    [Fact]
    public void No_trae_datos_personales()
    {
        var vista = LineBalancesReportQueryHandler.Vista;
        vista.PersonalData.Should().BeFalse();
        vista.RequiredPermission.Should().BeNull();
        LineBalancesReportQueryHandler.Columnas.Select(c => c.Nombre).Should().NotContain(["Deudor", "Titular", "Documento"]);
    }
}
