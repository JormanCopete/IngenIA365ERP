using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
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
    string? Calificacion)
{
    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 2): el saldo de la administración del crédito, aparte del seguro (nulo en una cuenta). (nuevo)</summary>
    public decimal? Administracion { get; init; }

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 4): lo reconocido al inicio del ciclo y todavía no causado de la cuota del período (nulo en una cuenta;
    /// cero sin reconocimientos): con lo causado, la cuota completa que se ofrece para pagar. (nuevo)
    /// </summary>
    public decimal? ReconocidoPorCausar { get; init; }

    /// <summary>2026-11-24 (feature 025, HU2; FR-012): la tasa mensual vencida pactada, en puntos (1,8 = 1,8 % MV); nula si se pactó en E.A. (nuevo)</summary>
    public decimal? TasaMensual { get; init; }
}

/// <summary>
/// Los totales del estado de cuenta: lo que debe (créditos como deudor) y lo que tiene (cuentas). 2026-11-16 (pedido del dueño): el cupo de crédito
/// de la persona (<see cref="CupoAsignado"/> y <see cref="CupoDisponible"/>: el consolidado si lo tiene, si no la suma de los por línea y rotativos;
/// nulos sin cupo o si no se pudo leer) y el <b>resumen de conceptos</b>: lo que debe hoy por concepto en sus créditos como deudor —«otros» son los
/// gastos de cobranza y los demás cargos—; nulos si no es deudora de ningún crédito del estado de cuenta.
/// </summary>
public sealed record TotalesDelEstadoDeCuentaDto(decimal SaldoDeCreditos, decimal ParaCancelar, decimal ValorVencido, int DiasDeMoraMaximos,
    decimal SaldoEnAportes, decimal SaldoEnAhorros, decimal? CupoAsignado = null, decimal? CupoDisponible = null,
    decimal? ConceptoCapital = null, decimal? ConceptoInteresCorriente = null, decimal? ConceptoInteresDeMora = null, decimal? ConceptoSeguro = null,
    decimal? ConceptoOtros = null)
{
    /// <summary>¿Lleva el resumen de conceptos?</summary>
    public bool ConResumenDeConceptos => ConceptoCapital is not null;

    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 2): la administración del resumen, aparte del seguro y de los otros. (nuevo)</summary>
    public decimal? ConceptoAdministracion { get; init; }

    /// <summary>La suma de los seis conceptos (antes de descontar el anticipo: eso es <see cref="ParaCancelar"/>).</summary>
    public decimal TotalDeConceptos => (ConceptoCapital ?? 0m) + (ConceptoInteresCorriente ?? 0m) + (ConceptoInteresDeMora ?? 0m) + (ConceptoSeguro ?? 0m)
        + (ConceptoAdministracion ?? 0m) + (ConceptoOtros ?? 0m);
}

/// <summary>
/// El membrete de lo que imprime Cartera (2026-11-16, pedido del dueño): el nombre y el NIT con dígito de la cooperativa y el logotipo vigente de
/// Cartera como <c>data:image/…;base64,…</c> (nulo si no hay o no se pudo leer). Lo arma <see cref="MembreteDeCartera"/>, el mismo de los documentos
/// emitidos. (nuevo)
/// </summary>
public sealed record MembreteDto(string Cooperativa, string? Nit, string? LogoDataUri);

/// <summary>El estado de cuenta de la persona. 2026-11-16: con el <see cref="Membrete"/> para imprimirlo.</summary>
public sealed record EstadoDeCuentaDeLaPersonaDto(PersonaDelEstadoDeCuentaDto Persona, DateOnly Fecha, IReadOnlyList<ProductoDelEstadoDeCuentaDto> Productos,
    TotalesDelEstadoDeCuentaDto Totales, IReadOnlyList<string> Avisos, MembreteDto? Membrete = null);

/// <summary>
/// Lo que comparten el estado de cuenta, sus secciones y la simulación de cancelación (2026-11-16): la persona con su nombre completo y el membrete.
/// (nuevo)
/// </summary>
public static class LecturasDelEstadoDeCuenta
{
    public const string CodigoPersonaInexistente = "Lending.PersonStatement.PersonNotFound";

    public static Error PersonaInexistente() => new(CodigoPersonaInexistente, "La persona no existe.");

