using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Core.Associates.Services;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory;
using IngenIA365ERP.Domain.Entities.Payroll;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// Escenario mínimo para las pruebas del maestro de personas (feature 008): contexto en
/// memoria, reloj y usuario fijos, las tres fábricas y ayudas para sembrar personas con o
/// sin rol. Nada de nómina real: para eso está <c>NominaTestData</c>.
/// </summary>
public sealed class PersonasTestData
{
    public static readonly DateTime Ahora = new(2026, 9, 13, 15, 0, 0, DateTimeKind.Utc);

    public TestApplicationDbContext Db { get; }
    public IDateTimeService Clock { get; }
    public ICurrentUserService User { get; }
    public PersonFactory Personas { get; }
    public EmployeeRegistrar Empleados { get; }
    public AssociateRegistrar Asociados { get; }
    /// <summary>Feature 012 (T175): la auditoría explícita (constancia «sin política vigente»).</summary>
    public IAuditService Auditoria { get; }
    public AutorizacionDeDatos Autorizacion { get; }
    public AltaConAutorizacion Altas { get; }
    public PayrollPlan Plan { get; }

    public PersonasTestData()
    {
        Db = TestDbContextFactory.Create();
        Clock = Substitute.For<IDateTimeService>();
        Clock.UtcNow.Returns(Ahora);
        User = Substitute.For<ICurrentUserService>();
        User.UserName.Returns("operador@demo");
        User.UserId.Returns(7);
        User.TenantId.Returns("1");

        Plan = new PayrollPlan { Name = "Mensual", Code = "MENSUAL", IsDefault = true, IsActive = true, CreatedAt = Ahora };
        Db.PayrollPlans.Add(Plan);
        Db.SaveChanges();

        Personas = new PersonFactory(Db, Clock, User);
        Empleados = new EmployeeRegistrar(Db, Clock, User);
        Asociados = new AssociateRegistrar(Db, Clock, User);
        Auditoria = Substitute.For<IAuditService>();
        Autorizacion = new AutorizacionDeDatos(Db, User, Clock, Auditoria);
        Altas = new AltaConAutorizacion(Db, Personas, Autorizacion);
    }

    public static PersonInput Entrada(string taxId = "1023456789", string nombre = "Ana", string apellido = "Pérez") => new()
    {
        IdType = "C", TaxId = taxId, FirstName = nombre, LastName = apellido,
        Email = "ana@demo.co", Mobile = "3001234567", Gender = "F", MaritalStatus = "S",
    };

    public Person Persona(string taxId = "1023456789", string nombre = "Ana", string apellido = "Pérez",
        bool eliminada = false, bool asociada = false, bool empleada = false, bool vendedora = false)
    {
        var p = new Person
        {
            IdType = "C", TaxId = taxId, FirstName = nombre, LastName = apellido, Status = "A",
            IsAssociate = asociada, IsEmployee = empleada, IsSalesperson = vendedora,
            IsDeleted = eliminada, DeletedAt = eliminada ? Ahora.AddMonths(-6) : null,
            DeletedBy = eliminada ? "admin@demo" : null,
            CreatedAt = Ahora.AddYears(-1), CreatedBy = "seed",
        };
        Db.People.Add(p);
        Db.SaveChanges();
        if (asociada) { Db.Associates.Add(new Associate { PersonId = p.Id, Status = "A", CreatedAt = Ahora }); }
        if (empleada) { Db.Employees.Add(Ficha(p, activa: true)); }
        if (vendedora) { Db.Salespeople.Add(new Salesperson { PersonId = p.Id, CreatedAt = Ahora }); }
        if (asociada || empleada || vendedora) Db.SaveChanges();
        return p;
    }

    public Employee Ficha(Person p, bool activa) => new()
    {
        PersonId = p.Id, PayrollCompanyId = 1, PayrollPlanId = Plan.Id, Salary = 2_000_000m,
        JoinDate = Ahora.AddYears(-2), TerminationDate = activa ? DateTime.MaxValue : Ahora.AddMonths(-1),
        Status = activa ? 1 : -1, CreatedAt = Ahora.AddYears(-2),
        CostCenterId = "", AreaCode = "", SectionId = "", TerminationCause = "", PensionFundMember = "",
        IsLiquidated = "", SpecialRegime = "", ExtraBonusFlag = "", PayrollBankId = "", PayrollBankAccountNumber = "",
    };
}
