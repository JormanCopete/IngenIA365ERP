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

        // T106 + T107 — Adjuntos cifrados (US5).
        services.Configure<AttachmentStorageSettings>(
            configuration.GetSection(AttachmentStorageSettings.SectionName));
        services.AddSingleton<IAttachmentCipher, AttachmentEncryptionService>();
        services.AddSingleton<IBlobStore, LocalEncryptedFileStore>();

        // T117 — Renderer de templates Razor para correo. Singleton: cachea
        // los .cshtml leídos del filesystem; thread-safe via ConcurrentDictionary.
        services.AddSingleton<INotificationTemplateRenderer, NotificationTemplateRenderer>();

        // T119 — Background dispatcher de correo para notificaciones US6.
        services.AddHostedService<NotificationEmailDispatcher>();
        return services;
    }
}
