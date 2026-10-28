using System.Diagnostics;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity.Documento;

/// <summary>
/// Feature 017 (T093, research R5): el ingreso tarda lo mismo con un identificador que no existe que con una
/// clave errada —el resolvedor simula la verificación de clave cuando no hay cuenta—, así que el tiempo no
/// revela quién está registrado. Compara las medianas de 24 intentos de cada uno, con un margen holgado para no
/// depender de la máquina. Es de rendimiento: corre sólo con <c>RUN_PERF_TESTS=1</c> y si no, sale omitida con
/// su motivo.
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class TiempoHomogeneoTests(CentralIdentityApiFixture fx)
{
    // Cuatro claves erradas por cuenta, por debajo del bloqueo (5): una cuenta bloqueada responde sin verificar la
    // clave y la medición dejaría de comparar lo que importa.
    private const int Cuentas = 6;
    private const int PorCuenta = 4;

    [FactDeRendimiento]
    public async Task Inexistente_y_clave_errada_tardan_lo_mismo()
    {
        var esc = new Escenario017(fx);
        using var http = fx.CreateClient();
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        var coop = await esc.CooperativaAsync(http, maestro, "tenant_f017_tiempo", "900171701");
        for (var c = 0; c < Cuentas; c++)
            await esc.PersonaAsync(http, maestro, coop, $"medida{c}@f017.test", "CC", $"6317000{c}");

        // Calentar con un inexistente, y alternar para que ninguno de los dos se lleve el arranque en frío.
        await Escenario017.IngresarAsync(http, "63178999", "Clave-Errada-Para-Medir-1");
        var errada = new List<double>();
        var inexistente = new List<double>();
        for (var i = 0; i < Cuentas * PorCuenta; i++)
        {
            errada.Add(await MedirAsync(http, $"6317000{i / PorCuenta}"));
            inexistente.Add(await MedirAsync(http, $"6317{9000 + i}"));
        }

        var medianaErrada = Mediana(errada);
        var medianaInexistente = Mediana(inexistente);
        var margen = Math.Max(60, medianaErrada * 0.5);
        Assert.True(Math.Abs(medianaErrada - medianaInexistente) <= margen,
            $"Mediana con clave errada {medianaErrada:F0} ms, con inexistente {medianaInexistente:F0} ms (margen {margen:F0} ms).");
    }

    private static async Task<double> MedirAsync(HttpClient http, string identificador)
    {
        var reloj = Stopwatch.StartNew();
        using var r = await Escenario017.IngresarAsync(http, identificador, "Clave-Errada-Para-Medir-1");
        reloj.Stop();
        return reloj.Elapsed.TotalMilliseconds;
    }

    private static double Mediana(List<double> valores)
    {
        var orden = valores.Order().ToList();
        return orden.Count % 2 == 1 ? orden[orden.Count / 2] : (orden[orden.Count / 2 - 1] + orden[orden.Count / 2]) / 2;
    }
}
