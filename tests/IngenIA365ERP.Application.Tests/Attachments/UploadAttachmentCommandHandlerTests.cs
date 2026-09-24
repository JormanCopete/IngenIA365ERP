using System.Security.Cryptography;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Attachments;

/// <summary>
/// T102 — El camino interno por el que los módulos guardan lo que generan (PILA, dispersión, definitiva).
/// Feature 011 (research R11, T060): el contenido va tal como se generó —sin el cifrado de la aplicación,
/// que ahora pone el bucket—, en formato <c>Direct</c> y ya <c>Available</c>, con la huella de ese mismo
/// contenido. Si la fila no se guarda, el objeto se retira.
/// </summary>
public class UploadAttachmentCommandHandlerTests
{
    private sealed class Escenario
    {
        public BaseQueFallaAlGuardar Base { get; } = new();
        public IBlobStore Almacen { get; } = Substitute.For<IBlobStore>();
        public ICurrentUserService Usuario { get; } = Substitute.For<ICurrentUserService>();
        public List<byte[]> Subido { get; } = [];

        public Escenario(string? tenantId = "1")
        {
            Usuario.TenantId.Returns(tenantId);
            Usuario.UserName.Returns("ana@demo");
            Almacen.PutAsync(Arg.Any<Stream>(), Arg.Any<BlobMetadata>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    using var copia = new MemoryStream();
                    ci.Arg<Stream>().CopyTo(copia);
                    Subido.Add(copia.ToArray());
                    return new BlobReference("1/2026/09/abc123.bin");
                });
        }

        public UploadAttachmentCommandHandler Handler(TestApplicationDbContext db) =>
            new(db, Almacen, Usuario, NullLogger<UploadAttachmentCommandHandler>.Instance);
    }

    [Fact]
    public async Task Guarda_el_contenido_tal_cual_en_formato_directo_y_disponible_con_su_huella()
    {
        var e = new Escenario();
        var planilla = "0100000001PILA DE PRUEBA\r\n"u8.ToArray();
        var dueno = Guid.NewGuid();

        using var db = e.Base.Db();
        var r = await e.Handler(db).Handle(new UploadAttachmentCommand("PilaGeneration", dueno, "pila-2026-09.txt", "text/plain", planilla), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        e.Subido.Should().ContainSingle().Which.Should().Equal(planilla, "el almacén recibe lo que se generó, no un cifrado de la aplicación");
        var fila = await db.Attachments.SingleAsync();
        fila.PublicId.Should().Be(r.Value);
        fila.Format.Should().Be(FormatoDeAdjunto.Direct);
        fila.Status.Should().Be(EstadoDeAdjunto.Available, "lo generó el programa: no hay nada que validar (FR-030)");
        fila.EncryptedDek.Should().BeEmpty();
        fila.Sha256Hex.Should().Be(Convert.ToHexString(SHA256.HashData(planilla)).ToLowerInvariant());
        fila.StoragePath.Should().Be("1/2026/09/abc123.bin");
        fila.OwnerEntityPublicId.Should().Be(dueno);
        fila.CreatedBy.Should().Be("ana@demo");
        await e.Almacen.Received(1).PutAsync(Arg.Any<Stream>(),
            Arg.Is<BlobMetadata>(m => m.ContentType == "text/plain" && m.SizeBytes == planilla.Length && m.Sha256Hex == fila.Sha256Hex),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_la_fila_no_se_guarda_el_objeto_se_retira()
    {
        var e = new Escenario();
        e.Base.Falla = true;

        using var db = e.Base.Db();
        await FluentActions.Invoking(() => e.Handler(db).Handle(
                new UploadAttachmentCommand("PilaGeneration", Guid.NewGuid(), "pila.txt", "text/plain", "x"u8.ToArray()), CancellationToken.None))
            .Should().ThrowAsync<DbUpdateException>();

        await e.Almacen.Received(1).DeleteAsync(Arg.Is<BlobReference>(b => b.Uri == "1/2026/09/abc123.bin"), CancellationToken.None);
    }

    [Fact]
    public async Task Fails_with_TenantRequired_when_no_tenant_in_context()
    {
        var e = new Escenario(tenantId: null);

        using var db = e.Base.Db();
        var result = await e.Handler(db).Handle(
            new UploadAttachmentCommand("User", Guid.NewGuid(), "f.pdf", "application/pdf", "x"u8.ToArray()),
            CancellationToken.None);

        result.Error.Code.Should().Be("Auth.TenantRequired");
        await e.Almacen.DidNotReceive().PutAsync(Arg.Any<Stream>(), Arg.Any<BlobMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_rejects_oversized_file()
    {
        var validator = new UploadAttachmentCommandValidator(Microsoft.Extensions.Options.Options.Create(new LimitesDeAdjuntos()));
        var big = new byte[AttachmentPolicy.MaxBytes + 1];

        var result = validator.Validate(new UploadAttachmentCommand(
            "User", Guid.NewGuid(), "huge.pdf", "application/pdf", big));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorCode == AttachmentErrorCodes.Validation_FileTooLarge);
    }

    [Fact]
    public void Validator_rejects_disallowed_mime_type()
    {
        var validator = new UploadAttachmentCommandValidator(Microsoft.Extensions.Options.Options.Create(new LimitesDeAdjuntos()));

        var result = validator.Validate(new UploadAttachmentCommand(
            "User", Guid.NewGuid(), "evil.exe",
            "application/x-msdownload", [1, 2, 3]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorCode == AttachmentErrorCodes.Validation_MimeTypeNotAllowed);
    }

    [Fact]
    public void Validator_rejects_empty_file()
    {
        var validator = new UploadAttachmentCommandValidator(Microsoft.Extensions.Options.Options.Create(new LimitesDeAdjuntos()));

        var result = validator.Validate(new UploadAttachmentCommand(
            "User", Guid.NewGuid(), "vacio.pdf", "application/pdf", []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorCode == AttachmentErrorCodes.Validation_FileEmpty);
    }
}
