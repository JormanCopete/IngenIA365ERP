using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Statements;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Statements;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.BankItems;

/// <summary>
/// El escenario de las pruebas de la fase 11 (feature 015, US6, T0186–T0189a) sobre <see cref="CajaConContratos"/> (los contratos simulados, la asociada con dos créditos y
/// una cuenta de aportes): un banco con código, la cuenta propia de recaudo <c>RECAUDO</c> de la oficina A, los tipos <c>RB</c> y el puerto real del extracto
/// (<see cref="MovimientosBancariosParaRecaudo"/>) sobre la misma base. Los abonos se siembran directo con <see cref="Abono"/>. Contabilidad no está iniciada y la
/// validación previa contesta «contabilizable». Las pruebas se escriben ahora y se corren al final del plan.
/// </summary>
internal sealed class BancosDePrueba
{
    public CajaConContratos K { get; } = new();
    public IContabilidadParaRecaudo Contabilidad { get; } = Substitute.For<IContabilidadParaRecaudo>();
    public IOrdenesDePagoDeTesoreria Ordenes { get; } = Substitute.For<IOrdenesDePagoDeTesoreria>();
    public Bank Banco { get; }
    public OwnBankAccount Cuenta { get; }
    public BankStatement Extracto { get; }
    public MiscConcept Rendimiento { get; }
    public MiscConcept GiroDevuelto { get; }

    private int _linea;

