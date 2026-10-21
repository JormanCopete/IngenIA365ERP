using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Queries;

// --- DTOs ---

public record AssociateDataDto(
    DateOnly? JoinDate,
    decimal ContributionRate,
    string? Status,
    string? CategoryRating,
    string? DeductionType,
    string? EmployerCompanyName,
    string? BranchName,
    string? SectionName);

public record SpouseDataDto(
    string? SpouseName,
    string? SpouseIdNumber,
    string? SpouseEmployer,
    decimal SpouseSalary,
    string? SpousePhone);

public record FinancialDataDto(
    decimal DebtCapacity,
    decimal OtherIncome,
    decimal TotalAssets,
    decimal VariableIncome,
    decimal CreditScore,
    decimal CreditBureauScore,
    string? CreditBureauRating,
    decimal ExternalDebtPayment,
    decimal ExternalDebtBalance);

/// <summary>
/// El resumen de cartera y ahorros de la persona. Feature 013 (T0169, T94, interfaces.md §7): los créditos salen de
/// <see cref="IConsultaDeObligaciones"/> (como deudor) y <see cref="LendingStatus"/> dice el estado de Cartera. Feature 014 (T043; contrato-de-cuentas
/// §5): las cuentas de ahorro y CDAT y los aportes salen de <see cref="IConsultasDeCartera.ProductosDeCaptacionAsync"/>, vivas y <b>sólo como
/// titular</b> (decisión del dueño del 2026-10-18, D20); las de cotitular se cuentan aparte en <see cref="CoHeldAccounts"/> y se ven en su propia
/// ventana (<see cref="GetPersonCoHeldAccountsQuery"/>). Si Cartera no responde, en cero.
/// </summary>
public record PortfolioSummaryDto(
    int ActiveLoans,
    decimal TotalLoanBalance,
    int SavingsAccounts,
    decimal TotalSavingsBalance,
    decimal TotalContributions,
    decimal TotalOverdueAmount)
{
    /// <summary>
    /// <see cref="PortfolioLendingStatus.NotActivated"/>, <see cref="PortfolioLendingStatus.Active"/>,
    /// <see cref="PortfolioLendingStatus.MigratedNotActivated"/> o <see cref="PortfolioLendingStatus.Unavailable"/> (Cartera no respondió).
    /// </summary>
    public string LendingStatus { get; init; } = PortfolioLendingStatus.NotActivated;

    /// <summary>Feature 014 (D20): las cuentas vivas donde la persona es cotitular; no suman a los saldos de arriba. Se listan con su botón aparte.</summary>
    public int CoHeldAccounts { get; init; }
}

/// <summary>Valores de <see cref="PortfolioSummaryDto.LendingStatus"/> (feature 013, T0169). (nuevo)</summary>
public static class PortfolioLendingStatus
{
    public const string NotActivated = "NotActivated";
    public const string Active = "Active";
    public const string MigratedNotActivated = "MigratedNotActivated";
    public const string Unavailable = "Unavailable";
}

public record PersonDetailDto(
    Guid PublicId,
    string FirstName,
    string LastName,
    string? BusinessName,
    string TaxId,
    string IdType,
    string? Address,
    string? Phone1,
    string? Mobile,
    string? Email,
    string? Gender,
    string? MaritalStatus,
    DateOnly? DateOfBirth,
    bool IsAssociate,
    bool IsEmployee,
    bool IsThirdParty,
    bool IsCustomer,
    bool IsSupplier,
    bool IsSalesperson,
    string? Status,
    AssociateDataDto? AssociateData,
    SpouseDataDto? SpouseData,
    FinancialDataDto? FinancialData,
    PortfolioSummaryDto PortfolioSummary);

// --- Query ---

public record GetPersonDetailQuery(Guid PublicId) : IRequest<Result<PersonDetailDto>>;

