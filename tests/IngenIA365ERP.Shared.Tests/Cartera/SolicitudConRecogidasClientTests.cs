using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// Feature 028 (T028–T029; HU2, api.md §2): la solicitud manda lo que recoge (<c>payoffs</c>, enums por número) con su proyección
/// (<c>payoffProjection</c>) y lee del servidor <c>payoffs[]</c> y <c>payoffSummary</c>; al corregir, lo guardado se vuelve a pedir tal cual
/// (<see cref="RecogidaDeSolicitudDto.ComoPedido"/>) o se marca en la tabla (<see cref="RecogidaDeSolicitudDto.ComoMarcada"/>); el desglose se dice por
/// concepto con lo que se descuenta en negativo. (nuevo)
/// </summary>
public class SolicitudConRecogidasClientTests
{
    private static readonly Guid Linea = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Solicitud = Guid.Parse("22222222-3333-4444-5555-666666666666");
    private static readonly Guid Persona = Guid.Parse("44444444-5555-6666-7777-888888888888");
    private static readonly Guid Oficina = Guid.Parse("55555555-6666-7777-8888-999999999999");
    private static readonly Guid Credito = Guid.Parse("66666666-7777-8888-9999-000000000000");

    private readonly ServidorDeCartera _servidor = new();
    private readonly OriginacionClient _cliente;

