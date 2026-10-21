using System.Security.Cryptography;
using FluentAssertions;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Collections.Tareas;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// Feature 013, US13 (T0786; pendiente de la fase 4): el despacho <c>cartera.mensajes</c> adjunta al correo el archivo que el mensaje cita en
/// <c>AttachmentPublicId</c> —el PDF de un documento emitido—, leído del almacén y comprobado contra su SHA-256; si no se puede leer, el envío cuenta
/// como «canal no disponible» y se difiere (un documento no sale sin su PDF); y cuando el mensaje de un documento queda <c>Sent</c>, el documento
/// recibe el evento <c>Delivered</c> con su mensaje.
/// </summary>
public sealed class AdjuntosEnElDespachoTests : IDisposable
{
    private static readonly byte[] Pdf = "%PDF-1.7 PS-000001"u8.ToArray();

    private readonly MensajesDePrueba _e = new();
    private readonly CanalDePrueba _correo;

    public AdjuntosEnElDespachoTests()
    {
        _e.AutorizarTratamiento(_e.Ana);
        _correo = _e.Canal(CodigosDeCanalExterno.Correo, [MessageChannel.Email], consultaEntrega: false, admiteAdjuntos: true);
        _e.Plantilla(EntregaDeDocumentosDeCartera.Plantilla, DebtorMessagePurpose.DocumentDelivery, MessageChannel.Email, "Su {{ClaseDeDocumento}}",
            "Adjuntamos su {{ClaseDeDocumento}} {{NumeroDelDocumento}}. Código {{CodigoDeVerificacion}}.");
    }

    public void Dispose() => _e.Dispose();

    private async Task<(IssuedLendingDocument Documento, DebtorMessage Mensaje)> DocumentoEnColaAsync(byte[] guardado, string huella)
    {
        var adjunto = new Attachment
        {
            TenantId = 1, OwnerEntityType = "LendingIssuedDocument", OwnerEntityPublicId = Guid.NewGuid(), FileName = "ps-000001.pdf",
            ContentType = "application/pdf", SizeBytes = guardado.Length, Sha256Hex = huella, StoragePath = "1/2026/12/ps.bin",
            Format = FormatoDeAdjunto.Direct, Status = EstadoDeAdjunto.Available, CreatedBy = "test",
        };
        _e.Db.Attachments.Add(adjunto);
        var documento = new IssuedLendingDocument
        {
            PublicId = adjunto.OwnerEntityPublicId, Kind = IssuedDocumentKind.LoanClearance, Number = "PS-000001", PersonId = _e.Ana.Id, HolderTaxIdType = "C",
            HolderTaxId = _e.Ana.TaxId, HolderName = "Ana Deudora", InformationCutoff = _e.G.UtcNow, VerificationCode = "7K2MQ9XD4HTPB1RW",
            ContentSha256 = huella, PdfAttachmentPublicId = adjunto.PublicId, RulesVersion = "LND-2026.1", SignerName = "María", SignerTitle = "Gerente",
            IssuedByUserId = 7, IssuedAt = _e.G.UtcNow, DeliveryMode = DocumentDeliveryMode.Email,
        };
        _e.Db.IssuedLendingDocuments.Add(documento);
        await _e.Db.SaveChangesAsync();
        _e.Almacen.GetAsync(Arg.Is<BlobReference>(b => b.Uri == adjunto.StoragePath), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream(guardado)));

        var encolado = await _e.Cola().EncolarAsync(new PedidoDeMensajeAlDeudor(_e.Ana.Id, _e.Credito.Id, DebtorMessagePurpose.DocumentDelivery,
            MessageChannel.Email, EntregaDeDocumentosDeCartera.Plantilla, EntregaDeDocumentosDeCartera.Origen, $"{documento.PublicId:N}", ActorKind.Person, 7)
        {
            OriginPublicId = documento.PublicId,
            AttachmentPublicId = adjunto.PublicId,
            IssuedDocumentId = documento.Id,
            Valores = new Dictionary<string, string?> { ["ClaseDeDocumento"] = "Paz y salvo", ["NumeroDelDocumento"] = "PS-000001", ["CodigoDeVerificacion"] = "7K2M" },
        }, default);
        encolado.IsSuccess.Should().BeTrue(encolado.IsFailure ? encolado.Error.Message : string.Empty);
        await _e.Db.SaveChangesAsync();
        return (documento, encolado.Value.Mensaje);
    }

    private Task CorrerAsync() => new TareaDeMensajesAlDeudor().EjecutarAsync(_e.Servicios(), default);

    [Fact]
    public async Task El_correo_lleva_el_pdf_guardado_y_el_documento_queda_entregado()
    {
        var (documento, mensaje) = await DocumentoEnColaAsync(Pdf, Convert.ToHexString(SHA256.HashData(Pdf)).ToLowerInvariant());

        await CorrerAsync();

        var enviado = _correo.Enviados.Should().ContainSingle().Subject;
        var adjunto = enviado.Adjuntos.Should().ContainSingle().Subject;
        adjunto.Bytes.Should().Equal(Pdf);
        adjunto.NombreDeArchivo.Should().Be("ps-000001.pdf");
        adjunto.ContentType.Should().Be("application/pdf");
        (await _e.UltimoAsync(mensaje)).Kind.Should().Be(DebtorMessageEventKind.Sent);
        var entrega = await _e.Db.IssuedDocumentEvents.AsNoTracking().SingleAsync(e => e.IssuedDocumentId == documento.Id);
        entrega.Kind.Should().Be(IssuedDocumentEventKind.Delivered);
        entrega.DebtorMessageId.Should().Be(mensaje.Id);
        entrega.ActorKind.Should().Be(ActorKind.Process);
    }

    [Fact]
    public async Task Si_el_pdf_no_se_puede_leer_no_sale_y_se_difiere_como_canal_no_disponible()
    {
        var (documento, mensaje) = await DocumentoEnColaAsync(Pdf, new string('0', 64));

        await CorrerAsync();

        _correo.Enviados.Should().BeEmpty("un documento no sale sin su PDF");
        var ultimo = await _e.UltimoAsync(mensaje);
        ultimo.Kind.Should().BeOneOf(DebtorMessageEventKind.Deferred, DebtorMessageEventKind.Failed);
        (await _e.Db.IssuedDocumentEvents.AsNoTracking().AnyAsync(e => e.IssuedDocumentId == documento.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Un_canal_sin_adjuntos_envia_el_texto_solo()
    {
        var sms = _e.Canal(CodigosDeCanalExterno.Correo, [MessageChannel.Email], consultaEntrega: false);
        await DocumentoEnColaAsync(Pdf, Convert.ToHexString(SHA256.HashData(Pdf)).ToLowerInvariant());

        await CorrerAsync();

        sms.Enviados.Should().ContainSingle().Which.Adjuntos.Should().BeEmpty();
    }
}