    /// <summary>La persona (también eliminada: su historia se sigue consultando), o nula si no existe.</summary>
    public static async Task<PersonaDelEstadoDeCuentaDto?> PersonaAsync(IApplicationDbContext db, Guid personPublicId, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == personPublicId)
            .Select(p => new
            {
                p.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName, p.TaxId, p.Email, p.Mobile, p.IsAssociate,
            })
            .FirstOrDefaultAsync(ct);
        if (persona is null) return null;
        var nombre = !string.IsNullOrWhiteSpace(persona.BusinessName)
            ? persona.BusinessName!
            : string.Join(' ', new[] { persona.FirstName, persona.OtherNames, persona.LastName, persona.SecondLastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new PersonaDelEstadoDeCuentaDto(persona.PublicId, nombre, persona.TaxId, persona.Email, persona.Mobile, persona.IsAssociate);
    }

    /// <summary>El membrete: nombre y NIT de la cooperativa y el logotipo vigente de Cartera como data URI. Un logotipo que no se lee no impide nada.</summary>
    public static async Task<MembreteDto> MembreteAsync(IApplicationDbContext db, IBlobStore store, IAttachmentCipher? cifrado, CancellationToken ct)
    {
        var (cooperativa, nit) = await MembreteDeCartera.CooperativaAsync(db, ct);
        var logotipo = await MembreteDeCartera.ImagenVigenteAsync(db, store, cifrado, AdjuntosDeModulo.LogotipoDeCartera, ct);
        return new MembreteDto(cooperativa, nit, logotipo?.DataUri);
    }
}

/// <summary>
/// <c>GET /api/lending/person-statement?personPublicId=&amp;date=&amp;includeClosed=</c> (2026-11-13, pedido del dueño; <c>Lending.Loans.Read</c>): el
/// estado de cuenta de una persona en Cartera —sus créditos como deudor y como codeudor y sus cuentas de aportes y ahorro como titular— con los datos
/// básicos de cada uno (saldo, valor inicial, fecha, cuota, periodicidad, plazo, tasa, tipo de cuota, vencimientos, cuotas y mora). No calcula nada
/// propio: cada crédito sale de su estado de cuenta (<see cref="GetLoanStatementQuery"/>), su plan vigente (<see cref="GetRepaymentScheduleQuery"/>) y
/// su última versión de condiciones; cada cuenta, del listado de cuentas. Respeta el alcance de oficinas y líneas de quien mira, porque los listados
/// que usa lo respetan. Sin <paramref name="IncludeClosed"/> deja fuera los créditos cancelados, anulados o novados y las cuentas cerradas. (nuevo)
/// <para>2026-11-16 (pedido del dueño): con el membrete (<see cref="MembreteDto"/>), el cupo de crédito (<see cref="GetCreditLimitAvailabilityQuery"/>;
/// si no se puede leer, nulo y un aviso) y el resumen de conceptos de sus créditos como deudor (de los saldos de cada estado de cuenta).</para>
/// </summary>
public sealed record GetPersonAccountStatementQuery(Guid PersonPublicId, DateOnly? Date = null, bool IncludeClosed = false)
    : IRequest<Result<EstadoDeCuentaDeLaPersonaDto>>;

public sealed class GetPersonAccountStatementQueryValidator : AbstractValidator<GetPersonAccountStatementQuery>
{
    public GetPersonAccountStatementQueryValidator() => RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Indique la persona.");
}

public sealed class GetPersonAccountStatementQueryHandler(IApplicationDbContext db, ISender sender, IDateTimeService reloj, IBlobStore store,
    IAttachmentCipher? cifrado = null)
    : IRequestHandler<GetPersonAccountStatementQuery, Result<EstadoDeCuentaDeLaPersonaDto>>
{
    private const int Tope = 200;

    /// <summary>Los conceptos que van en «otros» del resumen: todo lo cobrable que no es capital, interés ni seguro.</summary>
    public static readonly IReadOnlySet<string> ConceptosDeOtros = new HashSet<string>(StringComparer.Ordinal)
        { nameof(LoanConcept.CollectionFee), nameof(LoanConcept.OtherCharge) };

    public async Task<Result<EstadoDeCuentaDeLaPersonaDto>> Handle(GetPersonAccountStatementQuery request, CancellationToken ct)
    {
        var persona = await LecturasDelEstadoDeCuenta.PersonaAsync(db, request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<EstadoDeCuentaDeLaPersonaDto>(LecturasDelEstadoDeCuenta.PersonaInexistente());

        var fecha = request.Date ?? reloj.HoyLocal;
        var avisos = new List<string>();
        var productos = new List<ProductoDelEstadoDeCuentaDto>();
        var otrosComoDeudor = 0m;

        foreach (var rol in new[] { "Debtor", "CoDebtor" })
        {
            var lista = await sender.Send(new ListLoansQuery(PersonPublicId: request.PersonPublicId, Role: rol, PageSize: Tope), ct);
            if (lista.IsFailure) return Result.Failure<EstadoDeCuentaDeLaPersonaDto>(lista.Error);
            foreach (var credito in lista.Value.Items)
            {
                if (!request.IncludeClosed && credito.Status is nameof(LoanStatus.PaidOff) or nameof(LoanStatus.Voided) or nameof(LoanStatus.Novated)) continue;
                var (producto, saldos) = await CreditoAsync(credito, rol == "Debtor" ? "Deudor" : "Codeudor", fecha, avisos, ct);
                productos.Add(producto);
                if (rol == "Debtor" && saldos is not null) otrosComoDeudor += saldos.Where(s => ConceptosDeOtros.Contains(s.Key)).Sum(s => s.Value);
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
        var (cupoAsignado, cupoDisponible) = await CupoAsync(request.PersonPublicId, fecha, avisos, ct);
        var conConceptos = deudor.Count > 0;
        var totales = new TotalesDelEstadoDeCuentaDto(
            deudor.Sum(p => p.Saldo), deudor.Sum(p => p.ParaCancelar ?? 0m), deudor.Sum(p => p.ValorVencido ?? 0m),
            deudor.Select(p => p.DiasDeMora ?? 0).DefaultIfEmpty(0).Max(),
            productos.Where(p => p.Clase == "Cuenta" && p.Naturaleza == NaturalezaAportes).Sum(p => p.Saldo),
            productos.Where(p => p.Clase == "Cuenta" && p.Naturaleza != NaturalezaAportes).Sum(p => p.Saldo),
            cupoAsignado, cupoDisponible,
            conConceptos ? deudor.Sum(p => p.Saldo) : null,
            conConceptos ? deudor.Sum(p => p.InteresCorriente ?? 0m) : null,
            conConceptos ? deudor.Sum(p => p.InteresDeMora ?? 0m) : null,
            conConceptos ? deudor.Sum(p => p.Seguro ?? 0m) : null,
            conConceptos ? otrosComoDeudor : null)
        {
            // 2026-11-16 (feature 024, fase 2): la administración, aparte del seguro y de los otros.
            ConceptoAdministracion = conConceptos ? deudor.Sum(p => p.Administracion ?? 0m) : null,
        };

        var membrete = await LecturasDelEstadoDeCuenta.MembreteAsync(db, store, cifrado, ct);
        return Result.Success(new EstadoDeCuentaDeLaPersonaDto(persona, fecha, productos, totales, avisos, membrete));
    }

    /// <summary>
    /// El cupo de la persona a la fecha (<see cref="GetCreditLimitAvailabilityQuery"/>, que respeta el alcance de líneas): el consolidado si lo tiene;
    /// si no, la suma de los por línea y los rotativos. Sin cupo, nulos; si no se puede leer, nulos y el aviso.
    /// </summary>
    private async Task<(decimal? Asignado, decimal? Disponible)> CupoAsync(Guid persona, DateOnly fecha, List<string> avisos, CancellationToken ct)
    {
        var leido = await sender.Send(new GetCreditLimitAvailabilityQuery(persona, null, fecha), ct);
        if (leido.IsFailure)
        {
            avisos.Add($"El cupo de crédito no se pudo leer: {leido.Error.Message}");
            return (null, null);
        }
        var d = leido.Value;
        if (d.Consolidated is { } consolidado) return (consolidado.Limit, consolidado.Available);
        var otros = d.ByLine.Concat(d.Revolving).ToList();
        return otros.Count == 0 ? (null, null) : (otros.Sum(c => c.Limit), otros.Sum(c => c.Available));
    }

    private async Task<(ProductoDelEstadoDeCuentaDto Producto, IReadOnlyDictionary<string, decimal>? Saldos)> CreditoAsync(LoanListItemDto c, string rol,
        DateOnly fecha, List<string> avisos, CancellationToken ct)
    {
        var condiciones = await db.LoanTermsVersions.AsNoTracking()
            .Where(t => db.Loans.Any(l => l.Id == t.LoanId && l.PublicId == c.PublicId))
            .OrderByDescending(t => t.VersionNumber)
            .Select(t => new { t.AnnualRate, t.RateBasis, t.MonthlyRate, t.Periodicity, t.Installments, t.TermMonths, t.AmortizationSystem, t.FirstDueDate })
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
        // 2026-11-24 (feature 025, HU6): la cuota regular, no la primera: en un crédito cargado la primera es la recortada por el corte.
        var regular = plan is null ? null : CuotaRegular(plan.Installments, fecha);

        return (new ProductoDelEstadoDeCuentaDto(
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
            Saldo("Advance", "CreditBalance"), null, estado?.OfficialRating ?? estado?.InformativeRating ?? c.InformativeRating)
        {
            // 2026-11-16 (feature 024, fase 2): la administración, aparte del seguro.
            Administracion = Saldo("Administration"),
            // 2026-11-16 (feature 024, fase 4): la cuota del período reconocida al inicio del ciclo y todavía no causada.
            ReconocidoPorCausar = estado?.RecognizedPending,
            // 2026-11-24 (feature 025, HU2): la MV pactada, en puntos como la tasa.
            TasaMensual = condiciones is { RateBasis: RateBasis.MonthlyNominal, MonthlyRate: { } mv } ? decimal.Round(mv * 100m, 6) : null,
        }, estado?.Balances);
    }

    public const string NaturalezaAportes = "Aportes";

    /// <summary>
    /// 2026-11-24 (feature 025, HU6, FR-019): la cuota regular del plan —el total más frecuente entre las cuotas que no son extraordinarias ni de
    /// gracia—; con empate, la de la siguiente por vencer a <paramref name="hoy"/> (o la última si ya vencieron todas). Nula sin cuotas regulares. (nuevo)
    /// </summary>
    public static decimal? CuotaRegular(IReadOnlyList<ScheduleInstallmentDto> cuotas, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(cuotas);
        var regulares = cuotas.Where(q => !q.IsExtraordinary && !q.IsGraceInstallment).OrderBy(q => q.DueDate).ToList();
        if (regulares.Count == 0) return null;
        var grupos = regulares.GroupBy(q => q.Total).Select(g => (Total: g.Key, Veces: g.Count())).ToList();
        var maximo = grupos.Max(g => g.Veces);
        var empatados = grupos.Where(g => g.Veces == maximo).Select(g => g.Total).ToHashSet();
        if (empatados.Count == 1) return empatados.First();
        var siguiente = regulares.FirstOrDefault(q => q.DueDate >= hoy && empatados.Contains(q.Total))
                        ?? regulares.Last(q => empatados.Contains(q.Total));
        return siguiente.Total;
    }

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
            ["administracion"] = (TipoDeColumna.Moneda, p => p.Administracion),
            ["reconocidoPorCausar"] = (TipoDeColumna.Moneda, p => p.ReconocidoPorCausar),
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
        notas.AddRange(NotasDelResumen(t));
        notas.AddRange(e.Avisos);
        var subtitulo = $"{e.Persona.Nombre} · {e.Persona.Documento} · al {e.Fecha:dd/MM/yyyy}";
        return new TablaExportable(Titulo, subtitulo, columnasExportables, filas, null, notas);
    }

    /// <summary>
    /// 2026-11-16 (pedido del dueño): el resumen de conceptos y el cupo como notas (Excel, PDF y Word de la vista y del Excel del estado de cuenta):
    /// sólo lo que trae el estado de cuenta.
    /// </summary>
    public static IReadOnlyList<string> NotasDelResumen(TotalesDelEstadoDeCuentaDto t)
    {
        var notas = new List<string>();
        if (t.ConResumenDeConceptos)
        {
            // 2026-11-16 (feature 024, fase 2): la administración sólo si la hay, así la nota de siempre no cambia.
            var administracion = t.ConceptoAdministracion is > 0m or < 0m
                ? string.Create(Cultura, $"administración {t.ConceptoAdministracion:N0}; ")
                : string.Empty;
            notas.Add(string.Create(Cultura,
                $"Resumen de conceptos como deudor: capital {t.ConceptoCapital:N0}; interés corriente {t.ConceptoInteresCorriente:N0}; interés de mora {t.ConceptoInteresDeMora:N0}; seguro {t.ConceptoSeguro:N0}; {administracion}otros {t.ConceptoOtros:N0}; total {t.TotalDeConceptos:N0}."));
        }
        if (t.CupoAsignado is { } asignado)
            notas.Add(string.Create(Cultura, $"Cupo de crédito: asignado {asignado:N0}; disponible {(t.CupoDisponible ?? 0m):N0}."));
        return notas;
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
