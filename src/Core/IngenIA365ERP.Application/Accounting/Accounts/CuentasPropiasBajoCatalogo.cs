using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Accounts;

/// <summary>
/// Si la cooperativa admite cuentas propias de los niveles 2 a 4 donde el catálogo ya define las suyas (implantación de COOFLOPAL,
/// decisión del dueño del 2026-11-04). Apagado por defecto: esas cuentas no existen en el catálogo oficial y quien lo enciende
/// responde por cómo se reportan. Se cambia en Parámetros del sistema; su fila la crea el sembrador paramétrico.
/// </summary>
public static class CuentasPropiasBajoCatalogo
{
    public const string Clave = "Accounting.OwnAccountsUnderCatalog";

    public static async Task<bool> PermitidasAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var valor = await db.SystemSettings.AsNoTracking()
            .Where(s => s.SettingKey == Clave).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return bool.TryParse(valor, out var permitidas) && permitidas;
    }
}
