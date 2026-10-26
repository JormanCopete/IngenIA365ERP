using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Treasury;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Convenios;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, R2, T0432 (US16 «Independent Test»; FR-088 a FR-093): por HTTP, configurar un convenio con referencia «número de obligación + dígito», cargar un archivo de
/// diez pagos con uno de dígito inválido —nueve se aplican al cargar con un recibo por pago y el otro queda como partida—, volver a cargarlo (rechazado por su huella) y
/// conciliar el archivo con su abono neto de comisión: la conciliación queda explicada y el abono, identificado. Escrita para la corrida de cierre de R2; sin correr. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ConvenioDeRecaudoTests(CentralIdentityApiFixture fx)
{
    private static async Task<HttpResponseMessage> CargarAsync(HttpClient http, string token, Guid convenio, string csv)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/receipts/collection-agreements/{convenio}/files");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var multipart = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        multipart.Add(bytes, "archivo", "CONVENIO.csv");
        req.Content = multipart;
        return await http.SendAsync(req);
    }

    private static async Task<Guid> FormatoAsync(HttpClient http, string token)
    {
        const string definicion = """
            { "code": "CONV-E2E", "name": "Convenio e2e", "scope": "CollectionAgreementFile", "validFrom": "2026-01-01", "kind": "Delimited", "delimiter": ";",
              "quoteText": false, "encoding": "utf-8", "lineEnding": "CRLF", "uppercase": false, "stripAccents": false, "amountFormat": "Point2",
              "fileName": "CONVENIO.csv", "contentType": "text/csv",
              "records": {
                "header": { "enabled": false, "fields": [] },
                "detail": { "enabled": true, "fields": [
                  { "order": 1, "name": "Fecha", "source": "MovementDate", "format": "yyyy-MM-dd", "required": true },
                  { "order": 2, "name": "Valor", "source": "Amount", "required": true },
                  { "order": 3, "name": "Referencia", "source": "MovementReference" },
                  { "order": 4, "name": "Secuencia", "source": "BankSequence" } ] },
                "trailer": { "enabled": false, "fields": [] } } }
            """;
        var resp = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/bank-file-formats", JsonDocument.Parse(definicion).RootElement);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("formatPublicId").GetGuid();
    }

    [Fact]
    public async Task Diez_pagos_con_uno_de_digito_invalido_y_la_conciliacion_del_abono()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "convenio");
        var cuenta = await TesoreriaE2E.CuentaPropiaAsync(http, c.Token, c.Escenario, "RECCONV", 1, c.Escenario.CuentaDeOrigen);
        var formato = await FormatoAsync(http, c.Token);
        var convenio = (await InventarioE2E.ExitoAsync(http, c.Token, HttpMethod.Post, "/api/receipts/collection-agreements", new
        {
            code = "CONVE2E", name = "Convenio e2e", kind = "Bank", ownBankAccountPublicId = cuenta, formatPublicId = formato, referenceMode = "ByProduct",
            referenceLength = 0, checkDigit = "Modulo10",
        })).GetProperty("publicId").GetGuid();

        var pagables = await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/payables?person={c.Persona}");
        var numero = pagables.GetProperty("obligations").GetProperty("obligations").EnumerateArray()
            .First(o => o.GetProperty("obligationPublicId").GetGuid() == c.CreditoA).GetProperty("number").GetString()!;
        var referencia = DigitoDeVerificacion.Componer(numero, CheckDigitAlgorithm.Modulo10);
        var invalida = referencia[..^1] + (referencia[^1] == '9' ? '0' : (char)(referencia[^1] + 1));
        var hoy = c.Escenario.Hoy;
        // Nueve pagos de 8.000 caben en la cuota del ciclo de A (74.397,28); con 10.000 los dos últimos no tenían qué pagar (corrida del 2026-10-26).
        var lineas = Enumerable.Range(1, 9).Select(i => $"{hoy:yyyy-MM-dd};8000.00;{referencia};S{i}").Append($"{hoy:yyyy-MM-dd};8000.00;{invalida};S10");
        var csv = string.Join("\r\n", lineas);

        var carga = await CargarAsync(http, c.Token, convenio, csv);
        carga.StatusCode.Should().Be(HttpStatusCode.OK, await carga.Content.ReadAsStringAsync());
        var archivo = (await RecaudoYTesoreriaE2E.LeerAsync(carga)).GetProperty("file");
        archivo.GetProperty("applied").GetInt32().Should().Be(9, archivo.GetRawText());
        archivo.GetProperty("unidentified").GetInt32().Should().Be(1);
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(await CargarAsync(http, c.Token, convenio, csv))).Should().Be("Receipts.AgreementFile.AlreadyLoaded");

        var abono = await ExtractoE2E.CargarAsync(http, c.Token, cuenta, string.Join("\r\n", "fecha;valor;referencia;documento;consignante;descripcion;saldo",
            $"{hoy:yyyy-MM-dd};79000.00;CONVENIO;;;Liquidación del convenio;"));
        abono.GetProperty("loaded").GetInt32().Should().Be(1);
        var linea = (await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/bank-items?account={cuenta}&onlyPending=true")).GetProperty("items")
            .EnumerateArray().First().GetProperty("publicId").GetGuid();
        var conciliacion = await InventarioE2E.ExitoAsync(http, c.Token, HttpMethod.Post, $"/api/receipts/collection-agreements/{convenio}/reconciliations", new
        {
            filePublicId = archivo.GetProperty("publicId").GetGuid(), statementLinePublicId = linea, commission = 840m, vat = 160m,
        });
        conciliacion.GetProperty("status").GetInt32().Should().Be((int)AgreementReconciliationState.Explained);
    }
}
