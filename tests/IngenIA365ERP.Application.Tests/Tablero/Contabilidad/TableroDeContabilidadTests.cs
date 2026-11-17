using FluentAssertions;
using IngenIA365ERP.Application.Tablero;
using IngenIA365ERP.Application.Tablero.Contabilidad;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Tests.Tablero.Contabilidad;

/// <summary>2026-11-17 (pedido del dueño): las cifras y pendientes de Contabilidad en el tablero de inicio.</summary>
public class TableroDeContabilidadTests
{
    private const int Contadora = 9;
    private const int Auxiliar = 5;

    private readonly ContabilidadTestData _d = new();
    private readonly ChartOfAccount _caja;
    private readonly ChartOfAccount _ingreso;
    private readonly ChartOfAccount _gasto;
    private readonly ChartOfAccount _costo;

    public TableroDeContabilidadTests()
    {
        _caja = _d.Cuenta("110505");
        _ingreso = _d.Cuenta("413505", AccountNature.Credit);
        _gasto = _d.Cuenta("510506");
        _costo = _d.Cuenta("613505");
    }

    private static ContextoDeIndicador Contexto(DateOnly? hoy = null, bool soloLoMio = false, int? usuario = Contadora) =>
        new(soloLoMio, usuario, "contadora@demo", hoy ?? ContabilidadTestData.Hoy);

    /// <summary>Un comprobante con sus líneas, guardado tal cual (contabilizado, borrador o cierre).</summary>
    private AccountingDocument Comprobante(DateOnly fecha, decimal valor, ChartOfAccount debito, ChartOfAccount credito,
        DocumentStatus estado = DocumentStatus.Posted, DocumentKind kind = DocumentKind.Regular, int usuario = Contadora,
        Branch? sucursal = null, decimal? credito2 = null)
    {
        var tipo = _d.Db.VoucherTypes.Single(t => t.Code == (kind == DocumentKind.Closing ? "CI" : "CG"));
        var sucursalId = (sucursal ?? _d.Principal).Id;
        var contabilizado = estado != DocumentStatus.Draft;
        var documento = new AccountingDocument
        {
            VoucherTypeId = tipo.Id, Date = fecha, Description = "Prueba", Status = estado, Kind = kind, OriginModule = "CNT",
            TotalDebit = valor, TotalCredit = credito2 ?? valor, RegisteredByUserId = usuario, RegisteredBy = $"u{usuario}", CreatedBy = "test",
        };
        documento.Lines.Add(new JournalEntry { LineNumber = 1, AccountId = debito.Id, BranchId = sucursalId, Debit = valor, Date = fecha, IsPosted = contabilizado, CreatedBy = "test" });
        documento.Lines.Add(new JournalEntry { LineNumber = 2, AccountId = credito.Id, BranchId = sucursalId, Credit = credito2 ?? valor, Date = fecha, IsPosted = contabilizado, CreatedBy = "test" });
        _d.Db.AccountingDocuments.Add(documento);
        _d.Db.SaveChanges();
        return documento;
    }

    // ------------------------------------------------------------------------------------------ resultado del mes --

