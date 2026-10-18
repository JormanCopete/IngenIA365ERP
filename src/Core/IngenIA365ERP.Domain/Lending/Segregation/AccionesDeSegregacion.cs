namespace IngenIA365ERP.Domain.Lending.Segregation;

/// <summary>
/// Los actos de un expediente que la segregación compara (feature 013, T66; data-model parte 1 §3.13; T0201/T0275): texto en
/// <c>LND_CaseParticipations.Action</c> (nvarchar(30)) y en los pares del parámetro <c>Segregacion.Incompatibilidades</c>. K3 (T0881, T0884)
/// suma <see cref="Aplicar"/>: aplicar una novedad o una condonación aprobada. (nuevo)
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

    /// <summary>K3: aplicar una novedad o una condonación ya aprobada; quien la radicó no la aplica (regla fija, T0863).</summary>
    public const string Aplicar = nameof(Aplicar);

    /// <summary>Los diez de T66 y, desde K3, <see cref="Aplicar"/>.</summary>
    public static IReadOnlyList<string> Todas { get; } =
    [
        Radicar, Analizar, Decidir, Votar, Desembolsar, AnularOrdenDeGiro, ArmarLoteDeCastigo, AplicarCastigo, CargarMigracion,
        AceptarDiferencias, Aplicar,
    ];

    public static bool Existe(string? accion) => accion is not null && Todas.Contains(accion, StringComparer.Ordinal);
}
