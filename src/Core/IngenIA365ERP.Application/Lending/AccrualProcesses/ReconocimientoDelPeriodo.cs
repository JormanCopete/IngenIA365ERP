using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.AccrualProcesses;

/// <summary>
/// La clase <c>PeriodRecognition</c> del proceso de causación (2026-11-16, pedido del dueño; feature 024, fase 4): reconoce la cuota del período al inicio
/// de su ciclo para los rubros en «Manual» (interés corriente, seguro, administración; la mora nunca) en los créditos de las líneas con
/// <c>Causacion.Momento = InicioDelCiclo</c>. Los rubros en «Automatica» los reconoce solo el ciclo diario el primer día del período.
/// <list type="bullet">
/// <item><b>Qué se reconoce</b>: de cada crédito vivo del alcance y de los filtros (exactos: el proceso emite sus propios hechos), las cuotas cuyo período
/// (desde el día siguiente a su <c>Desde</c> hasta su vencimiento) se cruza con el rango; lo que la cuota va a causar desde el día del reconocimiento —el
/// primer día de su período o, si ya empezó, el siguiente al último causado— hasta su vencimiento (<see cref="ReconstruccionDeCredito.CuotaPorReconocer"/>),
/// con el modo de <c>Causacion.InicioDelCiclo.Reconocimiento</c> sellado en el concepto.</item>
/// <item><b>Fuera, con su motivo</b>: castigado, en UVR, con la causación suspendida, con abonos posteriores (reproceso), con el ciclo sin empezar
/// (empieza después de hoy), con el rubro ya reconocido, y los hechos con el mes contable cerrado o inexistente, la contabilidad sin iniciar o el mes
/// ya cerrado por Cartera. Contabilidad sin respuesta bloquea.</item>
/// <item><b>El hecho</b> es lo reconocido de un día y una sucursal (un <c>CuotaReconocida</c>); no hay mensaje en el borrador.</item>
/// <item><b>En firme</b>: con los créditos bloqueados y su causación todavía antes del día del reconocimiento, una corrida <c>PeriodRecognition</c> (ya
/// terminada) escribe las filas <c>PeriodRecognition</c> del libro y las partes de sus hechos, que esperan con el alcance de la causación manual; la
/// validación previa contable de la tanda (<c>Contabilidad.FaltaDeRegla = Bloquear</c>) puede impedirlo; y se ordena el lote con exactamente esos hechos.
/// La marca de causación no se mueve: la causación de esos días lo consume.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class ReconocimientoDelPeriodo(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    LectorDePoliticaDeReconocimiento politica,
    IContabilidadParaCartera contabilidad,
    PartesDeHechosDeCartera partes,
    RegistroDeCartera registro,
    ICerrojoDeCartera cerrojo,
    IActorActual actorActual,
    IDateTimeService reloj,
    ValidacionPreviaDeProceso? validacion = null) : IClaseDeProcesoDeCausacion
{
    /// <summary>Créditos que se reconstruyen a la vez.</summary>
    private const int Trozo = 200;

    public AccrualProcessKind Clase => AccrualProcessKind.PeriodRecognition;

    public async Task<Result<CalculoDelProceso>> CalcularAsync(ContextoDelCalculo contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var p = contexto.Parametros;
        var hoy = contexto.Hoy;
        var rubros = ConceptosDeCausacion.De(p.Conceptos).Select(c => ConceptosDeCausacion.RubroReconocible(c.Concepto)).OfType<LoanConcept>().ToList();
        var avisos = new List<AvisoDelCalculo>();

        // 1. Los créditos vivos del alcance y de los filtros, de las líneas que reconocen al inicio del ciclo.
        var consulta = db.Loans.AsNoTracking().Where(l => !l.IsDeleted && (l.Status == LoanStatus.Active || l.Status == LoanStatus.WrittenOff));
        if (p.OficinaId is { } oficina) consulta = consulta.Where(l => l.BranchId == oficina);
        if (p.LineaId is { } linea) consulta = consulta.Where(l => l.CreditLineId == linea);
        if (p.PagaduriaId is { } pagaduria)
            consulta = consulta.Where(l => db.LoanPayrollLinks.Any(v => v.LoanId == l.Id && !v.IsDeleted && v.ValidTo == null && v.PaymasterPersonId == pagaduria));
        var candidatos = (await consulta.Select(l => new Candidato(l.Id, l.BranchId, l.CreditLineId, l.Status)).ToListAsync(ct))
            .Where(c => contexto.Alcance.IncluyeOficina(c.Oficina) && contexto.Alcance.IncluyeLinea(c.Linea))
            .ToList();
        var leida = await politica.LeerAsync(hoy, candidatos.Select(c => c.Linea), ct);
        candidatos = candidatos.Where(c => leida.AlInicioDelCiclo(c.Linea)).OrderBy(c => c.Id).ToList();
        if (candidatos.Count == 0)
        {
            avisos.Add(new AvisoDelCalculo("Lending.AccrualProcess.NothingToRecognize",
                "Ninguna línea de los créditos de su alcance y de los filtros reconoce la cuota al inicio del ciclo (Causacion.Momento = InicioDelCiclo).", false));
            return Result.Success(new CalculoDelProceso([], [], avisos));
        }

        // 2. Lo que se reconoce de cada crédito, cuota y rubro.
        var lineas = new List<LineaDelCalculo>();
        foreach (var trozo in candidatos.Chunk(Trozo))
        {
            var ids = trozo.Select(c => c.Id).ToList();
            var marcas = await db.LoanControls.AsNoTracking().Where(c => ids.Contains(c.LoanId)).ToDictionaryAsync(c => c.LoanId, c => c.LastAccruedDate, ct);
            var cargados = await cargador.CargarAsync(ids, null, hoy, ct);
            foreach (var c in trozo)
            {
                if (!cargados.TryGetValue(c.Id, out var cargado) || !cargado.Listo || !marcas.TryGetValue(c.Id, out var marca)) continue;
                lineas.AddRange(DelCredito(contexto, c, cargado.Entrada!, marca, rubros, leida.Modo, hoy));
            }
        }
        if (lineas.Count == 0)
        {
            avisos.Add(new AvisoDelCalculo("Lending.AccrualProcess.NothingToRecognize",
                "No hay cuotas cuyo período se cruce con el rango y que falten por causar en los créditos de su alcance.", false));
            return Result.Success(new CalculoDelProceso([], [], avisos));
        }

        // 3. Los hechos: lo reconocido de un día y una sucursal, con el motivo contable o de Cartera que lo deja fuera.
        var hechos = new List<HechoDelCalculo>();
        var estados = new Dictionary<DateOnly, Result<EstadoDelPeriodoDto>>();
        var cierres = new Dictionary<(int, int), bool>();
        var finales = new List<LineaDelCalculo>(lineas.Where(l => l.Estado == AccrualProcessItemStatus.Excluded));
        foreach (var g in lineas.Where(l => l.Estado == AccrualProcessItemStatus.Included).GroupBy(l => (Dia: l.FechaDeReconocimiento!.Value, l.OficinaId))
                     .OrderBy(g => g.Key.Dia).ThenBy(g => g.Key.OficinaId))
        {
            var dia = g.Key.Dia;
            if (!estados.TryGetValue(dia, out var estado)) estados[dia] = estado = await contabilidad.EstadoDelPeriodoAsync(dia, ct);
            (AccrualExclusionReason Motivo, bool Bloquea, string Detalle)? fuera = null;
            if (estado.IsFailure)
                fuera = (AccrualExclusionReason.AccountingNoResponse, true, $"Contabilidad no respondió por el mes {Mes(dia)}: {estado.Error.Message}");
            else if (!estado.Value.AccountingInitialized)
                fuera = (AccrualExclusionReason.AccountingNotStarted, false, "La contabilidad no está iniciada.");
            else if (estado.Value.State != EstadoDePeriodoContable.Open)
                fuera = estado.Value.State == EstadoDePeriodoContable.Closed
                    ? (AccrualExclusionReason.AccountingPeriodClosed, false, $"El mes contable {Mes(dia)} está cerrado.")
                    : (AccrualExclusionReason.AccountingPeriodMissing, false, $"El mes contable {Mes(dia)} no existe en Contabilidad.");
            else
            {
                if (!cierres.TryGetValue((dia.Year, dia.Month), out var cerrado))
                    cierres[(dia.Year, dia.Month)] = cerrado = await RequisitosDelCierreMensual.MesConCierreVigenteAsync(db, dia.Year, dia.Month, ct);
                if (cerrado) fuera = (AccrualExclusionReason.LendingMonthClosed, false, $"Cartera ya cerró {Mes(dia)}.");
            }
            hechos.Add(new HechoDelCalculo(null, CuotaReconocidaV1.Type, dia, dia, g.Key.OficinaId, g.Select(l => l.CreditoId).Distinct().Count(),
                g.Sum(l => l.Monto), fuera is null ? AccrualProcessItemStatus.Included : AccrualProcessItemStatus.Excluded, fuera?.Motivo, fuera?.Bloquea ?? false,
                fuera?.Detalle));
            finales.AddRange(fuera is { } f
                ? g.Select(l => l with { Estado = AccrualProcessItemStatus.Excluded, Motivo = f.Motivo })
                : g);
        }
        if (hechos.Count == 0)
            avisos.Add(new AvisoDelCalculo("Lending.AccrualProcess.NothingToRecognize",
                "Todo lo del rango ya está reconocido o quedó fuera con su motivo.", false));
        return Result.Success(new CalculoDelProceso(hechos,
            finales.OrderBy(l => l.CreditoId).ThenBy(l => l.Cuota).ThenBy(l => l.Concepto).ThenBy(l => l.Estado).ToList(), avisos));
    }

    private sealed record Candidato(int Id, int Oficina, int Linea, LoanStatus Estado);

    /// <summary>Las líneas de un crédito: por cada cuota cuyo período se cruza con el rango y falta por causar, cada rubro pedido, incluido o con su motivo.</summary>
    private static IEnumerable<LineaDelCalculo> DelCredito(ContextoDelCalculo contexto, Candidato c, EntradaDeReconstruccion entrada, DateOnly marca,
        IReadOnlyList<LoanConcept> rubros, ModoDeReconocimiento modo, DateOnly hoy)
    {
        var p = contexto.Parametros;
        var estado = MotorDeCartera.Reconstruir(entrada, marca);
        if (estado.Plan is null) yield break;
        AccrualExclusionReason? delCredito = c.Estado == LoanStatus.WrittenOff ? AccrualExclusionReason.WrittenOff
            : entrada.Uvr is not null ? AccrualExclusionReason.UvrLoan
            : contexto.CreditosConAbonos.Contains(c.Id) ? AccrualExclusionReason.LaterPayment
            : estado.Suspendida ? AccrualExclusionReason.AccrualSuspended
            : null;
        var siguiente = marca.AddDays(1);
        foreach (var q in estado.Plan.Cuotas.Where(q => q.Desde.AddDays(1) <= p.Hasta && q.Vencimiento >= p.Desde))
        {
            var inicio = q.Desde.AddDays(1);
            var desde = inicio > siguiente ? inicio : siguiente;
            if (desde > q.Vencimiento) continue;
            var ya = entrada.Reconocimientos.Where(r => r.Cuota == q.Numero).ToList();
            foreach (var r in ya.Where(r => rubros.Contains(ConceptosReconocidos.RubroDe(r.Concepto))))
                yield return Linea(contexto, c, r.Concepto, r.Monto, AccrualProcessItemStatus.Excluded, AccrualExclusionReason.AlreadyRecognized, q.Numero, r.Fecha);
            var pendientes = rubros.Where(r => ya.All(x => ConceptosReconocidos.RubroDe(x.Concepto) != r)).ToList();
            if (pendientes.Count == 0) continue;
            var motivo = delCredito ?? (inicio > hoy ? AccrualExclusionReason.CycleNotStarted : null);
            if (motivo is { } fuera)
            {
                foreach (var r in pendientes)
                    yield return Linea(contexto, c, ConceptosReconocidos.De(r, modo), 0m, AccrualProcessItemStatus.Excluded, fuera, q.Numero, desde);
                continue;
            }
            foreach (var (rubro, monto) in ReconstruccionDeCredito.CuotaPorReconocer(entrada, q.Numero, desde, pendientes))
                yield return Linea(contexto, c, ConceptosReconocidos.De(rubro, modo), monto, AccrualProcessItemStatus.Included, null, q.Numero, desde);
        }
    }

    private static LineaDelCalculo Linea(ContextoDelCalculo contexto, Candidato c, LoanConcept concepto, decimal monto, AccrualProcessItemStatus estado,
        AccrualExclusionReason? motivo, int cuota, DateOnly desde) =>
        new(c.Id, CuotaReconocidaV1.Type, concepto, monto,
            contexto.Reprocesado is null ? null : contexto.Anteriores.TryGetValue((c.Id, CuotaReconocidaV1.Type, concepto), out var antes) ? antes : 0m,
            estado, motivo, c.Oficina, c.Linea)
        {
            Cuota = cuota, FechaDeReconocimiento = desde,
        };

    public async Task<Result<LoteDelProceso>> PonerEnFirmeAsync(AccrualProcess proceso, IReadOnlyList<AccrualProcessFact> incluidos, string motivo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proceso);
        ArgumentNullException.ThrowIfNull(incluidos);
        var lineas = await db.AccrualProcessLines.AsNoTracking()
            .Where(l => l.AccrualProcessId == proceso.Id && !l.IsDeleted && l.Status == AccrualProcessItemStatus.Included && l.InstallmentNumber != null
                        && l.RecognitionDate != null && l.Concept != null && l.Amount > 0m)
            .ToListAsync(ct);
        if (lineas.Count == 0 || incluidos.Count == 0) return Result.Failure<LoteDelProceso>(ErroresDeProcesosDeCausacion.NadaQueContabilizar());

        // 1. Los créditos bloqueados, su causación todavía antes del día del reconocimiento y la cuota sin reconocer: si no, el borrador ya no vale.
        var ids = lineas.Select(l => l.LoanId).Distinct().Order().ToList();
        await cerrojo.BloquearCreditosAsync(ids, ModoDeEspera.Esperar, ct);
        var marcas = new Dictionary<int, DateOnly>();
        var yaReconocidas = new HashSet<(int, int, LoanConcept)>();
        foreach (var trozo in ids.Chunk(500))
        {
            foreach (var c in await db.LoanControls.AsNoTracking().Where(c => trozo.Contains(c.LoanId)).Select(c => new { c.LoanId, c.LastAccruedDate }).ToListAsync(ct))
                marcas[c.LoanId] = c.LastAccruedDate;
            foreach (var t in await db.LoanTransactions.AsNoTracking()
                         .Where(t => trozo.Contains(t.LoanId) && t.Kind == LoanTransactionKind.PeriodRecognition && t.ReversesTransactionId == null)
                         .Select(t => new { t.LoanId, t.InstallmentNumber, t.Concept }).ToListAsync(ct))
                yaReconocidas.Add((t.LoanId, t.InstallmentNumber ?? 0, ConceptosReconocidos.RubroDe(t.Concept)));
        }
        if (lineas.Any(l => !marcas.TryGetValue(l.LoanId, out var marca) || marca >= l.RecognitionDate!.Value
                            || yaReconocidas.Contains((l.LoanId, l.InstallmentNumber!.Value, ConceptosReconocidos.RubroDe(l.Concept!.Value)))))
            return Result.Failure<LoteDelProceso>(ErroresDeProcesosDeCausacion.Desactualizado());

        // 2. La corrida que escribe, ya terminada.
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var dias = lineas.Select(l => l.RecognitionDate!.Value).Distinct().Order().ToList();
        var corrida = new LendingProcessRun
        {
            Kind = LendingProcessKind.PeriodRecognition, ProcessDate = reloj.HoyLocal, Status = LendingProcessRunStatus.Completed,
            RequestedByKind = actor.Kind, RequestedByUserId = actor.UserId, RequestedByCentralUserId = actor.CentralUserId, RequestedByName = actor.Name,
            ExecutionOrigin = $"Proceso de causación {proceso.Number}", Reason = Recortar(motivo), InformationCutoffAt = ahora, RequestedAt = ahora,
            StartedAt = ahora, FinishedAt = ahora, AffectedFrom = dias[0], AffectedTo = dias[^1], TotalLoans = ids.Count, ProcessedLoans = ids.Count,
            BatchCount = dias.Count,
        };
        db.LendingProcessRuns.Add(corrida);
        await db.SaveChangesAsync(ct);

        // 3. Las partes de los hechos (esperan con el alcance de la causación manual) y las filas del libro.
        var cargados = await cargador.CargarAsync(ids, null, dias[^1], ct);
        var datos = await partes.DatosAsync(cargados.Values, ct);
        var tandaPorDia = dias.Select((d, i) => (d, i)).ToDictionary(x => x.d, x => x.i + 1);
        var usuario = actor.CentralUserId is { } central ? new UserRefV1 { CentralUserId = central, Name = actor.Name } : null;
        var origen = new OrigenDePartes(AccountingFactPartSource.PeriodRecognition, corrida, null, tandaPorDia, usuario) { CausacionManual = true };
        var porCreditoYDia = lineas.Where(l => datos.ContainsKey(l.LoanId))
            .GroupBy(l => (l.LoanId, Dia: l.RecognitionDate!.Value))
            .OrderBy(g => g.Key.LoanId).ThenBy(g => g.Key.Dia)
            .Select(g => new LineaDeCausacion(datos[g.Key.LoanId], g.Key.Dia, g
                .OrderBy(l => l.InstallmentNumber).ThenBy(l => l.Concept)
                .Select(l => new MovimientoDeLibro(g.Key.Dia, LoanTransactionKind.PeriodRecognition, l.Concept!.Value, l.InstallmentNumber, l.Amount))
                .ToList()))
            .ToList();
        var emitidas = await partes.MasivasAsync(origen, porCreditoYDia, ct: ct);
        if (emitidas.IsFailure) return Result.Failure<LoteDelProceso>(emitidas.Error);
        if (validacion is not null && await validacion.BloqueoAsync(ct) is { } bloqueo)
            return Result.Failure<LoteDelProceso>(ErroresDeProcesosDeCausacion.Bloqueado(
                [bloqueo.Bloqueo?.Message ?? "Contabilidad no puede registrar lo reconocido: complete la matriz de Cartera y recalcule."]));
        var contable = emitidas.Value.Partes.Where(x => x.MessagePublicId is not null).ToDictionary(x => x.MessagePublicId!.Value, x => x.AccountingDate);
        registro.RegistrarTransacciones(porCreditoYDia.SelectMany(l => l.Filas.Select(f =>
        {
            var mensaje = emitidas.Value.Mensaje(l.Datos.Credito.Id, l.Dia, f.Concepto, f.Clase);
            return RegistroDeCartera.FilaDeCausacion(l.Datos.Credito.Id, f, corrida.Id, null,
                mensaje is { } m && contable.TryGetValue(m, out var fecha) ? fecha : l.Dia, ahora, mensaje);
        })));
        corrida.PartCount = emitidas.Value.Partes.Count;
        await db.SaveChangesAsync(ct);

        // 4. El lote con exactamente esos hechos (los que pasan a Contabilidad).
        var mensajes = emitidas.Value.Mensajes.Select(m => m.Id).ToList();
        var entregas = await db.IntegrationMessageDeliveries.Include(d => d.Message)
            .Where(d => d.Destination == IntegrationDestinations.Accounting && mensajes.Contains(d.MessageId) && d.Status == DeliveryStatus.InBatch
                        && d.BatchId == null)
            .ToListAsync(ct);
        if (entregas.Count == 0) return Result.Success(new LoteDelProceso(null, null, 0));
        var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), IntegrationDestinations.Accounting, BatchTrigger.Manual,
            actor, $"Proceso de causación {proceso.Number}: {motivo}", ahora);
        lote.CutoffMessageId = entregas.Max(d => d.MessageId);
        lote.DateFrom = entregas.Min(d => d.Message!.OperationDate);
        lote.DateTo = entregas.Max(d => d.Message!.OperationDate);
        lote.MessageCount = entregas.Count;
        lote.DocumentCount = entregas.Select(d => d.Message!.OriginPublicId).Distinct().Count();
        db.IntegrationBatches.Add(lote);
        await db.SaveChangesAsync(ct);
        LotesDeIntegracion.Asignar(lote, entregas);
        return Result.Success(new LoteDelProceso(lote.PublicId, lote.Number, entregas.Count));
    }

    private static string Mes(DateOnly fecha) => fecha.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static string Recortar(string texto) => texto.Length <= 1000 ? texto : texto[..1000];
}
