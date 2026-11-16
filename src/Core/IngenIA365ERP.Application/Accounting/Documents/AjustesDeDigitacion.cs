using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Documents;

/// <summary>
/// 2026-11-16 (pedido del dueño): cómo busca F2 en la ventana de comprobantes. Como <c>Comercial.BusquedaDeProductos</c> en Comercial,
/// pero propio de Contabilidad: <see cref="EnElCampo"/> (la lista sale debajo mientras se escribe, como siempre) o
/// <see cref="VentanaF2"/> (la cuenta sólo lee el código exacto y F2 abre una ventana para buscar por nombre). Se cambia en Parámetros
/// del sistema; su fila la crea el sembrador paramétrico.
/// </summary>
public static class BusquedaEnComprobantes
{
    public const string Clave = "Accounting.VoucherSearchMode";
    public const string EnElCampo = "EnElCampo";
    public const string VentanaF2 = "VentanaF2";

    /// <summary>Lo guardado, o <see cref="EnElCampo"/> si no se entiende: el defecto es como funcionaba antes del parámetro.</summary>
    public static string Normalizar(string? valor) =>
        string.Equals(valor?.Trim(), VentanaF2, StringComparison.OrdinalIgnoreCase) ? VentanaF2 : EnElCampo;
}

/// <summary>
/// Lo que la ventana de digitación necesita saber antes de la primera línea: el modo de F2, la sucursal que propone a quien digita (la
/// de su oficina por defecto o la principal, la misma que el servidor pone a una línea sin sucursal) y, si su alcance está restringido,
/// las únicas sucursales que puede elegir. Con una sola, la ventana la deja puesta.
/// </summary>
public sealed record AjustesDeDigitacionDto(
    string SearchMode,
    Guid? ProposedBranchPublicId,
    string? ProposedBranchName,
    IReadOnlyList<Guid>? AllowedBranchPublicIds);

public sealed record GetVoucherEntrySettingsQuery : IRequest<Result<AjustesDeDigitacionDto>>;

public sealed class GetVoucherEntrySettingsQueryValidator : AbstractValidator<GetVoucherEntrySettingsQuery>;

public sealed class GetVoucherEntrySettingsQueryHandler(IApplicationDbContext db, IUserBranchScope scope)
    : IRequestHandler<GetVoucherEntrySettingsQuery, Result<AjustesDeDigitacionDto>>
{
    public async Task<Result<AjustesDeDigitacionDto>> Handle(GetVoucherEntrySettingsQuery request, CancellationToken ct)
    {
        var modo = BusquedaEnComprobantes.Normalizar(await db.SystemSettings.AsNoTracking()
            .Where(s => s.SettingKey == BusquedaEnComprobantes.Clave).Select(s => s.SettingValue).FirstOrDefaultAsync(ct));

        var principal = await db.AccountingSetups.AsNoTracking().Where(s => !s.IsDeleted).Select(s => (int?)s.MainBranchId).FirstOrDefaultAsync(ct);
        var alcance = await scope.ObtenerAsync(ct);
        var propuesta = alcance.SucursalPorDefecto ?? (principal is { } p && alcance.Permite(p) ? p : null);
        if (propuesta is null && alcance.Restringido && alcance.Sucursales.Count == 1) propuesta = alcance.Sucursales.Single();

        var sucursal = propuesta is { } id
            ? await db.Branches.AsNoTracking().Where(b => b.Id == id && !b.IsDeleted).Select(b => new { b.PublicId, b.Name }).FirstOrDefaultAsync(ct)
            : null;

        IReadOnlyList<Guid>? permitidas = null;
        if (alcance.Restringido)
        {
            var ids = alcance.Sucursales.ToList();
            permitidas = await db.Branches.AsNoTracking().Where(b => ids.Contains(b.Id) && !b.IsDeleted).Select(b => b.PublicId).ToListAsync(ct);
        }

        return Result.Success(new AjustesDeDigitacionDto(modo, sucursal?.PublicId, sucursal?.Name, permitidas));
    }
}
