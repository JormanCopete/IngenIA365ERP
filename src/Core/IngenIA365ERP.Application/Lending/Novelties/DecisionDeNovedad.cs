using System.Text.Json;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>Un importe por concepto y cuota de la decisión de una novedad o de una condonación (JSON de la operación). (nuevo)</summary>
public sealed record PartidaDecidida(string Concept, int? Installment, decimal Amount)
{
    public PartidaDeNovedad Partida() => new(Enum.Parse<LoanConcept>(Concept), Installment, Amount);

    public static PartidaDecidida De(PartidaDeNovedad p) => new(p.Concepto.ToString(), p.Cuota, p.Monto);
}

/// <summary>
/// La decisión de una novedad que guarda la operación <c>TermsChange</c> en su <c>Payload</c> (feature 013, K3, T0881) y que la reconstrucción lee
/// (<see cref="ContenidoDeNovedad"/>, por <c>CargadorDeReconstruccion</c>): el mecanismo, el lado (el crédito de la novedad o el enlazado), las
/// cuotas y las condiciones nuevas, el conteo de reestructuraciones, lo capitalizado por concepto y cuota y, en el enlazado, lo que recibe. Sus
/// valores son las entradas canónicas de la operación (data-model §7.1). (nuevo)
/// </summary>
public sealed record DecisionDeNovedad
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Guid NoveltyPublicId { get; init; }

    public string NoveltyNumber { get; init; } = string.Empty;

    public string Mechanism { get; init; } = string.Empty;

    public string Cause { get; init; } = string.Empty;

    /// <summary><c>Credito</c> o <c>Enlazado</c> (<see cref="LadoDeLaNovedad"/>).</summary>
    public string Side { get; init; } = nameof(LadoDeLaNovedad.Credito);

    public int Installments { get; init; }

    public decimal? Rate { get; init; }

    public string? Periodicity { get; init; }

    public int? PaymentDay { get; init; }

    public DateOnly? FirstDueDate { get; init; }

    public int? RestructuringCountAfter { get; init; }

    public IReadOnlyList<PartidaDecidida> Capitalized { get; init; } = [];

    public IReadOnlyList<PartidaDecidida> Transfers { get; init; } = [];

    /// <summary>En una novación o refinanciación: el crédito enlazado (en el anterior) o los anteriores (en el enlazado), por su número.</summary>
    public IReadOnlyList<string> LinkedLoans { get; init; } = [];

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    /// <summary>El contenido de la reconstrucción.</summary>
    public ContenidoDeNovedad Contenido(int? noveltyId = null) =>
        new(Enum.Parse<NoveltyMechanism>(Mechanism), Installments)
        {
            NoveltyId = noveltyId,
            TasaEfectivaAnual = Rate,
            Periodicidad = Periodicity is null ? null : Enum.Parse<RepaymentPeriodicity>(Periodicity),
            DiaDePago = PaymentDay,
            PrimerVencimiento = FirstDueDate,
            ReestructuracionesDespues = RestructuringCountAfter,
            Lado = Enum.Parse<LadoDeLaNovedad>(Side),
            Capitalizaciones = Capitalized.Select(c => c.Partida()).ToList(),
            Traslados = Transfers.Select(c => c.Partida()).ToList(),
        };

    public static DecisionDeNovedad? Leer(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Trim() == "{}") return null;
        try
        {
            var d = JsonSerializer.Deserialize<DecisionDeNovedad>(payload, Json);
            return d is null || string.IsNullOrEmpty(d.Mechanism) ? null : d;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// La decisión de una condonación que guarda la operación <c>Waiver</c> en su <c>Payload</c> (feature 013, K3, T0884) y que la reconstrucción lee
/// (<see cref="ContenidoDeCondonacion"/>): cada concepto y cuota condonados —la condonación de «todas las cuotas» ya repartida en prelación—. (nuevo)
/// </summary>
public sealed record DecisionDeCondonacion
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Guid WaiverPublicId { get; init; }

    public string WaiverNumber { get; init; } = string.Empty;

    public IReadOnlyList<PartidaDecidida> Lines { get; init; } = [];

    /// <summary>Lo que salió contra el deterioro individual del último cierre, por concepto (sin cuota): las condonaciones siguientes lo descuentan.</summary>
    public IReadOnlyList<PartidaDecidida> ImpairmentApplied { get; init; } = [];

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    public ContenidoDeCondonacion Contenido() => new(Lines.Select(l => l.Partida()).ToList());

    public static DecisionDeCondonacion? Leer(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Trim() == "{}") return null;
        try
        {
            var d = JsonSerializer.Deserialize<DecisionDeCondonacion>(payload, Json);
            return d is null || d.Lines.Count == 0 ? null : d;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
