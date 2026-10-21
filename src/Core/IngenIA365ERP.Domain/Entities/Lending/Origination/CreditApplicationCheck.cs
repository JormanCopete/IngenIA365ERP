using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_CreditApplicationChecks</c> (feature 013; data-model §2.4; FR-036, FR-038, FR-041, FR-042): cada validación de la solicitud
/// con su resultado y su evidencia. Hecho de sólo inserción (<see cref="IHechoInmutable"/>, C42): re-evaluar agrega filas y la vigente
/// es la última por (<see cref="Kind"/>, <see cref="PartyId"/>, <see cref="Stage"/>). Una <c>Blocking</c> impide decidir.
/// </summary>
public class CreditApplicationCheck : AuditableEntity, IHechoInmutable
{
    public int ApplicationId { get; init; }

    /// <summary>Nulo = validación de la solicitud, no de una parte.</summary>
    public int? PartyId { get; init; }

    public ApplicationCheckKind Kind { get; init; }

    public CheckStage Stage { get; init; }

    public CheckOutcome Outcome { get; init; }

    /// <summary>Falso en capacidad de pago, autorización, listas y usura (<c>NoExceptuables</c>).</summary>
    public bool IsExceptionable { get; init; }

    /// <summary>El código <c>Lending.*</c> que explica el resultado.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>JSON con las cifras: capacidad disponible, exceso sobre el límite y su fuente, cupo disponible.</summary>
    public string? Detail { get; init; }

    public int? ScreeningResultId { get; init; }

    public int? BureauQueryId { get; init; }

    public int? CreditScoreId { get; init; }

    /// <summary>Huella de entradas y resultado.</summary>
    public string EvidenceSha256 { get; init; } = string.Empty;

    public DateTime EvaluatedAt { get; init; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0388) --

    /// <summary>¿Impide decidir? (data-model §2.4: <c>Blocking</c>; <c>Unavailable</c> también, hasta repetir la consulta.)</summary>
    public bool Bloquea => Outcome is CheckOutcome.Blocking or CheckOutcome.Unavailable;

    /// <summary>Llega al aprobador como alerta (FR-036).</summary>
    public bool EsAlerta => Outcome == CheckOutcome.Warning;

    /// <summary>
    /// Una validación con su evidencia (data-model §2.4): <see cref="EvidenceSha256"/> es la huella de entradas y resultado —clase, parte,
    /// etapa, resultado, si es exceptuable, código, detalle y la evidencia enlazada— calculada por <see cref="HuellaDeCartera"/>. Se agrega,
    /// nunca se reescribe: la vigente es la última por (clase, parte, etapa).
    /// </summary>
    public static CreditApplicationCheck Registrar(int applicationId, int? partyId, ApplicationCheckKind clase, CheckStage etapa, CheckOutcome resultado,
        bool exceptuable, string? codigo, string? detalle, DateTime evaluadaEl, int? screeningResultId = null, int? bureauQueryId = null,
        int? creditScoreId = null)
    {
        if (!Enum.IsDefined(clase) || !Enum.IsDefined(etapa) || !Enum.IsDefined(resultado))
            throw new ArgumentException("Clase, etapa o resultado de la validación desconocidos.");
        var huella = new HuellaDeCartera()
            .Entero("applicationId", applicationId)
            .Entero("partyId", partyId)
            .Enumeracion("kind", (ApplicationCheckKind?)clase)
            .Enumeracion("stage", (CheckStage?)etapa)
            .Enumeracion("outcome", (CheckOutcome?)resultado)
            .Logico("exceptionable", exceptuable)
            .Texto("errorCode", codigo)
            .Texto("detail", detalle)
            .Entero("screeningResultId", screeningResultId)
            .Entero("bureauQueryId", bureauQueryId)
            .Entero("creditScoreId", creditScoreId)
            .Instante("evaluatedAt", evaluadaEl)
            .Calcular();
        return new CreditApplicationCheck
        {
            ApplicationId = applicationId,
            PartyId = partyId,
            Kind = clase,
            Stage = etapa,
            Outcome = resultado,
            IsExceptionable = exceptuable,
            ErrorCode = codigo,
            Detail = detalle,
            ScreeningResultId = screeningResultId,
            BureauQueryId = bureauQueryId,
            CreditScoreId = creditScoreId,
            EvidenceSha256 = huella,
            EvaluatedAt = evaluadaEl,
        };
    }

    /// <summary>Las vigentes: la última de cada (clase, parte, etapa) entre <paramref name="todas"/>.</summary>
    public static IReadOnlyList<CreditApplicationCheck> Vigentes(IEnumerable<CreditApplicationCheck> todas) =>
        todas.GroupBy(c => (c.Kind, c.PartyId, c.Stage))
            .Select(g => g.OrderByDescending(c => c.EvaluatedAt).ThenByDescending(c => c.Id).First())
            .OrderBy(c => c.Kind).ThenBy(c => c.PartyId).ThenBy(c => c.Stage)
            .ToList();
}
