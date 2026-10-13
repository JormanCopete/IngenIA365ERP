using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La fecha contable de un hecho de Cartera (feature 013, T0228, T0440, C106; T36, FR-012; contracts/contabilidad.md §8). Cartera la calcula
/// <b>al emitir</b> y la pone en <c>Origin.OperationDate</c>; Contabilidad fecha el comprobante con ella y nunca la corre.
///
/// <para>
/// Sobre <see cref="IContabilidadParaCartera.EstadoDelPeriodoAsync"/>:
/// <list type="bullet">
/// <item>sin contabilidad iniciada, la fecha valor (el hecho sale <c>NoPasa</c>);</item>
/// <item>el mes de la fecha valor abierto: la fecha valor;</item>
/// <item>cerrado: el primer día del primer mes abierto desde él;</item>
/// <item>un mes que no existe (<c>PeriodNotFound</c>, p. ej. el ejercicio siguiente sin abrir el 1 de enero) no se salta ni se inventa: la fecha
/// queda en él —la fecha valor, o el primer día del mes siguiente al cerrado si no hay ninguno abierto después—, la operación sigue y se levanta
/// la alerta <c>Contabilidad.PeriodoInexistente</c> para quien abre períodos; Contabilidad responde <c>Retry</c> hasta que exista (T36).</item>
/// </list>
/// Los cierres de Cartera (<c>LND_MonthlyCloses</c>) no existen hasta US9 (<c>CarteraRiesgo</c>): mientras tanto sólo decide Contabilidad. US9
/// (T0711) suma que un mes con cierre vigente de Cartera también se salta, sin cambiar la firma: el punto de extensión es el estado del mes
/// (un mes con cierre de Cartera vigente cuenta como «cerrado» y se sigue al siguiente, con la misma búsqueda). US7 (T0656) lo completa con el
/// adaptador real: también el primer mes abierto que todavía no existe se queda en él y alerta.
/// </para>
/// <para>
/// Es una lectura en proceso sobre la misma base: si falla, falla la operación (Principio IX), sin fecha supuesta. (nuevo)
/// </para>
/// <para>
/// US9 (T0711; T36, §8, FR-078, FR-080): la mitad de Cartera. Mientras el mes de la fecha tenga un <b>cierre mensual de Cartera vigente</b> (no
/// reabierto), la fecha pasa al primer día del mes siguiente, y así hasta un mes sin cierre; después decide Contabilidad como siempre (y si
/// Contabilidad la corre a un mes que Cartera ya cerró, se vuelve a mirar). El cierre base no cuenta: el primer cierre mensual tras la activación
/// todavía no se hizo. La causación del día y los hechos del propio cierre (último día del mes) nunca saltan: piden la fecha con
/// <c>conCierreDeCartera = false</c>.
/// </para>
/// </summary>
public sealed class FechaContable(IContabilidadParaCartera contabilidad, IAlertas? alertas = null, IApplicationDbContext? db = null)
{
    /// <summary>El permiso de quien abre períodos contables: el destinatario de la alerta de período inexistente.</summary>
    public const string PermisoDePeriodos = "Accounting.Periods.CloseYear";

    /// <summary>Cuántos meses seguidos se recorren a lo sumo buscando uno sin cierre (una cooperativa nunca tiene tantos cerrados adelante).</summary>
    private const int MesesComoMaximo = 120;

    /// <summary>La fecha contable de un hecho con fecha valor <paramref name="fechaValor"/>: salta los meses que Cartera ya cerró.</summary>
    public Task<Result<DateOnly>> CalcularAsync(DateOnly fechaValor, CancellationToken ct) => CalcularAsync(fechaValor, conCierreDeCartera: true, ct);

