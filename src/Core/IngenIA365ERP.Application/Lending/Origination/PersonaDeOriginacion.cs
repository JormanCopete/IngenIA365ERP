using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>
/// Lo que la originación lee de una persona del maestro (feature 013, US2; FR-035, FR-036; data-model §2.1): si es asociado activo a una fecha
/// —la afiliación de <c>COR_Associates</c> sin retiro, o reingresada—, su antigüedad como asociado y su edad en meses y años, y si es empleado
/// (<c>COR_People.IsEmployee</c>, la libranza propia). Sólo lee. La regla de «retirado» es la misma que usa Comercial para el crédito en la
/// venta (estado <c>R</c> o fecha de retiro cumplida, salvo reingreso posterior); Cartera no la toma de allá porque no conoce Comercial
/// (<c>LaCarteraSoloSeTocaPorSusInterfaces</c>). (nuevo)
/// </summary>
public static class PersonaDeOriginacion
{
    /// <summary>La afiliación de la persona a la fecha.</summary>
    public sealed record Afiliacion(bool Activo, DateOnly? Desde);

    public static async Task<Afiliacion> AfiliacionAsync(IApplicationDbContext db, Person persona, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(persona);
        if (!persona.IsAssociate) return new Afiliacion(false, null);
        var a = await db.Associates.AsNoTracking().Where(x => x.PersonId == persona.Id && !x.IsDeleted)
            .Select(x => new { x.Status, x.JoinDate, x.WithdrawalDate, x.RejoinDate })
            .FirstOrDefaultAsync(ct);
        if (a is null) return new Afiliacion(false, null);
        var activo = !Retirado(a.Status, a.WithdrawalDate, a.RejoinDate, fecha);
        var desde = a.RejoinDate is { } r && r <= fecha && (a.WithdrawalDate is null || r >= a.WithdrawalDate) ? r : a.JoinDate;
        return new Afiliacion(activo, desde);
    }

    /// <summary>Retirado a la fecha: estado <c>R</c> o fecha de retiro cumplida, salvo un reingreso posterior ya cumplido.</summary>
    public static bool Retirado(string? estado, DateOnly? retiro, DateOnly? reingreso, DateOnly fecha)
    {
        var reingresado = reingreso is { } r && r <= fecha && (retiro is null || r >= retiro);
        if (reingresado) return false;
        return string.Equals(estado, "R", StringComparison.OrdinalIgnoreCase) || retiro is { } w && w <= fecha;
    }

    /// <summary>Meses cumplidos de <paramref name="desde"/> a <paramref name="hasta"/> (cero si no hay fecha o es futura).</summary>
    public static int MesesCumplidos(DateOnly? desde, DateOnly hasta)
    {
        if (desde is not { } d || d > hasta) return 0;
        var meses = (hasta.Year - d.Year) * 12 + hasta.Month - d.Month;
        if (hasta.Day < d.Day) meses--;
        return Math.Max(0, meses);
    }

    /// <summary>Años cumplidos a la fecha (nulo sin fecha de nacimiento).</summary>
    public static int? Edad(Person persona, DateOnly fecha) =>
        persona.DateOfBirth is { } n ? MesesCumplidos(n, fecha) / 12 : null;

    /// <summary>Lo que la persona pone en la banda de tasas: asociado o cliente, empleado y antigüedad como asociado.</summary>
    public static async Task<PerfilParaLaBanda> PerfilAsync(IApplicationDbContext db, Person persona, DateOnly fecha, CancellationToken ct)
    {
        var afiliacion = await AfiliacionAsync(db, persona, fecha, ct);
        return new PerfilParaLaBanda(afiliacion.Activo ? DebtorKind.Associate : DebtorKind.Customer, persona.IsEmployee,
            afiliacion.Activo ? MesesCumplidos(afiliacion.Desde, fecha) : null);
    }
}
