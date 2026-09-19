using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;

namespace IngenIA365ERP.Application.Payroll.Novelties.RecurringNovelties;

/// <summary>
/// Cuándo dos recurrentes son <b>la misma orden registrada dos veces</b>: mismo empleado y concepto,
/// vigencias que se cruzan, y o bien el concepto no admite repetirse en el período, o bien traen la
/// misma cantidad y el mismo valor. Una de ésas no se registra (<c>Payroll.RecurringNoveltyDuplicate</c>)
/// ni se materializa: el cálculo genera la más antigua y avisa de la sobrante.
///
/// <para>
/// Nació el 2026-09-19: en producción una deducción fija quedó registrada dos veces con ocho minutos
/// de diferencia, cada cálculo generaba las dos novedades, y anular una no servía porque el siguiente
/// cálculo la volvía a crear. Dos recurrentes del mismo concepto con valores distintos siguen siendo
/// válidas cuando el concepto admite repetirse (dos libranzas con dos entidades).
/// </para>
/// </summary>
public static class RecurrenteRepetida
{
    /// <summary>Activa y con cuotas por emitir: la que ya emitió todas no compite con una nueva.</summary>
    public static bool Vigente(PayrollRecurringNovelty r) =>
        r.IsActive && (r.TotalInstallments is null || r.InstallmentsIssued < r.TotalInstallments);

    public static bool SeCruzan(PayrollRecurringNovelty a, PayrollRecurringNovelty b) =>
        a.StartDate.Date <= (b.EndDate?.Date ?? DateTime.MaxValue.Date) && b.StartDate.Date <= (a.EndDate?.Date ?? DateTime.MaxValue.Date);

    public static bool EsLaMisma(PayrollRecurringNovelty existente, PayrollRecurringNovelty nueva, bool conceptoAdmiteRepetirse) =>
        existente.Id != nueva.Id
        && existente.EmployeeId == nueva.EmployeeId
        && string.Equals(existente.ConceptCode, nueva.ConceptCode, StringComparison.OrdinalIgnoreCase)
        && Vigente(existente)
        && SeCruzan(existente, nueva)
        && (!conceptoAdmiteRepetirse || (existente.Quantity == nueva.Quantity && existente.Amount == nueva.Amount));

    public static string Descripcion(PayrollRecurringNovelty r, string nombreDelConcepto)
    {
        var partes = new List<string> { $"{nombreDelConcepto} desde el {r.StartDate:dd/MM/yyyy}" };
        if (r.EndDate is { } fin) partes.Add($"hasta el {fin:dd/MM/yyyy}");
        if (r.Amount is { } valor) partes.Add($"por {valor:N0}");
        if (r.Quantity is { } cantidad) partes.Add($"cantidad {cantidad:0.##}");
        if (r.TotalInstallments is { } cuotas) partes.Add($"{r.InstallmentsIssued} de {cuotas} cuotas emitidas");
        return string.Join(", ", partes);
    }

    public static Error Reparo(PayrollRecurringNovelty existente, PayrollConceptDefinition concepto) =>
        new("Payroll.RecurringNoveltyDuplicate",
            $"Este empleado ya tiene una novedad recurrente activa de {Descripcion(existente, concepto.Name)} " +
            $"(registrada el {existente.CreatedAt:dd/MM/yyyy HH:mm}; existingPublicId={existente.PublicId}). " +
            (concepto.AllowsRepeatInPeriod
                ? "Es idéntica a la que intenta registrar. Si cambió el valor, desactive la anterior en «Recurrentes» y registre la nueva."
                : $"El concepto {concepto.Name} no admite repetirse en el período. Si cambió el valor, desactive la anterior en «Recurrentes» y registre la nueva."));
}
