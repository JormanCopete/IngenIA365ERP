using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Documents;
using IngenIA365ERP.Application.Accounting.Opening;
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

namespace IngenIA365ERP.Application.Tests.Accounting.Opening;

/// <summary>
/// T115 — US13 (FR-084..FR-087): importar valida cada fila con las reglas de cuenta y con un solo
/// error no guarda nada; lo que queda es un borrador AP fechado la víspera del primer período, que
/// se contabiliza y se reversa como cualquier comprobante; a lo sumo una apertura vigente.
/// </summary>
public class ImportOpeningBalancesCommandHandlerTests
{
    private static readonly string[] Encabezados = ["cuenta", "tercero", "tipoDocumento", "numeroDocumento", "centroCosto", "sucursal", "debito", "credito", "detalle"];

    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public ITabularFileReader Lector { get; } = Substitute.For<ITabularFileReader>();
        public IAuditAppendOnlyWriter Auditoria { get; } = Substitute.For<IAuditAppendOnlyWriter>();
        public AccountingAuditEmitter Emisor { get; }
        public ChartOfAccount Caja { get; }
        public ChartOfAccount Cartera { get; }
        public ChartOfAccount Aportes { get; }
        public ChartOfAccount Agrupacion { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Auditoria, D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
            Caja = D.Cuenta("11050501");
            // Cartera exige tercero y documento cruce, y sólo la mueve Cartera: la apertura igual la carga.
            Cartera = D.Cuenta("14050501", modulos: AccountingModules.Lending, tercero: true, cruce: true);
            Aportes = D.Cuenta("31050501", AccountNature.Credit);
            Agrupacion = D.Cuenta("140505", movimiento: false);
        }

        public ImportOpeningBalancesCommandHandler Importador() => new(D.Db, Lector, D.Clock, D.User, D.Alcance, D.Poster, Emisor);

