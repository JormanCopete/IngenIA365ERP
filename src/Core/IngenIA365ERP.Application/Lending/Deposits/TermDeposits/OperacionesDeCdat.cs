using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits;

/// <summary>
/// El pago de un período del plan de un CDAT (feature 014, US6, T172; FR-069; R06): en una operación <c>TermDepositInterestPayment</c> (<c>PIC-</c>) la
/// retención en pareja sobre el interés por pagar (−interés, +retención; data-model §0.2), el pago del neto (<c>InterestPaid</c>) y su destino —la cuenta de
/// ahorro a la vista del titular (<c>TransferIn</c>) con el GMF de la redención de CDAT abonada en cuenta (D32, Oficio DIAN 2787 de 2019; pendiente de
/// validar para los intereses, research E140) o una orden de giro (<c>TermDepositInterest</c>) con su <c>OrdenDeGiroEmitida</c> v2—, el registro del pago con
/// los acumulados de retención por titular y el hecho <c>InteresesDeCdatPagados</c> (+ <c>RetencionPracticada</c>, + <c>GmfRetenido</c>). Las cuentas ya están
/// bloqueadas; guarda. (nuevo)
/// </summary>
public sealed class PagoDeInteresesDeCdat(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ReglasDeMovimientos movimientos,
    ReglasDeRetencion retencion,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string PrefijoPago = "PIC-";

    public async Task<Result<MemberAccountOperation>> PagarAsync(MemberAccount cdat, MemberAccountControl control, MemberAccountTermsVersion v, PlanDeCdatCalculado plan,
        PeriodoDeCdat p, DateOnly fecha, LendingProcessRun? corrida, CancellationToken ct)
    {
        var destino = await PiezasDeCdat.DestinoAsync(db, cdat, v, alVencimiento: false, ct);
        if (destino.IsFailure) return Result.Failure<MemberAccountOperation>(destino.Error);
        var d = destino.Value;
        var linea = await db.DepositLineVersions.AsNoTracking().FirstAsync(x => x.Id == v.DepositLineVersionId, ct);
        var ret = await PiezasDeCdat.RetencionAsync(db, retencion, cdat, linea, plan, p.Desde, p.Hasta, p.Interes, fecha, ct);
        if (ret.IsFailure) return Result.Failure<MemberAccountOperation>(ret.Error);
        var (r, politica, perfil, natural) = ret.Value;
        var neto = p.Interes - r.Valor;

        var hecho = await emision.HechoDeCuentaAsync(cdat, fecha, ct);
        if (hecho.IsFailure) return Result.Failure<MemberAccountOperation>(hecho.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.TermDepositInterestPayment, PrefijoPago, ct);
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cdat.Id,
            CounterpartMemberAccountId = d.Cuenta?.Id,
            Kind = MemberAccountOperationKind.TermDepositInterestPayment,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = fecha,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = p.Interes,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorCode = LiquidacionDeIntereses.RecaudadorPropio,
            Payload = JsonSerializer.Serialize(new { version = v.VersionNumber, period = p.Numero, from = p.Desde, to = p.Hasta, gross = p.Interes, withholding = r.Valor }, Json),
            InputsSha256 = new HuellaDeCartera().Identificador("account", cdat.PublicId).Entero("version", v.VersionNumber).Entero("period", p.Numero)
                .Decimal("gross", p.Interes, 2).Decimal("withholding", r.Valor, 2).Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);

        Domain.Entities.Lending.Deposits.Accounts.AccountPayoutOrder? orden = null;
        OtraCuentaDeCdat? otra = null;
        (GmfAEmitir, HechoDeCuenta)? gmfAEmitir = null;
        GmfCalculado? gmf = null;
        HechoDeCuenta? hechoDestino = null;
        if (d.Cuenta is { } cuentaDestino)
        {
            var h = await emision.HechoDeCuentaAsync(cuentaDestino, fecha, ct);
            if (h.IsFailure) return Result.Failure<MemberAccountOperation>(h.Error);
            hechoDestino = h.Value;
            if (neto > 0m)
            {
                var leida = await movimientos.LeerAsync(cdat, fecha, ct);
                var g = await movimientos.GmfAsync(leida, HechosGeneradoresDeGmf.RedencionDeCdatAbonada, neto, fecha, ct);
                if (g.IsFailure) return Result.Failure<MemberAccountOperation>(g.Error);
                gmf = g.Value;
            }
            otra = new OtraCuentaDeCdat(h.Value, [new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = neto }]);
            if (GravamenMovimientosFinancieros.SeCobra(gmf))
                gmfAEmitir = (new GmfAEmitir(gmf, null, nameof(DepositClass.TermDeposit)), h.Value);
        }
        else if (neto > 0m)
        {
            orden = AccountPayoutOrder.Emitir(cdat.Id, operacion.Id, PayoutPurpose.TermDepositInterest, d.Beneficiario!.Id, d.Beneficiario.IdType ?? string.Empty,
                d.Beneficiario.TaxId ?? string.Empty, ReferenciasDeGobierno.Nombre(d.Beneficiario), d.BancoId, d.TipoDeCuenta, d.NumeroDeCuenta, neto,
                hecho.Value.FechaContable.AddDays(1), ahora, actor.UserId);
            await numerador.NumerarOrdenDeCuentaAsync(orden, Disbursements.TramoDeDesembolso.PrefijoDeOrden, ct);
            db.AccountPayoutOrders.Add(orden);
            await db.SaveChangesAsync(ct);
        }

        var pago = PiezasDeCdat.Pago(cdat, operacion, p.Numero, p.Desde, p.Hasta, p.Interes, p.Interes, r, politica, perfil, natural,
            d.Cuenta is null ? InterestDestination.PaymentOrder : InterestDestination.ToAccount);
        var unidad = await emision.InteresesDeCdatPagadosAsync(new HechoDeCdatAEmitir(hecho.Value, operacion,
            [new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.InterestPayable), Amount = -neto }], EmisionDeCartera.Terminos(v, cdat.Number),
            PrevalidationOutcome.NotApplicable)
        {
            PorElProceso = corrida is not null,
            OtraCuenta = otra,
            Orden = orden,
            Beneficiario = orden is null ? null : d.Payee,
            TipoDeOrden = nameof(PayoutPurpose.TermDepositInterest),
            Gmf = gmfAEmitir,
            Retencion = new RetencionDeCdat(r, politica, perfil, p.Hasta.Year),
            NumeroDePeriodo = p.Numero,
            PeriodoDesde = p.Desde.AddDays(1),
            PeriodoHasta = p.Hasta,
        }, ct);
        if (unidad.IsFailure) return Result.Failure<MemberAccountOperation>(unidad.Error);
        var mensaje = unidad.Value.De(InteresesDeCdatPagadosV1.Type);
        var mensajeRetencion = unidad.Value.De(RetencionPracticadaV1.Type);
        var mensajeGmf = unidad.Value.De(GmfRetenidoV1.Type);
        var contable = hecho.Value.FechaContable;
        var filas = new List<MemberAccountTransaction>();
        filas.AddRange(PiezasDeCdat.FilasDeRetencion(cdat.Id, operacion, r, fecha, contable, mensajeRetencion, v.Id, p.Numero));
        if (neto > 0m)
            filas.Add(PiezasDeCdat.Fila(cdat.Id, operacion, MemberAccountTransactionKind.InterestPaid, MemberAccountConcept.InterestPayable, -neto, fecha, contable,
                mensaje, v.Id, p.Numero));
        if (d.Cuenta is { } dc && neto > 0m)
        {
            filas.Add(PiezasDeCdat.Fila(dc.Id, operacion, MemberAccountTransactionKind.TransferIn, MemberAccountConcept.Principal, neto, fecha, contable, mensaje));
            if (GravamenMovimientosFinancieros.SeCobra(gmf))
            {
                filas.Add(PiezasDeCdat.Fila(dc.Id, operacion, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Principal, -gmf.Valor, fecha, contable, mensajeGmf));
                filas.Add(PiezasDeCdat.Fila(dc.Id, operacion, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Gmf, gmf.Valor, fecha, contable, mensajeGmf));
            }
        }
        if (filas.Count > 0) registro.RegistrarTransacciones(filas);
        if (d.Cuenta is { } dg && gmf is not null)
            registro.RegistrarUsoDeGmf(cdat.HolderPersonId, operacion, dg.Id, fecha, gmf.HechoGenerador, gmf.Base, gmf.ExentoAplicado, gmf.BaseGravada, gmf.Valor,
                gmf.Politica, gmf.Tarifa);
        registro.RegistrarPagoDeCdat(control, pago, await LiquidacionDeIntereses.AcumuladosAsync(db, cdat, operacion, pago, ct));
        await db.SaveChangesAsync(ct);
        return Result.Success(operacion);
    }
}

