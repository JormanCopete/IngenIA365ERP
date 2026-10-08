using System.Globalization;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Governance;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_AttributionMatrices</c> (feature 013, T0213; data-model §3.1; FR-038, T63): una versión de la matriz de atribuciones,
/// publicada completa con sus niveles (<see cref="AttributionLevel"/>) y reglas (<see cref="AttributionRule"/>). Hecho de sólo
/// inserción (<see cref="IHechoInmutable"/>, C42) que se lee con <see cref="VigenciaBitemporal"/>; se sella en cada solicitud y en
/// cada sujeto que se enruta. Debe terminar en un nivel colegiado sin techo (<c>Lending.Attribution.MatrixWithoutTopLevel</c>).
///
/// <para>
/// T0280: nace sólo por <see cref="Publicar"/>, que valida la matriz <b>completa</b> (<see cref="Validar"/>, data-model §3.1–§3.3) y
/// sella <see cref="ContentSha256"/> sobre la fecha, los niveles y las reglas (<see cref="Huella"/>). Las tablas no tienen
/// navegaciones, así que la publicación devuelve la matriz y sabe armar sus hijas cuando la base le asigna el Id
/// (<see cref="MatrizPublicada.Niveles"/>, <see cref="MatrizPublicada.Reglas"/>): quien publica guarda la matriz, después los
/// niveles y por último las reglas, en una sola transacción.
/// </para>
/// </summary>
public class AttributionMatrix : AuditableEntity, IHechoInmutable, IVersionDeCartera
{
    public const string CodigoSinNivelFinal = "Lending.Attribution.MatrixWithoutTopLevel";
    public const string CodigoInvalida = "Lending.Attribution.MatrixInvalid";
    public const string CodigoNivelDesconocido = "Lending.Attribution.LevelUnknown";
    public const string CodigoOrganoInexistente = "Lending.GoverningBody.NotFound";

    /// <summary>El prefijo de los permisos de los niveles individuales.</summary>
    public const string PrefijoDePermisoDeNivel = "Lending.Decisions.";

    /// <summary>Los cuatro permisos que puede llevar un nivel individual (data-model §3.2).</summary>
    public static IReadOnlyList<string> PermisosDeNivel { get; } =
    [
        PrefijoDePermisoDeNivel + "Analyst",
        PrefijoDePermisoDeNivel + "Coordinator",
        PrefijoDePermisoDeNivel + "Management",
        PrefijoDePermisoDeNivel + "Exceptions",
    ];

    public int VersionNumber { get; init; }

    /// <summary>Desde cuándo rige; no anterior a <c>HoyLocal</c> al publicar.</summary>
    public DateOnly ValidFrom { get; init; }

