using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>Los pasos del cierre como se anotan en <c>LND_ProcessRuns.CurrentStep</c> y <c>LND_ProcessRunErrors.Step</c> (data-model §22.1). (nuevo)</summary>
public static class PasosDelCierre
{
    public const byte Lineas = 1;
    public const byte TransaccionFinal = 2;

    public static string Nombre(byte paso) => paso == TransaccionFinal ? "CierreFinal" : "CierreLineas";
}

/// <summary>
/// El cierre mensual de Cartera (feature 013, US9, T0705; T47, T48; data-model §23.1–§23.5; contracts/contabilidad.md §11; mensajes.md §10.17,
/// §10.23), en dos pasos conducidos por <see cref="ConductorDeCorridasDeCartera"/>:
/// <list type="number">
/// <item><b>Líneas</b>, tanda por tanda en orden de crédito desde <c>CursorLoanId</c>: la primera revalida los requisitos, sincroniza la réplica y
/// sella R y la marca de agua; cada tanda carga el riesgo (<see cref="CargadorDeRiesgo"/>, con todos los créditos de sus deudores para el
/// arrastre), califica y deteriora (<see cref="CalculoDeRiesgo"/>) y guarda <c>LND_MonthlyCloseLoans</c> y el historial del mes enlazados a la
/// corrida, <b>sin</b> mensajes; un crédito que falla queda en <c>LND_ProcessRunErrors</c> y el cierre no se hace (la corrida termina con errores y
/// se reintenta: el reintento retoma lo que falta y conserva R).</item>
/// <item><b>Transacción final</b>, con <c>LND_Setup</c> exclusivo: revalida los requisitos, exige una línea por crédito vivo, calcula el general,
/// inserta el encabezado (R, marca de agua, documentos con SHA-256, política, modelo por segmento y marcas), el general, las partes con sus hechos
/// —primero <c>CarteraReclasificadaPorCalificacion</c> de los que cambiaron de letra, después <c>DeterioroCalculado</c> en delta con su movimiento,
/// el general y el adicional—, el informativo <c>CierreDeCarteraRealizado</c> y el lote <c>LendingMonthlyClose</c> con
/// <c>LendingClose:{aaaa-mm}</c>, y deja <c>TotalsJson</c> y <c>ResultSha256</c> como evento resumen de la corrida.</item>
/// </list>
/// Todo se fecha el último día del mes (T36). El primer cierre tras activar se compara con el base y no reconoce otra vez el deterioro migrado
/// (US8-5, SC-013). (nuevo)
/// </summary>
public sealed class CierreMensualDeCartera(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    RequisitosDelCierreMensual requisitos,
    LectorDelContextoDeRiesgo contextos,
    CargadorDeRiesgo cargador,
    EmisionDeCartera emision,
    ParametrosDeLaTandaDeCartera tanda,
    IDateTimeService reloj,
    ReplicacionDeValoresNacionales? replicacion = null,
    ILogger<CierreMensualDeCartera>? logger = null)
{
    private const int LargoDelMensaje = 1000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ILogger _logger = logger ?? (ILogger)NullLogger.Instance;

    /// <summary>Una tanda de la corrida <c>MonthlyClose</c>.</summary>
    public async Task<AvanceDeCorrida> TandaAsync(LendingProcessRun corrida, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (corrida.Kind != LendingProcessKind.MonthlyClose || corrida.PeriodYear is not { } anio || corrida.PeriodMonth is not { } mes)
            throw new InvalidOperationException("La tanda del cierre es de una corrida MonthlyClose con su mes.");
        var fin = MonthlyClose.UltimoDiaDe(anio, mes);

        if (corrida.InformationCutoffAt is null) await EmpezarAsync(corrida, anio, mes, fin, ct);
        if (corrida.CurrentStep == PasosDelCierre.TransaccionFinal) return await TransaccionFinalAsync(corrida, anio, mes, fin, ct);

        corrida.CurrentStep = PasosDelCierre.Lineas;
        var r = corrida.InformationCutoffAt!.Value;
        var cursor = corrida.CursorLoanId ?? 0;
        var conLinea = db.MonthlyCloseLoans.AsNoTracking().Where(l => l.ProcessRunId == corrida.Id).Select(l => l.LoanId);
        var ids = await CargadorDeRiesgo.VivosAl(db.Loans.AsNoTracking(), fin).Where(l => l.Id > cursor && !conLinea.Contains(l.Id))
            .OrderBy(l => l.Id).Select(l => l.Id).Take(Math.Max(1, tanda.TamanoDeTanda)).ToListAsync(ct);
        if (ids.Count == 0)
        {
            corrida.CurrentStep = PasosDelCierre.TransaccionFinal;
            // Con créditos sin línea el cierre no se hace: la corrida termina con errores y se reintenta.
            return corrida.FailedLoans > 0 ? AvanceDeCorrida.Terminada : AvanceDeCorrida.Pendiente;
        }

        var contexto = await contextos.LeerAsync(fin, r, paraElCierre: true, ct);
        if (contexto.IsFailure) throw new ReglaDeCarteraException(new ProblemaDeCartera(contexto.Error.Code, contexto.Error.Message));
        var anterior = RequisitosDelCierreMensual.Anterior(await RequisitosDelCierreMensual.CierresVigentesAsync(db, ct), anio, mes);
        var datos = await cargador.CargarAsync(ids, fin, r, contexto.Value, anterior, ct);
        var calculo = CalculoDeRiesgo.Calcular(datos, contexto.Value, conArrastre: true);

        var numeroDeTanda = corrida.BatchCount + 1;
        var errores = new List<LendingProcessRunError>();
        await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            foreach (var calculado in calculo.Calculados)
            {
                var linea = CalculoDeRiesgo.Linea(calculado, corrida.Id, numeroDeTanda, null);
                db.MonthlyCloseLoans.Add(linea);
                db.LoanArrearsHistory.Add(new LoanArrearsHistoryEntry
                {
                    LoanId = linea.LoanId, PeriodYear = anio, PeriodMonth = mes, MaxDaysPastDue = linea.MaxDaysPastDueInMonth,
                    DaysPastDueAtMonthEnd = linea.DaysPastDueAtClose, Origin = ArrearsHistoryOrigin.MonthlyClose, ProcessRunId = corrida.Id,
                });
            }
            foreach (var (loanId, error) in calculo.Errores.Where(e => ids.Contains(e.Key)))
            {
                var fila = Error(corrida, loanId, numeroDeTanda, PasosDelCierre.Lineas, error.Code, error.Message);
                db.LendingProcessRunErrors.Add(fila);
                errores.Add(fila);
            }
            // Los contadores van en el mismo guardado que las líneas (la corrida está en seguimiento).
            corrida.FailedLoans += errores.Count;
            corrida.ProcessedLoans += calculo.Calculados.Count;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

        // Sin seguimiento entre tandas: la corrida sigue; el cursor se mueve después de soltar, para que el guardado de quien conduce lo vea.
        db.DescartarCambiosSalvo([corrida]);
        corrida.CursorLoanId = ids.Max();
        if (errores.Count > 0)
            _logger.LogWarning("[Cartera.CierreConErrores] La tanda {Tanda} del cierre {Mes} dejó {N} créditos sin línea.", numeroDeTanda,
                ErroresDeRiesgo.Mes(anio, mes), errores.Count);
        return AvanceDeCorrida.Pendiente;
    }

    /// <summary>La primera tanda: requisitos, réplica, R y la marca de agua, y cuántos créditos se van a cerrar.</summary>
    private async Task EmpezarAsync(LendingProcessRun corrida, int anio, int mes, DateOnly fin, CancellationToken ct)
    {
        var evaluacion = await requisitos.EvaluarAsync(anio, mes, corrida.Id, ct);
        if (evaluacion.PrimerError is { } error) throw new ReglaDeCarteraException(new ProblemaDeCartera(error.Code, error.Message));
        if (replicacion is not null)
        {
            var replica = await replicacion.AplicarAsync(NationalValueReplicationOrigin.MonthlyClose, corrida.RequestedByName ?? Actor.NombreDelProceso, ct);
            if (!replica.CentralDisponible)
            {
                var e = ErroresDeRiesgo.ReplicaNoDisponible(replica.Motivo);
                throw new ReglaDeCarteraException(new ProblemaDeCartera(e.Code, e.Message));
            }
        }
        corrida.InformationCutoffAt = reloj.UtcNow;
        corrida.NationalValuesWatermark = await db.NationalValueReplications.AsNoTracking().MaxAsync(x => (long?)x.ToSequence, ct) ?? 0L;
        corrida.TotalLoans = await CargadorDeRiesgo.VivosAl(db.Loans.AsNoTracking(), fin).CountAsync(ct);
        corrida.CurrentStep = PasosDelCierre.Lineas;
    }

    // ------------------------------------------------------------------------------------------------ transacción final --

    private async Task<AvanceDeCorrida> TransaccionFinalAsync(LendingProcessRun corrida, int anio, int mes, DateOnly fin, CancellationToken ct)
    {
        var corridaId = corrida.Id;
        var resultado = await TransaccionExplicita.EjecutarAsync(db, () => FinalAsync(corridaId, anio, mes, fin, ct), ct);
        if (resultado.IsFailure) throw new ReglaDeCarteraException(new ProblemaDeCartera(resultado.Error.Code, resultado.Error.Message));
        var (totales, huella) = resultado.Value;
        // La corrida pudo haberse descartado al repetir la transacción: se toma la que está en seguimiento.
        var seguida = db.LendingProcessRuns.Local.FirstOrDefault(x => x.Id == corridaId) ?? corrida;
        seguida.TotalsJson = totales;
        seguida.ResultSha256 = huella;
        seguida.CurrentStep = PasosDelCierre.TransaccionFinal;
        return AvanceDeCorrida.Terminada;
    }

    private async Task<Result<(string Totales, string Huella)>> FinalAsync(int corridaId, int anio, int mes, DateOnly fin, CancellationToken ct)
    {
        await cerrojo.TomarSetupAsync(exclusivo: true, ct);
        var corrida = db.LendingProcessRuns.Local.FirstOrDefault(x => x.Id == corridaId) ?? await db.LendingProcessRuns.FirstAsync(x => x.Id == corridaId, ct);
        var evaluacion = await requisitos.EvaluarAsync(anio, mes, corrida.Id, ct);
        if (evaluacion.PrimerError is { } bloqueo) return Result.Failure<(string, string)>(bloqueo);

        var lineas = await db.MonthlyCloseLoans.AsNoTracking().Where(l => l.ProcessRunId == corrida.Id).OrderBy(l => l.LoanId).ToListAsync(ct);
        var conLinea = lineas.Select(l => l.LoanId).ToHashSet();
        var vivos = await CargadorDeRiesgo.VivosAl(db.Loans.AsNoTracking(), fin).Select(l => l.Id).ToListAsync(ct);
        var faltantes = vivos.Count(id => !conLinea.Contains(id));
        if (faltantes > 0) return Result.Failure<(string, string)>(ErroresDeRiesgo.LineasIncompletas(faltantes));

        var r = corrida.InformationCutoffAt!.Value;
        var contextoLeido = await contextos.LeerAsync(fin, r, paraElCierre: true, ct);
        if (contextoLeido.IsFailure) return Result.Failure<(string, string)>(contextoLeido.Error);
        var contexto = contextoLeido.Value;
        var anterior = evaluacion.Anterior;
        var regla = MovimientosDelDeterioro.Regla(contexto.DisminucionDeDeterioro);

        // El general por dimensión frente al del cierre anterior.
        var generalAnterior = anterior is null
            ? []
            : await db.MonthlyCloseGeneralImpairments.AsNoTracking().Where(g => g.MonthlyCloseId == anterior.Id).ToListAsync(ct);
        var general = MotorDeDeterioroGeneral.Calcular(
            lineas.Select(l => new LineaParaGeneral(Dimension(l), l.PrincipalBalance, l.TotalImpairment)),
            contexto.General!,
            generalAnterior.GroupBy(DimensionDe).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalAmount)),
            contexto.ModelosConGeneral,
            contexto.Redondeo);

        var ahora = reloj.UtcNow;
        var cierre = new MonthlyClose
        {
            Kind = MonthlyCloseKind.Monthly,
            PeriodYear = (short)anio,
            PeriodMonth = (byte)mes,
            Version = corrida.CloseVersion ?? evaluacion.Version,
            CloseDate = fin,
            ProcessRunId = corrida.Id,
            PreviousCloseId = anterior?.Id,
            SupersedesCloseId = evaluacion.Reemplaza?.Id,
            InformationCutoffAt = r,
            NationalValuesWatermark = corrida.NationalValuesWatermark ?? 0L,
            DocumentsUsedJson = JsonSerializer.Serialize(contexto.DocumentosUsados, Json),
            RiskPolicyVersionId = contexto.Politica?.Version.Id,
            ModelBySegmentJson = JsonSerializer.Serialize(contexto.Seleccion.Segmentos.Select(s => new
            {
                segment = s.Segmento, model = s.Modelo.ToString(), basis = s.Fundamento.ToString(), obligatoryModelUnavailable = s.ModeloObligatorioNoDisponible,
            }), Json),
            ObligatoryModelUnavailable = contexto.Seleccion.ModeloObligatorioNoDisponible,
            UnvalidatedParameters = contexto.ConParametrosSinValidar,
            UnvalidatedParametersJson = contexto.SinValidarJson,
            IncompleteHistory = false,
            LoanCount = lineas.Count,
            TotalPrincipal = lineas.Sum(l => l.PrincipalBalance),
            TotalInterest = lineas.Sum(l => l.CurrentInterestBalance + l.DefaultInterestBalance),
            TotalIndividualImpairment = lineas.Sum(l => l.TotalImpairment),
            TotalGeneralImpairment = general.Sum(g => g.Total),
            TotalsJson = Totales(lineas),
            LinesSha256 = PartesDelCierre.HuellaDeLineas(lineas),
            OrderedByUserId = corrida.RequestedByKind == ActorKind.Person ? corrida.RequestedByUserId : null,
            OrderedAt = corrida.RequestedAt,
            ClosedAt = ahora,
        };
        db.MonthlyCloses.Add(cierre);
        await db.SaveChangesAsync(ct);

        foreach (var g in general)
        {
            var previo = generalAnterior.FirstOrDefault(x => DimensionDe(x) == g.Dimension);
            db.MonthlyCloseGeneralImpairments.Add(new MonthlyCloseGeneralImpairment
            {
                MonthlyCloseId = cierre.Id, BranchId = g.Dimension.BranchId, ModalityCode = g.Dimension.ModalityCode, SubsegmentCode = g.Dimension.SubsegmentCode,
                IsEmployee = g.Dimension.IsEmployee, ImpairmentModel = g.Dimension.Modelo, GrossPortfolio = g.CarteraBruta, MinimumPercentage = g.PorcentajeMinimo,
                AdditionalPercentage = g.PorcentajeAdicional, MinimumAmount = g.MontoMinimo, AdditionalAmount = g.MontoAdicional,
                CompensationOfIndividual = g.CompensacionDelIndividual, TotalAmount = g.Total, PreviousAmount = previo?.TotalAmount ?? g.Anterior,
                Delta = g.Total - (previo?.TotalAmount ?? g.Anterior),
                SourceJson = JsonSerializer.Serialize(new { source = g.Fuente, policyVersion = contexto.Politica?.Version.VersionNumber }, Json),
            });
        }

        // Las partes, en orden: reclasificación y después deterioro (T48).
        var historia = await HistoriaDelEjercicioAsync(anterior, anio, lineas.Select(l => l.LoanId).ToList(), ct);
        var lineasAnteriores = anterior is null
            ? new Dictionary<int, MonthlyCloseLoan>()
            : await CategoriaContableDeCartera.LineasDe(db, anterior).ToDictionaryAsync(l => l.LoanId, ct);
        var liberados = lineasAnteriores.Values.Where(a => !conLinea.Contains(a.LoanId) && a.TotalImpairment != 0m).ToList();
        // Un crédito sin línea en el cierre anterior (nació después, o el anterior es el base sin él) llevó en sus hechos la categoría contable
        // vigente —la de crédito nuevo o, si es una novación, la del crédito anterior (§3.4)—: si el cierre lo deja en otra letra, sus saldos se
        // reclasifican desde esa. Hasta el bloque 12C sólo se reclasificaba a quien tenía línea anterior, y los saldos de un crédito nuevo que el
        // primer cierre llevaba a C quedaban en las cuentas de A (la conciliación por categoría no cuadraba).
        var sinLineaAnterior = lineas.Where(l => !lineasAnteriores.ContainsKey(l.LoanId)).Select(l => l.LoanId).ToList();
        IReadOnlyDictionary<int, CategoriaContableVigente> categoriasPrevias = sinLineaAnterior.Count == 0
            ? new Dictionary<int, CategoriaContableVigente>()
            : await new CategoriaContableDeCartera(db).VigentesAsync(sinLineaAnterior, fin, ct);
        var referencias = await PartesDelCierre.ReferenciasAsync(db, [.. lineas, .. liberados], ct);
        var usuario = corrida.RequestedByKind == ActorKind.Person && corrida.RequestedByCentralUserId is { } central
            ? new UserRefV1 { CentralUserId = central, Name = corrida.RequestedByName }
            : null;
        var ultimaTanda = Math.Max(1, lineas.Select(l => l.BatchNumber ?? 1).DefaultIfEmpty(1).Max()) + 1;

        var reclasificaciones = new List<PartidaDelCierre>();
        var deterioros = new List<PartidaDelCierre>();
        foreach (var l in lineas)
        {
            lineasAnteriores.TryGetValue(l.LoanId, out var previa);
            var nuevas = PartesDelCierre.Dimensiones(l, l.AccountingRating, referencias);
            var categoriaPrevia = previa?.AccountingRating
                                  ?? (categoriasPrevias.TryGetValue(l.LoanId, out var vigente) && Enum.TryParse<RatingCategory>(vigente.Category, out var deLosHechos)
                                      ? (RatingCategory?)deLosHechos
                                      : null);
            if (categoriaPrevia is { } desde && desde != l.AccountingRating)
            {
                var importes = PartesDelCierre.Reclasificacion(l, previa);
                if (importes.Count > 0)
                    reclasificaciones.Add(new PartidaDelCierre(l.LoanId, l.BranchId, l.BatchNumber ?? 1, new LoanFactLineV1
                    {
                        Loan = referencias.Creditos[l.LoanId], DebtorPersonPublicId = referencias.Personas.GetValueOrDefault(l.PersonId), Dimensions = nuevas,
                        PreviousDimensions = PartesDelCierre.Dimensiones(l, desde, referencias), Amounts = importes,
                    }));
            }
            var movimientos = Movimientos(PartesDelCierre.Deterioro(previa), PartesDelCierre.Deterioro(l), historia.GetValueOrDefault(l.LoanId), regla);
            var delta = PartesDelCierre.DeterioroEnDelta(movimientos);
            if (delta.Count > 0)
                deterioros.Add(new PartidaDelCierre(l.LoanId, l.BranchId, l.BatchNumber ?? 1, new LoanFactLineV1
                {
                    Loan = referencias.Creditos[l.LoanId], DebtorPersonPublicId = referencias.Personas.GetValueOrDefault(l.PersonId), Dimensions = nuevas,
                    Amounts = delta,
                }));
        }
        // Los créditos que salieron del cierre (cancelados en el mes) liberan su deterioro en su categoría anterior.
        foreach (var a in liberados)
        {
            var movimientos = Movimientos(PartesDelCierre.Deterioro(a), new Dictionary<LoanConcept, decimal>(), historia.GetValueOrDefault(a.LoanId), regla);
            var delta = PartesDelCierre.DeterioroEnDelta(movimientos);
            if (delta.Count > 0)
                deterioros.Add(new PartidaDelCierre(a.LoanId, a.BranchId, ultimaTanda, new LoanFactLineV1
                {
                    Loan = referencias.Creditos[a.LoanId], DebtorPersonPublicId = referencias.Personas.GetValueOrDefault(a.PersonId),
                    Dimensions = PartesDelCierre.Dimensiones(a, a.AccountingRating, referencias), Amounts = delta,
                }));
        }
        var generales = await GeneralEnDeltaAsync(general, generalAnterior, anterior, anio, regla, ct);

        var partes = new List<AccountingFactPart>();
        var resumen = new List<FactPartSummaryV1>();
        foreach (var g in reclasificaciones.GroupBy(p => (p.Tanda, p.BranchId)).OrderBy(g => g.Key.Tanda).ThenBy(g => g.Key.BranchId))
        {
            var parte = await ParteAsync(corrida, cierre, CarteraReclasificadaPorCalificacionV1.Type, g.Key.Tanda, g.Key.BranchId, g.Select(p => p.Linea).ToList(),
                null, referencias, usuario, ct);
            if (parte.IsFailure) return Result.Failure<(string, string)>(parte.Error);
            partes.Add(parte.Value);
        }
        var sucursalesDelGeneral = generales.Select(x => x.BranchId).Distinct().ToList();
        var gruposDeDeterioro = deterioros.GroupBy(p => (p.Tanda, p.BranchId)).Select(g => (g.Key.Tanda, g.Key.BranchId, Lineas: g.Select(p => p.Linea).ToList())).ToList();
        foreach (var sucursal in sucursalesDelGeneral.Where(s => !gruposDeDeterioro.Any(g => g.Tanda == ultimaTanda && g.BranchId == s)))
            gruposDeDeterioro.Add((ultimaTanda, sucursal, []));
        foreach (var (numero, sucursal, lineasDelGrupo) in gruposDeDeterioro.OrderBy(g => g.Tanda).ThenBy(g => g.BranchId))
        {
            var delGeneral = numero == ultimaTanda ? generales.Where(x => x.BranchId == sucursal).Select(x => x.Linea).ToList() : null;
            var parte = await ParteAsync(corrida, cierre, DeterioroCalculadoV1.Type, numero, sucursal, lineasDelGrupo,
                delGeneral is { Count: > 0 } ? delGeneral : null, referencias, usuario, ct);
            if (parte.IsFailure) return Result.Failure<(string, string)>(parte.Error);
            partes.Add(parte.Value);
        }
        resumen.AddRange(partes.GroupBy(p => p.FactType).Select(g => new FactPartSummaryV1 { Type = g.Key, Parts = g.Count(), Total = g.Sum(p => p.TotalAmount) }));

        var marcas = new List<string>();
        if (cierre.ObligatoryModelUnavailable) marcas.Add(nameof(MonthlyClose.ObligatoryModelUnavailable));
        if (cierre.UnvalidatedParameters) marcas.Add(nameof(MonthlyClose.UnvalidatedParameters));
        if (cierre.IncompleteHistory) marcas.Add(nameof(MonthlyClose.IncompleteHistory));
        var informativo = await emision.CierreRealizadoAsync(cierre, new CierreDeCarteraRealizadoV1
        {
            Year = anio, Month = mes, CloseVersion = cierre.Version, Kind = cierre.Kind.ToString(),
            InformationCutoffAt = new DateTimeOffset(DateTime.SpecifyKind(r, DateTimeKind.Utc)), Marks = marcas, FactParts = resumen,
        }, usuario, ct);
        if (informativo.IsFailure) return Result.Failure<(string, string)>(informativo.Error);

        await db.SaveChangesAsync(ct);
        await LoteAsync(fin, ct);

        var totales = JsonSerializer.Serialize(new
        {
            loans = lineas.Count, principal = cierre.TotalPrincipal, individualImpairment = cierre.TotalIndividualImpairment,
            generalImpairment = cierre.TotalGeneralImpairment, parts = resumen, monthlyClosePublicId = cierre.PublicId,
        }, Json);
        return Result.Success((totales, cierre.LinesSha256));
    }

    /// <summary>Una parte del cierre: su mensaje masivo (clave <c>Close:{versión}</c>) y su fila, con los totales y la huella de la lista.</summary>
    private async Task<Result<AccountingFactPart>> ParteAsync(LendingProcessRun corrida, MonthlyClose cierre, string tipo, int numeroDeTanda, int sucursal,
        IReadOnlyList<LoanFactLineV1> lineas, IReadOnlyList<GeneralImpairmentLineV1>? general, ReferenciasDelCierre referencias, UserRefV1? usuario,
        CancellationToken ct)
    {
        corrida.PartCount += 1;
        var numeroDeParte = corrida.PartCount;
        var partePublicId = Guid.NewGuid();
        var totales = PartesDelCierre.Totales(lineas);
        var control = lineas.Sum(l => l.Amounts.Sum(a => a.Amount));
        var r = new DateTimeOffset(DateTime.SpecifyKind(cierre.InformationCutoffAt, DateTimeKind.Utc));
        object contenido = tipo == CarteraReclasificadaPorCalificacionV1.Type
            ? new CarteraReclasificadaPorCalificacionV1
            {
                RunKind = nameof(LendingProcessKind.MonthlyClose), RunPublicId = corrida.PublicId, FactDate = cierre.CloseDate, BatchNumber = numeroDeTanda,
                PartNumber = numeroDeParte, CloseVersion = cierre.Version, Year = cierre.PeriodYear, Month = cierre.PeriodMonth, InformationCutoffAt = r,
                Totals = totales, Lines = lineas, LineCount = lineas.Count, ControlTotal = control,
            }
            : new DeterioroCalculadoV1
            {
                RunKind = nameof(LendingProcessKind.MonthlyClose), RunPublicId = corrida.PublicId, FactDate = cierre.CloseDate, BatchNumber = numeroDeTanda,
                PartNumber = numeroDeParte, CloseVersion = cierre.Version, Year = cierre.PeriodYear, Month = cierre.PeriodMonth, InformationCutoffAt = r,
                Totals = totales, Lines = lineas, LineCount = lineas.Count, ControlTotal = control, GeneralImpairment = general,
            };
        var rotulo = PartesDelCierre.Rotulo(false, cierre.PeriodYear, cierre.PeriodMonth, cierre.Version, numeroDeParte);
        var sucursalPublicId = referencias.Sucursales.TryGetValue(sucursal, out var s)
            ? s
            : await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == sucursal).Select(b => b.PublicId).FirstAsync(ct);
        var emitido = await emision.HechoDelCierreAsync(new EmisionDeParte(partePublicId, rotulo, cierre.CloseDate, sucursalPublicId, null, null, null, usuario),
            cierre.Version, tipo, contenido, ct);
        if (emitido.IsFailure) return Result.Failure<AccountingFactPart>(emitido.Error);

        var parte = new AccountingFactPart
        {
            PublicId = partePublicId,
            SourceKind = AccountingFactPartSource.MonthlyClose,
            ProcessRunId = corrida.Id,
            BatchNumber = numeroDeTanda,
            MonthlyCloseId = cierre.Id,
            CloseVersion = cierre.Version,
            FactType = tipo,
            PerLoanMessages = false,
            ValueDate = cierre.CloseDate,
            AccountingDate = cierre.CloseDate,
            BranchId = sucursal,
            OriginEventKey = ClavesDeEvento.Cierre(cierre.Version),
            MessagePublicId = emitido.Value.PublicId,
            MessageCount = 1,
            LoanCount = lineas.Select(l => l.Loan.LoanPublicId).Distinct().Count(),
            TotalAmount = control + (general?.Sum(g => g.Amount) ?? 0m),
            TotalsByDimensionJson = JsonSerializer.Serialize(totales, Json),
            LoanListSha256 = PartesDelCierre.Huella(lineas),
        };
        db.AccountingFactParts.Add(parte);
        return Result.Success(parte);
    }

    /// <summary>
    /// El lote del cierre (T56): las entregas por lotes sin lote con el alcance <c>LendingClose:{aaaa-mm}</c>, en un lote
    /// <c>BatchTrigger.LendingMonthlyClose</c> creado en la misma transacción.
    /// </summary>
    private async Task LoteAsync(DateOnly fin, CancellationToken ct)
    {
        var alcance = ModoDePasoDeCartera.AlcanceDeCierre(fin);
        var entregas = await db.IntegrationMessageDeliveries
            .Where(x => x.Destination == IntegrationDestinations.Accounting && x.Status == DeliveryStatus.InBatch && x.BatchId == null && x.BatchScopeKey == alcance)
            .OrderBy(x => x.MessageId).ToListAsync(ct);
        if (entregas.Count == 0) return;
        var actor = Actor.ProcesoDeIntegracion(Actor.OrigenDeTarea(TareaDelCierre));
        var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), IntegrationDestinations.Accounting,
            BatchTrigger.LendingMonthlyClose, actor, null, reloj.UtcNow);
        lote.MessageCount = entregas.Count;
        lote.CutoffMessageId = entregas[^1].MessageId;
        lote.DateFrom = fin;
        lote.DateTo = fin;
        db.IntegrationBatches.Add(lote);
        await db.SaveChangesAsync(ct);
        LotesDeIntegracion.Asignar(lote, entregas);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>El origen con que firma el lote del cierre.</summary>
    public const string TareaDelCierre = "cartera.cierre";

    // ------------------------------------------------------------------------------------------------ movimientos --

    /// <summary>El movimiento de cada concepto: del saldo vigente (la línea anterior, ya reclasificada) al calculado, con el gasto del ejercicio.</summary>
    private static Dictionary<LoanConcept, MovimientoDelDeterioro> Movimientos(IReadOnlyDictionary<LoanConcept, decimal> anterior,
        IReadOnlyDictionary<LoanConcept, decimal> nuevo, IReadOnlyList<IReadOnlyDictionary<LoanConcept, decimal>>? historia, DisminucionDeDeterioro regla)
    {
        var movimientos = new Dictionary<LoanConcept, MovimientoDelDeterioro>();
        foreach (var concepto in anterior.Keys.Union(nuevo.Keys))
        {
            var serie = (historia ?? []).Select(h => h.GetValueOrDefault(concepto)).ToList();
            serie.Add(anterior.GetValueOrDefault(concepto));
            var m = MovimientosDelDeterioro.DelCierre(serie, nuevo.GetValueOrDefault(concepto), regla);
            if (m.Neto != 0m) movimientos[concepto] = m;
        }
        return movimientos;
    }

    /// <summary>
    /// El deterioro por concepto de cada crédito en los cierres vigentes del ejercicio <b>antes</b> del anterior, empezando por el saldo con que
    /// abrió el ejercicio (el último cierre del año anterior o el base): con ellos y el anterior se reconstruye el gasto del ejercicio.
    /// </summary>
    private async Task<Dictionary<int, IReadOnlyList<IReadOnlyDictionary<LoanConcept, decimal>>>> HistoriaDelEjercicioAsync(MonthlyClose? anterior, int anio,
        IReadOnlyCollection<int> creditos, CancellationToken ct)
    {
        var cadena = await CadenaDelEjercicioAsync(anterior, anio, ct);
        var resultado = new Dictionary<int, IReadOnlyList<IReadOnlyDictionary<LoanConcept, decimal>>>();
        if (cadena.Count == 0) return resultado;
        var porCierre = new List<Dictionary<int, IReadOnlyDictionary<LoanConcept, decimal>>>();
        foreach (var c in cadena)
        {
            var lineas = await CategoriaContableDeCartera.LineasDe(db, c).Where(l => creditos.Contains(l.LoanId)).ToListAsync(ct);
            porCierre.Add(lineas.ToDictionary(l => l.LoanId, l => PartesDelCierre.Deterioro(l)));
        }
        var vacio = new Dictionary<LoanConcept, decimal>();
        foreach (var id in creditos)
            resultado[id] = porCierre.Select(p => p.GetValueOrDefault(id) ?? vacio).ToList();
        return resultado;
    }

    /// <summary>Los cierres vigentes anteriores al <paramref name="anterior"/> que cuentan para el ejercicio, del más viejo al más nuevo.</summary>
    private async Task<List<MonthlyClose>> CadenaDelEjercicioAsync(MonthlyClose? anterior, int anio, CancellationToken ct)
    {
        var cadena = new List<MonthlyClose>();
        if (anterior is null || anterior.CloseDate.Year < anio || anterior.Kind == MonthlyCloseKind.Base) return cadena;
        var actual = anterior;
        while (actual.PreviousCloseId is { } previoId)
        {
            var previo = await db.MonthlyCloses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == previoId, ct);
            if (previo is null) break;
            cadena.Insert(0, previo);
            if (previo.CloseDate.Year < anio || previo.Kind == MonthlyCloseKind.Base) break;
            actual = previo;
        }
        return cadena;
    }

    private sealed record LineaGeneral(int BranchId, GeneralImpairmentLineV1 Linea);

    /// <summary>
    /// El general y el adicional en delta por dimensión (§11.3): el mínimo como <c>General</c> y el adicional neto de su compensación como
    /// <c>Additional</c>, cada uno con su movimiento y la misma regla de la disminución.
    /// </summary>
    private async Task<List<LineaGeneral>> GeneralEnDeltaAsync(IReadOnlyList<DeterioroGeneralPorDimension> general,
        IReadOnlyList<MonthlyCloseGeneralImpairment> anteriores, MonthlyClose? anterior, int anio, DisminucionDeDeterioro regla, CancellationToken ct)
    {
        var cadena = await CadenaDelEjercicioAsync(anterior, anio, ct);
        var ids = cadena.Select(c => c.Id).ToList();
        var historia = await db.MonthlyCloseGeneralImpairments.AsNoTracking().Where(g => ids.Contains(g.MonthlyCloseId)).ToListAsync(ct);
        var deLasDimensiones = general.Select(g => g.Dimension.BranchId).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().IgnoreQueryFilters()
            .Where(b => deLasDimensiones.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);

        var lineas = new List<LineaGeneral>();
        foreach (var g in general)
        {
            var previo = anteriores.FirstOrDefault(x => DimensionDe(x) == g.Dimension);
            var serieMinimo = cadena.Select(c => historia.FirstOrDefault(h => h.MonthlyCloseId == c.Id && DimensionDe(h) == g.Dimension)?.MinimumAmount ?? 0m).ToList();
            serieMinimo.Add(previo?.MinimumAmount ?? 0m);
            var serieAdicional = cadena.Select(c => historia.FirstOrDefault(h => h.MonthlyCloseId == c.Id && DimensionDe(h) == g.Dimension) is { } h
                ? h.AdditionalAmount - h.CompensationOfIndividual : 0m).ToList();
            serieAdicional.Add(previo is null ? 0m : previo.AdditionalAmount - previo.CompensationOfIndividual);
            // El base sólo trae el total: es todo mínimo.
            if (previo is not null && previo.MinimumAmount == 0m && previo.AdditionalAmount == 0m && previo.TotalAmount != 0m) serieMinimo[^1] = previo.TotalAmount;

            Agregar("General", MovimientosDelDeterioro.DelCierre(serieMinimo, g.MontoMinimo, regla));
            Agregar("Additional", MovimientosDelDeterioro.DelCierre(serieAdicional, g.MontoAdicional - g.CompensacionDelIndividual, regla));

            void Agregar(string clase, MovimientoDelDeterioro m)
            {
                foreach (var (codigo, importe) in m.Partidas())
                    lineas.Add(new LineaGeneral(g.Dimension.BranchId, new GeneralImpairmentLineV1
                    {
                        Kind = clase, BranchPublicId = sucursales.GetValueOrDefault(g.Dimension.BranchId), ModalityCode = g.Dimension.ModalityCode,
                        SubsegmentCode = g.Dimension.SubsegmentCode, RiskSegment = g.Dimension.ModalityCode, ImpairmentModel = g.Dimension.Modelo.ToString(),
                        Amount = importe, ReasonCode = codigo,
                    }));
            }
        }
        return lineas;
    }

    private static DimensionDeGeneral Dimension(MonthlyCloseLoan l) => new(l.BranchId, l.ModalityCode, l.SubsegmentCode, l.IsEmployee, l.ImpairmentModel);

    private static DimensionDeGeneral DimensionDe(MonthlyCloseGeneralImpairment g) => new(g.BranchId, g.ModalityCode, g.SubsegmentCode, g.IsEmployee, g.ImpairmentModel);

    /// <summary>Los totales del encabezado por modalidad, sub-segmento, categoría y sucursal.</summary>
    private static string Totales(IReadOnlyList<MonthlyCloseLoan> lineas)
    {
        static object Por(IEnumerable<IGrouping<string, MonthlyCloseLoan>> grupos) => grupos.OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => new
        {
            loans = g.Count(), principal = g.Sum(l => l.PrincipalBalance), impairment = g.Sum(l => l.TotalImpairment),
        });
        return JsonSerializer.Serialize(new
        {
            byModality = Por(lineas.GroupBy(l => l.ModalityCode)),
            bySubsegment = Por(lineas.GroupBy(l => l.SubsegmentCode ?? "-")),
            byCategory = Por(lineas.GroupBy(l => l.AccountingRating.ToString())),
            byBranch = Por(lineas.GroupBy(l => l.BranchId.ToString(CultureInfo.InvariantCulture))),
        }, Json);
    }

    private static LendingProcessRunError Error(LendingProcessRun corrida, int? loanId, int tanda, byte paso, string codigo, string mensaje)
    {
        var texto = string.IsNullOrWhiteSpace(mensaje) ? codigo : mensaje;
        return new LendingProcessRunError
        {
            ProcessRunId = corrida.Id,
            LoanId = loanId,
            BatchNumber = tanda,
            AttemptNumber = corrida.RetryCount + 1,
            Step = PasosDelCierre.Nombre(paso),
            ErrorCode = codigo.Length > 80 ? codigo[..80] : codigo,
            ErrorMessage = texto.Length > LargoDelMensaje ? texto[..LargoDelMensaje] : texto,
            OccurredAt = corrida.LastHeartbeatAt ?? corrida.InformationCutoffAt ?? corrida.RequestedAt,
        };
    }
}

