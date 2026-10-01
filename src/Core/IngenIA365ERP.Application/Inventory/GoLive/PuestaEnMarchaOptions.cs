namespace IngenIA365ERP.Application.Inventory.GoLive;

/// <summary>
/// La opción de la puesta en marcha (feature 012, T313; contracts/api.md §13.3; data-model §6.4) <b>(nuevo)</b>. La fija la API
/// por ambiente, no la configuración: <c>PermitirActivacionSinComparacion = !env.IsProduction()</c>.
/// <para>
/// Antes de I2 no hay consulta de saldos contables (<c>IContabilidadParaInventario</c>, US7). Fuera de producción la activación
/// se ensaya con la diferencia aceptada, el permiso especial y un motivo («sin comparación contable»); en producción ese camino
/// no existe y <c>ActivateWarehouseCommand</c> responde 422 <c>Inventory.Activation.AccountingUnavailable</c>, así que ninguna
/// bodega se activa sin la comparación de FR-090. El valor por defecto es el seguro: <c>false</c>.
/// </para>
/// </summary>
public sealed class PuestaEnMarchaOptions
{
    /// <summary>¿Se admite activar sin la comparación contable, aceptando la diferencia con permiso y motivo?</summary>
    public bool PermitirActivacionSinComparacion { get; set; }
}
