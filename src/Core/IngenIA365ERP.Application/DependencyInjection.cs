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
            // 3b) Feature 009: reintento ante ConcurrencyConflictException para los requests marcados
            //     IReintentableAnteConcurrencia. Va DESPUES de Audit para que la auditoria vea un solo
            //     evento con el resultado final, no uno por intento.
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ReintentoPorConcurrenciaBehavior<,>));
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
        services.AddScoped<Payroll.Services.PayrollAuditEmitter>();
        // Feature 010 — liquidaciones especiales: cargador, saldo de provisiones, contabilizador
        // sobre el de nómina, persistencia y ciclo de vida comunes a las cuatro (T026-T028).
        services.AddScoped<Payroll.Services.ProvisionBalanceReader>();
        services.AddScoped<Payroll.Services.SettlementInputLoader>();
        services.AddScoped<Payroll.Services.SettlementAccountingPoster>();
        services.AddScoped<Payroll.Settlements.Common.SettlementRunPersister>();
        services.AddScoped<Payroll.Settlements.Common.SettlementRunWorkflow>();
        // Feature 010 US4 — vacaciones: saldo derivado y la novedad que el disfrute deja en la ordinaria (T054-T055).
        services.AddScoped<Payroll.Vacations.VacationBalanceCalculator>();
        services.AddScoped<Payroll.Vacations.VacationNoveltyPlanner>();
        // Feature 009: mismo molde para contabilidad (exportaciones, envios, configuracion).
        services.AddScoped<Accounting.Reports.AccountingAuditEmitter>();
        // Feature 009: el contrato de contabilizacion (unico camino al libro) y la elegibilidad
        // de cuentas que parametrizan los demas modulos (FR-016).
        services.AddScoped<Accounting.Posting.AccountingPoster>();
        services.AddScoped<Accounting.Accounts.AccountEligibility>();
        services.AddScoped<Payroll.Novelties.CarryOverNoveltiesService>();
        services.AddScoped<Payroll.Novelties.RecurringNovelties.RecurringNoveltiesMaterializer>();
        services.AddScoped<Payroll.Runs.Queries.RunSummaryBuilder>();
        services.AddScoped<Payroll.Payslips.PayslipModelBuilder>();
        services.AddScoped<Payroll.Services.IPayslipEmailDispatcher, Payroll.Payslips.PayslipEmailDispatcher>();

        // Feature 008 — cómo se crea una persona y cómo se le da un rol con tabla hija, en
        // un solo sitio. Los usan los comandos simples y los compuestos «con persona», que
        // guardan todo en un solo SaveChanges.
        services.AddScoped<Core.People.Services.PersonFactory>();
        services.AddScoped<Payroll.EmployeeManagement.Services.EmployeeRegistrar>();
        services.AddScoped<Core.Associates.Services.AssociateRegistrar>();

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
