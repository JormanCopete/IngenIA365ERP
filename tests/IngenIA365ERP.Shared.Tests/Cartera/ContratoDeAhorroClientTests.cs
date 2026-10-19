using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// Feature 014, US7 (T185; contracts/api.md §4): el ahorro contractual en <see cref="CaptacionesClient"/>. La instrucción al vencimiento lleva su
/// <c>Idempotency-Key</c> y va plana a <c>PUT /accounts/{id}/maturity-instruction</c>; el contrato y las condiciones selladas se leen sin clave; ninguna pone
/// <c>Authorization</c>; los textos del contrato y la apertura con su bloque <c>contractual</c>. (nuevo)
/// </summary>
public class ContratoDeAhorroClientTests
{
    private static readonly Guid Cuenta = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Vista = Guid.Parse("99999999-8888-7777-6666-555555555555");

    private readonly ServidorDeCartera _servidor = new();
    private readonly CaptacionesClient _cliente;

    public ContratoDeAhorroClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _cliente = new CaptacionesClient(http, auth);
    }

    [Fact]
    public async Task La_instruccion_al_vencimiento_lleva_su_clave_y_no_pone_Authorization()
    {
        await _cliente.CambiarInstruccionDeContratoAsync(Cuenta,
            new CambiarInstruccionDeContratoRequest(TextosDeContrato.PorTraslado, Vista, null, "Lo pide el titular"), new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle().Subject;
        vista.Metodo.Should().Be("PUT");
        vista.Ruta.Should().Be($"/api/lending/accounts/{Cuenta}/maturity-instruction");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task El_contrato_y_las_condiciones_se_leen_sin_clave()
    {
        await _cliente.ContratoDeAhorroAsync(Cuenta);
        await _cliente.CondicionesSelladasAsync(Cuenta, 2);

        _servidor.Vistas.Should().OnlyContain(v => v.Metodo == "GET" && v.Clave == null && v.Authorization == null);
        _servidor.Vistas.Select(v => v.Ruta).Should().Equal($"/api/lending/accounts/{Cuenta}/contractual", $"/api/lending/accounts/{Cuenta}/terms?version=2");
    }

    [Fact]
    public void La_instruccion_viaja_plana_como_la_pide_el_contrato_de_la_API()
    {
        var json = JsonSerializer.Serialize(new CambiarInstruccionDeContratoRequest(TextosDeContrato.PorGiro, null, new BeneficiarioDeGiroRequest(Vista), "Giro"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("kind").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("payee").GetProperty("personPublicId").GetGuid().Should().Be(Vista);
        doc.RootElement.GetProperty("reason").GetString().Should().Be("Giro");
    }

    [Fact]
    public void La_apertura_lleva_el_contrato_solo_en_una_contractual()
    {
        var pedido = new AbrirCuentaRequest { Contractual = new ContratoDeAhorroRequest { Installment = 200_000m, TermMonths = 12 } };
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(pedido, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        doc.RootElement.GetProperty("contractual").GetProperty("installment").GetDecimal().Should().Be(200_000m);
        doc.RootElement.GetProperty("contractual").GetProperty("termMonths").GetInt32().Should().Be(12);
        new AbrirCuentaRequest().Contractual.Should().BeNull();
    }

    [Fact]
    public void Los_textos_del_contrato()
    {
        TextosDeContrato.Estado(TextosDeContrato.Incumplido).Should().Be("Incumplido");
        TextosDeContrato.Efecto(3).Should().Be("Se cancela y se entrega");
        TextosDeContrato.Efecto(null).Should().Be("—");
        TextosDeContrato.Reconocimiento(2).Should().Be("Provisionada en cada cuota");
        TextosDeContrato.Tasa(0.06m).Should().Be("6 % EA");
        TextosDeContrato.EstadoDeCuota("NotGenerated", atrasada: false).Should().Be("Por generar");
        TextosDeContrato.EstadoDeCuota("Pending", atrasada: true).Should().Be("Atrasada");
        TextosDeContrato.EstadoDeCuota("Paid", atrasada: true).Should().Be("Pagada");
        TextosDeCaptaciones.ClaseDeOperacion(25).Should().Be("Incumplimiento del contrato");
        TextosDeCaptaciones.ClaseDeFila(21).Should().Be("Bonificación");
    }
}
