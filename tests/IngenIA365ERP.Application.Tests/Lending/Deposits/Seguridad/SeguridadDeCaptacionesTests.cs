using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Charges;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Seguridad;

/// <summary>
/// Feature 014, US16 (T162, T163, T164; FR-003, FR-004, FR-005; api.md §1.2, §1.7, §4): la seguridad de captaciones y aportes en la aplicación.
/// <list type="bullet">
/// <item>T162: la <b>otra</b> cuenta de una operación —el destino de un traslado, de su simulación y de una cesión de aportes— también tiene que estar en el
/// alcance; fuera, el mismo 404 que si no existiera, con <c>Lending.Access.OutOfScope</c> y sin mover saldo. El resumen de aportes de una persona exige el
/// alcance sobre todas sus cuentas.</item>
/// <item>T163: quien abrió la cuenta sí condona un cargo que registró otra persona (los cargos tienen su raíz de expediente), y quien registró el cargo no.</item>
/// <item>T164: el saldo a (D, R) y el extracto en pantalla, con su evento de lectura sensible; el detalle de la cuenta y el resumen de aportes también se
/// auditan.</item>
/// </list>
/// Escenario de <see cref="AportesDePrueba"/> (hoy 2026-12-01) con la UVT de 2026 y dos oficinas. (nuevo)
/// </summary>
public sealed class SeguridadDeCaptacionesTests : IDisposable
{
    private const decimal Uvt = 49_799m;

    private readonly AportesDePrueba _a = new();
    private readonly Branch _sur;

