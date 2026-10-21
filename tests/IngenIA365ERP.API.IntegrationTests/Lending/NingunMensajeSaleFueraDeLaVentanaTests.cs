using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.PagosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0934 (feature 013, K4, SC-011; <c>adaptadores-y-archivos.md</c> §4.9; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): ningún mensaje
/// sale fuera de la ventana de la Ley 2300, contando también los contactos directos de K4. La fase de mensajes de K1 no dejó esta prueba
/// (<c>MensajesAlDeudorTests</c> sólo prueba el domingo), así que nace aquí. Sobre una cooperativa aislada activada, con tres deudores en mora y el ciclo
/// diario corrido una vez (que encola los mensajes de la etapa y el aviso previo), se programan mensajes para un domingo, un festivo nacional, después
/// de las 19:00, dos para el mismo día (el tope diario), uno de cobranza después de un contacto directo por teléfono en la semana y uno por un canal que
/// el titular revoca antes de que salga. Después se despacha con <c>cartera.mensajes</c> llevando el reloj de la suite a cada vencimiento.
/// <para>La comprobación es <b>sobre el registro, nunca sobre los logs</b>: para cada mensaje que salió (<c>Sent</c>, <c>Delivered</c> o, fuera de
/// producción, <c>Simulated</c>) se reconstruye la entrada del motor con lo conocido en ese instante —los contactos anteriores (mensajes que salieron y
/// gestiones con contacto directo no anuladas), la autorización del canal vigente entonces, el destino del maestro, el horario y los festivos
/// nacionales— y se exige <see cref="Permitido"/>. El horario es el de la semilla (07:00–19:00 entre semana, 08:00–15:00 el sábado, un contacto por día),
/// que el ensayo no cambia. (nuevo)</para>
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class NingunMensajeSaleFueraDeLaVentanaTests(CentralIdentityApiFixture fx)
{
    private static readonly Dictionary<string, string> Abono = new() { ["ValorAplicado"] = "$ 50.000", ["NuevaCuota"] = "$ 40.000", ["NuevoPlazo"] = "35 meses" };

    [Fact]
    public async Task Cada_mensaje_que_salio_estaba_permitido_en_su_instante()
    {
        var esc = await EscenarioAsync(fx, "ventana");
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var tenant = esc.Coop.TenantPublicId;
        var hoy = esc.Hoy;
        using var reloj = new CobranzaE2E.Reloj(fx);
        var t = new CobranzaE2E.Tokens(http, esc.Usuarios);

        await ContabilidadDeCarteraE2E.MatrizPorPlantillaAsync(http, admin, hoy, conHuecos: false);
        await UsuariosDeCartera.FijarLineasAsync(http, admin, esc.Usuarios.Gestor, CobranzaE2E.Lineas(esc));
        await CobranzaE2E.PoliticaDeContactoAsync(http, admin);
        var (vera, _) = await DeudorAsync(http, admin, "Vera");
        var (wilson, _) = await DeudorAsync(http, admin, "Wilson");
        var (rita, _) = await DeudorAsync(http, admin, "Rita");
        foreach (var p in new[] { vera, wilson, rita }) await CobranzaE2E.AutorizarCanalAsync(http, admin, p, "Sms");
        var deVera = await CierreE2E.DesembolsadoAsync(http, esc, vera, 1_000_000m);
        var deWilson = await CierreE2E.DesembolsadoAsync(http, esc, wilson, 1_000_000m);
        var deRita = await CierreE2E.DesembolsadoAsync(http, esc, rita, 1_000_000m);

        var exigible = await ExigibleAsync(http, admin, deVera);
        var inicio = exigible.AddDays(2);
        var habiles = await CobranzaE2E.CalendarioAsync(fx, esc, inicio, inicio.AddDays(120));
        var domingo = Primero(inicio.AddDays(1), d => d.DayOfWeek == DayOfWeek.Sunday);
        var festivo = Primero(inicio.AddDays(1), d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !habiles.EsHabil(d));
        var tarde = Primero(inicio.AddDays(1), habiles.EsHabil);
        var doble = Primero(tarde.AddDays(1), habiles.EsHabil);
        var lunes = Primero(inicio.AddDays(4), d => d.DayOfWeek == DayOfWeek.Monday && habiles.EsHabil(d) && habiles.EsHabil(d.AddDays(2)));

        // =================================================================== el ciclo: los tres en mora, la etapa escribe ==
        reloj.Ir(inicio, 9);
        await CicloDeHoyAsync(fx, http, await t.JefeAsync(), tenant);
        reloj.Ir(inicio, 9, 30);
        var cartera = await t.AdministradorAsync();
        var aDomingo = await ProgramarAsync(http, cartera, deVera, vera, "Informative", "ABONOCAP", CobranzaE2E.Local(domingo, 10), Abono);
        var aFestivo = await ProgramarAsync(http, cartera, deVera, vera, "Informative", "ABONOCAP", CobranzaE2E.Local(festivo, 10), Abono);
        var deNoche = await ProgramarAsync(http, cartera, deVera, vera, "Informative", "ABONOCAP", CobranzaE2E.Local(tarde, 19, 30), Abono);
        var primeroDelDia = await ProgramarAsync(http, cartera, deVera, vera, "Informative", "ABONOCAP", CobranzaE2E.Local(doble, 9), Abono);
        var segundoDelDia = await ProgramarAsync(http, cartera, deVera, vera, "Informative", "ABONOCAP", CobranzaE2E.Local(doble, 9, 30), Abono);
        var revocado = await ProgramarAsync(http, cartera, deRita, rita, "Informative", "ABONOCAP", CobranzaE2E.Local(doble, 10), Abono);
        // Rita revoca el SMS antes de que salga nada: lo que estaba en cola se reevalúa al despachar.
        await CobranzaE2E.AutorizarCanalAsync(http, admin, rita, "Sms", reloj.UtcAhora.AddMinutes(-1), decision: "Declined");

        var alNacer = (await CobranzaE2E.MensajesAsync(fx, esc)).ToDictionary(m => m.PublicId);
        DiferidoPor(alNacer[aDomingo], ContactWindowReason.Sunday);
        DiferidoPor(alNacer[aFestivo], ContactWindowReason.Holiday);
        DiferidoPor(alNacer[deNoche], ContactWindowReason.OutsideHours);

        // ============================================ hasta el lunes: sale lo de la etapa; un contacto directo por teléfono ==
        await AvanzarDespachandoAsync(esc, reloj, tenant, CobranzaE2E.Local(lunes, 9, 59));
        reloj.Ir(lunes, 10, 30);
        var gestion = await CobranzaE2E.GestionAsync(http, await t.GestorAsync(), deWilson, wilson, CobranzaE2E.Local(lunes, 10), "ContactedNoCommitment");
        gestion.GetProperty("windowDecision").GetString().Should().Be("Permitido", "el lunes a las 10:00 nadie había contactado a Wilson");
        var miercoles = lunes.AddDays(2);
        var cobro = await ProgramarAsync(http, await t.AdministradorAsync(), deWilson, wilson, "Collection", "MORAADMIN", CobranzaE2E.Local(miercoles, 10),
            new Dictionary<string, string> { ["ValorVencido"] = "$ 40.000", ["DiasDeMora"] = "10", ["CuotasVencidas"] = "1", ["ValorParaPonerseAlDia"] = "$ 40.000" });
        DiferidoPor((await CobranzaE2E.MensajesAsync(fx, esc, wilson)).Single(m => m.PublicId == cobro), ContactWindowReason.WeeklyChannelLocked);

        // ================================================================ hasta que no quede nada en cola en el horizonte ==
        var horizonte = new[] { festivo, lunes.AddDays(14), doble }.Max().AddDays(3);
        await AvanzarDespachandoAsync(esc, reloj, tenant, CobranzaE2E.Local(horizonte, 20));

        var todos = (await CobranzaE2E.MensajesAsync(fx, esc)).ToDictionary(m => m.PublicId);
        Salio(todos[aDomingo]).En.Should().BeAfter(CobranzaE2E.Local(domingo, 10));
        Salio(todos[aDomingo]).En.DayOfWeek.Should().NotBe(DayOfWeek.Sunday, "el domingo no se contacta");
        DateOnly.FromDateTime(Salio(todos[aFestivo]).En).Should().BeAfter(festivo, "el festivo nacional no se contacta");
        var noche = Salio(todos[deNoche]).En;
        noche.Should().BeAfter(CobranzaE2E.Local(tarde, 19, 30));
        noche.Hour.Should().BeInRange(7, 18, "sale dentro del horario");
        DateOnly.FromDateTime(Salio(todos[primeroDelDia]).En).Should().NotBe(DateOnly.FromDateTime(Salio(todos[segundoDelDia]).En),
            "un contacto por día: el segundo sale otro día");
        CobranzaE2E.SemanaDe(DateOnly.FromDateTime(Salio(todos[cobro]).En)).Should().BeAfter(CobranzaE2E.SemanaDe(lunes),
            "la llamada fijó el teléfono como canal de esa semana");
        var deRitaRevocado = todos[revocado];
        deRitaRevocado.Salio.Should().BeNull("el canal se revocó antes de salir");
        deRitaRevocado.Estado.Should().Be(DebtorMessageEventKind.Excluded);
        deRitaRevocado.Eventos[^1].Motivos.Should().Contain(ContactWindowReason.ChannelNotAuthorized);
        todos.Values.Where(m => m.PersonId == todos[revocado].PersonId).Should().OnlyContain(m => m.Salio == null, "a Rita no le sale nada sin el canal");
        todos.Values.Where(m => m.Estado is DebtorMessageEventKind.Queued or DebtorMessageEventKind.Deferred).Should().BeEmpty("el horizonte despachó todo");

        // ===================================================================================== SC-011, sobre el registro ==
        var salieron = todos.Values.Where(m => m.Salio is not null).ToList();
        salieron.Should().HaveCountGreaterThanOrEqualTo(7, "salieron los programados y los de la etapa");
        var decisiones = await ReconstruirAsync(esc, todos.Values.ToList(), inicio.AddDays(-7), horizonte.AddDays(1));
        decisiones.Where(d => d.Decision is not Permitido).Select(d => $"{d.Mensaje.Plantilla} ({d.Mensaje.Proposito}) a las {d.Mensaje.Salio!.En:yyyy-MM-dd HH:mm}: {d.Decision}")
            .Should().BeEmpty("ningún mensaje sale fuera de la ventana legal (SC-011)");
        decisiones.Should().HaveCount(salieron.Count);
    }

    // ------------------------------------------------------------------------------------------------------------ ayudantes --

    private static DateOnly Primero(DateOnly desde, Func<DateOnly, bool> cumple)
    {
        for (var d = desde; d < desde.AddDays(120); d = d.AddDays(1))
            if (cumple(d)) return d;
        throw new InvalidOperationException($"No hay un día que sirva en los 120 días desde {desde:yyyy-MM-dd}.");
    }

    private static void DiferidoPor(CobranzaE2E.Mensaje m, ContactWindowReason motivo)
    {
        m.Eventos[0].Clase.Should().Be(DebtorMessageEventKind.Deferred, $"el mensaje {m.Plantilla} nace diferido por {motivo}");
        m.Eventos[0].Motivos.Should().Contain(motivo);
    }

    private static CobranzaE2E.Evento Salio(CobranzaE2E.Mensaje m)
    {
        m.Salio.Should().NotBeNull($"el mensaje {m.Plantilla} programado salió: {string.Join(" → ", m.Eventos.Select(e => e.Clase))}");
        return m.Salio!;
    }

    /// <summary>Un mensaje programado por la ruta de envío manual (<c>POST /api/lending/debtor-messages</c>); exige el 201 y devuelve su <c>PublicId</c>.</summary>
    private static async Task<Guid> ProgramarAsync(HttpClient http, string token, Guid credito, Guid persona, string proposito, string plantilla, DateTime enLocal,
        Dictionary<string, string> variables)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, CobranzaE2E.Mensajes, new
        {
            loanPublicId = credito, recipientPersonPublicId = persona, channel = "Sms", purpose = proposito, templateCode = plantilla,
            scheduledFor = new DateTimeOffset(enLocal, TimeSpan.FromHours(-5)), variables,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"programar {plantilla} para {enLocal:yyyy-MM-dd HH:mm}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    /// <summary>
    /// Lleva el reloj al vencimiento de cada mensaje en cola (<c>Queued</c> o <c>Deferred</c>) y corre <c>cartera.mensajes</c>, hasta que no quede nada que
    /// venza antes de <paramref name="hasta"/>; deja el reloj en <paramref name="hasta"/>. Falla si un vencido no avanza.
    /// </summary>
    private async Task AvanzarDespachandoAsync(Escenario esc, CobranzaE2E.Reloj reloj, Guid tenant, DateTime hasta)
    {
        (Guid, DateTime)? anterior = null;
        for (var i = 0; i < 400; i++)
        {
            var proximo = (await CobranzaE2E.MensajesAsync(fx, esc))
                .Where(m => m.Estado is DebtorMessageEventKind.Queued or DebtorMessageEventKind.Deferred && m.Eventos[^1].Vence is not null)
                .OrderBy(m => m.Eventos[^1].Vence).Select(m => ((Guid, DateTime)?)(m.PublicId, m.Eventos[^1].Vence!.Value)).FirstOrDefault();
            if (proximo is not { } p || p.Item2 > hasta) break;
            proximo.Should().NotBe(anterior, $"el mensaje {p.Item1} venció el {p.Item2:yyyy-MM-dd HH:mm} y cartera.mensajes no lo movió");
            anterior = proximo;
            var destino = p.Item2.AddMinutes(1);
            if (destino > reloj.Ahora) reloj.Ir(destino);
            await CobranzaE2E.DespacharAsync(fx, tenant);
        }
        if (hasta > reloj.Ahora) reloj.Ir(hasta);
    }

    /// <summary>Una decisión reconstruida del motor para un mensaje que salió.</summary>
    private sealed record Reconstruida(CobranzaE2E.Mensaje Mensaje, DecisionDeContacto Decision);

    /// <summary>
    /// Para cada mensaje que salió, la entrada del motor con lo conocido en el instante de su salida (§4.9) y la decisión de
    /// <see cref="VentanaLegalDeContacto.Evaluar"/>.
    /// </summary>
    private Task<List<Reconstruida>> ReconstruirAsync(Escenario esc, IReadOnlyList<CobranzaE2E.Mensaje> mensajes, DateOnly desde, DateOnly hasta) =>
        EnLaBaseAsync(fx, esc, async db =>
        {
            var desfase = new ConductorDelDespachador(fx).Reloj.AhoraLocal.Offset;
            var personas = mensajes.Select(m => m.PersonId).Distinct().ToList();
            var maestro = await db.People.AsNoTracking().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            var deudores = await db.Loans.AsNoTracking().Where(l => personas.Contains(l.DebtorPersonId)).ToDictionaryAsync(l => l.Id, l => l.DebtorPersonId);
            var gestiones = await db.CollectionActions.AsNoTracking().Where(a => personas.Contains(a.PersonId)).ToListAsync();
            var consentimientos = await db.HabeasDataConsents.AsNoTracking().Where(c => personas.Contains(c.PersonId)).ToListAsync();
            // Los no hábiles de la ventana: los festivos nacionales (el domingo lo sabe el motor; el sábado tiene su franja).
            var calendario = await new Application.Common.Calendar.CalendarioDeDiasHabiles(db).ObtenerAsync(desde, hasta,
                Application.Common.Calendar.DiasDeDescanso.SoloDomingo);
            var noHabiles = new List<DateOnly>();
            for (var d = desde; d <= hasta; d = d.AddDays(1))
                if (d.DayOfWeek != DayOfWeek.Sunday && !calendario.EsHabil(d)) noHabiles.Add(d);
            var reglas = new ReglasDeContacto(new FranjaHoraria(new TimeOnly(7, 0), new TimeOnly(19, 0)), new FranjaHoraria(new TimeOnly(8, 0), new TimeOnly(15, 0)),
                1, noHabiles);

            var salida = new List<Reconstruida>();
            foreach (var m in mensajes.Where(m => m.Salio is not null))
            {
                var en = m.Salio!.En;
                var antes = mensajes.Where(x => x.PersonId == m.PersonId && x.Id != m.Id && x.Salio is { } s && s.En < en)
                    .Select(x => ContactoRegistrado.DeMensaje(x.Salio!.En, x.Canal, x.Proposito));
                var directas = gestiones.Where(g => g.PersonId == m.PersonId && CobranzaE2E.ALocal(g.OccurredAt, desfase) < en)
                    .Select(g => new GestionParaHistorial(g.Id, CobranzaE2E.ALocal(g.OccurredAt, desfase), g.Channel, g.IsDirectContact, g.VoidsActionId));
                var historial = HistorialDeContactos.Componer(antes, directas);

                var persona = maestro[m.PersonId];
                var obligado = m.LoanId is not { } loan || deudores.GetValueOrDefault(loan) == m.PersonId;
                var titular = new EstadoDelTitular(obligado, ContactRole.Debtor)
                {
                    Autorizaciones = AutorizacionDelCanal(consentimientos, m.PersonId, m.Canal, en, desfase) is { } a ? [a] : [],
                    AutorizacionGeneral = Vigente(consentimientos, m.PersonId, DataAuthorizationPurpose.GeneralTreatment, null, en, desfase) is not null,
                    AutorizacionGeneralConEvidencia = Vigente(consentimientos, m.PersonId, DataAuthorizationPurpose.GeneralTreatment, null, en, desfase) is { } g
                                                      && (g.EvidenceAttachmentPublicId is not null || g.Origin != DataAuthorizationOrigin.Migration),
                    Destinos = Enum.GetValues<MessageChannel>().Where(c => ColaDeMensajesAlDeudor.DestinoDe(persona, c) is not null).ToList(),
                };
                var decision = VentanaLegalDeContacto.Evaluar(new SolicitudDeContacto(en, m.Canal, m.Proposito, Inmediato: true) { Ahora = en }, reglas, historial,
                    titular);
                salida.Add(new Reconstruida(m, decision));
            }
            return salida;
        });

    private static AutorizacionDeCanal? AutorizacionDelCanal(IReadOnlyList<Domain.Entities.Compliance.HabeasDataConsent> filas, int persona, MessageChannel canal,
        DateTime enLocal, TimeSpan desfase)
    {
        if (canal is not (MessageChannel.Email or MessageChannel.Sms or MessageChannel.WhatsApp)) return null;
        var deCanal = canal switch
        {
            MessageChannel.Email => DataAuthorizationChannel.Email,
            MessageChannel.Sms => DataAuthorizationChannel.Sms,
            _ => DataAuthorizationChannel.WhatsApp,
        };
        return Vigente(filas, persona, DataAuthorizationPurpose.ContactChannel, deCanal, enLocal, desfase) is { } f
            ? new AutorizacionDeCanal(canal, f.EvidenceAttachmentPublicId is not null || f.Origin != DataAuthorizationOrigin.Migration)
            : null;
    }

    /// <summary>La última decisión de la finalidad (y el canal) registrada hasta el instante, si es una aceptación.</summary>
    private static Domain.Entities.Compliance.HabeasDataConsent? Vigente(IReadOnlyList<Domain.Entities.Compliance.HabeasDataConsent> filas, int persona,
        DataAuthorizationPurpose finalidad, DataAuthorizationChannel? canal, DateTime enLocal, TimeSpan desfase)
    {
        var ultima = filas.Where(f => f.PersonId == persona && f.Purpose == finalidad && (canal is null || f.ContactChannel == canal)
                                      && CobranzaE2E.ALocal(f.ActionAt, desfase) <= enLocal)
            .OrderBy(f => f.ActionAt).ThenBy(f => f.Id).LastOrDefault();
        return ultima is { Action: AccionesDeConsentimiento.Accepted } ? ultima : null;
    }
}
