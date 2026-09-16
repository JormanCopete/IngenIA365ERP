namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Sucursales a las que quien opera puede mover y consultar (feature 009, FR-035, R7). Sin
/// asignaciones en <c>SEC_UserBranchAssignments</c> no hay restricción; con asignaciones, sólo
/// las sucursales de <c>COR_Branches</c> vinculadas a esas oficinas (<c>TenantBranchPublicId</c>)
/// —y si ninguna está vinculada, el alcance queda vacío a propósito: es mejor no dejar mover que
/// dejar mover en cualquier sucursal—. La propuesta de sucursal es la asignada por defecto.
/// </summary>
public sealed record AlcanceDeSucursales(bool Restringido, IReadOnlySet<int> Sucursales, int? SucursalPorDefecto)
{
    public static readonly AlcanceDeSucursales SinRestriccion = new(false, new HashSet<int>(), null);

    public static AlcanceDeSucursales Limitado(IEnumerable<int> sucursales, int? porDefecto) =>
        new(true, sucursales.ToHashSet(), porDefecto);

    public bool Permite(int branchId) => !Restringido || Sucursales.Contains(branchId);
}

/// <summary>
/// El alcance sólo limita lo que se digita y consulta en Contabilidad: las líneas que envía un
/// módulo llevan la sucursal de la operación y no dependen de quien la ejecuta (FR-035).
/// </summary>
public interface IUserBranchScope
{
    Task<AlcanceDeSucursales> ObtenerAsync(CancellationToken ct);
}
