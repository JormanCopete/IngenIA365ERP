using FluentAssertions;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Receipts.Reports;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Reports;

/// <summary>
/// Feature 015, US21, T0311 (FR-152 parte R0; api.md §17): las once vistas de R0 del centro de informes de Recaudo están registradas y responden una
/// <see cref="TablaExportable"/> con alcance; las columnas ocultas empiezan con <c>_</c>; <c>daily-collection</c> por medio cuadra con lo esperado de las sesiones (el
/// efectivo neto de las vueltas) y no suma los anulados; una agrupación que no existe es <c>Reports.ReceiptsTreasury.FilterInvalid</c>. Prueba escrita, sin correr.
/// </summary>
public class InformesDeRecaudoR0Tests
{
    private readonly EscenarioDeRecaudo _e = new();

    private ReceiptsReportQueryHandler Handler() => new(_e.Db, _e.Alcance, _e.Reloj);

    private static FiltrosDeInformeDeRecaudoYTesoreria Filtros(string? groupBy = null) =>
        new() { From = EscenarioDeRecaudo.Hoy, To = EscenarioDeRecaudo.Hoy, AsOf = EscenarioDeRecaudo.Hoy, GroupBy = groupBy };

    [Fact]
    public void Las_once_vistas_de_R0_y_la_del_paralelo_estan_registradas()
    {
        // R1 y R2 suman las suyas (sus pruebas las cuentan); aquí basta con que estén las de R0 (corrida del 2026-10-26, como InformesDeTesoreriaT0Tests en la fase 45).
        VistasDeRecaudo.Todas.Select(v => v.Key).Should().Contain(
        [
            "session-close", "day-close", "daily-collection", "receipts-by-cashier", "voided-receipts", "exceptions", "unidentified-items", "payer-credit-balances",
            "paymasters", "returned-checks", "contingency-pending", "parallel-comparison",
        ]);
        VistasDeRecaudo.Todas.Should().OnlyContain(v => !string.IsNullOrWhiteSpace(v.Name) && !string.IsNullOrWhiteSpace(v.FileName));
    }

    public static TheoryData<string> Claves()
    {
        var d = new TheoryData<string>();
        foreach (var v in VistasDeRecaudo.Todas) d.Add(v.Key);
        return d;
    }

    [Theory]
    [MemberData(nameof(Claves))]
    public async Task Cada_vista_responde_su_tabla_aun_sin_datos(string vista)
    {
        var r = await Handler().Handle(new ReceiptsReportQuery(vista, Filtros()), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Columnas.Should().NotBeEmpty();
        r.Value.Columnas.Where(c => c.Clave is not null).Should().OnlyContain(c => c.Clave!.StartsWith('_'));
    }

    [Fact]
    public async Task Una_vista_que_no_existe_se_rechaza()
    {
        var r = await Handler().Handle(new ReceiptsReportQuery("no-existe", Filtros()), default);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ReceiptsReportQueryHandler.CodigoVistaDesconocida);
    }

    [Fact]
    public async Task Una_agrupacion_que_no_existe_se_rechaza_con_las_admitidas()
    {
        var r = await Handler().Handle(new ReceiptsReportQuery(VistasDeRecaudo.RecaudoDelDia, Filtros("luna")), default);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(FiltrosDeInformeDeRecaudoYTesoreria.FiltroInvalidoCodigo);
    }

