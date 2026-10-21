namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>ExcedentesDistribuidos</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>EXD</c>; entrega K10): distribución de excedentes decidida por la asamblea (FR-046), forma masiva; <c>AO</c>. No admite proceso: lo origina una persona.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record ExcedentesDistribuidosV1
{
    public const string Type = "ExcedentesDistribuidos";

    /// <summary>La decisión de la asamblea que se aplica.</summary>
    public AssemblyDistributionRefV1 Distribution { get; init; } = new();

    /// <summary>Totales por combinación de dimensiones.</summary>
    public IReadOnlyList<DepositFactTotalV1> Totals { get; init; } = [];

    /// <summary>Una línea por asociado.</summary>
    public IReadOnlyList<DepositFactLineV1> Lines { get; init; } = [];

    /// <summary>El destino del excedente: <c>ContributionCapital</c>, <c>DepositAccount</c> u <c>Order</c>.</summary>
    public string? Destination { get; init; }

    public int LineCount { get; init; }

    public decimal ControlTotal { get; init; }

    /// <summary>
    /// US12 (T203; research §22): con el destino <c>Order</c> (pago), lo que se paga a cada asociado por orden de giro —no mueve ninguna cuenta del
    /// asociado: va del fondo de excedentes a los retornos por pagar (<c>DevolucionPorPagar</c>, motivo <c>SurplusPayment</c>)—. Vacío en los otros destinos.
    /// </summary>
    public IReadOnlyList<SurplusPaymentV1> Payments { get; init; } = [];

    /// <summary>
    /// Respuesta 89 B del dueño (E260): la retención en la fuente practicada a cada asociado sobre lo que se le reparte. Las líneas y los pagos llevan lo <b>neto</b>
    /// (lo bruto menos la retención); la retención sale del mismo fondo y va a la retención por pagar de la asamblea. Vacío: no se retuvo.
    /// </summary>
    public IReadOnlyList<AssemblyWithholdingV1> Withholdings { get; init; } = [];
}

/// <summary>
/// La retención en la fuente de un asociado sobre lo que reparte la asamblea (E260): su cuenta de aportes (para la sucursal y la línea), el titular, el valor
/// retenido y el detalle (base, tarifa, UVT, perfil tributario y el concepto de retención).
/// </summary>
public sealed record AssemblyWithholdingV1
{
    public MemberAccountRefV1 Account { get; init; } = new();

    public Guid HolderPersonPublicId { get; init; }

    public DepositFactDimensionsV1 Dimensions { get; init; } = new();

    public decimal Amount { get; init; }

    public WithholdingDetailV1 Detail { get; init; } = new();
}

/// <summary>El pago de los excedentes de un asociado (US12, T203): su cuenta de aportes (para la sucursal y la línea), el titular, el valor y la orden.</summary>
public sealed record SurplusPaymentV1
{
    public MemberAccountRefV1 Account { get; init; } = new();

    public Guid HolderPersonPublicId { get; init; }

    public DepositFactDimensionsV1 Dimensions { get; init; } = new();

    public decimal Amount { get; init; }

    public Guid? OrderPublicId { get; init; }
}
