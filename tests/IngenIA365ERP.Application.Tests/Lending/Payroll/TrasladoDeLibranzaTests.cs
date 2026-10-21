using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Lending.Payroll.Transfers;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// Feature 013, K2, T0812 (FR-116, US10-5, C44): el traslado de la libranza.
/// <list type="bullet">
/// <item>a otra pagaduría con convenio: cierra el vínculo vigente (<c>ValidTo</c>, <c>EndReason</c>) y abre otro <c>Origin = Transfer</c> con la misma
/// <c>LibranzaDate</c> y <c>PreviousLinkId</c>; no es una operación del crédito;</item>
/// <item>la capacidad se mide contra la certificación nueva (<c>Lending.Payroll.DeductionCapacityExceeded</c> con <c>available</c> y
/// <c>requested</c>); un crédito sin pagaduría responde <c>Lending.Loan.NotPayroll</c>;</item>
/// <item>el retiro sin nuevo empleador abre un vínculo <c>ChannelChange</c> con la forma alterna del convenio y avisa al deudor (<c>PASOACAJA</c>);</item>
/// <item>pasar entre con y sin libranza emite <c>DimensionContableCambiada</c> (<c>ConLibranza</c>/<c>SinLibranza</c>) en la misma transacción.</item>
/// </list>
/// </summary>
public sealed class TrasladoDeLibranzaTests : IAsyncLifetime
{
    private LibranzaDePrueba _l = null!;