        public void Archivo(params string?[][] filas)
        {
            var leidas = filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList();
            Lector.LeerAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Success(new TablaLeida(Encabezados, leidas, "csv"))));
        }

        public static string?[] Fila(string cuenta, decimal debito, decimal credito, string? tercero = null, string? tipo = null, string? numero = null, string? centro = null, string? sucursal = null) =>
            [cuenta, tercero, tipo, numero, centro, sucursal, debito == 0m ? "" : debito.ToString(System.Globalization.CultureInfo.InvariantCulture), credito == 0m ? "" : credito.ToString(System.Globalization.CultureInfo.InvariantCulture), "detalle"];

        public Task<Result<AperturaImportadaDto>> ImportarAsync() => Importador().Handle(new ImportOpeningBalancesCommand("apertura.csv", [1, 2, 3]), CancellationToken.None);

        public Task<Result<ContabilizadoDto>> ContabilizarAsync(Guid borrador) =>
            new PostDocumentCommandHandler(D.Db, D.User, D.Poster).Handle(new PostDocumentCommand(borrador), CancellationToken.None);

        public Task<Result<ReversadoDto>> ReversarAsync(Guid comprobante) =>
            new ReverseDocumentCommandHandler(D.Db, D.Clock, D.Poster).Handle(new ReverseDocumentCommand(comprobante, "Saldos equivocados"), CancellationToken.None);
    }

    [Fact]
    public async Task Con_errores_los_lista_por_fila_y_columna_y_no_guarda_nada()
    {
        var e = new Escenario();
        e.Archivo(
            Escenario.Fila(e.Agrupacion.Code, 100m, 0m),                                   // fila 2: cuenta de agrupación
            Escenario.Fila(e.Cartera.Code, 250m, 0m, tercero: "9999999999", tipo: "PG", numero: "1"), // fila 3: tercero inexistente y tipo de cruce inexistente
            Escenario.Fila(e.Cartera.Code, 250m, 0m, tercero: e.D.Tercero.TaxId),          // fila 4: la cuenta exige documento cruce
            Escenario.Fila(e.Caja.Code, 10.005m, 0m),                                       // fila 5: tres decimales
            Escenario.Fila(e.Caja.Code, 10m, 10m),                                          // fila 6: débito y crédito
            Escenario.Fila("99999999", 10m, 0m),                                            // fila 7: cuenta inexistente
            Escenario.Fila(e.Aportes.Code, 0m, 300m, sucursal: "Sucursal Sur"));            // fila 8: sucursal inexistente

        var r = await e.ImportarAsync();

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Opening.Invalid");
        var errores = ((ErrorConDatos)r.Error).Data.Should().BeAssignableTo<object>().Subject;
        var lista = (IReadOnlyList<Application.Accounting.Setup.ErrorDeFila>)errores.GetType().GetProperty("errors")!.GetValue(errores)!;
        lista.Select(x => (x.Row, x.Column, x.Code)).Should().BeEquivalentTo(
        [
            (2, "cuenta", "Accounting.Line.AccountNotMovement"),
            (3, "tercero", "Accounting.Line.ThirdPartyInvalid"),
            (3, "tipoDocumento", "Accounting.Line.CrossDocumentTypeInvalid"),
            (4, "tipoDocumento", "Accounting.Line.CrossDocumentRequired"),
            (5, "debito", "Accounting.Line.AmountInvalid"),
            (6, "debito", "Accounting.Line.AmountInvalid"),
            (7, "cuenta", "Accounting.Line.AccountNotFound"),
            (8, "sucursal", "Accounting.Line.BranchInvalid"),
        ]);
        (await e.D.Db.AccountingDocuments.CountAsync()).Should().Be(0, "nada a medias");
        (await e.D.Db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Importa_un_borrador_AP_fechado_la_vispera_del_primer_periodo_aunque_no_cuadre()
    {
        var e = new Escenario();
        e.Archivo(
            Escenario.Fila(e.Caja.Code, 1000m, 0m, sucursal: "Norte"),
            Escenario.Fila(e.Cartera.Code, 2500.5m, 0m, tercero: e.D.Tercero.TaxId, tipo: "FV", numero: "77"),
            Escenario.Fila(e.Aportes.Code, 0m, 3000m));

        var r = await e.ImportarAsync();

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Lines.Should().Be(3);
        r.Value.TotalDebit.Should().Be(3500.5m);
        r.Value.TotalCredit.Should().Be(3000m);
        r.Value.Errors.Should().BeEmpty();

        var borrador = await e.D.Db.AccountingDocuments.Include(d => d.Lines).Include(d => d.VoucherType).SingleAsync(d => d.PublicId == r.Value.DraftPublicId);
        borrador.Status.Should().Be(DocumentStatus.Draft);
        borrador.Kind.Should().Be(DocumentKind.Opening);
        borrador.VoucherType!.Code.Should().Be("AP");
        borrador.Date.Should().Be(new DateOnly(2025, 12, 31), "la víspera del primer período (enero de 2026)");
        borrador.PeriodId.Should().BeNull();
        borrador.Number.Should().BeNull();
        borrador.Lines.Should().HaveCount(3).And.OnlyContain(l => !l.IsPosted && l.Date == new DateOnly(2025, 12, 31));
        borrador.Lines.Single(l => l.AccountId == e.Caja.Id).BranchId.Should().Be(e.D.Norte.Id, "la sucursal se resuelve por nombre");
        borrador.Lines.Single(l => l.AccountId == e.Aportes.Id).BranchId.Should().Be(e.D.Principal.Id, "sin sucursal queda la principal");
        var cartera = borrador.Lines.Single(l => l.AccountId == e.Cartera.Id);
        cartera.PersonId.Should().Be(e.D.Tercero.Id);
        cartera.CrossDocumentNumber.Should().Be("77");
        cartera.CrossDocumentTypeId.Should().NotBeNull();
        await e.Auditoria.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(d => d.Action == "Accounting.Opening.Imported"), Arg.Any<CancellationToken>());

        // Descuadrado no se contabiliza; el borrador sigue ahí para corregirlo.
        var post = await e.ContabilizarAsync(borrador.PublicId);
        post.Error.Code.Should().Be("Accounting.Document.Unbalanced");
    }

    [Fact]
    public async Task Contabilizada_queda_como_la_unica_vigente_y_reversarla_libera_el_cupo()
    {
        var e = new Escenario();
        e.Archivo(Escenario.Fila(e.Caja.Code, 3000m, 0m), Escenario.Fila(e.Aportes.Code, 0m, 3000m));
        var primera = (await e.ImportarAsync()).Value.DraftPublicId;

        var post = await e.ContabilizarAsync(primera);
        post.IsSuccess.Should().BeTrue(post.Error?.Message);
        var apertura = await e.D.Db.AccountingDocuments.Include(d => d.Lines).SingleAsync(d => d.PublicId == primera);
        apertura.Status.Should().Be(DocumentStatus.Posted);
        apertura.Kind.Should().Be(DocumentKind.Opening);
        apertura.Date.Should().Be(new DateOnly(2025, 12, 31));
        apertura.PeriodId.Should().BeNull();
        apertura.Lines.Should().OnlyContain(l => l.IsPosted);
        (await e.D.Db.AccountingSetups.SingleAsync()).OpeningDocumentId.Should().Be(apertura.Id);

        // Otra importación y otro borrador AP digitado: rechazados mientras haya una vigente.
        var segunda = await e.ImportarAsync();
        segunda.Error.Code.Should().Be("Accounting.Opening.AlreadyExists");
        segunda.Error.Should().BeOfType<ErrorConDatos>();
        var digitada = await new SaveDraftDocumentCommandHandler(e.D.Db, e.D.Clock, e.D.User, e.D.Alcance, e.D.Poster).Handle(
            new SaveDraftDocumentCommand(null, "AP", new DateOnly(2026, 3, 1), "Otra apertura",
                [new LineaDeBorradorInput(e.Caja.Code, null, null, null, null, null, 1m, 0m, null, null), new LineaDeBorradorInput(e.Aportes.Code, null, null, null, null, null, 0m, 1m, null, null)]),
            CancellationToken.None);
        digitada.Error.Code.Should().Be("Accounting.Opening.AlreadyExists");

        // Reversar: en la misma fecha, de la misma clase, y la configuración suelta la referencia.
        var reversion = await e.ReversarAsync(primera);
        reversion.IsSuccess.Should().BeTrue(reversion.Error?.Message);
        var reverso = await e.D.Db.AccountingDocuments.SingleAsync(d => d.PublicId == reversion.Value.ReversalPublicId);
        reverso.Kind.Should().Be(DocumentKind.Opening);
        reverso.Date.Should().Be(new DateOnly(2025, 12, 31));
        reverso.PeriodId.Should().BeNull();
        (await e.D.Db.AccountingSetups.SingleAsync()).OpeningDocumentId.Should().BeNull();
        (await e.D.Db.AccountingDocuments.SingleAsync(d => d.PublicId == primera)).Status.Should().Be(DocumentStatus.Reversed);

        // Y se carga otra.
        var tercera = await e.ImportarAsync();
        tercera.IsSuccess.Should().BeTrue(tercera.Error?.Message);
        (await e.ContabilizarAsync(tercera.Value.DraftPublicId)).IsSuccess.Should().BeTrue();
        (await e.D.Db.AccountingSetups.SingleAsync()).OpeningDocumentId.Should().NotBeNull();
    }

    [Fact]
    public async Task La_apertura_digitada_toma_la_fecha_que_le_toca_y_no_la_digitada()
    {
        var e = new Escenario();
        var r = await new SaveDraftDocumentCommandHandler(e.D.Db, e.D.Clock, e.D.User, e.D.Alcance, e.D.Poster).Handle(
            new SaveDraftDocumentCommand(null, "AP", new DateOnly(2026, 3, 1), "Apertura digitada",
                [new LineaDeBorradorInput(e.Caja.Code, null, null, null, null, null, 500m, 0m, null, null), new LineaDeBorradorInput(e.Aportes.Code, null, null, null, null, null, 0m, 500m, null, null)]),
            CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Errors.Should().NotContain(x => x.Severity == "Error");
        var borrador = await e.D.Db.AccountingDocuments.SingleAsync(d => d.PublicId == r.Value.PublicId);
        borrador.Kind.Should().Be(DocumentKind.Opening);
        borrador.Date.Should().Be(new DateOnly(2025, 12, 31));
        borrador.PeriodId.Should().BeNull();
        (await e.ContabilizarAsync(borrador.PublicId)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_columna_obligatoria_o_sin_filas_no_importa()
    {
        var e = new Escenario();
        e.Lector.LeerAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new TablaLeida(["cuenta", "valor"], [new FilaLeida(2, ["11050501", "10"])], "csv"))));
        (await e.ImportarAsync()).Error.Code.Should().Be("Archivo.ColumnaFaltante");

        e.Archivo();
        (await e.ImportarAsync()).Error.Code.Should().Be("Archivo.Vacio");
    }
}
