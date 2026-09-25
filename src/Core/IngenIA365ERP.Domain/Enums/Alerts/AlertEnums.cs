namespace IngenIA365ERP.Domain.Enums.Alerts;

// Alertas de la plataforma (feature 012, T39, T091; decisiones-transversales §2.5; data-model §22). Se guardan como
// int y un valor nunca se renumera.

/// <summary>Estado compartido de una alerta: la atiende una persona (o el proceso) y queda atendida para todos.</summary>
public enum AlertStatus { Pending = 0, Attended = 1 }

public enum AlertSeverity { Info = 1, Warning = 2, Critical = 3 }

/// <summary>Por dónde se entrega: la notificación en la aplicación siempre va; el correo es opcional.</summary>
[Flags]
public enum AlertChannels { InApp = 1, Email = 2 }