    [Fact]
    public async Task El_resultado_del_mes_resta_gastos_y_costos_de_los_ingresos_y_compara_con_el_mes_anterior()
    {
        Comprobante(new DateOnly(2026, 3, 5), 1_000m, _caja, _ingreso);
        Comprobante(new DateOnly(2026, 3, 6), 300m, _gasto, _caja);
        Comprobante(new DateOnly(2026, 3, 7), 100m, _costo, _caja);
        Comprobante(new DateOnly(2026, 2, 10), 500m, _caja, _ingreso);
        Comprobante(new DateOnly(2026, 3, 8), 9_999m, _caja, _ingreso, DocumentStatus.Draft); // un borrador no cuenta
        Comprobante(new DateOnly(2026, 1, 10), 7_000m, _caja, _ingreso);                    // fuera de los dos meses

        var r = await new ResultadoDelMes(_d.Db, _d.Alcance).CalcularAsync(Contexto(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.Valor.Should().Be(600m);
        r.Value.Anterior.Should().Be(500m);
        r.Value.EtiquetaDelAnterior.Should().Be("mes anterior");
    }

    [Fact]
    public async Task El_resultado_del_mes_respeta_el_alcance_de_sucursal()
    {
        Comprobante(new DateOnly(2026, 3, 5), 1_000m, _caja, _ingreso);
        Comprobante(new DateOnly(2026, 3, 6), 200m, _caja, _ingreso, sucursal: _d.Norte);
        _d.RestringirA(_d.Norte);

        var r = await new ResultadoDelMes(_d.Db, _d.Alcance).CalcularAsync(Contexto(), CancellationToken.None);

        r.Value.Valor.Should().Be(200m, "el usuario sólo ve Norte");
    }

    // --------------------------------------------------------------------------------------------- seis meses --

    [Fact]
    public async Task La_serie_trae_los_seis_meses_sin_huecos_y_el_cierre_del_ejercicio_no_la_deja_en_cero()
    {
        _d.Cuenta("360505", AccountNature.Credit);
        var resultado = _d.Db.ChartOfAccounts.Single(a => a.Code == "360505");
        Comprobante(new DateOnly(2025, 12, 15), 800m, _caja, _ingreso);
        Comprobante(new DateOnly(2025, 12, 31), 800m, _ingreso, resultado, kind: DocumentKind.Closing); // el CI del ejercicio 2025
        Comprobante(new DateOnly(2026, 1, 10), 400m, _caja, _ingreso);
        Comprobante(new DateOnly(2026, 3, 5), 250m, _gasto, _caja);
        Comprobante(new DateOnly(2025, 9, 30), 5_000m, _caja, _ingreso); // séptimo mes hacia atrás: fuera

        var r = await new ResultadoDeLosUltimosSeisMeses(_d.Db, _d.Alcance).CalcularAsync(Contexto(), CancellationToken.None);

        r.Value.Serie.Should().NotBeNull();
        r.Value.Serie!.Select(p => p.Valor).Should().Equal(0m, 0m, 800m, 400m, 0m, -250m);
        r.Value.Serie.Should().HaveCount(6);
        r.Value.Valor.Should().Be(950m);
        r.Value.Nota.Should().Contain("Ingresos").And.Contain("gastos");
    }

    // ----------------------------------------------------------------------------------------------- borradores --

    [Fact]
    public async Task Los_borradores_cuentan_todo_y_solo_lo_mio_filtra_por_quien_los_registro()
    {
        Comprobante(new DateOnly(2026, 3, 5), 100m, _caja, _ingreso, DocumentStatus.Draft, usuario: Contadora);
        Comprobante(new DateOnly(2026, 3, 6), 100m, _caja, _ingreso, DocumentStatus.Draft, usuario: Contadora);
        Comprobante(new DateOnly(2026, 3, 7), 100m, _caja, _ingreso, DocumentStatus.Draft, usuario: Auxiliar, credito2: 90m);
        Comprobante(new DateOnly(2026, 3, 8), 100m, _caja, _ingreso, usuario: Contadora); // contabilizado

        var indicador = new ComprobantesEnBorrador(_d.Db, _d.Alcance);
        indicador.Definicion.AdmiteSoloLoMio.Should().BeTrue();

        var todo = await indicador.CalcularAsync(Contexto(), CancellationToken.None);
        var mio = await indicador.CalcularAsync(Contexto(soloLoMio: true), CancellationToken.None);
        var delAuxiliar = await indicador.CalcularAsync(Contexto(soloLoMio: true, usuario: Auxiliar), CancellationToken.None);

        todo.Value.Valor.Should().Be(3m);
        todo.Value.Nota.Should().Be("1 sin cuadrar");
        mio.Value.Valor.Should().Be(2m);
        mio.Value.Nota.Should().BeNull();
        delAuxiliar.Value.Valor.Should().Be(1m);
    }

    // ------------------------------------------------------------------------------------------------ pendientes --

    [Fact]
    public async Task Por_contabilizar_cuenta_solo_los_borradores_cuadrados_que_el_usuario_ve()
    {
        Comprobante(new DateOnly(2026, 3, 5), 100m, _caja, _ingreso, DocumentStatus.Draft);
        Comprobante(new DateOnly(2026, 3, 6), 100m, _caja, _ingreso, DocumentStatus.Draft, sucursal: _d.Norte);
        Comprobante(new DateOnly(2026, 3, 7), 100m, _caja, _ingreso, DocumentStatus.Draft, credito2: 90m);
        Comprobante(new DateOnly(2026, 3, 8), 100m, _caja, _ingreso);

        var pendiente = new ComprobantesPorContabilizar(_d.Db, _d.Alcance);
        (await pendiente.ContarAsync(Contexto(), CancellationToken.None)).Should().Be(2);

        _d.RestringirA(_d.Norte);
        (await pendiente.ContarAsync(Contexto(), CancellationToken.None)).Should().Be(1);
    }

    [Fact]
    public async Task Meses_por_cerrar_son_los_abiertos_que_ya_terminaron()
    {
        // El escenario: enero y febrero cerrados, marzo y abril abiertos, diciembre cerrado.
        var pendiente = new MesesPorCerrar(_d.Db);

        (await pendiente.ContarAsync(Contexto(new DateOnly(2026, 3, 20)), CancellationToken.None)).Should().Be(0, "marzo no ha terminado");
        (await pendiente.ContarAsync(Contexto(new DateOnly(2026, 4, 1)), CancellationToken.None)).Should().Be(1);
        (await pendiente.ContarAsync(Contexto(new DateOnly(2026, 5, 10)), CancellationToken.None)).Should().Be(2);
    }

    [Fact]
    public async Task Los_rechazos_se_cuentan_por_modulo_de_origen_y_solo_los_de_contabilidad()
    {
        Entrega("INV", IntegrationDestinations.Accounting, DeliveryStatus.Rejected);
        Entrega("INV", IntegrationDestinations.Accounting, DeliveryStatus.Rejected);
        Entrega("INV", IntegrationDestinations.Accounting, DeliveryStatus.InBatch);   // ya reprocesado
        Entrega("INV", IntegrationDestinations.Lending, DeliveryStatus.Rejected);     // otro destino
        Entrega("CAR", IntegrationDestinations.Accounting, DeliveryStatus.Rejected);

        (await new HechosDeInventarioRechazados(_d.Db).ContarAsync(Contexto(), CancellationToken.None)).Should().Be(2);
        (await new HechosDeCarteraRechazados(_d.Db).ContarAsync(Contexto(), CancellationToken.None)).Should().Be(1);
        (await new HechosDeRecaudoRechazados(_d.Db).ContarAsync(Contexto(), CancellationToken.None)).Should().Be(0);
    }

    private void Entrega(string origen, string destino, DeliveryStatus estado)
    {
        var mensaje = new IntegrationMessage
        {
            Type = "Prueba", Kind = IntegrationMessageKind.Business, OriginModule = origen, OriginKind = MessageOriginKind.Document,
            OriginPublicId = Guid.NewGuid(), OriginEventKey = Guid.NewGuid().ToString("N"), ChainRootPublicId = Guid.NewGuid(),
            OperationDate = ContabilidadTestData.Marzo15, BranchPublicId = Guid.NewGuid(), PayloadJson = "{}", PayloadSha256 = "x",
            OriginUserCentralId = Guid.NewGuid(), OriginUserName = "bodega@demo", EmittedAt = ContabilidadTestData.Ahora, CreatedBy = "test",
        };
        _d.Db.IntegrationMessages.Add(mensaje);
        _d.Db.SaveChanges();
        _d.Db.IntegrationMessageDeliveries.Add(new IntegrationMessageDelivery
        {
            MessageId = mensaje.Id, Destination = destino, Mode = DeliveryMode.Batch, Status = estado, CreatedBy = "test",
        });
        _d.Db.SaveChanges();
    }
}
