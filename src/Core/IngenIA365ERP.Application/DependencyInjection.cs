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
            // 2a) Feature 012, I4 (T734, T736): lo anotado para después del commit (el canónico de un documento electrónico, la espera
            //     en línea del POS) corre al volver de la transacción de Idempotency, sólo en la petición más externa y si terminó bien.
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(Common.Persistence.TrasElCommitBehavior<,>));
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
        // Feature 013 (T0157, T0158, T0162): Nómina recauda por el contrato de obligaciones de Cartera.
        services.AddScoped<Payroll.Settlements.PropuestaDeDescuentosDeCartera>();
        services.AddScoped<Payroll.Services.RecaudoPorNomina>();
        services.AddScoped<Payroll.Services.CuotasDeCarteraEnNomina>();
        services.AddScoped<Payroll.Services.RecaudoDeCuentasPorNomina>();
        services.AddScoped<Payroll.Services.CuotasDeCuentasEnNomina>();
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
        // Feature 013 (T37–T42, T93): valores nacionales. Los validadores de documentos normativos de la plataforma (Cartera
        // registra los suyos en sus fases), el nucleo unico de la replica con sus efectos (singletons sin estado: reciben el
        // contexto por parametro) y los tres lectores unicos de la copia de la cooperativa.
        services.AddSingleton<Domain.Common.NationalValues.IValidadorDeDocumentoNormativo, Domain.Common.NationalValues.ValidadorDeCatalogoDeModalidades>();
        services.AddSingleton<Domain.Common.NationalValues.IValidadorDeDocumentoNormativo, Domain.Common.NationalValues.ValidadorDeTiposDeOrganizacion>();
        // Feature 013 (T0204): los documentos normativos de Cartera (Anexo 1 y regla de pérdida esperada); el Anexo 2 lo suma K6a.
        services.AddSingleton<Domain.Common.NationalValues.IValidadorDeDocumentoNormativo>(Lending.Risk.ValidadorDeDocumentoNormativoDeCartera.Anexo1);
        services.AddSingleton<Domain.Common.NationalValues.IValidadorDeDocumentoNormativo>(Lending.Risk.ValidadorDeDocumentoNormativoDeCartera.ReglaDePerdidaEsperada);
        // US9 (T0691): el del Anexo 2 rechaza todo contenido hasta K6a; desde K6a (T1025) lee el documento tipado.
        services.AddSingleton<Domain.Common.NationalValues.IValidadorDeDocumentoNormativo>(Lending.Risk.ValidadorDeDocumentoNormativoDeCartera.Anexo2);
        services.AddSingleton<Common.NationalValues.IEfectoDeReplicacion, Payroll.NationalValues.EfectoDeReplicacionEnNomina>();
        services.AddSingleton<Common.NationalValues.IEfectoDeReplicacion, Common.NationalValues.EfectoDeReplicacionLimitesLegales>();
        // Feature 013, US1 (T0357): una usura o un catálogo de modalidades nuevos bloquean o cierran las líneas que ya no los respetan.
        services.AddSingleton<Common.NationalValues.IEfectoDeReplicacion, Lending.Configuration.EfectoDeReplicacionUsuraEnLineas>();
        services.AddScoped<Common.NationalValues.ReplicacionDeValoresNacionales>();
        services.AddScoped<Common.NationalValues.IValoresNacionales, Common.NationalValues.LectorDeValoresNacionales>();
        services.AddScoped<Common.NationalValues.INormativaVigente, Common.NationalValues.LectorDeDocumentoNormativo>();
        services.AddScoped<Common.Calendar.ICalendarioHabil, Common.Calendar.CalendarioDeDiasHabiles>();
        // Feature 012 (T226 US1, T286 US3): Inventario resuelve los ambitos Warehouse y DocumentType y pone las reglas del alta
        // de parametros (periodo cerrado, inicio de periodo para Costeo.*, modo de paso por cadena y tipo fiscal sin paso) y de
        // las politicas de aprobacion. Una sola instancia por peticion para las tres interfaces.
        services.AddScoped<Inventory.Common.ReglasDePlataformaDeInventario>();
        // Feature 013 (T43, T0067): el resolutor de ambitos y las reglas del alta son compuestos por modulo. Inventario registra sus
        // cinco ambitos (los mismos que antes resolvia o negaba) y sus reglas siguen siendo las de todo modulo sin reglas propias;
        // Cartera registra las suyas para LND (ReglasDeParametrosDeCartera) y, cuando existan LND_CreditLines y LND_FactTypes, el
        // resolutor de CreditLine y LendingFactType.
        services.AddScoped(sp => new Common.Parameters.ResolutorDeAmbitoDeModulo(
            Domain.Inventory.Parameters.ParametrosDeInventario.Modulo,
            [Domain.Enums.Parameters.ParameterScopeKind.Warehouse, Domain.Enums.Parameters.ParameterScopeKind.DocumentType,
             Domain.Enums.Parameters.ParameterScopeKind.PointOfSale, Domain.Enums.Parameters.ParameterScopeKind.CashRegister,
             Domain.Enums.Parameters.ParameterScopeKind.ThirdPartyKind],
            sp.GetRequiredService<Inventory.Common.ReglasDePlataformaDeInventario>()));
        // Feature 013 (T0653): Cartera resuelve el ambito LendingFactType (Contabilidad.ModoDePaso y sus hermanas por tipo de hecho).
        services.AddScoped(sp => new Common.Parameters.ResolutorDeAmbitoDeModulo(
            Domain.Lending.Parameters.ParametrosDeCartera.Modulo, Lending.Common.ResolutorDeAmbitoDeCartera.Ambitos,
            new Lending.Common.ResolutorDeAmbitoDeCartera(sp.GetRequiredService<Common.Interfaces.IApplicationDbContext>())));
        services.AddScoped<Common.Parameters.IResolutorDeAmbitoDeParametro, Common.Parameters.ResolutorDeAmbitoCompuesto>();
        services.AddScoped<Lending.Common.ReglasDeParametrosDeCartera>();
        services.AddScoped<Common.Parameters.IReglasDeParametrosDeModulo>(sp => sp.GetRequiredService<Lending.Common.ReglasDeParametrosDeCartera>());
        services.AddScoped<Common.Parameters.IReglasDeParametros>(sp => new Common.Parameters.ReglasDeParametrosPorModulo(
            sp.GetRequiredService<Inventory.Common.ReglasDePlataformaDeInventario>(),
            sp.GetServices<Common.Parameters.IReglasDeParametrosDeModulo>()));
        // Feature 013 (T50, T0064): el lector bitemporal del perfil regulatorio de la cooperativa.
        services.AddScoped<Common.NationalValues.IPerfilRegulatorio, Common.NationalValues.LectorDePerfilRegulatorio>();
        services.AddScoped<Common.Approvals.IReglasDePoliticaDeAprobacion>(sp => sp.GetRequiredService<Inventory.Common.ReglasDePlataformaDeInventario>());
        // Feature 012 (T7, T9, T078): el unico escritor de la bandeja de salida. Scoped porque recuerda lo que emitio
        // en su ambito (dos eventos del mismo guardado, o un relacionado en la transaccion de su original).
        services.AddScoped<Common.Integration.EmisorDeMensajes>();
        // Feature 012, I2 (T496-T498): el lado consumidor de la plataforma. Los destinos (IDestinoDeMensajes) los registra cada
        // modulo; sin ninguno registrado, el selector no devuelve nada y la bandeja los muestra como no disponibles. Los reintentos
        // llevan los defectos del contrato; la API los reemplaza con Integration:Retries (T527).
        services.AddScoped<Common.Integration.IMensajesEntrantes, Common.Integration.MensajesEntrantes>();
        services.AddScoped<Common.Integration.EntregasElegibles>();
        // Feature 013 (T0075, T0078; T16, T19): la vista de cada módulo en la bandeja (Inventario aquí; Cartera la agrega US7) y el
        // registro de participantes: su lector y su único escritor (agrega sin guardar). IAmbienteDeDespliegue lo pone la API.
        services.AddScoped<Common.Integration.IVistaDeBandejaDelModulo, Inventory.Integration.VistaDeBandejaDeInventario>();
        // Feature 013 (US7, bloque B): la bandeja vista desde Cartera (alcance por oficina, modo de paso por tipo de hecho).
        services.AddScoped<Common.Integration.IVistaDeBandejaDelModulo, Lending.Integration.VistaDeBandejaDeCartera>();
        services.AddScoped<Common.Integration.IRegistroDeIntegracion, Common.Integration.RegistroDeIntegracion>();
        services.AddScoped<Common.Integration.EscritorDelRegistroDeIntegracion>();
        AddCanalesExternos(services);
        // Feature 012, I2 (T505-T510): la matriz contable de Inventario. La resolucion (carga en bloque y elige por peso), el
        // punto unico de sus reglas (alta, version, desactivacion e importacion) y el resolutor del tipo de comprobante de una
        // unidad. IDimensionesDeInventario lo registra Inventario (T519).
        services.AddScoped<Accounting.Inventory.Reglas.ResolutorDeReglas>();
        services.AddScoped<Accounting.Inventory.Reglas.ReglasDeLaMatriz>();
        services.AddScoped<Accounting.Inventory.Reglas.TiposDeComprobanteDeInventario>();
        // Feature 013, US7 (T0641, T0642): la matriz contable de Cartera (resolución, reglas al guardar, tipos de comprobante). El puerto
        // IDimensionesDeCartera lo implementa Cartera (DimensionesDeCartera, T0657).
        services.AddScoped<Accounting.Lending.Reglas.ResolutorDeReglasDeCartera>();
        services.AddScoped<Accounting.Lending.Reglas.ReglasDeLaMatrizDeCartera>();
        services.AddScoped<Accounting.Lending.Contabilizacion.TiposDeComprobanteDeCartera>();
        // Feature 012, I2 (T511-T517): el consumidor contable. Contabilidad es un destino de la bandeja (consume por
        // PostInventoryMessagesCommand y PostInventorySummaryGroupCommand, planea los lotes con AgrupadorDeResumidos) y el
        // adaptador IContabilidadParaInventario le da a Inventario sus cuatro consultas en proceso.
        services.AddScoped<Accounting.Inventory.Contabilizacion.ConsumoDeInventario>();
        // Feature 013, US7 (T0645, T0647): el mismo destino reparte por módulo; lo común del consumidor de Cartera.
        services.AddScoped<Accounting.Lending.Contabilizacion.ConsumoDeCartera>();
        services.AddScoped<Common.Integration.IDestinoDeMensajes, Accounting.Integration.DestinoContabilidad>();
        services.AddScoped<Common.Integration.Accounting.IContabilidadParaInventario, Accounting.Inventory.ContabilidadParaInventario>();
        services.AddOptions<Common.Integration.ReintentosDeIntegracion>();
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
        // Feature 013 (US1-3): las alertas que sobreviven a la operación que las descubre (las levanta IdempotencyBehavior al terminar).
        services.AddScoped<Common.Alerts.AlertasDiferidas>();
        // Feature 013 (C68, T0090): el alcance de Cartera sobre los destinatarios; hasta US17 no filtra.
        services.TryAddScoped<Common.Alerts.IFiltroDeAlcanceDeAlertas, Common.Alerts.SinFiltroDeAlcanceDeAlertas>();
        services.AddScoped<Common.Alerts.VisibilidadDeAlertas>();
        // Feature 012 (T35, T090): el alcance comercial de un usuario, leído y escrito sólo por los puertos de asignación.
        services.AddScoped<Inventory.Security.Scopes.VistaDeAlcanceComercial>();
        // Feature 012, T424–T426: la única operación que da, restaura y retira el rol vendedor.
        services.AddScoped<Inventory.Salespeople.RolDeVendedor>();
        // Feature 012 (T16, T139): el unico que asigna numero a un documento de inventario no fiscal.
        services.AddScoped<Inventory.Documents.Numeracion.Numerador>();
        // Feature 013 (T0207, T0208): el numerador y el registro del libro de Cartera, únicos escritores de lo suyo.
        services.AddScoped<Lending.Common.NumeradorDeCartera>();
        services.AddScoped<Lending.Ledger.RegistroDeCartera>();
        // Feature 014 (T030): el único escritor del libro de cuentas de captación y aportes.
        services.AddScoped<Lending.Deposits.Ledger.RegistroDeCuentas>();
        // Feature 014 (T051–T055): el paso de captaciones del ciclo diario (sus subpasos, ISubpasoDeCaptaciones, los registra cada historia), la
        // sección de captaciones del cierre, los requisitos y la activación sin carga de aportes y captaciones.
        services.AddScoped<Lending.Deposits.Accrual.PasoDeCaptaciones>();
        services.AddScoped<Lending.Deposits.Accrual.SeccionDeCaptacionesDelCierre>();
        services.AddScoped<Lending.Deposits.Activation.RequisitosDeActivacionDeCaptaciones>();
        services.AddScoped<Lending.Deposits.Activation.ActivacionDeCaptacionesSinCarga>();
        // US9 (T153–T156): la migración de aportes y captaciones, su cuadre y la activación con carga.
        services.AddScoped<Lending.Deposits.Migration.ValidacionEntrePartesDeCaptaciones>();
        services.AddScoped<Lending.Deposits.Migration.CuadreDeCaptaciones>();
        services.AddScoped<Lending.Deposits.Activation.NacimientoDeCuentasMigradas>();
        services.AddScoped<Lending.Deposits.Activation.ActivacionDeCaptacionesConCarga>();
        // Feature 014, US1 (T064–T067): el único que registra una versión de una línea de captación o de aportes, lo que esas líneas leen fuera de sí
        // (perfil regulatorio, topes de aportes, anticipación del aviso, entrega) y el aviso de cambio de tasa a los titulares.
        services.AddScoped<Lending.Deposits.Lines.AltaDeVersionDeCaptacion>();
        services.AddScoped<Lending.Deposits.Lines.IContextoDeLineasDeCaptacion, Lending.Deposits.Lines.ContextoDeLineasDeCaptacion>();
        services.AddScoped<Lending.Deposits.Lines.AvisoDeCambioDeTasa>();
        // Feature 014, US2 (T073–T076): la titularidad común a la apertura y al cambio de partes, y el subpaso de la mayoría de edad del paso de
        // captaciones (el primero que se registra).
        services.AddScoped<Lending.Deposits.Accounts.TitularidadDeCuentas>();
        services.AddScoped<Lending.Deposits.Accrual.ISubpasoDeCaptaciones, Lending.Deposits.Accrual.MayoriaDeEdad>();
        // US3 (T082): las cuotas exigibles de aportes y del ahorro permanente, después de la mayoría de edad.
        services.AddScoped<Lending.Deposits.Accrual.ISubpasoDeCaptaciones, Lending.Deposits.Contributions.GeneradorDeCuotas>();
        // Feature 013 (T0284–T0289, US17): el alcance por oficina y línea (la API registra el de la petición; aquí, el que falla
        // cerrado), el salto común de oficinas que también usa UserBranchScope, el único escritor de LND_CaseParticipations, las
        // lecturas sensibles de Cartera y el último rechazo de forma de la petición (para Lending.Request.Rejected).
        services.AddScoped<Common.Security.OficinasDelUsuario>();
        services.AddScoped<Lending.Common.AlcanceDeCarteraDelActor>();
        services.AddScoped<Lending.Common.AlcanceDeCarteraDeUnUsuario>();
        services.AddScoped<Common.Integration.Accounting.IActivosLiquidosParaCartera, Accounting.Lending.ActivosLiquidosParaCartera>();
        services.TryAddScoped<Common.Interfaces.Security.IAlcanceDeCartera, Common.Interfaces.Security.AlcanceDeCarteraCerrado>();
        services.AddScoped<Lending.Segregation.RegistroDeIntervenciones>();
        services.AddScoped<Lending.Common.LendingAuditEmitter>();
        services.AddScoped<Common.Audit.RechazosDeFormaDeLaPeticion>();
        // Feature 013 (T0293–T0303, US17 bloque D): el gobierno de Cartera —la matriz vigente, los vinculados vigentes y su alta única, lo
        // que la ley fija sobre los vinculados, los asuntos de acta por sujeto (cada historia registra el suyo; dos con el mismo sujeto no
        // arman el registro), la validación única del punto de acta cerrado, el voto presencial y el PDF del acta (la API registra el de
        // QuestPDF, T0297; sin él el acta se cierra sin adjunto)—.
        // Feature 013, US1 (T0352–T0355): las reglas únicas de una versión de línea, su único escritor, el lector a (D, R) de la versión
        // vigente y el de las equivalencias de Comercial a la fecha de la venta.
        services.AddScoped<Lending.Configuration.ReglasDeLineaDeCredito>();
        // K6b (T1051, T1055): los límites de vivienda y el tope de tasa de vivienda, para la línea, la solicitud y el desembolso.
        services.AddScoped<Lending.Housing.LectorDeVivienda>();
        services.AddScoped<Lending.Configuration.AltaDeVersionDeLinea>();
        services.AddScoped<Lending.Configuration.VersionVigenteDeLinea>();
        services.AddScoped<Lending.Configuration.EquivalenciasDeComercio>();
        services.AddScoped<Lending.Attributions.MatrizVigenteDeAtribuciones>();
        services.AddScoped<Lending.Attributions.VinculadosVigentes>();
        services.AddScoped<Lending.Attributions.AltaDeVinculados>();
        services.AddScoped<Lending.Sessions.ReglasDeVinculados>();
        services.AddScoped<Lending.Sessions.AsuntosDeActa>();
        services.AddScoped<Lending.Sessions.PuntoDeActaCerrado>();
        services.AddScoped<Lending.Sessions.PresenciaEnSesion>();
        services.TryAddScoped<Lending.Sessions.IActaDeSesionEnPdf, Lending.Sessions.SinActaEnPdf>();
        services.AddScoped<Lending.Sessions.IAsuntoDeActa, Lending.Activation.AsuntoDeDiferenciasDeActivacion>();
        // Feature 013, US2 (bloque 6A; T0389–T0396): el simulador, la exposición consolidada, la capacidad de pago y de descuento, el grupo
        // conectado, la concentración, las validaciones de la originación, el enrutamiento y la evaluación de la solicitud. Los puertos que
        // las validaciones consumen —IConsultaDeListasDeCartera, IDisponibleDeCupo, IBloqueosDelDeudor— los registra el bloque 6B (abajo); el
        // renderizador del PDF de la simulación lo registra la API (QuestPDF).
        services.AddScoped<Lending.Origination.SimuladorDeCredito>();
        services.AddScoped<Lending.Origination.SimulacionEnAlcance>();
        services.AddScoped<Lending.Origination.ExposicionConsolidadaDelDeudor>();
        services.AddScoped<Lending.Origination.CapacidadDePago>();
        services.AddScoped<Lending.Origination.ConnectedGroups.GrupoConectadoDelDeudor>();
        services.AddScoped<Lending.Origination.ValidacionDeConcentracion>();
        services.AddScoped<Lending.Origination.ValidacionesDeOriginacion>();
        services.AddScoped<Lending.Origination.EnrutamientoDeSolicitud>();
        services.AddScoped<Lending.Origination.EvaluacionDeSolicitud>();
        services.AddScoped<Lending.Origination.ArmadoDeSolicitud>();
        // Feature 013, US2 (bloque 6B; T0398–T0407): la consulta de listas de Cartera, el disponible de cupo y los bloqueos (los puertos que
        // consumen las validaciones), el saldo de aportes (D19: Aportes primero, la plantilla si responde «no disponible»), los cupos y su
        // enrutamiento y revalidación, la atribución de quien decide, la decisión individual por sujeto y los asuntos de acta de la solicitud y del
        // cupo. La tarea cartera.re-escaneo-de-listas la registra Program.cs.
        // Feature 014, US3 (T084; research E33): la consulta de aportes es ya la real; responde «no disponible» hasta la activación de aportes de la
        // cooperativa, y SaldoDeAportesVigente usa entonces la plantilla (D19). ConsultaDeAportesNoDisponible queda para las pruebas.
        services.TryAddScoped<Common.Integration.Lending.IConsultaDeAportes, Lending.Integration.ConsultaDeAportesDeCartera>();
        services.AddScoped<Lending.Origination.CreditLimits.SaldoDeAportesVigente>();
        services.AddScoped<Lending.Origination.ConsultaDeListasDeCartera>();
        services.AddScoped<Lending.Origination.IConsultaDeListasDeCartera>(sp => sp.GetRequiredService<Lending.Origination.ConsultaDeListasDeCartera>());
        services.AddScoped<Lending.Origination.CreditLimits.DisponibleDeCupo>();
        services.AddScoped<Lending.Origination.IDisponibleDeCupo>(sp => sp.GetRequiredService<Lending.Origination.CreditLimits.DisponibleDeCupo>());
        services.AddScoped<Lending.Origination.CreditLimits.BloqueosDelDeudor>();
        services.AddScoped<Lending.Origination.IBloqueosDelDeudor>(sp => sp.GetRequiredService<Lending.Origination.CreditLimits.BloqueosDelDeudor>());
        services.AddScoped<Lending.Origination.CreditLimits.EnrutamientoDeCupo>();
        services.AddScoped<Lending.Origination.CreditLimits.RevalidacionDeCupos>();
        services.AddScoped<Lending.Attributions.AtribucionDelDecisor>();
        services.AddScoped<Lending.Origination.DecisionSobreSolicitud>();
        services.AddScoped<Lending.Attributions.IDecisionIndividual>(sp => sp.GetRequiredService<Lending.Origination.DecisionSobreSolicitud>());
        services.AddScoped<Lending.Origination.CreditLimits.DecisionSobreCupo>();
        services.AddScoped<Lending.Attributions.IDecisionIndividual>(sp => sp.GetRequiredService<Lending.Origination.CreditLimits.DecisionSobreCupo>());
        services.AddScoped<Lending.Sessions.IAsuntoDeActa, Lending.Origination.AsuntoDeActaDeSolicitud>();
        services.AddScoped<Lending.Sessions.IAsuntoDeActa, Lending.Origination.AsuntoDeActaDeCupo>();
        // Feature 013 (T0226): el conductor de corridas de Cartera; los IEjecutorDeCorridaDeCartera los suman US4 y US9.
        services.AddScoped<Lending.Processes.ConductorDeCorridasDeCartera>();
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
        // Feature 012, I3 (T596): el alcance por punto de venta sobre INV_UserPointOfSaleScopes, en lugar de la vacia que la API
        // registra con TryAdd (SinAsignacionesDePuntoDeVenta).
        services.AddScoped<Common.Interfaces.Security.IAsignacionesDePuntoDeVenta, Inventory.Security.Scopes.AsignacionesDePuntoDeVentaEnBase>();
        // Feature 012 (US2, T251-T258): el kardex. RegistroDeKardex es el unico escritor del kardex y sus proyecciones (con la
        // reconstruccion); las estrategias de ajuste (Scoped: recuerdan lo preparado por documento) se registran por clase; la
        // existencia real reemplaza a ExistenciasSinKardex; PosicionDeReposicion es el unico lector de la posicion de reposicion.
        services.AddScoped<Inventory.Kardex.RegistroDeKardex>();
        // Feature 012, I6 (T923): las reglas de lote, vencimiento y serie del borrador y de la confirmacion (el registro las repite en el cerrojo).
        services.AddScoped<Inventory.Documents.ReglasDeSeguimiento>();
        // I6 (T928): el ensamble de kits (grupo Adjustments); opera cuando el despliegue llega a I6.
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeEnsamble>();
        services.AddScoped<Inventory.Kardex.ReversionDeKardex>();
        services.AddScoped<Inventory.Kardex.VerificacionDeIntegridad>();
        services.AddScoped<Inventory.Kardex.ValorDeExistencias>();
        services.AddScoped<Inventory.Integration.EmisionDeInventario>();
        // Feature 012, I5 (T841): el cambio de metodo o de ambito de costeo corre dentro de AddParameterVersionCommand (la unica via).
        services.AddScoped<Common.Parameters.IEfectoDeAltaDeParametro, Inventory.Costing.CambioDeMetodoDeCosteo>();
        // Feature 012, I2 (T519-T521): el lado de Inventario de la integracion contable. Las dimensiones que Contabilidad le pide
        // a Inventario (lo unico de Inventario que conoce, T31), como se arman y emiten los mensajes de un documento, y la
        // validacion previa contable: el paso 5 de la confirmacion y la consulta /prevalidate preguntan por el mismo objeto.
        services.AddScoped<Common.Integration.Accounting.IDimensionesDeInventario, Inventory.Integration.DimensionesDeInventario>();
        services.AddScoped<Inventory.Integration.MensajesDelDocumento>();
        services.AddScoped<Inventory.Integration.ValidacionPreviaContable>();
        services.AddScoped<Inventory.Documents.IPasoDeValidacionPrevia>(sp => sp.GetRequiredService<Inventory.Integration.ValidacionPreviaContable>());
        services.AddScoped<Inventory.Replenishment.PosicionDeReposicion>();
        // Feature 012, US17 (T953, T954): la evaluación de reposición que comparten el aviso al confirmar, la revisión nocturna y
        // la vista reorder-alerts.
        services.AddScoped<Inventory.Replenishment.EvaluacionDeReposicion>();
        services.AddScoped<Inventory.Replenishment.AvisoDeReposicionAlConfirmar>();
        services.AddScoped<Inventory.Replenishment.RevisionDeReorden>();
        // Feature 012 (US3, T287-T291): el valorizado a una fecha (cierre y vista valuation), la revision del cierre y la
        // reclasificacion de grupo contable (comando y plantilla de productos).
        services.AddScoped<Inventory.Periods.ValorizadoALaFecha>();
        // Feature 012, I6, US17 (T960-T967): la analitica de inventario (margen, rotacion, consumo, sin movimiento, por vencer) que
        // comparten las vistas de I6, el motivo «sin movimiento» de impairment y el tablero.
        services.AddScoped<Inventory.Reports.AnaliticaDeInventario>();
        services.AddScoped<Inventory.Periods.RevisionDeCierre>();
        services.AddScoped<Inventory.Catalog.Products.ReclasificacionDeGrupo>();
        services.AddScoped<Inventory.Common.IExistenciasParaElCatalogo, Inventory.Kardex.ExistenciasEnKardex>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeAjustePositivo>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeAjusteNegativo>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeConsumoInterno>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeBaja>();
        // Feature 012 (US4, T309, T313; US7, T523): el saldo inicial por bodega y la activación bodega por bodega, con el cuadre
        // por conjuntos de cuentas que responde IContabilidadParaInventario.
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoSaldoInicial>();
        services.AddScoped<Inventory.GoLive.ComparacionDeActivacion>();
        // Feature 012 (US9, T338-T349): compras. El borrador del grupo Purchases (documento del proveedor, vinculos, impuestos
        // y totales en cada guardado), el calculo tributario de compra y las cuatro estrategias de I1.
        services.AddScoped<Inventory.Purchasing.Common.CalculoTributarioDeCompra>();
        services.AddScoped<Inventory.Purchasing.Common.VinculosDeCompra>();
        services.AddScoped<Inventory.Purchasing.Common.DiferenciasDePrecioDeCompra>();
        // I5 (T799, T800): los costos adicionales (flete, seguro) y su estrategia.
        services.AddScoped<Inventory.Purchasing.Common.CostosAdicionalesDeCompra>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeCostosAdicionales>();
        services.AddScoped<Inventory.Purchasing.Common.ContextoDeCompraDirecta>();
        services.AddScoped<Inventory.Documents.IBorradorDeGrupo, Inventory.Purchasing.Common.BorradorDeCompra>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoRecepcionDeCompra>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoFacturaDeProveedor>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoNotaDeProveedor>();
        // I4 (T740): el documento soporte y su nota de ajuste, sobre el modelo de la factura y la nota del proveedor.
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeDocumentoSoporte>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeNotaDeAjusteDeDocumentoSoporte>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDevolucionAProveedor>();
        services.AddScoped<Inventory.Documents.IConfirmacionEncadenada, Inventory.Purchasing.CompraDirectaEncadenada>();
        services.AddScoped<Inventory.Purchasing.LectorDeFacturaUbl>();
        // I5 (T787-T793): solicitudes y ordenes de compra (operan cuando la entrega vigente llega a I5), los pendientes por sus
        // vinculos, las reglas de la recepcion contra orden y el modelo de la orden en PDF (lo dibuja la API: IOrdenDeCompraEnPdf).
        services.AddScoped<Inventory.Purchasing.PendientesDeCompra>();
        services.AddScoped<Inventory.Purchasing.Common.RecepcionContraOrden>();
        // I5 (T794-T796): el cruce a tres vías de la factura del proveedor y la decisión de sus excepciones (SourceType PurchaseMatchLine).
        services.AddScoped<Inventory.Purchasing.CruceATresVias>();
        services.AddScoped<Common.Approvals.IFuenteDeAprobacion, Inventory.Purchasing.DecisionDeCruce>();
        services.AddScoped<Inventory.Purchasing.ModeloDeOrdenDeCompra>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeSolicitudDeCompra>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeOrdenDeCompra>();
        // Feature 012 (US10, T367-T374): traslados en dos pasos y movimiento entre ubicaciones. Las tres estrategias, el cierre de las
        // diferencias (confirma o descarta el documento que las resuelve) y su fuente de aprobación (SourceType TransferDiscrepancy).
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDespachoDeTraslado>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoRecepcionDeTraslado>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoMovimientoEntreUbicaciones>();
        services.AddScoped<Inventory.Transfers.CierreDeDiferencias>();
        services.AddScoped<Inventory.Transfers.VistaDeTraslados>();
        services.AddScoped<Common.Approvals.IFuenteDeAprobacion, Inventory.Transfers.FuenteDeAprobacionDeDiferencia>();
        services.AddScoped<Inventory.Purchasing.RevisionDeEventosRadian>();
        // Feature 012 (US11, T392-T399): conteos físicos. La estrategia del conteo (sin efecto ni mensajes), la vista, la guarda del
        // bloqueo (la llaman la confirmación y el borrador), la definición y el detalle, la regla de fecha y costo del ajuste y la fecha
        // que pone la última aprobación.
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoConteoFisico>();
        services.AddScoped<Inventory.Counts.VistaDeConteos>();
        services.AddScoped<Inventory.Counts.BloqueoPorConteo>();
        services.AddScoped<Inventory.Counts.DefinicionDeConteo>();
        services.AddScoped<Inventory.Counts.DetalleDeConteo>();
        services.AddScoped<Inventory.Counts.ReglaDelAjusteDeConteo>();
        services.AddScoped<Inventory.Documents.IAntesDeConfirmarPorAprobacion, Inventory.Counts.FechaDelAjusteDeConteo>();
        // La compra directa guarda sus dos borradores con el mismo handler del ciclo comun (sin el pipeline: un comando
        // reintentable anidado vaciaria el ChangeTracker de afuera).
        services.AddScoped<Inventory.Documents.SaveInventoryDraftCommandHandler>();
        services.AddScoped<Inventory.Documents.VistaDeDocumentos>();
        services.AddScoped<Inventory.Documents.ConfirmacionDeDocumento>();
        services.AddScoped<Common.Approvals.IFuenteDeAprobacion, Inventory.Documents.FuenteDeAprobacionDeDocumento>();
        services.AddScoped<Inventory.DocumentTypes.VistaDeTiposDeDocumento>();
        // Feature 012 (I3, T601, T602): la precificación de las líneas de venta y la aprobación de los descuentos sobre el tope, con
        // su fuente (SourceType DocumentLineDiscount), que exige al aprobador un tope suficiente y le copia aprobador y método.
        services.AddScoped<Inventory.Sales.PrecificacionDeVenta>();
        services.AddScoped<Inventory.Pricing.AprobacionDeDescuentos>();
        services.AddScoped<Common.Approvals.IFuenteDeAprobacion, Inventory.Pricing.FuenteDeAprobacionDeDescuento>();
        // Feature 012, I3 (T603-T607): el borrador del POS, los pagos del documento, la tirilla, la entrega y los eventos de riesgo.
        services.AddScoped<Inventory.Pos.BorradorDelPos>();
        services.AddScoped<Inventory.Pos.IAuditoriaDelPuntoDeVenta, Inventory.Pos.AuditoriaDelPuntoDeVenta>();
        services.AddScoped<Inventory.Sales.RegistroDePagos>();
        services.AddScoped<Inventory.Sales.ConstructorDeTirilla>();
        services.AddScoped<Inventory.Sales.EntregaDeDocumentos>();
        // Feature 012, I3 (T608-T615): la guardia fiscal (versión mínima de I3), el borrador y las reglas de las ventas de oficina, las
        // notas, las estrategias de las clases de venta (su anulación no tiene estrategia propia: AnulacionDeVenta), la alerta de venta
        // bajo costo después del guardado y el retiro gravado.
        services.AddScoped<ElectronicInvoicing.GuardiaDeEmisionFiscal>();
        services.AddScoped<Common.Persistence.TareasTrasElCommit>();
        // Feature 012, I4 (T701-T704): el único constructor del canónico y la fuente de Inventario (la plataforma no lee INV_; varias
        // fuentes pueden convivir y se eligen por SourceModule).
        services.AddScoped<ElectronicInvoicing.Canonical.ConstructorDelCanonico>();
        services.AddScoped<ElectronicInvoicing.Canonical.IFuenteDeDocumentoElectronico, Inventory.Integration.FuenteDeEmisionDeInventario>();
        // Feature 012, I5 (T803–T806): los eventos RADIAN que emite el ERP. Compras los prepara, los enlaza y registra su resultado (por el
        // puerto de la fuente); la plataforma arma el evento, lo numera (T802) y lo lleva por el procesador con la máquina simplificada.
        services.AddScoped<Inventory.Purchasing.EventosRadianDeInventario>();
        services.AddScoped<ElectronicInvoicing.Documents.CicloDelEventoRadian>();
        // Feature 012, I4 (T705, T710): el único escritor de LastIssuedNumber y los prefijos de notas de Inventario (la plataforma no lee
        // INV_: pregunta por el puerto).
        services.AddScoped<ElectronicInvoicing.Numeracion.NumeradorFiscal>();
        services.AddScoped<ElectronicInvoicing.Numeracion.IPrefijosDeModulos, Inventory.Integration.PrefijosDeInventario>();
        // Feature 012, I4 (T711-T721): el registro del documento electrónico en la confirmación, el intento contra el canal sellado
        // (emitir, consultar, transmitir contingencia) con sus artefactos, la contingencia 04, la representación, la entrega al comprador,
        // la bandeja (con el alcance que decide cada fuente) y las alertas DIAN de la tarea einvoicing.alerts. Las esperas entre intentos
        // son técnicas (ElectronicInvoicing:Retries, contracts/dian.md §6.3): estos son los defectos del contrato; T728 los lee de la sección.
        services.AddSingleton(new ElectronicInvoicing.Documents.EsperasDeReintento(
            [TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5),
             TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60)],
            TimeSpan.FromHours(1), TimeSpan.FromMinutes(2)));
        // Sin canales ni credenciales registrados (T728, T729 los agregan en AddElectronicInvoicing, que se llama después y manda), el
        // contenedor se arma igual: la validación de desarrollo exige poder construir cada manejador.
        services.TryAddScoped<ElectronicInvoicing.Channels.ICanalesDeEmision, ElectronicInvoicing.Channels.SinCanalesRegistrados>();
        services.TryAddScoped<ElectronicInvoicing.Channels.ICredencialesDeCanal, ElectronicInvoicing.Channels.SinCredencialesConfiguradas>();
        services.AddScoped<ElectronicInvoicing.Documents.RegistroDeDocumentoElectronico>();
        services.AddScoped<ElectronicInvoicing.Documents.IReconstruccionDelCanonico, ElectronicInvoicing.Documents.ReconstruccionDelCanonico>();
        services.AddScoped<ElectronicInvoicing.Documents.GuardadoDeArtefactos>();
        services.AddScoped<ElectronicInvoicing.Contingencies.ContingenciaDeLaDian>();
        services.AddScoped<ElectronicInvoicing.Documents.GeneracionDeRepresentacion>();
        services.AddScoped<ElectronicInvoicing.Documents.EntregaAlComprador>();
        services.AddScoped<ElectronicInvoicing.Documents.IntentoAnteElCanal>();
        services.AddScoped<ElectronicInvoicing.Documents.AlertasDeFacturacionElectronica>();
        services.AddScoped<ElectronicInvoicing.Documents.IConsultaDeFuenteElectronica, Inventory.Integration.ConsultaDeEmisionDeInventario>();
        // Feature 012, I4 (T722-T726): los casos a, b y c de un rechazo (la marca de la anulación sin efecto fiscal y del reemplazo que
        // leen los efectos de la venta) y las contingencias.
        services.AddScoped<Inventory.Integration.RechazoFiscalEnCurso>();
        services.AddScoped<ElectronicInvoicing.Documents.CasosDeRechazo>();
        // Feature 012, I4 (T734-T738): el flujo fiscal de la confirmación (guardia y contingencia, numeración por resolución, registro del
        // documento electrónico y subida del canónico después del commit), la espera en línea del POS y el estado electrónico que leen las
        // ventas (anular, notas, entrega).
        services.AddScoped<Inventory.Integration.EmisionFiscalDeLaConfirmacion>();
        services.AddScoped<Inventory.Pos.EsperaEnLineaDelPos>();
        services.AddScoped<Inventory.Sales.TrasladoDeVentaEnCurso>();
        services.AddScoped<Inventory.Sales.SaveCreditNoteDraftCommandHandler>();
        services.AddScoped<Inventory.Documents.IAvisoAlConfirmar, Inventory.Purchasing.PropuestaDeDocumentoSoporte>();
        services.AddScoped<Inventory.Documents.IPasoFiscalDeConfirmacion>(sp => sp.GetRequiredService<Inventory.Integration.EmisionFiscalDeLaConfirmacion>());
        services.AddScoped<Inventory.Sales.CalculoTributarioDeVenta>();
        services.AddScoped<Inventory.Sales.ReglasDeConfirmacionDeVenta>();
        // I3 (T651-T656, US6): el crédito provisional. Mientras no exista el destino Lending ni fecha en Cartera.IntegracionHabilitadaDesde,
        // las consultas a Cartera responden CarteraNoHabilitada (la entrega IC, T661, registra la real). El crédito dentro del pago, su
        // aprobación (SourceType DocumentPayment, Subject ProvisionalCredit) y la marca de lo que se está aprobando en la petición.
        // Feature 013, US6 (T0582, T0584): las consultas y las reservas de Cartera son ya las reales (Application/Lending/Integration); antes de
        // activar Cartera responden lo mismo que las provisionales (CarteraNoHabilitada / NotEnabled), así que Comercial sigue con el crédito
        // provisional hasta la activación (interfaces §5.3).
        services.AddScoped<Common.Integration.Lending.IConsultasDeCartera, Lending.Integration.ConsultasDeCartera>();
        // Feature 013 (T0152, T85): los puertos que Cartera ofrece. US5 (bloque 9B, T0550): el contrato de obligaciones y la consulta de
        // obligaciones son ya los reales (Application/Lending/Integration), Scoped sobre el IApplicationDbContext del ámbito de quien llama; antes de
        // activar responden igual que los provisionales (interfaces §3.17). Las reservas siguen «no habilitadas» hasta US6.
        services.AddScoped<Common.Integration.Lending.IConsultaDeObligaciones, Lending.Integration.ObligacionesDeCartera>();
        services.AddScoped<Common.Integration.Lending.IContratoDeObligaciones, Lending.Integration.ContratoDeObligacionesDeCartera>();
        services.AddScoped<Common.Integration.Lending.IReservasDeCartera, Lending.Integration.ReservasDeCartera>();
        // Feature 014 (T040, T083; contrato-de-cuentas §3.13, research E33): el contrato de cuentas es ya el real; responde «no habilitado» sin efectos
        // por naturaleza mientras aportes o captaciones no tengan su fecha de activación (ContratoDeCuentasSinActivar queda para las pruebas). Sus
        // piezas: la consignación, el reverso y las reglas de aportes; los validadores (Contrato/ValidadoresDelContratoDeCuentas.cs) los registra
        // AddValidatorsFromAssembly.
        services.AddScoped<Lending.Deposits.Contributions.ReglasDeAportes>();
        services.AddScoped<Lending.Deposits.Contributions.ConsignacionEnCuentas>();
        services.AddScoped<Lending.Deposits.Contributions.ReversionDeCuentas>();
        // US4 (T111, T116): las reglas comunes de los movimientos (disponible, firmas, topes, atribución, GMF, canje), el retiro, el traslado y los cargos.
        services.AddScoped<Lending.Deposits.Operations.ReglasDeMovimientos>();
        services.AddScoped<Lending.Deposits.Operations.RetiroDeCuenta>();
        services.AddScoped<Lending.Deposits.Operations.TrasladoDeCuenta>();
        services.AddScoped<Lending.Deposits.Charges.CargosDeCuentas>();
        services.AddScoped<Lending.Deposits.Charges.FacturasDeCargos>();
        // Feature 014, US14 (T208): la reactivación con su reclasificación y los subpasos de la inactividad y de las marcas del GMF que rigen desde hoy,
        // antes de los cargos periódicos (el cargo por inactividad se causa sobre la cuenta ya inactiva).
        services.AddScoped<Lending.Deposits.Accrual.ReactivacionDeCuenta>();
        services.AddScoped<Lending.Deposits.Accrual.ISubpasoDeCaptaciones, Lending.Deposits.Accrual.MarcasQueRigen>();
        services.AddScoped<Lending.Deposits.Accrual.ISubpasoDeCaptaciones, Lending.Deposits.Accrual.Inactividad>();
        // US14 (T209): los saldos no reclamados y su subpaso, después de la inactividad.
        services.AddScoped<Lending.Deposits.Unclaimed.NoReclamados>();
        services.AddScoped<Lending.Deposits.Accrual.ISubpasoDeCaptaciones, Lending.Deposits.Unclaimed.NoReclamadosDelCiclo>();
        // US14 (T210): el fallecimiento del titular —las cuentas, los beneficiarios, el reparto y la entrega— y su lectura.
        services.AddScoped<Lending.Deposits.Deceased.Fallecimientos>();
        services.AddScoped<Lending.Deposits.Deceased.VistaDeFallecimientos>();
        // US15 (T216): el fondo de liquidez, el riesgo de liquidez y los depósitos asegurados, sus fotos y el cierre del día del paso de captaciones.
        services.AddScoped<Lending.Deposits.Liquidity.FondoDeLiquidezDeCartera>();
        services.AddScoped<Lending.Deposits.Liquidity.FotosDelFondoDeLiquidez>();
        services.AddScoped<Lending.Deposits.Accrual.ICierreDelDiaDeCaptaciones, Lending.Deposits.Liquidity.FondoDeLiquidezDelCiclo>();
        // US4 (T116, T119): los subpasos de los cargos periódicos y del débito automático, después de las cuotas.
        services.AddScoped<Lending.Deposits.Accrual.ISubpasoDeCaptaciones, Lending.Deposits.Charges.CargosPeriodicos>();
        services.AddScoped<Lending.Deposits.Accrual.ISubpasoDeCaptaciones, Lending.Deposits.Operations.DebitoAutomatico>();
        // Feature 014, US11 (T189): los bloqueos y embargos —la reclasificación por la marca, lo retenido y su recálculo diario, antes de la causación (que lee
        // la suspensión por embargo total)—.
        services.AddScoped<Lending.Deposits.Holds.ReclasificacionDeCuenta>();
        services.AddScoped<Lending.Deposits.Holds.RetencionDeEmbargos>();
        services.AddScoped<Lending.Deposits.Accrual.ISubpasoDeCaptaciones, Lending.Deposits.Holds.EmbargosDelCiclo>();
        // Feature 014, US5 (T131–T135): el interés de las cuentas de captación. La causación y la liquidación son los últimos subpasos del paso de
        // captaciones (después de las cuotas, los cargos y el débito automático, que mueven los saldos del día): se causa el día con su saldo de cierre
        // y se liquida lo que venció (research E95).
        services.AddScoped<Lending.Deposits.Accrual.LectorDeInteres>();
        services.AddScoped<Lending.Deposits.Accrual.ReglasDeRetencion>();
        services.AddScoped<Lending.Deposits.Accrual.PartesDeHechosDeCaptaciones>();
        services.AddScoped<Lending.Deposits.Accrual.AjusteDeCausacionDeCuenta>();
        services.AddScoped<Lending.Deposits.Accrual.LiquidacionDeIntereses>();
        services.AddScoped<Lending.Deposits.Accrual.LiquidacionALaFecha>();
        services.AddScoped<Lending.Deposits.Accounts.CancelacionDeCuenta>();
        services.AddScoped<Lending.Deposits.Accrual.CausacionDeCaptaciones>();
        services.AddScoped<Lending.Deposits.Accrual.ISubpasoDeCaptaciones>(sp => sp.GetRequiredService<Lending.Deposits.Accrual.CausacionDeCaptaciones>());
        services.AddScoped<Lending.Deposits.Accrual.ISubpasoDeCaptaciones, Lending.Deposits.Accrual.LiquidacionPeriodicaDeIntereses>();
        // Feature 014, US6 (T172, T173): el pago de intereses, la prórroga y la salida del CDAT, y su subpaso del ciclo diario, después de la causación y de la
        // liquidación (el día ya causado se paga y vence).
        services.AddScoped<Lending.Deposits.TermDeposits.PagoDeInteresesDeCdat>();
        services.AddScoped<Lending.Deposits.TermDeposits.ProrrogaDeCdat>();
        services.AddScoped<Lending.Deposits.TermDeposits.SalidaDeCdat>();
        services.AddScoped<Lending.Deposits.Accrual.ISubpasoDeCaptaciones, Lending.Deposits.TermDeposits.CicloDelCdat>();
        // Feature 014, US7 (T182, T183): el ahorro contractual —su lectura, la constitución del contrato al abrir, sus cuotas, el incumplimiento, la bonificación y
        // el fin del contrato— y su subpaso del ciclo diario, el último (después del CDAT: el día ya causado y liquidado vence o incumple).
        services.AddScoped<Lending.Deposits.Contractual.LectorDeContractual>();
        services.AddScoped<Lending.Deposits.Contractual.AperturaDeContractual>();
        services.AddScoped<Lending.Deposits.Contractual.GeneradorDeCuotasDeContractual>();
        services.AddScoped<Lending.Deposits.Contractual.IncumplimientoDeContractual>();
        services.AddScoped<Lending.Deposits.Contractual.BonificacionDeContractual>();
        services.AddScoped<Lending.Deposits.Contractual.CierreDeContractual>();
        services.AddScoped<Lending.Deposits.Accrual.ISubpasoDeCaptaciones, Lending.Deposits.Contractual.CicloDelContractual>();
        // US4 (T111): la aprobación de un retiro o una consignación en efectivo sobre la atribución (aprobador presente, mecánica de la plataforma).
        services.AddScoped<Common.Approvals.IFuenteDeAprobacion, Lending.Deposits.Operations.FuenteDeAprobacionDeCuenta>();
        // US3 (T086): lo común de la devolución de aportes sin retiro y la cesión entre asociados.
        services.AddScoped<Lending.Deposits.Contributions.SolicitudesDeAportes>();
        // US3 (T087): la constancia y el paz y salvo de aportes, y la anulación de documentos por el reverso de una operación de cuenta.
        services.AddScoped<Lending.Deposits.Documents.DocumentosDeAportes>();
        services.AddScoped<Lending.Deposits.Documents.AnulacionDeDocumentosDeCuentas>();
        // US10 (T097–T103): el retiro del asociado —lo común del trámite, el plan de la liquidación, el cruce con la aplicación de pagos, las órdenes de
        // giro de cuentas (también el resultado de Tesorería), la liquidación emitida, el asunto de acta y el arrastre del reverso de un pago cruzado—.
        services.AddScoped<Lending.Deposits.Withdrawals.RetirosDeAsociado>();
        services.AddScoped<Lending.Deposits.Withdrawals.PlanesDeLiquidacionDeRetiro>();
        services.AddScoped<Lending.Deposits.Withdrawals.CruceDeRetiro>();
        services.AddScoped<Lending.Deposits.Operations.OrdenesDeGiroDeCuentas>();
        services.AddScoped<Lending.Deposits.Operations.RegiroDeOrdenesDeCuentas>();
        services.AddScoped<Lending.Deposits.Documents.DocumentosDeRetiro>();
        // Feature 014, US13 (T196): los documentos al titular de captaciones, la emisión en la unidad de trabajo de quien la pide, los títulos del CDAT
        // y la respuesta al juzgado como documento.
        services.AddScoped<Lending.Deposits.Documents.DocumentosDeCaptaciones>();
        services.AddScoped<Lending.Documents.EmisionDeDocumentosDeCartera>();
        services.AddScoped<Lending.Deposits.Documents.TitulosDeCdat>();
        services.AddScoped<Lending.Deposits.Documents.RespuestasAlJuzgado>();
        services.AddScoped<Lending.Sessions.IAsuntoDeActa, Lending.Deposits.Withdrawals.AsuntoDeRetiroDeAsociado>();
        // Feature 014, US12 (T203): la asamblea —revalorización, excedentes y amortización— y su asunto de acta.
        services.AddScoped<Lending.Deposits.Assembly.DistribucionesDeAsamblea>();
        services.AddScoped<Lending.Sessions.IAsuntoDeActa, Lending.Deposits.Assembly.AsuntoDeDecisionDeAsamblea>();
        services.TryAddScoped<Lending.Payments.IArrastreDeReversoDePago, Lending.Deposits.Withdrawals.ArrastreDelCruceAlRetiro>();
        // Feature 014, US6 (T170, T171): el CDAT —la apertura común (también de OpenMemberAccountCommand), la lectura y la cotización, el fondeo (por
        // traslado y por el recaudador del contrato de cuentas)— y la tasa negociada con su asunto de acta.
        services.AddScoped<Lending.Deposits.Accounts.AperturaDeCuentas>();
        services.AddScoped<Lending.Deposits.TermDeposits.LectorDeCdat>();
        services.AddScoped<Lending.Deposits.TermDeposits.CotizacionDeCdat>();
        services.AddScoped<Lending.Deposits.TermDeposits.ConstitucionDeCdat>();
        services.AddScoped<Lending.Deposits.TermDeposits.NegotiatedRates.NegociacionDeTasa>();
        services.AddScoped<Lending.Sessions.IAsuntoDeActa, Lending.Deposits.TermDeposits.NegotiatedRates.AsuntoDeTasaNegociada>();
        services.AddScoped<Lending.Integration.ContratoDeCuentasDeCartera>();
        services.AddScoped<Common.Integration.Lending.IContratoDeCuentas>(sp => sp.GetRequiredService<Lending.Integration.ContratoDeCuentasDeCartera>());
        // US6 (T0582–T0594): la verificación de listas sin escribir de la consulta, y el consumidor de Comercial con sus piezas (los validadores de
        // la reserva, Integration/Reservas, los registra AddValidatorsFromAssembly).
        services.AddScoped<Lending.Origination.IVerificacionDeListas>(sp => sp.GetRequiredService<Lending.Origination.ConsultaDeListasDeCartera>());
        services.AddScoped<Lending.Integration.Comercial.RecibosDeCartera>();
        services.AddScoped<Lending.Integration.Comercial.ConversionDeVentaACredito>();
        services.AddScoped<Lending.Integration.Comercial.EvaluacionDeVentaACredito>();
        services.AddScoped<Lending.Integration.Comercial.NacimientoDeObligacionDeVenta>();
        services.AddScoped<Lending.Integration.Comercial.ConsumoDeVentaACredito>();
        services.AddScoped<Lending.Integration.Comercial.AjusteDesdeComercial>();
        services.AddScoped<Lending.Integration.Comercial.VentasAnterioresAlCorte>();
        services.AddScoped<Lending.Migration.ConciliacionDeVentasDeLaCarga>();
        // US8 (bloque 13A, T0738–T0746): la carga de la migración —cuadre, conciliación, la carga vigente antes de activar y la revalidación de cupos—.
        services.AddScoped<Lending.Migration.CuadreDeMigracion>();
        services.AddScoped<Lending.Migration.ConciliacionDeVentasDeMigracion>();
        services.AddScoped<Lending.Migration.CargaVigenteDeCartera>();
        services.AddScoped<Lending.Migration.RevalidacionDeCuposMigrados>();
        // US5 (bloque 9B, T0538–T0548): el núcleo de los pagos, sus reglas y su auditoría. Los validadores del contrato (Contrato/
        // ValidadoresDelContrato.cs) los registra AddValidatorsFromAssembly. Los cierres de Cartera (US9) y la vigencia de documentos (US13) nacen
        // vacíos y sus historias los reemplazan.
        services.AddScoped<Lending.Integration.ITransaccionDelRecaudador, Lending.Integration.TransaccionDelRecaudador>();
        services.AddScoped<Lending.Integration.AuditoriaDeEscriturasDeCartera>();
        services.AddScoped<Lending.Integration.Contrato.NoDescontadosDelLote>();
        // US9 (bloque 12C): los cierres de Cartera ya existen; el esqueleto SinCierresDeCartera queda sólo para las pruebas.
        services.TryAddScoped<Lending.Payments.ICierresDeCartera, Lending.Risk.CierresDeCarteraVigentes>();
        // US13 (T0787): la anulación de documentos por reverso ya existe; el esqueleto SinDocumentosQueAnular queda sólo para las pruebas.
        services.TryAddScoped<Lending.Payments.IVigenciaDeDocumentosDeCartera, Lending.Documents.AnulacionDeDocumentosPorReverso>();
        // US13 (bloque 14A, T0782–T0786): los documentos al deudor. El renderizador del PDF (IDocumentoDeCarteraRenderer) lo registra la API (T0789).
        services.AddScoped<Lending.Documents.ConstructorDeDocumentosDeCartera>();
        services.AddScoped<Lending.Documents.EntregaDeDocumentosDeCartera>();
        services.AddSingleton<Lending.Documents.IGeneradorDeCodigosDeVerificacion, Lending.Documents.GeneradorDeCodigosDeVerificacion>();
        services.AddScoped<Lending.Payments.IAvisoDeAbonoConPreferencia, Lending.Payments.AvisoDeAbonoPorMensajeria>();
        services.AddScoped<Lending.Payments.ReglasDeFechaValor>();
        services.AddScoped<Lending.Payments.FirmezaDelPago>();
        services.AddScoped<Lending.Payments.PreferenciaDePrepagoVigente>();
        services.AddScoped<Lending.Payments.AplicacionDePagos>();
        services.AddScoped<Lending.Payments.ReversionDePago>();
        services.AddScoped<Lending.Payments.ResolucionDeSaldoAFavor>();
        // Feature 013 (T0227-T0229, T0233; C106, C107): el puerto de Contabilidad para Cartera en su forma provisional (US7 registra el
        // adaptador real ContabilidadParaCartera con la misma firma), las piezas tempranas de la integración contable de Cartera, su
        // único emisor y los requisitos de la activación. Los dobles de prueba (TesoreriaDePrueba) los registra la API, nunca aquí.
        // Feature 013, T0650: el adaptador real (Application/Accounting/Lending) reemplaza al provisional de la fase 3, con la misma firma (C106).
        services.AddScoped<Common.Integration.Accounting.IContabilidadParaCartera, Accounting.Lending.ContabilidadParaCartera>();
        services.AddScoped<Lending.Integration.FechaContable>();
        services.AddScoped<Lending.Integration.ModoDePasoDeCartera>();
        services.AddScoped<Lending.Integration.ValidacionPreviaContableDeCartera>();
        // T0801 (A8 = «Bloquear todo», 2026-10-14): la validación previa de las tandas de los procesos.
        services.AddScoped<Lending.Integration.ValidacionPreviaDeProceso>();
        services.AddScoped<Lending.Integration.ValidacionPreviaDeCaptaciones>();
        // Feature 014, US8 (T146): los saldos de captaciones por combinación para la conciliación.
        services.AddScoped<Lending.Integration.SaldosContablesDeCaptaciones>();
        // Feature 013, US7 (T0657, T0658): el puerto de las dimensiones de Cartera para Contabilidad (enmienda A1) y la conciliacion.
        services.AddScoped<Lending.Integration.SaldosContablesDeCartera>();
        services.AddScoped<Lending.Integration.ConciliacionContableDeCartera>();
        // T0659 (A3): los tres contingentes (aprobado sin desembolsar, cupo rotativo; la garantía la emiten sus operaciones).
        services.AddScoped<Lending.Integration.MovimientoContingenteDeCartera>();
        services.AddScoped<Common.Integration.Lending.IDimensionesDeCartera, Lending.Integration.DimensionesDeCartera>();
        services.AddScoped<Lending.Integration.CategoriaContableDeCartera>();
        services.AddScoped<Lending.Integration.EmisionDeCartera>();
        services.AddScoped<Lending.Activation.RequisitosDeActivacion>();
        // US8 (bloque 13B, T0748–T0752): la activación con cartera previa.
        services.AddScoped<Lending.Activation.RecalculoDelCuadreDeActivacion>();
        services.AddScoped<Lending.Activation.ValidacionDeCobrosProvisionales>();
        services.AddScoped<Lending.Activation.NacimientoDeCreditosMigrados>();
        services.AddScoped<Lending.Activation.NacimientoDeLoDelDeudorMigrado>();
        // Feature 013, US4 (bloque 8B): la carga de la reconstrucción, las series regulatorias, las partes de hechos, la puesta al día y el ciclo
        // diario. IMotorDeCreditoEnDesembolso (el punto de extensión de la fase 7) ya es el motor real.
        services.AddScoped<Lending.Engine.ISeriesRegulatorias, Lending.Engine.SeriesRegulatoriasDeCartera>();
        services.AddScoped<Lending.Engine.CargadorDeReconstruccion>();
        services.AddScoped<Lending.Accrual.PartesDeHechosDeCartera>();
        services.AddScoped<Lending.Accrual.PuestaAlDiaDeCredito>();
        services.AddScoped<Lending.Disbursements.IMotorDeCreditoEnDesembolso, Lending.Disbursements.MotorDeCreditoEnDesembolso>();
        services.AddScoped<Lending.Accrual.ParametrosDeLaTandaDeCartera>();
        services.AddScoped<Lending.Accrual.ReajusteDeTasa>();
        services.AddScoped<Lending.Ledger.VerificacionDelLibro>();
        services.AddScoped<Lending.Accrual.CicloDiarioDeCartera>();
        services.AddScoped<Lending.Accrual.CorreccionDeValorNacional>();
        services.AddScoped<Lending.Processes.IEjecutorDeCorridaDeCartera, Lending.Accrual.EjecutorDelCicloDiario>();
        services.AddScoped<Lending.Processes.IEjecutorDeCorridaDeCartera, Lending.Accrual.EjecutorDeLaCorreccionDeValorNacional>();
        services.AddSingleton<Common.NationalValues.IEfectoDeReplicacion, Lending.Accrual.EfectoDeReplicacionCorreccionDeValorNacional>();
        services.AddScoped<Lending.Processes.Tareas.MarcaDeTareaDiariaDeCartera>();
        // Feature 013, US9 (bloque 12B, T0699–T0711): el riesgo y los cierres. El contexto (norma, política efectiva, parámetros, indicadores,
        // perfil y modelo por segmento), la carga en bloque, la calificación informativa (también la del ciclo), los requisitos, el cierre mensual y
        // su reapertura con sus ejecutores, el cierre base, la política SARC con su asunto de acta, la obligación sobrevenida (alta del perfil y
        // réplica) y la calificación contable vigente (ya registrada arriba).
        services.AddScoped<Lending.Risk.LectorDeIndicadoresDeLaEntidad>();
        services.AddScoped<Lending.Risk.PoliticaSarcVigente>();
        services.AddScoped<Lending.Risk.LectorDelContextoDeRiesgo>();
        services.AddScoped<Lending.Risk.CargadorDeRiesgo>();
        services.AddScoped<Lending.Risk.CargadorDePerdidaEsperada>();
        services.AddScoped<Lending.Risk.CalificacionInformativa>();
        services.AddScoped<Lending.Risk.CalificacionDelCiclo>();
        services.AddScoped<Lending.Risk.RequisitosDelCierreMensual>();
        services.AddScoped<Lending.Risk.CierreMensualDeCartera>();
        services.AddScoped<Lending.Risk.ReaperturaDelCierre>();
        services.AddScoped<Lending.Risk.CierreBase>();
        services.AddScoped<Lending.Processes.IEjecutorDeCorridaDeCartera, Lending.Risk.EjecutorDelCierreMensual>();
        services.AddScoped<Lending.Processes.IEjecutorDeCorridaDeCartera, Lending.Risk.EjecutorDeLaReapertura>();
        services.AddScoped<Lending.Sessions.IAsuntoDeActa, Lending.Risk.AsuntoDeActaPoliticaSarc>();
        // K6a (T1034): el cambio voluntario del modelo de deterioro como asunto del acta del consejo.
        services.AddScoped<Lending.Sessions.IAsuntoDeActa, Lending.Risk.AsuntoDeActaCambioDelModelo>();
        services.AddScoped<Core.RegulatoryProfile.IEvaluadorDeObligacionDePerdidaEsperada, Lending.Risk.EvaluadorDeObligacionDePerdidaEsperada>();
        services.AddSingleton<Common.NationalValues.IEfectoDeReplicacion, Lending.Risk.EfectoDeReplicacionObligacionSobrevenida>();
        // Feature 013, US3 (bloque 7C, T0449) y US6 (T0586): Cartera como destino de la bandeja: ResultadoDelGiro v1 y la familia de Comercial
        // (VentaACreditoRegistrada y AjusteDeVentaACredito en toda versión, C16). El destino Lending cuenta cuando su consumidor acepta todo lo que la
        // fila CAR adopta (RegistroDeIntegracion.ImplementacionDisponible): desde US6 acepta lo que la semilla adopta y el despachador le entrega.
        services.AddScoped<Common.Integration.IDestinoDeMensajes, Lending.Integration.DestinoCartera>();
        // Feature 013, US6 (T0598; DT §2.9, CH9): Comercial como destino Inventory, el único; guarda ObligacionCreada y ObligacionRechazada v1 en
        // INV_LendingObligationResponses (número y estado, nunca saldos). La fila INV del registro lo adopta desde la semilla.
        services.AddScoped<Common.Integration.IDestinoDeMensajes, Inventory.Integration.DestinoInventario>();
        // Feature 014, US10 (T098; mensajes.md §7, research C16, E-04): Asociados recibe el retiro aprobado en la Cartera (participante ASO, que siembra
        // CaptacionesAportes).
        services.AddScoped<Common.Integration.IDestinoDeMensajes, Core.Associates.Integration.DestinoAsociados>();
        services.AddScoped<Lending.Disbursements.RequisitosDelDesembolso>();
        services.AddScoped<Lending.Disbursements.TramoDeDesembolso>();
        services.AddScoped<Lending.Disbursements.RecogidaEnElDesembolso>();
        services.AddScoped<Lending.Disbursements.ReversoDeDesembolso>();
        // Feature 013, US3 (bloque 7B): el pagaré por el depósito de valores y el archivo plano de desembolsos con el motor común.
        services.AddScoped<Lending.Disbursements.PromissoryNotes.DepositoDelPagare>();
        services.AddScoped<Lending.Disbursements.LoanDisbursementLines>();
        services.AddScoped<Lending.Disbursements.Files.PreparacionDeArchivoDeDesembolsos>();
        // Feature 013, K2 (US10, bloque B): la libranza —convenios, el archivo de descuentos con el motor común y el seguimiento de los no aplicados—.
        services.AddScoped<Lending.Payroll.ConveniosDeLibranza>();
        services.AddScoped<Lending.Payroll.Agreements.AltaDeVersionDeConvenio>();
        services.AddScoped<Lending.Payroll.Agreements.CopiaDeVersionDeConvenio>();
        services.AddScoped<Lending.Payroll.Files.PayrollDeductionLines>();
        services.AddScoped<Lending.Payroll.Files.PreparacionDeArchivoDeDescuentos>();
        services.AddScoped<Lending.Payroll.Results.SeguimientoDeNoAplicados>();
        // Feature 013, K3 (US11, bloque B): novedades (cálculo, enrutamiento, hechos, simulación, decisión individual y asunto de acta) y condonaciones.
        services.AddScoped<Lending.Novelties.CalculoDeNovedad>();
        services.AddScoped<Lending.Novelties.EnrutamientoDeNovedad>();
        services.AddScoped<Lending.Novelties.HechosDeLaNovedad>();
        services.AddScoped<Lending.Novelties.SimulacionDeNovedad>();
        services.AddScoped<Lending.Novelties.DecisionSobreNovedad>();
        services.AddScoped<Lending.Attributions.IDecisionIndividual>(sp => sp.GetRequiredService<Lending.Novelties.DecisionSobreNovedad>());
        services.AddScoped<Lending.Sessions.IAsuntoDeActa, Lending.Novelties.AsuntoDeNovedad>();
        services.AddScoped<Lending.Waivers.CondonacionesDeCartera>();
        services.AddScoped<Lending.Waivers.DecisionSobreCondonacion>();
        services.AddScoped<Lending.Attributions.IDecisionIndividual>(sp => sp.GetRequiredService<Lending.Waivers.DecisionSobreCondonacion>());
        services.AddScoped<Lending.Sessions.IAsuntoDeActa, Lending.Waivers.AsuntoDeCondonacion>();
        // Feature 013, K3 (US14, bloque B): el castigo (verificación, lote y su asunto de acta del consejo) y la evaluación de cartera (comité y consejo).
        services.AddScoped<Lending.WriteOffs.CastigosDeCartera>();
        services.AddScoped<Lending.Common.NormaPendienteDeCartera>();
        services.AddScoped<Lending.Sessions.IAsuntoDeActa, Lending.WriteOffs.AsuntoDeLoteDeCastigo>();
        services.AddScoped<Lending.Sessions.IAsuntoDeActa, Lending.Risk.Evaluations.AsuntoDeEvaluacionDeCartera>();
        services.AddScoped<Inventory.Sales.CreditoEnLaVenta>();
        services.AddScoped<Inventory.Sales.CreditosAprobadosEnCurso>();
        services.AddScoped<Inventory.Sales.AprobacionDeCredito>();
        services.AddScoped<Common.Approvals.IFuenteDeAprobacion, Inventory.Sales.FuenteDeAprobacionDeCredito>();
        services.AddScoped<Inventory.Documents.IBorradorDeGrupo, Inventory.Sales.BorradorDeVenta>();
        services.AddScoped<Inventory.Documents.IAvisoAlConfirmar, Inventory.Sales.AlertaDeVentaBajoCosto>();
        services.AddScoped<Inventory.Documents.Efectos.AnulacionDeVenta>();
        services.AddScoped<Inventory.Documents.Efectos.RetiroGravado>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoFacturaDeVenta>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDocumentoEquivalentePos>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoComprobanteDeVenta>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoNotaCreditoDeVenta>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoNotaDeAjustePos>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoNotaDeVentaNoElectronica>();
        // I6 (T877-T889): el ciclo comercial. Las reservas (único escritor de INV_Reservations y de Reserved), los vínculos del ciclo y
        // las estrategias de cotización, pedido, remisión, factura desde remisiones y nota débito. Operan cuando el despliegue llega a I6.
        services.AddScoped<Inventory.Sales.Reservas.ReservasDeInventario>();
        services.AddScoped<Inventory.Sales.CicloComercial.VinculosDelCiclo>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeCotizacion>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDePedido>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeRemision>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeFacturaDesdeRemisiones>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Documents.Efectos.EfectoDeNotaDebito>();
        // I3 (T617-T620): la caja. El grupo Cash por el ciclo común (borrador del movimiento, sus dos estrategias) y los servicios que
        // comparten las sesiones, el arqueo y los movimientos.
        services.AddScoped<Inventory.Cash.SesionesDeCaja>();
        services.AddScoped<Inventory.Cash.ReglasDeMovimientoDeCaja>();
        services.AddScoped<Inventory.Cash.ArqueoDeLaSesion>();
        services.AddScoped<Inventory.Documents.IBorradorDeGrupo, Inventory.Cash.BorradorDeMovimientoDeCaja>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Cash.EfectoMovimientoDeCaja>();
        services.AddScoped<Inventory.Documents.Efectos.IEfectoDeClase, Inventory.Cash.EfectoDiferenciaDeArqueo>();
        // Feature 012 (T49, T156): el motor común de las plantillas de importación (revisión y aplicación).
        services.AddScoped<Common.Imports.EjecutorDeImportacion>();
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
        // Feature 017: «Correo o usuario» → identidad, en un solo sitio (research R5).
        services.AddScoped<Identity.Auth.ResolutorDeIdentificador>();
        services.AddScoped<Identity.AuditoriaDeIdentidad>();
        services.AddScoped<Invitations.Services.EmisionDeInvitacion>();
        // Feature 017 (US5–US7): la exigencia de documento en las puertas y lo que se lleva a cada cooperativa.
        services.AddScoped<Identity.Auth.Documento.IExigenciaDeDocumento, Identity.Auth.Documento.ExigenciaDeDocumento>();
        services.AddScoped<Identity.Documento.PropagadorDeIdentidad>();
        services.AddScoped<Identity.Documento.CorreosDeIdentidad>();
        services.AddScoped<Common.Interfaces.Identity.ICanalDeRecuperacion, Identity.Profile.RequestPasswordReset.CanalDeRecuperacionPorCorreo>();
        services.AddScoped<Identity.Correccion.CorregirDocumento.CorreccionDeDocumento>();
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

    /// <summary>
    /// Feature 013 (T0079, T0081, T0083; T71–T74; contracts/adaptadores-y-archivos.md §2): los defectos de los canales externos. La guardia
    /// del efecto legal (con <c>CanalesExternosOptions</c>, que fija Program.cs), el correo como canal de mensajería siempre disponible, los
    /// registros de mensajería y de listas sobre los adaptadores que haya, las credenciales sin archivo, el circuito que no se abre y la
    /// carga de listas vacía. <c>AddExternalChannels</c> (sólo desde Program.cs, después de esto) suma los simulados y reemplaza
    /// credenciales y circuito; T0087 reemplaza la carga de listas.
    /// </summary>
    private static void AddCanalesExternos(IServiceCollection services)
    {
        services.AddOptions<Lending.Adapters.CanalesExternosOptions>();
        services.AddSingleton<Lending.Adapters.GuardiaDeCanalesExternos>();
        services.TryAddScoped<Common.ExternalChannels.ICredencialesDeCanalExterno, Common.ExternalChannels.SinCredencialesDeCanalExterno>();
        services.TryAddScoped<Common.ExternalChannels.ICircuitoDeCanalExterno, Common.ExternalChannels.CircuitoSiempreCerrado>();

        services.AddSingleton(new Common.ExternalChannels.CanalExternoRegistrado(
            Common.ExternalChannels.PuertosExternos.Mensajeria, Common.ExternalChannels.CodigosDeCanalExterno.Correo, typeof(Common.Messaging.CanalDeCorreo)));
        services.AddScoped<Common.Messaging.ICanalDeMensajeria, Common.Messaging.CanalDeCorreo>();
        services.AddScoped<Common.Messaging.ICanalesDeMensajeria, Common.Messaging.CanalesDeMensajeria>();
        services.AddScoped<Common.ExternalChannels.IRegistroDeCanalesExternos>(sp => sp.GetRequiredService<Common.Messaging.ICanalesDeMensajeria>());

        // Feature 013 (T0306, C108): la cola de mensajes al deudor, único escritor de LND_DebtorMessages y LND_DebtorMessageEvents. Nunca
        // guarda: encola en la unidad de trabajo de quien lo motiva. Sus tareas (cartera.mensajes, cartera.entregas) las registra Program.cs.
        services.AddScoped<Lending.Collections.ColaDeMensajesAlDeudor>();
        // Feature 013, K4 (T0943, T0944): el paso (7) del ciclo diario —la cobranza— y el ejecutor de la corrida que registra la etapa migrada (C64).
        services.AddScoped<Lending.Collections.CobranzaDelCicloDiario>();
        services.AddScoped<Lending.Processes.IEjecutorDeCorridaDeCartera, Lending.Collections.Stages.EjecutorDeEtapasMigradas>();
        // Feature 013, K5 (T0999): los layouts del SICSES y de las centrales se cargan y validan aquí, al componer: un JSON mal formado
        // impide arrancar (adaptadores-y-archivos.md §7.2).
        services.AddSingleton(Lending.Regulatory.CatalogoDeLayoutsDeCartera.Embebido);
        // Feature 013, K5 (T1002–T1004): las proyecciones del SICSES y de las centrales, la preparación común y la generación.
        services.AddScoped<Lending.Regulatory.ProyeccionSicsesCartera>();
        // Feature 014, US17 (T220): los formatos regulatorios de captaciones (SICSES de captaciones y exógena).
        services.AddScoped<Lending.Regulatory.Captaciones.ProyeccionDeCaptaciones>();
        services.AddScoped<Lending.Regulatory.ProyeccionCentralDeRiesgo>();
        services.AddScoped<Lending.Regulatory.PreparacionDeArchivoRegulatorio>();
        services.AddScoped<Lending.Regulatory.GeneracionDeArchivoRegulatorio>();

        // Feature 013 (T0087): la carga vigente de CARGA-PROPIA se lee de COR_RestrictiveListLoads/Entries.
        services.TryAddScoped<Common.Compliance.ICargaDeListasRestrictivas, Compliance.RestrictiveLists.CargaDeListasRestrictivasEnBase>();
        // Feature 013 (T0088): el deber de conservar del titular; hasta la fase 3 responde «sin obligaciones registradas».
        services.TryAddScoped<Compliance.DataSubjectRequests.IDeberDeConservacionDelTitular, Compliance.DataSubjectRequests.DeberDeConservacionPorObligaciones>();
        services.AddScoped<Common.Compliance.IProveedoresDeListas, Common.Compliance.ProveedoresDeListas>();
        services.AddScoped<Common.ExternalChannels.IRegistroDeCanalesExternos>(sp => sp.GetRequiredService<Common.Compliance.IProveedoresDeListas>());
        // Feature 013, US2 (T0400): el registro de centrales de riesgo; sin AddExternalChannels no tiene adaptadores (el defecto «sin canal»).
        services.AddScoped<Lending.Adapters.ICentralesDeRiesgo, Lending.Adapters.CentralesDeRiesgo>();
        services.AddScoped<Common.ExternalChannels.IRegistroDeCanalesExternos>(sp => sp.GetRequiredService<Lending.Adapters.ICentralesDeRiesgo>());
        // Feature 013, US3 (T0436): el registro de depósitos de valores; sin AddExternalChannels no tiene adaptadores (el defecto «sin canal»).
        services.AddScoped<Lending.Adapters.IDepositosDeValores, Lending.Adapters.DepositosDeValores>();
        services.AddScoped<Common.ExternalChannels.IRegistroDeCanalesExternos>(sp => sp.GetRequiredService<Lending.Adapters.IDepositosDeValores>());
    }
}
