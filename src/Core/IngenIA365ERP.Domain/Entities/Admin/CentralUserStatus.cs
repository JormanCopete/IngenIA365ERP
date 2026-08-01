namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>Estado global de la identidad central (independiente de membresías por tenant).</summary>
public enum CentralUserStatus
{
    Active = 0,
    Pending = 1,
    Disabled = 2,
}
