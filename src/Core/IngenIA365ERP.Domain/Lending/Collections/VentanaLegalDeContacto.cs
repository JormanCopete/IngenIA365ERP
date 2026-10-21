using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Collections;

/// <summary>
/// Una franja horaria de contacto: desde <see cref="Desde"/> (incluida) hasta <see cref="Hasta"/> (excluida). Con el final excluido,
/// «de 7:00 a 19:00» no contacta a las 19:00 en punto: ante la duda, lo más estricto. (nuevo; nombre de T0304)
/// </summary>
public sealed record FranjaHoraria
{
    public FranjaHoraria(TimeOnly desde, TimeOnly hasta)
    {
        if (hasta <= desde)
            throw new ArgumentException($"La franja {desde:HH\\:mm}–{hasta:HH\\:mm} está vacía: el final debe ser posterior al inicio.");
        Desde = desde;
        Hasta = hasta;
    }

    public TimeOnly Desde { get; }
    public TimeOnly Hasta { get; }

    public bool Contiene(TimeOnly hora) => hora >= Desde && hora < Hasta;

    /// <summary>La intersección; nula si no se cruzan.</summary>
    public static FranjaHoraria? Interseccion(FranjaHoraria? una, FranjaHoraria? otra)
    {
        if (una is null || otra is null) return null;
        var desde = una.Desde > otra.Desde ? una.Desde : otra.Desde;
        var hasta = una.Hasta < otra.Hasta ? una.Hasta : otra.Hasta;
        return hasta > desde ? new FranjaHoraria(desde, hasta) : null;
    }
}

/// <summary>
/// Las reglas de contacto efectivas (T0304, nombre fijado en <c>adaptadores-y-archivos.md</c> §4.5): la franja de lunes a viernes, la
/// del sábado (nula = no se contacta ese día), los contactos por día y los días no hábiles <b>nacionales</b> (los festivos «Manual» de
/// Nómina no cuentan, E5). Los domingos los sabe el motor. (nuevo)
/// </summary>
public sealed record ReglasDeContacto
{
    public ReglasDeContacto(FranjaHoraria? lunesAViernes, FranjaHoraria? sabado, int contactosPorDia, IReadOnlyCollection<DateOnly> noHabiles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(contactosPorDia);
        ArgumentNullException.ThrowIfNull(noHabiles);
        LunesAViernes = lunesAViernes;
        Sabado = sabado;
        ContactosPorDia = contactosPorDia;
        NoHabiles = noHabiles;
    }

    public FranjaHoraria? LunesAViernes { get; }
    public FranjaHoraria? Sabado { get; }
    public int ContactosPorDia { get; }
    public IReadOnlyCollection<DateOnly> NoHabiles { get; }

    /// <summary>
    /// El techo nacional ∩ la restricción de la cooperativa: la franja más estrecha de cada día, el menor tope y la unión de los no
    /// hábiles. La cooperativa sólo restringe; si trae algo más amplio (el escritor de parámetros lo rechaza, T42), manda el techo.
    /// </summary>
    public static ReglasDeContacto Combinar(ReglasDeContacto techo, ReglasDeContacto cooperativa)
    {
        ArgumentNullException.ThrowIfNull(techo);
        ArgumentNullException.ThrowIfNull(cooperativa);
        return new ReglasDeContacto(
            FranjaHoraria.Interseccion(techo.LunesAViernes, cooperativa.LunesAViernes),
            FranjaHoraria.Interseccion(techo.Sabado, cooperativa.Sabado),
            Math.Min(techo.ContactosPorDia, cooperativa.ContactosPorDia),
            techo.NoHabiles.Union(cooperativa.NoHabiles).Order().ToList());
    }

    public FranjaHoraria? FranjaDe(DateOnly dia) => dia.DayOfWeek switch
    {
        DayOfWeek.Sunday => null,
        DayOfWeek.Saturday => Sabado,
        _ => LunesAViernes,
    };
}

/// <summary>Un contacto de la semana del titular: cuándo, por qué canal y si fija el canal semanal. (nuevo)</summary>
public sealed record ContactoRegistrado(DateTime En, CollectionActionChannel Canal, bool FijaCanalSemanal)
{
    /// <summary>
    /// Un mensaje que salió (<c>Sent</c>, <c>Delivered</c> y, fuera de producción, <c>Simulated</c>). Un mensaje de cobranza fija el canal
    /// de la semana; un informativo o una entrega de documento no (I2).
    /// </summary>
    public static ContactoRegistrado DeMensaje(DateTime en, MessageChannel canal, DebtorMessagePurpose proposito) =>
        new(en, CanalDeGestion(canal), VentanaLegalDeContacto.EsDeCobranza(proposito));

