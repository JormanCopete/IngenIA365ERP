using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.PersonStatement;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.PersonStatement;

/// <summary>La persona del estado de cuenta.</summary>
public sealed record PersonaDelEstadoDeCuentaDto(Guid PublicId, string Nombre, string Documento, string? Correo, string? Celular, bool EsAsociado);

/// <summary>
/// Un producto de la persona en el estado de cuenta: un crédito (como deudor o codeudor) o una cuenta (aportes, ahorro, CDAT) de la que es titular.
/// Lo que no aplica al producto va nulo (una cuenta no tiene cuota ni tasa del plan).
/// </summary>
public sealed record ProductoDelEstadoDeCuentaDto(
    string Clase,
    Guid PublicId,
    string Numero,
    string Producto,
    string Naturaleza,
    string Rol,
    string Estado,
    string? Oficina,
    DateOnly? Fecha,
    decimal? ValorInicial,
    decimal Saldo,
    decimal? ParaCancelar,
    decimal? Cuota,
    string? Periodicidad,
    int? Plazo,
    int? PlazoMeses,
    decimal? Tasa,
    string? TipoDeCuota,
    DateOnly? PrimerVencimiento,
    DateOnly? ProximoVencimiento,
    decimal? ValorProximaCuota,
    DateOnly? UltimoVencimiento,
    int? CuotasPagadas,
    int? CuotasPendientes,
    int? CuotasVencidas,
    int? CuotasAnticipadas,
    int? CuotasExtras,
    int? DiasDeMora,
    decimal? ValorVencido,
    decimal? InteresCorriente,
    decimal? InteresDeMora,
    decimal? Seguro,
    decimal? Anticipo,
    decimal? Disponible,
    string? Calificacion);

/// <summary>Los totales del estado de cuenta: lo que debe (créditos como deudor) y lo que tiene (cuentas).</summary>
public sealed record TotalesDelEstadoDeCuentaDto(decimal SaldoDeCreditos, decimal ParaCancelar, decimal ValorVencido, int DiasDeMoraMaximos,
    decimal SaldoEnAportes, decimal SaldoEnAhorros);

public sealed record EstadoDeCuentaDeLaPersonaDto(PersonaDelEstadoDeCuentaDto Persona, DateOnly Fecha, IReadOnlyList<ProductoDelEstadoDeCuentaDto> Productos,
    TotalesDelEstadoDeCuentaDto Totales, IReadOnlyList<string> Avisos);

/// <summary>
/// <c>GET /api/lending/person-statement?personPublicId=&amp;date=&amp;includeClosed=</c> (2026-11-13, pedido del dueño; <c>Lending.Loans.Read</c>): el
/// estado de cuenta de una persona en Cartera —sus créditos como deudor y como codeudor y sus cuentas de aportes y ahorro como titular— con los datos
/// básicos de cada uno (saldo, valor inicial, fecha, cuota, periodicidad, plazo, tasa, tipo de cuota, vencimientos, cuotas y mora). No calcula nada
/// propio: cada crédito sale de su estado de cuenta (<see cref="GetLoanStatementQuery"/>), su plan vigente (<see cref="GetRepaymentScheduleQuery"/>) y
/// su última versión de condiciones; cada cuenta, del listado de cuentas. Respeta el alcance de oficinas y líneas de quien mira, porque los listados
/// que usa lo respetan. Sin <paramref name="IncludeClosed"/> deja fuera los créditos cancelados, anulados o novados y las cuentas cerradas. (nuevo)
/// </summary>
public sealed record GetPersonAccountStatementQuery(Guid PersonPublicId, DateOnly? Date = null, bool IncludeClosed = false)
    : IRequest<Result<EstadoDeCuentaDeLaPersonaDto>>;

