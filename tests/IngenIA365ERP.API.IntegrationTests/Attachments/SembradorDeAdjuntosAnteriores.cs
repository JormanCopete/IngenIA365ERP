using System.Security.Cryptography;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// Deja en una cooperativa un adjunto del <b>formato anterior</b> (<c>AppEncrypted</c>): cifrado por la
/// aplicación, subido con <see cref="IBlobStore.PutAsync"/> y con su fila, exactamente como lo dejaba
/// <c>UploadAttachmentCommand</c> antes de la feature 011. Sólo de pruebas.
///
/// <para>
/// Existe porque la ruta por la que se sembraba (<c>POST /api/attachments</c>) se retiró el 2026-09-23,
/// y porque <c>UploadAttachmentCommand</c> va a pasar al formato directo (T062): sembrar por él dejaría
/// de probar el formato anterior sin que ninguna prueba se pusiera roja. Los archivos que ya existen en
/// DEV y QA son de este formato y se siguen leyendo a través de la API (contracts/api.md §6).
/// </para>
/// </summary>
public static class SembradorDeAdjuntosAnteriores
{
    public sealed record Sembrado(Guid PublicId, byte[] Contenido, string Sha256Hex);

    public static async Task<Sembrado> SembrarAsync(
        CentralIdentityApiFixture fx, Guid cooperativa, string ownerEntityType, Guid ownerEntityPublicId,
        byte[] contenido, string nombre = "soporte.pdf", string tipo = "application/pdf")
    {
        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == cooperativa);

        var sha256 = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();
        var cifrado = servicios.GetRequiredService<IAttachmentCipher>().Encrypt(contenido);
        BlobReference referencia;
        await using (var flujo = new MemoryStream(cifrado.EncryptedBlob, writable: false))
        {
            referencia = await servicios.GetRequiredService<IBlobStore>().PutAsync(flujo, new BlobMetadata(
                TenantId: entrada.Id.ToString(),
                OwnerEntityType: ownerEntityType,
                OwnerEntityPublicId: ownerEntityPublicId,
                OriginalFileName: nombre,
                ContentType: tipo,
                SizeBytes: contenido.LongLength,
                Sha256Hex: sha256), CancellationToken.None);
        }

        var adjunto = new Attachment
        {
            TenantId = entrada.Id,
            OwnerEntityType = ownerEntityType,
            OwnerEntityPublicId = ownerEntityPublicId,
            FileName = nombre,
            ContentType = tipo,
            SizeBytes = contenido.LongLength,
            Sha256Hex = sha256,
            StoragePath = referencia.Uri,
            EncryptedDek = cifrado.WrappedDekBase64,
            Format = FormatoDeAdjunto.AppEncrypted,
            Status = EstadoDeAdjunto.Available,
            CreatedBy = "sembrador@pruebas",
            UpdatedBy = "sembrador@pruebas",
        };
        await using (var baseDeLaCooperativa = servicios.GetRequiredService<ITenantDbContextFactory>()
                         .Abrir(entrada.DatabaseName!, entrada.ConnectionString))
        {
            baseDeLaCooperativa.Db.Attachments.Add(adjunto);
            await baseDeLaCooperativa.Db.SaveChangesAsync(CancellationToken.None);
        }

        return new Sembrado(adjunto.PublicId, contenido, sha256);
    }
}
