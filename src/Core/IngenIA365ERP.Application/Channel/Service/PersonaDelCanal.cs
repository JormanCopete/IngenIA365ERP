using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Channel;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Service;

/// <summary>
/// Feature 021 (FR-026, FR-059; contracts/erp-api-de-servicio.md, «Persona»): toda ruta de servicio con <c>{personPublicId}</c> verifica
/// <b>en vivo</b> que la persona tenga la habilitación <c>Enabled</c>. Si no la tiene —o no existe, o está eliminada— la respuesta es la
/// misma, <see cref="NoHabilitada"/>, para no dejar saber cuál de los casos fue.
/// </summary>
public static class PersonaDelCanal
{
    public const string NoHabilitada = "Channel.Person.NotEnabled";

    public static Error ErrorNoHabilitada { get; } = new(NoHabilitada, "La persona no está habilitada para el canal.");

    /// <summary>La persona habilitada, con seguimiento si <paramref name="conSeguimiento"/>; nula si no lo está.</summary>
    public static async Task<Person?> HabilitadaAsync(IApplicationDbContext db, Guid personPublicId, CancellationToken ct, bool conSeguimiento = false)
    {
        var personas = conSeguimiento ? db.People.AsTracking() : db.People.AsNoTracking();
        return await (from p in personas
                      join h in db.ChannelEnablements.AsNoTracking() on p.Id equals h.PersonId
                      where p.PublicId == personPublicId && h.Status == EnablementStatus.Enabled
                      select p).FirstOrDefaultAsync(ct);
    }
}
