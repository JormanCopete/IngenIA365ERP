using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Settlement;
using IngenIA365ERP.Application.Payroll.Terminations;
using IngenIA365ERP.Application.Tests.Common.Integration.Lending;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Settlement;

/// <summary>
/// El escenario de la definitiva en pruebas (feature 010, US3): Ana con ocho meses aprobados de 2026
/// (provisiones y bases), septiembre abierto donde cae el retiro del 15-09-2026, el catálogo de
/// motivos sembrado, dos obligaciones vivas en Cartera y una libranza con cuotas causadas sin descontar.
/// Feature 013 (T0106, R118): Cartera se ve <b>sólo por sus puertos</b>: la propuesta sale de
/// <see cref="ConsultaDeObligacionesDePrueba"/> y la aprobación recauda por <see cref="ContratoDeObligacionesDePrueba"/>
/// (registra cada lote y responde lo que la prueba configure). Ya no hay <c>LoanPortfolio</c> ni <c>RecaudoDeCredito</c>.
/// </summary>
public sealed class DefinitivaDePrueba
{
    public static readonly DateOnly Retiro = new(2026, 9, 15);
    public static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    public NominaTestData D { get; }
    public PayPeriod Septiembre { get; }
    public ObligacionDto Credito1001 { get; private set; } = null!;
    public ObligacionDto Credito1002 { get; private set; } = null!;
    public PayrollRecurringNovelty Libranza { get; private set; } = null!;
    public Guid PersonaDeAna { get; }
    public ConsultaDeObligacionesDePrueba Obligaciones => D.Obligaciones;
    public ContratoDeObligacionesDePrueba Contrato => D.Contrato;
    public ISender Sender { get; }
    public ISettlementDocumentRenderer Renderer { get; }
    public ICurrentTenantService Tenant { get; }

    public DefinitivaDePrueba(bool conCartera = true, decimal saldo1001 = 1_500_000m)
    {
        D = LiquidacionDePrueba.ConPrimerSemestre();
        D.MesAprobado(2026, 7, D.Ana, 2_000_000m, 249_095m, 187_424m, 187_424m, 22_491m, 83_333m);
        D.MesAprobado(2026, 8, D.Ana, 2_000_000m, 249_095m, 187_424m, 187_424m, 22_491m, 83_333m);
        Septiembre = D.Periodo(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), PayPeriodStatus.Open);
        D.HoyEs(new DateTime(2026, 9, 20, 12, 0, 0));
        D.Db.TerminationReasons.AddRange(TerminationReasonsSeeder.Catalogo());
        D.Db.SaveChanges();

        PersonaDeAna = D.Db.People.Single(x => x.Id == D.Ana.PersonId).PublicId;
        if (conCartera)
        {
            // Dos obligaciones vivas como deudora: saldo total para cancelar y exigible a la fecha (la cuota del ciclo).
            Credito1001 = Obligaciones.Agregar(PersonaDeAna, ConsultaDeObligacionesDePrueba.Obligacion("1001", saldo1001, exigible: 250_000m, cuota: 250_000m));
            Credito1002 = Obligaciones.Agregar(PersonaDeAna, ConsultaDeObligacionesDePrueba.Obligacion("1002", 300_000m, exigible: 100_000m, cuota: 100_000m));
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
    /// Contabilidad iniciada con cuentas para todos los conceptos y septiembre abierto. Desde la enmienda D-08 (feature 013)
    /// <c>DESC_CARTERA</c> contabiliza en la definitiva contra la cuenta del concepto, como cualquier descuento.
    /// </summary>
    public void ConContabilidad()
    {
        D.ConfigurarContabilidad();
        D.PeriodoContable(2026, 9);
        D.Db.SaveChanges();
    }

    public RegisterTerminationCommandHandler Registrar() => new(D.Db, D.SettlementLoader, D.Persistidor(), D.Clock, D.User, D.AuditEmitter);

    public AdjustSettlementDeductionCommandHandler Ajustar() => new(D.Db, D.Recaudo, D.Clock, D.User, D.AuditEmitter);

    public RecalculateSettlementCommandHandler Recalcular() => new(D.Db, D.SettlementLoader, D.Persistidor(), D.Clock, D.User, D.AuditEmitter);

    public ApproveSettlementCommandHandler Aprobar(ICurrentUserService? quien = null)
    {
        quien ??= Contadora;
        return new(D.Db, D.Flujo(quien), D.PropuestaDeCartera, D.Recaudo, D.Policies, Sender, D.Clock, quien, Tenant, Renderer,
            new PayrollAuditEmitter(D.Audit, quien, D.Clock, NullLogger<PayrollAuditEmitter>.Instance), D.StaleMarker, NullLogger<ApproveSettlementCommandHandler>.Instance);
    }

    public ReverseSettlementCommandHandler Reversar(ICurrentUserService? quien = null)
    {
        quien ??= Contadora;
        return new(D.Db, D.Flujo(quien), D.Recaudo, D.Clock, quien, new PayrollAuditEmitter(D.Audit, quien, D.Clock, NullLogger<PayrollAuditEmitter>.Instance));
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

    public SettlementDeductionItemDto Descuento(TerminationRegisteredDto t, string numeroDeCredito) =>
        t.Deductions.Items.Single(i => i.Description.Contains($"Crédito {numeroDeCredito}"));

    public SettlementDeductionItemDto LibranzaDe(TerminationRegisteredDto t) =>
        t.Deductions.Items.Single(i => i.Kind == SettlementDeductionKind.ThirdPartyLibranza);
}
