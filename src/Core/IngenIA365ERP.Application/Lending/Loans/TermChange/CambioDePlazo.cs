using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Loans.TermChange;

/// <summary>
/// Los errores del cambio de plazo de un crédito sin pagos ni causación (2026-11-24, pedido del dueño). Todos en español y accionables: dicen por qué
/// no se puede y qué hacer (casi siempre, una novedad de prórroga). (nuevo)
/// </summary>
public static class ErroresDelCambioDePlazo
{
    public const string CodigoDeshabilitado = "Lending.Loan.TermChange.Disabled";
    public const string CodigoNoActivo = "Lending.Loan.TermChange.NotActive";
    public const string CodigoMigrado = "Lending.Loan.TermChange.Migrated";
    public const string CodigoConPagos = "Lending.Loan.TermChange.HasPayments";
    public const string CodigoConCausacion = "Lending.Loan.TermChange.HasAccrual";
    public const string CodigoConOperaciones = "Lending.Loan.TermChange.HasOperations";
    public const string CodigoCondicionesEspeciales = "Lending.Loan.TermChange.SpecialTerms";
    public const string CodigoMesCerrado = "Lending.Loan.TermChange.ClosedMonth";
    public const string CodigoFueraDeLaLinea = "Lending.Loan.TermChange.OutOfLineRange";
    public const string CodigoSinCambio = "Lending.Loan.TermChange.Unchanged";
    public const string CodigoPrimerVencimiento = "Lending.Loan.TermChange.FirstDueDateInvalid";
    public const string CodigoMismaPersona = "Lending.Loan.TermChange.SameUser";
    public const string CodigoMoveriaElLibro = "Lending.Loan.TermChange.WouldChangeLedger";

    private const string UseUnaNovedad = " Para cambiarlo, registre una novedad de prórroga en Cartera › Novedades.";

    public static Error Deshabilitado() => new(CodigoDeshabilitado,
        "La cooperativa no permite cambiar el plazo desde el crédito (Parámetros de Cartera › Creditos.CambioDePlazo)." + UseUnaNovedad);

    public static Error NoActivo(string estado) => new(CodigoNoActivo,
        $"El plazo sólo se cambia en un crédito activo; este está {estado}.");

    public static Error Migrado() => new(CodigoMigrado,
        "El crédito se cargó en la migración con su plan y su historia: su plazo no se cambia desde aquí." + UseUnaNovedad);

    public static Error ConPagos() => new(CodigoConPagos,
        "El crédito ya tiene pagos aplicados (o anticipos): el plazo sólo se cambia en un crédito sin pagos ni causación." + UseUnaNovedad);

    public static Error ConCausacion() => new(CodigoConCausacion,
        "El crédito ya tiene causación registrada: el plazo sólo se cambia en un crédito sin pagos ni causación." + UseUnaNovedad);

    public static Error ConOperaciones(IEnumerable<string> clases) => new ErrorConDatos(CodigoConOperaciones,
        $"El crédito ya tiene otras operaciones ({string.Join(", ", clases)}): el plazo sólo se cambia en un crédito recién nacido, sin pagos ni causación."
        + UseUnaNovedad, new { operations = clases.ToList() });

    public static Error CondicionesEspeciales(string cual) => new(CodigoCondicionesEspeciales,
        $"El crédito tiene {cual}: su plazo no se cambia desde aquí." + UseUnaNovedad);

    public static Error MesCerrado(DateOnly fecha) => new(CodigoMesCerrado,
        $"El crédito nació el {fecha:dd/MM/yyyy}, en un mes que ya se cerró: su plazo no se cambia desde aquí." + UseUnaNovedad);

    public static Error FueraDeLaLinea(int minimo, int maximo, string detalle) => new ErrorConDatos(CodigoFueraDeLaLinea,
        detalle, new { field = "installments", min = minimo, max = maximo });

    public static Error SinCambio(int cuotas) => new(CodigoSinCambio,
        $"El crédito ya tiene {cuotas.ToString(CultureInfo.InvariantCulture)} cuotas con ese primer vencimiento: no hay nada que cambiar.");

    public static Error PrimerVencimiento(string detalle) => new ErrorConDatos(CodigoPrimerVencimiento, detalle, new { field = "firstDueDate" });

