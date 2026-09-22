using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Accounting.Setup;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Setup;

/// <summary>
/// T051 — US1: iniciar copia el catálogo completo sin cuentas de movimiento y abre el ejercicio;
/// la configuración estructural se bloquea con la primera auxiliar o el primer movimiento; el
/// importador no guarda nada a medias y señala la fila.
/// </summary>
public class SetupCommandsTests
{
    private sealed class Escenario
    {
        public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
        public IDateTimeService Clock { get; } = Substitute.For<IDateTimeService>();
        public ICurrentUserService User { get; } = NominaTestData.UsuarioDePrueba("admin@demo", 3);
        public IAuditAppendOnlyWriter Audit { get; } = Substitute.For<IAuditAppendOnlyWriter>();
        public AccountingAuditEmitter Emisor { get; }
        public Branch Principal { get; }
        public AccountCatalog Catalogo { get; }

        public Escenario()
        {
            Clock.UtcNow.Returns(new DateTime(2026, 3, 20, 14, 0, 0, DateTimeKind.Utc));
            Clock.TodayUtc.Returns(new DateOnly(2026, 3, 20));
            Emisor = new AccountingAuditEmitter(Audit, User, Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
            Principal = new Branch { Name = "Principal", CreatedBy = "test" };
            Db.Branches.Add(Principal);
            Db.FinancialStatementItems.AddRange(
                new FinancialStatementItem { NiifGroup = 2, Code = "ESF-A-EFE", Name = "Efectivo", Statement = FinancialStatementKind.FinancialPosition, Section = "Activo", Order = 1, Sign = 1, CreatedBy = "test" },
                new FinancialStatementItem { NiifGroup = 2, Code = "ESF-P-DEP", Name = "Depósitos", Statement = FinancialStatementKind.FinancialPosition, Section = "Pasivo", Order = 2, Sign = 1, CreatedBy = "test" });
            Catalogo = new AccountCatalog { Code = "PUC-SOLIDARIO", Name = "Solidario", Version = "R2015", Source = CatalogSource.Official, CreatedBy = "test" };
            foreach (var (code, name, nature, rubro) in new[]
                     {
                         ("1", "ACTIVO", AccountNature.Debit, "ESF-A-EFE"), ("11", "EFECTIVO", AccountNature.Debit, "ESF-A-EFE"),
                         ("1105", "CAJA", AccountNature.Debit, "ESF-A-EFE"), ("110505", "Caja general", AccountNature.Debit, "ESF-A-EFE"),
                         ("2", "PASIVO", AccountNature.Credit, "ESF-P-DEP"), ("21", "DEPÓSITOS", AccountNature.Credit, "ESF-P-DEP"),
                         ("2105", "AHORRO", AccountNature.Credit, "ESF-P-DEP"), ("210505", "Ahorro ordinario", AccountNature.Credit, "ESF-P-DEP"),
                     })
            {
                Catalogo.Entries.Add(new AccountCatalogEntry
                {
                    Code = code, Name = name, Level = (byte)(code.Length switch { 1 => 1, 2 => 2, 4 => 3, _ => 4 }), Nature = nature, NiifItemCode = rubro,
                    ParentCode = code.Length switch { 2 => code[..1], 4 => code[..2], 6 => code[..4], _ => null }, CreatedBy = "test",
                });
            }
            Catalogo.EntryCount = Catalogo.Entries.Count;
            Db.AccountCatalogs.Add(Catalogo);
            Db.SaveChanges();
        }

        public InitializeAccountingCommandHandler Iniciador() => new(Db, Clock, User, Emisor);
        public UpdateAccountingSetupCommandHandler Actualizador() => new(Db, Clock, User, Emisor);
        public InitializeAccountingCommand Peticion(byte nivel = 6) => new("PUC-SOLIDARIO", nivel, 2, 2026, Principal.PublicId, false);

        public async Task<Result<InicializacionDto>> IniciarAsync(byte nivel = 6) => await Iniciador().Handle(Peticion(nivel), CancellationToken.None);

        public ChartOfAccount Auxiliar(string code, string padre)
        {
            var p = Db.ChartOfAccounts.Single(a => a.Code == padre);
            var c = new ChartOfAccount { Code = code, Name = "Auxiliar", Level = 5, Nature = p.Nature, ParentId = p.Id, NiifItemCode = p.NiifItemCode, Origin = AccountOrigin.Company, IsMovement = true, CreatedBy = "test" };
            Db.ChartOfAccounts.Add(c);
            Db.SaveChanges();
            return c;
        }
    }

