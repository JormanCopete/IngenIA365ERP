namespace IngenIA365ERP.Application.Common.Execution;

/// <summary>
/// Los cinco nombres fijos de <c>COR_BackgroundLeases</c> (feature 012, decisiones-transversales T10,
/// data-model §0.1): uno por trabajo de fondo que opera por cooperativa. La migración siembra una fila
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

    /// <summary>Los cinco, en el orden en que los siembra la migración.</summary>
    public static IReadOnlyList<string> Todos { get; } =
        [Despacho, ReenvioDeAuditoria, FacturacionElectronica, TareasProgramadas, Correo];
}