    public static Error MismaPersona() => new(CodigoMismaPersona,
        "Usted registró este crédito y la cooperativa pide que el plazo lo cambie otra persona (Parámetros de Cartera › "
        + "Segregacion.CambioDePlazoPorOtraPersona).");

    public static Error MoveriaElLibro() => new(CodigoMoveriaElLibro,
        "El cambio movería saldos o causación ya registrados del crédito: el plazo sólo se cambia en un crédito sin pagos ni causación." + UseUnaNovedad);
}

/// <summary>
/// Las reglas puras del cambio de plazo (2026-11-24): qué crédito lo admite. Sin base de datos, para probarlas solas. (nuevo)
/// <list type="bullet">
/// <item>activo, no migrado, en pesos (no en UVR), sin gracia ni cuotas extraordinarias pactadas (la regeneración no las conserva);</item>
/// <item>sus operaciones son sólo su desembolso (uno), los cambios de plazo anteriores y los cambios de preferencia de abono (no mueven saldos): un
/// pago, un cobro provisional, un reverso o una resolución de saldo es «tiene pagos»; cualquier otra (cargo, condonación, novedad, ajuste de
/// Comercial, reajuste de tasa…), «tiene otras operaciones»;</item>
/// <item>sin causación registrada (filas de causación con valor).</item>
/// </list>
/// </summary>
public static class ReglasDelCambioDePlazo
{
    /// <summary>Las clases de fila que son causación (las de <see cref="LibroTeorico.EsDeCausacion"/> y el reconocimiento al inicio del ciclo).</summary>
    public static IReadOnlyList<LoanTransactionKind> ClasesDeCausacion { get; } =
    [
        .. Enum.GetValues<LoanTransactionKind>().Where(LibroTeorico.EsDeCausacion),
        LoanTransactionKind.PeriodRecognition,
    ];

    private static readonly HashSet<LoanOperationKind> DePago =
    [
        LoanOperationKind.Payment, LoanOperationKind.ProvisionalCollection, LoanOperationKind.PaymentReversal, LoanOperationKind.CreditBalanceResolution,
    ];

    /// <summary>¿Es la operación un cambio de plazo anterior (no una novedad)?</summary>
    public static bool EsCambioDePlazo(LoanOperation o) =>
        o.Kind == LoanOperationKind.TermsChange && o.NoveltyId is null
        && string.Equals(DecisionDeNovedad.Leer(o.Payload)?.Mechanism, nameof(NoveltyMechanism.TermCorrection), StringComparison.Ordinal);

    /// <summary>El primer error que impide cambiar el plazo, o nulo si lo admite.</summary>
    public static Error? Evaluar(Loan credito, LoanTermsVersion pactadas, LoanTermsVersion vigentes, IReadOnlyList<LoanOperation> operaciones, bool tieneCausacion)
    {
        ArgumentNullException.ThrowIfNull(credito);
        ArgumentNullException.ThrowIfNull(pactadas);
        ArgumentNullException.ThrowIfNull(vigentes);
        ArgumentNullException.ThrowIfNull(operaciones);
        if (credito.Status != LoanStatus.Active) return ErroresDelCambioDePlazo.NoActivo(EstadoEnTexto(credito.Status));
        if (credito.Origin == LoanOrigin.Migration) return ErroresDelCambioDePlazo.Migrado();
        if (operaciones.Any(o => DePago.Contains(o.Kind))) return ErroresDelCambioDePlazo.ConPagos();
        var otras = operaciones
            .Where(o => o.Kind is not (LoanOperationKind.Disbursement or LoanOperationKind.PreferenceChange) && !EsCambioDePlazo(o))
            .Select(o => ClaseEnTexto(o.Kind)).Distinct().ToList();
        if (operaciones.Count(o => o.Kind == LoanOperationKind.Disbursement) > 1) otras.Insert(0, "más de un desembolso");
        if (otras.Count > 0) return ErroresDelCambioDePlazo.ConOperaciones(otras);
        if (!operaciones.Any(o => o.Kind == LoanOperationKind.Disbursement))
            return ErroresDelCambioDePlazo.CondicionesEspeciales("un origen distinto de un desembolso o una venta a crédito");
        if (tieneCausacion) return ErroresDelCambioDePlazo.ConCausacion();
        if (pactadas.DenominationUnit == DenominationUnit.Uvr) return ErroresDelCambioDePlazo.CondicionesEspeciales("su capital en UVR");
        if (vigentes.GraceKind is { } gracia && gracia != GraceKind.None) return ErroresDelCambioDePlazo.CondicionesEspeciales("período de gracia pactado");
        if (!string.IsNullOrWhiteSpace(vigentes.ExtraordinaryInstallments) && vigentes.ExtraordinaryInstallments.Trim() is not ("[]" or "null"))
            return ErroresDelCambioDePlazo.CondicionesEspeciales("cuotas extraordinarias pactadas");
        if (vigentes.Periodicity == RepaymentPeriodicity.SinglePayment) return ErroresDelCambioDePlazo.CondicionesEspeciales("pago único al vencimiento");
        return null;
    }