public class GetPersonDetailQueryHandler(IApplicationDbContext context, IConsultaDeObligaciones obligaciones, IConsultasDeCartera consultas,
    IDateTimeService reloj)
    : IRequestHandler<GetPersonDetailQuery, Result<PersonDetailDto>>
{
    public async Task<Result<PersonDetailDto>> Handle(GetPersonDetailQuery request, CancellationToken ct)
    {
        var person = await context.People
            .AsNoTracking()
            .Include(p => p.Associate)
                .ThenInclude(a => a!.EmployerCompany)
            .Include(p => p.Associate)
                .ThenInclude(a => a!.Branch)
            .Include(p => p.Associate)
                .ThenInclude(a => a!.Section)
            .Include(p => p.Spouse)
            .Include(p => p.Financial)
            .FirstOrDefaultAsync(p => p.PublicId == request.PublicId && !p.IsDeleted, ct);

        if (person is null)
            return Result.Failure<PersonDetailDto>(new Error("Person.NotFound",
                "Persona no encontrada."));

        // Associate data
        AssociateDataDto? associateData = null;
        if (person.Associate is not null)
        {
            var a = person.Associate;
            associateData = new AssociateDataDto(
                a.JoinDate,
                a.ContributionRate,
                a.Status,
                a.CategoryRating,
                a.DeductionType,
                a.EmployerCompany?.Name,
                a.Branch?.Name,
                a.Section?.Name);
        }

        // Spouse data — datos personales del conyuge vienen de Spouse,
        // datos laborales del conyuge vienen de Associate (decision P10).
        SpouseDataDto? spouseData = null;
        if (person.Spouse is not null)
        {
            var s = person.Spouse;
            var a = person.Associate;
            spouseData = new SpouseDataDto(
                s.SpouseName,
                s.SpouseIdNumber,
                a?.SpouseEmployer,
                a?.SpouseSalary ?? 0m,
                s.SpousePhone);
        }

        // Financial data
        FinancialDataDto? financialData = null;
        if (person.Financial is not null)
        {
            var f = person.Financial;
            financialData = new FinancialDataDto(
                f.DebtCapacity,
                f.OtherIncome,
                f.TotalAssets,
                f.VariableIncome,
                f.CreditScore,
                f.CreditBureauScore,
                f.CreditBureauRating,
                f.ExternalDebtPayment,
                f.ExternalDebtBalance);
        }

        // Portfolio summary: los créditos, por la consulta de obligaciones de Cartera (feature 013, T0169).
        var deCartera = await ResumenDeCarteraDePersona.ArmarAsync(obligaciones, person.PublicId, reloj.HoyLocal, ct);
        var activeLoans = deCartera.ActiveLoans;
        var totalLoanBalance = deCartera.TotalLoanBalance;

        // Feature 014 (T043, contrato-de-cuentas §5): las cuentas de captación y los aportes, por la consulta de productos de Cartera.
        var captacion = await ResumenDeCaptacionesDePersona.ArmarAsync(consultas, person.PublicId, reloj.HoyLocal, ct);
        var savingsCount = captacion.Cuentas;
        var totalSavings = captacion.Ahorros;

        var totalOverdue = deCartera.TotalOverdueAmount;

        var portfolioSummary = new PortfolioSummaryDto(
            activeLoans,
            totalLoanBalance,
            savingsCount,
            totalSavings,
            captacion.Aportes,
            totalOverdue)
        {
            LendingStatus = deCartera.LendingStatus,
            CoHeldAccounts = captacion.ComoCotitular,
        };

        var dto = new PersonDetailDto(
            person.PublicId,
            person.FirstName,
            person.LastName,
            person.BusinessName,
            person.TaxId,
            person.IdType,
            person.Address,
            person.Phone1,
            person.Mobile,
            person.Email,
            person.Gender,
            person.MaritalStatus,
            person.DateOfBirth,
            person.IsAssociate,
            person.IsEmployee,
            person.IsThirdParty,
            person.IsCustomer,
            person.IsSupplier,
            person.IsSalesperson,
            person.Status,
            associateData,
            spouseData,
            financialData,
            portfolioSummary);

        return Result.Success(dto);
    }
}

/// <summary>
/// La parte de captaciones del resumen de la persona (feature 014, T043; contrato-de-cuentas §5): las cuentas de ahorro y CDAT y los aportes vivos
/// donde es <b>titular</b> y, aparte, cuántas cuentas vivas tiene como cotitular (decisión del dueño del 2026-10-18, D20: la ficha muestra sólo las
/// suyas; las de cotitular, en una ventana aparte). (nuevo)
/// </summary>
public static class ResumenDeCaptacionesDePersona
{
    /// <summary>Cuentas de captación vivas como titular, su saldo y el de aportes, y las cuentas vivas como cotitular.</summary>
    public sealed record Resumen(int Cuentas, decimal Ahorros, decimal Aportes, int ComoCotitular = 0)
    {
        public static Resumen Vacio { get; } = new(0, 0m, 0m);
    }

    /// <summary>
    /// Feature 014 (T043): el resumen de captaciones de la persona, de una sola llamada a <see cref="IConsultasDeCartera.ProductosDeCaptacionAsync"/>.
    /// Si Cartera no responde, en cero (como el resumen de créditos). (nuevo)
    /// </summary>
    public static async Task<Resumen> ArmarAsync(IConsultasDeCartera consultas, Guid persona, DateOnly hoy, CancellationToken ct)
    {
        try
        {
            var respuesta = await consultas.ProductosDeCaptacionAsync(new ConsultaDeProductosDeCaptacion([persona], hoy, IncludeClosed: false, IncludeAsParty: true), ct);
            var productos = respuesta.People.Where(p => p.PersonPublicId == persona).SelectMany(p => p.Products)
                .Where(p => p.Status != EstadoDeCuenta.Closed).ToList();
            var vivos = productos.Where(p => p.Role == RolEnLaCuenta.Holder).ToList();
            var ahorros = vivos.Where(p => p.Nature == NaturalezaDeCuenta.Deposit).ToList();
            return new Resumen(ahorros.Count, ahorros.Sum(p => p.Balance), vivos.Where(p => p.Nature == NaturalezaDeCuenta.Contribution).Sum(p => p.Balance),
                productos.Count(p => p.Role == RolEnLaCuenta.CoHolder));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Resumen.Vacio;
        }
    }
}

