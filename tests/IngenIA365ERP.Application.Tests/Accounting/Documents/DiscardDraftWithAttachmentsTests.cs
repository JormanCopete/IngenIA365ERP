using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Documents;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Documents;

/// <summary>
/// Feature 011 (US3, FR-005, research R10): descartar un borrador que tiene soportes exige confirmar que
/// se borran en el mismo acto. Hasta el 2026-09-23 el borrador se descartaba y los soportes quedaban
/// vivos, colgando de un comprobante que ya no existía.
/// </summary>
public class DiscardDraftWithAttachmentsTests
{
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public IBlobStore Almacen { get; } = Substitute.For<IBlobStore>();

        public DiscardDraftCommandHandler Descartador() => new(D.Db, Almacen, D.Clock, D.User);

        public AccountingDocument Borrador(DocumentKind clase = DocumentKind.Regular)
        {
            var doc = new AccountingDocument
            {
                VoucherTypeId = 1, Date = ContabilidadTestData.Marzo15, Description = "Compra de papelería",
                OriginModule = "Accounting", RegisteredBy = "auxiliar@demo", Status = DocumentStatus.Draft, Kind = clase,
            };
            D.Db.AccountingDocuments.Add(doc);
            D.Db.SaveChanges();
            return doc;
        }

        public Attachment Soporte(AccountingDocument doc, string nombre)
        {
            var a = new Attachment
            {
                TenantId = 1, OwnerEntityType = AdjuntosDeModulo.Comprobante, OwnerEntityPublicId = doc.PublicId,
                FileName = nombre, ContentType = "application/pdf", SizeBytes = 3, Sha256Hex = "abc",
                StoragePath = $"1/2026/09/{nombre}.bin", EncryptedDek = "dek", CreatedBy = "auxiliar@demo",
            };
            D.Db.Attachments.Add(a);
            D.Db.SaveChanges();
            return a;
        }
    }

    [Fact]
    public async Task Sin_confirmar_no_descarta_nada_y_dice_cuantos_soportes_tiene()
    {
        var e = new Escenario();
        var borrador = e.Borrador();
        e.Soporte(borrador, "factura");
        e.Soporte(borrador, "remision");

        var r = await e.Descartador().Handle(new DiscardDraftCommand(borrador.PublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Document.HasAttachments");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { count = 2 });
        r.Error.Message.Should().Contain("2 soportes").And.Contain("90 días");
        (await e.D.Db.AccountingDocuments.SingleAsync()).IsDeleted.Should().BeFalse();
        (await e.D.Db.Attachments.CountAsync(a => !a.IsDeleted)).Should().Be(2);
        await e.Almacen.DidNotReceive().DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirmando_retira_los_objetos_primero_y_da_de_baja_soportes_y_borrador_juntos()
    {
        var e = new Escenario();
        var borrador = e.Borrador();
        e.Soporte(borrador, "factura");
        e.Soporte(borrador, "remision");
        var otroBorrador = e.Borrador();
        var ajeno = e.Soporte(otroBorrador, "ajeno");
        var bajasAlRetirar = new List<int>();
        e.Almacen.DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                bajasAlRetirar.Add(e.D.Db.Attachments.IgnoreQueryFilters().AsNoTracking().Count(a => a.IsDeleted));
                return Task.CompletedTask;
            });

        var r = await e.Descartador().Handle(new DiscardDraftCommand(borrador.PublicId, DeleteAttachments: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        bajasAlRetirar.Should().Equal([0, 0], "ninguna fila se da de baja antes de que su objeto esté en la papelera");
        await e.Almacen.Received(1).DeleteAsync(Arg.Is<BlobReference>(b => b.Uri == "1/2026/09/factura.bin"), Arg.Any<CancellationToken>());
        await e.Almacen.Received(1).DeleteAsync(Arg.Is<BlobReference>(b => b.Uri == "1/2026/09/remision.bin"), Arg.Any<CancellationToken>());
        var filas = await e.D.Db.Attachments.IgnoreQueryFilters().ToListAsync();
        filas.Where(a => a.OwnerEntityPublicId == borrador.PublicId).Should().OnlyContain(a => a.IsDeleted && a.DeletedBy != null);
        filas.Single(a => a.Id == ajeno.Id).IsDeleted.Should().BeFalse("los soportes de otro borrador no se tocan");
        (await e.D.Db.AccountingDocuments.SingleAsync(d => d.Id == borrador.Id)).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Vale_igual_para_un_borrador_de_apertura()
    {
        var e = new Escenario();
        var apertura = e.Borrador(DocumentKind.Opening);
        e.Soporte(apertura, "balance-solido");

        var sinConfirmar = await e.Descartador().Handle(new DiscardDraftCommand(apertura.PublicId), CancellationToken.None);
        var confirmado = await e.Descartador().Handle(new DiscardDraftCommand(apertura.PublicId, DeleteAttachments: true), CancellationToken.None);

        sinConfirmar.Error.Code.Should().Be("Accounting.Document.HasAttachments");
        confirmado.IsSuccess.Should().BeTrue(confirmado.Error?.Message);
        (await e.D.Db.Attachments.CountAsync(a => !a.IsDeleted)).Should().Be(0);
    }

    [Fact]
    public async Task Sin_soportes_se_descarta_como_siempre_y_el_almacen_no_se_entera()
    {
        var e = new Escenario();
        var borrador = e.Borrador();

        var r = await e.Descartador().Handle(new DiscardDraftCommand(borrador.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        await e.Almacen.DidNotReceive().DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
    }
}
