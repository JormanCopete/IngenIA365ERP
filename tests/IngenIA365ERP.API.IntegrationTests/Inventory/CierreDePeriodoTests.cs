using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Security;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T278 (nombre fijo, decisiones-transversales §2.18; quickstart §3.11; US3-3 a US3-5, US17-1; FR-047), en el motor de
/// <c>DB_PROVIDER</c>, sobre el escenario aislado «cierre» (corte en el mes antepasado, así que se cierran el mes del corte y el
/// anterior al actual): el cierre avisa de los borradores y sólo cierra con <c>acknowledgeWarnings</c>; confirmar un documento
/// fechado en el mes cerrado responde <c>Inventory.Period.Closed</c> nombrando el período; el valorizado al fin del mes coincide
/// con el que fijó el cierre; reabrir exige el permiso especial (404 sin él), sólo el último cerrado
/// (<c>Inventory.Period.NotLastClosed</c>) y emite <c>PeriodoInventarioReabierto</c> en <c>COR_IntegrationMessages</c>.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CierreDePeriodoTests(CentralIdentityApiFixture fx)
{
    private const string Periodos = "/api/inventory/periods";

    [Fact]
    public async Task Cerrar_avisar_rechazar_lo_fechado_en_el_mes_y_reabrir_el_ultimo()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "cierre");
        var usuarios = await UsuariosDelEnsayo.CrearAsync(fx, esc);
        using var http = fx.CreateClient();
        var hoy = InventarioE2E.HoyEnColombia;
        var mesAnterior = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);
        var mesDelCorte = mesAnterior.AddMonths(-1);
        var finDelMesAnterior = mesAnterior.AddMonths(1).AddDays(-1);

        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P1", 10, 1_000m)], fecha: mesAnterior.AddDays(9));
        var borrador = await esc.AjusteAsync(http, esc.Admin, "AJP", "PRIN", [new("P2", 4, 500m)], fecha: mesAnterior.AddDays(10));

        // El mes del corte se cierra primero (en orden, FR-047).
        var fueraDeOrden = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Periodos}/{mesAnterior.Year}/{mesAnterior.Month}/close",
            new { acknowledgeWarnings = true });
        (await InventarioE2E.FallaAsync(fueraDeOrden, "Inventory.Period.NotNext")).GetProperty("data").GetProperty("nextToClose").ToString()
            .Should().Contain(mesDelCorte.Year.ToString());
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"{Periodos}/{mesDelCorte.Year}/{mesDelCorte.Month}/close", new { acknowledgeWarnings = true });

        // El mes anterior tiene un borrador: avisa y sólo cierra reconociéndolo.
        var previa = await InventarioE2E.GetAsync(http, esc.Admin, $"{Periodos}/{mesAnterior.Year}/{mesAnterior.Month}/close");
        previa.GetProperty("warnings").GetProperty("drafts").GetInt32().Should().BeGreaterThan(0);
        var sinReconocer = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Periodos}/{mesAnterior.Year}/{mesAnterior.Month}/close", new { });
        await InventarioE2E.FallaAsync(sinReconocer, "Inventory.Period.WarningsNotAcknowledged");
        var cierre = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"{Periodos}/{mesAnterior.Year}/{mesAnterior.Month}/close",
            new { acknowledgeWarnings = true });
        cierre.GetProperty("total").GetDecimal().Should().Be(10_000m);

        // Lo fechado en el mes cerrado ya no se confirma.
        var tarde = await InventarioE2E.PedirConfirmarAsync(http, esc.Admin, "/api/inventory/adjustments", borrador);
        var datos = (await InventarioE2E.FallaAsync(tarde, "Inventory.Period.Closed")).GetProperty("data");
        datos.GetProperty("year").GetInt32().Should().Be(mesAnterior.Year);
        datos.GetProperty("month").GetInt32().Should().Be(mesAnterior.Month);
        datos.GetProperty("lastClosedDate").GetString().Should().Be(finDelMesAnterior.ToString("yyyy-MM-dd"));

        // El valorizado a la fecha de corte pasada coincide con el fijado (US17-1).
        var valorizado = await InventarioE2E.InformeAsync(http, esc.Admin, "valuation", $"asOf={finDelMesAnterior:yyyy-MM-dd}");
        valorizado.Totales.Should().NotBeNull();
        valorizado.Numero(valorizado.Totales!.Value, "Valor").Should().Be(cierre.GetProperty("total").GetDecimal());

        // Reabrir: sólo el último cerrado, con el permiso especial y motivo.
        var anterior = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Periodos}/{mesDelCorte.Year}/{mesDelCorte.Month}/reopen",
            new { reason = "Corregir el corte" });
        await InventarioE2E.FallaAsync(anterior, "Inventory.Period.NotLastClosed");
        var sinPermiso = await InventarioE2E.MandarAsync(http, usuarios.Aprobador.Token, HttpMethod.Post, $"{Periodos}/{mesAnterior.Year}/{mesAnterior.Month}/reopen",
            new { reason = "No puedo" });
        sinPermiso.StatusCode.Should().Be(HttpStatusCode.NotFound, "sin Inventory.Periods.Reopen es indistinguible de inexistente");

        var reabierto = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"{Periodos}/{mesAnterior.Year}/{mesAnterior.Month}/reopen",
            new { reason = "Falta un ajuste del mes" });
        reabierto.GetProperty("messagePublicId").GetGuid().Should().NotBeEmpty();
        var mensajes = await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop,
            """SELECT COUNT(*) FROM dbo."COR_IntegrationMessages" WHERE "Type" = 'PeriodoInventarioReabierto'""",
            "SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessages] WHERE [Type] = 'PeriodoInventarioReabierto'");
        Convert.ToInt32(mensajes).Should().Be(1);

        // Reabierto, el borrador ya se confirma.
        await InventarioE2E.ConfirmarAsync(http, esc.Admin, "/api/inventory/adjustments", borrador);
    }

    [Fact(Skip = "La foto de un conteo se toma al abrirlo (hoy) y el mes en curso no se cierra (Inventory.Period.NotEnded): por HTTP no se "
        + "llega a un conteo abierto con foto en un mes cerrable. El bloqueo Inventory.Period.OpenCounts lo prueba "
        + "InventoryPeriodCommandsTests (Application) con la foto fechada a mano.")]
    public void Un_conteo_abierto_con_foto_en_el_mes_impide_cerrarlo()
    {
    }
}
