using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;

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
/// suma que un mes con cierre vigente de Cartera también se salta, sin cambiar la firma.
/// </para>
/// <para>
/// Es una lectura en proceso sobre la misma base: si falla, falla la operación (Principio IX), sin fecha supuesta. (nuevo)
/// </para>
/// </summary>
public sealed class FechaContable(IContabilidadParaCartera contabilidad, IAlertas? alertas = null)
{
    /// <summary>El permiso de quien abre períodos contables: el destinatario de la alerta de período inexistente.</summary>
    public const string PermisoDePeriodos = "Accounting.Periods.CloseYear";

    /// <summary>La fecha contable de un hecho con fecha valor <paramref name="fechaValor"/>.</summary>
    public async Task<Result<DateOnly>> CalcularAsync(DateOnly fechaValor, CancellationToken ct)
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
                if (e.FirstOpenMonthFrom is { } abierto) return Result.Success(abierto);
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
