using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La <b>implementación real</b> de <see cref="IConsultaDeObligaciones"/> (feature 013, US5, T0549; nombre fijado en DT §2.9; FR-061, FR-112, C26, C32,
/// C44; contracts/interfaces.md §6.1, §12). Reemplaza a <see cref="ConsultaDeObligacionesSinActivar"/> en <c>DependencyInjection</c>. No escribe ni
/// aplica el alcance de Cartera: la autoriza el permiso del módulo que llama.
/// <list type="bullet">
/// <item><see cref="ObligacionesAsync"/>: por persona, como deudor y (si se pide) como codeudor, las vivas (<c>Active</c>, <c>WrittenOff</c>) y,
/// con <c>IncludeClosed</c>, también las cerradas con <c>ClosedOn</c>; los saldos salen de la reconstrucción a <c>Date</c> cargada en bloque
/// (<see cref="CargadorDeReconstruccion"/>); la clase y la pagaduría, del vínculo vigente de <c>LND_LoanPayrollLinks</c> (C44).</item>
/// <item><see cref="CuotasDeNominaAsync"/>: las cuotas que vencen en el período de las obligaciones vivas como deudor con forma de pago nómina
/// propia, por lo que les falta a la fecha de fin del período —sin las cubiertas por anticipo— y sin vencido acumulado (<c>PastDueIncluded = 0</c>
/// en K1, C32), en el orden de la libranza.</item>
/// <item>Sin activar: <c>NotActivated</c>, sin obligaciones, con <c>MigratedNotActivated</c> para los deudores y codeudores de la carga aplicada
/// vigente (C26).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class ObligacionesDeCartera(IApplicationDbContext db, CargadorDeReconstruccion cargador) : IConsultaDeObligaciones
{
    private static readonly LoanConcept[] Cobrables =
    [
        LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense,
        LoanConcept.DefaultInterestInSuspense, LoanConcept.Insurance, LoanConcept.CollectionFee, LoanConcept.OtherCharge,
    ];

    public async Task<ObligacionesDePersonasDto> ObligacionesAsync(ConsultaDeObligaciones consulta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        var pedidas = consulta.PersonPublicIds.Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => pedidas.Contains(p.PublicId))
            .Select(p => new { p.Id, p.PublicId }).ToListAsync(ct);
        if (!await ActivadaAsync(ct))
        {
            var migradas = await MigradasAsync(personas.Select(p => p.Id).ToList(), ct);
            return new ObligacionesDePersonasDto(EstadoDeCartera.NotActivated, pedidas.Select(p => new ObligacionesDePersonaDto(p,
                personas.Any(x => x.PublicId == p && migradas.Contains(x.Id)), [])).ToList());
        }

        var ids = personas.Select(p => p.Id).ToList();
        var estados = consulta.IncludeClosed
            ? new[] { LoanStatus.Active, LoanStatus.WrittenOff, LoanStatus.PaidOff, LoanStatus.Voided, LoanStatus.Novated }
            : [LoanStatus.Active, LoanStatus.WrittenOff];
        var comoDeudor = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.DebtorPersonId) && !l.IsDeleted && estados.Contains(l.Status))
            .ToListAsync(ct);
        var codeudas = consulta.IncludeAsCoDebtor
            ? await (from p in db.LoanParties.AsNoTracking()
                     join l in db.Loans.AsNoTracking() on p.LoanId equals l.Id
                     where ids.Contains(p.PersonId) && p.Role == CreditPartyRole.CoDebtor && !p.IsDeleted && !l.IsDeleted && estados.Contains(l.Status)
                           && (consulta.IncludeClosed || (p.ReleasedOn == null && p.ExtinguishedOn == null))
                     select new { p.PersonId, Credito = l }).ToListAsync(ct)
            : [];
        var todos = comoDeudor.Concat(codeudas.Select(c => c.Credito)).DistinctBy(l => l.Id).ToList();
        var cargados = await cargador.CargarAsync(todos.Select(l => l.Id).ToList(), null, consulta.Date, ct);
        var lineas = await LineasAsync(todos, ct);
        var vinculos = await VinculosAsync(todos.Select(l => l.Id).ToList(), consulta.Date, ct);

        var salida = new List<ObligacionesDePersonaDto>();
        foreach (var publico in pedidas)
        {
            var persona = personas.FirstOrDefault(p => p.PublicId == publico);
            if (persona is null)
            {
                salida.Add(new ObligacionesDePersonaDto(publico, false, []));
                continue;
            }
            var suyas = comoDeudor.Where(l => l.DebtorPersonId == persona.Id).Select(l => (Credito: l, Rol: RolEnLaObligacion.Debtor))
                .Concat(codeudas.Where(c => c.PersonId == persona.Id && c.Credito.DebtorPersonId != persona.Id)
                    .Select(c => (Credito: c.Credito, Rol: RolEnLaObligacion.CoDebtor)))
                .OrderBy(x => x.Credito.Number, StringComparer.Ordinal)
                .Select(x => Obligacion(x.Credito, x.Rol, cargados.GetValueOrDefault(x.Credito.Id), consulta.Date, lineas, vinculos))
                .ToList();
            salida.Add(new ObligacionesDePersonaDto(publico, false, suyas));
        }
        return new ObligacionesDePersonasDto(EstadoDeCartera.Active, salida);
    }

    public async Task<CuotasDeNominaDto> CuotasDeNominaAsync(ConsultaDeCuotasDeNomina consulta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        var pedidas = consulta.PersonPublicIds.Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => pedidas.Contains(p.PublicId))
            .Select(p => new { p.Id, p.PublicId }).ToListAsync(ct);
        var ids = personas.Select(p => p.Id).ToList();
        if (!await ActivadaAsync(ct))
        {
            var migradas = await MigradasAsync(ids, ct);
            return new CuotasDeNominaDto(EstadoDeCartera.NotActivated, [], personas.Where(p => migradas.Contains(p.Id)).Select(p => p.PublicId).ToList());
        }

        var fin = consulta.PeriodEnd;
        var creditos = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.DebtorPersonId) && !l.IsDeleted && l.Status == LoanStatus.Active)
            .ToListAsync(ct);
        var vinculos = await VinculosAsync(creditos.Select(l => l.Id).ToList(), fin, ct);
        var deNomina = creditos.Where(l => vinculos.TryGetValue(l.Id, out var v) && v.RepaymentChannel == RepaymentChannel.OwnPayroll).ToList();
        var cargados = await cargador.CargarAsync(deNomina.Select(l => l.Id).ToList(), null, fin, ct);
        var lineas = await LineasAsync(deNomina, ct);

        var cuotas = new List<CuotaDeNominaDto>();
        foreach (var persona in personas)
        {
            var orden = 0;
            foreach (var credito in deNomina.Where(l => l.DebtorPersonId == persona.Id)
                         .OrderBy(l => vinculos[l.Id].LibranzaDate ?? l.OpenedOn).ThenBy(l => l.Number, StringComparer.Ordinal))
            {
                if (!cargados.TryGetValue(credito.Id, out var cargado) || !cargado.Listo) continue;
                var estado = MotorDeCartera.Reconstruir(cargado.Entrada!, fin);
                if (estado.Plan is null) continue;
                foreach (var cuota in estado.Plan.Cuotas.Where(c => c.Vencimiento >= consulta.PeriodStart && c.Vencimiento <= fin).OrderBy(c => c.Numero))
                {
                    var falta = estado.Saldos.Where(s => s.Cuota == cuota.Numero && Cobrables.Contains(s.Concepto) && s.Valor > 0m).Sum(s => s.Valor);
                    if (falta <= 0m) continue;
                    orden += 1;
                    cuotas.Add(new CuotaDeNominaDto(persona.PublicId, credito.PublicId, credito.Number, lineas.GetValueOrDefault(credito.CreditLineId).Code ?? string.Empty,
                        cuota.Numero, cuota.Vencimiento, falta, 0m, orden));
                }
            }
        }
        return new CuotasDeNominaDto(EstadoDeCartera.Active, cuotas, []);
    }

    private static ObligacionDto Obligacion(Loan credito, RolEnLaObligacion rol, CreditoCargado? cargado, DateOnly fecha,
        IReadOnlyDictionary<int, (string Code, string Name)> lineas, IReadOnlyDictionary<int, VinculoVigente> vinculos)
    {
        var linea = lineas.GetValueOrDefault(credito.CreditLineId);
        var vinculo = vinculos.GetValueOrDefault(credito.Id);
        var clase = vinculo?.RepaymentChannel is RepaymentChannel.OwnPayroll or RepaymentChannel.PaymasterDeduction
            ? ClaseDeObligacion.PayrollDeduction
            : ClaseDeObligacion.Standard;
        var vivo = credito.Status is LoanStatus.Active or LoanStatus.WrittenOff;
        DateOnly? cerrada = credito.Status switch
        {
            LoanStatus.PaidOff => credito.PaidOffOn,
            LoanStatus.Voided => credito.VoidedOn,
            LoanStatus.Novated => credito.NovatedOn,
            _ => null,
        };
        EstadoDelCredito? e = cargado is { Listo: true } ? MotorDeCartera.Reconstruir(cargado.Entrada!, fecha) : null;
        if (e?.Plan is null)
            return new ObligacionDto(credito.PublicId, credito.Number, linea.Code ?? string.Empty, linea.Name ?? string.Empty, clase, null, rol,
                ComprobantesDePago.Estado(credito.Status), vivo, cerrada, 0m, 0m, 0, 0, 0m, 0m, 0m, 0, 0m);
        var total = Cobrables.Sum(c => e.Saldo(c));
        var proxima = e.Plan.Cuotas.FirstOrDefault(c => c.Vencimiento > fecha);
        return new ObligacionDto(credito.PublicId, credito.Number, linea.Code ?? string.Empty, linea.Name ?? string.Empty, clase,
            vinculo?.PaymasterPersonPublicId, rol, ComprobantesDePago.Estado(credito.Status), vivo, cerrada, total, ComprobantesDePago.Exigible(e),
            e.Cuotas.Count(c => c.Vencida), e.AlturaDeMora.Dias, e.Saldo(LoanConcept.Principal), e.Saldo(LoanConcept.CurrentInterest),
            e.Saldo(LoanConcept.DefaultInterest), e.Cuotas.Count(c => c.CapitalPendiente > 0m),
            e.Plan.CuotaNivelada ?? proxima?.Total ?? 0m);
    }

    private async Task<IReadOnlyDictionary<int, (string Code, string Name)>> LineasAsync(IReadOnlyCollection<Loan> creditos, CancellationToken ct)
    {
        var ids = creditos.Select(c => c.CreditLineId).Distinct().ToList();
        return await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => (l.Code, l.Name), ct);
    }

    /// <summary>El vínculo de forma de pago vigente a la fecha de cada crédito, con el <c>PublicId</c> de su pagaduría.</summary>
    private async Task<IReadOnlyDictionary<int, VinculoVigente>> VinculosAsync(IReadOnlyCollection<int> loanIds, DateOnly fecha, CancellationToken ct)
    {
        var ids = loanIds.ToList();
        var filas = await (from v in db.LoanPayrollLinks.AsNoTracking()
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on v.PaymasterPersonId equals p.Id into pp
                           from p in pp.DefaultIfEmpty()
                           where ids.Contains(v.LoanId) && !v.IsDeleted && v.ValidFrom <= fecha && (v.ValidTo == null || v.ValidTo >= fecha)
                           select new { v.LoanId, v.RepaymentChannel, v.LibranzaDate, v.ValidFrom, Pagaduria = p == null ? (Guid?)null : p.PublicId }).ToListAsync(ct);
        return filas.GroupBy(f => f.LoanId).ToDictionary(g => g.Key, g =>
        {
            var f = g.OrderByDescending(x => x.ValidFrom).First();
            return new VinculoVigente(f.RepaymentChannel, f.LibranzaDate, f.Pagaduria);
        });
    }

    private Task<bool> ActivadaAsync(CancellationToken ct) =>
        db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct);

    /// <summary>Las personas (deudoras o codeudoras) de la carga aplicada vigente que no se ha activado (C26).</summary>
    private async Task<HashSet<int>> MigradasAsync(IReadOnlyCollection<int> personas, CancellationToken ct) =>
        // US8 (T0746): el lector único de la carga aplicada (tandas vivas, deudores de vivos y castigados, codeudores; nada después de activar).
        [.. await new Migration.CargaVigenteDeCartera(db).PersonasAsync(personas, ct)];

    private sealed record VinculoVigente(RepaymentChannel RepaymentChannel, DateOnly? LibranzaDate, Guid? PaymasterPersonPublicId);
}
