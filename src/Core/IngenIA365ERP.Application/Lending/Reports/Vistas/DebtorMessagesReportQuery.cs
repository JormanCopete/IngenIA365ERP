using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>debtor-messages</c> de <c>/api/reports/lending</c> (feature 013, US15, K1, T0797; FR-122, FR-133; contracts/api.md §17): los
/// mensajes al deudor pedidos en el rango (hora de Colombia), filtrados por <c>purpose</c>, <c>channel</c> y <c>status</c> (el último evento),
/// cada uno con su crédito, su destinatario, el canal, el propósito, la plantilla sellada, el estado y, cuando quedó diferido, excluido, fallido o
/// cancelado, el motivo. La misma proyección y el mismo alcance del registro de envíos (<c>ProyeccionDeMensajesAlDeudor.ConAlcance</c>, por la
/// oficina y la línea del crédito; un mensaje sin crédito sólo lo ve el alcance total), con <see cref="IAlcanceDeCartera"/> en la consulta. Trae
/// datos del deudor (PD). Columnas ocultas <c>_credito</c> y <c>_mensaje</c>. (nuevo)
/// </summary>
public sealed record DebtorMessagesReportQuery(FiltrosDeInformeDeCartera Filtros, string? Purpose = null, string? Channel = null, string? Status = null)
    : IRequest<Result<TablaExportable>>;

public sealed class DebtorMessagesReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<DebtorMessagesReportQuery, Result<TablaExportable>>
{
    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    private static readonly DebtorMessageEventKind[] ConMotivo =
        [DebtorMessageEventKind.Deferred, DebtorMessageEventKind.Excluded, DebtorMessageEventKind.Failed, DebtorMessageEventKind.Cancelled];

    /// <summary>Lo que la vista declara al publicarse (T0798).</summary>
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "debtor-messages", "Mensajes al deudor",
        "Los mensajes pedidos en el rango con su canal, propósito, plantilla y estado, y el motivo de cada exclusión o diferimiento.",
        "mensajes-al-deudor", ["from", "to", "branch", "line"], ["purpose", "channel", "status"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha"),
        new("Crédito"),
        new("Destinatario"),
        new("Canal"),
        new("Propósito"),
        new("Plantilla"),
        new("Estado"),
        new("Motivo de exclusión o diferimiento"),
        new("_credito", Clave: "_credito"),
        new("_mensaje", Clave: "_mensaje"),
    ];

    public async Task<Result<TablaExportable>> Handle(DebtorMessagesReportQuery request, CancellationToken ct)
    {
        var proposito = Enumerado<DebtorMessagePurpose>("purpose", request.Purpose);
        if (proposito.IsFailure) return Result.Failure<TablaExportable>(proposito.Error);
        var canal = Enumerado<MessageChannel>("channel", request.Channel);
        if (canal.IsFailure) return Result.Failure<TablaExportable>(canal.Error);
        var estado = Enumerado<DebtorMessageEventKind>("status", request.Status);
        if (estado.IsFailure) return Result.Failure<TablaExportable>(estado.Error);

        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var consulta = ProyeccionDeMensajesAlDeudor.ConAlcance(db, ambito.Alcance);
        if (ambito.Vacio) consulta = consulta.Where(x => false);
        if (ambito.Sucursal is { } s) consulta = consulta.Where(x => x.Oficina == s);
        if (ambito.Linea is { } l) consulta = consulta.Where(x => x.Linea == l);
        if (proposito.Value is { } p) consulta = consulta.Where(x => x.Mensaje.Purpose == p);
        if (canal.Value is { } c) consulta = consulta.Where(x => x.Mensaje.Channel == c);
        var desdeUtc = DateTime.SpecifyKind(desde.ToDateTime(TimeOnly.MinValue) - Colombia, DateTimeKind.Utc);
        var hastaUtc = DateTime.SpecifyKind(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue) - Colombia, DateTimeKind.Utc);
        consulta = consulta.Where(x => x.Mensaje.RequestedAt >= desdeUtc && x.Mensaje.RequestedAt < hastaUtc);
        if (estado.Value is { } e)
            consulta = consulta.Where(x => db.DebtorMessageEvents
                .Where(ev => ev.DebtorMessageId == x.Mensaje.Id).OrderByDescending(ev => ev.Sequence).Select(ev => ev.Kind).FirstOrDefault() == e);

        var ids = await consulta.OrderBy(x => x.Mensaje.RequestedAt).ThenBy(x => x.Mensaje.Id).Select(x => x.Mensaje.Id).ToListAsync(ct);
        var mensajes = await ProyeccionDeMensajesAlDeudor.VariosAsync(db, ids, ct);
        var filas = ids.Select(id =>
        {
            var m = mensajes[id];
            var pedido = new DateTimeOffset(DateTime.SpecifyKind(m.RequestedAt, DateTimeKind.Utc)).ToOffset(Colombia);
            return new FilaExportable(
            [
                pedido.ToString("yyyy-MM-dd HH:mm"), m.Loan?.Number ?? string.Empty, m.Recipient.FullName, TextosDeInformesDeCartera.Canal(m.Channel),
                TextosDeInformesDeCartera.Proposito(m.Purpose), m.Template is { } t ? $"{t.Code} v{t.Version}" : string.Empty,
                TextosDeInformesDeCartera.EstadoDeMensaje(m.Status), Motivo(m.LastEvent), m.Loan?.LoanPublicId.ToString(), m.PublicId.ToString(),
            ]);
        }).ToList();
        return Result.Success(new TablaExportable("Mensajes al deudor",
            $"Pedidos del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd} · {filas.Count} mensajes · {ambito.Descripcion()}", Columnas, filas, null,
        [
            "Fecha del pedido en hora de Colombia; el estado es el del último evento del mensaje.",
            "El motivo se muestra cuando el mensaje quedó diferido (con la fecha del siguiente intento), excluido, fallido o cancelado.",
        ]));
    }

    private static string Motivo(UltimoEventoDelMensajeDto ultimo)
    {
        if (!ConMotivo.Contains(ultimo.Kind)) return string.Empty;
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(ultimo.Detail)) partes.Add(ultimo.Detail!);
        if (ultimo.Reasons.Count > 0) partes.Add(string.Join(", ", ultimo.Reasons));
        if (ultimo.Kind == DebtorMessageEventKind.Deferred && ultimo.NextAttemptAt is { } siguiente)
            partes.Add($"hasta {new DateTimeOffset(DateTime.SpecifyKind(siguiente, DateTimeKind.Utc)).ToOffset(Colombia):yyyy-MM-dd HH:mm}");
        return string.Join(" · ", partes);
    }

    /// <summary>Un filtro propio de un enum, por nombre o por número; vacío = sin filtro; otro valor, <c>Lending.Report.FilterInvalid</c>.</summary>
    private static Result<T?> Enumerado<T>(string filtro, string? valor) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(valor)) return Result.Success<T?>(null);
        return Enum.TryParse<T>(valor.Trim(), ignoreCase: true, out var v) && Enum.IsDefined(v)
            ? Result.Success<T?>(v)
            : Result.Failure<T?>(FiltrosDeInformeDeCartera.FiltroInvalido(filtro, valor, Enum.GetNames<T>()));
    }
}
