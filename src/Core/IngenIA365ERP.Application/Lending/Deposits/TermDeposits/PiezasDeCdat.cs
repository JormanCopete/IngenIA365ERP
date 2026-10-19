using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits;

/// <summary>
/// El destino de un pago, una cancelación o una redención de un CDAT ya resuelto (feature 014, US6, T172, T173): la cuenta de ahorro a la vista (con
/// seguimiento) o el beneficiario del giro con su banco. (nuevo)
/// </summary>
public sealed record DestinoResueltoDeCdat(TermDepositDestinationKind Kind, MemberAccount? Cuenta, Person? Beneficiario, int? BancoId, string? CodigoDeBanco,
    byte? TipoDeCuenta, string? NumeroDeCuenta)
{
    public PayeeV1? Payee => Beneficiario is null
        ? null
        : new PayeeV1
        {
            PersonPublicId = Beneficiario.PublicId, TaxIdType = Beneficiario.IdType ?? string.Empty, TaxId = Beneficiario.TaxId ?? string.Empty,
            Name = ReferenciasDeGobierno.Nombre(Beneficiario), BankCode = CodigoDeBanco, BankAccountNumber = NumeroDeCuenta,
            BankAccountType = TipoDeCuenta?.ToString(CultureInfo.InvariantCulture),
        };
}

