using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T826 (feature 012, I5; quickstart §7; US16-1 a US16-4; FR-002, FR-043, FR-045; api.md §7, §9.3, §27; SC-006, SC-007), en el motor de
/// <c>DB_PROVIDER</c>, cada caso en su cooperativa aislada del mismo host porque cambia todo el libro de costos:
/// <list type="bullet">
/// <item>«costeoretro» (con la contabilidad iniciada, I2): una entrada fechada antes de tres salidas confirmadas se rechaza con el
/// parámetro apagado nombrando el movimiento posterior; con <c>Costeo.RetroactivosPermitidos</c> y 30 días, <c>cost-impact</c> muestra
/// las tres salidas y confirmar escribe lo mismo —líneas <c>Retroactive</c> fechadas en cada salida— con un
/// <c>AjusteDeCostoReconocido</c> por salida, que llega a Contabilidad con su motivo y su fecha;</item>
/// <item>«costeocerrado»: con el parámetro encendido, un documento fechado en un período cerrado responde <c>Inventory.Period.Closed</c>;</item>
/// <item>«costeopeps»: <c>Costeo.Metodo = Peps</c> el primer día del mes con acta, dos capas de 10 a $1.000 y a $1.300 y una salida de 15
/// por $16.500, el valorizado por los dos métodos a la fecha del cambio, un retroactivo rechazado por D6 y la verificación de
/// integridad sin diferencias con capas.</item>
/// </list>
/// (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CosteoAvanzadoTests(CentralIdentityApiFixture fx)
{
    private const string Ajustes = "/api/inventory/adjustments";

    [Fact]
    public async Task Un_retroactivo_se_rechaza_apagado_y_encendido_muestra_su_impacto_y_ajusta_cada_salida_en_su_fecha()
    {
        var esc = await ContabilidadDeInventarioE2E.PrepararAsync(fx, "costeoretro");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var hoy = InventarioE2E.HoyEnColombia;
        DateOnly Dia(int atras) => hoy.AddDays(-atras);

        // La matriz del ajuste de costo: lo vendido va al costo de venta (rol Costo) y el retroactivo no tiene contrapartida puente.
        var inicioDelEjercicio = new DateOnly(hoy.Year, 1, 1);
        await ContabilidadE2E.CrearCuentaAsync(http, t, Ventas.ContabilidadDeVentasE2E.CostoDeVentas, "Costo de la mercancía vendida", "613520", ["CNT", "INV"]);
        foreach (var grupo in new[] { "ASEO", "ABARROTES" })
            await ContabilidadDeInventarioE2E.ReglaAsync(http, t, "AjusteDeCosto", "Costo", Ventas.ContabilidadDeVentasE2E.CostoDeVentas, inicioDelEjercicio,
                new { accountingGroupCode = grupo });
        await ContabilidadDeInventarioE2E.ReglaAsync(http, t, "AjusteDeCosto", "Contrapartida", Ventas.ContabilidadDeVentasE2E.CostoDeVentas, inicioDelEjercicio,
            new { reasonCode = "Retroactive" });

        await esc.AjusteConfirmadoAsync(http, t, "AJP", "PRIN", [new("P1", 10, 1_000m)], fecha: Dia(8));
        var salidas = new List<Guid>();
        foreach (var atras in new[] { 6, 5, 4 })
            salidas.Add((await esc.AjusteConfirmadoAsync(http, t, "AJN", "PRIN", [new("P1", 2)], fecha: Dia(atras))).GetProperty("publicId").GetGuid());

        // Apagado (el defecto): se rechaza nombrando el primer movimiento posterior (US16-3).
        var retro = await esc.AjusteAsync(http, t, "AJP", "PRIN", [new("P1", 10, 1_600m)], fecha: Dia(7));
        var apagado = await InventarioE2E.PedirConfirmarAsync(http, t, Ajustes, retro);
        var datos = (await InventarioE2E.FallaAsync(apagado, "Inventory.Costing.RetroactiveNotAllowed")).GetProperty("data");
        datos.GetProperty("productCode").GetString().Should().Be("P1");
        datos.GetProperty("laterMovement").GetProperty("documentPublicId").GetGuid().Should().Be(salidas[0]);

        // Encendido, con 30 días hacia atrás.
        var desde = esc.Corte.AddDays(1).ToString("yyyy-MM-dd");
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/parameters/INV/Costeo.RetroactivosPermitidos/versions",
            new { scopeKind = "None", value = "true", validFrom = desde, reason = "Retroactivos del ensayo" });
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/parameters/INV/Costeo.RetroactivosDiasMaximos/versions",
            new { scopeKind = "None", value = "30", validFrom = desde, reason = "Retroactivos del ensayo" });

        var impacto = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/inventory/documents/{retro}/cost-impact");
        impacto.GetProperty("retroactive").GetBoolean().Should().BeTrue();
        var afectados = impacto.GetProperty("affected").EnumerateArray().ToList();
        afectados.Select(a => a.GetProperty("documentPublicId").GetGuid()).Should().BeEquivalentTo(salidas, "las tres salidas posteriores");
        afectados.Should().OnlyContain(a => Math.Abs(a.GetProperty("soldAmount").GetDecimal()) == 600m, "2 unidades a 1.300 en vez de 1.000");
        (await InventarioE2E.GetAsync(http, t, $"{Ajustes}/{retro}")).GetProperty("status").GetInt32().Should().Be(0, "cost-impact no guarda nada");

        var mensajesAntes = await EnteroAsync(esc, MensajesDeAjuste("Retroactive"));
        await InventarioE2E.ConfirmarAsync(http, t, Ajustes, retro);

        var kardex = await InventarioE2E.InformeAsync(http, t, "kardex", $"product={esc.P("P1").Id}&warehouse={esc.Bodega("PRIN")}&from={Dia(9):yyyy-MM-dd}&to={hoy:yyyy-MM-dd}");
        var retroactivas = kardex.Filas.Where(f => kardex.Texto(f, "Motivo") == "Ajuste de costo: retroactivo").ToList();
        retroactivas.Should().HaveCount(3, "una línea por salida afectada");
        retroactivas.Select(f => DateOnly.Parse(kardex.Texto(f, "Fecha de operación"), System.Globalization.CultureInfo.InvariantCulture))
            .Should().BeEquivalentTo(new[] { Dia(6), Dia(5), Dia(4) }, "fechadas en cada salida (T18)");
        kardex.Filas.Where(f => kardex.Numero(f, "Salida") == 2m).Should().OnlyContain(f => kardex.Numero(f, "Costo unitario") == 1_000m,
            "las salidas originales no se reescriben");
        kardex.Numero(kardex.Filas[^1], "Saldo (cantidad)").Should().Be(14m);
        kardex.Numero(kardex.Filas[^1], "Saldo (valor)").Should().Be(18_200m, "14 × 1.300");
        (await EnteroAsync(esc, MensajesDeAjuste("Retroactive")) - mensajesAntes).Should().Be(3, "un AjusteDeCostoReconocido por salida afectada");
        foreach (var atras in new[] { 6, 5, 4 })
            (await EnteroAsync(esc, MensajesDeAjuste("Retroactive", Dia(atras).ToString("yyyy-MM-dd")))).Should().Be(1, $"el de la salida del {Dia(atras)}");

        // I2: cada ajuste llega a Contabilidad (el defecto con contabilidad es en línea).
        await fx.Despachador.PasadasAsync(esc.Coop.TenantPublicId, 3);
        (await EnteroAsync(esc,
            ("""SELECT COUNT(*) FROM dbo."COR_IntegrationMessageDeliveries" d JOIN dbo."COR_IntegrationMessages" m ON m."Id" = d."MessageId" WHERE m."Type" = 'AjusteDeCostoReconocido' AND m."PayloadJson" LIKE '%Retroactive%' AND d."Destination" = 'Accounting' AND d."Status" = 2""",
             "SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessageDeliveries] d JOIN [dbo].[COR_IntegrationMessages] m ON m.[Id] = d.[MessageId] WHERE m.[Type] = 'AjusteDeCostoReconocido' AND m.[PayloadJson] LIKE '%Retroactive%' AND d.[Destination] = 'Accounting' AND d.[Status] = 2")))
            .Should().Be(3, $"los tres retroactivos contabilizados: {await EntregasAsync(esc)}");
    }

    [Fact]
    public async Task Con_retroactivos_encendidos_un_documento_en_un_periodo_cerrado_se_rechaza()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "costeocerrado");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var hoy = InventarioE2E.HoyEnColombia;
        var mesAnterior = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);
        var mesDelCorte = mesAnterior.AddMonths(-1);
        var desde = esc.Corte.AddDays(1).ToString("yyyy-MM-dd");
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/parameters/INV/Costeo.RetroactivosPermitidos/versions",
            new { scopeKind = "None", value = "true", validFrom = desde, reason = "Retroactivos del ensayo" });
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/parameters/INV/Costeo.RetroactivosDiasMaximos/versions",
            new { scopeKind = "None", value = "90", validFrom = desde, reason = "Retroactivos del ensayo" });

        await esc.AjusteConfirmadoAsync(http, t, "AJP", "PRIN", [new("P1", 10, 1_000m)], fecha: mesAnterior.AddDays(9));
        await esc.AjusteConfirmadoAsync(http, t, "AJN", "PRIN", [new("P1", 2)], fecha: mesAnterior.AddDays(19));
        var retro = await esc.AjusteAsync(http, t, "AJP", "PRIN", [new("P1", 5, 1_600m)], fecha: mesAnterior.AddDays(14));

        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/inventory/periods/{mesDelCorte.Year}/{mesDelCorte.Month}/close", new { acknowledgeWarnings = true });
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/inventory/periods/{mesAnterior.Year}/{mesAnterior.Month}/close", new { acknowledgeWarnings = true });

        var cerrado = await InventarioE2E.PedirConfirmarAsync(http, t, Ajustes, retro);
        (await InventarioE2E.FallaAsync(cerrado, "Inventory.Period.Closed")).GetProperty("data").GetProperty("month").GetInt32().Should().Be(mesAnterior.Month);
    }

    [Fact]
    public async Task Con_PEPS_dos_capas_y_una_salida_de_15_cuestan_16500_y_el_valorizado_muestra_los_dos_metodos()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "costeopeps");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var hoy = InventarioE2E.HoyEnColombia;
        var inicioDelMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var antes = esc.Corte.AddDays(5);

        // La historia por promedio ponderado, antes del cambio: 10 a 1.000, 10 a 1.300 y una salida de 5 (queda 15 a 1.150).
        await esc.AjusteConfirmadoAsync(http, t, "AJP", "PRIN", [new("P1", 10, 1_000m)], fecha: antes);
        await esc.AjusteConfirmadoAsync(http, t, "AJP", "PRIN", [new("P1", 10, 1_300m)], fecha: antes.AddDays(1));
        await esc.AjusteConfirmadoAsync(http, t, "AJN", "PRIN", [new("P1", 5)], fecha: antes.AddDays(2));

        // A mitad de período no se admite; el primer día del mes, con justificación y acta, sí (FR-043).
        var aMitad = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/inventory/parameters/INV/Costeo.Metodo/versions", new
        {
            scopeKind = "None", value = "Peps", validFrom = antes.AddDays(1).ToString("yyyy-MM-dd"), reason = "Cambio a PEPS", legalSource = "Acta 7 del consejo",
        });
        await InventarioE2E.FallaAsync(aMitad, "Parameters.RequiresPeriodStart");
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/parameters/INV/Costeo.Metodo/versions", new
        {
            scopeKind = "None", value = "Peps", validFrom = inicioDelMes.ToString("yyyy-MM-dd"), reason = "La cooperativa adopta PEPS",
            legalSource = "Acta 7 del consejo de administración",
        });

        // US16-1: dos capas y una salida de 15 en un producto sin historia.
        await esc.AjusteConfirmadoAsync(http, t, "AJP", "PRIN", [new("P2", 10, 1_000m)]);
        await esc.AjusteConfirmadoAsync(http, t, "AJP", "PRIN", [new("P2", 10, 1_300m)]);
        await esc.AjusteConfirmadoAsync(http, t, "AJN", "PRIN", [new("P2", 15)]);
        var dia = hoy.ToString("yyyy-MM-dd");
        var kardex = await InventarioE2E.InformeAsync(http, t, "kardex", $"product={esc.P("P2").Id}&warehouse={esc.Bodega("PRIN")}&from={dia}&to={dia}");
        var salida = kardex.Filas.Single(f => kardex.Numero(f, "Salida") == 15m);
        kardex.Numero(salida, "Valor salida").Should().Be(16_500m, "10 × 1.000 + 5 × 1.300");
        kardex.Texto(salida, "Capas consumidas").Should().NotBeNullOrWhiteSpace();
        kardex.Numero(kardex.Filas[^1], "Saldo (valor)").Should().Be(6_500m, "queda la capa de 5 a 1.300");

        // D6: con PEPS no hay retroactivo.
        var retro = await esc.AjusteAsync(http, t, "AJP", "PRIN", [new("P2", 1, 900m)], fecha: hoy.AddDays(-1) >= inicioDelMes ? hoy.AddDays(-1) : inicioDelMes);
        var d6 = await InventarioE2E.PedirConfirmarAsync(http, t, Ajustes, retro);
        if (hoy > inicioDelMes)
            await InventarioE2E.FallaAsync(d6, "Inventory.Costing.RetroactiveRequiresWeightedAverage");

        // US16-4: el valorizado por los dos métodos a la fecha del cambio, por grupo contable.
        var valorizado = await InventarioE2E.InformeAsync(http, t, "method-change-valuation", $"asOf={inicioDelMes:yyyy-MM-dd}");
        var aseo = valorizado.Filas.Where(f => valorizado.Texto(f, "Grupo contable").Contains("Aseo", StringComparison.OrdinalIgnoreCase)
            && valorizado.Texto(f, "Fecha").StartsWith(inicioDelMes.ToString("yyyy-MM-dd"))).ToList();
        aseo.Should().ContainSingle($"una fila del grupo ASEO a la fecha del cambio: {valorizado.Raiz}");
        valorizado.Numero(aseo[0], "Valor promedio ponderado").Should().Be(17_250m, "15 × 1.150");
        valorizado.Numero(aseo[0], "Valor PEPS").Should().Be(18_000m, "5 × 1.000 + 10 × 1.300");

        // SC-006: el kardex y las proyecciones, capas incluidas, coinciden.
        var verificacion = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/integrity/verify",
            new { productPublicIds = new[] { esc.P("P1").Id, esc.P("P2").Id } });
        verificacion.GetProperty("incidents").GetArrayLength().Should().Be(0, verificacion.ToString());
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    private static (string, string) MensajesDeAjuste(string motivo, string? fecha = null)
    {
        var pg = $"""SELECT COUNT(*) FROM dbo."COR_IntegrationMessages" WHERE "Type" = 'AjusteDeCostoReconocido' AND "PayloadJson" LIKE '%{motivo}%'""";
        var ss = $"SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessages] WHERE [Type] = 'AjusteDeCostoReconocido' AND [PayloadJson] LIKE '%{motivo}%'";
        if (fecha is not null) { pg += $""" AND "PayloadJson" LIKE '%{fecha}%'"""; ss += $" AND [PayloadJson] LIKE '%{fecha}%'"; }
        return (pg, ss);
    }

    /// <summary>Las entregas a Contabilidad de los ajustes de costo, con su estado y su último error (para leer un fallo).</summary>
    private async Task<string?> EntregasAsync(EscenarioDeInventario esc) =>
        (await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop,
            """SELECT string_agg(d."Status" || ' ' || COALESCE(d."LastErrorCode", '') || ' ' || COALESCE(d."LastErrorMessage", ''), ' | ') FROM dbo."COR_IntegrationMessageDeliveries" d JOIN dbo."COR_IntegrationMessages" m ON m."Id" = d."MessageId" WHERE m."Type" = 'AjusteDeCostoReconocido' AND d."Destination" = 'Accounting'""",
            "SELECT STRING_AGG(CONCAT(d.[Status], ' ', d.[LastErrorCode], ' ', d.[LastErrorMessage]), ' | ') FROM [dbo].[COR_IntegrationMessageDeliveries] d JOIN [dbo].[COR_IntegrationMessages] m ON m.[Id] = d.[MessageId] WHERE m.[Type] = 'AjusteDeCostoReconocido' AND d.[Destination] = 'Accounting'"))?.ToString();

    private async Task<int> EnteroAsync(EscenarioDeInventario esc, (string Postgres, string SqlServer) sql) =>
        Convert.ToInt32(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop, sql.Postgres, sql.SqlServer));
}
