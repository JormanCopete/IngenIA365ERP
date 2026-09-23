using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.DeleteAttachment;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Attachments;

/// <summary>
/// Feature 011 (US3, research R8): borrar retira primero el objeto —a la papelera del almacén— y después
/// da de baja la fila. Así, si la baja falla, reintentar la completa; en el orden inverso quedaba la fila
/// borrada y el objeto vigente, sin nada que lo nombrara.
/// </summary>
public class DeleteAttachmentCommandHandlerTests
{
    /// <summary>Hace fallar el próximo guardado, como una base que se cae en el peor momento.</summary>
    private sealed class FallaAlGuardar : SaveChangesInterceptor
    {
        public bool Activo { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Activo ? throw new DbUpdateException("La base no respondió.") : base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private sealed class Escenario
    {
        private readonly string _base = Guid.NewGuid().ToString();
        public FallaAlGuardar Falla { get; } = new();
        public IBlobStore Almacen { get; } = Substitute.For<IBlobStore>();
        public ICurrentUserService Usuario { get; } = Substitute.For<ICurrentUserService>();
        public IPermissionChecker Permisos { get; } = Substitute.For<IPermissionChecker>();

        public Escenario()
        {
            Usuario.TenantId.Returns("1");
            Usuario.UserName.Returns("auxiliar@demo");
            Permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        }

        /// <summary>Un contexto nuevo sobre la misma base en memoria: lo que vería la petición siguiente.</summary>
        public TestApplicationDbContext Db() => new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(_base)
            .AddInterceptors(Falla)
            .Options);

        public DeleteAttachmentCommandHandler Handler(TestApplicationDbContext db) =>
            new(db, Almacen, Usuario, Permisos, NullLogger<DeleteAttachmentCommandHandler>.Instance);

        public Attachment Adjunto(string dueno = "User", Guid? del = null)
        {
            using var db = Db();
            var a = new Attachment
            {
                TenantId = 1, OwnerEntityType = dueno, OwnerEntityPublicId = del ?? Guid.NewGuid(), FileName = "factura.pdf",
                ContentType = "application/pdf", SizeBytes = 3, Sha256Hex = "abc", StoragePath = "1/2026/09/abc.bin",
                EncryptedDek = "dek", CreatedBy = "auxiliar@demo",
            };
            db.Attachments.Add(a);
            db.SaveChanges();
            return a;
        }

        public AccountingDocument Comprobante(DocumentStatus estado)
        {
            using var db = Db();
            var doc = new AccountingDocument
            {
                VoucherTypeId = 1, Date = new DateOnly(2026, 9, 1), Description = "Compra", OriginModule = "Accounting",
                RegisteredBy = "auxiliar@demo", Status = estado,
            };
            db.AccountingDocuments.Add(doc);
            db.SaveChanges();
            return doc;
        }

        public bool FilaViva(Guid publicId)
        {
            using var db = Db();
            return db.Attachments.Any(a => a.PublicId == publicId);
        }
    }

    [Fact]
    public async Task Primero_va_el_objeto_a_la_papelera_y_despues_se_da_de_baja_la_fila()
    {
        var e = new Escenario();
        var adjunto = e.Adjunto();
        bool? filaVivaAlRetirarElObjeto = null;
        e.Almacen.DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>())
            .Returns(_ => { filaVivaAlRetirarElObjeto = e.FilaViva(adjunto.PublicId); return Task.CompletedTask; });