/// <summary>El ejecutor de las corridas <c>MonthlyClose</c> que conduce <see cref="ConductorDeCorridasDeCartera"/> (T0705, T0707, C59). (nuevo)</summary>
public sealed class EjecutorDelCierreMensual(CierreMensualDeCartera cierre) : IEjecutorDeCorridaDeCartera
{
    public LendingProcessKind Clase => LendingProcessKind.MonthlyClose;

    public Task<AvanceDeCorrida> EjecutarTandaAsync(LendingProcessRun corrida, TimeSpan presupuesto, CancellationToken ct) => cierre.TandaAsync(corrida, ct);
}

/// <summary>
/// Una tanda del cierre mensual de la corrida <see cref="ProcessRunPublicId"/> (feature 013, US9, T0705). <b>Sin ruta</b>: la corre el procesador por
/// <c>IEjecutorEnCooperativa</c> con la persona que ordenó como actor (<see cref="EjecutorDelCierreMensual"/>) y la mandan a mano las pruebas e2e.
/// Devuelve si la corrida quedó pendiente o terminó. (nuevo)
/// </summary>
public sealed record ExecuteMonthlyCloseCommand(Guid ProcessRunPublicId) : IRequest<Result<AvanceDeCorrida>>;

public sealed class ExecuteMonthlyCloseCommandValidator : AbstractValidator<ExecuteMonthlyCloseCommand>
{
    public ExecuteMonthlyCloseCommandValidator() => RuleFor(x => x.ProcessRunPublicId).NotEmpty();
}