    public SeguridadDeCaptacionesTests()
    {
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Uvt, string.Empty, Uvt, "Pesos", null,
                new DateOnly(2026, 1, 1), null, null, "Resolución DIAN de prueba", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        _a.C.Cerrojo.BloquearCuentasAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.ArgAt<IReadOnlyCollection<int>>(0).ToList()));
        _sur = new Branch { Name = "Oficina sur", CreatedBy = "test" };
        _a.Db.Branches.Add(_sur);
        _a.Db.SaveChanges();
    }

    public void Dispose() => _a.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    /// <summary>Sólo la oficina principal (la del norte) y todas las líneas.</summary>
    private void SoloLaOficinaPrincipal() =>
        _a.G.AlcanceActual = new AlcanceDeCartera(false, new HashSet<int> { _a.C.Oficina.Id }, _a.C.Oficina.Id, true, new HashSet<int>());

    private async Task<(MemberAccountDto Norte, MemberAccountDto Sur)> DosCuentasAsync()
    {
        var linea = await _a.C.LineaDeCaptacionAsync();
        var norte = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana));
        var sur = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Beto) with { OfficePublicId = _sur.PublicId });
        await _a.ConsignadaAsync(_a.Consignacion(norte, 1_000_000m, ModalidadDeConsignacion.Free));
        return (norte, sur);
    }

    private ValidacionPreviaDeCaptaciones ValidacionPrevia() => new(_a.Db, _a.ValidacionPrevia());

    private TransferBetweenMemberAccountsCommandHandler Trasladar() =>
        new(_a.Db, _a.C.Cerrojo, _a.G.Alcance, _a.G.Auditoria, _a.Traslado(), ValidacionPrevia(), _a.Emision(), _a.G.Reloj);

    private SimulateMemberAccountOperationQueryHandler Simular() =>
        new(_a.Db, _a.G.Alcance, _a.G.Auditoria, _a.Movimientos(), _a.Retiro(), _a.Traslado(), _a.G.Reloj);

    private GetMemberAccountBalanceQueryHandler Saldo() =>
        new(_a.Db, _a.G.Alcance, _a.G.Auditoria, _a.Movimientos(), _a.Liquidacion(), _a.G.Reloj);

    private GetMemberAccountStatementQueryHandler Extracto() => new(_a.Db, _a.G.Alcance, _a.G.Auditoria, _a.Movimientos(), _a.G.Reloj);

    private static T Ok<T>(Result<T> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    // ---------------------------------------------------------------------------------------------------- alcance (T162) --

    [Fact]
    public async Task T162_trasladar_a_una_cuenta_de_otra_oficina_es_404_con_el_evento_y_no_mueve_saldo()
    {
        var (norte, sur) = await DosCuentasAsync();
        SoloLaOficinaPrincipal();
        var operaciones = await _a.Db.MemberAccountOperations.CountAsync();

        var r = await Trasladar().Handle(new TransferBetweenMemberAccountsCommand(norte.PublicId, sur.PublicId, 100_000m, null, [_a.C.Ana.PublicId], "Traslado")
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.Error.Code.Should().Be(ErroresDeCuentas.CuentaInexistente().Code, "fuera del alcance es lo mismo que inexistente");
        var inexistente = await Trasladar().Handle(new TransferBetweenMemberAccountsCommand(norte.PublicId, Guid.NewGuid(), 100_000m, null, [_a.C.Ana.PublicId],
            "Traslado") { OperationKey = Guid.NewGuid() }, default);
        inexistente.Error.Code.Should().Be(r.Error.Code);
        (await _a.Db.MemberAccountOperations.CountAsync()).Should().Be(operaciones, "no se escribió nada");
        (await _a.G.EventosAsync()).Should().Contain("Lending.Access.OutOfScope");
    }

    [Fact]
    public async Task T162_simular_un_traslado_a_una_cuenta_de_otra_oficina_tampoco_la_revela()
    {
        var (norte, sur) = await DosCuentasAsync();
        SoloLaOficinaPrincipal();

        var r = await Simular().Handle(new SimulateMemberAccountOperationQuery(norte.PublicId, "Transfer", 100_000m, sur.PublicId, null,
            [_a.C.Ana.PublicId]), default);

        r.Error.Code.Should().Be(ErroresDeCuentas.CuentaInexistente().Code);
        (await _a.G.EventosAsync()).Should().Contain("Lending.Access.OutOfScope");
    }

    [Fact]
    public async Task T162_la_cesion_de_aportes_a_un_asociado_cuya_cuenta_esta_fuera_del_alcance_responde_como_si_no_tuviera_cuenta()
    {
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version() with { AllowsTransferBetweenMembers = true });
        var ana = await _a.CuentaDeAportesAsync(_a.C.Ana, linea);
        await _a.C.AbiertaAsync(new OpenAccountInput { LinePublicId = linea.PublicId, HolderPersonPublicId = _a.C.Beto.PublicId, OfficePublicId = _sur.PublicId });
        await _a.ConsignadaAsync(_a.Consignacion(ana, 1_000_000m, ModalidadDeConsignacion.Free));
        SoloLaOficinaPrincipal();
        var solicitudes = new SolicitudesDeAportes(_a.Db, _a.G.Alcance, _a.G.Auditoria, _a.Reglas());

        var r = await new RequestContributionAssignmentCommandHandler(_a.Db, solicitudes, _a.Reglas(), _a.G.Intervenciones(), _a.G.Actor, _a.G.Reloj)
            .Handle(new RequestContributionAssignmentCommand(ana.PublicId, _a.C.Beto.PublicId, 50_000m, null, "Cesión a un asociado de otra oficina"), default);

        r.Error.Code.Should().Be(ErroresDeAportes.CodigoSinCuentaDeAportes);
        (await _a.Db.ContributionRequests.CountAsync()).Should().Be(0);
        (await _a.G.EventosAsync()).Should().Contain("Lending.Access.OutOfScope");
    }

    [Fact]
    public async Task T162_el_resumen_de_aportes_exige_el_alcance_sobre_todas_las_cuentas_de_la_persona_y_se_audita()
    {
        var aportes = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.ConsignadaAsync(_a.Consignacion(aportes, 100_000m, ModalidadDeConsignacion.Free));
        var resumen = new GetContributionsSummaryQueryHandler(_a.Db, _a.Consulta(), _a.G.Alcance, _a.G.Auditoria, _a.G.Reloj);

        Ok(await resumen.Handle(new GetContributionsSummaryQuery(_a.C.Ana.PublicId), default)).Total.Should().Be(100_000m);
        (await _a.G.EventosAsync()).Should().Contain("Lending.Savings.BalanceViewed", "ver los saldos de una persona es una lectura sensible");

        _a.G.AlcanceActual = new AlcanceDeCartera(false, new HashSet<int> { _sur.Id }, _sur.Id, true, new HashSet<int>());
        var fuera = await resumen.Handle(new GetContributionsSummaryQuery(_a.C.Ana.PublicId), default);
        fuera.Error.Code.Should().Be("Lending.Person.NotFound", "fuera del alcance es lo mismo que una persona inexistente");
        (await _a.G.EventosAsync()).Should().Contain("Lending.Access.OutOfScope");
    }

    // ------------------------------------------------------------------------------------------------ segregación (T163) --

    [Fact]
    public async Task T163_quien_abrio_la_cuenta_condona_el_cargo_que_registro_otra_persona_y_quien_lo_registro_no()
    {
        var linea = await _a.C.LineaDeCaptacionAsync("AHCARGO", CuentasDePrueba.ALaVista() with
        {
            Charges =
            [
                new DepositChargeInput
                {
                    Code = "CERTADIC", Name = "Certificado adicional", Kind = ChargeKind.ExtraDocument, Trigger = ChargeTrigger.ExtraDocument, Enabled = true,
                    Calculation = ChargeCalculation.FixedAmount, FixedAmount = 3_000m,
                },
            ],
        });
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana));
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 50_000m, ModalidadDeConsignacion.Free));

        _a.G.ActuarComo(_a.C.Jefe);
        var registrado = Ok(await new RegisterAccountChargeCommandHandler(_a.Db, _a.C.Cerrojo, _a.G.Alcance, _a.G.Auditoria, _a.Movimientos(), _a.Cargos(),
                _a.G.Intervenciones(), _a.G.Reloj)
            .Handle(new RegisterAccountChargeCommand(cuenta.PublicId, "CERTADIC", "CERT-1", "Certificado bancario adicional") { OperationKey = Guid.NewGuid() },
                default));
        _a.Db.ChangeTracker.Clear();
        WaiveAccountChargeCommandHandler Condonar() => new(_a.Db, _a.C.Cerrojo, _a.G.Alcance, _a.G.Auditoria, _a.Cargos(), _a.G.Intervenciones(), _a.G.Reloj);

        var propio = await Condonar().Handle(new WaiveAccountChargeCommand(cuenta.PublicId, registrado.PublicId, "Cortesía") { OperationKey = Guid.NewGuid() },
            default);
        propio.Error.Code.Should().Be(RegistroDeIntervenciones.CodigoConflicto, "quien registra el cargo no lo condona");

        _a.G.ActuarComo(_a.C.Asesora);
        _a.Db.ChangeTracker.Clear();
        var condonado = await Condonar().Handle(new WaiveAccountChargeCommand(cuenta.PublicId, registrado.PublicId, "Cortesía comercial")
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        condonado.IsSuccess.Should().BeTrue(condonado.IsFailure
            ? $"{condonado.Error.Code}: {condonado.Error.Message} — abrir la cuenta no es registrar el cargo (T163)"
            : string.Empty);
    }

    // ---------------------------------------------------------------------------------------- lecturas sensibles (T164) --

    [Fact]
    public async Task T164_el_saldo_a_D_con_lo_sabido_hasta_R_se_reconstruye_del_libro_y_se_audita()
    {
        var (norte, _) = await DosCuentasAsync();

        var hoy = Ok(await Saldo().Handle(new GetMemberAccountBalanceQuery(norte.PublicId), default));
        hoy.HolderBalance.Should().Be(1_000_000m);
        hoy.ByConcept["Principal"].Should().Be(1_000_000m);
        hoy.Available.Available.Should().Be(1_000_000m);
        hoy.Fingerprint.Should().NotBeNullOrWhiteSpace();
        hoy.AccruedNotSettled.Should().NotBeNull("la cuenta a la vista gana interés: lo causado sin liquidar a D");

        var antes = Ok(await Saldo().Handle(new GetMemberAccountBalanceQuery(norte.PublicId, null, Sessions.GobiernoDePrueba.Ahora.AddSeconds(-1)), default));
        antes.HolderBalance.Should().Be(0m, "con lo registrado antes de la consignación no se sabía de ella (R)");
        antes.Fingerprint.Should().NotBe(hoy.Fingerprint);
        (await _a.G.EventosAsync()).Count(e => e == "Lending.Savings.BalanceViewed").Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task T164_el_saldo_de_una_cuenta_fuera_del_alcance_es_404()
    {
        var (_, sur) = await DosCuentasAsync();
        SoloLaOficinaPrincipal();

        (await Saldo().Handle(new GetMemberAccountBalanceQuery(sur.PublicId), default)).Error.Code.Should().Be(ErroresDeCuentas.CuentaInexistente().Code);
        (await Extracto().Handle(new GetMemberAccountStatementQuery(sur.PublicId), default)).Error.Code.Should().Be(ErroresDeCuentas.CuentaInexistente().Code);
        (await _a.G.EventosAsync()).Should().Contain("Lending.Access.OutOfScope").And.NotContain("Lending.Savings.BalanceViewed");
    }

    [Fact]
    public async Task T164_el_extracto_cuadra_saldo_inicial_lineas_y_saldo_final_con_el_GMF_y_se_audita()
    {
        var (norte, _) = await DosCuentasAsync();
        var retiro = await _a.Contrato().RetirarAsync(new SolicitudDeRetiro(AportesDePrueba.Prueba, AportesDePrueba.Clave(), norte.PublicId, 400_000m, _a.G.HoyLocal,
            ClaseDeMedioDeRecaudo.Cash, [_a.C.Ana.PublicId], _a.C.Ana.PublicId, new ReferenciaDeRecibo("RC-9", _a.G.HoyLocal, null, null, "Recibo")), default);
        retiro.Result.Should().Be(ResultadoDelContrato.Applied, $"{retiro.Code}: {retiro.Message}");
        _a.Db.ChangeTracker.Clear();

        var e = Ok(await Extracto().Handle(new GetMemberAccountStatementQuery(norte.PublicId, _a.G.HoyLocal, _a.G.HoyLocal), default));

        e.OpeningBalance.Should().Be(0m);
        e.Lines.Should().HaveCount(2, "la consignación y el retiro (con su GMF dentro)");
        e.Lines[0].Amount.Should().Be(1_000_000m);
        e.Lines[1].Amount.Should().Be(-401_600m);
        e.Lines[1].Gmf.Should().Be(1_600m);
        e.Gmf.Should().Be(1_600m);
        e.ClosingBalance.Should().Be(598_400m);
        e.Lines[^1].Balance.Should().Be(e.ClosingBalance);
        e.AvailableToday.Available.Should().Be(598_400m);
        (await _a.G.EventosAsync()).Should().Contain("Lending.Savings.StatementViewed");
    }

    [Fact]
    public async Task T164_ver_el_detalle_de_una_cuenta_y_las_cuentas_de_una_persona_son_lecturas_auditadas()
    {
        var (norte, _) = await DosCuentasAsync();

        Ok(await _a.C.Leer().Handle(new GetMemberAccountQuery(norte.PublicId), default));
        Ok(await _a.C.Listar().Handle(new ListMemberAccountsQuery(PersonPublicId: _a.C.Ana.PublicId), default));
        Ok(await _a.C.Listar().Handle(new ListMemberAccountsQuery(), default));

        (await _a.G.EventosAsync()).Count(e => e == "Lending.Savings.BalanceViewed").Should().Be(2,
            "el detalle y la lista de una persona; la lista general sin titular no es una consulta de saldos de un tercero");
    }

    [Fact]
    public void T164_el_extracto_agrupa_lo_causado_por_mes_y_siempre_cuadra()
    {
        FilaDeExtracto Fila(long id, MemberAccountTransactionKind k, MemberAccountConcept c, decimal m, DateOnly d, long? op = null) => new(id, k, c, m, d, op);
        var filas = new List<FilaDeExtracto>
        {
            Fila(1, MemberAccountTransactionKind.Deposit, MemberAccountConcept.Principal, 500_000m, new DateOnly(2026, 10, 20), 10),
            Fila(2, MemberAccountTransactionKind.InterestAccrued, MemberAccountConcept.InterestPayable, 12.345678m, new DateOnly(2026, 11, 1)),
            Fila(3, MemberAccountTransactionKind.InterestAccrued, MemberAccountConcept.InterestPayable, 12.345678m, new DateOnly(2026, 11, 2)),
            Fila(4, MemberAccountTransactionKind.InterestCredited, MemberAccountConcept.InterestPayable, -24.69m, new DateOnly(2026, 11, 30), 11),
            Fila(5, MemberAccountTransactionKind.InterestCredited, MemberAccountConcept.Principal, 25m, new DateOnly(2026, 11, 30), 11),
            Fila(6, MemberAccountTransactionKind.WithholdingApplied, MemberAccountConcept.Principal, -2m, new DateOnly(2026, 11, 30), 11),
            Fila(7, MemberAccountTransactionKind.WithholdingApplied, MemberAccountConcept.Withholding, 2m, new DateOnly(2026, 11, 30), 11),
            Fila(8, MemberAccountTransactionKind.InterestAccrued, MemberAccountConcept.InterestPayable, 12.3m, new DateOnly(2026, 12, 1)),
        };
        var operaciones = new Dictionary<long, OperacionDeExtracto>
        {
            [10] = new(Guid.NewGuid(), "CON-1", "Deposit"), [11] = new(Guid.NewGuid(), "LQI-1", "InterestSettlement"),
        };

        var e = ExtractosDeCuenta.Armar(LineNature.Deposit, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), filas, operaciones);

        e.SaldoInicial.Should().Be(500_000m);
        e.Lineas.Select(l => (l.ValueDate, l.Kind)).Should().Equal((new DateOnly(2026, 11, 30), ExtractosDeCuenta.CausadoDelMes),
            (new DateOnly(2026, 11, 30), "InterestSettlement"));
        e.Lineas[0].Amount.Should().Be(24.691356m, "lo causado del mes en una línea");
        e.InteresesAbonados.Should().Be(25m);
        e.Retencion.Should().Be(2m);
        e.SaldoFinal.Should().Be(e.SaldoInicial + e.Lineas.Sum(l => l.Amount), "el extracto cuadra siempre");
        e.SaldoFinal.Should().Be(500_000m + 24.691356m - 24.69m + 25m - 2m);
    }
}
