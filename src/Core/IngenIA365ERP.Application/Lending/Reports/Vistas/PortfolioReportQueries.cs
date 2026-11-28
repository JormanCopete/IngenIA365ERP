using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.PersonStatement;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Schedules;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

// ======================================================================================================== resumen por línea ==

/// <summary>
/// La vista <c>portfolio-summary</c> de <c>/api/reports/lending</c> (2026-11-27, pedido del dueño para la implantación): el consolidado de saldos de la
/// cartera de créditos al corte, una fila por línea —código, nombre, cuenta contable de capital, número de créditos, valor inicial y saldo por
/// concepto—. Sale de <see cref="CarteraAlCorte"/>, el mismo estado del estado de cuenta, así que suma exactamente lo que suma el detalle
/// (<c>portfolio-detail</c>). Cada fila lleva la línea en <c>_linea</c> para abrir su detalle. Es agregada: sin datos del deudor. (nuevo)
/// </summary>
public sealed record PortfolioSummaryReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class PortfolioSummaryReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    CargadorDeReconstruccion cargador,
    ISender sender,
    IDateTimeService reloj) : IRequestHandler<PortfolioSummaryReportQuery, Result<TablaExportable>>
{
    public const string Titulo = "Resumen de saldos de cartera";

    public static readonly VistaDeInformeDeCartera Vista = new("portfolio-summary", Titulo,
        "Saldo de la cartera al corte por línea de crédito: cuenta contable, número de créditos, valor inicial y saldo por concepto. Un clic en la línea "
        + "abre el detalle de sus créditos.", "resumen-de-saldos-de-cartera", ["asOf", "branch", "line", "modality"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Línea"),
        new("Nombre de la línea"),
        new("Cuenta contable de capital"),
        new("Créditos", TipoDeColumna.Entero),
        new("Valor inicial", TipoDeColumna.Moneda),
        new("Capital", TipoDeColumna.Moneda),
        new("Capital vencido", TipoDeColumna.Moneda),
        new("Interés corriente", TipoDeColumna.Moneda),
        new("Interés de mora", TipoDeColumna.Moneda),
        new("Seguro", TipoDeColumna.Moneda),
        new("Administración", TipoDeColumna.Moneda),
        new("Otros cargos", TipoDeColumna.Moneda),
        new("Total adeudado", TipoDeColumna.Moneda),
        new("A favor del asociado", TipoDeColumna.Moneda),
        new("Créditos en mora", TipoDeColumna.Entero),
        new("_linea", Clave: "_linea"),
    ];

    public async Task<Result<TablaExportable>> Handle(PortfolioSummaryReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var creditos = await CarteraAlCorte.LeerAsync(db, cargador, ambito, corte, ct);
        var desembolsado = await ListLoansQueryHandler.DesembolsadoAsync(db, creditos.Select(c => c.Credito.Id).ToList(), ct);
        var (cuentas, notaDeCuentas) = await CuentasDeCapitalAsync(f, ct);

        var filas = creditos.GroupBy(c => (c.LineaCodigo, c.LineaNombre, c.LineaPublicId))
            .OrderBy(g => g.Key.LineaCodigo, StringComparer.Ordinal)
            .Select(g => new FilaExportable(
            [
                g.Key.LineaCodigo, g.Key.LineaNombre, cuentas.GetValueOrDefault(g.Key.LineaCodigo) ?? string.Empty, g.Count(),
                g.Sum(c => desembolsado.GetValueOrDefault(c.Credito.Id).Monto), g.Sum(c => c.Capital), g.Sum(c => c.CapitalVencido),
                g.Sum(c => c.InteresCorriente), g.Sum(c => c.InteresDeMora), g.Sum(c => c.Seguro), g.Sum(c => c.Administracion), g.Sum(c => c.Otros),
                g.Sum(c => c.TotalAdeudado), g.Sum(c => c.AFavor), g.Count(c => c.DiasDeMora > 0), g.Key.LineaPublicId.ToString(),
            ]))
            .ToList();

        var totales = new FilaExportable(Enumerable.Range(0, Columnas.Count).Select<int, object?>(i => i switch
        {
            0 => "Total",
            3 or 14 => filas.Sum(r => (int)r.Valores[i]!),
            >= 4 and <= 13 => filas.Sum(r => (decimal)r.Valores[i]!),
            _ => null,
        }).ToList(), Resaltada: true);

        var notas = new List<string>
        {
            "Saldos del estado de cuenta de cada crédito recalculado al corte (el mismo cálculo que ve la caja). El interés corriente y el de mora incluyen lo que está en suspenso.",
            "Total adeudado = capital + intereses + seguro + administración + otros cargos; lo que está a favor del asociado (anticipos y saldos a favor) va aparte y no se descuenta.",
        };
        if (notaDeCuentas is not null) notas.Add(notaDeCuentas);
        if (CarteraAlCorte.NotaDeErrores(creditos) is { } errores) notas.Add(errores);
        return Result.Success(new TablaExportable(Titulo, $"Al {corte:yyyy-MM-dd} · {creditos.Count} créditos · {ambito.Descripcion()}", Columnas, filas,
            totales, notas));
    }

    /// <summary>
    /// La cuenta contable de capital de cada línea, como la resuelve la matriz contable al corte: se toma de la vista <c>line-balances</c> (la que ya
    /// pregunta a Contabilidad), para no repetir esa consulta. Sin contabilidad o sin regla, vacía y la nota lo dice.
    /// </summary>
    private async Task<(IReadOnlyDictionary<string, string> Cuentas, string? Nota)> CuentasDeCapitalAsync(FiltrosDeInformeDeCartera f, CancellationToken ct)
    {
        var saldos = await sender.Send(new LineBalancesReportQuery(f), ct);
        if (saldos.IsFailure) return (new Dictionary<string, string>(), $"La cuenta contable no se pudo leer: {saldos.Error.Message}");
        var capital = TextosDeInformesDeCartera.Concepto(LoanConcept.Principal);
        var cuentas = saldos.Value.Filas
            .Where(r => r.Seccion == LineBalancesReportQueryHandler.SeccionDeCreditos && Equals(r.Valores[4], capital)
                        && r.Valores[0] is string cuenta && cuenta.Length > 0 && r.Valores[2] is string)
            .GroupBy(r => (string)r.Valores[2]!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(r => (string)r.Valores[0]!).Distinct(StringComparer.Ordinal)),
                StringComparer.OrdinalIgnoreCase);
        return (cuentas, cuentas.Count == 0 ? "Ninguna línea tiene cuenta contable de capital resuelta al corte (contabilidad sin iniciar o sin reglas en la matriz)." : null);
    }
}

