using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Reports;

/// <summary>
/// Revisión E2 de la feature 009, hallazgos 8 y 13. (8) La exportación se define en un solo sitio:
/// un <c>format</c> con espacios o mayúsculas alrededor de «json» es una consulta de pantalla y no
/// deja rastro de exportación —antes el filtro de la API no exigía el permiso y el handler igual
/// auditaba <c>Accounting.Report.Exported</c>—. (13) El filtro <c>niifItem</c> acota el libro a las
/// cuentas del rubro y de todos sus descendientes por <c>ParentCode</c>, y un rubro que no existe
/// para el grupo de la empresa responde su propio código de error.
/// </summary>
public class FiltroPorRubroYFormatoTests
{
    // ------------------------------------------------------------------------------------ formato --

    [Theory]
    [InlineData(null, "json", false)]
    [InlineData("", "json", false)]
    [InlineData("   ", "json", false)]
    [InlineData("json", "json", false)]
    [InlineData(" json", "json", false)]
    [InlineData("JSON ", "json", false)]
    [InlineData("xlsx", "xlsx", true)]
    [InlineData(" PDF ", "pdf", true)]
    [InlineData("Docx", "docx", true)]
    public void El_formato_se_normaliza_con_una_sola_regla_y_solo_lo_distinto_de_json_es_exportar(string? formato, string normalizado, bool exporta)
    {
        var f = new FiltrosDeInforme { Format = formato };

        f.FormatoNormalizado.Should().Be(normalizado);
        f.EsExportacion.Should().Be(exporta);
        // La API decide con la misma función: si estas dos divergen, la auditoría vuelve a mentir.
        FormatosDeInforme.Normalizar(formato).Should().Be(f.FormatoNormalizado);
        FormatosDeInforme.EsExportacion(formato).Should().Be(f.EsExportacion);
    }

    [Fact]
    public void Un_formato_desconocido_no_es_valido_pero_los_cuatro_del_contrato_si_con_espacios_y_mayusculas()
    {
        FormatosDeInforme.EsValido(null).Should().BeTrue();
        FormatosDeInforme.EsValido(" JSON").Should().BeTrue();
        FormatosDeInforme.EsValido("Xlsx ").Should().BeTrue();
        FormatosDeInforme.EsValido("pdf").Should().BeTrue();
        FormatosDeInforme.EsValido("docx").Should().BeTrue();
        FormatosDeInforme.EsValido("csv").Should().BeFalse();
    }

