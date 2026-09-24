using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.SolicitarSubida;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Attachments;

/// <summary>
/// Feature 011 (US1, T035): pedir una subida registra el adjunto como <c>Uploading</c> y devuelve una
/// autorización para <b>ese</b> archivo. Los códigos del contrato (tamaño, tipo, vacío) los responde el
/// handler, no el validador, para que lleguen al cliente.
/// </summary>
public class SolicitarSubidaDeAdjuntoCommandHandlerTests
{
    private static SolicitarSubidaDeAdjuntoCommandHandler Handler(EscenarioDeAdjuntos e, TestApplicationDbContext db) =>
        new(db, e.Almacen, e.Permisos, e.Usuario, e.Reloj, e.Limites, NullLogger<SolicitarSubidaDeAdjuntoCommandHandler>.Instance);

    private static SolicitarSubidaDeAdjuntoCommand Pedido(EscenarioDeAdjuntos e, long tamano = 20_481_234, string tipo = "application/pdf",
        string nombre = "Factura 1234 – Núñez.pdf", string? dueno = null, Guid? duenoId = null) =>
        new(dueno ?? AdjuntosDeModulo.Comprobante, duenoId ?? e.Borrador, nombre, tipo, tamano, EscenarioDeAdjuntos.Huella(EscenarioDeAdjuntos.Pdf()));

    [Fact]
    public async Task Registra_la_fila_subiendo_y_firma_para_ese_tamano_tipo_y_huella()
    {
        var e = new EscenarioDeAdjuntos();
        var pedido = Pedido(e);

        using var db = e.Base.Db();
        var r = await Handler(e, db).Handle(pedido, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var fila = e.Fila(r.Value.AttachmentPublicId);
        fila.Status.Should().Be(EstadoDeAdjunto.Uploading);
        fila.Format.Should().Be(FormatoDeAdjunto.Direct);
        fila.EncryptedDek.Should().BeEmpty("el formato directo no lleva cifrado de la aplicación");
        fila.UploadExpiresAt.Should().Be(EscenarioDeAdjuntos.Ahora.AddMinutes(5));
        fila.Sha256Hex.Should().Be(AdjuntosDirectos.Base64AHex(pedido.Sha256Base64));
        fila.SizeBytes.Should().Be(20_481_234);

        var firmada = e.Firmadas.Should().ContainSingle().Subject;
        firmada.Metadata.ContentType.Should().Be("application/pdf");
        firmada.Metadata.SizeBytes.Should().Be(20_481_234);
        firmada.Sha256Base64.Should().Be(pedido.Sha256Base64);
        firmada.VenceEn.Should().Be(new DateTimeOffset(EscenarioDeAdjuntos.Ahora.AddMinutes(5)));
        firmada.Referencia.Should().BeNull("una subida nueva tiene clave nueva");
        fila.StoragePath.Should().NotBeNullOrEmpty();

        r.Value.Upload.Method.Should().Be("POST");
        r.Value.Upload.FileField.Should().Be("file");
        r.Value.Upload.Fields["key"].Should().Be("dev/" + fila.StoragePath);
        r.Value.MaxBytes.Should().Be(AttachmentPolicy.MaxBytes);
    }

    [Theory]
    [InlineData(0L, "application/pdf", AttachmentErrorCodes.Validation_FileEmpty)]
    [InlineData(AttachmentPolicy.MaxBytes + 1, "application/pdf", AttachmentErrorCodes.Validation_FileTooLarge)]
    [InlineData(100L, "application/x-msdownload", AttachmentErrorCodes.Validation_MimeTypeNotAllowed)]
    public async Task Tamano_tipo_y_vacio_responden_con_el_codigo_del_contrato_sin_registrar_nada(long tamano, string tipo, string codigo)
    {
        var e = new EscenarioDeAdjuntos();

        using var db = e.Base.Db();
        var r = await Handler(e, db).Handle(Pedido(e, tamano, tipo), CancellationToken.None);

        r.Error.Code.Should().Be(codigo);
        db.Attachments.Should().BeEmpty();
        e.Firmadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Pasarse_del_tamano_dice_cual_es_el_maximo()
    {
        var e = new EscenarioDeAdjuntos();

        using var db = e.Base.Db();
        var r = await Handler(e, db).Handle(Pedido(e, AttachmentPolicy.MaxBytes + 1), CancellationToken.None);

        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { maxBytes = AttachmentPolicy.MaxBytes });
        r.Error.Message.Should().Contain("25 MB");
    }

    [Fact]
    public async Task No_se_suben_archivos_a_lo_que_genera_un_modulo()
    {
        var e = new EscenarioDeAdjuntos();

        using var db = e.Base.Db();
        var r = await Handler(e, db).Handle(Pedido(e, dueno: "PilaGeneration", duenoId: Guid.NewGuid()), CancellationToken.None);

        r.Error.Code.Should().Be(AttachmentErrorCodes.OwnerNotAllowed);
        e.Firmadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_permiso_de_escribir_comprobantes_o_con_un_comprobante_que_no_existe_responde_404()
    {
        var e = new EscenarioDeAdjuntos();
        using var db = e.Base.Db();
        var inexistente = await Handler(e, db).Handle(Pedido(e, duenoId: Guid.NewGuid()), CancellationToken.None);

        e.Permisos.HasPermissionAsync("Accounting.Vouchers.Create", Arg.Any<CancellationToken>()).Returns(false);
        var sinPermiso = await Handler(e, db).Handle(Pedido(e), CancellationToken.None);

        inexistente.Error.Code.Should().Be("Generic.NotFound");
        sinPermiso.Error.Code.Should().Be("Generic.NotFound");
        db.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task El_nombre_se_guarda_sin_la_ruta_que_mandan_algunos_navegadores()
    {
        var e = new EscenarioDeAdjuntos();

        using var db = e.Base.Db();
        var r = await Handler(e, db).Handle(Pedido(e, nombre: @"C:\fakepath\factura.pdf"), CancellationToken.None);

        e.Fila(r.Value.AttachmentPublicId).FileName.Should().Be("factura.pdf");
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-base64")]
    [InlineData("n4bQgYhMfWWaL+qgxVrQFaO/TxsrC4Is0V1sFbDwCg==")]
    public void Una_huella_que_no_es_un_SHA256_en_base64_no_pasa_el_validador(string huella)
    {
        var e = new EscenarioDeAdjuntos();

        new SolicitarSubidaDeAdjuntoCommandValidator().Validate(Pedido(e) with { Sha256Base64 = huella }).IsValid.Should().BeFalse();
    }
}
