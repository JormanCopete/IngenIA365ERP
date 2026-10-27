using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Treasury;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.LibranzaE2E;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, US7, T0204 («Independent Test»; quickstart recorrido F; FR-075 a FR-081, FR-201): la pagaduría de punta a punta sobre Cartera real, con el convenio de
/// libranza del escenario de K2 (<see cref="LibranzaE2E.EscenarioAsync"/>, cooperativa aislada «pagaduria_pp»), a escala —seis créditos en vez de cincuenta—:
/// <list type="number">
/// <item>Cartera genera el archivo de descuentos del período y llega a Recaudo (<c>/api/receipts/payroll-files</c>) con sus seis líneas;</item>
/// <item>el analista confirma el envío con el radicado: Recaudo emite <c>EnvioDeDescuentosConfirmado</c> y Cartera deja el archivo enviado; confirmarlo otra vez no
/// cambia nada ni emite otro mensaje;</item>
/// <item>el día exigible carga la respuesta en el formato de vuelta del convenio: cuatro descontadas, dos no (licencia y retiro) y una fila de un crédito que el archivo no
/// tiene, que queda «sin equivalencia»; la vista previa cuadra y el resultado vuelve a Cartera, que lo acepta: el archivo queda respondido;</item>
/// <item>el giro de la pagaduría llega al extracto por menos de lo descontado; se registra sobre esa línea en el orden del analista y se aplica: las tres líneas que cubre
/// quedan aplicadas por el contrato, cada una con su recibo y la fecha valor del descuento, y la cuarta sigue pendiente de giro;</item>
/// <item>en Cartera las dos no descontadas siguen exigibles como «no aplicados» con su motivo y lo pendiente de giro de la pagaduría es una línea.</item>
/// </list>
/// Lo que no cubre: las líneas de aportes de Captaciones (el convenio del escenario es de créditos; las cuotas de cuentas las prueban las unitarias de T0201 a T0203) y
/// la cuenta bloqueada que pasa a saldo de la pagaduría (la cubre <c>GiroDePagaduriaTests</c>). (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class PagaduriaDePuntaAPuntaTests(CentralIdentityApiFixture fx)
{
    private const int Creditos = 6;

    [Fact]
    public async Task Archivo_envio_respuesta_y_giro_menor_que_lo_descontado()
    {
        var esc = await EscenarioAsync(fx, "pagaduria_pp");
        using var http = fx.CreateClient();
        var accesos = await AccesosAsync(http, esc);
        var creditos = new List<(Guid Credito, string Numero, Guid Persona, string Empleado)>();
        for (var i = 1; i <= Creditos; i++) creditos.Add(await CreditoPorLibranzaAsync(http, esc, accesos, $"Pagaduria{i:00}"));
        var jefe = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Jefe);
        var (_, vence, _) = await PagosE2E.CuotaAsync(http, jefe, creditos[0].Credito);
        var exigible = await PagosE2E.ExigibleAsync(http, jefe, creditos[0].Credito);

        // (1) Cartera genera el archivo y llega a Recaudo.
        var adm = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
        var deCartera = await GenerarAsync(http, adm, esc.Convenio, vence.Year, vence.Month);
        var archivoDeCartera = deCartera.GetProperty("publicId").GetGuid();
        var admin = esc.Coop.TokenAdmin;
        var archivo = await ArchivoEnRecaudoAsync(fx, http, esc, archivoDeCartera);
        var recibido = await CarteraE2E.GetAsync(http, admin, $"{ArchivosDeRecaudo}/{archivo}");
        RecaudoYTesoreriaE2E.Enum<PayrollDeductionFileState>(recibido.GetProperty("status")).Should().Be(PayrollDeductionFileState.Received);
        var lineas = recibido.GetProperty("lines").EnumerateArray().ToList();
        lineas.Should().HaveCount(Creditos);
        var lineaDe = lineas.ToDictionary(l => l.GetProperty("productNumber").GetString()!);

        // (2) Confirmar el envío; otra vez, nada cambia.
        var enviado = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{ArchivosDeRecaudo}/{archivo}/sent", new { channel = "Portal", reference = "RAD-PP-0001" });
        enviado.StatusCode.Should().Be(HttpStatusCode.OK, await enviado.Content.ReadAsStringAsync());
        var otraVez = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{ArchivosDeRecaudo}/{archivo}/sent", new { channel = "Portal", reference = "RAD-PP-0002" });
        otraVez.StatusCode.Should().Be(HttpStatusCode.OK, await otraVez.Content.ReadAsStringAsync());
        var repetido = await CarteraE2E.LeerAsync(otraVez);
        RecaudoYTesoreriaE2E.Enum<PayrollDeductionFileState>(repetido.GetProperty("status")).Should().Be(PayrollDeductionFileState.Sent);
        repetido.GetProperty("sentReference").GetString().Should().Be("RAD-PP-0001", "la segunda confirmación no cambia el envío");
        var envios = await DesembolsoE2E.EnLaBaseAsync(fx, esc.Desembolso, db => db.IntegrationMessages.AsNoTracking()
            .CountAsync(m => m.Type == "EnvioDeDescuentosConfirmado"));
        envios.Should().Be(1, "repetir la confirmación no emite otro mensaje");
        await CarteraE2E.DespacharAsync(fx, esc.Coop.TenantPublicId);
        var (enCartera, _) = await ArchivoAsync(http, adm, archivoDeCartera);
        enCartera.GetProperty("status").GetString().Should().Be("Sent");
        enCartera.GetProperty("paymasterReference").GetString().Should().Be("RAD-PP-0001");

        decimal Total(string numero) => lineaDe[numero].GetProperty("amount").GetDecimal();
        var noDescontados = creditos.Take(2).ToList();
        var descontados = creditos.Skip(2).ToList();

        using (CausacionE2E.RelojEn(fx, exigible))
        {
            // (3) La respuesta: cuatro descontadas, una licencia, un retiro y una fila de un crédito que el archivo no tiene.
            await ResponderAsync(fx, http, esc, archivoDeCartera,
                descontados.Select(c => (c.Numero, "00", Total(c.Numero), (DateOnly?)exigible))
                    .Concat([(noDescontados[0].Numero, "07", 0m, null), (noDescontados[1].Numero, "08", 0m, null), ("NO-EXISTE-01", "00", 25_000m, (DateOnly?)exigible)]));
            var respuestas = (await CarteraE2E.GetAsync(http, admin, $"{ArchivosDeRecaudo}/{archivo}/responses")).EnumerateArray().ToList();
            var respuesta = respuestas.OrderByDescending(r => r.GetProperty("version").GetInt32()).First();
            var respuestaId = respuesta.GetProperty("publicId").GetGuid();
            respuesta.GetProperty("unmatched").GetInt32().Should().Be(1, "la fila del crédito que el archivo no tiene queda sin equivalencia");
            var previa = await CarteraE2E.GetAsync(http, admin, $"{ArchivosDeRecaudo}/{archivo}/responses/{respuestaId}/preview");
            previa.GetProperty("deducted").GetDecimal().Should().Be(descontados.Sum(c => Total(c.Numero)), "lo descontado de las líneas del archivo");

            var evaluada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{ArchivosDeRecaudo}/{archivo}/responses/{respuestaId}/refresh", new { });
            evaluada.StatusCode.Should().Be(HttpStatusCode.OK, await evaluada.Content.ReadAsStringAsync());
            RecaudoYTesoreriaE2E.Enum<PaymasterResponseState>((await CarteraE2E.LeerAsync(evaluada)).GetProperty("status"))
                .Should().Be(PaymasterResponseState.AcceptedByLending, "Cartera aceptó el resultado");
            var respondido = await CarteraE2E.GetAsync(http, admin, $"{ArchivosDeRecaudo}/{archivo}");
            RecaudoYTesoreriaE2E.Enum<PayrollDeductionFileState>(respondido.GetProperty("status")).Should().Be(PayrollDeductionFileState.Responded);
            lineaDe = respondido.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("productNumber").GetString()!);

            // (4) El giro por menos de lo descontado: cubre tres de las cuatro líneas, en el orden del analista.
            var cubiertos = descontados.Take(3).ToList();
            var giro = cubiertos.Sum(c => Total(c.Numero));
            var cuenta = await TesoreriaE2E.CuentaPropiaAsync(http, admin, esc.Desembolso, "RECPAG", 1, esc.Desembolso.CuentaDeOrigen);
            var csv = string.Join("\r\n", "fecha;valor;referencia;documento;consignante;descripcion;saldo",
                $"{exigible:yyyy-MM-dd};{giro.ToString("0.00", CultureInfo.InvariantCulture)};GIRO-SED-{vence:yyyyMM};;Secretaría de Educación;Giro de libranzas;");
            (await ExtractoE2E.CargarAsync(http, admin, cuenta, csv)).GetProperty("loaded").GetInt32().Should().Be(1);
            var abono = (await InventarioE2E.GetAsync(http, admin, $"/api/receipts/bank-items?account={cuenta}&onlyPending=true")).GetProperty("items")
                .EnumerateArray().Single().GetProperty("publicId").GetGuid();

            var registrado = await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/receipts/paymaster-remittances", new
            {
                paymasterPersonPublicId = esc.Pagaduria, bankStatementLinePublicId = abono, filePublicIds = new[] { archivo }, allocationOrder = "AnalystOrder",
                lineOrder = cubiertos.Select(c => lineaDe[c.Numero].GetProperty("deductionFileLinePublicId").GetGuid()).ToArray(), surplusAsCreditBalance = false,
                note = "Giro parcial de la Secretaría (e2e)",
            });
            registrado.GetProperty("amount").GetDecimal().Should().Be(giro);
            registrado.GetProperty("surplus").GetDecimal().Should().Be(0m);
            registrado.GetProperty("items").GetArrayLength().Should().Be(3, "el giro cubre tres líneas; la cuarta no entra al giro");
            var giroId = registrado.GetProperty("publicId").GetGuid();

            await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/receipts/paymaster-remittances/{giroId}/apply", new { });
            var aplicado = await CarteraE2E.GetAsync(http, admin, $"/api/receipts/paymaster-remittances/{giroId}");
            RecaudoYTesoreriaE2E.Enum<PaymasterRemittanceState>(aplicado.GetProperty("status")).Should().Be(PaymasterRemittanceState.Applied, aplicado.GetRawText());
            aplicado.GetProperty("applied").GetInt32().Should().Be(3);
            aplicado.GetProperty("items").EnumerateArray().Should().OnlyContain(l =>
                RecaudoYTesoreriaE2E.Enum<PaymasterRemittanceLineOutcome>(l.GetProperty("outcome")) == PaymasterRemittanceLineOutcome.Applied
                && l.GetProperty("receiptNumber").ValueKind == JsonValueKind.String);

            var final = (await CarteraE2E.GetAsync(http, admin, $"{ArchivosDeRecaudo}/{archivo}")).GetProperty("lines").EnumerateArray()
                .ToDictionary(l => l.GetProperty("productNumber").GetString()!);
            foreach (var c in cubiertos) final[c.Numero].GetProperty("remittedAmount").GetDecimal().Should().Be(Total(c.Numero), $"{c.Numero} quedó girado");
            final[descontados[3].Numero].GetProperty("remittedAmount").GetDecimal().Should().Be(0m, "lo que el giro no cubre sigue pendiente");

            // Cada recibo de la pagaduría lleva la fecha valor del descuento.
            var recibos = (await InventarioE2E.GetAsync(http, admin, "/api/receipts/receipts")).EnumerateArray()
                .Where(r => RecaudoYTesoreriaE2E.Enum<ReceiptChannel>(r.GetProperty("channel")) == ReceiptChannel.Paymaster).ToList();
            recibos.Should().HaveCount(3, "un recibo por línea aplicada del giro");
            recibos.Should().OnlyContain(r => r.GetProperty("valueDate").GetString() == exigible.ToString("yyyy-MM-dd"));

            // (5) En Cartera: las no descontadas siguen exigibles con su motivo; lo pendiente de giro es la cuarta.
            adm = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
            var abiertos = (await CarteraE2E.GetAsync(http, adm, $"{NoAplicados}?agreementPublicId={esc.Convenio}&origin=PaymasterResult&status=Open"))
                .GetProperty("items").EnumerateArray().ToList();
            abiertos.Select(n => n.GetProperty("loanPublicId").GetGuid()).Should().BeEquivalentTo(noDescontados.Select(c => c.Credito));
            abiertos.Select(n => n.GetProperty("reason").GetString()).Should().BeEquivalentTo(["Leave", "Termination"]);
            var pendiente = (await CarteraE2E.GetAsync(http, adm, $"{Convenios}/{esc.Convenio}/pending-transfers")).EnumerateArray().Single();
            pendiente.GetProperty("lines").GetInt32().Should().Be(1, "las tres giradas levantaron su pendiente");
            pendiente.GetProperty("amount").GetDecimal().Should().Be(Total(descontados[3].Numero));
        }
    }
}
