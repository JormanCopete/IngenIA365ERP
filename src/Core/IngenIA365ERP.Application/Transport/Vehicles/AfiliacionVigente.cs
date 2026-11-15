using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Transport;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Vehicles;

/// <summary>
/// La clase de vinculación del propietario, deducida y nunca guardada (feature 020, R4; FR-016): «asociado» si la persona es asociada
/// vigente a la fecha, «externo» si no. Es la misma regla de afiliación vigente de Cartera y Comercial
/// (<c>PersonaDeOriginacion.Retirado</c>), copiada a propósito: los módulos copian la regla en vez de referenciarse. El cambio de
/// propietario sella la clase de cada lado en su evento.
/// </summary>
public static class AfiliacionVigente
{
    public static async Task<AffiliationClass> DeAsync(IApplicationDbContext db, Person persona, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(persona);
        if (!persona.IsAssociate || persona.Id == 0) return AffiliationClass.External;
        var porPersona = await DeVariasAsync(db, [persona.Id], fecha, ct);
        return porPersona.GetValueOrDefault(persona.Id, AffiliationClass.External);
    }

    /// <summary>La clase de varias personas a la vez (listados): una sola consulta.</summary>
    public static async Task<Dictionary<int, AffiliationClass>> DeVariasAsync(
        IApplicationDbContext db, IReadOnlyCollection<int> personas, DateOnly fecha, CancellationToken ct)
    {
        var resultado = personas.Distinct().ToDictionary(p => p, _ => AffiliationClass.External);
        if (resultado.Count == 0) return resultado;
        var ids = resultado.Keys.ToList();
        var asociados = await db.Associates.AsNoTracking()
            .Where(a => ids.Contains(a.PersonId) && !a.IsDeleted && a.Person!.IsAssociate)
            .Select(a => new { a.PersonId, a.Status, a.WithdrawalDate, a.RejoinDate })
            .ToListAsync(ct);
        foreach (var a in asociados)
        {
            if (!Retirado(a.Status, a.WithdrawalDate, a.RejoinDate, fecha)) resultado[a.PersonId] = AffiliationClass.Associate;
        }
        return resultado;
    }

    /// <summary>Copia de la regla de Cartera: retirado si su estado es «R» o su retiro ya pasó, salvo un reingreso posterior.</summary>
    public static bool Retirado(string? estado, DateOnly? retiro, DateOnly? reingreso, DateOnly fecha)
    {
        var reingresado = reingreso is { } r && r <= fecha && (retiro is null || r >= retiro);
        if (reingresado) return false;
        return string.Equals(estado, "R", StringComparison.OrdinalIgnoreCase) || retiro is { } w && w <= fecha;
    }

    public static string Texto(AffiliationClass clase) => clase == AffiliationClass.Associate ? "Asociado" : "Externo";
}
