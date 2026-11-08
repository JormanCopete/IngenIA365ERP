using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Approvals;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Approvals;

/// <summary>
/// Si quien cargó o pidió confirmar el saldo inicial de inventario puede aprobarlo él mismo (decisión del dueño del
/// 2026-11-08, puesta en marcha de COOFLOPAL, donde una sola persona carga y aprueba). Apagado por defecto: la
/// segregación de FR-010 sigue valiendo para todo lo demás y para el saldo inicial de quien no lo enciende. Sólo quita
/// las exclusiones del creador y de quien pidió, y sólo cuando el nivel que se decide es el del saldo inicial
/// (<see cref="Permiso"/>). Se cambia en Parámetros del sistema; su fila la crea el sembrador paramétrico.
/// </summary>
public static class AutoaprobacionDelSaldoInicial
{
    public const string Clave = "Inventory.OpeningBalance.CreatorMayApprove";
    public const string Permiso = "Inventory.OpeningBalance.Approve";

    public static async Task<bool> PermitidaAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var valor = await db.SystemSettings.AsNoTracking()
            .Where(s => s.SettingKey == Clave).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return bool.TryParse(valor, out var permitida) && permitida;
    }

    /// <summary>¿Aplica la excepción a esta solicitud? El parámetro encendido y el nivel actual es el del saldo inicial.</summary>
    public static bool Aplica(bool permitida, ApprovalRequest solicitud) =>
        permitida && string.Equals(VistaDeSolicitudes.NivelActual(solicitud)?.PermissionCode, Permiso, StringComparison.Ordinal);
}
