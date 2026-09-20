using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Budgets;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Reports;

/// <summary>
/// T140–T141 — US9: la ejecución presupuestal compara el mes y el acumulado enero..mes contra la
/// versión vigente (y trae la inicial), con el ejecutado neto por naturaleza, la variación y el
/// porcentaje, agregado hacia arriba por la jerarquía del plan; respeta el alcance de las líneas
/// presupuestadas por sucursal y el de quien consulta; sin presupuesto, las columnas van en cero.
/// </summary>
public class BudgetExecutionQueryTests
{
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public AccountingAuditEmitter Emisor { get; }
        public IAuditAppendOnlyWriter Auditoria { get; } = Substitute.For<IAuditAppendOnlyWriter>();

        public ChartOfAccount Clase5 { get; }
        public ChartOfAccount Grupo51 { get; }
        public ChartOfAccount Cuenta5105 { get; }
        public ChartOfAccount Sub510506 { get; }
        public ChartOfAccount Gasto1 { get; }
        public ChartOfAccount Gasto2 { get; }
        public ChartOfAccount Clase4 { get; }
        public ChartOfAccount Ingreso { get; }
        public ChartOfAccount Caja { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Auditoria, D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance);
            Clase5 = Agrupacion("5", 1, null);
            Grupo51 = Agrupacion("51", 2, Clase5);
            Cuenta5105 = Agrupacion("5105", 3, Grupo51);
            Sub510506 = Agrupacion("510506", 4, Cuenta5105);
            Gasto1 = Movimiento("5105061", Sub510506);
            Gasto2 = Movimiento("5105062", Sub510506);
            Clase4 = Agrupacion("4", 1, null, AccountNature.Credit);
            Ingreso = Movimiento("4135051", Clase4, AccountNature.Credit);
            Caja = D.Cuenta("1105051");
            // Febrero está cerrado en el escenario base; aquí se abre para poder fechar movimientos antes del mes consultado.
            var febrero = D.Db.AccountingPeriods.Single(p => p.Month == 2);
            febrero.Status = PeriodStatus.Open;
            D.Db.SaveChanges();
        }

        private ChartOfAccount Agrupacion(string code, byte level, ChartOfAccount? padre, AccountNature nature = AccountNature.Debit)
        {
            var c = D.Cuenta(code, nature, movimiento: false);
            c.Level = level;
            c.ParentId = padre?.Id;
            D.Db.SaveChanges();
            return c;
        }

        private ChartOfAccount Movimiento(string code, ChartOfAccount padre, AccountNature nature = AccountNature.Debit)
        {
            var c = D.Cuenta(code, nature);
            c.ParentId = padre.Id;
            D.Db.SaveChanges();
            return c;
        }

        public BudgetExecutionQueryHandler Consulta() => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);
        public CreateBudgetCommandHandler Creador() => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);
        public ApproveBudgetCommandHandler Aprobador() => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);
        public UpdateBudgetCommandHandler Modificador() => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);

        public static decimal[] Meses(params (int Mes, decimal Valor)[] porMes)
        {
            var montos = new decimal[12];
            foreach (var (mes, valor) in porMes) montos[mes - 1] = valor;
            return montos;
        }

        /// <summary>Un CG contabilizado: débito a <paramref name="debito"/>, crédito a <paramref name="credito"/>, en la sucursal indicada (la principal si no viene).</summary>
        public async Task ContabilizarAsync(ChartOfAccount debito, ChartOfAccount credito, decimal valor, DateOnly fecha, int? sucursalId = null)
        {
            var r = await D.Poster.PrepareAsync(new PostingRequest("CG", fecha, "Prueba", ContabilidadTestData.Manual(),
            [
                PostingLine.Debito(debito.Id, valor) with { BranchId = sucursalId },
                PostingLine.Credito(credito.Id, valor) with { BranchId = sucursalId },
            ]), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error?.Message);
            await D.Db.SaveChangesAsync();
        }

        /// <summary>Presupuesto aprobado: Gasto1 100 en enero, febrero y marzo; Gasto2 50 en marzo sólo en Norte; Ingreso 500 en marzo.</summary>
        public async Task PresupuestoAprobadoAsync()
        {
            var r = await Creador().Handle(new CreateBudgetCommand(2026,
            [
                new BudgetLineInput(Gasto1.PublicId, null, null, Meses((1, 100m), (2, 100m), (3, 100m))),
                new BudgetLineInput(Gasto2.PublicId, D.Norte.PublicId, null, Meses((3, 50m))),
                new BudgetLineInput(Ingreso.PublicId, null, null, Meses((3, 500m))),
            ]), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error?.Message);
            (await Aprobador().Handle(new ApproveBudgetCommand(2026), CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        /// <summary>
        /// Movimientos: Gasto1 80 en febrero y 120 en marzo (principal) más 30 en marzo en Norte;
        /// Gasto2 10 en marzo en la principal y 5 en Norte; Ingreso 400 en marzo.
        /// </summary>
        public async Task MovimientosAsync()
        {
            await ContabilizarAsync(Gasto1, Caja, 80m, new DateOnly(2026, 2, 10));
            await ContabilizarAsync(Gasto1, Caja, 120m, new DateOnly(2026, 3, 5));
            await ContabilizarAsync(Gasto1, Caja, 30m, new DateOnly(2026, 3, 8), D.Norte.Id);
            await ContabilizarAsync(Gasto2, Caja, 10m, new DateOnly(2026, 3, 9));
            await ContabilizarAsync(Gasto2, Caja, 5m, new DateOnly(2026, 3, 9), D.Norte.Id);
            await ContabilizarAsync(Caja, Ingreso, 400m, ContabilidadTestData.Marzo15);
        }
    }

    private static FilaExportable Fila(TablaExportable t, string codigo) => t.Filas.Single(f => (string)f.Valores[0]! == codigo);
    private static object? Celda(TablaExportable t, string codigo, string columna) => Fila(t, codigo).Valores[t.Columnas.ToList().FindIndex(c => c.Nombre == columna)];
    private static object? Oculta(TablaExportable t, string codigo, string clave) => Fila(t, codigo).Valores[t.Columnas.ToList().FindIndex(c => c.Clave == clave)];

    [Fact]
    public async Task Mes_y_acumulado_con_variacion_y_porcentaje_y_agregacion_hasta_la_clase()
    {
        var e = new Escenario();
        await e.PresupuestoAprobadoAsync();
        await e.MovimientosAsync();

        var r = await e.Consulta().Handle(new BudgetExecutionQuery(2026, 3, new FiltrosDeInforme()), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        var t = r.Value;
        t.Columnas.Select(c => c.Nombre).Should().StartWith(["Código", "Nombre", "Ppto. mes", "Ejec. mes", "Var. mes", "% mes", "Ppto. acum.", "Ejec. acum.", "Var. acum.", "% acum.", "Ppto. inicial acum."]);
        t.Columnas.Where(c => c.EsOculta).Select(c => c.Clave).Should().Equal("_nodo", "_cuenta");
        t.Columnas.Single(c => c.Nombre == "% mes").Tipo.Should().Be(TipoDeColumna.Porcentaje);

        // Gasto1: ppto mes 100, ejecutado 150 (120 + 30 Norte: la línea no tiene sucursal, cuenta todo); acumulado ppto 300, ejec 230.
        Celda(t, "5105061", "Ppto. mes").Should().Be(100m);
        Celda(t, "5105061", "Ejec. mes").Should().Be(150m);
        Celda(t, "5105061", "Var. mes").Should().Be(50m);
        Celda(t, "5105061", "% mes").Should().Be(150m);
        Celda(t, "5105061", "Ppto. acum.").Should().Be(300m);
        Celda(t, "5105061", "Ejec. acum.").Should().Be(230m);
        Celda(t, "5105061", "Var. acum.").Should().Be(-70m);
        Celda(t, "5105061", "% acum.").Should().Be(76.67m);
        Celda(t, "5105061", "Ppto. inicial acum.").Should().Be(300m);
        Oculta(t, "5105061", "_nodo").Should().Be("account:5105061");
        Oculta(t, "5105061", "_cuenta").Should().Be(e.Gasto1.PublicId);

        // Gasto2: su única línea es de Norte → sólo cuenta el movimiento de Norte (5), no el de la principal (10).
        Celda(t, "5105062", "Ppto. mes").Should().Be(50m);
        Celda(t, "5105062", "Ejec. mes").Should().Be(5m);
        Celda(t, "5105062", "% mes").Should().Be(10m);

        // Ingreso: naturaleza crédito → el crédito de 400 es ejecución positiva.
        Celda(t, "4135051", "Ejec. mes").Should().Be(400m);
        Celda(t, "4135051", "Var. mes").Should().Be(-100m);
        Celda(t, "4135051", "% mes").Should().Be(80m);

        // Agregación: subcuenta, cuenta, grupo y clase suman lo de sus hijas.
        foreach (var padre in new[] { "510506", "5105", "51", "5" })
        {
            Celda(t, padre, "Ppto. mes").Should().Be(150m, padre);
            Celda(t, padre, "Ejec. mes").Should().Be(155m, padre);
            Celda(t, padre, "Ppto. acum.").Should().Be(350m, padre);
            Celda(t, padre, "Ejec. acum.").Should().Be(235m, padre);
            Celda(t, padre, "% acum.").Should().Be(67.14m, padre);
        }
        Fila(t, "5").Resaltada.Should().BeTrue();
        Fila(t, "5105061").Seccion.Should().Be("5 Cuenta 5");
        t.Filas.Select(f => (string)f.Valores[0]!).Should().BeInAscendingOrder(StringComparer.Ordinal);

        // La caja no tiene presupuesto pero sí movimiento: aparece con presupuesto cero y porcentaje vacío.
        Celda(t, "1105051", "Ppto. mes").Should().Be(0m);
        Celda(t, "1105051", "% mes").Should().BeNull();
        Celda(t, "1105051", "Ejec. mes").Should().Be(400m - 120m - 30m - 10m - 5m);
        t.Notas.Should().Contain(n => n.Contains("versión 1") && n.Contains("aprobado"));
    }

    [Fact]
    public async Task El_presupuesto_inicial_es_la_version_1_y_el_vigente_la_ultima()
    {
        var e = new Escenario();
        await e.PresupuestoAprobadoAsync();
        var cambio = await e.Modificador().Handle(new UpdateBudgetCommand(2026,
            [new BudgetLineInput(e.Gasto1.PublicId, null, null, Escenario.Meses((1, 100m), (2, 100m), (3, 200m)))], "Más gasto en marzo"), CancellationToken.None);
        cambio.IsSuccess.Should().BeTrue(cambio.Error?.Message);

        var r = await e.Consulta().Handle(new BudgetExecutionQuery(2026, 3, new FiltrosDeInforme()), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        Celda(r.Value, "5105061", "Ppto. mes").Should().Be(200m);
        Celda(r.Value, "5105061", "Ppto. acum.").Should().Be(400m);
        Celda(r.Value, "5105061", "Ppto. inicial acum.").Should().Be(300m);
        // La versión 2 ya no presupuesta el ingreso: su fila sigue porque la inicial sí lo traía, y eso es lo que la columna muestra.
        Celda(r.Value, "4135051", "Ppto. acum.").Should().Be(0m);
        Celda(r.Value, "4135051", "Ppto. inicial acum.").Should().Be(500m);
        r.Value.Notas.Should().Contain(n => n.Contains("versión 2"));
    }

    [Fact]
    public async Task Un_mes_anterior_no_arrastra_lo_de_meses_posteriores()
    {
        var e = new Escenario();
        await e.PresupuestoAprobadoAsync();
        await e.MovimientosAsync();

        var r = await e.Consulta().Handle(new BudgetExecutionQuery(2026, 2, new FiltrosDeInforme()), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        Celda(r.Value, "5105061", "Ppto. mes").Should().Be(100m);
        Celda(r.Value, "5105061", "Ejec. mes").Should().Be(80m);
        Celda(r.Value, "5105061", "Ppto. acum.").Should().Be(200m);
        Celda(r.Value, "5105061", "Ejec. acum.").Should().Be(80m);
        r.Value.Filas.Should().NotContain(f => (string)f.Valores[0]! == "4135051", "el ingreso sólo tiene presupuesto y movimiento en marzo");
    }

    [Fact]
    public async Task El_alcance_de_sucursal_de_quien_consulta_recorta_el_ejecutado()
    {
        var e = new Escenario();
        await e.PresupuestoAprobadoAsync();
        await e.MovimientosAsync();
        e.D.RestringirA(e.D.Norte);

        var r = await e.Consulta().Handle(new BudgetExecutionQuery(2026, 3, new FiltrosDeInforme()), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        Celda(r.Value, "5105061", "Ejec. mes").Should().Be(30m, "sólo el movimiento de Norte");
        Celda(r.Value, "5105061", "Ppto. mes").Should().Be(100m, "el presupuesto sin sucursal se sigue viendo");
        Celda(r.Value, "5105062", "Ejec. mes").Should().Be(5m);
        r.Value.Filas.Should().NotContain(f => (string)f.Valores[0]! == "4135051" && (decimal)f.Valores[3]! != 0m, "el ingreso se movió en la principal");
        r.Value.Notas.Should().Contain(n => n.Contains("sucursales asignadas"));
    }

    [Fact]
    public async Task El_filtro_de_sucursal_compara_las_lineas_de_esa_sucursal_con_su_movimiento()
    {
        var e = new Escenario();
        await e.PresupuestoAprobadoAsync();
        await e.MovimientosAsync();

        var r = await e.Consulta().Handle(new BudgetExecutionQuery(2026, 3, new FiltrosDeInforme { Branch = e.D.Norte.PublicId }), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        Celda(r.Value, "5105062", "Ppto. mes").Should().Be(50m);
        Celda(r.Value, "5105062", "Ejec. mes").Should().Be(5m);
        Celda(r.Value, "5105061", "Ppto. mes").Should().Be(0m, "Gasto1 no tiene línea para Norte");
        Celda(r.Value, "5105061", "Ejec. mes").Should().Be(30m);
        r.Value.Notas.Should().Contain(n => n.Contains("sucursal Norte"));
    }

    [Fact]
    public async Task Sin_presupuesto_las_columnas_presupuestadas_van_en_cero_y_una_nota_lo_dice()
    {
        var e = new Escenario();
        await e.MovimientosAsync();

        var r = await e.Consulta().Handle(new BudgetExecutionQuery(2026, 3, new FiltrosDeInforme()), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        Celda(r.Value, "5105061", "Ppto. mes").Should().Be(0m);
        Celda(r.Value, "5105061", "Ejec. mes").Should().Be(150m);
        Celda(r.Value, "5105061", "% mes").Should().BeNull();
        Celda(r.Value, "5105061", "Ppto. inicial acum.").Should().Be(0m);
        r.Value.Notas.Should().Contain(n => n.Contains("no tiene presupuesto"));
    }

    [Fact]
    public async Task El_nivel_recorta_las_filas_y_la_exportacion_queda_en_la_auditoria()
    {
        var e = new Escenario();
        await e.PresupuestoAprobadoAsync();
        await e.MovimientosAsync();

        var r = await e.Consulta().Handle(new BudgetExecutionQuery(2026, 3, new FiltrosDeInforme { Level = 2, Format = "xlsx" }), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Filas.Select(f => (string)f.Valores[0]!).Should().BeEquivalentTo("4", "5", "51");
        Celda(r.Value, "5", "Ejec. acum.").Should().Be(235m);
        await e.Auditoria.Received(1).AppendAsync(
            Arg.Is<AuditEventDocument>(a => a.Action == "Accounting.Report.Exported" && a.NewValuesJson!.Contains("budget-execution") && a.NewValuesJson.Contains("xlsx")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_exportar_no_se_audita_y_una_sucursal_inexistente_falla_con_su_codigo()
    {
        var e = new Escenario();

        var json = await e.Consulta().Handle(new BudgetExecutionQuery(2026, 3, new FiltrosDeInforme()), CancellationToken.None);
        var mala = await e.Consulta().Handle(new BudgetExecutionQuery(2026, 3, new FiltrosDeInforme { Branch = Guid.NewGuid() }), CancellationToken.None);

        json.IsSuccess.Should().BeTrue();
        json.Value.Filas.Should().BeEmpty();
        await e.Auditoria.DidNotReceive().AppendAsync(Arg.Any<AuditEventDocument>(), Arg.Any<CancellationToken>());
        mala.Error.Code.Should().Be("Accounting.Branch.NotFound");
    }

    [Fact]
    public async Task El_alcance_de_sucursal_tambien_recorta_el_presupuestado_y_deja_ver_el_de_la_empresa()
    {
        var e = new Escenario();
        // Gasto2 presupuestado en Norte (50) y en la Principal (70); Gasto1 sin sucursal (de la empresa).
        var r0 = await e.Creador().Handle(new CreateBudgetCommand(2026,
        [
            new BudgetLineInput(e.Gasto1.PublicId, null, null, Escenario.Meses((3, 100m))),
            new BudgetLineInput(e.Gasto2.PublicId, e.D.Norte.PublicId, null, Escenario.Meses((3, 50m))),
            new BudgetLineInput(e.Gasto2.PublicId, e.D.Principal.PublicId, null, Escenario.Meses((3, 70m))),
        ]), CancellationToken.None);
        r0.IsSuccess.Should().BeTrue(r0.Error?.Message);
        await e.MovimientosAsync();
        e.D.RestringirA(e.D.Norte);

        var r = await e.Consulta().Handle(new BudgetExecutionQuery(2026, 3, new FiltrosDeInforme()), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        // Antes del 2026-09-20 «Ppto. mes» de Gasto2 salía 120 (Norte + Principal) con ejecución sólo de Norte: un informe falso.
        Celda(r.Value, "5105062", "Ppto. mes").Should().Be(50m, "sólo la línea de Norte; la de la Principal no es de este usuario");
        Celda(r.Value, "5105062", "Ejec. mes").Should().Be(5m);
        Celda(r.Value, "5105061", "Ppto. mes").Should().Be(100m, "la línea de la empresa (sin sucursal) se sigue viendo");
        Celda(r.Value, "5105061", "Ejec. mes").Should().Be(30m);
        Celda(r.Value, "510506", "Ppto. mes").Should().Be(150m, "hacia arriba se suma sólo lo que se ve");
    }
}
