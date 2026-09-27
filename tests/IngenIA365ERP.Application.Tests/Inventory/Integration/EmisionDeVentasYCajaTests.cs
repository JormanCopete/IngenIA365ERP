using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Inventory.Integration;

/// <summary>
/// Feature 012, I3, T557 (contracts/mensajes.md §6.1, §6.2, §6.8, §6.11, §6.14, §6.15): los contenidos de los mensajes de venta y caja.
/// <c>VentaFacturada</c> cumple sus invariantes y lleva un pago por medio con el tercero según la clase y sin el número de la tarjeta;
/// <c>CostoDeVentaReconocido</c> no lleva base ni impuestos; la nota con devolución emite <c>NotaCreditoEmitida</c> y
/// <c>DevolucionRegistrada</c> con los reintegros <c>Refunded</c>; <c>MovimientoDeCajaRegistrado</c> lleva el destino y, en
/// <c>Register</c>, la sesión que recibe; <c>DiferenciaDeArqueoAprobada</c> una línea por medio (también la aceptada dentro de la
/// tolerancia) y el cajero con su persona si hay faltante a su cargo; <c>originEventKey</c> es <c>Confirmation</c> y la venta del punto
/// lleva <c>pointOfSaleCode</c> y <c>cashSessionPublicId</c>.
/// </summary>
public class EmisionDeVentasYCajaTests
{
    private static JsonElement Carga(VentasDePrueba v, Guid documento, string tipo) => JsonDocument.Parse(v.ContenidoDe(documento, tipo)).RootElement;

    private static decimal Suma(JsonElement arreglo, string propiedad, Func<JsonElement, bool>? filtro = null) =>
        arreglo.EnumerateArray().Where(e => filtro is null || filtro(e)).Sum(e => e.GetProperty(propiedad).GetDecimal());

    private static bool Es(JsonElement e, string propiedad, Enum valor) =>
        e.GetProperty(propiedad).ValueKind == JsonValueKind.String
            ? e.GetProperty(propiedad).GetString() == valor.ToString()
            : e.GetProperty(propiedad).GetInt32() == Convert.ToInt32(valor);

