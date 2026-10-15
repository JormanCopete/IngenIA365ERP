using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Accrual;

/// <summary>Los números técnicos de una tanda que no viven en la base (sección <c>Lending:Process</c>): créditos por tanda. (nuevo)</summary>
public sealed class ParametrosDeLaTandaDeCartera
{
    /// <summary>El defecto de <c>Lending:Process:BatchSize</c>.</summary>
    public const int TamanoPorDefecto = 250;

    public int TamanoDeTanda { get; set; } = TamanoPorDefecto;
}

/// <summary>Los pasos del ciclo diario (DT §2.13) como se anotan en <c>LND_ProcessRuns.CurrentStep</c> y <c>LND_ProcessRunErrors.Step</c>. (nuevo)</summary>
public static class PasosDelCiclo
{
    public const byte Replica = 1;
    public const byte Reajuste = 2;
    public const byte Causacion = 3;
    public const byte Suspension = 4;
    public const byte Anticipos = 5;
    public const byte Bloqueos = 6;
    public const byte Cobranza = 7;
    public const byte Verificacion = 8;

    public static string Nombre(byte paso) => paso switch
    {
        Replica => "Replica",
        Reajuste => "Reajuste",
        Causacion => "Causacion",
        Suspension => "Suspension",
        Anticipos => "Anticipos",
        Bloqueos => "Bloqueos",
        Cobranza => "Cobranza",
        _ => "Verificacion",
    };
}

