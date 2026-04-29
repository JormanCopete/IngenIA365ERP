using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IngenIA365ERP.Audit.Models;

public class AuditLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Module { get; set; } = string.Empty;

    public BsonDocument? OldValues { get; set; }
    public BsonDocument? NewValues { get; set; }
    public List<string>? ChangedFields { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Endpoint { get; set; }
    public string? HttpMethod { get; set; }
    public int? HttpStatusCode { get; set; }
    public long DurationMs { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public BsonDocument? Metadata { get; set; }
}