    private static string EstadoEnTexto(LoanStatus s) => s switch
    {
        LoanStatus.PendingDisbursement => "pendiente de giro",
        LoanStatus.PaidOff => "cancelado",
        LoanStatus.WrittenOff => "castigado",
        LoanStatus.Voided => "anulado",
        LoanStatus.Novated => "novado",
        _ => s.ToString(),
    };

    private static string ClaseEnTexto(LoanOperationKind k) => k switch
    {
        LoanOperationKind.OpeningBalance => "saldo inicial",
        LoanOperationKind.DisbursementReversal => "reverso de desembolso",
        LoanOperationKind.RateReset => "reajuste de tasa",
        LoanOperationKind.TermsChange => "novedad",
        LoanOperationKind.Charge => "cargo",
        LoanOperationKind.Waiver => "condonación",
        LoanOperationKind.CommercialAdjustment => "ajuste de Comercial",
        LoanOperationKind.WriteOff => "castigo",
        _ => k.ToString(),
    };
}

/// <summary>Un cambio de plazo calculado, listo para mostrarse o registrarse. (nuevo)</summary>
public sealed record CambioDePlazoCalculado(
    Loan Credito,
    CreditoCargado Cargado,
    DateOnly FechaValor,
    DateOnly FechaContable,
    PlanDePagos PlanAntes,
    PlanDePagos PlanNuevo,
    ContenidoDeNovedad Contenido,
    DecisionDeNovedad Decision,
    ResultadoDeUsura Usura,
    IReadOnlyList<string> Avisos)
{
    /// <summary>La cuota regular de un plan: la primera que no es de gracia ni extraordinaria (la que el estado de cuenta llama «cuota»).</summary>
    public static decimal CuotaRegular(PlanDePagos plan) =>
        plan.Cuotas.Where(q => !q.SoloInteres && q.Extraordinaria == 0m).Select(q => q.Total).FirstOrDefault();
}

