using FluentAssertions;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Tests.Lending.Libranza;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, K2, T0815 (FR-108–FR-110, US10-1): la solicitud por libranza con una pagaduría.
/// <list type="bullet">
/// <item>con la forma <c>PaymasterDeduction</c> el convenio vigente es obligatorio (<c>Lending.PayrollAgreement.NotAvailable</c>), y la certificación de
/// la pagaduría con su fecha (<c>Lending.Payroll.CertificationRequired</c>);</item>
/// <item>sin el registro de operador de libranza vigente en el perfil, <c>Lending.PayrollAgreement.OperatorRegistrationMissing</c>;</item>
/// <item>la capacidad de descuento excedida detiene la radicación: 422 <c>Lending.Payroll.DeductionCapacityExceeded</c> con lo disponible; dentro de la
/// capacidad queda la validación <c>DeductionCapacity</c> aprobada;</item>
/// <item>el desembolso crea el vínculo con el convenio, el identificador del empleado, la fecha de la libranza y la certificación.</item>
/// </list>
/// El escenario es el de la libranza (<see cref="LibranzaDePrueba"/>) con la línea <c>CONLIB</c> admitiendo la libranza con pagaduría.
/// </summary>
public sealed class RadicacionPorLibranzaTests : IAsyncLifetime
{
    private LibranzaDePrueba _l = null!;
    private OriginacionDePrueba O => _l.D.O;

    public async Task InitializeAsync()
    {
        _l = await LibranzaDePrueba.CrearAsync(vincular: false);
        var version = await _l.Db.CreditLineVersions.SingleAsync(v => v.CreditLineId == _l.Db.CreditLines.Single(c => c.PublicId == O.Linea.PublicId).Id);
        _l.Db.Entry(version).Property(nameof(CreditLineVersion.RepaymentChannels)).CurrentValue =
            ListaDeEnums.Conjunto([RepaymentChannel.Cashier, RepaymentChannel.OwnPayroll, RepaymentChannel.PaymasterDeduction]);
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
        _l.G.ActuarComo(O.Asesora);
    }

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    private CreditApplicationInput PorLibranza(decimal ingreso = 5_000_000m, Guid? convenio = null, bool conCertificacion = true) => O.Solicitud() with
    {
        PaymentForm = RepaymentChannel.PaymasterDeduction,
        ApplicantCapacity = new CapacityInput(ingreso, 100_000m, 0m),
        Payroll = new PayrollInput(convenio ?? _l.Convenio.PublicId, "EMP-0042", conCertificacion ? Guid.NewGuid() : null,
            conCertificacion ? LibranzaDePrueba.Hoy.AddDays(-3) : null),
    };

    private async Task<IngenIA365ERP.Application.Common.Models.Result<CreditApplicationDto>> BorradorAsync(CreditApplicationInput e)
    {
        _l.G.ActuarComo(O.Asesora);
        var r = await O.Crear().Handle(new CreateCreditApplicationCommand(e) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task Dentro_de_la_capacidad_radica_y_el_desembolso_crea_el_vinculo_de_libranza()
    {
        var radicada = await O.RadicadaAsync(PorLibranza());

        var solicitud = await _l.Db.CreditApplications.AsNoTracking().SingleAsync(s => s.PublicId == radicada.PublicId);
        (solicitud.RepaymentChannel, solicitud.PayrollAgreementId, solicitud.PayrollEmployeeIdentifier)
            .Should().Be((RepaymentChannel.PaymasterDeduction, (int?)_l.Convenio.Id, "EMP-0042"));
        (await _l.Db.CreditApplicationChecks.AsNoTracking().Where(c => c.ApplicationId == solicitud.Id && c.Kind == ApplicationCheckKind.DeductionCapacity)
                .Select(c => c.Outcome).ToListAsync())
            .Should().Contain(CheckOutcome.Passed);

        var credito = await _l.P.C.DesembolsarAsync(PorLibranza());

        var vinculo = await _l.Db.LoanPayrollLinks.AsNoTracking().SingleAsync(v => v.LoanId == credito.Id && v.ValidTo == null);
        (vinculo.RepaymentChannel, vinculo.PayrollAgreementId, vinculo.PaymasterPersonId, vinculo.EmployeeIdAtPaymaster, vinculo.IsOwnPayroll)
            .Should().Be((RepaymentChannel.PaymasterDeduction, (int?)_l.Convenio.Id, (int?)_l.Pagaduria.Id, "EMP-0042", false));
        vinculo.LibranzaDate.Should().NotBeNull();
        vinculo.CertificationAttachmentPublicId.Should().NotBeNull();
        vinculo.CertificationDate.Should().Be(LibranzaDePrueba.Hoy.AddDays(-3));
    }

    [Fact]
    public async Task La_capacidad_excedida_detiene_la_radicacion_con_lo_disponible()
    {
        var borrador = (await BorradorAsync(PorLibranza(ingreso: 200_000m))).Value;
        await O.DocumentosAsync(borrador);

        var r = await O.Radicar().Handle(O.Radicacion(borrador), default);
        _l.Db.ChangeTracker.Clear();

        r.Error.Code.Should().Be(ErroresDeLibranza.CodigoCapacidadExcedida);
        var datos = OriginacionDePrueba.Datos(r.Error);
        datos.GetProperty("available").GetDecimal().Should().BeLessThan(datos.GetProperty("requested").GetDecimal());
        (await _l.Db.CreditApplications.AsNoTracking().SingleAsync(s => s.PublicId == borrador.PublicId)).Status.Should().Be(CreditApplicationStatus.Draft);
    }

    [Fact]
    public async Task Sin_convenio_vigente_o_sin_certificacion_no_se_arma()
    {
        (await BorradorAsync(PorLibranza() with { Payroll = new PayrollInput() })).Error.Code.Should().Be(ErroresDeLibranza.CodigoConvenioNoDisponible);
        (await BorradorAsync(PorLibranza(convenio: Guid.NewGuid()))).Error.Code.Should().Be(ErroresDeLibranza.CodigoConvenioNoDisponible);
        (await BorradorAsync(PorLibranza(conCertificacion: false))).Error.Code.Should().Be(ErroresDeLibranza.CodigoCertificacionRequerida);

        var nomina = await _l.ConvenioAsync("NOMINA", _l.Cooperativa, nominaPropia: true);
        (await BorradorAsync(PorLibranza(convenio: nomina.PublicId))).Error.Code.Should().Be(ErroresDeLibranza.CodigoConvenioNoDisponible,
            "la nómina propia tiene su forma de pago");
        (await BorradorAsync(O.Solicitud() with { Payroll = new PayrollInput(_l.Convenio.PublicId) })).Error.Code
            .Should().Be(ErroresDeLibranza.CodigoConvenioNoDisponible, "un convenio sin la forma de pago libranza no se admite");
    }

    [Fact]
    public async Task Sin_registro_de_operador_de_libranza_no_se_arma()
    {
        _l.ConRegistroDeOperador(false);

        (await BorradorAsync(PorLibranza())).Error.Code.Should().Be(ErroresDeLibranza.CodigoSinRegistroDeOperador);
    }
}
