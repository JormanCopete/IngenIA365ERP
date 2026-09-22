using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Storage.Configuration;
using IngenIA365ERP.Storage.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        services.AddSingleton<IAttachmentCipher, AttachmentEncryptionService>();
        var proveedor = seccion[nameof(AttachmentStorageSettings.Provider)] ?? AttachmentStorageSettings.ProveedorLocal;
        if (string.Equals(proveedor, AttachmentStorageSettings.ProveedorS3, StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IBlobStore, S3BlobStore>();
        else if (string.Equals(proveedor, AttachmentStorageSettings.ProveedorLocal, StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IBlobStore, LocalEncryptedFileStore>();
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
