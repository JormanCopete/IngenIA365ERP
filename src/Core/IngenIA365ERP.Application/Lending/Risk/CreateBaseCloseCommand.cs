using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>
/// El cierre base de la activación (feature 013, US9, T0708; T49, C60, C107; data-model §23.1–§23.5): <c>Kind = Base</c>, fechado al
/// <b>corte</b> (no tiene que ser fin de mes), versión 1, con <c>ActivationId</c>; sus líneas salen de <c>LND_MigrationRiskData</c> (una fila por
/// crédito, plantillas.md §5.3.4) enlazadas al <b>cierre</b> —no a una corrida—; su general es la suma del <c>deterioroGeneral</c> de cada crédito
/// por la dimensión del general (oficina, modalidad, sub-segmento, empleado y modelo, la misma con que el primer cierre mensual calcula la
/// diferencia); los meses en E salen de la fecha de ingreso a E y el corte; sólo cuentan las tandas vivas (<c>Imported</c>); y el historial de mora con origen
/// <c>Migration</c> de <c>LND_MigrationArrearsHistory</c>. Sin hechos ni partes: Contabilidad ya tenía esos saldos y el cuadre de activación los
/// comprobó (FR-140). Un solo base por cooperativa (<c>UK_LND_MonthlyCloses_Base</c>). Lo llama la activación en su transacción, después de
/// guardar <c>LND_Activations</c> (con la carga vacía nace un base sin líneas: el primer cierre mensual tiene así su <c>PreviousClose</c>); US8
/// lo completa con las tandas de la carga. No abre transacción. (nuevo)
/// </summary>
public sealed class CierreBase(IApplicationDbContext db, INormativaVigente normativa, IDateTimeService reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<MonthlyClose>> CrearAsync(LendingActivation activacion, MigrationLoad carga, Guid publicId, int? ordenadoPor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(activacion);
        ArgumentNullException.ThrowIfNull(carga);
        if (await db.MonthlyCloses.AsNoTracking().AnyAsync(c => c.Kind == MonthlyCloseKind.Base && !c.IsDeleted, ct))
            return Result.Failure<MonthlyClose>(ErroresDeRiesgo.BaseYaExiste());

        var partes = db.MigrationLoadParts.AsNoTracking()
            .Where(p => p.MigrationLoadId == carga.Id && p.Status == MigrationPartStatus.Imported && !p.IsDeleted).Select(p => p.Id);
        var deCreditos = await db.MigrationRiskDataRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId))
            .OrderBy(r => r.MigrationLoadPartId).ThenBy(r => r.RowNumber).ToListAsync(ct);
        var numeros = deCreditos.Select(f => f.SourceLoanNumber).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().Where(l => l.MigrationLoadId == carga.Id && numeros.Contains(l.Number)).ToDictionaryAsync(l => l.Number, ct);
        var ids = creditos.Values.Select(l => l.Id).ToList();
        var condiciones = (await db.LoanTermsVersions.AsNoTracking().Where(t => ids.Contains(t.LoanId)).ToListAsync(ct))
            .GroupBy(t => t.LoanId).ToDictionary(g => g.Key, g => g.OrderBy(t => t.VersionNumber).Last());
        var saldos = (await db.MigrationBalanceRows.AsNoTracking().Where(b => partes.Contains(b.MigrationLoadPartId) && numeros.Contains(b.SourceLoanNumber))
                .ToListAsync(ct))
            .GroupBy(b => b.SourceLoanNumber).ToDictionary(g => g.Key, g => g.GroupBy(b => b.Concept).ToDictionary(c => c.Key, c => c.Sum(x => x.Amount)));
        var personas = creditos.Values.Select(l => l.DebtorPersonId).Distinct().ToList();
        var empleados = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.IsEmployee, ct);
        var oficinaIds = creditos.Values.Select(l => l.BranchId).Distinct().ToList();
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinaIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.TenantBranchPublicId ?? b.PublicId, ct);
        var catalogo = deCreditos.Count == 0 ? null : await normativa.CatalogoDeModalidadesAsync(carga.CutoffDate, null, ct);

        var ahora = reloj.UtcNow;
        var cierre = new MonthlyClose
        {
            PublicId = publicId,
            Kind = MonthlyCloseKind.Base,
            PeriodYear = (short)carga.CutoffDate.Year,
            PeriodMonth = (byte)carga.CutoffDate.Month,
            Version = 1,
            CloseDate = carga.CutoffDate,
            ActivationId = activacion.Id,
            InformationCutoffAt = ahora,
            NationalValuesWatermark = await db.NationalValueReplications.AsNoTracking().MaxAsync(r => (long?)r.ToSequence, ct) ?? 0L,
            DocumentsUsedJson = "[]",
            ModelBySegmentJson = "[]",
            OrderedByUserId = ordenadoPor,
            OrderedAt = ahora,
            ClosedAt = ahora,
        };

        var lineas = new List<MonthlyCloseLoan>();
        var generalPorLinea = new List<(MonthlyCloseLoan Linea, decimal General)>();
        foreach (var f in deCreditos)
        {
            if (!creditos.TryGetValue(f.SourceLoanNumber, out var credito) || !condiciones.TryGetValue(credito.Id, out var t)) continue;
            var deEste = saldos.GetValueOrDefault(f.SourceLoanNumber) ?? [];
            decimal Saldo(LoanConcept c) => deEste.GetValueOrDefault(c);
            var combinacion = catalogo is { IsSuccess: true } cat ? cat.Value.Documento.Buscar(t.ModalityCode, t.SubsegmentCode) : null;
            var categoria = f.Rating;
            var subcategoria = categoria == RatingCategory.E ? f.Subcategory : RatingSubcategory.None;
            var capital = f.PrincipalImpairment;
            var intereses = f.InterestImpairment;
            var otros = f.OtherImpairment;
            var principal = Saldo(LoanConcept.Principal);
            var linea = new MonthlyCloseLoan
            {
                MonthlyCloseId = 0, // se fija al guardar el encabezado
                LoanId = credito.Id,
                PersonId = credito.DebtorPersonId,
                OfficePublicId = oficinas.GetValueOrDefault(credito.BranchId),
                BranchId = credito.BranchId,
                CreditLineId = credito.CreditLineId,
                ModalityCode = t.ModalityCode,
                SubsegmentCode = t.SubsegmentCode,
                SarcSegmentCode = combinacion?.SarcSegment ?? t.ModalityCode,
                ExpectedLossSegmentCode = combinacion?.ExpectedLossSegment,
                ImpairmentModel = ImpairmentModel.ArrearsAnnex1,
                ModelBasis = ModelBasis.NotObligated,
                IsEmployee = empleados.GetValueOrDefault(credito.DebtorPersonId),
                IsRestructured = t.RestructuringCount > 0,
                RestructuringCount = t.RestructuringCount,
                IsSuspended = f.IsSuspended,
                IsWrittenOff = credito.Status == LoanStatus.WrittenOff,
                DaysPastDueAtClose = f.DaysPastDue,
                MaxDaysPastDueInMonth = f.DaysPastDue,
                MonthsInE = categoria == RatingCategory.E ? MesesEnE(f.EnteredEOn, carga.CutoffDate) : null,
                ArrearsRating = categoria,
                FinalRating = categoria,
                FinalSubcategory = subcategoria,
                AccountingRating = HomologacionDeCalificacion.Contable(categoria, subcategoria),
                RatingCause = f.Cause ?? RatingCause.Arrears,
                CandidatesJson = "[]",
                PrincipalBalance = principal,
                CurrentInterestBalance = Saldo(LoanConcept.CurrentInterest),
                DefaultInterestBalance = Saldo(LoanConcept.DefaultInterest),
                SuspendedInterestBalance = Saldo(LoanConcept.CurrentInterestInSuspense) + Saldo(LoanConcept.DefaultInterestInSuspense),
                OtherBalance = Saldo(LoanConcept.Insurance) + Saldo(LoanConcept.CollectionFee) + Saldo(LoanConcept.OtherCharge),
                BalancesByConceptJson = JsonSerializer.Serialize(deEste.ToDictionary(k => k.Key.ToString(), k => k.Value), Json),
                ImpairmentPercentage = principal > 0m ? Math.Min(1m, Math.Round(capital / principal, 6, MidpointRounding.ToEven)) : 0m,
                PrincipalImpairment = capital,
                InterestImpairment = intereses,
                OtherImpairment = otros,
                TotalImpairment = capital + intereses + otros,
                ExplanationJson = JsonSerializer.Serialize(new[] { $"Dato de riesgo migrado al corte {carga.CutoffDate:yyyy-MM-dd} (fila {f.RowNumber})." }, Json),
            };
            lineas.Add(linea);
            generalPorLinea.Add((linea, f.GeneralImpairment));
        }

        cierre = Completar(cierre, lineas);
        db.MonthlyCloses.Add(cierre);
        await db.SaveChangesAsync(ct);

        foreach (var l in lineas) db.MonthlyCloseLoans.Add(Enlazada(l, cierre.Id));

        // El general migrado: la parte de cada crédito sumada por la dimensión del general (la del cierre mensual, para que el primero emita sólo la
        // diferencia frente a este base).
        foreach (var g in generalPorLinea.Where(x => x.General > 0m)
                     .GroupBy(x => (x.Linea.BranchId, x.Linea.ModalityCode, x.Linea.SubsegmentCode, x.Linea.IsEmployee, x.Linea.ImpairmentModel)))
        {
            var total = g.Sum(x => x.General);
            db.MonthlyCloseGeneralImpairments.Add(new MonthlyCloseGeneralImpairment
            {
                MonthlyCloseId = cierre.Id, BranchId = g.Key.BranchId, ModalityCode = g.Key.ModalityCode, SubsegmentCode = g.Key.SubsegmentCode,
                IsEmployee = g.Key.IsEmployee, ImpairmentModel = g.Key.ImpairmentModel, GrossPortfolio = g.Sum(x => x.Linea.PrincipalBalance),
                MinimumAmount = total, TotalAmount = total, PreviousAmount = 0m, Delta = total,
                SourceJson = JsonSerializer.Serialize(new { source = "MigrationRiskData", loans = g.Count() }, Json),
            });
        }

        var historial = await db.MigrationArrearsHistoryRows.AsNoTracking().Where(h => partes.Contains(h.MigrationLoadPartId)).ToListAsync(ct);
        foreach (var h in historial)
        {
            if (!creditos.TryGetValue(h.SourceLoanNumber, out var credito)) continue;
            db.LoanArrearsHistory.Add(new LoanArrearsHistoryEntry
            {
                LoanId = credito.Id, PeriodYear = h.PeriodYear, PeriodMonth = h.PeriodMonth, MaxDaysPastDue = h.MaxDaysPastDue,
                DaysPastDueAtMonthEnd = h.DaysPastDueAtMonthEnd, Origin = ArrearsHistoryOrigin.Migration, MigrationLoadId = carga.Id,
            });
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(cierre);
    }

    /// <summary>Meses completos en E entre la fecha de ingreso y el corte; sin fecha, cero.</summary>
    public static int MesesEnE(DateOnly? ingreso, DateOnly corte)
    {
        if (ingreso is not { } desde || desde > corte) return 0;
        var meses = (corte.Year - desde.Year) * 12 + corte.Month - desde.Month;
        // Un mes se cumple el mismo día del mes siguiente, o su último día si el mes es más corto (31 de mayo → 30 de noviembre son seis).
        var finDeMes = corte.Day == DateTime.DaysInMonth(corte.Year, corte.Month);
        return corte.Day < desde.Day && !finDeMes ? meses - 1 : meses;
    }

    private static MonthlyClose Completar(MonthlyClose c, IReadOnlyList<MonthlyCloseLoan> lineas) => new()
    {
        PublicId = c.PublicId, Kind = c.Kind, PeriodYear = c.PeriodYear, PeriodMonth = c.PeriodMonth, Version = c.Version, CloseDate = c.CloseDate,
        ActivationId = c.ActivationId, InformationCutoffAt = c.InformationCutoffAt, NationalValuesWatermark = c.NationalValuesWatermark,
        DocumentsUsedJson = c.DocumentsUsedJson, ModelBySegmentJson = c.ModelBySegmentJson, OrderedByUserId = c.OrderedByUserId, OrderedAt = c.OrderedAt,
        ClosedAt = c.ClosedAt, LoanCount = lineas.Count, TotalPrincipal = lineas.Sum(l => l.PrincipalBalance),
        TotalInterest = lineas.Sum(l => l.CurrentInterestBalance + l.DefaultInterestBalance), TotalIndividualImpairment = lineas.Sum(l => l.TotalImpairment),
        TotalGeneralImpairment = 0m, TotalsJson = "{}", LinesSha256 = PartesDelCierre.HuellaDeLineas(lineas),
    };

    private static MonthlyCloseLoan Enlazada(MonthlyCloseLoan l, int cierreId) => new()
    {
        MonthlyCloseId = cierreId, LoanId = l.LoanId, PersonId = l.PersonId, OfficePublicId = l.OfficePublicId, BranchId = l.BranchId,
        CreditLineId = l.CreditLineId, ModalityCode = l.ModalityCode, SubsegmentCode = l.SubsegmentCode, SarcSegmentCode = l.SarcSegmentCode,
        ExpectedLossSegmentCode = l.ExpectedLossSegmentCode, ImpairmentModel = l.ImpairmentModel, ModelBasis = l.ModelBasis, IsEmployee = l.IsEmployee,
        IsRestructured = l.IsRestructured, RestructuringCount = l.RestructuringCount, IsSuspended = l.IsSuspended, IsWrittenOff = l.IsWrittenOff,
        DaysPastDueAtClose = l.DaysPastDueAtClose, MaxDaysPastDueInMonth = l.MaxDaysPastDueInMonth, MonthsInE = l.MonthsInE, ArrearsRating = l.ArrearsRating,
        FinalRating = l.FinalRating, FinalSubcategory = l.FinalSubcategory, AccountingRating = l.AccountingRating, RatingCause = l.RatingCause,
        CandidatesJson = l.CandidatesJson, PrincipalBalance = l.PrincipalBalance, CurrentInterestBalance = l.CurrentInterestBalance,
        DefaultInterestBalance = l.DefaultInterestBalance, SuspendedInterestBalance = l.SuspendedInterestBalance, OtherBalance = l.OtherBalance,
        BalancesByConceptJson = l.BalancesByConceptJson, ImpairmentPercentage = l.ImpairmentPercentage, PrincipalImpairment = l.PrincipalImpairment,
        InterestImpairment = l.InterestImpairment, OtherImpairment = l.OtherImpairment, TotalImpairment = l.TotalImpairment,
        ExplanationJson = l.ExplanationJson,
    };
}

