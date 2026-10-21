using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>Un requisito del cierre del mes: código, si se cumple, el detalle y el error que corresponde si no. (nuevo)</summary>
public sealed record RequisitoDelCierre(string Code, bool Met, string Detail)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Error? Error { get; init; }
}

/// <summary>Los requisitos evaluados, el cierre con el que se compara y la versión que tocaría. (nuevo)</summary>
public sealed record EvaluacionDelCierre(int Anio, int Mes, IReadOnlyList<RequisitoDelCierre> Requisitos, MonthlyClose? Anterior, int Version,
    MonthlyClose? Reemplaza, Error? Bloqueo)
{
    public bool Listo => Bloqueo is null && Requisitos.All(r => r.Met);

    /// <summary>El error del primer requisito incumplido (o el bloqueo previo).</summary>
    public Error? PrimerError => Bloqueo ?? Requisitos.FirstOrDefault(r => !r.Met)?.Error;
}

/// <summary>
/// Los requisitos del cierre mensual de Cartera (feature 013, US9, T0704; T47, T49; api.md §12.3; data-model §23.1). Los evalúan la consulta de
/// preparación, la orden (202) y la corrida al empezar y en su transacción final:
/// <list type="bullet">
/// <item><c>PreviousClose</c>: el cierre mensual vigente del mes anterior o, en el primero tras activar, el cierre base aunque sea del mismo mes
/// (<c>Lending.MonthlyClose.PreviousMissing</c>);</item>
/// <item><c>AccountingPeriodOpen</c>: el mes abierto en Contabilidad por <see cref="IContabilidadParaCartera.EstadoDelPeriodoAsync"/> (sin
/// contabilidad iniciada se cumple: los hechos salen <c>NoPasa</c>) (<c>.AccountingPeriodClosed</c>);</item>
/// <item><c>AccrualUpToDate</c>: todo crédito vivo causado hasta el último día del mes por <c>LND_LoanControls</c> (<c>.AccrualBehind</c>);</item>
/// <item><c>ReplicaSynced</c>: la réplica de valores nacionales al día; en la consulta, que hay réplica; la orden la pide a la central y la
/// corrida la vuelve a pedir antes de fijar la marca de agua (<c>.ReplicationUnavailable</c>, B7);</item>
/// <item><c>NoRunInProgress</c>: ninguna corrida de cierre o reapertura pedida o corriendo (<c>Lending.Process.AlreadyRunning</c>).</item>
/// </list>
/// Antes de los requisitos: Cartera activada (<c>Lending.NotActivated</c>), el mes terminado (<c>.MonthNotEnded</c>) y sin una versión vigente
/// (<c>.AlreadyClosed</c>). (nuevo)
/// </summary>
public sealed class RequisitosDelCierreMensual(IApplicationDbContext db, IContabilidadParaCartera contabilidad, IDateTimeService reloj)
{
    public const string PreviousClose = nameof(PreviousClose);
    public const string AccountingPeriodOpen = nameof(AccountingPeriodOpen);
    public const string AccrualUpToDate = nameof(AccrualUpToDate);
    public const string ReplicaSynced = nameof(ReplicaSynced);
    public const string NoRunInProgress = nameof(NoRunInProgress);

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <param name="corridaPropia">La corrida que se evalúa a sí misma (no cuenta como «otra en curso»).</param>
    public async Task<EvaluacionDelCierre> EvaluarAsync(int anio, int mes, int? corridaPropia, CancellationToken ct)
    {
        var fin = MonthlyClose.UltimoDiaDe(anio, mes);
        var activacion = await db.LendingSetups.AsNoTracking().Where(s => s.ActivationDate != null).Select(s => s.ActivationDate).FirstOrDefaultAsync(ct);
        if (activacion is null) return Bloqueada(anio, mes, new Error("Lending.NotActivated",
            "Cartera todavía no está activada en esta cooperativa. Esta operación estará disponible cuando se registre la activación."));
        if (reloj.HoyLocal <= fin) return Bloqueada(anio, mes, ErroresDeRiesgo.MesNoTerminado(anio, mes, reloj.HoyLocal));

        var vigentes = await CierresVigentesAsync(db, ct);
        if (vigentes.FirstOrDefault(c => c.Kind == MonthlyCloseKind.Monthly && c.PeriodYear == anio && c.PeriodMonth == mes) is { } hecho)
            return Bloqueada(anio, mes, ErroresDeRiesgo.CierreYaHecho(anio, mes, hecho.Version, hecho.PublicId));

        var requisitos = new List<RequisitoDelCierre>();

        // PreviousClose.
        var anterior = Anterior(vigentes, anio, mes);
        requisitos.Add(anterior is null
            ? new RequisitoDelCierre(PreviousClose, false, $"Falta el cierre vigente de {ErroresDeRiesgo.Mes(fin.AddMonths(-1).Year, fin.AddMonths(-1).Month)} o el cierre base.")
                { Error = ErroresDeRiesgo.CierreAnteriorFaltante(anio, mes) }
            : new RequisitoDelCierre(PreviousClose, true, anterior.Kind == MonthlyCloseKind.Base
                ? $"Primer cierre tras la activación: se compara con el cierre base del {anterior.CloseDate:yyyy-MM-dd}."
                : $"Cierre de {ErroresDeRiesgo.Mes(anterior.PeriodYear, anterior.PeriodMonth)}, versión {anterior.Version.ToString(Inv)}."));

        // AccountingPeriodOpen.
        requisitos.Add(await PeriodoAsync(anio, mes, fin, ct));

        // AccrualUpToDate.
        var atrasados = await (from c in db.LoanControls.AsNoTracking()
                               join l in CargadorDeRiesgo.VivosAl(db.Loans.AsNoTracking(), fin) on c.LoanId equals l.Id
                               where c.LastAccruedDate < fin
                               select c.LastAccruedDate).ToListAsync(ct);
        requisitos.Add(atrasados.Count == 0
            ? new RequisitoDelCierre(AccrualUpToDate, true, $"Todos los créditos vivos están causados hasta el {fin:yyyy-MM-dd}.")
            : new RequisitoDelCierre(AccrualUpToDate, false, $"{atrasados.Count.ToString(Inv)} créditos sin causar hasta el {fin:yyyy-MM-dd}.")
                { Error = ErroresDeRiesgo.CausacionAtrasada(anio, mes, atrasados.Count, atrasados.Min()) });

        // ReplicaSynced (en la consulta: que hay réplica; la orden y la corrida la piden a la central).
        var ultima = await db.NationalValueReplications.AsNoTracking().OrderByDescending(r => r.ToSequence)
            .Select(r => new { r.ToSequence, r.FinishedAt }).FirstOrDefaultAsync(ct);
        requisitos.Add(ultima is null
            ? new RequisitoDelCierre(ReplicaSynced, false, "La cooperativa no tiene réplica de valores nacionales.") { Error = ErroresDeRiesgo.ReplicaNoDisponible(null) }
            : new RequisitoDelCierre(ReplicaSynced, true, $"Última réplica hasta la secuencia {ultima.ToSequence.ToString(Inv)}."));

        // NoRunInProgress.
        var enCurso = await db.LendingProcessRuns.AsNoTracking()
            .Where(r => !r.IsDeleted && (r.Kind == LendingProcessKind.MonthlyClose || r.Kind == LendingProcessKind.CloseReopen)
                && (r.Status == LendingProcessRunStatus.Requested || r.Status == LendingProcessRunStatus.Running) && r.Id != (corridaPropia ?? 0))
            .Select(r => new { r.PublicId, r.Kind }).FirstOrDefaultAsync(ct);
        requisitos.Add(enCurso is null
            ? new RequisitoDelCierre(NoRunInProgress, true, "No hay otra corrida de cierre o reapertura en curso.")
            : new RequisitoDelCierre(NoRunInProgress, false, $"Hay una corrida de {ErroresDeCorridas.Nombre(enCurso.Kind)} en curso.")
                { Error = ErroresDeCorridas.EnCurso(enCurso.Kind, enCurso.PublicId) });

        var reemplaza = await db.MonthlyCloses.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Kind == MonthlyCloseKind.Monthly && c.PeriodYear == anio && c.PeriodMonth == mes)
            .OrderByDescending(c => c.Version).FirstOrDefaultAsync(ct);
        return new EvaluacionDelCierre(anio, mes, requisitos, anterior, (reemplaza?.Version ?? 0) + 1, reemplaza, null);
    }

    private static EvaluacionDelCierre Bloqueada(int anio, int mes, Error error) => new(anio, mes, [], null, 0, null, error);

    private async Task<RequisitoDelCierre> PeriodoAsync(int anio, int mes, DateOnly fin, CancellationToken ct)
    {
        var estado = await contabilidad.EstadoDelPeriodoAsync(fin, ct);
        if (estado.IsFailure)
            return new RequisitoDelCierre(AccountingPeriodOpen, false, $"Contabilidad no respondió: {estado.Error.Message}") { Error = estado.Error };
        var e = estado.Value;
        if (!e.AccountingInitialized) return new RequisitoDelCierre(AccountingPeriodOpen, true, "La contabilidad no está iniciada: los hechos del cierre no pasan.");
        return e.State == EstadoDePeriodoContable.Open
            ? new RequisitoDelCierre(AccountingPeriodOpen, true, $"{ErroresDeRiesgo.Mes(anio, mes)} está abierto en Contabilidad.")
            : new RequisitoDelCierre(AccountingPeriodOpen, false, $"{ErroresDeRiesgo.Mes(anio, mes)} no está abierto en Contabilidad ({e.State}).")
                { Error = ErroresDeRiesgo.PeriodoContableCerrado(anio, mes, e.State.ToString()) };
    }

    /// <summary>Lo que respondió Contabilidad del mes, para sellarlo en la reapertura.</summary>
    public async Task<(bool Abierto, string Json, Error? Error)> EstadoContableAsync(int anio, int mes, CancellationToken ct)
    {
        var estado = await contabilidad.EstadoDelPeriodoAsync(MonthlyClose.UltimoDiaDe(anio, mes), ct);
        if (estado.IsFailure) return (false, "{}", estado.Error);
        var e = estado.Value;
        var json = JsonSerializer.Serialize(new { accountingInitialized = e.AccountingInitialized, state = e.State.ToString(), firstOpenMonthFrom = e.FirstOpenMonthFrom });
        var abierto = !e.AccountingInitialized || e.State == EstadoDePeriodoContable.Open;
        return (abierto, json, abierto ? null : ErroresDeRiesgo.PeriodoContableCerrado(anio, mes, e.State.ToString()));
    }

    /// <summary>¿El mes tiene un cierre mensual vigente (no reabierto)?</summary>
    public static Task<bool> MesConCierreVigenteAsync(IApplicationDbContext db, int anio, int mes, CancellationToken ct)
    {
        var a = (short)anio;
        var m = (byte)mes;
        return db.MonthlyCloses.AsNoTracking().AnyAsync(c => !c.IsDeleted && c.Kind == MonthlyCloseKind.Monthly && c.PeriodYear == a && c.PeriodMonth == m
            && !db.MonthlyCloseReopenings.Any(r => r.MonthlyCloseId == c.Id && !r.IsDeleted), ct);
    }

    /// <summary>Los cierres vigentes (no reabiertos), del más reciente al más antiguo.</summary>
    public static async Task<IReadOnlyList<MonthlyClose>> CierresVigentesAsync(IApplicationDbContext db, CancellationToken ct) =>
        await db.MonthlyCloses.AsNoTracking()
            .Where(c => !c.IsDeleted && !db.MonthlyCloseReopenings.Any(r => r.MonthlyCloseId == c.Id && !r.IsDeleted))
            .OrderByDescending(c => c.CloseDate).ThenByDescending(c => c.Kind).ThenByDescending(c => c.Version)
            .ToListAsync(ct);

    /// <summary>
    /// El cierre con el que se compara el de <paramref name="anio"/>-<paramref name="mes"/> (T49): el mensual vigente del mes anterior o, si después
    /// del base no hay ningún mensual, el base cuyo corte cae en este mes o en el anterior.
    /// </summary>
    public static MonthlyClose? Anterior(IReadOnlyList<MonthlyClose> vigentes, int anio, int mes)
    {
        var previo = new DateOnly(anio, mes, 1).AddMonths(-1);
        var mensual = vigentes.FirstOrDefault(c => c.Kind == MonthlyCloseKind.Monthly && c.PeriodYear == previo.Year && c.PeriodMonth == previo.Month);
        if (mensual is not null) return mensual;
        var baseDeActivacion = vigentes.FirstOrDefault(c => c.Kind == MonthlyCloseKind.Base);
        if (baseDeActivacion is null) return null;
        var mensualesDespues = vigentes.Any(c => c.Kind == MonthlyCloseKind.Monthly && c.CloseDate > baseDeActivacion.CloseDate);
        var mesDelBase = new DateOnly(baseDeActivacion.CloseDate.Year, baseDeActivacion.CloseDate.Month, 1);
        var esteMes = new DateOnly(anio, mes, 1);
        // El base de un corte a mitad de mes se compara con el cierre de ese mismo mes; el de un corte a fin de mes, con el del mes siguiente.
        var corteAFinDeMes = baseDeActivacion.CloseDate == MonthlyClose.UltimoDiaDe(mesDelBase.Year, mesDelBase.Month);
        return !mensualesDespues && (mesDelBase == esteMes && !corteAFinDeMes || mesDelBase == previo && corteAFinDeMes) ? baseDeActivacion : null;
    }

    /// <summary>¿La corrida es la de este mes (para no contarla como otra en curso)?</summary>
    public static bool EsDelMes(LendingProcessRun corrida, int anio, int mes) => corrida.PeriodYear == anio && corrida.PeriodMonth == mes;
}
