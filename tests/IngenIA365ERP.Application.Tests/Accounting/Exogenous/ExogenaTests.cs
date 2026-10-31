using System.Xml.Linq;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Exogenous;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Exogenous;

/// <summary>
/// T151 — US11: la generación suma por tercero y concepto desde el libro; quien no llega a la cuantía mínima va a menores cuantías; un
/// documento inválido bloquea el XML y una dirección faltante sólo avisa; regenerar reemplaza la versión de trabajo; el XML sale bien
/// formado con encabezado y filas y el nombre de 33 caracteres; la semilla 2026 trae los formatos acordados; copiar un año no pisa nada.
/// </summary>
public class ExogenaTests
{
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public AccountingAuditEmitter Emisor { get; }
        public ChartOfAccount Honorarios { get; }
        public ChartOfAccount Banco { get; }
        public Person Grande { get; }
        public Person Pequeno { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Substitute.For<IAuditAppendOnlyWriter>(), D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
            Honorarios = D.Cuenta("5110011", tercero: true);
            Banco = D.Cuenta("1110051");
            Grande = new Person { FirstName = "Gloria", LastName = "Grande", TaxId = "900123456", IdType = "N", PersonType = "02", BusinessName = "GRANDE SAS", Address = "Calle 1", Status = "A", CreatedBy = "test" };
            Pequeno = new Person { FirstName = "Pedro", LastName = "Pequeño", TaxId = "1000000003", Status = "A", CreatedBy = "test" };
            D.Db.People.AddRange(Grande, Pequeno);
            D.Db.ExogenousFormats.Add(new ExogenousFormat
            {
                TaxYear = 2026, FormatCode = "1001", FormatVersion = 10, Name = "Pagos", Applies = true, Origin = ExogenousOrigin.Custom, MinAmount = 100_000m,
                MinorAmountsRule = true, MinorAmountsTaxId = "222222222", CreatedBy = "test",
                LayoutJson = new DisposicionDeExogena("pagos", "cpt", ["tdoc", "nid", "dv", "apl1", "apl2", "nom1", "nom2", "raz", "dir", "dpto", "mun", "pais"],
                    [new ColumnaDeExogena("PagoDeducible", "Pago deducible", "pded")]).ComoJson(),
                Concepts = [new ExogenousConcept { ConceptCode = "5002", Name = "Honorarios", CreatedBy = "test",
                    Accounts = [new ExogenousConceptAccount { ValueField = "PagoDeducible", AccountPrefix = "511001", Selector = "Debit", CreatedBy = "test" }] }],
            });
            D.Db.SaveChanges();
        }

        public async Task PagarAsync(Person p, decimal valor)
        {
            var r = await D.Poster.PrepareAsync(new PostingRequest("CG", ContabilidadTestData.Marzo15, "Honorarios", ContabilidadTestData.Manual(),
                [new PostingLine { AccountId = Honorarios.Id, Debit = valor, PersonId = p.Id }, PostingLine.Credito(Banco.Id, valor)]), default);
            r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
            await D.Db.SaveChangesAsync();
        }

