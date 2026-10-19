using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La otra cuenta del asociado de un hecho de CDAT (feature 014, US6, T174; research E107): la de origen de un fondeo o de un aumento, o la de destino de
/// un pago, una redención o una cancelación, con sus dimensiones vigentes y su cambio de saldo por concepto. Con ella el constructor cuadra el hecho sin
/// puente (las dos puntas son cuentas del asociado). (nuevo)
/// </summary>
public sealed record OtraCuentaDeCdat(HechoDeCuenta Hecho, IReadOnlyList<DepositConceptAmountV1> Valores);

/// <summary>La retención de un pago de intereses de CDAT, tal como viaja en su acompañante <c>RetencionPracticada</c>. (nuevo)</summary>
public sealed record RetencionDeCdat(RetencionCalculada Retencion, PoliticaDeRetencion Politica, PerfilDeRetencion Perfil, int Anio);

/// <summary>
/// Un hecho de CDAT tal como se emite (feature 014, US6, T174; mensajes.md §1.1 #10 a #16, §2.1, §4.1): el CDAT con su operación y su cambio de saldo, la
/// versión de condiciones (y la anterior en la prórroga y la renovación), la otra cuenta o la orden de giro, el recaudador del fondeo, el GMF de la cuenta
/// que se debita o se abona, la retención y la penalidad, la regla de tasa negociada, y la reclasificación por plazo de emisión si la versión lo cambia. (nuevo)
/// </summary>
public sealed record HechoDeCdatAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Operacion,
    IReadOnlyList<DepositConceptAmountV1> Valores,
    TermDepositTermsV1 Terminos,
    PrevalidationOutcome ValidacionPrevia)
{
    /// <summary>Lo emitió el ciclo diario (pago de intereses, prórroga, cancelación por instrucción).</summary>
    public bool PorElProceso { get; init; }

    public TermDepositTermsV1? TerminosAnteriores { get; init; }

    /// <summary>La otra cuenta del asociado (origen o destino).</summary>
    public OtraCuentaDeCdat? OtraCuenta { get; init; }

    /// <summary>La orden de giro, si el dinero sale por Tesorería; con su beneficiario sale también <c>OrdenDeGiroEmitida</c> v2.</summary>
    public AccountPayoutOrder? Orden { get; init; }

    public PayeeV1? Beneficiario { get; init; }

    /// <summary>El tipo de orden del giro (<c>TermDepositInterest</c> o <c>TermDepositRedemption</c>).</summary>
    public string? TipoDeOrden { get; init; }

    /// <summary>El recaudador que fondeó (constitución o aumento por el contrato de cuentas).</summary>
    public CollectorRefV1? Recaudador { get; init; }

    /// <summary>El GMF y la cuenta sobre la que se cobra (la origen del traslado o la destino del abono).</summary>
    public (GmfAEmitir Gmf, HechoDeCuenta Cuenta)? Gmf { get; init; }

    public RetencionDeCdat? Retencion { get; init; }

    /// <summary>La penalidad de una redención anticipada por descuento (acompañante <c>PenalidadAplicada</c>).</summary>
    public (decimal Valor, decimal Base, IReadOnlyList<DepositConceptAmountV1> Valores)? Penalidad { get; init; }

    public TermDepositPenaltyV1? DetalleDePenalidad { get; init; }

    public NegotiatedRateRefV1? Negociada { get; init; }

    /// <summary>Las dimensiones de las que sale el saldo si la versión nueva cambió el plazo de emisión (acompañante <c>CaptacionReclasificada</c>).</summary>
    public (DepositFactDimensionsV1 Anteriores, IReadOnlyList<DepositConceptAmountV1> Saldos)? Reclasificacion { get; init; }

    // Datos propios de cada hecho.
    public int NumeroDePeriodo { get; init; }

    public DateOnly? PeriodoDesde { get; init; }

    public DateOnly? PeriodoHasta { get; init; }

    public decimal InteresCapitalizado { get; init; }

    public string? CausaDeCancelacion { get; init; }

    public TermDepositAmountChangeV1? CambioDeMonto { get; init; }

    public Guid? Tercero { get; init; }
}

public sealed partial class EmisionDeCartera
{
    public const string ClaseConstitucionDeCdat = "TermDepositConstitution";
    public const string ClasePagoDeInteresesDeCdat = "TermDepositInterestPayment";
    public const string ClaseProrrogaDeCdat = "TermDepositRollover";
    public const string ClaseRenovacionDeCdat = "TermDepositRenewal";
    public const string ClaseRedencionDeCdat = "TermDepositRedemption";

