using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Application.Receipts.Sessions.Tareas;
using IngenIA365ERP.Application.Receipts.Vaults;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Vaults;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Sessions;

/// <summary>
/// La caja de las pruebas de la fase 5 (feature 015, US2, T0080–T0086) sobre <see cref="EscenarioDeRecaudo"/>: el punto <c>P1</c> de la oficina A con las cajas
/// <c>C1</c> (de la cajera, base 500.000, aviso 2.000.000, bloqueo 3.000.000) y <c>C2</c> (del otro cajero); efectivo (tolerancia 1.000, por denominaciones) y
/// transferencia (tolerancia 0, por referencias) ofrecidos; billetes de 50.000, 20.000, 10.000 y 1.000; los tipos <c>MC</c> y <c>AS</c>; la bóveda de la oficina
/// con el supervisor y el otro cajero de custodios. El cerrojo es simulado (crea la fila de numeración como el real) y Contabilidad no está iniciada: los
/// hechos nacen <c>NotPosted</c>. Las pruebas se escriben ahora y se corren al final del plan.
/// </summary>
internal sealed class CajaDePrueba
{
    public EscenarioDeRecaudo E { get; } = new();
    public ICerrojoDeRecaudo Cerrojo { get; } = Substitute.For<ICerrojoDeRecaudo>();
    public IContabilidadParaRecaudo Contabilidad { get; } = Substitute.For<IContabilidadParaRecaudo>();
    public IMotorDeAprobaciones Motor { get; } = Substitute.For<IMotorDeAprobaciones>();

    public CollectionPoint Punto { get; }
    public CashBox C1 { get; }
    public CashBox C2 { get; }
    public PaymentMeans Efectivo { get; }
    public PaymentMeans Transferencia { get; }
    public CashDenomination B50 { get; }
    public CashDenomination B20 { get; }
    public CashDenomination B10 { get; }
    public CashDenomination B1 { get; }
    public Vault Boveda { get; }

