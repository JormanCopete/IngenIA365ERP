using FluentAssertions;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Channel.Support;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Receipts.BankItems;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Channel;

/// <summary>
/// Feature 021, US12 (T121; FR-058 a FR-060): la atención junta lo del canal (cuenta, intentos con su estado en la pasarela) con lo que hizo el ERP
/// (aviso, recibo, reverso) y dice qué se puede reversar; sin canal muestra igual lo del ERP. Las acciones van al canal y quedan auditadas; los
/// indicadores suman las cifras del ERP. Que una cooperativa no vea lo de otra lo prueba el canal (Go, T123).
/// </summary>
public sealed class AtencionDelCanalTests
{
    private const string Ref = "ABC2612010000000S1";

    private readonly BancosDePrueba _b = new();
    private readonly ICanalSatelite _canal = Substitute.For<ICanalSatelite>();
    private readonly Application.Common.Interfaces.ICurrentTenantService _cooperativa = Substitute.For<Application.Common.Interfaces.ICurrentTenantService>();
    private readonly Guid _coop = Guid.NewGuid();
    private readonly ChannelPaymentNoticeResultDto _aviso;

    public AtencionDelCanalTests()
    {
        var e = _b.K.C.E;
        e.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "RL", Name = "Recibo en línea", Kind = ReceiptDocumentKind.OnlineReceipt });
        var convenio = new CollectionAgreement
        {
            Code = "AVAL", Name = "Pasarela", Kind = CollectionAgreementKind.Gateway, OwnBankAccountPublicId = _b.Cuenta.PublicId, BranchId = e.OficinaA.Id,
        };
        e.Db.CollectionAgreements.Add(convenio);
        e.Db.SaveChanges();
        var destino = new ChannelPaymentDestination { Name = "Avalpay", CollectionAgreementId = convenio.Id };
        e.Db.ChannelPaymentDestinations.Add(destino);
        e.Db.SaveChanges();
        e.Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.AsociadoPorCanal(_b.K.Asociado.PublicId, "canal", "190.0.0.1"));
        _aviso = new RegisterChannelPaymentCommandHandler(e.Db, new AplicacionDelCanal(e.Db, _b.Aplicacion, _b.K.Obligaciones, _b.K.Cuentas, e.Alertas, e.Reloj))
            .Handle(new RegisterChannelPaymentCommand(_b.K.Asociado.PublicId, Ref, destino.PublicId, 332_500m, "COP",
                new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.FromHours(-5)),
                new AprobacionDeLaPasarelaDto(733112, 1598634446, "3324451", "1597317690", "_PSE_", "PSE", "Banco de pruebas"),
                [new ItemDelAvisoDto(ClaveDeConcepto.DeCredito(_b.K.CreditoA), "Due", 332_500m, null)], null)
            { OperationKey = RegisterChannelPaymentCommand.ClaveDeOperacion(Ref) }, default).GetAwaiter().GetResult().Value;
        e.Como(e.Cajera);
        _cooperativa.TenantId.Returns(_coop.ToString());
        _canal.Configurado.Returns(true);
    }

    private AuditoriaDelCanal Auditoria => new(_b.K.C.E.Servicios, NullLogger<AuditoriaDelCanal>.Instance);

    private SearchChannelSupportQueryHandler Buscar() => new(_b.K.C.E.Db, _canal, _cooperativa, Auditoria);

    private static IntentoDelCanalDto Intento(string referencia, string estado = "approved", decimal total = 332_500m) =>
        new(referencia, Guid.NewGuid(), estado, total, DateTime.UtcNow, DateTime.UtcNow, "3324451", "PSE", "applied", null);

    [Fact]
    public async Task Por_referencia_junta_el_intento_del_canal_con_el_recibo_del_ERP_y_permite_reversar()
    {
        _canal.BuscarIntentosAsync(_coop, null, null, Ref, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<IntentoDelCanalDto>>([Intento(Ref)]));

        var r = await Buscar().Handle(new SearchChannelSupportQuery(null, null, Ref.ToLowerInvariant()), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.ChannelAvailable.Should().BeTrue();
        var fila = r.Value.Attempts.Should().ContainSingle().Subject;
        fila.NoticeOutcome.Should().Be("Applied");
        fila.ReceiptNumber.Should().Be(_aviso.ReceiptNumber);
        fila.ReceiptStatus.Should().Be(nameof(ReceiptState.Valid));
        fila.CanReverse.Should().BeTrue();
        await _canal.DidNotReceive().BuscarCuentaAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Por_documento_trae_la_cuenta_y_los_intentos_sin_aviso_no_se_reversan()
    {
        var cuenta = new CuentaDelCanalDto(Guid.NewGuid(), "active", DateTime.UtcNow, DateTime.UtcNow, null, "a•••@e•••.test", "•••••• 4567", [], 1, "enabled");
        _canal.BuscarCuentaAsync(_coop, "CC", _b.K.Asociado.TaxId, Arg.Any<CancellationToken>()).Returns(Result.Success<CuentaDelCanalDto?>(cuenta));
        _canal.BuscarIntentosAsync(_coop, "CC", _b.K.Asociado.TaxId, null, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<IntentoDelCanalDto>>([Intento(Ref), Intento("ABC2612010000000P2", "pending", 50_000m)]));

        var r = await Buscar().Handle(new SearchChannelSupportQuery("cc", _b.K.Asociado.TaxId, null), default);

        r.Value.Account.Should().Be(cuenta);
        r.Value.Attempts.Should().HaveCount(2);
        r.Value.Attempts.Single(a => a.Status == "pending").CanReverse.Should().BeFalse("no llegó al ERP");
    }

    [Fact]
    public async Task Sin_canal_muestra_lo_que_llego_al_ERP()
    {
        _canal.Configurado.Returns(false);

        var r = await Buscar().Handle(new SearchChannelSupportQuery(null, null, Ref), default);

        r.Value.ChannelAvailable.Should().BeFalse();
        r.Value.ChannelError.Should().NotBeNullOrEmpty();
        r.Value.Attempts.Should().ContainSingle(a => a.Reference == Ref && a.ReceiptNumber == _aviso.ReceiptNumber);
    }

    [Fact]
    public async Task Desbloquear_y_cerrar_sesiones_van_al_canal()
    {
        var cuenta = Guid.NewGuid();
        _canal.DesbloquearCuentaAsync(_coop, cuenta, Arg.Any<CancellationToken>()).Returns(Result.Success());
        _canal.CerrarSesionesAsync(_coop, cuenta, Arg.Any<CancellationToken>()).Returns(Result.Failure("Canal.Atencion.CuentaNoExiste", "No existe."));

        (await new UnlockChannelAccountCommandHandler(_b.K.C.E.Db, _canal, _cooperativa, Auditoria).Handle(new UnlockChannelAccountCommand(cuenta), default))
            .IsSuccess.Should().BeTrue();
        var cerrar = await new RevokeChannelSessionsCommandHandler(_b.K.C.E.Db, _canal, _cooperativa, Auditoria).Handle(new RevokeChannelSessionsCommand(cuenta), default);
        cerrar.Error.Code.Should().Be("Canal.Atencion.CuentaNoExiste");
    }

    [Fact]
    public async Task Los_indicadores_suman_las_cifras_del_ERP()
    {
        var delCanal = new MetricasDelCanalDto(10, 25, 4, 2, 1, 900_000m, new Dictionary<string, int> { ["Fondos insuficientes"] = 2 }, 12, 0);
        _canal.MetricasAsync(_coop, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Arg.Any<CancellationToken>()).Returns(Result.Success(delCanal));

        var r = await new GetChannelMetricsQueryHandler(_b.K.C.E.Db, _canal, _cooperativa)
            .Handle(new GetChannelMetricsQuery(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), default);

        r.Value.Channel.Should().Be(delCanal);
        r.Value.NoticesApplied.Should().Be(1);
        r.Value.ValueApplied.Should().Be(332_500m);
        r.Value.Reversals.Should().Be(0);
        new GetChannelMetricsQueryValidator().Validate(new GetChannelMetricsQuery(new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 1))).IsValid.Should().BeFalse();
    }
}
