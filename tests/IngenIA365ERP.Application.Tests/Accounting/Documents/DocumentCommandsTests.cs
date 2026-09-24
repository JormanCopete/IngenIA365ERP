using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Documents;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Accounting.Documents;

/// <summary>
/// T071 — US3: el borrador se guarda con errores y sólo con tipos manuales; validar señala línea
/// y campo; contabilizar exige cuatro ojos cuando la cooperativa lo pide, cuadre y período
/// abierto, y numera en su sitio; reversar deja neto cero y no se repite.
/// </summary>
public class DocumentCommandsTests
{
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public ChartOfAccount Caja { get; }
        public ChartOfAccount Proveedores { get; }

        public Escenario()
        {
            Caja = D.Cuenta("110505");
            Proveedores = D.Cuenta("220505", AccountNature.Credit, tercero: true);
        }

        public SaveDraftDocumentCommandHandler Guardador(ICurrentUserService? usuario = null) =>
            new(D.Db, D.Clock, usuario ?? D.User, D.Alcance, Poster(usuario));

        public PostDocumentCommandHandler Contabilizador(ICurrentUserService? usuario = null) => new(D.Db, usuario ?? D.User, Poster(usuario));

        public ReverseDocumentCommandHandler Reversador() => new(D.Db, D.Clock, D.Poster);

        public ValidateDraftQueryHandler Validador() => new(D.Db, D.Poster);

        public DiscardDraftCommandHandler Descartador() => new(D.Db, NSubstitute.Substitute.For<IngenIA365ERP.Application.Common.Interfaces.Storage.IBlobStore>(), D.Clock, D.User);

        private AccountingPoster Poster(ICurrentUserService? usuario) => usuario is null ? D.Poster : new AccountingPoster(D.Db, D.Clock, usuario, D.Alcance);

        public static LineaDeBorradorInput Linea(ChartOfAccount cuenta, decimal debito, decimal credito, Guid? tercero = null) =>
            new(cuenta.Code, null, null, tercero, null, null, debito, credito, "detalle", null);