        public Task<Result<GeneracionDeExogenaDto>> GenerarAsync() =>
            new GenerateExogenousRunCommandHandler(D.Db, D.Clock, D.User, Emisor).Handle(new GenerateExogenousRunCommand(2026, "1001"), default);
    }

    [Fact]
    public async Task Suma_por_tercero_y_agrupa_las_menores_cuantias()
    {
        var e = new Escenario();
        await e.PagarAsync(e.Grande, 3_000_000m);
        await e.PagarAsync(e.Pequeno, 50_000m);

        var g = await e.GenerarAsync();
        g.IsSuccess.Should().BeTrue(g.IsFailure ? g.Error.Message : null);
        var lineas = await e.D.Db.ExogenousRunLines.Where(l => l.Run!.PublicId == g.Value.PublicId).ToListAsync();
        lineas.Should().HaveCount(2);
        var grande = lineas.Single(l => l.IdNumber == "900123456");
        grande.Value1.Should().Be(3_000_000m);
        grande.IdType.Should().Be("31");
        grande.CheckDigit.Should().Be(GeneracionDigito("900123456"));
        grande.BusinessName.Should().Be("GRANDE SAS");
        var menores = lineas.Single(l => l.IdNumber == "222222222");
        menores.IdType.Should().Be("43");
        menores.Value1.Should().Be(50_000m);
    }

    [Fact]
    public async Task Un_documento_invalido_bloquea_el_xml_y_la_direccion_faltante_solo_avisa()
    {
        var e = new Escenario();
        e.Pequeno.TaxId = "10-AB";
        await e.D.Db.SaveChangesAsync();
        await e.PagarAsync(e.Pequeno, 500_000m);

        var g = await e.GenerarAsync();
        g.Value.BlockingIssueCount.Should().Be(1);
        g.Value.IssueCount.Should().BeGreaterThan(1);
        var xml = await new ExportExogenousXmlCommandHandler(e.D.Db, Substitute.For<ISender>(), e.D.Clock, e.D.User, e.Emisor).Handle(new ExportExogenousXmlCommand(g.Value.PublicId), default);
        xml.Error.Code.Should().Be("Accounting.Exogenous.BlockingIssues");
    }

    [Fact]
    public async Task Regenerar_da_de_baja_la_version_de_trabajo()
    {
        var e = new Escenario();
        await e.PagarAsync(e.Grande, 3_000_000m);
        var primera = (await e.GenerarAsync()).Value;
        var segunda = (await e.GenerarAsync()).Value;
        segunda.Version.Should().Be(primera.Version + 1);
        var runs = await new ListExogenousRunsQueryHandler(e.D.Db).Handle(new ListExogenousRunsQuery(2026), default);
        runs.Value.Should().ContainSingle().Which.Version.Should().Be(segunda.Version);
    }

    [Fact]
    public async Task El_xml_sale_bien_formado_con_encabezado_y_filas_y_queda_exportado()
    {
        var e = new Escenario();
        await e.PagarAsync(e.Grande, 3_000_000m);
        var g = (await e.GenerarAsync()).Value;
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success(Guid.NewGuid()));

        var r = await new ExportExogenousXmlCommandHandler(e.D.Db, sender, e.D.Clock, e.D.User, e.Emisor).Handle(new ExportExogenousXmlCommand(g.PublicId), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.FileName.Should().HaveLength(33).And.StartWith("Dmuisca_0101001102026");
        var doc = XDocument.Load(new MemoryStream(r.Value.Content));
        doc.Root!.Name.LocalName.Should().Be("mas");
        doc.Root.Element("Cab")!.Element("CantReg")!.Value.Should().Be("1");
        doc.Root.Element("Cab")!.Element("ValorTotal")!.Value.Should().Be("3000000");
        var fila = doc.Root.Element("pagos")!;
        fila.Attribute("cpt")!.Value.Should().Be("5002");
        fila.Attribute("nid")!.Value.Should().Be("900123456");
        fila.Attribute("pded")!.Value.Should().Be("3000000");
        (await e.D.Db.ExogenousRuns.SingleAsync(x => x.PublicId == g.PublicId)).Status.Should().Be(ExogenousRunStatus.Exported);
    }

    [Fact]
    public async Task Copiar_el_anio_no_pisa_lo_parametrizado()
    {
        var e = new Escenario();
        var r = await new CopyExogenousYearCommandHandler(e.D.Db, e.D.Clock, e.D.User, e.Emisor).Handle(new CopyExogenousYearCommand(2026, 2027), default);
        r.Value.Should().ContainSingle(f => f.FormatCode == "1001" && f.Concepts.Count == 1);
        var otra = await new CopyExogenousYearCommandHandler(e.D.Db, e.D.Clock, e.D.User, e.Emisor).Handle(new CopyExogenousYearCommand(2026, 2027), default);
        otra.Value.Should().HaveCount(1);
    }

    [Fact]
    public void La_semilla_2026_trae_los_formatos_acordados()
    {
        var semilla = SemillaDeExogena.DelAnio(2026);
        semilla.Should().NotBeNull();
        semilla!.Formats.Select(f => f.Code).Should().BeEquivalentTo(["1001", "1003", "1005", "1006", "1007", "1008", "1009", "1010", "1012"]);
        semilla.Formats.SelectMany(f => f.Concepts).SelectMany(c => c.Accounts)
            .Should().OnlyContain(a => SelectoresDeExogena.Todos.Contains(a.Selector));
        foreach (var f in semilla.Formats)
            f.Concepts.SelectMany(c => c.Accounts).Should().OnlyContain(a => f.Columns.Any(col => col.Field == a.Field), $"el formato {f.Code} nombra sólo sus columnas");
    }

    // 900123456 → pesos 3,7,13,17,19,23,29,37,41 sobre los dígitos de derecha a izquierda.
    private static string GeneracionDigito(string nit)
    {
        int[] pesos = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];
        var suma = nit.Reverse().Select((c, i) => (c - '0') * pesos[i]).Sum();
        var r = suma % 11;
        return (r > 1 ? 11 - r : r).ToString();
    }
}
