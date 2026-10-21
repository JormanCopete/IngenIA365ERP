using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_GoverningBodies</c> (feature 013, T0213; data-model §3.5; FR-039, FR-040, T64): un órgano de decisión (consejo, junta de
/// vigilancia, comités, gerencia). Catálogo con código de la cooperativa; no es <c>COR_Committees</c>. No es hecho.
///
/// <para>
/// T0281: un solo <c>BoardOfDirectors</c> activo (<see cref="ValidarConsejoUnico"/>); un órgano que nombra un nivel vigente de la
/// matriz no se desactiva (<c>Lending.GoverningBody.InUse</c>, lo pregunta el comando con la matriz vigente); su regla de quórum y
/// mayorías es un hecho aparte (<see cref="GoverningBodyRuleVersion"/>) y sus miembros, estado con fechas
/// (<see cref="GoverningBodyMember"/>).
/// </para>
/// </summary>
public class GoverningBody : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public GoverningBodyKind Kind { get; set; }

    public bool DecidesCredit { get; set; }

    /// <summary>Prefijo del acta; el consecutivo vive en <c>LND_NumberSequences</c> (<c>SessionMinutes</c>, ámbito = el órgano; C48).</summary>
    public string MinutesPrefix { get; set; } = string.Empty;

    /// <summary>Un solo <c>BoardOfDirectors</c> activo (lo valida el comando).</summary>
    public bool IsActive { get; set; } = true;

    public const string CodigoInexistente = "Lending.GoverningBody.NotFound";
    public const string CodigoEnUso = "Lending.GoverningBody.InUse";
    public const string CodigoConsejoDuplicado = "Lending.GoverningBody.BoardAlreadyActive";

    /// <summary>
    /// Un segundo consejo de administración activo no se admite: el nivel del consejo de la matriz (FR-040) tiene que saber cuál es.
    /// </summary>
    /// <param name="otrosActivos">Los demás órganos activos de la cooperativa (sin éste).</param>
    public ProblemaDeCartera? ValidarConsejoUnico(IEnumerable<GoverningBody> otrosActivos)
    {
        if (!IsActive || Kind != GoverningBodyKind.BoardOfDirectors) return null;
        var otro = otrosActivos.FirstOrDefault(o => o.IsActive && o.Kind == GoverningBodyKind.BoardOfDirectors && o.PublicId != PublicId);
        return otro is null
            ? null
            : ProblemaDeCartera.Con(CodigoConsejoDuplicado,
                $"Ya hay un consejo de administración activo ({otro.Name}). Desactívelo antes de crear otro.", "existingName", otro.Name);
    }

    /// <summary>Desactiva el órgano (no se borra). Quien llama ya comprobó que ningún nivel vigente lo nombra.</summary>
    public void Desactivar() => IsActive = false;
}
