using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Accrual;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Accrual;

/// <summary>Lo que causó un crédito en un día al ponerlo al día o en una tanda: las filas, la transición y el estado a ese día. (nuevo)</summary>
public sealed record DiaCausado(DateOnly Dia, IReadOnlyList<MovimientoDeLibro> Filas, TransicionDeEstado? Transicion, EstadoDelCredito Estado);

/// <summary>Las posiciones diarias ya escritas de unos créditos: la última versión y su huella por (crédito, día). (nuevo)</summary>
public static class PosicionesDeCartera
{
    public static async Task<IReadOnlyDictionary<(int LoanId, DateOnly Dia), (int Version, string Huella)>> UltimasAsync(IApplicationDbContext db,
        IReadOnlyCollection<int> loanIds, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var ids = loanIds.ToList();
        var filas = await db.LoanDailyPositions.AsNoTracking()
            .Where(p => ids.Contains(p.LoanId) && p.PositionDate >= desde && p.PositionDate <= hasta)
            .Select(p => new { p.LoanId, p.PositionDate, p.Version, p.InputsSha256 })
            .ToListAsync(ct);
        return filas.GroupBy(p => (p.LoanId, p.PositionDate))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Version).Select(p => (p.Version, p.InputsSha256)).First());
    }

    /// <summary>El texto de la causa de una transición de la causación (mensajes.md §10.6).</summary>
    public static string Causa(TransicionDeEstado t) => t.Clase == TransicionDeCausacion.Suspendida
        ? t.Motivo switch
        {
            MotivoDeSuspension.Reestructuraciones => "RepeatedRestructuring",
            MotivoDeSuspension.Insolvencia => "Insolvency",
            MotivoDeSuspension.Castigo => "WriteOff",
            _ => "ArrearsThreshold",
        }
        : "ArrearsCured";

    /// <summary>El hecho informativo de una transición (<c>CausacionSuspendida</c> o <c>CausacionReanudada</c>, §10.6).</summary>
    public static (string Tipo, object Contenido) HechoDeTransicion(DatosDeHecho datos, TransicionDeEstado t, EstadoDelCredito estado, DateTime registradoEn,
        LoanFactDimensionsV1 dimensiones) =>
        t.Clase == TransicionDeCausacion.Suspendida
            ? (CausacionSuspendidaV1.Type, new CausacionSuspendidaV1
            {
                Loan = datos.Referencia, DebtorPersonPublicId = datos.DeudorPublicId, Dimensions = dimensiones,
                RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(registradoEn, DateTimeKind.Utc)), EffectiveDate = t.Fecha, Cause = Causa(t),
                DaysPastDue = estado.AlturaDeMora.Dias,
            })
            : (CausacionReanudadaV1.Type, new CausacionReanudadaV1
            {
                Loan = datos.Referencia, DebtorPersonPublicId = datos.DeudorPublicId, Dimensions = dimensiones,
                RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(registradoEn, DateTimeKind.Utc)), EffectiveDate = t.Fecha, Cause = Causa(t),
                DaysPastDue = estado.AlturaDeMora.Dias,
            });
}

