using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.SoftDelete;

/// <summary>
/// T100 — Mecánica común para restaurar una entidad <see cref="AuditableEntity"/>
/// soft-deleted, lookup-able por <c>PublicId</c>. Los handlers concretos
/// (<c>RestoreUser</c>, <c>RestoreRole</c>, <c>RestoreBranch</c>…) usan
/// este helper para no duplicar:
/// <list type="bullet">
///   <item>Lookup con <c>IgnoreQueryFilters()</c> (el filtro global oculta los soft-deleted).</item>
///   <item>Validación "ya está activa, nada que restaurar".</item>
///   <item>Reseteo de las 3 columnas de soft-delete + actualización de auditoría.</item>
/// </list>
///
/// <para>
/// El handler concreto conserva responsabilidad sobre:
///   <list type="bullet">
///     <item>Codigo de error específico (cada entidad tiene su <c>*ErrorCodes</c>).</item>
///     <item>Side-effects (cache invalidation, notificaciones, etc.).</item>
///     <item>Reseteo de otros flags propios de la entidad (ej. <c>IsActive</c>).</item>
///   </list>
/// </para>
/// </summary>
public static class SoftDeleteRestoreHelper
{
    /// <summary>
    /// Carga la entidad ignorando el filtro de soft-delete y comprueba que
    /// está en estado borrado. Devuelve la entidad lista para mutar, o un
    /// <see cref="Result{T}"/> de fallo si no existe o ya está activa.
    ///
    /// El handler concreto debe llamar <c>SaveChangesAsync</c> después de
    /// aplicar sus propios cambios.
    /// </summary>
    public static async Task<Result<TEntity>> LoadSoftDeletedAsync<TEntity>(
        DbSet<TEntity> dbSet,
        Guid publicId,
        string notFoundCode,
        string notDeletedCode,
        CancellationToken ct)
        where TEntity : AuditableEntity
    {
        var entity = await dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.PublicId == publicId, ct);

        if (entity is null)
        {
            return Result.Failure<TEntity>(notFoundCode, "Recurso no encontrado.");
        }

        if (!entity.IsDeleted)
        {
            return Result.Failure<TEntity>(notDeletedCode,
                "El recurso no está eliminado — nada que restaurar.");
        }

        return Result.Success(entity);
    }

    /// <summary>
    /// Resetea las columnas <c>IsDeleted</c>, <c>DeletedAt</c>, <c>DeletedBy</c>
    /// y propaga la auditoría de <c>UpdatedBy</c> con el actor actual. NO
    /// llama a <c>SaveChangesAsync</c>; el handler lo decide.
    /// </summary>
    public static void ApplyRestore(
        AuditableEntity entity,
        ICurrentUserService currentUser)
    {
        entity.IsDeleted = false;
        entity.DeletedAt = null;
        entity.DeletedBy = null;
        entity.UpdatedBy = currentUser.UserName ?? "SYSTEM";
    }
}