    [Fact]
    public async Task Iniciar_copia_el_catalogo_completo_sin_cuentas_de_movimiento_y_abre_el_ejercicio()
    {
        var e = new Escenario();

        var r = await e.IniciarAsync();

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Should().Be(new InicializacionDto(8, 12));
        var cuentas = await e.Db.ChartOfAccounts.ToListAsync();
        cuentas.Should().HaveCount(8);
        cuentas.Should().OnlyContain(c => !c.IsMovement && c.Origin == AccountOrigin.Catalog && c.IsActive && c.EnabledModules == AccountingModules.None);
        var caja = cuentas.Single(c => c.Code == "110505");
        caja.Level.Should().Be(4);
        caja.ParentId.Should().Be(cuentas.Single(c => c.Code == "1105").Id);
        caja.NiifItemCode.Should().Be("ESF-A-EFE");
        var setup = await e.Db.AccountingSetups.SingleAsync();
        setup.MovementLevel.Should().Be(6);
        setup.MainBranchId.Should().Be(e.Principal.Id);
        (await e.Db.FiscalYears.Include(f => f.Periods).SingleAsync()).Periods.Should().HaveCount(12).And.OnlyContain(p => p.Status == PeriodStatus.Open);
        (await e.Db.AccountingPeriods.SingleAsync(p => p.Month == 2)).EndDate.Should().Be(new DateOnly(2026, 2, 28));
        await e.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == "Accounting.Setup.Initialized"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Iniciar_dos_veces_o_con_catalogo_o_sucursal_inexistente_se_rechaza()
    {
        var e = new Escenario();
        (await e.IniciarAsync()).IsSuccess.Should().BeTrue();

        (await e.IniciarAsync()).Error.Code.Should().Be("Accounting.Setup.AlreadyInitialized");

        var e2 = new Escenario();
        (await e2.Iniciador().Handle(e2.Peticion() with { CatalogCode = "NOEXISTE" }, CancellationToken.None)).Error.Code.Should().Be("Accounting.Setup.CatalogNotFound");
        (await e2.Iniciador().Handle(e2.Peticion() with { MainBranchPublicId = Guid.NewGuid() }, CancellationToken.None)).Error.Code.Should().Be("Accounting.Setup.BranchNotFound");
        (await e2.Iniciador().Handle(e2.Peticion() with { MovementLevel = 7 }, CancellationToken.None)).Error.Code.Should().Be("Accounting.Setup.LengthsInvalid", "el nivel de movimiento es 5 o 6");
        (await e2.Db.ChartOfAccounts.CountAsync()).Should().Be(0, "nada se guarda si falla");
    }

    [Fact]
    public async Task La_estructura_se_bloquea_con_la_primera_auxiliar_pero_cuatro_ojos_cambia_siempre()
    {
        var e = new Escenario();
        await e.IniciarAsync();
        e.Auxiliar("11050501", "110505");

        var r = await e.Actualizador().Handle(new UpdateAccountingSetupCommand(null, 5, null, null, null, true, 3, 1m), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Setup.Locked");
        r.Error.Should().BeOfType<ErrorConDatos>();
        r.Error.Message.Should().Contain("1 cuenta(s) auxiliar(es)");

        var soloCuatroOjos = await e.Actualizador().Handle(new UpdateAccountingSetupCommand(null, null, null, null, null, true, 5, 2m), CancellationToken.None);
        soloCuatroOjos.IsSuccess.Should().BeTrue(soloCuatroOjos.Error.Message);
        var setup = await e.Db.AccountingSetups.SingleAsync();
        setup.FourEyes.Should().BeTrue();
        setup.ReconciliationDayTolerance.Should().Be(5);
        setup.TaxTolerance.Should().Be(2m);
        setup.MovementLevel.Should().Be(6, "lo estructural no se tocó");
    }

    [Fact]
    public async Task Con_movimientos_el_bloqueo_dice_desde_cuando()
    {
        var e = new Escenario();
        await e.IniciarAsync();
        var aux = e.Auxiliar("11050501", "110505");
        aux.FirstMovementAt = new DateOnly(2026, 3, 15);
        await e.Db.SaveChangesAsync();

        var r = await e.Actualizador().Handle(new UpdateAccountingSetupCommand("PUC-SOLIDARIO", 5, null, null, null, false, 3, 1m), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Setup.Locked");
        r.Error.Message.Should().Contain("2026-03-15");
    }

    [Fact]
    public async Task Cambiar_de_catalogo_dos_veces_y_volver_no_viola_el_unico_del_codigo()
    {
        var e = new Escenario();
        await e.IniciarAsync();
        var comercial = new AccountCatalog { Code = "PUC-COMERCIAL", Name = "Comercial", Version = "D2650", Source = CatalogSource.Official, CreatedBy = "test" };
        comercial.Entries.Add(new AccountCatalogEntry { Code = "1", Name = "ACTIVO", Level = 1, Nature = AccountNature.Debit, NiifItemCode = "ESF-A-EFE", CreatedBy = "test" });
        comercial.Entries.Add(new AccountCatalogEntry { Code = "11", Name = "DISPONIBLE", Level = 2, Nature = AccountNature.Debit, NiifItemCode = "ESF-A-EFE", ParentCode = "1", CreatedBy = "test" });
        e.Db.AccountCatalogs.Add(comercial);
        await e.Db.SaveChangesAsync();

        var aComercial = await e.Actualizador().Handle(new UpdateAccountingSetupCommand("PUC-COMERCIAL", null, null, null, null, false, 3, 1m), CancellationToken.None);
        aComercial.IsSuccess.Should().BeTrue(aComercial.Error.Message);
        (await e.Db.ChartOfAccounts.CountAsync(a => !a.IsDeleted)).Should().Be(2);
        (await e.Db.ChartOfAccounts.CountAsync(a => a.IsDeleted)).Should().Be(8, "el plan anterior queda retirado, no borrado");

        var deVuelta = await e.Actualizador().Handle(new UpdateAccountingSetupCommand("PUC-SOLIDARIO", null, null, null, null, false, 3, 1m), CancellationToken.None);
        deVuelta.IsSuccess.Should().BeTrue(deVuelta.Error.Message);
        (await e.Db.ChartOfAccounts.CountAsync(a => !a.IsDeleted && a.Code == "1")).Should().Be(1, "el código vuelve a existir entre las vivas");
        (await e.Db.AccountingSetups.SingleAsync()).CatalogId.Should().Be(e.Catalogo.Id);
    }

    [Fact]
    public async Task Las_cuentas_nuevas_del_catalogo_se_listan_y_se_adoptan_con_su_tramo()
    {
        var e = new Escenario();
        await e.IniciarAsync();
        e.Catalogo.Entries.Add(new AccountCatalogEntry { Code = "1110", Name = "BANCOS", Level = 3, Nature = AccountNature.Debit, NiifItemCode = "ESF-A-EFE", ParentCode = "11", CreatedBy = "test" });
        e.Catalogo.Entries.Add(new AccountCatalogEntry { Code = "111005", Name = "Moneda nacional", Level = 4, Nature = AccountNature.Debit, NiifItemCode = "ESF-A-EFE", ParentCode = "1110", CreatedBy = "test" });
        await e.Db.SaveChangesAsync();

        var nuevas = await new ListCatalogUpdatesQueryHandler(e.Db).Handle(new ListCatalogUpdatesQuery(), CancellationToken.None);
        nuevas.Value.Select(n => n.Code).Should().Equal("1110", "111005");

        var adopcion = await new AdoptCatalogUpdatesCommandHandler(e.Db, e.Clock, e.User, e.Emisor).Handle(new AdoptCatalogUpdatesCommand(["111005"]), CancellationToken.None);
        adopcion.IsSuccess.Should().BeTrue(adopcion.Error.Message);
        adopcion.Value.Accounts.Should().Be(2, "la subcuenta arrastra a su cuenta");
        var bancos = await e.Db.ChartOfAccounts.SingleAsync(a => a.Code == "1110");
        (await e.Db.ChartOfAccounts.SingleAsync(a => a.Code == "111005")).ParentId.Should().Be(bancos.Id);
        (await new ListCatalogUpdatesQueryHandler(e.Db).Handle(new ListCatalogUpdatesQuery(), CancellationToken.None)).Value.Should().BeEmpty();
    }

    // ---- importador ----

    private static ITabularFileReader LectorCsv()
    {
        var lector = Substitute.For<ITabularFileReader>();
        lector.LeerAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var lineas = Encoding.UTF8.GetString(ci.ArgAt<byte[]>(0)).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
                var encabezados = lineas[0].Split(';').Select(h => h.Trim()).ToList();
                var filas = lineas.Skip(1).Select((l, i) => new FilaLeida(i + 2, l.Split(';').Select(c => (string?)c.Trim()).ToList())).ToList();
                return Task.FromResult(Result.Success(new TablaLeida(encabezados, filas, "csv")));
            });
        return lector;
    }

    private static ImportAccountCatalogCommandHandler Importador(Escenario e) => new(e.Db, LectorCsv(), e.Clock, e.User, e.Emisor);

    [Fact]
    public async Task Importar_un_catalogo_correcto_lo_deja_como_propio_con_sus_entradas()
    {
        var e = new Escenario();
        var csv = "codigo;nombre;naturaleza;rubro\n1;ACTIVO;D;ESF-A-EFE\n11;EFECTIVO;D;ESF-A-EFE\n1105;CAJA;D;ESF-A-EFE\n110505;Caja general;D;ESF-A-EFE\n";

        var r = await Importador(e).Handle(new ImportAccountCatalogCommand("Mi plan", "plan.csv", Encoding.UTF8.GetBytes(csv)), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Code.Should().Be("PROPIO-1");
        r.Value.EntryCount.Should().Be(4);
        var catalogo = await e.Db.AccountCatalogs.Include(c => c.Entries).SingleAsync(c => c.Code == "PROPIO-1");
        catalogo.Source.Should().Be(CatalogSource.Imported);
        catalogo.ImportedBy.Should().Be("admin@demo");
        catalogo.Entries.Single(x => x.Code == "110505").ParentCode.Should().Be("1105");
        catalogo.Entries.Single(x => x.Code == "110505").Level.Should().Be(4);
    }

    [Fact]
    public async Task Una_fila_sin_padre_una_longitud_rara_o_un_duplicado_impiden_importar_y_dicen_la_fila()
    {
        var e = new Escenario();
        var csv = "codigo;nombre;naturaleza;rubro\n1;ACTIVO;D;ESF-A-EFE\n1105;CAJA;D;ESF-A-EFE\n110;RARA;D;ESF-A-EFE\n1;ACTIVO;X;NOEXISTE\n";

        var r = await Importador(e).Handle(new ImportAccountCatalogCommand("Mi plan", "plan.csv", Encoding.UTF8.GetBytes(csv)), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Catalog.Invalid");
        var errores = ((IEnumerable<ErrorDeFila>)((ErrorConDatos)r.Error).Data.GetType().GetProperty("errors")!.GetValue(((ErrorConDatos)r.Error).Data)!).ToList();
        errores.Should().Contain(x => x.Row == 3 && x.Code == "Accounting.Catalog.ParentMissing");
        errores.Should().Contain(x => x.Row == 4 && x.Code == "Accounting.Catalog.LengthInvalid");
        errores.Should().Contain(x => x.Row == 5 && x.Code == "Accounting.Catalog.Duplicate");
        errores.Should().Contain(x => x.Row == 5 && x.Column == "naturaleza");
        errores.Should().Contain(x => x.Row == 5 && x.Column == "rubro");
        (await e.Db.AccountCatalogs.CountAsync(c => c.Source == CatalogSource.Imported)).Should().Be(0, "nada a medias");
    }

    [Fact]
    public async Task Validar_deja_constancia_y_retirar_solo_admite_importados_que_no_esten_en_uso()
    {
        var e = new Escenario();
        await e.IniciarAsync();
        var csv = "codigo;nombre;naturaleza;rubro\n1;ACTIVO;D;ESF-A-EFE\n";
        (await Importador(e).Handle(new ImportAccountCatalogCommand("Mi plan", "plan.csv", Encoding.UTF8.GetBytes(csv)), CancellationToken.None)).IsSuccess.Should().BeTrue();

        (await new ValidateCatalogCommandHandler(e.Db, e.Clock, e.User, e.Emisor).Handle(new ValidateCatalogCommand("PROPIO-1"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await e.Db.AccountCatalogs.SingleAsync(c => c.Code == "PROPIO-1")).ValidatedBy.Should().Be("admin@demo");

        var retirador = new RemoveCatalogCommandHandler(e.Db, e.Clock, e.User, e.Emisor);
        (await retirador.Handle(new RemoveCatalogCommand("PUC-SOLIDARIO"), CancellationToken.None)).Error.Code.Should().Be("Accounting.Catalog.NotImported");
        (await retirador.Handle(new RemoveCatalogCommand("PROPIO-1"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await e.Db.AccountCatalogs.CountAsync(c => !c.IsDeleted)).Should().Be(1);
    }
}
