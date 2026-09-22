using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Common;

/// <summary>
/// Escenario mínimo de contabilidad (feature 009) sobre InMemory: configuración iniciada, dos
/// sucursales (la principal), un centro de costo, un tercero vigente, los tipos CG (manual), NM
/// (Nómina), AP (apertura) y CI (cierre), el tipo de cruce FV y el ejercicio 2026 con enero y
/// febrero cerrados, marzo y abril abiertos, y diciembre cerrado (el cierre del ejercicio se fecha
/// el 31/12 y el contrato exige que el período exista, E2). El reloj marca el 20 de marzo de 2026.
/// Las cuentas se piden con <see cref="Cuenta"/> y sus reglas por parámetro.
/// </summary>
public sealed class ContabilidadTestData
{
    public static readonly DateTime Ahora = new(2026, 3, 20, 14, 0, 0, DateTimeKind.Utc);
    public static readonly DateOnly Hoy = new(2026, 3, 20);
    public static readonly DateOnly Marzo15 = new(2026, 3, 15);

    public TestApplicationDbContext Db { get; }
    public IDateTimeService Clock { get; }
    public ICurrentUserService User { get; }
    public IUserBranchScope Alcance { get; }
    public AccountingPoster Poster { get; }
    public Branch Principal { get; }
    public Branch Norte { get; }
    public CostCenter Centro { get; }
    public Person Tercero { get; }
    public AccountingSetup Setup { get; }

    public ContabilidadTestData(bool iniciada = true)
    {
        Db = TestDbContextFactory.Create();
        Clock = Substitute.For<IDateTimeService>();
        Clock.UtcNow.Returns(Ahora);
        Clock.TodayUtc.Returns(Hoy);
        User = NominaTestData.UsuarioDePrueba("contadora@demo", 9);
        Alcance = Substitute.For<IUserBranchScope>();
        Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(AlcanceDeSucursales.SinRestriccion));
        Poster = new AccountingPoster(Db, Clock, User, Alcance);

        Principal = new Branch { Name = "Principal", CreatedBy = "test" };
        Norte = new Branch { Name = "Norte", CreatedBy = "test" };
        Db.Branches.AddRange(Principal, Norte);
        Centro = new CostCenter { LegacyCode = "01", Name = "Administración", CreatedBy = "test" };
        Db.CostCenters.Add(Centro);
        Tercero = new Person { FirstName = "Ana", LastName = "Prueba", TaxId = "1000000001", Status = "A", CreatedBy = "test" };
        Db.People.Add(Tercero);
        Db.VoucherTypes.AddRange(
            new VoucherType { Code = "CG", Name = "Comprobante general", Usage = VoucherUsage.Manual, IsSeeded = true, CreatedBy = "test" },
            new VoucherType { Code = "NM", Name = "Nómina", Usage = VoucherUsage.Module, ModuleCode = "NOM", IsSeeded = true, CreatedBy = "test" },
            new VoucherType { Code = "AP", Name = "Apertura", Usage = VoucherUsage.Opening, IsSeeded = true, CreatedBy = "test" },
            new VoucherType { Code = "CI", Name = "Cierre", Usage = VoucherUsage.Closing, IsSeeded = true, CreatedBy = "test" });
        Db.CrossDocumentTypes.Add(new CrossDocumentType { Code = "FV", Name = "Factura de venta", IsSeeded = true, CreatedBy = "test" });
        var catalogo = new AccountCatalog { Code = "PRUEBA", Name = "Catálogo de prueba", Version = "2026", CreatedBy = "test" };
        Db.AccountCatalogs.Add(catalogo);
        Db.SaveChanges();

        var ejercicio = new FiscalYear { Year = 2026, CreatedBy = "test" };
        Db.FiscalYears.Add(ejercicio);
        Db.SaveChanges();
        Db.AccountingPeriods.AddRange(
            Periodo(ejercicio.Id, 1, PeriodStatus.Closed), Periodo(ejercicio.Id, 2, PeriodStatus.Closed),
            Periodo(ejercicio.Id, 3, PeriodStatus.Open), Periodo(ejercicio.Id, 4, PeriodStatus.Open), Periodo(ejercicio.Id, 12, PeriodStatus.Closed));

