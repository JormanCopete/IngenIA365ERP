using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Domain.Enums.Lending;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DocumentosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0770 (feature 013, US13; adaptadores-y-archivos.md §8.8, §10, Edge Case; T80; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>), en la
/// cooperativa «documentos» (<see cref="DocumentosE2E"/>): se paga el saldo total, se emite el paz y salvo y se reversa ese pago —por la pantalla del
/// crédito (<c>ReverseLoanPaymentCommand</c>, cuya respuesta trae <c>voidedDocuments[]</c>) y por el contrato de obligaciones con el recaudador de
/// prueba (<c>ReversarAsync</c>)—: el paz y salvo verifica <c>Voided</c> con el motivo («Se reversó el pago …») y su evento lleva como operación causa
/// el reverso, en la misma transacción. Un documento emitido <b>antes</b> del pago reversado (el plan de pagos) no se anula. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class ElReversoAnulaLosDocumentosTests(CentralIdentityApiFixture fx)
{
    private static Guid Credito(JsonElement desembolso) => desembolso.GetProperty("loanPublicId").GetGuid();

    [Fact]
    public async Task El_reverso_por_pantalla_anula_el_paz_y_salvo_y_no_lo_emitido_antes_del_pago()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var admin = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
        var jefe = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Jefe);
        var (_, persona, d) = await DesembolsoE2E.CreditoDesembolsadoAsync(http, esc, "DocReversoPantalla");
        var credito = Credito(d);
        var dia = esc.Hoy.AddDays(2);

        using (RelojEn(fx, dia))
        {
            // Antes del pago: el plan de pagos.
            var plan = await EmitidoAsync(http, admin, Pedido("PaymentPlan", persona, credito));
            var (clave, _) = await CancelarAsync(http, esc, persona, credito, dia);
            var paz = await EmitidoAsync(http, admin, Pedido("LoanClearance", persona, credito));
            var pazId = paz.GetProperty("documentPublicId").GetGuid();
            var pago = (await PagosE2E.OperacionesAsync(fx, esc, credito)).Single(o => o.Clave == clave);

            // El reverso desde la pantalla del crédito: vista previa con su huella y reverso; la respuesta trae el paz y salvo anulado.
            const string motivo = "Cheque devuelto por fondos insuficientes (e2e)";
            var previa = await CarteraE2E.ExitoAsync(http, jefe, HttpMethod.Post,
                $"{DesembolsoE2E.Creditos}/{credito}/operations/{pago.PublicId}/reversal-preview", new { reason = motivo });
            var reverso = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{DesembolsoE2E.Creditos}/{credito}/operations/{pago.PublicId}/reverse",
                new { reason = motivo, expectedPreviewSha256 = previa.GetProperty("previewSha256").GetString() });
            reverso.StatusCode.Should().Be(HttpStatusCode.Created, await reverso.Content.ReadAsStringAsync());
            var hecho = await CarteraE2E.LeerAsync(reverso);
            var anulados = hecho.GetProperty("voidedDocuments").EnumerateArray().Select(x => x.GetGuid()).ToList();
            anulados.Should().Contain(pazId, "el reverso anula en su misma transacción el paz y salvo que respaldaba el pago");
            anulados.Should().NotContain(plan.GetProperty("documentPublicId").GetGuid(), "el plan se emitió antes del pago reversado");

            // La verificación lo dice: anulado con el motivo, que nombra el pago reversado; la operación causa del evento es el reverso.
            var verificado = await VerificarAsync(http, admin, paz.GetProperty("verificationCode").GetString()!);
            verificado.GetProperty("status").GetString().Should().Be("Voided");
            verificado.GetProperty("voided").GetProperty("reason").GetString().Should().Contain(pago.Numero);
            var anulacion = (await CarteraE2E.GetAsync(http, admin, $"{Documentos}/{pazId}")).GetProperty("events").EnumerateArray()
                .Single(e => e.GetProperty("kind").GetString() == "Voided");
            anulacion.GetProperty("causeOperation").GetString().Should().Be(hecho.GetProperty("number").GetString());

            // Lo emitido antes del pago sigue vigente.
            (await VerificarAsync(http, admin, plan.GetProperty("verificationCode").GetString()!)).GetProperty("status").GetString().Should().Be("Valid");
            (await PagosE2E.EstadoAsync(fx, esc, credito)).Should().Be(LoanStatus.Active, "sin el pago el crédito vuelve a tener saldo");
        }
    }

    [Fact]
    public async Task El_reverso_por_el_contrato_de_obligaciones_tambien_anula_el_paz_y_salvo()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var admin = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
        var (_, persona, d) = await DesembolsoE2E.CreditoDesembolsadoAsync(http, esc, "DocReversoContrato");
        var credito = Credito(d);
        var dia = esc.Hoy.AddDays(2);

        using (RelojEn(fx, dia))
        {
            var (clave, _) = await CancelarAsync(http, esc, persona, credito, dia);
            var paz = await EmitidoAsync(http, admin, Pedido("LoanClearance", persona, credito));
            paz.GetProperty("status").GetString().Should().Be("SubjectToFinality");

            // El recaudador de prueba reversa su pago por el contrato (como lo haría Recaudo con un cheque devuelto).
            var reverso = await PagosE2E.ReversarPorClaveAsync(http, esc.Usuarios, clave, "El banco devolvió el cheque (e2e)");
            PagosE2E.Resultado(reverso).Should().Be(PagosE2E.Aplicado, reverso.ToString());

            var verificado = await VerificarAsync(http, admin, paz.GetProperty("verificationCode").GetString()!);
            verificado.GetProperty("status").GetString().Should().Be("Voided");
            var operaciones = await PagosE2E.OperacionesAsync(fx, esc, credito);
            var pago = operaciones.Single(o => o.Clave == clave);
            var deReverso = operaciones.Single(o => o.Clase == LoanOperationKind.PaymentReversal);
            var anulacion = (await CarteraE2E.GetAsync(http, admin, $"{Documentos}/{paz.GetProperty("documentPublicId").GetGuid()}"))
                .GetProperty("events").EnumerateArray().Single(e => e.GetProperty("kind").GetString() == "Voided");
            anulacion.GetProperty("causeOperation").GetString().Should().Be(deReverso.Numero, "la operación causa es el reverso");
            anulacion.GetProperty("reason").GetString().Should().Contain(pago.Numero, "el motivo nombra el pago reversado");
        }
    }
}
