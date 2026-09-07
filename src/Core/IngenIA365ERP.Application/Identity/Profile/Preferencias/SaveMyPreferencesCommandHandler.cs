using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Identity.Profile.Preferencias;

public sealed class SaveMyPreferencesCommandHandler(
    IAdminDbContext db,
    ICurrentCentralUserContext currentUser)
    : IRequestHandler<SaveMyPreferencesCommand, Result>
{
    public async Task<Result> Handle(SaveMyPreferencesCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return Result.Failure("Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (currentUser.Purpose != CentralJwtPurposes.Full)
            return Result.Failure("Identity.WrongTokenPurpose",
                "Este endpoint requiere purpose=full.");

        var centralUserId = currentUser.CentralUserId.Value;
        var tenantActivo = currentUser.ActiveTenantPublicId ?? Guid.Empty;
        var autor = currentUser.Email ?? centralUserId.ToString("N");

        // Una preferencia de navegación sin cooperativa activa no se puede
        // guardar: quedaría en el ámbito global y reaparecería en todas las
        // demás, que es justamente lo contrario de lo que significa.
        var navegacionSinTenant = request.Preferencias.Keys
            .Where(UserSettingKeys.PorTenant.Contains)
            .ToList();
        if (tenantActivo == Guid.Empty && navegacionSinTenant.Count > 0)
        {
            return Result.Failure(
                "Profile.Preferencias.SinCooperativaActiva",
                $"'{string.Join("', '", navegacionSinTenant)}' dependen de la cooperativa, " +
                "y el token no tiene una activa.");
        }

        var claves = request.Preferencias.Keys.ToList();

        // Se traen las filas ya existentes de una vez en lugar de consultar por
        // clave: son pocas y así el guardado es una sola ida a la base.
        var existentes = await db.UserSettings
            .Where(s => s.CentralUserId == centralUserId && claves.Contains(s.SettingKey))
            .ToListAsync(ct);

        foreach (var (clave, valor) in request.Preferencias)
        {
            var ambito = UserSettingKeys.PorTenant.Contains(clave) ? tenantActivo : Guid.Empty;
            var fila = existentes.FirstOrDefault(
                s => s.SettingKey == clave && s.TenantPublicId == ambito);

            if (fila is null)
            {
                // No se crea una fila para dejarla vacía.
                if (string.IsNullOrEmpty(valor))
                    continue;

                db.UserSettings.Add(new UserSetting
                {
                    CentralUserId = centralUserId,
                    TenantPublicId = ambito,
                    SettingKey = clave,
                    SettingValue = valor,
                    CreatedBy = autor,
                    UpdatedBy = autor,
                });
            }
            else
            {
                fila.SettingValue = string.IsNullOrEmpty(valor) ? null : valor;
                fila.UpdatedBy = autor;
            }
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
