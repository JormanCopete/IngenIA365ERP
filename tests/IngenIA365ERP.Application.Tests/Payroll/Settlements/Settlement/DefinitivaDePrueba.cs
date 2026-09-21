using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Payments.Services;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Settlement;
using IngenIA365ERP.Application.Payroll.Terminations;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Lending;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Settlement;

/// <summary>
/// El escenario de la definitiva en pruebas (feature 010, US3): Ana con ocho meses aprobados de 2026
/// (provisiones y bases), septiembre abierto donde cae el retiro del 15-09-2026, el catálogo de
/// motivos sembrado, dos créditos vivos en Cartera —con su línea de crédito, sus cuotas pendientes y
/// el tipo <c>RC</c>— y una libranza con cuotas causadas sin descontar. Cartera es <b>real</b>: la
/// aprobación recauda por <see cref="RecaudoDeCredito"/> (cuota a cuota, transacción RC y comprobante
/// por el contrato), no por un sustituto. Hasta el 2026-09-21 el <c>ISender</c> respondía éxito a todo
/// <c>ProcessPaymentCommand</c> y el recaudo nunca se había ejecutado con datos en ninguna prueba.
/// </summary>
public sealed class DefinitivaDePrueba
{
    public static readonly DateOnly Retiro = new(2026, 9, 15);
    public static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    public NominaTestData D { get; }
    public PayPeriod Septiembre { get; }
    public LoanPortfolio Credito1001 { get; private set; } = null!;
    public LoanPortfolio Credito1002 { get; private set; } = null!;
    public PayrollRecurringNovelty Libranza { get; private set; } = null!;
    public CreditLineParameter LineaDeCredito { get; private set; } = null!;
    public ISender Sender { get; }
    public ISettlementDocumentRenderer Renderer { get; }
    public ICurrentTenantService Tenant { get; }

    /// <summary>Cuentas de la línea de crédito (cartera, ingreso por intereses, mora): se crean con la contabilidad, habilitadas para Cartera.</summary>
    public const string CuentaCartera = "140405";
    public const string CuentaInteresesCartera = "410205";
    public const string CuentaMoraCartera = "410210";

    public DefinitivaDePrueba(bool conCartera = true, decimal saldo1001 = 1_500_000m)
    {
        D = LiquidacionDePrueba.ConPrimerSemestre();
        D.MesAprobado(2026, 7, D.Ana, 2_000_000m, 249_095m, 187_424m, 187_424m, 22_491m, 83_333m);
        D.MesAprobado(2026, 8, D.Ana, 2_000_000m, 249_095m, 187_424m, 187_424m, 22_491m, 83_333m);
        Septiembre = D.Periodo(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), PayPeriodStatus.Open);
        D.HoyEs(new DateTime(2026, 9, 20, 12, 0, 0));
        D.Db.TerminationReasons.AddRange(TerminationReasonsSeeder.Catalogo());
        D.Db.SaveChanges();

