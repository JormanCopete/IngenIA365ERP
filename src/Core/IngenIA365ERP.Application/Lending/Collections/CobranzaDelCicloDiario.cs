using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Collections.Agreements;
using IngenIA365ERP.Application.Lending.Collections.Assignments;
using IngenIA365ERP.Application.Lending.Collections.Campaigns;
using IngenIA365ERP.Application.Lending.Collections.Claims;
using IngenIA365ERP.Application.Lending.Collections.Fees;
using IngenIA365ERP.Application.Lending.Collections.PriorNotices;
using IngenIA365ERP.Application.Lending.Collections.Stages;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Collections;

/// <summary>Un crédito de la tanda con su estado al día del ciclo (el de la causación, o reconstruido si ya estaba al día). (nuevo)</summary>
public sealed record CreditoParaCobranza(Loan Credito, EstadoDelCredito Estado);

/// <summary>Lo que hizo el paso de cobranza en una tanda o al cerrar la corrida. (nuevo)</summary>
public sealed record ResultadoDeCobranzaDelDia(
    int CambiosDeEtapa,
    int Asignaciones,
    int Mensajes,
    int AvisosAbiertos,
    int GastosPropuestos,
    int PromesasIncumplidas,
    int AcuerdosIncumplidos,
    int ReclamosAceptados,
    IReadOnlyList<AlertaALevantar> Alertas)
{
    public static ResultadoDeCobranzaDelDia Vacio { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, []);
}

