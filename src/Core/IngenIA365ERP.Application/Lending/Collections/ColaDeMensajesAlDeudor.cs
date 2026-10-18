using System.Globalization;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Collections;

/// <summary>
/// Lo que pide quien motiva un mensaje al deudor (T0306): a quién (persona y crédito), para qué, por dónde, con qué plantilla, de dónde
/// sale y quién lo pide. <see cref="ClaveDeEntidad"/> es la <c>{entidad}</c> de la clave de idempotencia
/// <c>{origen}:{entidad}:{propósito}:{fecha}</c>: quien encola a varios destinatarios por la misma causa la hace distinta por destinatario.
/// <see cref="ProgramadoPara"/> es un instante UTC (nulo = ahora); <see cref="Inmediato"/> es el envío manual inmediato, que fuera de ventana
/// se rechaza en vez de diferirse. <see cref="Valores"/> llena las variables propias del propósito; las comunes las pone la cola. (nuevo)
/// </summary>
public sealed record PedidoDeMensajeAlDeudor(
    int PersonId,
    int? LoanId,
    DebtorMessagePurpose Purpose,
    MessageChannel Channel,
    string TemplateCode,
    string OriginKind,
    string ClaveDeEntidad,
    ActorKind ActorKind,
    int? ActorUserId)
{
    public Guid? OriginPublicId { get; init; }
    public DateTime? ProgramadoPara { get; init; }
    public bool Inmediato { get; init; }
    public IReadOnlyDictionary<string, string?>? Valores { get; init; }
    public Guid? AttachmentPublicId { get; init; }
    public int? PriorNoticeId { get; init; }
    public int? IssuedDocumentId { get; init; }
    public int? InstallmentNonDeductionId { get; init; }

    /// <summary>K4 (T0949): la campaña que lo programa (<c>LND_DebtorMessages.CampaignId</c>).</summary>
    public int? CampaignId { get; init; }
}

/// <summary>El mensaje que quedó en la unidad de trabajo, con su evento vigente; <see cref="Repetido"/> si la clave ya existía. (nuevo)</summary>
public sealed record MensajeEncolado(DebtorMessage Mensaje, DebtorMessageEvent Evento, bool Repetido, IReadOnlyList<AvisoDePlantilla> Avisos);

/// <summary>Lo que se le pregunta a la ventana legal: a quién, por qué canal, para qué y cuándo (UTC). (nuevo)</summary>
public sealed record ConsultaDeVentana(int PersonId, int? LoanId, MessageChannel Channel, DebtorMessagePurpose Purpose, DateTime PedidoUtc, bool Inmediato)
{
    /// <summary>El mensaje que se está despachando: no cuenta en su propio historial.</summary>
    public int? SinElMensaje { get; init; }

    /// <summary>
    /// Feature 013, US2 (T0390, C87): la persona a la que se le simuló un crédito y se le envía la simulación (informativo, sin crédito). Todavía
    /// no es deudora: cuenta como obligada sólo para ese mensaje, que respeta igual la ventana, la autorización de canal y el tope diario.
    /// </summary>
    public bool PersonaSimulada { get; init; }

    /// <summary>
    /// K4 (T0945): se evalúa en el instante pedido y no en el de ahora —una gestión que ya ocurrió se juzga en su momento, aunque se registre
    /// minutos después—.
    /// </summary>
    public bool EnElInstantePedido { get; init; }
}

/// <summary>
/// La respuesta de la ventana legal (T0306): la decisión del motor (instantes locales), si el destinatario está obligado y con qué papel,
/// su nombre, la autorización de canal que se usaría, el canal que fijó la semana (si hay uno distinto) y el lunes de esa semana. (nuevo)
/// </summary>
public sealed record VentanaDeContacto(
    DecisionDeContacto Decision,
    bool Obligado,
    ContactRole Rol,
    string NombreDelTitular,
    Guid? AutorizacionPublicId,
    MessageChannel? CanalDeLaSemana,
    DateOnly InicioDeSemana,
    TimeSpan Desfase)
{
    /// <summary>El instante en que sale (permitido) o el siguiente permitido (diferido), con la hora de Colombia.</summary>
    public DateTimeOffset? PermitidoDesde => Decision switch
    {
        Permitido p => new DateTimeOffset(DateTime.SpecifyKind(p.En, DateTimeKind.Unspecified), Desfase),
        Diferido d => new DateTimeOffset(DateTime.SpecifyKind(d.Siguiente, DateTimeKind.Unspecified), Desfase),
        _ => null,
    };
}

