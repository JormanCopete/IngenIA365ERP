namespace IngenIA365ERP.Caching.Configuration;

public class RedisSettings
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379,abortConnect=false";
    public string InstanceName { get; set; } = "IngenIA365ERP:";
    public int DefaultExpiryMinutes { get; set; } = 30;
}
