using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Reports;

/// <summary>
/// T101 (feature 009 E2, FR-047): los cuatro estados financieros se arman sobre los rubros NIIF
/// del grupo de la empresa. El ESF cuadra con el resultado del ejercicio inyectado, el ERI
/// subtotaliza por rubros, los cuatro comparan con un año atrás, el ECP y el EFE se alimentan del
/// ESF con mapeos fijos y la «Diferencia» del EFE es cero; las cuentas de orden quedan fuera del
/// cuadre, una cuenta sin rubro no se pierde, cada cuenta se mide por el lado que su rubro espera
/// y el alcance de sucursal se respeta.
/// </summary>
public class EstadosFinancierosQueriesTests
{
    private static readonly DateOnly Marzo5 = new(2026, 3, 5);
    private static readonly DateOnly Marzo10 = new(2026, 3, 10);
    private static readonly DateOnly Marzo15 = ContabilidadTestData.Marzo15;
    private static readonly DateOnly Marzo20 = ContabilidadTestData.Hoy;

    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public IAuditAppendOnlyWriter Audit { get; } = Substitute.For<IAuditAppendOnlyWriter>();
        public AccountingAuditEmitter Emisor { get; }

        public ChartOfAccount Caja { get; }
        public ChartOfAccount Cxc { get; }
        public ChartOfAccount Cxp { get; }
        public ChartOfAccount Capital { get; }
        public ChartOfAccount Excedente { get; }
        public ChartOfAccount Ingreso { get; }
        public ChartOfAccount Devolucion { get; }
        public ChartOfAccount Gasto { get; }
        public ChartOfAccount Impuesto { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Audit, D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance);
            SembrarRubros();
            Caja = Cuenta("110505", AccountNature.Debit, "ESF-A-EFE");
            Cxc = Cuenta("160505", AccountNature.Debit, "ESF-A-CXC");
            Cxp = Cuenta("240505", AccountNature.Credit, "ESF-P-CXP");
            Capital = Cuenta("310505", AccountNature.Credit, "ESF-PT-CAP");
            Excedente = Cuenta("350505", AccountNature.Credit, "ESF-PT-REJ");
            Ingreso = Cuenta("410505", AccountNature.Credit, "ERI-ING");
            Devolucion = Cuenta("417505", AccountNature.Debit, "ERI-ING-DEV");
            Gasto = Cuenta("510505", AccountNature.Debit, "ERI-GAD");
            Impuesto = Cuenta("523505", AccountNature.Debit, "ERI-IMP");
        }

        /// <summary>Los rubros que estas pruebas necesitan, copiados de rubros-niif.json para el grupo 2 (el del fixture).</summary>
        private void SembrarRubros()
        {
            (string Code, string Name, FinancialStatementKind Statement, string Section, int Order, short Sign, string? Parent)[] rubros =
            [
                ("ESF-A", "Activo", FinancialStatementKind.FinancialPosition, "Activo", 100, 1, null),
                ("ESF-A-EFE", "Efectivo y equivalentes al efectivo", FinancialStatementKind.FinancialPosition, "Activo", 110, 1, "ESF-A"),
                ("ESF-A-CAR", "Cartera de créditos", FinancialStatementKind.FinancialPosition, "Activo", 130, 1, "ESF-A"),
                ("ESF-A-CAR-DET", "Deterioro de cartera de créditos", FinancialStatementKind.FinancialPosition, "Activo", 131, -1, "ESF-A-CAR"),
                ("ESF-A-CXC", "Cuentas comerciales y otras cuentas por cobrar", FinancialStatementKind.FinancialPosition, "Activo", 140, 1, "ESF-A"),
                ("ESF-A-PPE", "Propiedades, planta y equipo", FinancialStatementKind.FinancialPosition, "Activo", 160, 1, "ESF-A"),
                ("ESF-P", "Pasivo", FinancialStatementKind.FinancialPosition, "Pasivo", 200, 1, null),
                ("ESF-P-CXP", "Cuentas comerciales y otras cuentas por pagar", FinancialStatementKind.FinancialPosition, "Pasivo", 230, 1, "ESF-P"),
                ("ESF-PT", "Patrimonio", FinancialStatementKind.FinancialPosition, "Patrimonio", 300, 1, null),
                ("ESF-PT-CAP", "Capital social", FinancialStatementKind.FinancialPosition, "Patrimonio", 310, 1, "ESF-PT"),
                ("ESF-PT-RAC", "Resultados acumulados", FinancialStatementKind.FinancialPosition, "Patrimonio", 350, 1, "ESF-PT"),
                ("ESF-PT-REJ", "Resultado del ejercicio", FinancialStatementKind.FinancialPosition, "Patrimonio", 360, 1, "ESF-PT"),
                ("ORD", "Cuentas de orden", FinancialStatementKind.FinancialPosition, "Cuentas de orden", 900, 1, null),
                ("ORD-DEU", "Cuentas de orden deudoras", FinancialStatementKind.FinancialPosition, "Cuentas de orden", 910, 1, "ORD"),
                ("ORD-ACR", "Cuentas de orden acreedoras", FinancialStatementKind.FinancialPosition, "Cuentas de orden", 920, 1, "ORD"),
                ("ERI-ING", "Ingresos de actividades ordinarias", FinancialStatementKind.IncomeStatement, "Ingresos", 100, 1, null),
                ("ERI-ING-DEV", "Devoluciones, rebajas y descuentos", FinancialStatementKind.IncomeStatement, "Ingresos", 101, -1, "ERI-ING"),
                ("ERI-COS", "Costo de ventas y de prestación de servicios", FinancialStatementKind.IncomeStatement, "Costos", 200, 1, null),
                ("ERI-GAD", "Gastos de administración", FinancialStatementKind.IncomeStatement, "Gastos", 300, 1, null),
                ("ERI-DET", "Deterioro, depreciación y amortización", FinancialStatementKind.IncomeStatement, "Gastos", 320, 1, null),
                ("ERI-OING", "Otros ingresos", FinancialStatementKind.IncomeStatement, "Otros resultados", 400, 1, null),
                ("ERI-FIN", "Gastos financieros", FinancialStatementKind.IncomeStatement, "Otros resultados", 420, 1, null),
                ("ERI-IMP", "Impuesto a las ganancias", FinancialStatementKind.IncomeStatement, "Impuestos", 500, 1, null),
                ("ERI-ORI", "Otro resultado integral del período", FinancialStatementKind.IncomeStatement, "Otro resultado integral", 600, 1, null),
                ("ERI-CIE", "Cierre del ejercicio", FinancialStatementKind.IncomeStatement, "Cierre", 900, 1, null),
                ("ECP-CAP", "Capital social", FinancialStatementKind.EquityChanges, "Patrimonio", 100, 1, null),
                ("ECP-RAC", "Resultados acumulados", FinancialStatementKind.EquityChanges, "Patrimonio", 500, 1, null),
                ("ECP-REJ", "Resultado del ejercicio", FinancialStatementKind.EquityChanges, "Patrimonio", 600, 1, null),
                ("EFE-OPE", "Actividades de operación", FinancialStatementKind.CashFlow, "Operación", 100, 1, null),
                ("EFE-OPE-RES", "Resultado del ejercicio", FinancialStatementKind.CashFlow, "Operación", 110, 1, "EFE-OPE"),
                ("EFE-OPE-AJU", "Ajustes por partidas que no afectan el efectivo", FinancialStatementKind.CashFlow, "Operación", 120, 1, "EFE-OPE"),
                ("EFE-OPE-CAR", "Cambios en cartera y cuentas por cobrar", FinancialStatementKind.CashFlow, "Operación", 130, -1, "EFE-OPE"),
                ("EFE-OPE-CXP", "Cambios en cuentas por pagar y otros pasivos", FinancialStatementKind.CashFlow, "Operación", 160, 1, "EFE-OPE"),
                ("EFE-INV", "Actividades de inversión", FinancialStatementKind.CashFlow, "Inversión", 200, 1, null),
                ("EFE-INV-PPE", "Propiedades, planta, equipo e intangibles", FinancialStatementKind.CashFlow, "Inversión", 210, -1, "EFE-INV"),
                ("EFE-FIN", "Actividades de financiación", FinancialStatementKind.CashFlow, "Financiación", 300, 1, null),
                ("EFE-FIN-CAP", "Aportes sociales y patrimonio", FinancialStatementKind.CashFlow, "Financiación", 320, 1, "EFE-FIN"),
                ("EFE-EFE", "Efectivo y equivalentes", FinancialStatementKind.CashFlow, "Efectivo", 400, 1, null),
            ];
            foreach (var r in rubros)
                D.Db.FinancialStatementItems.Add(new FinancialStatementItem
                {
                    NiifGroup = 2, Code = r.Code, Name = r.Name, Statement = r.Statement, Section = r.Section, Order = r.Order, Sign = r.Sign, ParentCode = r.Parent, CreatedBy = "test",
                });
            D.Db.SaveChanges();
        }

        public ChartOfAccount Cuenta(string code, AccountNature nature, string rubro)
        {
            var cuenta = D.Cuenta(code, nature);
            cuenta.NiifItemCode = rubro;
            D.Db.SaveChanges();
            return cuenta;
        }

        /// <summary>Contabiliza un comprobante cuadrado por el contrato y lo guarda.</summary>
        public async Task Contabilizar(DateOnly fecha, (ChartOfAccount Cuenta, decimal Debito, decimal Credito)[] lineas,
            int? sucursalId = null, DocumentKind kind = DocumentKind.Regular, string tipo = "CG")
        {
            var lines = lineas.Select(l => new PostingLine { AccountId = l.Cuenta.Id, Debit = l.Debito, Credit = l.Credito, BranchId = sucursalId, Detail = "Prueba" }).ToList();
            var r = await D.Poster.PrepareAsync(new PostingRequest(tipo, fecha, "Prueba", ContabilidadTestData.Manual(), lines, kind), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error.Message);
            await D.Db.SaveChangesAsync();
            D.Db.DescartarCambios();
        }

        /// <summary>El escenario base: aporte inicial y, dentro del rango 10–20 de marzo, ventas, cobro y gastos.</summary>
        public async Task DatosBase()
        {
            await Contabilizar(Marzo5, [(Caja, 1000m, 0m), (Capital, 0m, 1000m)]);
            await Contabilizar(Marzo15, [(Caja, 500m, 0m), (Ingreso, 0m, 500m)]);
            await Contabilizar(Marzo15, [(Cxc, 300m, 0m), (Ingreso, 0m, 300m)]);
            await Contabilizar(Marzo15, [(Gasto, 200m, 0m), (Caja, 0m, 200m)]);
            await Contabilizar(Marzo15, [(Gasto, 100m, 0m), (Cxp, 0m, 100m)]);
        }

        public Task<TablaExportable> Esf(FiltrosDeInforme? f = null) => Ejecutar(new FinancialPositionQueryHandler(D.Db, D.Alcance, D.Clock, D.User, Emisor).Handle(new FinancialPositionQuery(f ?? Rango()), CancellationToken.None));
        public Task<TablaExportable> Eri(FiltrosDeInforme? f = null) => Ejecutar(new IncomeStatementQueryHandler(D.Db, D.Alcance, D.Clock, D.User, Emisor).Handle(new IncomeStatementQuery(f ?? Rango()), CancellationToken.None));
        public Task<TablaExportable> Ecp(FiltrosDeInforme? f = null) => Ejecutar(new EquityChangesQueryHandler(D.Db, D.Alcance, D.Clock, D.User, Emisor).Handle(new EquityChangesQuery(f ?? Rango()), CancellationToken.None));
        public Task<TablaExportable> Efe(FiltrosDeInforme? f = null) => Ejecutar(new CashFlowQueryHandler(D.Db, D.Alcance, D.Clock, D.User, Emisor).Handle(new CashFlowQuery(f ?? Rango()), CancellationToken.None));

        private static async Task<TablaExportable> Ejecutar(Task<IngenIA365ERP.Application.Common.Models.Result<TablaExportable>> tarea)
        {
            var r = await tarea;
            r.IsSuccess.Should().BeTrue(r.Error.Message);
            return r.Value;
        }

        public static FiltrosDeInforme Rango(DateOnly? desde = null, DateOnly? hasta = null, bool cierre = false) =>
            new() { From = desde ?? Marzo10, To = hasta ?? Marzo20, IncludeClosing = cierre };

        /// <summary>Abre el ejercicio 2025 completo para poder contabilizar en el año anterior (comparativos y cierre).</summary>
        public void AbrirEjercicio2025()
        {
            var ejercicio = new FiscalYear { Year = 2025, CreatedBy = "test" };
            D.Db.FiscalYears.Add(ejercicio);
            D.Db.SaveChanges();
            for (var mes = 1; mes <= 12; mes++)
            {
                var inicio = new DateOnly(2025, mes, 1);
                D.Db.AccountingPeriods.Add(new AccountingPeriod { FiscalYearId = ejercicio.Id, Month = (byte)mes, StartDate = inicio, EndDate = inicio.AddMonths(1).AddDays(-1), Status = PeriodStatus.Open, CreatedBy = "test" });
            }
            D.Db.SaveChanges();
        }
    }

    // ------------------------------------------------------------------------------ helpers --

    /// <summary>La columna oculta <c>_rubro</c>, la última de la fila.</summary>
    private static object? Codigo(FilaExportable f) => f.Valores[f.Valores.Count - 1];

    private static FilaExportable FilaDeRubro(TablaExportable t, string rubro) =>
        t.Filas.Should().ContainSingle(f => Equals(Codigo(f), rubro), $"debe existir la fila del rubro {rubro}").Subject;

    private static FilaExportable FilaLlamada(TablaExportable t, string nombre) =>
        t.Filas.Should().ContainSingle(f => Equals(f.Valores[1], nombre) || Equals(f.Valores[0], nombre), $"debe existir la fila «{nombre}»").Subject;

    private static decimal Saldo(TablaExportable t, string rubro) => (decimal)FilaDeRubro(t, rubro).Valores[2]!;
    private static decimal Comparativo(TablaExportable t, string rubro) => (decimal)FilaDeRubro(t, rubro).Valores[3]!;
    private static decimal Variacion(TablaExportable t, string rubro) => (decimal)FilaDeRubro(t, rubro).Valores[4]!;

    // -------------------------------------------------------------------------------- ESF --

    [Fact]
    public async Task Esf_cuadra_activo_igual_pasivo_mas_patrimonio_con_el_resultado_inyectado()
    {
        var e = new Escenario();
        await e.DatosBase();

        var t = await e.Esf();

        // Activo: caja 1000 + 500 − 200 = 1300, CxC 300. Pasivo: CxP 100. Patrimonio: capital 1000 + resultado (800 − 300) = 1500.
        Saldo(t, "ESF-A-EFE").Should().Be(1300m);
        Saldo(t, "ESF-A-CXC").Should().Be(300m);
        Saldo(t, "ESF-A").Should().Be(1600m);
        Saldo(t, "ESF-P").Should().Be(100m);
        Saldo(t, "ESF-PT-CAP").Should().Be(1000m);
        Saldo(t, "ESF-PT-REJ").Should().Be(500m, "el resultado del ejercicio se inyecta aunque no haya cierre");
        Saldo(t, "ESF-PT").Should().Be(1500m);
        Saldo(t, "ESF-A").Should().Be(Saldo(t, "ESF-P") + Saldo(t, "ESF-PT"), "Activo = Pasivo + Patrimonio");
        FilaLlamada(t, "Total pasivo y patrimonio").Valores[2].Should().Be(1600m);
        t.Notas.Should().NotContain(n => n.Contains("no cuadra"));

        FilaDeRubro(t, "ESF-A").Resaltada.Should().BeTrue("los totales de sección van resaltados");
        FilaDeRubro(t, "ESF-A-EFE").Resaltada.Should().BeFalse();
        FilaDeRubro(t, "ESF-A-EFE").Seccion.Should().Be("Activo");
        t.Columnas[^1].Clave.Should().Be("_rubro");
        t.Columnas[^1].EsOculta.Should().BeTrue();
        t.Filas.Select(f => f.Valores[^1]).Should().Contain("ESF-PT-REJ");
        var indiceActivo = t.Filas.ToList().FindIndex(f => Equals(Codigo(f), "ESF-A"));
        var indiceEfectivo = t.Filas.ToList().FindIndex(f => Equals(Codigo(f), "ESF-A-EFE"));
        indiceEfectivo.Should().BeLessThan(indiceActivo, "el total de la sección cierra la sección");
    }

    [Fact]
    public async Task Esf_comparativo_al_mismo_dia_del_ano_anterior_y_el_cierre_anterior_siempre_cuenta()
    {
        var e = new Escenario();
        e.AbrirEjercicio2025();
        var marzo2025 = new DateOnly(2025, 3, 15);
        var diciembre2025 = new DateOnly(2025, 12, 31);
        await e.Contabilizar(marzo2025, [(e.Caja, 100m, 0m), (e.Ingreso, 0m, 100m)]);
        // El cierre de 2025 lleva el resultado a la cuenta del excedente.
        await e.Contabilizar(diciembre2025, [(e.Ingreso, 100m, 0m), (e.Excedente, 0m, 100m)], kind: DocumentKind.Closing, tipo: "CI");
        await e.DatosBase();

        var t = await e.Esf();

        Comparativo(t, "ESF-A-EFE").Should().Be(100m, "al 20/03/2025 sólo estaba la venta de marzo de 2025");
        Comparativo(t, "ESF-PT-REJ").Should().Be(100m, "en 2025 el resultado aún no estaba cerrado: se inyecta");
        Variacion(t, "ESF-A-EFE").Should().Be(1400m - 100m);
        // En 2026 el cierre de 2025 cuenta aunque no se pida: el excedente ya está en la cuenta 35 y el ERI de 2025 en cero.
        Saldo(t, "ESF-PT-REJ").Should().Be(100m + 500m);
        Saldo(t, "ESF-A").Should().Be(Saldo(t, "ESF-P") + Saldo(t, "ESF-PT"), "el ESF del segundo año cuadra");
        t.Notas.Should().Contain(n => n.Contains("Comparativo al 20/03/2025"));

        // A fin de 2025 sin la bandera, el cierre de ese ejercicio no entra y el resultado se inyecta; con la bandera, lo trae la cuenta 35.
        var sinCierre = await e.Esf(Escenario.Rango(new DateOnly(2025, 1, 1), diciembre2025));
        Saldo(sinCierre, "ESF-PT-REJ").Should().Be(100m);
        var conCierre = await e.Esf(Escenario.Rango(new DateOnly(2025, 1, 1), diciembre2025, cierre: true));
        Saldo(conCierre, "ESF-PT-REJ").Should().Be(100m, "no se cuenta dos veces");
        Saldo(conCierre, "ESF-A").Should().Be(Saldo(conCierre, "ESF-P") + Saldo(conCierre, "ESF-PT"));
    }

    [Fact]
    public async Task Esf_deja_las_cuentas_de_orden_fuera_del_cuadre_como_memorando()
    {
        var e = new Escenario();
        var deudora = e.Cuenta("810505", AccountNature.Debit, "ORD-DEU");
        var acreedora = e.Cuenta("910505", AccountNature.Credit, "ORD-ACR");
        await e.DatosBase();
        await e.Contabilizar(Marzo15, [(deudora, 5000m, 0m), (acreedora, 0m, 5000m)]);

        var t = await e.Esf();

        Saldo(t, "ORD-DEU").Should().Be(5000m);
        Saldo(t, "ORD-ACR").Should().Be(5000m);
        FilaDeRubro(t, "ORD").Seccion.Should().Be("Cuentas de orden");
        Saldo(t, "ESF-A").Should().Be(1600m, "las cuentas de orden no son activo");
        FilaLlamada(t, "Total pasivo y patrimonio").Valores[2].Should().Be(1600m);
        t.Filas.Last(f => f.Valores[^1] is string).Valores[^1].Should().Be("ORD", "el memorando va al final");
        t.Notas.Should().Contain(n => n.Contains("memorando"));
        t.Notas.Should().NotContain(n => n.Contains("no cuadra"));
    }

    [Fact]
    public async Task Una_cuenta_con_rubro_desconocido_va_a_la_fila_sin_rubro_y_se_avisa()
    {
        var e = new Escenario();
        var huerfana = e.Cuenta("189505", AccountNature.Debit, "ZZZ");
        await e.DatosBase();
        await e.Contabilizar(Marzo15, [(huerfana, 70m, 0m), (e.Caja, 0m, 70m)]);

        var t = await e.Esf();

        var sinRubro = FilaLlamada(t, "Sin rubro NIIF");
        sinRubro.Valores[2].Should().Be(70m, "los pesos no se pierden");
        sinRubro.Seccion.Should().Be("Sin rubro NIIF");
        Saldo(t, "ESF-A").Should().Be(1600m - 70m, "la cuenta sin rubro no entra en ningún total");
        t.Notas.Should().Contain(n => n.Contains("189505") && n.Contains("Sin rubro NIIF"));
        t.Notas.Should().Contain(n => n.Contains("no cuadra") && n.Contains("70"));
    }

    [Fact]
    public async Task Los_estados_respetan_el_alcance_de_sucursal()
    {
        var e = new Escenario();
        await e.DatosBase();
        await e.Contabilizar(Marzo15, [(e.Caja, 900m, 0m), (e.Ingreso, 0m, 900m)], sucursalId: e.D.Norte.Id);
        var completo = await e.Esf();
        Saldo(completo, "ESF-A-EFE").Should().Be(2200m);

        e.D.RestringirA(e.D.Principal);

        var restringido = await e.Esf();
        Saldo(restringido, "ESF-A-EFE").Should().Be(1300m, "lo de Norte no se ve");
        Saldo(restringido, "ESF-PT-REJ").Should().Be(500m);
        restringido.Notas.Should().Contain(n => n.Contains("sucursales asignadas"));
        var eri = await e.Eri();
        Saldo(eri, "ERI-ING").Should().Be(800m);
        var efe = await e.Efe();
        FilaLlamada(efe, "Efectivo y equivalentes").Valores[1].Should().Be(300m);
    }

    // -------------------------------------------------------------------------------- ERI --

    [Fact]
    public async Task Eri_por_rubros_con_subtotales_y_resultado_del_ejercicio()
    {
        var e = new Escenario();
        await e.DatosBase();
        await e.Contabilizar(Marzo15, [(e.Devolucion, 50m, 0m), (e.Caja, 0m, 50m)]);
        await e.Contabilizar(Marzo15, [(e.Impuesto, 40m, 0m), (e.Cxp, 0m, 40m)]);

        var t = await e.Eri();

        Saldo(t, "ERI-ING-DEV").Should().Be(-50m, "la contra se muestra con su signo");
        Saldo(t, "ERI-ING").Should().Be(750m, "el ingreso neto de devoluciones");
        Saldo(t, "ERI-GAD").Should().Be(300m);
        Saldo(t, "ERI-IMP").Should().Be(40m);
        FilaLlamada(t, "Utilidad bruta").Valores[2].Should().Be(750m);
        FilaLlamada(t, "Resultado operacional").Valores[2].Should().Be(450m);
        FilaLlamada(t, "Resultado antes de impuestos").Valores[2].Should().Be(450m);
        FilaLlamada(t, "Resultado del ejercicio").Valores[2].Should().Be(410m);
        FilaLlamada(t, "Resultado integral total").Valores[2].Should().Be(410m, "el grupo 2 tiene ORI, en cero");
        FilaLlamada(t, "Resultado del ejercicio").Resaltada.Should().BeTrue();
        FilaLlamada(t, "Resultado del ejercicio").Seccion.Should().Be("Impuestos", "el subtotal cierra la sección que lo completa");
        t.Filas.Should().NotContain(f => Equals(Codigo(f), "ERI-CIE"), "el cierre sólo aparece cuando se pide");
        t.Notas.Should().NotContain(n => n.Contains("difiere"));

        var conCierre = await e.Eri(Escenario.Rango(cierre: true));
        conCierre.Filas.Should().Contain(f => Equals(Codigo(f), "ERI-CIE"));
    }

    [Fact]
    public async Task Eri_compara_con_el_mismo_rango_un_ano_antes()
    {
        var e = new Escenario();
        e.AbrirEjercicio2025();
        await e.Contabilizar(new DateOnly(2025, 3, 12), [(e.Caja, 100m, 0m), (e.Ingreso, 0m, 100m)]);
        await e.Contabilizar(new DateOnly(2025, 3, 25), [(e.Caja, 999m, 0m), (e.Ingreso, 0m, 999m)], sucursalId: null);
        await e.DatosBase();

        var t = await e.Eri();

        Saldo(t, "ERI-ING").Should().Be(800m);
        Comparativo(t, "ERI-ING").Should().Be(100m, "sólo lo del 10 al 20 de marzo de 2025");
        Variacion(t, "ERI-ING").Should().Be(700m);
        Comparativo(t, "ERI-GAD").Should().Be(0m);
        FilaLlamada(t, "Resultado del ejercicio").Valores[3].Should().Be(100m);
        t.Subtitulo.Should().Contain("comparativo del 10/03/2025 al 20/03/2025");
    }

    // -------------------------------------------------------------------------------- ECP --

    [Fact]
    public async Task Ecp_con_saldo_inicial_aumentos_disminuciones_y_saldo_final()
    {
        var e = new Escenario();
        await e.DatosBase();
        await e.Contabilizar(Marzo15, [(e.Caja, 200m, 0m), (e.Capital, 0m, 200m)]);
        await e.Contabilizar(Marzo15, [(e.Capital, 50m, 0m), (e.Caja, 0m, 50m)]);

        var t = await e.Ecp();

        var capital = FilaDeRubro(t, "ECP-CAP");
        capital.Valores[2].Should().Be(1000m, "saldo al 09/03/2026");
        capital.Valores[3].Should().Be(200m, "créditos del rango");
        capital.Valores[4].Should().Be(50m, "débitos del rango");
        capital.Valores[5].Should().Be(1150m);

        var resultado = FilaDeRubro(t, "ECP-REJ");
        resultado.Valores[2].Should().Be(0m, "antes del 10 de marzo no había ingresos ni gastos");
        resultado.Valores[3].Should().Be(500m, "el resultado del rango entra como aumento");
        resultado.Valores[4].Should().Be(0m);
        resultado.Valores[5].Should().Be(500m);

        FilaDeRubro(t, "ECP-RAC").Valores[5].Should().Be(0m);
        t.Totales.Should().NotBeNull();
        t.Totales!.Valores[2].Should().Be(1000m);
        t.Totales.Valores[5].Should().Be(1650m, "igual al patrimonio del ESF al 20 de marzo");
        var esf = await e.Esf();
        Saldo(esf, "ESF-PT").Should().Be(1650m);
        t.Notas.Should().Contain(n => n.Contains("ECP-REJ ← ESF-PT-REJ"));
    }

    // -------------------------------------------------------------------------------- EFE --

    [Fact]
    public async Task Efe_indirecto_explica_la_variacion_del_efectivo_y_la_diferencia_es_cero()
    {
        var e = new Escenario();
        await e.Contabilizar(Marzo5, [(e.Caja, 1000m, 0m), (e.Capital, 0m, 1000m)]);
        // Venta a crédito de 500 cobrada en parte (300) y un gasto de 200 pagado en efectivo.
        await e.Contabilizar(Marzo15, [(e.Cxc, 500m, 0m), (e.Ingreso, 0m, 500m)]);
        await e.Contabilizar(Marzo15, [(e.Caja, 300m, 0m), (e.Cxc, 0m, 300m)]);
        await e.Contabilizar(Marzo15, [(e.Gasto, 200m, 0m), (e.Caja, 0m, 200m)]);

        var t = await e.Efe();

        decimal Valor(string rubro) => (decimal)FilaDeRubro(t, rubro).Valores[1]!;
        Valor("EFE-OPE-RES").Should().Be(300m, "ingresos 500 − gastos 200");
        Valor("EFE-OPE-AJU").Should().Be(0m);
        Valor("EFE-OPE-CAR").Should().Be(-200m, "la cartera creció 200: efectivo que no entró");
        Valor("EFE-OPE-CXP").Should().Be(0m);
        Valor("EFE-OPE").Should().Be(100m);
        Valor("EFE-INV").Should().Be(0m);
        Valor("EFE-FIN").Should().Be(0m, "el aporte fue antes del rango");
        Valor("EFE-EFE").Should().Be(100m, "caja: 1000 → 1100");
        FilaLlamada(t, "Efectivo y equivalentes al inicio").Valores[1].Should().Be(1000m);
        FilaLlamada(t, "Efectivo y equivalentes al final").Valores[1].Should().Be(1100m);
        var diferencia = t.Filas.Last();
        diferencia.Valores[0].Should().BeOfType<string>().Which.Should().StartWith("Diferencia");
        diferencia.Valores[1].Should().Be(0m);
        diferencia.Resaltada.Should().BeTrue();
        FilaDeRubro(t, "EFE-OPE").Resaltada.Should().BeTrue();
        FilaDeRubro(t, "EFE-OPE").Seccion.Should().Be("Operación");
        t.Notas.Should().NotContain(n => n.Contains("fuera del mapeo"));
    }

    [Fact]
    public async Task Efe_lleva_los_aportes_a_financiacion_y_los_pasivos_a_operacion_y_sigue_cuadrando()
    {
        var e = new Escenario();
        var ppe = e.Cuenta("152005", AccountNature.Debit, "ESF-A-PPE");
        await e.DatosBase();
        await e.Contabilizar(Marzo15, [(e.Caja, 250m, 0m), (e.Capital, 0m, 250m)]);
        await e.Contabilizar(Marzo15, [(ppe, 400m, 0m), (e.Caja, 0m, 400m)]);

        var t = await e.Efe();

        decimal Valor(string rubro) => (decimal)FilaDeRubro(t, rubro).Valores[1]!;
        Valor("EFE-OPE-RES").Should().Be(500m);
        Valor("EFE-OPE-CAR").Should().Be(-300m);
        Valor("EFE-OPE-CXP").Should().Be(100m, "la cuenta por pagar creció: efectivo que no salió");
        Valor("EFE-INV-PPE").Should().Be(-400m);
        Valor("EFE-FIN-CAP").Should().Be(250m);
        Valor("EFE-EFE").Should().Be(500m - 200m + 250m - 400m);
        t.Filas.Last().Valores[1].Should().Be(0m, "Σ actividades = Δ efectivo");
    }

    // ------------------------------------------------- naturaleza esperada por el rubro (h9) --

    [Fact]
    public async Task Esf_mide_cada_cuenta_por_el_lado_que_su_rubro_espera_y_cuadra_con_perdida_en_3510_y_deterioro_en_1408()
    {
        var e = new Escenario();
        e.AbrirEjercicio2025();
        // 3510 «Pérdida del ejercicio» es débito dentro del patrimonio (rubro +1); 1408 es crédito dentro del activo (rubro −1).
        var perdida = e.Cuenta("351005", AccountNature.Debit, "ESF-PT-REJ");
        var cartera = e.Cuenta("140405", AccountNature.Debit, "ESF-A-CAR");
        var deterioro = e.Cuenta("140805", AccountNature.Credit, "ESF-A-CAR-DET");
        var gastoDeterioro = e.Cuenta("519905", AccountNature.Debit, "ERI-DET");
        // 2025 cierra con pérdida de 100: el cierre la deja como débito en 3510.
        await e.Contabilizar(new DateOnly(2025, 3, 5), [(e.Caja, 1000m, 0m), (e.Capital, 0m, 1000m)]);
        await e.Contabilizar(new DateOnly(2025, 3, 15), [(e.Gasto, 100m, 0m), (e.Caja, 0m, 100m)]);
        await e.Contabilizar(new DateOnly(2025, 12, 31), [(perdida, 100m, 0m), (e.Gasto, 0m, 100m)], kind: DocumentKind.Closing, tipo: "CI");
        await e.DatosBase();
        await e.Contabilizar(Marzo15, [(cartera, 1000m, 0m), (e.Caja, 0m, 1000m)]);
        await e.Contabilizar(Marzo15, [(gastoDeterioro, 50m, 0m), (deterioro, 0m, 50m)]);

        var t = await e.Esf();

        Saldo(t, "ESF-A-CAR-DET").Should().Be(-50m, "la contra resta al activo");
        Saldo(t, "ESF-A-CAR").Should().Be(950m, "cartera bruta 1000 menos el deterioro");
        Saldo(t, "ESF-A").Should().Be(1200m + 300m + 950m);
        // La pérdida cerrada resta al patrimonio (−100) y el resultado de 2026 (500 − 50) se inyecta.
        Saldo(t, "ESF-PT-REJ").Should().Be(-100m + 450m, "una pérdida en 3510 baja el patrimonio, no lo sube");
        Saldo(t, "ESF-PT").Should().Be(2000m - 100m + 450m);
        Saldo(t, "ESF-A").Should().Be(Saldo(t, "ESF-P") + Saldo(t, "ESF-PT"), "Activo = Pasivo + Patrimonio con cuentas de naturaleza contraria a su clase");
        t.Notas.Should().NotContain(n => n.Contains("no cuadra"));
        // El comparativo (20/03/2025) también cuadra con la pérdida aún abierta e inyectada.
        Comparativo(t, "ESF-PT-REJ").Should().Be(-100m);
        Comparativo(t, "ESF-A").Should().Be(Comparativo(t, "ESF-P") + Comparativo(t, "ESF-PT"));
    }

    [Fact]
    public async Task Eri_un_credito_en_una_cuenta_de_costos_resta_a_los_costos_y_el_resultado_por_rubros_coincide_con_el_contable()
    {
        var e = new Escenario();
        // 6220 es crédito dentro de los costos (rubro +1): un saldo crédito de 30 tiene que bajar los costos, no subirlos.
        var costo = e.Cuenta("620505", AccountNature.Debit, "ERI-COS");
        var ajusteDeCosto = e.Cuenta("622005", AccountNature.Credit, "ERI-COS");
        await e.DatosBase();
        await e.Contabilizar(Marzo15, [(costo, 120m, 0m), (e.Caja, 0m, 120m)]);
        await e.Contabilizar(Marzo15, [(e.Caja, 30m, 0m), (ajusteDeCosto, 0m, 30m)]);

        var t = await e.Eri();

        Saldo(t, "ERI-COS").Should().Be(90m, "120 de costo menos 30 de ajuste");
        FilaLlamada(t, "Utilidad bruta").Valores[2].Should().Be(800m - 90m);
        FilaLlamada(t, "Resultado del ejercicio").Valores[2].Should().Be(800m - 90m - 300m);
        t.Notas.Should().NotContain(n => n.Contains("difiere"), "el resultado por rubros y el contable coinciden");
        var esf = await e.Esf();
        Saldo(esf, "ESF-PT-REJ").Should().Be(410m);
        Saldo(esf, "ESF-A").Should().Be(Saldo(esf, "ESF-P") + Saldo(esf, "ESF-PT"));
    }

    // ------------------------------------------------------- EFE a través de un cierre (h7) --

    [Fact]
    public async Task Efe_del_segundo_ejercicio_trae_el_resultado_del_rango_y_no_traslada_el_excedente_cerrado_a_financiacion()
    {
        var e = new Escenario();
        e.AbrirEjercicio2025();
        await e.Contabilizar(new DateOnly(2025, 3, 5), [(e.Caja, 1000m, 0m), (e.Capital, 0m, 1000m)]);
        await e.Contabilizar(new DateOnly(2025, 3, 15), [(e.Caja, 100m, 0m), (e.Ingreso, 0m, 100m)]);
        // El cierre de 2025 lleva el excedente (100) a la 35.
        await e.Contabilizar(new DateOnly(2025, 12, 31), [(e.Ingreso, 100m, 0m), (e.Excedente, 0m, 100m)], kind: DocumentKind.Closing, tipo: "CI");
        await e.DatosBase();
        // En 2026 se distribuyen 40 del excedente anterior: eso sí es un flujo de financiación.
        await e.Contabilizar(Marzo15, [(e.Excedente, 40m, 0m), (e.Caja, 0m, 40m)]);

        var t = await e.Efe(Escenario.Rango(new DateOnly(2026, 1, 1), Marzo20));

        decimal Valor(string rubro) => (decimal)FilaDeRubro(t, rubro).Valores[1]!;
        Valor("EFE-OPE-RES").Should().Be(500m, "el resultado del rango es el de 2026, no 2026 menos 2025");
        Valor("EFE-FIN-CAP").Should().Be(1000m - 40m, "aporte de 2026 menos la distribución; el excedente cerrado de 2025 no es un flujo");
        Valor("EFE-OPE-CAR").Should().Be(-300m);
        Valor("EFE-OPE-CXP").Should().Be(100m);
        FilaLlamada(t, "Efectivo y equivalentes al inicio").Valores[1].Should().Be(1100m);
        FilaLlamada(t, "Efectivo y equivalentes al final").Valores[1].Should().Be(1100m + 1000m + 500m - 200m - 40m);
        Valor("EFE-EFE").Should().Be(1260m);
        t.Filas.Last().Valores[1].Should().Be(0m, "Σ actividades = Δ efectivo");
        t.Notas.Should().Contain(n => n.Contains("cierres de ejercicios anteriores"));
    }

    // ------------------------------------------------ comparativo del ECP y del EFE (h11) --

    [Fact]
    public async Task Ecp_y_efe_comparan_con_el_mismo_rango_un_ano_antes()
    {
        var e = new Escenario();
        e.AbrirEjercicio2025();
        await e.Contabilizar(new DateOnly(2025, 3, 12), [(e.Caja, 100m, 0m), (e.Capital, 0m, 100m)]);
        await e.Contabilizar(new DateOnly(2025, 3, 15), [(e.Caja, 50m, 0m), (e.Ingreso, 0m, 50m)]);
        await e.Contabilizar(new DateOnly(2025, 12, 31), [(e.Ingreso, 50m, 0m), (e.Excedente, 0m, 50m)], kind: DocumentKind.Closing, tipo: "CI");
        await e.DatosBase();

        var ecp = await e.Ecp();

        ecp.Columnas.Select(c => c.Nombre).Should().ContainInOrder("Saldo final", "Comparativo", "Variación", "_rubro");
        ecp.Columnas[^1].EsOculta.Should().BeTrue("la columna oculta sigue al final");
        var capital = FilaDeRubro(ecp, "ECP-CAP");
        capital.Valores[5].Should().Be(1100m, "saldo final al 20/03/2026: 100 de 2025 más 1000 de 2026");
        capital.Valores[6].Should().Be(100m, "saldo final al 20/03/2025");
        capital.Valores[7].Should().Be(1000m);
        var resultado = FilaDeRubro(ecp, "ECP-REJ");
        resultado.Valores[2].Should().Be(50m, "el excedente cerrado de 2025 ya está en la 35");
        resultado.Valores[5].Should().Be(550m);
        resultado.Valores[6].Should().Be(50m, "al 20/03/2025 el resultado acumulado era 50");
        resultado.Valores[7].Should().Be(500m);
        ecp.Totales!.Valores[6].Should().Be(150m);
        ecp.Totales.Valores[7].Should().Be(1650m - 150m);
        ecp.Notas.Should().Contain(n => n.Contains("Comparativo del 10/03/2025 al 20/03/2025") && n.Contains("saldo final"));
        ecp.Subtitulo.Should().Contain("comparativo del 10/03/2025 al 20/03/2025");

        var efe = await e.Efe();

        efe.Columnas.Select(c => c.Nombre).Should().Equal("Concepto", "Valor", "Comparativo", "Variación", "_rubro");
        var res = FilaDeRubro(efe, "EFE-OPE-RES");
        res.Valores[1].Should().Be(500m);
        res.Valores[2].Should().Be(50m, "el resultado del 10 al 20 de marzo de 2025");
        res.Valores[3].Should().Be(450m);
        var aportes = FilaDeRubro(efe, "EFE-FIN-CAP");
        aportes.Valores[1].Should().Be(0m, "el aporte de 2026 fue antes del rango");
        aportes.Valores[2].Should().Be(100m, "el de 2025 cayó dentro del rango comparativo");
        FilaLlamada(efe, "Efectivo y equivalentes al inicio").Valores[2].Should().Be(0m);
        FilaLlamada(efe, "Efectivo y equivalentes al final").Valores[2].Should().Be(150m);
        efe.Filas.Last().Valores[2].Should().Be(0m, "la diferencia del comparativo también es cero");
        efe.Notas.Should().Contain(n => n.Contains("Comparativo del 10/03/2025 al 20/03/2025") && n.Contains("valor"));
    }

    [Fact]
    public async Task Ecp_y_efe_dejan_el_comparativo_vacio_cuando_el_rango_anterior_cae_antes_de_la_fecha_minima()
    {
        var e = new Escenario();
        await e.DatosBase();
        var f = Escenario.Rango(MovimientosContables.FechaMinima.AddDays(4), MovimientosContables.FechaMinima.AddDays(19));

        var ecp = await e.Ecp(f);
        var efe = await e.Efe(f);

        FilaDeRubro(ecp, "ECP-CAP").Valores[6].Should().BeNull();
        FilaDeRubro(ecp, "ECP-CAP").Valores[7].Should().BeNull();
        ecp.Totales!.Valores[6].Should().BeNull();
        ecp.Notas.Should().Contain(n => n.StartsWith("Sin comparativo"));
        ecp.Subtitulo.Should().NotContain("comparativo");
        FilaDeRubro(efe, "EFE-OPE-RES").Valores[2].Should().BeNull();
        FilaDeRubro(efe, "EFE-OPE-RES").Valores[3].Should().BeNull();
        efe.Filas.Last().Valores[2].Should().BeNull();
        efe.Notas.Should().Contain(n => n.StartsWith("Sin comparativo"));
    }

    // ----------------------------------------------------------------------- exportación --

    [Fact]
    public async Task Exportar_deja_el_evento_de_auditoria_y_las_columnas_ocultas_no_se_exportan()
    {
        var e = new Escenario();
        await e.DatosBase();

        var t = await e.Esf(Escenario.Rango() with { Format = "xlsx" });

        await e.Audit.Received(1).AppendAsync(
            Arg.Is<AuditEventDocument>(d => d.Action == "Accounting.Report.Exported" && d.NewValuesJson!.Contains("financial-position") && d.NewValuesJson.Contains("xlsx")),
            Arg.Any<CancellationToken>());
        t.SinOcultas().Columnas.Should().NotContain(c => c.Clave == "_rubro");
        t.SinOcultas().Columnas.Should().HaveCount(5);
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_responde_el_error_del_modulo()
    {
        var d = new ContabilidadTestData(iniciada: false);
        var emisor = new AccountingAuditEmitter(Substitute.For<IAuditAppendOnlyWriter>(), d.User, d.Clock, NullLogger<AccountingAuditEmitter>.Instance);

        var r = await new FinancialPositionQueryHandler(d.Db, d.Alcance, d.Clock, d.User, emisor).Handle(new FinancialPositionQuery(Escenario.Rango()), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.NotInitialized");
    }

    [Fact]
    public void Los_validadores_admiten_solo_niveles_y_formatos_conocidos()
    {
        var v = new FinancialPositionQueryValidator();
        v.Validate(new FinancialPositionQuery(new FiltrosDeInforme { Level = 7 })).IsValid.Should().BeFalse();
        v.Validate(new FinancialPositionQuery(new FiltrosDeInforme { Format = "csv" })).IsValid.Should().BeFalse();
        v.Validate(new FinancialPositionQuery(new FiltrosDeInforme { Level = 4, Format = "PDF" })).IsValid.Should().BeTrue();
        new CashFlowQueryValidator().Validate(new CashFlowQuery(new FiltrosDeInforme())).IsValid.Should().BeTrue();
    }
}