        using var db = e.Db();
        var r = await e.Handler(db).Handle(new DeleteAttachmentCommand(adjunto.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        filaVivaAlRetirarElObjeto.Should().BeTrue("la fila se da de baja después de retirar el objeto");
        await e.Almacen.Received(1).DeleteAsync(Arg.Is<BlobReference>(b => b.Uri == "1/2026/09/abc.bin"), Arg.Any<CancellationToken>());
        e.FilaViva(adjunto.PublicId).Should().BeFalse();
    }

    [Fact]
    public async Task Si_falla_la_baja_de_la_fila_el_objeto_ya_esta_en_la_papelera_y_reintentar_completa()
    {
        var e = new Escenario();
        var adjunto = e.Adjunto();

        e.Falla.Activo = true;
        using (var db = e.Db())
        {
            await FluentActions.Invoking(() => e.Handler(db).Handle(new DeleteAttachmentCommand(adjunto.PublicId), CancellationToken.None))
                .Should().ThrowAsync<DbUpdateException>();
        }
        e.Falla.Activo = false;

        e.FilaViva(adjunto.PublicId).Should().BeTrue("la fila sigue viva: nada quedó a medias sin nombre");
        await e.Almacen.Received(1).DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());

        using (var db = e.Db())
        {
            var reintento = await e.Handler(db).Handle(new DeleteAttachmentCommand(adjunto.PublicId), CancellationToken.None);
            reintento.IsSuccess.Should().BeTrue(reintento.Error.Message);
        }
        e.FilaViva(adjunto.PublicId).Should().BeFalse();
        await e.Almacen.Received(2).DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(DocumentStatus.Posted)]
    [InlineData(DocumentStatus.Reversed)]
    public async Task El_soporte_de_un_comprobante_contabilizado_no_se_borra_ni_se_toca_el_almacen(DocumentStatus estado)
    {
        var e = new Escenario();
        var soporte = e.Adjunto(AdjuntosDeModulo.Comprobante, e.Comprobante(estado).PublicId);

        using var db = e.Db();
        var r = await e.Handler(db).Handle(new DeleteAttachmentCommand(soporte.PublicId), CancellationToken.None);

        r.Error.Code.Should().Be(AttachmentErrorCodes.OwnerLocked);
        e.FilaViva(soporte.PublicId).Should().BeTrue();
        await e.Almacen.DidNotReceive().DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_soporte_de_un_borrador_si_se_borra()
    {
        var e = new Escenario();
        var soporte = e.Adjunto(AdjuntosDeModulo.Comprobante, e.Comprobante(DocumentStatus.Draft).PublicId);

        using var db = e.Db();
        var r = await e.Handler(db).Handle(new DeleteAttachmentCommand(soporte.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        e.FilaViva(soporte.PublicId).Should().BeFalse();
    }

    [Fact]
    public async Task Lo_que_genera_un_modulo_sigue_sin_borrarse_y_el_almacen_ni_se_entera()
    {
        var e = new Escenario();
        var pila = e.Adjunto("PilaGeneration");

        using var db = e.Db();
        var r = await e.Handler(db).Handle(new DeleteAttachmentCommand(pila.PublicId), CancellationToken.None);

        r.Error.Code.Should().Be(AttachmentErrorCodes.OwnedByModule);
        await e.Almacen.DidNotReceive().DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Borrar_queda_en_la_auditoria_con_quien_y_que()
    {
        var e = new Escenario();
        var adjunto = e.Adjunto();
        var auditoria = Substitute.For<IAuditService>();
        AuditLogCommand? registrado = null;
        auditoria.LogAsync(Arg.Do<AuditLogCommand>(c => registrado = c), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var comportamiento = new AuditBehavior<DeleteAttachmentCommand, Result>(
            auditoria, e.Usuario, NullLogger<AuditBehavior<DeleteAttachmentCommand, Result>>.Instance);
        var comando = new DeleteAttachmentCommand(adjunto.PublicId);

        using var db = e.Db();
        var r = await comportamiento.Handle(comando, _ => e.Handler(db).Handle(comando, CancellationToken.None), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        registrado.Should().NotBeNull("FR-002: borrar es siempre un acto auditado");
        registrado!.Action.Should().Be("Delete");
        registrado.EntityType.Should().Be("Attachment");
        registrado.NewValues.Should().BeSameAs(comando, "el evento lleva el adjunto que se borró");
    }
}