/// <summary>
/// La puesta al día de un crédito por una operación (feature 013, US4, T0496; T6, C04; data-model §22.4): toda operación sobre un crédito con
/// marca anterior a su fecha valor lo causa primero, día por día desde la marca + 1 hasta esa fecha, <b>en su propia transacción</b> y bajo
/// el cerrojo que ya tomó quien la pide. En dos tiempos, porque la parte <c>LoanCatchUp</c> apunta a la operación (<c>TriggerOperationId</c>)
/// y la operación todavía no existe cuando se calcula:
/// <list type="number">
/// <item><see cref="PrepararAsync"/>, antes de registrar la operación: carga en bloque y causa con el motor (no escribe);</item>
/// <item><see cref="RegistrarAsync"/>, con la operación ya guardada: una parte <c>LoanCatchUp</c> por sucursal y tipo con la operación como
/// tanda (<c>batchNumber = 1</c>, <c>catchUpOperation</c> en el contenido), las filas <c>Accrual</c> con el mensaje de su parte y la
/// operación como origen, la marca de agua y las posiciones de cada día, y los hechos de suspensión o reanudación.</item>
/// </list>
/// La usan el desembolso de un tramo siguiente y las recogidas (por <c>IMotorDeCreditoEnDesembolso</c>) y, desde US5, el contrato. El ciclo del
/// día ya no toma esos créditos y los cuenta como <c>SkippedLoans</c>. Un crédito sin usura certificada para un día se pone al día hasta la víspera
/// de ese día (su marca queda ahí). No guarda. (nuevo)
/// </summary>
public sealed class PuestaAlDiaDeCredito(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    RegistroDeCartera registro,
    PartesDeHechosDeCartera partes,
    IDateTimeService reloj)
{
    private readonly List<(CreditoCargado Cargado, LoanControl Control, IReadOnlyList<DiaCausado> Dias)> _pendientes = [];

    /// <summary>Si hay causación calculada esperando su operación.</summary>
    public bool HayPendiente => _pendientes.Count > 0;

    /// <summary>
    /// Si el crédito ya tiene causación calculada esperando la operación que la pidió (US5: la recogida dentro de un desembolso no la vuelve a
    /// calcular ni se la apropia; la escribe el desembolso con su tramo).
    /// </summary>
    public bool EstaPendiente(int loanId) => _pendientes.Any(p => p.Control.LoanId == loanId);

    /// <summary>Calcula la puesta al día de <paramref name="loanIds"/> hasta <paramref name="hasta"/> (las filas de control ya bloqueadas). No escribe.</summary>
    public async Task PrepararAsync(IReadOnlyCollection<int> loanIds, DateOnly hasta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(loanIds);
        if (loanIds.Count == 0) return;
        var ids = loanIds.Distinct().ToList();
        var controles = await db.LoanControls.Where(c => ids.Contains(c.LoanId) && c.LastAccruedDate < hasta).ToListAsync(ct);
        if (controles.Count == 0) return;
        var cargados = await cargador.CargarAsync(controles.Select(c => c.LoanId).ToList(), null, hasta, ct);
        foreach (var control in controles.OrderBy(c => c.LoanId))
        {
            if (!cargados.TryGetValue(control.LoanId, out var cargado) || !cargado.Listo) continue;
            if (_pendientes.Any(p => p.Control.LoanId == control.LoanId)) continue;
            var dias = Causar(cargado.Entrada!, control.LastAccruedDate, hasta);
            if (dias.Count > 0) _pendientes.Add((cargado, control, dias));
        }
    }

    /// <summary>
    /// Causa con el motor día por día desde <paramref name="marca"/> + 1 hasta <paramref name="hasta"/>; se detiene en el primer día sin usura
    /// certificada (devuelve lo causado hasta la víspera).
    /// </summary>
    public static IReadOnlyList<DiaCausado> Causar(EntradaDeReconstruccion entrada, DateOnly marca, DateOnly hasta)
    {
        var dias = new List<DiaCausado>();
        for (var dia = marca.AddDays(1); dia <= hasta; dia = dia.AddDays(1))
        {
            var r = MotorDeCartera.Causar(entrada, dia.AddDays(-1), dia);
            if (!r.Causo) break;
            dias.Add(new DiaCausado(dia, r.Filas, r.Transicion, r.Estado));
        }
        return dias;
    }

    /// <summary>
    /// Escribe lo preparado con la operación que lo pidió (ya guardada: su <c>Id</c> va en la parte). Devuelve los mensajes. Con
    /// <paramref name="soloCreditos"/>, sólo lo de esos créditos (US5: el contrato escribe lo que él preparó; lo de un desembolso en curso sigue
    /// esperando a su tramo).
    /// </summary>
    public async Task<Result<IReadOnlyList<IntegrationMessage>>> RegistrarAsync(LoanOperation disparadora, CancellationToken ct,
        IReadOnlyCollection<int>? soloCreditos = null)
    {
        ArgumentNullException.ThrowIfNull(disparadora);
        var pendientes = _pendientes.Where(p => soloCreditos is null || soloCreditos.Contains(p.Control.LoanId)).ToList();
        if (pendientes.Count == 0) return Result.Success<IReadOnlyList<IntegrationMessage>>([]);
        if (disparadora.Id == 0) throw new InvalidOperationException("La puesta al día se registra con la operación que la pidió ya guardada.");
        _pendientes.RemoveAll(p => pendientes.Contains(p));
        var datos = await partes.DatosAsync(pendientes.Select(p => p.Cargado), ct);
        var origen = new OrigenDePartes(AccountingFactPartSource.LoanCatchUp, null, disparadora, new Dictionary<DateOnly, int>(), null);
        var lineas = pendientes.SelectMany(p => p.Dias.Select(d => new LineaDeCausacion(datos[p.Control.LoanId], d.Dia, d.Filas))).ToList();
        var masivas = await partes.MasivasAsync(origen, lineas, ct: ct);
        if (masivas.IsFailure) return Result.Failure<IReadOnlyList<IntegrationMessage>>(masivas.Error);

        var ahora = reloj.UtcNow;
        var causaciones = new List<CausacionAEscribir>();
        foreach (var p in pendientes)
            foreach (var d in p.Dias)
                causaciones.Add(new CausacionAEscribir(p.Control, d.Dia, d.Filas
                    .Where(f => f.Monto != 0m)
                    .Select(f => RegistroDeCartera.FilaDeCausacion(p.Control.LoanId, f, null, disparadora, d.Dia, ahora,
                        masivas.Value.Mensaje(p.Control.LoanId, d.Dia, f.Concepto)))
                    .ToList()));
        registro.CausarEnBloque(causaciones);

        // Las posiciones de cada día causado (una versión nueva si el día ya tenía y cambió).
        var desde = pendientes.Min(p => p.Dias[0].Dia);
        var hasta = pendientes.Max(p => p.Dias[^1].Dia);
        var ultimas = await PosicionesDeCartera.UltimasAsync(db, pendientes.Select(p => p.Control.LoanId).ToList(), desde, hasta, ct);
        foreach (var p in pendientes)
            foreach (var d in p.Dias)
            {
                var u = ultimas.GetValueOrDefault((p.Control.LoanId, d.Dia));
                registro.PosicionVersionada(p.Control, d.Estado, p.Cargado.Credito, p.Cargado.Vigentes, u.Version, u.Huella, ahora);
            }

        // Suspensión o reanudación: un hecho por crédito y clase (la última de la puesta al día). Y los anticipos aplicados al vencer en los días
        // puestos al día (US5, paso (5)): un AnticipoAplicado por crédito y día, y sus filas sin operación propia (las lleva esta).
        var hechos = new List<HechoPorCredito>();
        var fuentes = await AnticiposAlVencer.FuentesAsync(db, pendientes.Select(p => p.Control.LoanId).ToList(), ct);
        foreach (var p in pendientes)
            foreach (var d in p.Dias)
                if (AnticiposAlVencer.Hecho(datos[p.Control.LoanId], d, ahora, fuentes[p.Control.LoanId]) is { } anticipo)
                    hechos.Add(anticipo);
        foreach (var p in pendientes)
        {
            var dd = datos[p.Control.LoanId];
            foreach (var t in p.Dias.Where(d => d.Transicion is not null).GroupBy(d => d.Transicion!.Clase).Select(g => g.Last()))
            {
                var categoria = await new Integration.CategoriaContableDeCartera(db).VigenteAsync(p.Control.LoanId, t.Dia, ct);
                var (tipo, contenido) = PosicionesDeCartera.HechoDeTransicion(dd, t.Transicion!, t.Estado, ahora,
                    Integration.EmisionDeCartera.Dimensiones(dd.Hecho(t.Dia), categoria));
                hechos.Add(new HechoPorCredito(tipo, dd, t.Dia, contenido));
            }
        }
        var porCredito = await partes.PorCreditoAsync(origen, hechos, ct);
        if (porCredito.IsFailure) return Result.Failure<IReadOnlyList<IntegrationMessage>>(porCredito.Error);
        foreach (var p in pendientes)
            foreach (var d in p.Dias)
            {
                var filas = AnticiposAlVencer.FilasDelLibro(p.Control.LoanId, d, null, disparadora, ahora,
                    AnticiposAlVencer.Mensaje(porCredito.Value, p.Cargado.Credito.PublicId, d.Dia));
                if (filas.Count > 0) registro.RegistrarTransacciones(filas);
            }
        return Result.Success<IReadOnlyList<IntegrationMessage>>([.. masivas.Value.Mensajes, .. porCredito.Value.Mensajes]);
    }

    /// <summary>Prepara y registra en un paso, cuando la operación ya existe (US5: el contrato pone al día antes de aplicar).</summary>
    public async Task<Result<IReadOnlyList<IntegrationMessage>>> PonerAlDiaAsync(IReadOnlyCollection<int> loanIds, DateOnly hasta, LoanOperation disparadora,
        CancellationToken ct)
    {
        await PrepararAsync(loanIds, hasta, ct);
        return await RegistrarAsync(disparadora, ct, loanIds);
    }
}
