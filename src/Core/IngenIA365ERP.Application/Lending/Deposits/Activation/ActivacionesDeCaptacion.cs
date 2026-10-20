using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Activation;

/// <summary>Las fechas de activación de la Cartera por naturaleza, leídas de <c>LND_Setup</c> (feature 014, T053). (nuevo)</summary>
public sealed record FechasDeActivacion(DateOnly? Creditos, DateOnly? Captaciones, DateOnly? Aportes)
{
    public static FechasDeActivacion Ninguna { get; } = new(null, null, null);

    public DateOnly? De(LineNature naturaleza) => naturaleza switch
    {
        LineNature.Placement => Creditos,
        LineNature.Deposit => Captaciones,
        LineNature.Contribution => Aportes,
        _ => null,
    };

    /// <summary>¿La naturaleza está activada (con cualquier fecha)?</summary>
    public bool Activada(LineNature naturaleza) => De(naturaleza) is not null;

    /// <summary>¿Rige la naturaleza el día <paramref name="fecha"/> (activada con fecha ≤ <paramref name="fecha"/>)?</summary>
    public bool RigeEn(LineNature naturaleza, DateOnly fecha) => De(naturaleza) is { } desde && desde <= fecha;

    /// <summary>Las naturalezas de captación (aportes y captaciones) que rigen el día <paramref name="fecha"/>.</summary>
    public IReadOnlyList<LineNature> DeCaptacionEn(DateOnly fecha) =>
        new[] { LineNature.Contribution, LineNature.Deposit }.Where(n => RigeEn(n, fecha)).ToList();

    /// <summary>¿Hay aportes o captaciones activados (con cualquier fecha)?</summary>
    public bool AlgunaDeCaptacion => Captaciones is not null || Aportes is not null;
}

/// <summary>
/// El único lector de «qué naturaleza está activada» (feature 014, T053; data-model §1.2, R21): las fechas de <c>LND_Setup</c>, que sólo escriben
/// las activaciones. Lo usan el paso de captaciones del ciclo diario, la sección de captaciones del cierre, la activación y la consulta de productos
/// de captación (que hasta T053 lo deducía de <c>LND_Activations</c>, research E22). Sólo lee, sin seguimiento. (nuevo)
/// </summary>
public static class ActivacionesDeCaptacion
{
    /// <summary>
    /// La fecha más temprana desde la que puede regir una versión nueva de una línea de captación o de aportes (respuesta 106 A del dueño, E250):
    /// hoy una vez activada la naturaleza; <b>antes de activarla</b> no hay límite, para dejar la historia de tarifas de la puesta en marcha.
    /// </summary>
    public static async Task<DateOnly> FechaMinimaDeVersionAsync(IApplicationDbContext db, LineNature naturaleza, DateOnly hoy, CancellationToken ct) =>
        (await LeerAsync(db, ct)).Activada(naturaleza) ? hoy : DateOnly.MinValue;

    public static async Task<FechasDeActivacion> LeerAsync(IApplicationDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var setup = await db.LendingSetups.AsNoTracking().Where(s => s.Id == LendingSetup.IdDeLaFilaUnica)
            .Select(s => new { s.ActivationDate, s.DepositsActivationDate, s.ContributionsActivationDate })
            .FirstOrDefaultAsync(ct);
        return setup is null
            ? FechasDeActivacion.Ninguna
            : new FechasDeActivacion(setup.ActivationDate, setup.DepositsActivationDate, setup.ContributionsActivationDate);
    }
}
