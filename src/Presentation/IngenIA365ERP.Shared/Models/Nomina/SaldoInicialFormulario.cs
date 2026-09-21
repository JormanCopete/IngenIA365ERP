using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Shared.Services.Nomina;

namespace IngenIA365ERP.Shared.Models.Nomina;

/// <summary>
/// El formulario de «Digitar / Editar / Ajustar saldo inicial» (feature 010, FR-007;
/// contracts/api.md §4). El <c>PUT</c> reemplaza la fila entera, así que el formulario tiene que
/// arrancar con <b>todo</b> lo guardado, no sólo con lo que la grilla muestra: hasta el 2026-09-21
/// se armaba desde el resumen, que no trae los «días ya contados» ni las notas, y editar sólo el
/// valor de cesantías los mandaba en nulo; el motor volvía a contar los días por calendario —lo
/// que el campo existía para evitar— y la nota de origen del dato se perdía en silencio.
/// </summary>
public sealed class SaldoInicialFormulario
{
    public DateTime AsOfDate { get; set; } = DateTime.Today;
    [Range(0, double.MaxValue, ErrorMessage = "No puede ser negativo.")] public decimal PendingVacationDays { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "No puede ser negativo.")] public decimal AccruedSeverance { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "No puede ser negativo.")] public decimal AccruedSeveranceInterest { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "No puede ser negativo.")] public decimal AccruedServiceBonus { get; set; }
    public int? ServiceBonusDaysAccrued { get; set; }
    public int? SeveranceDaysAccrued { get; set; }
    [StringLength(300)] public string Reason { get; set; } = string.Empty;
    [StringLength(500)] public string? Notes { get; set; }

    /// <summary>
    /// Arranca desde lo guardado. <paramref name="vigente"/> es la fila vigente del detalle
    /// (<c>GET /api/payroll/benefit-balances/{id}</c>): trae los días ya contados y las notas que el
    /// resumen no lleva. Sin fila vigente (saldo nuevo) parte de hoy y ceros.
    /// </summary>
    public static SaldoInicialFormulario Desde(SaldoInicialResumenDto resumen, SaldoInicialFilaDto? vigente)
    {
        if (vigente is not null)
        {
            return new SaldoInicialFormulario
            {
                AsOfDate = vigente.AsOfDate.ToDateTime(TimeOnly.MinValue),
                PendingVacationDays = vigente.PendingVacationDays,
                AccruedSeverance = vigente.AccruedSeverance,
                AccruedSeveranceInterest = vigente.AccruedSeveranceInterest,
                AccruedServiceBonus = vigente.AccruedServiceBonus,
                ServiceBonusDaysAccrued = vigente.ServiceBonusDaysAccrued,
                SeveranceDaysAccrued = vigente.SeveranceDaysAccrued,
                Notes = vigente.Notes,
            };
        }

        return new SaldoInicialFormulario
        {
            AsOfDate = resumen.AsOfDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today,
            PendingVacationDays = resumen.PendingVacationDays ?? 0m,
            AccruedSeverance = resumen.AccruedSeverance ?? 0m,
            AccruedSeveranceInterest = resumen.AccruedSeveranceInterest ?? 0m,
            AccruedServiceBonus = resumen.AccruedServiceBonus ?? 0m,
        };
    }

    public GuardarSaldoInicialRequest AGuardar() => new(
        DateOnly.FromDateTime(AsOfDate), PendingVacationDays, AccruedSeverance, AccruedSeveranceInterest, AccruedServiceBonus,
        ServiceBonusDaysAccrued, SeveranceDaysAccrued, Vacio(Notes));

    public AjustarSaldoInicialRequest AAjustar() => new(
        DateOnly.FromDateTime(AsOfDate), PendingVacationDays, AccruedSeverance, AccruedSeveranceInterest, AccruedServiceBonus,
        Reason.Trim(), ServiceBonusDaysAccrued, SeveranceDaysAccrued, Vacio(Notes));

    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