        if (conCartera)
        {
            // La línea de crédito con sus cuentas por código (feature 009, R16): el recaudo las resuelve para Cartera.
            LineaDeCredito = new CreditLineParameter
            {
                CreditLineId = 1, Description = "Libre inversión", AccountCode = CuentaCartera, AccountInterestIncome = CuentaInteresesCartera,
                AccountInterestDefault = CuentaMoraCartera, CreatedBy = "test",
            };
            D.Db.CreditLineParameters.Add(LineaDeCredito);
            D.Db.SaveChanges();
            // En Cartera CurrentBalance es el saldo de capital (el recaudo sólo le resta el capital pagado); las cuotas
            // pendientes son de capital puro para que «bajó exactamente lo aplicado» sea aritmética exacta.
            Credito1001 = new LoanPortfolio { PersonId = D.Ana.PersonId, CreditLineId = LineaDeCredito.Id, PortfolioNumber = 1001, CurrentBalance = saldo1001, CapitalBalanceCurrent = saldo1001, InstallmentAmount = 250_000m, PendingInstallmentCount = 6, DisbursementDate = new DateOnly(2026, 1, 10), CreatedBy = "test" };
            Credito1002 = new LoanPortfolio { PersonId = D.Ana.PersonId, CreditLineId = LineaDeCredito.Id, PortfolioNumber = 1002, CurrentBalance = 300_000m, CapitalBalanceCurrent = 300_000m, InstallmentAmount = 100_000m, PendingInstallmentCount = 3, DisbursementDate = new DateOnly(2026, 5, 10), CreatedBy = "test" };
            D.Db.LoanPortfolios.AddRange(Credito1001, Credito1002);
            D.Db.SaveChanges();
            var persona = D.Db.People.Single(x => x.Id == D.Ana.PersonId);
            var codigoPersona = persona.LegacyCode ?? persona.TaxId;
            CuotasPendientes(Credito1001, codigoPersona, 6, saldo1001);
            CuotasPendientes(Credito1002, codigoPersona, 3, 300_000m);
            // Libranza: cuota de 50.000 desde junio, cuatro cuotas causadas (junio a septiembre), dos descontadas.
            Libranza = new PayrollRecurringNovelty
            {
                EmployeeId = D.Ana.Id, ConceptCode = SettlementInputLoader.LibranzaCode, Amount = 50_000m, StartDate = new DateTime(2026, 6, 1),
                TotalInstallments = 12, InstallmentsIssued = 2, Notes = "Coopcentral", CreatedBy = "test",
            };
            D.Db.PayrollRecurringNovelties.Add(Libranza);
            D.Db.SaveChanges();
        }

