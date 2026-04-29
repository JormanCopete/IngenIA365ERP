namespace IngenIA365ERP.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
