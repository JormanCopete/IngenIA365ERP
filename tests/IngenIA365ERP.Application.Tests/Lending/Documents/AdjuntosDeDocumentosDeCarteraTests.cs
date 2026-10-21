using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Core;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Documents;

/// <summary>
/// Feature 013, US13, T0780 y T0781 (T81; adaptadores-y-archivos.md §9; pregunta I6 de DT §5, rige la propuesta por defecto; C90, C101): el PDF de un
/// documento emitido (<c>LendingIssuedDocument</c>) es de módulo —se lee con <c>Lending.Documents.Read</c>, no recibe subidas y no se borra—; el
/// logotipo y la imagen de la firma (<c>LendingLogo</c>, <c>LendingSignatureImage</c>) se leen con <c>Lending.Documents.Read</c>, se suben a la
/// configuración de Cartera con <c>Lending.Parameters.Manage</c> (JPEG o PNG) y sólo se borran cuando ya los reemplazó otro.
/// </summary>
public class AdjuntosDeDocumentosDeCarteraTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly HashSet<string> _concedidos = ["Lending.Documents.Read", "Lending.Parameters.Manage"];
    private readonly LendingSetup _setup = new() { Id = LendingSetup.IdDeLaFilaUnica };

    public AdjuntosDeDocumentosDeCarteraTests()
    {
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ci => _concedidos.Contains(ci.ArgAt<string>(0)));
        _db.LendingSetups.Add(_setup);
        _db.SaveChanges();
    }

    private Attachment Imagen(string tipo)
    {
        var a = new Attachment
        {
            TenantId = 1, OwnerEntityType = tipo, OwnerEntityPublicId = _setup.PublicId, FileName = "logo.png", ContentType = "image/png", SizeBytes = 10,
            Sha256Hex = new string('a', 64), StoragePath = $"1/{Guid.NewGuid():N}.bin", Format = FormatoDeAdjunto.Direct, Status = EstadoDeAdjunto.Available,
            CreatedBy = "test",
        };
        _db.Attachments.Add(a);
        _db.SaveChanges();
        return a;
    }

    [Fact]
    public async Task El_pdf_de_un_documento_emitido_es_de_modulo()
    {
        var regla = AdjuntosDeModulo.De("LendingIssuedDocument");

        regla.Should().NotBeNull();
        regla!.PermisoDeLectura.Should().Be("Lending.Documents.Read");
        regla.Borrable.Should().BeFalse();
        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, "LendingIssuedDocument", Guid.NewGuid(), default))!.Code.Should().Be("Attachments.OwnerNotAllowed");
        (await AdjuntosDeModulo.PuedeBorrarAsync(_db, "LendingIssuedDocument", Guid.NewGuid(), default, _permisos))!.Code.Should().Be("Attachments.OwnedByModule");
        _concedidos.Remove("Lending.Documents.Read");
        (await AdjuntosDeModulo.PuedeLeerAsync(_permisos, "LendingIssuedDocument", default)).Should().BeFalse();
    }

    [Theory]
    [InlineData("LendingLogo")]
    [InlineData("LendingSignatureImage")]
    public async Task El_logotipo_y_la_firma_se_suben_a_la_configuracion_con_el_permiso_de_parametros(string tipo)
    {
        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, tipo, _setup.PublicId, default, "image/png")).Should().BeNull();
        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, tipo, _setup.PublicId, default, "application/pdf"))!.Code
            .Should().Be("Validation.Attachments.MimeTypeNotAllowed");
        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, tipo, Guid.NewGuid(), default, "image/png"))!.Code.Should().Be("Generic.NotFound");
        (await AdjuntosDeModulo.PuedeLeerAsync(_permisos, tipo, default)).Should().BeTrue();

        _concedidos.Remove("Lending.Parameters.Manage");
        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, tipo, _setup.PublicId, default, "image/png"))!.Code.Should().Be("Generic.NotFound");
    }

    [Theory]
    [InlineData("LendingLogo")]
    [InlineData("LendingSignatureImage")]
    public async Task Solo_se_borran_cuando_ya_los_reemplazo_otro(string tipo)
    {
        var viejo = Imagen(tipo);

        (await AdjuntosDeModulo.PuedeBorrarAsync(_db, tipo, _setup.PublicId, default, _permisos, viejo.PublicId))!.Code
            .Should().Be("Attachments.OwnerLocked", "la vigente no se borra: se reemplaza");

        var nuevo = Imagen(tipo);

        (await AdjuntosDeModulo.PuedeBorrarAsync(_db, tipo, _setup.PublicId, default, _permisos, viejo.PublicId)).Should().BeNull();
        (await AdjuntosDeModulo.PuedeBorrarAsync(_db, tipo, _setup.PublicId, default, _permisos, nuevo.PublicId))!.Code.Should().Be("Attachments.OwnerLocked");
        (await AdjuntosDeModulo.PuedeBorrarAsync(_db, tipo, _setup.PublicId, default, _permisos))!.Code.Should().Be("Attachments.OwnerLocked",
            "sin decir cuál, la respuesta conservadora");
    }
}
