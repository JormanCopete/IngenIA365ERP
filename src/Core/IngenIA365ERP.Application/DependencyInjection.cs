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
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IngenIA365ERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // MediatR + pipeline behaviors.
        // Orden de envoltura (outermost → innermost): Validation → Logging → Idempotency → Audit →
        // ReintentoPorConcurrencia → Performance (feature 012, T14).
        // 1) Validation se ejecuta primero para que las requests inválidas no lleguen al logger ni al audit.
        // 2) Logging abre el scope con tenant/usuario antes de que cualquier otro behavior emita.
        // 3) Audit registra solo lo que pasó validación.
        // 4) Performance es lo más cercano al handler — su cronómetro mide tiempo "útil".
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            // 2b) Feature 012 (T13, T14): los comandos IOperacionIdempotente abren aqui su transaccion
            //     (TransaccionExplicita) y guardan la clave en COR_OperationKeys; va ANTES de Audit para
            //     que la auditoria del comando quede dentro de la transaccion y una repeticion no la
            //     dispare otra vez. Orden: Validation -> Logging -> Idempotency -> Audit ->
            //     ReintentoPorConcurrencia -> Performance.
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
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
        // Feature 010 (US8): dispersión bancaria sobre el motor genérico de archivos planos (Common/BankFiles).
        services.AddScoped<Payroll.Dispersion.PayrollDisbursementLines>();
        services.AddScoped<Payroll.Dispersion.PreparacionDeDispersion>();
        services.AddScoped<Payroll.Pila.PilaInputLoader>();
        services.AddScoped<Payroll.Pila.PreparacionDePila>();
        services.AddScoped<Payroll.WithholdingRates.WithholdingRateInputLoader>();
        services.AddScoped<Payroll.Services.SettlementAccountingPoster>();
        services.AddScoped<Payroll.Settlements.Common.SettlementRunPersister>();
        services.AddScoped<Payroll.Settlements.Common.SettlementRunWorkflow>();
        // Feature 010 US4 — vacaciones: saldo derivado y la novedad que el disfrute deja en la ordinaria (T054-T055).
        services.AddScoped<Payroll.Vacations.VacationBalanceCalculator>();
        services.AddScoped<Payroll.Vacations.VacationNoveltyPlanner>();
        // Feature 009: mismo molde para contabilidad (exportaciones, envios, configuracion).
        services.AddScoped<Accounting.Reports.AccountingAuditEmitter>();
        // Feature 012 (T181): el de Inventario, por la bandeja encadenada (exportar informes y catalogos con datos).
        services.AddScoped<Inventory.Reports.InventoryAuditEmitter>();
        // Feature 009: el contrato de contabilizacion (unico camino al libro) y la elegibilidad
        // de cuentas que parametrizan los demas modulos (FR-016).
        services.AddScoped<Accounting.Posting.AccountingPoster>();
        // Feature 012 (T21, T070-T071): el unico lector de parametros con vigencia (memoriza por peticion) y los
        // dos ganchos del alta, vacios hasta que Inventario los implemente (US1 T226, US3 T286).
        services.AddScoped<Common.Parameters.ILectorDeParametros, Common.Parameters.LectorDeParametros>();
        // Feature 012 (T23, T163-T164): el unico lector de la UVT y el unico lector del catalogo tributario (arma la foto
        // del motor tributario a una fecha). Scoped: memorizan por peticion.
        services.AddScoped<Common.Taxation.IValorUvt, Common.Taxation.LectorDeUvt>();
        services.AddScoped<Core.Taxes.LectorDeCatalogoTributario>();
        // Feature 012 (T226, US1): Inventario resuelve el ambito Warehouse (bodega del alcance); US3 (T286) suma el resto.
        services.AddScoped<Common.Parameters.IResolutorDeAmbitoDeParametro, Inventory.Common.ReglasDePlataformaDeInventario>();
        services.AddScoped<Common.Parameters.IReglasDeParametros, Common.Parameters.ReglasDeParametrosVacias>();
        // Feature 012 (T7, T9, T078): el unico escritor de la bandeja de salida. Scoped porque recuerda lo que emitio
        // en su ambito (dos eventos del mismo guardado, o un relacionado en la transaccion de su original).
        services.AddScoped<Common.Integration.EmisorDeMensajes>();
        // Feature 012 (T33, T34, T083-T085): el motor de aprobaciones y lo que comparte con sus consultas. Las reglas
        // de politica de Inventario van vacias hasta que existan sus duenos (tipos y periodos, fases 3 a 6); cada fuente (IFuenteDeAprobacion) la registra su
        // historia. TryAdd para que el modulo que los implementa los reemplace sin quitar estas lineas.
        services.AddScoped<Common.Approvals.VistaDeSolicitudes>();
        services.AddScoped<Common.Approvals.IMotorDeAprobaciones, Common.Approvals.MotorDeAprobaciones>();
        // Feature 012 (T39, T093): el aviso Aprobaciones.Pendiente sale por las alertas (reemplaza a SinAvisosDeAprobacion).
        services.TryAddScoped<Common.Approvals.IAvisosDeAprobacion, Common.Alerts.AvisosDeAprobacionPorAlertas>();
        services.TryAddScoped<Common.Approvals.IReglasDePoliticaDeAprobacion, Common.Approvals.ReglasDePoliticaDeAprobacionVacias>();
        // Feature 012 (T39, T093-T094): levantar y atender alertas, y qué alertas alcanzan a quien pregunta. Los
        // destinatarios por permiso (IDestinatariosPorPermiso) los resuelve la API. Adelanto de T096.
        services.AddScoped<Common.Alerts.IAlertas, Common.Alerts.Alertas>();
        services.AddScoped<Common.Alerts.VisibilidadDeAlertas>();
        // Feature 012 (T35, T090): el alcance comercial de un usuario, leído y escrito sólo por los puertos de asignación.
        services.AddScoped<Inventory.Security.Scopes.VistaDeAlcanceComercial>();
        // Feature 012 (T16, T139): el unico que asigna numero a un documento de inventario no fiscal.
        services.AddScoped<Inventory.Documents.Numeracion.Numerador>();
        // Feature 012 (T142-T150): el ciclo común del documento y los tipos. Las estrategias por clase (IEfectoDeClase) las
        // registra cada historia; los maestros del documento (bodegas, productos, unidades, corte) los reemplaza US1/US3
        // (TryAdd); la guardia fiscal y la validación previa se registran en I3/I4 e I2 (sin registro se omiten).
        services.AddScoped<Inventory.Documents.Efectos.EfectosDeClase>();
        // Feature 012 (US1): los maestros reales sobre las tablas del catalogo y las bodegas (el corte lo suma US3).
        services.AddScoped<Inventory.Documents.IMaestrosDelDocumento, Inventory.Documents.MaestrosDelDocumentoEnBase>();
        // Feature 012 (T222-T225, US1): bodegas y catalogo. El alcance por bodega se lee y se escribe por
        // AsignacionesDeBodegaEnBase (T224; la API registra la vacia con TryAdd despues, asi que gana esta). La existencia
        // para inactivar y para la busqueda la informa IExistenciasParaElCatalogo: sin kardex hasta que US2 registre la real.
        services.AddScoped<Inventory.Warehouses.VistaDeBodegas>();
        services.AddScoped<Common.Interfaces.Security.IAsignacionesDeBodega, Inventory.Security.Scopes.AsignacionesDeBodegaEnBase>();
        services.TryAddScoped<Inventory.Common.IExistenciasParaElCatalogo, Inventory.Common.ExistenciasSinKardex>();
        services.AddScoped<Inventory.Documents.VistaDeDocumentos>();
        services.AddScoped<Inventory.Documents.ConfirmacionDeDocumento>();
        services.AddScoped<Common.Approvals.IFuenteDeAprobacion, Inventory.Documents.FuenteDeAprobacionDeDocumento>();
        services.AddScoped<Inventory.DocumentTypes.VistaDeTiposDeDocumento>();
        // Feature 012 (T49, T156): el motor común de las plantillas de importación (revisión y aplicación).
        services.AddScoped<Common.Imports.EjecutorDeImportacion>();
        services.AddScoped<Accounting.Accounts.AccountEligibility>();
        // El recaudo de Cartera como servicio: ProcessPaymentCommand lo llama por el pipeline y la
        // definitiva (feature 010, D-08) directo, dentro de su transacción y sin reintento anidado.
        services.AddScoped<Lending.Payments.Services.RecaudoDeCredito>();
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
        // Feature 012 (T46, T175): el alta con la autorización de datos del titular, y su lectura sólo por PublicId.
        services.AddScoped<Compliance.HabeasData.AutorizacionDeDatos>();
        services.AddScoped<Compliance.HabeasData.IAutorizacionDeDatos>(sp => sp.GetRequiredService<Compliance.HabeasData.AutorizacionDeDatos>());
        services.AddScoped<Core.People.Services.AltaConAutorizacion>();

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
