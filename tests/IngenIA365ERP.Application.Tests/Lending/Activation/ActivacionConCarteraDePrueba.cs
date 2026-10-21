using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Tests.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Activation;

/// <summary>
/// El escenario de la activación con cartera previa (feature 013, US8, bloque 13B): sobre las ventas a crédito de US6 (Ana, Beto, Carla y Darío; la
/// oficina Norte; la línea <c>CONLIB</c> con versión desde hoy y origen comercial por defecto para asociados; Contabilidad doble con el mes abierto),
/// Cartera sin activar, el destino <c>CAR</c> inhabilitado y los dobles <c>TST</c> y <c>TES</c>, y una carga con corte <b>hoy</b> (2026-12-01) armada
/// fila por fila en sus 17 partes:
/// <list type="bullet">
/// <item><c>CR-100</c> de Ana, por caja, nacido de la venta <c>FV-MIG</c>: seis cuotas de un sexto del capital (la 1 y la 2 vencidas, con 30.000 y
/// 25.000 de corriente y 2.000 de mora; la 3 en curso con 20.000 de corriente, que es el suspenso), Beto codeudor, pagaré y prenda, reestructurado una
/// vez, aviso previo, reporte negativo y reclamo en trámite, historial de dos meses;</item>
/// <item><c>CR-200</c> de Beto por nómina propia: cuatro cuotas, la primera vence dos días después del corte;</item>
/// <item><c>CO-300</c> de Carla, castigado;</item>
/// <item>cupos (Carla migrado; Beto ya tiene uno a mano), bloqueos (embargo de Beto, insolvencia de Carla), la autorización de Ana, el vinculado Beto
/// y las ventas <c>FV-MIG</c> (migrada) y <c>FV-CAN</c> (cancelada antes del corte).</item>
/// </list>
/// En la bandeja esperan las ventas <c>FV-MIG</c>, <c>FV-CAN</c>, <c>FV-SIN</c> (no migrada con saldo) y <c>FV-POS</c> (confirmada después del
/// corte). La activación se registra tres días después del corte. (nuevo)
/// </summary>
internal sealed class ActivacionConCarteraDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = VentasACreditoDePrueba.Hoy;
    public static readonly DateOnly Corte = Hoy;
    public static readonly DateOnly Activacion = Corte.AddDays(1);
    public static readonly DateOnly Registro = Corte.AddDays(3);

    private ActivacionConCarteraDePrueba(VentasACreditoDePrueba v) => V = v;

    public VentasACreditoDePrueba V { get; }
    public Disbursements.DesembolsoDePrueba D => V.D;
    public Origination.OriginacionDePrueba O => V.O;
    public Sessions.GobiernoDePrueba G => V.G;
    public IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => V.Db;
    public IAmbienteDeDespliegue Ambiente { get; } = Substitute.For<IAmbienteDeDespliegue>();
    public ITabularFileReader Lector { get; } = Substitute.For<ITabularFileReader>();
    public IBlobStore Almacen { get; } = Substitute.For<IBlobStore>();
    public TablaLeida Tabla { get; set; } = new([], [], "xlsx");
    public MigrationLoad Carga { get; private set; } = null!;
    public int LineaId { get; private set; }
    public decimal Cuota { get; private set; }
    public decimal Capital => Cuota * 6;
    public Dictionary<string, VentasACreditoDePrueba.Venta> Ventas { get; } = new(StringComparer.Ordinal);

    /// <summary>El saldo contable por combinación; nulo = Contabilidad no resuelve la combinación (la diferencia es todo lo de Cartera).</summary>
    public Func<CombinacionDeSaldo, decimal?> SaldoContable { get; set; } = _ => null;

    public void Dispose() => V.Dispose();

    public static async Task<ActivacionConCarteraDePrueba> CrearAsync(decimal cuotaDeAna = 500_000m)
    {
        var a = new ActivacionConCarteraDePrueba(await VentasACreditoDePrueba.CrearAsync()) { Cuota = cuotaDeAna };
        a.Ambiente.EsProduccion.Returns(false);
        var setup = await a.Db.LendingSetups.SingleAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica);
        setup.ActivationDate = null;
        setup.ActivatedAt = null;
        a.Participante("CAR", IntegrationParticipantClass.Lending, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Lending, false,
            ("VentaACreditoRegistrada", 2));
        a.Participante("TST", IntegrationParticipantClass.Test, IntegrationParticipantRole.ObligationsCollector, string.Empty, true,
            (ContratosDeIntegracion.ProveedorDeObligaciones, 1));
        a.Participante("TES", IntegrationParticipantClass.Test, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury, true,
            (OrdenDeGiroEmitidaV1.Type, 1), (AnulacionDeOrdenDeGiroV1.Type, 1));
        await a.Db.SaveChangesAsync();
        a.LineaId = (await a.Db.CreditLines.AsNoTracking().SingleAsync(l => l.Code == "CONLIB")).Id;

        a.Carga = new MigrationLoad { Number = 1, CutoffDate = Corte, Status = MigrationLoadStatus.Draft, CreatedByUserId = a.V.Cajera.Id };
        a.Db.MigrationLoads.Add(a.Carga);
        await a.Db.SaveChangesAsync();
        await a.PartesAsync();

        foreach (var (numero, dias, monto) in new[] { ("FV-MIG", -90, a.Capital), ("FV-CAN", -40, 400_000m), ("FV-SIN", -5, 250_000m) })
        {
            var venta = a.V.NuevaVenta(monto, Corte.AddDays(dias), numero);
            a.Ventas[numero] = venta;
            await a.V.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta));
        }
        var posterior = a.V.NuevaVenta(600_000m, Corte.AddDays(1), "FV-POS");
        a.Ventas["FV-POS"] = posterior;
        await a.V.EmitirAsync(posterior, VentasACreditoDePrueba.ContenidoV2(posterior));

        a.D.Contabilidad.SaldosDeCuentasMapeadasAsync(Arg.Any<DateOnly>(), Arg.Any<IReadOnlyList<CombinacionDeSaldo>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var combinaciones = ci.Arg<IReadOnlyList<CombinacionDeSaldo>>();
                var conjuntos = combinaciones.Select((c, i) => (c, i)).Where(x => a.SaldoContable(x.c) is not null)
                    .Select(x => new ConjuntoDeCuentasDeCarteraDto(x.c.Role, [x.i],
                        [new CuentaDelConjuntoDto("1441" + x.i.ToString("00", System.Globalization.CultureInfo.InvariantCulture), "Cuenta " + x.c.Role, x.c.Role,
                            [new SaldoPorSucursalDto(x.c.BranchPublicId, a.SaldoContable(x.c)!.Value * SaldosContablesDeCartera.Signo(x.c.Role))])], 0m))
                    .ToList();
                var sinRegla = combinaciones.Select((c, i) => (c, i)).Where(x => a.SaldoContable(x.c) is null).Select(x => x.i).ToList();
                return Task.FromResult(Result.Success(new SaldosMapeadosDeCarteraDto(conjuntos, sinRegla)));
            });
        a.Lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(a.Tabla)));
        a.G.Permisos.EsMaestroGlobal.Returns(true);
        a.G.Limite(IngenIA365ERP.Domain.Common.NationalValues.CatalogoDeLimitesLegales.AvisoPrevioDias, 20m, "Ley 2157 de 2021");
        a.G.ActuarComo(a.V.Cajera);
        a.Db.ChangeTracker.Clear();
        return a;
    }

    private void Participante(string codigo, IntegrationParticipantClass clase, IntegrationParticipantRole rol, string destino, bool habilitado,
        params (string Contrato, short Version)[] adopciones)
    {
        var siempre = new DateOnly(1900, 1, 1);
        var p = new IntegrationParticipant
        {
            Code = codigo, Class = clase, Role = rol, Destination = destino, IsEnabled = habilitado, ValidFrom = siempre, Reason = "Semilla",
            RegisteredByKind = ActorKind.Process, RegisteredByName = "Prueba",
        };
        foreach (var (contrato, version) in adopciones)
            p.Adoptions.Add(new IntegrationParticipantAdoption { Participant = p, ContractName = contrato, Version = version, ValidFrom = siempre, Reason = "Semilla" });
        Db.IntegrationParticipants.Add(p);
    }

    // ------------------------------------------------------------------------------------------------------- la carga --

    private int _fila = 1;

    private MigrationLoadPart Parte(string clave)
    {
        var p = new MigrationLoadPart
        {
            MigrationLoadId = Carga.Id, PartKey = clave, BatchNumber = 1, FileName = clave + ".xlsx", FileSha256 = Guid.NewGuid().ToString("N"),
            ImportedByUserId = V.Cajera.Id, ImportedAt = G.UtcNow, RowCount = 1,
        };
        Db.MigrationLoadParts.Add(p);
        Db.SaveChanges();
        return p;
    }

    private const string Condiciones =
        """{"rateKind":"Fixed","annualRate":0.22,"defaultRateKind":"RelativeToCurrent","defaultFactor":1.5,"dayCount":"Actual365","periodicity":"Monthly","term":6}""";

    private async Task PartesAsync()
    {
        var norte = O.Norte.PublicId;
        int F() => ++_fila;

        var loans = Parte(DefinicionesDeMigracion.Partes.Loans);
        Db.MigrationLoanRows.AddRange(
            new MigrationLoanRow { MigrationLoadPartId = loans.Id, RowNumber = F(), SourceLoanNumber = "CR-100", PersonId = O.Ana.Id, CreditLineCode = "CONLIB",
                CreditLineId = LineaId, OfficePublicId = norte, InvoiceNumber = "FV-MIG", DisbursementDate = Corte.AddDays(-90), OriginalAmount = Capital,
                TermsJson = Condiciones, PrepaymentChoice = PrepaymentChoice.ReduceTerm, RepaymentChannel = RepaymentChannel.Cashier },
            new MigrationLoanRow { MigrationLoadPartId = loans.Id, RowNumber = F(), SourceLoanNumber = "CR-200", PersonId = O.Beto.Id, CreditLineCode = "CONLIB",
                CreditLineId = LineaId, OfficePublicId = norte, DisbursementDate = Corte.AddDays(-30), OriginalAmount = 1_200_000m,
                TermsJson = Condiciones.Replace("\"term\":6", "\"term\":4", StringComparison.Ordinal), RepaymentChannel = RepaymentChannel.OwnPayroll });

        var cuotas = Parte(DefinicionesDeMigracion.Partes.Installments);
        Db.MigrationInstallmentRows.AddRange(
            Cuota_(cuotas, "CR-100", 1, Corte.AddDays(-60), Cuota, 30_000m, 2_000m),
            Cuota_(cuotas, "CR-100", 2, Corte.AddDays(-30), Cuota, 25_000m),
            Cuota_(cuotas, "CR-100", 3, Corte.AddDays(30), Cuota, 20_000m),
            Cuota_(cuotas, "CR-100", 4, Corte.AddDays(60), Cuota),
            Cuota_(cuotas, "CR-100", 5, Corte.AddDays(90), Cuota),
            Cuota_(cuotas, "CR-100", 6, Corte.AddDays(120), Cuota),
            Cuota_(cuotas, "CR-200", 1, Corte.AddDays(2), 300_000m),
            Cuota_(cuotas, "CR-200", 2, Corte.AddDays(32), 300_000m),
            Cuota_(cuotas, "CR-200", 3, Corte.AddDays(62), 300_000m),
            Cuota_(cuotas, "CR-200", 4, Corte.AddDays(92), 300_000m));

        var saldos = Parte(DefinicionesDeMigracion.Partes.Balances);
        Db.MigrationBalanceRows.AddRange(
            Saldo(saldos, "CR-100", LoanConcept.Principal, Capital),
            Saldo(saldos, "CR-100", LoanConcept.CurrentInterest, 55_000m),
            Saldo(saldos, "CR-100", LoanConcept.CurrentInterestInSuspense, 20_000m),
            Saldo(saldos, "CR-100", LoanConcept.DefaultInterest, 2_000m),
            Saldo(saldos, "CR-200", LoanConcept.Principal, 1_200_000m));

        var riesgo = Parte(DefinicionesDeMigracion.Partes.RiskData);
        Db.MigrationRiskDataRows.AddRange(
            new MigrationRiskDataRow { MigrationLoadPartId = riesgo.Id, RowNumber = F(), SourceLoanNumber = "CR-100", DaysPastDue = 60, Rating = RatingCategory.B,
                PrincipalImpairment = 30_000m, GeneralImpairment = 10_000m },
            new MigrationRiskDataRow { MigrationLoadPartId = riesgo.Id, RowNumber = F(), SourceLoanNumber = "CR-200", DaysPastDue = 0, Rating = RatingCategory.A,
                GeneralImpairment = 12_000m });

        Db.MigrationRestructuringRows.Add(new MigrationRestructuringRow { MigrationLoadPartId = Parte(DefinicionesDeMigracion.Partes.Restructurings).Id, RowNumber = F(),
            SourceLoanNumber = "CR-100", Cause = TermsVersionCause.Restructuring, RestructuringCount = 1, LastRestructuredOn = Corte.AddDays(-200),
            RatingAtRestructuring = RatingCategory.B, ConsecutivePaidInstallments = 3 });
        Db.MigrationCoDebtorRows.Add(new MigrationCoDebtorRow { MigrationLoadPartId = Parte(DefinicionesDeMigracion.Partes.CoDebtors).Id, RowNumber = F(),
            SourceLoanNumber = "CR-100", CoDebtorPersonId = O.Beto.Id });
        var garantias = Parte(DefinicionesDeMigracion.Partes.Collaterals);
        Db.MigrationCollateralRows.AddRange(
            new MigrationCollateralRow { MigrationLoadPartId = garantias.Id, RowNumber = F(), SourceLoanNumber = "CR-100", CollateralKind = CollateralKind.PromissoryNote,
                Reference = "PG-100", Value = Capital, ConstitutedOn = Corte.AddDays(-90), PromissoryNoteKind = PromissoryNoteKind.Physical },
            new MigrationCollateralRow { MigrationLoadPartId = garantias.Id, RowNumber = F(), SourceLoanNumber = "CR-100", CollateralKind = CollateralKind.Pledge,
                Reference = "ABC123", Value = 5_000_000m, ConstitutedOn = Corte.AddDays(-90), Description = "Motocicleta" });
        Db.MigrationChargedOffRows.Add(new MigrationChargedOffRow { MigrationLoadPartId = Parte(DefinicionesDeMigracion.Partes.ChargedOff).Id, RowNumber = F(),
            SourceLoanNumber = "CO-300", PersonId = O.Carla.Id, CreditLineCode = "CONLIB", OfficePublicId = norte, WrittenOffOn = Corte.AddDays(-200),
            PrincipalBalance = 800_000m, InterestBalance = 50_000m, HistoricalApprovalReference = "Acta 12 de 2025" });
        var cupos = Parte(DefinicionesDeMigracion.Partes.CreditLimits);
        Db.MigrationCreditLimitRows.AddRange(
            new MigrationCreditLimitRow { MigrationLoadPartId = cupos.Id, RowNumber = F(), PersonId = O.Carla.Id, LimitKind = CreditLimitKind.Consolidated,
                Amount = 2_000_000m, ValidFrom = Corte.AddDays(-10), Reason = "Cupo migrado" },
            new MigrationCreditLimitRow { MigrationLoadPartId = cupos.Id, RowNumber = F(), PersonId = O.Dario.Id, LimitKind = CreditLimitKind.Consolidated,
                Amount = 1_000_000m, ValidFrom = Corte.AddDays(-10), Reason = "Cupo migrado de Darío" });
        var bloqueos = Parte(DefinicionesDeMigracion.Partes.Blocks);
        Db.MigrationBlockRows.AddRange(
            new MigrationBlockRow { MigrationLoadPartId = bloqueos.Id, RowNumber = F(), PersonId = O.Beto.Id, BlockReason = DebtorBlockReason.Garnishment,
                Since = Corte.AddDays(-20), SourceLoanNumber = "CR-200", SupportReference = "Oficio 123" },
            new MigrationBlockRow { MigrationLoadPartId = bloqueos.Id, RowNumber = F(), PersonId = O.Carla.Id, BlockReason = DebtorBlockReason.Insolvency,
                Since = Corte.AddDays(-15), SupportReference = "Auto de apertura" });
        Db.MigrationAuthorizationRows.Add(new MigrationAuthorizationRow { MigrationLoadPartId = Parte(DefinicionesDeMigracion.Partes.Authorizations).Id, RowNumber = F(),
            PersonId = O.Ana.Id, Purpose = "CreditBureauReport", AuthorizedOn = Corte.AddDays(-300), EvidenceReference = "Formato físico 12", HasEvidence = true });
        Db.MigrationPriorNoticeRows.Add(new MigrationPriorNoticeRow { MigrationLoadPartId = Parte(DefinicionesDeMigracion.Partes.PriorNotices).Id, RowNumber = F(),
            SourceLoanNumber = "CR-100", EpisodeStartDate = Corte.AddDays(-60), RequiredCommunications = 1, FirstSentOn = Corte.AddDays(-50),
            FirstChannel = MessageChannel.Email, FirstReference = "G-1" });
        Db.MigrationBureauReportRows.Add(new MigrationBureauReportRow { MigrationLoadPartId = Parte(DefinicionesDeMigracion.Partes.BureauReports).Id, RowNumber = F(),
            SourceLoanNumber = "CR-100", BureauCode = "DATACREDITO", LastReportedYear = 2026, LastReportedMonth = 11, LastReportedStatus = "MORA 60",
            IsNegative = true, EpisodeStartDate = Corte.AddDays(-60), WithPriorNotice = true });
        Db.MigrationClaimRows.Add(new MigrationClaimRow { MigrationLoadPartId = Parte(DefinicionesDeMigracion.Partes.Claims).Id, RowNumber = F(),
            SourceLoanNumber = "CR-100", BureauCode = "DATACREDITO", Kind = CreditReportClaimKind.Claim, ReceivedOn = Corte.AddDays(-5) });
        Db.MigrationRelatedPartyRows.Add(new MigrationRelatedPartyRow { MigrationLoadPartId = Parte(DefinicionesDeMigracion.Partes.RelatedParties).Id, RowNumber = F(),
            PersonId = O.Beto.Id, Basis = RelatedPartyBasis.BoardMember, ValidFrom = new DateOnly(2026, 1, 1), SupportReference = "Acta de asamblea" });
        var ventas = Parte(DefinicionesDeMigracion.Partes.Sales);
        Db.MigrationSaleRows.AddRange(
            new MigrationSaleRow { MigrationLoadPartId = ventas.Id, RowNumber = F(), InvoiceNumber = "FV-MIG", PersonId = O.Ana.Id, SaleDate = Corte.AddDays(-90),
                OriginalAmount = Capital, BalanceAtCutoff = Capital, Outcome = SaleReconciliationOutcome.Migrated, SourceLoanNumber = "CR-100" },
            new MigrationSaleRow { MigrationLoadPartId = ventas.Id, RowNumber = F(), InvoiceNumber = "FV-CAN", PersonId = O.Ana.Id, SaleDate = Corte.AddDays(-40),
                OriginalAmount = 400_000m, ExternalCollections = 400_000m, BalanceAtCutoff = 0m, Outcome = SaleReconciliationOutcome.CancelledBeforeCutoff,
                CreditLineCode = "CONLIB" });
        var historial = Parte(DefinicionesDeMigracion.Partes.ArrearsHistory);
        Db.MigrationArrearsHistoryRows.AddRange(
            new MigrationArrearsHistoryRow { MigrationLoadPartId = historial.Id, RowNumber = F(), SourceLoanNumber = "CR-100", PeriodYear = 2026, PeriodMonth = 10, MaxDaysPastDue = 30 },
            new MigrationArrearsHistoryRow { MigrationLoadPartId = historial.Id, RowNumber = F(), SourceLoanNumber = "CR-100", PeriodYear = 2026, PeriodMonth = 11, MaxDaysPastDue = 60 });
        await Db.SaveChangesAsync();
    }

    private MigrationInstallmentRow Cuota_(MigrationLoadPart p, string numero, int cuota, DateOnly vence, decimal capital, decimal corriente = 0m, decimal mora = 0m) =>
        new() { MigrationLoadPartId = p.Id, RowNumber = ++_fila, SourceLoanNumber = numero, InstallmentNumber = cuota, DueDate = vence, Principal = capital,
            CurrentInterest = corriente, DefaultInterest = mora };

    private MigrationBalanceRow Saldo(MigrationLoadPart p, string numero, LoanConcept concepto, decimal valor) =>
        new() { MigrationLoadPartId = p.Id, RowNumber = ++_fila, SourceLoanNumber = numero, Concept = concepto, Amount = valor };

    // ------------------------------------------------------------------------------------------- aplicar y aceptar --

    public CuadreDeMigracion Cuadre() => new(Db, D.Contabilidad);

    public ConciliacionDeVentasDeMigracion Conciliacion() => new(Db, new ConciliacionDeVentasDeLaCarga(Db, new MensajesEntrantes(Db)));

    public async Task AplicarAsync()
    {
        var r = await new ApplyMigrationLoadCommandHandler(Db, V.Emision, G.Parametros(), G.Actor, G.Reloj, Cuadre(), Conciliacion())
            .Handle(new ApplyMigrationLoadCommand(Carga.PublicId, "Carga de ensayo") { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
    }

    /// <summary>Un punto de sesión cerrado y aprobado del sujeto <c>ActivationDifferences</c> (o el que se diga).</summary>
    public Guid Acta(Guid? sujeto = null, string tipoDeSujeto = LendingDecisionSubjects.ActivationDifferences)
    {
        var organo = G.Organo("CONSEJO" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(), "Consejo de administración", GoverningBodyKind.BoardOfDirectors);
        var sesion = new Session { GoverningBodyId = organo.Id, Kind = SessionKind.Extraordinary, Mode = SessionMode.InPerson, HeldOn = Corte, Status = SessionStatus.Closed,
            CreatedBy = "test" };
        Db.LendingSessions.Add(sesion);
        Db.SaveChanges();
        var punto = new SessionItem
        {
            SessionId = sesion.Id, Order = 1, Kind = SessionItemKind.Decision, Rule = VotingRule.Majority, SubjectType = tipoDeSujeto,
            SubjectPublicId = sujeto ?? Carga.PublicId, Title = "Diferencias y activación", Outcome = CreditDecisionOutcome.Approve, CreatedBy = "test",
        };
        Db.SessionItems.Add(punto);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return punto.PublicId;
    }

    /// <summary>Acepta todas las diferencias vigentes de la carga con un acta, como la analista (no la cajera, que cargó).</summary>
    public async Task AceptarTodoAsync()
    {
        var carga = await Db.MigrationLoads.AsNoTracking().SingleAsync(l => l.Id == Carga.Id);
        var vigentes = await ConciliacionDeVentasDeMigracion.VigentesAsync(Db, carga, carga.CurrentCheckVersion, default);
        G.ActuarComo(O.Analista);
        var r = await new AcceptMigrationDifferencesCommandHandler(Db, new PuntoDeActaCerrado(Db), G.Intervenciones(), G.Actor, G.Reloj)
            .Handle(new AcceptMigrationDifferencesCommand(Carga.PublicId, vigentes.Where(d => !d.Aceptada).Select(d => d.PublicId).ToList(), Acta(),
                "Diferencias del sistema de origen aprobadas por el consejo") { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        G.ActuarComo(V.Cajera);
        Db.ChangeTracker.Clear();
    }

    // ---------------------------------------------------------------------------------------------------- activar --

    /// <summary>
    /// Decisión del dueño del 2026-10-18: los requisitos de la activación se exigen en todos los ambientes. Con <see cref="ConPerdidaEsperada"/> los
    /// requisitos miran la base, así que esta cooperativa los cumple como una real: el Anexo 1 vigente desde el corte, los roles de Cartera de las
    /// plantillas y la oficina Norte (la de los créditos de la carga) vinculada a una sucursal contable. Hasta esa fecha, fuera de producción quedaban
    /// dispensados.
    /// </summary>
    public async Task RequisitosDeLaPlataformaAsync()
    {
        var contenido = IngenIA365ERP.Persistence.Seeding.Parametric.PlatformNationalValuesSeeder.Contenido("sarc-anexo-1.json");
        var replica = new IngenIA365ERP.Domain.Entities.Core.NationalValues.NationalValueReplication
        {
            FromSequence = 1, ToSequence = 1, DocumentCount = 1, Origin = IngenIA365ERP.Domain.Enums.Core.NationalValueReplicationOrigin.ScheduledTask,
            StartedAt = G.UtcNow.AddDays(-1), FinishedAt = G.UtcNow.AddDays(-1), ActorName = "prueba",
        };
        replica.Documents.Add(new IngenIA365ERP.Domain.Entities.Core.NationalValues.RegulatoryDocument
        {
            Kind = nameof(IngenIA365ERP.Domain.Common.NationalValues.RegulatoryDocumentKind.SarcAnexo1), Code = "ANEXO1", ValidFrom = new DateOnly(2026, 1, 1),
            Content = contenido, ContentSha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(contenido))),
            SchemaVersion = 1, Verified = true, Source = "Superintendencia de la Economía Solidaria", SourcePublicId = Guid.NewGuid(), SourceSequence = 90_100,
            CentralRegisteredAt = G.UtcNow.AddDays(-1), KnownAt = G.UtcNow.AddDays(-1),
        });
        Db.NationalValueReplications.Add(replica);
        foreach (var plantilla in IngenIA365ERP.Application.Security.Roles.Plantillas.PerfilesSugeridos.DelModulo(
                     IngenIA365ERP.Application.Security.Roles.Plantillas.PerfilesSugeridos.ModuloCartera))
        {
            var codigo = IngenIA365ERP.Application.Security.Roles.Plantillas.CreateSuggestedLendingRolesCommand.CodigoDeRol(plantilla.Key);
            if (!await Db.Roles.AnyAsync(r => r.Code == codigo))
                Db.Roles.Add(new IngenIA365ERP.Domain.Entities.Security.Role { Code = codigo, Name = plantilla.Actor, CreatedBy = "test" });
        }
        var norte = await Db.Branches.SingleAsync(b => b.Id == O.Norte.Id);
        norte.TenantBranchPublicId ??= Guid.NewGuid();
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    /// <summary>K6a (T1020): los requisitos con la normativa, el perfil del doble y los parámetros, para evaluar la pérdida esperada.</summary>
    public bool ConPerdidaEsperada { get; set; }

    public RequisitosDeActivacion Requisitos() => ConPerdidaEsperada
        ? new(new RegistroDeIntegracion(Db, [new TesoreriaDePrueba()], Ambiente), D.Contabilidad, Ambiente, Db, new LectorDeDocumentoNormativo(Db), G.Perfil,
            O.L.Valores(), null, D.Parametros())
        : new(new RegistroDeIntegracion(Db, [new TesoreriaDePrueba()], Ambiente), D.Contabilidad, Ambiente);

    public ValidacionDeCobrosProvisionales Cobros() => new(Db, Lector, G.Permisos, new MensajesEntrantes(Db), Almacen);

    public ActivateLendingCommandHandler Activador(bool conRevalidacion = true) => new(
        Db, O.Cerrojo, Requisitos(), new EscritorDelRegistroDeIntegracion(Db, Ambiente, G.Actor, G.Reloj), V.Emision, new PuntoDeActaCerrado(Db), G.Actor,
        G.Reloj,
        new CierreBase(Db, new LectorDeDocumentoNormativo(Db), G.Reloj),
        new RecalculoDelCuadreDeActivacion(Db, D.Contabilidad, new MensajesEntrantes(Db)),
        Cobros(),
        new NacimientoDeCreditosMigrados(Db, D.Registro(), D.Numerador(), G.Reloj, new CalendarioDeDiasHabiles(Db)),
        new NacimientoDeLoDelDeudorMigrado(Db, G.Reloj, O.Autorizaciones, D.Parametros(), O.L.Valores(), new CalendarioDeDiasHabiles(Db)),
        D.Numerador(),
        D.Registro(),
        conRevalidacion ? new RevalidacionDeCuposMigrados(Db, new CargaVigenteDeCartera(Db), O.Revalidacion(), D.Alertas, G.Actor, G.Reloj) : null,
        D.Alertas,
        G.Auditoria);

    public ActivateLendingCommand Orden(Guid punto, DateOnly? fecha = null, Guid? cobros = null) =>
        new(Carga.PublicId, fecha ?? Activacion, cobros, punto, AcknowledgeWarnings: true, "Arranque de Cartera con la cartera migrada",
            PayrollWarningAcknowledged: true) { OperationKey = Guid.NewGuid() };

    /// <summary>Activa con el registro tres días después del corte.</summary>
    public async Task<Result<LendingActivationDto>> ActivarAsync(ActivateLendingCommand orden, DateOnly? registro = null)
    {
        G.HoyLocal = registro ?? Registro;
        var r = await Activador().Handle(orden, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>Un archivo de cobros provisionales ya revisado: su adjunto de módulo con el contenido que el lector doble entrega.</summary>
    public async Task<Guid> AdjuntoDeCobrosAsync(params (string Factura, string Valor, string Fecha, string Referencia)[] filas)
    {
        var encabezados = DefinicionesDeMigracion.CobrosProvisionales.Hojas[0].Columnas.Select(c => c.Nombre).ToList();
        Tabla = new TablaLeida(encabezados, filas.Select((f, i) => new FilaLeida(i + 2, encabezados.Select(e => e switch
        {
            DefinicionesDeMigracion.Columnas.Factura => f.Factura,
            DefinicionesDeMigracion.Columnas.Valor => f.Valor,
            DefinicionesDeMigracion.Columnas.FechaValor => f.Fecha,
            DefinicionesDeMigracion.Columnas.ReferenciaContable => f.Referencia,
            _ => (string?)null,
        }).ToArray())).ToList(), "xlsx");
        var contenido = Encoding.UTF8.GetBytes("cobros-provisionales:" + Guid.NewGuid());
        var adjunto = new Attachment
        {
            OwnerEntityType = IngenIA365ERP.Application.Attachments.Common.AdjuntosDeModulo.CobrosProvisionales, OwnerEntityPublicId = Carga.PublicId, FileName = "cobros.xlsx",
            ContentType = ReviewProvisionalCollectionsCommandHandler.TipoXlsx, SizeBytes = contenido.Length, Sha256Hex = Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(contenido)), StoragePath = "cobros/" + Guid.NewGuid().ToString("N"), CreatedBy = "test",
        };
        Db.Attachments.Add(adjunto);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        Almacen.GetAsync(Arg.Is<BlobReference>(b => b.Uri == adjunto.StoragePath), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream(contenido)));
        return adjunto.PublicId;
    }
}
