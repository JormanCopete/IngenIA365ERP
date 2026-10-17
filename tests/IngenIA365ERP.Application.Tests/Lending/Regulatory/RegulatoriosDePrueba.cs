using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Reports;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.Extensions.Options;
using NSubstitute;
using Contables = IngenIA365ERP.Application.Common.Integration.Accounting;

namespace IngenIA365ERP.Application.Tests.Lending.Regulatory;

/// <summary>
/// El escenario de los archivos regulatorios (feature 013, K5, US16; T0989–T0992): el de <see cref="InformesDePrueba"/> —InMemory, hoy local
/// 2026-12-01, el crédito de Ana de 3.000.000 a 24 cuotas desembolsado hoy por el registro del libro, con sus condiciones y su plan— con
/// <list type="bullet">
/// <item>el puerto de Contabilidad resolviendo el capital de toda combinación a la cuenta <c>14110501</c> (o a ninguna, si se pide);</item>
/// <item>el almacén de adjuntos como doble (<see cref="Subidas"/>) y las alertas en <see cref="Alertas"/>;</item>
/// <item>los códigos de suscriptor <c>DATACREDITO=123456;TRANSUNION=ABC123</c> y la permanencia del dato negativo (2 veces la mora, 48 meses);</item>
/// <item>créditos, posiciones y cierres sembrados como hechos, en diciembre de 2026 (el primer mes con layouts vigentes).</item>
/// </list>
/// (nuevo)
/// </summary>
internal sealed class RegulatoriosDePrueba : IDisposable
{
    public const int Anio = 2026;
    public const int Mes = 12;
    public static readonly DateOnly Corte = new(2026, 12, 31);
    public const string CuentaDeCapital = "14110501";

    private int _corrida = 7000;

    private RegulatoriosDePrueba(InformesDePrueba e)
    {
        E = e;
        Sender.Send(NSubstitute.Arg.Do<UploadAttachmentCommand>(Subidas.Add), NSubstitute.Arg.Any<CancellationToken>()).Returns(_ => Result.Success(Guid.NewGuid()));
        ResolverCuentas(true);
        G.Limite(CatalogoDeLimitesLegales.PermanenciaNegativoVecesMora, 2m, "Ley 2157 de 2021");
        G.Limite(CatalogoDeLimitesLegales.PermanenciaNegativoMesesMaximos, 48m, "Ley 2157 de 2021");
    }

    public static async Task<RegulatoriosDePrueba> CrearAsync()
    {
        var r = new RegulatoriosDePrueba(await InformesDePrueba.CrearAsync());
        await r.E.ParametroAsync(ParametrosDeCartera.CentralesCodigosDeSuscriptor, "DATACREDITO=123456;TRANSUNION=ABC123");
        r.G.Conceder();
        r.G.ActuarComo(r.D.Cajera);
        return r;
    }

    public InformesDePrueba E { get; }
    public Disbursements.DesembolsoDePrueba D => E.D;
    public Sessions.GobiernoDePrueba G => E.G;
    public TestApplicationDbContext Db => E.Db;
    public Loan Credito => E.Credito;
    public Person Ana => D.O.Ana;

    public ISender Sender { get; } = Substitute.For<ISender>();
    public List<UploadAttachmentCommand> Subidas { get; } = [];
    public List<AlertaALevantar> Alertas => D.O.L.Levantadas;

    public void Dispose() => E.Dispose();

    // ------------------------------------------------------------------------------------------------------------ piezas --

    public ProyeccionSicsesCartera Sicses() => new(Db, D.Contabilidad, G.Perfil, D.O.L.Normativa());

    public ProyeccionCentralDeRiesgo Centrales() => new(Db, D.O.Autorizaciones, G.Valores, D.Parametros(),
        Options.Create(new CanalesExternosOptions { AmbienteDePruebas = true }), G.Reloj);

    public PreparacionDeArchivoRegulatorio Preparacion() => new(CatalogoDeLayoutsDeCartera.Embebido, Sicses(), Centrales());

    public GeneracionDeArchivoRegulatorio Generacion() => new(Db, Preparacion(), D.Numerador(), Sender, D.Alertas, G.Auditoria, G.Actor, G.Reloj);

