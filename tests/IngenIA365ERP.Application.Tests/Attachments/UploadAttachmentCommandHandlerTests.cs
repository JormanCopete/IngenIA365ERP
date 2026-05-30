using System.Security.Cryptography;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Attachments;

/// <summary>
/// T102 — Tests del handler de upload (US5). Cubre cifrado, hash,
/// persistencia, validación de tamaño/MIME y aislamiento por tenant.
/// </summary>
public class UploadAttachmentCommandHandlerTests
{
    private static (UploadAttachmentCommandHandler Handler,
                    TestApplicationDbContext Db,
                    IBlobStore Store,
                    IAttachmentCipher Cipher)
        Build(string? tenantId = "1")
    {
        var db = TestDbContextFactory.Create();
        var store = Substitute.For<IBlobStore>();
        var cipher = Substitute.For<IAttachmentCipher>();
        var cu = Substitute.For<ICurrentUserService>();
        cu.TenantId.Returns(tenantId);
        cu.UserName.Returns("ana@demo");
        return (new UploadAttachmentCommandHandler(
            db, store, cipher, cu,
            NullLogger<UploadAttachmentCommandHandler>.Instance), db, store, cipher);
    }

    [Fact]
    public async Task Computes_sha256_encrypts_persists_blob_and_metadata()
    {
        var (handler, db, store, cipher) = Build();

        var payload = "%PDF-1.7\nfake pdf body"u8.ToArray();
        var expectedSha = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

        cipher.Encrypt(Arg.Any<byte[]>())
            .Returns(new AttachmentCipherPayload([0xAB, 0xCD], "wrapped-dek-base64"));
        store.PutAsync(Arg.Any<Stream>(), Arg.Any<BlobMetadata>(), Arg.Any<CancellationToken>())
            .Returns(new BlobReference("demo/2026/05/abc123.bin"));

        var ownerId = Guid.NewGuid();
        var result = await handler.Handle(
            new UploadAttachmentCommand("User", ownerId, "evidencia.pdf",
                "application/pdf", payload),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = db.Attachments.Single();
        persisted.TenantId.Should().Be(1);
        persisted.OwnerEntityType.Should().Be("User");
        persisted.OwnerEntityPublicId.Should().Be(ownerId);
        persisted.FileName.Should().Be("evidencia.pdf");
        persisted.ContentType.Should().Be("application/pdf");
        persisted.SizeBytes.Should().Be(payload.LongLength);
        persisted.Sha256Hex.Should().Be(expectedSha);
        persisted.StoragePath.Should().Be("demo/2026/05/abc123.bin");
        persisted.EncryptedDek.Should().Be("wrapped-dek-base64");
        persisted.CreatedBy.Should().Be("ana@demo");
    }

    [Fact]
    public async Task Encrypts_payload_before_passing_to_store()
    {
        var (handler, _, store, cipher) = Build();
        var payload = "secret data"u8.ToArray();
        var encrypted = new byte[] { 1, 2, 3, 4 };
        cipher.Encrypt(Arg.Any<byte[]>())
            .Returns(new AttachmentCipherPayload(encrypted, "dek"));
        store.PutAsync(Arg.Any<Stream>(), Arg.Any<BlobMetadata>(), Arg.Any<CancellationToken>())
            .Returns(new BlobReference("x"));

        await handler.Handle(
            new UploadAttachmentCommand("User", Guid.NewGuid(), "f.pdf",
                "application/pdf", payload),
            CancellationToken.None);

        cipher.Received(1).Encrypt(Arg.Is<byte[]>(b => b.SequenceEqual(payload)));
        // El stream pasado al store debe llevar los bytes cifrados, NUNCA el plaintext.
        await store.Received(1).PutAsync(
            Arg.Is<Stream>(s => StreamMatches(s, encrypted)),
            Arg.Any<BlobMetadata>(),
            Arg.Any<CancellationToken>());
    }

    // Rollback de blob cuando SaveChanges falla: cubierto a nivel de código
    // por el `try/catch` del handler que llama `store.DeleteAsync` en
    // CancellationToken.None — un test unitario requiere mocks complejos
    // de IApplicationDbContext. La invariante se verifica con inspección
    // de código + integration test futuro con BD que falla.

    [Fact]
    public async Task Fails_with_TenantRequired_when_no_tenant_in_context()
    {
        var (handler, _, store, cipher) = Build(tenantId: null);

        var result = await handler.Handle(
            new UploadAttachmentCommand("User", Guid.NewGuid(), "f.pdf",
                "application/pdf", "x"u8.ToArray()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.TenantRequired");
        cipher.DidNotReceive().Encrypt(Arg.Any<byte[]>());
        await store.DidNotReceive().PutAsync(
            Arg.Any<Stream>(), Arg.Any<BlobMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_rejects_oversized_file()
    {
        var validator = new UploadAttachmentCommandValidator();
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
        var validator = new UploadAttachmentCommandValidator();

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
        var validator = new UploadAttachmentCommandValidator();

        var result = validator.Validate(new UploadAttachmentCommand(
            "User", Guid.NewGuid(), "vacio.pdf", "application/pdf", []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorCode == AttachmentErrorCodes.Validation_FileEmpty);
    }

    private static bool StreamMatches(Stream stream, byte[] expected)
    {
        if (stream is not MemoryStream ms) return false;
        return ms.ToArray().SequenceEqual(expected);
    }
}
