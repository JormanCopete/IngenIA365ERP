using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Inventory.Purchasing;

namespace IngenIA365ERP.Application.Inventory.Purchasing.Common;

/// <summary>
/// Las tolerancias del cruce vigentes a una fecha (feature 012, I5, T789; FR-049, FR-050; data-model §4.2): lee
/// <c>Compras.ToleranciaCantidadPorcentaje</c>, <c>…CantidadValor</c>, <c>…PrecioPorcentaje</c>, <c>…PrecioValor</c> y
/// <c>Compras.ReglaDeTolerancia</c> por el único lector de parámetros y las entrega como <see cref="ToleranciasDelCruce"/> al motor puro
/// <see cref="CruceDeCompra"/>. Sin vigencia guardada valen sus defectos seguros (cero y <c>AmbasCondiciones</c>): no se tolera
/// ninguna diferencia. Ningún valor escrito aquí. (nuevo)
/// </summary>
public static class ToleranciasVigentesDeCompra
{
    public static async Task<Result<ToleranciasDelCruce>> LeerAsync(ILectorDeParametros parametros, DateOnly fecha, CancellationToken ct)
    {
        const string m = ParametrosDeInventario.Modulo;
        var cantidadPorcentaje = await parametros.LeerComoAsync<decimal>(m, ParametrosDeInventario.ComprasToleranciaCantidadPorcentaje, fecha, ct: ct);
        if (cantidadPorcentaje.IsFailure) return Result.Failure<ToleranciasDelCruce>(cantidadPorcentaje.Error);
        var cantidadValor = await parametros.LeerComoAsync<decimal>(m, ParametrosDeInventario.ComprasToleranciaCantidadValor, fecha, ct: ct);
        if (cantidadValor.IsFailure) return Result.Failure<ToleranciasDelCruce>(cantidadValor.Error);
        var precioPorcentaje = await parametros.LeerComoAsync<decimal>(m, ParametrosDeInventario.ComprasToleranciaPrecioPorcentaje, fecha, ct: ct);
        if (precioPorcentaje.IsFailure) return Result.Failure<ToleranciasDelCruce>(precioPorcentaje.Error);
        var precioValor = await parametros.LeerComoAsync<decimal>(m, ParametrosDeInventario.ComprasToleranciaPrecioValor, fecha, ct: ct);
        if (precioValor.IsFailure) return Result.Failure<ToleranciasDelCruce>(precioValor.Error);
        var regla = await parametros.LeerComoAsync<string>(m, ParametrosDeInventario.ComprasReglaDeTolerancia, fecha, ct: ct);
        if (regla.IsFailure) return Result.Failure<ToleranciasDelCruce>(regla.Error);

        return Result.Success(new ToleranciasDelCruce(cantidadPorcentaje.Value, cantidadValor.Value, precioPorcentaje.Value, precioValor.Value,
            CruceDeCompra.ReglaDesde(regla.Value ?? nameof(ReglaDeTolerancia.AmbasCondiciones))));
    }
}
