using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Configuration;

/// <summary>
/// Feature 013, US1 (T0361; D20, T44): el dueño <c>CreditLineTaxSupport</c> en <see cref="AdjuntosDeModulo"/>, el soporte del concepto
/// tributario del interés que cita una versión de la línea. Sube quien tiene <c>Lending.CreditLines.Manage</c> sobre una línea que exista
/// (inexistente, borrada o sin permiso: el mismo 404); se lee con <c>Lending.CreditLines.View</c>; nunca se borra
/// (<c>Attachments.OwnerLocked</c>): respalda versiones selladas. (nuevo)
/// </summary>
public class SoporteTributarioDeLineaTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();

    public SoporteTributarioDeLineaTests() => _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

    private CreditLine Linea(bool borrada = false)
    {
        var linea = CreditLine.Nueva("ROTATIVO", "Cupo rotativo", "Compras en los almacenes");
        if (borrada) linea.IsDeleted = true;
        _db.CreditLines.Add(linea);
        _db.SaveChanges();
        return linea;
    }

    [Fact]
    public async Task Con_el_permiso_de_administrar_lineas_se_sube_a_una_linea_que_existe()
    {
        var linea = Linea();

        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.SoporteTributarioDeLinea, linea.PublicId, default)).Should().BeNull();
    }

    [Fact]
    public async Task Sin_permiso_o_con_una_linea_inexistente_o_borrada_es_el_mismo_404()
    {
        var borrada = Linea(borrada: true);
        var viva = Linea();

        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.SoporteTributarioDeLinea, Guid.NewGuid(), default))!.Code
            .Should().Be("Generic.NotFound");
        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.SoporteTributarioDeLinea, borrada.PublicId, default))!.Code
            .Should().Be("Generic.NotFound", "una línea borrada ya no recibe soportes");
        _permisos.HasPermissionAsync("Lending.CreditLines.Manage", Arg.Any<CancellationToken>()).Returns(false);
        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.SoporteTributarioDeLinea, viva.PublicId, default))!.Code
            .Should().Be("Generic.NotFound");
    }

    [Fact]
    public async Task Se_lee_con_el_permiso_de_ver_lineas_y_nunca_se_borra()
    {
        var linea = Linea();

        (await AdjuntosDeModulo.PuedeLeerAsync(_permisos, AdjuntosDeModulo.SoporteTributarioDeLinea, default)).Should().BeTrue();
        _permisos.HasPermissionAsync("Lending.CreditLines.View", Arg.Any<CancellationToken>()).Returns(false);
        (await AdjuntosDeModulo.PuedeLeerAsync(_permisos, AdjuntosDeModulo.SoporteTributarioDeLinea, default)).Should().BeFalse();

        (await AdjuntosDeModulo.PuedeBorrarAsync(_db, AdjuntosDeModulo.SoporteTributarioDeLinea, linea.PublicId, default, _permisos))!.Code
            .Should().Be("Attachments.OwnerLocked");
    }
}