    /// <summary>Una gestión del gestor (K4, §4.8): fija el canal sólo si hubo contacto directo.</summary>
    public static ContactoRegistrado DeGestion(DateTime en, CollectionActionChannel canal, bool contactoDirecto) =>
        new(en, canal, contactoDirecto);

    internal static CollectionActionChannel CanalDeGestion(MessageChannel canal) => canal switch
    {
        MessageChannel.Email => CollectionActionChannel.Email,
        MessageChannel.Sms => CollectionActionChannel.Sms,
        MessageChannel.WhatsApp => CollectionActionChannel.WhatsApp,
        MessageChannel.Letter => CollectionActionChannel.Letter,
        _ => throw new ArgumentOutOfRangeException(nameof(canal), canal, "Canal de mensaje desconocido."),
    };
}

/// <summary>
/// Los contactos que cuentan para la ventana: los mensajes que salieron y, desde K4, las gestiones con contacto directo (las anuladas
/// no; <see cref="Componer"/>). Quien llama trae al menos la semana calendario del día pedido y la de los 14 siguientes. (nuevo)
/// </summary>
public sealed record HistorialDeContactos(IReadOnlyList<ContactoRegistrado> Contactos)
{
    public static HistorialDeContactos Vacio { get; } = new([]);

    /// <summary>
    /// K4 (T0938; <c>adaptadores-y-archivos.md</c> §4.8): los mensajes que salieron más las gestiones con <b>contacto directo</b> que nadie
    /// anuló. Una gestión sin contacto directo (no contestó, número errado) no es un contacto para la ley; una anulada deja de contar, y la
    /// que la anula cuenta por sí misma si fue contacto directo. Quedan en orden de instante.
    /// </summary>
    public static HistorialDeContactos Componer(IEnumerable<ContactoRegistrado> mensajes, IEnumerable<GestionParaHistorial> gestiones)
    {
        ArgumentNullException.ThrowIfNull(mensajes);
        ArgumentNullException.ThrowIfNull(gestiones);
        var todas = gestiones.ToList();
        var anuladas = todas.Where(g => g.AnulaA is not null).Select(g => g.AnulaA!.Value).ToHashSet();
        var directas = todas.Where(g => g.ContactoDirecto && !anuladas.Contains(g.Id))
            .Select(g => ContactoRegistrado.DeGestion(g.En, g.Canal, contactoDirecto: true));
        return new HistorialDeContactos(mensajes.Concat(directas).OrderBy(c => c.En).ToList());
    }
}

/// <summary>
/// Una gestión de cobranza (<c>LND_CollectionActions</c>, K4) como la mira el historial: su <c>Id</c>, el instante local, el canal, si fue
/// contacto directo y la gestión que anula, si es una corrección. (nuevo)
/// </summary>
public sealed record GestionParaHistorial(int Id, DateTime En, CollectionActionChannel Canal, bool ContactoDirecto, int? AnulaA = null);

/// <summary>Una autorización de canal del titular y si tiene evidencia (FR-006). (nuevo)</summary>
public sealed record AutorizacionDeCanal(MessageChannel Canal, bool ConEvidencia);

/// <summary>
/// Lo que el motor necesita saber del destinatario (T0304): si está obligado (deudor o codeudor del crédito) y su papel, sus
/// autorizaciones por canal con evidencia, la de tratamiento general, los canales para los que tiene destino en el maestro, y las
/// marcas de fallecimiento con reclamación, insolvencia y cobro suspendido. Lo que no se dice, falla cerrado: sin autorizaciones ni
/// destinos no sale nada. (nuevo)
/// </summary>
public sealed record EstadoDelTitular(bool Obligado, ContactRole Rol)
{
    public IReadOnlyList<AutorizacionDeCanal> Autorizaciones { get; init; } = [];
    public bool AutorizacionGeneral { get; init; }
    public bool AutorizacionGeneralConEvidencia { get; init; }
    public IReadOnlyCollection<MessageChannel> Destinos { get; init; } = [];

    /// <summary>El deudor del crédito falleció y la reclamación al seguro está en trámite (FR-097, T102).</summary>
    public bool DeudorFallecidoConReclamacion { get; init; }

