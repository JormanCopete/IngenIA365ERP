using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Exogenous;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Accounting.Taxes;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Taxes;

/// <summary>
/// Feature 009 E4 (2026-10-31): los planos de configuración. El de formularios reemplaza cada formulario del archivo y no toca los demás; el
/// de exógena reemplaza los conceptos de cada formato del archivo y exige que el formato exista y que la columna sea suya. Con un error no
/// se guarda nada y se dice fila y columna. Lo que se baja se puede volver a subir tal cual.
/// </summary>
public class PlanosTests
{
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public ITabularFileReader Lector { get; } = Substitute.For<ITabularFileReader>();
        public ISender Sender { get; } = Substitute.For<ISender>();
        public AccountingAuditEmitter Emisor { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Substitute.For<IAuditAppendOnlyWriter>(), D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
            // Los planos guardan por la misma puerta que la pantalla: el remitente de prueba entrega a los handlers de verdad.
            Sender.Send(Arg.Any<SaveTaxFormCommand>(), Arg.Any<CancellationToken>())
                .Returns(c => new SaveTaxFormCommandHandler(D.Db, D.Clock, D.User, Emisor).Handle(c.Arg<SaveTaxFormCommand>(), default));
            Sender.Send(Arg.Any<SaveExogenousFormatCommand>(), Arg.Any<CancellationToken>())
                .Returns(c => new SaveExogenousFormatCommandHandler(D.Db, D.Clock, D.User, Emisor).Handle(c.Arg<SaveExogenousFormatCommand>(), default));
        }

        public void Archivo(IReadOnlyList<string> encabezados, params string?[][] filas) =>
            Lector.LeerAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Success(new TablaLeida(encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "csv"))));

        public void FormatoDelAnio(string codigo, params string[] columnas) =>
            D.Db.ExogenousFormats.Add(new ExogenousFormat
            {
                TaxYear = 2026, FormatCode = codigo, FormatVersion = 10, Name = $"Formato {codigo}", Applies = true, Origin = ExogenousOrigin.Seed, MinAmount = 100_000m,
                MinorAmountsRule = true, MinorAmountsTaxId = "222222222", CreatedBy = "test",
                LayoutJson = new DisposicionDeExogena("pagos", "cpt", ["tdoc", "nid"], columnas.Select(c => new ColumnaDeExogena(c, c, c.ToLowerInvariant())).ToList()).ComoJson(),
            });
    }

    [Fact]
    public async Task El_plano_de_formularios_reemplaza_los_que_vienen_y_no_toca_los_demas()
    {
        var e = new Escenario();
        e.D.Db.TaxForms.Add(new TaxForm { Code = "ICA", Name = "ICA", IsActive = true, CreatedBy = "test",
            Lines = [new TaxFormLine { LineCode = "R1", Description = "Base", Selector = TaxFormSelector.Base, AccountPrefixes = "244015", Sign = 1, Order = 1, CreatedBy = "test" }] });
        e.D.Db.SaveChanges();
        e.Archivo(PlanoDeFormularios.Columnas,
            ["350", "Retención", "sí", "HON-B", "Honorarios base", "Base", "243515", "1"],
            ["350", null, null, "HON-R", "Honorarios retención", "Amount", "243515, 236515", ""]);

        var r = await new ImportTaxFormsCommandHandler(e.Lector, e.Sender).Handle(new ImportTaxFormsCommand("plano.csv", [1]), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Forms.Should().Be(1);
        var formulario = await e.D.Db.TaxForms.Include(f => f.Lines).SingleAsync(f => f.Code == "350" && !f.IsDeleted);
        formulario.Lines.Where(l => !l.IsDeleted).Select(l => l.LineCode).Should().Equal("HON-B", "HON-R");
        formulario.Lines.Single(l => l.LineCode == "HON-R").AccountPrefixes.Should().Be("243515,236515");
        (await e.D.Db.TaxForms.Include(f => f.Lines).SingleAsync(f => f.Code == "ICA")).Lines.Should().ContainSingle(l => !l.IsDeleted);
    }

    [Fact]
    public async Task Con_un_error_el_plano_de_formularios_no_guarda_nada_y_dice_fila_y_columna()
    {
        var e = new Escenario();
        e.Archivo(PlanoDeFormularios.Columnas,
            ["350", "Retención", "sí", "HON-B", "Base", "Base", "243515", "1"],
            ["350", null, null, "HON-R", "Retención", "Lunar", "243515", "1"]);

        var r = await new ImportTaxFormsCommandHandler(e.Lector, e.Sender).Handle(new ImportTaxFormsCommand("plano.csv", [1]), default);
        r.Error.Code.Should().Be("Accounting.Taxes.FormsFileInvalid");
        r.Error.Should().BeOfType<ErrorConDatos>();
        (await e.D.Db.TaxForms.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task El_plano_de_exogena_reemplaza_los_conceptos_y_exige_formato_y_columna_existentes()
    {
        var e = new Escenario();
        e.FormatoDelAnio("1001", "PagoDeducible", "RetencionRenta");
        e.D.Db.SaveChanges();
        e.Archivo(PlanoDeExogena.Columnas,
            ["1001", "5002", "Honorarios", "PagoDeducible", "511001", "Debit"],
            ["1001", "5002", "Honorarios", "RetencionRenta", "243515", "Credit"],
            ["1001", "5016", "Otros", null, null, null]);

        var r = await new ImportExogenousConceptsCommandHandler(e.D.Db, e.Lector, e.Sender).Handle(new ImportExogenousConceptsCommand(2026, "plano.csv", [1]), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Should().BeEquivalentTo(new { Formats = 1, Concepts = 2, Accounts = 2 });

        e.Archivo(PlanoDeExogena.Columnas,
            ["1007", "4001", "Ingresos", "Ingresos", "41", "Credit"],
            ["1001", "5002", "Honorarios", "NoExiste", "511001", "Debit"]);
        var malo = await new ImportExogenousConceptsCommandHandler(e.D.Db, e.Lector, e.Sender).Handle(new ImportExogenousConceptsCommand(2026, "plano.csv", [1]), default);
        malo.Error.Code.Should().Be("Accounting.Exogenous.FileInvalid");
        var concepto = await e.D.Db.ExogenousConcepts.Include(c => c.Accounts).SingleAsync(c => c.ConceptCode == "5002" && !c.IsDeleted);
        concepto.Accounts.Where(a => !a.IsDeleted).Should().HaveCount(2, "el archivo con errores no tocó nada");
    }

    [Fact]
    public async Task Lo_que_se_baja_trae_lo_configurado()
    {
        var e = new Escenario();
        e.D.Db.TaxForms.Add(new TaxForm { Code = "350", Name = "Retención", IsActive = true, CreatedBy = "test",
            Lines = [new TaxFormLine { LineCode = "HON-B", Description = "Honorarios", Selector = TaxFormSelector.Base, AccountPrefixes = "243515", Sign = 1, Order = 1, CreatedBy = "test" }] });
        e.D.Db.SaveChanges();
        var tabla = await new ExportTaxFormsQueryHandler(e.D.Db).Handle(new ExportTaxFormsQuery(), default);
        tabla.Value.Filas.Should().ContainSingle();
        tabla.Value.Filas[0].Valores.Take(4).Should().Equal("350", "Retención", "sí", "HON-B");
    }
}
