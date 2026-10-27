using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Receipts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Feature 015, US25, T0274 (US25 «Independent Test» y escenarios 1 a 5; quickstart.md recorrido I pasos 2 a 4; SC-031; FR-131, FR-216, FR-217): en una cuenta propia
/// de recaudo y pagos, el mes trae un abono identificado en Recaudo, una remesa de la bóveda, un pago por transferencia, un cheque girado y entregado, un abono que
/// nadie registró y un cargo repetido del banco. Quien preparó los pagos no concilia; el conciliador (otra persona) carga el extracto, el cruce automático empareja el
/// recibo por su línea, la remesa (pasa a confirmada con <c>RemesaConfirmada</c>) y el cheque (pasa a «cobrado»); el pago, que tiene dos cargos iguales, lo cruza a
/// mano y un cruce repetido se rechaza; el abono sin registro pasa a Recaudo como partida por identificar y el cargo repetido queda como partida y alerta. Cierra
/// cuadrando saldo del extracto, partidas y saldo en libros; reabrir exige permiso y motivo y deja una versión nueva. (nuevo)
/// </summary>
/// <remarks>
/// En la cooperativa de las e2e los hechos de Recaudo y Tesorería no pasan a Contabilidad (la matriz no se carga), así que el saldo en libros de la cuenta no se mueve
/// con lo del mes: el conciliador digita al abrir el saldo final que informa el banco, que es el de los libros menos el cargo repetido.
/// </remarks>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ConciliacionMinimaTests(CentralIdentityApiFixture fx)
{
    private const decimal Abono = 30_000m;
    private const decimal Remesa = 500_000m;
    private const decimal SinRegistro = 7_700m;
    private const decimal Pago = 1_234_000m;
    private const decimal Cheque = 1_345_000m;
    private const string Sello = "SELLO-CON-0001";

    [Fact]
    public async Task El_conciliador_cruza_marca_lleva_partidas_cierra_cuadrado_y_reabre_con_motivo()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "conminima");
        var esc = c.Escenario;
        var admin = c.Token;
        var jefe = esc.Usuarios.Jefe.Token;
        var conciliador = esc.Usuarios.Pruebas.Token;
        var hoy = esc.Hoy;
        await TesoreriaE2E.AprobadorDeTesoreriaAsync(http, esc);
        await TesoreriaE2E.TesoreroAsync(http, esc);
        await ChequesYRemesasE2E.ConciliadorAsync(http, esc);

        // La cuenta de pagos de la configuración mínima recibe también el recaudo (propósitos recaudo y pagos).
        var cuenta = await RecaudoYTesoreriaE2E.CuentaDePagosAsync(http, admin);
        var datos = await CarteraE2E.GetAsync(http, admin, $"/api/treasury/bank-accounts/{cuenta}");
        TesoreriaE2E.EsExito(await InventarioE2E.MandarAsync(http, admin, HttpMethod.Put, $"/api/treasury/bank-accounts/{cuenta}", new
        {
            name = datos.GetProperty("name").GetString(), branchPublicId = datos.GetProperty("branchPublicId").GetGuid(),
            purposes = (int)(BankAccountPurpose.Collection | BankAccountPurpose.Payments), isGmfExempt = false,
            bankThirdPartyPersonPublicId = (Guid?)null, chartAccountPublicId = (Guid?)null,
        }), "la cuenta de pagos también recibe recaudo");

        // Lo del mes en los libros de Tesorería. El administrador prepara los lotes y el jefe los aprueba.
        var t = new TesoreriaE2E.Tesoreria(esc, admin, cuenta, c.Persona);
        var porTransferencia = await TesoreriaE2E.OrdenDeDesembolsoAsync(fx, http, t, Pago);
        await RecaudoYTesoreriaE2E.GirarOrdenAsync(http, admin, jefe, Id(porTransferencia), cuenta);
        await RecaudoYTesoreriaE2E.RegistrarResultadoAsync(http, admin, Id(porTransferencia), "Disbursed", hoy);

        var porCheque = await ChequesYRemesasE2E.OrdenPorChequeAsync(fx, http, esc, Cheque);
        var chequera = await ChequesYRemesasE2E.ChequeraAsync(http, admin, cuenta, 800_001, 800_020);
        var cheque = (await ChequesYRemesasE2E.EmitirAsync(http, admin, jefe, cuenta, chequera, [Id(porCheque)])).Should().ContainSingle().Subject;
        var numeroDelCheque = cheque.GetProperty("checkNumber").GetInt64();
        await ChequesYRemesasE2E.EntregarAsync(http, admin, Id(cheque), hoy);

        var boveda = await ChequesYRemesasE2E.BovedaConEfectivoAsync(http, esc, 10);
        var remesa = (await ChequesYRemesasE2E.RemesarAsync(http, esc, boveda, cuenta, await ChequesYRemesasE2E.ActaAsync(http, admin, boveda.Boveda), 10, Sello))
            .GetProperty("remittancePublicId").GetGuid();

        // Quien preparó pagos de la cuenta en el mes no la concilia (US25-3, FR-217).
        var propia = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, "/api/treasury/reconciliations",
            new { bankAccountPublicId = cuenta, year = hoy.Year, month = hoy.Month });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(propia)).Should().Be("Treasury.Reconciliation.SegregationConflict");

        // El conciliador carga el extracto del mes: el banco cobró dos veces el pago.
        var csv = string.Join("\r\n", "fecha;valor;referencia;documento;consignante;descripcion;saldo",
            $"{hoy:yyyy-MM-dd};30000.00;ABONO-CUOTA;;;Abono a la cuota;",
            $"{hoy:yyyy-MM-dd};500000.00;{Sello};;;Consignación de remesa;",
            $"{hoy:yyyy-MM-dd};7700.00;;;;Consignación sin soporte;",
            $"{hoy:yyyy-MM-dd};-1234000.00;;;;Pago por transferencia;",
            $"{hoy:yyyy-MM-dd};-1234000.00;;;;Débito repetido del pago;",
            $"{hoy:yyyy-MM-dd};-1345000.00;{numeroDelCheque};;;Cheque pagado;");
        (await ExtractoE2E.CargarAsync(http, conciliador, cuenta, csv)).GetProperty("loaded").GetInt32().Should().Be(6);

        // Recaudo identifica el abono contra la cuota del crédito: el recibo de banco nace de esa línea.
        var abonos = (await InventarioE2E.GetAsync(http, admin, $"/api/receipts/bank-items?account={cuenta}&onlyPending=true")).GetProperty("items").EnumerateArray().ToList();
        var abono = abonos.First(a => a.GetProperty("amount").GetDecimal() == Abono).GetProperty("publicId").GetGuid();
        (await ExtractoE2E.IdentificarAsync(http, admin, abono, [new { target = "Loan", beneficiaryPersonPublicId = c.Persona, amount = Abono, productPublicId = c.CreditoA }]))
            .GetProperty("pending").GetDecimal().Should().Be(0m);

        // Abre con el saldo final que informa el banco: los libros menos el cargo repetido.
        var libros = (await CarteraE2E.GetAsync(http, conciliador, $"/api/treasury/bank-accounts/{cuenta}/balances?date={hoy:yyyy-MM-dd}"))
            .GetProperty("bookBalance").GetDecimal();
        var saldoDelBanco = libros - Pago;
        var abierta = await InventarioE2E.ExitoAsync(http, conciliador, HttpMethod.Post, "/api/treasury/reconciliations",
            new { bankAccountPublicId = cuenta, year = hoy.Year, month = hoy.Month, statementBalance = saldoDelBanco });
        var id = Id(abierta);
        abierta.GetProperty("status").GetString().Should().Be("Open");
        abierta.GetProperty("version").GetInt32().Should().Be(1);
        abierta.GetProperty("statementBalance").GetDecimal().Should().Be(saldoDelBanco);
        abierta.GetProperty("ledgerBalance").GetDecimal().Should().Be(libros);

        // El cruce automático: el recibo por su línea, la remesa y el cheque; el pago no, porque tiene dos cargos iguales (el empate lo resuelve una persona).
        await InventarioE2E.ExitoAsync(http, conciliador, HttpMethod.Post, $"/api/treasury/reconciliations/{id}/auto-match");
        var detalle = await CarteraE2E.GetAsync(http, conciliador, $"/api/treasury/reconciliations/{id}");
        detalle.GetProperty("matches").EnumerateArray().Select(m => m.GetProperty("recordKind").GetString()).Should()
            .BeEquivalentTo(["ReceiptsBankReceipt", "Remittance", "IssuedCheck"], detalle.GetRawText());
        detalle.GetProperty("matches").EnumerateArray().Should().OnlyContain(m => m.GetProperty("mode").GetString() == "Automatic");
        var abiertas = detalle.GetProperty("openLines").EnumerateArray().ToList();
        abiertas.Select(l => l.GetProperty("amount").GetDecimal()).Should().BeEquivalentTo([SinRegistro, Pago, Pago]);

        // La remesa cruzada queda confirmada con la línea y su hecho; el cheque cruzado, cobrado (US25-4).
        var confirmada = (await CarteraE2E.GetAsync(http, admin, "/api/treasury/remittances?state=Confirmed")).EnumerateArray().Single(r => Id(r) == remesa);
        confirmada.GetProperty("confirmedOn").GetString().Should().Be(hoy.ToString("yyyy-MM-dd"));
        confirmada.GetProperty("confirmedByStatementLinePublicId").ValueKind.Should().Be(JsonValueKind.String);
        (await ChequesYRemesasE2E.MensajesAsync(fx, esc, RemesaConfirmadaV1.Type, remesa)).Should().Be(1);
        var cobrado = (await CarteraE2E.GetAsync(http, admin, $"/api/treasury/issued-checks?checkbook={chequera}")).EnumerateArray().Single(x => Id(x) == Id(cheque));
        cobrado.GetProperty("status").GetInt32().Should().Be((int)IssuedCheckState.Cashed);
        cobrado.GetProperty("cashedOn").GetString().Should().Be(hoy.ToString("yyyy-MM-dd"));

        // A mano: el primer cargo con el pago. El cargo del cheque, ya cruzado, no se vuelve a cruzar (US25-1).
        var pago = detalle.GetProperty("availableRecords").EnumerateArray().Single(r => r.GetProperty("kind").GetString() == "TreasuryPayment");
        pago.GetProperty("amount").GetDecimal().Should().Be(Pago);
        var cargos = abiertas.Where(l => l.GetProperty("amount").GetDecimal() == Pago).OrderBy(l => l.GetProperty("lineNumber").GetInt32()).ToList();
        await InventarioE2E.ExitoAsync(http, conciliador, HttpMethod.Post, $"/api/treasury/reconciliations/{id}/matches", new
        {
            statementLinePublicId = Id(cargos[0]), records = new[] { new { kind = "TreasuryPayment", publicId = Id(pago) } }, note = "El banco cobró dos veces (e2e)",
        });
        var lineaDelCheque = detalle.GetProperty("matches").EnumerateArray().Single(m => m.GetProperty("recordKind").GetString() == "IssuedCheck")
            .GetProperty("statementLinePublicId").GetGuid();
        var repetido = await InventarioE2E.MandarAsync(http, conciliador, HttpMethod.Post, $"/api/treasury/reconciliations/{id}/matches", new
        {
            statementLinePublicId = lineaDelCheque, records = new[] { new { kind = "TreasuryPayment", publicId = Id(pago) } }, note = "Otra vez (e2e)",
        });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(repetido)).Should().Be("Treasury.Reconciliation.AlreadyMatched");

        // Sin partidas no cuadra: falta explicar el cargo repetido.
        var sinPartidas = await InventarioE2E.MandarAsync(http, conciliador, HttpMethod.Post, $"/api/treasury/reconciliations/{id}/close");
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(sinPartidas)).Should().Be("Treasury.Reconciliation.NotSquared");

        // El abono sin registro pasa a Recaudo como partida por identificar (US25-2); el cargo repetido queda como partida y alerta.
        var lineaSinRegistro = abiertas.Single(l => l.GetProperty("amount").GetDecimal() == SinRegistro);
        await InventarioE2E.ExitoAsync(http, conciliador, HttpMethod.Post, $"/api/treasury/reconciliations/{id}/items", new
        {
            kind = "UnidentifiedCredit", amount = SinRegistro, statementLinePublicId = Id(lineaSinRegistro), note = "Consignación sin soporte (e2e)",
        });
        await InventarioE2E.ExitoAsync(http, conciliador, HttpMethod.Post, $"/api/treasury/reconciliations/{id}/items", new
        {
            kind = "UnidentifiedDebit", amount = Pago, statementLinePublicId = Id(cargos[1]), note = "Débito repetido: reclamar al banco (e2e)",
        });
        var conPartidas = await CarteraE2E.GetAsync(http, conciliador, $"/api/treasury/reconciliations/{id}");
        conPartidas.GetProperty("openLines").GetArrayLength().Should().Be(0, "todo el extracto quedó cruzado o explicado");
        var partidas = conPartidas.GetProperty("items").EnumerateArray().ToList();
        var aRecaudo = partidas.Single(p => p.GetProperty("kind").GetString() == "UnidentifiedCredit");
        aRecaudo.GetProperty("signedAmount").GetDecimal().Should().Be(0m, "queda en los libros por la partida de Recaudo");
        var partidaDeRecaudo = aRecaudo.GetProperty("unidentifiedItemPublicId").GetGuid();
        (await InventarioE2E.GetAsync(http, admin, "/api/receipts/unidentified-items?onlyOpen=true")).GetRawText().Should().Contain(partidaDeRecaudo.ToString());
        var cargo = partidas.Single(p => p.GetProperty("kind").GetString() == "UnidentifiedDebit");
        cargo.GetProperty("signedAmount").GetDecimal().Should().Be(Pago);
        cargo.GetProperty("alertPublicId").ValueKind.Should().Be(JsonValueKind.String, "el cargo no identificado alerta");
        (await CarteraE2E.GetAsync(http, admin, "/api/inventory/alerts?typeCode=Tesoreria.CargoNoIdentificado")).ToString().Should().Contain("Tesoreria.CargoNoIdentificado");

        // Cierra: saldo del extracto − saldo en libros + partidas = 0, con su informe.
        var cerrada = await InventarioE2E.ExitoAsync(http, conciliador, HttpMethod.Post, $"/api/treasury/reconciliations/{id}/close");
        cerrada.GetProperty("status").GetString().Should().Be("Closed");
        cerrada.GetProperty("difference").GetDecimal().Should().Be(0m);
        cerrada.GetProperty("reconcilingItemsTotal").GetDecimal().Should().Be(Pago);
        (cerrada.GetProperty("statementBalance").GetDecimal() - cerrada.GetProperty("ledgerBalance").GetDecimal()
         + cerrada.GetProperty("reconcilingItemsTotal").GetDecimal()).Should().Be(0m);
        cerrada.GetProperty("reportAttachmentPublicId").ValueKind.Should().Be(JsonValueKind.String, "el informe de partidas conciliatorias queda guardado");

        // Reabrir exige el permiso (el jefe no lo tiene: la ruta no existe para él) y el motivo; deja una versión nueva abierta (US25-5).
        (await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"/api/treasury/reconciliations/{id}/reopen", new { reason = "Sin permiso (e2e)" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CarteraE2E.ConClaveAsync(http, conciliador, HttpMethod.Post, $"/api/treasury/reconciliations/{id}/reopen", new { reason = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var reabierta = await InventarioE2E.ExitoAsync(http, conciliador, HttpMethod.Post, $"/api/treasury/reconciliations/{id}/reopen",
            new { reason = "El banco reversó el débito repetido: corregir la partida (e2e)" });
        reabierta.GetProperty("version").GetInt32().Should().Be(2);
        reabierta.GetProperty("status").GetString().Should().Be("Open");
        var versiones = await CarteraE2E.GetAsync(http, conciliador, $"/api/treasury/reconciliations?account={cuenta}&year={hoy.Year}&month={hoy.Month}");
        versiones.EnumerateArray().Single(v => Id(v) == id).GetProperty("status").GetString().Should().Be("Reopened");
    }

    private static Guid Id(JsonElement e) => e.GetProperty("publicId").GetGuid();
}
