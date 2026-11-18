using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Collections.Actions;
using IngenIA365ERP.Application.Lending.Collections.PriorNotices;
using IngenIA365ERP.Application.Lending.Deposits.Reports;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.PersonStatement;

/// <summary>
/// Las secciones del estado de cuenta de una persona (2026-11-16, pedido del dueño): las tablas que la pantalla <c>/cartera/estado-de-cuenta</c> abre
/// con sus botones generales, cada una con su código (el final de la ruta), su título y el permiso que la publica (la ruta lo exige). (nuevo)
/// </summary>
public static class SeccionesDelEstadoDeCuenta
{
    public const string Cuotas = "installments";
    public const string Mensajes = "messages";
    public const string Gestiones = "collection-actions";
    public const string Solicitudes = "applications";
    public const string Novedades = "novelties";
    public const string Cupos = "credit-limits";

    /// <summary>Las seis secciones: código, título y permiso.</summary>
    public static readonly IReadOnlyList<(string Codigo, string Titulo, string Permiso)> Todas =
    [
        (Cuotas, "Cuotas pendientes de todas las obligaciones", "Lending.Loans.Read"),
        (Mensajes, "Circulares y avisos de cobro", "Lending.DebtorMessages.Read"),
        (Gestiones, "Gestiones de cobranza", "Lending.Collections.Read"),
        (Solicitudes, "Solicitudes de crédito", "Lending.Applications.Read"),
        (Novedades, "Novedades y descuentos", "Lending.Novelties.Read"),
        (Cupos, "Cupo de crédito", "Lending.CreditLimits.Read"),
    ];

    public static bool Existe(string? codigo) => Todas.Any(s => string.Equals(s.Codigo, codigo, StringComparison.Ordinal));

    public static string Titulo(string codigo) => Todas.First(s => s.Codigo == codigo).Titulo;

    public static Error Invalida(string? codigo) => new("Lending.PersonStatement.SectionInvalid",
        $"La sección «{codigo}» no existe. Admite: {string.Join(", ", Todas.Select(s => s.Codigo))}.");
}

/// <summary>
/// <c>GET /api/lending/person-statement/sections/{section}?personPublicId=&amp;date=</c> (2026-11-16, pedido del dueño): una sección del estado de cuenta
/// de la persona como <see cref="TablaExportable"/> —título, columnas tipadas, filas y, si aplica, la fila de totales—. No lee créditos, mensajes ni
/// solicitudes por su cuenta: envía las consultas de cada módulo por <see cref="ISender"/>, que aplican el alcance de oficinas y líneas de quien mira;
/// cuando una consulta no filtra por persona, recorre los créditos de la persona (<see cref="ListLoansQuery"/> como deudora y como codeudora) y
/// consulta por crédito. Las listas paginadas se leen enteras hasta <see cref="GetPersonStatementSectionQueryHandler.Tope"/> filas. (nuevo)
/// </summary>
public sealed record GetPersonStatementSectionQuery(Guid PersonPublicId, string Section, DateOnly? Date = null) : IRequest<Result<TablaExportable>>;

public sealed class GetPersonStatementSectionQueryValidator : AbstractValidator<GetPersonStatementSectionQuery>
{
    public GetPersonStatementSectionQueryValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Indique la persona.");
        RuleFor(x => x.Section).Must(SeccionesDelEstadoDeCuenta.Existe)
            .WithMessage($"La sección es una de: {string.Join(", ", SeccionesDelEstadoDeCuenta.Todas.Select(s => s.Codigo))}.");
    }
}

