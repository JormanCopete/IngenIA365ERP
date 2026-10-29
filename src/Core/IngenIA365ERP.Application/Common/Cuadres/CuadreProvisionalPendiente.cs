using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Domain.Common.Cuadres;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Cuadres;

/// <summary>El cuadre provisional de la activación que todavía no tiene su definitivo (FR-228). (nuevo)</summary>
public sealed record CuadreProvisionalPendienteDto(Guid SquarePublicId, DateOnly ActivatedOn, DateOnly CutoffDate, DateOnly? DeadlineOn, string? Reason)
{
    /// <summary>Venció la fecha límite del definitivo a <paramref name="hoy"/>.</summary>
    public bool VencidoEn(DateOnly hoy) => DeadlineOn is { } limite && hoy > limite;
}

/// <summary>
/// Lo común de la guarda del cuadre provisional de Recaudo y de Tesorería (feature 015, US24, T0321; FR-167, FR-228; decisión del dueño: cuadre provisional con aprobación y
/// fecha límite). Cada módulo pregunta por <b>sus</b> tablas —<c>RCD_</c> o <c>TES_</c>—, así ninguno nombra al otro (<c>RecaudoYTesoreriaSeHablanPorSusPuertos</c>):
/// <list type="bullet">
/// <item><see cref="PendienteAsync{TCuadre}"/>: la activación vigente tomó un cuadre provisional y ese cuadre sigue <c>ProvisionalApproved</c> (no lo reemplazó un
/// definitivo, que lo deja <c>Superseded</c> con <c>DefinitiveSquareId</c>);</item>
/// <item><see cref="PendienteDelCierre"/>: el pendiente que <c>IEstadoOperativoParaElCierre</c> devuelve al cierre del <b>mes de la activación</b>, que lo niega;</item>
/// <item><see cref="AlertarAsync"/> y <see cref="CerrarAlertasAsync"/>: la alerta diaria (se repite mientras siga pendiente), que escala al vencer la fecha límite
/// —clave aparte y también al dueño (<c>Platform.GoLive.Override</c>)— y que el definitivo cierra.</item>
/// </list>
/// Sólo lee y alerta. (nuevo)
/// </summary>
public static class CuadreProvisionalPendiente
{
    /// <summary>El código del pendiente de cierre (y del error de Contabilidad).</summary>
    public const string CodigoDelPendiente = "Accounting.Period.OpeningSquarePending";

    /// <summary>A quién más va la alerta cuando la fecha límite venció: a quien aprueba seguir con un rojo del go/no-go (el dueño).</summary>
    public const string PermisoDelEscalamiento = "Platform.GoLive.Override";

