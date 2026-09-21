using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Pila;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Pila;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Policies;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Pila;

/// <summary>
/// El escenario de la US5 en Application: la empresa, los datos del aportante, los cuatro
/// catálogos con código PILA, y dos empleados con la nómina de diciembre de 2026 aprobada
/// (Ana bajo el umbral de exoneración, Gloria con FSP). El <c>ISender</c> sólo atiende la
/// subida del adjunto.
/// </summary>
public sealed class PilaDePrueba
{
    public NominaTestData D { get; } = new();
    public ISender Sender { get; }
    public Employee Ana { get; }
    public Employee Gloria { get; }
    public List<UploadAttachmentCommand> Subidas { get; } = [];

    public PilaDePrueba(bool exonerada = true, bool conAjustes = true)
    {
        D.Db.Companies.Add(new Company { Name = "Cooperativa de prueba", TaxId = "900123456", TaxIdCheckDigit = "7", CreatedBy = "test" });
        D.Db.HealthInsuranceProviders.Add(new HealthInsuranceProvider { Code = "SURA", Name = "EPS Sura", ShortName = "SURA", TaxId = "800088702", PilaCode = "EPS010", CreatedBy = "test" });
        D.Db.PensionProviders.Add(new PensionProvider { Code = "PORV", Name = "Porvenir", ShortName = "PORV", TaxId = "800144331", PilaCode = "230301", CreatedBy = "test" });
        D.Db.WorkRiskProviders.Add(new WorkRiskProvider { Code = "SURAARL", Name = "ARL Sura", ShortName = "SURA", TaxId = "800256161", PilaCode = "14-23", CreatedBy = "test" });
        D.Db.FamilyCompensationFunds.Add(new FamilyCompensationFund { Code = "COMFANDI", Name = "Comfandi", ShortName = "COMF", TaxId = "890303093", PilaCode = "CCF24", CreatedBy = "test" });
        if (conAjustes)
            D.Db.PilaSettings.Add(new PilaSettings { ContributorType = "1", ContributorClass = "B", PresentationForm = "U", ArlPilaCode = "14-23", MunicipalityDaneCode = "76001", EconomicActivityCode = "1649501", OperatorCode = "24", OperatorName = "Aportes en Línea", CreatedBy = "test" });
        D.Politica(CompanyPolicyKeys.Exonerada114_1, exonerada ? "true" : "false", new DateOnly(2026, 1, 1));
        D.Db.SaveChanges();

        Ana = D.Empleado("Ana", 2_000_000m, new DateTime(2020, 1, 15));
        Gloria = D.Empleado("Gloria", 8_000_000m, new DateTime(2015, 8, 18));
        foreach (var e in new[] { Ana, Gloria })
        {
            var p = D.Db.People.Single(x => x.Id == e.PersonId);
            p.SecondLastName = "Segundo";
            e.WorkMunicipalityDaneCode = "76001"; e.EconomicActivityCode = "1649501"; e.WorkRiskId = 1;
        }
        D.Db.SaveChanges();

        // Diciembre de 2026 aprobado con los aportes que la nómina liquidó (los mismos del caso dorado 01 para Ana y Gloria).
        D.CorridaAprobada(2026, 12, Ana,
            new("SALARIO", ConceptNature.Earning, 2_000_000m, 30m), new("SALUD_EMP", ConceptNature.Deduction, 80_000m), new("PENSION_EMP", ConceptNature.Deduction, 80_000m),
            new("SALUD_EMPLEADOR", ConceptNature.EmployerContribution, exonerada ? 0m : 170_000m), new("PENSION_EMPLEADOR", ConceptNature.EmployerContribution, 240_000m),
            new("ARL", ConceptNature.EmployerContribution, 10_500m), new("CAJA", ConceptNature.EmployerContribution, 80_000m),
            new("SENA", ConceptNature.EmployerContribution, exonerada ? 0m : 40_000m), new("ICBF", ConceptNature.EmployerContribution, exonerada ? 0m : 60_000m));
        D.CorridaAprobada(2026, 12, Gloria,
            new("SALARIO", ConceptNature.Earning, 8_000_000m, 30m), new("SALUD_EMP", ConceptNature.Deduction, 320_000m), new("PENSION_EMP", ConceptNature.Deduction, 320_000m), new("FSP", ConceptNature.Deduction, 80_000m),
            new("SALUD_EMPLEADOR", ConceptNature.EmployerContribution, exonerada ? 0m : 680_000m), new("PENSION_EMPLEADOR", ConceptNature.EmployerContribution, 960_000m),
            new("ARL", ConceptNature.EmployerContribution, 41_800m), new("CAJA", ConceptNature.EmployerContribution, 320_000m),
            new("SENA", ConceptNature.EmployerContribution, exonerada ? 0m : 160_000m), new("ICBF", ConceptNature.EmployerContribution, exonerada ? 0m : 240_000m));

        Sender = Substitute.For<ISender>();
        Sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => { Subidas.Add(ci.Arg<UploadAttachmentCommand>()); return Result.Success(Guid.NewGuid()); });
    }

    public PilaInputLoader Loader => new(D.Db, D.Policies);
    public PreparacionDePila Preparacion => new(D.Db, Loader);
    public ValidatePilaQueryHandler Validador => new(Preparacion);
    public GeneratePilaCommandHandler Generador(string usuario = "contadora@demo") => new(D.Db, Preparacion, Sender, D.Clock, NominaTestData.UsuarioDePrueba(usuario, 9), D.AuditEmitter);
    public MarkPilaUploadedCommandHandler Marcador() => new(D.Db, D.Clock, D.User, D.AuditEmitter);
}