    public SolicitudConRecogidasClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _cliente = new OriginacionClient(http, auth);
    }

    private static DesgloseDeRecogidaDto Desglose(decimal capital, decimal devuelto = 0m, decimal anticipo = 0m) =>
        new(capital, 12_000m, devuelto, 0m, 0m, 3_000m, 0m, 0m, anticipo, 0m);

    [Fact]
    public async Task El_borrador_manda_lo_que_recoge_con_su_proyeccion()
    {
        string? cuerpo = null;
        _servidor.Responder = r =>
        {
            cuerpo = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return ServidorDeCartera.Json(HttpStatusCode.OK, new { });
        };
        var solicitud = new SolicitudRequest
        {
            ApplicantPersonPublicId = Persona, OfficePublicId = Oficina, CreditLinePublicId = Linea, Amount = 8_000_000m, Installments = 24,
            Payoffs = [new RecogidaElegidaRequest(TextosDeRecogida.ClaseCredito, Credito, TextosDeRecogida.ModoParcial, 400_000m)],
            PayoffProjection = new ProyeccionDeRecogidaRequest(new DateOnly(2026, 12, 5), new DateTime(2026, 11, 27, 15, 4, 0, DateTimeKind.Utc)),
        };

        await _cliente.CrearAsync(solicitud, new ClaveDeOperacion());

        using var doc = JsonDocument.Parse(cuerpo!);
        var recogida = doc.RootElement.GetProperty("payoffs")[0];
        (recogida.GetProperty("kind").GetInt32(), recogida.GetProperty("mode").GetInt32()).Should().Be((1, 2), "los enums viajan por número");
        recogida.GetProperty("productPublicId").GetGuid().Should().Be(Credito);
        recogida.GetProperty("amount").GetDecimal().Should().Be(400_000m);
        doc.RootElement.GetProperty("payoffProjection").GetProperty("projectedDisbursementDate").GetString().Should().Be("2026-12-05");
    }

    [Fact]
    public async Task Lee_lo_que_recoge_la_solicitud_y_su_resumen()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new
        {
            publicId = Solicitud, number = "SOL-000123", kind = 1, status = 3, rowVersion = Convert.ToBase64String([1]),
            applicant = new { personPublicId = Persona, fullName = "ANA SOLICITANTE" }, debtorKind = 1, office = (object?)null,
            line = new { publicId = Linea, code = "CONLIB", name = "Consumo", versionPublicId = (Guid?)null }, amount = 8_000_000m, paymentForm = 1,
            policyExceptionRequested = false, parties = Array.Empty<object>(), requirements = Array.Empty<object>(), documents = Array.Empty<object>(),
            checks = Array.Empty<object>(), isRelatedParty = false, decisions = Array.Empty<object>(), blockedByRestrictiveLists = false,
            createdBy = "asesora", createdAt = "2026-11-27T15:00:00Z",
            payoffs = new[]
            {
                new
                {
                    kind = 1, productPublicId = Credito, reference = (string?)null, number = "65-78925", mode = 1, requestedAmount = (decimal?)null,
                    projectedAmount = 1_015_000m, breakdown = new
                    {
                        principal = 1_000_000m, currentInterest = 12_000m, interestReturned = 0m, interestCharged = 0m, defaultInterest = 0m,
                        insurance = 3_000m, administration = 0m, otherCharges = 0m, advance = 0m, creditBalance = 0m,
                    },
                    installmentTreatment = 1, interestMode = "Corrido",
                    currentInstallment = new { number = 7, dueDate = "2026-12-30", amount = 303_683.06m, treatment = 1, choosable = false, sent = (object?)null },
                    projectedDate = "2026-12-05", recordedAsOf = "2026-11-27T15:04:00Z", projected = true, inOtherApplication = "", order = 1,
                },
            },
            payoffSummary = new { total = 1_015_000m, netToDisburse = 6_985_000m, projectedDate = "2026-12-05", recordedAsOf = "2026-11-27T15:04:00Z" },
        });

        var r = await _cliente.SolicitudAsync(Solicitud);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var recogida = r.Value!.Payoffs.Should().ContainSingle().Subject;
        (recogida.Number, recogida.Mode, recogida.ProjectedAmount, recogida.Projected).Should().Be(("65-78925", 1, 1_015_000m, true));
        recogida.CurrentInstallment!.Number.Should().Be(7);
        TextosDeRecogida.EnOtraSolicitud(recogida.InOtherApplication).Should().Be("Está en otra solicitud en borrador");
        r.Value.PayoffSummary.Should().Be(new ResumenDeRecogidasDeSolicitudDto(1_015_000m, 6_985_000m, new DateOnly(2026, 12, 5),
            new DateTime(2026, 11, 27, 15, 4, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Al_corregir_lo_guardado_se_pide_tal_cual_y_se_marca_en_la_tabla()
    {
        var total = new RecogidaDeSolicitudDto(TextosDeRecogida.ClaseCredito, Credito, null, "65-78925", TextosDeRecogida.ModoTotal, 500m, 1_015_000m,
            Desglose(1_000_000m), TextosDeRecogida.CuotaIncluida, "Corrido", null, new DateOnly(2026, 12, 5), DateTime.UtcNow, true, null, 1);
        var parcial = total with { Mode = TextosDeRecogida.ModoParcial, RequestedAmount = 400_000m, ProjectedAmount = 400_000m };

        total.ComoPedido().Should().Be(new RecogidaElegidaRequest(1, Credito, 1, null, null, 1), "el valor sólo va en un parcial");
        parcial.ComoPedido().Amount.Should().Be(400_000m);
        // 2026-11-27 (feature 028, E2, T066): con el tratamiento de la cuota causada que guardó (en una recogida total).
        total.ComoMarcada().Should().Be(new RecogidaMarcada(Credito, "65-78925", 1, null, 1_015_000m, TextosDeRecogida.CuotaIncluida));
        parcial.ComoMarcada()!.Amount.Should().Be(400_000m);
        // 2026-11-27 (feature 028, E4, T089): una factura se marca por su número, con el identificador que da el servidor.
        var factura = (total with { Kind = TextosDeRecogida.ClaseFactura, ProductPublicId = null, Reference = "FE-1", Number = "FE-1" }).ComoMarcada();
        factura.Should().Be(new RecogidaMarcada(IdentificadoresDeRecogida.DeFactura("FE-1"), "FE-1", 1, null, 1_015_000m, Kind: TextosDeRecogida.ClaseFactura,
            Reference: "FE-1"));
        factura!.ComoPedido().Should().Be(new RecogidaElegidaRequest(TextosDeRecogida.ClaseFactura, null, 1, null, "FE-1"), "una factura se pide por su número");
        // El mismo identificador que da el servidor (FacturaRecogible.IdentificadorDe; RecogidaDeFacturasTests fija el mismo valor).
        IdentificadoresDeRecogida.DeFactura("FV-1001").Should().Be(Guid.Parse("6a85d6f9-d2a5-f33d-a7d1-496853c57e16"));
    }

    [Fact]
    public void El_desglose_se_dice_por_concepto_y_lo_que_se_descuenta_va_en_negativo()
    {
        TextosDeRecogida.Conceptos(Desglose(1_000_000m, devuelto: 2_500m, anticipo: 10_000m)).Should().Equal(
            ("Capital", 1_000_000m), ("Interés corriente", 12_000m), ("Interés devuelto por proporcionales", -2_500m), ("Seguro", 3_000m),
            ("Anticipo a favor", -10_000m));
    }
}
