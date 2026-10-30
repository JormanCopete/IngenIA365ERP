using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Statements;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Statements;

/// <summary>
/// Feature 015, US6, T0186 (R-06; FR-065, FR-066, FR-196, FR-197): un solo <see cref="LoadBankStatementCommand"/> (para las pantallas de Tesorería y de Recaudo) lee el
/// extracto con el formato del banco, <c>EXTRACTO-GENERICO</c> o la captura a mano, con revisión previa que no guarda y error por línea; las ilegibles se informan y no se
/// cargan (<c>Treasury.Statement.IllegibleLines</c>); un archivo ya cargado se reconoce por su huella (<c>.AlreadyLoaded</c>); lo ya cargado por la huella del banco se
/// omite y se cuenta; los iguales sin secuencia quedan como posibles duplicados para que una persona decida. Prueba escrita, sin correr.
/// </summary>
public class CargaDeExtractoTests
{
    private readonly EscenarioDeTesoreria _t = new();
    private readonly ISender _sender = Substitute.For<ISender>();

    public CargaDeExtractoTests()
    {
        _sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success(Guid.NewGuid()));
        var formato = new BankFileFormat();
        FormatosGenericosDeRecaudoYTesoreria.DefinicionDelExtracto().AplicarA(formato, bankId: null);
        _t.E.Db.BankFileFormats.Add(formato);
        _t.E.Db.SaveChanges();
        _t.E.Como(_t.E.Supervisor);
    }

    private LoadBankStatementCommandHandler Cargar() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Numerador,
        new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _sender, _t.E.Reloj);

    private static byte[] Csv(params string[] lineas) =>
        Encoding.UTF8.GetBytes(string.Join("\r\n", new[] { "fecha;valor;referencia;documento;consignante;descripcion;saldo" }.Concat(lineas)));

    private static readonly byte[] CincoAbonos = Csv(
        "2026-11-25;150000.00;REF-1;1010;Ana Pérez;Consignación;1150000.00",
        "2026-11-25;80000.00;REF-2;1011;Luis Gómez;Transferencia;1230000.00",
        "2026-11-26;60000.00;REF-3;;;PSE;1290000.00",
        "2026-11-26;-20000.00;;;;Comisión;1270000.00",
        "2026-11-27;45000.00;REF-5;2020;Rosa Díaz;Transferencia;1315000.00");

    [Fact]
    public async Task La_revision_previa_no_guarda_nada()
    {
        var r = await Cargar().Handle(new LoadBankStatementCommand(_t.Pagos.PublicId, "extracto.csv", CincoAbonos, Preview: true) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Preview.Should().BeTrue();
        r.Value.Loaded.Should().Be(5);
        r.Value.Source.Should().Be(nameof(BankStatementSource.GenericTemplate));
        (await _t.E.Db.TreasuryBankStatements.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Carga_el_extracto_y_el_mismo_archivo_no_se_carga_dos_veces()
    {
        var primera = await Cargar().Handle(new LoadBankStatementCommand(_t.Pagos.PublicId, "extracto.csv", CincoAbonos) { OperationKey = Guid.NewGuid() }, default);

        primera.IsSuccess.Should().BeTrue(primera.IsFailure ? primera.Error.Code : null);
        (await _t.E.Db.TreasuryBankStatementLines.CountAsync()).Should().Be(5);
        (await _t.E.Db.TreasuryBankStatementLines.CountAsync(l => l.RecaudoStatus == StatementLineRecaudoStatus.Pending)).Should().Be(4, "los abonos quedan para Recaudo");
        await _sender.Received(1).Send(Arg.Is<UploadAttachmentCommand>(c => c.OwnerEntityType == LoadBankStatementCommandHandler.TipoDeAdjunto), Arg.Any<CancellationToken>());

        var segunda = await Cargar().Handle(new LoadBankStatementCommand(_t.Pagos.PublicId, "extracto.csv", CincoAbonos) { OperationKey = Guid.NewGuid() }, default);
        segunda.Error.Code.Should().Be(ErroresDeTesoreria.CodigoExtractoYaCargado);
    }

    [Fact]
    public async Task Sin_secuencia_los_iguales_quedan_como_posibles_duplicados()
    {
        await Cargar().Handle(new LoadBankStatementCommand(_t.Pagos.PublicId, "a.csv", CincoAbonos) { OperationKey = Guid.NewGuid() }, default);
        var otro = Csv("2026-11-25;150000.00;REF-1;1010;Ana Pérez;Consignación;1150000.00", "2026-11-28;10000.00;REF-9;;;PSE;1325000.00");

        var r = await Cargar().Handle(new LoadBankStatementCommand(_t.Pagos.PublicId, "b.csv", otro) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue();
        r.Value.PossibleDuplicates.Should().Be(1, "sin identificador del banco una persona decide (FR-197)");
        var duplicado = await _t.E.Db.TreasuryBankStatementLines.SingleAsync(l => l.DuplicateDecision == StatementLineDuplicateDecision.Pending);
        duplicado.PossibleDuplicateOfLineId.Should().NotBeNull();
    }

    [Fact]
    public async Task Las_lineas_ilegibles_se_informan_y_no_se_cargan()
    {
        var conErrores = Csv("2026-11-25;150000.00;REF-1;;;;", "no-es-fecha;abc;;;;;", "2026-11-26;60000.00;REF-3;;;;");

        var sinAceptar = await Cargar().Handle(new LoadBankStatementCommand(_t.Pagos.PublicId, "c.csv", conErrores) { OperationKey = Guid.NewGuid() }, default);
        sinAceptar.Error.Code.Should().Be(ErroresDeTesoreria.CodigoLineasIlegibles);

        var aceptando = await Cargar().Handle(new LoadBankStatementCommand(_t.Pagos.PublicId, "c.csv", conErrores, AcceptIllegible: true) { OperationKey = Guid.NewGuid() }, default);
        aceptando.IsSuccess.Should().BeTrue(aceptando.IsFailure ? aceptando.Error.Code : null);
        aceptando.Value.Illegible.Should().Be(1);
        (await _t.E.Db.TreasuryBankStatementLines.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Se_captura_a_mano_con_la_misma_huella()
    {
        var r = await Cargar().Handle(new LoadBankStatementCommand(_t.Pagos.PublicId, Captured:
        [
            new MovimientoCapturado(new DateOnly(2026, 11, 25), 150_000m, "REF-1", DepositorTaxId: "1010"),
            new MovimientoCapturado(new DateOnly(2026, 11, 25), -5_000m, Description: "Comisión"),
        ]) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Source.Should().Be(nameof(BankStatementSource.Manual));
        (await _t.E.Db.TreasuryBankStatements.SingleAsync()).FileSha256.Should().BeNull();
        await _sender.DidNotReceive().Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fuera_del_alcance_de_cuentas_no_existe()
    {
        _t.Alcance.Valor = new Application.Common.Scope.AlcanceDeTesoreria(false, new HashSet<int> { _t.FondoDeLiquidez.Id });

        var r = await Cargar().Handle(new LoadBankStatementCommand(_t.Pagos.PublicId, "x.csv", CincoAbonos) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Generic.NotFound");
    }
}