/// <summary>El resultado de la salida de un CDAT: la operación, la orden de giro si la hubo y la redención calculada. (nuevo)</summary>
public sealed record SalidaDeCdatAplicada(MemberAccountOperation Operacion, AccountPayoutOrder? Orden, RedencionDeCdatCalculada Calculo, decimal Entregado,
    decimal Retencion, GmfCalculado? Gmf);

/// <summary>Lo que se calculó para una salida sin escribir (la vista previa de la redención, api.md §5). (nuevo)</summary>
public sealed record SalidaDeCdatCalculada(
    MemberAccount Cuenta,
    MemberAccountTermsVersion Version,
    PlanDeCdatCalculado Plan,
    decimal Capital,
    decimal InteresPorPagar,
    bool Anticipada,
    RedencionDeCdatCalculada Calculo,
    RetencionCalculada Retencion,
    PoliticaDeRetencion Politica,
    PerfilDeRetencion Perfil,
    bool Natural,
    GmfCalculado? GmfSiSeAbona,
    PeriodoDeCdat? Periodo)
{
    /// <summary>
    /// Lo que el plan causa hasta <see cref="CausadaHasta"/> y el proceso diario todavía no causó (la salida el mismo día, antes de la corrida de la noche):
    /// entra a lo causado de la redención y la operación lo escribe como ajuste (research E141).
    /// </summary>
    public decimal Faltante { get; init; }

    /// <summary>Hasta dónde queda causado el CDAT con la salida: la fecha de la salida o el vencimiento, lo que sea antes.</summary>
    public DateOnly CausadaHasta { get; init; }

    public decimal InteresNeto => Calculo.InteresAPagar - Retencion.Valor;

    public decimal CapitalEntregado => Capital - Calculo.PenalidadSobreCapital;

    public decimal Entregado => CapitalEntregado + InteresNeto;

    /// <summary>La huella de la vista previa (lo que el titular vio antes de confirmar).</summary>
    public string Huella => new HuellaDeCartera().Identificador("account", Cuenta.PublicId).Entero("version", Version.VersionNumber)
        .Decimal("principal", Capital, 2).Decimal("interest", Calculo.InteresAPagar, 2).Decimal("adjustment", Calculo.AjusteDeCausacion, 6)
        .Decimal("penaltyInterest", Calculo.PenalidadSobreInteres, 2).Decimal("penaltyPrincipal", Calculo.PenalidadSobreCapital, 2)
        .Decimal("withholding", Retencion.Valor, 2).Logico("early", Anticipada).Calcular();
}

