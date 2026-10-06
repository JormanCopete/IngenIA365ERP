namespace IngenIA365ERP.Application.Common.Interfaces;

/// <summary>
/// Si este despliegue es de producción (feature 013, T20, CH20): un solo criterio, <c>IHostEnvironment.IsProduction()</c>, que la
/// API envuelve aquí para que Application no dependa del host. Lo usan el registro de participantes (los dobles de prueba nunca
/// cuentan en producción) y su escritor (los rechaza). El carácter de producción lo da el despliegue, no la cooperativa. (nuevo)
/// </summary>
public interface IAmbienteDeDespliegue
{
    bool EsProduccion { get; }
}
