using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Payroll.Settlements;

/// <summary>
/// Lo que la definitiva propone descontar de los créditos del empleado con la cooperativa (feature 013, T0157; R104, G4, FR-088;
/// contracts/interfaces.md §6.2), leído por <see cref="IConsultaDeObligaciones.ObligacionesAsync"/> con
/// <c>IncludeAsCoDebtor: false</c>, nunca de las tablas de Cartera. Según <c>DeduccionAlRetiroModo</c>: <c>SaldoTotal</c> →
/// <see cref="ObligacionDto.TotalBalance"/>; <c>SoloCuotasCausadas</c> → <see cref="ObligacionDto.DueAmount"/>; <c>NoProponer</c> →
/// nada. Todas las obligaciones vivas como deudor (castigadas incluidas), nunca las de codeudor. El desglose JSON conserva las claves
/// de la 010 (<c>capital</c>, <c>intereses</c>, <c>mora</c>, <c>cuotasPendientes</c>, <c>cuota</c>, <c>saldoTotal</c>, <c>modo</c>).
/// Si Cartera falla o no responde, la propuesta sale vacía con el aviso <c>Payroll.Settlement.PortfolioUnavailable</c>: la
/// definitiva no se bloquea. Al aprobar se vuelve a consultar con <see cref="VivasAsync"/>. (nuevo)
/// </summary>
public sealed class PropuestaDeDescuentosDeCartera(IConsultaDeObligaciones obligaciones, IDateTimeService clock, ILogger<PropuestaDeDescuentosDeCartera> logger)
{
    /// <summary>Lo que Cartera propone hoy para la persona, con la marca «migrada, sin activar».</summary>
    public sealed record PropuestaDeCartera(IReadOnlyList<DeudaPropuesta> Deudas, bool MigradaSinActivar, EstadoDeCartera? EstadoDeCartera);

    /// <summary>Propone los descuentos a hoy (fecha local); nunca lanza por un fallo de Cartera (aviso y propuesta vacía).</summary>
    public async Task<PropuestaDeCartera> ProponerAsync(Guid personaPublicId, DeduccionAlRetiro modo, List<WarningDto> avisos, CancellationToken ct)
    {
        if (modo == DeduccionAlRetiro.NoProponer) return new PropuestaDeCartera([], false, null);
        try
        {
            return await VivasAsync(personaPublicId, modo, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Cartera no respondió al proponer los descuentos de la definitiva de la persona {Person}", personaPublicId);
            avisos.Add(SettlementErrors.PortfolioUnavailable(ex.Message));
            return new PropuestaDeCartera([], false, null);
        }
    }

    /// <summary>
    /// La misma propuesta, pero un fallo de Cartera sube como excepción: la usa la aprobación para comparar el conjunto vivo con lo
    /// propuesto (<c>Payroll.Settlement.LoanProposalOutdated</c>) y una falla técnica allí aborta la transacción.
    /// </summary>
    public async Task<PropuestaDeCartera> VivasAsync(Guid personaPublicId, DeduccionAlRetiro modo, CancellationToken ct)
    {
        var fecha = clock.HoyLocal;
        var respuesta = await obligaciones.ObligacionesAsync(new ConsultaDeObligaciones([personaPublicId], fecha, IncludeAsCoDebtor: false), ct);
        var persona = respuesta.People.FirstOrDefault(p => p.PersonPublicId == personaPublicId);
        if (persona is null || modo == DeduccionAlRetiro.NoProponer)
            return new PropuestaDeCartera([], persona?.MigratedNotActivated ?? false, respuesta.LendingStatus);

        var deudas = new List<DeudaPropuesta>();
        foreach (var o in persona.Obligations.Where(o => o.Role == RolEnLaObligacion.Debtor && o.IsLive))
        {
            var propuesto = modo == DeduccionAlRetiro.SaldoTotal ? o.TotalBalance : o.DueAmount;
            if (propuesto <= 0m) continue;
            var desglose = JsonSerializer.Serialize(new
            {
                capital = o.Principal,
                intereses = o.CurrentInterest,
                mora = o.DefaultInterest,
                cuotasPendientes = o.PendingInstallments,
                cuota = o.InstallmentAmount,
                saldoTotal = o.TotalBalance,
                modo = modo.ToString(),
            }, RunJson.Options);
            deudas.Add(new DeudaPropuesta(
                SettlementDeductionKind.CooperativeLoan, o.ObligationPublicId, o.Number, null,
                Descripcion(o), propuesto, propuesto, desglose, WellKnownConceptCodes.LoanDeduction,
                AccountedByOtherModule: false, null, null));
        }
        return new PropuestaDeCartera(deudas, persona.MigratedNotActivated, respuesta.LendingStatus);
    }

    /// <summary>«Crédito {número} ({línea})», dentro de los 200 caracteres de <c>PAY_SettlementDeductions.Description</c>.</summary>
    public static string Descripcion(ObligacionDto o)
    {
        var texto = string.IsNullOrWhiteSpace(o.CreditLineName) ? $"Crédito {o.Number}" : $"Crédito {o.Number} ({o.CreditLineName})";
        return texto.Length <= 200 ? texto : texto[..200];
    }
}
