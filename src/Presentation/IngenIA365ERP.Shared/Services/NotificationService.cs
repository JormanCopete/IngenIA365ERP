namespace IngenIA365ERP.Shared.Services;

public interface INotificationService
{
    event Func<string, string, NotificationType, Task>? OnNotify;
    Task SuccessAsync(string message);
    Task ErrorAsync(string message);
    Task WarningAsync(string message);
    Task InfoAsync(string message);
}

public enum NotificationType { Success, Error, Warning, Info }

public class NotificationService : INotificationService
{
    public event Func<string, string, NotificationType, Task>? OnNotify;

    public Task SuccessAsync(string message) =>
        OnNotify?.Invoke("Operacion exitosa", message, NotificationType.Success) ?? Task.CompletedTask;

    public Task ErrorAsync(string message) =>
        OnNotify?.Invoke("Error", message, NotificationType.Error) ?? Task.CompletedTask;

    public Task WarningAsync(string message) =>
        OnNotify?.Invoke("Advertencia", message, NotificationType.Warning) ?? Task.CompletedTask;

    public Task InfoAsync(string message) =>
        OnNotify?.Invoke("Informacion", message, NotificationType.Info) ?? Task.CompletedTask;
}