/// <summary>
/// El cierre base de una activación (feature 013, US9, T0708; US8, T0749; C60, C66; data-model §23.1, §32.1). <b>Sin ruta</b>: la activación llama a
/// <see cref="CierreBase"/> dentro de su transacción, después de que nacen los créditos de todas las tandas de la carga; este comando lo deja disponible
/// para las pruebas y para un reintento de soporte. Exige que la activación exista, que la cooperativa no tenga ya su base
/// (<c>Lending.MonthlyClose.BaseAlreadyExists</c>) y, con el puerto de transacción registrado, una transacción en curso de quien lo llama
/// (<c>Lending.Contract.TransactionRequired</c>): no abre ni confirma la suya. El base va fechado al <b>corte</b> (no tiene que ser fin de mes), con
/// <c>ActivationId</c>, sus líneas desde <c>risk-data</c>, el general sumado por dimensión y el historial migrado (origen <c>Migration</c>); sin
/// hechos ni <c>CierreDeCarteraRealizado</c>, y nunca se reabre. (nuevo)
/// </summary>
public sealed record CreateBaseCloseCommand(Guid ActivationPublicId) : IRequest<Result<Guid>>;

public sealed class CreateBaseCloseCommandValidator : AbstractValidator<CreateBaseCloseCommand>
{
    public CreateBaseCloseCommandValidator() => RuleFor(x => x.ActivationPublicId).NotEmpty();
}

public sealed class CreateBaseCloseCommandHandler(IApplicationDbContext db, CierreBase cierreBase, Integration.ITransaccionDelRecaudador? transaccion = null)
    : IRequestHandler<CreateBaseCloseCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateBaseCloseCommand request, CancellationToken ct)
    {
        if (transaccion is not null && !transaccion.HayEnCurso()) return Result.Failure<Guid>(Payments.ErroresDePagos.TransaccionRequerida());
        var activacion = await db.LendingActivations.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == request.ActivationPublicId && !a.IsDeleted, ct);
        if (activacion is null) return Result.Failure<Guid>(new Error("Lending.Activation.NotFound", "La activación no existe."));
        var carga = await db.MigrationLoads.AsNoTracking().FirstAsync(l => l.Id == activacion.MigrationLoadId, ct);
        var creado = await cierreBase.CrearAsync(activacion, carga, Guid.NewGuid(), activacion.RegisteredByUserId, ct);
        return creado.IsFailure ? Result.Failure<Guid>(creado.Error) : Result.Success(creado.Value.PublicId);
    }
}
