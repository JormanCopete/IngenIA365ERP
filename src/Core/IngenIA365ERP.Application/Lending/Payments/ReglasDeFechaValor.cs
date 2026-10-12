using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payments;

/// <summary>
/// Hasta qué día hay cierre de Cartera vigente (feature 013, US5, bloque 9B; FR-012, D16): el último día del último mes cerrado. Los cierres
/// (<c>LND_MonthlyCloses</c>) nacen con US9 (<c>CarteraRiesgo</c>); hasta entonces la implementación registrada es <see cref="SinCierresDeCartera"/> y la
/// regla «antes de un cierre» no muerde. US9 la reemplaza sin tocar a quien la usa. (nuevo)
/// </summary>
public interface ICierresDeCartera
{
    /// <summary>El último día cubierto por un cierre de Cartera vigente; nulo si no hay ninguno.</summary>
    Task<DateOnly?> CerradoHastaAsync(CancellationToken ct);
}

/// <summary>Sin cierres de Cartera (hasta US9). (nuevo)</summary>
public sealed class SinCierresDeCartera : ICierresDeCartera
{
    public Task<DateOnly?> CerradoHastaAsync(CancellationToken ct) => Task.FromResult<DateOnly?>(null);
}

/// <summary>El rango de fechas valor que admite un pago hoy: la mínima (nula si no hay ventana, A9), la máxima (hoy) y la ventana en días.</summary>
public sealed record RangoDeFechaValor(DateOnly? Minima, DateOnly Maxima, int Dias);

/// <summary>
/// Las reglas de la <b>fecha valor</b> de un pago (feature 013, US5, T0539; FR-012, FR-142, D4, D16, A9, C41, T9, T84; interfaces §3.7), que usan
/// el contrato y la simulación de pantalla:
/// <list type="number">
/// <item>nunca futura: <c>ValueDate ≤ HoyLocal</c> (T9);</item>
/// <item>hasta <c>Pagos.DiasMaximosFechaValor</c> días atrás (D4), leído por <see cref="ReglasDeParametrosDeCartera"/>; <b>no rige</b> para Nómina
/// (A9: su fecha valor la fija la norma);</item>
/// <item>posterior al corte de la carga activada (<c>LND_Setup.ActivationDate − 1</c>): lo de antes debía venir en la carga
/// (<c>Lending.Contract.BeforeCutoff</c>, T84);</item>
/// <item>si cae en un mes con cierre de Cartera vigente (<see cref="ICierresDeCartera"/>), el usuario que actúa necesita
/// <c>Lending.Payments.BackdateBeforeClose</c> y que el valor no supere su límite de monto (<c>SEC_PermissionAmountLimits</c>): se consulta sobre
/// <b>quien actúa</b> —sus permisos (<see cref="ICurrentUserPermissions"/>, el equivalente de Common del <c>IPermissionChecker</c> de Nómina, que Cartera
/// no puede nombrar) y su límite (<see cref="LimitesDePermisoPorUsuario"/>)—, nunca su alcance de oficina, línea ni monto de Cartera (C41).
/// Misma exención de Nómina.</item>
/// </list>
/// No escribe. (nuevo)
/// </summary>
public sealed class ReglasDeFechaValor(
    IApplicationDbContext db,
    ReglasDeParametrosDeCartera parametros,
    ICurrentUserPermissions permisos,
    IActorActual actorActual,
    ICierresDeCartera cierres,
    IDateTimeService reloj)
{
    /// <summary>La atribución para aplicar con fecha valor anterior a un cierre (D16).</summary>
    public const string PermisoAntesDelCierre = "Lending.Payments.BackdateBeforeClose";

    /// <summary>La ventana por defecto si el parámetro no se puede leer (el defecto del catálogo, D4).</summary>
    public const int DiasPorDefecto = 5;

    /// <summary>El rango que admite la clase de recaudador hoy.</summary>
    public async Task<RangoDeFechaValor> RangoAsync(ClaseDeRecaudador clase, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var dias = await DiasAsync(hoy, ct);
        return new RangoDeFechaValor(clase == ClaseDeRecaudador.Payroll ? null : hoy.AddDays(-dias), hoy, dias);
    }

    /// <summary>El primer error de la fecha valor de un pago de <paramref name="monto"/>, o nulo si la fecha se admite.</summary>
    public async Task<Error?> ValidarAsync(ClaseDeRecaudador clase, DateOnly fechaValor, decimal monto, CancellationToken ct)
    {
        var rango = await RangoAsync(clase, ct);
        var minima = rango.Minima ?? DateOnly.MinValue;
        if (fechaValor > rango.Maxima || fechaValor < minima)
            return ErroresDePagos.FechaValorFueraDeRango(fechaValor, rango.Minima ?? fechaValor, rango.Maxima, rango.Dias);

        var activacion = await db.LendingSetups.AsNoTracking().Where(s => s.Id == LendingSetup.IdDeLaFilaUnica)
            .Select(s => s.ActivationDate).FirstOrDefaultAsync(ct);
        if (activacion is { } activada && fechaValor <= activada.AddDays(-1))
            return ErroresDePagos.AntesDelCorte(fechaValor, activada.AddDays(-1));

        if (clase == ClaseDeRecaudador.Payroll) return null;
        if (await cierres.CerradoHastaAsync(ct) is not { } cerrado || fechaValor > cerrado) return null;
        if (!permisos.EsMaestroGlobal && !(await permisos.ListAsync(ct)).Contains(PermisoAntesDelCierre, StringComparer.OrdinalIgnoreCase)) return ErroresDePagos.FechaValorAntesDelCierre(fechaValor, cerrado);
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is { } usuario
            && await LimitesDePermisoPorUsuario.DeAsync(db, usuario, PermisoAntesDelCierre, reloj.HoyLocal, ct) is { } maximo && monto > maximo)
            return ErroresDePagos.FechaValorAntesDelCierre(fechaValor, cerrado);
        return null;
    }

    private async Task<int> DiasAsync(DateOnly hoy, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.PagosDiasMaximosFechaValor, hoy, ct: ct);
        return leido.IsSuccess && int.TryParse(leido.Value.Texto, out var dias) && dias >= 0 ? dias : DiasPorDefecto;
    }
}
