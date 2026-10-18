using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.Contrato;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, T039–T043 (contrato-de-cuentas §3, §4, §5; research R09, R20): el contrato de cuentas «sin activar» responde «no habilitado» sin efectos
/// y con el código de la naturaleza de la cuenta; la consulta de aportes v2 conserva la v1; el registro de contratos conoce <c>ProveedorDeCuentas</c>;
/// Tesorería tiene su clave y sus medios; y la consulta de productos de captación lee en bloque las cuentas de unas personas con su saldo del libro.
/// </summary>
public class PuertosDeCuentasTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 3);
    private static readonly DateTime Ahora = new(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Recaudador Prueba = new("TST", ClaseDeRecaudador.Test);
    private static readonly ReferenciaDeRecibo Recibo = new("R-1", Hoy, null, null, null);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private MemberAccount Cuenta(int titular, LineNature naturaleza, DepositClass? clase, int lineaId = 1, string numero = "C-1")
    {
        var cuenta = MemberAccount.Abrir(lineaId, naturaleza, clase, titular, 3, HandlingMode.Indistinct, Hoy, 5, Ahora);
        cuenta.Number = numero;
        _db.MemberAccounts.Add(cuenta);
        _db.SaveChanges();
        return cuenta;
    }

    // ------------------------------------------------------------------------------------------- T040: sin activar --

    [Fact]
    public async Task Sin_activar_las_consultas_responden_vacias_y_las_escrituras_se_rechazan_con_el_codigo_de_la_naturaleza()
    {
        var aportes = Cuenta(7, LineNature.Contribution, null);
        var ahorro = Cuenta(7, LineNature.Deposit, DepositClass.Demand, numero: "C-2");
        var contrato = new ContratoDeCuentasSinActivar(_db);

        var consulta = await contrato.ConsultarAsync(new SolicitudDeConsultaDeCuentas(Prueba, Guid.NewGuid(), Hoy), CancellationToken.None);
        consulta.Result.Should().Be(ResultadoDelContrato.Answered);
        consulta.Value!.DepositsStatus.Should().Be(EstadoDeCartera.NotActivated);
        consulta.Value.ContributionsStatus.Should().Be(EstadoDeCartera.NotActivated);
        consulta.Value.Accounts.Should().BeEmpty();
        (await contrato.ListarExigiblesAsync(new SolicitudDeExigibles(Prueba, [Guid.NewGuid()], Hoy), CancellationToken.None)).Value!.Installments.Should().BeEmpty();

        var simulacion = await contrato.SimularAsync(new SolicitudDeSimulacionDeCuenta(Prueba, "Deposit", aportes.PublicId, 1000m, Hoy,
            ClaseDeMedioDeRecaudo.Cash, []), CancellationToken.None);
        simulacion.Result.Should().Be(ResultadoDelContrato.Answered);
        simulacion.Code.Should().Be(ContratoDeCuentasSinActivar.CodigoAportesNoActivados);
        simulacion.Value!.Allowed.Should().BeFalse();

        var consignacion = await contrato.ConsignarAsync(new SolicitudDeConsignacion(Prueba, FormaDeAplicacion.NonInteractive,
        [
            new ConsignacionAAplicar("TST:" + Guid.NewGuid().ToString("N"), aportes.PublicId, ModalidadDeConsignacion.Free, 100m, Hoy, ClaseDeMedioDeRecaudo.Cash,
                Guid.NewGuid(), Recibo),
            new ConsignacionAAplicar("TST:" + Guid.NewGuid().ToString("N"), ahorro.PublicId, ModalidadDeConsignacion.Free, 100m, Hoy, ClaseDeMedioDeRecaudo.Cash,
                Guid.NewGuid(), Recibo),
            new ConsignacionAAplicar("TST:" + Guid.NewGuid().ToString("N"), Guid.NewGuid(), ModalidadDeConsignacion.Free, 100m, Hoy, ClaseDeMedioDeRecaudo.Cash,
                Guid.NewGuid(), Recibo),
        ], []), CancellationToken.None);
        consignacion.Deposits.Select(d => (d.Result, d.Code)).Should().Equal(
            (ResultadoDelContrato.Rejected, ContratoDeCuentasSinActivar.CodigoAportesNoActivados),
            (ResultadoDelContrato.Rejected, ContratoDeCuentasSinActivar.CodigoCaptacionesNoActivadas),
            (ResultadoDelContrato.Rejected, ContratoDeCuentasSinActivar.CodigoCaptacionesNoActivadas));

        var retiro = await contrato.RetirarAsync(new SolicitudDeRetiro(Prueba, "TST:" + Guid.NewGuid().ToString("N"), ahorro.PublicId, 10m, Hoy,
            ClaseDeMedioDeRecaudo.Cash, [], Guid.NewGuid(), Recibo), CancellationToken.None);
        retiro.Result.Should().Be(ResultadoDelContrato.Rejected);
        var traslado = await contrato.TrasladarAsync(new SolicitudDeTraslado(Prueba, "TST:" + Guid.NewGuid().ToString("N"), ahorro.PublicId, aportes.PublicId, 10m,
            Hoy, [], Recibo), CancellationToken.None);
        traslado.Code.Should().Be(ContratoDeCuentasSinActivar.CodigoAportesNoActivados);
        (await contrato.ReversarAsync(new SolicitudDeReversoDeCuenta(Prueba, "TST:x", "cheque devuelto"), CancellationToken.None)).Result
            .Should().Be(ResultadoDelContrato.Rejected);

        _db.MemberAccountOperations.Local.Should().BeEmpty("sin activar no escribe nada");
        _db.MemberAccountTransactions.Local.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------------------------- T041: aportes v2 --

    [Fact]
    public async Task La_consulta_de_aportes_v2_suma_dos_campos_que_la_v1_ve_en_cero()
    {
        IConsultaDeAportes.VersionDelContrato.Should().Be(2);
        new SaldoDeAportesDto(Guid.NewGuid(), 1m, 2m, Hoy).Should().BeEquivalentTo(new { Minimum = 0m, InstallmentsInArrears = 0 },
            o => o.ExcludingMissingMembers());
        (await new ConsultaDeAportesNoDisponible().SaldosAsync(new SolicitudDeSaldoDeAportes([Guid.NewGuid()], Hoy), CancellationToken.None))
            .Should().BeSameAs(SaldosDeAportesDto.NoDisponible);
    }

    // ------------------------------------------------------------------------------------ T042: registro y Tesorería --

    [Fact]
    public void El_registro_conoce_el_contrato_de_cuentas_y_Tesoreria_tiene_su_clave_y_sus_medios()
    {
        ContratosDeIntegracion.VersionesSoportadas(ContratosDeIntegracion.ProveedorDeCuentas).Should().Equal(IContratoDeCuentas.VersionDelContrato);
        ContratosDeIntegracion.VersionesSoportadas(ContratosDeIntegracion.ProveedorDeObligaciones).Should().Equal(1);
        ContratosDeIntegracion.ProveedorDeCuentas.Should().Be("ProveedorDeCuentas");

        var clave = $"TES:{Guid.NewGuid():N}:{Guid.NewGuid():N}";
        GramaticaDeClaves.Cumple(ClaseDeRecaudador.Treasury, clave).Should().BeTrue();
        GramaticaDeClaves.Cumple(ClaseDeRecaudador.Treasury, "TES:123").Should().BeFalse();
        GramaticaDeClaves.Cumple(ClaseDeRecaudador.Treasury, $"REC:{Guid.NewGuid():N}").Should().BeFalse();

        ClasesDeMedioDeRecaudo.Admite(ClaseDeRecaudador.Treasury, ClaseDeMedioDeRecaudo.Transfer).Should().BeTrue();
        ClasesDeMedioDeRecaudo.Admite(ClaseDeRecaudador.Treasury, ClaseDeMedioDeRecaudo.BankDeposit).Should().BeTrue();
        ClasesDeMedioDeRecaudo.Admite(ClaseDeRecaudador.Treasury, ClaseDeMedioDeRecaudo.Cash).Should().BeFalse();
        ClasesDeMedioDeRecaudo.Admite(ClaseDeRecaudador.Treasury, ClaseDeMedioDeRecaudo.CustomerCredit).Should().BeFalse();
    }

    // ------------------------------------------------------------------------------ T043: productos de captación --

    [Fact]
    public async Task Sin_cartera_habilitada_los_productos_responden_vacios()
    {
        var respuesta = await new ConsultasDeCarteraNoHabilitada().ProductosDeCaptacionAsync(new ConsultaDeProductosDeCaptacion([Guid.NewGuid()], Hoy),
            CancellationToken.None);
        respuesta.Should().BeSameAs(ProductosDeCaptacionDto.NoHabilitada);
        respuesta.People.Should().BeEmpty();
    }

    [Fact]
    public async Task Los_productos_salen_en_bloque_con_su_papel_y_su_saldo_del_libro()
    {
        var titular = new Person { FirstName = "Ana", LastName = "Ruiz", TaxId = "900", IdType = "CC" };
        var cotitular = new Person { FirstName = "Luis", LastName = "Ruiz", TaxId = "901", IdType = "CC" };
        _db.People.AddRange(titular, cotitular);
        _db.CreditLines.AddRange(CreditLine.Nueva("AHO", "Ahorro a la vista", null, LineNature.Deposit), CreditLine.Nueva("APO", "Aportes", null, LineNature.Contribution));
        _db.SaveChanges();
        var lineaAhorro = _db.CreditLines.Local.Single(l => l.Code == "AHO").Id;
        var lineaAportes = _db.CreditLines.Local.Single(l => l.Code == "APO").Id;
        _db.LendingActivations.Add(new LendingActivation { MigrationLoadId = 1, Nature = LineNature.Contribution });

        var ahorro = Cuenta(titular.Id, LineNature.Deposit, DepositClass.Demand, lineaAhorro, "AH-1");
        var aportes = Cuenta(titular.Id, LineNature.Contribution, null, lineaAportes, "AP-1");
        var cerrada = Cuenta(titular.Id, LineNature.Deposit, DepositClass.Demand, lineaAhorro, "AH-2");
        cerrada.Cerrar(Hoy, 5, "desiste", Ahora);
        _db.MemberAccountParties.Add(MemberAccountParty.Nueva(ahorro, cotitular.Id, MemberAccountPartyRole.CoHolder, Hoy, 0.5m));
        void Fila(MemberAccount c, MemberAccountConcept concepto, decimal monto, DateOnly? dia = null) => _db.MemberAccountTransactions.Add(new MemberAccountTransaction
        {
            MemberAccountId = c.Id, OperationId = 1, Kind = MemberAccountTransactionKind.Deposit, Concept = concepto, Amount = monto,
            ValueDate = dia ?? Hoy, AccountingDate = Hoy, RegisteredAt = Ahora,
        });
        Fila(ahorro, MemberAccountConcept.Principal, 500_000m);
        Fila(ahorro, MemberAccountConcept.InterestPayable, 12.5m);
        Fila(ahorro, MemberAccountConcept.Principal, 1m, Hoy.AddDays(1));
        Fila(aportes, MemberAccountConcept.ContributionMinimum, 300_000m);
        Fila(aportes, MemberAccountConcept.ContributionOrdinary, 40_000m);
        _db.MemberAccountDailyPositions.Add(new MemberAccountDailyPosition
        {
            MemberAccountId = ahorro.Id, PositionDate = Hoy, Version = 1, RegisteredAt = Ahora, AvailableBalance = 450_000m, InputsSha256 = "h",
        });
        _db.SaveChanges();

        var respuesta = await ProductosDeCaptacionDeCartera.ConsultarAsync(_db,
            new ConsultaDeProductosDeCaptacion([titular.PublicId, cotitular.PublicId, Guid.NewGuid()], Hoy), CancellationToken.None);

        respuesta.DepositsStatus.Should().Be(EstadoDeCartera.NotActivated);
        respuesta.ContributionsStatus.Should().Be(EstadoDeCartera.Active, "aportes tiene su activación");
        respuesta.People.Should().HaveCount(3);

        var deAna = respuesta.People[0];
        deAna.HasLiveProducts.Should().BeTrue();
        deAna.Products.Select(p => p.Number).Should().Equal("AH-1", "AP-1");
        deAna.Products[0].Balance.Should().Be(500_012.5m, "capital + interés por pagar a la fecha; lo de mañana no cuenta");
        deAna.Products[0].Available.Should().Be(450_000m, "el disponible es el de la última posición");
        deAna.Products[0].Role.Should().Be(RolEnLaCuenta.Holder);
        deAna.Products[1].Balance.Should().Be(340_000m);
        deAna.Products[1].Nature.Should().Be(NaturalezaDeCuenta.Contribution);
        deAna.Products[1].DepositClass.Should().BeNull();

        var deLuis = respuesta.People[1];
        deLuis.Products.Should().ContainSingle().Which.Role.Should().Be(RolEnLaCuenta.CoHolder);
        deLuis.HasLiveProducts.Should().BeTrue("el cotitular también tiene el producto vivo");

        respuesta.People[2].Products.Should().BeEmpty();
        respuesta.People[2].HasLiveProducts.Should().BeFalse();

        var conCerradas = await ProductosDeCaptacionDeCartera.ConsultarAsync(_db,
            new ConsultaDeProductosDeCaptacion([titular.PublicId], Hoy, IncludeClosed: true, IncludeAsParty: false), CancellationToken.None);
        conCerradas.People[0].Products.Select(p => p.Number).Should().Equal("AH-1", "AH-2", "AP-1");
        conCerradas.People[0].Products[1].ClosedOn.Should().Be(Hoy);
    }
}