    /// <summary>Dos recibos vigentes (efectivo 120.000 con 20.000 de vueltas y transferencia 50.000) y uno anulado: por medio, 100.000 + 50.000.</summary>
    private (CashSession Sesion, PaymentMeans Efectivo, PaymentMeans Transferencia) Recibos()
    {
        var punto = _e.Punto("P1");
        var caja = _e.Caja(punto, "C1");
        var efectivo = _e.Medio("EF", PaymentMeansClass.Cash);
        var transferencia = _e.Medio("TR", PaymentMeansClass.Transfer);
        var dia = new CollectionPointDay { CollectionPointId = punto.Id, BusinessDate = EscenarioDeRecaudo.Hoy };
        _e.Db.CollectionPointDays.Add(dia);
        _e.Db.SaveChanges();
        var sesion = new CashSession { CashBoxId = caja.Id, CashierUserId = _e.Cajera.Id, CollectionPointDayId = dia.Id, OpenedAt = new DateTime(2026, 12, 1, 13, 0, 0, DateTimeKind.Utc) };
        _e.Db.ReceiptsCashSessions.Add(sesion);
        _e.Db.SaveChanges();
        Receipt Recibo(string numero, decimal aplicado, decimal vueltas, ReceiptState estado) => new()
        {
            Number = numero, BranchId = _e.OficinaA.Id, CollectionPointId = punto.Id, CashBoxId = caja.Id, CashSessionId = sesion.Id, CashierUserId = _e.Cajera.Id,
            PayerPersonId = _e.PersonaCajera.Id, ValueDate = EscenarioDeRecaudo.Hoy, ProposedAccountingDate = EscenarioDeRecaudo.Hoy, TotalApplied = aplicado,
            TotalMeans = aplicado + vueltas, ChangeGiven = vueltas, Status = estado, OperationKey = Guid.NewGuid(), RegisteredAt = DateTime.UtcNow,
        };
        var r1 = Recibo("RC-1", 100_000m, 20_000m, ReceiptState.Valid);
        var r2 = Recibo("RC-2", 50_000m, 0m, ReceiptState.Valid);
        var r3 = Recibo("RC-3", 70_000m, 0m, ReceiptState.Voided);
        _e.Db.IssuedReceipts.AddRange(r1, r2, r3);
        _e.Db.SaveChanges();
        _e.Db.ReceiptMeans.AddRange(
            new ReceiptMeans { ReceiptId = r1.Id, Ordinal = 1, PaymentMeansId = efectivo.Id, MeansClass = PaymentMeansClass.Cash, Amount = 120_000m },
            new ReceiptMeans { ReceiptId = r2.Id, Ordinal = 1, PaymentMeansId = transferencia.Id, MeansClass = PaymentMeansClass.Transfer, Amount = 50_000m },
            new ReceiptMeans { ReceiptId = r3.Id, Ordinal = 1, PaymentMeansId = efectivo.Id, MeansClass = PaymentMeansClass.Cash, Amount = 70_000m });
        _e.Db.ReceiptApplications.AddRange(
            new ReceiptApplication { ReceiptId = r1.Id, Ordinal = 1, Target = ApplicationTarget.Loan, ProductNumber = "CR-1", BeneficiaryPersonId = _e.PersonaCajera.Id, Amount = 100_000m },
            new ReceiptApplication { ReceiptId = r2.Id, Ordinal = 1, Target = ApplicationTarget.Account, ProductNumber = "AP-1", BeneficiaryPersonId = _e.PersonaCajera.Id, Amount = 50_000m });
        _e.Db.SaveChanges();
        return (sesion, efectivo, transferencia);
    }

    [Fact]
    public async Task El_recaudo_del_dia_por_medio_cuadra_con_lo_esperado_y_no_suma_anulados()
    {
        Recibos();

        var r = await Handler().Handle(new ReceiptsReportQuery(VistasDeRecaudo.RecaudoDelDia, Filtros("means")), default);

        r.IsSuccess.Should().BeTrue();
        var porMedio = r.Value.Filas.ToDictionary(f => (string)f.Valores[1]!, f => (decimal)f.Valores[3]!);
        porMedio.Values.Sum().Should().Be(150_000m, "los dos vigentes, con el efectivo neto de las vueltas");
        porMedio.Should().HaveCount(2);
        r.Value.Totales!.Valores[2].Should().Be(2, "el anulado no cuenta");
    }

    [Fact]
    public async Task El_recaudo_del_dia_por_modulo_separa_creditos_de_aportes()
    {
        Recibos();

        var r = await Handler().Handle(new ReceiptsReportQuery(VistasDeRecaudo.RecaudoDelDia, Filtros("module")), default);

        r.Value.Filas.Select(f => (string)f.Valores[1]!).Should().BeEquivalentTo(["Cartera (créditos)", "Captaciones y aportes"]);
    }

    [Fact]
    public async Task El_cierre_de_sesion_cuenta_recibos_y_anulados()
    {
        Recibos();

        var r = await Handler().Handle(new ReceiptsReportQuery(VistasDeRecaudo.CierreDeSesion, Filtros()), default);

        var fila = r.Value.Filas.Should().ContainSingle().Subject;
        fila.Valores[7].Should().Be(2);
        fila.Valores[8].Should().Be(150_000m);
        fila.Valores[9].Should().Be(1);
    }

    [Fact]
    public async Task Fuera_del_alcance_no_se_ve()
    {
        Recibos();
        _e.Alcance.SoloPuntos(-99);

        var r = await Handler().Handle(new ReceiptsReportQuery(VistasDeRecaudo.RecaudoDelDia, Filtros()), default);

        r.Value.Filas.Should().BeEmpty();
    }
}
