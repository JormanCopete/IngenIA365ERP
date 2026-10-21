using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Contributions;

/// <summary>El límite de participación que rige para una persona: la fracción y de dónde sale (la línea o el parámetro legal). (nuevo)</summary>
public sealed record LimiteDeParticipacion(decimal Fraccion, string Fuente);

/// <summary>
/// Las reglas de aportes que leen la base (feature 014, US3, T083, T086; data-model §2.5; FR-041 a FR-044, FR-080): el ingreso registrado del asociado,
/// el mínimo no reducible a una fecha, los saldos de aportes de una persona y de la cooperativa, el límite de participación que rige y los créditos
/// vivos que los aportes respaldan. Sólo lee. (nuevo)
/// </summary>
public sealed class ReglasDeAportes(IApplicationDbContext db, ReglasDeParametrosDeCartera parametros, IValoresNacionales valores)
{
    /// <summary>
    /// El ingreso registrado de la persona (research E54): el salario del asociado (<c>COR_Associates.ExternalSalary</c>); nulo si no es asociado o no
    /// lo tiene.
    /// </summary>
    public async Task<decimal?> IngresoAsync(int personaId, CancellationToken ct)
    {
        var salario = await db.Associates.AsNoTracking().Where(a => a.PersonId == personaId && !a.IsDeleted).OrderByDescending(a => a.Id)
            .Select(a => (decimal?)a.ExternalSalary).FirstOrDefaultAsync(ct);
        return salario is > 0m ? salario : null;
    }

    /// <summary>
    /// El mínimo no reducible del asociado a <paramref name="fecha"/> por la versión de la línea (FR-042): valor fijo, fracción del ingreso registrado o
    /// fracción del salario mínimo vigente. Sin la base que el modo pide, cero (no se exige lo que no se puede calcular).
    /// </summary>
    public async Task<decimal> MinimoAsync(ContributionLineVersion version, int personaId, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(version);
        switch (version.MinimumMode)
        {
            case ContributionAmountMode.FixedAmount:
                return version.MinimumAmount ?? 0m;
            case ContributionAmountMode.IncomeFraction:
                return await IngresoAsync(personaId, ct) is { } ingreso ? decimal.Round(ingreso * (version.MinimumFraction ?? 0m), 2, MidpointRounding.AwayFromZero) : 0m;
            case ContributionAmountMode.MinimumWageFraction:
                var smmlv = await valores.LeerAsync(CodigosDeValorNacional.Smmlv, string.Empty, fecha, null, ct);
                return smmlv.IsSuccess && smmlv.Value.Valor is { } s
                    ? decimal.Round(s * (version.MinimumFraction ?? 0m), 2, MidpointRounding.AwayFromZero)
                    : 0m;
            default:
                return 0m;
        }
    }

