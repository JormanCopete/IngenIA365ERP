using System.Reflection;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Services;
using IngenIA365ERP.Application.Invitations.Services;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // MediatR + pipeline behaviors.
        // Orden de envoltura (outermost → innermost): Validation → Logging → Audit → Performance.
        // 1) Validation se ejecuta primero para que las requests inválidas no lleguen al logger ni al audit.
        // 2) Logging abre el scope con tenant/usuario antes de que cualquier otro behavior emita.
        // 3) Audit registra solo lo que pasó validación.
        // 4) Performance es lo más cercano al handler — su cronómetro mide tiempo "útil".
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        });

        // FluentValidation — auto-register all validators in this assembly
        services.AddValidatorsFromAssembly(assembly);

        // Mapster
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(assembly);
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        // Feature 002 — US1 services.
        // ITenantUserProvisioner: garantiza fila SEC_Users en el tenant destino
        // al aceptar invitación (T056). IInvitationEmailDispatcher: arma + envía
        // el correo de invitación (T057). Ambos Scoped — consumen IApplicationDbContext
        // / IEmailSender que también son Scoped.
        // Lo que pasa tras superar el segundo factor. Lo comparten la
        // verificacion por codigo y la de passkey: son dos formas de demostrar lo
        // mismo, y a partir de ahi tiene que ocurrir exactamente lo mismo.
        services.AddScoped<
            Identity.Auth.Common.IEmisorDeSesionTrasSegundoFactor,
            Identity.Auth.Common.EmisorDeSesionTrasSegundoFactor>();

        // Y lo que pasa tras INSCRIBIR el primero viniendo forzado, que es otra
        // cosa. También lo comparten los dos métodos.
        services.AddScoped<
            Identity.Profile.Common.IElevadorDeSesionTrasInscripcion,
            Identity.Profile.Common.ElevadorDeSesionTrasInscripcion>();

        services.AddScoped<
            Identity.Auth.Common.IPoliticaDePlataforma,
            Identity.Auth.Common.PoliticaDePlataforma>();

        services.AddScoped<ITenantUserProvisioner, TenantUserProvisioner>();
        services.AddScoped<IInvitationEmailDispatcher, InvitationEmailDispatcher>();

        // Feature 005 — servicios compartidos de nómina (T044-T046). El motor
        // (Domain) no se registra: es puro y se instancia donde se usa.
        services.AddScoped<Payroll.Services.PayrollPolicyReader>();
        services.AddScoped<Payroll.Services.CalculationInputLoader>();
        services.AddScoped<Payroll.Services.PayrollAccountingPoster>();
        services.AddScoped<Payroll.Services.IPayrollRunStaleMarker, Payroll.Services.PayrollRunStaleMarker>();

        // Phase 4b — dispatcher del correo "olvidé mi contraseña".
        services.AddScoped<
            IngenIA365ERP.Application.Identity.Profile.Services.IPasswordResetEmailDispatcher,
            IngenIA365ERP.Application.Identity.Profile.Services.PasswordResetEmailDispatcher>();



        // El aviso de recuperacion del segundo factor. Mismo camino que el de
        // contrasena —plantilla + IEmailSender— porque ocurre en el mismo estado:
        // sin cooperativa elegida, donde el contexto de datos lanza por Principio IV.
        services.AddScoped<
            Identity.Auth.Recuperacion.IMfaRecoveryEmailDispatcher,
            Identity.Auth.Recuperacion.MfaRecoveryEmailDispatcher>();

        services.AddScoped<
            Identity.Auth.Recuperacion.ICanceladorDeRecuperacionesAlEntrar,
            Identity.Auth.Recuperacion.CanceladorDeRecuperacionesAlEntrar>();

        // Helper transversal — generador de tokens crypto-safe para flujos
        // de un solo uso (invitaciones US1, password reset Phase 4b).
        // Singleton: stateless, basado en RandomNumberGenerator + SHA-256.
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();

        return services;
    }
}