    /// <summary>Insolvencia en negociación (FR-098).</summary>
    public bool EnInsolvencia { get; init; }

    /// <summary>Cobro suspendido (FR-124).</summary>
    public bool CobroSuspendido { get; init; }
}

/// <summary>
/// La solicitud (T0304): el instante local pedido, el canal, el propósito y si es inmediata (el envío manual inmediato que no se
/// permite responde 422 en la aplicación; el programado se difiere). <see cref="Ahora"/> es el instante local de la evaluación; si no se
/// da, es el pedido. (nuevo)
/// </summary>
public sealed record SolicitudDeContacto(DateTime Pedido, MessageChannel Canal, DebtorMessagePurpose Proposito, bool Inmediato)
{
    public DateTime? Ahora { get; init; }
}

/// <summary>La decisión del motor: <see cref="Permitido"/>, <see cref="Diferido"/> o <see cref="Excluido"/> (nombres fijados en §4.5). (nuevo)</summary>
public abstract record DecisionDeContacto;

/// <summary>Sale en <paramref name="En"/>.</summary>
public sealed record Permitido(DateTime En) : DecisionDeContacto;

/// <summary>Sale en <paramref name="Siguiente"/>, el primer instante permitido; <paramref name="Motivos"/> son los del instante pedido.</summary>
public sealed record Diferido(DateTime Siguiente, IReadOnlyList<ContactWindowReason> Motivos) : DecisionDeContacto;

/// <summary>No sale.</summary>
public sealed record Excluido(ContactWindowReason Motivo) : DecisionDeContacto;

/// <summary>
/// Motor puro de la ventana legal de contacto (feature 013, T0304; T74, Ley 2300; <c>adaptadores-y-archivos.md</c> §4.5). Sin IO;
/// instantes en hora local de Colombia (T9), semana de lunes a domingo. Se evalúa al programar y otra vez al despachar. (nuevo)
///
/// <para>Las once reglas, en orden (la primera exclusión gana; después, la exención; después, el aplazamiento):</para>
/// <list type="number">
/// <item>destinatario no obligado → <c>Excluido(RecipientNotObligated)</c>;</item>
/// <item>deudor fallecido con reclamación en trámite: nada al fallecido, nada de cobranza a los codeudores → <c>Excluido(DebtorDeceased)</c>;</item>
/// <item>insolvencia en negociación o cobro suspendido: nada de cobranza → <c>Excluido(Insolvency | CollectionSuspended)</c>;</item>
/// <item>canal no autorizado (la entrega de documentos y la carta piden la autorización de tratamiento general, no la de canal) →
/// <c>Excluido(ChannelNotAuthorized)</c>;</item>
/// <item>cobranza con la autorización usada sin evidencia → <c>Excluido(AuthorizationWithoutEvidence)</c>;</item>
/// <item>sin destino para el canal en el maestro → <c>Excluido(NoDestination)</c>;</item>
/// <item>entrega de documento pedida por el titular → <c>Permitido</c> (exenta de 8 a 10);</item>
/// <item>domingo, festivo nacional o fuera de la franja → diferido (<c>Sunday</c>, <c>Holiday</c>, <c>OutsideHours</c>);</item>
/// <item>tope de contactos del día cumplido (los informativos cuentan) → diferido (<c>DailyLimitReached</c>);</item>
/// <item>cobranza con el canal de la semana fijado por otro canal → diferido (<c>WeeklyChannelLocked</c>);</item>
/// <item>ninguna → <c>Permitido(máx(pedido, ahora))</c>.</item>
/// </list>
/// <para>El aplazamiento busca, día por día, el primer instante que cumple 8, 9 y 10 a la vez en el día pedido y los
/// <see cref="DiasDeBusqueda"/> siguientes; si no lo hay, <c>Excluido(NoWindow)</c>.</para>
/// </summary>
public static class VentanaLegalDeContacto
{
    /// <summary>Hasta cuántos días después del pedido se busca la ventana (§4.5).</summary>
    public const int DiasDeBusqueda = 14;

    /// <summary>Los propósitos de cobranza (§4.3): los que se excluyen con insolvencia, piden evidencia y respetan el canal semanal.</summary>
    public static bool EsDeCobranza(DebtorMessagePurpose proposito) =>
        proposito is DebtorMessagePurpose.Collection or DebtorMessagePurpose.PriorNotice or DebtorMessagePurpose.CollectionFeeNotice;

