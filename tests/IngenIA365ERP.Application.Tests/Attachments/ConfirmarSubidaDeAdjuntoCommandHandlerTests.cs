using FluentAssertions;
using IngenIA365ERP.Application.Attachments.ConfirmarSubida;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Attachments;

/// <summary>
/// Feature 011 (US1, T036): confirmar revisa lo que llegó al almacén —tamaño, huella y firma del
/// contenido sobre los primeros 8 KiB— y deja el adjunto en su estado final. Es idempotente.
/// </summary>
public class ConfirmarSubidaDeAdjuntoCommandHandlerTests
{
    private static ConfirmarSubidaDeAdjuntoCommandHandler Handler(EscenarioDeAdjuntos e, TestApplicationDbContext db) =>
        new(db, e.Almacen, e.Permisos, e.Usuario, e.Reloj, NullLogger<ConfirmarSubidaDeAdjuntoCommandHandler>.Instance);

    private static async Task<IngenIA365ERP.Application.Common.Models.Result<IngenIA365ERP.Application.Attachments.Common.ConfirmacionDeSubidaDto>> Confirmar(EscenarioDeAdjuntos e, Guid id)
    {
        using var db = e.Base.Db();
        return await Handler(e, db).Handle(new ConfirmarSubidaDeAdjuntoCommand(id), CancellationToken.None);
    }

    [Fact]
    public async Task Lo_que_llego_entero_y_con_la_firma_del_tipo_queda_disponible()
    {
        var e = new EscenarioDeAdjuntos();
        var pdf = EscenarioDeAdjuntos.Pdf();
        var a = e.Subiendo(pdf);
        e.EnElAlmacen(a, pdf);

        var r = await Confirmar(e, a.PublicId);

        r.Value.Status.Should().Be(EstadoDeAdjunto.Available);
        var fila = e.Fila(a.PublicId);
        fila.Status.Should().Be(EstadoDeAdjunto.Available);
        fila.ConfirmedAt.Should().Be(EscenarioDeAdjuntos.Ahora);
        fila.ConfirmedBy.Should().Be("auxiliar@demo");
        await e.Almacen.Received(1).LeerInicioAsync(Arg.Any<BlobReference>(), 8 * 1024, Arg.Any<CancellationToken>());
        await e.Almacen.DidNotReceive().DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_ejecutable_con_nombre_de_pdf_se_rechaza_y_va_a_la_papelera_antes_de_marcar_la_fila()
    {
        var e = new EscenarioDeAdjuntos();
        var exe = new byte[4096];
        "MZ"u8.CopyTo(exe);
        var a = e.Subiendo(exe);
        e.EnElAlmacen(a, exe);
        EstadoDeAdjunto? estadoAlRetirar = null;
        e.Almacen.DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>())
            .Returns(_ => { estadoAlRetirar = e.Fila(a.PublicId).Status; return Task.CompletedTask; });

        var r = await Confirmar(e, a.PublicId);

        r.Value.Status.Should().Be(EstadoDeAdjunto.Rejected);
        r.Value.RejectionReason.Should().Be("Se declaró un PDF, pero el contenido es un ejecutable de Windows.");
        await e.Almacen.Received(1).DeleteAsync(Arg.Is<BlobReference>(b => b.Uri == a.StoragePath), Arg.Any<CancellationToken>());
        estadoAlRetirar.Should().Be(EstadoDeAdjunto.Uploading, "primero el objeto a la papelera, después la fila (R7, R8)");
        e.Fila(a.PublicId).RejectionReason.Should().Be(r.Value.RejectionReason);
    }

    [Fact]
    public async Task Otro_tamano_se_rechaza_sin_leer_el_contenido()
    {
        var e = new EscenarioDeAdjuntos();
        var pdf = EscenarioDeAdjuntos.Pdf(4096);
        var a = e.Subiendo(pdf);
        e.EnElAlmacen(a, EscenarioDeAdjuntos.Pdf(4000));

        var r = await Confirmar(e, a.PublicId);

        r.Value.Status.Should().Be(EstadoDeAdjunto.Rejected);
        r.Value.RejectionReason.Should().Contain("se autorizaron");
        await e.Almacen.DidNotReceive().LeerInicioAsync(Arg.Any<BlobReference>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await e.Almacen.Received(1).DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Otra_huella_se_rechaza_aunque_el_tamano_coincida()
    {
        var e = new EscenarioDeAdjuntos();
        var pdf = EscenarioDeAdjuntos.Pdf();
        var a = e.Subiendo(pdf);
        e.EnElAlmacen(a, pdf, huella: EscenarioDeAdjuntos.Huella(new byte[4096]));

        var r = await Confirmar(e, a.PublicId);

        r.Value.Status.Should().Be(EstadoDeAdjunto.Rejected);
        r.Value.RejectionReason.Should().Be("El contenido no corresponde a la huella autorizada.");
    }

    [Fact]
    public async Task Sin_objeto_y_con_la_autorizacion_vigente_sigue_subiendo_y_no_toca_la_fila()
    {
        var e = new EscenarioDeAdjuntos();
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf());

        var r = await Confirmar(e, a.PublicId);

        r.Value.Status.Should().Be(EstadoDeAdjunto.Uploading, "la subida puede estar en curso");
        e.Fila(a.PublicId).ConfirmedAt.Should().BeNull();
    }

    [Fact]
    public async Task Sin_objeto_y_con_la_autorizacion_vencida_queda_incompleta_y_no_se_borra()
    {
        var e = new EscenarioDeAdjuntos();
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf());
        e.Adelantar(TimeSpan.FromMinutes(6));

        var r = await Confirmar(e, a.PublicId);

        r.Value.Status.Should().Be(EstadoDeAdjunto.Incomplete);
        e.Fila(a.PublicId).IsDeleted.Should().BeFalse("una subida a medias queda para que alguien decida (FR-006)");
        await e.Almacen.DidNotReceive().DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirmar_dos_veces_da_el_mismo_resultado_sin_volver_a_mirar_el_almacen()
    {
        var e = new EscenarioDeAdjuntos();
        var pdf = EscenarioDeAdjuntos.Pdf();
        var a = e.Subiendo(pdf);
        e.EnElAlmacen(a, pdf);

        var primera = await Confirmar(e, a.PublicId);
        var segunda = await Confirmar(e, a.PublicId);

        segunda.Value.Should().Be(primera.Value);
        await e.Almacen.Received(1).ConsultarAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_permiso_de_consultar_comprobantes_responde_como_si_no_existiera()
    {
        var e = new EscenarioDeAdjuntos();
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf());
        e.Permisos.HasPermissionAsync("Accounting.Vouchers.View", Arg.Any<CancellationToken>()).Returns(false);

        var r = await Confirmar(e, a.PublicId);

        r.Error.Code.Should().Be("Generic.NotFound");
    }
}
