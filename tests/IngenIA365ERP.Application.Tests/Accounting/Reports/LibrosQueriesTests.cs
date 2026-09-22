using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Reports;

/// <summary>
/// Feature 009 E2 (FR-044, FR-046): los cuatro libros leen el mismo libro y dicen lo mismo. Lo
/// que se fija aquí: el balance cuadra siempre (también tras reversar), un saldo es la suma de sus
/// líneas, los borradores no existen, el cierre entra sólo pedido, la apertura es saldo inicial
/// caiga donde caiga, el alcance de sucursal recorta, los filtros recortan, los niveles agregan
/// hacia arriba, y exportar deja rastro en la auditoría.
/// </summary>
public class LibrosQueriesTests
{
    private static readonly DateOnly Marzo15 = ContabilidadTestData.Marzo15;
    private static readonly DateOnly Marzo10 = new(2026, 3, 10);
    private static readonly DateOnly Apertura = new(2025, 12, 31);

    /// <summary>
    /// Un plan chico con la cadena completa: clase → grupo → cuenta → auxiliar (nivel 5, el de
    /// movimiento de la empresa de prueba). <see cref="ContabilidadTestData.Cuenta"/> no enlaza
    /// padres, así que se hace aquí.
    /// </summary>
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public IAuditAppendOnlyWriter Audit { get; } = Substitute.For<IAuditAppendOnlyWriter>();
        public AccountingAuditEmitter Emisor { get; }

        public ChartOfAccount Caja { get; }
        public ChartOfAccount Banco { get; }
        public ChartOfAccount Cxc { get; }
        public ChartOfAccount Capital { get; }
        public ChartOfAccount Excedente { get; }
        public ChartOfAccount Ingreso { get; }
        public ChartOfAccount Gasto { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Audit, D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
            Caja = Rama("1", "11", "1105", "110505", AccountNature.Debit);
            Banco = Rama("1", "11", "1110", "111005", AccountNature.Debit);
            Cxc = Rama("1", "13", "1305", "130505", AccountNature.Debit, tercero: true);
            Capital = Rama("3", "31", "3105", "310505", AccountNature.Credit);
            Excedente = Rama("3", "35", "3505", "350505", AccountNature.Credit);
            Ingreso = Rama("4", "41", "4135", "413505", AccountNature.Credit);
            Gasto = Rama("5", "51", "5105", "510505", AccountNature.Debit);
        }

        private ChartOfAccount Rama(string clase, string grupo, string cuenta, string auxiliar, AccountNature nature, bool tercero = false)
        {
            var c1 = Agrupacion(clase, 1, nature, null);
            var c2 = Agrupacion(grupo, 2, nature, c1);
            var c4 = Agrupacion(cuenta, 4, nature, c2);
            var aux = D.Cuenta(auxiliar, nature, tercero: tercero);
            aux.ParentId = c4.Id;
            D.Db.SaveChanges();
            return aux;
        }

        private ChartOfAccount Agrupacion(string code, byte nivel, AccountNature nature, ChartOfAccount? padre)
        {
            var existente = D.Db.ChartOfAccounts.FirstOrDefault(a => a.Code == code);
            if (existente is not null) return existente;
            var c = D.Cuenta(code, nature, movimiento: false);
            c.Level = nivel;
            c.ParentId = padre?.Id;
            D.Db.SaveChanges();
            return c;
        }

