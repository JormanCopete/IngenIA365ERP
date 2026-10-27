using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.Domain.Receipts.Cumplimiento;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, US14, T0237 («Independent Test»; quickstart recorrido J pasos 2 y 3; FR-094, FR-095, FR-097, FR-191, FR-202): el control de efectivo en la caja sobre
/// Cartera real. Los umbrales son los cuatro parámetros <c>Sarlaft.*</c> con vigencia, que la prueba registra <b>a escala</b> (declaración e individual en 500.000,
/// múltiples en 1.500.000, fraccionamiento «3;7»): con los 5.000.000 y 30.000.000 sembrados, la caja del escenario no tiene a qué aplicar 6.000.000 sin excedente
/// (FR-043). La forma del recorrido es la misma:
/// <list type="number">
/// <item>600.000 en efectivo sin declaración: <c>Receipts.Compliance.DeclarationRequired</c>; con la declaración y su soporte sigue faltando quien efectúa la entrega
/// (el efectivo llega al umbral individual, <c>.DeliveredByRequired</c>); registrado otro que el pagador, el recibo sale y lo lleva;</item>
/// <item>cuatro pagos de 400.000 en efectivo de la misma persona el mismo día (bajo el umbral de declaración): el registro de efectivo tiene una fila por recibo y el
/// oficial recibe la alerta de fraccionamiento y la del acumulado del mes, una vez cada una, con la versión del parámetro.</item>
/// </list>
/// La lista vinculante en la ventanilla (paso 4) está en <see cref="ListaVinculanteBloqueaTests"/>. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ControlDeEfectivoYSarlaftTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Declaracion_quien_efectua_fraccionamiento_y_acumulado_del_mes()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "sarlaft");
        var t = c.Token;
        var hoy = c.Escenario.Hoy;
        const string Motivo = "Umbrales a escala del recorrido J (e2e); los reales los valida el oficial (P7)";
        await RecaudoYTesoreriaE2E.ParametroAsync(fx, c.Escenario.Coop, "RCD", "Sarlaft.UmbralDeclaracionOrigenFondos", "500000", hoy, Motivo);
        await RecaudoYTesoreriaE2E.ParametroAsync(fx, c.Escenario.Coop, "RCD", "Sarlaft.UmbralTransaccionIndividual", "500000", hoy, Motivo);
        await RecaudoYTesoreriaE2E.ParametroAsync(fx, c.Escenario.Coop, "RCD", "Sarlaft.UmbralTransaccionesMultiples", "1500000", hoy, Motivo);
        await RecaudoYTesoreriaE2E.ParametroAsync(fx, c.Escenario.Coop, "RCD", "Sarlaft.Fraccionamiento", "3;7", hoy, Motivo);

        // (1) 600.000 en efectivo, abono a capital del crédito B.
        var op = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Abono(c.CreditoB, 600_000m)], [CajaE2E.Medio(c.Efectivo, 600_000m)]);
        var sinDeclaracion = await CajaE2E.ConfirmarAsync(http, c, op, Guid.NewGuid());
        sinDeclaracion.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await sinDeclaracion.Content.ReadAsStringAsync());
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(sinDeclaracion)).Should().Be("Receipts.Compliance.DeclarationRequired");

        var soporte = await AdjuntosE2E.SubirAsync(http, t, op, Encoding.UTF8.GetBytes("%PDF-1.4 declaración de origen de fondos (e2e)"), "declaracion.pdf",
            "application/pdf", "ReceiptsSourceOfFunds");
        soporte.EstadoDelAlmacen.Should().Be(HttpStatusCode.NoContent, "el soporte de la declaración llega al almacén");
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/receipts/cash-operations/{op}/declaration", new
        {
            origin = "Venta de una cosecha de café (e2e)", cashAmount = 600_000m, supportAttachmentPublicId = soporte.Adjunto,
        });
        var sinQuienEfectua = await CajaE2E.ConfirmarAsync(http, c, op, Guid.NewGuid());
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(sinQuienEfectua)).Should().Be("Receipts.Compliance.DeliveredByRequired",
            "el efectivo llega al umbral de la transacción individual");

        var mensajero = await CarteraE2E.PersonaAsync(http, t, "Mensajero", "Sarlaft");
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/receipts/cash-operations/{op}/delivered-by", new { deliveredByPersonPublicId = mensajero });
        var recibo = await CajaE2E.ReciboAsync(http, c, op);
        recibo.GetProperty("deliveredByPersonPublicId").GetGuid().Should().Be(mensajero, "quien efectúa la entrega queda en el recibo");
        recibo.GetProperty("totalMeans").GetDecimal().Should().Be(600_000m);

        // (2) Cuatro pagos de 400.000 en efectivo: cada uno bajo el umbral de declaración; juntos, fraccionamiento y acumulado.
        foreach (var credito in new[] { c.CreditoA, c.CreditoB, c.CreditoA, c.CreditoB })
        {
            var pago = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Abono(credito, 400_000m)], [CajaE2E.Medio(c.Efectivo, 400_000m)]);
            await CajaE2E.ReciboAsync(http, c, pago);
        }

        var efectivo = (await InventarioE2E.GetAsync(http, t, $"/api/receipts/compliance/cash-transactions?person={c.Persona}")).EnumerateArray().ToList();
        efectivo.Should().HaveCount(5, "una fila del registro de efectivo por recibo con efectivo");
        efectivo.Sum(x => x.GetProperty("cashAmount").GetDecimal()).Should().Be(2_200_000m);
        efectivo.Should().OnlyContain(x => x.GetProperty("month").GetInt32() == hoy.Year * 100 + hoy.Month);

        var senales = (await InventarioE2E.GetAsync(http, t, $"/api/receipts/compliance/signals?person={c.Persona}")).EnumerateArray().ToList();
        var tipos = senales.Select(s => RecaudoYTesoreriaE2E.Enum<SenalDeEfectivo>(s.GetProperty("signal"))).ToList();
        tipos.Should().Contain(SenalDeEfectivo.Fragmentation).And.Contain(SenalDeEfectivo.Accumulated);
        senales.Where(s => RecaudoYTesoreriaE2E.Enum<SenalDeEfectivo>(s.GetProperty("signal")) is SenalDeEfectivo.Fragmentation or SenalDeEfectivo.Accumulated)
            .Should().OnlyContain(s => s.GetProperty("parameterVersion").ValueKind == JsonValueKind.String, "cada señal guarda la versión del parámetro que la levantó");

        foreach (var tipo in new[] { "Recaudo.Fraccionamiento", "Recaudo.AcumuladoDeEfectivo" })
        {
            var alertas = await CarteraE2E.GetAsync(http, t, $"/api/inventory/alerts?typeCode={tipo}");
            alertas.ToString().Should().Contain(tipo, $"el oficial recibe la alerta {tipo}");
        }
    }
}
