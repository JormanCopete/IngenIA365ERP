using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Reports;

/// <summary>
/// T099 — US5 (FR-042): el libro auxiliar se recorre clase → grupo → cuenta → subcuenta → auxiliar →
/// tercero → documento cruce → comprobante → línea y en cada escalón el saldo final del padre es la
/// suma de sus hijos; los filtros y el alcance de sucursal aplican en todos los niveles; la
/// apertura es saldo inicial y el cierre no entra si no se pide.
/// </summary>
public class LedgerQueryTests
{
    private static readonly DateOnly Marzo8 = new(2026, 3, 8);
    private const int SaldoInicial = 2, Debitos = 3, Creditos = 4, SaldoFinal = 5, Nodo = 6, Tipo = 7, Cuenta = 8, Comprobante = 9;

    /// <summary>
    /// Caja (débito) e ingresos (crédito), cada una con su cadena clase → grupo → cuenta → subcuenta →
    /// auxiliar. Apertura de 1.000 sin tercero; el 3 de marzo 500 a Ana con FV 001 (antes del rango,
    /// que empieza el 8); en el rango 100 (Ana, FV 001), 200 (Ana, FV 002), 300 (Bob, sin documento,
    /// sucursal Norte) y 50 sin tercero; y un cierre de 100 que no debe verse.
    /// </summary>
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public ChartOfAccount Caja { get; }
        public ChartOfAccount Ingreso { get; }
        public Person Bob { get; }
        public AccountingDocument AnaFv001 { get; }
        public AccountingDocument BobNorte { get; }
        public LedgerQueryHandler Handler { get; }

        public Escenario()
        {
            Caja = Rama(["1", "11", "1105", "110505", "11050501"], AccountNature.Debit);
            Ingreso = Rama(["4", "41", "4135", "413505", "41350501"], AccountNature.Credit);
            Bob = new Person { FirstName = "Bob", LastName = "Segundo", TaxId = "1000000002", Status = "A", CreatedBy = "test" };
            D.Db.People.Add(Bob);
            D.Db.SaveChanges();

            Contabilizar(new PostingRequest("AP", new DateOnly(2025, 12, 31), "Apertura", ContabilidadTestData.Manual(),
                [PostingLine.Debito(Caja.Id, 1000m, "Apertura"), PostingLine.Credito(Ingreso.Id, 1000m, "Apertura")], DocumentKind.Opening));
            Contabilizar(Venta(new DateOnly(2026, 3, 3), 500m, D.Tercero, "FV", "001"));
            AnaFv001 = Contabilizar(Venta(new DateOnly(2026, 3, 9), 100m, D.Tercero, "FV", "001"));
            Contabilizar(Venta(new DateOnly(2026, 3, 10), 200m, D.Tercero, "FV", "002"));
            BobNorte = Contabilizar(Venta(new DateOnly(2026, 3, 12), 300m, Bob, null, null, D.Norte));
            Contabilizar(Venta(ContabilidadTestData.Marzo15, 50m, null, null, null));
            Contabilizar(new PostingRequest("CI", new DateOnly(2026, 3, 18), "Cierre", ContabilidadTestData.Manual(),
                [PostingLine.Debito(Ingreso.Id, 100m), PostingLine.Credito(Caja.Id, 100m)], DocumentKind.Closing));

            var emisor = new AccountingAuditEmitter(Substitute.For<IAuditAppendOnlyWriter>(), D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance);
            Handler = new LedgerQueryHandler(D.Db, D.Alcance, D.Clock, D.User, emisor);
        }

        private ChartOfAccount Rama(string[] codigos, AccountNature naturaleza)
        {
            ChartOfAccount? padre = null;
            for (var i = 0; i < codigos.Length; i++)
            {
                var ultima = i == codigos.Length - 1;
                var cuenta = D.Cuenta(codigos[i], naturaleza, movimiento: ultima);
                cuenta.Level = (byte)(i + 1);
                cuenta.ParentId = padre?.Id;
                D.Db.SaveChanges();
                padre = cuenta;
            }
            return padre!;
        }

        /// <summary>Otra venta a Ana con FV 001 dentro del rango, para que la combinación tenga más de un comprobante.</summary>
        public AccountingDocument OtraVentaAnaFv001(DateOnly fecha, decimal valor) => Contabilizar(Venta(fecha, valor, D.Tercero, "FV", "001"));

