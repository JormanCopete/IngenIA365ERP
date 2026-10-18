namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

/// <summary>
/// <c>CaptacionesActivadas</c> v1 (contracts/mensajes.md §1.1 y §4.1 de la 014; <c>CAV</c>; entrega K7): activación de aportes o de captaciones (campo <c>Nature</c>; research C23: <c>CAV</c>, no <c>CTA</c>); informativo, con recibo. No admite proceso: lo origina una persona.
/// Declarado en la fase fundacional (T048) con su contenido de §4.1 para que el catálogo, <c>LND_FactTypes</c> y la tabla hecho × rol lo
/// nombren; lo emite la historia de su entrega.
/// </summary>
public sealed record CaptacionesActivadasV1
{
    public const string Type = "CaptacionesActivadas";


    /// <summary>Valor de <c>LineNature</c>: <c>Deposit</c> o <c>Contribution</c>.</summary>
    public string Nature { get; init; } = string.Empty;

    public DateOnly ActivationDate { get; init; }

    /// <summary>La carga de migración; nula en la activación sin carga (FR-117).</summary>
    public Guid? LoadPublicId { get; init; }

    /// <summary>Cuántas cuentas quedaron activas.</summary>
    public int AccountCount { get; init; }

    /// <summary>Cuántas filas de saldo inicial.</summary>
    public int OpeningTransactions { get; init; }
}
