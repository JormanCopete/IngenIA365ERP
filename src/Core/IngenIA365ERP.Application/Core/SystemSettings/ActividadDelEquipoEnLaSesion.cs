using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.SystemSettings;

/// <summary>
/// Si la cooperativa deja que el uso del computador —en cualquier programa, no sólo en la pestaña del ERP— cuente como
/// actividad para el cierre de sesión por inactividad (pedido del dueño del 2026-11-10). Apagado por defecto: así nadie nota
/// el cambio hasta que la cooperativa lo decide. Se cambia en Parámetros del sistema; su fila la crea el sembrador
/// paramétrico y, si falta, vale apagado.
///
/// <para>
/// Lo que el navegador deja saber es sólo «hay teclado o ratón en uso» y «la pantalla está bloqueada» (Idle Detection API,
/// Chrome y Edge, con permiso de la persona); nunca qué programa se usa. Con la pantalla bloqueada no cuenta, y la duración
/// máxima de la sesión no cambia.
/// </para>
/// </summary>
public static class ActividadDelEquipoEnLaSesion
{
    public const string Clave = "Security.Session.CountDeviceActivity";

    public static async Task<bool> ActivaAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var valor = await db.SystemSettings.AsNoTracking()
            .Where(s => s.SettingKey == Clave).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return bool.TryParse(valor, out var activa) && activa;
    }
}

/// <summary>
/// Si «Abrir en ventana nueva» lleva la sesión a la ventana nueva (pedido del dueño del 2026-11-17: varias ventanas
/// independientes, como en una aplicación de escritorio). Encendido por defecto; apagado, la ventana nueva pide entrar. La
/// ventana que hereda queda en el mismo grupo que la que la abrió: la inactividad se cuenta junta, la duración máxima es la
/// misma (no se reinicia) y cerrar sesión en una cierra todas. Sólo se hereda entre ventanas del mismo navegador.
/// </summary>
public static class VentanasConSesion
{
    public const string Clave = "Security.Session.NewWindowInheritsSession";

    public static async Task<bool> HeredanAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var valor = await db.SystemSettings.AsNoTracking()
            .Where(s => s.SettingKey == Clave).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return !bool.TryParse(valor, out var heredan) || heredan;
    }
}

/// <summary>Lo que la pestaña necesita saber de la cooperativa activa para contar la inactividad.</summary>
public sealed record PoliticaDeSesionDeLaCooperativaQuery : IRequest<Result<PoliticaDeSesionDeLaCooperativaDto>>;

public sealed record PoliticaDeSesionDeLaCooperativaDto(bool CountDeviceActivity, bool NewWindowInheritsSession = true);

public sealed class PoliticaDeSesionDeLaCooperativaQueryHandler(IApplicationDbContext db)
    : IRequestHandler<PoliticaDeSesionDeLaCooperativaQuery, Result<PoliticaDeSesionDeLaCooperativaDto>>
{
    public async Task<Result<PoliticaDeSesionDeLaCooperativaDto>> Handle(
        PoliticaDeSesionDeLaCooperativaQuery request, CancellationToken ct) =>
        Result.Success(new PoliticaDeSesionDeLaCooperativaDto(
            await ActividadDelEquipoEnLaSesion.ActivaAsync(db, ct),
            await VentanasConSesion.HeredanAsync(db, ct)));
}
