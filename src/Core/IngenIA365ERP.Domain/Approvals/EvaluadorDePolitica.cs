namespace IngenIA365ERP.Domain.Approvals;

/// <summary>
/// Un nivel sellado en <c>COR_ApprovalRequests.RequiredLevelsJson</c>, con la forma de data-model §21
/// (<c>{ order, threshold, permission }</c>). Vive aquí y no junto a la entidad porque no es una entidad. (nuevo)
/// </summary>
public sealed record NivelSellado(int Order, decimal Threshold, string Permission);

/// <summary>Un nivel de una política o de una regla fija: orden 1..n, umbral en pesos y el permiso que lo decide. (nuevo)</summary>
public sealed record NivelDeAprobacion(int Order, decimal Threshold, string PermissionCode);

/// <summary>
/// Una versión de política de un sujeto, como la ve el motor puro: del tipo de documento (o de todos, con
/// <see cref="DocumentTypePublicId"/> nulo), su vigencia y sus niveles. Una política sin niveles significa «sin
/// aprobación» desde <see cref="ValidFrom"/>; no es lo mismo que no tener política. (nuevo)
/// </summary>
public sealed record PoliticaDeAprobacion(
    Guid? DocumentTypePublicId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<NivelDeAprobacion> Niveles)
{
    /// <summary>Vigente a la fecha, inclusive en los dos extremos.</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}

/// <summary>Qué decidió el evaluador. (nuevo)</summary>
public enum ResultadoDeEvaluacion
{
    /// <summary>Se confirma sin aprobación.</summary>
    SinAprobacion = 0,

    /// <summary>Exige los niveles de <see cref="EvaluacionDeAprobacion.Niveles"/>, en orden.</summary>
    ConNiveles = 1,

    /// <summary>El monto supera el máximo del permiso y no hay nivel que forzar: <c>Inventory.Approval.AmountExceedsLimit</c>.</summary>
    ExcedeLimite = 2,
}

/// <summary>
/// Resultado de evaluar un monto (nuevo). <see cref="NivelForzado"/> dice que el nivel 1 entró sólo porque el monto
/// supera el máximo del permiso (T34); <see cref="ReglaFija"/>, que los niveles son la regla fija del sujeto y no una
/// política (la solicitud se sella con <c>PolicyId</c> nulo).
/// </summary>
public sealed record EvaluacionDeAprobacion(
    ResultadoDeEvaluacion Resultado,
    IReadOnlyList<NivelDeAprobacion> Niveles,
    decimal? MontoMaximo,
    bool NivelForzado,
    bool ReglaFija)
{
    public bool RequiereAprobacion => Resultado == ResultadoDeEvaluacion.ConNiveles;
}

/// <summary>
/// Por qué alguien no puede decidir una solicitud (FR-010; contracts/api.md §15.2, <c>data.reason</c> de
/// <c>Approvals.SelfApprovalForbidden</c> y <c>excludedReason</c> del DTO). Se nombra en inglés porque viaja por la
/// API como texto. (nuevo)
/// </summary>
public enum MotivoDeExclusion
{
    Creator = 1,
    Requester = 2,
    Participant = 3,
    PreviousLevel = 4,
}

/// <summary>
/// Quiénes intervienen en lo que se aprueba, por <c>SEC_Users.Id</c> (nunca el entero del token ni el correo): el
/// creador del documento, quien pidió la aprobación, los participantes que declara el documento (quien abrió o
/// capturó el conteo, quien despachó o recibió el traslado, el cajero) y quienes ya aprobaron otro nivel. (nuevo)
/// </summary>
public sealed record ParticipantesDeAprobacion(
    int Creador,
    int Solicitante,
    IReadOnlyCollection<int> Participantes,
    IReadOnlyCollection<int> AprobadoresPrevios);

/// <summary>
/// El motor puro de aprobaciones (feature 012, T33, T34, T080; FR-009, FR-010). Sin IO: recibe las políticas, el
/// monto, el monto máximo efectivo del permiso y quién intervino, y devuelve los niveles exigidos o
/// <see cref="ResultadoDeEvaluacion.ExcedeLimite"/>, o por qué alguien no puede decidir. Lo prueban los casos dorados
/// de <c>Domain.Tests/Approvals/Casos</c>; lo usa <c>MotorDeAprobaciones</c>.
/// </summary>
public static class EvaluadorDePolitica
{
    /// <summary>
    /// La política que rige a la fecha para un tipo de documento: la del tipo gana sobre la de todos los tipos del
    /// mismo sujeto; entre las del mismo alcance, la vigente (y, si hubiera dos por un dato malo, la más reciente).
    /// Nula si ninguna rige a esa fecha.
    /// </summary>
    public static PoliticaDeAprobacion? ElegirPolitica(IEnumerable<PoliticaDeAprobacion> candidatas, Guid? tipoDeDocumento, DateOnly fecha)
    {
        var vigentes = candidatas.Where(p => p.VigenteEn(fecha)).OrderByDescending(p => p.ValidFrom).ToList();
        return (tipoDeDocumento is { } tipo ? vigentes.FirstOrDefault(p => p.DocumentTypePublicId == tipo) : null)
            ?? vigentes.FirstOrDefault(p => p.DocumentTypePublicId is null);
    }

    /// <summary>
    /// La regla fija de un nivel de un sujeto sin política (data-model §21): el descuento sobre el tope con
    /// <c>Inventory.Discounts.Authorize</c>, el crédito provisional con <c>Inventory.Sales.SellOnCredit</c> y la
    /// diferencia de traslado con <c>Inventory.Transfers.Approve</c>, desde el primer peso. Los demás sujetos no tienen.
    /// </summary>
    public static NivelDeAprobacion? ReglaFija(string sujeto) => sujeto switch
    {
        ApprovalSubjects.DiscountOverCap => new NivelDeAprobacion(1, 0m, "Inventory.Discounts.Authorize"),
        ApprovalSubjects.ProvisionalCredit => new NivelDeAprobacion(1, 0m, "Inventory.Sales.SellOnCredit"),
        ApprovalSubjects.TransferDiscrepancy => new NivelDeAprobacion(1, 0m, "Inventory.Transfers.Approve"),
        _ => null,
    };

    /// <summary>
    /// Evalúa un monto (FR-010; T34). Con política: los niveles cuyo umbral alcanza el monto (≥), en orden, y el
    /// nivel 1 forzado si el monto supera el máximo del permiso aunque no alcance su umbral. Sin política: la regla
    /// fija del sujeto, si la tiene. Si el monto supera el máximo y no queda ningún nivel que forzar (sin política, o
    /// una política vacía), <see cref="ResultadoDeEvaluacion.ExcedeLimite"/> con el máximo.
    /// </summary>
    /// <param name="montoMaximo">El máximo efectivo del permiso; nulo = sin límite.</param>
    public static EvaluacionDeAprobacion Evaluar(string sujeto, decimal monto, PoliticaDeAprobacion? politica, decimal? montoMaximo)
    {
        var excede = montoMaximo is { } maximo && monto > maximo;

        IReadOnlyList<NivelDeAprobacion> niveles;
        var reglaFija = false;
        if (politica is not null)
        {
            niveles = politica.Niveles.OrderBy(n => n.Order).ToList();
        }
        else if (ReglaFija(sujeto) is { } fija)
        {
            niveles = [fija];
            reglaFija = true;
        }
        else
        {
            niveles = [];
        }

        if (niveles.Count == 0)
        {
            return excede
                ? new EvaluacionDeAprobacion(ResultadoDeEvaluacion.ExcedeLimite, [], montoMaximo, false, false)
                : new EvaluacionDeAprobacion(ResultadoDeEvaluacion.SinAprobacion, [], montoMaximo, false, false);
        }

        var alcanzados = niveles.Where(n => monto >= n.Threshold).ToList();
        var forzado = false;
        if (excede && alcanzados.All(n => n.Order != niveles[0].Order))
        {
            alcanzados.Insert(0, niveles[0]);
            forzado = true;
        }

        return alcanzados.Count == 0
            ? new EvaluacionDeAprobacion(ResultadoDeEvaluacion.SinAprobacion, [], montoMaximo, false, reglaFija)
            : new EvaluacionDeAprobacion(ResultadoDeEvaluacion.ConNiveles, alcanzados, montoMaximo, forzado, reglaFija);
    }

    /// <summary>
    /// La segregación fija, no parametrizable (FR-010): el creador, quien pidió, los participantes declarados y quien
    /// ya aprobó otro nivel no deciden. Devuelve el primer motivo que aplica, en ese orden, o nulo si puede decidir.
    /// </summary>
    public static MotivoDeExclusion? ValidarDecision(int decisor, ParticipantesDeAprobacion participantes)
    {
        if (decisor == participantes.Creador) return MotivoDeExclusion.Creator;
        if (decisor == participantes.Solicitante) return MotivoDeExclusion.Requester;
        if (participantes.Participantes.Contains(decisor)) return MotivoDeExclusion.Participant;
        if (participantes.AprobadoresPrevios.Contains(decisor)) return MotivoDeExclusion.PreviousLevel;
        return null;
    }

    /// <summary>
    /// Qué tiene de malo una lista de niveles (<c>Approvals.Policy.LevelsInvalid</c>, <c>data.reason</c>), o nulo si
    /// está bien: órdenes 1..n consecutivos, umbrales ≥ 0 y no decrecientes con el orden, y un permiso en cada nivel.
    /// Una lista vacía es válida (= sin aprobación).
    /// </summary>
    public static string? ValidarNiveles(IReadOnlyList<NivelDeAprobacion> niveles)
    {
        var ordenados = niveles.OrderBy(n => n.Order).ToList();
        for (var i = 0; i < ordenados.Count; i++)
        {
            if (ordenados[i].Order != i + 1)
                return "Los niveles van con orden 1, 2, 3… consecutivos y sin repetir.";
        }

        if (ordenados.Any(n => n.Threshold < 0))
            return "El umbral de un nivel no puede ser negativo.";

        for (var i = 1; i < ordenados.Count; i++)
        {
            if (ordenados[i].Threshold < ordenados[i - 1].Threshold)
                return $"El umbral del nivel {ordenados[i].Order} es menor que el del nivel {ordenados[i - 1].Order}: los umbrales no decrecen con el orden.";
        }

        if (ordenados.Any(n => string.IsNullOrWhiteSpace(n.PermissionCode)))
            return "Cada nivel lleva el permiso que lo decide.";

        return null;
    }
}
