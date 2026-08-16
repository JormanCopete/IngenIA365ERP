using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Promociones;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Promociones;

public class PromocionesTests
{
    private static readonly DateTime Ahora = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);

    private static IDateTimeService Reloj()
    {
        var reloj = Substitute.For<IDateTimeService>();
        reloj.UtcNow.Returns(Ahora);
        return reloj;
    }

    private static ICurrentCentralUserContext Contexto(
        bool autenticado = true, bool maestroGlobal = true)
    {
        var ctx = Substitute.For<ICurrentCentralUserContext>();
        ctx.CentralUserId.Returns(autenticado ? Guid.NewGuid() : null);
        ctx.IsAuthenticated.Returns(autenticado);
        ctx.Purpose.Returns(CentralJwtPurposes.Full);
        ctx.IsGlobalMasterAdmin.Returns(maestroGlobal);
        ctx.Email.Returns("admin@ingenia365.co");
        return ctx;
    }

    private static PromoContenido Pieza(
        string titulo, bool publicado = true,
        DateTime? desde = null, DateTime? hasta = null, int orden = 0) =>
        new()
        {
            Titulo = titulo,
            Publicado = publicado,
            VigenteDesde = desde,
            VigenteHasta = hasta,
            Orden = orden,
        };

    // ------------------------------------------------------------ publicas --

    [Fact]
    public async Task Publicas_NoDevuelveBorradoresNiCaducadas()
    {
        // Este endpoint es anonimo: lo que salga de aca lo ve cualquiera.
        await using var db = TestAdminDbContext.Create();
        db.PromoContenidos.AddRange(
            Pieza("Visible", orden: 1),
            Pieza("Borrador", publicado: false, orden: 2),
            Pieza("Todavia no empieza", desde: Ahora.AddDays(1), orden: 3),
            Pieza("Ya termino", hasta: Ahora.AddDays(-1), orden: 4));
        await db.SaveChangesAsync();

        var r = await new ListarPromocionesPublicasQueryHandler(db, Reloj())
            .Handle(new ListarPromocionesPublicasQuery(), CancellationToken.None);

        r.Value!.Select(p => p.Titulo).Should().BeEquivalentTo(["Visible"]);
    }

    [Fact]
    public async Task Publicas_RespetaElOrdenConfigurado()
    {
        await using var db = TestAdminDbContext.Create();
        db.PromoContenidos.AddRange(
            Pieza("Tercera", orden: 3), Pieza("Primera", orden: 1), Pieza("Segunda", orden: 2));
        await db.SaveChangesAsync();

        var r = await new ListarPromocionesPublicasQueryHandler(db, Reloj())
            .Handle(new ListarPromocionesPublicasQuery(), CancellationToken.None);

        r.Value!.Select(p => p.Titulo).Should().ContainInOrder("Primera", "Segunda", "Tercera");
    }

    [Fact]
    public async Task Publicas_NoDevuelveMasDeDiez()
    {
        // Sin tope, una carga descuidada se sirve entera a cualquier visitante.
        await using var db = TestAdminDbContext.Create();
        for (var i = 0; i < 25; i++) db.PromoContenidos.Add(Pieza($"Pieza {i}", orden: i));
        await db.SaveChangesAsync();

        var r = await new ListarPromocionesPublicasQueryHandler(db, Reloj())
            .Handle(new ListarPromocionesPublicasQuery(), CancellationToken.None);

        r.Value!.Should().HaveCount(10);
    }

    [Fact]
    public async Task Imagen_DeUnaPiezaDespublicada_NoSeSirve()
    {
        // Conocer el PublicId no debe bastar para seguir descargando la imagen
        // de algo que ya no esta publicado.
        await using var db = TestAdminDbContext.Create();
        var pieza = Pieza("Retirada", publicado: false);
        pieza.Imagen = [1, 2, 3];
        pieza.ImagenTipoMime = "image/png";
        db.PromoContenidos.Add(pieza);
        await db.SaveChangesAsync();

        var r = await new ObtenerImagenPromoQueryHandler(db, Reloj())
            .Handle(new ObtenerImagenPromoQuery(pieza.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
    }

    // --------------------------------------------------------------- admin --

    [Fact]
    public async Task Guardar_SinSerMaestroGlobal_EsRechazado()
    {
        // Es contenido de la empresa dueña del producto, no de una cooperativa:
        // un administrador de tenant no debe poder publicar en el login de todos.
        await using var db = TestAdminDbContext.Create();
        var handler = new GuardarPromoCommandHandler(db, Contexto(maestroGlobal: false));

        var r = await handler.Handle(NuevaOrden("Publicidad ajena"), CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
        r.Error!.Code.Should().Be("Promociones.Forbidden");
        db.PromoContenidos.Should().BeEmpty();
    }

    [Fact]
    public async Task Guardar_SinAutenticar_EsRechazado()
    {
        await using var db = TestAdminDbContext.Create();
        var handler = new GuardarPromoCommandHandler(db, Contexto(autenticado: false));

        var r = await handler.Handle(NuevaOrden("Anonima"), CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
        db.PromoContenidos.Should().BeEmpty();
    }

    [Fact]
    public async Task Guardar_CreaYLuegoActualizaLaMismaPieza()
    {
        await using var db = TestAdminDbContext.Create();
        var handler = new GuardarPromoCommandHandler(db, Contexto());

        var creada = await handler.Handle(NuevaOrden("Original"), CancellationToken.None);
        creada.IsSuccess.Should().BeTrue();

        var r = await handler.Handle(
            NuevaOrden("Corregida") with { PublicId = creada.Value }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        db.PromoContenidos.Should().HaveCount(1);
        db.PromoContenidos.Single().Titulo.Should().Be("Corregida");
    }

    [Fact]
    public async Task Guardar_ImagenNula_NoBorraLaImagenExistente()
    {
        // Editar el titulo no puede borrar la imagen: nulo significa "no tocar".
        await using var db = TestAdminDbContext.Create();
        var handler = new GuardarPromoCommandHandler(db, Contexto());

        var creada = await handler.Handle(
            NuevaOrden("Con imagen") with
            {
                ImagenBase64 = Convert.ToBase64String([9, 9, 9]),
                ImagenTipoMime = "image/png",
                ImagenTextoAlternativo = "Un grafico",
            }, CancellationToken.None);

        await handler.Handle(
            NuevaOrden("Titulo nuevo") with { PublicId = creada.Value }, CancellationToken.None);

        db.PromoContenidos.Single().Imagen.Should().NotBeNull();
    }

    [Fact]
    public async Task Guardar_ImagenVacia_SiLaBorra()
    {
        await using var db = TestAdminDbContext.Create();
        var handler = new GuardarPromoCommandHandler(db, Contexto());

        var creada = await handler.Handle(
            NuevaOrden("Con imagen") with
            {
                ImagenBase64 = Convert.ToBase64String([9, 9, 9]),
                ImagenTipoMime = "image/png",
                ImagenTextoAlternativo = "Un grafico",
            }, CancellationToken.None);

        await handler.Handle(
            NuevaOrden("Sin imagen") with { PublicId = creada.Value, ImagenBase64 = "" },
            CancellationToken.None);

        db.PromoContenidos.Single().Imagen.Should().BeNull();
    }

    [Fact]
    public async Task Guardar_ImagenQueSuperaElTope_EsRechazada()
    {
        await using var db = TestAdminDbContext.Create();
        var handler = new GuardarPromoCommandHandler(db, Contexto());
        var demasiado = Convert.ToBase64String(new byte[PromoContenido.MaximoBytesImagen + 1]);

        var r = await handler.Handle(
            NuevaOrden("Pesada") with
            {
                ImagenBase64 = demasiado,
                ImagenTipoMime = "image/png",
                ImagenTextoAlternativo = "x",
            }, CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
        r.Error!.Code.Should().Be("Validation.ImagenMuyGrande");
    }

    [Fact]
    public async Task Eliminar_EsBorradoLogico_YDejaDeVerseEnElLogin()
    {
        await using var db = TestAdminDbContext.Create();
        var pieza = Pieza("A retirar");
        db.PromoContenidos.Add(pieza);
        await db.SaveChangesAsync();

        var r = await new EliminarPromoCommandHandler(db, Contexto())
            .Handle(new EliminarPromoCommand(pieza.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        pieza.IsDeleted.Should().BeTrue("el principio VII exige borrado logico");
        pieza.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Eliminar_SinSerMaestroGlobal_EsRechazado()
    {
        await using var db = TestAdminDbContext.Create();
        var pieza = Pieza("Intocable");
        db.PromoContenidos.Add(pieza);
        await db.SaveChangesAsync();

        var r = await new EliminarPromoCommandHandler(db, Contexto(maestroGlobal: false))
            .Handle(new EliminarPromoCommand(pieza.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
        pieza.IsDeleted.Should().BeFalse();
    }

    // ----------------------------------------------------------- validador --

    [Theory]
    [InlineData("https://ingenia365.co", true)]
    [InlineData("/promociones/detalle", true)]
    [InlineData("", true)]
    [InlineData("http://sitio-inseguro.example", false)]
    [InlineData("//sitio-externo.example", false)]
    [InlineData("javascript:alert(1)", false)]
    public void Validador_ControlaElDestinoDelEnlace(string enlace, bool esperado)
    {
        var v = new GuardarPromoCommandValidator();
        var r = v.Validate(NuevaOrden("t") with { Enlace = enlace });
        r.IsValid.Should().Be(esperado);
    }

    [Fact]
    public void Validador_ImagenSinDescripcion_EsRechazada()
    {
        // En la pantalla de entrada no hay forma de saltar una imagen sin
        // describir cuando se navega con lector de pantalla.
        var v = new GuardarPromoCommandValidator();
        var r = v.Validate(NuevaOrden("t") with
        {
            ImagenBase64 = Convert.ToBase64String([1, 2, 3]),
            ImagenTipoMime = "image/png",
            ImagenTextoAlternativo = null,
        });

        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validador_SvgNoSeAdmite()
    {
        // Un SVG puede llevar script y se serviria desde el propio dominio.
        var v = new GuardarPromoCommandValidator();
        var r = v.Validate(NuevaOrden("t") with
        {
            ImagenBase64 = Convert.ToBase64String([1, 2, 3]),
            ImagenTipoMime = "image/svg+xml",
            ImagenTextoAlternativo = "x",
        });

        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validador_VigenciaAlReves_EsRechazada()
    {
        var v = new GuardarPromoCommandValidator();
        var r = v.Validate(NuevaOrden("t") with
        {
            VigenteDesde = Ahora,
            VigenteHasta = Ahora.AddDays(-1),
        });

        r.IsValid.Should().BeFalse();
    }

    private static GuardarPromoCommand NuevaOrden(string titulo) =>
        new(null, titulo, null, null, null, null, null, null, 1, true, null, null);
}
