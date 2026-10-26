using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Core.PersonBankAccounts;
using IngenIA365ERP.Application.Treasury.Accounting;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.LiquidityFund;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Application.Treasury.Results;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury;

/// <summary>
/// El escenario de las pruebas de la Tesorería nueva (feature 015, fases 7 a 9, T0127–T0131, T0152, T0153, T0164) sobre <see cref="EscenarioDeRecaudo"/> (oficinas,
/// usuarios, reloj, lector de parámetros): un banco con código ACH y otro sin él, la cuenta de pagos <c>PAGOS</c> y la del fondo <c>FONDO</c>, una beneficiaria con
/// su cuenta registrada y activa, el cerrojo simulado (crea la fila de numeración como el real), Contabilidad simulada (vinculada y sin iniciar), las listas sin
/// coincidencias y el motor de aprobaciones simulado. Las pruebas se escriben ahora y se corren al final del plan.
/// </summary>
internal sealed class EscenarioDeTesoreria
{
    public EscenarioDeRecaudo E { get; } = new();
    public ICerrojoDeTesoreria Cerrojo { get; } = Substitute.For<ICerrojoDeTesoreria>();
    public IContabilidadParaTesoreria Contabilidad { get; } = Substitute.For<IContabilidadParaTesoreria>();
    public IListasDeTesoreria Listas { get; } = Substitute.For<IListasDeTesoreria>();
    public IMotorDeAprobaciones Motor { get; } = Substitute.For<IMotorDeAprobaciones>();
    public IMensajesEntrantes Entrantes { get; } = Substitute.For<IMensajesEntrantes>();
    public AlcanceDeTesoreriaFijo Alcance { get; } = new(AlcanceDeTesoreria.Todo);

    public NumeradorDeTesoreria Numerador => new(E.Db, Cerrojo);
    public ParametrosDeTesoreria Parametros => new(E.Lector);
    public CuentasBancariasDePersona CuentasDePersona => new(E.Db, E.Lector, E.Reloj);
    public ValidadorDeOrdenes Validador => new(E.Db, CuentasDePersona, Listas, E.Alertas, E.Reloj);
    public GuardaDelFondoDeLiquidez Fondo => new(E.Db);
    public IResultadoDeOrdenesDeRecaudo Recaudo { get; } = Substitute.For<IResultadoDeOrdenesDeRecaudo>();
    public EmisorDeMensajes Emisor => _emisor ??= new EmisorDeMensajes(E.Db, E.Actor, E.Reloj);
    public EmisionDeTesoreria Emision => new(E.Db, Emisor, Contabilidad, E.Lector, E.Actor);
    public RegistroDeResultadosDelGiro Resultados => new(E.Db, E.Reloj, Emisor, Emision, Parametros, [Recaudo], Numerador);
    public SegregacionDeTesoreria Segregacion => new(E.Personas);
    public AprobacionDiferidaEnTesoreria Aprobacion => new(E.Db, Motor, E.Actor);

    private EmisorDeMensajes? _emisor;

    public Bank Banco { get; }
    public Bank BancoSinCodigo { get; }
    public OwnBankAccount Pagos { get; }
    public OwnBankAccount FondoDeLiquidez { get; }
    public Person Beneficiaria { get; }
    public PersonBankAccount CuentaDeLaBeneficiaria { get; }