/// <summary>
/// La parte de Cartera del resumen de la persona (feature 013, T0169; interfaces.md §7): las obligaciones vivas como deudor y el estado de
/// Cartera, de una sola llamada a <see cref="IConsultaDeObligaciones"/> con codeudas (la misma de <c>DeletePersonCommand</c>). Si Cartera
/// no responde, sale sin créditos y con estado <see cref="PortfolioLendingStatus.Unavailable"/>. (nuevo)
/// </summary>
public static class ResumenDeCarteraDePersona
{
    public sealed record Resumen(int ActiveLoans, decimal TotalLoanBalance, decimal TotalOverdueAmount, string LendingStatus);

    public static async Task<Resumen> ArmarAsync(IConsultaDeObligaciones obligaciones, Guid persona, DateOnly hoy, CancellationToken ct)
    {
        try
        {
            var r = await obligaciones.ObligacionesAsync(new ConsultaDeObligaciones([persona], hoy, IncludeAsCoDebtor: true), ct);
            var propia = r.People.FirstOrDefault(p => p.PersonPublicId == persona);
            var deudas = propia?.Obligations.Where(o => o.IsLive && o.Role == RolEnLaObligacion.Debtor).ToList() ?? [];
            var estado = propia?.MigratedNotActivated == true
                ? PortfolioLendingStatus.MigratedNotActivated
                : r.LendingStatus == EstadoDeCartera.Active ? PortfolioLendingStatus.Active : PortfolioLendingStatus.NotActivated;
            return new Resumen(deudas.Count, deudas.Sum(o => o.TotalBalance), deudas.Where(o => o.DaysPastDue > 0).Sum(o => o.DueAmount), estado);
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is not OutOfMemoryException)
        {
            return new Resumen(0, 0m, 0m, PortfolioLendingStatus.Unavailable);
        }
    }
}

/// <summary>
/// Feature 014 (decisión del dueño del 2026-10-18, D20): las cuentas vivas donde la persona es <b>cotitular</b>, para la ventana que la ficha abre con su
/// botón aparte (la ficha muestra sólo las cuentas donde es titular). Sale de <see cref="IConsultasDeCartera.ProductosDeCaptacionAsync"/>; si Cartera no
/// responde, la lista va vacía con <see cref="CoHeldAccountsDto.Available"/> en falso. (nuevo)
/// </summary>
public record GetPersonCoHeldAccountsQuery(Guid PublicId) : IRequest<Result<CoHeldAccountsDto>>;

/// <summary>Las cuentas como cotitular: número, naturaleza, clase, línea, estado, saldo y disponible (D20). (nuevo)</summary>
public sealed record CoHeldAccountsDto(bool Available, IReadOnlyList<CoHeldAccountDto> Accounts);

/// <summary>Una cuenta como cotitular (D20). (nuevo)</summary>
public sealed record CoHeldAccountDto(Guid AccountPublicId, string Number, string Nature, string? DepositClass, string LineCode, string LineName,
    string Status, decimal Balance, decimal Available);

public sealed class GetPersonCoHeldAccountsQueryHandler(IApplicationDbContext context, IConsultasDeCartera consultas, IDateTimeService reloj)
    : IRequestHandler<GetPersonCoHeldAccountsQuery, Result<CoHeldAccountsDto>>
{
    public async Task<Result<CoHeldAccountsDto>> Handle(GetPersonCoHeldAccountsQuery request, CancellationToken ct)
    {
        if (!await context.People.AsNoTracking().AnyAsync(p => p.PublicId == request.PublicId && !p.IsDeleted, ct))
            return Result.Failure<CoHeldAccountsDto>(new Error("Person.NotFound", "Persona no encontrada."));
        try
        {
            var respuesta = await consultas.ProductosDeCaptacionAsync(
                new ConsultaDeProductosDeCaptacion([request.PublicId], reloj.HoyLocal, IncludeClosed: false, IncludeAsParty: true), ct);
            var cuentas = respuesta.People.Where(p => p.PersonPublicId == request.PublicId).SelectMany(p => p.Products)
                .Where(p => p.Role == RolEnLaCuenta.CoHolder && p.Status != EstadoDeCuenta.Closed)
                .OrderBy(p => p.Number, StringComparer.Ordinal)
                .Select(p => new CoHeldAccountDto(p.AccountPublicId, p.Number, p.Nature.ToString(), p.DepositClass?.ToString(), p.LineCode, p.LineName,
                    p.Status.ToString(), p.Balance, p.Available))
                .ToList();
            return Result.Success(new CoHeldAccountsDto(true, cuentas));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Success(new CoHeldAccountsDto(false, []));
        }
    }
}