/// <summary>
/// Una tanda del ciclo diario de Cartera a la fecha d (feature 013, US4, T0497; DT §2.13, data-model §22.1–§22.4; T5, T6, T14, C02–C04). Los
/// pasos, en orden:
/// <list type="number">
/// <item>la marca de agua de la réplica de valores nacionales que usa la corrida (<c>NationalValuesWatermark</c>) y R (el inicio de la
/// corrida, <c>InformationCutoffAt</c>);</item>
/// <item>el reajuste de las tasas variables cuya fecha cae en lo que se causa (<see cref="ReajusteDeTasa"/>);</item>
/// <item>la causación corriente, de mora y de seguros con el motor, día por día desde la marca + 1 hasta d (<c>MotorDeCartera.Causar</c>), en
/// bloque por <see cref="RegistroDeCartera"/> con las partes y sus mensajes masivos (<see cref="PartesDeHechosDeCartera"/>);</item>
/// <item>la suspensión y la reanudación (<c>CausacionSuspendida</c>/<c>CausacionReanudada</c> por crédito);</item>
/// <item>los anticipos al vencer (US5): <see cref="AnticiposAlVencer"/>, filas <c>AdvanceApplied</c> de la corrida y un <c>AnticipoAplicado</c> por
/// crédito y día;</item>
/// <item>los bloqueos automáticos por mora (<c>Bloqueos.DiasDeMoraParaBloquear</c>) y por retiro del asociado según el maestro (FR-060), con su
/// levantamiento automático, y la dimensión contable cambiada (el deudor que deja de ser empleado o pasa a serlo, C8);</item>
/// <item>el gancho de cobranza (vacío hasta K4);</item>
/// <item>la muestra de verificación del libro, al terminar la corrida.</item>
/// </list>
/// Cada tanda: transacción explícita, cerrojo con salto (el ocupado se cuenta como saltado y lo toma el ciclo siguiente), carga en bloque, motor,
/// registro, partes y mensajes, posiciones y un <c>SaveChanges</c>. Un crédito que falla queda en <c>LND_ProcessRunErrors</c> con
/// <c>Cartera.CreditoSinCausar</c> y su marca no avanza; una modalidad sin usura del día deja un error de grupo (<c>LoanId</c> nulo) con
/// <c>Cartera.ValorNacionalFaltante</c>. Si la tanda entera falla al guardar, se reintenta crédito por crédito. Las partes de una tanda que causa
/// varios días llevan una tanda por fecha (<c>UK_LND_AccountingFactParts_Batch</c> no tiene la fecha): la corrida cuenta esas tandas. (nuevo)
/// </summary>
public sealed class CicloDiarioDeCartera(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    CargadorDeReconstruccion cargador,
    RegistroDeCartera registro,
    PartesDeHechosDeCartera partes,
    ReajusteDeTasa reajuste,
    VerificacionDelLibro verificacion,
    ReglasDeParametrosDeCartera parametros,
    IAlertas alertas,
    ParametrosDeLaTandaDeCartera tanda,
    IDateTimeService reloj,
    ILogger<CicloDiarioDeCartera>? logger = null,
    MovimientoContingenteDeCartera? contingentes = null,
    Risk.CalificacionDelCiclo? calificacion = null,
    ValidacionPreviaDeProceso? validacionDeProceso = null)
{
    /// <summary>Créditos de la muestra de verificación del paso (8).</summary>
    public const int MuestraDeVerificacion = 20;

    public const string ErrorDeCredito = "Cartera.CreditoSinCausar";

    private const int LargoDelMensaje = 1000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ILogger _logger = logger ?? (ILogger)NullLogger.Instance;

    private sealed record AlertaPendiente(AlertaALevantar Alerta, LendingProcessRunError? Error);

    /// <summary>Una tanda de la corrida <c>DailyCycle</c>. Terminada cuando no queda crédito por recorrer después del cursor.</summary>
    public async Task<AvanceDeCorrida> TandaAsync(LendingProcessRun corrida, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (corrida.Kind != LendingProcessKind.DailyCycle || corrida.ProcessDate is not { } d)
            throw new InvalidOperationException("La tanda del ciclo diario es de una corrida DailyCycle con su fecha.");

        // (1) R y la marca de agua de la réplica, una vez por corrida.
        if (corrida.InformationCutoffAt is null)
        {
            corrida.InformationCutoffAt = reloj.UtcNow;
            corrida.NationalValuesWatermark = await db.NationalValueReplications.AsNoTracking().MaxAsync(r => (long?)r.ToSequence, ct);
            corrida.CurrentStep = PasosDelCiclo.Replica;
            corrida.TotalLoans = await CandidatosAsync(d, null).CountAsync(ct);
        }

        var cursor = corrida.CursorLoanId;
        var ids = await CandidatosAsync(d, cursor).OrderBy(c => c.LoanId).Select(c => c.LoanId).Take(Math.Max(1, tanda.TamanoDeTanda)).ToListAsync(ct);
        if (ids.Count == 0)
        {
            await TerminarAsync(corrida, d, ct);
            return AvanceDeCorrida.Terminada;
        }

        corrida.CurrentStep = PasosDelCiclo.Causacion;
        var pendientes = new List<AlertaPendiente>();
        var errores = new List<LendingProcessRunError>();
        // A8 = «Bloquear todo» (2026-10-14): con Contabilidad.FaltaDeRegla = Bloquear, una tanda con hechos que Contabilidad no puede registrar no
        // se guarda; la corrida se detiene con el código y la alerta, y «Reintentar» la retoma desde el mismo cursor.
        DecisionDeValidacionPrevia? detenida = null;
        async Task<Result> GuardarSiEsContabilizableAsync()
        {
            if (validacionDeProceso is not null && await validacionDeProceso.BloqueoAsync(ct) is { } bloqueo)
            {
                detenida = bloqueo;
                return Result.Failure(bloqueo.Bloqueo!);
            }
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
        try
        {
            await TransaccionExplicita.EjecutarAsync(db, async () =>
            {
                await UnaTandaAsync(corrida, d, ids, intento: 1, pendientes, errores, ct);
                return await GuardarSiEsContabilizableAsync();
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // La tanda entera no se pudo guardar: se reintenta crédito por crédito; el que vuelve a fallar queda en los errores.
            _logger.LogWarning(ex, "[Cartera.TandaFallida] La tanda {Tanda} del ciclo {Fecha} falló; se reintenta crédito por crédito.",
                corrida.BatchCount + 1, d);
            Soltar(corrida, []);
            pendientes.Clear();
            errores.Clear();
            foreach (var id in ids)
            {
                var suyas = new List<AlertaPendiente>();
                var suyos = new List<LendingProcessRunError>();
                try
                {
                    await TransaccionExplicita.EjecutarAsync(db, async () =>
                    {
                        await UnaTandaAsync(corrida, d, [id], intento: 2, suyas, suyos, ct);
                        return await GuardarSiEsContabilizableAsync();
                    }, ct);
                    if (detenida is not null) break;
                    pendientes.AddRange(suyas);
                    errores.AddRange(suyos);
                }
                catch (Exception otra) when (otra is not OperationCanceledException)
                {
                    Soltar(corrida, []);
                    var error = Error(corrida, id, 2, PasosDelCiclo.Causacion, otra.GetType().Name, otra.Message, null);
                    db.LendingProcessRunErrors.Add(error);
                    corrida.FailedLoans += 1;
                    await db.SaveChangesAsync(ct);
                    errores.Add(error);
                    pendientes.Add(new AlertaPendiente(AlertaDeCredito(id, d, otra.Message), error));
                }
            }
        }

        if (detenida is not null)
        {
            // La tanda no se guardó: el cursor no se mueve, la alerta sale y la corrida queda Failed con el código (quien conduce lo anota).
            Soltar(corrida, []);
            throw await validacionDeProceso!.DetenerAsync(detenida, string.Create(CultureInfo.InvariantCulture, $"la causación del {d:yyyy-MM-dd}"), ct);
        }

        // Sin seguimiento entre tandas (T67): la corrida y los errores siguen, lo demás se suelta. El cursor se mueve después de soltar, para
        // que el guardado de quien conduce lo vea como cambio.
        Soltar(corrida, errores);
        corrida.CursorLoanId = ids.Max();
        foreach (var p in pendientes)
        {
            var r = await alertas.LevantarAsync(p.Alerta, ct);
            if (p.Error is not null && r.IsSuccess && r.Value.AlertPublicId is { } alerta) p.Error.AlertPublicId = alerta;
        }
        return AvanceDeCorrida.Pendiente;
    }

    /// <summary>
    /// Los créditos vivos después del cursor. Los que ya están al día a d (una operación los puso al día, o nacieron después) se recorren y se
    /// cuentan como saltados (<c>SkippedLoans</c>, data-model §22.4); los demás se causan desde su marca.
    /// </summary>
    private IQueryable<LoanControl> CandidatosAsync(DateOnly d, int? cursor) =>
        from c in db.LoanControls.AsNoTracking()
        join l in db.Loans.AsNoTracking() on c.LoanId equals l.Id
        where l.Status == LoanStatus.Active && (cursor == null || c.LoanId > cursor)
        select c;

    /// <summary>
    /// Suelta todo salvo la corrida y sus errores ya guardados, con su token de concurrencia (bloque 8C: en PostgreSQL la sombra <c>xmin</c> se
    /// perdía al soltar y volver a adjuntar, y el guardado de la corrida fallaba por concurrencia después de la primera tanda).
    /// </summary>
    private void Soltar(LendingProcessRun corrida, IReadOnlyList<LendingProcessRunError> errores) =>
        db.DescartarCambiosSalvo([corrida, .. errores.Where(e => e.Id != 0)]);

    private async Task UnaTandaAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> ids, int intento, List<AlertaPendiente> alertasPendientes,
        List<LendingProcessRunError> errores, CancellationToken ct)
    {
        var r = corrida.InformationCutoffAt!.Value;
        var ahora = reloj.UtcNow;
        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        var bloqueados = await cerrojo.BloquearCreditosAsync(ids, ModoDeEspera.Saltar, ct);
        corrida.SkippedLoans += ids.Count - bloqueados.Count;
        if (bloqueados.Count == 0) return;

        var controles = await db.LoanControls.Where(c => bloqueados.Contains(c.LoanId)).ToDictionaryAsync(c => c.LoanId, ct);
        var vivos = controles.Values.Where(c => c.LastAccruedDate < d).Select(c => c.LoanId).ToList();
        corrida.SkippedLoans += controles.Count - vivos.Count;
        var cargados = await cargador.CargarAsync(vivos, r, d, ct);
        var datos = await partes.DatosAsync(cargados.Values, ct);

        var causados = new List<(CreditoCargado Cargado, LoanControl Control, IReadOnlyList<DiaCausado> Dias)>();
        var sinUsura = new Dictionary<(string Modalidad, string? Clave), DateOnly>();
        foreach (var id in vivos.Order())
        {
            var control = controles[id];
            if (!cargados.TryGetValue(id, out var cargado) || !cargado.Listo)
            {
                var problema = cargado?.Error ?? new Error("Lending.Replay.InvalidOrigin", "El crédito no tiene condiciones selladas para reconstruirse.");
                if (problema.Code == ErroresDeValoresNacionales.MissingCode && cargado is not null)
                {
                    sinUsura.TryAdd((cargado.Pactadas.ModalityCode, cargado.Series?.ClaveDeUsura), control.LastAccruedDate.AddDays(1));
                    corrida.SkippedLoans += 1;
                    continue;
                }
                FallarCredito(corrida, id, intento, PasosDelCiclo.Causacion, problema.Code, problema.Message, d, alertasPendientes, errores);
                continue;
            }
            try
            {
                // (2) Reajuste de la tasa variable cuya fecha cae en lo que se causa.
                var entrada = cargado.Entrada!;
                var reajustado = await reajuste.SiCorrespondeAsync(cargado, control, d, ahora, ct);
                if (reajustado.IsFailure)
                {
                    FallarCredito(corrida, id, intento, PasosDelCiclo.Reajuste, reajustado.Error.Code, reajustado.Error.Message, d, alertasPendientes, errores);
                    continue;
                }
                if (reajustado.Value is { } rr) entrada = rr.Entrada;

                // (3) Causación día por día.
                var dias = PuestaAlDiaDeCredito.Causar(entrada, control.LastAccruedDate, d);
                if (dias.Count == 0 || dias[^1].Dia < d)
                {
                    var faltante = dias.Count == 0 ? control.LastAccruedDate.AddDays(1) : dias[^1].Dia.AddDays(1);
                    if (cargado.Series is { ExigeUsura: true } s && s.UsuraDe(faltante).IsFailure)
                        sinUsura.TryAdd((cargado.Pactadas.ModalityCode, s.ClaveDeUsura), faltante);
                    corrida.SkippedLoans += dias.Count == 0 ? 1 : 0;
                }
                if (dias.Count > 0) causados.Add((cargado, control, dias));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                FallarCredito(corrida, id, intento, PasosDelCiclo.Causacion, ex is Domain.Lending.ReglaDeCarteraException rc ? rc.Codigo : ex.GetType().Name,
                    ex.Message, d, alertasPendientes, errores);
            }
        }

        // Una modalidad sin usura del día: un error de grupo por modalidad y fecha, con su alerta.
        foreach (var ((modalidad, clave), fecha) in sinUsura)
        {
            var e = ErroresDeValoresNacionales.Faltante(CodigosDeValorNacional.Usura, clave ?? modalidad, fecha);
            var error = Error(corrida, null, intento, PasosDelCiclo.Causacion, e.Code, e.Message,
                JsonSerializer.Serialize(new { modality = modalidad, usuryKey = clave, date = fecha }, Json));
            db.LendingProcessRunErrors.Add(error);
            errores.Add(error);
            alertasPendientes.Add(new AlertaPendiente(new AlertaALevantar(TiposDeAlerta.ValorNacionalFaltante, $"Falta la usura de {modalidad}",
                $"{e.Message} Los créditos de {modalidad} no se causan desde ese día hasta que se replique.", ModuloDeOrigen: "Lending",
                DedupKey: AlertasDeValoresNacionales.ClaveDeFaltante(CodigosDeValorNacional.Usura, clave ?? modalidad, fecha)), error));
        }
        if (causados.Count == 0) return;

        // Las tandas de la corrida: una por fecha causada (la conducción suma la primera).
        var fechas = causados.SelectMany(c => c.Dias.Select(x => x.Dia)).Distinct().Order().ToList();
        var tandaPorDia = fechas.Select((f, i) => (f, i)).ToDictionary(x => x.f, x => corrida.BatchCount + 1 + x.i);
        corrida.BatchCount += Math.Max(0, fechas.Count - 1);
        var usuario = corrida.RequestedByKind == ActorKind.Person && corrida.RequestedByCentralUserId is { } central
            ? new UserRefV1 { CentralUserId = central, Name = corrida.RequestedByName }
            : null;
        var origen = new OrigenDePartes(AccountingFactPartSource.DailyCycleBatch, corrida, null, tandaPorDia, usuario);
        var lineas = causados.SelectMany(c => c.Dias.Select(x => new LineaDeCausacion(datos[c.Control.LoanId], x.Dia, x.Filas))).ToList();
        var masivas = await partes.MasivasAsync(origen, lineas, ct: ct);
        if (masivas.IsFailure) throw new InvalidOperationException($"{masivas.Error.Code}: {masivas.Error.Message}");

        var resumen = registro.CausarEnBloque(causados.SelectMany(c => c.Dias.Select(x => new CausacionAEscribir(c.Control, x.Dia,
            x.Filas.Where(f => f.Monto != 0m)
                .Select(f => RegistroDeCartera.FilaDeCausacion(c.Control.LoanId, f, corrida.Id, null, x.Dia, ahora, masivas.Value.Mensaje(c.Control.LoanId, x.Dia, f.Concepto)))
                .ToList()))).ToList());

        // Las posiciones de cada día causado, con la calificación informativa del día (US9, T0701: el mismo motor del cierre).
        var ultimas = await PosicionesDeCartera.UltimasAsync(db, causados.Select(c => c.Control.LoanId).ToList(), fechas[0], fechas[^1], ct);
        var calificaciones = calificacion is null
            ? new Dictionary<(int, DateOnly), Risk.CalificacionDiaria>()
            : await calificacion.CalificarAsync(causados.SelectMany(c => c.Dias.Select(x => new Risk.DiaParaCalificar(c.Cargado.Credito, c.Cargado.Vigentes, x.Dia, x.Estado)))
                .ToList(), ct);
        foreach (var c in causados)
            foreach (var x in c.Dias)
            {
                var u = ultimas.GetValueOrDefault((c.Control.LoanId, x.Dia));
                registro.PosicionVersionada(c.Control, x.Estado, c.Cargado.Credito, c.Cargado.Vigentes, u.Version, u.Huella, ahora,
                    calificacion: calificaciones.GetValueOrDefault((c.Control.LoanId, x.Dia)));
            }

        // (4) Suspensión y reanudación: un hecho por crédito y transición. US9 (T0702): con la categoría contable vigente de cada crédito.
        corrida.CurrentStep = PasosDelCiclo.Suspension;
        var categorias = await new CategoriaContableDeCartera(db).VigentesAsync(causados.Select(c => c.Control.LoanId).ToList(), d, ct);
        var hechos = new List<HechoPorCredito>();
        foreach (var c in causados)
            foreach (var x in c.Dias.Where(x => x.Transicion is not null))
            {
                var dd = datos[c.Control.LoanId];
                var (tipo, contenido) = PosicionesDeCartera.HechoDeTransicion(dd, x.Transicion!, x.Estado, ahora,
                    EmisionDeCartera.Dimensiones(dd.Hecho(x.Dia), categorias[c.Control.LoanId]));
                hechos.Add(new HechoPorCredito(tipo, dd, x.Dia, contenido));
            }

        // (5) Anticipos al vencer (US5, bloque 9B): un AnticipoAplicado por crédito y día que venció una cuota cubierta, y sus filas AdvanceApplied
        // con la corrida (mensajes.md §10.10).
        corrida.CurrentStep = PasosDelCiclo.Anticipos;
        var fuentes = await AnticiposAlVencer.FuentesAsync(db, causados.Select(c => c.Control.LoanId).ToList(), ct);
        var anticipos = new List<HechoPorCredito>();
        foreach (var c in causados)
            foreach (var x in c.Dias)
                if (AnticiposAlVencer.Hecho(datos[c.Control.LoanId], x, ahora, fuentes[c.Control.LoanId], categorias[c.Control.LoanId]) is { } anticipo)
                    anticipos.Add(anticipo);
        if (anticipos.Count > 0)
        {
            var emitidos = await partes.PorCreditoAsync(origen, anticipos, ct);
            if (emitidos.IsFailure) throw new InvalidOperationException($"{emitidos.Error.Code}: {emitidos.Error.Message}");
            foreach (var c in causados)
                foreach (var x in c.Dias)
                {
                    var filas = AnticiposAlVencer.FilasDelLibro(c.Control.LoanId, x, corrida.Id, null, ahora,
                        AnticiposAlVencer.Mensaje(emitidos.Value, c.Cargado.Credito.PublicId, x.Dia));
                    if (filas.Count > 0) registro.RegistrarTransacciones(filas);
                }
        }

        // (6) Bloqueos y dimensión contable cambiada. (7) Cobranza: K4.
        corrida.CurrentStep = PasosDelCiclo.Bloqueos;
        await BloqueosAsync(causados.Select(c => (c.Cargado, c.Dias[^1].Estado)).ToList(), d, ahora, ct);
        hechos.AddRange(await DimensionesCambiadasAsync(causados.Select(c => (datos[c.Control.LoanId], c.Dias[^1].Estado)).ToList(), d, ahora, ct));
        var porCredito = await partes.PorCreditoAsync(origen, hechos, ct);
        if (porCredito.IsFailure) throw new InvalidOperationException($"{porCredito.Error.Code}: {porCredito.Error.Message}");

        // La usura topada, una alerta por crédito y mes.
        foreach (var c in causados.Where(c => c.Dias.Any(x => x.Estado.UsuraTopada)))
        {
            var dd = datos[c.Control.LoanId];
            alertasPendientes.Add(new AlertaPendiente(new AlertaALevantar(TiposDeAlerta.UsuraTopada, $"Crédito {dd.Credito.Number} topado por usura",
                $"La causación del crédito {dd.Credito.Number} topó la tasa del día en la usura certificada de {dd.Condiciones.ModalityCode} "
                + $"({Origination.ErroresDeOriginacion.Fecha(d)}). Revise las condiciones de la línea {dd.CodigoDeLinea}.", EntityType: "Loan", EntityPublicId: dd.Credito.PublicId,
                DedupKey: $"{TiposDeAlerta.UsuraTopada}:{dd.Credito.PublicId:D}:{d:yyyy-MM}", ModuloDeOrigen: "Lending"), null));
        }

        // Los errores resueltos: un crédito que antes falló y ahora causó.
        var resueltos = causados.Select(c => c.Control.LoanId).ToList();
        foreach (var previo in await db.LendingProcessRunErrors.Where(e => e.LoanId != null && resueltos.Contains(e.LoanId.Value) && e.ResolvedAt == null).ToListAsync(ct))
            previo.ResolvedAt = ahora;

        corrida.ProcessedLoans += causados.Count;
        Acumular(corrida, resumen);
    }

    private void FallarCredito(LendingProcessRun corrida, int loanId, int intento, byte paso, string codigo, string mensaje, DateOnly d,
        List<AlertaPendiente> alertasPendientes, List<LendingProcessRunError> errores)
    {
        var error = Error(corrida, loanId, intento, paso, codigo, mensaje, null);
        db.LendingProcessRunErrors.Add(error);
        errores.Add(error);
        corrida.FailedLoans += 1;
        alertasPendientes.Add(new AlertaPendiente(AlertaDeCredito(loanId, d, mensaje), error));
    }

    private static LendingProcessRunError Error(LendingProcessRun corrida, int? loanId, int intento, byte paso, string codigo, string mensaje, string? detalle)
    {
        var texto = string.IsNullOrWhiteSpace(mensaje) ? codigo : mensaje;
        return new LendingProcessRunError
        {
            ProcessRunId = corrida.Id,
            LoanId = loanId,
            BatchNumber = corrida.BatchCount + 1,
            AttemptNumber = intento + corrida.RetryCount * 2,
            Step = PasosDelCiclo.Nombre(paso),
            ErrorCode = codigo.Length > 80 ? codigo[..80] : codigo,
            ErrorMessage = texto.Length > LargoDelMensaje ? texto[..LargoDelMensaje] : texto,
            DetailJson = detalle,
            OccurredAt = corrida.LastHeartbeatAt ?? corrida.InformationCutoffAt ?? corrida.RequestedAt,
        };
    }

    private static AlertaALevantar AlertaDeCredito(int loanId, DateOnly d, string mensaje) =>
        new(TiposDeAlerta.CreditoSinCausar, "Crédito sin causar",
            $"Un crédito quedó con error en el ciclo diario del {Origination.ErroresDeOriginacion.Fecha(d)} y su causación no avanzó: {mensaje} "
            + "El ciclo siguiente lo vuelve a intentar desde su marca.",
            EntityType: "Loan", DedupKey: $"{TiposDeAlerta.CreditoSinCausar}:{loanId.ToString(CultureInfo.InvariantCulture)}", ModuloDeOrigen: "Lending");

    // --------------------------------------------------------------------------------------------- (6) bloqueos --

    /// <summary>
    /// Los bloqueos automáticos del paso (6): por mora (<c>Bloqueos.DiasDeMoraParaBloquear</c>, uno por crédito, que se levanta cuando el crédito
    /// se pone al día) y por retiro del asociado (FR-060: el maestro lo muestra retirado; se levanta cuando lo vuelve a mostrar activo). Nunca
    /// duplica uno vigente.
    /// </summary>
    private async Task BloqueosAsync(IReadOnlyList<(CreditoCargado Cargado, Domain.Lending.Replay.EstadoDelCredito Estado)> creditos, DateOnly d, DateTime ahora,
        CancellationToken ct)
    {
        var umbral = await parametros.LeerAsync(ParametrosDeCartera.BloqueosDiasDeMoraParaBloquear, d, ct: ct);
        var dias = umbral.IsSuccess && umbral.Value.Valor is int u && u > 0 ? u : (int?)null;
        var personas = creditos.Select(c => c.Cargado.Credito.DebtorPersonId).Distinct().ToList();
        var vigentes = await db.DebtorBlocks.Where(b => personas.Contains(b.PersonId) && b.LiftedAt == null && !b.IsDeleted && b.IsAutomatic
            && (b.Reason == DebtorBlockReason.Arrears || b.Reason == DebtorBlockReason.AssociateWithdrawal)).ToListAsync(ct);

        if (dias is { } limite)
            foreach (var (cargado, estado) in creditos)
            {
                var clave = ClaveDeMora(cargado.Credito.PublicId);
                var vigente = vigentes.FirstOrDefault(b => b.Reason == DebtorBlockReason.Arrears && b.SourceKey == clave);
                if (estado.AlturaDeMora.Dias >= limite && vigente is null)
                    db.DebtorBlocks.Add(DebtorBlock.Nuevo(cargado.Credito.DebtorPersonId, DebtorBlockReason.Arrears, clave, d, null, null, cargado.Credito.Id));
                else if (estado.AlturaDeMora.Dias < limite && vigente is not null)
                    vigente.Levantar(d, ahora, null, "el crédito se puso al día");
            }

        var asociados = creditos.Where(c => c.Cargado.Credito.DebtorKind == DebtorKind.Associate).Select(c => c.Cargado.Credito.DebtorPersonId).Distinct().ToList();
        if (asociados.Count == 0) return;
        var maestro = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => asociados.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, p.IsAssociate }).ToListAsync(ct);
        var afiliaciones = await db.Associates.AsNoTracking().Where(a => asociados.Contains(a.PersonId) && !a.IsDeleted)
            .Select(a => new { a.PersonId, a.WithdrawalDate, a.RejoinDate }).ToListAsync(ct);
        foreach (var p in maestro)
        {
            var afiliacion = afiliaciones.FirstOrDefault(a => a.PersonId == p.Id);
            var retirado = !p.IsAssociate || (afiliacion?.WithdrawalDate is { } retiro && retiro <= d
                                              && (afiliacion.RejoinDate is not { } reingreso || reingreso < retiro || reingreso > d));
            var clave = ClaveDeRetiro(p.PublicId);
            var vigente = vigentes.FirstOrDefault(b => b.Reason == DebtorBlockReason.AssociateWithdrawal && b.PersonId == p.Id);
            if (retirado && vigente is null)
                db.DebtorBlocks.Add(DebtorBlock.Nuevo(p.Id, DebtorBlockReason.AssociateWithdrawal, clave, d, null, null));
            else if (!retirado && vigente is not null)
                vigente.Levantar(d, ahora, null, MotivoDeReingreso);
        }
    }

    /// <summary>El motivo del levantamiento automático del bloqueo por retiro.</summary>
    public const string MotivoDeReingreso = "reingreso del asociado";

    public static string ClaveDeMora(Guid loanPublicId) => $"Loan:{loanPublicId:N}";

    public static string ClaveDeRetiro(Guid personPublicId) => $"Person:{personPublicId:N}";

    // --------------------------------------------------------------------------------- (6) dimensión cambiada --

    /// <summary>
    /// La dimensión contable cambiada que detecta el ciclo (C8): si el deudor dejó de ser empleado o pasó a serlo frente al último hecho del
    /// crédito que llevó sus dimensiones (<c>CreditoDesembolsado</c> o el último <c>DimensionContableCambiada</c>), un hecho por crédito que
    /// mueve sus saldos vivos de la combinación anterior a la nueva. Un crédito sin hecho previo (migrado) no se compara.
    /// </summary>
    private async Task<IReadOnlyList<HechoPorCredito>> DimensionesCambiadasAsync(IReadOnlyList<(DatosDeHecho Datos, Domain.Lending.Replay.EstadoDelCredito Estado)> creditos,
        DateOnly d, DateTime ahora, CancellationToken ct)
    {
        var deudores = creditos.Select(c => (Guid?)c.Datos.DeudorPublicId).Distinct().ToList();
        var tipos = new[] { CreditoDesembolsadoV1.Type, DimensionContableCambiadaV1.Type };
        var mensajes = await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginModule == ModulosDeOrigen.Cartera && tipos.Contains(m.Type) && deudores.Contains(m.PersonPublicId))
            .OrderBy(m => m.Id)
            .Select(m => new { m.Id, m.PayloadJson })
            .ToListAsync(ct);
        var ultimas = new Dictionary<Guid, LoanFactDimensionsV1>();
        foreach (var m in mensajes)
        {
            var leido = JsonSerializer.Deserialize<ConDimensiones>(m.PayloadJson ?? "{}", Json);
            if (leido?.Loan is { } credito && leido.Dimensions is { } dims) ultimas[credito.LoanPublicId] = dims;
        }

        var hechos = new List<HechoPorCredito>();
        foreach (var (datos, estado) in creditos)
        {
            if (!ultimas.TryGetValue(datos.Credito.PublicId, out var anteriores) || anteriores.IsEmployee == datos.DeudorEsEmpleado) continue;
            var nuevas = anteriores with { IsEmployee = datos.DeudorEsEmpleado };
            var valores = estado.SaldosPorConcepto.Where(s => s.Value > 0m && s.Key is not (LoanConcept.Advance or LoanConcept.CreditBalance))
                .Select(s => new ConceptAmountV1 { Concept = s.Key.ToString(), Amount = s.Value }).ToList();
            hechos.Add(new HechoPorCredito(DimensionContableCambiadaV1.Type, datos, d, new DimensionContableCambiadaV1
            {
                Loan = datos.Referencia,
                DebtorPersonPublicId = datos.DeudorPublicId,
                ValueDate = d,
                RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(ahora, DateTimeKind.Utc)),
                Dimensions = nuevas,
                Amounts = valores,
                RulesVersion = datos.Condiciones.RulesVersion,
                PreviousDimensions = anteriores,
                ChangedDimensions = ["isEmployee"],
                EffectiveDate = d,
            }));
        }
        return hechos;
    }

    private sealed record ConDimensiones(LoanRefV1? Loan, LoanFactDimensionsV1? Dimensions);

    // ------------------------------------------------------------------------------------------ fin de la corrida --

    /// <summary>
    /// Al terminar: (8) la muestra de verificación del libro, el lote de la causación del día (<c>BatchTrigger.LendingAccrualRun</c> con las
    /// entregas por lotes de <c>AccrualRun:{d}</c>) y el evento resumen (<c>TotalsJson</c>, <c>ResultSha256</c>).
    /// </summary>
    private async Task TerminarAsync(LendingProcessRun corrida, DateOnly d, CancellationToken ct)
    {
        corrida.CurrentStep = PasosDelCiclo.Verificacion;
        var muestra = await verificacion.VerificarAsync(null, MuestraDeVerificacion, d, ct);
        if (muestra.Diferencias.Count > 0)
            _logger.LogWarning("[Cartera.LibroDescuadrado] La muestra del ciclo {Fecha} encontró {N} diferencias del libro contra la reconstrucción.",
                d, muestra.Diferencias.Count);

        // T0659 (A3): lo que vence con el tiempo —aprobaciones y cupos rotativos— mueve sus contingentes; los emite el proceso.
        if (contingentes is not null && await contingentes.VencimientosAsync(d, ct) is { IsFailure: true } contingente)
            _logger.LogWarning("[Cartera.Contingentes] El ciclo {Fecha} no pudo poner al día los contingentes: {Codigo} {Mensaje}", d, contingente.Error.Code,
                contingente.Error.Message);

        // T0654, C09: toma las entregas InBatch sin lote de AccrualRun:{x} con x ≤ d (las de la causación de d y los hechos FinDeCausacion
        // nacidos fuera de una corrida anterior, sellados con su día siguiente), en el mismo guardado con que termina la corrida. Lo pendiente
        // (los contadores de la corrida, los contingentes) se guarda antes: el punto de guardado del lote sólo puede tocar el lote, el contador y las
        // entregas, porque al volver a él EF ya dio por guardado lo que se escribió adentro. Los contingentes pasan antes por la validación previa
        // de los procesos (A8 = «Bloquear todo»): si no se pueden contabilizar, la corrida se detiene sin terminar.
        if (validacionDeProceso is not null && await validacionDeProceso.BloqueoAsync(ct) is { } bloqueo)
        {
            Soltar(corrida, []);
            throw await validacionDeProceso.DetenerAsync(bloqueo, string.Create(CultureInfo.InvariantCulture, $"el cierre del ciclo del {d:yyyy-MM-dd}"), ct);
        }
        await db.SaveChangesAsync(ct);
        var alcance = EmisionDeCartera.AlcanceDeLoteDeCausacion(d);
        var entregas = await db.IntegrationMessageDeliveries
            .Where(x => x.Destination == IntegrationDestinations.Accounting && x.Status == DeliveryStatus.InBatch && x.BatchId == null
                && x.BatchScopeKey != null && x.BatchScopeKey.StartsWith(ModoDePasoDeCartera.PrefijoDeCausacion)
                && string.Compare(x.BatchScopeKey, alcance) <= 0)
            .OrderBy(x => x.MessageId).ToListAsync(ct);
        if (entregas.Count > 0)
        {
            var actor = IngenIA365ERP.Application.Common.Execution.Actor.ProcesoDeIntegracion(IngenIA365ERP.Application.Common.Execution.Actor.OrigenDeTarea(TareaDelCiclo));
            var desde = await db.IntegrationMessages.AsNoTracking().Where(m => m.Id == entregas[0].MessageId).Select(m => (DateOnly?)m.OperationDate)
                .FirstOrDefaultAsync(ct) ?? d;
            IntegrationBatch? lote = null;
            // Dos corridas que terminan a la vez (la de un día atrasado y la de hoy) ven las mismas entregas sin lote: la segunda en guardar pierde por
            // la RowVersion del contador o de la entrega. El lote y su asignación van juntos bajo un punto de guardado: la que pierde vuelve al punto,
            // suelta lo suyo y sigue (las entregas quedaron en el lote de la otra, o las toma el ciclo siguiente, que recoge todo AccrualRun ≤ d).
            // Hasta el 2026-10-14 la asignación se guardaba en el guardado de quien conduce y la corrida terminaba con ConcurrencyConflictException.
            var tomado = await TransaccionExplicita.EjecutarAsync(db, () => PuntoDeGuardado.EjecutarAsync(db, "lote_de_causacion", async () =>
            {
                lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), IntegrationDestinations.Accounting,
                    BatchTrigger.LendingAccrualRun, actor, null, reloj.UtcNow);
                lote.MessageCount = entregas.Count;
                lote.CutoffMessageId = entregas[^1].MessageId;
                lote.DateFrom = desde;
                lote.DateTo = d;
                db.IntegrationBatches.Add(lote);
                await db.SaveChangesAsync(ct);
                LotesDeIntegracion.Asignar(lote, entregas);
                await db.SaveChangesAsync(ct);
                return true;
            }, e => e is Domain.Exceptions.ConcurrencyConflictException or DbUpdateConcurrencyException ? false : throw new InvalidOperationException(
                "No se pudo armar el lote de la causación del día.", e), ct), ct);
            if (!tomado)
            {
                _logger.LogInformation("[Cartera.LoteDeCausacionTomado] Otra corrida tomó a la vez las entregas de la causación hasta {Fecha}: van en su lote.", d);
                foreach (var entrega in entregas) db.IntegrationMessageDeliveries.Entry(entrega).State = EntityState.Detached;
                foreach (var contador in db.IntegrationBatchCounters.Local.ToList()) db.IntegrationBatchCounters.Entry(contador).State = EntityState.Detached;
                if (lote is not null) db.IntegrationBatches.Entry(lote).State = EntityState.Detached;
            }
        }

        var totales = Totales(corrida);
        corrida.ResultSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(totales)));
    }

    /// <summary>El nombre de la tarea del ciclo en el origen de las corridas programadas: <c>Tarea:cartera.diario:{fecha}</c>.</summary>
    public const string TareaDelCiclo = "cartera.diario";

    /// <summary>El origen de ejecución de una corrida programada del día.</summary>
    public static string OrigenDelDia(DateOnly d) => $"Tarea:{TareaDelCiclo}:{d:yyyy-MM-dd}";

    private sealed record TotalesDeCorrida(int Loans, int Days, int Rows, Dictionary<string, decimal> ByConcept);

    private static void Acumular(LendingProcessRun corrida, ResumenDeCausacion resumen)
    {
        var actual = string.IsNullOrWhiteSpace(corrida.TotalsJson)
            ? new TotalesDeCorrida(0, 0, 0, [])
            : JsonSerializer.Deserialize<TotalesDeCorrida>(corrida.TotalsJson, Json) ?? new TotalesDeCorrida(0, 0, 0, []);
        foreach (var (concepto, monto) in resumen.PorConcepto)
            actual.ByConcept[concepto.ToString()] = actual.ByConcept.GetValueOrDefault(concepto.ToString()) + monto;
        corrida.TotalsJson = JsonSerializer.Serialize(actual with
        {
            Loans = actual.Loans + resumen.Creditos,
            Days = actual.Days + resumen.Dias,
            Rows = actual.Rows + resumen.Filas,
        }, Json);
    }

    private static string Totales(LendingProcessRun corrida) => corrida.TotalsJson ?? "{}";
}
