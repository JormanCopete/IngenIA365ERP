using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.DeleteAttachment;
using IngenIA365ERP.Application.Attachments.DownloadAttachment;
using IngenIA365ERP.Application.Attachments.ListAttachments;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
        var h = new DeleteAttachmentCommandHandler(e.Db, Substitute.For<IBlobStore>(), e.Usuario, e.Permisos, NullLogger<DeleteAttachmentCommandHandler>.Instance);

        var r = await h.Handle(new DeleteAttachmentCommand(e.Definitiva.PublicId), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(AttachmentErrorCodes.OwnedByModule);
        e.Db.Attachments.IgnoreQueryFilters().Single(a => a.Id == e.Definitiva.Id).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Un_adjunto_que_subio_una_persona_sigue_borrandose_con_Attachments_Delete()
    {
        var e = new Escenario(conPermisoDeLiquidaciones: false);
        var h = new DeleteAttachmentCommandHandler(e.Db, Substitute.For<IBlobStore>(), e.Usuario, e.Permisos, NullLogger<DeleteAttachmentCommandHandler>.Instance);

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

    // ---------------------------------------------------------------------------------------------
    // Feature 011 (R9): soportes de comprobantes. Hasta el 2026-09-23 no tenían regla: se leían sin
    // permiso de contabilidad, se podía subir a cualquier dueño y el soporte de un comprobante
    // contabilizado se borraba por la API.
    // ---------------------------------------------------------------------------------------------

    private sealed class Contable
    {
        public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
        public ICurrentUserService Usuario { get; } = Substitute.For<ICurrentUserService>();
        public IPermissionChecker Permisos { get; } = Substitute.For<IPermissionChecker>();

        public Contable(bool puedeVer = true, bool puedeEscribir = true)
        {
            Usuario.TenantId.Returns("1");
            Usuario.UserName.Returns("auxiliar@demo");
            Permisos.HasPermissionAsync("Accounting.Vouchers.View", Arg.Any<CancellationToken>()).Returns(puedeVer);
            Permisos.HasPermissionAsync("Accounting.Vouchers.Create", Arg.Any<CancellationToken>()).Returns(puedeEscribir);
        }

        public AccountingDocument Comprobante(DocumentStatus estado)
        {
            var doc = new AccountingDocument
            {
                VoucherTypeId = 1, Date = new DateOnly(2026, 9, 1), Description = "Compra de papelería",
                OriginModule = "Accounting", RegisteredBy = "auxiliar@demo", Status = estado,
            };
            Db.AccountingDocuments.Add(doc);
            Db.SaveChanges();
            return doc;
        }

        public Attachment Soporte(AccountingDocument doc)
        {
            var a = new Attachment
            {
                TenantId = 1, OwnerEntityType = AdjuntosDeModulo.Comprobante, OwnerEntityPublicId = doc.PublicId,
                FileName = "factura.pdf", ContentType = "application/pdf", SizeBytes = 3, Sha256Hex = "abc",
                StoragePath = "1/factura.pdf", EncryptedDek = "dek", CreatedBy = "auxiliar@demo",
            };
            Db.Attachments.Add(a);
            Db.SaveChanges();
            return a;
        }
    }

    [Fact]
    public async Task Sin_permiso_de_consultar_comprobantes_sus_soportes_no_se_ven_ni_se_bajan()
    {
        var e = new Contable(puedeVer: false);
        var soporte = e.Soporte(e.Comprobante(DocumentStatus.Draft));
        var store = Substitute.For<IBlobStore>();

        var lista = await new ListAttachmentsByOwnerQueryHandler(e.Db, e.Usuario, e.Permisos)
            .Handle(new ListAttachmentsByOwnerQuery(AdjuntosDeModulo.Comprobante, soporte.OwnerEntityPublicId), CancellationToken.None);
        var descarga = await new DownloadAttachmentQueryHandler(e.Db, store, Substitute.For<IAttachmentCipher>(), e.Usuario, e.Permisos)
            .Handle(new DownloadAttachmentQuery(soporte.PublicId), CancellationToken.None);

        lista.Value.Should().BeEmpty("FR-025: sin Accounting.Vouchers.View no hay soportes");
        descarga.Error.Code.Should().Be("Generic.NotFound", "la misma respuesta que un adjunto inexistente");
        await store.DidNotReceive().GetAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("PilaGeneration")]
    [InlineData("BankDisbursementFile")]
    [InlineData("EmploymentTermination")]
    public async Task Nadie_sube_archivos_a_lo_que_genera_un_modulo(string tipo)
    {
        var e = new Contable();

        var error = await AdjuntosDeModulo.PuedeSubirAsync(e.Db, e.Permisos, tipo, Guid.NewGuid(), CancellationToken.None);

        error!.Code.Should().Be(AttachmentErrorCodes.OwnerNotAllowed);
    }

    [Fact]
    public async Task Un_tipo_de_dueno_sin_pantalla_todavia_no_admite_subidas()
    {
        var e = new Contable();

        var error = await AdjuntosDeModulo.PuedeSubirAsync(e.Db, e.Permisos, "User", Guid.NewGuid(), CancellationToken.None);

        error!.Code.Should().Be(AttachmentErrorCodes.OwnerNotAllowed);
    }

    [Fact]
    public async Task Subir_a_un_comprobante_inexistente_o_sin_permiso_de_escribir_responde_igual_que_si_no_existiera()
    {
        var conPermiso = new Contable();
        var inexistente = await AdjuntosDeModulo.PuedeSubirAsync(
            conPermiso.Db, conPermiso.Permisos, AdjuntosDeModulo.Comprobante, Guid.NewGuid(), CancellationToken.None);

        var sinPermiso = new Contable(puedeEscribir: false);
        var doc = sinPermiso.Comprobante(DocumentStatus.Draft);
        var sinEscribir = await AdjuntosDeModulo.PuedeSubirAsync(
            sinPermiso.Db, sinPermiso.Permisos, AdjuntosDeModulo.Comprobante, doc.PublicId, CancellationToken.None);

        inexistente!.Code.Should().Be("Generic.NotFound", "con una base por cooperativa, el de otra tampoco existe aquí");
        sinEscribir!.Code.Should().Be("Generic.NotFound");
        sinEscribir.Message.Should().Be(inexistente.Message, "la respuesta no revela qué comprobantes existen");
    }

    [Theory]
    [InlineData(DocumentStatus.Draft)]
    [InlineData(DocumentStatus.Posted)]
    public async Task Se_sube_soporte_a_un_borrador_y_tambien_a_uno_contabilizado(DocumentStatus estado)
    {
        var e = new Contable();
        var doc = e.Comprobante(estado);

        var error = await AdjuntosDeModulo.PuedeSubirAsync(e.Db, e.Permisos, AdjuntosDeModulo.Comprobante, doc.PublicId, CancellationToken.None);

        error.Should().BeNull("el soporte suele llegar después de contabilizar, y agregarlo no altera el comprobante");
    }

    [Theory]
    [InlineData(DocumentStatus.Posted)]
    [InlineData(DocumentStatus.Reversed)]
    public async Task El_soporte_de_un_comprobante_contabilizado_no_se_borra(DocumentStatus estado)
    {
        var e = new Contable();
        var doc = e.Comprobante(estado);

        var error = await AdjuntosDeModulo.PuedeBorrarAsync(e.Db, AdjuntosDeModulo.Comprobante, doc.PublicId, CancellationToken.None);

        error!.Code.Should().Be(AttachmentErrorCodes.OwnerLocked);
        error.Should().BeOfType<IngenIA365ERP.Application.Common.Models.ErrorConDatos>();
    }

    [Fact]
    public async Task El_soporte_de_un_borrador_se_borra_y_el_de_un_comprobante_que_ya_no_existe_tambien()
    {
        var e = new Contable();
        var borrador = e.Comprobante(DocumentStatus.Draft);

        (await AdjuntosDeModulo.PuedeBorrarAsync(e.Db, AdjuntosDeModulo.Comprobante, borrador.PublicId, CancellationToken.None))
            .Should().BeNull();
        (await AdjuntosDeModulo.PuedeBorrarAsync(e.Db, AdjuntosDeModulo.Comprobante, Guid.NewGuid(), CancellationToken.None))
            .Should().BeNull("un borrador descartado antes de esta regla no tiene nada que proteger");
    }

    [Theory]
    [InlineData(DocumentStatus.Draft, true, true)]
    [InlineData(DocumentStatus.Posted, true, false)]
    [InlineData(DocumentStatus.Reversed, true, false)]
    [InlineData(DocumentStatus.Draft, false, false)]
    public async Task La_lista_dice_si_cada_soporte_se_puede_borrar(DocumentStatus estado, bool conPermisoDeBorrar, bool esperado)
    {
        var e = new Contable();
        e.Permisos.HasPermissionAsync(AdjuntosDeModulo.PermisoDeBorrar, Arg.Any<CancellationToken>()).Returns(conPermisoDeBorrar);
        var doc = e.Comprobante(estado);
        e.Soporte(doc);

        var lista = await new ListAttachmentsByOwnerQueryHandler(e.Db, e.Usuario, e.Permisos)
            .Handle(new ListAttachmentsByOwnerQuery(AdjuntosDeModulo.Comprobante, doc.PublicId), CancellationToken.None);

        lista.Value.Should().ContainSingle().Which.CanDelete.Should().Be(esperado,
            "la pantalla muestra Borrar según lo que decide el servidor (FR-004)");
    }

    [Fact]
    public async Task Lo_que_genera_un_modulo_sigue_sin_borrarse()
    {
        var e = new Contable();

        var error = await AdjuntosDeModulo.PuedeBorrarAsync(e.Db, "PilaGeneration", Guid.NewGuid(), CancellationToken.None);

        error!.Code.Should().Be(AttachmentErrorCodes.OwnedByModule);
    }
}
