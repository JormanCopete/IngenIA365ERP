using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_CreditApplicationParties</c> (feature 013; data-model §2.2; FR-035, FR-036, FR-043): solicitante, codeudores y avalistas de
/// la solicitud con los datos de capacidad de cada uno. Exactamente un <see cref="CreditPartyRole.Applicant"/>, que es el
/// <c>ApplicantPersonId</c> de la solicitud. Se edita en <c>Draft</c> y <c>Returned</c>; no es hecho.
/// </summary>
public class CreditApplicationParty : AuditableEntity
{
    public int ApplicationId { get; set; }

    public int PersonId { get; set; }

    public CreditPartyRole Role { get; set; }

    public int Order { get; set; }

    /// <summary>Ingreso mensual declarado con soporte (FR-036).</summary>
    public decimal? MonthlyIncome { get; set; }

    /// <summary>Gasto mensual declarado con soporte (FR-036).</summary>
    public decimal? MonthlyExpenses { get; set; }

    /// <summary>Cuotas con terceros, declaradas o de centrales.</summary>
    public decimal? ExternalInstallments { get; set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0388) --

    public const string CodigoParteRepetida = "Lending.Application.DuplicateParty";

    /// <summary>Una parte de la solicitud (data-model §2.2): el solicitante va en el orden 0 y los codeudores y avalistas después.</summary>
    public static CreditApplicationParty Nueva(int applicationId, int personId, CreditPartyRole papel, int orden) => new()
    {
        ApplicationId = applicationId,
        PersonId = personId,
        Role = papel,
        Order = orden,
    };

    /// <summary>
    /// Lo que está mal en las partes, o nulo (FR-035, FR-043): exactamente un solicitante y una persona no está dos veces
    /// (<c>Lending.Application.DuplicateParty</c>, con el <c>PublicId</c> que llega del comando en <paramref name="nombreDe"/>).
    /// </summary>
    public static ProblemaDeCartera? ValidarPartes(IReadOnlyCollection<(int PersonId, CreditPartyRole Papel)> partes, Func<int, object?>? nombreDe = null)
    {
        ArgumentNullException.ThrowIfNull(partes);
        if (partes.Count(p => p.Papel == CreditPartyRole.Applicant) != 1)
            throw new ArgumentException("La solicitud tiene exactamente un solicitante.", nameof(partes));
        var repetida = partes.GroupBy(p => p.PersonId).FirstOrDefault(g => g.Count() > 1);
        return repetida is null
            ? null
            : ProblemaDeCartera.Con(CodigoParteRepetida,
                "Una persona aparece dos veces en la solicitud (como solicitante y codeudor, o como codeudor repetido). Quítela de una de las dos.",
                "personPublicId", nombreDe?.Invoke(repetida.Key) ?? repetida.Key);
    }

    /// <summary>La capacidad declarada (FR-036): ingreso, gasto y cuotas con terceros, todos no negativos.</summary>
    public void Declarar(decimal? ingreso, decimal? gasto, decimal? cuotasExternas)
    {
        if (ingreso < 0m || gasto < 0m || cuotasExternas < 0m)
            throw new ArgumentException("Ingresos, gastos y cuotas no son negativos.");
        MonthlyIncome = ingreso;
        MonthlyExpenses = gasto;
        ExternalInstallments = cuotasExternas;
    }
}