/// <summary>
/// La cola de mensajes al deudor (feature 013, T0306; T74, T75, C108; contracts/adaptadores-y-archivos.md §4.1–§4.6; data-model §26.1–§26.2;
/// FR-122, FR-142, C57). Es el <b>único escritor</b> de <c>LND_DebtorMessages</c> y <c>LND_DebtorMessageEvents</c> y <b>nunca guarda</b>: el
/// mensaje nace en la unidad de trabajo de quien lo motiva —el abono con preferencia (D24), la cuota no descontada (FR-112), el envío de la
/// simulación, la entrega de un documento, el envío manual— y cada paso de su historia lo agrega el comando que lo registra.
/// <list type="bullet">
/// <item>Sólo al deudor y a los codeudores obligados (FR-122); <b>nunca</b> a una referencia: no lee <c>COR_References</c>
/// (<c>LaCobranzaNuncaContactaReferencias</c>). <c>COR_Notifications</c> no se usa: su destinatario es un usuario del ERP (T74).</item>
/// <item>La ventana legal la decide <see cref="VentanaLegalDeContacto"/> con las reglas efectivas —<c>Cobranza.HorarioLunesAViernes</c>,
/// <c>Cobranza.HorarioSabado</c> y <c>Cobranza.ContactosPorDia</c> de la cooperativa acotados por los <c>COBRANZA_*</c> de la ley
/// (<see cref="ReglasDeParametrosDeCartera"/>)—, los festivos nacionales de <see cref="ICalendarioHabil"/>, las autorizaciones de
/// <see cref="IAutorizacionDeDatos"/> y el historial de la semana: al programar nace <c>Queued</c>, <c>Deferred</c> o <c>Excluded</c>.</item>
/// <item>Sella la plantilla vigente con su versión, el cuerpo renderizado una vez y el adaptador del canal (correo de la plataforma; el SMS o
/// WhatsApp configurado por la cooperativa o <c>SIMULADO</c>).</item>
/// <item>La clave <c>{origen}:{entidad}:{propósito}:{fecha}</c> es única: repetirla devuelve el mismo mensaje.</item>
/// <item>Antes de la activación no encola, y nunca programa con fecha anterior al registro de la activación (FR-142).</item>
/// </list>
/// Todos los instantes se guardan en UTC; el motor razona en la hora de Colombia (T9). (nuevo)
/// </summary>
public sealed class ColaDeMensajesAlDeudor(
    IApplicationDbContext db,
    IDateTimeService reloj,
    ReglasDeParametrosDeCartera parametros,
    ICalendarioHabil calendario,
    IAutorizacionDeDatos autorizaciones,
    ICanalesDeMensajeria canales,
    IOptions<CanalesExternosOptions> opciones)
{
    /// <summary>El módulo de las plantillas de Cartera en <c>COR_NotificationTemplates</c>.</summary>
    public const string Modulo = CreateMessageTemplateVersionCommandHandler.Modulo;

    /// <summary>El código de motivo de una cancelación porque la causa se reversó.</summary>
    public const string MotivoCausaReversada = "CauseReversed";

    private const int LargoDelAsunto = 300;
    private const int LargoDelTexto = 500;
    private const int DiasDeHistorialHaciaAtras = 7;

    /// <summary>
    /// El origen del envío de una simulación (US2, T0390, C87): un informativo sin crédito a la persona simulada, que todavía no es deudora.
    /// </summary>
    public const string OrigenSimulacion = DebtorMessage.OrigenSimulacion;

    /// <summary>¿Es el envío de una simulación? Sólo un informativo de ese origen y sin crédito.</summary>
    public static bool EsDeSimulacion(string? origen, int? loanId, DebtorMessagePurpose proposito) =>
        loanId is null && proposito == DebtorMessagePurpose.Informative && string.Equals(origen, OrigenSimulacion, StringComparison.Ordinal);

    /// <summary>¿El despliegue es producción? (la guardia del efecto legal y el estado final del simulado lo usan).</summary>
    public bool EnProduccion => !opciones.Value.AmbienteDePruebas;

    // ------------------------------------------------------------------------------------------------------------ encolar --

    /// <summary>
    /// Encola un mensaje en la unidad de trabajo de quien lo motiva, <b>sin guardar</b>. Falla si Cartera no está activada
    /// (<c>Lending.NotActivated</c>), si el destinatario no está obligado (<c>Lending.DebtorMessage.RecipientNotObligated</c>), si no hay
    /// plantilla vigente (<c>Lending.MessageTemplate.NotFound</c>) y, en el envío manual inmediato, si la ventana no lo permite ahora
    /// (<c>.OutsideLegalWindow</c>, <c>.WeeklyChannelUsed</c>, <c>.ChannelNotAuthorized</c>, <c>Lending.Authorization.WithoutEvidence</c>).
    /// </summary>
    public async Task<Result<MensajeEncolado>> EncolarAsync(PedidoDeMensajeAlDeudor pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        if (!DebtorMessage.Origenes.Contains(pedido.OriginKind, StringComparer.Ordinal))
            throw new ArgumentException($"«{pedido.OriginKind}» no es un origen de mensaje al deudor.", nameof(pedido));
        if (string.IsNullOrWhiteSpace(pedido.ClaveDeEntidad))
            throw new ArgumentException("La entidad de la clave de idempotencia es obligatoria.", nameof(pedido));

        var setup = await db.LendingSetups.AsNoTracking().FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct);
        if (setup?.ActivationDate is null) return Result.Failure<MensajeEncolado>(ErroresDeMensajesAlDeudor.CarteraSinActivar());

        var ahora = reloj.UtcNow;
        var desfase = reloj.AhoraLocal.Offset;
        var pedidoUtc = pedido.ProgramadoPara is { } programado ? DateTime.SpecifyKind(programado, DateTimeKind.Utc) : ahora;
        // FR-142: lo vencido entre el corte y el registro de la activación no genera mensajes con fecha anterior al registro.
        if (setup.ActivatedAt is { } registro && pedidoUtc < registro) pedidoUtc = DateTime.SpecifyKind(registro, DateTimeKind.Utc);

        var fecha = DateOnly.FromDateTime(ALocal(pedidoUtc, desfase));
        var clave = string.Create(CultureInfo.InvariantCulture,
            $"{pedido.OriginKind}:{pedido.ClaveDeEntidad.Trim()}:{pedido.Purpose}:{fecha:yyyy-MM-dd}");
        if (clave.Length > DebtorMessage.LargoDeLaClave)
            throw new ArgumentException($"La clave de idempotencia pasa de {DebtorMessage.LargoDeLaClave} caracteres.", nameof(pedido));

        var existente = db.DebtorMessages.Local.FirstOrDefault(m => m.IdempotencyKey == clave)
                        ?? await db.DebtorMessages.FirstOrDefaultAsync(m => m.IdempotencyKey == clave, ct);
        if (existente is not null)
        {
            var vigente = await UltimoEventoAsync(existente, ct)
                          ?? throw new InvalidOperationException($"El mensaje {existente.PublicId} no tiene historia.");
            return Result.Success(new MensajeEncolado(existente, vigente, true, []));
        }

        var evaluada = await EvaluarAsync(new ConsultaDeVentana(pedido.PersonId, pedido.LoanId, pedido.Channel, pedido.Purpose, pedidoUtc,
            pedido.Inmediato) { PersonaSimulada = EsDeSimulacion(pedido.OriginKind, pedido.LoanId, pedido.Purpose) }, ct);
        if (evaluada.IsFailure) return Result.Failure<MensajeEncolado>(evaluada.Error);
        var ventana = evaluada.Value;
        if (!ventana.Obligado) return Result.Failure<MensajeEncolado>(ErroresDeMensajesAlDeudor.DestinatarioNoObligado());
        if (pedido.Inmediato && RechazoDelInmediato(ventana, pedido.Channel) is { } rechazo) return Result.Failure<MensajeEncolado>(rechazo);

        var hoy = reloj.HoyLocal;
        var plantilla = await PlantillaVigenteAsync(pedido.TemplateCode, pedido.Channel, hoy, ct);
        if (plantilla is null)
            return Result.Failure<MensajeEncolado>(ErroresDeMensajesAlDeudor.PlantillaInexistente(Codigo(pedido.TemplateCode), pedido.Channel));
        if (!string.Equals(plantilla.Purpose, pedido.Purpose.ToString(), StringComparison.Ordinal))
            return Result.Failure<MensajeEncolado>(ErroresDePlantillasDeMensaje.OtroProposito(plantilla.Code, pedido.Channel, plantilla.Purpose));

        var valores = await ValoresComunesAsync(ventana.NombreDelTitular, pedido.LoanId, hoy, ct);
        foreach (var (nombre, valor) in pedido.Valores ?? new Dictionary<string, string?>()) valores[nombre] = valor;
        var renderizado = RenderizadorDePlantillasDeMensaje.Renderizar(pedido.Purpose, pedido.Channel, plantilla.Subject, plantilla.BodyTemplate,
            plantilla.ProviderTemplateId, valores);
        if (renderizado.IsFailure) return Result.Failure<MensajeEncolado>(renderizado.Error);

        var sinAutorizacionDeCanal = pedido.Purpose == DebtorMessagePurpose.DocumentDelivery || pedido.Channel == MessageChannel.Letter;
        var borrador = new BorradorDeMensajeAlDeudor(pedido.PersonId, ventana.Rol, pedido.LoanId, pedido.Purpose, pedido.Channel,
            await CodigoDeCanalAsync(pedido.Channel, hoy, ct), renderizado.Value.Cuerpo, clave, pedido.OriginKind, ahora, pedido.ActorKind,
            pedido.ActorUserId)
        {
            TemplateId = plantilla.Id,
            TemplateVersion = plantilla.Version,
            Subject = Recortar(renderizado.Value.Asunto, LargoDelAsunto),
            AttachmentPublicId = pedido.AttachmentPublicId,
            OriginPublicId = pedido.OriginPublicId,
            PriorNoticeId = pedido.PriorNoticeId,
            IssuedDocumentId = pedido.IssuedDocumentId,
            InstallmentNonDeductionId = pedido.InstallmentNonDeductionId,
            CampaignId = pedido.CampaignId,
            AuthorizationConsentPublicId = sinAutorizacionDeCanal ? null : ventana.AutorizacionPublicId,
            IsManualImmediate = pedido.Inmediato,
            ScheduledFor = pedido.ProgramadoPara is null ? null : pedidoUtc,
        };
        if (DebtorMessage.Validar(borrador) is { } problema) return Result.Failure<MensajeEncolado>(ErroresDeGobierno.De(problema));

        var mensaje = DebtorMessage.Nacer(borrador);
        var evento = DebtorMessageEvent.Registrar(mensaje, null, EventoAlNacer(ventana, ahora, pedido.ActorKind, pedido.ActorUserId), EnProduccion);
        db.DebtorMessages.Add(mensaje);
        db.DebtorMessageEvents.Add(evento);
        return Result.Success(new MensajeEncolado(mensaje, evento, false, renderizado.Value.Avisos));
    }

    /// <summary>
    /// La causa se reversó (T0306; §4.4): los mensajes de ese origen que todavía no salieron (<c>Queued</c> o <c>Deferred</c>) se cancelan
    /// en la unidad de trabajo del reverso, sin guardar. Lo que ya salió no se toca. Devuelve cuántos canceló.
    /// </summary>
    public async Task<int> CancelarPorCausaAsync(string originKind, Guid originPublicId, string motivo, ActorKind actorKind, int? actorUserId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        var guardados = await db.DebtorMessages.Where(m => m.OriginKind == originKind && m.OriginPublicId == originPublicId).ToListAsync(ct);
        var mensajes = guardados
            .Concat(db.DebtorMessages.Local.Where(m => m.OriginKind == originKind && m.OriginPublicId == originPublicId))
            .Distinct()
            .ToList();

        var canceladas = 0;
        foreach (var mensaje in mensajes)
        {
            var ultimo = await UltimoEventoAsync(mensaje, ct);
            if (ultimo?.Kind is not (DebtorMessageEventKind.Queued or DebtorMessageEventKind.Deferred)) continue;
            await RegistrarAsync(mensaje, new EventoDeMensaje(DebtorMessageEventKind.Cancelled, reloj.UtcNow, actorKind, actorUserId)
            {
                CodigoDeMotivo = MotivoCausaReversada,
                TextoDelMotivo = Recortar(motivo.Trim(), LargoDelTexto),
            }, ct);
            canceladas++;
        }
        return canceladas;
    }

    /// <summary>¿Hay una reclamación al seguro en trámite por el deudor fallecido (o por el crédito)? (T0886)</summary>
    private Task<bool> ReclamacionEnTramiteAsync(int deudorId, int? loanId, CancellationToken ct) =>
        DebtorEvents.ReclamacionesEnTramite.HayAsync(db, deudorId, loanId, ct);

    /// <summary>¿Alguna de las personas está en negociación de insolvencia a <paramref name="dia"/>? (T0886)</summary>
    private Task<bool> InsolvenciaEnNegociacionAsync(int[] personas, DateOnly dia, CancellationToken ct) =>
        db.InsolvencyProceedings.AsNoTracking().AnyAsync(p => !p.IsDeleted && personas.Contains(p.PersonId)
                                                              && p.Status == InsolvencyProceedingStatus.Negotiating && p.StartDate <= dia, ct);

    /// <summary>El motivo con que un evento del deudor (fallecimiento o insolvencia, K3) cancela sus mensajes de cobro.</summary>
    public const string MotivoEventoDelDeudor = "DebtorEvent";

    /// <summary>
    /// Un evento del deudor que suspende el cobro (feature 013, K3, T0885; FR-097, FR-098): los mensajes de cobro, de aviso previo y de gasto de
    /// cobranza que todavía no salieron (<c>Queued</c> o <c>Deferred</c>) de los créditos de <paramref name="loanIds"/> —a cualquier destinatario— o
    /// dirigidos al deudor se cancelan en la unidad de trabajo del evento, sin guardar. Los informativos y la entrega de documentos siguen. Devuelve
    /// cuántos canceló.
    /// </summary>
    public async Task<int> CancelarCobrosDelDeudorAsync(int deudorId, IReadOnlyCollection<int> loanIds, string motivo, ActorKind actorKind,
        int? actorUserId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        var creditos = loanIds.Distinct().ToArray();
        DebtorMessagePurpose[] decobro = [DebtorMessagePurpose.Collection, DebtorMessagePurpose.PriorNotice, DebtorMessagePurpose.CollectionFeeNotice];
        var guardados = await db.DebtorMessages
            .Where(m => decobro.Contains(m.Purpose) && (m.PersonId == deudorId || (m.LoanId != null && creditos.Contains(m.LoanId.Value))))
            .ToListAsync(ct);
        var mensajes = guardados
            .Concat(db.DebtorMessages.Local.Where(m => decobro.Contains(m.Purpose)
                                                       && (m.PersonId == deudorId || (m.LoanId is { } l && creditos.Contains(l)))))
            .Distinct()
            .ToList();
        var canceladas = 0;
        foreach (var mensaje in mensajes)
        {
            var ultimo = await UltimoEventoAsync(mensaje, ct);
            if (ultimo?.Kind is not (DebtorMessageEventKind.Queued or DebtorMessageEventKind.Deferred)) continue;
            await RegistrarAsync(mensaje, new EventoDeMensaje(DebtorMessageEventKind.Cancelled, reloj.UtcNow, actorKind, actorUserId)
            {
                CodigoDeMotivo = MotivoEventoDelDeudor,
                TextoDelMotivo = Recortar(motivo.Trim(), LargoDelTexto),
            }, ct);
            canceladas++;
        }
        return canceladas;
    }

    /// <summary>
    /// Agrega el paso siguiente de la historia de <paramref name="mensaje"/> (sin guardar), con las transiciones del dominio. Lo llaman los
    /// comandos que registran un intento, una entrega o una cancelación: así la cola sigue siendo el único escritor.
    /// </summary>
    public async Task<DebtorMessageEvent> RegistrarAsync(DebtorMessage mensaje, EventoDeMensaje evento, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        ArgumentNullException.ThrowIfNull(evento);
        var anterior = await UltimoEventoAsync(mensaje, ct);
        var nuevo = DebtorMessageEvent.Registrar(mensaje, anterior, evento with
        {
            TextoDelMotivo = Recortar(evento.TextoDelMotivo, LargoDelTexto),
        }, EnProduccion);
        db.DebtorMessageEvents.Add(nuevo);
        return nuevo;
    }

    /// <summary>El último evento del mensaje, guardado o agregado en esta unidad de trabajo.</summary>
    public async Task<DebtorMessageEvent?> UltimoEventoAsync(DebtorMessage mensaje, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var locales = db.DebtorMessageEvents.Local
            .Where(e => ReferenceEquals(e.DebtorMessage, mensaje) || (mensaje.Id > 0 && e.DebtorMessageId == mensaje.Id))
            .ToList();
        if (mensaje.Id > 0)
        {
            var guardado = await db.DebtorMessageEvents.Where(e => e.DebtorMessageId == mensaje.Id)
                .OrderByDescending(e => e.Sequence).FirstOrDefaultAsync(ct);
            if (guardado is not null) locales.Add(guardado);
        }
        return locales.OrderByDescending(e => e.Sequence).FirstOrDefault();
    }

    // ------------------------------------------------------------------------------------------------------------ ventana --

    /// <summary>
    /// La decisión de la ventana legal para una consulta, sin escribir nada (la usan el encolado, el despacho y <c>contact-window</c>). Un
    /// límite legal sin replicar responde <c>NationalValues.Missing</c>: sin techo no se contacta (T40).
    /// </summary>
    public Task<Result<VentanaDeContacto>> EvaluarAsync(ConsultaDeVentana consulta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return EvaluarConAsync(consulta, (solicitud, reglas, historial, titular) => VentanaLegalDeContacto.Evaluar(solicitud, reglas, historial, titular), ct);
    }

    /// <summary>
    /// K4 (T0945; §4.8; Ley 2300): la decisión de la ventana para una <b>gestión</b> que ya ocurrió en <paramref name="ocurrioUtc"/>, juzgada en ese
    /// instante. Si la gestión fue por un canal de mensaje (correo, SMS, WhatsApp, carta), es la del mensaje de cobranza inmediato por ese canal; si
    /// fue una llamada, una visita o una atención en persona, la de <see cref="VentanaLegalDeContacto.EvaluarGestion"/> (horario y, con contacto
    /// directo, el tope del día y el canal de la semana). Sin escribir nada.
    /// </summary>
    public Task<Result<VentanaDeContacto>> EvaluarGestionAsync(int personId, int? loanId, CollectionActionChannel canal, bool contactoDirecto,
        DateTime ocurrioUtc, CancellationToken ct)
    {
        if (CanalDeMensaje(canal) is { } mensaje)
            return EvaluarAsync(new ConsultaDeVentana(personId, loanId, mensaje, DebtorMessagePurpose.Collection, ocurrioUtc, true) { EnElInstantePedido = true },
                ct);
        return EvaluarConAsync(new ConsultaDeVentana(personId, loanId, MessageChannel.Letter, DebtorMessagePurpose.Collection, ocurrioUtc, true)
        {
            EnElInstantePedido = true,
        }, (solicitud, reglas, historial, titular) => VentanaLegalDeContacto.EvaluarGestion(solicitud.Pedido, canal, contactoDirecto, reglas, historial, titular), ct);
    }

    private async Task<Result<VentanaDeContacto>> EvaluarConAsync(ConsultaDeVentana consulta,
        Func<SolicitudDeContacto, ReglasDeContacto, HistorialDeContactos, EstadoDelTitular, DecisionDeContacto> decidir, CancellationToken ct)
    {
        var desfase = reloj.AhoraLocal.Offset;
        var pedidoLocal = ALocal(DateTime.SpecifyKind(consulta.PedidoUtc, DateTimeKind.Utc), desfase);
        var ahoraLocal = consulta.EnElInstantePedido ? pedidoLocal : ALocal(reloj.UtcNow, desfase);
        var dia = DateOnly.FromDateTime(pedidoLocal > ahoraLocal ? pedidoLocal : ahoraLocal);
        var lunes = InicioDeSemana(dia);

        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == consulta.PersonId, ct);
        if (persona is null)
            return Result.Success(new VentanaDeContacto(new Excluido(ContactWindowReason.RecipientNotObligated), false, ContactRole.Debtor,
                string.Empty, null, null, lunes, desfase));
        var nombre = ReferenciasDeGobierno.Nombre(persona);

        var (obligado, rol, deudorId) = consulta.PersonaSimulada && consulta.LoanId is null
            ? (true, ContactRole.Debtor, (int?)consulta.PersonId)
            : await ObligacionAsync(consulta.PersonId, consulta.LoanId, consulta.Purpose, reloj.HoyLocal, ct);
        var deudor = deudorId is { } idDelDeudor && idDelDeudor != persona.Id
            ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == idDelDeudor, ct)
            : persona;

        var general = await autorizaciones.VigenteAsync(persona.PublicId, DataAuthorizationPurpose.GeneralTreatment, null, ct);
        var deCanal = consulta.Channel is MessageChannel.Email or MessageChannel.Sms or MessageChannel.WhatsApp
            ? await autorizaciones.VigenteAsync(persona.PublicId, DataAuthorizationPurpose.ContactChannel, consulta.Channel.ToString(), ct)
            : null;

        var titular = new EstadoDelTitular(obligado, rol)
        {
            Autorizaciones = deCanal is { Autoriza: true } ? [new AutorizacionDeCanal(consulta.Channel, deCanal.HasEvidence)] : [],
            AutorizacionGeneral = general is { Autoriza: true },
            AutorizacionGeneralConEvidencia = general is { Autoriza: true, HasEvidence: true },
            Destinos = Enum.GetValues<MessageChannel>().Where(c => DestinoDe(persona, c) is not null).ToList(),
            // La marca de fallecido del deudor o una reclamación al seguro en trámite (migrada o registrada en K3, T0886): ante la duda, lo más estricto.
            DeudorFallecidoConReclamacion = deudor?.IsDeceased == true || await ReclamacionEnTramiteAsync(deudor?.Id ?? persona.Id, consulta.LoanId, ct),
            EnInsolvencia = persona.IsInsolvent || await InsolvenciaEnNegociacionAsync([persona.Id, deudor?.Id ?? persona.Id], dia, ct),
        };

        var reglas = await ReglasAsync(dia, ct);
        if (reglas.IsFailure) return Result.Failure<VentanaDeContacto>(reglas.Error);
        var historial = await HistorialAsync(consulta.PersonId, lunes, dia, desfase, consulta.SinElMensaje, ct);

        var decision = decidir(new SolicitudDeContacto(pedidoLocal, consulta.Channel, consulta.Purpose, consulta.Inmediato) { Ahora = ahoraLocal },
            reglas.Value, historial, titular);
        var canalDeLaSemana = historial.Contactos
            .Where(c => c.FijaCanalSemanal && InicioDeSemana(DateOnly.FromDateTime(c.En)) == lunes)
            .Select(c => CanalDeMensaje(c.Canal))
            .FirstOrDefault(c => c is not null && c != consulta.Channel);
        return Result.Success(new VentanaDeContacto(decision, obligado, rol, nombre,
            deCanal is { Autoriza: true } ? deCanal.ConsentPublicId : null, canalDeLaSemana, lunes, desfase));
    }

    /// <summary>La ventana del mensaje al despacharlo, ahora (§4.5: se evalúa al programar y otra vez al despachar).</summary>
    public Task<Result<VentanaDeContacto>> EvaluarParaDespachoAsync(DebtorMessage mensaje, CancellationToken ct) =>
        EvaluarAsync(new ConsultaDeVentana(mensaje.PersonId, mensaje.LoanId, mensaje.Channel, mensaje.Purpose, reloj.UtcNow, false)
        {
            SinElMensaje = mensaje.Id,
            PersonaSimulada = EsDeSimulacion(mensaje.OriginKind, mensaje.LoanId, mensaje.Purpose),
        }, ct);

    /// <summary>El primer instante permitido (UTC) desde <paramref name="desdeUtc"/>, o nulo si no hay ventana o el mensaje quedó excluido.</summary>
    public async Task<DateTime?> SiguienteVentanaAsync(DebtorMessage mensaje, DateTime desdeUtc, CancellationToken ct)
    {
        var r = await EvaluarAsync(new ConsultaDeVentana(mensaje.PersonId, mensaje.LoanId, mensaje.Channel, mensaje.Purpose, desdeUtc, false)
        {
            SinElMensaje = mensaje.Id,
            PersonaSimulada = EsDeSimulacion(mensaje.OriginKind, mensaje.LoanId, mensaje.Purpose),
        }, ct);
        return r.IsSuccess && r.Value.PermitidoDesde is { } desde ? desde.UtcDateTime : null;
    }

    // ----------------------------------------------------------------------------------------------------------- despacho --

    /// <summary>¿Cartera está activada en esta cooperativa? Las tareas salen si no (DT §2.13).</summary>
    public Task<bool> CarteraActivadaAsync(CancellationToken ct) =>
        db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct);

    /// <summary>
    /// Hasta <paramref name="maximo"/> mensajes vencidos (§4.6 paso 1): su último evento es <c>Queued</c> o <c>Deferred</c> con el instante de
    /// salida cumplido, en orden de <c>RequestedFor</c> (programado o pedido).
    /// </summary>
    public async Task<IReadOnlyList<int>> VencidosAsync(int maximo, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        var vencidos = from e in db.DebtorMessageEvents.AsNoTracking()
                       where (e.Kind == DebtorMessageEventKind.Queued || e.Kind == DebtorMessageEventKind.Deferred)
                             && e.DueAt != null && e.DueAt <= ahora
                             && !db.DebtorMessageEvents.Any(x => x.DebtorMessageId == e.DebtorMessageId && x.Sequence > e.Sequence)
                       join m in db.DebtorMessages.AsNoTracking() on e.DebtorMessageId equals m.Id
                       orderby m.ScheduledFor ?? m.RequestedAt, m.Id
                       select m.Id;
        return await vencidos.Take(maximo).ToListAsync(ct);
    }

    /// <summary>
    /// Los mensajes enviados de las últimas <paramref name="horas"/> horas que esperan su desenlace (§4.6, <c>cartera.entregas</c>): último
    /// evento <c>Sent</c> —o <c>Simulated</c> fuera de producción— con referencia del proveedor, y canal que consulta la entrega.
    /// </summary>
    public async Task<IReadOnlyList<int>> PorConfirmarEntregaAsync(int horas, int maximo, CancellationToken ct)
    {
        var desde = reloj.UtcNow.AddHours(-horas);
        var conSimulados = !EnProduccion;
        var candidatos = await (from e in db.DebtorMessageEvents.AsNoTracking()
                                where (e.Kind == DebtorMessageEventKind.Sent || (conSimulados && e.Kind == DebtorMessageEventKind.Simulated))
                                      && e.OccurredAt >= desde && e.ProviderReference != null
                                      && !db.DebtorMessageEvents.Any(x => x.DebtorMessageId == e.DebtorMessageId && x.Sequence > e.Sequence)
                                join m in db.DebtorMessages.AsNoTracking() on e.DebtorMessageId equals m.Id
                                orderby e.OccurredAt, m.Id
                                select new { m.Id, m.ChannelCode })
            .Take(maximo)
            .ToListAsync(ct);
        return candidatos.Where(c => canales.Resolver(c.ChannelCode).Capacidades.ConsultaEntrega).Select(c => c.Id).ToList();
    }

    /// <summary>El número del intento siguiente del mensaje (cada paso del despacho lleva el suyo).</summary>
    public async Task<int> SiguienteIntentoAsync(int mensajeId, CancellationToken ct) =>
        (await db.DebtorMessageEvents.AsNoTracking().Where(e => e.DebtorMessageId == mensajeId).MaxAsync(e => e.AttemptNumber, ct) ?? 0) + 1;

    /// <summary>
    /// ¿El crédito se puso al día? (§4.6 paso 2): cancelado, o la última posición diaria sin días ni cuotas ni capital vencidos. Sin
    /// posición no se sabe: no se cancela.
    /// </summary>
    public async Task<bool> CreditoAlDiaAsync(int loanId, CancellationToken ct)
    {
        var estado = await db.Loans.AsNoTracking().Where(l => l.Id == loanId).Select(l => (LoanStatus?)l.Status).FirstOrDefaultAsync(ct);
        if (estado is null) return false;
        if (estado == LoanStatus.PaidOff) return true;
        var posicion = await db.LoanDailyPositions.AsNoTracking().Where(p => p.LoanId == loanId)
            .OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version)
            .Select(p => new { p.DaysPastDue, p.InstallmentsPastDue, p.PrincipalPastDue })
            .FirstOrDefaultAsync(ct);
        return posicion is { DaysPastDue: 0, InstallmentsPastDue: 0 } && posicion.PrincipalPastDue == decimal.Zero;
    }

    /// <summary>El destino del canal en el maestro de personas, leído al despachar (C57): correo, celular o dirección; nulo si no hay.</summary>
    public static string? DestinoDe(Person persona, MessageChannel canal)
    {
        ArgumentNullException.ThrowIfNull(persona);
        var destino = canal switch
        {
            MessageChannel.Email => persona.Email,
            MessageChannel.Sms or MessageChannel.WhatsApp => persona.Mobile,
            MessageChannel.Letter => string.IsNullOrWhiteSpace(persona.MailingAddress) ? persona.Address : persona.MailingAddress,
            _ => null,
        };
        return string.IsNullOrWhiteSpace(destino) ? null : destino.Trim();
    }

    // ---------------------------------------------------------------------------------------------------------- privados --

    /// <summary>
    /// Por qué un envío manual inmediato no puede salir ahora, o nulo (lo que <see cref="EncolarAsync"/> rechaza). Feature 013, US2 (T0390): el
    /// envío de una simulación lo pregunta antes de pintar y subir el PDF, para no dejar un adjunto sin mensaje.
    /// </summary>
    public static Error? RechazoDelInmediato(VentanaDeContacto ventana, MessageChannel canal) => ventana.Decision switch
    {
        Excluido { Motivo: ContactWindowReason.ChannelNotAuthorized } => ErroresDeMensajesAlDeudor.CanalNoAutorizado(ventana.NombreDelTitular, canal),
        Excluido { Motivo: ContactWindowReason.AuthorizationWithoutEvidence } =>
            ErroresDeMensajesAlDeudor.AutorizacionSinEvidencia(ventana.NombreDelTitular, $"el contacto por {ErroresDeMensajesAlDeudor.NombreDelCanal(canal)}"),
        Diferido d when d.Motivos.Contains(ContactWindowReason.WeeklyChannelLocked) =>
            ErroresDeMensajesAlDeudor.CanalSemanalUsado(ventana.CanalDeLaSemana, ventana.InicioDeSemana, ventana.PermitidoDesde!.Value),
        Diferido d => ErroresDeMensajesAlDeudor.FueraDeVentana(ventana.PermitidoDesde!.Value, d.Motivos),
        _ => null,
    };

    private static EventoDeMensaje EventoAlNacer(VentanaDeContacto ventana, DateTime ahora, ActorKind actorKind, int? actorUserId) => ventana.Decision switch
    {
        Permitido => new EventoDeMensaje(DebtorMessageEventKind.Queued, ahora, actorKind, actorUserId)
        {
            DueAt = ventana.PermitidoDesde!.Value.UtcDateTime,
        },
        Diferido d => new EventoDeMensaje(DebtorMessageEventKind.Deferred, ahora, actorKind, actorUserId)
        {
            DueAt = ventana.PermitidoDesde!.Value.UtcDateTime,
            Motivos = d.Motivos.ToList(),
        },
        Excluido x => new EventoDeMensaje(DebtorMessageEventKind.Excluded, ahora, actorKind, actorUserId) { Motivos = [x.Motivo] },
        _ => throw new InvalidOperationException("Decisión de la ventana legal desconocida."),
    };

    /// <summary>
    /// Si la persona está obligada y con qué papel: con crédito, su deudor o un codeudor vigente (ni liberado ni extinguido); sin crédito,
    /// el titular de un documento, o el deudor o codeudor de algún crédito vivo. Devuelve también el deudor del crédito.
    /// </summary>
    private async Task<(bool Obligado, ContactRole Rol, int? DeudorId)> ObligacionAsync(int personId, int? loanId, DebtorMessagePurpose proposito,
        DateOnly hoy, CancellationToken ct)
    {
        if (loanId is { } id)
        {
            var deudorId = await db.Loans.AsNoTracking().Where(l => l.Id == id).Select(l => (int?)l.DebtorPersonId).FirstOrDefaultAsync(ct);
            if (deudorId is null) return (false, ContactRole.Debtor, null);
            if (deudorId == personId) return (true, ContactRole.Debtor, deudorId);
            var codeudor = await db.LoanParties.AsNoTracking().AnyAsync(p => p.LoanId == id && p.PersonId == personId
                && p.Role == CreditPartyRole.CoDebtor && (p.ReleasedOn == null || p.ReleasedOn > hoy)
                && (p.ExtinguishedOn == null || p.ExtinguishedOn > hoy), ct);
            return (codeudor, ContactRole.CoDebtor, deudorId);
        }

        if (proposito == DebtorMessagePurpose.DocumentDelivery) return (true, ContactRole.Debtor, personId);
        var vivos = new[] { LoanStatus.PendingDisbursement, LoanStatus.Active };
        if (await db.Loans.AsNoTracking().AnyAsync(l => l.DebtorPersonId == personId && vivos.Contains(l.Status), ct))
            return (true, ContactRole.Debtor, personId);
        var comoCodeudor = await (from p in db.LoanParties.AsNoTracking()
                                  join l in db.Loans.AsNoTracking() on p.LoanId equals l.Id
                                  where p.PersonId == personId && p.Role == CreditPartyRole.CoDebtor && p.ReleasedOn == null
                                        && p.ExtinguishedOn == null && vivos.Contains(l.Status)
                                  select (int?)l.DebtorPersonId).FirstOrDefaultAsync(ct);
        return comoCodeudor is null ? (false, ContactRole.Debtor, null) : (true, ContactRole.CoDebtor, comoCodeudor);
    }

    /// <summary>Las reglas efectivas de contacto a la fecha: lo de la cooperativa acotado por la ley, y los festivos nacionales.</summary>
    private async Task<Result<ReglasDeContacto>> ReglasAsync(DateOnly dia, CancellationToken ct)
    {
        var lunesAViernes = await parametros.LeerAsync(ParametrosDeCartera.CobranzaHorarioLunesAViernes, dia, ct: ct);
        if (lunesAViernes.IsFailure) return Result.Failure<ReglasDeContacto>(lunesAViernes.Error);
        var sabado = await parametros.LeerAsync(ParametrosDeCartera.CobranzaHorarioSabado, dia, ct: ct);
        if (sabado.IsFailure) return Result.Failure<ReglasDeContacto>(sabado.Error);
        var contactos = await parametros.LeerAsync(ParametrosDeCartera.CobranzaContactosPorDia, dia, ct: ct);
        if (contactos.IsFailure) return Result.Failure<ReglasDeContacto>(contactos.Error);

        var desde = dia.AddDays(-DiasDeHistorialHaciaAtras);
        var hasta = dia.AddDays(VentanaLegalDeContacto.DiasDeBusqueda + DiasDeHistorialHaciaAtras);
        var calendarioDelRango = await calendario.ObtenerAsync(desde, hasta, DiasDeDescanso.SoloDomingo, ct: ct);
        var noHabiles = new List<DateOnly>();
        for (var d = desde; d <= hasta; d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Sunday && !calendarioDelRango.EsHabil(d)) noHabiles.Add(d);

        var tope = int.TryParse(contactos.Value.Texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        return Result.Success(new ReglasDeContacto(
            Franja(lunesAViernes.Value, CatalogoDeLimitesLegales.CobranzaHoraInicioLunesAViernes, CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes),
            Franja(sabado.Value, CatalogoDeLimitesLegales.CobranzaHoraInicioSabado, CatalogoDeLimitesLegales.CobranzaHoraFinSabado),
            Math.Max(tope, 0), noHabiles));
    }

    /// <summary>La franja <c>HH:mm-HH:mm</c> que rige; sin texto propio, la de los límites legales; si no hay nada, ninguna (no se contacta).</summary>
    private static FranjaHoraria? Franja(ParametroDeCartera parametro, string codigoDeInicio, string codigoDeFin)
    {
        var partes = parametro.Texto.Split('-', StringSplitOptions.TrimEntries);
        if (partes.Length == 2
            && TimeOnly.TryParseExact(partes[0], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var inicio)
            && TimeOnly.TryParseExact(partes[1], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fin)
            && fin > inicio)
            return new FranjaHoraria(inicio, fin);

        var desde = parametro.Limites.FirstOrDefault(l => l.Codigo == codigoDeInicio);
        var hasta = parametro.Limites.FirstOrDefault(l => l.Codigo == codigoDeFin);
        if (desde is null || hasta is null || hasta.Valor <= desde.Valor) return null;
        return new FranjaHoraria(Hora(desde.Valor), Hora(hasta.Valor));
    }

    private static TimeOnly Hora(decimal horas)
    {
        var minutos = decimal.ToInt32(decimal.Round(horas * TimeSpan.MinutesPerHour, MidpointRounding.AwayFromZero));
        return new TimeOnly(Math.Clamp(minutos / (int)TimeSpan.MinutesPerHour, 0, 23), minutos % (int)TimeSpan.MinutesPerHour);
    }

    /// <summary>
    /// Los contactos del titular que cuentan para la ventana (§4.5): cada mensaje una vez, por el primer evento que dice que salió —<c>Sent</c>,
    /// <c>Delivered</c> y, fuera de producción, <c>Simulated</c>—, desde el lunes de la semana pedida hasta los días que el motor busca. K4
    /// suma las gestiones con contacto directo que nadie anuló (T0938, <c>HistorialDeContactos.Componer</c>).
    /// </summary>
    private async Task<HistorialDeContactos> HistorialAsync(int personId, DateOnly lunes, DateOnly dia, TimeSpan desfase, int? sinElMensaje,
        CancellationToken ct)
    {
        var desdeUtc = AUtc(lunes.ToDateTime(TimeOnly.MinValue), desfase);
        var hastaUtc = AUtc(dia.AddDays(VentanaLegalDeContacto.DiasDeBusqueda + 1).ToDateTime(TimeOnly.MinValue), desfase);
        var salieron = EnProduccion
            ? new[] { DebtorMessageEventKind.Sent, DebtorMessageEventKind.Delivered }
            : new[] { DebtorMessageEventKind.Sent, DebtorMessageEventKind.Delivered, DebtorMessageEventKind.Simulated };
        var filas = await (from e in db.DebtorMessageEvents.AsNoTracking()
                           join m in db.DebtorMessages.AsNoTracking() on e.DebtorMessageId equals m.Id
                           where m.PersonId == personId && salieron.Contains(e.Kind) && e.OccurredAt >= desdeUtc && e.OccurredAt < hastaUtc
                                 && (sinElMensaje == null || m.Id != sinElMensaje)
                           select new { m.Id, e.OccurredAt, m.Channel, m.Purpose })
            .ToListAsync(ct);
        var contactos = filas
            .GroupBy(f => f.Id)
            .Select(g => g.OrderBy(f => f.OccurredAt).First())
            .OrderBy(f => f.OccurredAt)
            .Select(f => ContactoRegistrado.DeMensaje(ALocal(DateTime.SpecifyKind(f.OccurredAt, DateTimeKind.Utc), desfase), f.Channel, f.Purpose))
            .ToList();

        // K4 (T0938, §4.8): las gestiones del titular en la ventana —las directas y las que anulan otra, para descontar las anuladas—.
        var gestiones = await db.CollectionActions.AsNoTracking()
            .Where(a => a.PersonId == personId && a.OccurredAt >= desdeUtc && a.OccurredAt < hastaUtc
                        && (a.IsDirectContact || a.VoidsActionId != null))
            .Select(a => new { a.Id, a.OccurredAt, a.Channel, a.IsDirectContact, a.VoidsActionId })
            .ToListAsync(ct);
        // Una corrección es posterior a lo que corrige y la ventana llega hasta 15 días adelante: las que anulan algo de la ventana están en ella.
        var paraHistorial = gestiones
            .Select(a => new GestionParaHistorial(a.Id, ALocal(DateTime.SpecifyKind(a.OccurredAt, DateTimeKind.Utc), desfase), a.Channel,
                a.IsDirectContact, a.VoidsActionId))
            .ToList();
        return HistorialDeContactos.Componer(contactos, paraHistorial);
    }

    /// <summary>¿Hay plantilla vigente de ese código, canal y propósito? (K4: el envío de cobranza prueba canales.)</summary>
    public async Task<bool> HayPlantillaAsync(string templateCode, MessageChannel canal, DebtorMessagePurpose proposito, CancellationToken ct) =>
        await PlantillaVigenteAsync(templateCode, canal, reloj.HoyLocal, ct) is { } t && string.Equals(t.Purpose, proposito.ToString(), StringComparison.Ordinal);

    /// <summary>El código del canal sellado en la carta de aviso previo registrada a mano con su guía (K4, T0950): no es simulado.</summary>
    public const string CanalDeCartaManual = "CARTA-MANUAL";

    /// <summary>
    /// K4 (T0950; data-model §26.11; adaptadores-y-archivos.md §5.3): la carta de un aviso previo enviada por correo físico, registrada a mano con la
    /// guía adjunta. Nace como mensaje <c>Letter</c> de propósito <c>PriorNotice</c>, sin plantilla, con el canal <see cref="CanalDeCartaManual"/>, y su
    /// historia es <c>Queued</c> y <c>Sent</c> en el instante del envío, con la guía (<c>LendingPriorNoticeEvidence</c>) como artefacto y la
    /// transportadora y el número de guía como referencia. La cola sigue siendo el único escritor; no guarda.
    /// </summary>
    public DebtorMessage RegistrarCartaDeAviso(PriorNotice aviso, ContactRole rol, int comunicacion, DateTime enviadaUtc, string transportadora, string guia,
        Guid adjunto, int usuarioId, string direccion)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        var cuerpo = $"Carta de aviso previo al reporte negativo (comunicación {comunicacion}) enviada por {transportadora.Trim()}, guía {guia.Trim()}.";
        var mensaje = DebtorMessage.Nacer(new BorradorDeMensajeAlDeudor(aviso.PersonId, rol, aviso.LoanId, DebtorMessagePurpose.PriorNotice,
            MessageChannel.Letter, CanalDeCartaManual, cuerpo,
            string.Create(CultureInfo.InvariantCulture, $"PriorNotice:{aviso.PublicId:N}:carta-{comunicacion}:{DebtorMessagePurpose.PriorNotice}"),
            "PriorNotice", reloj.UtcNow, ActorKind.Person, usuarioId)
        {
            OriginPublicId = aviso.PublicId,
            PriorNoticeId = aviso.Id,
            AttachmentPublicId = adjunto,
        });
        var enviada = DateTime.SpecifyKind(enviadaUtc, DateTimeKind.Utc);
        var encolado = DebtorMessageEvent.Registrar(mensaje, null,
            new EventoDeMensaje(DebtorMessageEventKind.Queued, enviada, ActorKind.Person, usuarioId) { DueAt = enviada }, EnProduccion);
        var salio = DebtorMessageEvent.Registrar(mensaje, encolado, new EventoDeMensaje(DebtorMessageEventKind.Sent, enviada, ActorKind.Person, usuarioId)
        {
            DestinationAddress = Recortar(direccion, 320),
            AttemptNumber = 1,
            ProviderReference = Recortar($"{transportadora.Trim()} {guia.Trim()}", 120),
            ProviderArtifactAttachmentPublicId = adjunto,
        }, EnProduccion);
        db.DebtorMessages.Add(mensaje);
        db.DebtorMessageEvents.Add(encolado);
        db.DebtorMessageEvents.Add(salio);
        return mensaje;
    }

    private async Task<NotificationTemplate?> PlantillaVigenteAsync(string templateCode, MessageChannel canal, DateOnly hoy, CancellationToken ct)
    {
        var code = Codigo(templateCode);
        var nombreDelCanal = canal.ToString();
        var versiones = await db.NotificationTemplates.AsNoTracking()
            .Where(t => t.Module == Modulo && t.Code == code && t.Channel == nombreDelCanal && t.IsActive && !t.IsDeleted)
            .ToListAsync(ct);
        return versiones.Where(t => t.VigenteEn(hoy)).OrderByDescending(t => t.Version).FirstOrDefault();
    }

    /// <summary>Las variables comunes (§4.7): el titular, la cooperativa, su teléfono, el número del crédito y la fecha de corte.</summary>
    private async Task<Dictionary<string, string?>> ValoresComunesAsync(string titular, int? loanId, DateOnly hoy, CancellationToken ct)
    {
        var cooperativa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id)
            .Select(c => new { c.Name, c.ShortName, c.Phone }).FirstOrDefaultAsync(ct);
        var numero = loanId is { } id ? await db.Loans.AsNoTracking().Where(l => l.Id == id).Select(l => l.Number).FirstOrDefaultAsync(ct) : null;
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NombreDelTitular"] = titular,
            ["NombreDeLaCooperativa"] = cooperativa is null ? null : string.IsNullOrWhiteSpace(cooperativa.ShortName) ? cooperativa.Name : cooperativa.ShortName,
            ["TelefonoDeLaCooperativa"] = cooperativa?.Phone,
            ["NumeroDelCredito"] = numero,
            ["FechaDeCorte"] = hoy.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// El adaptador que se sella (§3.3): el correo de la plataforma; para SMS y WhatsApp, el canal que la cooperativa tiene configurado hoy o,
    /// sin configuración, <c>SIMULADO</c> (la guardia le quita el efecto en producción). La carta no sale por un adaptador en K1: también
    /// <c>SIMULADO</c>, con constancia manual.
    /// </summary>
    private async Task<string> CodigoDeCanalAsync(MessageChannel canal, DateOnly hoy, CancellationToken ct)
    {
        if (canal == MessageChannel.Email) return CodigosDeCanalExterno.Correo;
        ExternalChannelKind? clase = canal switch
        {
            MessageChannel.Sms => ExternalChannelKind.Sms,
            MessageChannel.WhatsApp => ExternalChannelKind.WhatsApp,
            _ => null,
        };
        if (clase is null) return CodigosDeCanalExterno.Simulado;
        var configurados = await db.ExternalChannelSettings.AsNoTracking().Where(s => s.Kind == clase && !s.IsDeleted).ToListAsync(ct);
        return configurados.Where(s => s.VigenteEn(hoy)).OrderByDescending(s => s.ValidFrom).Select(s => s.ChannelCode).FirstOrDefault()
               ?? CodigosDeCanalExterno.Simulado;
    }

    /// <summary>El canal de mensaje de un canal de gestión, o nulo si la gestión no es un mensaje (llamada, visita, en persona).</summary>
    public static MessageChannel? CanalDeMensaje(CollectionActionChannel canal) => canal switch
    {
        CollectionActionChannel.Email => MessageChannel.Email,
        CollectionActionChannel.Sms => MessageChannel.Sms,
        CollectionActionChannel.WhatsApp => MessageChannel.WhatsApp,
        CollectionActionChannel.Letter => MessageChannel.Letter,
        _ => null,
    };

    private static string Codigo(string templateCode) =>
        CodigoDeCatalogo.Normalizar(templateCode) ?? templateCode.Trim().ToUpperInvariant();

    private static DateOnly InicioDeSemana(DateOnly dia) => dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));

    private static DateTime ALocal(DateTime utc, TimeSpan desfase) => DateTime.SpecifyKind(utc + desfase, DateTimeKind.Unspecified);

    private static DateTime AUtc(DateTime local, TimeSpan desfase) => DateTime.SpecifyKind(local - desfase, DateTimeKind.Utc);

    private static string? Recortar(string? texto, int largo) =>
        texto is null ? null : texto.Length <= largo ? texto : texto[..largo];
}