// ===================================================================================================== detalle por crédito ==

/// <summary>
/// La vista <c>portfolio-detail</c> (2026-11-27, pedido del dueño): la cartera de créditos al corte, una fila por crédito con lo que muestra el estado de
/// cuenta —documento y nombre del deudor, línea, modalidad, estado, fecha, valor inicial, saldo por concepto, para cancelar, cuota, periodicidad, plazo,
/// tasa, vencimientos, cuotas pagadas, pendientes y vencidas, días de mora, valor vencido y calificación—. Con <c>line</c> es el detalle de una línea
/// (desde el resumen); sin él, el de todas. Trae datos del deudor. (nuevo)
/// </summary>
public sealed record PortfolioDetailReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class PortfolioDetailReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    CargadorDeReconstruccion cargador,
    IDateTimeService reloj) : IRequestHandler<PortfolioDetailReportQuery, Result<TablaExportable>>
{
    public const string Titulo = "Detalle de la cartera por crédito";

    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor("portfolio-detail", Titulo,
        "Cada crédito vivo al corte con los datos de su estado de cuenta: deudor, línea, saldos por concepto, cuota, plazo, tasa, vencimientos, cuotas y mora.",
        "detalle-de-cartera", ["asOf", "branch", "line", "modality"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Tipo de documento"), new("Documento"), new("Nombre completo"),
        new("Línea"), new("Nombre de la línea"), new("Crédito"), new("Clase (modalidad)"), new("Oficina"), new("Estado"),
        new("Fecha", TipoDeColumna.Fecha), new("Valor inicial", TipoDeColumna.Moneda),
        new("Saldo de capital", TipoDeColumna.Moneda), new("Capital vencido", TipoDeColumna.Moneda),
        new("Interés corriente", TipoDeColumna.Moneda), new("Interés de mora", TipoDeColumna.Moneda), new("Seguro", TipoDeColumna.Moneda),
        new("Administración", TipoDeColumna.Moneda), new("Otros cargos", TipoDeColumna.Moneda), new("Total adeudado", TipoDeColumna.Moneda),
        new("A favor", TipoDeColumna.Moneda), new("Para cancelar", TipoDeColumna.Moneda),
        new("Cuota", TipoDeColumna.Moneda), new("Periodicidad"), new("Plazo (cuotas)", TipoDeColumna.Entero), new("Plazo (meses)", TipoDeColumna.Entero),
        new("Tasa E.A. %", TipoDeColumna.Porcentaje), new("Tasa M.V. %", TipoDeColumna.Porcentaje), new("Tipo de cuota"),
        new("Primer vencimiento", TipoDeColumna.Fecha), new("Próximo vencimiento", TipoDeColumna.Fecha), new("Valor próxima cuota", TipoDeColumna.Moneda),
        new("Último vencimiento", TipoDeColumna.Fecha),
        new("Cuotas pagadas", TipoDeColumna.Entero), new("Cuotas pendientes", TipoDeColumna.Entero), new("Cuotas vencidas", TipoDeColumna.Entero),
        new("Días de mora", TipoDeColumna.Entero), new("Valor vencido", TipoDeColumna.Moneda), new("Calificación"), new("Observación"),
        new("_credito", Clave: "_credito"), new("_persona", Clave: "_persona"),
    ];

    /// <summary>Las columnas de moneda que suman en el total (índices).</summary>
    private static readonly int[] Sumables = [10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 36];

    public async Task<Result<TablaExportable>> Handle(PortfolioDetailReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var creditos = await CarteraAlCorte.LeerAsync(db, cargador, ambito, corte, ct);
        var desembolsado = await ListLoansQueryHandler.DesembolsadoAsync(db, creditos.Select(c => c.Credito.Id).ToList(), ct);

        var filas = creditos.OrderBy(c => c.LineaCodigo, StringComparer.Ordinal).ThenBy(c => c.Credito.Number, StringComparer.Ordinal)
            .Select(c => Fila(c, corte, desembolsado.GetValueOrDefault(c.Credito.Id).Monto))
            .ToList();
        var totales = new FilaExportable(Enumerable.Range(0, Columnas.Count).Select<int, object?>(i =>
            i == 0 ? "Total" : i == 5 ? filas.Count : Sumables.Contains(i) ? filas.Sum(r => r.Valores[i] as decimal? ?? 0m) : null).ToList(), Resaltada: true);

        var notas = new List<string>
        {
            "Los saldos son los del estado de cuenta de cada crédito recalculado al corte. Interés corriente y de mora incluyen lo que está en suspenso.",
            "Cuota: la cuota regular del plan. Valor vencido: lo que deben las cuotas vencidas. Calificación: la de la posición diaria.",
        };
        if (CarteraAlCorte.NotaDeErrores(creditos) is { } errores) notas.Add(errores);
        return Result.Success(new TablaExportable(Titulo, $"Al {corte:yyyy-MM-dd} · {creditos.Count} créditos · {ambito.Descripcion()}", Columnas, filas,
            totales, notas));
    }

    private static FilaExportable Fila(CreditoAlCorte c, DateOnly corte, decimal valorInicial)
    {
        var e = c.Estado;
        var t = c.Condiciones;
        var cuotas = e?.Cuotas ?? [];
        var plan = e?.Plan?.Cuotas ?? [];
        decimal Debe(int numero) => e!.Saldos.Where(s => s.Cuota == numero && s.Concepto is not (LoanConcept.Advance or LoanConcept.CreditBalance)).Sum(s => s.Valor);
        var pagadas = e is null ? (int?)null : cuotas.Count(q => q.Vencimiento <= corte && Debe(q.Numero) <= 0m);
        var pendientes = e is null ? (int?)null : cuotas.Count(q => !(q.Vencimiento <= corte && Debe(q.Numero) <= 0m));
        var proxima = e is null ? null
            : cuotas.Where(q => !q.Vencida && Debe(q.Numero) > 0m).OrderBy(q => q.Vencimiento).FirstOrDefault()
              ?? cuotas.Where(q => q.Vencida && Debe(q.Numero) > 0m).OrderBy(q => q.Vencimiento).FirstOrDefault();
        var valorProxima = proxima is null ? (decimal?)null : Debe(proxima.Numero) is > 0m and var debe ? debe : plan.FirstOrDefault(p => p.Numero == proxima.Numero)?.Total;
        var vencido = e is null ? (decimal?)null : cuotas.Where(q => q.Vencida).Sum(q => Math.Max(0m, Debe(q.Numero)));

        return new FilaExportable(
        [
            c.Deudor.TipoDeDocumento, c.Deudor.Documento, c.Deudor.Nombre,
            c.LineaCodigo, c.LineaNombre, c.Credito.Number, c.Posicion?.ModalityCode ?? string.Empty, c.Oficina,
            GetPersonAccountStatementQueryHandler.EstadoDeCredito(c.Credito.Status.ToString()),
            c.Credito.OpenedOn, valorInicial,
            c.Capital, c.CapitalVencido, c.InteresCorriente, c.InteresDeMora, c.Seguro, c.Administracion, c.Otros, c.TotalAdeudado, c.AFavor,
            e?.ValorParaCancelar,
            CuotaRegular(plan, corte),
            t is null ? null : GetPersonAccountStatementQueryHandler.Periodicidad(t.Periodicity), t?.Installments, t?.TermMonths,
            t?.AnnualRate is { } tasa ? decimal.Round(tasa * 100m, 4) : null,
            t is { RateBasis: RateBasis.MonthlyNominal, MonthlyRate: { } mv } ? decimal.Round(mv * 100m, 6) : null,
            t is null ? null : GetPersonAccountStatementQueryHandler.TipoDeCuota(t.AmortizationSystem),
            c.CondicionesIniciales?.FirstDueDate ?? t?.FirstDueDate, proxima?.Vencimiento, valorProxima, plan.Count == 0 ? null : plan[^1].Vencimiento,
            pagadas, pendientes, e?.AlturaDeMora.CuotasVencidas ?? c.Posicion?.InstallmentsPastDue,
            c.DiasDeMora, vencido, c.Calificacion, c.Error ?? string.Empty,
            c.Credito.PublicId.ToString(), c.Deudor.PublicId.ToString(),
        ], c.LineaCodigo.Length == 0 ? null : $"{c.LineaCodigo} {c.LineaNombre}", Resaltada: c.DiasDeMora > 0);
    }

    /// <summary>La cuota regular del plan: el total más frecuente entre las cuotas que no son extraordinarias ni de gracia; con empate, la siguiente por vencer.</summary>
    public static decimal? CuotaRegular(IReadOnlyList<CuotaDelPlan> plan, DateOnly corte)
    {
        var regulares = plan.Where(q => q.Extraordinaria == 0m && q.InteresDeGracia == 0m && !q.SoloInteres).OrderBy(q => q.Vencimiento).ToList();
        if (regulares.Count == 0) return null;
        var grupos = regulares.GroupBy(q => q.Total).Select(g => (Total: g.Key, Veces: g.Count())).ToList();
        var maximo = grupos.Max(g => g.Veces);
        var empatados = grupos.Where(g => g.Veces == maximo).Select(g => g.Total).ToHashSet();
        if (empatados.Count == 1) return empatados.First();
        return (regulares.FirstOrDefault(q => q.Vencimiento >= corte && empatados.Contains(q.Total)) ?? regulares.Last(q => empatados.Contains(q.Total))).Total;
    }
}

// ======================================================================================================== cuotas pendientes ==

/// <summary>
/// La vista <c>pending-installments</c> (2026-11-27, pedido del dueño): cada cuota que todavía debe algo al corte, crédito por crédito, con el valor del
/// plan (capital, interés, seguro, administración, total) y lo que debe por concepto (capital, interés corriente, mora, seguro, administración, otros).
/// Filtro propio <c>status</c>: <c>PastDue</c> (sólo vencidas), <c>Upcoming</c> (sólo por vencer) o vacío (todas). Trae datos del deudor. (nuevo)
/// </summary>
public sealed record PendingInstallmentsReportQuery(FiltrosDeInformeDeCartera Filtros, string? Status) : IRequest<Result<TablaExportable>>;

public sealed class PendingInstallmentsReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    CargadorDeReconstruccion cargador,
    IDateTimeService reloj) : IRequestHandler<PendingInstallmentsReportQuery, Result<TablaExportable>>
{
    public const string Titulo = "Cuotas pendientes";
    public const string Vencidas = "PastDue";
    public const string PorVencer = "Upcoming";

    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor("pending-installments", Titulo,
        "Cada cuota con saldo al corte: deudor, línea, crédito, vencimiento, valor del plan y lo que debe por concepto. Filtre por vencidas o por vencer.",
        "cuotas-pendientes", ["asOf", "branch", "line", "modality"], ["status"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Tipo de documento"), new("Documento"), new("Nombre completo"),
        new("Línea"), new("Nombre de la línea"), new("Crédito"), new("Clase (modalidad)"), new("Oficina"),
        new("Cuota", TipoDeColumna.Entero), new("Vencimiento", TipoDeColumna.Fecha), new("Fecha exigible", TipoDeColumna.Fecha), new("Estado"),
        new("Días vencida", TipoDeColumna.Entero),
        new("Capital del plan", TipoDeColumna.Moneda), new("Interés del plan", TipoDeColumna.Moneda), new("Seguro del plan", TipoDeColumna.Moneda),
        new("Administración del plan", TipoDeColumna.Moneda), new("Valor de la cuota", TipoDeColumna.Moneda),
        new("Debe capital", TipoDeColumna.Moneda), new("Debe interés corriente", TipoDeColumna.Moneda), new("Debe interés de mora", TipoDeColumna.Moneda),
        new("Debe seguro", TipoDeColumna.Moneda), new("Debe administración", TipoDeColumna.Moneda), new("Debe otros cargos", TipoDeColumna.Moneda),
        new("Total pendiente", TipoDeColumna.Moneda),
        new("_credito", Clave: "_credito"), new("_persona", Clave: "_persona"),
    ];

    public async Task<Result<TablaExportable>> Handle(PendingInstallmentsReportQuery request, CancellationToken ct)
    {
        var estado = FiltrosDeInformeDeCartera.Elegir("status", request.Status, ["All", Vencidas, PorVencer], "All");
        if (estado.IsFailure) return Result.Failure<TablaExportable>(estado.Error);
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var creditos = await CarteraAlCorte.LeerAsync(db, cargador, ambito, corte, ct);

        var filas = new List<FilaExportable>();
        foreach (var c in creditos.OrderBy(c => c.LineaCodigo, StringComparer.Ordinal).ThenBy(c => c.Credito.Number, StringComparer.Ordinal))
        {
            if (c.Estado is not { } e) continue;
            var plan = (e.Plan?.Cuotas ?? []).ToDictionary(p => p.Numero);
            foreach (var q in e.Cuotas.OrderBy(q => q.Numero))
            {
                decimal Debe(params LoanConcept[] conceptos) => e.Saldos.Where(s => s.Cuota == q.Numero && conceptos.Contains(s.Concepto)).Sum(s => s.Valor);
                var capital = Debe(LoanConcept.Principal);
                var corriente = Debe(LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense);
                var mora = Debe(LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense);
                var seguro = Debe(LoanConcept.Insurance);
                var administracion = Debe(LoanConcept.Administration);
                var otros = Debe(LoanConcept.CollectionFee, LoanConcept.OtherCharge);
                var total = capital + corriente + mora + seguro + administracion + otros;
                if (total <= 0m) continue;
                if (estado.Value == Vencidas && !q.Vencida) continue;
                if (estado.Value == PorVencer && q.Vencida) continue;
                plan.TryGetValue(q.Numero, out var p);
                filas.Add(new FilaExportable(
                [
                    c.Deudor.TipoDeDocumento, c.Deudor.Documento, c.Deudor.Nombre, c.LineaCodigo, c.LineaNombre, c.Credito.Number,
                    c.Posicion?.ModalityCode ?? string.Empty, c.Oficina,
                    q.Numero, q.Vencimiento, q.FechaExigible, q.Vencida ? "Vencida" : "Por vencer",
                    q.Vencida ? Math.Max(0, corte.DayNumber - q.FechaExigible.DayNumber) : 0,
                    p?.Capital, p?.Interes, p?.Seguro, p?.Administracion, p?.Total,
                    capital, corriente, mora, seguro, administracion, otros, total,
                    c.Credito.PublicId.ToString(), c.Deudor.PublicId.ToString(),
                ], $"{c.LineaCodigo} {c.LineaNombre}", Resaltada: q.Vencida));
            }
        }

        int[] sumables = [18, 19, 20, 21, 22, 23, 24];
        var totales = new FilaExportable(Enumerable.Range(0, Columnas.Count).Select<int, object?>(i =>
            i == 0 ? "Total" : i == 8 ? filas.Count : sumables.Contains(i) ? filas.Sum(r => (decimal)r.Valores[i]!) : null).ToList(), Resaltada: true);
        var notas = new List<string>
        {
            "Lo que debe cada cuota es el saldo del estado de cuenta recalculado al corte; el interés de las cuotas por vencer es sólo lo causado hasta el corte.",
            "Interés corriente y de mora incluyen lo que está en suspenso. Una cuota cubierta por anticipo no aparece.",
        };
        if (CarteraAlCorte.NotaDeErrores(creditos) is { } errores) notas.Add(errores + " Esos créditos no aparecen en este informe.");
        var titulo = estado.Value switch { Vencidas => "vencidas", PorVencer => "por vencer", _ => "todas" };
        return Result.Success(new TablaExportable(Titulo, $"Al {corte:yyyy-MM-dd} · {titulo} · {filas.Count} cuotas · {ambito.Descripcion()}", Columnas, filas,
            totales, notas));
    }
}
