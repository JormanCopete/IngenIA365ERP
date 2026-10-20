using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T218 (US15, escenario 1 de la spec y la prueba independiente; contracts/api.md §15; research §24): el fondo de liquidez por HTTP, sobre una
/// cooperativa con captaciones activadas (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>, nombre <c>cap_liquidez</c>) de categoría básica e
/// inscrita en el seguro de depósitos.
/// <list type="number">
/// <item>Con 10.000.000 en ahorro a la vista el requerido es el 10 % (régimen general); sin constituido, la posición avisa el faltante y la foto lo deja con la
/// alerta (US15-1). Pedir la foto otra vez devuelve la misma.</item>
/// <item>Registrada una inversión de 1.100.000 con su soporte, la posición queda cubierta; el IRL se mide en las cuatro bandas y la vista
/// <c>insured-deposits</c> lista a la titular.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class FondoDeLiquidezTests(CentralIdentityApiFixture fx)
{
    private const string Fondo = "/api/lending/liquidity-fund";

    [Fact]
    public async Task US15_1_requerido_del_regimen_general_faltante_con_alerta_y_cubierto_al_registrar_lo_constituido()
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "liquidez");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var hoy = CarteraE2E.Hoy(fx);
        await PerfilBasicoAseguradoAsync(http, admin);

        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHLIQ");
        var titular = await MovimientosTests.AsociadoAsync(http, admin, "Liquidez");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, linea, titular);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, titular, 10_000_000m, hoy, medio: "Transfer");

        // 1. El régimen general: 10 % del ahorro a la vista; sin constituido, faltante.
        var posicion = await CarteraE2E.GetAsync(http, admin, $"{Fondo}?date={hoy:yyyy-MM-dd}");
        posicion.GetProperty("category").GetString().Should().Be("Basica");
        posicion.GetProperty("regime").GetProperty("stage").GetString().Should().Be("General");
        posicion.GetProperty("required").GetDecimal().Should().Be(1_000_000m);
        posicion.GetProperty("alert").GetString().Should().Be("Shortfall");

        var foto = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Fondo}/snapshots", new { date = hoy, reason = "Corte del tesorero (e2e)" });
        foto.GetProperty("shortfall").GetDecimal().Should().Be(1_000_000m);
        foto.GetProperty("alertPublicId").ValueKind.Should().NotBe(JsonValueKind.Null, "el faltante avisa al tesorero y a la gerencia (US15-1)");
        var otra = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Fondo}/snapshots", new { date = hoy });
        otra.GetProperty("publicId").GetGuid().Should().Be(foto.GetProperty("publicId").GetGuid(), "una fecha tiene una sola foto");

        // 2. Lo constituido con su soporte: queda cubierto, con el colchón del 5 %.
        var banco = await MovimientosTests.AsociadoAsync(http, admin, "BancoDelFondo");
        var reservado = Guid.NewGuid();
        var inversion = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Fondo}/holdings", new
        {
            instrument = "BankDeposit", entityPersonPublicId = banco, reference = "CTA-FONDO-1", amount = 1_100_000m, constitutedOn = hoy, maturityDate = (DateOnly?)null,
            supportAttachmentPublicId = await SoporteAsync(http, admin, reservado), note = "Cuenta del fondo (e2e)", publicId = reservado,
        });
        inversion.StatusCode.Should().Be(HttpStatusCode.OK, await inversion.Content.ReadAsStringAsync());
        (await CarteraE2E.GetAsync(http, admin, $"{Fondo}?date={hoy:yyyy-MM-dd}")).GetProperty("alert").GetString().Should().Be("None");

        var irl = await CarteraE2E.GetAsync(http, admin, $"{Fondo}/risk?date={hoy:yyyy-MM-dd}&measure=Irl");
        irl.GetProperty("bands").GetArrayLength().Should().Be(4);
        irl.GetProperty("netLiquidAssets").GetDecimal().Should().Be(1_100_000m, "la cuenta de ahorros del fondo no lleva descuento");

        var asegurados = await CarteraE2E.GetAsync(http, admin, $"/api/reports/lending/insured-deposits?asOf={hoy:yyyy-MM-dd}");
        asegurados.GetProperty("filas").GetArrayLength().Should().Be(1);
    }

    // ------------------------------------------------------------------------------------------------------------ apoyo --

    /// <summary>Una versión del perfil regulatorio de categoría básica para el fondo de liquidez e inscrita en el seguro de depósitos.</summary>
    private static async Task PerfilBasicoAseguradoAsync(HttpClient http, string admin)
    {
        const string Perfil = "/api/core/regulatory-profile";
        var vigente = (await CarteraE2E.GetAsync(http, admin, Perfil)).GetProperty("current");
        var perfil = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Perfil}/versions", new
        {
            organizationType = vigente.GetProperty("organizationTypeCode").GetString(), financialActivity = true, supervisionLevel = 2,
            validFrom = DateOnly.Parse(vigente.GetProperty("validFrom").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            source = "Certificado de la Supersolidaria (e2e)", reason = "Categoría del fondo de liquidez y seguro de depósitos (e2e)",
            supersedesPublicId = vigente.GetProperty("versionPublicId").GetGuid(), mayTakeDeposits = true, liquidityFundCategory = "Basica",
            depositInsuranceEnrolled = true,
        });
        perfil.StatusCode.Should().Be(HttpStatusCode.Created, $"el perfil de la cooperativa: «{await perfil.Content.ReadAsStringAsync()}»");
    }

    /// <summary>Sube el extracto de la cuenta del fondo con el dueño reservado, como el navegador: pedir, enviar al almacén local, confirmar.</summary>
    private static async Task<Guid> SoporteAsync(HttpClient http, string token, Guid dueno)
    {
        var contenido = AdjuntosE2E.Pdf(4_096);
        using var pedido = await AdjuntosE2E.PedirSubidaAsync(http, token, dueno, contenido, "extracto-fondo.pdf", ownerEntityType: "LendingLiquidityFundSupport");
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var autorizacion = await CarteraE2E.LeerAsync(pedido);
        var adjunto = autorizacion.GetProperty("attachmentPublicId").GetGuid();
        var subida = autorizacion.GetProperty("upload");
        using var formulario = new MultipartFormDataContent();
        foreach (var campo in subida.GetProperty("fields").EnumerateObject())
            formulario.Add(new StringContent(campo.Value.GetString()!), campo.Name);
        formulario.Add(new ByteArrayContent(contenido), subida.GetProperty("fileField").GetString()!, "extracto-fondo.pdf");
        var url = subida.GetProperty("url").GetString()!;
        var absoluta = Uri.TryCreate(url, UriKind.Absolute, out var destino);
        using var almacen = await http.PostAsync(absoluta ? destino!.PathAndQuery : url, formulario);
        almacen.IsSuccessStatusCode.Should().BeTrue($"el almacén recibe el soporte: {(int)almacen.StatusCode}");
        using var confirmacion = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, $"/api/attachments/{adjunto}/confirm", null);
        confirmacion.StatusCode.Should().Be(HttpStatusCode.OK, await confirmacion.Content.ReadAsStringAsync());
        return adjunto;
    }
}
