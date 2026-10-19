using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using P = IngenIA365ERP.Application.Accounting.Lending.Reglas.PlantillaDeMatrizDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 013, T0615 (T52, C70; contracts/contabilidad.md §3.7; contracts/plantillas.md §9 y §0.5): la plantilla de la matriz de Cartera
/// sobre <c>EjecutorDeImportacion</c>. Las columnas son las de §3.7 (booleanos <c>S</c>/<c>N</c>/vacío, y vacío y <c>*</c> son lo mismo);
/// <c>review</c> no guarda; <c>apply</c> es todo o nada con 422 <c>Import.Invalid</c> y <c>data.errors[] { row, column, code }</c>; la misma
/// clave con una fecha posterior es una versión nueva; las reglas son las del alta una a una (mismos códigos); y queda el evento
/// <c>Accounting.LendingRules.Imported</c>. Molde: <c>ImportInventoryPostingRulesCommandHandlerTests</c>.
/// </summary>
public class ImportLendingPostingRulesCommandHandlerTests
{
    private sealed class Escenario
    {
        public MatrizDePruebaDeCartera M { get; } = new();
        public ITabularFileReader Lector { get; } = Substitute.For<ITabularFileReader>();
        public ICurrentUserPermissions Permisos { get; } = Substitute.For<ICurrentUserPermissions>();
        public IAuditAppendOnlyWriter Auditoria { get; } = Substitute.For<IAuditAppendOnlyWriter>();

        public Escenario() => Permisos.EsMaestroGlobal.Returns(true);

        public void Archivo(params string?[][] filas)
        {
            // Un archivo de la versión 1 de la plantilla (feature 014, T144): las columnas de captaciones faltan y valen «*».
            var tabla = new TablaLeida(ColumnasV1, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");
            Lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Success(tabla)));
        }

        public async Task<Result<ImportResultDto>> ImportarAsync(ModoDeImportacion modo)
        {
            var servicios = new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider();
            var ejecutor = new EjecutorDeImportacion(M.D.Db, Lector, Permisos, servicios);
            var emisor = new AccountingAuditEmitter(Auditoria, M.D.User, M.D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
            var r = await new ImportLendingPostingRulesCommandHandler(M.D.Db, ejecutor, M.Reglas(), emisor)
                .Handle(new ImportLendingPostingRulesCommand(modo, new ArchivoDeImportacion("matriz-cartera.xlsx", [1, 2, 3])), default);
            M.D.Db.ChangeTracker.Clear();
            return r;
        }
    }

    /// <summary>Las columnas de la versión 1 de la plantilla (la de la 013), en el orden de §3.7.</summary>
    private static readonly string[] ColumnasV1 =
    [
        "operacion", "rol", "linea", "modalidad", "subsegmento", "categoria", "concepto", "empleado", "gracia", "porCuentaDelAsociado",
        "reestructurado", "claseDeRecaudador", "motivo", "sucursal", "centroDeCosto", "cuenta", "vigenteDesde", "vigenteHasta", "notas",
    ];

    /// <summary>Una fila en el orden de §3.7.</summary>
    private static string?[] Fila(string rol, string? cuenta, string desde = "2026-01-01", string? operacion = null, string? linea = null,
        string? modalidad = null, string? subsegmento = null, string? categoria = null, string? concepto = null, string? empleado = null,
        string? gracia = null, string? porCuenta = null, string? reestructurado = null, string? clase = null, string? motivo = null,
        string? sucursal = null, string? centro = null, string? hasta = null, string? notas = null) =>
        [operacion, rol, linea, modalidad, subsegmento, categoria, concepto, empleado, gracia, porCuenta, reestructurado, clase, motivo, sucursal, centro,
            cuenta, desde, hasta, notas];

