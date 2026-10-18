using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollAgreements</c> (feature 013, K2; data-model §24.1; FR-108, FR-112): el convenio de libranza con una pagaduría, que es una
/// <b>persona jurídica</b> del maestro. Lo que cambia con el tiempo (formatos, fechas, tope, ventana de giro, regla del no aplicado) vive en
/// sus versiones (<see cref="PayrollAgreementVersion"/>); aquí sólo el código de la cooperativa, la pagaduría, si es la nómina propia y el
/// estado. Un solo convenio <see cref="IsOwnPayroll"/> vivo (la cooperativa como pagaduría de sus empleados, I13). No es hecho: el estado
/// avanza y la descripción se corrige. (nuevo)
/// </summary>
public class PayrollAgreement : AuditableEntity
{
    /// <summary>Nomenclatura de la cooperativa (<c>CodigoDeCatalogo</c>, 10); único entre vivos.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>La pagaduría: persona jurídica del maestro (FR-108). En la nómina propia, la cooperativa misma.</summary>
    public int PaymasterPersonId { get; set; }

    /// <summary>La nómina propia (FR-112): sin archivo, sólo fija la regla del no descontado (C32).</summary>
    public bool IsOwnPayroll { get; set; }

    public PayrollAgreementStatus Status { get; private set; } = PayrollAgreementStatus.Active;

    public string? Description { get; set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0823) --

    /// <summary>¿Admite originar, trasladar o generar archivo? Sólo activo.</summary>
    public bool Operable => Status == PayrollAgreementStatus.Active;

    /// <summary>Suspende un convenio activo: no admite créditos nuevos ni archivos hasta reactivarlo; lo vinculado sigue vigente.</summary>
    public void Suspender()
    {
        if (Status != PayrollAgreementStatus.Active)
            throw new InvalidOperationException($"El convenio {Code} no está activo: está {Status}.");
        Status = PayrollAgreementStatus.Suspended;
    }

    /// <summary>Reactiva un convenio suspendido. Uno cerrado no se reactiva: se crea otro.</summary>
    public void Reactivar()
    {
        if (Status != PayrollAgreementStatus.Suspended)
            throw new InvalidOperationException($"El convenio {Code} no está suspendido: está {Status}.");
        Status = PayrollAgreementStatus.Active;
    }

    /// <summary>
    /// Cierra el convenio. Con créditos vinculados vigentes no se cierra (data-model §24.1): primero se trasladan o pasan a la forma alterna.
    /// </summary>
    /// <param name="creditosVinculadosVigentes">Los vínculos vigentes (<c>LND_LoanPayrollLinks</c> sin <c>ValidTo</c>) que lo nombran.</param>
    public void Cerrar(int creditosVinculadosVigentes)
    {
        if (Status == PayrollAgreementStatus.Closed)
            throw new InvalidOperationException($"El convenio {Code} ya está cerrado.");
        if (creditosVinculadosVigentes > 0)
            throw new InvalidOperationException($"El convenio {Code} tiene {creditosVinculadosVigentes} crédito(s) vinculado(s): no se cierra.");
        Status = PayrollAgreementStatus.Closed;
    }
}
