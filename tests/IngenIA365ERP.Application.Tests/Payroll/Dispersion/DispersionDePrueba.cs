using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Dispersion;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Dispersion;

/// <summary>
/// El escenario de la US8 (quickstart §3.8, contracts/archivos.md §2.2): una prima aprobada con
/// tres empleados —Ana y Carlos con cuenta, Beatriz sin cuenta—, dos bancos con código ACH y el
/// formato <c>DEMO-ANCHOFIJO</c> guardado como dato. El <c>ISender</c> sólo atiende la subida del
/// adjunto (devuelve un id y guarda lo subido para inspeccionarlo).
/// </summary>
public sealed class DispersionDePrueba
{
    public NominaTestData D { get; } = new();
    public ISender Sender { get; }
    public Bank AvVillas { get; }
    public Bank Bancolombia { get; }
    public Employee Ana { get; }
    public Employee Carlos { get; }
    public Employee Beatriz { get; }
    public PayrollRun Prima { get; }
    public BankFileFormat Formato { get; }
    public Domain.Entities.Accounting.ChartOfAccount CuentaOrigen { get; }
    public List<UploadAttachmentCommand> Subidas { get; } = [];

    public DispersionDePrueba()
    {
        D.Db.Companies.Add(new Company { Name = "Cooperativa de prueba", TaxId = "890300001", TaxIdCheckDigit = "7", CreatedBy = "test" });
        AvVillas = new Bank { LegacyCode = "52", Name = "Banco AV Villas", TransferCode = "0052", CreatedBy = "test" };
        Bancolombia = new Bank { LegacyCode = "07", Name = "Bancolombia", TransferCode = "0007", CreatedBy = "test" };
        D.Db.Banks.AddRange(AvVillas, Bancolombia);
        D.Db.SaveChanges();

        Ana = D.Empleado("Ana", 2_000_000m, new DateTime(2025, 1, 15));
        Carlos = D.Empleado("Carlos", 1_750_905m, new DateTime(2026, 3, 15));
        Beatriz = D.Empleado("Beatriz", 1_500_000m, new DateTime(2025, 6, 1));
        Ana.DisbursementBankId = AvVillas.Id; Ana.PayrollBankAccountType = 1; Ana.PayrollBankAccountNumber = "9876543210";
        Carlos.DisbursementBankId = Bancolombia.Id; Carlos.PayrollBankAccountType = 2; Carlos.PayrollBankAccountNumber = "1234567890";
        var personaAna = D.Db.People.Single(p => p.Id == Ana.PersonId);
        personaAna.FirstName = "Ana María"; personaAna.LastName = "López"; personaAna.SecondLastName = "Pérez"; personaAna.TaxId = "1234567890";
        var personaCarlos = D.Db.People.Single(p => p.Id == Carlos.PersonId);
        personaCarlos.FirstName = "Carlos"; personaCarlos.LastName = "Ruiz"; personaCarlos.TaxId = "987654321";
        D.Db.SaveChanges();

        // La prima 2026-II aprobada, con su corte, como la deja el ciclo de US1 (los netos del contrato §2.2).
        Prima = new PayrollRun
        {
            Kind = PayrollRunKind.ServiceBonus, Year = 2026, Semester = 2, CutoffDate = new DateOnly(2026, 12, 31), Version = 1,
            Status = PayrollRunStatus.Approved, CalculatedAt = NominaTestData.Ahora, CalculatedBy = "ana@demo",
            ApprovedAt = NominaTestData.Ahora, ApprovedBy = "contadora@demo", InputsHash = new string('c', 64), CreatedBy = "test",
        };
        foreach (var (e, neto) in new[] { (Ana, 1_124_547.50m), (Carlos, 630_404.72m), (Beatriz, 500_000m) })
            Prima.Employees.Add(new PayrollRunEmployee { EmployeeId = e.Id, PayrollPlanId = D.Plan.Id, DaysWorked = 180, TotalEarnings = neto, NetPay = neto, CreatedBy = "test" });
        Prima.EmployeeCount = 3; Prima.TotalNet = Prima.Employees.Sum(x => x.NetPay);
        D.Db.PayrollRuns.Add(Prima);
        D.Db.SaveChanges();

        // La cuenta bancaria de la cooperativa en AV Villas (cuenta del plan con banco, feature 009): la cuenta origen del archivo.
        CuentaOrigen = D.Cuenta("11100501", "Banco AV Villas cta. corriente", Domain.Enums.Accounting.AccountNature.Debit);
        CuentaOrigen.BankId = AvVillas.Id; CuentaOrigen.BankAccountNumber = "1234567890";
        D.Db.SaveChanges();

        Formato = FlatFileWriterTests.CargarDemoAnchoFijo();
        Formato.CreatedBy = "test";
        D.Db.BankFileFormats.Add(Formato);
        D.Db.SaveChanges();

        Sender = Substitute.For<ISender>();
        Sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => { Subidas.Add(ci.Arg<UploadAttachmentCommand>()); return Result.Success(Guid.NewGuid()); });
    }

    public PayrollDisbursementLines Lineas => new(D.Db);
    public PreparacionDeDispersion Preparacion => new(D.Db, Lineas, D.Clock);

    public GenerateDisbursementFileCommandHandler Generador(string usuario = "contadora@demo") =>
        new(D.Db, Preparacion, Sender, D.Clock, NominaTestData.UsuarioDePrueba(usuario, 9), D.AuditEmitter);

    public MarkDisbursementSentCommandHandler Enviador(string usuario = "contadora@demo") =>
        new(D.Db, D.Clock, NominaTestData.UsuarioDePrueba(usuario, 9), D.AuditEmitter);

    public CancelDisbursementFileCommandHandler Anulador() => new(D.Db, D.Clock, D.User, D.AuditEmitter);

    public GenerateDisbursementFileCommand ComandoDePrima(Guid? formato = null, IReadOnlyList<Guid>? empleados = null) =>
        new(Prima.PublicId, formato ?? Formato.PublicId, new DateOnly(2026, 12, 15), CuentaOrigen.PublicId, "PRIMA2026II", empleados);
}
