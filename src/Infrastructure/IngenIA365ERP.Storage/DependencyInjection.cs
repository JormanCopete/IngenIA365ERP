using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Storage.Configuration;
using IngenIA365ERP.Storage.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddStorageServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        // Feature 005: si hay correo saliente configurado, antes de intentar enviar comprobantes.
        services.AddSingleton<Application.Common.Interfaces.Notifications.IOutboundEmailStatus, Services.OutboundEmailStatus>();
        // Feature 005: el archivo de novedades se lee con CsvHelper, que solo conoce Storage.
        services.AddSingleton<Application.Payroll.Services.INoveltyFileParser, Payroll.CsvNoveltyFileParser>();

        // T106 + T107 — Adjuntos cifrados (US5). El cifrado es siempre nuestro; lo que cambia es
        // dónde queda el blob: disco en desarrollo, S3 en el clúster (2026-09-22). Un proveedor
        // desconocido tumba el arranque en vez de caer al disco sin avisar: en producción eso sería
        // escribir soportes en un volumen que nadie respalda.
        var seccion = configuration.GetSection(AttachmentStorageSettings.SectionName);
        services.Configure<AttachmentStorageSettings>(seccion);
        // Feature 011: los límites (tamaño, vida de las autorizaciones) los aplica Application, así que
        // su clase vive allá; se leen de esta misma sección y se validan AL ARRANCAR: una autorización de
        // subida de media hora no tiene que llegar a producción por un error de configuración.
        services.AddOptions<LimitesDeAdjuntos>().Bind(seccion).ValidateOnStart();
        services.AddSingleton<IValidateOptions<LimitesDeAdjuntos>, ValidadorDeLimitesDeAdjuntos>();
        services.AddSingleton<IAttachmentCipher, AttachmentEncryptionService>();
        var proveedor = seccion[nameof(AttachmentStorageSettings.Provider)] ?? AttachmentStorageSettings.ProveedorLocal;
        if (string.Equals(proveedor, AttachmentStorageSettings.ProveedorS3, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IBlobStore, S3BlobStore>();
            // Sin rutas locales con S3; se registra igual porque DEV valida al arrancar toda dependencia.
            services.AddSingleton<IAlmacenLocal, AlmacenLocalNoDisponible>();
        }
        else if (string.Equals(proveedor, AttachmentStorageSettings.ProveedorLocal, StringComparison.OrdinalIgnoreCase))
        {
            // Una sola instancia: firma las autorizaciones (IBlobStore) y las cobra (IAlmacenLocal).
            services.AddSingleton<LocalEncryptedFileStore>();
            services.AddSingleton<IBlobStore>(sp => sp.GetRequiredService<LocalEncryptedFileStore>());
            services.AddSingleton<IAlmacenLocal>(sp => sp.GetRequiredService<LocalEncryptedFileStore>());
        }
        else
            throw new InvalidOperationException($"AttachmentStorage:Provider = «{proveedor}» no existe. Use {AttachmentStorageSettings.ProveedorLocal} o {AttachmentStorageSettings.ProveedorS3}.");

        // T117 — Renderer de templates Razor para correo. Singleton: cachea
        // los .cshtml leídos del filesystem; thread-safe via ConcurrentDictionary.
        services.AddSingleton<INotificationTemplateRenderer, NotificationTemplateRenderer>();

        // Feature 002 — plantillas HTML del flujo de identidad central
        // (Invitation, PasswordReset, PasswordChanged) bajo Templates/ con
        // interpolación simple {{Key}}. Singleton: cachea cada plantilla.
        services.AddSingleton<IIdentityEmailTemplates, IdentityEmailTemplates>();

        // Phase 4b — notificación post-cambio de contraseña.
        services.AddScoped<IPasswordChangedNotifier, PasswordChangedNotifier>();

        // T119 — Background dispatcher de correo para notificaciones US6.
        services.AddHostedService<NotificationEmailDispatcher>();
        return services;
    }
}

/// <summary>Traduce <see cref="LimitesDeAdjuntos.Problema"/> al mensaje que ve quien arranca el servicio.</summary>
internal sealed class ValidadorDeLimitesDeAdjuntos : IValidateOptions<LimitesDeAdjuntos>
{
    public ValidateOptionsResult Validate(string? name, LimitesDeAdjuntos options) =>
        options.Problema() is { } problema ? ValidateOptionsResult.Fail(problema) : ValidateOptionsResult.Success;
}