        public SaveDraftDocumentCommand Borrador(decimal credito = 100m, string tipo = "CG", Guid? id = null, Guid? tercero = null) =>
            new(id, tipo, ContabilidadTestData.Marzo15, "Prueba", [Linea(Caja, 100m, 0m), Linea(Proveedores, 0m, credito, tercero ?? D.Tercero.PublicId)]);
    }

    [Fact]
    public async Task Un_borrador_descuadrado_y_sin_tercero_se_guarda_y_devuelve_sus_infracciones()
    {
        var e = new Escenario();

        var r = await e.Guardador().Handle(e.Borrador(credito: 80m, tercero: null) with { Lines = [Escenario.Linea(e.Caja, 100m, 0m), Escenario.Linea(e.Proveedores, 0m, 80m)] }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Errors.Select(x => x.Code).Should().Contain(["Accounting.Line.ThirdPartyRequired", "Accounting.Document.Unbalanced"]);
        r.Value.Errors.Single(x => x.Code == "Accounting.Line.ThirdPartyRequired").Should().Match<ErrorDeLineaDto>(x => x.LineNumber == 2 && x.Field == "Person");
        var doc = await e.D.Db.AccountingDocuments.Include(d => d.Lines).SingleAsync(d => d.PublicId == r.Value.PublicId);
        doc.Status.Should().Be(DocumentStatus.Draft);
        doc.Number.Should().BeNull();
        doc.Lines.Should().HaveCount(2).And.OnlyContain(l => !l.IsPosted && l.BranchId == e.D.Principal.Id);
        doc.RegisteredByUserId.Should().Be(9);
    }

    [Fact]
    public async Task Un_tipo_de_modulo_no_sirve_para_digitar()
    {
        var e = new Escenario();
        var r = await e.Guardador().Handle(e.Borrador(tipo: "NM"), CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Document.ManualOnly");
    }

    [Fact]
    public async Task Una_cuenta_inexistente_no_se_puede_guardar_y_se_señala_en_su_linea()
    {
        var e = new Escenario();
        var comando = e.Borrador() with { Lines = [Escenario.Linea(e.Caja, 100m, 0m), new LineaDeBorradorInput("999999", null, null, null, null, null, 0m, 100m, null, null)] };
        var r = await e.Guardador().Handle(comando, CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Line.AccountNotFound");
        r.Error.Should().BeOfType<ErrorConDatos>();
        (await e.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Actualizar_reemplaza_lineas_por_numero_y_marca_eliminadas_las_sobrantes()
    {
        var e = new Escenario();
        var creado = await e.Guardador().Handle(e.Borrador(), CancellationToken.None);
        var tres = e.Borrador(id: creado.Value.PublicId) with { Lines = [Escenario.Linea(e.Caja, 50m, 0m), Escenario.Linea(e.Caja, 50m, 0m), Escenario.Linea(e.Proveedores, 0m, 100m, e.D.Tercero.PublicId)] };
        (await e.Guardador().Handle(tres, CancellationToken.None)).IsSuccess.Should().BeTrue();

        var dos = e.Borrador(id: creado.Value.PublicId);
        var r = await e.Guardador().Handle(dos, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.PublicId.Should().Be(creado.Value.PublicId);
        var lineas = await e.D.Db.JournalEntries.Where(l => l.Document!.PublicId == creado.Value.PublicId).ToListAsync();
        lineas.Should().HaveCount(3);
        lineas.Where(l => !l.IsDeleted).Select(l => l.LineNumber).Should().BeEquivalentTo([1, 2]);
        lineas.Single(l => l.IsDeleted).LineNumber.Should().Be(3);
        (await e.D.Db.AccountingDocuments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Validar_no_guarda_y_devuelve_errores_con_linea_campo_y_totales()
    {
        var e = new Escenario();
        var r = await e.Validador().Handle(new ValidateDraftQuery("CG", ContabilidadTestData.Marzo15, "x", [Escenario.Linea(e.Caja, 100m, 0m), Escenario.Linea(e.Proveedores, 0m, 90m)]), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.TotalDebit.Should().Be(100m);
        r.Value.TotalCredit.Should().Be(90m);
        r.Value.Difference.Should().Be(10m);
        r.Value.Errors.Should().Contain(x => x.LineNumber == 2 && x.Field == "Person" && x.Severity == "Error");
        r.Value.Errors.Should().Contain(x => x.LineNumber == 0 && x.Field == "Total" && x.Code == "Accounting.Document.Unbalanced");
        (await e.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Contabilizar_numera_en_su_sitio_y_deja_las_lineas_contabilizadas()
    {
        var e = new Escenario();
        var borrador = await e.Guardador().Handle(e.Borrador(), CancellationToken.None);

        var r = await e.Contabilizador().Handle(new PostDocumentCommand(borrador.Value.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Number.Should().Be(1);
        var doc = await e.D.Db.AccountingDocuments.Include(d => d.Lines).SingleAsync(d => d.PublicId == borrador.Value.PublicId);
        doc.Status.Should().Be(DocumentStatus.Posted);
        doc.PostedByUserId.Should().Be(9);
        doc.PeriodId.Should().NotBeNull();
        doc.Lines.Should().OnlyContain(l => l.IsPosted && l.Date == ContabilidadTestData.Marzo15);
        (await e.D.Db.VoucherTypes.SingleAsync(v => v.Code == "CG")).NextNumber.Should().Be(2);
        (await e.D.Db.AccountingDocuments.CountAsync()).Should().Be(1, "se contabiliza el mismo documento, no una copia");
    }

    [Fact]
    public async Task Con_cuatro_ojos_quien_registro_no_contabiliza_pero_otra_persona_si()
    {
        var e = new Escenario();
        e.D.Setup.FourEyes = true;
        await e.D.Db.SaveChangesAsync();
        var borrador = await e.Guardador().Handle(e.Borrador(), CancellationToken.None);

        var misma = await e.Contabilizador().Handle(new PostDocumentCommand(borrador.Value.PublicId), CancellationToken.None);
        misma.IsFailure.Should().BeTrue();
        misma.Error.Code.Should().Be("Accounting.Document.FourEyes");

        var otra = NominaTestData.UsuarioDePrueba("revisor@demo", 11);
        var r = await e.Contabilizador(otra).Handle(new PostDocumentCommand(borrador.Value.PublicId), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        (await e.D.Db.AccountingDocuments.SingleAsync()).PostedByUserId.Should().Be(11);
    }

    [Fact]
    public async Task Un_borrador_descuadrado_o_en_periodo_cerrado_no_se_contabiliza()
    {
        var e = new Escenario();
        var descuadrado = await e.Guardador().Handle(e.Borrador(credito: 90m), CancellationToken.None);
        var r1 = await e.Contabilizador().Handle(new PostDocumentCommand(descuadrado.Value.PublicId), CancellationToken.None);
        r1.IsFailure.Should().BeTrue();
        r1.Error.Code.Should().Be("Accounting.Document.Unbalanced");

        var enero = await e.Guardador().Handle(e.Borrador() with { Date = new DateOnly(2026, 1, 10) }, CancellationToken.None);
        var r2 = await e.Contabilizador().Handle(new PostDocumentCommand(enero.Value.PublicId), CancellationToken.None);
        r2.IsFailure.Should().BeTrue();
        r2.Error.Code.Should().Be("Accounting.Period.Closed");
        (await e.D.Db.AccountingDocuments.CountAsync(d => d.Status == DocumentStatus.Draft)).Should().Be(2);
    }

    [Fact]
    public async Task Reversar_deja_neto_cero_y_no_se_repite_ni_sobre_la_reversion()
    {
        var e = new Escenario();
        var borrador = await e.Guardador().Handle(e.Borrador(), CancellationToken.None);
        await e.Contabilizador().Handle(new PostDocumentCommand(borrador.Value.PublicId), CancellationToken.None);

        var r = await e.Reversador().Handle(new ReverseDocumentCommand(borrador.Value.PublicId, "Se digitó dos veces"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Number.Should().Be(2);
        var saldoCaja = await e.D.Db.JournalEntries.Where(l => l.AccountId == e.Caja.Id && l.IsPosted).SumAsync(l => l.Debit - l.Credit);
        saldoCaja.Should().Be(0m);
        (await e.D.Db.AccountingDocuments.SingleAsync(d => d.PublicId == borrador.Value.PublicId)).Status.Should().Be(DocumentStatus.Reversed);

        var otraVez = await e.Reversador().Handle(new ReverseDocumentCommand(borrador.Value.PublicId, "otra vez"), CancellationToken.None);
        otraVez.Error.Code.Should().Be("Accounting.Document.AlreadyReversed");
        var laReversion = await e.Reversador().Handle(new ReverseDocumentCommand(r.Value.ReversalPublicId, "no"), CancellationToken.None);
        laReversion.Error.Code.Should().Be("Accounting.Document.IsReversal");
    }

    [Fact]
    public async Task Un_comprobante_de_modulo_solo_se_reversa_desde_su_modulo()
    {
        var e = new Escenario();
        var nomina = await e.D.Poster.PrepareAsync(ContabilidadTestData.Comprobante(e.Caja, e.Proveedores, origen: ContabilidadTestData.Nomina(), tipo: "NM") with
        {
            Lines = [PostingLine.Debito(e.Caja.Id, 100m), new PostingLine { AccountId = e.Proveedores.Id, Credit = 100m, PersonId = e.D.Tercero.Id }],
        }, CancellationToken.None);
        nomina.IsSuccess.Should().BeTrue(nomina.Error?.Message);
        await e.D.Db.SaveChangesAsync();

        var r = await e.Reversador().Handle(new ReverseDocumentCommand(nomina.Value.PublicId, "no debería"), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Document.ModuleOwned");
        r.Error.Should().BeOfType<ErrorConDatos>();
    }

    [Fact]
    public async Task Descartar_marca_eliminado_el_borrador_y_sus_lineas_sin_quitar_filas()
    {
        var e = new Escenario();
        var borrador = await e.Guardador().Handle(e.Borrador(), CancellationToken.None);

        var r = await e.Descartador().Handle(new DiscardDraftCommand(borrador.Value.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        (await e.D.Db.AccountingDocuments.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await e.D.Db.AccountingDocuments.SingleAsync()).IsDeleted.Should().BeTrue();
        (await e.D.Db.JournalEntries.ToListAsync()).Should().OnlyContain(l => l.IsDeleted);
        var contabilizado = await e.Contabilizador().Handle(new PostDocumentCommand(borrador.Value.PublicId), CancellationToken.None);
        contabilizado.Error.Code.Should().Be("Accounting.Document.NotFound");
    }
}