public sealed class GetPersonStatementSectionQueryHandler(IApplicationDbContext db, ISender sender, IDateTimeService reloj)
    : IRequestHandler<GetPersonStatementSectionQuery, Result<TablaExportable>>
{
    /// <summary>Cuántas filas se leen como máximo de una lista paginada (mensajes, gestiones, solicitudes, novedades).</summary>
    public const int Tope = 500;

    private const int Pagina = 200;

    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Los conceptos de una cuota que van en «Interés» (corriente y mora, con sus suspensos).</summary>
    private static readonly HashSet<string> Intereses = new(StringComparer.Ordinal)
    {
        nameof(LoanConcept.CurrentInterest), nameof(LoanConcept.CurrentInterestInSuspense), nameof(LoanConcept.DefaultInterest),
        nameof(LoanConcept.DefaultInterestInSuspense),
    };

    /// <summary>Lo que no se cobra en una cuota (el anticipo y el saldo a favor van aparte).</summary>
    private static readonly HashSet<string> InteresCorriente = new(StringComparer.Ordinal)
    {
        nameof(LoanConcept.CurrentInterest), nameof(LoanConcept.CurrentInterestInSuspense),
    };

    private static readonly HashSet<string> InteresDeMora = new(StringComparer.Ordinal)
    {
        nameof(LoanConcept.DefaultInterest), nameof(LoanConcept.DefaultInterestInSuspense),
    };

    private static readonly HashSet<string> AFavor = new(StringComparer.Ordinal) { nameof(LoanConcept.Advance), nameof(LoanConcept.CreditBalance) };

    public async Task<Result<TablaExportable>> Handle(GetPersonStatementSectionQuery request, CancellationToken ct)
    {
        if (!SeccionesDelEstadoDeCuenta.Existe(request.Section))
            return Result.Failure<TablaExportable>(SeccionesDelEstadoDeCuenta.Invalida(request.Section));
        var persona = await LecturasDelEstadoDeCuenta.PersonaAsync(db, request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<TablaExportable>(LecturasDelEstadoDeCuenta.PersonaInexistente());

        var fecha = request.Date ?? reloj.HoyLocal;
        var titulo = SeccionesDelEstadoDeCuenta.Titulo(request.Section);
        var subtitulo = $"{persona.Nombre} · {persona.Documento} · al {fecha.ToString("dd/MM/yyyy", Co)}";
        return request.Section switch
        {
            SeccionesDelEstadoDeCuenta.Cuotas => await CuotasAsync(persona.PublicId, fecha, titulo, subtitulo, ct),
            SeccionesDelEstadoDeCuenta.Mensajes => await MensajesAsync(persona.PublicId, titulo, subtitulo, ct),
            SeccionesDelEstadoDeCuenta.Gestiones => await GestionesAsync(persona.PublicId, titulo, subtitulo, ct),
            SeccionesDelEstadoDeCuenta.Solicitudes => await SolicitudesAsync(persona.PublicId, titulo, subtitulo, ct),
            SeccionesDelEstadoDeCuenta.Novedades => await NovedadesAsync(persona.PublicId, fecha, titulo, subtitulo, ct),
            _ => await CuposAsync(persona.PublicId, fecha, titulo, subtitulo, ct),
        };
    }

    // ------------------------------------------------------------------------------------------------------- cuotas --

    /// <summary>
    /// Las cuotas por vencer y vencidas de cada crédito vivo (vigente o castigado) como deudora, del estado de cuenta de cada crédito a la fecha: lo que
    /// debe de cada una por concepto (capital, interés corriente y de mora, seguro, administración, otros), el valor de la cuota del plan y lo que debe en
    /// total.
    /// </summary>
    private async Task<Result<TablaExportable>> CuotasAsync(Guid persona, DateOnly fecha, string titulo, string subtitulo, CancellationToken ct)
    {
        var creditos = await CreditosAsync(persona, soloDeudor: true, ct);
        if (creditos.IsFailure) return Result.Failure<TablaExportable>(creditos.Error);

        var avisos = new List<string>();
        var cuotas = new List<(LoanListItemDto Credito, StatementInstallmentDto Cuota)>();
        foreach (var (credito, _) in creditos.Value.Where(c => c.Credito.Status is nameof(LoanStatus.Active) or nameof(LoanStatus.WrittenOff)))
        {
            var estado = await sender.Send(new GetLoanStatementQuery(credito.PublicId, fecha), ct);
            if (estado.IsFailure)
            {
                avisos.Add($"El crédito {credito.Number}: {estado.Error.Message}");
                continue;
            }
            cuotas.AddRange(estado.Value.Installments.Where(q => q.Status is EstadosDeCuota.PorVencer or EstadosDeCuota.Vencida).Select(q => (credito, q)));
        }

        var columnas = new List<ColumnaExportable>
        {
            new("Crédito"), new("Línea"), new("Cuota", TipoDeColumna.Entero), new("Vence", TipoDeColumna.Fecha), new("Estado"),
            // 2026-11-17 (pedido del dueño): el interés corriente y el de mora en columnas aparte, como en el detalle de la caja.
            new("Capital", TipoDeColumna.Moneda), new("Interés corriente", TipoDeColumna.Moneda), new("Interés de mora", TipoDeColumna.Moneda),
            new("Seguro", TipoDeColumna.Moneda),
            // 2026-11-16 (pedido del dueño; feature 024, fase 2): la administración, aparte del seguro y de los otros.
            new("Administración", TipoDeColumna.Moneda),
            new("Otros", TipoDeColumna.Moneda), new("Valor cuota", TipoDeColumna.Moneda), new("Debe", TipoDeColumna.Moneda), new("Crédito", Clave: "_credito"),
        };
        var filas = new List<FilaExportable>();
        decimal capital = 0m, interes = 0m, mora = 0m, seguro = 0m, administracion = 0m, otros = 0m, valor = 0m, debe = 0m;
        foreach (var (credito, q) in cuotas.OrderBy(x => x.Cuota.DueDate).ThenBy(x => x.Credito.Number, StringComparer.Ordinal).ThenBy(x => x.Cuota.Number))
        {
            var c = q.Owed.GetValueOrDefault(nameof(LoanConcept.Principal));
            var i = q.Owed.Where(o => InteresCorriente.Contains(o.Key)).Sum(o => o.Value);
            var m = q.Owed.Where(o => InteresDeMora.Contains(o.Key)).Sum(o => o.Value);
            var s = q.Owed.GetValueOrDefault(nameof(LoanConcept.Insurance));
            var a = q.Owed.GetValueOrDefault(nameof(LoanConcept.Administration));
            var o = q.Owed.Where(x => x.Key != nameof(LoanConcept.Principal) && x.Key != nameof(LoanConcept.Insurance)
                                       && x.Key != nameof(LoanConcept.Administration) && !Intereses.Contains(x.Key) && !AFavor.Contains(x.Key)).Sum(x => x.Value);
            var d = c + i + m + s + a + o;
            capital += c; interes += i; mora += m; seguro += s; administracion += a; otros += o; valor += q.Total; debe += d;
            filas.Add(new FilaExportable([credito.Number, $"{credito.Line.Code} · {credito.Line.Name}", q.Number, q.DueDate,
                q.Status == EstadosDeCuota.Vencida ? "Vencida" : "Por vencer", c, i, m, s, a, o, q.Total, d, credito.PublicId.ToString()],
                Resaltada: q.Status == EstadosDeCuota.Vencida));
        }

        var vencidas = cuotas.Count(x => x.Cuota.Status == EstadosDeCuota.Vencida);
        var totales = new FilaExportable(["Total", null, null, null, $"{cuotas.Count} cuotas", capital, interes, mora, seguro, administracion, otros, valor, debe, null]);
        var notas = new List<string>
        {
            $"{vencidas} cuotas vencidas y {cuotas.Count - vencidas} por vencer de sus créditos como deudora. «Debe» es lo que debe hoy de cada cuota "
            + "(en las por vencer, el interés causado hasta la fecha); «Valor cuota» es la del plan.",
        };
        notas.AddRange(avisos);
        return Result.Success(new TablaExportable(titulo, subtitulo, columnas, filas, filas.Count == 0 ? null : totales, notas));
    }

    // ---------------------------------------------------------------------------------------------------- mensajes --

    /// <summary>
    /// Los mensajes enviados a la persona (<see cref="ListDebtorMessagesQuery"/> por persona, todas las páginas hasta el tope) y los avisos previos de sus
    /// créditos dirigidos a ella (<see cref="ListPriorNoticesQuery"/> por crédito), el más reciente primero.
    /// </summary>
    private async Task<Result<TablaExportable>> MensajesAsync(Guid persona, string titulo, string subtitulo, CancellationToken ct)
    {
        var mensajes = await TodasAsync(p => new ListDebtorMessagesQuery(PersonPublicId: persona, Page: p, PageSize: Pagina), ct);
        if (mensajes.IsFailure) return Result.Failure<TablaExportable>(mensajes.Error);
        var creditos = await CreditosAsync(persona, soloDeudor: false, ct);
        if (creditos.IsFailure) return Result.Failure<TablaExportable>(creditos.Error);

        var desfase = reloj.AhoraLocal.Offset;
        var filas = new List<(DateOnly Fecha, FilaExportable Fila)>();
        foreach (var m in mensajes.Value)
        {
            var dia = DiaLocal(m.RequestedAt, desfase);
            filas.Add((dia, new FilaExportable([dia, m.Loan?.Number, TextosDeInformesDeCartera.Proposito(m.Purpose), TextosDeInformesDeCartera.Canal(m.Channel),
                TextosDeInformesDeCartera.EstadoDeMensaje(m.Status), m.Template is { } t ? $"{t.Code} v{t.Version.ToString(Co)}" : null])));
        }

        var avisos = 0;
        foreach (var (credito, _) in creditos.Value)
        {
            var leidos = await TodasAsync(p => new ListPriorNoticesQuery(LoanPublicId: credito.PublicId, Page: p, PageSize: Pagina), ct);
            if (leidos.IsFailure) return Result.Failure<TablaExportable>(leidos.Error);
            foreach (var n in leidos.Value.Where(n => n.Person.PersonPublicId == persona))
            {
                avisos++;
                var canales = n.Communications.Select(c => TextosDeInformesDeCartera.Canal(c.Channel)).Distinct().ToList();
                var estado = TextosDeInformesDeCartera.EstadoDeAvisoPrevio(n.Status) + (n.Origin == AvisosPrevios.OrigenMigracion ? " (migrado)" : string.Empty);
                var dia = n.EvidenceDate ?? n.EpisodeStart;
                filas.Add((dia, new FilaExportable([dia, n.Loan.Number, TextosDeInformesDeCartera.Proposito(DebtorMessagePurpose.PriorNotice),
                    canales.Count == 0 ? null : string.Join(", ", canales), estado, AvisosPrevios.Plantilla])));
            }
        }

        var columnas = new List<ColumnaExportable>
        {
            new("Fecha", TipoDeColumna.Fecha), new("Crédito"), new("Clase"), new("Canal"), new("Estado"), new("Plantilla"),
        };
        var notas = new List<string>
        {
            $"{mensajes.Value.Count} mensajes y {avisos} avisos previos al reporte negativo (cada aviso previo es un episodio de mora; su estado dice si "
            + "ya se puede reportar).",
        };
        if (mensajes.Value.Count >= Tope) notas.Add($"Se muestran los {Tope} mensajes más recientes.");
        return Result.Success(new TablaExportable(titulo, subtitulo, columnas,
            filas.OrderByDescending(f => f.Fecha).Select(f => f.Fila).ToList(), null, notas));
    }

    // ---------------------------------------------------------------------------------------------------- gestiones --

    /// <summary>Las gestiones de cobranza de cada crédito de la persona —como deudora y como codeudora— con su promesa, la más reciente primero.</summary>
    private async Task<Result<TablaExportable>> GestionesAsync(Guid persona, string titulo, string subtitulo, CancellationToken ct)
    {
        var creditos = await CreditosAsync(persona, soloDeudor: false, ct);
        if (creditos.IsFailure) return Result.Failure<TablaExportable>(creditos.Error);

        var gestiones = new Dictionary<Guid, CollectionActionDto>();
        foreach (var (credito, _) in creditos.Value)
        {
            var leidas = await TodasAsync(p => new ListCollectionActionsQuery(LoanPublicId: credito.PublicId, Page: p, PageSize: Pagina), ct);
            if (leidas.IsFailure) return Result.Failure<TablaExportable>(leidas.Error);
            foreach (var g in leidas.Value) gestiones.TryAdd(g.PublicId, g);
        }

        var columnas = new List<ColumnaExportable>
        {
            new("Fecha", TipoDeColumna.Fecha), new("Crédito"), new("Canal"), new("Contactado"), new("Resultado"), new("Promesa"), new("Gestor"), new("Notas"),
        };
        var filas = gestiones.Values.OrderByDescending(g => g.At).Select(g => new FilaExportable([
            DateOnly.FromDateTime(g.At.DateTime), g.Loan?.Number, TextosDeInformesDeCartera.CanalDeGestion(g.Channel),
            $"{g.ContactedPerson.FullName} ({TextosDeInformesDeCartera.RolDeContacto(g.ContactRole)})",
            TextosDeInformesDeCartera.ResultadoDeGestion(g.Result) + (g.VoidsActionPublicId is not null ? " · anula otra gestión" : string.Empty),
            g.Promise is { } p
                ? $"{p.Amount.ToString("N0", Co)} para el {p.Date.ToString("dd/MM/yyyy", Co)} · {TextosDeInformesDeCartera.EstadoDePromesa(p.Status, p.Voided)}"
                : null,
            g.Collector?.Name ?? "Sistema", string.IsNullOrWhiteSpace(g.Notes) ? g.VoidReason : g.Notes,
        ])).ToList();
        var promesas = gestiones.Values.Where(g => g.Promise is { Voided: false }).ToList();
        var notas = new List<string>
        {
            $"{gestiones.Count} gestiones; {promesas.Count} con promesa de pago ({promesas.Count(g => g.Promise!.Status is null or PaymentPromiseStatus.Pending)} "
            + "pendientes).",
        };
        return Result.Success(new TablaExportable(titulo, subtitulo, columnas, filas, null, notas));
    }

    // -------------------------------------------------------------------------------------------------- solicitudes --

    /// <summary>Las solicitudes de crédito en que la persona es la solicitante, la más reciente primero.</summary>
    private async Task<Result<TablaExportable>> SolicitudesAsync(Guid persona, string titulo, string subtitulo, CancellationToken ct)
    {
        var solicitudes = await TodasAsync(p => new ListCreditApplicationsQuery(ApplicantPersonPublicId: persona, Page: p, PageSize: Pagina), ct);
        if (solicitudes.IsFailure) return Result.Failure<TablaExportable>(solicitudes.Error);

        var desfase = reloj.AhoraLocal.Offset;
        var columnas = new List<ColumnaExportable>
        {
            new("Número"), new("Fecha", TipoDeColumna.Fecha), new("Línea"), new("Monto", TipoDeColumna.Moneda), new("Plazo (cuotas)", TipoDeColumna.Entero),
            new("Estado"), new("Solicitud", Clave: "_solicitud"),
        };
        var filas = solicitudes.Value.Select(s => new FilaExportable([
            s.Number ?? "Sin radicar", s.SubmittedAt is { } radicada ? DiaLocal(radicada, desfase) : null, $"{s.Line.Code} · {s.Line.Name}", s.Amount,
            s.Installments, TextosDeInformesDeCartera.EstadoDeSolicitud(s.Status), s.PublicId.ToString(),
        ])).ToList();
        var notas = new List<string> { $"{filas.Count} solicitudes; la fecha es la de radicación." };
        if (solicitudes.Value.Count >= Tope) notas.Add($"Se muestran las {Tope} más recientes.");
        return Result.Success(new TablaExportable(titulo, subtitulo, columnas, filas, null, notas));
    }

    // ---------------------------------------------------------------------------------------------------- novedades --

    /// <summary>
    /// Las novedades de cada crédito de la persona (<see cref="ListLoanNoveltiesQuery"/> por crédito; una novedad de varios créditos sale una vez) y las
    /// cuotas no aplicadas por la libranza o la nómina (<see cref="LoanStatementDto.NotApplied"/> del estado de cuenta de cada crédito a la fecha).
    /// </summary>
    private async Task<Result<TablaExportable>> NovedadesAsync(Guid persona, DateOnly fecha, string titulo, string subtitulo, CancellationToken ct)
    {
        var creditos = await CreditosAsync(persona, soloDeudor: false, ct);
        if (creditos.IsFailure) return Result.Failure<TablaExportable>(creditos.Error);

        var desfase = reloj.AhoraLocal.Offset;
        var novedades = new Dictionary<Guid, LoanNoveltyListItemDto>();
        var noAplicados = new List<(string Credito, StatementNonDeductionDto NoAplicado)>();
        var avisos = new List<string>();
        foreach (var (credito, _) in creditos.Value)
        {
            var leidas = await TodasAsync(p => new ListLoanNoveltiesQuery(LoanPublicId: credito.PublicId, Page: p, PageSize: Pagina), ct);
            if (leidas.IsFailure) return Result.Failure<TablaExportable>(leidas.Error);
            foreach (var n in leidas.Value) novedades.TryAdd(n.PublicId, n);

            if (credito.Status is nameof(LoanStatus.PendingDisbursement) or nameof(LoanStatus.Voided)) continue;
            var estado = await sender.Send(new GetLoanStatementQuery(credito.PublicId, fecha), ct);
            if (estado.IsSuccess) noAplicados.AddRange(estado.Value.NotApplied.Select(n => (credito.Number, n)));
            else avisos.Add($"El crédito {credito.Number}: {estado.Error.Message}");
        }

        var filas = new List<(DateOnly Fecha, FilaExportable Fila)>();
        foreach (var n in novedades.Values)
        {
            var dia = DiaLocal(n.RequestedAt, desfase);
            filas.Add((dia, new FilaExportable([dia, string.Join(", ", n.Loans.Select(l => l.LoanNumber).Distinct()), "Novedad",
                $"{n.Number} · {TextosDeInformesDeCartera.MecanismoDeNovedad(n.Mechanism)} · {TextosDeInformesDeCartera.CausaDeNovedad(n.Cause)}", null,
                TextosDeInformesDeCartera.EstadoDeNovedad(n.Status)])));
        }
        foreach (var (credito, n) in noAplicados)
        {
            var motivo = Enum.TryParse<NonDeductionReason>(n.Reason, out var r) ? InstallmentsInArrearsReportQueryHandler.Motivo(r) : n.Reason;
            var detalle = $"Cuota {n.Installment.ToString(Co)} · {motivo}" + (string.IsNullOrWhiteSpace(n.ReasonText) ? string.Empty : $" ({n.ReasonText})")
                          + $" · {TextosDeInformesDeCartera.OrigenDeNoAplicado(n.Origin)}";
            filas.Add((n.DueDate, new FilaExportable([n.DueDate, credito, "Descuento no aplicado", detalle, n.NotDeductedAmount,
                n.NotDeductedAmount >= n.ExpectedAmount ? "No descontado" : "Descontado en parte"])));
        }

        var columnas = new List<ColumnaExportable>
        {
            new("Fecha", TipoDeColumna.Fecha), new("Crédito"), new("Clase"), new("Detalle"), new("Valor", TipoDeColumna.Moneda), new("Estado"),
        };
        var noDescontado = noAplicados.Sum(x => x.NoAplicado.NotDeductedAmount);
        var notas = new List<string>
        {
            $"{novedades.Count} novedades y {noAplicados.Count} cuotas no aplicadas por la libranza o la nómina. La fecha de una novedad es la de su "
            + "solicitud; la de un descuento no aplicado, el vencimiento de la cuota.",
        };
        notas.AddRange(avisos);
        return Result.Success(new TablaExportable(titulo, subtitulo, columnas, filas.OrderByDescending(f => f.Fecha).Select(f => f.Fila).ToList(),
            noAplicados.Count == 0 ? null : new FilaExportable(["Total no descontado", null, null, null, noDescontado, null]), notas));
    }

    // -------------------------------------------------------------------------------------------------------- cupos --

    /// <summary>
    /// Los cupos de la persona (<see cref="ListCreditLimitsQuery"/>: el que rige de cada clase y línea y el que espera decisión) con su disponible a la
    /// fecha (<see cref="GetCreditLimitAvailabilityQuery"/>: consolidado, por línea y rotativo) y, en las notas, el estado del deudor y los bloqueos.
    /// </summary>
    private async Task<Result<TablaExportable>> CuposAsync(Guid persona, DateOnly fecha, string titulo, string subtitulo, CancellationToken ct)
    {
        var disponible = await sender.Send(new GetCreditLimitAvailabilityQuery(persona, null, fecha), ct);
        if (disponible.IsFailure) return Result.Failure<TablaExportable>(disponible.Error);
        var cupos = await TodasAsync(p => new ListCreditLimitsQuery(PersonPublicId: persona, Page: p, PageSize: Pagina), ct);
        if (cupos.IsFailure) return Result.Failure<TablaExportable>(cupos.Error);

        var d = disponible.Value;
        var items = new List<(CreditLimitKind Clase, CreditLimitAvailabilityItemDto Item)>();
        if (d.Consolidated is { } consolidado) items.Add((CreditLimitKind.Consolidated, consolidado));
        items.AddRange(d.ByLine.Select(i => (CreditLimitKind.ByLine, i)));
        items.AddRange(d.Revolving.Select(i => (CreditLimitKind.Revolving, i)));
        var versiones = cupos.Value.ToDictionary(c => c.PublicId);

        static string Linea(CreditLimitLineDto? l) => l is null ? "Todas" : $"{l.Code} · {l.Name}";
        static string Vigencia(CreditLimitDto v) =>
            v.ValidTo is { } hasta ? $"{v.ValidFrom.ToString("dd/MM/yyyy", Co)} – {hasta.ToString("dd/MM/yyyy", Co)}" : $"Desde {v.ValidFrom.ToString("dd/MM/yyyy", Co)}";

        var filas = new List<FilaExportable>();
        foreach (var (clase, i) in items)
        {
            var v = versiones.GetValueOrDefault(i.CreditLimitPublicId);
            filas.Add(new FilaExportable([TextosDeInformesDeCartera.ClaseDeCupo(clase), Linea(i.Line), i.Limit, i.Used, i.Reserved, i.ApprovedNotDisbursed,
                i.Available, v is null ? TextosDeInformesDeCartera.EstadoDeCupo(d.Status) : TextosDeInformesDeCartera.EstadoDeCupo(v.Status),
                v is null ? null : Vigencia(v), v is null ? null : TextosDeInformesDeCartera.OrigenDeCupo(v.Origin)]));
        }
        // Las versiones que no tienen disponible (las que esperan decisión, las terminadas): con su valor y sin uso.
        var conDisponible = items.Select(i => i.Item.CreditLimitPublicId).ToHashSet();
        foreach (var v in cupos.Value.Where(v => !conDisponible.Contains(v.PublicId)))
            filas.Add(new FilaExportable([TextosDeInformesDeCartera.ClaseDeCupo(v.Kind), Linea(v.Line), v.Amount, null, null, null, null,
                TextosDeInformesDeCartera.EstadoDeCupo(v.Status), Vigencia(v), TextosDeInformesDeCartera.OrigenDeCupo(v.Origin)]));

        var columnas = new List<ColumnaExportable>
        {
            new("Clase"), new("Línea"), new("Cupo", TipoDeColumna.Moneda), new("Usado", TipoDeColumna.Moneda), new("Reservado", TipoDeColumna.Moneda),
            new("Aprobado sin desembolsar", TipoDeColumna.Moneda), new("Disponible", TipoDeColumna.Moneda), new("Estado"), new("Vigencia"), new("Origen"),
        };
        var notas = new List<string> { $"Estado del deudor: {TextosDeInformesDeCartera.EstadoDeCupo(d.Status).ToLower(Co)}." };
        if (d.DaysPastDue is { } mora) notas.Add($"Mora máxima de sus créditos vigentes: {mora.ToString(Co)} días.");
        foreach (var b in d.Blocks.Where(b => b.LiftedAt is null))
            notas.Add($"Bloqueo vigente: {TextosDeInformesDeCartera.ClaseDeBloqueo(b.Kind)} desde el {b.Since.ToString("dd/MM/yyyy", Co)}"
                      + (string.IsNullOrWhiteSpace(b.Reason) ? "." : $" ({b.Reason})."));
        if (filas.Count == 0) notas.Add("La persona no tiene cupo de crédito.");
        return Result.Success(new TablaExportable(titulo, subtitulo, columnas, filas, null, notas));
    }

    // ------------------------------------------------------------------------------------------------------- piezas --

    /// <summary>Los créditos de la persona como deudora y, si se piden, como codeudora (<see cref="ListLoansQuery"/>, con el alcance de quien mira).</summary>
    private async Task<Result<List<(LoanListItemDto Credito, string Rol)>>> CreditosAsync(Guid persona, bool soloDeudor, CancellationToken ct)
    {
        var creditos = new List<(LoanListItemDto, string)>();
        string[] roles = soloDeudor ? ["Debtor"] : ["Debtor", "CoDebtor"];
        foreach (var rol in roles)
        {
            var lista = await sender.Send(new ListLoansQuery(PersonPublicId: persona, Role: rol, PageSize: Pagina), ct);
            if (lista.IsFailure) return Result.Failure<List<(LoanListItemDto, string)>>(lista.Error);
            creditos.AddRange(lista.Value.Items.Select(c => (c, rol)));
        }
        return Result.Success(creditos.DistinctBy(c => c.Item1.PublicId).ToList());
    }

    /// <summary>Todas las páginas de una lista hasta <see cref="Tope"/> filas.</summary>
    private async Task<Result<List<T>>> TodasAsync<T>(Func<int, IRequest<Result<PagedResult<T>>>> pedir, CancellationToken ct)
    {
        var todas = new List<T>();
        for (var pagina = 1; todas.Count < Tope; pagina++)
        {
            var leida = await sender.Send(pedir(pagina), ct);
            if (leida.IsFailure) return Result.Failure<List<T>>(leida.Error);
            todas.AddRange(leida.Value.Items);
            if (leida.Value.Items.Count == 0 || todas.Count >= leida.Value.TotalCount) break;
        }
        return Result.Success(todas.Take(Tope).ToList());
    }

    /// <summary>El día local de un instante guardado en UTC.</summary>
    private static DateOnly DiaLocal(DateTime instante, TimeSpan desfase) =>
        DateOnly.FromDateTime(DateTime.SpecifyKind(instante, DateTimeKind.Unspecified) + desfase);
}