    public async Task InitializeAsync() => _l = await LibranzaDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    private async Task<IngenIA365ERP.Application.Common.Models.Result<PayrollTransferDto>> TrasladarAsync(TransferLoanPayrollCommand c)
    {
        _l.G.ActuarComo(_l.D.Cajera);
        var r = await _l.Trasladar().Handle(c with { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    private TransferLoanPayrollCommand ALaGobernacion(Guid convenio, decimal ingreso) => new(_l.Credito.PublicId, "Lo trasladaron a la Gobernación", convenio,
        "GOB-778", Guid.NewGuid(), LibranzaDePrueba.Hoy.AddDays(-2), ingreso);

    private async Task<List<LoanPayrollLink>> VinculosAsync() =>
        await _l.Db.LoanPayrollLinks.AsNoTracking().Where(v => v.LoanId == _l.Credito.Id).OrderBy(v => v.Id).ToListAsync();

    [Fact]
    public async Task Trasladar_a_otra_pagaduria_cierra_el_vinculo_y_abre_otro_con_la_misma_libranza()
    {
        var gobernacion = await _l.ConvenioAsync("GOBVALLE", _l.OtraPagaduria);
        var antes = (await VinculosAsync()).Single();
        var operaciones = await _l.Db.LoanOperations.CountAsync();

        var r = await TrasladarAsync(ALaGobernacion(gobernacion.PublicId, 10_000_000m));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var vinculos = await VinculosAsync();
        vinculos.Should().HaveCount(2);
        var cerrado = vinculos[0];
        var nuevo = vinculos[1];
        cerrado.ValidTo.Should().Be(nuevo.ValidFrom.AddDays(-1));
        cerrado.EndReason.Should().NotBeNullOrWhiteSpace();
        (nuevo.Origin, nuevo.PreviousLinkId, nuevo.LibranzaDate, nuevo.PayrollAgreementId, nuevo.PaymasterPersonId, nuevo.EmployeeIdAtPaymaster)
            .Should().Be((LoanPayrollLinkOrigin.Transfer, (int?)cerrado.Id, antes.LibranzaDate, (int?)gobernacion.Id, (int?)_l.OtraPagaduria.Id, "GOB-778"));
        (nuevo.CertifiedNetIncome, nuevo.CertificationDate).Should().Be(((decimal?)10_000_000m, (DateOnly?)LibranzaDePrueba.Hoy.AddDays(-2)));
        nuevo.CertifiedAvailableCapacity.Should().BeGreaterThan(0m);
        nuevo.ValidFrom.Should().Be(LibranzaDePrueba.Hoy.AddDays(1), "un vínculo abierto hoy se traslada desde mañana");
        r.Value.AgreementCode.Should().Be("GOBVALLE");
        (await _l.Db.LoanOperations.CountAsync()).Should().Be(operaciones, "el traslado no es una operación del crédito");
        (await _l.Db.DebtorMessages.AnyAsync(m => m.OriginKind == TransferLoanPayrollCommandHandler.OrigenDelAviso)).Should().BeFalse();
    }

    [Fact]
    public async Task La_capacidad_se_mide_contra_la_certificacion_nueva()
    {
        var gobernacion = await _l.ConvenioAsync("GOBVALLE", _l.OtraPagaduria);

        var r = await TrasladarAsync(ALaGobernacion(gobernacion.PublicId, 100_000m));

        r.Error.Code.Should().Be(ErroresDeLibranza.CodigoCapacidadExcedida);
        var datos = Disbursements.DesembolsoDePrueba.Datos(r.Error);
        datos.GetProperty("available").GetDecimal().Should().BeLessThan(datos.GetProperty("requested").GetDecimal());
        (await VinculosAsync()).Should().ContainSingle("con un error no se escribe nada");

        var sinCertificacion = await TrasladarAsync(ALaGobernacion(gobernacion.PublicId, 10_000_000m) with { CertificationAttachmentPublicId = null });
        sinCertificacion.Error.Code.Should().Be(ErroresDeLibranza.CodigoCertificacionRequerida);
        (await TrasladarAsync(ALaGobernacion(_l.Convenio.PublicId, 10_000_000m))).Error.Code.Should().Be(ErroresDeLibranza.CodigoConvenioNoDisponible,
            "ya es su convenio");
    }

    [Fact]
    public async Task Un_credito_sin_pagaduria_no_se_traslada()
    {
        using var l = await LibranzaDePrueba.CrearAsync(vincular: false);
        l.G.ActuarComo(l.D.Cajera);

        var r = await l.Trasladar().Handle(new TransferLoanPayrollCommand(l.Credito.PublicId, "Retiro") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeLibranza.CodigoNoEsLibranza);
    }

    [Fact]
    public async Task El_retiro_sin_nuevo_empleador_pasa_a_la_forma_alterna_del_convenio_y_avisa()
    {
        using var l = await LibranzaDePrueba.CrearAsync(conConvenio: false);
        l.Convenio = await l.ConvenioAsync("PAGMUN01", l.Pagaduria, version: l.Version() with { AlternateRepaymentChannel = RepaymentChannel.DirectDebit });
        l.Vincular(l.Credito, l.Convenio, "EMP-0001");
        l.G.ActuarComo(l.D.Cajera);

        var r = await l.Trasladar().Handle(new TransferLoanPayrollCommand(l.Credito.PublicId, "Se retiró de la Secretaría") { OperationKey = Guid.NewGuid() },
            default);
        l.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var nuevo = await l.Db.LoanPayrollLinks.AsNoTracking().SingleAsync(v => v.LoanId == l.Credito.Id && v.ValidTo == null);
        (nuevo.Origin, nuevo.RepaymentChannel, nuevo.PayrollAgreementId).Should().Be((LoanPayrollLinkOrigin.ChannelChange, RepaymentChannel.DirectDebit, (int?)null));
        var aviso = await l.Db.DebtorMessages.AsNoTracking().SingleAsync(m => m.OriginKind == TransferLoanPayrollCommandHandler.OrigenDelAviso);
        (aviso.Purpose, aviso.LoanId, aviso.OriginPublicId).Should().Be((DebtorMessagePurpose.Informative, (int?)l.Credito.Id, (Guid?)nuevo.PublicId));
        System.Net.WebUtility.HtmlDecode(aviso.RenderedBody).Should().Contain("débito automático");
    }

    /// <summary>Las condiciones del crédito con el sub-segmento de consumo con libranza (el escenario desembolsa con el de sin libranza).</summary>
    private static async Task ConLibranzaAsync(LibranzaDePrueba l)
    {
        var terminos = await l.Db.LoanTermsVersions.SingleAsync(t => t.LoanId == l.Credito.Id);
        l.Db.Entry(terminos).Property(nameof(LoanTermsVersion.SubsegmentCode)).CurrentValue = EmisionDeCartera.SubsegmentoConLibranza;
        await l.Db.SaveChangesAsync();
        l.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Pasar_de_con_a_sin_libranza_emite_DimensionContableCambiada()
    {
        await ConLibranzaAsync(_l);

        var r = await TrasladarAsync(new TransferLoanPayrollCommand(_l.Credito.PublicId, "Se retiró de la Secretaría"));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        (r.Value.SubsegmentChanged, r.Value.Subsegment).Should().Be((true, EmisionDeCartera.SubsegmentoSinLibranza));
        var mensaje = await _l.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == DimensionContableCambiadaV1.Type);
        mensaje.PayloadJson.Should().Contain(EmisionDeCartera.SubsegmentoConLibranza).And.Contain(EmisionDeCartera.SubsegmentoSinLibranza);

        // De una pagaduría a otra sigue con libranza: el traslado no toca la dimensión.
        using var otro = await LibranzaDePrueba.CrearAsync();
        await ConLibranzaAsync(otro);
        var g2 = await otro.ConvenioAsync("GOBVALLE", otro.OtraPagaduria);
        otro.G.ActuarComo(otro.D.Cajera);
        var sin = await otro.Trasladar().Handle(new TransferLoanPayrollCommand(otro.Credito.PublicId, "Traslado", g2.PublicId, "GOB-1", Guid.NewGuid(),
            LibranzaDePrueba.Hoy, 10_000_000m) { OperationKey = Guid.NewGuid() }, default);
        sin.Value.SubsegmentChanged.Should().BeFalse();
        (await otro.Db.IntegrationMessages.AnyAsync(m => m.Type == DimensionContableCambiadaV1.Type)).Should().BeFalse();
    }
}
