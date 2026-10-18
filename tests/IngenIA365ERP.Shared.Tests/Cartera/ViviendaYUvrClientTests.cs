using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// K6b (feature 013, fase 23, bloque 2; T1060; api.md §29): los clientes leen lo de vivienda y UVR que la API agrega —opcional, nulo fuera de UVR—: la
/// UVR del día y los saldos en UVR de la liquidación, el tope y los límites de una línea de vivienda con su fuente; y los textos lo muestran a la
/// diezmilésima sin código técnico. Una respuesta sin esos campos (K1–K6a) se lee igual. (nuevo)
/// </summary>
public class ViviendaYUvrClientTests
{
    private static readonly Guid Credito = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa");
    private static readonly Guid Linea = Guid.Parse("77777777-8888-9999-aaaa-bbbbbbbbbbbb");

    private readonly ServidorDeCartera _servidor = new();
    private readonly CreditosClient _creditos;
    private readonly LineasDeCreditoClient _lineas;

    public ViviendaYUvrClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _creditos = new CreditosClient(http, auth);
        _lineas = new LineasDeCreditoClient(http, auth);
    }

    [Fact]
    public async Task La_liquidacion_de_un_credito_en_UVR_trae_la_UVR_del_dia_y_los_saldos_en_UVR_y_la_de_pesos_no()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, Liquidacion(new
        {
            uvrDate = "2026-11-15", uvrValue = 405.6612m, projected = true, lastPublished = "2026-11-10", principalUvr = 12_345.6789m,
            toPayOffUvr = 12_400.1234m, balancesUvr = new Dictionary<string, decimal> { ["Principal"] = 12_345.6789m, ["CurrentInterest"] = 54.4445m },
            legend = "UVR proyectada",
        }));

        var enUvr = (await _creditos.LiquidacionAsync(Credito, new DateOnly(2026, 11, 15))).Value!;
        enUvr.Uvr!.Projected.Should().BeTrue();
        enUvr.Uvr.UvrValue.Should().Be(405.6612m);
        enUvr.Uvr.BalancesUvr["CurrentInterest"].Should().Be(54.4445m);
        enUvr.Uvr.LastPublished.Should().Be(new DateOnly(2026, 11, 10));

        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, Liquidacion(null));
        (await _creditos.LiquidacionAsync(Credito)).Value!.Uvr.Should().BeNull("un crédito en pesos no trae nada de UVR");
    }

    [Fact]
    public async Task Una_linea_de_vivienda_trae_el_tope_y_los_limites_con_su_fuente()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new
        {
            publicId = Linea, code = "VIVUVR", name = "Vivienda en UVR", purpose = (string?)null, status = 2, closedReason = (int?)null,
            closedAt = (DateTime?)null, closeNotes = (string?)null, usuryBlockedSince = (DateOnly?)null, usuryBlockedByValuePublicId = (Guid?)null,
            admitsNewCredits = true, currentVersion = (object?)null, versions = Array.Empty<object>(),
            housing = new
            {
                date = "2026-11-15",
                rateCap = new { rate = 0.12m, validFrom = "2026-11-01", source = "Superintendencia Financiera", nationalValuePublicId = Guid.NewGuid() },
                limits = new[] { new { code = "VIVIENDA_FINANCIABLE_VIS", value = 0.80m, source = "Ley 546 de 1999, art. 17", nationalValuePublicId = Guid.NewGuid() } },
                missing = Array.Empty<string>(),
            },
        });

        var linea = (await _lineas.LineaAsync(Linea)).Value!;
        linea.Housing!.RateCap!.Rate.Should().Be(0.12m);
        linea.Housing.Limits.Single().Source.Should().Be("Ley 546 de 1999, art. 17");
        TextosDeCreditos.LimiteDeVivienda(linea.Housing.Limits.Single().Code).Nombre.Should().Be("Porcentaje financiable de vivienda de interés social");
        TextosDeCreditos.ValorDeLimiteDeVivienda("VIVIENDA_PLAZO_MAXIMO_MESES", 360m).Should().Be("360 meses");
    }

    [Fact]
    public void Los_textos_muestran_la_UVR_a_la_diezmilesima_y_la_correccion_monetaria_en_espanol()
    {
        TextosDeCreditos.Uvr(12_345.6789m).Should().Be("12.345,6789 UVR");
        TextosDeCreditos.Uvr(null).Should().Be("—");
        TextosDeCreditos.ValorDeUvr(405.1234m).Should().Be("$ 405,1234");
        TextosDeCreditos.UnidadDeCapital("Uvr").Should().Be("UVR");
        TextosDeCreditos.UnidadDeCapital("Pesos").Should().Be("Pesos");
        TextosDeCreditos.ClaseDeMovimiento("MonetaryCorrection").Should().Be("Corrección monetaria UVR");
    }

    private static object Liquidacion(object? uvr) => new
    {
        loanPublicId = Credito, number = "CR-0002107", date = "2026-11-15", recordedAsOf = (DateTime?)null, rulesVersion = "LND-2026.2",
        fingerprint = "sha256:x", toPayOff = 5_030_000.00m, byConcept = new Dictionary<string, decimal> { ["Principal"] = 5_008_190.12m },
        credits = new Dictionary<string, decimal>(), byInstallment = Array.Empty<object>(), daysPastDue = 0, suspended = false, usuryCapped = false,
        explanation = Array.Empty<object>(), explanationText = Array.Empty<string>(), validUntil = (DateOnly?)null, warnings = Array.Empty<string>(),
        uvr,
    };
}
