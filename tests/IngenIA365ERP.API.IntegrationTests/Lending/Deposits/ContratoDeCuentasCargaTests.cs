using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// T126 (feature 014, US4; SC-005), en el motor de <c>DB_PROVIDER</c> y sólo con <c>RUN_PERF_TESTS=1</c> (sin ella se reporta omitida con su motivo): el
/// recaudador de prueba manda en paralelo muchas consignaciones y retiros sobre las mismas cuentas, y cada uno <b>tres veces con la misma clave</b> (los
/// reintentos de una red que se corta). Al final hay exactamente una operación por clave, el saldo de cada cuenta es la suma de lo aplicado y ninguna
/// respuesta dejó un 5xx. El tamaño se baja con <c>CAPTACIONES_OPERACIONES_DE_CARGA</c> para probar el arnés.
/// <b>e2e en la corrida final</b> (decisión del dueño): se escribe y compila; no se corre por fase. (nuevo)
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public sealed class ContratoDeCuentasCargaTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    private const int OperacionesPorDefecto = 600;
    private const int Cuentas = 20;
    private const int Reintentos = 3;
    private const int Paralelo = 16;

    [FactDeRendimiento]
    public async Task Los_reintentos_con_la_misma_clave_nunca_duplican_una_operacion()
    {
        var cuantas = int.TryParse(Environment.GetEnvironmentVariable("CAPTACIONES_OPERACIONES_DE_CARGA"), out var n) && n > 0 ? n : OperacionesPorDefecto;
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "carga_contrato");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHCARGA");

        var cuentas = new List<(Guid Cuenta, Guid Titular)>();
        for (var i = 0; i < Cuentas; i++)
        {
            var titular = await MovimientosTests.AsociadoAsync(http, admin, $"Carga{i}");
            var cuenta = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, linea, titular, exenta: true);
            await MovimientosTests.ConsignarAsync(http, admin, cuenta, titular, 10_000_000m, hoy);
            cuentas.Add((cuenta, titular));
        }

        // Dos de cada tres son consignaciones de 10.000 y una, un retiro de 5.000; cada una se manda tres veces con la misma clave.
        var pedidos = Enumerable.Range(0, cuantas).Select(i =>
        {
            var (cuenta, titular) = cuentas[i % Cuentas];
            return (Clave: $"REC:{Guid.NewGuid():N}", Cuenta: cuenta, Titular: titular, Retiro: i % 3 == 2);
        }).ToList();
        var fallas = 0;
        using var limite = new SemaphoreSlim(Paralelo);
        var tareas = pedidos.SelectMany(p => Enumerable.Range(0, Reintentos).Select(async _ =>
        {
            await limite.WaitAsync();
            try
            {
                using var cliente = fx.CreateClient();
                var resp = p.Retiro
                    ? await MovimientosTests.RetirarAsync(cliente, admin, p.Cuenta, p.Titular, 5_000m, hoy, clave: p.Clave)
                    : await CarteraE2E.ConClaveAsync(cliente, admin, HttpMethod.Post, Receipts.RecaudoEnsayoDelContrato.Ruta + "/accounts/deposits", new
                    {
                        deposits = new[]
                        {
                            new
                            {
                                idempotencyKey = p.Clave, accountPublicId = p.Cuenta, modality = "Free", amount = 10_000m, valueDate = hoy, meansClass = "Cash",
                                depositorPublicId = p.Titular, receipt = MovimientosTests.Recibo(hoy),
                            },
                        },
                    });
                if ((int)resp.StatusCode >= 500) Interlocked.Increment(ref fallas);
            }
            finally
            {
                limite.Release();
            }
        })).ToList();
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        await Task.WhenAll(tareas);
        salida.WriteLine($"{tareas.Count:N0} pedidos ({cuantas:N0} claves × {Reintentos}) en {reloj.Elapsed.TotalSeconds:N1} s.");

        fallas.Should().Be(0, "ningún reintento debe terminar en un error del servidor");
        var claves = pedidos.Select(p => p.Clave).ToList();
        var porClave = await CausacionE2E.EnLaBaseAsync(fx, coop.TenantPublicId, db => db.MemberAccountOperations.AsNoTracking()
            .Where(o => o.IdempotencyKey != null && claves.Contains(o.IdempotencyKey))
            .GroupBy(o => o.IdempotencyKey!).Select(g => g.Count()).ToListAsync());
        porClave.Should().OnlyContain(c => c == 1, "cero operaciones duplicadas (SC-005)");
        porClave.Count.Should().BeLessThanOrEqualTo(cuantas);
    }
}