        Sender = Substitute.For<ISender>();
        Renderer = Substitute.For<ISettlementDocumentRenderer>();
        Renderer.Render(Arg.Any<SettlementDocumentModel>()).Returns([1, 2, 3]);
        Tenant = Substitute.For<ICurrentTenantService>();
        Tenant.TenantName.Returns("Coop. Prueba");
    }

    /// <summary>
    /// Contabilidad iniciada con cuentas para todos los conceptos, septiembre abierto, el tipo <c>RC</c> de
    /// Cartera y las cuentas de la línea de crédito. La cuenta débito de <c>DESC_CARTERA</c> es la «caja» del
    /// recaudo y el contrato la exige habilitada para Cartera: en producción hay que habilitarla igual.
    /// </summary>
    public void ConContabilidad()
    {
        D.ConfigurarContabilidad();
        D.PeriodoContable(2026, 9);
        D.Db.VoucherTypes.Add(new VoucherType { Code = "RC", Name = "Recaudo", Usage = VoucherUsage.Module, ModuleCode = ModuloContable.Cartera, NextNumber = 1, IsSeeded = true, CreatedBy = "test" });
        D.Db.CrossDocumentTypes.Add(new CrossDocumentType { Code = "PG", Name = "Pagaré", IsActive = true, IsSeeded = true, CreatedBy = "test" });
        D.Cuenta(CuentaCartera, "Créditos de consumo", AccountNature.Debit, modulos: AccountingModules.Accounting | AccountingModules.Lending);
        D.Cuenta(CuentaInteresesCartera, "Intereses de cartera", AccountNature.Credit, modulos: AccountingModules.Accounting | AccountingModules.Lending);
        D.Cuenta(CuentaMoraCartera, "Intereses de mora", AccountNature.Credit, modulos: AccountingModules.Accounting | AccountingModules.Lending);
        var cuentaDelRecaudo = (from a in D.Db.PayrollConceptDefinitionAccounts join c in D.Db.ChartOfAccounts on a.DebitAccountId equals c.Id
                                where a.ConceptCode == WellKnownConceptCodes.LoanDeduction select c).Single();
        cuentaDelRecaudo.EnabledModules |= AccountingModules.Lending;
        D.Db.SaveChanges();
    }

    /// <summary>El recaudo real de Cartera sobre el mismo contexto, con el usuario que aprueba.</summary>
    public RecaudoDeCredito Recaudo(ICurrentUserService quien) =>
        new(D.Db, D.Clock, quien, new AccountingPoster(D.Db, D.Clock, quien, D.Alcance), new AccountEligibility(D.Db));

    /// <summary>Las cuotas pendientes de un crédito, de capital puro, en partes iguales (la última lleva el redondeo).</summary>
    private void CuotasPendientes(LoanPortfolio credito, string codigoPersona, int cuotas, decimal saldo)
    {
        var parte = Math.Round(saldo / cuotas, 2);
        for (var n = 1; n <= cuotas; n++)
        {
            var capital = n == cuotas ? saldo - parte * (cuotas - 1) : parte;
            D.Db.PendingInstallments.Add(new PendingInstallment
            {
                PersonCode = codigoPersona, CreditLineId = credito.CreditLineId, PortfolioNumber = credito.PortfolioNumber, AccrualPeriod = 202609 + n,
                AccruedCapital = capital, BalanceCapital = capital, TotalBalance = capital, TotalInstallment = capital, CreatedBy = "test",
            });
        }
        D.Db.SaveChanges();
    }

    public RegisterTerminationCommandHandler Registrar() => new(D.Db, D.SettlementLoader, D.Persistidor(), D.Clock, D.User, D.AuditEmitter);

    public AdjustSettlementDeductionCommandHandler Ajustar() => new(D.Db, D.Clock, D.User, D.AuditEmitter);

    public RecalculateSettlementCommandHandler Recalcular() => new(D.Db, D.SettlementLoader, D.Persistidor(), D.Clock, D.User, D.AuditEmitter);

    public ApproveSettlementCommandHandler Aprobar(ICurrentUserService? quien = null)
    {
        quien ??= Contadora;
        return new(D.Db, D.Flujo(quien), Recaudo(quien), Sender, D.Clock, quien, Tenant, Renderer,
            new PayrollAuditEmitter(D.Audit, quien, D.Clock, NullLogger<PayrollAuditEmitter>.Instance), NullLogger<ApproveSettlementCommandHandler>.Instance);
    }

    public ReverseSettlementCommandHandler Reversar(ICurrentUserService? quien = null)
    {
        quien ??= Contadora;
        return new(D.Db, D.Flujo(quien), D.Clock, quien, new PayrollAuditEmitter(D.Audit, quien, D.Clock, NullLogger<PayrollAuditEmitter>.Instance));
    }

    public DiscardSettlementCommandHandler Descartar() => new(D.Db, D.Flujo(D.User), D.Clock, D.User);

    /// <summary>Registra la terminación de Ana con el motivo indicado (por defecto despido sin justa causa, término fijo hasta el 31-12-2026).</summary>
    public async Task<TerminationRegisteredDto> RegistrarAnaAsync(string motivo = "DESP_SINJC", DianContractType contrato = DianContractType.FixedTerm, DateOnly? finContrato = null)
    {
        var r = await Registrar().Handle(new RegisterTerminationCommand(D.Ana.PublicId, Retiro, motivo, contrato,
            finContrato ?? (contrato is DianContractType.FixedTerm or DianContractType.WorkOrLabor ? new DateOnly(2026, 12, 31) : null)), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        return r.Value;
    }

    public SettlementDeductionItemDto Descuento(TerminationRegisteredDto t, long numeroDeCredito) =>
        t.Deductions.Items.Single(i => i.Description.Contains($"Crédito {numeroDeCredito}"));

    public SettlementDeductionItemDto LibranzaDe(TerminationRegisteredDto t) =>
        t.Deductions.Items.Single(i => i.Kind == SettlementDeductionKind.ThirdPartyLibranza);
}
