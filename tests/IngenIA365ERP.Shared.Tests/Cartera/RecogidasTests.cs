using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// Feature 028 (T018, T022; HU1, api.md §1): <see cref="RecogidasClient"/> consulta los productos para recoger y su PDF por POST, sin clave de operación
/// (no escriben) y sin poner <c>Authorization</c>; manda los enums por número y lee la respuesta del servidor (enums como número). Los textos dicen en
/// palabras clase, modo, origen, forma de pago, estado y los motivos por los que no se recoge, y <see cref="SeleccionDeRecogida.Resumir"/> resume lo
/// marcado con la regla del servidor. (nuevo)
/// </summary>
public class RecogidasTests
{
    private static readonly Guid Persona = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Linea = Guid.Parse("22222222-3333-4444-5555-666666666666");
    private static readonly Guid Credito = Guid.Parse("33333333-4444-5555-6666-777777777777");
    private static readonly Guid Otro = Guid.Parse("44444444-5555-6666-7777-888888888888");
    private static readonly Guid EnMora = Guid.Parse("55555555-6666-7777-8888-999999999999");

    private readonly ServidorDeCartera _servidor = new();
    private readonly RecogidasClient _cliente;

    public RecogidasTests()
    {
        var (http, auth) = _servidor.Sesion();
        _cliente = new RecogidasClient(http, auth);
    }

    /// <summary>Lo que respondería el servidor: un crédito vigente por caja, uno de libro y uno en mora; enums como número.</summary>
    private static object Respuesta() => new
    {
        date = "2026-12-05",
        recordedAsOf = "2026-11-27T15:04:00Z",
        products = new object[]
        {
            Producto(Credito, "65-78925", 5_123_456.78m, eligible: true, installment: 303_683.06m, channel: 1, origin: 1),
            Producto(Otro, "CR-000002", 1_000_000m, eligible: true, installment: null, channel: null, origin: 2),
            Producto(EnMora, "CR-000003", 520_000m, eligible: false, installment: 50_000m, channel: 3, origin: 1, bloqueo: "RequiresRestructuring",
                dias: 45),
        },
        summary = new { selectedTotal = 0m, netToDisburse = (decimal?)null, releasedInstallments = 0m, capacityAfter = (object?)null, warnings = Array.Empty<string>() },
        person = new { publicId = Persona, nombre = "ANA SOLICITANTE", documento = "52000001", correo = (string?)null, celular = (string?)null, esAsociado = true },
        newLoanLine = "CONLIB · Consumo libre inversión",
    };

    private static object Producto(Guid id, string numero, decimal valor, bool eligible, decimal? installment, int? channel, int origin,
        string? bloqueo = null, int dias = 0) => new
    {
        kind = 1, productPublicId = id, number = numero, line = "CONLIB · Consumo libre inversión", origin, channel, daysPastDue = dias, installment,
        eligible, blockReason = bloqueo, admittedModes = new[] { 1, 2 }, toPayOff = valor,
        breakdown = new
        {
            principal = valor, currentInterest = 0m, interestReturned = 0m, interestCharged = 0m, defaultInterest = 0m, insurance = 0m, administration = 0m,
            otherCharges = 0m, advance = 0m, creditBalance = 0m,
        },
        interestMode = "Corrido", interestModeReason = (string?)null,
        currentInstallment = new { number = 7, dueDate = "2026-12-30", amount = 312_400m, treatment = 1, choosable = false, sent = (object?)null },
        toPayOffExcludingCurrentInstallment = (decimal?)null, inOtherApplication = (string?)null, fingerprint = "sha256:" + new string('a', 64),
        warnings = Array.Empty<string>(), status = 2, blockMessage = bloqueo is null ? null : "El crédito CR-000003 tiene 45 días de mora.",
    };

    // ------------------------------------------------------------------------------------------------------------ cliente --

    [Fact]
    public async Task La_consulta_va_por_POST_sin_clave_ni_Authorization_y_manda_los_enums_por_numero()
    {
        string? cuerpo = null;
        _servidor.Responder = req =>
        {
            cuerpo = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return ServidorDeCartera.Json(HttpStatusCode.OK, Respuesta());
        };

        var r = await _cliente.ProductosAsync(new ProductosParaRecogerRequest(Persona, Linea, new DateOnly(2026, 12, 5),
            Selected: [new RecogidaMarcada(Credito, "65-78925", TextosDeRecogida.ModoParcial, 200_000m, 5_123_456.78m).ComoPedido()],
            NewLoan: new CreditoNuevoDeRecogidaRequest(8_000_000m, 412_300m)));

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Should().ContainSingle().Subject;
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be("/api/lending/payoffs/candidates");
        vista.Clave.Should().BeNull("consultar no escribe");
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");

        using var json = JsonDocument.Parse(cuerpo!);
        var raiz = json.RootElement;
        raiz.GetProperty("personPublicId").GetGuid().Should().Be(Persona);
        raiz.GetProperty("creditLinePublicId").GetGuid().Should().Be(Linea);
        raiz.GetProperty("projectedDisbursementDate").GetString().Should().Be("2026-12-05");
        var marcado = raiz.GetProperty("selected")[0];
        marcado.GetProperty("kind").GetInt32().Should().Be(1, "un crédito (PayoffProductKind.Loan)");
        marcado.GetProperty("mode").GetInt32().Should().Be(2, "parcial (PayoffMode.Partial)");
        marcado.GetProperty("amount").GetDecimal().Should().Be(200_000m);
        raiz.GetProperty("newLoan").GetProperty("amount").GetDecimal().Should().Be(8_000_000m);
    }