public sealed class GetPersonAccountStatementQueryHandler(IApplicationDbContext db, ISender sender, IDateTimeService reloj)
    : IRequestHandler<GetPersonAccountStatementQuery, Result<EstadoDeCuentaDeLaPersonaDto>>
{
    private const int Tope = 200;

    public async Task<Result<EstadoDeCuentaDeLaPersonaDto>> Handle(GetPersonAccountStatementQuery request, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == request.PersonPublicId)
            .Select(p => new
            {
                p.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName, p.TaxId, p.Email, p.Mobile, p.IsAssociate,
            })
            .FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<EstadoDeCuentaDeLaPersonaDto>(new Error("Lending.PersonStatement.PersonNotFound", "La persona no existe."));

        var fecha = request.Date ?? reloj.HoyLocal;
        var nombre = !string.IsNullOrWhiteSpace(persona.BusinessName)
            ? persona.BusinessName!
            : string.Join(' ', new[] { persona.FirstName, persona.OtherNames, persona.LastName, persona.SecondLastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var avisos = new List<string>();
        var productos = new List<ProductoDelEstadoDeCuentaDto>();

        foreach (var rol in new[] { "Debtor", "CoDebtor" })
        {
            var lista = await sender.Send(new ListLoansQuery(PersonPublicId: request.PersonPublicId, Role: rol, PageSize: Tope), ct);
            if (lista.IsFailure) return Result.Failure<EstadoDeCuentaDeLaPersonaDto>(lista.Error);
            foreach (var credito in lista.Value.Items)
            {
                if (!request.IncludeClosed && credito.Status is nameof(LoanStatus.PaidOff) or nameof(LoanStatus.Voided) or nameof(LoanStatus.Novated)) continue;
                var producto = await CreditoAsync(credito, rol == "Debtor" ? "Deudor" : "Codeudor", fecha, avisos, ct);
                productos.Add(producto);
            }
        }

        var cuentas = await sender.Send(new ListMemberAccountsQuery(PersonPublicId: request.PersonPublicId, PageSize: Tope), ct);
        if (cuentas.IsSuccess)
        {
            var ids = cuentas.Value.Items.Select(c => c.PublicId).ToList();
            var aperturas = await db.MemberAccounts.AsNoTracking().Where(a => ids.Contains(a.PublicId))
                .ToDictionaryAsync(a => a.PublicId, a => a.OpenedOn, ct);
            foreach (var c in cuentas.Value.Items)
            {
                if (!request.IncludeClosed && c.Status is MemberAccountStatus.Closed) continue;
                productos.Add(new ProductoDelEstadoDeCuentaDto(
                    "Cuenta", c.PublicId, c.Number, $"{c.Line.Code} · {c.Line.Name}", Naturaleza(c.Nature, c.DepositClass), "Titular",
                    EstadoDeCuenta(c.Status), c.Office?.Name, aperturas.TryGetValue(c.PublicId, out var abierta) ? abierta : null,
                    null, c.Balance, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
                    null, null, null, null, c.Available, null));
            }
        }
        else
        {
            // Sin permiso de cuentas o sin Captaciones activada: el estado de cuenta sale con los créditos y lo dice.
            avisos.Add($"Las cuentas de aportes y ahorro no se pudieron leer: {cuentas.Error.Message}");
        }

        var deudor = productos.Where(p => p.Clase == "Credito" && p.Rol == "Deudor").ToList();
        var totales = new TotalesDelEstadoDeCuentaDto(
            deudor.Sum(p => p.Saldo), deudor.Sum(p => p.ParaCancelar ?? 0m), deudor.Sum(p => p.ValorVencido ?? 0m),
            deudor.Select(p => p.DiasDeMora ?? 0).DefaultIfEmpty(0).Max(),
            productos.Where(p => p.Clase == "Cuenta" && p.Naturaleza == NaturalezaAportes).Sum(p => p.Saldo),
            productos.Where(p => p.Clase == "Cuenta" && p.Naturaleza != NaturalezaAportes).Sum(p => p.Saldo));

        return Result.Success(new EstadoDeCuentaDeLaPersonaDto(
            new PersonaDelEstadoDeCuentaDto(persona.PublicId, nombre, persona.TaxId, persona.Email, persona.Mobile, persona.IsAssociate),
            fecha, productos, totales, avisos));
    }

    private async Task<ProductoDelEstadoDeCuentaDto> CreditoAsync(LoanListItemDto c, string rol, DateOnly fecha, List<string> avisos, CancellationToken ct)
    {
        var condiciones = await db.LoanTermsVersions.AsNoTracking()
            .Where(t => db.Loans.Any(l => l.Id == t.LoanId && l.PublicId == c.PublicId))
            .OrderByDescending(t => t.VersionNumber)
            .Select(t => new { t.AnnualRate, t.Periodicity, t.Installments, t.TermMonths, t.AmortizationSystem, t.FirstDueDate })
            .FirstOrDefaultAsync(ct);
        var primera = await db.LoanTermsVersions.AsNoTracking()
            .Where(t => db.Loans.Any(l => l.Id == t.LoanId && l.PublicId == c.PublicId) && t.VersionNumber == 1)
            .Select(t => (DateOnly?)t.FirstDueDate).FirstOrDefaultAsync(ct);
        var apertura = await db.Loans.AsNoTracking().Where(l => l.PublicId == c.PublicId).Select(l => (DateOnly?)l.OpenedOn).FirstOrDefaultAsync(ct);

        LoanStatementDto? estado = null;
        RepaymentScheduleDto? plan = null;
        if (c.Status is not (nameof(LoanStatus.PendingDisbursement) or nameof(LoanStatus.Voided)))
        {
            var leido = await sender.Send(new GetLoanStatementQuery(c.PublicId, fecha), ct);
            if (leido.IsSuccess) estado = leido.Value;
            else avisos.Add($"El crédito {c.Number}: {leido.Error.Message}");
            var planLeido = await sender.Send(new GetRepaymentScheduleQuery(c.PublicId), ct);
            if (planLeido.IsSuccess) plan = planLeido.Value;
        }

        decimal? Saldo(params string[] conceptos) => estado is null ? null : conceptos.Sum(k => estado.Balances.GetValueOrDefault(k));
        var cuotas = estado?.Installments ?? [];
        var proxima = cuotas.Where(q => q.Status is "Upcoming" or "PastDue").OrderBy(q => q.DueDate).FirstOrDefault(q => q.Status == "Upcoming")
                      ?? cuotas.Where(q => q.Status == "PastDue").OrderBy(q => q.DueDate).FirstOrDefault();
        var regular = plan?.Installments.Where(q => !q.IsExtraordinary && !q.IsGraceInstallment).Select(q => (decimal?)q.Total).FirstOrDefault();

        return new ProductoDelEstadoDeCuentaDto(
            "Credito", c.PublicId, c.Number, $"{c.Line.Code} · {c.Line.Name}", "Crédito", rol, EstadoDeCredito(c.Status), c.Office.Name, apertura,
            c.DisbursedAmount, Saldo("Principal") ?? c.PrincipalBalance, estado?.ToPayOff, regular,
            condiciones is null ? null : Periodicidad(condiciones.Periodicity), condiciones?.Installments, condiciones?.TermMonths,
            condiciones?.AnnualRate is { } tasa ? decimal.Round(tasa * 100m, 4) : null,
            condiciones is null ? null : TipoDeCuota(condiciones.AmortizationSystem),
            primera ?? condiciones?.FirstDueDate, proxima?.DueDate, proxima?.Owed.Values.Sum() is > 0m and var debe ? debe : proxima?.Total,
            plan?.Installments.Select(q => (DateOnly?)q.DueDate).LastOrDefault(),
            estado is null ? null : cuotas.Count(q => q.Status == "Paid"),
            estado is null ? null : cuotas.Count(q => q.Status is "Upcoming" or "PastDue"),
            estado?.InstallmentsPastDue,
            estado is null ? null : cuotas.Count(q => q.Status == "Covered"),
            plan?.Installments.Count(q => q.IsExtraordinary),
            estado?.DaysPastDue ?? c.DaysPastDue,
            estado is null ? null : cuotas.Where(q => q.Status == "PastDue").Sum(q => q.Owed.Values.Sum()),
            Saldo("CurrentInterest", "CurrentInterestInSuspense"), Saldo("DefaultInterest", "DefaultInterestInSuspense"), Saldo("Insurance"),
            Saldo("Advance", "CreditBalance"), null, estado?.OfficialRating ?? estado?.InformativeRating ?? c.InformativeRating);
    }

    public const string NaturalezaAportes = "Aportes";

    public static string Naturaleza(LineNature naturaleza, DepositClass? clase) => naturaleza switch
    {
        LineNature.Contribution => NaturalezaAportes,
        LineNature.Deposit => clase switch
        {
            DepositClass.Demand => "Ahorro a la vista",
            DepositClass.Contractual => "Ahorro contractual",
            DepositClass.Permanent => "Ahorro permanente",
            DepositClass.TermDeposit => "CDAT",
            _ => "Ahorro",
        },
        _ => "Crédito",
    };

    public static string EstadoDeCredito(string estado) => estado switch
    {
        nameof(LoanStatus.PendingDisbursement) => "Por desembolsar",
        nameof(LoanStatus.Active) => "Vigente",
        nameof(LoanStatus.PaidOff) => "Cancelado",
        nameof(LoanStatus.WrittenOff) => "Castigado",
        nameof(LoanStatus.Voided) => "Anulado",
        nameof(LoanStatus.Novated) => "Novado",
        _ => estado,
    };

    public static string EstadoDeCuenta(MemberAccountStatus estado) => estado switch
    {
        MemberAccountStatus.Opening => "En apertura",
        MemberAccountStatus.Active => "Activa",
        MemberAccountStatus.Inactive => "Inactiva",
        MemberAccountStatus.Matured => "Vencida",
        MemberAccountStatus.Closing => "En cierre",
        MemberAccountStatus.Closed => "Cerrada",
        _ => estado.ToString(),
    };

    public static string Periodicidad(RepaymentPeriodicity periodicidad) => periodicidad switch
    {
        RepaymentPeriodicity.SinglePayment => "Pago único",
        RepaymentPeriodicity.Weekly => "Semanal",
        RepaymentPeriodicity.TenDay => "Decadal",
        RepaymentPeriodicity.Biweekly => "Quincenal",
        RepaymentPeriodicity.Monthly => "Mensual",
        RepaymentPeriodicity.Bimonthly => "Bimestral",
        RepaymentPeriodicity.Quarterly => "Trimestral",
        RepaymentPeriodicity.SemiAnnual => "Semestral",
        RepaymentPeriodicity.Annual => "Anual",
        _ => periodicidad.ToString(),
    };

    public static string TipoDeCuota(AmortizationSystem sistema) => sistema switch
    {
        AmortizationSystem.LevelPayment => "Cuota fija",
        AmortizationSystem.ConstantPrincipal => "Abono constante a capital",
        AmortizationSystem.Bullet => "Pago único",
        _ => sistema.ToString(),
    };
}

/// <summary>
/// La tabla exportable del estado de cuenta, con las columnas que eligió quien lo mira (<see cref="ColumnasDelEstadoDeCuenta"/>): la usan la vista
/// <c>person-statement</c> del centro de informes (Excel, PDF, Word e impresión) y el envío por correo. (nuevo)
/// </summary>
public static class TablaDelEstadoDeCuenta
{
    public const string Titulo = "Estado de cuenta";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    private static readonly IReadOnlyDictionary<string, (TipoDeColumna Tipo, Func<ProductoDelEstadoDeCuentaDto, object?> Valor)> Definiciones =
        new Dictionary<string, (TipoDeColumna, Func<ProductoDelEstadoDeCuentaDto, object?>)>(StringComparer.Ordinal)
        {
            ["producto"] = (TipoDeColumna.Texto, p => p.Producto),
            ["numero"] = (TipoDeColumna.Texto, p => p.Numero),
            ["rol"] = (TipoDeColumna.Texto, p => p.Rol),
            ["estado"] = (TipoDeColumna.Texto, p => p.Estado),
            ["fecha"] = (TipoDeColumna.Fecha, p => p.Fecha),
            ["valorInicial"] = (TipoDeColumna.Moneda, p => p.ValorInicial),
            ["saldo"] = (TipoDeColumna.Moneda, p => p.Saldo),
            ["paraCancelar"] = (TipoDeColumna.Moneda, p => p.ParaCancelar),
            ["cuota"] = (TipoDeColumna.Moneda, p => p.Cuota),
            ["periodicidad"] = (TipoDeColumna.Texto, p => p.Periodicidad),
            ["plazo"] = (TipoDeColumna.Entero, p => p.Plazo),
            ["plazoMeses"] = (TipoDeColumna.Entero, p => p.PlazoMeses),
            ["tasa"] = (TipoDeColumna.Porcentaje, p => p.Tasa),
            ["tipoDeCuota"] = (TipoDeColumna.Texto, p => p.TipoDeCuota),
            ["primerVencimiento"] = (TipoDeColumna.Fecha, p => p.PrimerVencimiento),
            ["proximoVencimiento"] = (TipoDeColumna.Fecha, p => p.ProximoVencimiento),
            ["valorProximaCuota"] = (TipoDeColumna.Moneda, p => p.ValorProximaCuota),
            ["ultimoVencimiento"] = (TipoDeColumna.Fecha, p => p.UltimoVencimiento),
            ["cuotasPagadas"] = (TipoDeColumna.Entero, p => p.CuotasPagadas),
            ["cuotasPendientes"] = (TipoDeColumna.Entero, p => p.CuotasPendientes),
            ["cuotasVencidas"] = (TipoDeColumna.Entero, p => p.CuotasVencidas),
            ["cuotasAnticipadas"] = (TipoDeColumna.Entero, p => p.CuotasAnticipadas),
            ["cuotasExtras"] = (TipoDeColumna.Entero, p => p.CuotasExtras),
            ["diasDeMora"] = (TipoDeColumna.Entero, p => p.DiasDeMora),
            ["valorVencido"] = (TipoDeColumna.Moneda, p => p.ValorVencido),
            ["interesCorriente"] = (TipoDeColumna.Moneda, p => p.InteresCorriente),
            ["interesDeMora"] = (TipoDeColumna.Moneda, p => p.InteresDeMora),
            ["seguro"] = (TipoDeColumna.Moneda, p => p.Seguro),
            ["anticipo"] = (TipoDeColumna.Moneda, p => p.Anticipo),
            ["disponible"] = (TipoDeColumna.Moneda, p => p.Disponible),
            ["calificacion"] = (TipoDeColumna.Texto, p => p.Calificacion),
            ["oficina"] = (TipoDeColumna.Texto, p => p.Oficina),
        };

    /// <summary>Los códigos del catálogo que tienen definición: los deja ver una prueba para que el catálogo y la tabla no se separen.</summary>
    public static IReadOnlyCollection<string> CodigosDefinidos => [.. Definiciones.Keys];

    public static TablaExportable Armar(EstadoDeCuentaDeLaPersonaDto e, IEnumerable<string>? columnas)
    {
        var elegidas = ColumnasDelEstadoDeCuenta.Normalizar(columnas).Where(Definiciones.ContainsKey).ToList();
        var nombres = ColumnasDelEstadoDeCuenta.Todas.ToDictionary(c => c.Codigo, c => c.Nombre, StringComparer.Ordinal);
        var columnasExportables = new List<ColumnaExportable> { new("Clase") };
        columnasExportables.AddRange(elegidas.Select(c => new ColumnaExportable(nombres[c], Definiciones[c].Tipo, c)));

        var filas = e.Productos
            .OrderBy(p => p.Clase == "Credito" ? 0 : 1).ThenBy(p => p.Rol).ThenBy(p => p.Numero, StringComparer.Ordinal)
            .Select(p => new FilaExportable([p.Clase == "Credito" ? "Crédito" : p.Naturaleza, .. elegidas.Select(c => Definiciones[c].Valor(p))]))
            .ToList();

        var t = e.Totales;
        var notas = new List<string>
        {
            string.Create(Cultura, $"Créditos como deudor: saldo de capital {t.SaldoDeCreditos:N0}; total para cancelar {t.ParaCancelar:N0}; vencido {t.ValorVencido:N0}; mora máxima {t.DiasDeMoraMaximos} días."),
            string.Create(Cultura, $"Aportes: {t.SaldoEnAportes:N0}. Ahorros: {t.SaldoEnAhorros:N0}."),
        };
        notas.AddRange(e.Avisos);
        var subtitulo = $"{e.Persona.Nombre} · {e.Persona.Documento} · al {e.Fecha:dd/MM/yyyy}";
        return new TablaExportable(Titulo, subtitulo, columnasExportables, filas, null, notas);
    }
}

/// <summary>
/// La vista <c>person-statement</c> de <c>/api/reports/lending</c> (2026-11-13): el estado de cuenta de una persona para exportar, imprimir o enviar,
/// con los filtros propios <c>person</c> (obligatorio), <c>columns</c> (códigos separados por coma; vacío = los de por defecto) e <c>includeClosed</c>,
/// y la fecha <c>asOf</c>. Trae datos de la persona. (nuevo)
/// </summary>
public sealed record PersonStatementReportQuery(FiltrosDeInformeDeCartera Filtros, Guid? Persona, string? Columnas, bool IncluirCerrados)
    : IRequest<Result<TablaExportable>>;

public sealed class PersonStatementReportQueryHandler(ISender sender, IDateTimeService reloj)
    : IRequestHandler<PersonStatementReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = new("person-statement", TablaDelEstadoDeCuenta.Titulo,
        "El estado de cuenta de una persona: sus créditos, aportes y ahorros con las columnas que elija.", "estado-de-cuenta", ["asOf"],
        ["person", "columns", "includeClosed"], PersonalData: true, RequiredPermission: "Lending.Loans.Read");

    public async Task<Result<TablaExportable>> Handle(PersonStatementReportQuery request, CancellationToken ct)
    {
        if (request.Persona is not { } persona)
            return Result.Failure<TablaExportable>(new Error("Lending.Report.FilterInvalid", "Indique la persona del estado de cuenta (person)."));
        var estado = await sender.Send(new GetPersonAccountStatementQuery(persona, request.Filtros.ALaFecha(reloj.HoyLocal), request.IncluirCerrados), ct);
        if (estado.IsFailure) return Result.Failure<TablaExportable>(estado.Error);
        var columnas = (request.Columnas ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Result.Success(TablaDelEstadoDeCuenta.Armar(estado.Value, columnas));
    }
}
