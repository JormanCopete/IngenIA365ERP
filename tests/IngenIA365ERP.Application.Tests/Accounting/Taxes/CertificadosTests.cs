using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Accounting.Taxes;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Taxes;

/// <summary>
/// T145 — US10: los certificados salen del libro, uno por tercero con retenciones y con consecutivo por clase; generar otra vez no
/// repite; una reversión los deja desactualizados y reexpedir da número nuevo y anula el anterior; lo de Captaciones y Cartera queda
/// fuera; el resumen de un formulario suma por renglón lo que su selector dice.
/// </summary>
public class CertificadosTests
{
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public AccountingAuditEmitter Emisor { get; }
        public ChartOfAccount Gasto { get; }
        public ChartOfAccount Retencion { get; }
        public ChartOfAccount Banco { get; }
        public Person Otro { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Substitute.For<IAuditAppendOnlyWriter>(), D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
            Gasto = D.Cuenta("5110251");
            Banco = D.Cuenta("1110051");
            Retencion = D.Cuenta("2435251", AccountNature.Credit, tercero: true, baseGravable: true, tarifa: 0.04m);
            Otro = new Person { FirstName = "Beto", LastName = "Proveedor", TaxId = "1000000002", Status = "A", CreatedBy = "test" };
            D.Db.People.Add(Otro);
            D.Db.SaveChanges();
        }

        /// <summary>Un egreso de honorarios: gasto contra banco y retención al 4 % del tercero.</summary>
        public async Task<Domain.Entities.Accounting.Transactions.AccountingDocument> PagarAsync(Person tercero, decimal valorBase, AccountingOrigin? origen = null)
        {
            var retenido = valorBase * 0.04m;
            var r = await D.Poster.PrepareAsync(new PostingRequest("CG", ContabilidadTestData.Marzo15, "Honorarios", origen ?? ContabilidadTestData.Manual(),
            [
                PostingLine.Debito(Gasto.Id, valorBase),
                new PostingLine { AccountId = Retencion.Id, Credit = retenido, PersonId = tercero.Id, TaxBase = valorBase, Detail = "Retefuente" },
                PostingLine.Credito(Banco.Id, valorBase - retenido),
            ]), default);
            r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
            await D.Db.SaveChangesAsync();
            return r.Value;
        }

        public Task<IngenIA365ERP.Application.Common.Models.Result<GeneracionDeCertificadosDto>> GenerarAsync() =>
            new GenerateWithholdingCertificatesCommandHandler(D.Db, D.Clock, D.User, Emisor).Handle(new GenerateWithholdingCertificatesCommand("Withholding", 2026), default);

