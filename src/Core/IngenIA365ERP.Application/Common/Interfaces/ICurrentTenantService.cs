namespace IngenIA365ERP.Application.Common.Interfaces;

public interface ICurrentTenantService
{
    string? TenantId { get; }
    string? TenantName { get; }
    string? Schema { get; }
    string? ConnectionString { get; }
}
