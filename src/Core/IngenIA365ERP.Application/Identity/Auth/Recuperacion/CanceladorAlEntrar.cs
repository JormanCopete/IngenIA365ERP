using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Auth.Recuperacion;

/// <summary>
/// Cancela cualquier recuperación en curso cuando la persona entra con normalidad.
///
/// <para>
/// <b>Si pudo entrar, no la necesitaba.</b> Y si no fue ella quien la pidió, se
/// deshace sola sin que tenga que leer ningún correo — que es la mitad de las veces
/// lo que va a pasar: el aviso llega a un buzón que no se mira a diario, y la
/// persona se entera antes por seguir usando el sistema con normalidad.
/// </para>
///
/// <para>
/// Va después de superar el segundo factor y no en el login: acertar la contraseña
/// es justamente lo que ya hizo quien pidió la recuperación, así que cancelar ahí
/// permitiría que el atacante deshiciera la solicitud de la víctima. Superar el
/// segundo factor, en cambio, sólo puede hacerlo quien todavía lo tiene.
/// </para>
/// </summary>
public interface ICanceladorDeRecuperacionesAlEntrar
{
    Task CancelarLasVivasAsync(Guid centralUserId, DateTime ahora, CancellationToken ct);
}

public sealed class CanceladorDeRecuperacionesAlEntrar(
    IAdminDbContext adminDb,
    ILogger<CanceladorDeRecuperacionesAlEntrar> logger)
    : ICanceladorDeRecuperacionesAlEntrar
{
    public async Task CancelarLasVivasAsync(
        Guid centralUserId, DateTime ahora, CancellationToken ct)
    {
        try
        {
            var vivas = await adminDb.MfaRecoveryRequests
                .Where(r => r.CentralUserId == centralUserId
                         && r.EjecutadaEn == null && r.CanceladaEn == null)
                .ToListAsync(ct);

            if (vivas.Count == 0) return;

            foreach (var solicitud in vivas)
            {
                solicitud.Cancelar(ahora, MfaRecoveryRequest.Motivos.EntroConNormalidad);
            }

            await adminDb.SaveChangesAsync(ct);

            logger.LogInformation(
                "Se cancelaron {Cuantas} recuperación(es) de segundo factor de {Id}: entró con normalidad.",
                vivas.Count, centralUserId);
        }
        catch (Exception ex)
        {
            // Un ingreso correcto NO puede caerse por esto. Lo peor que deja un
            // fallo aquí es una solicitud viva que la persona puede cancelar desde
            // su correo, o que caduca sola; tumbar el login sería mucho peor.
            logger.LogError(ex,
                "No se pudieron cancelar las recuperaciones vivas de {Id} tras un ingreso correcto.",
                centralUserId);
        }
    }
}
