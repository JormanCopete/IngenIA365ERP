using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.ExternalChannels;

/// <summary>
/// Resuelve la credencial de la cooperativa resuelta para un canal externo (feature 013, T0079; T72; contracts/adaptadores-y-archivos.md
/// §3.4). La ruta la arma la implementación (<c>CredencialesDeCanalExternoEnArchivo</c>) <b>sólo</b> con el <c>PublicId</c> de la
/// cooperativa resuelta y un <paramref name="channelCode"/> que cumple <c>^[A-Z0-9-]{2,30}$</c>; nunca con un texto leído de la base.
/// <c>SIMULADO</c> y <c>CARGA-PROPIA</c> no leen archivo. Un archivo ausente o ilegible es un fallo
/// (<c>Core.ExternalChannel.CredentialMissing</c>), no una excepción; una <c>CredentialKey</c> guardada que no es la derivada,
/// <c>Core.ExternalChannel.CredentialMismatch</c>. (nuevo)
/// </summary>
public interface ICredencialesDeCanalExterno
{
    Task<Result<CredencialDeCanalExterno>> ResolverAsync(string channelCode, CancellationToken ct);
}

/// <summary>
/// El defecto cuando el proceso no compuso <c>AddExternalChannels</c> (feature 013, T0079): los canales que no piden archivo resuelven una
/// credencial vacía; cualquier otro, <c>Core.ExternalChannel.CredentialMissing</c>. Lo reemplaza <c>CredencialesDeCanalExternoEnArchivo</c>.
/// (nuevo)
/// </summary>
public sealed class SinCredencialesDeCanalExterno(Interfaces.ICurrentTenantService tenant) : ICredencialesDeCanalExterno
{
    public Task<Result<CredencialDeCanalExterno>> ResolverAsync(string channelCode, CancellationToken ct)
    {
        var canal = CodigosDeCanalExterno.Normalizar(channelCode);
        if (canal is not null && CodigosDeCanalExterno.NoPideCredencial(canal) && CooperativaDelCanal.Resuelta(tenant) is { } cooperativa)
            return Task.FromResult(Result.Success(CredencialDeCanalExterno.Vacia(cooperativa, canal)));

        return Task.FromResult(Result.Failure<CredencialDeCanalExterno>(ErroresDeCanalesExternos.SinCredencial(channelCode ?? string.Empty,
            "Este proceso no tiene configurado de dónde leer las credenciales de los canales externos.")));
    }
}
