using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Channels;

/// <summary>
/// El registro de canales cuando el proceso no tiene ninguno (feature 012, I4): ningún código, y resolver uno es un defecto de despliegue. Lo
/// registra <c>AddApplication</c> con <c>TryAdd</c> para que el contenedor se arme —la validación del contenedor en desarrollo exige que todo
/// manejador se pueda construir— mientras <c>AddElectronicInvoicing</c> no registre el real (<c>CanalesDeEmision</c>, T728), que lo reemplaza.
/// Con él, la guardia responde <c>ChannelUnknown</c>, igual que sin registro. (nuevo)
/// </summary>
public sealed class SinCanalesRegistrados : ICanalesDeEmision
{
    public IReadOnlyCollection<string> Codigos { get; } = [];

    public ICanalDeEmisionElectronica Resolver(string channelCode) =>
        throw new InvalidOperationException($"No hay ningún canal de emisión registrado en este proceso (se pidió «{channelCode}»).");
}

/// <summary>
/// Las credenciales cuando el proceso no tiene de dónde leerlas (feature 012, I4): siempre un fallo, que la emisión traduce a
/// <c>ChannelUnavailable</c> y la verificación a <c>CredentialMismatch</c>. Lo reemplaza <c>CredencialesEnArchivo</c> (T729). (nuevo)
/// </summary>
public sealed class SinCredencialesConfiguradas : ICredencialesDeCanal
{
    public Task<Result<CredencialesDeCanal>> ResolverAsync(string channelCode, CancellationToken ct) =>
        Task.FromResult(Result.Failure<CredencialesDeCanal>("ElectronicInvoicing.CredentialMismatch",
            "Este proceso no tiene configurado de dónde leer las credenciales de la facturación electrónica."));
}
