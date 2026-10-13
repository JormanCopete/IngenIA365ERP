using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// El escenario del cierre mensual (feature 013, US9, bloque 12B): el de <see cref="CausacionDePrueba"/> —InMemory, hoy local 2026-12-01, el crédito de
/// Ana (3.000.000 al 20 % EA, 24 cuotas, línea <c>CONLIB</c> de consumo sin libranza)— con el Anexo 1 sembrado, los tipos de hecho de Cartera, el límite
/// <c>DETERIORO_GENERAL_MINIMO</c> (1 %) y el <b>cierre base</b> de la activación al 2026-11-30, sin líneas (el corte a fin de noviembre: el primer
/// cierre mensual es el de diciembre, comparado con el base). Las corridas se arman con la orden real y se conducen a mano. (nuevo)
/// </summary>
internal sealed class CierreDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = CausacionDePrueba.Hoy;
    public static readonly DateOnly CorteDeLaActivacion = new(2026, 11, 30);
    public static readonly DateOnly FinDeDiciembre = new(2026, 12, 31);

    private CierreDePrueba(CausacionDePrueba c) => C = c;

    public static async Task<CierreDePrueba> CrearAsync(bool conBase = true)
    {
        var c = await CausacionDePrueba.CrearAsync();
        await c.AnexoAsync(new DateOnly(2026, 1, 1));
        await c.SembrarTiposDeHechoAsync();
        c.D.G.Limite(CatalogoDeLimitesLegales.DeterioroGeneralMinimo, 0.01m, "Circular Básica Contable y Financiera, Anexo 1");
        var p = new CierreDePrueba(c);
        if (conBase) p.Base = await p.BaseAsync();
        p.C.ActorDelProceso.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Actor.ProcesoDeIntegracion("Tarea:lending.process")));
        return p;
    }

    public CausacionDePrueba C { get; }
    public Application.Tests.Lending.Disbursements.DesembolsoDePrueba D => C.D;
    public Application.Tests.Lending.Sessions.GobiernoDePrueba G => C.D.G;
    public TestApplicationDbContext Db => C.Db;
    public Loan Credito => C.Credito;
    public MonthlyClose? Base { get; private set; }

    /// <summary>El usuario que ordena y reabre los cierres (el jefe de cartera de la escena).</summary>
    public Domain.Entities.Security.User Jefe => D.O.L.Jefe;

    public void Dispose() => C.Dispose();

    // ------------------------------------------------------------------------------------------------------- piezas --

    public LectorDelContextoDeRiesgo Contextos() => new(new LectorDeDocumentoNormativo(Db), G.Valores, D.Parametros(),
        new LectorDeIndicadoresDeLaEntidad(D.Contabilidad, D.Parametros()), G.Perfil, new PoliticaSarcVigente(Db));
    public CargadorDeRiesgo Cargador() => new(Db, C.Cargador());
    public RequisitosDelCierreMensual Requisitos() => new(Db, D.Contabilidad, G.Reloj);
    public CierreMensualDeCartera Cierre() => new(Db, D.O.Cerrojo, Requisitos(), Contextos(), Cargador(), C.EmisionDelProceso(), C.Tanda, G.Reloj);
    public ExecuteMonthlyCloseCommandHandler Ejecutar() => new(Db, Cierre(), G.Reloj);
    public ReaperturaDelCierre Reapertura() => new(Db, D.O.Cerrojo, Requisitos(), C.EmisionDelProceso(), G.Reloj);
    public OrderMonthlyCloseCommandHandler Ordenar(ReplicacionDeValoresNacionales? replicacion = null) => new(Db, Requisitos(), G.Actor, G.Reloj, replicacion);
    public ReopenMonthlyCloseCommandHandler Reabrir() => new(Db, Requisitos(), G.Actor, G.Reloj);
    public CalificacionInformativa Informativa() => new(Db, Contextos(), Cargador());

    // ---------------------------------------------------------------------------------------------------- escenario --

    /// <summary>El cierre base de la activación al corte, sin líneas (C107).</summary>
    public async Task<MonthlyClose> BaseAsync(DateOnly? corte = null)
    {
        var fecha = corte ?? CorteDeLaActivacion;
        var cierre = new MonthlyClose
        {
            Kind = MonthlyCloseKind.Base, PeriodYear = (short)fecha.Year, PeriodMonth = (byte)fecha.Month, Version = 1, CloseDate = fecha,
            InformationCutoffAt = G.UtcNow.AddDays(-1), DocumentsUsedJson = "[]", ModelBySegmentJson = "[]", TotalsJson = "{}",
            LinesSha256 = new string('0', 64), OrderedAt = G.UtcNow.AddDays(-1), ClosedAt = G.UtcNow.AddDays(-1),
        };
        Db.MonthlyCloses.Add(cierre);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return cierre;
    }

    /// <summary>Una línea del base para el crédito, como si se hubiera migrado con su calificación y su deterioro.</summary>
    public async Task<MonthlyCloseLoan> LineaDelBaseAsync(RatingCategory categoria, decimal deterioroDeCapital, decimal capital = 3_000_000m)
    {
        var linea = new MonthlyCloseLoan
        {
            MonthlyCloseId = Base!.Id, LoanId = Credito.Id, PersonId = Credito.DebtorPersonId, OfficePublicId = Guid.NewGuid(), BranchId = Credito.BranchId,
            CreditLineId = Credito.CreditLineId, ModalityCode = "CONSUMO", SubsegmentCode = "SinLibranza", SarcSegmentCode = "CONSUMO",
            ImpairmentModel = ImpairmentModel.ArrearsAnnex1, ModelBasis = ModelBasis.NotObligated, ArrearsRating = categoria, FinalRating = categoria,
            AccountingRating = categoria, RatingCause = RatingCause.Arrears, PrincipalBalance = capital, PrincipalImpairment = deterioroDeCapital,
            TotalImpairment = deterioroDeCapital, BalancesByConceptJson = $$"""{"Principal": {{capital}}}""",
        };
        Db.MonthlyCloseLoans.Add(linea);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return linea;
    }

    /// <summary>Causa diciembre entero (el ciclo del 31) y deja el reloj el 2 de enero, con el jefe de cartera actuando.</summary>
    public async Task CausarDiciembreAsync()
    {
        await C.CausarHastaAsync(FinDeDiciembre);
        IrA(new DateOnly(2027, 1, 2));
    }

    /// <summary>Mueve el reloj y actúa como el jefe de cartera.</summary>
    public void IrA(DateOnly fecha)
    {
        C.Ir(fecha);
        G.ActuarComo(Jefe);
    }

    /// <summary>Ordena el cierre del mes (con clave) y devuelve el resultado.</summary>
    public Task<Result<ProcessRunRequestedDto>> OrdenarAsync(int anio, int mes, ReplicacionDeValoresNacionales? replicacion = null) =>
        Ordenar(replicacion).Handle(new OrderMonthlyCloseCommand(anio, mes) { OperationKey = Guid.NewGuid() }, default);

    /// <summary>Conduce una corrida del cierre tanda por tanda hasta que termina (como el conductor) y la devuelve releída.</summary>
    public async Task<LendingProcessRun> ConducirAsync(Guid corrida, int maximo = 20)
    {
        var terminada = false;
        for (var i = 0; i < maximo && !terminada; i++)
        {
            var r = await Ejecutar().Handle(new ExecuteMonthlyCloseCommand(corrida), default);
            r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
            terminada = r.Value == AvanceDeCorrida.Terminada;
        }
        terminada.Should().BeTrue($"el cierre termina en a lo sumo {maximo} tandas");
        Db.ChangeTracker.Clear();
        return await Db.LendingProcessRuns.AsNoTracking().SingleAsync(r => r.PublicId == corrida);
    }

    /// <summary>Ordena y conduce el cierre del mes; devuelve el cierre.</summary>
    public async Task<MonthlyClose> CerrarAsync(int anio, int mes)
    {
        var orden = await OrdenarAsync(anio, mes);
        orden.IsSuccess.Should().BeTrue(orden.IsFailure ? $"{orden.Error.Code}: {orden.Error.Message}" : string.Empty);
        var corrida = await ConducirAsync(orden.Value.ProcessRunPublicId);
        corrida.Status.Should().Be(LendingProcessRunStatus.Completed);
        return await Db.MonthlyCloses.AsNoTracking().Where(c => c.ProcessRunId == corrida.Id).SingleAsync();
    }

    /// <summary>Ordena y conduce la reapertura del cierre.</summary>
    public async Task<LendingProcessRun> ReabrirAsync(MonthlyClose cierre, string motivo = "Calificación por análisis registrada tarde")
    {
        var orden = await Reabrir().Handle(new ReopenMonthlyCloseCommand(cierre.PublicId, motivo) { OperationKey = Guid.NewGuid() }, default);
        orden.IsSuccess.Should().BeTrue(orden.IsFailure ? $"{orden.Error.Code}: {orden.Error.Message}" : string.Empty);
        var corrida = await Db.LendingProcessRuns.SingleAsync(r => r.PublicId == orden.Value.ProcessRunPublicId);
        corrida.Status = LendingProcessRunStatus.Running;
        var avance = await Reapertura().TandaAsync(corrida, default);
        avance.Should().Be(AvanceDeCorrida.Terminada);
        corrida.Status = LendingProcessRunStatus.Completed;
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return await Db.LendingProcessRuns.AsNoTracking().SingleAsync(r => r.Id == corrida.Id);
    }
}