    public DateTime RegisteredAt { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>Fuente: el reglamento de crédito.</summary>
    public string? LegalSource { get; init; }

    /// <summary>Punto de acta que la aprobó, si la aprobó un órgano.</summary>
    public int? SessionItemId { get; init; }

    /// <summary>Huella canónica de la matriz con sus niveles y reglas.</summary>
    public string ContentSha256 { get; init; } = string.Empty;

    public int RegisteredByUserId { get; init; }

    // ------------------------------------------------------------------------------------------------------ fábrica --

    /// <summary>
    /// Lo que está mal en la matriz, o nulo si se puede publicar. Reglas (data-model §3.1–§3.3, api.md §3.5.1): al menos un nivel;
    /// códigos y rangos únicos; un nivel individual lleva uno de <see cref="PermisosDeNivel"/> y ningún órgano, y uno colegiado un
    /// órgano y ningún permiso; el nivel más alto es colegiado y sin techo, y es el único sin techo
    /// (<see cref="CodigoSinNivelFinal"/>); exactamente un nivel de excepción y exactamente un consejo, colegiado; cada regla nombra
    /// un sujeto de <see cref="LendingDecisionSubjects"/> y un nivel de la misma matriz (<see cref="CodigoNivelDesconocido"/>), con
    /// órdenes consecutivos desde 1 y rangos bien formados; <c>ValidFrom</c> no anterior a <paramref name="hoy"/>. Con
    /// <paramref name="organos"/> comprueba además que cada órgano exista, decida crédito y esté activo
    /// (<see cref="CodigoOrganoInexistente"/>) y que el del consejo sea un <c>BoardOfDirectors</c>.
    /// </summary>
    public static ProblemaDeCartera? Validar(BorradorDeMatriz borrador, DateOnly hoy, IReadOnlyDictionary<int, OrganoDeMatriz>? organos = null)
    {
        ArgumentNullException.ThrowIfNull(borrador);
        var niveles = borrador.Niveles;
        if (niveles.Count == 0) return SinNivelFinal("La matriz no tiene niveles: agregue al menos el nivel colegiado final, sin techo.");
        if (borrador.ValidFrom < hoy)
            return Invalida("ValidFromInPast", $"La matriz no puede regir desde {Fecha(borrador.ValidFrom)}, antes de hoy ({Fecha(hoy)}).");

        if (niveles.Any(n => string.IsNullOrWhiteSpace(n.Code) || string.IsNullOrWhiteSpace(n.Name)))
            return Invalida("LevelWithoutName", "Cada nivel lleva código y nombre.");
        if (borrador.Reglas.Any(r => string.IsNullOrWhiteSpace(r.MinimumLevelCode)))
            return Invalida("RuleWithoutLevel", "Cada regla nombra el nivel mínimo que exige.");
        if (niveles.Select(n => n.Code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != niveles.Count)
            return Invalida("DuplicateLevelCode", "Dos niveles tienen el mismo código: cada nivel lleva el suyo.");
        if (niveles.Select(n => n.Rank).Distinct().Count() != niveles.Count)
            return Invalida("DuplicateRank", "Dos niveles tienen el mismo rango: el rango ordena los niveles y no se repite.");

        foreach (var nivel in niveles)
        {
            if (nivel.MaxConsolidatedAmount is <= 0m)
                return Invalida("CeilingNotPositive", $"El techo del nivel {nivel.Code} debe ser mayor que cero.");

            switch (nivel.Kind)
            {
                case AttributionLevelKind.Individual:
                    if (nivel.GoverningBodyId is not null || nivel.PermissionCode is null
                        || !PermisosDeNivel.Contains(nivel.PermissionCode, StringComparer.Ordinal))
                        return Invalida("IndividualLevelPermission",
                            $"El nivel individual {nivel.Code} lleva uno de los permisos de decisión de Cartera ({string.Join(", ", PermisosDeNivel)}) y ningún órgano.");
                    break;
                case AttributionLevelKind.Collegiate:
                    if (nivel.GoverningBodyId is null || nivel.PermissionCode is not null)
                        return Invalida("CollegiateLevelBody", $"El nivel colegiado {nivel.Code} lleva un órgano y ningún permiso.");
                    break;
                default:
                    return Invalida("LevelKindUnknown", $"El nivel {nivel.Code} no es individual ni colegiado.");
            }
        }

        var alto = niveles.MaxBy(n => n.Rank)!;
        if (alto.Kind != AttributionLevelKind.Collegiate || alto.MaxConsolidatedAmount is not null)
            return SinNivelFinal($"El nivel más alto ({alto.Code}) debe ser colegiado y sin techo, para que ninguna solicitud quede sin quién la decida.");
        var sinTecho = niveles.Where(n => n.MaxConsolidatedAmount is null && n != alto).Select(n => n.Code).ToList();
        if (sinTecho.Count > 0)
            return Invalida("CeilingMissing", $"Sólo el nivel más alto va sin techo; ponga techo a {string.Join(", ", sinTecho)}.");

        var excepcion = niveles.Count(n => n.IsExceptionLevel);
        if (excepcion != 1)
            return Invalida("ExceptionLevelCount", $"La matriz lleva exactamente un nivel de excepción; tiene {excepcion}.");
        var consejos = niveles.Where(n => n.IsBoardLevel).ToList();
        if (consejos.Count != 1)
            return Invalida("BoardLevelCount", $"La matriz lleva exactamente un nivel de consejo; tiene {consejos.Count}.");
        if (consejos[0].Kind != AttributionLevelKind.Collegiate)
            return Invalida("BoardLevelNotCollegiate", $"El nivel de consejo ({consejos[0].Code}) es colegiado.");

        if (organos is not null)
        {
            foreach (var nivel in niveles.Where(n => n.GoverningBodyId is not null))
            {
                if (!organos.TryGetValue(nivel.GoverningBodyId!.Value, out var organo) || !organo.DecidesCredit || !organo.IsActive)
                    return ProblemaDeCartera.Con(CodigoOrganoInexistente,
                        $"El órgano del nivel {nivel.Code} no existe, no está activo o no decide crédito.", "levelCode", nivel.Code);
                if (nivel.IsBoardLevel && organo.Kind != GoverningBodyKind.BoardOfDirectors)
                    return Invalida("BoardLevelNotBoard", $"El nivel de consejo ({nivel.Code}) es del consejo de administración.");
            }
        }

        var reglas = borrador.Reglas;
        var ordenes = reglas.Select(r => r.Order).OrderBy(o => o).ToList();
        if (!ordenes.SequenceEqual(Enumerable.Range(1, reglas.Count)))
            return Invalida("RuleOrderNotConsecutive", "Las reglas se numeran 1, 2, 3… sin saltos ni repetidos.");
        var codigos = niveles.Select(n => n.Code.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var regla in reglas)
        {
            if (!LendingDecisionSubjects.Existe(regla.Subject))
                return Invalida("RuleSubjectUnknown", $"La regla {regla.Order.ToString(Inv)} nombra un sujeto que Cartera no decide («{regla.Subject}»).");
            if (!codigos.Contains(regla.MinimumLevelCode.Trim()))
                return ProblemaDeCartera.Con(CodigoNivelDesconocido,
                    $"La regla {regla.Order.ToString(Inv)} exige el nivel «{regla.MinimumLevelCode}», que no está en la matriz.",
                    "levelCode", regla.MinimumLevelCode);
            if (regla.MinConsolidatedAmount > regla.MaxConsolidatedAmount || regla.MinTermMonths > regla.MaxTermMonths
                || regla.MinDaysPastDue > regla.MaxDaysPastDue
                || regla.MinConsolidatedAmount < 0m || regla.MinTermMonths < 0 || regla.MinDaysPastDue < 0)
                return Invalida("RuleRangeInvalid", $"La regla {regla.Order.ToString(Inv)} tiene un rango con el mínimo por encima del máximo o negativo.");
        }

        return null;
    }

    /// <summary>
    /// Publica la versión <paramref name="versionNumber"/> de la matriz: la valida entera (lanza <see cref="ReglaDeCarteraException"/>
    /// si quien llama no lo hizo) y sella <see cref="ContentSha256"/>.
    /// </summary>
    public static MatrizPublicada Publicar(
        BorradorDeMatriz borrador, int versionNumber, DateOnly hoy, DateTime registeredAt, int registeredByUserId,
        IReadOnlyDictionary<int, OrganoDeMatriz>? organos = null)
    {
        ReglaDeCarteraException.SiHay(Validar(borrador, hoy, organos));
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNumber, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(borrador.Reason);

        var matriz = new AttributionMatrix
        {
            VersionNumber = versionNumber,
            ValidFrom = borrador.ValidFrom,
            RegisteredAt = registeredAt,
            Reason = borrador.Reason.Trim(),
            LegalSource = string.IsNullOrWhiteSpace(borrador.LegalSource) ? null : borrador.LegalSource.Trim(),
            SessionItemId = borrador.SessionItemId,
            RegisteredByUserId = registeredByUserId,
            ContentSha256 = Huella(borrador),
        };
        return new MatrizPublicada(matriz, borrador);
    }

    /// <summary>
    /// La huella canónica de la matriz (data-model §0.13): fecha desde la que rige, niveles por rango y reglas por orden, con el nivel
    /// mínimo por su código. El motivo, la fuente y el número de versión no son contenido.
    /// </summary>
    public static string Huella(BorradorDeMatriz borrador) =>
        new HuellaDeCartera()
            .Fecha("validFrom", borrador.ValidFrom)
            .Lista("levels", borrador.Niveles.OrderBy(n => n.Rank), (h, n) => h
                .Texto("code", n.Code.Trim().ToUpperInvariant())
                .Texto("name", n.Name.Trim())
                .Entero("rank", n.Rank)
                .Enumeracion("kind", (AttributionLevelKind?)n.Kind)
                .Texto("permissionCode", n.PermissionCode)
                .Entero("governingBodyId", n.GoverningBodyId)
                .Decimal("maxConsolidatedAmount", n.MaxConsolidatedAmount)
                .Logico("isExceptionLevel", n.IsExceptionLevel)
                .Logico("isBoardLevel", n.IsBoardLevel))
            .Lista("rules", borrador.Reglas.OrderBy(r => r.Order), (h, r) => h
                .Entero("order", r.Order)
                .Texto("subject", r.Subject)
                .Entero("creditLineId", r.CreditLineId)
                .Texto("modalityCode", string.IsNullOrWhiteSpace(r.ModalityCode) ? null : r.ModalityCode.Trim())
                .Decimal("minConsolidatedAmount", r.MinConsolidatedAmount)
                .Decimal("maxConsolidatedAmount", r.MaxConsolidatedAmount)
                .Entero("minTermMonths", r.MinTermMonths)
                .Entero("maxTermMonths", r.MaxTermMonths)
                .Enumeracion("collateralKind", r.CollateralKind)
                .Logico("scoreBelowMinimum", r.ScoreBelowMinimum)
                .Logico("hasWarnings", r.HasWarnings)
                .Logico("policyException", r.PolicyException)
                .Entero("minDaysPastDue", r.MinDaysPastDue)
                .Entero("maxDaysPastDue", r.MaxDaysPastDue)
                .Enumeracion("concept", r.Concept)
                .Texto("minimumLevelCode", r.MinimumLevelCode.Trim().ToUpperInvariant()))
            .Calcular();

    /// <summary>
    /// La huella de una matriz ya guardada, para verificar que nadie la cambió: la misma de <see cref="Huella"/>, reconstruida de las
    /// filas (el nivel mínimo de cada regla, por su código).
    /// </summary>
    public string HuellaDeLoGuardado(IEnumerable<AttributionLevel> niveles, IEnumerable<AttributionRule> reglas)
    {
        var propios = niveles.Where(n => n.MatrixId == Id).ToList();
        var codigoPorId = propios.ToDictionary(n => n.Id, n => n.Code);
        return Huella(new BorradorDeMatriz(ValidFrom, Reason, LegalSource,
            propios.Select(BorradorDeNivel.De).ToList(),
            reglas.Where(r => r.MatrixId == Id).Select(r => BorradorDeRegla.De(r, codigoPorId[r.MinimumLevelId])).ToList()));
    }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", Inv);

    private static ProblemaDeCartera Invalida(string motivo, string mensaje) => ProblemaDeCartera.Con(CodigoInvalida, mensaje, "reason", motivo);

    private static ProblemaDeCartera SinNivelFinal(string mensaje) => ProblemaDeCartera.Con(CodigoSinNivelFinal, mensaje, "reason", "WithoutTopLevel");
}
