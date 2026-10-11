using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0397 (T81, DT §2.24): los dueños de adjuntos de la originación en <see cref="AdjuntosDeModulo"/>. El documento de una solicitud
/// (<c>LoanApplicationDocument</c>) se sube con <c>Lending.Applications.Create</c> mientras la solicitud no esté decidida, se lee con
/// <c>Lending.Applications.Read</c> y, decidida, queda bloqueado (<c>Attachments.OwnerLocked</c>); el informe de centrales
/// (<c>LendingBureauReport</c>) y la simulación enviada (<c>LendingSimulation</c>) son de módulo: no se borran ni reciben subidas.
/// </summary>
public class AdjuntosDeOriginacionTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();

    public AdjuntosDeOriginacionTests() => _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

    private CreditApplication Solicitud(CreditApplicationStatus estado)
    {
        var s = new CreditApplication { Status = estado, RequestedAmount = 1_000_000m, CreatedBy = "test" };
        _db.CreditApplications.Add(s);
        _db.SaveChanges();
        return s;
    }

    [Theory]
    [InlineData(CreditApplicationStatus.Draft)]
    [InlineData(CreditApplicationStatus.Returned)]
    [InlineData(CreditApplicationStatus.PendingDecision)]
    public async Task Se_sube_mientras_la_solicitud_no_este_decidida(CreditApplicationStatus estado)
    {
        var s = Solicitud(estado);

        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.DocumentoDeSolicitud, s.PublicId, default)).Should().BeNull();
        (await AdjuntosDeModulo.PuedeBorrarAsync(_db, AdjuntosDeModulo.DocumentoDeSolicitud, s.PublicId, default, _permisos)).Should().BeNull();
    }

    [Theory]
    [InlineData(CreditApplicationStatus.Approved)]
    [InlineData(CreditApplicationStatus.Denied)]
    [InlineData(CreditApplicationStatus.Cancelled)]
    [InlineData(CreditApplicationStatus.Disbursed)]
    public async Task Decidida_queda_bloqueada(CreditApplicationStatus estado)
    {
        var s = Solicitud(estado);

        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.DocumentoDeSolicitud, s.PublicId, default))!.Code
            .Should().Be("Attachments.OwnerLocked");
        (await AdjuntosDeModulo.PuedeBorrarAsync(_db, AdjuntosDeModulo.DocumentoDeSolicitud, s.PublicId, default, _permisos))!.Code
            .Should().Be("Attachments.OwnerLocked");
    }

    [Fact]
    public async Task Sin_permiso_o_sin_solicitud_es_el_mismo_404_y_se_lee_con_el_permiso_de_leer_solicitudes()
    {
        var s = Solicitud(CreditApplicationStatus.Draft);
        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.DocumentoDeSolicitud, Guid.NewGuid(), default))!.Code
            .Should().Be("Generic.NotFound");
        _permisos.HasPermissionAsync("Lending.Applications.Create", Arg.Any<CancellationToken>()).Returns(false);
        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.DocumentoDeSolicitud, s.PublicId, default))!.Code
            .Should().Be("Generic.NotFound");

        (await AdjuntosDeModulo.PuedeLeerAsync(_permisos, AdjuntosDeModulo.DocumentoDeSolicitud, default)).Should().BeTrue();
        _permisos.HasPermissionAsync("Lending.Applications.Read", Arg.Any<CancellationToken>()).Returns(false);
        (await AdjuntosDeModulo.PuedeLeerAsync(_permisos, AdjuntosDeModulo.DocumentoDeSolicitud, default)).Should().BeFalse();
    }

    [Theory]
    [InlineData(AdjuntosDeModulo.InformeDeCentrales, "Lending.CreditBureau.ReadReports")]
    [InlineData(AdjuntosDeModulo.SimulacionEnviada, "Lending.DebtorMessages.Read")]
    public async Task El_informe_de_centrales_y_la_simulacion_enviada_son_de_modulo(string tipo, string permisoDeLectura)
    {
        var regla = AdjuntosDeModulo.De(tipo)!;
        regla.Borrable.Should().BeFalse();
        regla.PermisoDeLectura.Should().Be(permisoDeLectura);
        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, tipo, Guid.NewGuid(), default))!.Code.Should().Be("Attachments.OwnerNotAllowed");
        (await AdjuntosDeModulo.PuedeBorrarAsync(_db, tipo, Guid.NewGuid(), default, _permisos))!.Code.Should().Be("Attachments.OwnedByModule");
    }
}
