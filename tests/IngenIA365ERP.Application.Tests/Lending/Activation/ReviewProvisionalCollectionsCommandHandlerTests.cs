using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Tests.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;

namespace IngenIA365ERP.Application.Tests.Lending.Activation;

/// <summary>
/// Feature 013, US8, T0722 (plantillas.md §6; C62; api.md §19.3, <c>POST /api/lending/activation/provisional-collections/review</c>): la revisión
/// previa de los cobros provisionales. La factura es una venta con <c>VentaACreditoRegistrada</c> acumulado (sin consumir) <b>confirmada después del
/// corte</b>, no una de la migración; la suma de una factura no supera su valor a crédito (<c>Lending.ProvisionalCollection.ExceedsSale</c>); la fecha
/// valor va entre la venta y hoy; la llave <c>factura</c> + <c>referenciaContable</c> no se repite. Sin errores deja el archivo como adjunto de módulo de
/// la carga y devuelve <c>{ stagedAttachmentPublicId, sha256, rows, errors[] }</c> <b>sin guardar filas</b>; con errores responde 422
/// <c>Import.Invalid</c> y no sube nada. InMemory sobre las ventas de US6 y el lector de archivos doble. (nuevo)
/// </summary>
public sealed class ReviewProvisionalCollectionsCommandHandlerTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = VentasACreditoDePrueba.Hoy;
    private static readonly DateOnly Corte = Hoy.AddDays(-10);

    private VentasACreditoDePrueba _v = null!;
    private MigrationLoad _carga = null!;
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly List<UploadAttachmentCommand> _subidas = [];
    private readonly Guid _adjunto = Guid.NewGuid();
    private TablaLeida _tabla = new([], [], "xlsx");

    public async Task InitializeAsync()
    {
        _v = await VentasACreditoDePrueba.CrearAsync();
        var setup = await _v.Db.LendingSetups.SingleAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica);
        setup.ActivationDate = null;
        _carga = new MigrationLoad { Number = 1, CutoffDate = Corte, Status = MigrationLoadStatus.Applied, ActivationDeadline = Corte.AddDays(15),
            CreatedByUserId = _v.Cajera.Id, CurrentCheckVersion = 1 };
        _v.Db.MigrationLoads.Add(_carga);
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();

        // Dos ventas confirmadas después del corte y una de la migración (hasta el corte).
        foreach (var (numero, dias, monto) in new[] { ("FV-POS1", -5, 600_000m), ("FV-POS2", -2, 300_000m), ("FV-MIG", -20, 500_000m) })
        {
            var venta = _v.NuevaVenta(monto, Hoy.AddDays(dias), numero);
            await _v.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta));
        }
        _v.G.ActuarComo(_v.Cajera);
        _v.G.Permisos.EsMaestroGlobal.Returns(true);
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(_tabla)));
        _sender.Send(Arg.Do<UploadAttachmentCommand>(_subidas.Add), Arg.Any<CancellationToken>()).Returns(Result.Success(_adjunto));
    }

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    private ValidacionDeCobrosProvisionales Validacion() =>
        new(_v.Db, _lector, _v.G.Permisos, new MensajesEntrantes(_v.Db), Substitute.For<IBlobStore>());

    private ReviewProvisionalCollectionsCommandHandler Handler() => new(_v.Db, Validacion(), _sender, _v.G.Reloj);

    private void Archivo(params (string Factura, string Valor, string Fecha, string Referencia)[] filas)
    {
        var encabezados = DefinicionesDeMigracion.CobrosProvisionales.Hojas[0].Columnas.Select(c => c.Nombre).ToList();
        _tabla = new TablaLeida(encabezados, filas.Select((f, i) => new FilaLeida(i + 2, encabezados.Select(e => e switch
        {
            C.Factura => f.Factura,
            C.Valor => f.Valor,
            C.FechaValor => f.Fecha,
            C.ReferenciaContable => f.Referencia,
            _ => (string?)null,
        }).ToArray())).ToList(), "xlsx");
    }

    private Task<Result<ProvisionalCollectionsReviewDto>> RevisarAsync(string nombre = "cobros.xlsx") =>
        Handler().Handle(new ReviewProvisionalCollectionsCommand(new ArchivoDeImportacion(nombre, Encoding.UTF8.GetBytes("cobros:" + Guid.NewGuid())))
            { OperationKey = Guid.NewGuid() }, default);

    private static string Dia(int dias) => Hoy.AddDays(dias).ToString("yyyy-MM-dd");

    private static IReadOnlyList<ErrorDeFila> Errores(Error error) =>
        error.Should().BeOfType<ErrorConDatos>().Subject.Data.Should().BeOfType<ImportResultDto>().Subject.Errors;

    [Fact]
    public async Task Sin_errores_deja_el_adjunto_de_la_carga_y_no_guarda_filas()
    {
        Archivo(("FV-POS1", "200000", Dia(-4), "RC-1"), ("FV-POS1", "400000", Dia(-1), "RC-2"), ("FV-POS2", "300000", Dia(0), "RC-3"));

        var r = await RevisarAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Should().BeEquivalentTo(new { StagedAttachmentPublicId = _adjunto, Rows = 3, Total = 900_000m }, o => o.ExcludingMissingMembers());
        r.Value.Sha256.Should().HaveLength(64);
        r.Value.Errors.Should().BeEmpty();
        var subida = _subidas.Should().ContainSingle().Subject;
        (subida.OwnerEntityType, subida.OwnerEntityPublicId, subida.FileName).Should().Be((AdjuntosDeModulo.CobrosProvisionales, _carga.PublicId, "cobros.xlsx"));
        subida.ContentType.Should().Be(ReviewProvisionalCollectionsCommandHandler.TipoXlsx);
        AdjuntosDeModulo.De(AdjuntosDeModulo.CobrosProvisionales)!.Borrable.Should().BeFalse("lo genera Cartera y no se borra");
        (await _v.Db.ProvisionalCollections.CountAsync()).Should().Be(0, "la revisión no guarda filas: las guarda la activación");
    }

    [Fact]
    public async Task Un_csv_se_guarda_como_csv()
    {
        Archivo(("FV-POS2", "100000", Dia(-1), "RC-9"));

        (await RevisarAsync("cobros.csv")).IsSuccess.Should().BeTrue();

        _subidas.Single().ContentType.Should().Be(ReviewProvisionalCollectionsCommandHandler.TipoCsv);
    }

    [Fact]
    public async Task Una_venta_hasta_el_corte_una_desconocida_una_fecha_fuera_de_rango_y_una_llave_repetida_son_errores_de_fila_y_no_sube_nada()
    {
        Archivo(
            ("FV-MIG", "100000", Dia(-4), "RC-1"),
            ("FV-NADA", "100000", Dia(-4), "RC-2"),
            ("FV-POS1", "100000", Dia(-6), "RC-3"),
            ("FV-POS1", "100000", Dia(1), "RC-4"),
            ("FV-POS2", "50000", Dia(-1), "RC-5"),
            ("FV-POS2", "50000", Dia(-1), "RC-5"));

        var r = await RevisarAsync();

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        Errores(r.Error).Select(e => (e.Row, e.Column, e.Code)).Should().BeEquivalentTo(
        [
            (2, C.Factura, ImportErrors.CellNotFound),
            (3, C.Factura, ImportErrors.CellNotFound),
            (4, C.FechaValor, ImportErrors.CellFormat),
            (5, C.FechaValor, ImportErrors.CellFormat),
            (7, C.ReferenciaContable, ImportErrors.RowDuplicate),
        ]);
        _subidas.Should().BeEmpty();
    }

    [Fact]
    public async Task La_suma_de_una_factura_que_supera_la_venta_responde_ExceedsSale()
    {
        Archivo(("FV-POS2", "200000", Dia(-1), "RC-1"), ("FV-POS2", "150000", Dia(-1), "RC-2"));

        var r = await RevisarAsync();

        r.Error.Code.Should().Be(ErroresDeMigracion.CodigoCobrosExcedenLaVenta);
        r.Error.Message.Should().Contain("FV-POS2");
        Errores(r.Error).Should().ContainSingle().Which.Should().BeEquivalentTo(new { Row = 3, Column = C.Valor }, o => o.ExcludingMissingMembers());
        _subidas.Should().BeEmpty();

        Archivo(("FV-POS2", "200000", Dia(-1), "RC-1"), ("FV-POS2", "150000", Dia(-1), "RC-2"), ("FV-NADA", "1", Dia(-1), "RC-3"));
        (await RevisarAsync()).Error.Code.Should().Be(ImportErrors.InvalidCode, "con otros errores, el archivo es inválido");
    }

    [Fact]
    public async Task Sin_carga_aplicada_o_con_Cartera_activada_no_revisa()
    {
        Archivo(("FV-POS2", "100000", Dia(-1), "RC-1"));
        var carga = await _v.Db.MigrationLoads.SingleAsync();
        carga.Status = MigrationLoadStatus.Replaced;
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();

        var sinCarga = await RevisarAsync();

        sinCarga.Error.Code.Should().Be(ErroresDeActivacion.CodigoFaltanRequisitos);
        var setup = await _v.Db.LendingSetups.SingleAsync();
        setup.ActivationDate = Hoy;
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();
        (await RevisarAsync()).Error.Code.Should().Be(ErroresDeActivacion.CodigoYaActivada);
    }
}