public sealed class ExecuteMonthlyCloseCommandHandler(IApplicationDbContext db, CierreMensualDeCartera cierre, IDateTimeService reloj)
    : IRequestHandler<ExecuteMonthlyCloseCommand, Result<AvanceDeCorrida>>
{
    public async Task<Result<AvanceDeCorrida>> Handle(ExecuteMonthlyCloseCommand request, CancellationToken ct)
    {
        var corrida = db.LendingProcessRuns.Local.FirstOrDefault(r => r.PublicId == request.ProcessRunPublicId)
                      ?? await db.LendingProcessRuns.FirstOrDefaultAsync(r => r.PublicId == request.ProcessRunPublicId && !r.IsDeleted, ct);
        if (corrida is null || corrida.Kind != LendingProcessKind.MonthlyClose) return Result.Failure<AvanceDeCorrida>(ErroresDeCorridas.Inexistente());
        if (corrida.Status is LendingProcessRunStatus.Requested)
        {
            corrida.Status = LendingProcessRunStatus.Running;
            corrida.StartedAt ??= reloj.UtcNow;
        }
        AvanceDeCorrida avance;
        try
        {
            avance = await cierre.TandaAsync(corrida, ct);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<AvanceDeCorrida>(new Error(ex.Codigo, ex.Problema.Mensaje));
        }
        corrida = db.LendingProcessRuns.Local.FirstOrDefault(r => r.Id == corrida.Id) ?? corrida;
        corrida.BatchCount += 1;
        corrida.LastHeartbeatAt = reloj.UtcNow;
        if (avance == AvanceDeCorrida.Terminada)
        {
            corrida.Status = corrida.FailedLoans > 0 ? LendingProcessRunStatus.CompletedWithErrors : LendingProcessRunStatus.Completed;
            corrida.FinishedAt = reloj.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(avance);
    }
}