        Setup = new AccountingSetup
        {
            CatalogId = catalogo.Id, MovementLevel = 5, NiifGroup = 2, FirstFiscalYear = 2026,
            MainBranchId = Principal.Id, TaxTolerance = 5m, InitializedAt = Ahora, InitializedBy = "test", CreatedBy = "test",
        };
        if (iniciada) Db.AccountingSetups.Add(Setup);
        Db.SaveChanges();
    }

    /// <summary>Feature 009 E2 (US6): el ejercicio 2026 completo, con los doce meses en el estado que se pida (por defecto todos cerrados, listo para el cierre del año).</summary>
    public FiscalYear EjercicioCompleto(PeriodStatus estado = PeriodStatus.Closed)
    {
        var ejercicio = Db.FiscalYears.Single(f => f.Year == 2026);
        var existentes = Db.AccountingPeriods.Where(p => p.FiscalYearId == ejercicio.Id).ToList();
        foreach (var p in existentes) p.Status = estado;
        for (var mes = 5; mes <= 11; mes++) Db.AccountingPeriods.Add(Periodo(ejercicio.Id, mes, estado));
        Db.SaveChanges();
        return ejercicio;
    }

    /// <summary>Un ejercicio más (abierto o cerrado) con sus doce períodos, para probar «el anterior tiene que estar cerrado» y «sólo se reabre el último».</summary>
    public FiscalYear OtroEjercicio(int year, PeriodStatus estado)
    {
        var ejercicio = new FiscalYear { Year = year, Status = estado, CreatedBy = "test" };
        Db.FiscalYears.Add(ejercicio);
        Db.SaveChanges();
        for (var mes = 1; mes <= 12; mes++)
        {
            var inicio = new DateOnly(year, mes, 1);
            Db.AccountingPeriods.Add(new AccountingPeriod { FiscalYearId = ejercicio.Id, Month = (byte)mes, StartDate = inicio, EndDate = inicio.AddMonths(1).AddDays(-1), Status = estado, CreatedBy = "test" });
        }
        Db.SaveChanges();
        return ejercicio;
    }

    private static AccountingPeriod Periodo(int ejercicioId, int mes, PeriodStatus estado)
    {
        var inicio = new DateOnly(2026, mes, 1);
        return new AccountingPeriod { FiscalYearId = ejercicioId, Month = (byte)mes, StartDate = inicio, EndDate = inicio.AddMonths(1).AddDays(-1), Status = estado, CreatedBy = "test" };
    }

    public ChartOfAccount Cuenta(
        string code,
        AccountNature nature = AccountNature.Debit,
        AccountingModules modulos = AccountingModules.Accounting | AccountingModules.Payroll,
        bool movimiento = true,
        bool activa = true,
        bool tercero = false,
        bool cruce = false,
        bool centro = false,
        bool sucursal = false,
        bool baseGravable = false,
        decimal? tarifa = null)
    {
        var cuenta = new ChartOfAccount
        {
            Code = code, Name = $"Cuenta {code}", Level = movimiento ? (byte)5 : (byte)4, Nature = nature, NiifItemCode = "X",
            Origin = movimiento ? AccountOrigin.Company : AccountOrigin.Catalog, IsMovement = movimiento, IsActive = activa,
            EnabledModules = modulos, RequiresThirdParty = tercero, RequiresCrossDocument = cruce, RequiresCostCenter = centro,
            RequiresBranch = sucursal, RequiresTaxBase = baseGravable, TaxKind = baseGravable ? TaxKind.Withholding : TaxKind.None,
            CreatedBy = "test",
        };
        if (tarifa is { } t) cuenta.TaxRates.Add(new AccountTaxRate { ValidFrom = new DateOnly(2026, 1, 1), Rate = t, CreatedBy = "test" });
        Db.ChartOfAccounts.Add(cuenta);
        Db.SaveChanges();
        return cuenta;
    }

    public void RestringirA(Branch sucursal) =>
        Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(AlcanceDeSucursales.Limitado([sucursal.Id], sucursal.Id)));

    public static AccountingOrigin Manual() => AccountingOrigin.Manual(Guid.NewGuid());
    public static AccountingOrigin Nomina() => new(ModuloContable.Nomina, "PayrollRun", Guid.NewGuid());

    /// <summary>Un CG cuadrado de 100: débito a <paramref name="debito"/>, crédito a <paramref name="credito"/>.</summary>
    public static PostingRequest Comprobante(ChartOfAccount debito, ChartOfAccount credito, DateOnly? fecha = null, AccountingOrigin? origen = null, string tipo = "CG", DocumentKind kind = DocumentKind.Regular) =>
        new(tipo, fecha ?? Marzo15, "Prueba", origen ?? Manual(),
            [PostingLine.Debito(debito.Id, 100m, "D"), PostingLine.Credito(credito.Id, 100m, "C")], kind);
}
