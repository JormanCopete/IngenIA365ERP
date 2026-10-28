using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Identity;

/// <inheritdoc cref="IFusionDeIdentidades"/>
/// <remarks>
/// Vive aquí y no en Application porque toca a la vez las filas de Identity (<c>ADM_CentralUsers</c>), las
/// credenciales (<c>ADM_MfaCredentials</c>, fuera de <c>IAdminDbContext</c>) y las membresías, y las tres
/// cosas tienen que moverse en la misma transacción: una fusión a medias dejaría a la persona con la
/// membresía en una cuenta y el segundo factor en la otra. <c>TwoFactorEnabled</c> se deriva de TODAS las
/// credenciales activas, con la misma regla de <c>AspNetCoreIdentityProvider</c>.
/// </remarks>
public sealed class FusionDeIdentidades(AdminDbContext db) : IFusionDeIdentidades
{
    public async Task<ResultadoDeFusion> TrasladarAsync(Guid origen, Guid destino, DateTime ahora, CancellationToken ct)
    {
        var estrategia = db.Database.CreateExecutionStrategy();
        return await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var membresias = await db.TenantMemberships
                .Where(m => m.CentralUserId == origen)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.CentralUserId, destino), ct);
            var credenciales = await db.MfaCredentials
                .Where(c => c.CentralUserId == origen)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CentralUserId, destino), ct);
            var invitaciones = await db.Invitations
                .Where(i => i.CentralUserId == origen && i.Status != InvitationStatus.Accepted)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.CentralUserId, (Guid?)destino), ct);
            var historial = await db.DocumentHistory
                .Where(h => h.CentralUserId == origen)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.CentralUserId, destino), ct);

            var cuentas = await db.Users.Where(u => u.Id == origen || u.Id == destino).ToListAsync(ct);
            var deOrigen = cuentas.Single(u => u.Id == origen);
            var deDestino = cuentas.Single(u => u.Id == destino);

            // El documento: el destino conserva el suyo; si no tenía, la fusión le da el del origen. Se libera
            // aquí en el origen porque el índice único no admite dos filas con el mismo usuario ni un instante.
            var documento = (deOrigen.DocumentType, deOrigen.DocumentNumber, deOrigen.DocumentUsername);
            deOrigen.DocumentType = null;
            deOrigen.DocumentNumber = null;
            deOrigen.DocumentUsername = null;
            deOrigen.Status = (int)CentralUserStatus.Disabled;
            deOrigen.TwoFactorEnabled = false;
            deOrigen.SecurityStamp = Guid.NewGuid().ToString("N");
            deOrigen.UpdatedAt = ahora;
            await db.SaveChangesAsync(ct);

            deDestino.MfaSecret ??= deOrigen.MfaSecret;
            deDestino.TwoFactorEnabled = await db.MfaCredentials.AnyAsync(c => c.CentralUserId == destino, ct)
                || deDestino.MfaSecret is not null && deDestino.TwoFactorEnabled;
            deDestino.UpdatedAt = ahora;
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            // El documento del origen no se copia aquí: lo escribe sólo el proveedor de identidad
            // (ElUsuarioSeDerivaEnUnSoloSitio). Se devuelve para que la fusión se lo dé al destino si no tenía.
            var documentoParaElDestino = deDestino.DocumentUsername is null && documento.DocumentType is not null
                ? (documento.DocumentType, documento.DocumentNumber)
                : ((string?)null, (string?)null);
            return new ResultadoDeFusion(membresias, credenciales, invitaciones, historial,
                deDestino.DocumentUsername, documentoParaElDestino.Item1, documentoParaElDestino.Item2);
        });
    }
}