    [Fact]
    public void La_plantilla_tiene_las_columnas_de_3_7_y_sus_instrucciones_salen_de_los_catalogos()
    {
        P.Definicion.Clave.Should().Be("accounting.lending-rules");
        P.Definicion.EsDeUnaHoja.Should().BeTrue();
        // Versión 2 (feature 014, T144): las cinco dimensiones de captaciones y la cuenta sugerida (informativa).
        P.Definicion.Hojas[0].Columnas.Select(c => c.Nombre).Should().Equal(
            "operacion", "rol", "linea", "modalidad", "subsegmento", "categoria", "concepto", "empleado", "gracia", "porCuentaDelAsociado",
            "reestructurado", "claseDeRecaudador", "motivo", "naturaleza", "claseDeCaptacion", "plazoDeEmision", "claseDeAporte", "marcaDeCuenta",
            "sucursal", "centroDeCosto", "cuenta", "cuentaSugerida", "vigenteDesde", "vigenteHasta", "notas");
        P.Definicion.Hojas[0].Columnas.Where(c => c.Obligatoria).Select(c => c.Nombre).Should().Equal("rol", "cuenta", "vigenteDesde");
        P.Definicion.Hoja("Datos")!.Columna("categoria")!.Reglas.Should().Contain("obligatoria en Capital, InteresPorCobrar");
        P.Definicion.Hoja("Datos")!.Columna("motivo")!.Reglas.Should().Contain("DesembolsoPorGirar: Transfer, Check, ThirdPartyPayment");
        P.Definicion.Hoja("Datos")!.Columna("empleado")!.Tipo.Should().Be(TipoDeValor.SiNoIndiferente);
    }