    public static DecisionDeContacto Evaluar(
        SolicitudDeContacto solicitud, ReglasDeContacto reglas, HistorialDeContactos historial, EstadoDelTitular titular)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(reglas);
        ArgumentNullException.ThrowIfNull(historial);
        ArgumentNullException.ThrowIfNull(titular);

        var proposito = solicitud.Proposito;
        var canal = solicitud.Canal;
        var cobranza = EsDeCobranza(proposito);
        var ahora = solicitud.Ahora ?? solicitud.Pedido;
        var desde = solicitud.Pedido > ahora ? solicitud.Pedido : ahora;

        // 1. Sólo deudor o codeudor obligado.
        if (!titular.Obligado)
            return new Excluido(ContactWindowReason.RecipientNotObligated);

        // 2. Fallecido con reclamación en trámite.
        if (titular.DeudorFallecidoConReclamacion && (titular.Rol == ContactRole.Debtor || cobranza))
            return new Excluido(ContactWindowReason.DebtorDeceased);

        // 3. Insolvencia o cobro suspendido: nada de cobranza.
        if (cobranza && titular.EnInsolvencia)
            return new Excluido(ContactWindowReason.Insolvency);
        if (cobranza && titular.CobroSuspendido)
            return new Excluido(ContactWindowReason.CollectionSuspended);

        // 4. Autorización: la carta y la entrega de documentos piden la de tratamiento general; los demás, la del canal.
        var sinAutorizacionDeCanal = proposito == DebtorMessagePurpose.DocumentDelivery || canal == MessageChannel.Letter;
        var autorizacion = titular.Autorizaciones.FirstOrDefault(a => a.Canal == canal);
        if (sinAutorizacionDeCanal ? !titular.AutorizacionGeneral : autorizacion is null)
            return new Excluido(ContactWindowReason.ChannelNotAuthorized);

        // 5. Cobranza con una autorización sin evidencia.
        var conEvidencia = sinAutorizacionDeCanal ? titular.AutorizacionGeneralConEvidencia : autorizacion!.ConEvidencia;
        if (cobranza && !conEvidencia)
            return new Excluido(ContactWindowReason.AuthorizationWithoutEvidence);

        // 6. Destino en el maestro.
        if (!titular.Destinos.Contains(canal))
            return new Excluido(ContactWindowReason.NoDestination);

        // 7. Entrega de documento pedida por el titular: exenta del horario, del tope y del canal semanal.
        if (proposito == DebtorMessagePurpose.DocumentDelivery)
            return new Permitido(desde);

        // 8 a 10 en el instante pedido; si alguna no se cumple, se busca la apertura siguiente.
        var noHabiles = reglas.NoHabiles.ToHashSet();
        var canalDeGestion = ContactoRegistrado.CanalDeGestion(canal);
        var motivos = Motivos(desde, reglas, noHabiles, historial, cobranza, canalDeGestion);
        if (motivos.Count == 0)
            return new Permitido(desde);

