namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// Quién recauda en cada llamada al contrato de obligaciones (feature 013, T0149; contracts/interfaces.md §3.2). <c>Code</c> es el
/// de su fila en <c>COR_IntegrationParticipants</c> y forma, con la clave, el único <c>(CollectorCode, IdempotencyKey)</c> de
/// <c>LND_LoanOperations</c>. (nuevo)
/// </summary>
public sealed record Recaudador(string Code, ClaseDeRecaudador Class)
{
    /// <summary>Código de la nómina propia en el registro de participantes (semilla de <c>PlataformaParaCartera</c>, E-03).</summary>
    public const string CodigoDeNomina = "NOM";

    /// <summary>La nómina propia: definitiva y ordinaria (clase <see cref="ClaseDeRecaudador.Payroll"/>).</summary>
    public static Recaudador Nomina { get; } = new(CodigoDeNomina, ClaseDeRecaudador.Payroll);
}
