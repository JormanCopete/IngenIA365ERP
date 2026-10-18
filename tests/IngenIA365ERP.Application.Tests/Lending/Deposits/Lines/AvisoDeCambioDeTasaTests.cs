using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Tests.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Lines;

/// <summary>
/// Feature 014, T067 y T068 (US1; FR-038; research E41): el aviso de cambio de tasa de una línea a la vista. Con titulares vivos la versión espera la
/// anticipación (la mayor entre el reglamento y la de la línea) y cada titular recibe el informativo <c>CAMBIOTASA</c> por la mensajería de la Cartera;
/// sin cambio de tasa, con tasa pactada o sin titulares no se avisa ni se espera. Escenario de <see cref="MensajesDePrueba"/>: Cartera activada, hoy
/// martes 1 de diciembre de 2026; Carla no tiene crédito pero sí una cuenta de ahorro a la vista. (nuevo)
/// </summary>
public sealed class AvisoDeCambioDeTasaTests : IDisposable
{
    private readonly MensajesDePrueba _e = new();
    private readonly IContextoDeLineasDeCaptacion _contexto = Substitute.For<IContextoDeLineasDeCaptacion>();
    private int _anticipacion;

    public AvisoDeCambioDeTasaTests()
    {
        _contexto.Entrega.Returns(EntregaDeCartera.K8);
        _contexto.PerfilAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new PerfilParaLineas(true, false, false, "AHORRO_CREDITO", null));
        _contexto.TopesDeAportesAsync(Arg.Any<DateOnly>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Result.Success(TopesDeAportes.Ninguno));
        _contexto.AnticipacionDelAvisoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(_ => _anticipacion);
        _e.G.Conceder(PermisosDeLineas.VerCaptacion, PermisosDeLineas.AdministrarCaptacion);
        _e.G.ActuarComo(_e.G.Usuario(_e.G.Persona("Juana", "Captaciones", "9001"), "jefa@coop.test"));
        _e.Plantilla(AvisoDeCambioDeTasa.CodigoDePlantilla, DebtorMessagePurpose.Informative, MessageChannel.Email, "Cambio de tasa de su cuenta {{NumeroDeCuenta}}",
            "{{NombreDelTitular}}: desde el {{FechaDelCambio}} la tasa de su cuenta {{NumeroDeCuenta}} pasa de {{TasaAnterior}} a {{TasaNueva}}.");
        _e.Autorizar(_e.Carla, MessageChannel.Email);
    }

    public void Dispose() => _e.Dispose();

    private DateOnly Hoy => _e.G.Reloj.HoyLocal;

    private AltaDeVersionDeCaptacion Alta() => new(_e.Db, _e.G.Reloj);

    private AddDepositLineVersionCommandHandler Versionar() =>
        new(_e.Db, Alta(), new AvisoDeCambioDeTasa(_e.Db, _contexto, _e.Cola()), _contexto, _e.G.Alcance, _e.G.Auditoria, _e.G.Actor, _e.G.Reloj);

    private static DepositLineVersionInput Version(DateOnly desde, decimal tasa, bool pactada = false, int aviso = 0) => new()
    {
        ValidFrom = desde,
        Reason = "Cambio de tasa",
        AllowsPartialWithdrawals = true,
        Rate = new DepositRateInput { EffectiveAnnual = tasa },
        DeclaresAgreedRate = pactada,
        RateChangeNoticeDays = aviso,
    };

    private async Task<Guid> LineaConCuentaDeCarlaAsync(bool conCuenta = true)
    {
        var r = await new CreateDepositLineCommandHandler(_e.Db, Alta(), _contexto, _e.G.Reloj)
            .Handle(new CreateDepositLineCommand("AHVISTA", "Ahorro a la vista", DepositClass.Demand, true, Version(Hoy, 0.02m)), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        if (!conCuenta) return r.Value.PublicId;

        var linea = await _e.Db.CreditLines.SingleAsync(l => l.PublicId == r.Value.PublicId);
        var cuenta = new MemberAccount
        {
            Number = "AHVISTA-0001", CreditLineId = linea.Id, Nature = LineNature.Deposit, DepositClass = DepositClass.Demand, HolderPersonId = _e.Carla.Id,
            BranchId = MensajesDePrueba.Norte, Status = MemberAccountStatus.Active, StatusChangedAt = _e.G.UtcNow, OpenedOn = Hoy.AddMonths(-3),
        };
        _e.Db.MemberAccounts.Add(cuenta);
        await _e.Db.SaveChangesAsync();
        _e.Db.MemberAccountParties.Add(new MemberAccountParty
        {
            MemberAccountId = cuenta.Id, PersonId = _e.Carla.Id, Role = MemberAccountPartyRole.Holder, ValidFrom = Hoy.AddMonths(-3),
        });
        await _e.Db.SaveChangesAsync();
        return r.Value.PublicId;
    }

    [Fact]
    public async Task Con_titulares_el_cambio_espera_la_anticipacion_y_avisa_a_cada_titular()
    {
        var linea = await LineaConCuentaDeCarlaAsync();
        _anticipacion = 15;

        var corta = await Versionar().Handle(new AddDepositLineVersionCommand(linea, Version(Hoy.AddDays(10), 0.025m)), default);
        var aTiempo = await Versionar().Handle(new AddDepositLineVersionCommand(linea, Version(Hoy.AddDays(15), 0.025m)) { OperationKey = Guid.NewGuid() }, default);

        corta.Error.Code.Should().Be("Lending.Savings.RateChangeNoticeTooShort");
        Sessions.GobiernoDePrueba.Datos(corta.Error).GetProperty("requiredDays").GetInt32().Should().Be(15);
        aTiempo.IsSuccess.Should().BeTrue(aTiempo.IsFailure ? $"{aTiempo.Error.Code}: {aTiempo.Error.Message}" : string.Empty);
        var mensaje = await _e.Db.DebtorMessages.SingleAsync();
        mensaje.PersonId.Should().Be(_e.Carla.Id);
        mensaje.LoanId.Should().BeNull();
        mensaje.OriginKind.Should().Be(DebtorMessage.OrigenCambioDeTasa);
        mensaje.Purpose.Should().Be(DebtorMessagePurpose.Informative);
        mensaje.RequestedByUserId.Should().NotBeNull("lo origina la persona que registró la versión");
        mensaje.RenderedBody.Should().Contain("AHVISTA-0001").And.Contain("2 % EA").And.Contain("2,5 % EA");
    }

    [Fact]
    public async Task La_anticipacion_de_la_linea_rige_si_es_mayor_que_la_del_reglamento()
    {
        var linea = await LineaConCuentaDeCarlaAsync();
        _anticipacion = 5;

        var r = await Versionar().Handle(new AddDepositLineVersionCommand(linea, Version(Hoy.AddDays(10), 0.025m, aviso: 30)), default);

        r.Error.Code.Should().Be("Lending.Savings.RateChangeNoticeTooShort");
        Sessions.GobiernoDePrueba.Datos(r.Error).GetProperty("requiredDays").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task Sin_cambio_de_tasa_con_tasa_pactada_o_sin_titulares_no_se_avisa_ni_se_espera()
    {
        _anticipacion = 15;
        var sinTitulares = await LineaConCuentaDeCarlaAsync(conCuenta: false);
        (await Versionar().Handle(new AddDepositLineVersionCommand(sinTitulares, Version(Hoy.AddDays(1), 0.03m)) { OperationKey = Guid.NewGuid() }, default))
            .IsSuccess.Should().BeTrue("una línea sin cuentas no tiene a quién avisar");

        var linea = await _e.Db.CreditLines.SingleAsync();
        var cuenta = new MemberAccount
        {
            Number = "AHVISTA-0001", CreditLineId = linea.Id, Nature = LineNature.Deposit, DepositClass = DepositClass.Demand, HolderPersonId = _e.Carla.Id,
            BranchId = MensajesDePrueba.Norte, Status = MemberAccountStatus.Active, StatusChangedAt = _e.G.UtcNow, OpenedOn = Hoy,
        };
        _e.Db.MemberAccounts.Add(cuenta);
        await _e.Db.SaveChangesAsync();
        _e.Db.MemberAccountParties.Add(new MemberAccountParty { MemberAccountId = cuenta.Id, PersonId = _e.Carla.Id, Role = MemberAccountPartyRole.Holder, ValidFrom = Hoy });
        await _e.Db.SaveChangesAsync();

        (await Versionar().Handle(new AddDepositLineVersionCommand(linea.PublicId, Version(Hoy.AddDays(2), 0.03m) with { Reason = "Otro mínimo",
            Minimums = new DepositMinimumsInput(ToOpen: 5_000m) }) { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue("la tasa no cambió");
        (await Versionar().Handle(new AddDepositLineVersionCommand(linea.PublicId, Version(Hoy.AddDays(3), 0.04m, pactada: true))
            { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue("con tasa pactada cada cuenta conserva la suya");
        (await _e.Db.DebtorMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Solo_se_avisa_en_la_vista_sin_tasa_pactada_si_cambia_la_tasa()
    {
        var anterior = Version(new DateOnly(2026, 12, 1), 0.02m).Borrador(DepositClass.Demand, null, false);
        var nueva = Version(new DateOnly(2026, 12, 20), 0.03m).Borrador(DepositClass.Demand, null, false);

        AvisoDeCambioDeTasa.SeAvisa(anterior, nueva).Should().BeTrue();
        AvisoDeCambioDeTasa.SeAvisa(null, nueva).Should().BeFalse();
        AvisoDeCambioDeTasa.SeAvisa(anterior, nueva with { DeclaresAgreedRate = true }).Should().BeFalse();
        AvisoDeCambioDeTasa.SeAvisa(anterior with { DepositClass = DepositClass.Permanent }, nueva with { DepositClass = DepositClass.Permanent }).Should().BeFalse();
        AvisoDeCambioDeTasa.Tasa(nueva).Should().Be("3 % EA");
    }

    [Fact]
    public async Task La_cola_admite_al_titular_de_una_cuenta_viva_solo_en_un_informativo()
    {
        await LineaConCuentaDeCarlaAsync();
        var otra = new Person { FirstName = "Dora", LastName = "SinNada", TaxId = "1", IdType = "C", CreatedBy = "test" };
        _e.Db.People.Add(otra);
        await _e.Db.SaveChangesAsync();
        _e.Autorizar(otra, MessageChannel.Email);
        Application.Lending.Collections.PedidoDeMensajeAlDeudor PedidoDe(Person p, DebtorMessagePurpose proposito) => new(p.Id, null, proposito, MessageChannel.Email, AvisoDeCambioDeTasa.CodigoDePlantilla,
            DebtorMessage.OrigenCambioDeTasa, $"prueba:{p.Id}:{proposito}", Domain.Enums.Integration.ActorKind.Person, 7);

        (await _e.Cola().EncolarAsync(PedidoDe(_e.Carla, DebtorMessagePurpose.Informative), default)).IsSuccess.Should().BeTrue();
        (await _e.Cola().EncolarAsync(PedidoDe(otra, DebtorMessagePurpose.Informative), default)).Error.Code
            .Should().Be(DebtorMessage.CodigoDestinatarioNoObligado);
        (await _e.Cola().EncolarAsync(PedidoDe(_e.Carla, DebtorMessagePurpose.Collection), default)).Error.Code
            .Should().Be(DebtorMessage.CodigoDestinatarioNoObligado, "la cobranza sigue exigiendo una obligación");
    }
}
