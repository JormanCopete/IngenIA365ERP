using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollAgreementContacts</c> (feature 013, K2; data-model §24.3; FR-108): un contacto del convenio. Es una persona del maestro
/// (Principio V): su nombre, cargo y medios de contacto viven allí, aquí sólo el papel y la vigencia. Bajo <c>PayrollAgreements.View</c> la
/// consulta muestra nombre, cargo y contacto institucional (C84). (nuevo)
/// </summary>
public class PayrollAgreementContact : AuditableEntity
{
    /// <summary>Los papeles admitidos (<see cref="Role"/>, texto).</summary>
    public static readonly IReadOnlyList<string> Papeles = ["Payroll", "Treasury", "Legal", "Other"];

    public int PayrollAgreementId { get; set; }

    public int PersonId { get; set; }

    /// <summary><c>Payroll</c>, <c>Treasury</c>, <c>Legal</c> u <c>Other</c>.</summary>
    public string Role { get; set; } = "Payroll";

    public bool IsPrimary { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public bool VigenteEn(DateOnly dia) => ValidFrom <= dia && (ValidTo is null || dia <= ValidTo);
}