    /// <summary>Las causas de <c>CdatCancelado</c> (mensajes.md §4.1).</summary>
    public const string CancelacionPorInstruccion = "Instruction";
    public const string CancelacionEnLaGracia = "GraceRedemption";
    public const string CancelacionALaVista = "MovedToDemand";
    public const string CancelacionSinTasa = "NoRolloverRate";
    public const string CancelacionPorMaximoDeProrrogas = "MaxRolloversReached";

    /// <summary>US6 (T170): <c>CdatConstituido</c> (+ <c>GmfRetenido</c> sobre la cuenta origen); clase <c>TermDepositConstitution</c>. Sin guardar.</summary>
    public Task<Result<UnidadEmitida>> CdatConstituidoAsync(HechoDeCdatAEmitir e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var h = e.Hecho;
        var funding = new TermDepositFundingV1
        {
            Kind = e.OtraCuenta is null ? "Collector" : "TransferFromAccount",
            Collector = e.Recaudador,
            SourceAccount = e.OtraCuenta?.Hecho.Referencia,
            SourceHolderPersonPublicId = e.OtraCuenta?.Hecho.TitularPublicId,
            SourceDimensions = e.OtraCuenta?.Hecho.Dimensiones(),
            SourceAmounts = e.OtraCuenta?.Valores ?? [],
        };
        var contenido = new CdatConstituidoV1
        {
            Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = e.Operacion.ValueDate, RegisteredAt = Instante(e.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(), Amounts = e.Valores, OperationNumber = e.Operacion.Number, RulesVersion = e.Operacion.RulesVersion,
            Terms = e.Terminos, Funding = funding, Negotiated = e.Negociada,
        };
        return EmitirCdatAsync(e, [contenido], CdatConstituidoV1.Type, ClaseConstitucionDeCdat, ct);
    }

    /// <summary>US6 (T172): <c>InteresesDeCdatPagados</c> (+ <c>RetencionPracticada</c>, + <c>GmfRetenido</c>, + la orden de giro). Admite proceso. Sin guardar.</summary>
    public Task<Result<UnidadEmitida>> InteresesDeCdatPagadosAsync(HechoDeCdatAEmitir e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var h = e.Hecho;
        var contenido = new InteresesDeCdatPagadosV1
        {
            Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = e.Operacion.ValueDate, RegisteredAt = Instante(e.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(), Amounts = e.Valores, OperationNumber = e.Operacion.Number, RulesVersion = e.Operacion.RulesVersion,
            ScheduleNumber = e.NumeroDePeriodo, PeriodFrom = e.PeriodoDesde ?? e.Operacion.ValueDate, PeriodTo = e.PeriodoHasta ?? e.Operacion.ValueDate,
            Destination = Destino(e), Timing = e.Terminos.Timing,
        };
        return EmitirCdatAsync(e, [contenido], InteresesDeCdatPagadosV1.Type, ClasePagoDeInteresesDeCdat, ct);
    }

    /// <summary>US6 (T172): <c>CdatProrrogado</c> (+ <c>RetencionPracticada</c> si capitaliza, + <c>CaptacionReclasificada</c>). Admite proceso. Sin guardar.</summary>
    public Task<Result<UnidadEmitida>> CdatProrrogadoAsync(HechoDeCdatAEmitir e, string reglaDeTasa, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var h = e.Hecho;
        var contenido = new CdatProrrogadoV1
        {
            Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = e.Operacion.ValueDate, RegisteredAt = Instante(e.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(), Amounts = e.Valores, OperationNumber = e.Operacion.Number, RulesVersion = e.Operacion.RulesVersion,
            PreviousTerms = e.TerminosAnteriores ?? e.Terminos, NewTerms = e.Terminos, CapitalizedInterest = e.InteresCapitalizado, RolloverRateRule = reglaDeTasa,
        };
        return EmitirCdatAsync(e, [contenido], CdatProrrogadoV1.Type, ClaseProrrogaDeCdat, ct);
    }

    /// <summary>US6 (T173): <c>CdatRenovado</c> (+ GMF, + retención, + reclasificación). Lo origina una persona. Sin guardar.</summary>
    public Task<Result<UnidadEmitida>> CdatRenovadoAsync(HechoDeCdatAEmitir e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var h = e.Hecho;
        var cambio = e.CambioDeMonto;
        if (cambio is not null && e.OtraCuenta is { } otra)
            cambio = cambio.Kind == "Increase"
                ? cambio with
                {
                    Funding = new TermDepositFundingV1
                    {
                        Kind = "TransferFromAccount", SourceAccount = otra.Hecho.Referencia, SourceHolderPersonPublicId = otra.Hecho.TitularPublicId,
                        SourceDimensions = otra.Hecho.Dimensiones(), SourceAmounts = otra.Valores,
                    },
                }
                : cambio with { Destination = Destino(e) };
        else if (cambio is not null && e.Orden is not null)
            cambio = cambio with { Destination = Destino(e) };
        var contenido = new CdatRenovadoV1
        {
            Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = e.Operacion.ValueDate, RegisteredAt = Instante(e.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(), Amounts = e.Valores, OperationNumber = e.Operacion.Number, RulesVersion = e.Operacion.RulesVersion,
            PreviousTerms = e.TerminosAnteriores ?? e.Terminos, NewTerms = e.Terminos, AmountChange = cambio,
        };
        return EmitirCdatAsync(e, [contenido], CdatRenovadoV1.Type, ClaseRenovacionDeCdat, ct);
    }

    /// <summary>US6 (T173): <c>CdatRedimido</c> (+ <c>PenalidadAplicada</c>, + retención, + GMF, + la orden). Lo origina una persona. Sin guardar.</summary>
    public Task<Result<UnidadEmitida>> CdatRedimidoAsync(HechoDeCdatAEmitir e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var h = e.Hecho;
        var contenido = new CdatRedimidoV1
        {
            Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = e.Operacion.ValueDate, RegisteredAt = Instante(e.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(), Amounts = e.Valores, OperationNumber = e.Operacion.Number, RulesVersion = e.Operacion.RulesVersion,
            Terms = e.Terminos, Penalty = e.DetalleDePenalidad ?? new TermDepositPenaltyV1(), Destination = Destino(e),
        };
        return EmitirCdatAsync(e, [contenido], CdatRedimidoV1.Type, ClaseRedencionDeCdat, ct);
    }

    /// <summary>US6 (T172, T173): <c>CdatCancelado</c> (+ retención, + GMF, + la orden). Admite proceso. Sin guardar.</summary>
    public Task<Result<UnidadEmitida>> CdatCanceladoAsync(HechoDeCdatAEmitir e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var h = e.Hecho;
        var contenido = new CdatCanceladoV1
        {
            Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = e.Operacion.ValueDate, RegisteredAt = Instante(e.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(), Amounts = e.Valores, OperationNumber = e.Operacion.Number, RulesVersion = e.Operacion.RulesVersion,
            Terms = e.Terminos, Cause = e.CausaDeCancelacion ?? CancelacionPorInstruccion, Destination = Destino(e),
        };
        return EmitirCdatAsync(e, [contenido], CdatCanceladoV1.Type, ClaseRedencionDeCdat, ct);
    }

    /// <summary>El destino de un pago, una redención o una cancelación: la otra cuenta con su contrapartida, o la orden de giro.</summary>
    private static TermDepositDestinationV1 Destino(HechoDeCdatAEmitir e) => e.Orden is { } orden
        ? new TermDepositDestinationV1 { Kind = "DisbursementOrder", OrderPublicId = orden.PublicId }
        : e.OtraCuenta is { } otra
            ? new TermDepositDestinationV1
            {
                Kind = "Account", TargetAccount = otra.Hecho.Referencia, TargetHolderPersonPublicId = otra.Hecho.TitularPublicId,
                TargetDimensions = otra.Hecho.Dimensiones(), TargetAmounts = otra.Valores,
            }
            : new TermDepositDestinationV1 { Kind = "None" };

    /// <summary>La unidad del hecho principal con sus acompañantes (mensajes.md §2.1) y, si hay giro, la orden v2 con origen la orden.</summary>
    private async Task<Result<UnidadEmitida>> EmitirCdatAsync(HechoDeCdatAEmitir e, List<object> contenidos, string tipo, string clase, CancellationToken ct)
    {
        var h = e.Hecho;
        var op = e.Operacion;
        if (e.Retencion is { } r && r.Retencion.SeRetiene)
        {
            // La retención de un CDAT sale del interés por pagar (data-model §0.2): [InterestPayable −r, Withholding +r].
            var linea = new DepositFactLineV1
            {
                Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, Dimensions = h.Dimensiones(), ValueDate = op.ValueDate,
                Amounts =
                [
                    new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.InterestPayable), Amount = -r.Retencion.Valor },
                    new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Withholding), Amount = r.Retencion.Valor },
                ],
            };
            contenidos.Add(new RetencionPracticadaV1
            {
                RunKind = e.PorElProceso ? nameof(LendingProcessKind.DailyCycle) : null, FactDate = op.ValueDate, BatchNumber = 1, PartNumber = 1,
                Totals = [new DepositFactTotalV1 { Dimensions = h.Dimensiones(), Amounts = linea.Amounts, AccountCount = 1 }], Lines = [linea], LineCount = 1,
                ControlTotal = 0m, Withholdings = [DetalleDeRetencion(r.Retencion, r.Politica, r.Perfil, r.Anio, DepositClass.TermDeposit)],
            });
        }
        if (e.Penalidad is { } p && p.Valor > 0m)
            contenidos.Add(new PenalidadAplicadaV1
            {
                Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = op.ValueDate, RegisteredAt = Instante(op.RegisteredAt),
                Dimensions = h.Dimensiones(), Amounts = p.Valores, OperationNumber = op.Number, RulesVersion = op.RulesVersion,
                Kind = "TermDepositEarlyRedemption", Basis = p.Base,
            });
        if (e.Gmf is { } g && g.Gmf.Calculo.Valor > 0m) contenidos.Add(GmfRetenido(g.Cuenta, op, g.Gmf));
        if (e.Reclasificacion is { } rc && rc.Saldos.Count > 0)
            contenidos.Add(new CaptacionReclasificadaV1
            {
                Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = op.ValueDate, RegisteredAt = Instante(op.RegisteredAt),
                Dimensions = h.Dimensiones(), Amounts = rc.Saldos, OperationNumber = op.Number, RulesVersion = op.RulesVersion,
                Cause = "IssueTermBand", PreviousDimensions = rc.Anteriores,
            });

        var modo = await modoDePaso.ResolverAsync(tipo, h.FechaContable, e.PorElProceso, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, clase, Prefijo(op.Number), op.Number, h.FechaContable, h.SucursalPublicId,
                PersonPublicId: e.Tercero ?? h.TitularPublicId),
            ClavesDeEvento.Confirmacion, contenidos, modo.Value, ValidacionPrevia: e.ValidacionPrevia), ct);
        if (e.Orden is null || e.Beneficiario is null) return Result.Success(new UnidadEmitida(mensajes));

        var orden = new OrdenDeGiroEmitidaV2
        {
            OrderPublicId = e.Orden.PublicId, OrderNumber = e.Orden.Number, OrderKind = e.TipoDeOrden ?? "TermDepositRedemption", Account = h.Referencia,
            SourceDocumentPublicId = op.PublicId, Form = "Transfer", Payee = e.Beneficiario, GrossAmount = e.Orden.Amount, NetAmount = e.Orden.Amount,
            PayDate = h.FechaContable, Concept = $"CDAT {h.Cuenta.Number}",
        };
        var deLaOrden = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, e.Orden.PublicId, ClaseOrdenDeGiro, Prefijo(e.Orden.Number), e.Orden.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: e.Beneficiario.PersonPublicId),
            ClavesDeEvento.Confirmacion, [orden], new ModoDeEntrega.Sellado(DeliveryMode.Always)), ct);
        return Result.Success(new UnidadEmitida([.. mensajes, .. deLaOrden]));
    }

    /// <summary>La versión sellada como viaja en los hechos (mensajes.md §3, <c>TermDepositTermsV1</c>).</summary>
    public static TermDepositTermsV1 Terminos(MemberAccountTermsVersion v, string numeroDeCuenta) => new()
    {
        TermsVersion = v.VersionNumber,
        TitleNumber = MemberAccountTermsVersion.NumeroDeTitulo(numeroDeCuenta, v.VersionNumber),
        Amount = v.PrincipalAmount,
        TermDays = v.TermDays ?? 0,
        StartDate = v.StartDate ?? v.EffectiveValueDate,
        MaturityDate = v.MaturityDate ?? v.EffectiveValueDate,
        EffectiveAnnual = v.AppliedAnnualRate,
        TableRate = v.TableAnnualRate,
        NegotiatedMarginEa = v.NegotiatedSpread,
        InterestPeriodicity = v.InterestPeriodicity is { } per ? (int)per : 0,
        Timing = (v.InterestTiming ?? InterestTiming.Arrears).ToString(),
        DayCount = Domain.Lending.DayCount.ContadorDeDias.Base(v.DayCount),
        IssueTermBand = v.IssueTermBand?.ToString() ?? string.Empty,
        RolloverNumber = v.RolloverCount,
        RulesVersion = v.RulesVersion,
    };
}