    public EscenarioDeTesoreria()
    {
        Cerrojo.When(c => c.AsegurarNumeracionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())).Do(ci =>
        {
            E.Db.Sequences.Add(new Sequence { Name = ci.ArgAt<string>(0), ShortName = ci.ArgAt<string>(1), DocumentType = CatalogoDeTesoreria.Modulo, NextSequence = 1 });
            E.Db.SaveChanges();
        });
        Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null));
        Contabilidad.VinculoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new VinculoDto(ci.ArgAt<Guid>(0), Guid.NewGuid(), "11100501", EscenarioDeRecaudo.Hoy.AddYears(-1)));
        Contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<CombinacionDeHecho>>(), Arg.Any<CancellationToken>())
            .Returns(new EvaluacionContableDto(true, "SinContabilidad", []));
        Contabilidad.VincularCuentaBancariaAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        Listas.EvaluarAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns(ResultadoDeListasDeTesoreria.Limpio);
        Motor.SolicitarAsync(Arg.Any<SolicitudDeAprobacion>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result.Success<ApprovalRequest?>(new ApprovalRequest { SourcePublicId = ci.ArgAt<SolicitudDeAprobacion>(0).SourcePublicId }));

        Banco = new Bank { Name = "Banco de pruebas", TransferCode = "1007", LegacyCode = "07" };
        BancoSinCodigo = new Bank { Name = "Banco sin código" };
        E.Db.Banks.AddRange(Banco, BancoSinCodigo);
        Beneficiaria = new Person { FirstName = "Berta", LastName = "Beneficiaria", TaxId = "2001", IdType = "C" };
        E.Db.People.Add(Beneficiaria);
        E.Db.SaveChanges();

        Pagos = new OwnBankAccount
        {
            Code = "PAGOS", Name = "Cuenta de pagos", BankId = Banco.Id, AccountNumber = "4000123456", BranchId = E.OficinaA.Id,
            Purposes = BankAccountPurpose.Payments | BankAccountPurpose.Collection, ValidFrom = EscenarioDeRecaudo.Hoy.AddYears(-1),
        };
        FondoDeLiquidez = new OwnBankAccount
        {
            Code = "FONDO", Name = "Fondo de liquidez", BankId = Banco.Id, AccountNumber = "4000999999", BranchId = E.OficinaA.Id,
            Purposes = BankAccountPurpose.LiquidityFund, ValidFrom = EscenarioDeRecaudo.Hoy.AddYears(-1),
        };
        E.Db.OwnBankAccounts.AddRange(Pagos, FondoDeLiquidez);
        CuentaDeLaBeneficiaria = new PersonBankAccount
        {
            PersonId = Beneficiaria.Id, BankId = Banco.Id, AccountType = "Savings", AccountNumber = "123456789", HolderName = "Berta Beneficiaria",
            HolderTaxId = "2001", Status = PersonBankAccountState.Active, RequestedByName = "test", DecidedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PayableFrom = EscenarioDeRecaudo.Hoy.AddYears(-1),
        };
        E.Db.PersonBankAccounts.Add(CuentaDeLaBeneficiaria);
        E.Db.SaveChanges();
    }

    /// <summary>Una orden en la bandeja, ya validada como <paramref name="estado"/>.</summary>
    public PaymentOrder Orden(decimal neto = 1_000_000m, PaymentOrderState estado = PaymentOrderState.Ready, PaymentForm forma = PaymentForm.Transfer,
        PaymentOrderOrigin origen = PaymentOrderOrigin.LendingDisbursement, string clase = "LoanDisbursement", string? numero = null)
    {
        var orden = new PaymentOrder
        {
            Number = numero ?? $"OP-{E.Db.PaymentOrders.Count() + 1:000}", Origin = origen, OriginOrderPublicId = Guid.NewGuid(), OriginOrderNumber = "DO-1",
            OriginMessagePublicId = Guid.NewGuid(), OrderKind = clase, Form = forma, BeneficiaryPersonId = Beneficiaria.Id, BeneficiaryName = "Berta Beneficiaria",
            BeneficiaryTaxId = "2001", DestinationBankCode = "1007", DestinationBankId = Banco.Id, DestinationAccountNumber = "123456789",
            DestinationAccountType = "Savings", GrossAmount = neto, NetAmount = neto, PayDate = EscenarioDeRecaudo.Hoy, Concept = "Desembolso", Status = estado,
            ReceivedAt = new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc),
        };
        E.Db.PaymentOrders.Add(orden);
        E.Db.SaveChanges();
        return orden;
    }

    /// <summary>Un mensaje que <see cref="IMensajesEntrantes"/> devuelve para el destino <c>Treasury</c>.</summary>
    public Guid Mensaje(object contenido, string tipo, int version, Guid? persona = null, UserRefV1? quien = null)
    {
        var id = Guid.NewGuid();
        var sobre = new IntegrationEnvelopeV1
        {
            MessageId = id, Type = tipo, Version = version, Kind = IntegrationMessageKind.Business, OriginModule = "CAR", PersonPublicId = persona,
            BranchPublicId = E.OficinaA.PublicId, OriginUser = quien ?? new UserRefV1 { CentralUserId = Guid.NewGuid(), Name = "Persona de Cartera" },
        };
        Entrantes.LeerAsync(Arg.Is<IReadOnlyCollection<Guid>>(l => l.Contains(id)), IntegrationDestinations.Treasury, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MensajeEntrante>>(
            [
                new MensajeEntrante(sobre, contenido, "{}", "sha", null,
                    new EntregaEntrante(IntegrationDestinations.Treasury, DeliveryMode.Always, DeliveryStatus.Pending, null, null, null, 0, null)),
            ]));
        return id;
    }

    /// <summary>Una orden de giro v1 de Cartera a la beneficiaria.</summary>
    public OrdenDeGiroEmitidaV1 OrdenDeGiro(decimal neto = 1_000_000m, string forma = "Transfer", bool porArchivo = false, Guid? reemplaza = null) => new()
    {
        OrderPublicId = Guid.NewGuid(), OrderNumber = "DO-9", Loan = new LoanRefV1 { LoanPublicId = Guid.NewGuid(), LoanNumber = "CR-1" }, Form = forma,
        Payee = new PayeeV1
        {
            PersonPublicId = Beneficiaria.PublicId, TaxIdType = "C", TaxId = "2001", Name = "Berta Beneficiaria", BankCode = "1007",
            BankAccountNumber = "123456789", BankAccountType = "Savings",
        },
        GrossAmount = neto, NetAmount = neto, PayDate = EscenarioDeRecaudo.Hoy, Concept = "CR-1", PaidByFile = porArchivo, ReplacesOrderPublicId = reemplaza,
    };
}

/// <summary>El alcance fijo de Tesorería de las pruebas. (nuevo)</summary>
internal sealed class AlcanceDeTesoreriaFijo(AlcanceDeTesoreria inicial) : IAlcanceDeTesoreria
{
    public AlcanceDeTesoreria Valor { get; set; } = inicial;

    public Task<AlcanceDeTesoreria> ObtenerAsync(CancellationToken ct = default) => Task.FromResult(Valor);
}
