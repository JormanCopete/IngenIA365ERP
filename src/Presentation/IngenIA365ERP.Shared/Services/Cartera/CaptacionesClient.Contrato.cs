using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Feature 014, US7 (T185; contracts/api.md §4 de la 014): las rutas del ahorro contractual sobre la cuenta —el contrato con su plan, su cumplimiento y lo
/// proyectado, las condiciones selladas y la instrucción al vencimiento—. Sin <c>Authorization</c> a mano (la pone el handler); la escritura con su
/// <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed partial class CaptacionesClient
{
    /// <summary>El contrato de un ahorro contractual: condiciones, cuotas del plan, cumplimiento, proyección e instrucción.</summary>
    public Task<ResultadoDeInventario<ContratoDeAhorroDto>> ContratoDeAhorroAsync(Guid cuenta, CancellationToken ct = default) =>
        Enviar<ContratoDeAhorroDto>(HttpMethod.Get, $"{RutaDeCuentas}/{cuenta}/contractual", null, null, ct);

    /// <summary>Las versiones de condiciones selladas de una cuenta (todas, o la pedida).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<CondicionesSelladasDto>>> CondicionesSelladasAsync(Guid cuenta, int? version = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CondicionesSelladasDto>>(HttpMethod.Get, ConQuery($"{RutaDeCuentas}/{cuenta}/terms", Query(("version", Texto(version)))), null, null,
            ct);

    /// <summary>Cambia la instrucción al vencimiento del contractual (mientras el contrato no termine).</summary>
    public Task<ResultadoDeInventario<ContratoDeAhorroDto>> CambiarInstruccionDeContratoAsync(Guid cuenta, CambiarInstruccionDeContratoRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ContratoDeAhorroDto>(HttpMethod.Put, $"{RutaDeCuentas}/{cuenta}/maturity-instruction", request, clave, ct);

    /// <summary>Retira la instrucción al vencimiento (E263): al vencer, el contrato queda a disposición del titular sin mover dinero.</summary>
    public Task<ResultadoDeInventario<ContratoDeAhorroDto>> RetirarInstruccionDeContratoAsync(Guid cuenta, RetirarInstruccionDeContratoRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ContratoDeAhorroDto>(HttpMethod.Post, $"{RutaDeCuentas}/{cuenta}/maturity-instruction/withdraw", request, clave, ct);
}
