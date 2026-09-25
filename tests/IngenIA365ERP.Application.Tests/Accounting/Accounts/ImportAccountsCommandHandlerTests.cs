using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Accounts;

/// <summary>
/// Carga masiva de auxiliares (E2, 2026-09-22): las mismas reglas que crear una a una —nivel,
/// longitud, padre, cuentas propias sólo donde el catálogo no trae hijos— y con un error no se
/// guarda nada; una cuenta que ya existe se actualiza, y con movimientos sólo cambia el nombre.
/// </summary>
public class ImportAccountsCommandHandlerTests
{
    private static readonly string[] Encabezados =
    [
        "cuenta", "nombre", "aplicaA", "exigeTercero", "exigeDocumento", "exigeCentro", "exigeSucursal",
        "banco", "numeroCuenta", "claseImpuesto", "conceptoTributario", "exigeBase", "tarifa", "tarifaDesde",
    ];

    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public ITabularFileReader Lector { get; } = Substitute.For<ITabularFileReader>();
        public IAuditAppendOnlyWriter Auditoria { get; } = Substitute.For<IAuditAppendOnlyWriter>();
        public AccountingAuditEmitter Emisor { get; }
        public ChartOfAccount Subcuenta { get; }
        public ChartOfAccount SinHijas { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Auditoria, D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
            // 511005 (nivel 4) viene del catálogo y tiene su clase: debajo van auxiliares de nivel 5.
            Subcuenta = Catalogo("511005", 4);
            // 3505 (nivel 3) no trae subcuentas: ahí la empresa crea la propia de 6 dígitos y debajo la auxiliar.
            SinHijas = Catalogo("3505", 3, AccountNature.Credit);
        }

        private ChartOfAccount Catalogo(string code, byte nivel, AccountNature naturaleza = AccountNature.Debit)
        {
            var cuenta = new ChartOfAccount
            {
                Code = code, Name = $"Catálogo {code}", Level = nivel, Nature = naturaleza, NiifItemCode = "X",
                Origin = AccountOrigin.Catalog, IsMovement = false, IsActive = true, CreatedBy = "test",
            };
            D.Db.ChartOfAccounts.Add(cuenta);
            D.Db.SaveChanges();
            return cuenta;
        }

        public ImportAccountsCommandHandler Importador() => new(D.Db, Lector, D.Clock, D.User, Emisor);

