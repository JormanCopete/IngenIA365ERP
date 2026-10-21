using System.Globalization;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Governance;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_RelatedParties</c> (feature 013, T0213; data-model §3.12 y C118; FR-040, T42): una persona vinculada (Ley 454, art. 61) con
/// su fundamento y vigencia. Un vínculo vigente lleva el sujeto al consejo con la regla calificada. Leerla es lectura sensible
/// auditada. No es hecho; dos vigencias del mismo vínculo no se cruzan (lo valida el comando).
///
/// <para>
/// T0283: nace por <see cref="Registrar"/>: cónyuge y pariente nombran a la persona por la que existe el vínculo
/// (<c>Lending.RelatedParty.LinkRequired</c>) y el pariente, la clase y el grado; los demás fundamentos no la llevan; el porcentaje de
/// aportes es una fracción; dos vigencias del mismo vínculo (persona, fundamento y persona del vínculo) no se cruzan
/// (<c>Lending.RelatedParty.Duplicate</c>). El grado frente al límite legal lo compara el comando con el valor nacional. Se retira con
/// <see cref="Terminar"/>, que cierra la vigencia con quién y por qué.
/// </para>
/// </summary>
public class RelatedParty : AuditableEntity
{
    public const string CodigoVinculoRequerido = "Lending.RelatedParty.LinkRequired";
    public const string CodigoDuplicado = "Lending.RelatedParty.Duplicate";
    public const string CodigoInexistente = "Lending.RelatedParty.NotFound";

    public int PersonId { get; set; }

    public RelatedPartyBasis Basis { get; set; }

    /// <summary>En <c>Spouse</c> y <c>Relative</c>: la persona por la que existe el vínculo.</summary>
    public int? RelatedThroughPersonId { get; set; }

    public KinshipKind? KinshipKind { get; set; }

    /// <summary>Grado dentro del límite legal (<c>LIMITE</c>).</summary>
    public byte? KinshipDegree { get; set; }

    /// <summary>Porcentaje de aportes, como fracción (C118; plantilla <c>lending.related-parties</c>).</summary>
    public decimal? ContributionPercentage { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public RelatedPartySource Source { get; set; } = RelatedPartySource.Manual;

    public Guid? SupportAttachmentPublicId { get; set; }

    public string? Notes { get; set; }

    public int? EndedByUserId { get; set; }

    public string? EndReason { get; set; }

    /// <summary>Vigente en <paramref name="fecha"/> (los dos extremos incluidos).</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is not { } hasta || fecha <= hasta);

    /// <summary>El mismo vínculo: persona, fundamento y persona por la que existe.</summary>
    public bool EsElMismoVinculo(int personId, RelatedPartyBasis basis, int? relatedThroughPersonId) =>
        PersonId == personId && Basis == basis && RelatedThroughPersonId == relatedThroughPersonId;

    /// <summary>Lo que está mal en el vínculo, o nulo. <paramref name="existentes"/>: los vínculos (vivos) de la misma persona.</summary>
    public static ProblemaDeCartera? Validar(BorradorDeVinculado v, IEnumerable<RelatedParty> existentes)
    {
        ArgumentNullException.ThrowIfNull(v);
        if (!Enum.IsDefined(v.Basis)) throw new ArgumentException("Fundamento de vínculo desconocido.", nameof(v));
        var porOtraPersona = v.Basis is RelatedPartyBasis.Spouse or RelatedPartyBasis.Relative;
        if (porOtraPersona && v.RelatedThroughPersonId is null)
            return Vinculo("El cónyuge o el pariente se registran con la persona por la que existe el vínculo.");
        if (!porOtraPersona && (v.RelatedThroughPersonId is not null || v.KinshipKind is not null || v.KinshipDegree is not null))
            return Vinculo("Sólo el cónyuge y el pariente nombran a otra persona, clase y grado de parentesco.");
        if (v.RelatedThroughPersonId == v.PersonId)
            return Vinculo("Una persona no es vinculada por sí misma.");
        if (v.Basis == RelatedPartyBasis.Relative && (v.KinshipKind is null || v.KinshipDegree is null or 0))
            return Vinculo("El pariente lleva la clase de parentesco y el grado (desde 1).");
        if (v.ContributionPercentage is { } p && (p <= 0m || p > 1m))
            throw new ArgumentException("El porcentaje de aportes es una fracción mayor que cero y hasta uno.", nameof(v));
        if (v.ValidTo is { } fin && fin < v.ValidFrom)
            throw new ArgumentException("La vigencia termina antes de empezar.", nameof(v));

        var cruce = existentes.FirstOrDefault(e => !e.IsDeleted && e.EsElMismoVinculo(v.PersonId, v.Basis, v.RelatedThroughPersonId)
            && e.ValidFrom <= (v.ValidTo ?? DateOnly.MaxValue) && v.ValidFrom <= (e.ValidTo ?? DateOnly.MaxValue));
        return cruce is null
            ? null
            : new ProblemaDeCartera(CodigoDuplicado, "Ese vínculo ya está registrado en esas fechas. Termine el vigente antes de registrarlo otra vez.",
                new Dictionary<string, object?>
                {
                    ["relatedPartyPublicId"] = cruce.PublicId,
                    ["validFrom"] = cruce.ValidFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                });
    }

    /// <summary>Registra el vínculo (lanza si no se validó antes).</summary>
    public static RelatedParty Registrar(BorradorDeVinculado v, IEnumerable<RelatedParty> existentes)
    {
        ReglaDeCarteraException.SiHay(Validar(v, existentes));
        return new RelatedParty
        {
            PersonId = v.PersonId,
            Basis = v.Basis,
            RelatedThroughPersonId = v.RelatedThroughPersonId,
            KinshipKind = v.KinshipKind,
            KinshipDegree = v.KinshipDegree,
            ContributionPercentage = v.ContributionPercentage,
            ValidFrom = v.ValidFrom,
            ValidTo = v.ValidTo,
            Source = v.Source,
            SupportAttachmentPublicId = v.SupportAttachmentPublicId,
            Notes = string.IsNullOrWhiteSpace(v.Notes) ? null : v.Notes.Trim(),
        };
    }

    /// <summary>Cierra la vigencia el <paramref name="validTo"/>, con quién y por qué.</summary>
    public ProblemaDeCartera? Terminar(DateOnly validTo, int endedByUserId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (validTo < ValidFrom || ValidTo is { } hasta && hasta < validTo)
            return ProblemaDeCartera.Con(CodigoInexistente, "El vínculo no está vigente en esa fecha.", "validFrom",
                ValidFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        ValidTo = validTo;
        EndedByUserId = endedByUserId;
        EndReason = reason.Trim();
        return null;
    }

    private static ProblemaDeCartera Vinculo(string mensaje) => new(CodigoVinculoRequerido, mensaje);
}
