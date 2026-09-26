using System.Globalization;
using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T449 (T511; contracts/contabilidad.md §3.2 a §3.8): los casos dorados del constructor. Cada archivo de
/// <c>Casos/</c> trae el sobre, el contenido <c>…V1</c>, lo que agrega a la matriz del escenario y las líneas esperadas,
/// calculadas a mano. El comprobante que sale tiene que ser exactamente ése —cuenta, lado, importe, tercero, cruce, base,
/// sucursal, centro y líneas del documento— y además pasar las reglas 1 a 11 de la 009 (<c>AccountingPoster.ValidarAsync</c>):
/// lo que el constructor arma, el contrato lo contabiliza.
/// </summary>
public class ConstructorDeLineasDeInventarioTests
{
    public static IEnumerable<object[]> Casos() => EscenarioContable.NombresDeCasos();

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task El_caso_dorado_da_el_comprobante_esperado(string nombre)
    {
        var e = new EscenarioContable();
        var caso = EscenarioContable.Caso(nombre);
        if (caso["matrizExtra"] is JsonArray extra) e.AgregarReglas(extra);
        var unidad = EscenarioContable.Unidad(caso);

        var c = ConstructorDeLineasDeInventario.Construir(unidad, await e.CatalogosAsync(unidad));

        c.Fallos.Select(f => $"{f.Error.Code}: {f.Error.Message}").Should().BeEmpty(nombre);
        var esperado = caso["esperado"]!.AsObject();
        c.Tipo!.VoucherTypeCode.Should().Be((string)esperado["tipo"]!);
        c.Request!.VoucherTypeCode.Should().Be((string)esperado["tipo"]!);
        c.Request.Date.Should().Be(DateOnly.Parse((string)esperado["fecha"]!, CultureInfo.InvariantCulture));
        c.Request.Kind.Should().Be(DocumentKind.Regular, "anulaciones, notas y ajustes también son Regular (T29)");
        c.Request.Origin.Module.Should().Be(ModuloContable.Inventario);
        c.Request.Origin.SourceType.Should().Be((string)esperado["origen"]!);
        c.Request.RegistradoPor!.Name.Should().Be(unidad[0].Sobre.OriginUser.Name, "el usuario de origen queda como dato (FR-083)");
        c.Mapa.Should().HaveCount(c.Request.Lines.Count);

        var reales = c.Request.Lines.Select((l, i) => Real(e, l, c.Mapa[i])).ToList();
        var esperadas = esperado["lineas"]!.AsArray().Select(l => Esperada(l!.AsObject())).ToList();
        reales.Should().BeEquivalentTo(esperadas, nombre);
        c.Request.Lines.Sum(l => l.Debit).Should().Be(c.Request.Lines.Sum(l => l.Credit), "cuadra por construcción");

        var validacion = await e.D.Poster.ValidarAsync(c.Request, default);
        validacion.Errores.Select(x => $"línea {x.LineNumber} {x.AccountCode}: {x.Code} {x.Message}").Should().BeEmpty("el contrato de la 009 lo acepta");
    }

    [Fact]
    public async Task La_venta_de_mensajes_14_suma_267830_50_por_lado()
    {
        var e = new EscenarioContable();
        var unidad = EscenarioContable.Unidad(EscenarioContable.Caso("01-venta-pos-de-mensajes-14"));

        var c = ConstructorDeLineasDeInventario.Construir(unidad, await e.CatalogosAsync(unidad));

        c.Request!.Lines.Should().HaveCount(12);
        c.Request.Lines.Sum(l => l.Debit).Should().Be(267830.50m);
        c.Request.Description.Should().Be("Documento equivalente POS PV01-1532");
        c.Request.Origin.SourcePublicId.Should().Be(Guid.Parse("3f0c9d42-5a1b-4e7c-8d21-6b9f0e4a7c13"));
        c.Request.RegistradoPor!.Name.Should().Be("Cajero de ensayo");
    }

    [Fact]
    public async Task El_espejo_usa_la_regla_del_original_y_una_compra_nueva_la_vigente()
    {
        var e = new EscenarioContable();
        var caso = EscenarioContable.Caso("24-espejo-de-anulacion-con-regla-cambiada");
        e.AgregarReglas(caso["matrizExtra"]!.AsArray());
        var anulacion = EscenarioContable.Unidad(caso);
        var nueva = EscenarioContable.Compra(1000m, "LACTEOS", fecha: new DateOnly(2026, 3, 15));

        var catalogos = await e.CatalogosAsync(anulacion, nueva);
        var espejo = ConstructorDeLineasDeInventario.Construir(anulacion, catalogos);
        var compra = ConstructorDeLineasDeInventario.Construir(nueva, catalogos);

        espejo.Request!.Lines.Should().Contain(l => l.AccountCode == "14350505" && l.Credit == 1000m, "la regla del 10 de marzo, aunque hoy rija otra (G2)");
        compra.Request!.Lines.Should().Contain(l => l.AccountCode == "14350506" && l.Debit == 1000m);
        espejo.Request.Date.Should().Be(new DateOnly(2026, 3, 15), "la fecha del documento de anulación, nunca la del original");
    }