/// <summary>
/// La salida de un CDAT (feature 014, US6, T172, T173; FR-070, FR-071; research E141, E142): la cancelación al vencimiento o en la gracia (sin penalidad,
/// <c>CdatCancelado</c>) y la redención anticipada (<c>CdatRedimido</c>) con la penalidad sellada por <see cref="PlanDeCdat.Redimir"/> —reliquidar a la tasa de
/// penalidad (un ajuste de la causación a la baja; si lo pagado supera lo reliquidado, el exceso sale del capital) o descontar una fracción de lo causado—. En
/// una operación <c>TermDepositRedemption</c> (<c>RDC-</c>): el ajuste, la penalidad, la retención en pareja sobre el interés que se paga, el pago del interés y
/// la salida del capital, y el destino: la cuenta de ahorro a la vista (con el GMF de la redención abonada, D32, sobre capital más intereses) o la orden de
/// giro (<c>TermDepositRedemption</c>), que deja la cuenta en cierre hasta que Tesorería informe. Las cuentas ya están bloqueadas; guarda. (nuevo)
/// </summary>
public sealed class SalidaDeCdat(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ReglasDeMovimientos movimientos,
    ReglasDeRetencion retencion,
    LectorDeCdat lector,
    IActorActual actorActual,
    IDateTimeService reloj,
    Documents.TitulosDeCdat? titulos = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string PrefijoSalida = "RDC-";

    /// <summary>Calcula sin escribir la salida a <paramref name="fecha"/>: anticipada (antes del vencimiento) con la penalidad sellada, o sin penalidad.</summary>
    public async Task<Result<SalidaDeCdatCalculada>> CalcularAsync(MemberAccount cdat, DateOnly fecha, CancellationToken ct)
    {
        var leido = await lector.LeerAsync(cdat, ct);
        if (leido.Vigente is not { } v) return Result.Failure<SalidaDeCdatCalculada>(ErroresDeCdat.FondeoInvalido($"El CDAT {cdat.Number} no está constituido."));
        var plan = await lector.PlanAsync(v, ct);
        var saldos = (await LectorDeCuentas.SaldosAsync(db, [cdat.Id], ct)).GetValueOrDefault(cdat.Id) ?? [];
        var capital = saldos.GetValueOrDefault(MemberAccountConcept.Principal);
        var anticipada = fecha < plan.Vencimiento;
        var hasta = anticipada ? fecha : plan.Vencimiento;
        var marca = await db.MemberAccountControls.AsNoTracking().Where(x => x.MemberAccountId == cdat.Id).Select(x => x.LastAccruedDate).FirstAsync(ct);
        var faltante = marca < hasta ? PlanDeCdat.CausadoEntre(plan, marca > plan.Condiciones.Inicio ? marca : plan.Condiciones.Inicio, hasta) : 0m;
        var porPagar = saldos.GetValueOrDefault(MemberAccountConcept.InterestPayable) + faltante;
        var pagado = await PiezasDeCdat.PagadoDeLaVersionAsync(db, cdat, plan, ct);
        PenalidadDeCdat? penalidad = anticipada && v.EarlyRedemptionPenaltyMode is { } modo
            ? new PenalidadDeCdat(modo, v.PenaltyAnnualRate, v.PenaltyDiscountFraction)
            : null;
        var calculo = PlanDeCdat.Redimir(LectorDeCdat.Condiciones(v), fecha, porPagar, pagado, penalidad);
        var periodo = plan.PeriodoDe(fecha) ?? plan.Periodos.LastOrDefault();
        var linea = await db.DepositLineVersions.AsNoTracking().FirstAsync(x => x.Id == v.DepositLineVersionId, ct);
        var ret = await PiezasDeCdat.RetencionAsync(db, retencion, cdat, linea, plan, periodo?.Desde ?? plan.Condiciones.Inicio,
            fecha < plan.Vencimiento ? fecha : plan.Vencimiento, calculo.InteresAPagar, fecha, ct);
        if (ret.IsFailure) return Result.Failure<SalidaDeCdatCalculada>(ret.Error);
        var (r, politica, perfil, natural) = ret.Value;
        var entregado = capital - calculo.PenalidadSobreCapital + calculo.InteresAPagar - r.Valor;
        GmfCalculado? gmf = null;
        if (entregado > 0m)
        {
            var g = await movimientos.GmfAsync(await movimientos.LeerAsync(cdat, fecha, ct), HechosGeneradoresDeGmf.RedencionDeCdatAbonada, entregado, fecha, ct);
            if (g.IsFailure) return Result.Failure<SalidaDeCdatCalculada>(g.Error);
            gmf = g.Value;
        }
        return Result.Success(new SalidaDeCdatCalculada(cdat, v, plan, capital, porPagar, anticipada, calculo, r, politica, perfil, natural, gmf, periodo)
        {
            Faltante = faltante,
            CausadaHasta = hasta,
        });
    }

    /// <summary>
    /// Aplica la salida calculada al destino resuelto. <paramref name="causa"/> es la de <c>CdatCancelado</c> (sin penalidad) o nula en la redención
    /// anticipada (<c>CdatRedimido</c>). <paramref name="aprobadoPor"/> es el aprobador de la atribución con monto, si la hubo.
    /// </summary>
    public async Task<Result<SalidaDeCdatAplicada>> AplicarAsync(SalidaDeCdatCalculada s, MemberAccountControl control, DestinoResueltoDeCdat d, DateOnly fecha,
        string? causa, string motivo, LendingProcessRun? corrida, int? aprobadoPor, PrevalidationOutcome validacion, CancellationToken ct)
    {
        var cdat = s.Cuenta;
        var v = s.Version;
        var c = s.Calculo;
        var r = s.Retencion;
        var entregado = s.Entregado;
        var hecho = await emision.HechoDeCuentaAsync(cdat, fecha, ct);
        if (hecho.IsFailure) return Result.Failure<SalidaDeCdatAplicada>(hecho.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.TermDepositRedemption, PrefijoSalida, ct);
        var texto = LectorDeCdat.Recortar(motivo, 500);
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cdat.Id,
            CounterpartMemberAccountId = d.Cuenta?.Id,
            Kind = MemberAccountOperationKind.TermDepositRedemption,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = fecha,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = entregado,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorCode = LiquidacionDeIntereses.RecaudadorPropio,
            AuthorizedByUserId = aprobadoPor,
            Reason = texto,
            Payload = JsonSerializer.Serialize(new
            {
                early = s.Anticipada, cause = causa, principal = s.Capital, interest = c.InteresAPagar, adjustment = c.AjusteDeCausacion + c.AjustePorRedondeo,
                penaltyInterest = c.PenalidadSobreInteres, penaltyPrincipal = c.PenalidadSobreCapital, withholding = r.Valor, delivered = entregado,
            }, Json),
            InputsSha256 = s.Huella,
            RulesVersion = VersionesDeReglas.Vigente,
            PrevalidationOutcome = validacion.ToString(),
        });
        await db.SaveChangesAsync(ct);

        AccountPayoutOrder? orden = null;
        OtraCuentaDeCdat? otra = null;
        (GmfAEmitir, HechoDeCuenta)? gmfAEmitir = null;
        var gmf = d.Cuenta is null ? null : s.GmfSiSeAbona;
        if (d.Cuenta is { } cuentaDestino)
        {
            var h = await emision.HechoDeCuentaAsync(cuentaDestino, fecha, ct);
            if (h.IsFailure) return Result.Failure<SalidaDeCdatAplicada>(h.Error);
            otra = new OtraCuentaDeCdat(h.Value, [new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = entregado }]);
            if (GravamenMovimientosFinancieros.SeCobra(gmf)) gmfAEmitir = (new GmfAEmitir(gmf, null, nameof(DepositClass.TermDeposit)), h.Value);
        }
        else if (entregado > 0m)
        {
            orden = AccountPayoutOrder.Emitir(cdat.Id, operacion.Id, PayoutPurpose.TermDepositRedemption, d.Beneficiario!.Id, d.Beneficiario.IdType ?? string.Empty,
                d.Beneficiario.TaxId ?? string.Empty, ReferenciasDeGobierno.Nombre(d.Beneficiario), d.BancoId, d.TipoDeCuenta, d.NumeroDeCuenta, entregado,
                hecho.Value.FechaContable.AddDays(1), ahora, actor.UserId);
            await numerador.NumerarOrdenDeCuentaAsync(orden, Disbursements.TramoDeDesembolso.PrefijoDeOrden, ct);
            db.AccountPayoutOrders.Add(orden);
            await db.SaveChangesAsync(ct);
        }

        var periodo = s.Periodo?.Numero;
        var ajuste = s.Faltante + c.AjusteDeCausacion + c.AjustePorRedondeo;
        // Lo que el plan causa hasta la salida y el proceso no había causado entra en la operación: la marca de agua llega a ese día (research E141).
        for (var dia = control.LastAccruedDate.AddDays(1); dia <= s.CausadaHasta; dia = dia.AddDays(1)) RegistroDeCuentas.AvanzarCausacion(control, dia);
        var penalidadValores = new List<DepositConceptAmountV1>();
        if (c.PenalidadSobreInteres > 0m)
            penalidadValores.Add(new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.InterestPayable), Amount = -c.PenalidadSobreInteres });
        if (c.PenalidadSobreCapital > 0m)
            penalidadValores.Add(new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = -c.PenalidadSobreCapital });
        var valores = new List<DepositConceptAmountV1>();
        if (s.CapitalEntregado != 0m) valores.Add(new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = -s.CapitalEntregado });
        if (s.InteresNeto != 0m) valores.Add(new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.InterestPayable), Amount = -s.InteresNeto });
        var e = new HechoDeCdatAEmitir(hecho.Value, operacion, valores, EmisionDeCartera.Terminos(v, cdat.Number), validacion)
        {
            PorElProceso = corrida is not null,
            OtraCuenta = otra,
            Orden = orden,
            Beneficiario = orden is null ? null : d.Payee,
            TipoDeOrden = nameof(PayoutPurpose.TermDepositRedemption),
            Gmf = gmfAEmitir,
            Retencion = c.InteresAPagar > 0m ? new RetencionDeCdat(r, s.Politica, s.Perfil, fecha.Year) : null,
            Penalidad = penalidadValores.Count > 0 ? (c.PenalidadSobreInteres + c.PenalidadSobreCapital, s.InteresPorPagar, penalidadValores) : null,
            DetalleDePenalidad = s.Anticipada
                ? new TermDepositPenaltyV1
                {
                    Kind = v.EarlyRedemptionPenaltyMode?.ToString() ?? string.Empty, RepricedRate = v.EarlyRedemptionPenaltyMode == EarlyRedemptionPenaltyMode.PenaltyRate
                        ? v.PenaltyAnnualRate
                        : null,
                    Amount = c.CostoDeLaRedencion,
                }
                : null,
            AjusteDeCausacion = ajuste,
            CausaDelAjuste = c.AjusteDeCausacion != 0m ? EmisionDeCartera.CausaRedencionAnticipada : EmisionDeCartera.CausaCorreccionDeTasa,
            CausaDeCancelacion = causa,
        };
        var unidad = s.Anticipada && causa is null ? await emision.CdatRedimidoAsync(e, ct) : await emision.CdatCanceladoAsync(e, ct);
        if (unidad.IsFailure) return Result.Failure<SalidaDeCdatAplicada>(unidad.Error);
        var mensaje = unidad.Value.De(s.Anticipada && causa is null ? CdatRedimidoV1.Type : CdatCanceladoV1.Type);
        var mRet = unidad.Value.De(RetencionPracticadaV1.Type);
        var mGmf = unidad.Value.De(GmfRetenidoV1.Type);
        var mPen = unidad.Value.De(PenalidadAplicadaV1.Type);
        var mAj = unidad.Value.De(AjusteDeCausacionDeCaptacionV1.Type);
        var contable = hecho.Value.FechaContable;
        var filas = new List<MemberAccountTransaction>();
        void Del(MemberAccountTransactionKind k, MemberAccountConcept co, decimal m, Guid? msg)
        {
            if (m != 0m) filas.Add(PiezasDeCdat.Fila(cdat.Id, operacion, k, co, m, fecha, contable, msg, v.Id, periodo));
        }
        Del(MemberAccountTransactionKind.AccrualAdjustment, MemberAccountConcept.InterestPayable, ajuste, mAj);
        Del(MemberAccountTransactionKind.PenaltyApplied, MemberAccountConcept.InterestPayable, -c.PenalidadSobreInteres, mPen);
        Del(MemberAccountTransactionKind.PenaltyApplied, MemberAccountConcept.Principal, -c.PenalidadSobreCapital, mPen);
        if (c.InteresAPagar > 0m) filas.AddRange(PiezasDeCdat.FilasDeRetencion(cdat.Id, operacion, r, fecha, contable, mRet, v.Id, periodo));
        Del(MemberAccountTransactionKind.InterestPaid, MemberAccountConcept.InterestPayable, -s.InteresNeto, mensaje);
        Del(d.Cuenta is null ? MemberAccountTransactionKind.Withdrawal : MemberAccountTransactionKind.TransferOut, MemberAccountConcept.Principal, -s.CapitalEntregado,
            mensaje);
        if (d.Cuenta is { } dc && entregado > 0m)
        {
            filas.Add(PiezasDeCdat.Fila(dc.Id, operacion, MemberAccountTransactionKind.TransferIn, MemberAccountConcept.Principal, entregado, fecha, contable, mensaje));
            if (GravamenMovimientosFinancieros.SeCobra(gmf))
            {
                filas.Add(PiezasDeCdat.Fila(dc.Id, operacion, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Principal, -gmf.Valor, fecha, contable, mGmf));
                filas.Add(PiezasDeCdat.Fila(dc.Id, operacion, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Gmf, gmf.Valor, fecha, contable, mGmf));
            }
        }
        if (filas.Count > 0) registro.RegistrarTransacciones(filas);
        if (d.Cuenta is { } dg && gmf is not null)
            registro.RegistrarUsoDeGmf(cdat.HolderPersonId, operacion, dg.Id, fecha, gmf.HechoGenerador, gmf.Base, gmf.ExentoAplicado, gmf.BaseGravada, gmf.Valor,
                gmf.Politica, gmf.Tarifa);
        if (c.InteresAPagar > 0m && periodo is { } np)
        {
            var desde = s.Periodo?.Desde ?? s.Plan.Condiciones.Inicio;
            var pago = PiezasDeCdat.Pago(cdat, operacion, np, desde, fecha, c.InteresAPagar - c.AjustePorRedondeo, c.InteresAPagar, r, s.Politica, s.Perfil, s.Natural,
                d.Cuenta is null ? InterestDestination.PaymentOrder : InterestDestination.ToAccount);
            registro.RegistrarPagoDeCdat(control, pago, await LiquidacionDeIntereses.AcumuladosAsync(db, cdat, operacion, pago, ct));
        }
        if (d.Cuenta is { } activa)
        {
            var controlDestino = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == activa.Id, ct);
            RegistroDeCuentas.MarcarActividadDelTitular(controlDestino, fecha);
        }
        RegistroDeCuentas.MarcarActividadDelTitular(control, fecha);
        var seguida = db.MemberAccounts.Local.FirstOrDefault(a => a.Id == cdat.Id) ?? cdat;
        if (orden is not null) seguida.PedirCierre(ahora);
        else seguida.Cerrar(fecha, actor.UserId, texto.Length > 0 ? texto : "Cancelación del CDAT", ahora);
        seguida.MaturityInstructionJson = null;
        // US13 (T196; documentos.md §2.5): el título vigente queda liquidado.
        if (titulos is not null) await titulos.LiquidarAsync(cdat, operacion, actor.UserId, operacion.RegisteredAt, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new SalidaDeCdatAplicada(operacion, orden, c, entregado, r.Valor, gmf));
    }
}