    /// <summary>El saldo de aportes de las cuentas de aportes vivas de una persona, por clase (Σ del libro con todo lo registrado).</summary>
    public async Task<IReadOnlyDictionary<MemberAccountConcept, decimal>> AportesDeAsync(int personaId, CancellationToken ct)
    {
        var conceptos = MemberAccountTransaction.ConceptosDeAportes.ToArray();
        return await (from t in db.MemberAccountTransactions.AsNoTracking()
                      join a in db.MemberAccounts.AsNoTracking() on t.MemberAccountId equals a.Id
                      where a.HolderPersonId == personaId && a.Nature == LineNature.Contribution && !t.IsDeleted && conceptos.Contains(t.Concept)
                      group t by t.Concept into g
                      select new { Concepto = g.Key, Total = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.Concepto, x => x.Total, ct);
    }

    /// <summary>El capital social de la cooperativa: Σ de las cuatro clases de aporte de todas las cuentas de aportes.</summary>
    public async Task<decimal> CapitalSocialAsync(CancellationToken ct)
    {
        var conceptos = MemberAccountTransaction.ConceptosDeAportes.ToArray();
        return await db.MemberAccountTransactions.AsNoTracking().Where(t => !t.IsDeleted && conceptos.Contains(t.Concept)).SumAsync(t => (decimal?)t.Amount, ct)
               ?? 0m;
    }

    /// <summary>
    /// El límite individual de participación que rige (FR-043): el más estricto entre el de la línea y el legal de la clase de persona
    /// (<c>Aportes.LimiteParticipacionPersonaNatural</c> o <c>…Juridica</c>); nulo si ninguno está fijado.
    /// </summary>
    public async Task<LimiteDeParticipacion?> LimiteAsync(ContributionLineVersion version, int personaId, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(version);
        var juridica = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == personaId)
            .Select(p => p.PersonType).FirstOrDefaultAsync(ct) is { } tipo && tipo.Trim() == LectorDeCuentas.PersonaJuridica;
        var clave = juridica ? ParametrosDeCartera.AportesLimiteParticipacionPersonaJuridica : ParametrosDeCartera.AportesLimiteParticipacionPersonaNatural;
        var legal = await DecimalAsync(clave, fecha, ct);
        LimiteDeParticipacion? limite = legal is { } l && l > 0m ? new(l, clave) : null;
        if (version.MaxParticipationFraction is { } propio && propio > 0m && (limite is null || propio < limite.Fraccion))
            limite = new(propio, "line");
        return limite;
    }

    /// <summary>El mínimo irreducible de la entidad (FR-080, <c>Aportes.MinimoIrreducibleEntidad</c>); nulo sin fijar.</summary>
    public Task<decimal?> MinimoDeLaEntidadAsync(DateOnly fecha, CancellationToken ct) => DecimalAsync(ParametrosDeCartera.AportesMinimoIrreducibleEntidad, fecha, ct);

    /// <summary>
    /// Decisión del dueño del 2026-10-18 (D41): de qué fondos sale primero el cruce al retiro (<c>Captaciones.OrdenDeFondosAlRetiro</c>, según los
    /// estatutos); sin valor legible, aportes primero.
    /// </summary>
    public async Task<OrdenDeFondosAlRetiro> OrdenDeFondosAlRetiroAsync(DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.CaptacionesOrdenDeFondosAlRetiro, fecha, ct: ct);
        return leido.IsSuccess && string.Equals(leido.Value.Texto, nameof(OrdenDeFondosAlRetiro.AhorrosPrimero), StringComparison.Ordinal)
            ? OrdenDeFondosAlRetiro.AhorrosPrimero
            : OrdenDeFondosAlRetiro.AportesPrimero;
    }

    /// <summary>Los créditos vivos de la persona como deudora (FR-044): los aportes los respaldan.</summary>
    public async Task<IReadOnlyList<string>> CreditosVivosAsync(int personaId, CancellationToken ct) =>
        await db.Loans.AsNoTracking()
            .Where(l => l.DebtorPersonId == personaId && !l.IsDeleted
                        && (l.Status == LoanStatus.Active || l.Status == LoanStatus.PendingDisbursement || l.Status == LoanStatus.WrittenOff))
            .OrderBy(l => l.Number).Select(l => l.Number).ToListAsync(ct);

    /// <summary>La versión de aportes vigente de la cuenta a <paramref name="fecha"/>.</summary>
    public async Task<ContributionLineVersion?> VersionAsync(MemberAccount cuenta, DateOnly fecha, CancellationToken ct) =>
        await Lines.LectorDeLineasDeCaptacion.VigenteDeAportesAsync(db, cuenta.CreditLineId, fecha, null, ct);

    private async Task<decimal?> DecimalAsync(string clave, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, fecha, ct: ct);
        if (leido.IsFailure) return null;
        return leido.Value.Valor switch
        {
            decimal d => d,
            int i => i,
            _ => decimal.TryParse(leido.Value.Texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null,
        };
    }
}