    [Fact]
    public async Task Sin_regla_dice_la_operacion_el_rol_y_las_lineas_del_documento()
    {
        var e = new EscenarioContable();
        var unidad = EscenarioContable.Compra(1000m, "CARNES");

        var c = ConstructorDeLineasDeInventario.Construir(unidad, await e.CatalogosAsync(unidad));

        c.EsValida.Should().BeFalse();
        var fallo = c.Fallos.Single();
        fallo.Error.Code.Should().Be("Accounting.InventoryRule.Missing");
        fallo.DocumentLines.Should().Equal(1);
        fallo.MessageType.Should().Be(CompraRecibidaV1.Type);
    }

    [Fact]
    public async Task Un_contenido_que_no_cuadra_es_defecto_del_emisor()
    {
        var e = new EscenarioContable();
        var caso = EscenarioContable.Caso("06-nota-debito-un-impuesto-por-renglon");
        caso["mensajes"]![0]!["payload"]!["totals"]!["taxTotal"] = 3700.00m;
        var unidad = EscenarioContable.Unidad(caso);

        var c = ConstructorDeLineasDeInventario.Construir(unidad, await e.CatalogosAsync(unidad));

        c.Fallos.Select(f => f.Error.Code).Should().Contain("Accounting.InventoryMessage.Unbalanced");
    }

    [Fact]
    public async Task La_tarifa_distinta_de_la_cuenta_se_distingue_de_la_de_la_regla()
    {
        var e = new EscenarioContable();
        // La cuenta del IVA 19 % cambia a 18 % desde marzo; la regla sigue diciendo 19 % (C8).
        e.Cuenta("24080502").TaxRates.Add(new Domain.Entities.Accounting.AccountTaxRate { ValidFrom = new DateOnly(2026, 3, 1), Rate = 0.18m, CreatedBy = "test" });
        e.D.Db.SaveChanges();
        var caso = EscenarioContable.Caso("06-nota-debito-un-impuesto-por-renglon");
        var unidad = EscenarioContable.Unidad(caso);

        var c = ConstructorDeLineasDeInventario.Construir(unidad, await e.CatalogosAsync(unidad));

        c.Fallos.Should().NotBeEmpty();
        c.Fallos.Should().OnlyContain(f => f.Error.Code == "Accounting.InventoryRule.TaxRateMismatch" && f.DeLaCuenta);
    }

    [Fact]
    public void El_principal_es_el_comercial_y_la_fecha_del_ajuste_es_la_efectiva()
    {
        var unidad = EscenarioContable.Unidad(EscenarioContable.Caso("05-nota-credito-con-devolucion-cruza-con-la-venta"));
        ConstructorDeLineasDeInventario.Principal(unidad.Reverse().ToList())!.Tipo.Should().Be(NotaCreditoEmitidaV1.Type);

        var ajuste = EscenarioContable.Unidad(EscenarioContable.Caso("15-ajuste-de-costo-diferencia-de-precio"));
        ConstructorDeLineasDeInventario.FechaDelComprobante(ajuste[0]).Should().Be(new DateOnly(2026, 3, 12));
    }

    // -------------------------------------------------------------------------------------------------------------

    private static string Real(EscenarioContable e, PostingLine l, LineaDeLaUnidad mapa) => Linea(
        l.AccountCode!, l.Debit, l.Credit,
        l.PersonId is { } p ? e.PersonaPorId[p].ToString("D") : null,
        l.CrossDocumentType is null ? null : $"{l.CrossDocumentType} {l.CrossDocumentNumber}",
        l.TaxBase,
        l.BranchId == e.D.Norte.Id ? "norte" : "principal",
        l.CostCenterId is not null,
        mapa.DocumentLines);

    private static string Esperada(JsonObject x) => Linea(
        (string)x["cuenta"]!, (decimal)x["debito"]!, (decimal)x["credito"]!, (string?)x["tercero"], (string?)x["cruce"], (decimal?)x["base"],
        (string?)x["sucursal"] ?? "principal", (bool?)x["centro"] ?? false,
        x["lineas"] is JsonArray lineas ? lineas.Select(n => (int)n!).ToList() : []);

    private static string Linea(string cuenta, decimal d, decimal c, string? tercero, string? cruce, decimal? @base, string sucursal, bool centro, IReadOnlyList<int> lineas) =>
        string.Join(" | ", cuenta, d.ToString("0.00", CultureInfo.InvariantCulture), c.ToString("0.00", CultureInfo.InvariantCulture), tercero ?? "-",
            cruce ?? "-", @base?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-", sucursal, centro ? "centro" : "-", string.Join(',', lineas));

    /// <summary>
    /// La anulación armada en memoria (la validación previa, antes de guardar) trae cada contenido anulado como <c>JsonNode</c>
    /// —<c>EmisionDeInventario.Invertido</c>—, no como el <c>JsonElement</c> que deja leerlo de la base. Los dos se leen igual:
    /// hasta las e2e de I2 la validación previa de toda anulación respondía «no existe el tipo de comprobante del mensaje
    /// DocumentoAnulado» y no se podía anular nada que pasara a Contabilidad.
    /// </summary>
    [Fact]
    public void El_contenido_anulado_en_memoria_se_lee_como_el_leido_de_la_base()
    {
        var nodo = JsonNode.Parse("""{"operation":"AjustePositivo","lines":[]}""");

        Application.Accounting.Inventory.Reglas.TiposDeComprobanteDeInventario.OperacionDelContenido(nodo).Should().Be("AjustePositivo");
        ConstructorDeLineasDeInventario.ContenidoTipado(AjusteInventarioAprobadoV1.Type, 1, nodo)
            .Should().BeOfType<AjusteInventarioAprobadoV1>().Which.Operation.Should().Be("AjustePositivo");
    }
}
