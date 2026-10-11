using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_CreditLimits</c> (feature 013; data-model §2.10; FR-029, FR-049, FR-058, FR-059, FR-142): una versión del cupo consolidado,
/// por línea o rotativo de una persona. Hecho de sólo inserción (<see cref="IHechoInmutable"/>, C42) y versión propia de Cartera
/// (<see cref="IVersionDeCartera"/>, §0.6): <see cref="Amount"/> 0 = sin cupo. El ordinal por (persona, clase, línea) lo garantiza el
/// cerrojo del deudor, no un único. El disponible se calcula, nunca se guarda.
/// </summary>
public class CreditLimit : AuditableEntity, IHechoInmutable, IVersionDeCartera
{
    public int PersonId { get; init; }

    public CreditLimitKind Kind { get; init; }

    /// <summary>Obligatoria en <c>ByLine</c>; en <c>Revolving</c>, nula = toda línea de origen comercial aplicable (C54).</summary>
    public int? CreditLineId { get; init; }

    /// <summary>0 = sin cupo (fin de vigencia).</summary>
    public decimal Amount { get; init; }

    public CreditLimitOrigin Origin { get; init; }

    public DateOnly ValidFrom { get; init; }

    public int VersionNumber { get; init; }

    public DateTime RegisteredAt { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>Solicitud que originó el cupo rotativo.</summary>
    public int? ApplicationId { get; init; }

    /// <summary>Decisión de atribución que lo otorgó (§3.4).</summary>
    public int? DecisionId { get; init; }

    public int? AssignedByUserId { get; init; }

    /// <summary>JSON: relación con aportes, capacidad, puntaje.</summary>
    public string? CalculationDetail { get; init; }

    /// <summary>El saldo de aportes usado (D19).</summary>
    public int? ContributionBalanceLoadId { get; init; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0388) --

    public const string CodigoInvalido = "Validation.Invalid";

    /// <summary>
    /// Lo que está mal en una versión de cupo, o nulo (data-model §2.10): el monto no es negativo (0 = sin cupo), la línea es obligatoria
    /// en <c>ByLine</c> y no va en <c>Consolidated</c> (en <c>Revolving</c> es opcional, C54), y lleva motivo.
    /// </summary>
    public static ProblemaDeCartera? Validar(CreditLimitKind clase, int? creditLineId, decimal monto, string? motivo)
    {
        if (!Enum.IsDefined(clase)) throw new ArgumentException("Clase de cupo desconocida.", nameof(clase));
        if (monto < 0m)
            return ProblemaDeCartera.Con(CodigoInvalido, "El cupo no es negativo (cero termina el cupo).", "field", "amount");
        if (clase == CreditLimitKind.ByLine && creditLineId is null)
            return ProblemaDeCartera.Con(CodigoInvalido, "El cupo por línea nombra su línea.", "field", "creditLinePublicId");
        if (clase == CreditLimitKind.Consolidated && creditLineId is not null)
            return ProblemaDeCartera.Con(CodigoInvalido, "El cupo consolidado no es de una línea.", "field", "creditLinePublicId");
        if (string.IsNullOrWhiteSpace(motivo))
            return ProblemaDeCartera.Con(CodigoInvalido, "Indique el motivo del cupo.", "field", "reason");
        return null;
    }

    /// <summary>
    /// La versión siguiente del cupo de (persona, clase, línea): sólo inserción (§0.6), con su número; el ordinal lo garantiza el cerrojo
    /// del deudor que toma quien llama (T34).
    /// </summary>
    public static CreditLimit NuevaVersion(int personId, CreditLimitKind clase, int? creditLineId, decimal monto, CreditLimitOrigin origen,
        DateOnly desde, int numero, DateTime registradoEl, string motivo, int? asignadoPor, int? applicationId = null, int? decisionId = null,
        string? detalle = null, int? cargaDeAportes = null)
    {
        ReglaDeCarteraException.SiHay(Validar(clase, creditLineId, monto, motivo));
        if (numero < 1) throw new ArgumentOutOfRangeException(nameof(numero), "La versión empieza en 1.");
        return new CreditLimit
        {
            PersonId = personId,
            Kind = clase,
            CreditLineId = creditLineId,
            Amount = monto,
            Origin = origen,
            ValidFrom = desde,
            VersionNumber = numero,
            RegisteredAt = registradoEl,
            Reason = motivo.Trim(),
            AssignedByUserId = asignadoPor,
            ApplicationId = applicationId,
            DecisionId = decisionId,
            CalculationDetail = detalle,
            ContributionBalanceLoadId = cargaDeAportes,
        };
    }

    /// <summary>¿Es del mismo cupo (persona, clase, línea)?</summary>
    public bool EsDelMismoCupo(int personId, CreditLimitKind clase, int? creditLineId) =>
        PersonId == personId && Kind == clase && CreditLineId == creditLineId;
}
