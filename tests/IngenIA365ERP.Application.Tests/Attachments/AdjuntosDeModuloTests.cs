using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.DeleteAttachment;
using IngenIA365ERP.Application.Attachments.DownloadAttachment;
using IngenIA365ERP.Application.Attachments.ListAttachments;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Attachments;

/// <summary>
/// Revisión de la feature 010 (2026-09-21, D-36): el PDF para firma de la liquidación definitiva vive en
/// <c>COR_Attachments</c> con dueño <c>EmploymentTermination</c>. Lo gobierna el módulo dueño
/// (<see cref="AdjuntosDeModulo"/>): leerlo exige <c>Payroll.Settlements.View</c> y la ruta genérica
/// no lo borra. Antes, cualquier rol con <c>Attachments.Download</c> lo descargaba y el Operador —que
/// no aprueba ni ajusta descuentos— lo borraba con <c>Attachments.Delete</c>.
/// </summary>
public class AdjuntosDeModuloTests
{
    private sealed class Escenario
    {
        public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
        public ICurrentUserService Usuario { get; } = Substitute.For<ICurrentUserService>();
        public IPermissionChecker Permisos { get; } = Substitute.For<IPermissionChecker>();
        public Attachment Definitiva { get; }
        public Attachment DeUnaPersona { get; }

        public Escenario(bool conPermisoDeLiquidaciones)
        {
            Usuario.TenantId.Returns("1");
            Usuario.UserName.Returns("operador@demo");
            Permisos.HasPermissionAsync("Payroll.Settlements.View", Arg.Any<CancellationToken>()).Returns(conPermisoDeLiquidaciones);
            Definitiva = Adjunto("EmploymentTermination", "liquidacion-definitiva-1234-20260915.pdf");
            DeUnaPersona = Adjunto("User", "cedula.pdf");
            Db.Attachments.AddRange(Definitiva, DeUnaPersona);
            Db.SaveChanges();
        }

        private static Attachment Adjunto(string dueño, string nombre) => new()
        {
            TenantId = 1, OwnerEntityType = dueño, OwnerEntityPublicId = Guid.NewGuid(), FileName = nombre,
            ContentType = "application/pdf", SizeBytes = 3, Sha256Hex = "abc", StoragePath = $"1/{nombre}", EncryptedDek = "dek", CreatedBy = "sistema",
        };
    }

    [Fact]
    public async Task El_documento_de_la_definitiva_no_se_borra_por_la_ruta_generica()
    {
        var e = new Escenario(conPermisoDeLiquidaciones: true);
        var h = new DeleteAttachmentCommandHandler(e.Db, e.Usuario, e.Permisos);

        var r = await h.Handle(new DeleteAttachmentCommand(e.Definitiva.PublicId), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(AttachmentErrorCodes.OwnedByModule);
        e.Db.Attachments.IgnoreQueryFilters().Single(a => a.Id == e.Definitiva.Id).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Un_adjunto_que_subio_una_persona_sigue_borrandose_con_Attachments_Delete()
    {
        var e = new Escenario(conPermisoDeLiquidaciones: false);
        var h = new DeleteAttachmentCommandHandler(e.Db, e.Usuario, e.Permisos);

        var r = await h.Handle(new DeleteAttachmentCommand(e.DeUnaPersona.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        await e.Permisos.DidNotReceive().HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_el_permiso_del_modulo_la_descarga_responde_como_si_no_existiera()
    {
        var e = new Escenario(conPermisoDeLiquidaciones: false);
        var store = Substitute.For<IBlobStore>();
        var h = new DownloadAttachmentQueryHandler(e.Db, store, Substitute.For<IAttachmentCipher>(), e.Usuario, e.Permisos);

        var r = await h.Handle(new DownloadAttachmentQuery(e.Definitiva.PublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Generic.NotFound");
        await store.DidNotReceive().GetAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_el_permiso_del_modulo_el_listado_por_dueño_sale_vacio_y_con_el_permiso_lo_trae()
    {
        var sin = new Escenario(conPermisoDeLiquidaciones: false);
        var vacio = await new ListAttachmentsByOwnerQueryHandler(sin.Db, sin.Usuario, sin.Permisos)
            .Handle(new ListAttachmentsByOwnerQuery("EmploymentTermination", sin.Definitiva.OwnerEntityPublicId), CancellationToken.None);
        vacio.IsSuccess.Should().BeTrue();
        vacio.Value.Should().BeEmpty();

        var con = new Escenario(conPermisoDeLiquidaciones: true);
        var lista = await new ListAttachmentsByOwnerQueryHandler(con.Db, con.Usuario, con.Permisos)
            .Handle(new ListAttachmentsByOwnerQuery("EmploymentTermination", con.Definitiva.OwnerEntityPublicId), CancellationToken.None);
        lista.Value.Should().ContainSingle().Which.FileName.Should().StartWith("liquidacion-definitiva-");
    }

    [Fact]
    public void Solo_los_duenos_declarados_tienen_regla()
    {
        AdjuntosDeModulo.De("EmploymentTermination").Should().BeEquivalentTo(new { PermisoDeLectura = "Payroll.Settlements.View", Borrable = false });
        AdjuntosDeModulo.De("User").Should().BeNull();
        AdjuntosDeModulo.De(null).Should().BeNull();
    }
}
