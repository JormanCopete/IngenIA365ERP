namespace IngenIA365ERP.Domain.Lending.Segregation;

/// <summary>
/// Los diez actos de un expediente que la segregación compara (feature 013, T66; data-model parte 1 §3.13; T0201/T0275): texto en
/// <c>LND_CaseParticipations.Action</c> (nvarchar(30)) y en los pares del parámetro <c>Segregacion.Incompatibilidades</c>. (nuevo)
/// </summary>
public static class AccionesDeSegregacion
{
    public const string Radicar = nameof(Radicar);
    public const string Analizar = nameof(Analizar);
    public const string Decidir = nameof(Decidir);
    public const string Votar = nameof(Votar);
    public const string Desembolsar = nameof(Desembolsar);
    public const string AnularOrdenDeGiro = nameof(AnularOrdenDeGiro);
    public const string ArmarLoteDeCastigo = nameof(ArmarLoteDeCastigo);
    public const string AplicarCastigo = nameof(AplicarCastigo);
    public const string CargarMigracion = nameof(CargarMigracion);
    public const string AceptarDiferencias = nameof(AceptarDiferencias);

    /// <summary>Los diez, en el orden de T66.</summary>
    public static IReadOnlyList<string> Todas { get; } =
    [
        Radicar, Analizar, Decidir, Votar, Desembolsar, AnularOrdenDeGiro, ArmarLoteDeCastigo, AplicarCastigo, CargarMigracion,
        AceptarDiferencias,
    ];

    public static bool Existe(string? accion) => accion is not null && Todas.Contains(accion, StringComparer.Ordinal);
}
