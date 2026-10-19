using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Holds;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Holds;

/// <summary>
/// Feature 014, US11 (T190; FR-054, FR-055, FR-057, FR-065, FR-005; research §20): los embargos y los bloqueos sobre el libro real —el embargo que retiene
/// sólo el exceso sobre el límite inembargable (la prueba independiente de la historia), el de alimentos sin límite, el oficio sólo sobre aportes que se
/// rechaza con su respuesta, quien registra no levanta, el desembargo que libera y reclasifica, la consignación por orden de giro, el disponible del retiro,
/// el bloqueo por valor y el total, el recálculo del ciclo diario y el límite que falta—. El escenario de <see cref="AportesDePrueba"/> (hoy 2026-12-01) con
/// un límite inembargable de prueba de 50.000.000. (nuevo)
/// </summary>
public sealed class EmbargosTests : IDisposable
{
    private const decimal Limite = 50_000_000m;

    private readonly AportesDePrueba _a = new();
    private readonly Person _juzgado;

    public EmbargosTests()
    {
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.LimiteInembargable, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.LimiteInembargable, string.Empty, Limite, "Pesos",
                null, new DateOnly(2026, 1, 1), null, null, "Carta circular de prueba", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Uvt, string.Empty, 49_799m, "Pesos", null,
                new DateOnly(2026, 1, 1), null, null, "Resolución DIAN de prueba", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        _a.C.Cerrojo.BloquearCuentasAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.ArgAt<IReadOnlyCollection<int>>(0).ToList()));
        _juzgado = _a.G.Persona("Juzgado", "Primero Civil Municipal", "899999001");
    }

    public void Dispose() => _a.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    private ReclasificacionDeCuenta Reclasificacion() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.G.Actor, _a.G.Reloj);

    private RetencionDeEmbargos Retencion() => new(_a.Db, _a.C.Parametros(), _a.G.Valores, Reclasificacion(), _a.G.Reloj);

    private RegisterGarnishmentCommandHandler Registrar() =>
        new(_a.Db, _a.C.Cerrojo, _a.G.Alcance, _a.G.Auditoria, Retencion(), _a.G.Intervenciones(), _a.Calendario, _a.G.Actor, _a.G.Reloj);

    private ReleaseGarnishmentCommandHandler Levantar() =>
        new(_a.Db, _a.C.Cerrojo, _a.G.Alcance, _a.G.Auditoria, Retencion(), _a.G.Intervenciones(), _a.G.Reloj);

    private ConsignGarnishmentCommandHandler Consignar() =>
        new(_a.Db, _a.C.Cerrojo, _a.G.Alcance, _a.G.Auditoria, Retencion(), _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(),
            new ValidacionPreviaDeCaptaciones(_a.Db, _a.ValidacionPrevia()), _a.G.Actor, _a.G.Reloj);

    private async Task<MemberAccountDto> AhorroConSaldoAsync(string linea, decimal saldo, DepositClass clase = DepositClass.Demand)
    {
        var l = await _a.C.LineaDeCaptacionAsync(linea, CuentasDePrueba.ALaVista(), clase);
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(l, _a.C.Ana));
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, saldo, ModalidadDeConsignacion.Free));
        return cuenta;
    }

    private RegisterGarnishmentInput Oficio(decimal? valor, bool alimentos = false, GarnishmentAccountsScope alcance = GarnishmentAccountsScope.AllSavings,
        IReadOnlyList<Guid>? cuentas = null) => new()
    {
        PersonPublicId = _a.C.Ana.PublicId, CourtPersonPublicId = _juzgado.PublicId, ProcessNumber = "11001400300120260045600", OfficeReference = "OF-118",
        OfficeDate = _a.G.HoyLocal.AddDays(-2), ClaimantName = "Banco Ejemplo S.A.", Amount = valor, IsAlimony = alimentos, AccountsScope = alcance,
        AccountPublicIds = cuentas, SupportAttachmentPublicId = Guid.NewGuid(),
    };

    private async Task<GarnishmentDto> RegistradoAsync(RegisterGarnishmentInput oficio)
    {
        var r = await Registrar().Handle(new RegisterGarnishmentCommand(oficio) { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<decimal> RetenidoAsync(MemberAccountDto cuenta)
    {
        var id = await _a.Db.MemberAccounts.Where(a => a.PublicId == cuenta.PublicId).Select(a => a.Id).SingleAsync();
        return await _a.Db.AccountHolds.AsNoTracking().Where(h => h.MemberAccountId == id && h.ReleasedAt == null && h.Reason == HoldReason.Garnishment)
            .SumAsync(h => h.Amount ?? 0m);
    }

    private async Task<bool> EmbargadaAsync(MemberAccountDto cuenta) =>
        await _a.Db.MemberAccounts.AsNoTracking().Where(a => a.PublicId == cuenta.PublicId).Select(a => a.IsGarnished).SingleAsync();

    // --------------------------------------------------------------------------------------------------------- US11 --

    [Fact]
    public async Task US11_dos_cuentas_sobre_el_limite_retienen_solo_el_exceso_en_el_orden()
    {
        var vieja = await AhorroConSaldoAsync("AHVISTA", 40_000_000m);
        var nueva = await AhorroConSaldoAsync("AHVISTA2", 30_000_000m);

        var embargo = await RegistradoAsync(Oficio(15_000_000m));

        embargo.Status.Should().Be(GarnishmentStatus.Active);
        embargo.RetainedTotal.Should().Be(15_000_000m);
        embargo.UnembargableLimit.Should().Be(Limite);
        (await RetenidoAsync(vieja)).Should().Be(15_000_000m, "la más antigua primero, del exceso de 20.000.000");
        (await RetenidoAsync(nueva)).Should().Be(0m);
        (await EmbargadaAsync(vieja)).Should().BeTrue();
        (await EmbargadaAsync(nueva)).Should().BeFalse();
        (await _a.HechosAsync()).Should().Contain(CaptacionReclasificadaV1.Type, "la marca embargada reclasifica los saldos de la cuenta");
        (await _a.Db.DebtorBlocks.AsNoTracking().AnyAsync(b => b.Reason == DebtorBlockReason.Garnishment && b.LiftedAt == null)).Should().BeTrue();
        embargo.ResponseDueOn.Should().NotBeNull("el plazo informativo de respuesta: tres días hábiles");

        var consulta = await _a.Contrato().ConsultarAsync(new SolicitudDeConsultaDeCuentas(AportesDePrueba.Prueba, _a.C.Ana.PublicId, _a.G.HoyLocal), default);
        var disponible = consulta.Value!.Accounts.Single(a => a.AccountPublicId == vieja.PublicId).Available;
        disponible.Garnishments.Should().Be(15_000_000m);
        disponible.Available.Should().Be(25_000_000m);
    }

    [Fact]
    public async Task US11_1_por_alimentos_el_limite_inembargable_no_aplica()
    {
        var vieja = await AhorroConSaldoAsync("AHVISTA", 40_000_000m);
        var nueva = await AhorroConSaldoAsync("AHVISTA2", 30_000_000m);

        var embargo = await RegistradoAsync(Oficio(60_000_000m, alimentos: true));

        embargo.RetainedTotal.Should().Be(60_000_000m);
        (await RetenidoAsync(vieja)).Should().Be(40_000_000m);
        (await RetenidoAsync(nueva)).Should().Be(20_000_000m);
    }

    [Fact]
    public async Task US11_2_un_oficio_solo_sobre_aportes_se_rechaza_y_deja_la_respuesta_al_juzgado()
    {
        var aportes = await _a.CuentaDeAportesAsync(_a.C.Ana);

        var embargo = await RegistradoAsync(Oficio(5_000_000m, alcance: GarnishmentAccountsScope.Specific, cuentas: [aportes.PublicId]));

        embargo.Status.Should().Be(GarnishmentStatus.Rejected);
        embargo.Warnings.Should().Contain(w => w.StartsWith(ErroresDeEmbargos.CodigoAportesInembargables, StringComparison.Ordinal));
        embargo.Responses.Should().ContainSingle().Which.Kind.Should().Be(GarnishmentResponseKind.ContributionsNotGarnishable);
        (await _a.Db.AccountHolds.AnyAsync()).Should().BeFalse("los aportes no se embargan (Ley 79 de 1988, art. 49)");
    }

    [Fact]
    public async Task US11_3_quien_registra_no_levanta_y_el_desembargo_libera_y_conserva_la_historia()
    {
        var cuenta = await AhorroConSaldoAsync("AHVISTA", 70_000_000m);
        var embargo = await RegistradoAsync(Oficio(null));
        (await RetenidoAsync(cuenta)).Should().Be(20_000_000m);

        var mismo = await Levantar().Handle(new ReleaseGarnishmentCommand(embargo.PublicId, "OF-200", null, null, null, "Desembargo del juzgado")
            { OperationKey = Guid.NewGuid() }, default);
        mismo.Error.Code.Should().Be(RegistroDeIntervenciones.CodigoConflicto);

        _a.Db.ChangeTracker.Clear();
        _a.G.ActuarComo(_a.C.Jefe);
        var levantado = await Levantar().Handle(new ReleaseGarnishmentCommand(embargo.PublicId, "OF-200", null, null, null, "Desembargo del juzgado")
            { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();

        levantado.IsSuccess.Should().BeTrue(levantado.IsFailure ? $"{levantado.Error.Code}: {levantado.Error.Message}" : string.Empty);
        levantado.Value.Status.Should().Be(GarnishmentStatus.Released);
        (await RetenidoAsync(cuenta)).Should().Be(0m);
        (await EmbargadaAsync(cuenta)).Should().BeFalse();
        (await _a.Db.AccountHolds.AsNoTracking().CountAsync(h => h.ReleasedAt != null)).Should().Be(1, "el bloqueo se levanta, no se borra");
        (await _a.HechosAsync()).Count(t => t == CaptacionReclasificadaV1.Type).Should().Be(2, "embargo y desembargo reclasifican");
        (await _a.Db.DebtorBlocks.AsNoTracking().AnyAsync(b => b.Reason == DebtorBlockReason.Garnishment && b.LiftedAt == null)).Should().BeFalse();
    }

    [Fact]
    public async Task Un_desembargo_parcial_baja_la_medida_y_el_embargo_sigue()
    {
        var cuenta = await AhorroConSaldoAsync("AHVISTA", 70_000_000m);
        var embargo = await RegistradoAsync(Oficio(15_000_000m));
        _a.G.ActuarComo(_a.C.Jefe);

        var r = await Levantar().Handle(new ReleaseGarnishmentCommand(embargo.PublicId, "OF-201", null, null, 5_000_000m, "Reducción ordenada")
            { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(GarnishmentStatus.Active);
        r.Value.Amount.Should().Be(10_000_000m);
        (await RetenidoAsync(cuenta)).Should().Be(10_000_000m);
    }

    [Fact]
    public async Task La_consignacion_sale_por_orden_de_giro_y_baja_lo_retenido()
    {
        var cuenta = await AhorroConSaldoAsync("AHVISTA", 70_000_000m);
        var embargo = await RegistradoAsync(Oficio(15_000_000m));

        var sobra = await Consignar().Handle(new ConsignGarnishmentCommand(embargo.PublicId, 16_000_000m, null, "0001234", 1, "Consignación al juzgado")
            { OperationKey = Guid.NewGuid() }, default);
        sobra.Error.Code.Should().Be(ErroresDeEmbargos.CodigoExcedeRetenido);
        _a.Db.ChangeTracker.Clear();

        var r = await Consignar().Handle(new ConsignGarnishmentCommand(embargo.PublicId, 10_000_000m, null, "0001234", 1, "Consignación al juzgado")
            { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Lines.Should().ContainSingle().Which.Amount.Should().Be(10_000_000m);
        r.Value.Garnishment.Consigned.Should().Be(10_000_000m);
        (await RetenidoAsync(cuenta)).Should().Be(5_000_000m, "lo que queda de la medida");
        (await _a.Db.AccountPayoutOrders.AsNoTracking().SingleAsync()).Purpose.Should().Be(PayoutPurpose.GarnishmentConsignment);
        (await _a.Db.MemberAccountTransactions.AsNoTracking().Where(t => t.Kind == MemberAccountTransactionKind.GarnishmentConsigned).SumAsync(t => t.Amount))
            .Should().Be(-10_000_000m);
        (await _a.HechosAsync()).Should().Contain(EmbargoConsignadoV1.Type);
    }

    [Fact]
    public async Task El_retiro_respeta_lo_embargado()
    {
        var cuenta = await AhorroConSaldoAsync("AHVISTA", 60_000_000m);
        await RegistradoAsync(Oficio(null));

        var r = await _a.Contrato().RetirarAsync(new SolicitudDeRetiro(AportesDePrueba.Prueba, AportesDePrueba.Clave(), cuenta.PublicId, 55_000_000m,
            _a.G.HoyLocal, ClaseDeMedioDeRecaudo.Cash, [cuenta.Holder.PersonPublicId], cuenta.Holder.PersonPublicId,
            new ReferenciaDeRecibo("RC-9", _a.G.HoyLocal, null, null, "Recibo")), default);

        r.Result.Should().Be(ResultadoDelContrato.Rejected);
        r.Code.Should().Be(Application.Lending.Deposits.Operations.ErroresDeMovimientos.CodigoDisponibleInsuficiente);
    }

    [Fact]
    public async Task Sin_el_limite_inembargable_registrado_no_se_sabe_que_retener()
    {
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.LimiteInembargable, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ValorNacionalVigente>(new Error("NationalValues.Missing", "Sin valor.")));
        await AhorroConSaldoAsync("AHVISTA", 60_000_000m);

        var r = await Registrar().Handle(new RegisterGarnishmentCommand(Oficio(1_000_000m)) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("NationalValues.Missing");
    }

    [Fact]
    public async Task El_ciclo_diario_retiene_lo_que_se_consigna_despues()
    {
        var cuenta = await AhorroConSaldoAsync("AHVISTA", 45_000_000m);
        var embargo = await RegistradoAsync(Oficio(10_000_000m));
        embargo.RetainedTotal.Should().Be(0m);
        embargo.Warnings.Should().Contain(w => w.StartsWith(ErroresDeEmbargos.CodigoNadaEmbargable, StringComparison.Ordinal));

        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 8_000_000m, ModalidadDeConsignacion.Free));
        var id = await _a.Db.MemberAccounts.Where(a => a.PublicId == cuenta.PublicId).Select(a => a.Id).SingleAsync();
        var fallos = await new EmbargosDelCiclo(_a.Db, _a.C.Cerrojo, Retencion()).ProcesarAsync(new LendingProcessRun { Id = 1 }, _a.G.HoyLocal, [id], default);
        _a.Db.ChangeTracker.Clear();

        fallos.Should().BeEmpty();
        (await RetenidoAsync(cuenta)).Should().Be(3_000_000m, "53.000.000 menos el límite de 50.000.000");
    }

    // ------------------------------------------------------------------------------------------------------ bloqueos --

    [Fact]
    public async Task Un_bloqueo_por_valor_no_supera_el_disponible_y_el_total_lo_deja_en_cero()
    {
        var cuenta = await AhorroConSaldoAsync("AHVISTA", 1_000_000m);
        var poner = new PlaceAccountHoldCommandHandler(_a.Db, _a.C.Cerrojo, _a.G.Alcance, _a.G.Auditoria, _a.Movimientos(), _a.G.Actor, _a.G.Reloj);

        var excede = await poner.Handle(new PlaceAccountHoldCommand(cuenta.PublicId, HoldReason.Internal, 2_000_000m, null, null, null, "Orden interna 5")
            { OperationKey = Guid.NewGuid() }, default);
        excede.Error.Code.Should().Be(ErroresDeEmbargos.CodigoDisponibleInsuficiente);
        _a.Db.ChangeTracker.Clear();

        var total = await poner.Handle(new PlaceAccountHoldCommand(cuenta.PublicId, HoldReason.SignaturePending, null, null, null, null, "Falta la firma")
            { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        total.IsSuccess.Should().BeTrue(total.IsFailure ? $"{total.Error.Code}: {total.Error.Message}" : string.Empty);
        total.Value.Total.Should().BeTrue();

        var consulta = await _a.Contrato().ConsultarAsync(new SolicitudDeConsultaDeCuentas(AportesDePrueba.Prueba, _a.C.Ana.PublicId, _a.G.HoyLocal), default);
        consulta.Value!.Accounts.Single(a => a.AccountPublicId == cuenta.PublicId).Available.Available.Should().Be(0m);

        var levantar = new ReleaseAccountHoldCommandHandler(_a.Db, _a.C.Cerrojo, _a.G.Alcance, _a.G.Auditoria, _a.G.Actor, _a.G.Reloj);
        var r = await levantar.Handle(new ReleaseAccountHoldCommand(cuenta.PublicId, total.Value.PublicId, "Firmó") { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue();
        r.Value.ReleasedAt.Should().NotBeNull();
    }
}