    /// <summary>
    /// La fecha contable con o sin la mitad de Cartera: <paramref name="conCierreDeCartera"/> falso para la causación del día y los hechos del
    /// cierre y su reapertura, que se fechan en su día aunque el mes ya tenga su cierre.
    /// </summary>
    public async Task<Result<DateOnly>> CalcularAsync(DateOnly fechaValor, bool conCierreDeCartera, CancellationToken ct)
    {
        var fecha = fechaValor;
        for (var i = 0; i < MesesComoMaximo; i++)
        {
            if (conCierreDeCartera && db is not null && await MesCerradoPorCarteraAsync(fecha, ct))
            {
                fecha = new DateOnly(fecha.Year, fecha.Month, 1).AddMonths(1);
                continue;
            }
            var contable = await DeContabilidadAsync(fecha, ct);
            if (contable.IsFailure || contable.Value == fecha || !conCierreDeCartera || db is null) return contable;
            fecha = contable.Value;
        }
        return Result.Success(fecha);
    }

    /// <summary>¿El mes de <paramref name="fecha"/> tiene un cierre mensual de Cartera vigente (no reabierto)?</summary>
    public async Task<bool> MesCerradoPorCarteraAsync(DateOnly fecha, CancellationToken ct)
    {
        if (db is null) return false;
        var anio = (short)fecha.Year;
        var mes = (byte)fecha.Month;
        return await db.MonthlyCloses.AsNoTracking().AnyAsync(c => !c.IsDeleted && c.Kind == MonthlyCloseKind.Monthly && c.PeriodYear == anio
            && c.PeriodMonth == mes && !db.MonthlyCloseReopenings.Any(r => r.MonthlyCloseId == c.Id && !r.IsDeleted), ct);
    }

    /// <summary>La mitad contable (la de US3 y US7): el mes abierto, el primero abierto desde él o el inexistente con su alerta.</summary>
    private async Task<Result<DateOnly>> DeContabilidadAsync(DateOnly fechaValor, CancellationToken ct)
    {
        var estado = await contabilidad.EstadoDelPeriodoAsync(fechaValor, ct);
        if (estado.IsFailure) return Result.Failure<DateOnly>(estado.Error);

        var e = estado.Value;
        if (!e.AccountingInitialized) return Result.Success(fechaValor);
        switch (e.State)
        {
            case EstadoDePeriodoContable.Open:
                return Result.Success(fechaValor);
            case EstadoDePeriodoContable.NotFound:
                await AlertarAsync(fechaValor, ct);
                return Result.Success(fechaValor);
            default:
                if (e.FirstOpenMonthFrom is { } abierto)
                {
                    // El «primer mes abierto» puede ser uno que todavía no existe (el ejercicio siguiente sin abrir): no se salta, la fecha
                    // queda en él y se alerta, igual que con la fecha valor en un mes inexistente (T0656).
                    var siguienteEstado = await contabilidad.EstadoDelPeriodoAsync(abierto, ct);
                    if (siguienteEstado.IsFailure) return Result.Failure<DateOnly>(siguienteEstado.Error);
                    if (siguienteEstado.Value.State == EstadoDePeriodoContable.NotFound) await AlertarAsync(abierto, ct);
                    return Result.Success(abierto);
                }
                var siguiente = new DateOnly(fechaValor.Year, fechaValor.Month, 1).AddMonths(1);
                await AlertarAsync(siguiente, ct);
                return Result.Success(siguiente);
        }
    }

    private async Task AlertarAsync(DateOnly fecha, CancellationToken ct)
    {
        if (alertas is null) return;
        var mes = fecha.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        await alertas.LevantarAsync(new AlertaALevantar(
            TiposDeAlerta.PeriodoInexistente,
            $"Cartera emitió un hecho para {mes}, que no existe en Contabilidad",
            $"Cartera fechó un hecho en {mes} y ese período no existe en Contabilidad. Ábralo para que el hecho se contabilice: hasta entonces queda en reintento.",
            DedupKey: $"{TiposDeAlerta.PeriodoInexistente}:{mes}",
            RecipientPermissions: [PermisoDePeriodos],
            ModuloDeOrigen: "Lending"), ct);
    }
}
