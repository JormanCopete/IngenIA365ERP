using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

/// <summary>
/// Cliente tipado de las cuentas bancarias propias, sus saldos, el alcance por cuenta y el fondo de liquidez (feature 015, US10, T0147; contracts/api.md §9). Sin
/// <c>Authorization</c> a mano; las escrituras llevan <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class CuentasBancariasClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/treasury";

    public Task<ResultadoDeInventario<IReadOnlyList<CuentaBancariaPropiaDto>>> CuentasAsync(bool incluirInactivas = false, CancellationToken ct = default) =>
        Get<IReadOnlyList<CuentaBancariaPropiaDto>>(EnvioDeVentas.ConQuery($"{Base}/bank-accounts", EnvioDeVentas.Query(("includeInactive", EnvioDeVentas.Texto(incluirInactivas)))), ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CuentaContableBancariaDto>>> CuentasDelPlanAsync(Guid? banco, CancellationToken ct = default) =>
        Get<IReadOnlyList<CuentaContableBancariaDto>>(EnvioDeVentas.ConQuery($"{Base}/bank-accounts/chart-accounts", EnvioDeVentas.Query(("bank", EnvioDeVentas.Texto(banco)))), ct);

    public Task<ResultadoDeInventario<SaldosDeCuentaDto>> SaldosAsync(Guid cuenta, CancellationToken ct = default) =>
        Get<SaldosDeCuentaDto>($"{Base}/bank-accounts/{cuenta}/balances", ct);

    public Task<ResultadoDeInventario<CuentaBancariaPropiaDto>> CrearAsync(CrearCuentaBancariaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<CuentaBancariaPropiaDto>(http, auth, HttpMethod.Post, $"{Base}/bank-accounts", r, clave, ct);

    public Task<ResultadoDeInventario<CuentaBancariaPropiaDto>> ActualizarAsync(Guid cuenta, ActualizarCuentaBancariaRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<CuentaBancariaPropiaDto>(http, auth, HttpMethod.Put, $"{Base}/bank-accounts/{cuenta}", r, clave, ct);

    public Task<ResultadoDeInventario<CuentaBancariaPropiaDto>> InactivarAsync(Guid cuenta, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<CuentaBancariaPropiaDto>(http, auth, HttpMethod.Post, $"{Base}/bank-accounts/{cuenta}/inactivate", null, clave, ct);

    public Task<ResultadoDeInventario<AlcanceDeTesoreriaDto>> AlcanceAsync(Guid usuario, CancellationToken ct = default) =>
        Get<AlcanceDeTesoreriaDto>($"{Base}/users/{usuario}/scope", ct);

    public Task<ResultadoDeInventario<AlcanceDeTesoreriaDto>> FijarAlcanceAsync(Guid usuario, FijarAlcanceDeTesoreriaRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<AlcanceDeTesoreriaDto>(http, auth, HttpMethod.Put, $"{Base}/users/{usuario}/scope", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<UsoDelFondoDto>>> UsosDelFondoAsync(Guid? cuenta = null, int? estado = null, CancellationToken ct = default) =>
        Get<IReadOnlyList<UsoDelFondoDto>>(EnvioDeVentas.ConQuery($"{Base}/liquidity-fund-uses",
            EnvioDeVentas.Query(("bankAccount", EnvioDeVentas.Texto(cuenta)), ("state", EnvioDeVentas.Texto(estado)))), ct);

    public Task<ResultadoDeInventario<UsoDelFondoDto>> PedirUsoAsync(PedirUsoDelFondoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<UsoDelFondoDto>(http, auth, HttpMethod.Post, $"{Base}/liquidity-fund-uses", r, clave, ct);

    public Task<ResultadoDeInventario<UsoDelFondoDto>> AprobarUsoAsync(Guid uso, string? motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<UsoDelFondoDto>(http, auth, HttpMethod.Post, $"{Base}/liquidity-fund-uses/{uso}/approve", new MotivoRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<UsoDelFondoDto>> RechazarUsoAsync(Guid uso, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<UsoDelFondoDto>(http, auth, HttpMethod.Post, $"{Base}/liquidity-fund-uses/{uso}/reject", new MotivoRequest(motivo), clave, ct);

    private Task<ResultadoDeInventario<T>> Get<T>(string url, CancellationToken ct) => EnvioDeVentas.EnviarAsync<T>(http, auth, HttpMethod.Get, url, null, null, ct);
}
