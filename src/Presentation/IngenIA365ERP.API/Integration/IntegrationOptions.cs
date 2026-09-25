namespace IngenIA365ERP.API.Integration;

/// <summary>
/// Sección <c>Integration</c> de <c>appsettings.json</c> (feature 012, T049; decisiones-transversales T10,
/// T47): la configuración técnica de los trabajos de fondo. Se valida al arrancar
/// (<see cref="Problemas"/>, <c>ValidateOnStart</c> en <c>Program.cs</c>): un cero aquí no se descubre
/// hasta que el correo deja de salir.
///
/// <para>
/// Cada trabajo se apaga con su <c>Enabled</c>; la fixture de pruebas los apaga todos y conduce cada
/// pasada a mano. El despachador de mensajes que lee <see cref="Dispatcher"/> y <see cref="Retries"/> es de
/// I2; la fase 12 (US7) <b>amplía</b> esta clase (p. ej. <c>LateToleranceMinutes</c>), no la crea.
/// </para>
/// </summary>
public sealed class IntegrationOptions
{
    public const string SectionName = "Integration";

    public DispatcherOptions Dispatcher { get; set; } = new();
    public RetriesOptions Retries { get; set; } = new();
    public JobOptions AuditForwarder { get; set; } = new();
    public ScheduledJobOptions ScheduledTasks { get; set; } = new() { IntervalSeconds = 60 };
    public ScheduledJobOptions EmailDispatcher { get; set; } = new() { IntervalSeconds = 15 };

    /// <summary>Lo que está mal, con la clave completa; vacío si todo está bien.</summary>
    public IReadOnlyList<string> Problemas()
    {
        var problemas = new List<string>();
        void Positivo(int valor, string clave)
        {
            if (valor <= 0) problemas.Add($"{SectionName}:{clave} tiene que ser mayor que cero (vale {valor}).");
        }

        Positivo(Dispatcher.IntervalSeconds, "Dispatcher:IntervalSeconds");
        Positivo(Dispatcher.BudgetSeconds, "Dispatcher:BudgetSeconds");
        Positivo(Dispatcher.LeaseTtlSeconds, "Dispatcher:LeaseTtlSeconds");
        Positivo(Dispatcher.BatchSize, "Dispatcher:BatchSize");
        if (Dispatcher.LeaseTtlSeconds > 0 && Dispatcher.BudgetSeconds > 0 && Dispatcher.LeaseTtlSeconds < Dispatcher.BudgetSeconds)
        {
            problemas.Add(
                $"{SectionName}:Dispatcher:LeaseTtlSeconds ({Dispatcher.LeaseTtlSeconds}) no puede ser menor que BudgetSeconds " +
                $"({Dispatcher.BudgetSeconds}): el arrendamiento vencería a mitad del presupuesto y otra réplica entraría a la misma cooperativa.");
        }

        Positivo(Retries.BaseDelaySeconds, "Retries:BaseDelaySeconds");
        Positivo(Retries.MaxDelayMinutes, "Retries:MaxDelayMinutes");
        Positivo(Retries.AlertAfterAttempts, "Retries:AlertAfterAttempts");
        Positivo(Retries.AlertAfterMinutes, "Retries:AlertAfterMinutes");
        if (Retries.BaseDelaySeconds > 0 && Retries.MaxDelayMinutes > 0 && Retries.BaseDelaySeconds > Retries.MaxDelayMinutes * 60)
            problemas.Add($"{SectionName}:Retries:BaseDelaySeconds no puede superar el tope MaxDelayMinutes.");

        Positivo(ScheduledTasks.IntervalSeconds, "ScheduledTasks:IntervalSeconds");
        Positivo(EmailDispatcher.IntervalSeconds, "EmailDispatcher:IntervalSeconds");
        return problemas;
    }

    /// <summary>El despachador de mensajes (I2): sondeo, presupuesto por cooperativa, arrendamiento y tanda.</summary>
    public sealed class DispatcherOptions
    {
        public bool Enabled { get; set; } = true;
        public int IntervalSeconds { get; set; } = 5;
        public int BudgetSeconds { get; set; } = 60;
        public int LeaseTtlSeconds { get; set; } = 120;
        public int BatchSize { get; set; } = 100;
    }

    /// <summary>
    /// Reintentos de las entregas transitorias: espera <c>min(base·2^(n−1), tope)</c> + jitter y alerta
    /// <c>Integracion.MensajeSinEntregar</c> a los N intentos o M minutos, lo que llegue primero (T10).
    /// </summary>
    public sealed class RetriesOptions
    {
        public int BaseDelaySeconds { get; set; } = 15;
        public int MaxDelayMinutes { get; set; } = 15;
        public int AlertAfterAttempts { get; set; } = 3;
        public int AlertAfterMinutes { get; set; } = 15;
    }

    /// <summary>Un trabajo que sólo se enciende o se apaga.</summary>
    public class JobOptions
    {
        public bool Enabled { get; set; } = true;
    }

    /// <summary>Un trabajo con su intervalo entre pasadas.</summary>
    public sealed class ScheduledJobOptions : JobOptions
    {
        public int IntervalSeconds { get; set; } = 60;
    }
}