/// <summary>
/// La prórroga de un CDAT (feature 014, US6, T172; FR-070, FR-073; D17, D34; research E139): al cierre del último día de gracia sin otra instrucción, por el
/// mismo plazo con la regla de tasa de la línea —la misma tasa de la tabla, la vigente de la tabla para el plazo y el capital, o la pactada—, con el margen
/// negociado sólo si la versión lo conserva, y con o sin capitalización del interés del último período neto de retención (FR-063). La versión nueva empieza
/// en el vencimiento si la gracia causa y, si no, el último día de gracia (los días de gracia no causan, D17). Operación <c>TermDepositRollover</c>
/// (<c>PRC-</c>), versión nueva con su plan por <see cref="RegistroDeCuentas.SellarCondiciones"/>, título nuevo (sufijo de la versión) y el hecho
/// <c>CdatProrrogado</c> (+ <c>RetencionPracticada</c> si capitaliza, + <c>CaptacionReclasificada</c> si el plazo de emisión cambia). Guarda. (nuevo)
/// </summary>
public sealed class ProrrogaDeCdat(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ReglasDeRetencion retencion,
    LectorDeCdat lector,
    IActorActual actorActual,
    IDateTimeService reloj,
    Documents.TitulosDeCdat? titulos = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string PrefijoProrroga = "PRC-";

    /// <summary>Sin tasa vigente para el plazo o sin versión de la línea: no hay prórroga (FR-070).</summary>
    public const string CodigoSinTasaDeProrroga = "Lending.TermDeposit.NoRolloverRate";

    /// <summary>
    /// Prorroga la versión <paramref name="v"/> (ver la clase). Falla con <see cref="CodigoSinTasaDeProrroga"/> si la línea no tiene versión o tasa para el
    /// plazo y el capital nuevos (quien llama aplica el tratamiento de la línea).
    /// </summary>
    public async Task<Result<MemberAccountTermsVersion>> ProrrogarAsync(MemberAccount cdat, MemberAccountControl control, MemberAccountTermsVersion v,
        PlanDeCdatCalculado plan, DateOnly finDeLaGracia, bool capitaliza, LendingProcessRun? corrida, CancellationToken ct)
    {
        var inicio = v.GraceAccrues == true ? plan.Vencimiento : finDeLaGracia;
        var linea = await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, cdat.CreditLineId, inicio, null, ct);
        if (linea is null) return SinTasa(cdat.Number);
        var ultimo = plan.Periodos[^1];
        RetencionCalculada? r = null;
        PoliticaDeRetencion? politica = null;
        var perfil = PerfilDeRetencion.Standard;
        var natural = false;
        var capitalizado = 0m;
        if (capitaliza)
        {
            var lineaAnterior = await db.DepositLineVersions.AsNoTracking().FirstAsync(x => x.Id == v.DepositLineVersionId, ct);
            var ret = await PiezasDeCdat.RetencionAsync(db, retencion, cdat, lineaAnterior, plan, ultimo.Desde, ultimo.Hasta, ultimo.Interes, inicio, ct);
            if (ret.IsFailure) return Result.Failure<MemberAccountTermsVersion>(ret.Error);
            (r, politica, perfil, natural) = ret.Value;
            capitalizado = ultimo.Interes - r.Valor;
        }
        var capital = v.PrincipalAmount + capitalizado;
        var margen = v.KeepsNegotiatedMargin == true ? v.NegotiatedSpread : null;
        decimal tabla;
        switch (v.RolloverRateRule ?? RolloverRateRule.CurrentTableRate)
        {
            case RolloverRateRule.SameRate:
                tabla = v.TableAnnualRate;
                break;
            default:
                var leida = await lector.TasaDeLaTablaAsync(linea, v.TermDays ?? 0, capital, inicio, ct);
                if (leida.IsFailure) return SinTasa(cdat.Number);
                tabla = leida.Value.Tasa;
                break;
        }
        var condiciones = new CondicionesDeCdat(capital, tabla + (margen ?? 0m), inicio, v.TermDays ?? 0, v.InterestPeriodicity ?? RepaymentPeriodicity.SinglePayment,
            v.InterestTiming ?? InterestTiming.Arrears, linea.DayCount, linea.Redondeo());
        var calendario = await lector.CalendarioAsync(inicio, PlanDeCdat.Vencimiento(inicio, condiciones.PlazoDias), ct);
        var nuevo = PlanDeCdat.Generar(condiciones, calendario);

        var antes = await emision.HechoDeCuentaAsync(cdat, inicio, ct);
        if (antes.IsFailure) return Result.Failure<MemberAccountTermsVersion>(antes.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.TermDepositRollover, PrefijoProrroga, ct);
        var huella = new HuellaDeCartera().Identificador("account", cdat.PublicId).Entero("version", v.VersionNumber + 1).Fecha("start", inicio)
            .Decimal("principal", capital, 2).Decimal("rate", condiciones.TasaEfectivaAnual, 6).Decimal("interest", nuevo.InteresTotal, 2).Calcular();
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cdat.Id,
            Kind = MemberAccountOperationKind.TermDepositRollover,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = inicio,
            AccountingDate = antes.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = capital,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorCode = LiquidacionDeIntereses.RecaudadorPropio,
            Payload = JsonSerializer.Serialize(new { previous = v.VersionNumber, start = inicio, principal = capital, capitalized = capitalizado, rate = condiciones.TasaEfectivaAnual,
                rule = (v.RolloverRateRule ?? RolloverRateRule.CurrentTableRate).ToString() }, Json),
            InputsSha256 = huella,
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);

        NegociacionDeCdat? negociacion = null;
        if (margen is { } m && v.NegotiatedRateRuleId is { } reglaId)
        {
            var publica = await db.NegotiatedRateRules.AsNoTracking().Where(x => x.Id == reglaId).Select(x => x.PublicId).FirstOrDefaultAsync(ct);
            negociacion = new NegociacionDeCdat(m, reglaId, publica, v.NegotiationApprovedByUserId, v.NegotiationReason ?? "Margen conservado en la prórroga",
                v.NegotiatedRateRequestId);
        }
        var destino = new DestinoDeCdat(v.InterestDestination == InterestDestination.PaymentOrder ? TermDepositDestinationKind.PaymentOrder : TermDepositDestinationKind.Account,
            null, null, null, v.DestinationAccountType, v.DestinationBankAccountNumber);
        var terminos = LectorDeCdat.NuevaVersion(cdat, v.VersionNumber + 1, operacion, AccountTermsCause.Rollover, linea, nuevo, tabla, negociacion, destino,
            v.DestinationAccountId, v.DestinationPersonId, v.DestinationBankId, v.MaturityInstruction ?? MaturityInstruction.Rollover, v.RolloverCount + 1, v.Id, ahora,
            huella);
        registro.SellarCondiciones(control, terminos, LectorDeCdat.FilasDelPlan(terminos, nuevo), v.VersionNumber);
        await db.SaveChangesAsync(ct);

        var despues = await emision.HechoDeCuentaAsync(cdat, inicio, ct);
        if (despues.IsFailure) return Result.Failure<MemberAccountTermsVersion>(despues.Error);
        var cambiaElPlazo = v.IssueTermBand != terminos.IssueTermBand;
        var valores = capitalizado > 0m
            ? new List<DepositConceptAmountV1>
            {
                new() { Concept = nameof(MemberAccountConcept.InterestPayable), Amount = -capitalizado },
                new() { Concept = nameof(MemberAccountConcept.Principal), Amount = capitalizado },
            }
            : [];
        var unidad = await emision.CdatProrrogadoAsync(new HechoDeCdatAEmitir(despues.Value, operacion, valores, EmisionDeCartera.Terminos(terminos, cdat.Number),
            PrevalidationOutcome.NotApplicable)
        {
            PorElProceso = corrida is not null,
            TerminosAnteriores = EmisionDeCartera.Terminos(v, cdat.Number),
            InteresCapitalizado = capitalizado,
            Retencion = r is not null && politica is not null ? new RetencionDeCdat(r, politica, perfil, ultimo.Hasta.Year) : null,
            Reclasificacion = cambiaElPlazo
                ? (antes.Value.Dimensiones(), [new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = v.PrincipalAmount }])
                : null,
        }, (v.RolloverRateRule ?? RolloverRateRule.CurrentTableRate).ToString(), ct);
        if (unidad.IsFailure) return Result.Failure<MemberAccountTermsVersion>(unidad.Error);
        if (capitaliza && r is not null && politica is not null)
        {
            var mensaje = unidad.Value.De(CdatProrrogadoV1.Type);
            var mRet = unidad.Value.De(RetencionPracticadaV1.Type);
            var contable = despues.Value.FechaContable;
            var filas = new List<MemberAccountTransaction>();
            filas.AddRange(PiezasDeCdat.FilasDeRetencion(cdat.Id, operacion, r, inicio, contable, mRet, v.Id, ultimo.Numero));
            if (capitalizado > 0m)
            {
                filas.Add(PiezasDeCdat.Fila(cdat.Id, operacion, MemberAccountTransactionKind.Capitalization, MemberAccountConcept.InterestPayable, -capitalizado, inicio,
                    contable, mensaje, v.Id, ultimo.Numero));
                filas.Add(PiezasDeCdat.Fila(cdat.Id, operacion, MemberAccountTransactionKind.Capitalization, MemberAccountConcept.Principal, capitalizado, inicio, contable,
                    mensaje, terminos.Id));
            }
            if (filas.Count > 0) registro.RegistrarTransacciones(filas);
            var pago = PiezasDeCdat.Pago(cdat, operacion, ultimo.Numero, ultimo.Desde, ultimo.Hasta, ultimo.Interes, ultimo.Interes, r, politica, perfil, natural,
                InterestDestination.Capitalize);
            registro.RegistrarPagoDeCdat(control, pago, await LiquidacionDeIntereses.AcumuladosAsync(db, cdat, operacion, pago, ct));
        }
        var seguida = db.MemberAccounts.Local.FirstOrDefault(a => a.Id == cdat.Id) ?? cdat;
        seguida.MaturityInstructionJson = null;
        await db.SaveChangesAsync(ct);
        // US13 (T196; documentos.md §2.5): el título de la versión nueva reemplaza al anterior (el del proceso, sin persona).
        if (titulos is not null) await titulos.EmitirAsync(seguida, terminos, operacion, actor.UserId, ct);
        return Result.Success(terminos);
    }

    private static Result<MemberAccountTermsVersion> SinTasa(string numero) => Result.Failure<MemberAccountTermsVersion>(new ErrorConDatos(CodigoSinTasaDeProrroga,
        $"La línea del CDAT {numero} no tiene tasa vigente para prorrogarlo: rige el tratamiento al vencimiento de la línea.", new { accountNumber = numero }));
}