    public CajaDePrueba()
    {
        E.Reloj.AhoraLocal.Returns(new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.FromHours(-5)));
        Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null));
        Contabilidad.SaldoContableAsync(Arg.Any<SolicitudDeSaldo>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<SaldoContableDto>());
        Motor.SolicitarAsync(Arg.Any<SolicitudDeAprobacion>(), Arg.Any<CancellationToken>()).Returns(Result.Success<Domain.Entities.Approvals.ApprovalRequest?>(null));
        Cerrojo.When(c => c.AsegurarNumeracionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())).Do(ci =>
        {
            E.Db.Sequences.Add(new Sequence { Name = ci.ArgAt<string>(0), ShortName = ci.ArgAt<string>(1), DocumentType = CatalogoDeRecaudo.Modulo, NextSequence = 1 });
            E.Db.SaveChanges();
        });

        Punto = E.Punto("P1");
        C1 = E.Caja(Punto, "C1", 500_000, aviso: 2_000_000, bloqueo: 3_000_000);
        C2 = E.Caja(Punto, "C2");
        Efectivo = E.Medio("EF", PaymentMeansClass.Cash, 1_000);
        Transferencia = E.Medio("TR", PaymentMeansClass.Transfer, 0);
        E.Ofrecer(Punto, Efectivo, Transferencia);
        E.AsignarCajero(C1, E.Cajera);
        E.AsignarCajero(C2, E.OtroCajero);
        B50 = E.Denominacion(50_000);
        B20 = E.Denominacion(20_000);
        B10 = E.Denominacion(10_000);
        B1 = E.Denominacion(1_000);
        E.Db.ReceiptDocumentTypes.AddRange(
            new ReceiptDocumentType { Code = "MC", Name = "Movimiento de caja", Kind = ReceiptDocumentKind.CashMovement },
            new ReceiptDocumentType { Code = "AS", Name = "Ajuste de sesión", Kind = ReceiptDocumentKind.SessionAdjustment });
        Boveda = new Vault { BranchId = E.OficinaA.Id };
        E.Db.Vaults.Add(Boveda);
        E.Db.SaveChanges();
        E.Db.VaultCustodians.AddRange(
            new VaultCustodian { VaultId = Boveda.Id, UserId = E.Supervisor.Id, ValidFrom = EscenarioDeRecaudo.Hoy.AddMonths(-1) },
            new VaultCustodian { VaultId = Boveda.Id, UserId = E.OtroCajero.Id, ValidFrom = EscenarioDeRecaudo.Hoy.AddMonths(-1) },
            new VaultCustodian { VaultId = Boveda.Id, UserId = E.Cajera.Id, ValidFrom = EscenarioDeRecaudo.Hoy.AddMonths(-1) });
        E.Db.SaveChanges();
    }

    public NumeradorDeRecaudo Numerador => new(E.Db, Cerrojo);
    public EmisionDeRecaudo Emision => new(E.Db, new EmisorDeMensajes(E.Db, E.Actor, E.Reloj), Contabilidad, E.Lector, E.Actor);
    public ArqueosDeSesion Arqueos => new(E.Db, Emision);
    public AprobacionPresenteEnCaja Presente => new(E.Db, Motor, E.Actor, new ServiceCollection().BuildServiceProvider());

    public OpenCashSessionCommandHandler Abrir() => new(E.Db, E.Alcance, E.Actor, E.Reloj, Cerrojo, Numerador, E.Parametros);
    public GetSessionExpectedQueryHandler Esperado() => new(E.Db, E.Alcance);
    public GetCurrentSessionQueryHandler Actual() => new(E.Db, E.Actor);
    public RegisterCashMovementCommandHandler Mover() => new(E.Db, E.Alcance, E.Actor, E.Reloj, Cerrojo, Numerador, E.Segregacion, Emision);
    public ReclassifyMeansCommandHandler Reclasificar() => new(E.Db, E.Alcance, E.Actor, E.Reloj, Cerrojo, Numerador, Presente, Emision);
    public RegisterClosingCountCommandHandler Contar() => new(E.Db, E.Alcance, E.Actor, E.Reloj, Cerrojo, Arqueos, E.Alertas);
    public ApproveCountDifferenceCommandHandler Aprobar() => new(E.Db, E.Alcance, E.Actor, E.Reloj, Cerrojo, E.Segregacion, Arqueos, E.Alertas);
    public CloseForgottenSessionCommandHandler CerrarOlvidada() => new(E.Db, E.Alcance, E.Actor, E.Reloj, Cerrojo, E.Segregacion, Arqueos, E.Alertas);
    public RegisterSessionAdjustmentCommandHandler Ajustar() => new(E.Db, E.Alcance, E.Actor, E.Reloj, Cerrojo, Numerador, Presente, Emision);
    public RegisterSurpriseCountCommandHandler Sorpresivo() => new(E.Db, E.Alcance, E.Actor, E.Reloj, Cerrojo, Arqueos);
    public CloseCollectionPointDayCommandHandler CerrarDia() => new(E.Db, E.Alcance, E.Actor, E.Reloj, Cerrojo);
    public RegisterVaultMovementCommandHandler MoverBoveda() => new(E.Db, E.Alcance, E.Actor, E.Reloj, Cerrojo, Numerador, E.Segregacion, E.Parametros, Emision, []);
    public AttachPayrollDeductionAuthorizationCommandHandler Autorizar() => new(E.Db, E.Alcance);
    public MoveSurplusToIncomeCommandHandler SobranteAIngreso() => new(E.Db, E.Alcance, E.Actor, E.Reloj, Cerrojo, E.Segregacion, Emision);
    public MarcaDeSesionesOlvidadas Olvidadas() => new(E.Db, E.Parametros, E.Reloj, E.Alertas);
    public CuadreDiarioDeRecaudo Cuadre() => new(E.Db, Contabilidad, E.Alertas);

    public static IReadOnlyList<DenominacionContadaDto> Billetes(params (CashDenomination D, int Cantidad)[] billetes) =>
        billetes.Select(b => new DenominacionContadaDto(b.D.PublicId, b.Cantidad)).ToList();

    /// <summary>Abre la sesión de <paramref name="caja"/> como <paramref name="cajero"/> con 500.000 (10 × 50.000).</summary>
    public async Task<CashSessionDto> AbrirAsync(CashBox? caja = null, User? cajero = null)
    {
        E.Como(cajero ?? E.Cajera);
        var r = await Abrir().Handle(new OpenCashSessionCommand((caja ?? C1).PublicId, Billetes((B50, 10))), default);
        if (r.IsFailure) throw new InvalidOperationException(r.Error.Code + ": " + r.Error.Message);
        return r.Value;
    }

    /// <summary>Lleva a la bóveda billetes que vuelven del banco, con el supervisor y el otro cajero de custodios.</summary>
    public async Task SurtirBovedaAsync(params (CashDenomination D, int Cantidad)[] billetes)
    {
        E.Como(E.Supervisor);
        var r = await MoverBoveda().Handle(new RegisterVaultMovementCommand(Boveda.PublicId, VaultMovementKind.RemittanceIn, Billetes(billetes), "Fondo inicial",
            E.OtroCajero.PublicId), default);
        if (r.IsFailure) throw new InvalidOperationException(r.Error.Code + ": " + r.Error.Message);
    }

    /// <summary>Lo contado de un medio por total.</summary>
    public ConteoDeMedioDto Total(PaymentMeans medio, decimal total, string? motivo = null) => new(medio.PublicId, Total: total, Reason: motivo);

    /// <summary>Lo contado del efectivo por denominaciones.</summary>
    public ConteoDeMedioDto EnBilletes(string? motivo, params (CashDenomination D, int Cantidad)[] billetes) =>
        new(Efectivo.PublicId, Denominations: Billetes(billetes), Reason: motivo);
}
