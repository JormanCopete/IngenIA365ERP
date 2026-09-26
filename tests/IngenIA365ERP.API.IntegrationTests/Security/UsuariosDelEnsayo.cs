using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Security;

/// <summary>
/// T417 (quickstart §2.4): los usuarios del ensayo sobre un <see cref="EscenarioDeInventario"/>, todo por HTTP. Cada uno recibe
/// un rol propio creado desde su plantilla (<c>POST /api/admin/roles/from-template</c>) con el ajuste de la tabla, su alcance
/// comercial (<c>PUT /api/inventory/scopes/users/{userPublicId}</c>) y, el comprador, su monto máximo en
/// <c>Inventory.Purchases.Confirm</c> (<c>POST /api/inventory/amount-limits</c>). El token de cada uno sale de aceptar su
/// invitación: la cooperativa aislada no exige segundo factor, así que la sesión es directa (la del maestro sí lo pasa, en la
/// fixture).
/// <list type="table">
/// <item><c>jefe</c>: <c>inventario.jefe</c> + <c>Inventory.Approvals.Management</c> (nivel 2); todas las bodegas.</item>
/// <item><c>bodega.a</c>: <c>inventario.bodeguero</c> + <c>Inventory.Adjustments.Confirm</c>; PRIN y PV1.</item>
/// <item><c>bodega.b</c>: <c>inventario.bodeguero</c>; PV2.</item>
/// <item><c>comprador</c>: <c>inventario.comprador</c>; PRIN; hasta 5.000.000.</item>
/// <item><c>aprobador</c>: <c>inventario.aprobador</c>; PRIN, PV1 y PV2.</item>
/// <item><c>auditor</c>: <c>inventario.auditor</c>; todas.</item>
/// <item><c>lectura</c>: el rol integrado <c>ReadOnly</c>; ninguna.</item>
/// </list>
/// Uno por escenario: las pruebas que piden los usuarios del mismo escenario los comparten. (nuevo)
/// </summary>
public sealed class UsuariosDelEnsayo
{
    public sealed record Usuario(string Token, Guid PublicId, Guid? Rol);

    public required Usuario Jefe { get; init; }
    public required Usuario BodegaA { get; init; }
    public required Usuario BodegaB { get; init; }
    public required Usuario Comprador { get; init; }
    public required Usuario Aprobador { get; init; }
    public required Usuario Auditor { get; init; }
    public required Usuario Lectura { get; init; }

    /// <summary>El límite de <c>comprador</c> en <c>Inventory.Purchases.Confirm</c> (quickstart §2.4).</summary>
    public const decimal LimiteDelComprador = 5_000_000m;

    private static readonly Dictionary<EscenarioDeInventario, Task<UsuariosDelEnsayo>> Creados = [];
    private static readonly object Cerrojo = new();

    public static Task<UsuariosDelEnsayo> CrearAsync(CentralIdentityApiFixture fx, EscenarioDeInventario esc)
    {
        lock (Cerrojo)
        {
            if (!Creados.TryGetValue(esc, out var tarea))
            {
                tarea = CrearDeVerdadAsync(fx, esc);
                Creados[esc] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<UsuariosDelEnsayo> CrearDeVerdadAsync(CentralIdentityApiFixture fx, EscenarioDeInventario esc)
    {
        using var http = fx.CreateClient();
        var coop = esc.Coop;
        var sufijo = coop.TenantPublicId.ToString("N")[..8];

        async Task<Usuario> ConRolAsync(string nombre, string codigoDeRol, string? plantilla, string[] extra, params Guid[] bodegas)
        {
            var rol = await InventarioE2E.RolAsync(http, coop, codigoDeRol, plantilla, extra);
            var (token, usuario) = await InventarioE2E.UsuarioAsync(fx, http, coop, $"{nombre}.{sufijo}@coop.inventario.test", codigoDeRol);
            if (bodegas.Length > 0) await InventarioE2E.AlcanceAsync(http, coop.TokenAdmin, usuario, bodegas);
            return new Usuario(token, usuario, rol);
        }

        var prin = esc.Bodega("PRIN");
        var pv1 = esc.Bodega("PV1");
        var pv2 = esc.Bodega("PV2");

        var jefe = await ConRolAsync("jefe", "ENSJEFE", "inventario.jefe", ["Inventory.Approvals.Management"]);
        var bodegaA = await ConRolAsync("bodega.a", "ENSBODA", "inventario.bodeguero", ["Inventory.Adjustments.Confirm"], prin, pv1);
        var bodegaB = await ConRolAsync("bodega.b", "ENSBODB", "inventario.bodeguero", [], pv2);
        var comprador = await ConRolAsync("comprador", "ENSCOMP", "inventario.comprador", [], prin);
        var aprobador = await ConRolAsync("aprobador", "ENSAPRO", "inventario.aprobador", [], prin, pv1, pv2);
        var auditor = await ConRolAsync("auditor", "ENSAUDI", "inventario.auditor", []);
        var (tokenLectura, idLectura) = await InventarioE2E.UsuarioAsync(fx, http, coop, $"lectura.{sufijo}@coop.inventario.test", "ReadOnly");

        await InventarioE2E.ExitoAsync(http, coop.TokenAdmin, HttpMethod.Post, "/api/inventory/amount-limits", new
        {
            rolePublicId = comprador.Rol, permissionCode = "Inventory.Purchases.Confirm", maxAmount = LimiteDelComprador,
            validFrom = esc.Corte.AddDays(1).ToString("yyyy-MM-dd"), reason = "Límite del comprador del ensayo",
        });

        return new UsuariosDelEnsayo
        {
            Jefe = jefe, BodegaA = bodegaA, BodegaB = bodegaB, Comprador = comprador, Aprobador = aprobador, Auditor = auditor,
            Lectura = new Usuario(tokenLectura, idLectura, null),
        };
    }
}