/// <summary>
/// El paso (7) del ciclo diario de Cartera (feature 013, K4, T0943; DT §2.13; FR-117–FR-121, FR-123; data-model §26): sólo con
/// <see cref="CatalogoDeCartera.EntregaVigente"/> en K4 o después. Corre dentro de la tanda y de su <c>SaveChanges</c> —no guarda nada—, sobre los créditos de
/// la tanda, con el actor del proceso.
/// <list type="number">
/// <item><b>Etapas</b>: los días de mora (o, al día, los que faltan para el próximo vencimiento con signo menos) eligen la etapa vigente
/// (<see cref="EtapasDeCobranza"/>); el crédito <b>entra</b> a una etapa cuando cambia de etapa o vuelve a la misma después de haber salido (el día
/// anterior estaba fuera de su rango), con una entrada <c>Source = DailyCycle</c> y la corrida. Un crédito con un acuerdo de pago aprobado y al día no
/// avanza; el día en que el acuerdo se incumple vuelve a la etapa que le corresponde (FR-120). No entra dos veces el mismo día.</item>
/// <item><b>Reparto</b>: los que entraron se reparten con las reglas vigentes (<see cref="RepartoDeGestores"/>); quien ya tiene a un gestor de la regla lo
/// conserva.</item>
/// <item><b>Mensajes de la etapa</b> (sus plantillas, por el primer canal que la ley y el titular permiten), el <b>aviso previo</b> del episodio si la etapa
/// lo pide (la acción «aviso previo»: se abre aquí; sus comunicaciones las encola el cierre del ciclo, cuando el aviso ya tiene Id) y el <b>gasto de
/// cobranza</b> de la regla de la etapa, que sólo se propone e informa al deudor (<c>CollectionFeeNotice</c>): causarlo es de una persona.</item>
/// <item><b>Promesas</b> vencidas ayer sin el pago: <c>Cartera.PromesaIncumplida</c> al gestor (vuelven a la agenda).</item>
/// </list>
/// Al cerrar la corrida (<see cref="CierreAsync"/>): los reclamos sobre el reporte vencidos sin respuesta pasan a <c>TacitlyAccepted</c> con
/// <c>Cartera.PlazoRegulatorioPorVencer</c>; las comunicaciones pendientes de los avisos previos (la primera; la segunda en otro día) y
/// <c>Cartera.AvisoPrevioSinConstancia</c> para el aviso que, pasados sus días mínimos desde que se abrió, sigue sin constancia; y el estado de las campañas.
/// No toca a los créditos con el deudor fallecido y reclamación al seguro en trámite ni en negociación de insolvencia (FR-097, FR-098). La cobranza
/// <b>nunca contacta referencias</b>: todo mensaje va al deudor por <see cref="ColaDeMensajesAlDeudor"/>, que respeta la ventana de la Ley 2300. (nuevo)
/// </summary>
public sealed class CobranzaDelCicloDiario(
    IApplicationDbContext db,
    ColaDeMensajesAlDeudor cola,
    ReglasDeParametrosDeCartera parametros,
    IValoresNacionales valores,
    CargadorDeReconstruccion cargador,
    IDateTimeService reloj,
    ILogger<CobranzaDelCicloDiario>? logger = null,
    EntregaDeCartera? entrega = null)
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    /// <summary>El motivo de la entrada de etapa cuando un acuerdo se incumple.</summary>
    public const string MotivoAcuerdoIncumplido = "Acuerdo de pago incumplido";

    /// <summary>¿La entrega vigente ya trae la cobranza? (K4)</summary>
    public bool Habilitada => (entrega ?? CatalogoDeCartera.EntregaVigente) >= EntregaDeCartera.K4;

    // ------------------------------------------------------------------------------------------------------------- tanda --

    /// <summary>
    /// El paso (7) sobre los créditos de una tanda: los causados con su estado y <paramref name="otros"/> —los que ya estaban al día a <paramref name="dia"/>,
    /// que se reconstruyen aquí—. Sin guardar.
    /// </summary>
    public async Task<ResultadoDeCobranzaDelDia> CreditosAsync(LendingProcessRun corrida, DateOnly dia, IReadOnlyList<CreditoParaCobranza> causados,
        IReadOnlyCollection<int> otros, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (!Habilitada) return ResultadoDeCobranzaDelDia.Vacio;
        // Cada crédito una sola vez: uno causado en la tanda ya trae su estado y no se reconstruye (T0933 lo destapó: el ciclo lo pasaba en las dos
        // listas y el reparto fallaba con la clave repetida, dejando la tanda entera sin guardar).
        var creditos = causados.DistinctBy(c => c.Credito.Id).ToList();
        var yaCausados = creditos.Select(c => c.Credito.Id).ToHashSet();
        var porReconstruir = otros.Where(id => !yaCausados.Contains(id)).Distinct().ToList();
        if (porReconstruir.Count > 0) creditos.AddRange(await ReconstruirAsync(corrida, porReconstruir, dia, ct));
        creditos = creditos.Where(c => c.Credito.Status is LoanStatus.Active or LoanStatus.WrittenOff).ToList();
        if (creditos.Count == 0) return ResultadoDeCobranzaDelDia.Vacio;

        var etapas = await CobranzaDeCartera.EtapasVigentesAsync(db, dia, ct);
        var ids = creditos.Select(c => c.Credito.Id).ToList();
        var excluidos = await ExcluidosAsync(creditos.Select(c => c.Credito).ToList(), dia, ct);
        var vigentes = await CobranzaDeCartera.EtapaVigenteAsync(db, ids, ct);
        var pagos = await CobranzaDeCartera.PagosVigentesAsync(db, ids, ct);
        var desfase = reloj.AhoraLocal.Offset;
        var alertas = new List<AlertaALevantar>();
        int cambios = 0, mensajes = 0, avisos = 0, gastos = 0, acuerdosRotos = 0;

        // Los acuerdos aprobados: al día congelan la etapa; incumplidos hoy, la devuelven.
        var acuerdos = (await db.PaymentAgreements.AsNoTracking().Include(a => a.Installments)
                .Where(a => ids.Contains(a.LoanId) && a.Status == PaymentAgreementStatus.Approved && !a.IsDeleted).ToListAsync(ct))
            .GroupBy(a => a.LoanId).ToDictionary(g => g.Key, g => g.ToList());

        var entraron = new List<(CreditoParaCobranza Credito, CollectionStage Etapa, CollectionStageHistoryEntry Entrada, int Dias)>();
        if (etapas.Count > 0)
        {
            var definiciones = etapas.Select(e => e.Definicion()).ToList();
            foreach (var c in creditos.Where(c => !excluidos.Contains(c.Credito.Id)))
            {
                if (CobranzaDeCartera.DiasParaEtapa(c.Estado, dia) is not { } dias) continue;
                var def = EtapasDeCobranza.EtapaPara(definiciones, dias);
                if (def is null) continue;
                var actual = vigentes.GetValueOrDefault(c.Credito.Id);
                if (actual?.EnteredOn == dia) continue;

                string? motivo = null;
                if (acuerdos.TryGetValue(c.Credito.Id, out var delCredito))
                {
                    var resultados = delCredito.Select(a => AcuerdosDePago.Cumplimiento(a, pagos.GetValueOrDefault(c.Credito.Id) ?? [], dia, desfase)).ToList();
                    if (resultados.Any(r => r?.Estado == PaymentAgreementPerformance.Current)) continue;
                    if (resultados.Any(r => r?.Estado == PaymentAgreementPerformance.Broken && r.IncumplidoEl == dia))
                    {
                        motivo = MotivoAcuerdoIncumplido;
                        acuerdosRotos++;
                    }
                }
                var entra = motivo is not null || actual is null || actual.StageCode != def.Codigo || !def.Contiene(dias - 1);
                if (!entra) continue;

                var etapa = etapas.First(e => e.Code == def.Codigo);
                var entrada = CollectionStageHistoryEntry.Registrar(c.Credito.Id, etapa, dia, dias,
                    CollectionStageHistoryEntry.FuenteCicloDiario, corrida.Id, motivo: motivo, anteriorId: actual is { Id: > 0 } a ? a.Id : null);
                db.CollectionStageHistory.Add(entrada);
                entraron.Add((c, etapa, entrada, dias));
                cambios++;
            }
        }

        var asignaciones = await RepartirAsync(entraron.Select(e => e.Credito).ToList(), dia, ct);

        foreach (var (c, etapa, entrada, dias) in entraron)
        {
            // Los mensajes de la etapa.
            foreach (var plantilla in AccionesDeEtapa.Leer(etapa.TemplateCodesJson))
            {
                var r = await EnvioDeCobranza.EncolarAsync(cola, canal => new PedidoDeMensajeAlDeudor(c.Credito.DebtorPersonId, c.Credito.Id,
                    DebtorMessagePurpose.Collection, canal, plantilla, "CollectionStage", $"{c.Credito.PublicId:N}:{etapa.Code}:{plantilla}", ActorKind.Process, null)
                {
                    OriginPublicId = entrada.PublicId,
                    Valores = Variables(c.Estado, dias),
                }, reloj.UtcNow, ct);
                if (r.IsSuccess && !r.Value.Repetido) mensajes++;
                else if (r.IsFailure) _log.LogWarning("[Cartera.MensajeDeEtapaSinSalir] El crédito {Credito} entró a {Etapa} y su mensaje {Plantilla} no se pudo encolar: {Codigo}",
                    c.Credito.Number, etapa.Code, plantilla, r.Error.Code);
            }

            // El aviso previo del episodio, si la etapa lo pide y el crédito está en mora.
            if (EtapasDeCartera.PideAvisoPrevio(etapa) && c.Estado.AlturaDeMora.Dias > 0)
            {
                var abierto = await AvisosPrevios.AbrirAsync(db, parametros, valores, c.Credito, dia.AddDays(-c.Estado.AlturaDeMora.Dias),
                    Math.Max(0m, c.Estado.ValorParaCancelar), dia, ct);
                if (abierto.IsSuccess && abierto.Value.Nuevo) avisos++;
                else if (abierto.IsFailure)
                    _log.LogWarning("[Cartera.AvisoPrevioSinAbrir] El aviso previo del crédito {Credito} no se abrió: {Codigo} {Mensaje}", c.Credito.Number,
                        abierto.Error.Code, abierto.Error.Message);
            }

            // El gasto de cobranza de la regla de la etapa: se propone e informa; lo causa una persona.
            if (ReglaDeGastoDeCobranza.Leer(etapa.CollectionFeeRuleJson) is { Moment: ReglaDeGastoDeCobranza.MomentoAlEntrar } regla && c.Credito.Status == LoanStatus.Active)
            {
                var valor = regla.Calcular(CobranzaDeCartera.ValorVencido(c.Estado), c.Estado.CapitalVencido);
                if (valor > 0m)
                {
                    var r = await GastosDeCobranza.InformarAsync(cola, c.Credito, new GastoPropuesto(valor, etapa.Code,
                        GastosDeCobranza.ClaveDeEtapa(c.Credito.PublicId, etapa.Code, valor), entrada.PublicId), reloj, ActorKind.Process, null, ct);
                    if (r.IsSuccess && !r.Value.Repetido) gastos++;
                }
            }
        }

        var promesas = await PromesasIncumplidasAsync(creditos.Select(c => c.Credito).ToList(), pagos, dia, ct);
        alertas.AddRange(promesas);
        return new ResultadoDeCobranzaDelDia(cambios, asignaciones, mensajes, avisos, gastos, promesas.Count, acuerdosRotos, 0, alertas);
    }

    // ----------------------------------------------------------------------------------------------------- cierre del ciclo --

    /// <summary>
    /// Lo del paso (7) que es de la cooperativa y no de una tanda, al terminar la corrida (antes de su guardado): la aceptación tácita de los reclamos
    /// vencidos, las comunicaciones de los avisos previos y su alerta sin constancia, y el estado de las campañas. Sin guardar.
    /// </summary>
    public async Task<ResultadoDeCobranzaDelDia> CierreAsync(DateOnly dia, CancellationToken ct)
    {
        if (!Habilitada) return ResultadoDeCobranzaDelDia.Vacio;
        var alertas = new List<AlertaALevantar>();

        var aceptados = await AceptacionTacita.AplicarAsync(db, dia, ct);
        if (aceptados.Count > 0)
        {
            var personas = await ReferenciasDeGobierno.PersonasAsync(db, aceptados.Select(a => a.PersonId), ct);
            foreach (var r in aceptados)
                alertas.Add(new AlertaALevantar(TiposDeAlerta.PlazoRegulatorioPorVencer, "Reclamo sobre el reporte aceptado tácitamente",
                    $"El reclamo de {personas[r.PersonId].FullName} sobre el reporte a centrales venció el {ErroresDeCobranza.Fecha(r.ExtendedDueDate ?? r.DueDate)} sin "
                    + "respuesta y quedó aceptado tácitamente: corrija el reporte a favor del titular.",
                    EntityType: nameof(CreditReportClaim), EntityPublicId: r.PublicId,
                    DedupKey: $"{TiposDeAlerta.PlazoRegulatorioPorVencer}:reclamo:{r.PublicId:D}", ModuloDeOrigen: "Lending"));
        }

        var (mensajes, sinConstancia) = await AvisosAsync(dia, ct);
        alertas.AddRange(sinConstancia);
        await CampanasDeCobranza.AvanzarAsync(db, ct);
        return new ResultadoDeCobranzaDelDia(0, 0, mensajes, 0, 0, 0, 0, aceptados.Count, alertas);
    }

    // ------------------------------------------------------------------------------------------------------------ privados --

    private async Task<IReadOnlyList<CreditoParaCobranza>> ReconstruirAsync(LendingProcessRun corrida, IReadOnlyCollection<int> otros, DateOnly dia,
        CancellationToken ct)
    {
        var cargados = await cargador.CargarAsync(otros, corrida.InformationCutoffAt, dia, ct);
        var salida = new List<CreditoParaCobranza>();
        foreach (var c in cargados.Values.Where(c => c.Listo))
        {
            try
            {
                salida.Add(new CreditoParaCobranza(c.Credito, MotorDeCartera.Reconstruir(c.Entrada!, dia)));
            }
            catch (ReglaDeCarteraException ex)
            {
                _log.LogWarning("[Cartera.CobranzaSinEstado] El crédito {Credito} no se pudo reconstruir para la cobranza del {Dia}: {Codigo}", c.Credito.Number, dia,
                    ex.Codigo);
            }
        }
        return salida;
    }

    /// <summary>Los créditos con el deudor fallecido y reclamación en trámite o en negociación de insolvencia: la cobranza no los toca.</summary>
    private async Task<IReadOnlySet<int>> ExcluidosAsync(IReadOnlyList<Loan> creditos, DateOnly dia, CancellationToken ct)
    {
        var ids = creditos.Select(c => c.Id).ToList();
        var conReclamacion = await DebtorEvents.ReclamacionesEnTramite.CreditosAsync(db, ids, ct);
        var personas = creditos.Select(c => c.DebtorPersonId).Distinct().ToList();
        var enInsolvencia = (await db.InsolvencyProceedings.AsNoTracking().Where(p => personas.Contains(p.PersonId) && !p.IsDeleted
            && p.Status == InsolvencyProceedingStatus.Negotiating && p.StartDate <= dia).Select(p => p.PersonId).ToListAsync(ct)).ToHashSet();
        return creditos.Where(c => conReclamacion.Contains(c.Id) || enInsolvencia.Contains(c.DebtorPersonId)).Select(c => c.Id).ToHashSet();
    }

    /// <summary>El reparto de los que entraron a una etapa con las reglas vigentes; devuelve cuántas asignaciones escribió.</summary>
    private async Task<int> RepartirAsync(IReadOnlyList<CreditoParaCobranza> entraron, DateOnly dia, CancellationToken ct)
    {
        if (entraron.Count == 0) return 0;
        var reglas = await db.CollectorAssignmentRules.AsNoTracking()
            .Where(r => !r.IsDeleted && r.ValidFrom <= dia && (r.ValidTo == null || r.ValidTo >= dia)).ToListAsync(ct);
        if (reglas.Count == 0) return 0;
        var creditos = entraron.Select(e => e.Credito).ToList();
        var ids = creditos.Select(c => c.Id).ToList();
        var etapas = await CobranzaDeCartera.EtapaVigenteAsync(db, ids, ct);
        var vigentes = await CobranzaDeCartera.GestorVigenteAsync(db, creditos.Select(c => (c.Id, c.DebtorPersonId)).ToList(), ct);
        var saldos = entraron.ToDictionary(e => e.Credito.Id, e => e.Estado.Saldo(LoanConcept.Principal));
        var candidatos = await AsignacionesDeCobranza.CandidatosAsync(db, creditos, saldos, etapas, vigentes, ct);
        ResultadoDeReparto reparto;
        try
        {
            reparto = RepartoDeGestores.Repartir(reglas.Select(r => r.Regla()).ToList(), candidatos);
        }
        catch (ReglaDeCarteraException ex)
        {
            _log.LogWarning("[Cartera.ReglaDeRepartoInvalida] Las reglas de reparto vigentes no sirven ({Codigo}): no se repartió la cartera del {Dia}.", ex.Codigo, dia);
            return 0;
        }
        var gestores = await AsignacionesDeCobranza.UsuariosAsync(db, reparto.Asignaciones.Select(a => a.GestorPublicId), ct);
        var validas = reparto with { Asignaciones = reparto.Asignaciones.Where(a => gestores.ContainsKey(a.GestorPublicId)).ToList() };
        return AsignacionesDeCobranza.Escribir(db, validas, creditos.ToDictionary(c => c.PublicId), vigentes, gestores,
            reglas.ToDictionary(r => r.PublicId, r => r.Id), dia, null, AsignacionesDeCobranza.MotivoDelCiclo);
    }

    /// <summary>Las promesas de los créditos que vencieron ayer sin el pago: una alerta por promesa al gestor, con el alcance del crédito.</summary>
    private async Task<IReadOnlyList<AlertaALevantar>> PromesasIncumplidasAsync(IReadOnlyList<Loan> creditos, IReadOnlyDictionary<int, IReadOnlyList<PagoParaCobranza>> pagos,
        DateOnly dia, CancellationToken ct)
    {
        var ids = creditos.Select(c => c.Id).ToList();
        var ayer = dia.AddDays(-1);
        var vencidas = await (from p in db.PaymentPromises.AsNoTracking()
                              join a in db.CollectionActions.AsNoTracking() on p.CollectionActionId equals a.Id
                              where ids.Contains(p.LoanId) && !p.IsDeleted && p.PromisedDate == ayer
                              select new { Promesa = p, a.OccurredAt, a.CollectorUserId, Anulada = db.CollectionActions.Any(x => x.VoidsActionId == a.Id) })
            .ToListAsync(ct);
        if (vencidas.Count == 0) return [];
        var porCredito = creditos.ToDictionary(c => c.Id);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, vencidas.Select(v => v.CollectorUserId), ct);
        var oficinas = creditos.Select(c => c.BranchId).Distinct().ToList();
        var deOficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
        var lineas = creditos.Select(c => c.CreditLineId).Distinct().ToList();
        var deLinea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => lineas.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.PublicId, ct);
        var alertas = new List<AlertaALevantar>();
        foreach (var v in vencidas.Where(v => !v.Anulada))
        {
            var estado = EstadoDePromesa.Calcular(v.Promesa.ParaEstado(v.OccurredAt, false), pagos.GetValueOrDefault(v.Promesa.LoanId) ?? [], dia);
            if (estado.Estado != PaymentPromiseStatus.Broken) continue;
            var c = porCredito[v.Promesa.LoanId];
            alertas.Add(new AlertaALevantar(TiposDeAlerta.PromesaIncumplida, $"Promesa incumplida: crédito {c.Number}",
                $"La promesa de pago de {ErroresDeCobranza.Pesos(v.Promesa.PromisedAmount)} para el {ErroresDeCobranza.Fecha(v.Promesa.PromisedDate)} del crédito {c.Number} "
                + $"venció sin el pago (abonado {ErroresDeCobranza.Pesos(estado.Abonado)}). Vuelve a la agenda de {usuarios[v.CollectorUserId].Name}.",
                EntityType: nameof(PaymentPromise), EntityPublicId: v.Promesa.PublicId,
                DedupKey: $"{TiposDeAlerta.PromesaIncumplida}:{v.Promesa.PublicId:D}", ModuloDeOrigen: "Lending",
                ScopeBranchPublicId: deOficina.TryGetValue(c.BranchId, out var o) ? o : null, ScopeCreditLinePublicId: deLinea.TryGetValue(c.CreditLineId, out var l) ? l : null));
        }
        return alertas;
    }

    /// <summary>
    /// Las comunicaciones pendientes de los avisos previos abiertos (la primera; la segunda, en otro día que la primera y cuando ésta ya salió) y
    /// <c>Cartera.AvisoPrevioSinConstancia</c> para el aviso que lleva sus días mínimos abierto sin la constancia exigida.
    /// </summary>
    private async Task<(int Mensajes, IReadOnlyList<AlertaALevantar> Alertas)> AvisosAsync(DateOnly dia, CancellationToken ct)
    {
        var abiertos = await (from n in db.PriorNotices
                              join l in db.Loans.AsNoTracking() on n.LoanId equals l.Id
                              where !n.IsDeleted && n.MigratedReportedNegativeSince == null && (l.Status == LoanStatus.Active || l.Status == LoanStatus.WrittenOff)
                              select n).ToListAsync(ct);
        if (abiertos.Count == 0) return (0, []);
        var desfase = reloj.AhoraLocal.Offset;
        var evaluados = await AvisosPrevios.EvaluarAsync(db, abiertos, cola.EnProduccion, desfase, ct);
        var loanIds = abiertos.Select(a => a.LoanId).Distinct().ToArray();
        var creditos = await db.Loans.AsNoTracking().Where(l => loanIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var posiciones = (await db.LoanDailyPositions.AsNoTracking().Where(p => loanIds.Contains(p.LoanId) && p.PositionDate <= dia).ToListAsync(ct))
            .GroupBy(p => p.LoanId).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).First());
        var mensajes = 0;
        var alertas = new List<AlertaALevantar>();
        foreach (var a in abiertos)
        {
            var (e, comunicaciones) = evaluados[a.Id];
            if (e.Estado is PriorNoticeStatus.Valid or PriorNoticeStatus.Closed) continue;
            var credito = creditos[a.LoanId];
            // Una comunicación en cola espera; una excluida (sin canal autorizado) la resuelve una persona —la carta con su guía—, no el ciclo.
            var pendiente = comunicaciones.Any(x => x.LastEvent is DebtorMessageEventKind.Queued or DebtorMessageEventKind.Deferred)
                            || comunicaciones.LastOrDefault()?.LastEvent == DebtorMessageEventKind.Excluded;
            var salieronHoy = e.DiasConConstancia.Contains(dia);
            if (!pendiente && !salieronHoy && e.DiasConConstancia.Count < a.RequiredCommunications)
            {
                var p = posiciones.GetValueOrDefault(a.LoanId);
                var r = await EnvioDeCobranza.EncolarAsync(cola, canal => new PedidoDeMensajeAlDeudor(a.PersonId, a.LoanId, DebtorMessagePurpose.PriorNotice, canal,
                    AvisosPrevios.Plantilla, AvisosPrevios.Origen, $"{a.PublicId:N}:{comunicaciones.Count + 1}", ActorKind.Process, null)
                {
                    OriginPublicId = a.PublicId,
                    PriorNoticeId = a.Id,
                    Valores = AvisosPrevios.Variables(p?.PayoffAmount ?? 0m, p is null ? 0m : p.PrincipalPastDue + p.DefaultInterest + p.DefaultInterestInSuspense,
                        dia.AddDays(a.MinimumDays)),
                }, reloj.UtcNow, ct);
                if (r.IsSuccess && !r.Value.Repetido) mensajes++;
            }
            var abiertoEl = DateOnly.FromDateTime(CobranzaDeCartera.ALocal(a.CreatedAt == default ? reloj.UtcNow : a.CreatedAt, desfase));
            if (abiertoEl.AddDays(a.MinimumDays) <= dia)
                alertas.Add(new AlertaALevantar(TiposDeAlerta.AvisoPrevioSinConstancia, $"Aviso previo sin constancia: crédito {credito.Number}",
                    $"El aviso previo del episodio de mora del crédito {credito.Number} (desde el {ErroresDeCobranza.Fecha(a.EpisodeStartDate)}) lleva "
                    + $"{(dia.DayNumber - abiertoEl.DayNumber).ToString(CultureInfo.InvariantCulture)} días sin la constancia que exige la ley: el dato negativo no "
                    + "se puede reportar. Registre la constancia de la carta o revise por qué no salió el mensaje.",
                    EntityType: nameof(PriorNotice), EntityPublicId: a.PublicId, DedupKey: $"{TiposDeAlerta.AvisoPrevioSinConstancia}:{a.PublicId:D}",
                    ModuloDeOrigen: "Lending"));
        }
        return (mensajes, alertas);
    }

    /// <summary>Las variables de un mensaje de cobranza desde el estado del crédito (adaptadores-y-archivos.md §4.7).</summary>
    public static Dictionary<string, string?> Variables(EstadoDelCredito estado, int dias)
    {
        var vencido = CobranzaDeCartera.ValorVencido(estado);
        return new Dictionary<string, string?>
        {
            ["ValorVencido"] = ErroresDeCobranza.Pesos(vencido),
            ["DiasDeMora"] = Math.Max(0, dias).ToString(CultureInfo.InvariantCulture),
            ["CuotasVencidas"] = estado.Cuotas.Count(c => c.Vencida && estado.Saldos.Any(s => s.Cuota == c.Numero && s.Valor > 0m))
                .ToString(CultureInfo.InvariantCulture),
            ["ValorParaPonerseAlDia"] = ErroresDeCobranza.Pesos(vencido),
        };
    }
}