    public BancosDePrueba()
    {
        var e = K.C.E;
        Contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<CombinacionDeHecho>>(), Arg.Any<CancellationToken>()).Returns(new EvaluacionContableDto(true, "Bloquear", []));
        Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null));
        Banco = new Bank { Name = "Banco de pruebas", TransferCode = "1007" };
        e.Db.Banks.Add(Banco);
        e.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "RB", Name = "Recibo de banco", Kind = ReceiptDocumentKind.BankReceipt });
        Rendimiento = new MiscConcept { Code = "RENDIM", Name = "Rendimientos", Direction = MiscConceptDirection.Income, IsSystem = true };
        GiroDevuelto = new MiscConcept { Code = "GIRODEV", Name = "Giro devuelto", Direction = MiscConceptDirection.Income, IsSystem = true };
        e.Db.MiscConcepts.AddRange(Rendimiento, GiroDevuelto);
        e.Db.SaveChanges();
        Cuenta = new OwnBankAccount
        {
            Code = "RECAUDO", Name = "Cuenta de recaudo", BankId = Banco.Id, AccountNumber = "4000555555", BranchId = e.OficinaA.Id,
            Purposes = BankAccountPurpose.Collection, ValidFrom = EscenarioDeRecaudo.Hoy.AddYears(-1),
        };
        e.Db.OwnBankAccounts.Add(Cuenta);
        e.Db.SaveChanges();
        Extracto = new BankStatement
        {
            Number = "EX000001", BankAccountId = Cuenta.Id, Source = BankStatementSource.GenericTemplate, From = EscenarioDeRecaudo.Hoy.AddDays(-10), To = EscenarioDeRecaudo.Hoy,
            LoadedByUserId = e.Supervisor.Id, LoadedAt = DateTime.UtcNow,
        };
        e.Db.TreasuryBankStatements.Add(Extracto);
        e.Db.SaveChanges();
        Ordenes.EmitirAsync(Arg.Any<SolicitudDeOrdenDePago>(), Arg.Any<CancellationToken>()).Returns(new OrdenDePagoEmitidaDto(Guid.NewGuid(), "OP-000009", "Ready"));
        Ordenes.EstadoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new EstadoDeOrdenDto(ci.ArgAt<Guid>(0), "OP-000007", "Disbursed", EscenarioDeRecaudo.Hoy.AddDays(-5), null));
    }

    /// <summary>Un abono del extracto por <paramref name="valor"/>, <paramref name="diasAtras"/> días antes de hoy.</summary>
    public BankStatementLine Abono(decimal valor, int diasAtras = 1, string? referencia = "REF", string? consignante = null, bool posibleDuplicado = false)
    {
        var l = new BankStatementLine
        {
            BankStatementId = Extracto.Id, BankAccountId = Cuenta.Id, LineNumber = ++_linea, MovementDate = EscenarioDeRecaudo.Hoy.AddDays(-diasAtras),
            Direction = StatementLineDirection.Credit, Amount = valor, Reference = referencia, DepositorTaxId = consignante, Fingerprint = Guid.NewGuid().ToString("N") + "ab",
            RecaudoStatus = StatementLineRecaudoStatus.Pending,
            DuplicateDecision = posibleDuplicado ? StatementLineDuplicateDecision.Pending : null,
        };
        K.C.E.Db.TreasuryBankStatementLines.Add(l);
        K.C.E.Db.SaveChanges();
        return l;
    }

    public AuditoriaDeRecaudo Auditoria => new(K.C.E.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance);
    public AuditoriaDeTesoreria AuditoriaDeTesoreria => new(K.C.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance);
    public MovimientosBancariosParaRecaudo Movimientos => new(K.C.E.Db, AuditoriaDeTesoreria);
    public Application.Receipts.Accounting.EmisionDeRecaudo Emision => new(K.C.E.Db, new Application.Common.Integration.EmisorDeMensajes(K.C.E.Db, K.C.E.Actor, K.C.E.Reloj),
        Contabilidad, K.C.E.Lector, K.C.E.Actor);

    public AplicacionNoInteractiva Aplicacion => new(K.C.E.Db, K.C.E.Actor, K.C.E.Reloj, K.C.Cerrojo, K.C.Numerador, K.Obligaciones, K.Cuentas, K.Listas, Contabilidad, Emision,
        K.C.E.Alertas);

    public PartidasPorIdentificar Partidas => new(K.C.E.Db, K.C.Numerador, Emision, Auditoria, K.C.E.Reloj);

    public IdentifyBankItemCommandHandler Identificar() => new(K.C.E.Db, K.C.E.Alcance, K.C.E.Actor, Movimientos, Aplicacion, Partidas, Auditoria, K.C.E.Reloj);

    public MarkBankItemUnidentifiedCommandHandler DejarComoPartida() => new(K.C.E.Db, K.C.E.Alcance, K.C.E.Actor, Movimientos, K.C.Cerrojo, Partidas, K.C.E.Reloj);

    public ClassifyBankItemCommandHandler Clasificar() =>
        new(K.C.E.Db, K.C.E.Alcance, K.C.E.Actor, Movimientos, Ordenes, K.C.Cerrojo, K.C.Presente, Emision, Auditoria, K.C.E.Alertas, K.C.E.Reloj);

    public DecideBankItemDuplicateCommandHandler Duplicado() => new(K.C.E.Db, K.C.E.Alcance, K.C.E.Actor, Movimientos, K.C.Cerrojo, Auditoria, K.C.E.Reloj);

    public RetryProcessingRunLineCommandHandler Reprocesar() =>
        new(K.C.E.Db, K.C.E.Alcance, K.C.E.Actor, Movimientos, Aplicacion, Partidas, Auditoria, [], K.C.E.Reloj);

    /// <summary>Una cuota del crédito A por <paramref name="valor"/>.</summary>
    public ParteDeAbono CuotaA(decimal valor) => new(ApplicationTarget.Loan, K.Asociado.PublicId, valor, K.CreditoA);

    public ParteDeAbono CuotaB(decimal valor) => new(ApplicationTarget.Loan, K.Asociado.PublicId, valor, K.CreditoB);

    public ParteDeAbono Aportes(decimal valor) => new(ApplicationTarget.Account, K.Asociado.PublicId, valor, K.Aportes);

    /// <summary>Hace que el contrato de obligaciones rechace la aplicación de <paramref name="credito"/>.</summary>
    public void RechazarCredito(Guid credito, string codigo = "Lending.Payment.ObligationNotPayable") =>
        K.Obligaciones.AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>()).Returns(ci => new RespuestaDeAplicacionDto(
            ci.Arg<SolicitudDeAplicacion>().Payments.Select(p => p.ObligationPublicId == credito
                ? new RespuestaDelContratoDto<ComprobanteDeAplicacionDto>(ResultadoDelContrato.Rejected, codigo, "No admite pagos.", null)
                : CajaConContratos.Aplicado(p)).ToList(), []));
}