    /// <summary>El pendiente de la activación (<paramref name="activadaEl"/>, <paramref name="cuadreId"/>), si el cuadre que tomó sigue provisional.</summary>
    public static async Task<CuadreProvisionalPendienteDto?> PendienteAsync<TCuadre>(IQueryable<TCuadre> cuadres, DateOnly activadaEl, int? cuadreId, CancellationToken ct)
        where TCuadre : CuadreDeAperturaBase
    {
        if (cuadreId is not { } id) return null;
        var cuadre = await cuadres.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new { c.PublicId, c.Kind, c.Status, c.CutoffDate, c.DeadlineOn, c.Reason }).FirstOrDefaultAsync(ct);
        if (cuadre is null || cuadre.Kind != OpeningSquareKind.Provisional || cuadre.Status != OpeningSquareState.ProvisionalApproved) return null;
        return new CuadreProvisionalPendienteDto(cuadre.PublicId, activadaEl, cuadre.CutoffDate, cuadre.DeadlineOn, cuadre.Reason);
    }

    /// <summary>El pendiente que bloquea el cierre del mes de la activación; nada si el mes es otro.</summary>
    public static IReadOnlyList<PendienteDeCierreDto> PendienteDelCierre(string modulo, string nombre, CuadreProvisionalPendienteDto? pendiente, int anio, int mes)
    {
        if (pendiente is null || pendiente.ActivatedOn.Year != anio || pendiente.ActivatedOn.Month != mes) return [];
        var limite = pendiente.DeadlineOn is { } d ? string.Create(CultureInfo.InvariantCulture, $" (fecha límite del definitivo: {d:dd/MM/yyyy})") : string.Empty;
        return
        [
            new PendienteDeCierreDto(modulo, CodigoDelPendiente,
                string.Create(CultureInfo.InvariantCulture,
                    $"{nombre} se activó el {pendiente.ActivatedOn:dd/MM/yyyy} con el cuadre de apertura provisional al {pendiente.CutoffDate:dd/MM/yyyy}{limite}: registre el definitivo en su puesta en marcha antes de cerrar el mes."),
                Blocks: true),
        ];
    }

    /// <summary>La clave de la alerta del cuadre: una mientras está en plazo, otra al vencer (la que escala).</summary>
    public static string Clave(string tipoDeAlerta, Guid cuadre, bool vencido) =>
        string.Create(CultureInfo.InvariantCulture, $"{tipoDeAlerta}:{cuadre:N}{(vencido ? ":vencido" : string.Empty)}");

    /// <summary>
    /// La alerta del día: se repite (la misma clave pendiente suma una ocurrencia) mientras el cuadre siga provisional; vencida la fecha límite, otra clave que llega
    /// también al dueño. Devuelve 1 si alertó.
    /// </summary>
    public static async Task<int> AlertarAsync(IAlertas alertas, string tipoDeAlerta, string nombre, string moduloDeOrigen, string entidad,
        CuadreProvisionalPendienteDto? pendiente, DateOnly hoy, CancellationToken ct)
    {
        if (pendiente is null) return 0;
        var vencido = pendiente.VencidoEn(hoy);
        var definicion = TiposDeAlerta.Buscar(tipoDeAlerta);
        IReadOnlyList<string>? destinatarios = vencido
            ? [.. definicion?.Destinatarios ?? [], PermisoDelEscalamiento]
            : null;
        var limite = pendiente.DeadlineOn is { } d ? string.Create(CultureInfo.InvariantCulture, $"{d:dd/MM/yyyy}") : "sin fecha";
        await alertas.LevantarAsync(new AlertaALevantar(tipoDeAlerta,
            vencido ? $"{nombre}: venció la fecha límite del cuadre definitivo" : $"{nombre} sigue con el cuadre de apertura provisional",
            string.Create(CultureInfo.InvariantCulture,
                $"La activación del {pendiente.ActivatedOn:dd/MM/yyyy} tomó el cuadre provisional al {pendiente.CutoffDate:dd/MM/yyyy}; la fecha límite del definitivo es {limite}.")
                + (vencido ? " Ya venció: el cierre contable del mes de la activación sigue negado hasta registrar el definitivo." : " Mientras siga provisional, el cierre contable del mes de la activación se niega."),
            entidad, pendiente.SquarePublicId, DedupKey: Clave(tipoDeAlerta, pendiente.SquarePublicId, vencido), RecipientPermissions: destinatarios,
            ModuloDeOrigen: moduloDeOrigen), ct);
        return 1;
    }

    /// <summary>El definitivo cierra las dos alertas del provisional que reemplazó.</summary>
    public static async Task CerrarAlertasAsync(IAlertas alertas, string tipoDeAlerta, Guid cuadreProvisional, CancellationToken ct)
    {
        const string nota = "Se registró el cuadre definitivo.";
        await alertas.AtenderPorProcesoAsync(Clave(tipoDeAlerta, cuadreProvisional, vencido: false), nota, ct);
        await alertas.AtenderPorProcesoAsync(Clave(tipoDeAlerta, cuadreProvisional, vencido: true), nota, ct);
    }
}