    [Fact]
    public async Task Lee_la_respuesta_del_servidor()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, Respuesta());

        var r = await _cliente.ProductosAsync(new ProductosParaRecogerRequest(Persona));

        var d = r.Value!;
        d.Date.Should().Be(new DateOnly(2026, 12, 5));
        d.Person!.Nombre.Should().Be("ANA SOLICITANTE");
        d.NewLoanLine.Should().StartWith("CONLIB");
        d.Products.Should().HaveCount(3);
        var vigente = d.Products[0];
        vigente.Kind.Should().Be(TextosDeRecogida.ClaseCredito);
        vigente.ToPayOff.Should().Be(5_123_456.78m);
        vigente.Breakdown.Total.Should().Be(5_123_456.78m);
        vigente.AdmittedModes.Should().Equal(TextosDeRecogida.ModoTotal, TextosDeRecogida.ModoParcial);
        vigente.AdmiteParcial.Should().BeTrue();
        vigente.CurrentInstallment!.Number.Should().Be(7);
        vigente.CurrentInstallment.Treatment.Should().Be(TextosDeRecogida.CuotaIncluida);
        vigente.Channel.Should().Be(1);
        vigente.Status.Should().Be(2);
        var mora = d.Products[2];
        mora.Eligible.Should().BeFalse();
        mora.BlockReason.Should().Be("RequiresRestructuring");
        mora.BlockMessage.Should().Contain("45 días");
    }

    [Fact]
    public async Task El_PDF_va_por_POST_sin_clave_ni_Authorization()
    {
        _servidor.Responder = _ =>
        {
            var respuesta = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]) };
            respuesta.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            respuesta.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "simulacion-de-recogida-52000001-20261205.pdf" };
            return respuesta;
        };

        var r = await _cliente.SimulacionEnPdfAsync(new ProductosParaRecogerRequest(Persona, Linea, new DateOnly(2026, 12, 5)));

        r.IsSuccess.Should().BeTrue();
        r.Value!.Nombre.Should().Be("simulacion-de-recogida-52000001-20261205.pdf");
        r.Value.TipoContenido.Should().Be("application/pdf");
        var vista = _servidor.Vistas.Should().ContainSingle().Subject;
        (vista.Metodo, vista.Ruta).Should().Be(("POST", "/api/lending/payoffs/candidates/pdf"));
        vista.Clave.Should().BeNull();
        vista.Authorization.Should().BeNull();
    }

    // ------------------------------------------------------------------------------------------------------------- resumen --

    private static List<ProductoParaRecogerDto> Productos()
    {
        var d = JsonSerializer.Deserialize<ProductosParaRecogerDto>(JsonSerializer.Serialize(Respuesta()), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return [.. d.Products];
    }

    [Fact]
    public void El_resumen_de_la_pantalla_sigue_la_regla_del_servidor()
    {
        var productos = Productos();
        IReadOnlyList<RecogidaMarcada> marcadas =
        [
            new(Credito, "65-78925", TextosDeRecogida.ModoTotal, null, 5_123_456.78m),
            new(Otro, "CR-000002", TextosDeRecogida.ModoParcial, 3_000_000m, 1_000_000m),
            new(EnMora, "CR-000003", TextosDeRecogida.ModoTotal, null, 520_000m),
        ];

        var r = SeleccionDeRecogida.Resumir(productos, marcadas, 8_000_000m, 412_300m);

        r.SelectedTotal.Should().Be(6_123_456.78m, "el parcial se topa en lo que cuesta recoger y el de mora no se cuenta");
        r.SelectedCount.Should().Be(2);
        r.NetToDisburse.Should().Be(1_876_543.22m);
        r.ReleasedInstallments.Should().Be(303_683.06m, "sólo libera su cuota el recogido total");
        r.NewInstallment.Should().Be(412_300m);
        r.NetNotPositive.Should().BeFalse();
        r.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Un_neto_en_cero_o_negativo_se_avisa_y_sin_monto_no_hay_neto()
    {
        var productos = Productos();
        IReadOnlyList<RecogidaMarcada> marcadas = [new(Credito, "65-78925", TextosDeRecogida.ModoTotal, null, 5_123_456.78m)];

        var negativo = SeleccionDeRecogida.Resumir(productos, marcadas, 5_000_000m, null);
        var sinMonto = SeleccionDeRecogida.Resumir(productos, marcadas, null, null);

        negativo.NetNotPositive.Should().BeTrue();
        negativo.Warnings.Should().ContainSingle(a => a.Contains("no se puede radicar"));
        sinMonto.NetToDisburse.Should().BeNull();
        sinMonto.NetNotPositive.Should().BeFalse();
        sinMonto.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Lo_marcado_se_pide_como_lo_espera_el_servidor()
    {
        new RecogidaMarcada(Credito, "65-78925", TextosDeRecogida.ModoTotal, 100m, 5_000m).ComoPedido()
            .Should().Be(new RecogidaElegidaRequest(TextosDeRecogida.ClaseCredito, Credito, TextosDeRecogida.ModoTotal), "un total no lleva valor");
        new RecogidaMarcada(Credito, "65-78925", TextosDeRecogida.ModoParcial, 100m, 5_000m).ComoPedido()
            .Should().Be(new RecogidaElegidaRequest(TextosDeRecogida.ClaseCredito, Credito, TextosDeRecogida.ModoParcial, 100m));
        var seleccion = new SeleccionDeRecogida([new(Credito, "65-78925", TextosDeRecogida.ModoTotal, null, 5_000m)],
            new ResumenDeRecogidaDto(5_000m, null, 0m, null, []), new DateOnly(2026, 12, 5), null);
        seleccion.Pedido.Should().ContainSingle().Which.ProductPublicId.Should().Be(Credito);
        SeleccionDeRecogida.Vacia(new DateOnly(2026, 12, 5)).Marcadas.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------------------------------------------- textos --

    [Theory]
    [InlineData("RequiresRestructuring", "En mora: requiere reestructuración")]
    [InlineData("WrittenOff", "Castigado")]
    [InlineData("Insolvency", "En insolvencia")]
    [InlineData("NoBalance", "Sin saldo")]
    [InlineData("NotActive", "No está vigente")]
    [InlineData("NotAdmitted", "La línea no lo admite")]
    [InlineData("NotSettled", "No se pudo liquidar")]
    [InlineData(null, "")]
    [InlineData("Otro", "Otro")]
    public void Cada_motivo_se_dice_en_palabras(string? motivo, string texto) => TextosDeRecogida.Motivo(motivo).Should().Be(texto);

    [Fact]
    public void Los_numeros_de_los_enums_se_dicen_en_palabras()
    {
        TextosDeRecogida.Clase(1).Should().Be("Crédito");
        TextosDeRecogida.Clase(3).Should().Be("Factura a crédito");
        TextosDeRecogida.Modo(1).Should().Be("Total");
        TextosDeRecogida.Modo(2).Should().Be("Parcial");
        TextosDeRecogida.Modo(null).Should().Be("—");
        TextosDeRecogida.Origen(1).Should().Be("Crédito");
        TextosDeRecogida.Origen(3).Should().Be("Crédito", "un crédito migrado es un crédito");
        TextosDeRecogida.Origen(2).Should().Be("Venta de Comercial");
        TextosDeRecogida.Origen(4).Should().Be("Cargo masivo");
        TextosDeRecogida.Canal(1).Should().Be("Caja");
        TextosDeRecogida.Canal(2).Should().Be("Nómina propia");
        TextosDeRecogida.Canal(3).Should().Be("Libranza");
        TextosDeRecogida.Canal(null).Should().Be("—");
        TextosDeRecogida.Estado(2).Should().Be("Vigente");
        TextosDeRecogida.Estado(4).Should().Be("Castigado");
        TextosDeRecogida.TratamientoDeCuota(TextosDeRecogida.CuotaIncluida).Should().Be("Se incluye en la recogida");
        TextosDeRecogida.ModoDeIntereses("CuotaCompleta").Should().Be("Cuota completa");
        TextosDeRecogida.ModoDeIntereses("Corrido").Should().Be("Interés corrido");
    }

    [Fact]
    public void Otra_solicitud_se_dice_con_su_numero_o_como_borrador()
    {
        TextosDeRecogida.EnOtraSolicitud("S-000123").Should().Be("Está en la solicitud S-000123");
        TextosDeRecogida.EnOtraSolicitud("").Should().Be("Está en otra solicitud en borrador");
        TextosDeRecogida.EnOtraSolicitud(null).Should().BeEmpty();
    }

    [Fact]
    public void Los_textos_no_nombran_el_sistema_anterior()
    {
        var textos = new[] { TextosDeRecogida.Motivo("NotAdmitted"), TextosDeRecogida.EnOtraSolicitud("S-1"), TextosDeRecogida.Pesos(1_000m) };
        textos.Should().NotContain(t => t.Contains("SOLIDO", StringComparison.OrdinalIgnoreCase));
        TextosDeRecogida.Pesos(1_234_567m).Should().Contain("1.234.567");
        TextosDeRecogida.Pesos((decimal?)null).Should().Be("—");
    }
}