        public void Archivo(params string?[][] filas)
        {
            var leidas = filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList();
            Lector.LeerAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Success(new TablaLeida(Encabezados, leidas, "csv"))));
        }

        public static string?[] Fila(string cuenta, string nombre, string modulos = "CNT", string tercero = "", string documento = "",
            string centro = "", string sucursal = "", string banco = "", string numero = "", string impuesto = "", string concepto = "",
            string baseGravable = "", string tarifa = "", string desde = "") =>
            [cuenta, nombre, modulos, tercero, documento, centro, sucursal, banco, numero, impuesto, concepto, baseGravable, tarifa, desde];

        public Task<Result<ImportacionDeCuentasDto>> ImportarAsync() =>
            Importador().Handle(new ImportAccountsCommand("cuentas.csv", [1, 2, 3]), CancellationToken.None);

        public static IReadOnlyList<Application.Common.Imports.ErrorDeFila> Errores(Error error)
        {
            var datos = ((ErrorConDatos)error).Data;
            return (IReadOnlyList<Application.Common.Imports.ErrorDeFila>)datos.GetType().GetProperty("errors")!.GetValue(datos)!;
        }
    }

    [Fact]
    public async Task Crea_auxiliares_con_sus_reglas_y_la_cuenta_propia_donde_el_catalogo_no_trae_hijas()
    {
        var e = new Escenario();
        e.Archivo(
            Escenario.Fila("51100501", "Seguros generales", "CNT NOM", tercero: "sí", centro: "sí"),
            // La propia de 6 dígitos y su auxiliar, en desorden a propósito: el importador las ordena.
            Escenario.Fila("35050501", "Excedente del ejercicio", "CNT"),
            Escenario.Fila("350505", "Excedentes del ejercicio", ""),
            Escenario.Fila("51100502", "Retención por honorarios", "CNT", impuesto: "Withholding", concepto: "2365", baseGravable: "sí", tarifa: "11", desde: "2026-01-01"));

        var r = await e.ImportarAsync();

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Created.Should().Be(4);
        r.Value.Updated.Should().Be(0);

        var seguros = await e.D.Db.ChartOfAccounts.SingleAsync(a => a.Code == "51100501");
        seguros.Level.Should().Be(5);
        seguros.IsMovement.Should().BeTrue();
        seguros.ParentId.Should().Be(e.Subcuenta.Id);
        seguros.Nature.Should().Be(AccountNature.Debit, "la naturaleza la hereda del padre");
        seguros.EnabledModules.Should().Be(AccountingModules.Accounting | AccountingModules.Payroll);
        seguros.RequiresThirdParty.Should().BeTrue();
        seguros.RequiresCostCenter.Should().BeTrue();
        seguros.RequiresCrossDocument.Should().BeFalse();

        var propia = await e.D.Db.ChartOfAccounts.SingleAsync(a => a.Code == "350505");
        propia.Level.Should().Be(4);
        propia.IsMovement.Should().BeFalse();
        propia.ParentId.Should().Be(e.SinHijas.Id);
        var auxiliar = await e.D.Db.ChartOfAccounts.SingleAsync(a => a.Code == "35050501");
        auxiliar.ParentId.Should().Be(propia.Id, "la auxiliar cuelga de la propia creada en el mismo archivo");
        auxiliar.Nature.Should().Be(AccountNature.Credit);

        var retencion = await e.D.Db.ChartOfAccounts.Include(a => a.TaxRates).SingleAsync(a => a.Code == "51100502");
        retencion.TaxKind.Should().Be(TaxKind.Withholding);
        retencion.TaxConceptCode.Should().Be("2365");
        retencion.RequiresTaxBase.Should().BeTrue();
        retencion.TaxRates.Should().ContainSingle().Which.Rate.Should().Be(0.11m, "11 en la plantilla es el 11 %");
        await e.Auditoria.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(d => d.Action == "Accounting.Accounts.Imported"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Con_errores_los_lista_por_fila_y_no_guarda_nada()
    {
        var e = new Escenario();
        e.Archivo(
            Escenario.Fila("5110050X", "Código con letra"),                              // fila 2
            Escenario.Fila("51100501", "Sin nombre repetida"),                           // fila 3 (válida)
            Escenario.Fila("51100501", "Repetida"),                                      // fila 4: duplicada en el archivo
            Escenario.Fila("511005011234", "Demasiado larga"),                           // fila 5: 12 dígitos = nivel 6 bajo una de nivel 5 inexistente
            Escenario.Fila("99999999", "Sin padre"),                                     // fila 6
            Escenario.Fila("51100503", "Módulo raro", "XXX"),                            // fila 7
            Escenario.Fila("51100504", "Bandera rara", "CNT", tercero: "quizás"),        // fila 8
            Escenario.Fila("51100505", "Banco que no existe", "CNT", banco: "Banco Fantasma"),  // fila 9
            Escenario.Fila("51100506", "Impuesto raro", "CNT", impuesto: "Patente"),     // fila 10
            Escenario.Fila("51100507", "Base sin clase", "CNT", baseGravable: "sí"),     // fila 11
            Escenario.Fila("", "Sin código"));                                           // fila 12

        var r = await e.ImportarAsync();

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Accounts.Invalid");
        var errores = Escenario.Errores(r.Error);
        errores.Select(x => (x.Row, x.Column)).Should().Contain(
        [
            (2, "cuenta"), (4, "cuenta"), (6, "cuenta"), (7, "aplicaA"), (8, "exigeTercero"),
            (9, "banco"), (10, "claseImpuesto"), (11, "exigeBase"), (12, "cuenta"),
        ]);
        (await e.D.Db.ChartOfAccounts.CountAsync(a => a.Origin == AccountOrigin.Company)).Should().Be(0, "nada a medias");
    }

    [Fact]
    public async Task Una_cuenta_que_ya_existe_se_actualiza_y_con_movimientos_solo_cambia_el_nombre()
    {
        var e = new Escenario();
        e.Archivo(Escenario.Fila("51100501", "Seguros", "CNT"));
        (await e.ImportarAsync()).IsSuccess.Should().BeTrue();

        // Otra vez, igual: ni se crea ni se actualiza.
        var igual = await e.ImportarAsync();
        igual.Value.Created.Should().Be(0);
        igual.Value.Unchanged.Should().Be(1);

        // Cambio de nombre y de reglas: se aplica mientras no haya movimientos.
        e.Archivo(Escenario.Fila("51100501", "Seguros generales", "CNT NOM", tercero: "sí"));
        var cambio = await e.ImportarAsync();
        cambio.Value.Updated.Should().Be(1);
        var cuenta = await e.D.Db.ChartOfAccounts.SingleAsync(a => a.Code == "51100501");
        cuenta.Name.Should().Be("Seguros generales");
        cuenta.RequiresThirdParty.Should().BeTrue();

        // Con movimientos, las reglas quedan fijas: el archivo que las cambia falla y dice desde cuándo.
        cuenta.FirstMovementAt = new DateOnly(2026, 3, 1);
        await e.D.Db.SaveChangesAsync();
        e.Archivo(Escenario.Fila("51100501", "Seguros generales", "CNT"));
        var bloqueada = await e.ImportarAsync();
        bloqueada.IsFailure.Should().BeTrue();
        Escenario.Errores(bloqueada.Error).Should().ContainSingle().Which.Code.Should().Be("Accounting.Account.Locked");

        // Sólo el nombre, en cambio, sí.
        e.Archivo(Escenario.Fila("51100501", "Seguros y pólizas", "CNT NOM", tercero: "sí"));
        var soloNombre = await e.ImportarAsync();
        soloNombre.IsSuccess.Should().BeTrue(soloNombre.Error?.Message);
        (await e.D.Db.ChartOfAccounts.SingleAsync(a => a.Code == "51100501")).Name.Should().Be("Seguros y pólizas");
    }

    [Fact]
    public async Task Una_cuenta_del_catalogo_no_se_edita_ni_se_le_cuelga_donde_el_catalogo_ya_define_sus_cuentas()
    {
        var e = new Escenario();
        e.Archivo(Escenario.Fila("511005", "Otro nombre para la subcuenta"));
        Escenario.Errores((await e.ImportarAsync()).Error).Should().ContainSingle().Which.Code.Should().Be("Accounting.Account.FromCatalog");

        // 5110 (nivel 3) tiene a 511005 como hija del catálogo: no admite cuentas propias debajo.
        e.D.Db.ChartOfAccounts.Add(new ChartOfAccount
        {
            Code = "5110", Name = "Gastos generales", Level = 3, Nature = AccountNature.Debit, NiifItemCode = "X",
            Origin = AccountOrigin.Catalog, IsMovement = false, IsActive = true, CreatedBy = "test",
        });
        await e.D.Db.SaveChangesAsync();
        var subcuenta = await e.D.Db.ChartOfAccounts.SingleAsync(a => a.Code == "511005");
        subcuenta.ParentId = (await e.D.Db.ChartOfAccounts.SingleAsync(a => a.Code == "5110")).Id;
        await e.D.Db.SaveChangesAsync();

        e.Archivo(Escenario.Fila("511099", "Cuenta propia donde no corresponde"));
        Escenario.Errores((await e.ImportarAsync()).Error).Should().ContainSingle().Which.Message.Should().Contain("el catálogo ya define sus cuentas");
    }

    [Fact]
    public async Task Una_cuenta_que_no_recibe_movimiento_no_lleva_reglas()
    {
        var e = new Escenario();
        e.Archivo(Escenario.Fila("350505", "Excedentes", "CNT", tercero: "sí"));

        var r = await e.ImportarAsync();

        Escenario.Errores(r.Error).Should().ContainSingle().Which.Code.Should().Be("Accounting.Account.NotMovement");
    }
}