/// <summary>
/// El cálculo del <b>cambio de plazo</b> de un crédito sin pagos ni causación (2026-11-24, pedido del dueño: «cambiar el plazo (las cuotas) y que esto
/// recalcule el valor de la cuota y las cuotas en los parámetros, siempre y cuando a este crédito no se le hayan realizado pagos y no tenga
/// causación»). No es una novedad: el crédito queda como si hubiera nacido con las cuotas nuevas. Lo usan la simulación y el registro, así lo que se
/// muestra es lo que se escribe. No escribe.
/// <list type="bullet">
/// <item>Fecha valor: la del primer desembolso (el plan se rehace desde el nacimiento). Un mes ya cerrado no se toca
/// (<c>Lending.Loan.TermChange.ClosedMonth</c>).</item>
/// <item>El plan nuevo lo calcula la reconstrucción misma con la operación <c>TermsChange</c> de mecanismo <see cref="NoveltyMechanism.TermCorrection"/>
/// (<see cref="InterpreteDeNovedad"/>): sobre todo el capital, con las cuotas nuevas y el primer vencimiento pedido o el del plan de hoy, así el
/// día de pago no se corre.</item>
/// <item>Los rangos de la versión de la línea con que nació el crédito (cuotas y meses) y la usura vigente a la fecha valor.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class CambioDePlazo(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    FechaContable fechaContable,
    ILectorDeParametros parametros,
    IDateTimeService reloj)
{
    /// <summary>El prefijo del número de su operación.</summary>
    public const string PrefijoDeOperacion = "CPZ-";

    /// <summary>¿Permite la cooperativa (o la línea) el cambio de plazo? Si el parámetro no se puede leer, lo de fábrica: sí.</summary>
    public async Task<bool> PermitidoAsync(Loan credito, CancellationToken ct)
    {
        var leido = await parametros.LeerComoAsync<bool>(ParametrosDeCartera.Modulo, ParametrosDeCartera.CreditosCambioDePlazo, reloj.HoyLocal,
            ParameterScopeKind.CreditLine, credito.CreditLineId, ct);
        return leido.IsFailure || leido.Value;
    }

    /// <summary>¿Pide la cooperativa que el plazo lo cambie otra persona? Si no se puede leer, no (lo de fábrica).</summary>
    public async Task<bool> ExigeOtraPersonaAsync(CancellationToken ct)
    {
        var leido = await parametros.LeerComoAsync<bool>(ParametrosDeCartera.Modulo, ParametrosDeCartera.SegregacionCambioDePlazoPorOtraPersona,
            reloj.HoyLocal, ct: ct);
        return leido.IsSuccess && leido.Value;
    }

    /// <summary>Calcula el cambio a <paramref name="cuotas"/> cuotas y, si viene, con <paramref name="primerVencimiento"/>.</summary>
    public async Task<Result<CambioDePlazoCalculado>> CalcularAsync(Loan credito, int cuotas, DateOnly? primerVencimiento, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credito);
        var hoy = reloj.HoyLocal;
        var cargados = await cargador.CargarAsync([credito.Id], null, hoy, ct);
        if (!cargados.TryGetValue(credito.Id, out var cargado)) return Falla(Settlement.LecturaDelCredito.Inexistente());

        // Las reglas primero: un crédito con pagos o causación responde eso, aunque su reconstrucción también tuviera algo que decir.
        var clases = ReglasDelCambioDePlazo.ClasesDeCausacion.ToArray();
        var causado = await db.LoanTransactions.AsNoTracking()
            .AnyAsync(t => t.LoanId == credito.Id && clases.Contains(t.Kind) && t.Amount != 0m, ct);
        if (ReglasDelCambioDePlazo.Evaluar(credito, cargado.Pactadas, cargado.Vigentes, cargado.Operaciones, causado) is { } impedimento)
            return Falla(impedimento);
        if (!cargado.Listo) return Falla(cargado.Error!);

        var desembolso = cargado.Operaciones.First(o => o.Kind == LoanOperationKind.Disbursement);
        var v = desembolso.ValueDate;
        var contable = await fechaContable.CalcularAsync(v, ct);
        if (contable.IsFailure) return Falla(contable.Error);
        if (contable.Value.Year != v.Year || contable.Value.Month != v.Month) return Falla(ErroresDelCambioDePlazo.MesCerrado(v));

        PlanDePagos antes;
        try
        {
            antes = MotorDeCartera.Reconstruir(cargado.Entrada!, v).Plan ?? throw new InvalidOperationException("El crédito activo no tiene plan.");
        }
        catch (ReglaDeCarteraException ex)
        {
            return Falla(ErroresDeGobierno.De(ex.Problema));
        }

        var vence = primerVencimiento ?? antes.Cuotas[0].Vencimiento;
        if (vence <= v)
            return Falla(ErroresDelCambioDePlazo.PrimerVencimiento($"El primer vencimiento va después del desembolso ({v:dd/MM/yyyy})."));
        if (vence <= hoy && vence != antes.Cuotas[0].Vencimiento)
            return Falla(ErroresDelCambioDePlazo.PrimerVencimiento("El primer vencimiento nuevo va después de hoy."));
        if (cuotas == antes.Cuotas.Count && vence == antes.Cuotas[0].Vencimiento) return Falla(ErroresDelCambioDePlazo.SinCambio(cuotas));

        var rango = await RangoDeLaLineaAsync(cargado.Vigentes, cuotas, v, vence, ct);
        if (rango is not null) return Falla(rango);

        // Con otro primer vencimiento, las demás cuotas vencen ese mismo día de cada período (si no, el generador seguiría con el día de pago viejo).
        var decision = new DecisionDeNovedad
        {
            Mechanism = nameof(NoveltyMechanism.TermCorrection),
            Side = nameof(LadoDeLaNovedad.Credito),
            Installments = cuotas,
            FirstDueDate = vence,
            PaymentDay = vence != antes.Cuotas[0].Vencimiento ? vence.Day : null,
        };
        var contenido = decision.Contenido();
        PlanDePagos nuevo;
        try
        {
            var simulada = cargado.Entrada! with
            {
                Operaciones = [.. cargado.Entrada!.Operaciones, new EntradaDeOperacion("cambio-de-plazo", LoanOperationKind.TermsChange, v, reloj.UtcNow,
                    cargado.Operaciones.Count == 0 ? 1 : cargado.Operaciones.Max(o => o.Sequence) + 1) { Contenido = contenido }],
                InformacionHasta = DateTime.MaxValue,
            };
            nuevo = MotorDeCartera.Reconstruir(simulada, v).Plan ?? throw new InvalidOperationException("El cambio de plazo no dejó plan.");
        }
        catch (ReglaDeCarteraException ex)
        {
            return Falla(ErroresDeGobierno.De(ex.Problema));
        }

        var usura = ControlDeUsura.Evaluar(nuevo, Usura(cargado, v));
        if (usura.Veredicto == VeredictoDeUsura.Excede)
            return Falla(ErroresDeLineasDeCredito.UsuraExcedida("installments", v, usura, cargado.Vigentes.ModalityCode));

        var avisos = new List<string>();
        if (usura.Veredicto == VeredictoDeUsura.SinUsura && cargado.Series?.ExigeUsura == true)
            avisos.Add($"No hay usura certificada de la modalidad el {v:dd/MM/yyyy}: el plan nuevo no se pudo controlar contra la usura.");
        if (credito.ApplicationId is { } solicitud
            && await db.SignatureDocumentSets.AsNoTracking().AnyAsync(s => s.ApplicationId == solicitud && !s.IsDeleted, ct))
            avisos.Add("La solicitud ya tiene documentos para firma (pagaré, carta de instrucciones o libranza) con el plazo anterior: genérelos otra vez.");
        if (credito.Origin == LoanOrigin.Commercial)
            avisos.Add("La factura de la venta a crédito conserva las cuotas con que se vendió; el plan del crédito es el que manda en el cobro.");
        return Result.Success(new CambioDePlazoCalculado(credito, cargado, v, contable.Value, antes, nuevo, contenido, decision, usura, avisos));
    }

    /// <summary>Los rangos de cuotas y meses de la versión de la línea con que nació el crédito (la de sus condiciones).</summary>
    private async Task<Error?> RangoDeLaLineaAsync(LoanTermsVersion vigentes, int cuotas, DateOnly desembolso, DateOnly primerVencimiento, CancellationToken ct)
    {
        var linea = await db.CreditLineVersions.AsNoTracking().Where(l => l.Id == vigentes.CreditLineVersionId)
            .Select(l => new { l.MinTermMonths, l.MaxTermMonths, l.MaxInstallments }).FirstOrDefaultAsync(ct);
        if (linea is null) return null;
        var periodicidad = vigentes.Periodicity;
        var minimo = PlanTipoDeLinea.Periodos(linea.MinTermMonths, periodicidad);
        var maximo = Math.Min(PlanTipoDeLinea.Periodos(linea.MaxTermMonths, periodicidad), linea.MaxInstallments ?? int.MaxValue);
        if (cuotas < minimo || cuotas > maximo)
            return ErroresDelCambioDePlazo.FueraDeLaLinea(minimo, maximo,
                $"La línea del crédito admite de {minimo.ToString(CultureInfo.InvariantCulture)} a {maximo.ToString(CultureInfo.InvariantCulture)} cuotas "
                + $"con su periodicidad.");
        var meses = SimuladorDeCredito.PlazoEnMeses(cuotas, periodicidad, desembolso, primerVencimiento);
        if (meses < linea.MinTermMonths || meses > linea.MaxTermMonths)
            return ErroresDelCambioDePlazo.FueraDeLaLinea(linea.MinTermMonths, linea.MaxTermMonths,
                $"El plazo ({meses.ToString(CultureInfo.InvariantCulture)} meses) tiene que estar entre "
                + $"{linea.MinTermMonths.ToString(CultureInfo.InvariantCulture)} y {linea.MaxTermMonths.ToString(CultureInfo.InvariantCulture)} meses en la línea del crédito.");
        return null;
    }

    /// <summary>La usura certificada de la modalidad del crédito a la fecha (de sus series); nula si no hay o no la exige.</summary>
    private static UsuraCertificada? Usura(CreditoCargado cargado, DateOnly fecha)
    {
        if (cargado.Series is not { ExigeUsura: true } series) return null;
        var vigente = Domain.Common.VigenciaBitemporal.Vigente(series.Usura, fecha, DateTime.MaxValue);
        return vigente is null ? null : new UsuraCertificada(vigente.Ea, vigente.PublicId, series.ClaveDeUsura!, vigente.ValidFrom);
    }

    /// <summary>
    /// La versión de condiciones del cambio: la vigente con las cuotas, el plazo en meses, el primer vencimiento y, si cambió, el día de pago nuevos
    /// (causa <c>TermCorrection</c>).
    /// </summary>
    public static LoanTermsVersion Version(LoanTermsVersion v, LoanOperation operacion, PlanDePagos plan, DateOnly fecha, int? diaDePago = null) => new()
    {
        LoanId = v.LoanId, VersionNumber = v.VersionNumber + 1, Operation = operacion, EffectiveValueDate = fecha, RegisteredAt = operacion.RegisteredAt,
        Cause = TermsVersionCause.TermCorrection, NoveltyId = null, RulesVersion = v.RulesVersion, CreditLineVersionId = v.CreditLineVersionId,
        ModalityCode = v.ModalityCode, SubsegmentCode = v.SubsegmentCode, ForEmployees = v.ForEmployees, InterestTaxTreatment = v.InterestTaxTreatment,
        RateKind = v.RateKind, AnnualRate = v.AnnualRate, IndexCode = v.IndexCode, IndexKey = v.IndexKey, Margin = v.Margin, RateFloor = v.RateFloor,
        RateCeiling = v.RateCeiling, ResetPeriodicity = v.ResetPeriodicity, NextResetDate = v.NextResetDate, DefaultRateKind = v.DefaultRateKind,
        DefaultRate = v.DefaultRate, DefaultRateFactor = v.DefaultRateFactor, DefaultOnInterestAllowed = v.DefaultOnInterestAllowed, DayCount = v.DayCount,
        RateEquivalence = v.RateEquivalence, AmortizationSystem = v.AmortizationSystem, Periodicity = v.Periodicity,
        Installments = plan.Cuotas.Count,
        TermMonths = CalculoDeNovedad.Meses(plan.Cuotas.Count, v.Periodicity),
        FirstDueDate = plan.Cuotas[0].Vencimiento,
        PaymentDay = diaDePago is { } dia ? (byte)dia : v.PaymentDay, GraceKind = v.GraceKind, GraceInstallments = v.GraceInstallments, GraceInterestTreatment = v.GraceInterestTreatment,
        GraceDistributionInstallments = v.GraceDistributionInstallments, ExtraordinaryInstallments = v.ExtraordinaryInstallments,
        RoundingUnit = v.RoundingUnit, RoundingMode = v.RoundingMode, RoundingMoment = v.RoundingMoment, DenominationUnit = v.DenominationUnit,
        AllocationOrder = v.AllocationOrder, ConceptPriority = v.ConceptPriority, SuspenseWithItsConcept = v.SuspenseWithItsConcept,
        AllowsSpecificInstallmentOutOfOrder = v.AllowsSpecificInstallmentOutOfOrder, NoteRecalculation = v.NoteRecalculation, Insurances = v.Insurances,
        SubsidizedPurchase = v.SubsidizedPurchase, ResultingEffectiveRate = v.ResultingEffectiveRate, ChargesSpread = v.ChargesSpread,
        UsuryValuePublicId = v.UsuryValuePublicId, RestructuringCount = v.RestructuringCount, RestructuredOn = v.RestructuredOn,
        InputsSha256 = operacion.InputsSha256,
    };

    private static Result<CambioDePlazoCalculado> Falla(Error e) => Result.Failure<CambioDePlazoCalculado>(e);
}