        public async Task<IReadOnlyList<CertificadoDto>> ListarAsync() =>
            (await new ListCertificatesQueryHandler(D.Db, D.Clock).Handle(new ListCertificatesQuery("Withholding", 2026), default)).Value;
    }

    [Fact]
    public async Task Uno_por_tercero_con_consecutivo_y_generar_otra_vez_no_repite()
    {
        var e = new Escenario();
        await e.PagarAsync(e.D.Tercero, 1_000_000m);
        await e.PagarAsync(e.D.Tercero, 500_000m);
        await e.PagarAsync(e.Otro, 2_000_000m);

        var g = await e.GenerarAsync();
        g.IsSuccess.Should().BeTrue(g.IsFailure ? g.Error.Message : null);
        g.Value.Issued.Should().Be(2);
        g.Value.Certificates.Select(c => c.Number).Should().BeEquivalentTo(new long[] { 1, 2 });
        var ana = g.Value.Certificates.Single(c => c.PersonTaxId == e.D.Tercero.TaxId);
        ana.TotalBase.Should().Be(1_500_000m);
        ana.TotalAmount.Should().Be(60_000m);

        var otra = await e.GenerarAsync();
        otra.Value.Issued.Should().Be(0);
        otra.Value.AlreadyCurrent.Should().Be(2);
    }

    [Fact]
    public async Task Una_reversion_lo_deja_desactualizado_y_reexpedir_da_numero_nuevo()
    {
        var e = new Escenario();
        await e.PagarAsync(e.D.Tercero, 1_000_000m);
        var segundo = await e.PagarAsync(e.D.Tercero, 500_000m);
        await e.GenerarAsync();

        var original = await e.D.Db.AccountingDocuments.Include(d => d.Lines).Include(d => d.VoucherType).SingleAsync(d => d.Id == segundo.Id);
        (await e.D.Poster.PrepareReversalAsync(original, ContabilidadTestData.Marzo15, "Pago duplicado", AccountingOrigin.Manual(original.PublicId), default)).IsSuccess.Should().BeTrue();
        await e.D.Db.SaveChangesAsync();

        var lista = await e.ListarAsync();
        lista.Single().Status.Should().Be("Outdated");

        var nuevo = await new ReissueCertificateCommandHandler(e.D.Db, e.D.Clock, e.D.User, e.Emisor).Handle(new ReissueCertificateCommand(lista.Single().PublicId), default);
        nuevo.IsSuccess.Should().BeTrue(nuevo.IsFailure ? nuevo.Error.Message : null);
        nuevo.Value.Number.Should().Be(2);
        nuevo.Value.TotalAmount.Should().Be(40_000m);
        var despues = await e.ListarAsync();
        despues.Single(c => c.Number == 1).Status.Should().Be("Voided");
        despues.Single(c => c.Number == 1).VoidedByPublicId.Should().Be(nuevo.Value.PublicId);
        despues.Single(c => c.Number == 2).Status.Should().Be("Current");
    }

    [Fact]
    public async Task Lo_de_Captaciones_y_Cartera_no_entra_al_certificado()
    {
        var e = new Escenario();
        e.D.Db.VoucherTypes.Add(new VoucherType { Code = "MA", Name = "Captaciones", Usage = VoucherUsage.Module, ModuleCode = "CAR", IsSeeded = true, CreatedBy = "test" });
        e.Retencion.EnabledModules |= AccountingModules.Lending;
        e.Gasto.EnabledModules |= AccountingModules.Lending;
        e.Banco.EnabledModules |= AccountingModules.Lending;
        await e.D.Db.SaveChangesAsync();
        var r = await e.D.Poster.PrepareAsync(new PostingRequest("MA", ContabilidadTestData.Marzo15, "Intereses", new AccountingOrigin(ModuloContable.Cartera, "LendingIssuedDocument", Guid.NewGuid()),
        [
            PostingLine.Debito(e.Gasto.Id, 100_000m),
            new PostingLine { AccountId = e.Retencion.Id, Credit = 4_000m, PersonId = e.Otro.Id, TaxBase = 100_000m },
            PostingLine.Credito(e.Banco.Id, 96_000m),
        ]), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        await e.D.Db.SaveChangesAsync();
        await e.PagarAsync(e.D.Tercero, 1_000_000m);

        var g = await e.GenerarAsync();
        g.Value.Certificates.Should().ContainSingle(c => c.PersonTaxId == e.D.Tercero.TaxId);
    }

    [Fact]
    public async Task El_resumen_del_formulario_suma_base_y_valor_por_renglon()
    {
        var e = new Escenario();
        await e.PagarAsync(e.D.Tercero, 1_000_000m);
        await new SaveTaxFormCommandHandler(e.D.Db, e.D.Clock, e.D.User, e.Emisor).Handle(new SaveTaxFormCommand("350", "Retención", true,
        [
            new TaxFormLineInput("HON-B", "Honorarios: base", "Base", "243525"),
            new TaxFormLineInput("HON-R", "Honorarios: retención", "Amount", "243525"),
        ]), default);

        var tabla = await new TaxFormSummaryQueryHandler(e.D.Db, e.D.Alcance, e.D.Clock, e.D.User, e.Emisor)
            .Handle(new TaxFormSummaryQuery("350", new FiltrosDeInforme { From = new DateOnly(2026, 3, 1), To = new DateOnly(2026, 3, 31) }), default);
        tabla.IsSuccess.Should().BeTrue(tabla.IsFailure ? tabla.Error.Message : null);
        tabla.Value.Filas[0].Valores[4].Should().Be(1_000_000m);
        tabla.Value.Filas[1].Valores[4].Should().Be(40_000m);
    }

    [Fact]
    public void Los_sugeridos_dependen_del_catalogo()
    {
        FormulariosTributarios.Sugeridos("PUC-SOLIDARIO").Select(f => f.Code).Should().Equal("350", "300");
        FormulariosTributarios.Sugeridos("PUC-SOLIDARIO")[0].Lines.Should().Contain(l => l.AccountPrefixes == "243515");
        FormulariosTributarios.Sugeridos("PUC-COMERCIAL")[0].Lines.Should().Contain(l => l.AccountPrefixes == "236515");
        FormulariosTributarios.Sugeridos("OTRO").Should().BeEmpty();
    }
}
