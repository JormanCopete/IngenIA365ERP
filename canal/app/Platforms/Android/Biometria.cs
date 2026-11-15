using Android.Content.PM;
using Android.Hardware.Biometrics;
using Android.OS;
using Java.Lang;
using Java.Util.Concurrent;

namespace IngenIA365Canal.App.Puente;

/// <summary>
/// Feature 021, US11 (T137): <c>BiometricPrompt</c> del sistema (Android 10 o posterior, sin AndroidX). En versiones anteriores no se ofrece la
/// biometría y la persona entra con su clave.
/// </summary>
public sealed partial class Biometria
{
    public partial Task<BiometriaDelDispositivo> DisponibleAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29)) return Task.FromResult(new BiometriaDelDispositivo(false, null));
        var gestor = Platform.AppContext.GetSystemService(Android.Content.Context.BiometricService) as BiometricManager;
#pragma warning disable CA1422 // CanAuthenticate() sin argumentos: API 29, la mínima que se ofrece.
        var disponible = gestor?.CanAuthenticate() == (int)BiometricCode.Success;
#pragma warning restore CA1422
        var pm = Platform.AppContext.PackageManager;
        var tipo = pm?.HasSystemFeature(PackageManager.FeatureFace) == true ? "rostro" : "huella";
        return Task.FromResult(new BiometriaDelDispositivo(disponible, disponible ? tipo : null));
    }

    public partial Task<bool> AutenticarAsync(string motivo)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29) || Platform.CurrentActivity is not { } actividad) return Task.FromResult(false);
        var resultado = new TaskCompletionSource<bool>();
        var dialogo = new BiometricPrompt.Builder(actividad)
            .SetTitle("Canal del asociado")
            .SetSubtitle(motivo)
            .SetNegativeButton("Usar mi clave", actividad.MainExecutor!, new Cancelar(resultado))
            .Build();
        dialogo.Authenticate(new CancellationSignal(), actividad.MainExecutor!, new Resultado(resultado));
        return resultado.Task;
    }

    private sealed class Resultado(TaskCompletionSource<bool> tcs) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result) => tcs.TrySetResult(true);

        public override void OnAuthenticationError(BiometricErrorCode errorCode, ICharSequence? errString) => tcs.TrySetResult(false);
    }

    private sealed class Cancelar(TaskCompletionSource<bool> tcs) : Java.Lang.Object, Android.Content.IDialogInterfaceOnClickListener
    {
        public void OnClick(Android.Content.IDialogInterface? dialog, int which) => tcs.TrySetResult(false);
    }
}
