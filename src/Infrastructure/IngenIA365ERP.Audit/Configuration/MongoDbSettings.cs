namespace IngenIA365ERP.Audit.Configuration;

public class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "IngenIA365ERP_Audit";
    public int RetentionDays { get; set; } = 1825; // 5 years for financial
    public int AccessLogRetentionDays { get; set; } = 90;
    public int BatchSize { get; set; } = 100;
    public int FlushIntervalSeconds { get; set; } = 5;
}
