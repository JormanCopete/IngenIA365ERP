using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Accounts;

/// <summary>
/// T060 — US2: las auxiliares cuelgan de su padre con su prefijo y la longitud del nivel (7–9 y 10–12), sólo
/// la del nivel de movimiento recibe reglas, las del catálogo no se editan ni eliminan, con
/// movimientos las reglas se bloquean, y el buscador sólo ofrece lo que cada módulo puede usar.
/// </summary>
public class AccountCommandsTests
{
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public IAccountReferenceFinder Referencias { get; } = Substitute.For<IAccountReferenceFinder>();
        public AccountingAuditEmitter Emisor { get; }
        public ChartOfAccount Subcuenta { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Substitute.For<IAuditAppendOnlyWriter>(), D.User, CooperativaDePrueba.Actual, D.Clock, NullLogger<AccountingAuditEmitter>.Instance);
            Referencias.BuscarAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ReferenciaDeCuenta>>([]));
            // Un tramo del catálogo: 1 › 11 › 1105 › 110505, ninguna de movimiento (Setup: movimiento en 5; una auxiliar lleva de 7 a 9 dígitos).
            var clase = Catalogo("1", 1, null); var grupo = Catalogo("11", 2, clase); var cuenta = Catalogo("1105", 3, grupo);
            Subcuenta = Catalogo("110505", 4, cuenta);
            var setup = D.Db.AccountingSetups.Single();
            setup.MovementLevel = 5;
            D.Db.SaveChanges();
        }

        public ChartOfAccount Catalogo(string code, byte level, ChartOfAccount? padre)
        {
            var c = new ChartOfAccount { Code = code, Name = $"Cuenta {code}", Level = level, Nature = AccountNature.Debit, ParentId = padre?.Id, NiifItemCode = "ESF-A-EFE", Origin = AccountOrigin.Catalog, IsMovement = false, CreatedBy = "test" };
            D.Db.ChartOfAccounts.Add(c);
            D.Db.SaveChanges();
            return c;
        }

        public CreateAccountCommandHandler Creador() => new(D.Db, D.Clock, D.User, Emisor);
        public UpdateAccountCommandHandler Editor() => new(D.Db, D.Clock, D.User, Emisor);
        public DeleteAccountCommandHandler Eliminador() => new(D.Db, Referencias, D.Clock, D.User, Emisor);

        public CreateAccountCommand Peticion(string code = "11050501", Guid? padre = null, IReadOnlyList<string>? modulos = null, bool tercero = false) =>
            new(code, "Caja principal", padre ?? Subcuenta.PublicId, modulos ?? ["CNT", "NOM"], tercero, false, false, false, null, null);
    }

    [Fact]
    public async Task Crea_la_auxiliar_de_movimiento_con_prefijo_longitud_herencia_y_reglas()
    {
        var e = new Escenario();

        var r = await e.Creador().Handle(e.Peticion(tercero: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var cuenta = await e.D.Db.ChartOfAccounts.SingleAsync(a => a.PublicId == r.Value);
        cuenta.Level.Should().Be(5);
        cuenta.IsMovement.Should().BeTrue();
        cuenta.Origin.Should().Be(AccountOrigin.Company);
        cuenta.Nature.Should().Be(AccountNature.Debit, "hereda del padre");
        cuenta.NiifItemCode.Should().Be("ESF-A-EFE");
        cuenta.ParentId.Should().Be(e.Subcuenta.Id);
        cuenta.EnabledModules.Should().Be(AccountingModules.Accounting | AccountingModules.Payroll);
        cuenta.RequiresThirdParty.Should().BeTrue();
    }

    [Fact]
    public async Task Rechaza_nivel_por_encima_del_movimiento_longitud_distinta_prefijo_ajeno_y_duplicado()
    {
        var e = new Escenario();
        var aux = await e.Creador().Handle(e.Peticion(), CancellationToken.None);
        aux.IsSuccess.Should().BeTrue(aux.Error.Message);

        (await e.Creador().Handle(e.Peticion("1105050101", aux.Value), CancellationToken.None)).Error.Code.Should().Be("Accounting.Account.LevelNotAllowed");
        (await e.Creador().Handle(e.Peticion("110505"), CancellationToken.None)).Error.Code.Should().Be("Accounting.Account.CodeInvalid", "6 dígitos es la subcuenta: una auxiliar lleva de 7 a 9");
        (await e.Creador().Handle(e.Peticion("1105050001"), CancellationToken.None)).Error.Code.Should().Be("Accounting.Account.CodeInvalid", "10 dígitos ya son de nivel 6");
        (await e.Creador().Handle(e.Peticion("1105059"), CancellationToken.None)).IsSuccess.Should().BeTrue("7 dígitos es el mínimo de una auxiliar");
        (await e.Creador().Handle(e.Peticion("110505999"), CancellationToken.None)).IsSuccess.Should().BeTrue("9 dígitos es el máximo de una auxiliar");
        (await e.Creador().Handle(e.Peticion("11059901"), CancellationToken.None)).Error.Code.Should().Be("Accounting.Account.CodeInvalid");
        (await e.Creador().Handle(e.Peticion("11050501"), CancellationToken.None)).Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        (await e.Creador().Handle(e.Peticion(padre: Guid.NewGuid()), CancellationToken.None)).Error.Code.Should().Be("Accounting.Account.ParentNotFound");
    }

    [Fact]
    public async Task Bajo_un_nodo_del_catalogo_sin_hijos_la_empresa_crea_su_subcuenta_y_solo_ahi()
    {
        // El CUIF solidario deja 127 cuentas y 6 grupos sin subcuentas (reservas, fondos, provisiones…):
        // la empresa crea las suyas ahí, con el largo exacto del nivel; bajo 1105, que sí trae 110505, no.
        var e = new Escenario();
        var patrimonio = e.Catalogo("3", 1, null); var reservas = e.Catalogo("32", 2, patrimonio); var reserva = e.Catalogo("3205", 3, reservas);
        var pasivos = e.Catalogo("2", 1, null); var diferido = e.Catalogo("25", 2, pasivos);
        var cuenta1105 = await e.D.Db.ChartOfAccounts.SingleAsync(a => a.Code == "1105");

        var subcuenta = await e.Creador().Handle(e.Peticion("320505", reserva.PublicId), CancellationToken.None);
        subcuenta.IsSuccess.Should().BeTrue(subcuenta.Error.Message);
        var propia = await e.D.Db.ChartOfAccounts.SingleAsync(a => a.PublicId == subcuenta.Value);
        propia.Level.Should().Be(4);
        propia.IsMovement.Should().BeFalse("el movimiento está en el 5");
        propia.Origin.Should().Be(AccountOrigin.Company);
        propia.Nature.Should().Be(reserva.Nature);
        (await e.Creador().Handle(e.Peticion("32050501", subcuenta.Value), CancellationToken.None)).IsSuccess.Should().BeTrue("de la subcuenta propia cuelgan las auxiliares");

        (await e.Creador().Handle(e.Peticion("2505", diferido.PublicId), CancellationToken.None)).IsSuccess.Should().BeTrue("un grupo sin cuentas admite una cuenta propia de 4 dígitos");
        (await e.Creador().Handle(e.Peticion("32051", reserva.PublicId), CancellationToken.None)).Error.Code.Should().Be("Accounting.Account.CodeInvalid", "una subcuenta lleva 6 dígitos exactos");
        var bajo1105 = await e.Creador().Handle(e.Peticion("110599", cuenta1105.PublicId), CancellationToken.None);
        bajo1105.Error.Code.Should().Be("Accounting.Account.CodeInvalid");
        bajo1105.Error.Message.Should().Contain("el catálogo ya define sus cuentas");
    }

    [Fact]
    public async Task Las_cuentas_del_catalogo_no_se_editan_ni_eliminan_solo_se_inactivan()
    {
        var e = new Escenario();

        (await e.Editor().Handle(new UpdateAccountCommand(e.Subcuenta.PublicId, "Otro", ["CNT"], false, false, false, false, null, null), CancellationToken.None))
            .Error.Code.Should().Be("Accounting.Account.FromCatalog");
        (await e.Eliminador().Handle(new DeleteAccountCommand(e.Subcuenta.PublicId), CancellationToken.None)).Error.Code.Should().Be("Accounting.Account.FromCatalog");

        var inactivar = await new SetAccountActiveCommandHandler(e.D.Db, e.D.Clock, e.D.User, e.Emisor).Handle(new SetAccountActiveCommand(e.Subcuenta.PublicId, false), CancellationToken.None);
        inactivar.IsSuccess.Should().BeTrue();
        (await e.D.Db.ChartOfAccounts.SingleAsync(a => a.Id == e.Subcuenta.Id)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Con_movimientos_solo_cambia_el_nombre_y_las_reglas_dicen_desde_cuando()
    {
        var e = new Escenario();
        var id = (await e.Creador().Handle(e.Peticion(), CancellationToken.None)).Value;
        var cuenta = await e.D.Db.ChartOfAccounts.SingleAsync(a => a.PublicId == id);
        cuenta.FirstMovementAt = new DateOnly(2026, 3, 10);
        await e.D.Db.SaveChangesAsync();

        var reglas = await e.Editor().Handle(new UpdateAccountCommand(id, "Caja principal", ["CNT", "NOM"], true, false, false, false, null, null), CancellationToken.None);
        reglas.Error.Code.Should().Be("Accounting.Account.Locked");
        reglas.Error.Message.Should().Contain("2026-03-10");

        var nombre = await e.Editor().Handle(new UpdateAccountCommand(id, "Caja general", ["CNT", "NOM"], false, false, false, false, null, null), CancellationToken.None);
        nombre.IsSuccess.Should().BeTrue(nombre.Error.Message);
        (await e.D.Db.ChartOfAccounts.SingleAsync(a => a.PublicId == id)).Name.Should().Be("Caja general");
    }

    [Fact]
    public async Task Eliminar_una_referenciada_o_con_movimientos_se_rechaza_y_dice_donde()
    {
        var e = new Escenario();
        var id = (await e.Creador().Handle(e.Peticion(), CancellationToken.None)).Value;
        e.Referencias.BuscarAsync(Arg.Any<int>(), "11050501", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ReferenciaDeCuenta>>([new("Nómina", "Cuentas por concepto", "SALARIO")]));

        var r = await e.Eliminador().Handle(new DeleteAccountCommand(id), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Account.Referenced");
        r.Error.Message.Should().Contain("SALARIO");
        r.Error.Should().BeOfType<ErrorConDatos>();

        e.Referencias.BuscarAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ReferenciaDeCuenta>>([]));
        (await e.D.Db.ChartOfAccounts.SingleAsync(a => a.PublicId == id)).FirstMovementAt = new DateOnly(2026, 3, 1);
        await e.D.Db.SaveChangesAsync();
        (await e.Eliminador().Handle(new DeleteAccountCommand(id), CancellationToken.None)).Error.Code.Should().Be("Accounting.Account.HasMovements");
    }

    [Fact]
    public async Task Eliminar_una_auxiliar_libre_la_retira_sin_borrarla()
    {
        var e = new Escenario();
        var id = (await e.Creador().Handle(e.Peticion(), CancellationToken.None)).Value;

        (await e.Eliminador().Handle(new DeleteAccountCommand(id), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await e.D.Db.ChartOfAccounts.SingleAsync(a => a.PublicId == id)).IsDeleted.Should().BeTrue();
        // Y el código queda libre para crearla de nuevo (índice único filtrado).
        (await e.Creador().Handle(e.Peticion(), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task El_buscador_no_ofrece_agrupacion_inactivas_ni_cuentas_de_otro_modulo()
    {
        var e = new Escenario();
        await e.Creador().Handle(e.Peticion("11050501", modulos: ["NOM"]), CancellationToken.None);
        var inactivaId = (await e.Creador().Handle(e.Peticion("11050502", modulos: ["NOM", "CAR"]), CancellationToken.None)).Value;
        await new SetAccountActiveCommandHandler(e.D.Db, e.D.Clock, e.D.User, e.Emisor).Handle(new SetAccountActiveCommand(inactivaId, false), CancellationToken.None);
        await e.Creador().Handle(e.Peticion("11050503", modulos: ["CAR"]), CancellationToken.None);
        var buscador = new SearchAccountsQueryHandler(e.D.Db);

        var nomina = await buscador.Handle(new SearchAccountsQuery("1105", "NOM"), CancellationToken.None);
        nomina.Value.Select(c => c.Code).Should().Equal("11050501");

        var cartera = await buscador.Handle(new SearchAccountsQuery("Caja", "CAR"), CancellationToken.None);
        cartera.Value.Select(c => c.Code).Should().Equal("11050503");

        var todas = await buscador.Handle(new SearchAccountsQuery("11", OnlyMovement: false), CancellationToken.None);
        todas.Value.Select(c => c.Code).Should().Contain(["11", "1105", "110505", "11050501", "11050502", "11050503"]);
    }

    [Fact]
    public async Task El_arbol_da_hijos_por_padre_y_dice_si_tienen_hijos()
    {
        var e = new Escenario();
        await e.Creador().Handle(e.Peticion(), CancellationToken.None);
        var arbol = new GetAccountTreeQueryHandler(e.D.Db);

        var raiz = await arbol.Handle(new GetAccountTreeQuery(), CancellationToken.None);
        raiz.Value.Should().ContainSingle(n => n.Code == "1").Which.HasChildren.Should().BeTrue();

        var hijos = await arbol.Handle(new GetAccountTreeQuery(e.Subcuenta.PublicId), CancellationToken.None);
        hijos.Value.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Code = "11050501", IsMovement = true, HasChildren = false, Origin = "Company" });
    }

    [Fact]
    public async Task Las_parametrizaciones_invalidas_nombran_concepto_y_cuenta_y_los_vinculos_que_faltan()
    {
        var e = new Escenario();
        var okId = (await e.Creador().Handle(e.Peticion("11050501", modulos: ["NOM"], tercero: true), CancellationToken.None)).Value;
        var ok = await e.D.Db.ChartOfAccounts.SingleAsync(a => a.PublicId == okId);
        // SALUD_EMPLEADOR es un aporte: su tercero es la EPS del empleado, así que la EPS sin persona vinculada es una parametrización inválida.
        e.D.Db.PayrollConceptDefinitions.Add(new PayrollConceptDefinition { Code = "SALUD_EMPLEADOR", Name = "Salud (aporte del empleador)", Nature = IngenIA365ERP.Domain.Enums.Payroll.ConceptNature.EmployerContribution, CreatedBy = "test" });
        e.D.Db.PayrollConceptDefinitionAccounts.Add(new PayrollConceptDefinitionAccount { ConceptCode = "SALUD_EMPLEADOR", DebitAccountId = ok.Id, CreditAccountId = e.Subcuenta.Id, CreatedBy = "test" });
        e.D.Db.HealthInsuranceProviders.Add(new HealthInsuranceProvider { Code = "EPS1", Name = "EPS Sura", CreatedBy = "test" });
        await e.D.Db.SaveChangesAsync();

        var r = await new ListInvalidParameterizationsQueryHandler(e.D.Db).Handle(new ListInvalidParameterizationsQuery(), CancellationToken.None);

        r.Value.Should().Contain(p => p.Where == "Cuentas por concepto" && p.Detail.StartsWith("SALUD_EMPLEADOR") && p.AccountCode == "110505" && p.Problem.Contains("no es de movimiento"));
        r.Value.Should().Contain(p => p.Where == "EPS" && p.Detail == "EPS Sura");
    }
}