    [Fact]
    public void En_la_anulacion_los_pagos_tambien_invierten_su_importe_y_conservan_su_sentido()
    {
        // mensajes.md §3: «Los pagos llevan amount positivo y el sentido en direction; en un DocumentoAnulado su amount también se
        // invierte». Hasta el 2026-09-27 se saltaban y el espejo de una venta descuadraba por el doble de lo cobrado (e2e T570).
        var original = """{"totals":{"total":100000.00},"lines":[{"grossAmount":100000.00}],"payments":[{"direction":"Received","amount":100000.00,"lineNumber":1}]}""";

        var invertido = EmisionDeInventario.Invertido(original)!;

        invertido["totals"]!["total"]!.GetValue<decimal>().Should().Be(-100000m);
        invertido["lines"]![0]!["grossAmount"]!.GetValue<decimal>().Should().Be(-100000m);
        invertido["payments"]![0]!["amount"]!.GetValue<decimal>().Should().Be(-100000m);
        invertido["payments"]![0]!["direction"]!.GetValue<string>().Should().Be("Received", "el sentido no cambia: lo invierte el signo");
        invertido["payments"]![0]!["lineNumber"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public async Task VentaFacturada_cumple_sus_invariantes_y_lleva_un_pago_por_medio_con_su_tercero_sin_numero_de_tarjeta()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(total =>
        [
            v.Pago(v.Efectivo, 5000m),
            new DocumentPaymentInput(v.Tarjeta.PublicId, 6900m, AuthorizationCode: "123456", Last4: "4242", CashSessionPublicId: v.Sesion.PublicId),
            v.Pago(v.Bono, total - 11900m, "BN-9"),
        ], lineas: [v.Linea(v.P1, 2m), v.Linea(v.P3, 1m)]);

        var carga = Carga(v, venta.PublicId, VentaFacturadaV1.Type);
        var totales = carga.GetProperty("totals");
        var lineas = carga.GetProperty("lines");
        var impuestos = carga.GetProperty("taxes");
        var pagos = carga.GetProperty("payments");

        Suma(lineas, "grossAmount").Should().Be(totales.GetProperty("subtotal").GetDecimal());
        Suma(lineas, "discountAmount").Should().Be(totales.GetProperty("discountTotal").GetDecimal());
        Suma(impuestos, "amount", t => Es(t, "treatment", TaxTreatment.Generated)).Should().Be(totales.GetProperty("taxTotal").GetDecimal()).And.Be(1900m);
        Suma(impuestos, "amount", t => Es(t, "treatment", TaxTreatment.WithholdingSuffered)).Should().Be(totales.GetProperty("withholdingTotal").GetDecimal());
        Suma(pagos, "amount").Should().Be(totales.GetProperty("amountDue").GetDecimal()).And.Be(15900m);
        pagos.GetArrayLength().Should().Be(3);
        var porMedio = pagos.EnumerateArray().ToDictionary(p => p.GetProperty("paymentMeansCode").GetString()!);
        porMedio["VISA"].GetProperty("thirdPartyPersonPublicId").GetGuid().Should().Be(v.Adquirente.PublicId, "la tarjeta va contra el adquirente");
        porMedio["EFECTIVO"].GetProperty("thirdPartyPersonPublicId").ValueKind.Should().Be(JsonValueKind.Null);
        porMedio["BONO"].GetProperty("thirdPartyPersonPublicId").ValueKind.Should().Be(JsonValueKind.Null);
        porMedio.Values.Should().OnlyContain(p => Es(p, "direction", PaymentDirection.Received));
        Regex.IsMatch(v.ContenidoDe(venta.PublicId, VentaFacturadaV1.Type), @"\d{13,19}").Should().BeFalse("nunca viaja el número de la tarjeta");
    }

    [Fact]
    public async Task CostoDeVentaReconocido_trae_solo_la_salida_al_costo_sin_base_ni_impuestos()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P1, 3m)]);

        var carga = Carga(v, venta.PublicId, CostoDeVentaReconocidoV1.Type);

        carga.TryGetProperty("taxes", out _).Should().BeFalse();
        var linea = carga.GetProperty("lines").EnumerateArray().Single();
        Es(linea, "movement", KardexEntryKind.Exit).Should().BeTrue();
        (linea.GetProperty("quantityBase").GetDecimal(), linea.GetProperty("cost").GetDecimal()).Should().Be((3m, 3000m));
        linea.TryGetProperty("grossAmount", out _).Should().BeFalse();
    }

    [Fact]
    public async Task La_nota_con_devolucion_emite_NotaCreditoEmitida_y_DevolucionRegistrada_con_reintegros_Refunded()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P1, 2m)]);
        var linea = venta.Lines.Single(l => !l.IsDeleted);

        var nota = await v.NotaAsync(new CreditNoteDraftInput(venta.PublicId, "Devuelve", false, true, [new CreditNoteLineInput(linea.PublicId, 1m)], []));
        var r = await v.ConfirmarAsync(nota.Value.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        v.MensajesDe(nota.Value.PublicId).Should().Equal(NotaCreditoEmitidaV1.Type, DevolucionRegistradaV1.Type);
        var credito = Carga(v, nota.Value.PublicId, NotaCreditoEmitidaV1.Type);
        credito.GetProperty("withReturn").GetBoolean().Should().BeTrue();
        credito.GetProperty("payments").EnumerateArray().Should().OnlyContain(p => Es(p, "direction", PaymentDirection.Refunded));
        Suma(credito.GetProperty("payments"), "amount").Should().Be(credito.GetProperty("totals").GetProperty("amountDue").GetDecimal()).And.Be(2000m);
        var devolucion = Carga(v, nota.Value.PublicId, DevolucionRegistradaV1.Type);
        devolucion.GetProperty("operation").GetString().Should().Be("DevolucionDeCliente");
        Es(devolucion.GetProperty("lines").EnumerateArray().Single(), "movement", KardexEntryKind.Entry).Should().BeTrue();
    }

    [Fact]
    public async Task originEventKey_es_Confirmation_y_la_venta_del_punto_lleva_su_codigo_y_su_sesion()
    {
        var v = await VentasDePrueba.CrearAsync();
        var borrador = await v.GuardarAsync(v.Venta(pagos: [v.Pago(v.Efectivo, 2000m)], lineas: [v.Linea(v.P1, 1m)]));
        var documento = v.Documento(borrador.Value.PublicId);
        documento.PointOfSaleId = v.Punto.Id;
        documento.CashRegisterId = v.Caja.Id;
        documento.CashSessionId = v.Sesion.Id;
        await v.Db.SaveChangesAsync();

        var r = await v.ConfirmarAsync(documento.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        v.Db.IntegrationMessages.Where(m => m.OriginPublicId == documento.PublicId).Select(m => m.OriginEventKey).ToList()
            .Should().NotBeEmpty().And.OnlyContain(k => k == ClavesDeEvento.Confirmacion);
        foreach (var tipo in new[] { VentaFacturadaV1.Type, CostoDeVentaReconocidoV1.Type })
        {
            var carga = Carga(v, documento.PublicId, tipo);
            carga.GetProperty("pointOfSaleCode").GetString().Should().Be("PV1");
            carga.GetProperty("cashSessionPublicId").GetGuid().Should().Be(v.Sesion.PublicId);
        }
        Carga(v, documento.PublicId, VentaFacturadaV1.Type).GetProperty("cashRegisterCode").GetString().Should().Be("CJ1");
    }

    [Fact]
    public async Task MovimientoDeCajaRegistrado_lleva_el_destino_y_en_Register_la_sesion_que_recibe()
    {
        var v = await VentasDePrueba.CrearAsync();
        var otraCaja = new CashRegister { PointOfSaleId = v.Punto.Id, Code = "CJ2", Name = "Caja 2", WarehouseId = v.K.Principal.Id, ReceiptWidthMm = 80 };
        v.Db.CashRegisters.Add(otraCaja);
        await v.Db.SaveChangesAsync();
        var recibe = new CashSession
        {
            CashRegisterId = otraCaja.Id, PointOfSaleId = v.Punto.Id, CashierUserId = 21, CashierName = "Cajero Dos", OperatingDate = Catalog.CatalogoDePrueba.Hoy,
            OpenedAt = new DateTime(2026, 9, 25, 13, 0, 0, DateTimeKind.Utc), LastActivityAt = new DateTime(2026, 9, 25, 13, 0, 0, DateTimeKind.Utc),
        };
        v.Db.CashSessions.Add(recibe);
        await v.Db.SaveChangesAsync();
        var documento = new InventoryDocument { Class = DocumentClass.CashMovement, Reason = "Faltó sencillo en la caja 2", PointOfSaleId = v.Punto.Id };
        var detalle = new CashMovementDetail
        {
            CashSessionId = v.Sesion.Id, Kind = CashMovementKind.WithdrawalToRegister, SourcePaymentMeansId = v.Efectivo.Id,
            Destination = CashMovementDestination.Register, DestinationCashRegisterId = otraCaja.Id, DestinationCashSessionId = recibe.Id, Amount = 300_000m,
        };

        var m = await new EmisionDeInventario(v.Db).MovimientoDeCajaAsync(documento, detalle, default);

        (m.MovementKind, m.PaymentMeansCode, m.Destination, m.Amount).Should().Be((CashMovementKind.WithdrawalToRegister, "EFECTIVO", CashMovementDestination.Register, 300_000m));
        (m.PointOfSaleCode, m.CashRegisterCode, m.CashSessionPublicId).Should().Be(("PV1", "CJ1", v.Sesion.PublicId));
        (m.DestinationCashRegisterCode, m.DestinationCashSessionPublicId).Should().Be(("CJ2", recibe.PublicId));
        m.Reason.Should().Be("Faltó sencillo en la caja 2");
    }

    [Fact]
    public async Task DiferenciaDeArqueoAprobada_una_linea_por_medio_tambien_la_de_tolerancia_y_el_cajero_con_persona()
    {
        var v = await VentasDePrueba.CrearAsync();
        var cajera = new Person { PersonType = "01", TaxId = "1110000", FirstName = "Cajera", LastName = "Uno" };
        v.Db.People.Add(cajera);
        await v.Db.SaveChangesAsync();
        var conteo = new CashCount { CashSessionId = v.Sesion.Id, CountedAt = DateTime.UtcNow, CountedByUserId = KardexDePrueba.Usuario };
        var efectivo = new CashCountLine
        {
            CashCount = conteo, PaymentMeansId = v.Efectivo.Id, CountMethod = CashCountMethod.PhysicalCount, ExpectedAmount = 500_000m, CountedAmount = 480_000m,
            DifferenceAmount = -20_000m, ToleranceAmount = 1_000m, WithinTolerance = false, Treatment = CashDifferenceTreatment.ShortageToCashier, Reason = "Faltante",
        };
        var tarjeta = new CashCountLine
        {
            CashCount = conteo, PaymentMeansId = v.Tarjeta.Id, CountMethod = CashCountMethod.VoucherTotal, ExpectedAmount = 100_000m, CountedAmount = 100_050m,
            DifferenceAmount = 50m, ToleranceAmount = 100m, WithinTolerance = true, Treatment = CashDifferenceTreatment.Surplus, Reason = "Redondeo del datáfono",
        };
        conteo.Lines.Add(efectivo);
        conteo.Lines.Add(tarjeta);
        v.Db.CashCounts.Add(conteo);
        await v.Db.SaveChangesAsync();
        var documento = new InventoryDocument { Class = DocumentClass.CashCountDifference, CashSessionId = v.Sesion.Id };
        IReadOnlyList<CashDocumentLine> Lineas(int? persona) =>
        [
            new() { LineNumber = 1, CashCountLineId = efectivo.Id, PaymentMeansId = v.Efectivo.Id, Sign = -1, Amount = 20_000m,
                Treatment = CashDifferenceTreatment.ShortageToCashier, CashierUserId = KardexDePrueba.Usuario, CashierPersonId = persona, Reason = "Faltante" },
            new() { LineNumber = 2, CashCountLineId = tarjeta.Id, PaymentMeansId = v.Tarjeta.Id, Sign = 1, Amount = 50m, WithinTolerance = true,
                Treatment = CashDifferenceTreatment.Surplus, CashierUserId = KardexDePrueba.Usuario, Reason = "Redondeo del datáfono" },
        ];
        var aprobacion = new ApprovalRefV1
        {
            ApprovalRequestPublicId = Guid.NewGuid(), ApprovedBy = new UserRefV1 { Name = "Supervisora" }, Level = 1, Method = ApprovalMethod.OwnSession,
            Reason = "Revisado", DecidedAt = DateTimeOffset.UtcNow,
        };

        var m = await new EmisionDeInventario(v.Db).DiferenciaDeArqueoAsync(documento, Lineas(cajera.Id), aprobacion, Guid.NewGuid(), default);
        var sinPersona = () => new EmisionDeInventario(v.Db).DiferenciaDeArqueoAsync(documento, Lineas(null), aprobacion, null, default);

        m.Lines.Should().HaveCount(2);
        m.Lines.Should().ContainSingle(l => l.WithinTolerance && l.PaymentMeansCode == "VISA" && l.Difference == 50m && l.Treatment == CashDifferenceTreatment.Surplus);
        m.Lines.Should().ContainSingle(l => !l.WithinTolerance && l.Difference == -20_000m && l.Treatment == CashDifferenceTreatment.ShortageToCashier);
        m.Cashier.PersonPublicId.Should().Be(cajera.PublicId);
        (m.PointOfSaleCode, m.CashRegisterCode, m.CashSessionPublicId).Should().Be(("PV1", "CJ1", v.Sesion.PublicId));
        await sinPersona.Should().ThrowAsync<InvalidOperationException>("un faltante a cargo del cajero exige su persona (T50)");
    }
}
