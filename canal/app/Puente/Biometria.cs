namespace IngenIA365Canal.App.Puente;

/// <summary>Feature 021, US11 (T137): la biometría del teléfono, con el código propio de cada plataforma (sin plugins).</summary>
public interface IBiometria
{
    Task<BiometriaDelDispositivo> DisponibleAsync();

    /// <summary>Muestra el diálogo del sistema; verdadero sólo si la persona se identificó.</summary>
    Task<bool> AutenticarAsync(string motivo);
}

/// <summary>La implementación de cada plataforma está en Platforms/{Android,iOS}/Biometria.cs.</summary>
public sealed partial class Biometria : IBiometria
{
    public partial Task<BiometriaDelDispositivo> DisponibleAsync();

    public partial Task<bool> AutenticarAsync(string motivo);
}
