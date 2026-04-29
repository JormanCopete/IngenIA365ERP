namespace IngenIA365ERP.Application.Common.Interfaces;

public interface IDateTimeService
{
    DateTime UtcNow { get; }
    DateOnly TodayUtc { get; }
}
