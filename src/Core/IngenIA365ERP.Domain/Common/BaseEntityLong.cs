namespace IngenIA365ERP.Domain.Common;

public abstract class BaseEntityLong
{
    public long Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    // Concurrencia optimista (FR-022). Mapeado a SQL Server `rowversion`/`timestamp`
    // por BaseEntityConfigurationExtensions.ConfigureRowVersion via convención.
    public byte[] RowVersion { get; set; } = [];

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(IDomainEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
