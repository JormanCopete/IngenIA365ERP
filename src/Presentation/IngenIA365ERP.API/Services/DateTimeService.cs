using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.API.Services;

public class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}
