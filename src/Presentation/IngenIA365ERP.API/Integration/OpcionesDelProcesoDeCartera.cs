using IngenIA365ERP.Application.Lending.Processes;

namespace IngenIA365ERP.API.Integration;

/// <summary>
/// Sección <c>Lending:Process</c> de <c>appsettings.json</c> (feature 013, T5, PM-13, T0225; data-model parte 3 §12.1, «Técnicos»;
/// quickstart §1.7): la configuración técnica de <see cref="ProcesadorDeCartera"/>. No son parámetros de la cooperativa. La fixture de
/// pruebas lo apaga (<c>Enabled = false</c>) y conduce cada pasada a mano. Se valida al arrancar: el presupuesto tiene que caber en el
/// arrendamiento. (nuevo)
/// </summary>
public sealed class OpcionesDelProcesoDeCartera
{
    public const string SectionName = "Lending:Process";

    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 30;

    public int BudgetSeconds { get; set; } = 60;

    public int LeaseTtlSeconds { get; set; } = 120;

    public int MaxParallelCooperatives { get; set; } = 2;

    /// <summary>Hora local (America/Bogota) desde la que corre el ciclo del día (<c>HH:mm</c>).</summary>
    public string StartTimeLocal { get; set; } = "00:30";

    /// <summary>Créditos por tanda.</summary>
    public int BatchSize { get; set; } = 250;

    public ParametrosDeLaPasada Pasada() => new(MaxParallelCooperatives, TimeSpan.FromSeconds(BudgetSeconds), TimeSpan.FromSeconds(LeaseTtlSeconds))
    {
        HoraDeInicio = TimeOnly.TryParseExact(StartTimeLocal, "HH:mm", out var hora) ? hora : new TimeOnly(0, 30),
        TamanoDeTanda = BatchSize > 0 ? BatchSize : IngenIA365ERP.Application.Lending.Accrual.ParametrosDeLaTandaDeCartera.TamanoPorDefecto,
    };

    /// <summary>Lo que está mal, con la clave completa; vacío si todo está bien.</summary>
    public IReadOnlyList<string> Problemas()
    {
        var problemas = Pasada().Problemas().Select(p => $"{SectionName}: {p}").ToList();
        if (IntervalSeconds <= 0) problemas.Add($"{SectionName}:IntervalSeconds tiene que ser mayor que cero (vale {IntervalSeconds}).");
        if (BatchSize <= 0) problemas.Add($"{SectionName}:BatchSize tiene que ser mayor que cero (vale {BatchSize}).");
        if (!TimeOnly.TryParseExact(StartTimeLocal, "HH:mm", out _))
            problemas.Add($"{SectionName}:StartTimeLocal va como HH:mm (vale «{StartTimeLocal}»).");
        return problemas;
    }
}
