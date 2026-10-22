using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Core.CostCenters;

/// <summary>Errores del catálogo de centros de costo que comparten los módulos que lo referencian (Nómina, Inventario…).</summary>
public static class CostCenterErrors
{
    public static Error NotFound() => new("Core.CostCenter.NotFound", "El centro de costo no existe.");
}