        public TrialBalanceQueryHandler Balance() => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);
        public GeneralLedgerQueryHandler Mayor() => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);
        public JournalQueryHandler Diario() => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);
        public VoucherListQueryHandler Relacion() => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);
        public LedgerQueryHandler Auxiliar() => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);
        public ThirdPartyStatementQueryHandler EstadoDeCuenta() => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);

        /// <summary>
        /// Un ejercicio anterior con su diciembre abierto, para contabilizar movimientos y el cierre del
        /// año 1 y consultar el año 2 (los datos de prueba sólo traen 2026).
        /// </summary>
        public void AbrirDiciembreDe2025()
        {
            var ejercicio = new FiscalYear { Year = 2025, CreatedBy = "test" };
            D.Db.FiscalYears.Add(ejercicio);
            D.Db.SaveChanges();
            D.Db.AccountingPeriods.Add(new AccountingPeriod
            {
                FiscalYearId = ejercicio.Id, Month = 12, StartDate = new DateOnly(2025, 12, 1), EndDate = new DateOnly(2025, 12, 31),
                Status = PeriodStatus.Open, CreatedBy = "test",
            });
            D.Db.SaveChanges();
        }

        /// <summary>Contabiliza y guarda un comprobante cuadrado; devuelve el documento.</summary>
        public async Task<AccountingDocument> ContabilizarAsync(PostingRequest request)
        {
            var r = await D.Poster.PrepareAsync(request, CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error.Message);
            await D.Db.SaveChangesAsync();
            return r.Value;
        }

        public Task<AccountingDocument> ContabilizarAsync(ChartOfAccount debito, ChartOfAccount credito, decimal valor = 100m, DateOnly? fecha = null, string tipo = "CG", AccountingOrigin? origen = null, DocumentKind kind = DocumentKind.Regular, int? sucursal = null, int? tercero = null) =>
            ContabilizarAsync(new PostingRequest(tipo, fecha ?? Marzo15, "Prueba", origen ?? ContabilidadTestData.Manual(),
            [
                new PostingLine { AccountId = debito.Id, Debit = valor, Detail = "D", BranchId = sucursal, PersonId = tercero },
                new PostingLine { AccountId = credito.Id, Credit = valor, Detail = "C", BranchId = sucursal },
            ], kind));

        public async Task<TablaExportable> BalanceAsync(FiltrosDeInforme? filtros = null)
        {
            var r = await Balance().Handle(new TrialBalanceQuery(filtros ?? new FiltrosDeInforme()), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error.Message);
            return r.Value;
        }
    }

    private static FilaExportable Fila(TablaExportable t, string codigo) =>
        t.Filas.Should().ContainSingle(f => Equals(f.Valores[0], codigo), $"la cuenta {codigo} tiene que estar una sola vez").Subject;

    private static int Columna(TablaExportable t, string nombre) => t.Columnas.ToList().FindIndex(c => c.Nombre == nombre);

    private static decimal Numero(FilaExportable f, int indice) => (decimal)f.Valores[indice]!;

    // ---------------------------------------------------------------------------- balance de prueba --

    [Fact]
    public async Task El_balance_cuadra_tras_contabilizar_y_tras_reversar_y_muestra_los_dos_movimientos()
    {
        var e = new Escenario();
        var original = await e.ContabilizarAsync(e.Caja, e.Ingreso);

        var t = await e.BalanceAsync();
        t.Totales.Should().NotBeNull();
        Numero(t.Totales!, 3).Should().Be(100m);
        Numero(t.Totales!, 4).Should().Be(100m);
        t.Totales!.Valores[2].Should().BeNull("los saldos no se suman: mezclan naturalezas");
        t.Totales!.Valores[5].Should().BeNull();
        Numero(Fila(t, "110505"), 5).Should().Be(100m, "débito-positivo en una cuenta de naturaleza débito");
        Numero(Fila(t, "413505"), 5).Should().Be(100m, "crédito-positivo en una de naturaleza crédito");

        var reversa = await e.D.Poster.PrepareReversalAsync(original, Marzo15, "Se duplicó", ContabilidadTestData.Manual(), CancellationToken.None);
        reversa.IsSuccess.Should().BeTrue(reversa.Error.Message);
        await e.D.Db.SaveChangesAsync();

        t = await e.BalanceAsync();
        Numero(t.Totales!, 3).Should().Be(200m, "el original y su espejo cuentan los dos (decisión 7)");
        Numero(t.Totales!, 4).Should().Be(200m);
        var caja = Fila(t, "110505");
        Numero(caja, 3).Should().Be(100m);
        Numero(caja, 4).Should().Be(100m);
        Numero(caja, 5).Should().Be(0m, "se netean");
    }

    [Fact]
    public async Task El_saldo_de_una_cuenta_es_la_suma_de_sus_lineas_y_lleva_las_claves_de_profundizacion()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 100m);
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 250m, fecha: Marzo10);
        await e.ContabilizarAsync(e.Banco, e.Caja, 30m);

        var t = await e.BalanceAsync();
        var caja = Fila(t, "110505");
        Numero(caja, 2).Should().Be(0m);
        Numero(caja, 3).Should().Be(350m);
        Numero(caja, 4).Should().Be(30m);
        Numero(caja, 5).Should().Be(320m);
        caja.Valores[Columna(t, "Nodo")].Should().Be("account:110505");
        caja.Valores[Columna(t, "Cuenta")].Should().Be(e.Caja.PublicId);
        caja.Seccion.Should().Be("1 Cuenta 1");

        t.Columnas.Where(c => c.EsOculta).Select(c => c.Clave).Should().BeEquivalentTo(new[] { "_nodo", "_cuenta" });
        t.Notas.Should().Contain(n => n.StartsWith("Período:")).And.Contain(EncabezadoDeInforme.NotaDeSignos);
        t.Subtitulo.Should().StartWith("Nivel 5", "el nivel de movimiento de la empresa cuando no se pide otro");
    }

    [Fact]
    public async Task Los_borradores_no_existen_para_el_balance()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Ingreso);

        var borrador = e.D.Poster.NuevoBorrador();
        borrador.Date = Marzo15;
        borrador.Description = "Sin contabilizar";
        var linea = e.D.Poster.NuevaLineaDeBorrador();
        linea.LineNumber = 1; linea.AccountId = e.Caja.Id; linea.BranchId = e.D.Principal.Id; linea.Debit = 999m; linea.Date = Marzo15;
        borrador.Lines.Add(linea);
        var contra = e.D.Poster.NuevaLineaDeBorrador();
        contra.LineNumber = 2; contra.AccountId = e.Ingreso.Id; contra.BranchId = e.D.Principal.Id; contra.Credit = 999m; contra.Date = Marzo15;
        borrador.Lines.Add(contra);
        await e.D.Db.SaveChangesAsync();

        var t = await e.BalanceAsync();
        Numero(Fila(t, "110505"), 5).Should().Be(100m);
        Numero(t.Totales!, 3).Should().Be(100m);

        var relacion = await e.Relacion().Handle(new VoucherListQuery(new FiltrosDeInforme()), CancellationToken.None);
        relacion.Value.Filas.Should().ContainSingle("el borrador tampoco se relaciona");
    }

    [Fact]
    public async Task El_cierre_queda_fuera_salvo_que_se_pida()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Gasto, e.Caja);
        // El cierre va el 31/12 (es la única fecha que el contrato le admite, E2/US6); el balance se pide hasta ahí.
        var finDeAnio = new DateOnly(2026, 12, 31);
        await e.ContabilizarAsync(e.Caja, e.Gasto, fecha: finDeAnio, tipo: "CI", kind: DocumentKind.Closing);

        var sinCierre = await e.BalanceAsync(new FiltrosDeInforme { To = finDeAnio });
        Numero(Fila(sinCierre, "510505"), 5).Should().Be(100m);
        Numero(sinCierre.Totales!, 3).Should().Be(100m);

        var conCierre = await e.BalanceAsync(new FiltrosDeInforme { To = finDeAnio, IncludeClosing = true });
        Numero(Fila(conCierre, "510505"), 5).Should().Be(0m, "el cierre cancela el gasto");
        Numero(conCierre.Totales!, 3).Should().Be(200m);
        Numero(conCierre.Totales!, 4).Should().Be(200m);
        conCierre.Notas.Should().Contain(n => n.Contains("incluye el cierre"));
    }

    [Fact]
    public async Task El_cierre_del_ejercicio_anterior_forma_el_saldo_inicial_del_siguiente_en_balance_auxiliar_y_estado_de_cuenta()
    {
        // Año 1 (2025): una venta a Ana de 100 y el cierre del 31/12 que lleva el ingreso al excedente.
        // Año 2 (2026): otra venta de 40. Sin pedir el cierre, el año 2 tiene que arrancar con los
        // resultados en cero y el excedente en la 35 (US6, escenario 4); hasta corregirlo, la 4135
        // traía 100 de saldo inicial y la 3505 no aparecía, y el balance igual cuadraba.
        var e = new Escenario();
        e.AbrirDiciembreDe2025();
        var venta2025 = new DateOnly(2025, 12, 10);
        var cierre2025 = new DateOnly(2025, 12, 31);
        await e.ContabilizarAsync(new PostingRequest("CG", venta2025, "Venta a Ana", ContabilidadTestData.Manual(),
        [
            new PostingLine { AccountId = e.Cxc.Id, Debit = 100m, Detail = "Factura", PersonId = e.D.Tercero.Id },
            new PostingLine { AccountId = e.Ingreso.Id, Credit = 100m, Detail = "Venta", PersonId = e.D.Tercero.Id },
        ]));
        await e.ContabilizarAsync(new PostingRequest("CI", cierre2025, "Cierre 2025", ContabilidadTestData.Manual(),
        [
            new PostingLine { AccountId = e.Ingreso.Id, Debit = 100m, Detail = "Cierre", PersonId = e.D.Tercero.Id },
            new PostingLine { AccountId = e.Excedente.Id, Credit = 100m, Detail = "Excedente" },
        ], DocumentKind.Closing));
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 40m);

        // Balance de prueba del año 2 con la bandera por defecto.
        var balance = await e.BalanceAsync();
        var ingreso = Fila(balance, "413505");
        Numero(ingreso, 2).Should().Be(0m, "el cierre de 2025 ya canceló el ingreso de 2025");
        Numero(ingreso, 4).Should().Be(40m);
        Numero(ingreso, 5).Should().Be(40m);
        Numero(Fila(balance, "350505"), 2).Should().Be(100m, "el excedente del año 1 es saldo inicial del año 2");
        Numero(Fila(balance, "350505"), 5).Should().Be(100m);
        Numero(Fila(balance, "130505"), 2).Should().Be(100m);
        Numero(balance.Totales!, 3).Should().Be(40m, "el cierre del año anterior no es movimiento del período");
        Numero(balance.Totales!, 4).Should().Be(40m);

        // El del propio ejercicio sigue fuera salvo que se pida: en diciembre de 2025 el ingreso está vivo.
        var diciembre = await e.BalanceAsync(new FiltrosDeInforme { From = new DateOnly(2025, 12, 1), To = cierre2025 });
        Numero(Fila(diciembre, "413505"), 5).Should().Be(100m);
        diciembre.Filas.Select(f => f.Valores[0]).Should().NotContain("350505");
        var diciembreCerrado = await e.BalanceAsync(new FiltrosDeInforme { From = new DateOnly(2025, 12, 1), To = cierre2025, IncludeClosing = true });
        Numero(Fila(diciembreCerrado, "413505"), 5).Should().Be(0m);
        Numero(Fila(diciembreCerrado, "350505"), 5).Should().Be(100m);

        // El libro auxiliar dice lo mismo desde la raíz.
        var auxiliar = await e.Auxiliar().Handle(new LedgerQuery(new FiltrosDeInforme(), null), CancellationToken.None);
        auxiliar.IsSuccess.Should().BeTrue(auxiliar.Error.Message);
        Numero(Fila(auxiliar.Value, "4"), 2).Should().Be(0m);
        Numero(Fila(auxiliar.Value, "4"), 5).Should().Be(40m);
        Numero(Fila(auxiliar.Value, "3"), 2).Should().Be(100m);

        // Y el estado de cuenta de Ana no le inventa un saldo inicial en el ingreso: la cuenta quedó
        // saldada en 2025 y sin movimiento en 2026, así que ni se lista; la cartera sí, con sus 100.
        var estado = await e.EstadoDeCuenta().Handle(new ThirdPartyStatementQuery(new FiltrosDeInforme { Person = e.D.Tercero.PublicId }), CancellationToken.None);
        estado.IsSuccess.Should().BeTrue(estado.Error.Message);
        estado.Value.Filas.Should().NotContain(f => Equals(f.Valores[2], "413505"));
        var cartera = estado.Value.Filas.Should().ContainSingle(f => Equals(f.Valores[2], "130505")).Subject;
        cartera.Valores[4].Should().Be("Saldo inicial");
        cartera.Valores[7].Should().Be(100m);

        // El diario del año 2 tampoco lista el cierre del año 1.
        var diario = await e.Diario().Handle(new JournalQuery(new FiltrosDeInforme()), CancellationToken.None);
        diario.Value.Filas.Should().OnlyContain(f => Equals(f.Valores[1], "CG-2"));
    }

    [Fact]
    public async Task La_apertura_es_saldo_inicial_aunque_su_fecha_caiga_en_el_rango()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Capital, fecha: Apertura, tipo: "AP", kind: DocumentKind.Opening);
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 40m);

        // El rango arranca antes de la apertura: igual es saldo inicial, no débito del período (decisión 9).
        var t = await e.BalanceAsync(new FiltrosDeInforme { From = new DateOnly(2025, 12, 1) });
        var caja = Fila(t, "110505");
        Numero(caja, 2).Should().Be(100m);
        Numero(caja, 3).Should().Be(40m);
        Numero(caja, 5).Should().Be(140m);
        Numero(Fila(t, "310505"), 2).Should().Be(100m, "crédito-positivo en el patrimonio");
        Numero(t.Totales!, 3).Should().Be(40m, "la apertura no es movimiento del período");
        Numero(t.Totales!, 4).Should().Be(40m);

        // Con el rango por defecto (1 de enero a hoy) dice lo mismo.
        var porDefecto = await e.BalanceAsync();
        Numero(Fila(porDefecto, "110505"), 2).Should().Be(100m);
        Numero(Fila(porDefecto, "110505"), 5).Should().Be(140m);

        // Y el diario y la relación no la listan.
        var diario = await e.Diario().Handle(new JournalQuery(new FiltrosDeInforme { From = new DateOnly(2025, 12, 1) }), CancellationToken.None);
        diario.Value.Filas.Should().OnlyContain(f => Equals(f.Valores[1], "CG-1"));
    }

    [Fact]
    public async Task El_alcance_de_sucursal_deja_fuera_la_otra_sucursal_en_los_cuatro_libros()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 100m, sucursal: e.D.Principal.Id);
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 25m, sucursal: e.D.Norte.Id);
        e.D.RestringirA(e.D.Norte);

        var balance = await e.BalanceAsync();
        Numero(Fila(balance, "110505"), 5).Should().Be(25m);
        Numero(balance.Totales!, 3).Should().Be(25m);
        balance.Notas.Should().Contain(n => n.Contains("sólo las sucursales asignadas"));

        var mayor = await e.Mayor().Handle(new GeneralLedgerQuery(new FiltrosDeInforme()), CancellationToken.None);
        Numero(Fila(mayor.Value, "1105"), 5).Should().Be(25m);

        var diario = await e.Diario().Handle(new JournalQuery(new FiltrosDeInforme()), CancellationToken.None);
        diario.Value.Filas.Should().HaveCount(2).And.OnlyContain(f => Equals(f.Valores[1], "CG-2"));

        var relacion = await e.Relacion().Handle(new VoucherListQuery(new FiltrosDeInforme()), CancellationToken.None);
        relacion.Value.Filas.Should().ContainSingle().Which.Valores[2].Should().Be(2L);
    }

    [Fact]
    public async Task Filtra_por_tercero_por_origen_y_por_tipo_de_comprobante()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Cxc, e.Ingreso, 100m, tercero: e.D.Tercero.Id);
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 50m);
        await e.ContabilizarAsync(e.Gasto, e.Banco, 70m, tipo: "NM", origen: ContabilidadTestData.Nomina());

        var porTercero = await e.BalanceAsync(new FiltrosDeInforme { Person = e.D.Tercero.PublicId });
        porTercero.Filas.Where(f => f.Valores[0] is string c && c.Length == 6).Select(f => f.Valores[0]).Should().Equal("130505");
        Numero(Fila(porTercero, "130505"), 5).Should().Be(100m);
        porTercero.Notas.Should().Contain(n => n.Contains("Ana Prueba"));

        var porOrigen = await e.BalanceAsync(new FiltrosDeInforme { Origin = "nom" });
        porOrigen.Filas.Where(f => f.Valores[0] is string c && c.Length == 6).Select(f => f.Valores[0]).Should().Equal("111005", "510505");
        Numero(porOrigen.Totales!, 3).Should().Be(70m);

        var porTipo = await e.BalanceAsync(new FiltrosDeInforme { VoucherType = "CG" });
        Numero(porTipo.Totales!, 3).Should().Be(150m);
        porTipo.Filas.Select(f => f.Valores[0]).Should().NotContain("510505");

        var terceroInexistente = await e.Balance().Handle(new TrialBalanceQuery(new FiltrosDeInforme { Person = Guid.NewGuid() }), CancellationToken.None);
        terceroInexistente.Error.Code.Should().Be("Accounting.Person.NotFound");
    }

    [Fact]
    public async Task Los_niveles_agregan_hacia_arriba_y_la_clase_suma_sus_cuentas()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 100m);
        await e.ContabilizarAsync(e.Banco, e.Ingreso, 60m);
        await e.ContabilizarAsync(e.Gasto, e.Caja, 10m);

        var completo = await e.BalanceAsync();
        Numero(Fila(completo, "1"), 5).Should().Be(150m, "caja 90 + banco 60");
        Numero(Fila(completo, "11"), 5).Should().Be(150m);
        Numero(Fila(completo, "1105"), 5).Should().Be(90m);
        Numero(Fila(completo, "4"), 5).Should().Be(160m);
        Numero(Fila(completo, "5"), 5).Should().Be(10m);
        Fila(completo, "1").Resaltada.Should().BeTrue("la clase se destaca");

        var clases = await e.BalanceAsync(new FiltrosDeInforme { Level = 1 });
        clases.Filas.Select(f => f.Valores[0]).Should().Equal("1", "4", "5");
        Numero(clases.Totales!, 3).Should().Be(170m, "los totales no dependen del nivel");

        var grupos = await e.BalanceAsync(new FiltrosDeInforme { Level = 2 });
        grupos.Filas.Select(f => f.Valores[0]).Should().Equal("1", "11", "4", "41", "5", "51");
    }

    [Fact]
    public async Task Con_terceros_abre_cada_auxiliar_por_tercero_con_el_nodo_del_libro_auxiliar()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Cxc, e.Ingreso, 100m, tercero: e.D.Tercero.Id);
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 50m);

        var t = await e.BalanceAsync(new FiltrosDeInforme { WithThirdParties = true });

        var indice = t.Filas.ToList().FindIndex(f => Equals(f.Valores[0], "130505"));
        var ana = t.Filas[indice + 1];
        ana.Valores[0].Should().Be("1000000001");
        ana.Valores[1].Should().Be("Ana Prueba");
        Numero(ana, 5).Should().Be(100m);
        ana.Seccion.Should().Be("130505 Cuenta 130505");
        ana.Valores[Columna(t, "Nodo")].Should().Be($"person:130505|{e.D.Tercero.PublicId}");
        ana.Valores[Columna(t, "Cuenta")].Should().BeNull("no es una cuenta");

        var caja = t.Filas.ToList().FindIndex(f => Equals(f.Valores[0], "110505"));
        t.Filas[caja + 1].Valores[1].Should().Be("Sin tercero");
        t.Filas[caja + 1].Valores[Columna(t, "Nodo")].Should().Be("person:110505|none");

        var cuenta1305 = t.Filas.ToList().FindIndex(f => Equals(f.Valores[0], "1305"));
        t.Filas[cuenta1305 + 1].Valores[0].Should().Be("130505", "las agrupaciones no se abren por tercero");
        Numero(t.Totales!, 3).Should().Be(150m);
    }

    // ---------------------------------------------------------------------- libro mayor y balances --

    [Fact]
    public async Task El_libro_mayor_va_al_nivel_4_por_defecto_con_saldo_anterior_y_nuevo_saldo()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 80m, fecha: new DateOnly(2026, 3, 5));
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 20m);

        var r = await e.Mayor().Handle(new GeneralLedgerQuery(new FiltrosDeInforme { From = Marzo10 }), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;

        t.Titulo.Should().Be("Libro mayor y balances");
        t.Columnas.Select(c => c.Nombre).Should().StartWith(["Código", "Nombre", "Saldo anterior", "Débitos", "Créditos", "Nuevo saldo"]);
        t.Filas.Select(f => f.Valores[0]).Should().Equal("1", "11", "1105", "4", "41", "4135");
        var cuenta = Fila(t, "1105");
        Numero(cuenta, 2).Should().Be(80m, "lo anterior al rango es saldo anterior");
        Numero(cuenta, 3).Should().Be(20m);
        Numero(cuenta, 5).Should().Be(100m);
        cuenta.Valores[Columna(t, "Nodo")].Should().Be("account:1105");
        Numero(t.Totales!, 3).Should().Be(20m);
    }

    // ------------------------------------------------------------------------------ libro diario --

    [Fact]
    public async Task El_libro_diario_trae_una_seccion_por_comprobante_con_sus_lineas_en_orden()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 100m, fecha: Marzo15);
        await e.ContabilizarAsync(new PostingRequest("CG", Marzo10, "Venta a Ana", ContabilidadTestData.Manual(),
        [
            new PostingLine { AccountId = e.Cxc.Id, Debit = 40m, Detail = "Factura", PersonId = e.D.Tercero.Id, CrossDocumentType = "FV", CrossDocumentNumber = "77" },
            PostingLine.Credito(e.Ingreso.Id, 40m, "Venta"),
        ]));

        var r = await e.Diario().Handle(new JournalQuery(new FiltrosDeInforme()), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;

        t.Filas.Should().HaveCount(4);
        t.Filas.Select(f => f.Seccion).Distinct().Should().Equal($"CG-2 · {Marzo10:dd/MM/yyyy} · Venta a Ana", $"CG-1 · {Marzo15:dd/MM/yyyy} · Prueba");
        var factura = t.Filas[0];
        factura.Valores[0].Should().Be(Marzo10);
        factura.Valores[1].Should().Be("CG-2");
        factura.Valores[2].Should().Be("130505");
        factura.Valores[4].Should().Be("Ana Prueba");
        factura.Valores[5].Should().Be("FV 77");
        factura.Valores[6].Should().Be("Factura");
        Numero(factura, 7).Should().Be(40m);
        factura.Valores[Columna(t, "Comprobante (id)")].Should().BeOfType<Guid>();
        t.Columnas.Single(c => c.EsOculta).Clave.Should().Be("_comprobante");
        Numero(t.Totales!, 7).Should().Be(140m);
        Numero(t.Totales!, 8).Should().Be(140m);

        var soloFv = await e.Diario().Handle(new JournalQuery(new FiltrosDeInforme { CrossDocument = "FV|77" }), CancellationToken.None);
        soloFv.Value.Filas.Should().ContainSingle().Which.Valores[2].Should().Be("130505");
    }

    // ------------------------------------------------------------------- relación de comprobantes --

    [Fact]
    public async Task La_relacion_de_comprobantes_lista_contabilizados_y_reversados_con_su_estado()
    {
        var e = new Escenario();
        var cg = await e.ContabilizarAsync(e.Caja, e.Ingreso, 100m, fecha: Marzo10);
        await e.ContabilizarAsync(e.Gasto, e.Banco, 70m, tipo: "NM", origen: ContabilidadTestData.Nomina());
        var reversa = await e.D.Poster.PrepareReversalAsync(cg, Marzo15, "Se duplicó", ContabilidadTestData.Manual(), CancellationToken.None);
        reversa.IsSuccess.Should().BeTrue(reversa.Error.Message);
        await e.D.Db.SaveChangesAsync();

        var r = await e.Relacion().Handle(new VoucherListQuery(new FiltrosDeInforme()), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;

        t.Filas.Should().HaveCount(3);
        t.Filas.Select(f => $"{f.Valores[1]}-{f.Valores[2]}").Should().Equal("CG-1", "CG-2", "NM-1");
        t.Filas[0].Valores[5].Should().Be("Reversado");
        t.Filas[1].Valores[5].Should().Be("Contabilizado · Reversión");
        t.Filas[2].Valores[5].Should().Be("Contabilizado");
        t.Filas[2].Valores[4].Should().Be("Nómina");
        t.Filas[0].Valores[8].Should().Be("contadora@demo");
        t.Filas[0].Valores[9].Should().Be("contadora@demo");
        t.Filas[0].Valores[Columna(t, "Comprobante (id)")].Should().Be(cg.PublicId);
        Numero(t.Totales!, 6).Should().Be(270m);
        Numero(t.Totales!, 7).Should().Be(270m);

        // Filtrar por cuenta deja sólo los comprobantes que la tocan, con sus totales completos.
        var soloGasto = await e.Relacion().Handle(new VoucherListQuery(new FiltrosDeInforme { AccountPublicId = e.Gasto.PublicId }), CancellationToken.None);
        soloGasto.Value.Filas.Should().ContainSingle().Which.Valores[1].Should().Be("NM");
    }

    // ----------------------------------------------------------------------------------- exportar --

    [Fact]
    public async Task Exportar_emite_auditoria_y_consultar_en_pantalla_no()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Ingreso);

        await e.BalanceAsync(new FiltrosDeInforme { Format = "json" });
        await e.Audit.DidNotReceive().AppendAsync(Arg.Any<AuditEventDocument>(), Arg.Any<CancellationToken>());

        await e.BalanceAsync(new FiltrosDeInforme { Format = "xlsx" });
        await e.Audit.Received(1).AppendAsync(
            Arg.Is<AuditEventDocument>(a => a.Action == "Accounting.Report.Exported" && a.Module == "Accounting" && a.NewValuesJson!.Contains("trial-balance") && a.NewValuesJson.Contains("xlsx")),
            Arg.Any<CancellationToken>());

        e.Audit.ClearReceivedCalls();
        await e.Mayor().Handle(new GeneralLedgerQuery(new FiltrosDeInforme { Format = "pdf" }), CancellationToken.None);
        await e.Diario().Handle(new JournalQuery(new FiltrosDeInforme { Format = "docx" }), CancellationToken.None);
        await e.Relacion().Handle(new VoucherListQuery(new FiltrosDeInforme { Format = "xlsx" }), CancellationToken.None);
        await e.Audit.Received(3).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == "Accounting.Report.Exported"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_rango_al_reves_se_rechaza_con_su_codigo()
    {
        var e = new Escenario();
        var r = await e.Balance().Handle(new TrialBalanceQuery(new FiltrosDeInforme { From = Marzo15, To = Marzo10 }), CancellationToken.None);
        r.Error.Code.Should().Be("Accounting.Report.InvalidRange");
    }

    [Fact]
    public void El_validador_acota_el_nivel_y_el_formato()
    {
        var v = new TrialBalanceQueryValidator();
        v.Validate(new TrialBalanceQuery(new FiltrosDeInforme())).IsValid.Should().BeTrue();
        v.Validate(new TrialBalanceQuery(new FiltrosDeInforme { Level = 6, Format = "PDF" })).IsValid.Should().BeTrue();
        v.Validate(new TrialBalanceQuery(new FiltrosDeInforme { Level = 7 })).IsValid.Should().BeFalse();
        v.Validate(new TrialBalanceQuery(new FiltrosDeInforme { Level = 0 })).IsValid.Should().BeFalse();
        v.Validate(new TrialBalanceQuery(new FiltrosDeInforme { Format = "csv" })).IsValid.Should().BeFalse();
        new JournalQueryValidator().Validate(new JournalQuery(new FiltrosDeInforme { Format = "csv" })).IsValid.Should().BeFalse();
        new GeneralLedgerQueryValidator().Validate(new GeneralLedgerQuery(new FiltrosDeInforme { Level = 9 })).IsValid.Should().BeFalse();
        new VoucherListQueryValidator().Validate(new VoucherListQuery(new FiltrosDeInforme())).IsValid.Should().BeTrue();
    }
}