/// <summary>
/// Lo común de las operaciones del CDAT después de su constitución (feature 014, US6, T172, T173): el estado que el ciclo y los comandos leen —la versión
/// vigente con su plan, los períodos ya pagados, el fin de la gracia, la instrucción que rige—, el destino sellado o pedido, la retención de un interés y la
/// fila del libro. No escribe. (nuevo)
/// </summary>
public static class PiezasDeCdat
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>El destino cerrado o inexistente de un pago o un vencimiento.</summary>
    public const string CodigoDestinoNoDisponible = "Lending.TermDeposit.DestinationUnavailable";

    /// <summary>
    /// Los períodos pagados de la versión: un pago vivo (<c>LND_InterestSettlements</c> no reversado con su número y su fin, también los de esta unidad de
    /// trabajo) o, en un CDAT migrado, todo período cuya fecha de pago no pasa del corte (lo pagó el sistema anterior).
    /// </summary>
    public static async Task<IReadOnlySet<int>> PeriodosPagadosAsync(IApplicationDbContext db, MemberAccount cuenta, PlanDeCdatCalculado plan, CancellationToken ct)
    {
        var pagos = await db.InterestSettlements.AsNoTracking()
            .Where(s => s.MemberAccountId == cuenta.Id && s.SchedulePeriodNumber != null && s.ReversedByOperationId == null && !s.IsDeleted)
            .Select(s => new { s.PeriodTo, s.SchedulePeriodNumber }).ToListAsync(ct);
        var locales = db.InterestSettlements.Local.Where(s => s.Id == 0 && s.MemberAccountId == cuenta.Id && s.SchedulePeriodNumber != null)
            .Select(s => new { s.PeriodTo, s.SchedulePeriodNumber });
        var todos = pagos.Concat(locales).ToList();
        var corte = LectorDeInteres.CorteDeMigracion(cuenta);
        return plan.Periodos
            .Where(p => todos.Any(x => x.PeriodTo == p.Hasta && x.SchedulePeriodNumber == p.Numero) || (corte is { } c && p.FechaDePago <= c))
            .Select(p => p.Numero).ToHashSet();
    }

    /// <summary>El interés bruto ya pagado (o capitalizado) de la versión: lo que la reliquidación de una redención descuenta (research E142).</summary>
    public static async Task<decimal> PagadoDeLaVersionAsync(IApplicationDbContext db, MemberAccount cuenta, PlanDeCdatCalculado plan, CancellationToken ct)
    {
        var pagados = await PeriodosPagadosAsync(db, cuenta, plan, ct);
        return plan.Periodos.Where(p => pagados.Contains(p.Numero)).Sum(p => p.Interes);
    }

    /// <summary>El último día de la gracia de la versión (D17, días hábiles).</summary>
    public static async Task<DateOnly> FinDeLaGraciaAsync(LectorDeCdat lector, MemberAccountTermsVersion v, PlanDeCdatCalculado plan, CancellationToken ct)
    {
        var calendario = await lector.CalendarioAsync(plan.Vencimiento, plan.Vencimiento.AddDays(((v.GraceBusinessDays ?? 0) + 1) * (int)RepaymentPeriodicity.Weekly), ct);
        return PlanDeCdat.FinDeLaGracia(plan.Vencimiento, v.GraceBusinessDays ?? 0, calendario);
    }

    /// <summary>La instrucción que rige al vencimiento: la que cambió el titular (<c>MaturityInstructionJson</c>) o la sellada; por defecto prorrogar.</summary>
    public static MaturityInstruction InstruccionVigente(MemberAccount cuenta, MemberAccountTermsVersion v) =>
        InstruccionDeCdat.Leer(cuenta.MaturityInstructionJson)?.Instruccion ?? v.MaturityInstruction ?? MaturityInstruction.Rollover;

    /// <summary>El destino sellado en la versión (o el que cambió el titular con su instrucción), resuelto con sus Ids internos.</summary>
    public static async Task<Result<DestinoResueltoDeCdat>> DestinoAsync(IApplicationDbContext db, MemberAccount cuenta, MemberAccountTermsVersion v, bool alVencimiento,
        CancellationToken ct)
    {
        var cambiado = alVencimiento ? InstruccionDeCdat.Leer(cuenta.MaturityInstructionJson)?.Destino : null;
        if (cambiado is not null) return await DestinoAsync(db, cambiado, ct);
        if (v.InterestDestination == InterestDestination.ToAccount)
        {
            var destino = v.DestinationAccountId is { } id ? await db.MemberAccounts.FirstOrDefaultAsync(a => a.Id == id, ct) : null;
            return destino is { Viva: true }
                ? Result.Success(new DestinoResueltoDeCdat(TermDepositDestinationKind.Account, destino, null, null, null, null, null))
                : Result.Failure<DestinoResueltoDeCdat>(new ErrorConDatos(CodigoDestinoNoDisponible,
                    $"La cuenta destino del CDAT {cuenta.Number} no existe o está cancelada: cambie la instrucción con otro destino.", new { accountNumber = cuenta.Number }));
        }
        var persona = v.DestinationPersonId is { } p ? await db.People.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == p, ct) : null;
        if (persona is null)
            return Result.Failure<DestinoResueltoDeCdat>(new ErrorConDatos(CodigoDestinoNoDisponible,
                $"El CDAT {cuenta.Number} no tiene beneficiario del giro.", new { accountNumber = cuenta.Number }));
        var codigo = v.DestinationBankId is { } b ? await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == b).Select(x => x.TransferCode).FirstOrDefaultAsync(ct) : null;
        return Result.Success(new DestinoResueltoDeCdat(TermDepositDestinationKind.PaymentOrder, null, persona, v.DestinationBankId, codigo, v.DestinationAccountType,
            v.DestinationBankAccountNumber));
    }

    /// <summary>Un destino pedido (ya validado por quien lo pide), resuelto con sus Ids internos.</summary>
    public static async Task<Result<DestinoResueltoDeCdat>> DestinoAsync(IApplicationDbContext db, DestinoDeCdat d, CancellationToken ct)
    {
        if (d.Kind == TermDepositDestinationKind.Account)
        {
            var cuenta = d.AccountPublicId is { } a ? await db.MemberAccounts.FirstOrDefaultAsync(x => x.PublicId == a && !x.IsDeleted, ct) : null;
            return cuenta is { Viva: true }
                ? Result.Success(new DestinoResueltoDeCdat(TermDepositDestinationKind.Account, cuenta, null, null, null, null, null))
                : Result.Failure<DestinoResueltoDeCdat>(new ErrorConDatos(CodigoDestinoNoDisponible, "La cuenta destino no existe o está cancelada.", new { field = "destination" }));
        }
        var persona = d.PayeePersonPublicId is { } p ? await db.People.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(x => x.PublicId == p, ct) : null;
        if (persona is null) return Result.Failure<DestinoResueltoDeCdat>(new ErrorConDatos(CodigoDestinoNoDisponible, "El beneficiario del giro no existe.", new { field = "destination" }));
        var banco = d.BankPublicId is { } bp
            ? await db.Banks.AsNoTracking().Where(x => x.PublicId == bp && !x.IsDeleted).Select(x => new { x.Id, x.TransferCode }).FirstOrDefaultAsync(ct)
            : null;
        return Result.Success(new DestinoResueltoDeCdat(TermDepositDestinationKind.PaymentOrder, null, persona, banco?.Id, banco?.TransferCode, d.BankAccountType,
            d.BankAccountNumber));
    }

    /// <summary>
    /// La retención de un interés de un CDAT (R06, D10): la política de la versión de la línea a la fecha, el perfil del titular y el interés de cada día del
    /// período (vencido: lo causado en el libro; anticipado o sin libro: lo que el plan reparte por día).
    /// </summary>
    public static async Task<Result<(RetencionCalculada Retencion, PoliticaDeRetencion Politica, PerfilDeRetencion Perfil, bool Natural)>> RetencionAsync(
        IApplicationDbContext db, ReglasDeRetencion reglas, MemberAccount cuenta, DepositLineVersion linea, PlanDeCdatCalculado plan, DateOnly desdeExcluido,
        DateOnly hasta, decimal bruto, DateOnly fecha, CancellationToken ct)
    {
        var politica = await reglas.PoliticaAsync(linea, fecha, ct);
        if (politica.IsFailure) return Result.Failure<(RetencionCalculada, PoliticaDeRetencion, PerfilDeRetencion, bool)>(politica.Error);
        var titular = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == cuenta.HolderPersonId, ct);
        var (perfil, natural) = ReglasDeRetencion.PerfilDe(titular);
        var clases = LectorDeInteres.ClasesDeLoCausado.ToArray();
        var delLibro = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuenta.Id && t.Concept == MemberAccountConcept.InterestPayable && clases.Contains(t.Kind) && t.ValueDate > desdeExcluido
                        && t.ValueDate <= hasta && !t.IsDeleted)
            .GroupBy(t => t.ValueDate).Select(g => new { Dia = g.Key, Monto = g.Sum(t => t.Amount) }).ToListAsync(ct);
        IReadOnlyList<InteresDiarioDeRetencion> diarios = delLibro.Count > 0
            ? delLibro.OrderBy(x => x.Dia).Select(x => new InteresDiarioDeRetencion(x.Dia, x.Monto)).ToList()
            : Enumerable.Range(1, Math.Max(0, hasta.DayNumber - desdeExcluido.DayNumber)).Select(i => desdeExcluido.AddDays(i))
                .Select(dia => new InteresDiarioDeRetencion(dia, PlanDeCdat.CausadoDelDia(plan, dia))).ToList();
        var r = RetencionSobreRendimientos.Calcular(politica.Value, new EntradaDeRetencion(perfil, natural, DepositClass.TermDeposit, bruto, diarios));
        return Result.Success((r, politica.Value, perfil, natural));
    }

    /// <summary>El pago de un interés de un CDAT como fila de <c>LND_InterestSettlements</c> (data-model §4.5), con su período del plan.</summary>
    public static InterestSettlement Pago(MemberAccount cuenta, MemberAccountOperation op, int periodo, DateOnly desde, DateOnly hasta, decimal causado, decimal bruto,
        RetencionCalculada r, PoliticaDeRetencion politica, PerfilDeRetencion perfil, bool natural, InterestDestination destino) => new()
    {
        MemberAccountId = cuenta.Id,
        Operation = op,
        OperationId = op.Id,
        PeriodFrom = desde.AddDays(1),
        PeriodTo = hasta,
        SchedulePeriodNumber = periodo,
        AccruedInterest = causado,
        GrossInterest = bruto,
        RoundingDifference = bruto - causado,
        ThresholdReached = r.UmbralAlcanzado,
        ThresholdDailyAmount = r.UmbralDiario,
        InflationaryComponentFraction = r.ComponenteInflacionario,
        InflationaryComponentAmount = r.ComponenteInflacionario is null ? null : r.ComponenteInflacionarioValor,
        WithholdingBase = r.SeRetiene ? r.BaseGravable : 0m,
        WithholdingRate = r.Tarifa,
        WithholdingAmount = r.Valor,
        WithholdingExemptionReason = r.MotivoDeExencion,
        NetInterest = bruto - r.Valor,
        Destination = destino,
        TaxProfileSnapshotJson = JsonSerializer.Serialize(new { profile = perfil.ToString(), naturalPerson = natural, policy = politica, explanation = r.Explicacion }, Json),
        InputsSha256 = new Domain.Lending.HuellaDeCartera().Identificador("account", cuenta.PublicId).Entero("period", periodo).Fecha("to", hasta)
            .Decimal("gross", bruto, 2).Decimal("withholding", r.Valor, 2).Calcular(),
    };

    /// <summary>Una fila del libro de una operación de CDAT (con la versión y el período del plan cuando son del CDAT).</summary>
    public static MemberAccountTransaction Fila(int cuenta, MemberAccountOperation op, MemberAccountTransactionKind clase, MemberAccountConcept concepto, decimal monto,
        DateOnly valor, DateOnly contable, Guid? mensaje, int? terminos = null, int? periodo = null) => new()
    {
        MemberAccountId = cuenta, Operation = op, OperationId = op.Id == 0 ? null : op.Id, Kind = clase, Concept = concepto, Amount = monto, ValueDate = valor,
        AccountingDate = contable, RegisteredAt = op.RegisteredAt, MessagePublicId = mensaje, TermsVersionId = terminos, SchedulePeriodNumber = periodo,
    };

    /// <summary>
    /// La pareja de la retención de <see cref="RetencionSobreRendimientos"/> sobre un interés de un CDAT (−por pagar, +retención; FR-063): nada si no se
    /// retiene. Las operaciones del CDAT la escriben desde aquí, junto al motor que la calcula (R06).
    /// </summary>
    public static IEnumerable<MemberAccountTransaction> FilasDeRetencion(int cuenta, MemberAccountOperation op, RetencionCalculada r, DateOnly valor,
        DateOnly contable, Guid? mensaje, int? terminos, int? periodo)
    {
        if (!r.SeRetiene || r.Valor == 0m) yield break;
        yield return Fila(cuenta, op, MemberAccountTransactionKind.WithholdingApplied, MemberAccountConcept.InterestPayable, -r.Valor, valor, contable, mensaje, terminos,
            periodo);
        yield return Fila(cuenta, op, MemberAccountTransactionKind.WithholdingApplied, MemberAccountConcept.Withholding, r.Valor, valor, contable, mensaje, terminos,
            periodo);
    }

    /// <summary>¿Está el CDAT a disposición (embargado o con el titular fallecido)? No se paga ni se prorroga (FR-072).</summary>
    public static async Task<bool> ADisposicionAsync(IApplicationDbContext db, MemberAccount cuenta, CancellationToken ct) =>
        cuenta.IsGarnished || await db.People.AsNoTracking().IgnoreQueryFilters().AnyAsync(p => p.Id == cuenta.HolderPersonId && p.IsDeceased, ct);
}
