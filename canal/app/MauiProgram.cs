using IngenIA365Canal.App.Puente;

namespace IngenIA365Canal.App;

/// <summary>Feature 021, US11 (T135): el arranque de la app del canal.</summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
#if CANAL_FIREBASE
        Notificaciones.Registrar(builder);
#endif
        builder.Services.AddSingleton<IBiometria, Biometria>();
        builder.Services.AddSingleton<PuenteDelCanal>();
        builder.Services.AddSingleton<PaginaPrincipal>();
        return builder.Build();
    }
}
