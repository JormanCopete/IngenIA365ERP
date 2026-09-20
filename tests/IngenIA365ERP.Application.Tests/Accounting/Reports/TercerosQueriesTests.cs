using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Reports;

/// <summary>
/// T100 (feature 009 E2, US5 escenarios 7 y 8): el estado de cuenta del tercero con saldo corrido,
/// los documentos cruce con saldo pendiente y el saldo diario promedio. Todo se calcula sobre lo
/// contabilizado por el contrato (FR-046), con el alcance de sucursal de quien consulta, y la
/// exportación queda en la auditoría.
/// </summary>
public class TercerosQueriesTests
{
    private static readonly DateOnly Marzo1 = new(2026, 3, 1);
    private static readonly DateOnly Marzo3 = new(2026, 3, 3);
    private static readonly DateOnly Marzo10 = new(2026, 3, 10);
    private static readonly DateOnly Marzo11 = new(2026, 3, 11);
    private static readonly DateOnly Marzo12 = new(2026, 3, 12);
    private static readonly DateOnly Marzo15 = ContabilidadTestData.Marzo15;
    private static readonly DateOnly Marzo16 = new(2026, 3, 16);
    private static readonly DateOnly Marzo31 = new(2026, 3, 31);

    /// <summary>El escenario de cartera: una cuenta por cobrar con tercero y factura, la caja, un ingreso y un gasto.</summary>
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public IAuditAppendOnlyWriter Audit { get; } = Substitute.For<IAuditAppendOnlyWriter>();
        public AccountingAuditEmitter Emisor { get; }
        public ChartOfAccount Cartera { get; }
        public ChartOfAccount Caja { get; }
        public ChartOfAccount Ingreso { get; }
        public ChartOfAccount Gasto { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Audit, D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance);
            Cartera = D.Cuenta("130505", tercero: true, cruce: true);
            Caja = D.Cuenta("110505");
            Ingreso = D.Cuenta("413505", AccountNature.Credit);
            Gasto = D.Cuenta("510505");
        }

        public ThirdPartyStatementQueryHandler EstadoDeCuenta => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);
        public PendingDocumentsQueryHandler Pendientes => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);
        public DailyAverageBalanceQueryHandler Promedio => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);

        public PostingLine CarteraDebito(decimal valor, string factura, int? sucursal = null) =>
            new() { AccountId = Cartera.Id, Debit = valor, PersonId = D.Tercero.Id, CrossDocumentType = "FV", CrossDocumentNumber = factura, Detail = $"FV {factura}", BranchId = sucursal };

        public PostingLine CarteraCredito(decimal valor, string factura, int? sucursal = null) =>
            new() { AccountId = Cartera.Id, Credit = valor, PersonId = D.Tercero.Id, CrossDocumentType = "FV", CrossDocumentNumber = factura, Detail = $"Pago FV {factura}", BranchId = sucursal };

        /// <summary>Contabiliza por el contrato y guarda; devuelve el documento para conocer su PublicId.</summary>
        public async Task<AccountingDocument> Contabilizar(DateOnly fecha, params PostingLine[] lineas)
        {
            var r = await D.Poster.PrepareAsync(new PostingRequest("CG", fecha, "Prueba", ContabilidadTestData.Manual(), lineas), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error.Message);
            await D.Db.SaveChangesAsync();
            D.Db.DescartarCambios();
            return r.Value;
        }

        /// <summary>Factura de 300 el 3, abono de 100 el 12 y factura nueva de 250 el 15: el tercero debe 450 al final.</summary>
        public async Task<(AccountingDocument Factura1, AccountingDocument Abono, AccountingDocument Factura2)> Cartera450()
        {
            var f1 = await Contabilizar(Marzo3, CarteraDebito(300m, "1001"), PostingLine.Credito(Ingreso.Id, 300m));
            var abono = await Contabilizar(Marzo12, PostingLine.Debito(Caja.Id, 100m), CarteraCredito(100m, "1001"));
            var f2 = await Contabilizar(Marzo15, CarteraDebito(250m, "1002"), PostingLine.Credito(Ingreso.Id, 250m));
            return (f1, abono, f2);
        }

        public FiltrosDeInforme Filtros(DateOnly desde, DateOnly hasta, bool conTercero = true, string? formato = null) =>
            new() { From = desde, To = hasta, Person = conTercero ? D.Tercero.PublicId : null, Format = formato };
    }

    private static int Col(TablaExportable t, string nombre) => t.Columnas.ToList().FindIndex(c => c.Nombre == nombre);
    private static int Oculta(TablaExportable t, string clave) => t.Columnas.ToList().FindIndex(c => c.Clave == clave);

    // ------------------------------------------------------------- estado de cuenta --

    [Fact]
    public async Task El_estado_de_cuenta_arranca_en_el_saldo_inicial_y_lleva_el_saldo_corrido_por_cuenta()
    {
        var e = new Escenario();
        var (_, abono, factura2) = await e.Cartera450();

        var r = await e.EstadoDeCuenta.Handle(new ThirdPartyStatementQuery(e.Filtros(Marzo10, Marzo31)), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;
        t.Titulo.Should().Contain("Ana Prueba").And.Contain("1000000001");
        var saldo = Col(t, "Saldo");
        var detalle = Col(t, "Detalle");
        var comprobante = Oculta(t, "_comprobante");
        var cuenta = Oculta(t, "_cuenta");
        var nodo = Oculta(t, "_nodo");
        t.Columnas.Where(c => c.EsOculta).Select(c => c.Clave).Should().BeEquivalentTo(["_comprobante", "_cuenta", "_nodo"]);

        t.Filas.Should().HaveCount(3, "saldo inicial + abono + factura 2; la factura del 3 queda antes del rango");
        t.Filas.Should().OnlyContain(f => f.Seccion == "130505 Cuenta 130505");

        var inicial = t.Filas[0];
        inicial.Resaltada.Should().BeTrue();
        inicial.Valores[detalle].Should().Be("Saldo inicial");
        inicial.Valores[saldo].Should().Be(300m, "la factura 1001 del 3 de marzo es lo que traía");
        inicial.Valores[Col(t, "Fecha")].Should().Be(Marzo10.AddDays(-1));

        t.Filas[1].Valores[Col(t, "Fecha")].Should().Be(Marzo12);
        t.Filas[1].Valores[Col(t, "Comprobante")].Should().Be("CG-2");
        t.Filas[1].Valores[Col(t, "Documento cruce")].Should().Be("FV 1001");
        t.Filas[1].Valores[Col(t, "Crédito")].Should().Be(100m);
        t.Filas[1].Valores[saldo].Should().Be(200m, "cuenta de naturaleza débito: el abono baja el saldo");
        t.Filas[1].Valores[comprobante].Should().Be(abono.PublicId.ToString());

        t.Filas[2].Valores[Col(t, "Fecha")].Should().Be(Marzo15);
        t.Filas[2].Valores[Col(t, "Débito")].Should().Be(250m);
        t.Filas[2].Valores[saldo].Should().Be(450m);
        t.Filas[2].Valores[comprobante].Should().Be(factura2.PublicId.ToString());

        t.Filas.Should().OnlyContain(f => (string?)f.Valores[cuenta] == e.Cartera.PublicId.ToString());
        t.Filas.Should().OnlyContain(f => (string?)f.Valores[nodo] == $"person:130505|{e.D.Tercero.PublicId}");

        t.Totales.Should().NotBeNull();
        t.Totales!.Valores[Col(t, "Débito")].Should().Be(250m);
        t.Totales.Valores[Col(t, "Crédito")].Should().Be(100m);
        t.Notas.Should().Contain(n => n.StartsWith("Empresa:")).And.Contain(n => n.StartsWith("Filtros:") && n.Contains("Ana Prueba"));
    }

    [Fact]
    public async Task El_estado_de_cuenta_de_una_cuenta_de_credito_va_con_su_naturaleza()
    {
        var e = new Escenario();
        var proveedores = e.D.Cuenta("220505", AccountNature.Credit, tercero: true);
        await e.Contabilizar(Marzo3, PostingLine.Debito(e.Gasto.Id, 500m),
            new PostingLine { AccountId = proveedores.Id, Credit = 500m, PersonId = e.D.Tercero.Id });
        await e.Contabilizar(Marzo12, new PostingLine { AccountId = proveedores.Id, Debit = 200m, PersonId = e.D.Tercero.Id },
            PostingLine.Credito(e.Caja.Id, 200m));

        var r = await e.EstadoDeCuenta.Handle(new ThirdPartyStatementQuery(e.Filtros(Marzo1, Marzo31)), CancellationToken.None);

        var t = r.Value;
        var saldo = Col(t, "Saldo");
        t.Filas.Should().HaveCount(3);
        t.Filas[0].Valores[saldo].Should().Be(0m, "nada antes del 1 de marzo");
        t.Filas[1].Valores[saldo].Should().Be(500m, "crédito en cuenta de crédito: positivo");
        t.Filas[2].Valores[saldo].Should().Be(300m, "el pago la baja");
        t.Filas[1].Valores[Col(t, "Documento cruce")].Should().Be(string.Empty, "la cuenta no maneja documento cruce");
    }

    [Fact]
    public async Task Sin_tercero_el_estado_de_cuenta_responde_PersonRequired()
    {
        var e = new Escenario();

        var r = await e.EstadoDeCuenta.Handle(new ThirdPartyStatementQuery(e.Filtros(Marzo1, Marzo31, conTercero: false)), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Report.PersonRequired");
    }

    [Fact]
    public async Task Un_tercero_dado_de_baja_conserva_su_estado_de_cuenta()
    {
        var e = new Escenario();
        await e.Cartera450();
        var persona = await e.D.Db.People.SingleAsync(p => p.Id == e.D.Tercero.Id);
        persona.IsDeleted = true;
        persona.DeletedAt = ContabilidadTestData.Ahora;
        await e.D.Db.SaveChangesAsync();
        e.D.Db.DescartarCambios();

        var r = await e.EstadoDeCuenta.Handle(new ThirdPartyStatementQuery(e.Filtros(Marzo1, Marzo31)), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Filas.Should().HaveCount(4, "saldo inicial + tres movimientos: los asientos no se van con la persona");
        r.Value.Filas[^1].Valores[Col(r.Value, "Saldo")].Should().Be(450m);
    }

    [Fact]
    public async Task El_alcance_de_sucursal_recorta_el_estado_de_cuenta()
    {
        var e = new Escenario();
        await e.Contabilizar(Marzo3, e.CarteraDebito(300m, "1001", e.D.Principal.Id), PostingLine.Credito(e.Ingreso.Id, 300m));
        await e.Contabilizar(Marzo12, e.CarteraDebito(700m, "2001", e.D.Norte.Id), PostingLine.Credito(e.Ingreso.Id, 700m));
        e.D.RestringirA(e.D.Principal);

        var r = await e.EstadoDeCuenta.Handle(new ThirdPartyStatementQuery(e.Filtros(Marzo1, Marzo31)), CancellationToken.None);

        var t = r.Value;
        t.Filas.Should().HaveCount(2, "saldo inicial + la factura de la principal; la de Norte no se ve");
        t.Filas[^1].Valores[Col(t, "Saldo")].Should().Be(300m);
        t.Notas.Should().Contain(n => n.Contains("sólo las sucursales asignadas"));
    }

    // ------------------------------------------------------------------ pendientes --

    [Fact]
    public async Task Los_documentos_pendientes_dejan_fuera_lo_saldado_y_acumulan_hasta_la_fecha_final()
    {
        var e = new Escenario();
        await e.Cartera450();
        // La 1001 queda saldada del todo el 16: no debe salir; la 1002 sigue pendiente por 250.
        await e.Contabilizar(Marzo16, PostingLine.Debito(e.Caja.Id, 200m), e.CarteraCredito(200m, "1001"));

        var r = await e.Pendientes.Handle(new PendingDocumentsQuery(e.Filtros(Marzo10, Marzo31)), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;
        t.Filas.Should().ContainSingle("la 1001 se pagó completa; sólo queda la 1002");
        var f = t.Filas[0];
        f.Valores[Col(t, "Cuenta")].Should().Be("130505");
        f.Valores[Col(t, "Tercero")].Should().Be("Ana Prueba");
        f.Valores[Col(t, "Documento")].Should().Be("FV");
        f.Valores[Col(t, "Número")].Should().Be("1002");
        f.Valores[Col(t, "Fecha primer mov.")].Should().Be(Marzo15);
        f.Valores[Col(t, "Débitos")].Should().Be(250m);
        f.Valores[Col(t, "Créditos")].Should().Be(0m);
        f.Valores[Col(t, "Saldo")].Should().Be(250m);
        f.Valores[Oculta(t, "_nodo")].Should().Be($"document:130505|{e.D.Tercero.PublicId}|FV|1002");
        f.Valores[Oculta(t, "_cuenta")].Should().Be(e.Cartera.PublicId.ToString());
        t.Totales!.Valores[Col(t, "Saldo")].Should().Be(250m);
    }

    [Fact]
    public async Task Los_documentos_pendientes_se_miran_a_la_fecha_final_no_al_rango()
    {
        var e = new Escenario();
        await e.Cartera450();

        // Al 11 de marzo sólo existe la 1001 con 300 pendientes (el abono es del 12 y la 1002 del 15).
        var r = await e.Pendientes.Handle(new PendingDocumentsQuery(new FiltrosDeInforme { From = Marzo10, To = Marzo11 }), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;
        t.Filas.Should().ContainSingle();
        t.Filas[0].Valores[Col(t, "Número")].Should().Be("1001");
        t.Filas[0].Valores[Col(t, "Saldo")].Should().Be(300m, "la factura es anterior al rango pero sigue abierta: se acumula todo hasta la fecha final");
        t.Titulo.Should().Be("Documentos cruce con saldo pendiente", "sin tercero, es de todos");
    }

    // ---------------------------------------------------------------- saldo diario --

    [Fact]
    public async Task El_saldo_diario_promedio_se_calcula_dia_por_dia_incluidos_los_dias_sin_movimiento()
    {
        var e = new Escenario();
        // Caja: +100 el 1, +200 el 11, −60 el 16. Saldos: 100 × 10 días, 300 × 5 días, 240 × 16 días.
        await e.Contabilizar(Marzo1, PostingLine.Debito(e.Caja.Id, 100m), PostingLine.Credito(e.Ingreso.Id, 100m));
        await e.Contabilizar(Marzo11, PostingLine.Debito(e.Caja.Id, 200m), PostingLine.Credito(e.Ingreso.Id, 200m));
        await e.Contabilizar(Marzo16, PostingLine.Debito(e.Gasto.Id, 60m), PostingLine.Credito(e.Caja.Id, 60m));

        var r = await e.Promedio.Handle(new DailyAverageBalanceQuery(new FiltrosDeInforme { From = Marzo1, To = Marzo31, AccountPublicId = e.Caja.PublicId }), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;
        var saldo = Col(t, "Saldo");
        t.Filas.Should().HaveCount(32, "el saldo inicial y los 31 días de marzo");
        t.Filas[0].Resaltada.Should().BeTrue();
        t.Filas[0].Valores[Col(t, "Fecha")].Should().Be(new DateOnly(2026, 2, 28));
        t.Filas[0].Valores[saldo].Should().Be(0m);
        t.Filas[1].Valores[saldo].Should().Be(100m);
        t.Filas[10].Valores[saldo].Should().Be(100m, "el 10 no hubo movimiento y el saldo se mantiene");
        t.Filas[11].Valores[Col(t, "Débitos")].Should().Be(200m);
        t.Filas[11].Valores[saldo].Should().Be(300m);
        t.Filas[16].Valores[Col(t, "Créditos")].Should().Be(60m);
        t.Filas[16].Valores[saldo].Should().Be(240m);
        t.Filas[31].Valores[Col(t, "Fecha")].Should().Be(Marzo31);
        t.Filas[31].Valores[saldo].Should().Be(240m);

        // (100 × 10 + 300 × 5 + 240 × 16) / 31 = 6340 / 31 = 204,516… → 204,52
        t.Totales!.Valores[saldo].Should().Be(204.52m);
        t.Totales.Valores[Col(t, "Débitos")].Should().Be(300m);
        t.Totales.Valores[Col(t, "Créditos")].Should().Be(60m);
        t.Notas.Should().Contain(n => n.Contains("31 días") && n.Contains("FR-048"));
    }

    [Fact]
    public async Task Sin_cuenta_el_saldo_diario_responde_AccountRequired()
    {
        var e = new Escenario();

        var r = await e.Promedio.Handle(new DailyAverageBalanceQuery(new FiltrosDeInforme { From = Marzo1, To = Marzo31 }), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Report.AccountRequired");
    }

    [Fact]
    public async Task El_saldo_diario_de_una_rama_suma_sus_auxiliares_y_arranca_en_lo_anterior_al_rango()
    {
        var e = new Escenario();
        var grupo = e.D.Cuenta("1105", movimiento: false);
        var cajaMenor = e.D.Cuenta("110510");
        await e.Contabilizar(Marzo3, PostingLine.Debito(e.Caja.Id, 100m), PostingLine.Credito(e.Ingreso.Id, 100m));
        await e.Contabilizar(Marzo12, PostingLine.Debito(cajaMenor.Id, 50m), PostingLine.Credito(e.Ingreso.Id, 50m));

        var r = await e.Promedio.Handle(new DailyAverageBalanceQuery(new FiltrosDeInforme { From = Marzo10, To = Marzo12, AccountPublicId = grupo.PublicId }), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;
        var saldo = Col(t, "Saldo");
        t.Filas.Select(f => f.Valores[saldo]).Should().Equal(100m, 100m, 100m, 150m);
        // (100 + 100 + 150) / 3 = 116,666… → 116,67
        t.Totales!.Valores[saldo].Should().Be(116.67m);
    }

    // ------------------------------------------------------------------- auditoría --

    [Fact]
    public async Task Exportar_deja_el_evento_en_la_auditoria_y_consultar_en_json_no()
    {
        var e = new Escenario();
        await e.Cartera450();

        await e.EstadoDeCuenta.Handle(new ThirdPartyStatementQuery(e.Filtros(Marzo1, Marzo31)), CancellationToken.None);
        await e.Audit.DidNotReceive().AppendAsync(Arg.Any<AuditEventDocument>(), Arg.Any<CancellationToken>());

        await e.EstadoDeCuenta.Handle(new ThirdPartyStatementQuery(e.Filtros(Marzo1, Marzo31, formato: "xlsx")), CancellationToken.None);
        await e.Pendientes.Handle(new PendingDocumentsQuery(e.Filtros(Marzo1, Marzo31, formato: "pdf")), CancellationToken.None);
        await e.Promedio.Handle(new DailyAverageBalanceQuery(new FiltrosDeInforme { From = Marzo1, To = Marzo31, AccountPublicId = e.Caja.PublicId, Format = "docx" }), CancellationToken.None);

        await e.Audit.Received(3).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == "Accounting.Report.Exported" && a.Module == "Accounting"), Arg.Any<CancellationToken>());
        await e.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.NewValuesJson!.Contains("third-party-statement") && a.NewValuesJson.Contains("xlsx")), Arg.Any<CancellationToken>());
        await e.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.NewValuesJson!.Contains("pending-documents")), Arg.Any<CancellationToken>());
        await e.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.NewValuesJson!.Contains("daily-average")), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------ validadores --

    [Fact]
    public void Los_validadores_rechazan_nivel_fuera_de_rango_y_formato_desconocido()
    {
        var malos = new FiltrosDeInforme { Level = 7, Format = "csv" };
        var buenos = new FiltrosDeInforme { Level = 6, Format = "XLSX" };

        new ThirdPartyStatementQueryValidator().Validate(new ThirdPartyStatementQuery(malos)).Errors.Should().HaveCount(2);
        new PendingDocumentsQueryValidator().Validate(new PendingDocumentsQuery(malos)).Errors.Should().HaveCount(2);
        new DailyAverageBalanceQueryValidator().Validate(new DailyAverageBalanceQuery(malos)).Errors.Should().HaveCount(2);
        new ThirdPartyStatementQueryValidator().Validate(new ThirdPartyStatementQuery(buenos)).IsValid.Should().BeTrue();
    }
}
