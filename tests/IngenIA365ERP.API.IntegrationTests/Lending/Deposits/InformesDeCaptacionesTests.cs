using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T224 (US17, escenario 1 de la spec y la prueba independiente; contracts/api.md §18; research §25): los informes y los reportes regulatorios
/// de captaciones por HTTP, sobre una cooperativa con captaciones activadas (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>, nombre
/// <c>cap_regulatorios</c>) con el ciclo diario y el cierre mensual corridos a mano (<see cref="CausacionE2E"/>, <see cref="CierreE2E"/>).
/// <list type="number">
/// <item>Antes del cierre del mes, validar el formato de captaciones responde <c>Lending.Regulatory.PeriodNotClosed</c>.</item>
/// <item>Cerrado el mes: validar no guarda nada y avisa «formato sin cotejar»; generar sin reconocer responde 422 con ese código; reconocido, 201 con la
/// versión 1 <c>Generated</c> y su <c>formatCode</c>; el total de capital del archivo cuadra con la vista <c>deposit-balances</c>; regenerar deja la anterior
/// <c>Superseded</c>.</item>
/// <item>La vista previa de los saldos diarios trae un día por fila y termina en el saldo del ahorro a la vista; la de cuentas por estado lista la cuenta;
/// el registro de vistas ofrece las siete de los formatos.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class InformesDeCaptacionesTests(CentralIdentityApiFixture fx)
{
    private const string Formatos = "/api/lending/regulatory/deposit-formats";
    private const string Archivos = "/api/lending/regulatory/files";
    private const string Vistas = "/api/reports/lending";

    [Fact]
    public async Task US17_1_formato_de_captaciones_del_mes_cerrado_sin_cotejar_cuadra_con_los_saldos_y_se_regenera()
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "regulatorios");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var tenant = coop.TenantPublicId;
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var hoy = CarteraE2E.Hoy(fx);
        var (anio, mes) = (hoy.Year, hoy.Month);
        var fin = new DateOnly(anio, mes, DateTime.DaysInMonth(anio, mes));

        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHREG");
        var titular = await MovimientosTests.AsociadoAsync(http, admin, "Regulatorios");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, linea, titular);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, titular, 6_000_000m, hoy, medio: "Transfer");

        // 1. Sin cierre del mes no hay formato.
        var sinCierre = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Formatos}/sicses-captaciones/validate", new { year = anio, month = mes });
        sinCierre.StatusCode.Should().NotBe(HttpStatusCode.OK, await sinCierre.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(sinCierre)).Should().Be("Lending.Regulatory.PeriodNotClosed");

        // 2. El ciclo del último día y el cierre del mes, con el reloj al día siguiente.
        using (CausacionE2E.RelojEn(fx, fin.AddDays(1)))
        {
            await CausacionE2E.CicloDeHoyAsync(fx, http, admin, tenant);
            var corrida = await CierreE2E.OrdenarAsync(http, admin, anio, mes);
            await CierreE2E.CorrerAsync(fx, http, admin, tenant, corrida);

            var validacion = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Formatos}/sicses-captaciones/validate", new { year = anio, month = mes });
            validacion.GetProperty("rows").GetInt32().Should().Be(1);
            validacion.GetProperty("blocking").GetInt32().Should().Be(0, validacion.ToString());
            validacion.GetProperty("warnings").EnumerateArray().Select(w => w.GetProperty("code").GetString())
                .Should().Contain("Lending.Regulatory.LayoutSinCotejar", "el formato sin cotejar se advierte (US17-1)");
            (await CarteraE2E.GetAsync(http, admin, $"{Archivos}?format=sicses-captaciones")).GetProperty("items").GetArrayLength()
                .Should().Be(0, "validar no guarda nada");

            var sinReconocer = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Formatos}/sicses-captaciones/generate",
                new { year = anio, month = mes, acknowledgeWarnings = false });
            sinReconocer.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await sinReconocer.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(sinReconocer)).Should().Be("Lending.Regulatory.LayoutSinCotejar");

            var generar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Formatos}/sicses-captaciones/generate",
                new { year = anio, month = mes, acknowledgeWarnings = true });
            generar.StatusCode.Should().Be(HttpStatusCode.Created, await generar.Content.ReadAsStringAsync());
            var detalle = await CarteraE2E.LeerAsync(generar);
            var archivo = detalle.GetProperty("file");
            archivo.GetProperty("formatCode").GetString().Should().Be("sicses-captaciones");
            archivo.GetProperty("version").GetInt32().Should().Be(1);
            archivo.GetProperty("sha256").GetString().Should().MatchRegex("^[0-9a-fA-F]{64}$");

            // El total del archivo cuadra con los saldos del libro al cierre del mes.
            var saldos = await CarteraE2E.GetAsync(http, admin, $"{Vistas}/deposit-balances?asOf={fin:yyyy-MM-dd}");
            var capital = saldos.GetProperty("totales").GetProperty("valores")[2].GetDecimal();
            detalle.GetProperty("totals").GetProperty("Totales.Capital").GetDecimal().Should().Be(capital);

            var otra = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Formatos}/sicses-captaciones/generate",
                new { year = anio, month = mes, acknowledgeWarnings = true });
            otra.StatusCode.Should().Be(HttpStatusCode.Created, await otra.Content.ReadAsStringAsync());
            var primera = await CarteraE2E.GetAsync(http, admin, $"{Archivos}/{archivo.GetProperty("publicId").GetGuid()}");
            Estado(primera.GetProperty("file")).Should().Be("Superseded", "regenerar deja la versión anterior reemplazada");

            // 3. La vista previa de los saldos diarios y las cuentas por estado.
            var diarios = await CarteraE2E.GetAsync(http, admin, $"{Vistas}/sicses-saldos-diarios?year={anio}&month={mes}");
            var filas = diarios.GetProperty("filas").EnumerateArray().ToList();
            filas.Should().HaveCount(DateTime.DaysInMonth(anio, mes));
            // El ciclo del último día liquida y abona el interés del mes con fecha valor del fin de mes: el capital al cierre es los 6.000.000 más lo abonado
            // (6.001.799 con la tasa de la línea), el mismo capital que da la vista de saldos.
            decimal.Parse(filas[^1].GetProperty("valores")[1].GetString()!, System.Globalization.CultureInfo.InvariantCulture).Should().Be(capital,
                "el último día del mes el capital diario es el de los saldos al cierre");
            var estados = await CarteraE2E.GetAsync(http, admin, $"{Vistas}/deposit-accounts-by-status?asOf={fin:yyyy-MM-dd}");
            estados.GetProperty("filas").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        }

        var registro = await CarteraE2E.GetAsync(http, admin, Vistas);
        registro.EnumerateArray().Select(v => v.GetProperty("key").GetString()).Should().Contain(
        [
            "deposit-accounts-by-status", "sicses-captaciones", "sicses-cotitulares", "sicses-aportes", "sicses-saldos-diarios", "sicses-fondo-liquidez",
            "exogena-1019", "exogena-1020",
        ]);
    }

    /// <summary>El estado de una generación por su nombre, venga el enum como número o como texto.</summary>
    private static string Estado(JsonElement archivo)
    {
        var estado = archivo.GetProperty("status");
        return estado.ValueKind == JsonValueKind.Number
            ? estado.GetInt32() switch { 1 => "Validated", 2 => "Generated", 3 => "Superseded", 4 => "Uploaded", var n => n.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            : estado.GetString()!;
    }
}