        private PostingRequest Venta(DateOnly fecha, decimal valor, Person? tercero, string? tipoCruce, string? numero, Branch? sucursal = null) =>
            new("CG", fecha, $"Venta {valor:N0}", ContabilidadTestData.Manual(),
            [
                new PostingLine { AccountId = Caja.Id, Debit = valor, Detail = "Recaudo", PersonId = tercero?.Id, CrossDocumentType = tipoCruce, CrossDocumentNumber = numero, BranchId = sucursal?.Id },
                new PostingLine { AccountId = Ingreso.Id, Credit = valor, Detail = "Venta", PersonId = tercero?.Id, BranchId = sucursal?.Id },
            ]);

        private AccountingDocument Contabilizar(PostingRequest request)
        {
            var r = D.Poster.PrepareAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            r.IsSuccess.Should().BeTrue(r.Error.Message);
            D.Db.SaveChanges();
            return r.Value;
        }

        public FiltrosDeInforme Filtros(Guid? tercero = null, bool cierre = false) =>
            new() { From = Marzo8, To = ContabilidadTestData.Hoy, Person = tercero, IncludeClosing = cierre };

        public async Task<TablaExportable> Consultar(string? nodo, FiltrosDeInforme? filtros = null)
        {
            var r = await Handler.Handle(new LedgerQuery(filtros ?? Filtros(), nodo), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error.Message);
            return r.Value;
        }
    }

    private static FilaExportable Fila(TablaExportable t, string codigo) =>
        t.Filas.Should().ContainSingle(f => (string)f.Valores[0]! == codigo, $"debe haber una fila {codigo}").Subject;

    private static decimal N(FilaExportable f, int i) => (decimal)f.Valores[i]!;

    private static void Saldos(FilaExportable f, decimal inicial, decimal debitos, decimal creditos, decimal final)
    {
        N(f, SaldoInicial).Should().Be(inicial);
        N(f, Debitos).Should().Be(debitos);
        N(f, Creditos).Should().Be(creditos);
        N(f, SaldoFinal).Should().Be(final);
    }

    private static string Texto(FilaExportable f, int i) => (string)f.Valores[i]!;

    [Fact]
    public async Task La_raiz_muestra_las_clases_con_saldo_y_la_apertura_es_saldo_inicial()
    {
        var e = new Escenario();

        var raiz = await e.Consultar(null);

        raiz.Titulo.Should().Be("Libro auxiliar");
        raiz.Columnas.Where(c => c.EsOculta).Select(c => c.Clave).Should().Equal("_nodo", "_tipo", "_cuenta", "_comprobante");
        raiz.Filas.Should().HaveCount(2);
        var clase1 = Fila(raiz, "1");
        // 1.000 de apertura + 500 del 3 de marzo son saldo inicial; en el rango 100 + 200 + 300 + 50.
        Saldos(clase1, 1500m, 650m, 0m, 2150m);
        Texto(clase1, Nodo).Should().Be("account:1");
        Texto(clase1, Tipo).Should().Be("account");
        clase1.Valores[Cuenta].Should().Be(e.D.Db.ChartOfAccounts.Single(a => a.Code == "1").PublicId);
        var clase4 = Fila(raiz, "4");
        Saldos(clase4, 1500m, 0m, 650m, 2150m);
        raiz.Totales.Should().NotBeNull();
        N(raiz.Totales!, Debitos).Should().Be(650m);
        N(raiz.Totales!, Creditos).Should().Be(650m);
        // Activo 2.150 + ingresos 2.150 = 4.300 no es el saldo de nada: como en el balance de prueba, vacío y con la nota.
        raiz.Totales!.Valores[SaldoInicial].Should().BeNull("las clases mezclan naturalezas");
        raiz.Totales!.Valores[SaldoFinal].Should().BeNull("las clases mezclan naturalezas");
        raiz.Notas.Should().Contain("Totales: suma de débitos y créditos del período; los saldos no se suman porque mezclan naturalezas.");
        raiz.Notas.Should().Contain(n => n.StartsWith("Período: 08/03/2026"));
    }

    [Fact]
    public async Task Bajo_una_cuenta_el_total_suma_los_saldos_de_las_hijas_porque_comparten_naturaleza()
    {
        var e = new Escenario();

        var grupos = await e.Consultar("account:1");
        Saldos(grupos.Totales!, 1500m, 650m, 0m, 2150m);
        grupos.Notas.Should().NotContain(n => n.StartsWith("Totales:"));

        var terceros = await e.Consultar("account:11050501");
        Saldos(terceros.Totales!, 1500m, 650m, 0m, 2150m);

        var documentos = await e.Consultar($"person:11050501|{e.D.Tercero.PublicId}");
        Saldos(documentos.Totales!, 500m, 300m, 0m, 800m);
    }

    [Fact]
    public async Task Cada_nivel_del_plan_devuelve_los_hijos_y_el_saldo_del_padre_es_la_suma_de_los_hijos()
    {
        var e = new Escenario();
        var padre = Fila(await e.Consultar(null), "1");

        foreach (var (nodo, hijo) in new[] { ("account:1", "11"), ("account:11", "1105"), ("account:1105", "110505"), ("account:110505", "11050501") })
        {
            var nivel = await e.Consultar(nodo);
            nivel.Filas.Should().ContainSingle();
            var fila = Fila(nivel, hijo);
            N(fila, SaldoFinal).Should().Be(N(padre, SaldoFinal), $"el saldo de {hijo} es el de su padre");
            nivel.Filas.Sum(f => N(f, SaldoFinal)).Should().Be(N(padre, SaldoFinal));
            Texto(fila, Nodo).Should().Be($"account:{hijo}");
            nivel.Titulo.Should().StartWith("Libro auxiliar · ").And.Contain(nodo["account:".Length..]);
            padre = fila;
        }
    }

    [Fact]
    public async Task La_auxiliar_se_abre_por_terceros_y_el_tercero_por_documentos_cruce()
    {
        var e = new Escenario();

        var terceros = await e.Consultar("account:11050501");

        terceros.Titulo.Should().Be("Libro auxiliar · 11050501 Cuenta 11050501");
        terceros.Filas.Should().HaveCount(3);
        var ana = Fila(terceros, e.D.Tercero.TaxId);
        Saldos(ana, 500m, 300m, 0m, 800m);
        Texto(ana, 1).Should().Be("Ana Prueba");
        Texto(ana, Nodo).Should().Be($"person:11050501|{e.D.Tercero.PublicId}");
        Texto(ana, Tipo).Should().Be("person");
        ana.Valores[Cuenta].Should().Be(e.Caja.PublicId);
        Saldos(Fila(terceros, e.Bob.TaxId), 0m, 300m, 0m, 300m);
        var sinTercero = terceros.Filas.Last();
        Texto(sinTercero, 1).Should().Be("Sin tercero");
        Saldos(sinTercero, 1000m, 50m, 0m, 1050m);
        Texto(sinTercero, Nodo).Should().Be("person:11050501|none");
        terceros.Filas.Sum(f => N(f, SaldoFinal)).Should().Be(2150m, "los terceros suman la auxiliar");

        var documentos = await e.Consultar(Texto(ana, Nodo));

        documentos.Titulo.Should().Be("Libro auxiliar · 11050501 Cuenta 11050501 · Ana Prueba (1000000001)");
        documentos.Filas.Should().HaveCount(2);
        var fv001 = Fila(documentos, "FV 001");
        Saldos(fv001, 500m, 100m, 0m, 600m);
        Texto(fv001, 1).Should().Be("Factura de venta");
        Texto(fv001, Nodo).Should().Be($"document:11050501|{e.D.Tercero.PublicId}|FV|001");
        Texto(fv001, Tipo).Should().Be("document");
        Saldos(Fila(documentos, "FV 002"), 0m, 200m, 0m, 200m);
        documentos.Filas.Sum(f => N(f, SaldoFinal)).Should().Be(N(ana, SaldoFinal));

        var sinDocumento = await e.Consultar($"person:11050501|{e.Bob.PublicId}");
        sinDocumento.Filas.Should().ContainSingle().Which.Valores[1].Should().Be("Sin documento");
        Texto(sinDocumento.Filas[0], Nodo).Should().Be($"document:11050501|{e.Bob.PublicId}|none|none");
    }

    [Fact]
    public async Task El_documento_lista_sus_comprobantes_con_saldo_corrido_y_el_comprobante_sus_lineas()
    {
        var e = new Escenario();

        var comprobantes = await e.Consultar($"document:11050501|{e.D.Tercero.PublicId}|FV|001");

        comprobantes.Titulo.Should().Be("Libro auxiliar · 11050501 Cuenta 11050501 · Ana Prueba (1000000001) · FV 001 (Factura de venta)");
        comprobantes.Filas.Should().ContainSingle("el del 3 de marzo queda antes del rango: es saldo inicial");
        var fila = comprobantes.Filas[0];
        Texto(fila, 0).Should().Be($"CG-{e.AnaFv001.Number}");
        Texto(fila, 1).Should().Be("09/03/2026 · Venta 100");
        Saldos(fila, 500m, 100m, 0m, 600m);
        Texto(fila, Nodo).Should().Be($"voucher:{e.AnaFv001.PublicId}");
        Texto(fila, Tipo).Should().Be("voucher");
        fila.Valores[Comprobante].Should().Be(e.AnaFv001.PublicId);

        var lineas = await e.Consultar(Texto(fila, Nodo));

        lineas.Titulo.Should().Be($"Libro auxiliar · CG-{e.AnaFv001.Number} · 09/03/2026 Venta 100");
        lineas.Filas.Should().HaveCount(2);
        var recaudo = Fila(lineas, "11050501");
        Texto(recaudo, 1).Should().Be("Cuenta 11050501 · Ana Prueba · FV 001 · Recaudo");
        N(recaudo, Debitos).Should().Be(100m);
        N(recaudo, Creditos).Should().Be(0m);
        recaudo.Valores[SaldoInicial].Should().BeNull();
        recaudo.Valores[Nodo].Should().BeNull("una línea no se profundiza más");
        Texto(recaudo, Tipo).Should().Be("line");
        recaudo.Valores[Cuenta].Should().Be(e.Caja.PublicId);
        recaudo.Valores[Comprobante].Should().Be(e.AnaFv001.PublicId);
        Fila(lineas, "41350501").Valores[Comprobante].Should().Be(e.AnaFv001.PublicId);
        N(lineas.Totales!, Debitos).Should().Be(100m);
        N(lineas.Totales!, Creditos).Should().Be(100m);
        lineas.Totales!.Valores[SaldoInicial].Should().BeNull("las líneas no llevan saldo");
        lineas.Totales!.Valores[SaldoFinal].Should().BeNull("las líneas no llevan saldo");
        lineas.Notas.Should().NotContain(n => n.StartsWith("Totales:"), "la nota de naturalezas es para filas con saldo");
    }

    [Fact]
    public async Task El_total_de_los_comprobantes_lleva_el_saldo_inicial_de_la_combinacion_y_el_final_corrido_no_la_suma_de_corridos()
    {
        var e = new Escenario();
        e.OtraVentaAnaFv001(new DateOnly(2026, 3, 11), 100m);
        e.OtraVentaAnaFv001(new DateOnly(2026, 3, 13), 200m);

        var comprobantes = await e.Consultar($"document:11050501|{e.D.Tercero.PublicId}|FV|001");

        comprobantes.Filas.Should().HaveCount(3);
        Saldos(comprobantes.Filas[0], 500m, 100m, 0m, 600m);
        Saldos(comprobantes.Filas[1], 600m, 100m, 0m, 700m);
        Saldos(comprobantes.Filas[2], 700m, 200m, 0m, 900m);
        // Sumar los corridos daría 1.800 y 2.200: el total es el inicial de la combinación y el final del último comprobante.
        Saldos(comprobantes.Totales!, 500m, 400m, 0m, 900m);
    }

    [Fact]
    public async Task El_filtro_por_tercero_aplica_en_todos_los_niveles()
    {
        var e = new Escenario();
        var filtros = e.Filtros(tercero: e.D.Tercero.PublicId);

        var raiz = await e.Consultar(null, filtros);
        Saldos(Fila(raiz, "1"), 500m, 300m, 0m, 800m);

        var terceros = await e.Consultar("account:11050501", filtros);
        terceros.Filas.Should().ContainSingle().Which.Valores[1].Should().Be("Ana Prueba");
        raiz.Notas.Should().Contain(n => n.Contains("tercero Ana Prueba (1000000001)"));
    }

    [Fact]
    public async Task El_alcance_de_sucursal_deja_fuera_lo_de_otra_sucursal_en_todos_los_niveles()
    {
        var e = new Escenario();
        e.D.RestringirA(e.D.Principal);

        var raiz = await e.Consultar(null);
        Saldos(Fila(raiz, "1"), 1500m, 350m, 0m, 1850m);

        var terceros = await e.Consultar("account:11050501");
        terceros.Filas.Should().NotContain(f => (string)f.Valores[0]! == e.Bob.TaxId, "lo de Bob fue en Norte");

        var lineas = await e.Consultar($"voucher:{e.BobNorte.PublicId}");
        lineas.Filas.Should().BeEmpty("las líneas del comprobante de Norte no se ven desde Principal");
    }

    [Fact]
    public async Task El_cierre_solo_entra_si_se_pide()
    {
        var e = new Escenario();

        Saldos(Fila(await e.Consultar("account:110505"), "11050501"), 1500m, 650m, 0m, 2150m);
        Saldos(Fila(await e.Consultar("account:110505", e.Filtros(cierre: true)), "11050501"), 1500m, 650m, 100m, 2050m);
    }

    [Theory]
    [InlineData("cuenta:1")]
    [InlineData("account:")]
    [InlineData("account:1|x")]
    [InlineData("person:11050501")]
    [InlineData("person:11050501|no-es-guid")]
    [InlineData("document:11050501|none|FV")]
    [InlineData("voucher:abc")]
    [InlineData("sin-dos-puntos")]
    public async Task Un_nodo_mal_formado_es_un_error_de_negocio(string nodo)
    {
        var e = new Escenario();

        var r = await e.Handler.Handle(new LedgerQuery(e.Filtros(), nodo), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Report.InvalidNode");
    }

    [Fact]
    public async Task Una_cuenta_un_tercero_o_un_comprobante_que_no_existen_responden_su_NotFound()
    {
        var e = new Escenario();

        (await e.Handler.Handle(new LedgerQuery(e.Filtros(), "account:9999999"), CancellationToken.None)).Error.Code.Should().Be("Accounting.Account.NotFound");
        (await e.Handler.Handle(new LedgerQuery(e.Filtros(), $"person:11050501|{Guid.NewGuid()}"), CancellationToken.None)).Error.Code.Should().Be("Accounting.Person.NotFound");
        (await e.Handler.Handle(new LedgerQuery(e.Filtros(), $"voucher:{Guid.NewGuid()}"), CancellationToken.None)).Error.Code.Should().Be("Accounting.Document.NotFound");
        (await e.Handler.Handle(new LedgerQuery(e.Filtros(), $"document:11050501|none|ZZ|1"), CancellationToken.None)).Error.Code.Should().Be("Accounting.CrossDocumentType.NotFound");
    }

    [Fact]
    public async Task Exportar_deja_el_evento_de_auditoria_con_el_informe_y_el_nodo()
    {
        var e = new Escenario();
        var escritor = Substitute.For<IAuditAppendOnlyWriter>();
        var handler = new LedgerQueryHandler(e.D.Db, e.D.Alcance, e.D.Clock, e.D.User,
            new AccountingAuditEmitter(escritor, e.D.User, e.D.Clock, NullLogger<AccountingAuditEmitter>.Instance));

        var json = await handler.Handle(new LedgerQuery(e.Filtros(), "account:1"), CancellationToken.None);
        json.IsSuccess.Should().BeTrue();
        await escritor.DidNotReceive().AppendAsync(Arg.Any<AuditEventDocument>(), Arg.Any<CancellationToken>());

        var xlsx = await handler.Handle(new LedgerQuery(e.Filtros() with { Format = "xlsx" }, "account:1"), CancellationToken.None);
        xlsx.IsSuccess.Should().BeTrue();
        await escritor.Received(1).AppendAsync(
            Arg.Is<AuditEventDocument>(a => a.Action == "Accounting.Report.Exported" && a.NewValuesJson!.Contains("\"informe\":\"ledger\"") && a.NewValuesJson.Contains("\"node\":\"account:1\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void El_validador_acota_nivel_formato_y_largo_del_nodo()
    {
        var v = new LedgerQueryValidator();

        v.Validate(new LedgerQuery(new FiltrosDeInforme(), null)).IsValid.Should().BeTrue();
        v.Validate(new LedgerQuery(new FiltrosDeInforme { Level = 7 }, null)).IsValid.Should().BeFalse();
        v.Validate(new LedgerQuery(new FiltrosDeInforme { Format = "csv" }, null)).IsValid.Should().BeFalse();
        v.Validate(new LedgerQuery(new FiltrosDeInforme { Format = "PDF" }, null)).IsValid.Should().BeTrue();
        v.Validate(new LedgerQuery(new FiltrosDeInforme(), new string('x', LedgerQueryHandler.LargoMaximoDeNodo + 1))).IsValid.Should().BeFalse();
    }
}