    [Theory]
    [InlineData(" json")]
    [InlineData("JSON ")]
    public async Task Consultar_con_json_entre_espacios_o_mayusculas_no_deja_rastro_de_exportacion(string formato)
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 100m);

        var r = await e.Balance().Handle(new TrialBalanceQuery(new FiltrosDeInforme { Format = formato }), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        await e.Audit.DidNotReceive().AppendAsync(Arg.Any<AuditEventDocument>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exportar_audita_el_formato_ya_normalizado()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 100m);

        await e.Balance().Handle(new TrialBalanceQuery(new FiltrosDeInforme { Format = " XLSX " }), CancellationToken.None);

        // Los filtros se guardan tal como llegaron (ahí sí va « XLSX »); el formato del evento es el normalizado.
        await e.Audit.Received(1).AppendAsync(
            Arg.Is<AuditEventDocument>(a => a.Action == "Accounting.Report.Exported" && a.NewValuesJson!.Contains("\"formato\":\"xlsx\"")),
            Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------------------------- rubro --

    [Fact]
    public async Task El_rubro_acota_el_libro_a_sus_cuentas_y_a_las_de_todos_sus_descendientes()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 100m);     // 110505 (ESF-A-EFE)  ↔ 413505 (ERI-ING)
        await e.ContabilizarAsync(e.Cartera, e.Ingreso, 50m);   // 130505 (ESF-A-CAR-CRE) ↔ 413505
        await e.ContabilizarAsync(e.Gasto, e.Proveedor, 30m);   // 510505 (ERI-GAD)   ↔ 220505 (ESF-P)

        // Todo el activo: el rubro raíz y sus dos niveles de hijos (ESF-A → ESF-A-CAR → ESF-A-CAR-CRE).
        (await e.CuentasDelLibroAsync("ESF-A")).Should().BeEquivalentTo(["110505", "130505"]);
        // Un rubro hoja: sólo sus cuentas.
        (await e.CuentasDelLibroAsync("ESF-A-EFE")).Should().BeEquivalentTo(["110505"]);
        // Un rubro intermedio: lo suyo y lo que cuelga; el código se admite en minúsculas y con espacios.
        (await e.CuentasDelLibroAsync(" esf-a-car ")).Should().BeEquivalentTo(["130505"]);
        // Un rubro del ERI.
        (await e.CuentasDelLibroAsync("ERI-ING")).Should().BeEquivalentTo(["413505", "413505"]);
    }

    [Fact]
    public async Task El_rubro_se_combina_con_los_demas_filtros_y_el_encabezado_lo_dice()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 100m, sucursal: e.D.Principal.Id);
        await e.ContabilizarAsync(e.Cartera, e.Ingreso, 50m, sucursal: e.D.Norte.Id);

        var filtros = new FiltrosDeInforme { NiifItem = "ESF-A", Branch = e.D.Norte.PublicId };
        var ctx = await MovimientosContables.PrepararAsync(e.D.Db, e.D.Alcance, e.D.Clock, filtros, CancellationToken.None);

        ctx.IsSuccess.Should().BeTrue(ctx.Error.Message);
        var codigos = await MovimientosContables.Base(e.D.Db, ctx.Value).Select(x => x.Account!.Code).ToListAsync();
        codigos.Should().BeEquivalentTo(["130505"]);
        EncabezadoDeInforme.Filtros(filtros, ctx.Value).Should().Contain("rubro ESF-A Activo").And.Contain("sucursal Norte");
    }

    [Fact]
    public async Task Un_rubro_que_no_existe_para_el_grupo_responde_su_codigo()
    {
        var e = new Escenario();

        var ctx = await MovimientosContables.PrepararAsync(e.D.Db, e.D.Alcance, e.D.Clock, new FiltrosDeInforme { NiifItem = "ESF-Z" }, CancellationToken.None);
        ctx.IsFailure.Should().BeTrue();
        ctx.Error.Code.Should().Be("Accounting.Report.NiifItemNotFound");

        // Un rubro de otro grupo NIIF tampoco existe para esta empresa (grupo 2).
        var otroGrupo = await MovimientosContables.PrepararAsync(e.D.Db, e.D.Alcance, e.D.Clock, new FiltrosDeInforme { NiifItem = "G1-SOLO" }, CancellationToken.None);
        otroGrupo.Error.Code.Should().Be("Accounting.Report.NiifItemNotFound");

        // Y el handler lo propaga tal cual: la pantalla recibe un 404 con este código, no un libro vacío.
        var r = await e.Balance().Handle(new TrialBalanceQuery(new FiltrosDeInforme { NiifItem = "ESF-Z" }), CancellationToken.None);
        r.Error.Code.Should().Be("Accounting.Report.NiifItemNotFound");
    }

    [Fact]
    public async Task Sin_rubro_el_libro_no_se_acota()
    {
        var e = new Escenario();
        await e.ContabilizarAsync(e.Caja, e.Ingreso, 100m);

        var ctx = await MovimientosContables.PrepararAsync(e.D.Db, e.D.Alcance, e.D.Clock, new FiltrosDeInforme { NiifItem = "  " }, CancellationToken.None);

        ctx.IsSuccess.Should().BeTrue(ctx.Error.Message);
        ctx.Value.Rubro.Should().BeNull();
        (await MovimientosContables.Base(e.D.Db, ctx.Value).CountAsync()).Should().Be(2);
    }

    // --------------------------------------------------------------------------------- escenario --

    /// <summary>
    /// Rubros del grupo 2 en tres niveles (ESF-A → ESF-A-CAR → ESF-A-CAR-CRE) y cuentas de
    /// movimiento que apuntan a ellos, más un rubro de otro grupo que no debe verse.
    /// </summary>
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public IAuditAppendOnlyWriter Audit { get; } = Substitute.For<IAuditAppendOnlyWriter>();
        public AccountingAuditEmitter Emisor { get; }

        public ChartOfAccount Caja { get; }
        public ChartOfAccount Cartera { get; }
        public ChartOfAccount Proveedor { get; }
        public ChartOfAccount Ingreso { get; }
        public ChartOfAccount Gasto { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Audit, D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
            (string Code, string Name, byte Grupo, string? Parent)[] rubros =
            [
                ("ESF-A", "Activo", 2, null),
                ("ESF-A-EFE", "Efectivo y equivalentes", 2, "ESF-A"),
                ("ESF-A-CAR", "Cartera de créditos", 2, "ESF-A"),
                ("ESF-A-CAR-CRE", "Créditos de consumo", 2, "ESF-A-CAR"),
                ("ESF-P", "Pasivo", 2, null),
                ("ERI-ING", "Ingresos", 2, null),
                ("ERI-GAD", "Gastos de administración", 2, null),
                ("G1-SOLO", "Rubro de otro grupo", 1, null),
            ];
            foreach (var r in rubros)
                D.Db.FinancialStatementItems.Add(new FinancialStatementItem
                {
                    NiifGroup = r.Grupo, Code = r.Code, Name = r.Name, Statement = r.Code.StartsWith("ERI") ? FinancialStatementKind.IncomeStatement : FinancialStatementKind.FinancialPosition,
                    Section = "Prueba", Order = 1, Sign = 1, ParentCode = r.Parent, CreatedBy = "test",
                });
            D.Db.SaveChanges();

            Caja = Cuenta("110505", AccountNature.Debit, "ESF-A-EFE");
            Cartera = Cuenta("130505", AccountNature.Debit, "ESF-A-CAR-CRE");
            Proveedor = Cuenta("220505", AccountNature.Credit, "ESF-P");
            Ingreso = Cuenta("413505", AccountNature.Credit, "ERI-ING");
            Gasto = Cuenta("510505", AccountNature.Debit, "ERI-GAD");
        }

        private ChartOfAccount Cuenta(string code, AccountNature nature, string rubro)
        {
            var c = D.Cuenta(code, nature);
            c.NiifItemCode = rubro;
            D.Db.SaveChanges();
            return c;
        }

        public TrialBalanceQueryHandler Balance() => new(D.Db, D.Alcance, D.Clock, D.User, Emisor);

        public async Task ContabilizarAsync(ChartOfAccount debito, ChartOfAccount credito, decimal valor, int? sucursal = null)
        {
            var r = await D.Poster.PrepareAsync(new PostingRequest("CG", ContabilidadTestData.Marzo15, "Prueba", ContabilidadTestData.Manual(),
            [
                new PostingLine { AccountId = debito.Id, Debit = valor, Detail = "D", BranchId = sucursal },
                new PostingLine { AccountId = credito.Id, Credit = valor, Detail = "C", BranchId = sucursal },
            ]), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error.Message);
            await D.Db.SaveChangesAsync();
        }

        /// <summary>Los códigos de cuenta de los asientos que pasan el filtro por rubro (con repetidos: una por línea).</summary>
        public async Task<List<string>> CuentasDelLibroAsync(string rubro)
        {
            var ctx = await MovimientosContables.PrepararAsync(D.Db, D.Alcance, D.Clock, new FiltrosDeInforme { NiifItem = rubro }, CancellationToken.None);
            ctx.IsSuccess.Should().BeTrue(ctx.Error.Message);
            return await MovimientosContables.Base(D.Db, ctx.Value).Select(x => x.Account!.Code).ToListAsync();
        }
    }
}
