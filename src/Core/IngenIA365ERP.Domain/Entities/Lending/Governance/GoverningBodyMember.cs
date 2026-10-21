using IngenIA365ERP.Domain.Common;
using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Sessions;
using IngenIA365ERP.Domain.Lending.Governance;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_GoverningBodyMembers</c> (feature 013, T0213; data-model §3.7; FR-039, FR-040): un miembro del órgano —una persona, no
/// necesariamente usuario (I-1)— con su papel y vigencia. Estado con fechas, no hecho; una persona no tiene dos vigencias cruzadas en
/// el mismo órgano (<c>Lending.GoverningBody.MemberOverlaps</c>).
///
/// <para>
/// T0281: nace por <see cref="Nombrar"/>, que valida el papel (el suplente no es principal y nombra a un principal vigente del mismo
/// órgano; el principal no nombra a nadie) y que la persona no tenga otra vigencia cruzada en el órgano. Se retira con
/// <see cref="Terminar"/>, que sólo cierra la vigencia (no se borra). <see cref="ComoMiembro"/> lo entrega a
/// <see cref="EvaluadorDeVotacion"/>: el suplente, atado a la persona de su principal.
/// </para>
/// </summary>
public class GoverningBodyMember : AuditableEntity
{
    public int GoverningBodyId { get; set; }

    public int PersonId { get; set; }

    public BodyMemberRole Role { get; set; }

    public bool HasVote { get; set; }

    public bool IsPrincipal { get; set; }

    /// <summary>El principal de quien es suplente.</summary>
    public int? AlternateForMemberId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Nombramiento (acto externo a Cartera).</summary>
    public string? AppointmentNotes { get; set; }

    public Guid? AppointmentAttachmentPublicId { get; set; }

    public const string CodigoCruce = "Lending.GoverningBody.MemberOverlaps";
    public const string CodigoSuplenteInvalido = "Lending.GoverningBody.AlternateInvalid";
    public const string CodigoMiembroInexistente = "Lending.GoverningBody.MemberNotFound";

    /// <summary>Vigente en <paramref name="fecha"/> (los dos extremos incluidos).</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is not { } hasta || fecha <= hasta);

    /// <summary>Si las vigencias se cruzan (un día en común basta).</summary>
    public bool SeCruzaCon(DateOnly desde, DateOnly? hasta) =>
        ValidFrom <= (hasta ?? DateOnly.MaxValue) && desde <= (ValidTo ?? DateOnly.MaxValue);

    /// <summary>
    /// Lo que está mal en el nombramiento, o nulo. <paramref name="miembrosDelOrgano"/> son los miembros (vivos) del mismo órgano;
    /// <paramref name="principal"/>, el miembro del que es suplente (si lo es).
    /// </summary>
    public static ProblemaDeCartera? Validar(BorradorDeMiembro m, IEnumerable<GoverningBodyMember> miembrosDelOrgano, GoverningBodyMember? principal)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (m.ValidTo is { } hasta && hasta < m.ValidFrom)
            return ProblemaDeCartera.Con(CodigoCruce, "La vigencia termina antes de empezar.", "validTo", hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var cruce = miembrosDelOrgano.FirstOrDefault(o => o.GoverningBodyId == m.GoverningBodyId && o.PersonId == m.PersonId && !o.IsDeleted
            && o.SeCruzaCon(m.ValidFrom, m.ValidTo));
        if (cruce is not null)
            return new ProblemaDeCartera(CodigoCruce,
                "La persona ya es miembro de este órgano en esas fechas. Termine la vigencia anterior antes de nombrarla otra vez.",
                new Dictionary<string, object?>
                {
                    ["memberPublicId"] = cruce.PublicId,
                    ["validFrom"] = cruce.ValidFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["validTo"] = cruce.ValidTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                });

        var esSuplente = m.Role == BodyMemberRole.Alternate || m.AlternateForMemberId is not null;
        if (!esSuplente) return null;

        if (m.Role != BodyMemberRole.Alternate || m.IsPrincipal || m.AlternateForMemberId is null)
            return Suplente("Un suplente tiene el papel de suplente, no es principal y nombra a su principal.");
        if (principal is null || principal.Id != m.AlternateForMemberId || principal.GoverningBodyId != m.GoverningBodyId || principal.IsDeleted)
            return Suplente("El principal del suplente no es miembro de este órgano.");
        if (!principal.IsPrincipal || principal.AlternateForMemberId is not null)
            return Suplente("Un suplente lo es de un miembro principal, no de otro suplente.");
        if (principal.PersonId == m.PersonId)
            return Suplente("Una persona no es su propio suplente.");
        if (!principal.SeCruzaCon(m.ValidFrom, m.ValidTo) || m.ValidFrom < principal.ValidFrom)
            return Suplente("La vigencia del suplente cae fuera de la de su principal.");
        return null;
    }

    /// <summary>Nombra al miembro (lanza si no se validó antes).</summary>
    public static GoverningBodyMember Nombrar(BorradorDeMiembro m, IEnumerable<GoverningBodyMember> miembrosDelOrgano, GoverningBodyMember? principal)
    {
        var existentes = miembrosDelOrgano as IReadOnlyCollection<GoverningBodyMember> ?? miembrosDelOrgano.ToList();
        ReglaDeCarteraException.SiHay(Validar(m, existentes, principal));
        return new GoverningBodyMember
        {
            GoverningBodyId = m.GoverningBodyId,
            PersonId = m.PersonId,
            Role = m.Role,
            HasVote = m.HasVote,
            IsPrincipal = m.IsPrincipal,
            AlternateForMemberId = m.AlternateForMemberId,
            ValidFrom = m.ValidFrom,
            ValidTo = m.ValidTo,
            AppointmentNotes = string.IsNullOrWhiteSpace(m.AppointmentNotes) ? null : m.AppointmentNotes.Trim(),
            AppointmentAttachmentPublicId = m.AppointmentAttachmentPublicId,
        };
    }

    /// <summary>Cierra la vigencia el <paramref name="validTo"/> (no antes de que empiece, ni después de un cierre ya hecho).</summary>
    public ProblemaDeCartera? Terminar(DateOnly validTo)
    {
        if (validTo < ValidFrom)
            return ProblemaDeCartera.Con(CodigoCruce, "La vigencia no puede terminar antes de empezar.", "validFrom",
                ValidFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (ValidTo is { } hasta && hasta < validTo)
            return ProblemaDeCartera.Con(CodigoMiembroInexistente, "El miembro ya no está vigente en esa fecha.", "validTo",
                hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        ValidTo = validTo;
        return null;
    }

    /// <summary>
    /// El miembro como lo cuenta <see cref="EvaluadorDeVotacion"/>: personas de <c>COR_People</c>; el suplente nombra a la persona
    /// de su principal (<paramref name="personaDelPrincipal"/>).
    /// </summary>
    public MiembroDelOrgano ComoMiembro(int? personaDelPrincipal = null)
    {
        if (AlternateForMemberId is not null && personaDelPrincipal is null)
            throw new ArgumentException("El suplente se entrega con la persona de su principal.", nameof(personaDelPrincipal));
        return new MiembroDelOrgano(PersonId, IsPrincipal, HasVote, AlternateForMemberId is null ? null : personaDelPrincipal);
    }

    private static ProblemaDeCartera Suplente(string mensaje) => new(CodigoSuplenteInvalido, mensaje);
}
