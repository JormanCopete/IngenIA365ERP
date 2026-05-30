namespace IngenIA365ERP.Storage.Configuration;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool UseStartTls { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@ingenia365.local";
    public string FromName { get; set; } = "IngenIA365 ERP";

    // Backoff inicial en segundos. Los reintentos crecen ×4 con jitter.
    public int InitialBackoffSeconds { get; set; } = 1;
    public int MaxRetries { get; set; } = 3;
}