    /// <summary>Con <paramref name="resuelve"/>, Contabilidad resuelve el capital de toda combinación a <see cref="CuentaDeCapital"/>; sin él, a ninguna.</summary>
    public void ResolverCuentas(bool resuelve) =>
        D.Contabilidad.SaldosDeCuentasMapeadasAsync(Arg.Any<DateOnly>(), Arg.Any<IReadOnlyList<CombinacionDeSaldo>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var n = ci.ArgAt<IReadOnlyList<CombinacionDeSaldo>>(1).Count;
                IReadOnlyList<ConjuntoDeCuentasDeCarteraDto> conjuntos = resuelve
                    ? [new ConjuntoDeCuentasDeCarteraDto("Capital", Enumerable.Range(0, n).ToList(),
                        [new Contables.CuentaDelConjuntoDto(CuentaDeCapital, "Créditos de consumo", "Capital", [])], 0m)]
                    : [];
                return Task.FromResult(Result.Success(new SaldosMapeadosDeCarteraDto(conjuntos, resuelve ? [] : Enumerable.Range(0, n).ToList())));
            });

    // ------------------------------------------------------------------------------------------------------- siembra --

    public Person Persona(string nombre, string apellido, string documento, string idType = "C")
    {
        var p = G.Persona(nombre, apellido, documento);
        p.IdType = idType;
        p.Address = "Calle 10 # 20-30";
        p.Mobile = "3001234567";
        p.Email = $"{nombre.ToLowerInvariant()}@correo.test";
        p.DaneCityCode = "05001";
        Db.SaveChanges();
        return p;
    }

    /// <summary>Un crédito sembrado como hecho, con su posición al <paramref name="posicion"/> (por defecto, el corte) y sus días de mora.</summary>
    public async Task<Loan> CreditoAsync(string numero, Person deudor, decimal monto = 1_000_000m, int dias = 0, LoanStatus estado = LoanStatus.Active,
        DateOnly? abierto = null, DateOnly? posicion = null, decimal? capital = null, Action<Loan>? ajustar = null, string modalidad = "CONSUMO")
    {
        var credito = new Loan
        {
            Number = numero, DebtorPersonId = deudor.Id, DebtorKind = DebtorKind.Associate, BranchId = Credito.BranchId, CreditLineId = Credito.CreditLineId,
            Origin = LoanOrigin.Origination, Status = estado, ApprovedAmount = monto, OpenedOn = abierto ?? new DateOnly(2026, 6, 1),
        };
        ajustar?.Invoke(credito);
        Db.Loans.Add(credito);
        await Db.SaveChangesAsync();
        Db.LoanDailyPositions.Add(new LoanDailyPosition
        {
            LoanId = credito.Id, PositionDate = posicion ?? Corte, Version = 1, Status = estado, BranchId = credito.BranchId, CreditLineId = credito.CreditLineId,
            ModalityCode = modalidad, Principal = capital ?? monto, PrincipalPastDue = dias > 0 ? 50_000m : 0m, DefaultInterest = dias > 0 ? 1_500m : 0m,
            DaysPastDue = dias, InstallmentsPastDue = dias > 0 ? 1 + dias / 30 : 0, InputsSha256 = "x",
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return credito;
    }

    /// <summary>Una línea de cierre del crédito con sus saldos, días, calificación y deterioro.</summary>
    public static MonthlyCloseLoan Linea(Loan c, int dias = 0, RatingCategory cal = RatingCategory.A, RatingSubcategory sub = RatingSubcategory.None,
        decimal capital = 1_000_000m, decimal deterioro = 0m, string modalidad = "CONSUMO", bool reestructurado = false, int veces = 0,
        decimal garantia = 0m, bool empleado = false) => new()
    {
        LoanId = c.Id, PersonId = c.DebtorPersonId, BranchId = c.BranchId, CreditLineId = c.CreditLineId, ModalityCode = modalidad, SarcSegmentCode = modalidad,
        DaysPastDueAtClose = dias, FinalRating = cal, FinalSubcategory = sub, AccountingRating = cal, ArrearsRating = cal, PrincipalBalance = capital,
        CurrentInterestBalance = 12_000m, DefaultInterestBalance = dias > 0 ? 1_500m : 0m, SuspendedInterestBalance = 0m, OtherBalance = 3_000m,
        PrincipalImpairment = deterioro, InterestImpairment = 0m, OtherImpairment = 0m, TotalImpairment = deterioro, CollateralAdmissibleValue = garantia,
        IsRestructured = reestructurado, RestructuringCount = veces, IsEmployee = empleado,
    };

    /// <summary>Un cierre mensual (por defecto diciembre de 2026) con sus líneas, enlazadas a su corrida.</summary>
    public async Task<MonthlyClose> CierreAsync(IEnumerable<MonthlyCloseLoan> lineas, int version = 1, int anio = Anio, int mes = Mes,
        bool sinModelo = false, bool pendiente = false)
    {
        var corrida = ++_corrida;
        var cierre = new MonthlyClose
        {
            Kind = MonthlyCloseKind.Monthly, PeriodYear = (short)anio, PeriodMonth = (byte)mes, Version = version, CloseDate = MonthlyClose.UltimoDiaDe(anio, mes),
            ProcessRunId = corrida, ObligatoryModelUnavailable = sinModelo, UnvalidatedParameters = pendiente,
            UnvalidatedParametersJson = pendiente ? "Anexo 1 pendiente de validar" : null, TotalGeneralImpairment = 4_321m,
        };
        Db.MonthlyCloses.Add(cierre);
        await Db.SaveChangesAsync();
        foreach (var l in lineas)
            Db.MonthlyCloseLoans.Add(new MonthlyCloseLoan
            {
                ProcessRunId = corrida, LoanId = l.LoanId, PersonId = l.PersonId, BranchId = l.BranchId, CreditLineId = l.CreditLineId, ModalityCode = l.ModalityCode,
                SarcSegmentCode = l.SarcSegmentCode, DaysPastDueAtClose = l.DaysPastDueAtClose, FinalRating = l.FinalRating, FinalSubcategory = l.FinalSubcategory,
                AccountingRating = l.AccountingRating, ArrearsRating = l.ArrearsRating, PrincipalBalance = l.PrincipalBalance,
                CurrentInterestBalance = l.CurrentInterestBalance, DefaultInterestBalance = l.DefaultInterestBalance,
                SuspendedInterestBalance = l.SuspendedInterestBalance, OtherBalance = l.OtherBalance, PrincipalImpairment = l.PrincipalImpairment,
                InterestImpairment = l.InterestImpairment, OtherImpairment = l.OtherImpairment, TotalImpairment = l.TotalImpairment,
                CollateralAdmissibleValue = l.CollateralAdmissibleValue, IsRestructured = l.IsRestructured, RestructuringCount = l.RestructuringCount,
                IsEmployee = l.IsEmployee,
            });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return cierre;
    }

    public async Task ReabrirAsync(MonthlyClose cierre)
    {
        Db.MonthlyCloseReopenings.Add(new MonthlyCloseReopening { MonthlyCloseId = cierre.Id, ProcessRunId = cierre.ProcessRunId ?? 0, Reason = "prueba" });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    /// <summary>El perfil regulatorio (tipo <c>COOP_AHORRO_CREDITO</c>, nivel <paramref name="nivel"/>) y el documento con su <c>sicsesReport</c>.</summary>
    public async Task PerfilConSicsesAsync(int nivel = 1, string sicses = """[ { "supervisionLevel": 1, "cutoffMonths": [1,2,3,4,5,6,7,8,9,10,11,12], "dueDays": 20, "warningDays": 5 } ]""")
    {
        var contenido = $$"""
            {
              "employeeFundCategories": [],
              "organizationTypes": [
                { "code": "COOP_AHORRO_CREDITO", "name": "Cooperativa de ahorro y crédito", "isEmployeeFund": false, "expectedLoss": "Admitted",
                  "sicsesReport": {{sicses}} }
              ]
            }
            """;
        var replica = new NationalValueReplication
        {
            FromSequence = 3, ToSequence = 3, DocumentCount = 1, Origin = NationalValueReplicationOrigin.ScheduledTask,
            StartedAt = G.UtcNow.AddDays(-1), FinishedAt = G.UtcNow.AddDays(-1), ActorName = "prueba",
        };
        replica.Documents.Add(new RegulatoryDocument
        {
            Kind = nameof(RegulatoryDocumentKind.TiposDeOrganizacion), Code = "TIPOS", ValidFrom = new DateOnly(2026, 1, 1), Content = contenido,
            ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenido))), SchemaVersion = 1, Verified = true,
            Source = "Superintendencia de la Economía Solidaria", SourcePublicId = Guid.NewGuid(), SourceSequence = 90_101,
            CentralRegisteredAt = G.UtcNow.AddDays(-1), KnownAt = G.UtcNow.AddDays(-1),
        });
        Db.NationalValueReplications.Add(replica);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        G.Perfil.VigenteAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<PerfilRegulatorioDto?>(
            new PerfilRegulatorioDto(Guid.NewGuid(), new DateOnly(2026, 1, 1), "COOP_AHORRO_CREDITO", true, (byte)nivel, null, 2, false, null, null, "Prueba", null,
                "Perfil de prueba", null, "prueba", G.UtcNow.AddDays(-30))));
    }

    public static Domain.Common.Layouts.FilaDeLayout FilaDe(IEnumerable<Domain.Common.Layouts.FilaDeLayout> filas, string credito) =>
        filas.Single(f => f.Credito == credito);
}
