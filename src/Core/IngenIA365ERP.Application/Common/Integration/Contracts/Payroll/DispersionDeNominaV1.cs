using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Payroll;

/// <summary>La cuenta de origen de un archivo de dispersión: el código del banco (ACH) y el número de la cuenta. (nuevo)</summary>
public sealed record CuentaDeOrigenDeLaDispersionV1
{
    public string? BankCode { get; init; }

    public string? AccountNumber { get; init; }
}

/// <summary>
/// <c>DispersionDeNominaGenerada</c> v1 (<c>NOM → Treasury</c>; feature 015, T2, fase 41, T0448; E-05, contratos-v2.md §5; FR-168): el archivo de dispersión que Nómina
/// generó, para que Tesorería lo tome como compromiso de la posición de liquidez y lo concilie con el cargo del banco. Nómina sigue pagando con su archivo (D27). Origen el
/// archivo, clave <c>Confirmation:{archivo:N}</c>, modo <c>Always</c> (no pasa a Contabilidad), a nombre de quien genera. (nuevo)
/// </summary>
public sealed record DispersionDeNominaGeneradaV1
{
    public const string Type = "DispersionDeNominaGenerada";

    public Guid FilePublicId { get; init; }

    public CuentaDeOrigenDeLaDispersionV1 BankAccount { get; init; } = new();

    public decimal Total { get; init; }

    public int LineCount { get; init; }

    public DateOnly PayDate { get; init; }

    public Guid RunPublicId { get; init; }

    public UserRefV1 GeneratedBy { get; init; } = new();
}

/// <summary>Los mensajes que emite Nómina por la bandeja (feature 015, T2, fase 41; E-05). Nómina es el módulo de origen <c>NOM</c>. (nuevo)</summary>
public static class CatalogoDeMensajesDeNominaV1
{
    public static IReadOnlyList<TipoDeMensaje> Todos { get; } =
    [
        new(typeof(DispersionDeNominaGeneradaV1), DispersionDeNominaGeneradaV1.Type, 1, ModulosDeOrigen.Nomina, TipoDeMensaje.Un(IntegrationDestinations.Treasury),
            IntegrationMessageKind.Business, FormaDeClaveDeEvento.ConfirmacionPorPublicId, AdmiteProceso: false),
    ];
}