        var siguiente = Buscar(desde, reglas, noHabiles, historial, cobranza, canalDeGestion);
        return siguiente is { } en
            ? new Diferido(en, motivos)
            : new Excluido(ContactWindowReason.NoWindow);
    }

    /// <summary>
    /// K4 (T0945; <c>adaptadores-y-archivos.md</c> §4.8; Ley 2300): la decisión para una gestión que <b>no es un mensaje</b> —una llamada, una
    /// visita o una atención en persona— en el instante en que ocurrió (local). Aplica las reglas 1 a 3 (obligado, fallecido con reclamación,
    /// insolvencia o cobro suspendido) y después el horario: domingo, festivo o fuera de la franja. Una gestión con <b>contacto directo</b>
    /// además cuenta para el tope del día y respeta el canal fijado en la semana. La atención en persona (el titular vino a la cooperativa) no
    /// tiene horario: sólo las reglas 1 a 3. No busca otra ventana: lo que ya ocurrió fuera de la ley se rechaza (<see cref="Diferido"/> con el
    /// siguiente instante permitido, para decirlo). (nuevo)
    /// </summary>
    public static DecisionDeContacto EvaluarGestion(DateTime instante, CollectionActionChannel canal, bool contactoDirecto, ReglasDeContacto reglas,
        HistorialDeContactos historial, EstadoDelTitular titular)
    {
        ArgumentNullException.ThrowIfNull(reglas);
        ArgumentNullException.ThrowIfNull(historial);
        ArgumentNullException.ThrowIfNull(titular);
        if (!Enum.IsDefined(canal)) throw new ArgumentOutOfRangeException(nameof(canal), canal, "Canal de gestión desconocido.");

        if (!titular.Obligado) return new Excluido(ContactWindowReason.RecipientNotObligated);
        if (titular.DeudorFallecidoConReclamacion) return new Excluido(ContactWindowReason.DebtorDeceased);
        if (titular.EnInsolvencia) return new Excluido(ContactWindowReason.Insolvency);
        if (titular.CobroSuspendido) return new Excluido(ContactWindowReason.CollectionSuspended);
        if (canal == CollectionActionChannel.InPerson) return new Permitido(instante);

        var noHabiles = reglas.NoHabiles.ToHashSet();
        var motivos = Motivos(instante, reglas, noHabiles, historial, cobranza: contactoDirecto, canal);
        if (!contactoDirecto) motivos.RemoveAll(m => m is ContactWindowReason.DailyLimitReached or ContactWindowReason.WeeklyChannelLocked);
        if (motivos.Count == 0) return new Permitido(instante);
        var siguiente = Buscar(instante, reglas, noHabiles, historial, contactoDirecto, canal);
        return siguiente is { } en ? new Diferido(en, motivos) : new Excluido(ContactWindowReason.NoWindow);
    }

    private static List<ContactWindowReason> Motivos(
        DateTime instante, ReglasDeContacto reglas, HashSet<DateOnly> noHabiles, HistorialDeContactos historial, bool cobranza,
        CollectionActionChannel canal)
    {
        var motivos = new List<ContactWindowReason>();
        var dia = DateOnly.FromDateTime(instante);
        var hora = TimeOnly.FromDateTime(instante);

        if (dia.DayOfWeek == DayOfWeek.Sunday) motivos.Add(ContactWindowReason.Sunday);
        else if (noHabiles.Contains(dia)) motivos.Add(ContactWindowReason.Holiday);
        else if (reglas.FranjaDe(dia) is not { } franja || !franja.Contiene(hora)) motivos.Add(ContactWindowReason.OutsideHours);

        if (TopeCumplido(dia, reglas, historial)) motivos.Add(ContactWindowReason.DailyLimitReached);
        if (cobranza && CanalSemanalBloqueado(dia, historial, canal)) motivos.Add(ContactWindowReason.WeeklyChannelLocked);
        return motivos;
    }

    /// <summary>El primer instante desde <paramref name="desde"/> que cumple 8, 9 y 10, en el día pedido y los 14 siguientes.</summary>
    private static DateTime? Buscar(
        DateTime desde, ReglasDeContacto reglas, HashSet<DateOnly> noHabiles, HistorialDeContactos historial, bool cobranza,
        CollectionActionChannel canal)
    {
        var primerDia = DateOnly.FromDateTime(desde);
        for (var i = 0; i <= DiasDeBusqueda; i++)
        {
            var dia = primerDia.AddDays(i);
            if (dia.DayOfWeek == DayOfWeek.Sunday || noHabiles.Contains(dia)) continue;
            if (reglas.FranjaDe(dia) is not { } franja) continue;

            TimeOnly hora;
            if (i == 0)
            {
                var pedida = TimeOnly.FromDateTime(desde);
                if (pedida >= franja.Hasta) continue;
                hora = pedida < franja.Desde ? franja.Desde : pedida;
            }
            else
            {
                hora = franja.Desde;
            }

            if (TopeCumplido(dia, reglas, historial)) continue;
            if (cobranza && CanalSemanalBloqueado(dia, historial, canal)) continue;
            return dia.ToDateTime(hora);
        }
        return null;
    }

    private static bool TopeCumplido(DateOnly dia, ReglasDeContacto reglas, HistorialDeContactos historial) =>
        historial.Contactos.Count(c => DateOnly.FromDateTime(c.En) == dia) >= reglas.ContactosPorDia;

    /// <summary>Hubo en la semana (lunes a domingo) del día un contacto que fija el canal, por otro canal.</summary>
    private static bool CanalSemanalBloqueado(DateOnly dia, HistorialDeContactos historial, CollectionActionChannel canal)
    {
        var lunes = dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));
        var domingo = lunes.AddDays(6);
        return historial.Contactos.Any(c =>
        {
            var fecha = DateOnly.FromDateTime(c.En);
            return c.FijaCanalSemanal && fecha >= lunes && fecha <= domingo && c.Canal != canal;
        });
    }
}