    [Fact]
    public async Task Review_no_guarda_y_apply_crea_con_booleanos_y_comodines()
    {
        var e = new Escenario();
        e.Archivo(
            Fila("Capital", "14410501", modalidad: "CONSUMO", subsegmento: "ConLibranza", categoria: "A", empleado: "N", linea: "*", notas: "Contadora"),
            Fila("Capital", "14690501", modalidad: "CONSUMO", categoria: "A", empleado: "S"),
            Fila("RecaudoPorAplicar", "11059501", clase: "Payroll", sucursal: "Principal"),
            Fila("Ingreso", "41502509", operacion: CreditoAjustadoDesdeComercialV1.Type, modalidad: "CONSUMO", concepto: "CurrentInterest"));

        var revision = await e.ImportarAsync(ModoDeImportacion.Review);
        revision.IsSuccess.Should().BeTrue(revision.Error?.Message);
        revision.Value.Valid.Should().BeTrue(string.Join(" | ", revision.Value.Errors.Select(x => $"{x.Row}/{x.Column}: {x.Message}")));
        revision.Value.Sheets.Single().Created.Should().Be(4);
        (await e.M.D.Db.LendingPostingRules.CountAsync()).Should().Be(0, "review corre las mismas reglas y no guarda nada");

        var aplicacion = await e.ImportarAsync(ModoDeImportacion.Apply);
        aplicacion.IsSuccess.Should().BeTrue(aplicacion.Error?.Message);
        var reglas = await e.M.D.Db.LendingPostingRules.ToListAsync();
        reglas.Should().HaveCount(4);
        var libranza = reglas.Single(r => r.SubsegmentCode == "ConLibranza");
        libranza.IsEmployee.Should().BeFalse("N es no");
        libranza.CreditLineCode.Should().BeNull("* es comodín");
        libranza.Notes.Should().Be("Contadora");
        reglas.Single(r => r.AccountId == e.M.CapitalEmpleadosA.Id).IsEmployee.Should().BeTrue();
        reglas.Single(r => r.Role == "RecaudoPorAplicar").BranchId.Should().Be(e.M.D.Principal.Id, "la sucursal se busca por código o nombre");
        reglas.Single(r => r.Role == "Ingreso").SpecificityWeight.Should().Be(2, "el hecho fijado en un rol de resultado");
        await e.Auditoria.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(d => d.Action == "Accounting.LendingRules.Imported"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_es_todo_o_nada_con_los_errores_por_fila_columna_y_codigo()
    {
        var e = new Escenario();
        e.Archivo(
            Fila("Capital", "14410501", modalidad: "CONSUMO", categoria: "A"),
            Fila("Capital", "14410501", modalidad: "CONSUMO", categoria: "A", linea: "LIB99"),
            Fila("Capital", "14410501", modalidad: "CONSUMO"),
            Fila("Capital", "51050501", modalidad: "CONSUMO", categoria: "B"),
            Fila("Capital", "14410501", modalidad: "CONSUMO", categoria: "C", clase: "Receipts"),
            Fila("Capital", "14410501", operacion: PagoAplicadoV1.Type, modalidad: "CONSUMO", categoria: "D"),
            Fila("Capital", "14410501", modalidad: "CONSUMO", categoria: "E", empleado: "quizás"));

        var r = await e.ImportarAsync(ModoDeImportacion.Apply);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Import.Invalid");
        var cuerpo = (ImportResultDto)((ErrorConDatos)r.Error).Data;
        cuerpo.Errors.Select(x => (x.Row, x.Column, x.Code)).Should().BeEquivalentTo(new[]
        {
            (3, "linea", "Accounting.LendingRule.DimensionCodeUnknown"),
            (4, "categoria", "Accounting.LendingRule.DimensionRequired"),
            (5, "cuenta", "Accounting.Account.NotEligible"),
            (6, "claseDeRecaudador", "Accounting.LendingRule.DimensionNotAllowed"),
            (7, "operacion", "Accounting.LendingRule.DimensionNotAllowed"),
            (8, "empleado", "Import.Cell.Format"),
        }, "las mismas reglas y los mismos códigos que el alta una a una");
        (await e.M.D.Db.LendingPostingRules.CountAsync()).Should().Be(0, "con un error no se guarda nada");
    }

    [Fact]
    public async Task La_misma_clave_con_fecha_posterior_es_version_nueva_y_repetir_el_archivo_no_cambia_nada()
    {
        var e = new Escenario();
        e.Archivo(Fila("Capital", "14410501", modalidad: "CONSUMO", categoria: "A"));
        (await e.ImportarAsync(ModoDeImportacion.Apply)).IsSuccess.Should().BeTrue();

        e.Archivo(
            Fila("Capital", "14410501", modalidad: "CONSUMO", categoria: "A"),
            Fila("Capital", "14420501", "2026-03-01", modalidad: "CONSUMO", categoria: "A"));
        var r = await e.ImportarAsync(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Sheets.Single().Unchanged.Should().Be(1);
        r.Value.Sheets.Single().Created.Should().Be(1);
        var reglas = await e.M.D.Db.LendingPostingRules.OrderBy(x => x.ValidFrom).ToListAsync();
        reglas.Should().HaveCount(2);
        reglas[0].ValidTo.Should().Be(new DateOnly(2026, 2, 28), "la versión nueva cierra la anterior la víspera");
        reglas[1].AccountId.Should().Be(e.M.CapitalConsumoA.Id);
    }

    [Fact]
    public async Task La_retroactividad_y_los_cruces_responden_como_en_el_alta()
    {
        var e = new Escenario();
        e.Archivo(Fila("Capital", "14410501", modalidad: "CONSUMO", categoria: "A"));
        (await e.ImportarAsync(ModoDeImportacion.Apply)).IsSuccess.Should().BeTrue();
        e.M.Contabilizado(PagoAplicadoV1.Type, new DateOnly(2026, 3, 9), MatrizDePruebaDeCartera.Pago(), "000123");

        e.Archivo(
            Fila("Capital", "14420501", "2026-03-05", modalidad: "CONSUMO", categoria: "A"),
            Fila("Capital", "14420501", modalidad: "CONSUMO", categoria: "A"));
        var r = await e.ImportarAsync(ModoDeImportacion.Review);

        r.Value.Valid.Should().BeFalse();
        r.Value.Errors.Select(x => (x.Row, x.Column, x.Code)).Should().BeEquivalentTo(new[]
        {
            (2, "vigenteDesde", "Accounting.LendingRule.RetroactiveOverPosted"),
            (3, "cuenta", "Accounting.LendingRule.Overlaps"),
        });
    }

    [Fact]
    public async Task Una_fila_sugerida_que_vuelve_sin_cuenta_se_ignora()
    {
        var e = new Escenario();
        e.Archivo(
            Fila("Capital", null, modalidad: "CONSUMO", categoria: "B", notas: P.PendienteDeValidar),
            Fila("Capital", "14410501", modalidad: "CONSUMO", categoria: "A"),
            Fila("Capital", null, modalidad: "CONSUMO", categoria: "C"));

        var r = await e.ImportarAsync(ModoDeImportacion.Review);

        r.Value.Errors.Select(x => (x.Row, x.Column, x.Code)).Should().BeEquivalentTo(new[] { (4, "cuenta", "Import.Cell.Required") },
            "sólo la fila sugerida (con la marca) puede volver sin cuenta");
        r.Value.Sheets.Single().Unchanged.Should().Be(1);
        r.Value.Sheets.Single().Created.Should().Be(1);
    }
}
