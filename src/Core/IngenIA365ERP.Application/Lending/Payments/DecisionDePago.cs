using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payments;

namespace IngenIA365ERP.Application.Lending.Payments;

/// <summary>
/// La decisión de un pago tal como se sella en <c>LND_LoanOperations.Payload</c> (feature 013, US5, bloque 9B; data-model §7.1, §7.8; D4,
/// FR-081): lo que la reconstrucción necesita para <b>redistribuirlo</b> —modalidad, valor tomado (lo recibido menos el excedente que volvió
/// al recaudador, D5), elección aplicada y cuota—, más lo que lo explica (recaudador, forma, clase de medio, huella de la simulación, el tope
/// legal del prepago con los valores nacionales que rigieron). La distribución no se guarda: se recalcula. Sin columnas nuevas: el tope legal
/// (<c>WithinLegalPrepaymentCap</c>, nuevo) y sus valores van aquí. (nuevo)
/// </summary>
public sealed record DecisionDePago
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Modality { get; init; } = string.Empty;

    /// <summary>Lo recibido.</summary>
    public decimal Received { get; init; }

    /// <summary>Lo que Cartera tomó (recibido − excedente): es el valor con que se redistribuye.</summary>
    public decimal Applied { get; init; }

    public DateOnly ValueDate { get; init; }

    /// <summary>La elección aplicada (la del deudor o su preferencia, D24); nula en un abono sin preferencia.</summary>
    public string? Choice { get; init; }

    public bool ChoiceFromPreference { get; init; }

    public int? Installment { get; init; }

    public string CollectorCode { get; init; } = string.Empty;

    public string CollectorClass { get; init; } = string.Empty;

    public string Form { get; init; } = string.Empty;

    public string MeansClass { get; init; } = string.Empty;

    public string? SimulationId { get; init; }

    /// <summary>(nuevo) El crédito cabe en el tope legal del prepago sin penalidad (FR-075, Ley 1555); nulo si no aplica a la modalidad.</summary>
    public bool? WithinLegalPrepaymentCap { get; init; }

    /// <summary>El tope en pesos que rigió (veces × SMMLV al desembolso).</summary>
    public decimal? LegalPrepaymentCap { get; init; }

    /// <summary>El <c>PublicId</c> del SMMLV usado (la copia de la cooperativa).</summary>
    public Guid? MinimumWagePublicId { get; init; }

    /// <summary>El <c>PublicId</c> del límite <c>PREPAGO_TOPE_SMMLV</c> usado.</summary>
    public Guid? CapLimitPublicId { get; init; }

    /// <summary>La operación de origen de una aplicación por resolución de saldo a favor (<c>{clave}:destino</c>).</summary>
    public Guid? SourceOperationPublicId { get; init; }

    /// <summary>
    /// 2026-11-16 (pedido del dueño): cómo cobró el interés de la cuota en curso una cancelación cuando <c>Pagos.InteresesAlPagarAntesDelVencimiento</c>
    /// pedía la cuota completa —<c>CuotaCompleta</c>, o <c>Corrido</c> si el tope legal del prepago sin penalidad (Ley 1555 de 2012) o la vivienda lo
    /// impidieron (<see cref="Common.InteresesAlCancelar"/>)—. Nulo con el parámetro en «Corrido» o si la modalidad no cancela: así la decisión de siempre se
    /// sella igual que antes. Lo lee la reconstrucción: un pago que no lo trae es «Corrido», así su historia no cambia. Sin columna nueva, como el tope.
    /// (nuevo)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InterestBeforeDueDate { get; init; }

    /// <summary>2026-11-16: por qué se cobró el corrido aunque el parámetro pida la cuota completa (<c>Housing</c>, <c>WithinLegalPrepaymentCap</c>,
    /// <c>LegalPrepaymentCapUnknown</c>). (nuevo)</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InterestBeforeDueDateReason { get; init; }

    /// <summary>
    /// 2026-11-16 (pedido del dueño): el orden de rubros de la empresa con que se repartió el pago (<c>Pagos.PrelacionDeRubros</c> a la fecha valor,
    /// <see cref="Common.PrelacionDeLaEmpresa"/>), en su texto canónico. Nulo —un pago anterior al parámetro o de una línea con prelación propia— es la
    /// prelación de la línea: así la decisión de siempre se sella igual y la reconstrucción de un pago viejo no cambia. Sin columna nueva. (nuevo)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AllocationRubrics { get; init; }

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    /// <summary>
    /// El contenido que lee la reconstrucción: valor tomado, modalidad, elección, cuota y, desde el 2026-11-16, si cobra la cuota completa y el orden de
    /// rubros sellado.
    /// </summary>
    public ContenidoDePago Contenido() =>
        new(Enum.Parse<PaymentModality>(Modality), Applied)
        {
            Eleccion = Choice is null ? null : Enum.Parse<PrepaymentChoice>(Choice),
            Cuota = Installment,
            CuotaCompleta = string.Equals(InterestBeforeDueDate, Domain.Lending.Parameters.ParametrosDeCartera.InteresesDeLaCuotaCompleta, StringComparison.Ordinal),
            Rubros = OrdenDeRubros.DesdeSellado(AllocationRubrics),
        };

    /// <summary>
    /// El contenido de una operación de pago o de cobro provisional: el de su decisión sellada o, si no la trae (un pago anterior a 9B), el de
    /// sus columnas (modalidad, valor menos excedente, elección). Nulo si no hay con qué (la reconstrucción falla el crédito, a propósito).
    /// </summary>
    public static ContenidoDePago? ContenidoDe(LoanOperation operacion)
    {
        ArgumentNullException.ThrowIfNull(operacion);
        if (Leer(operacion.Payload) is { Modality.Length: > 0 } decision) return decision.Contenido();
        if (operacion.PaymentModality is { } modalidad)
            return new ContenidoDePago(modalidad, operacion.Amount - (operacion.ExcessAmount ?? 0m)) { Eleccion = operacion.PrepaymentChoice };
        return null;
    }

    public static DecisionDePago? Leer(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Trim() == "{}") return null;
        try
        {
            var d = JsonSerializer.Deserialize<DecisionDePago>(payload, Json);
            return d is null || string.IsNullOrEmpty(d.Modality) || !Enum.TryParse<PaymentModality>(d.Modality, out _) ? null : d;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// La decisión de una resolución de saldo a favor o de anticipo (<c>CreditBalanceResolution</c>; data-model §7.10): de qué concepto, cuánto,
/// cómo se resolvió y, si se aplicó como abono a capital, con qué elección. (nuevo)
/// </summary>
public sealed record DecisionDeResolucion
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Concept { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Form { get; init; } = string.Empty;

    public string? Choice { get; init; }

    public Guid? TargetObligationPublicId { get; init; }

    public string? TargetModality { get; init; }

    public string CollectorCode { get; init; } = string.Empty;

    public string CollectorClass { get; init; } = string.Empty;

    /// <summary>
    /// 2026-11-16 (decisión del dueño): cómo cobra el interés de la cuota en curso el saldo a favor aplicado como abono si termina cancelando, como en
    /// <see cref="DecisionDePago.InterestBeforeDueDate"/>. Nulo con el parámetro en «Corrido» o en otra forma de resolución. (nuevo)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InterestBeforeDueDate { get; init; }

    /// <summary>2026-11-16: por qué se cobra el corrido aunque el parámetro pida la cuota completa (como en <see cref="DecisionDePago"/>). (nuevo)</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InterestBeforeDueDateReason { get; init; }

    /// <summary>2026-11-16 (pedido del dueño): el orden de rubros sellado al aplicar el saldo a favor como abono (<see cref="DecisionDePago.AllocationRubrics"/>). (nuevo)</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AllocationRubrics { get; init; }

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    /// <summary>El contenido que lee la reconstrucción (2026-11-16: con la cuota completa y el orden de rubros sellados, si los trae).</summary>
    public ContenidoDeResolucion Contenido() =>
        new(Enum.Parse<LoanConcept>(Concept), Amount)
        {
            ComoAbonoACapital = Form == nameof(IngenIA365ERP.Application.Common.Integration.Lending.FormaDeResolucion.ApplyAsExtraordinaryPrincipal),
            Eleccion = Choice is null ? null : Enum.Parse<PrepaymentChoice>(Choice),
            CuotaCompleta = string.Equals(InterestBeforeDueDate, Domain.Lending.Parameters.ParametrosDeCartera.InteresesDeLaCuotaCompleta, StringComparison.Ordinal),
            Rubros = OrdenDeRubros.DesdeSellado(AllocationRubrics),
        };

    public static DecisionDeResolucion? Leer(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Trim() == "{}") return null;
        try
        {
            var d = JsonSerializer.Deserialize<DecisionDeResolucion>(payload, Json);
            return d is null || !Enum.TryParse<LoanConcept>(d.Concept, out _) ? null : d;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
