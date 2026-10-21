using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.API.Integration;

/// <summary>
/// <see cref="IAmbienteDeDespliegue"/> desde el host (feature 013, T20, CH20): <c>IHostEnvironment.IsProduction()</c> al arrancar, el
/// único criterio de producción para los dobles de prueba y el registro de participantes. (nuevo)
/// </summary>
public sealed class AmbienteDeDespliegue(bool esProduccion) : IAmbienteDeDespliegue
{
    public bool EsProduccion { get; } = esProduccion;
}
