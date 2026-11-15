using Foundation;
using LocalAuthentication;

namespace IngenIA365Canal.App.Puente;

/// <summary>Feature 021, US11 (T137): <c>LAContext</c> (Face ID o Touch ID).</summary>
public sealed partial class Biometria
{
    public partial Task<BiometriaDelDispositivo> DisponibleAsync()
    {
        using var contexto = new LAContext();
        var disponible = contexto.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _);
        var tipo = contexto.BiometryType == LABiometryType.FaceId ? "rostro" : "huella";
        return Task.FromResult(new BiometriaDelDispositivo(disponible, disponible ? tipo : null));
    }

    public partial async Task<bool> AutenticarAsync(string motivo)
    {
        using var contexto = new LAContext { LocalizedCancelTitle = "Usar mi clave" };
        try
        {
            var (ok, _) = await contexto.EvaluatePolicyAsync(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, motivo);
            return ok;
        }
        catch (NSErrorException)
        {
            return false;
        }
    }
}
