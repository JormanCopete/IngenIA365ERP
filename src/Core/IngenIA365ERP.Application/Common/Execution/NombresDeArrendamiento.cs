namespace IngenIA365ERP.Application.Common.Execution;

/// <summary>
/// Los nombres fijos de <c>COR_BackgroundLeases</c> (feature 012, decisiones-transversales T10,
/// data-model §0.1; feature 013 suma <see cref="ProcesoDeCartera"/>): uno por trabajo de fondo que opera por cooperativa. La migración siembra una fila
/// por nombre; un nombre que no está aquí no tiene fila y nunca se toma.
/// </summary>
public static class NombresDeArrendamiento
{
    /// <summary>Despachador de mensajes (<c>DespachadorDeMensajes</c>, I2).</summary>
    public const string Despacho = "integration.dispatch";

    /// <summary>Reenviador de auditoría (<c>AuditOutboxForwarder</c>).</summary>
    public const string ReenvioDeAuditoria = "audit.forward";

    /// <summary>Procesador de documentos electrónicos DIAN (<c>ProcesadorDeDocumentosElectronicos</c>, I4).</summary>
    public const string FacturacionElectronica = "einvoicing.process";

    /// <summary>Tareas programadas (<c>ProgramadorDeTareas</c>).</summary>
    public const string TareasProgramadas = "scheduled.tasks";

    /// <summary>Despachador de correo de las notificaciones (<c>NotificationEmailDispatcher</c>).</summary>
    public const string Correo = "email.dispatch";

    /// <summary>
    /// Procesador de Cartera (<c>ProcesadorDeCartera</c>; feature 013, T5, T0225): ciclo diario, cierre, reapertura y corrección por
    /// valor nacional. La siembra <c>PlataformaParaCartera</c>.
    /// </summary>
    public const string ProcesoDeCartera = "lending.process";

    /// <summary>Todos, en el orden en que los siembran las migraciones (los cinco de la 012 y el de Cartera).</summary>
    public static IReadOnlyList<string> Todos { get; } =
        [Despacho, ReenvioDeAuditoria, FacturacionElectronica, TareasProgramadas, Correo, ProcesoDeCartera];
}
