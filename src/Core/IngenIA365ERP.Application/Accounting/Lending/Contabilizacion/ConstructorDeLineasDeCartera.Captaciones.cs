using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Accounting;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;

/// <summary>
/// Feature 014, US8 (T143; contracts/contabilidad.md §1 a §5 de la 014; research C20, E106 a E110): el constructor de líneas de los hechos de captaciones y
/// aportes. Mismo constructor, misma matriz, mismo camino al libro (<c>AccountingPoster</c>); lo que cambia es cómo se lee el contenido.
///
/// <para>
/// <b>Los importes de un hecho de cuenta son el cambio del saldo de cada concepto</b> (mensajes.md §3: + = la Cartera le debe más al titular). Cada concepto
/// va a su rol (<see cref="R.RolDelConceptoDeCuenta"/>) por su lado natural —un rol de naturaleza crédito (capital, interés por pagar, retención, GMF, aportes)
/// sube por el crédito; el cargo por cobrar, por el débito— y lo que queda sin cuadrar va a la <b>contrapartida del hecho</b>: el puente del recaudador en una
/// consignación o un retiro por el contrato, la devolución por pagar en un retiro por giro o una devolución, el gasto por intereses en la causación, el
/// ingreso por cargos en un cargo, el puente del retiro (<c>OwnLoanPayoff</c>) en el cruce, etc. Los hechos que mueven dos conceptos de la cuenta (la
/// liquidación, la retención, el cargo pendiente que cobra una consignación) cuadran solos y no llevan contrapartida.
/// </para>
///
/// <para>
/// Cada línea de captaciones lleva el número de la cuenta (<see cref="Partida.Cuenta"/>): es su documento cruce <c>CTA</c> si la cuenta contable lo exige
/// (§5) y, en las cuentas que exigen tercero, cruce o base, la línea va una por cuenta también en el resumido. La marca de la cuenta (<c>AccountFlag</c>,
/// C20) y el plazo de emisión (sólo CDAT y contractual) viajan en las dimensiones y los exige el rol del capital. El reverso (<c>OperacionDeCuentaReversada</c>)
/// es el espejo de cada cuenta del original con las dimensiones vigentes y la contrapartida del original, que el reverso nombra.
/// </para>
/// </summary>
public static partial class ConstructorDeLineasDeCartera
{
    /// <summary>La clase del puente del débito automático y del cruce al retiro (contabilidad.md §3, D45).</summary>
    public const string ClaseDelPuenteInterno = nameof(IngenIA365ERP.Application.Common.Integration.Lending.ClaseDeRecaudador.OwnLoanPayoff);

    private static readonly HashSet<string> TiposDeCaptaciones =
        OperacionesDeCartera.DeCaptaciones.Select(o => o.Codigo).ToHashSet(StringComparer.Ordinal);

    /// <summary>¿El hecho es de captaciones o aportes (feature 014)?</summary>
    public static bool EsDeCaptaciones(string? tipo) => tipo is not null && TiposDeCaptaciones.Contains(tipo);

    /// <summary>La clase del recaudador de un hecho de captaciones (el mapeo <c>DN</c> de la consignación y el aporte que recauda Nómina).</summary>
    public static string? ClaseDeRecaudadorDeCaptacion(object? contenido) => contenido switch
    {
        ConsignacionEnCuentaAplicadaV1 x => x.Collector.CollectorClass,
        AporteRecibidoV1 x => x.Collector.CollectorClass,
        RetiroDeCuentaAplicadoV1 x => x.Collector?.CollectorClass,
        CdatConstituidoV1 x => x.Funding.Collector?.CollectorClass,
        OperacionDeCuentaReversadaV1 x => x.CollectorClass,
        _ => null,
    };

    /// <summary>La cuenta de captación o de aportes de un hecho que nombra una sola (para la descripción del comprobante).</summary>
    public static MemberAccountRefV1? CuentaDeCaptacion(object? contenido) =>
        contenido is null || !EsDeCaptaciones(contenido.GetType().GetField("Type")?.GetValue(null) as string)
            ? null
            : contenido.GetType().GetProperty("Account")?.GetValue(contenido) as MemberAccountRefV1 is { AccountNumber.Length: > 0 } cuenta ? cuenta : null;

    /// <summary>
    /// El tipo de comprobante de la unidad (§4.2, C01): el del hecho principal; el reverso de una operación de cuenta toma el de su <b>original</b>
    /// (contabilidad.md §5: «hereda el comprobante del original»; research E26) y, si el original no tiene mapeo, el suyo.
    /// </summary>
    private static Result<TipoDeLaUnidadDeCartera> TipoDeLaUnidad(HechoDeCartera principal, string? claseDeRecaudador, MapeosDeCartera mapeos)
    {
        if (principal.Contenido is OperacionDeCuentaReversadaV1 r
            && (Texto(r.Original?.Type) ?? Texto(r.Mirrors.FirstOrDefault()?.Type)) is { } original
            && mapeos.Resolver(original, claseDeRecaudador) is { IsSuccess: true } delOriginal)
            return delOriginal;
        return mapeos.Resolver(principal.Tipo, claseDeRecaudador);
    }

    /// <summary>
    /// El tipo de documento cruce de una línea: el de la unidad; en una unidad que junta hechos de crédito y de captaciones (el cruce al retiro con el pago
    /// del crédito, D45) la línea de la otra naturaleza toma el cruce del mapeo de su propio hecho (<c>PG</c> el crédito, <c>CTA</c> la cuenta).
    /// </summary>
    private static string? TipoDeCruce(Partida p, HechoDeCartera principal, TipoDeLaUnidadDeCartera? tipo, string? claseDeRecaudador, MapeosDeCartera mapeos)
    {
        if (EsDeCaptaciones(p.Hecho) == EsDeCaptaciones(principal.Tipo)) return tipo?.CrossDocumentTypeCode;
        var propio = mapeos.Resolver(p.Hecho, claseDeRecaudador);
        return propio.IsSuccess ? propio.Value.CrossDocumentTypeCode : tipo?.CrossDocumentTypeCode;
    }

    /// <summary>
    /// El desglose de un hecho de captaciones o aportes en partidas (§3 de la 014). Devuelve falso si el contenido no es de captaciones (el desglosador de
    /// créditos lo informa como versión no aceptada).
    /// </summary>
    private sealed class DesglosadorDeCaptaciones(HechoDeCartera hecho, List<Partida> destino, List<FalloDeCartera> fallos, Func<int> nuevoPar)
    {
        private readonly IntegrationEnvelopeV1 _sobre = hecho.Sobre;

        public bool Desglosar()
        {
            switch (hecho.Contenido)
            {
                case ConsignacionEnCuentaAplicadaV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                        Recaudo(x.Dimensions, x.Account, x.Collector.CollectorClass, x.Collector.PayerPersonPublicId), "sin recaudador");
                    break;
                case AporteRecibidoV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                        Recaudo(x.Dimensions, x.Account, x.Collector.CollectorClass, x.Collector.PayerPersonPublicId), "sin recaudador");
                    break;
                case RetiroDeCuentaAplicadoV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts, DestinoDelRetiro(x), "un retiro sin recaudador ni orden de giro");
                    break;
                case TrasladoEntreCuentasAplicadoV1 x:
                    // Las dos puntas son cuentas del asociado: cuadran entre sí (el GMF va en su acompañante).
                    Saldos(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts);
                    Saldos(x.Target, x.TargetHolderPersonPublicId, x.TargetDimensions, x.TargetAmounts);
                    break;
                case GmfRetenidoV1 x:
                    // El GMF por pagar con la clase del producto que generó el hecho (§2), contra el capital de la cuenta debitada.
                    // Respuesta 97 (E253): el GMF del pago de un saldo no reclamado sale de los no reclamados por pagar, no del capital.
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                        x.Gmf.GeneratingEvent == Domain.Lending.Deposits.HechosGeneradoresDeGmf.PagoDeNoReclamado
                            ? NoReclamado(x.Dimensions, x.Account, x.HolderPersonPublicId)
                            : Capital(x.Dimensions, x.Account, x.HolderPersonPublicId),
                        "sin cuenta", ClaseGeneradoraDelGmf(x));
                    break;
                case InteresDeCaptacionCausadoV1 x:
                    Lineas(x.Lines, x.Totals, (d, cuenta, titular) => Resultado(R.GastoPorIntereses, d, cuenta, titular));
                    break;
                case AjusteDeCausacionDeCaptacionV1 x:
                    Lineas(x.Lines, x.Totals, (d, cuenta, titular) => Resultado(R.GastoPorIntereses, d, cuenta, titular));
                    break;
                case InteresDeCaptacionLiquidadoV1 x:
                    Lineas(x.Lines, x.Totals, (_, _, _) => null);
                    break;
                case RetencionPracticadaV1 x:
                    Lineas(x.Lines, x.Totals, (_, _, _) => null);
                    break;
                case CruceAlRetiroAplicadoV1 x:
                    // D45: el débito de cada cuenta que paga el crédito contra el puente OwnLoanPayoff, que el PagoAplicado de la misma unidad debita.
                    if (x.Parts.Count == 0)
                        Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                            Recaudo(x.Dimensions, x.Account, ClaseDelPuenteInterno, x.HolderPersonPublicId), "sin puente");
                    else
                        foreach (var parte in x.Parts)
                            Operacion(parte.Account, x.HolderPersonPublicId, parte.Dimensions, parte.Amounts,
                                Recaudo(parte.Dimensions, parte.Account, ClaseDelPuenteInterno, x.HolderPersonPublicId), "sin puente");
                    break;
                case PerdidaAplicadaAlRetiroV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts, Simple(R.PerdidaAbsorbida, x.Dimensions), "sin pérdida");
                    break;
                case DevolucionOrdenadaV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                        Devolucion(x.Dimensions, x.Account, x.OrderKind, x.PayeePersonPublicId), "sin tipo de orden");
                    break;
                case CargoDeCuentaCausadoV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                        Resultado(R.IngresoPorCargos, x.Dimensions, x.Account, x.HolderPersonPublicId, motivo: x.Charge.Kind), "sin clase de cargo");
                    break;
                case CargoPendienteCobradoV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts, null, "el cobro no cuadra");
                    break;
                case CargoDeCuentaCondonadoV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                        Resultado(R.IngresoPorCargos, x.Dimensions, x.Account, x.HolderPersonPublicId, motivo: x.Charge.Kind), "sin clase de cargo");
                    break;
                case CaptacionReclasificadaV1 x:
                    Reclasificacion(x);
                    break;
                case EmbargoConsignadoV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                        new Lado(R.EmbargosPorConsignar, Valores(x.Dimensions), null, null, Persona(x.Garnishment.CourtPersonPublicId), null, Numero(x.Account)),
                        "sin juzgado");
                    break;
                case SaldoTrasladadoANoReclamadosV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                        new Lado(R.NoReclamadosPorPagar, Valores(x.Dimensions), null, null, Persona(x.HolderPersonPublicId), null, Numero(x.Account)),
                        "sin no reclamados");
                    break;
                case PenalidadAplicadaV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                        Resultado(R.IngresoPorPenalidad, x.Dimensions, x.Account, x.HolderPersonPublicId), "sin ingreso");
                    break;
                case BonificacionReconocidaV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                        Resultado(R.GastoPorBonificacion, x.Dimensions, x.Account, x.HolderPersonPublicId), "sin gasto");
                    break;
                case CdatConstituidoV1 x:
                    // US6 (T174): fondeado desde otra cuenta del asociado, las dos puntas cuadran entre sí (como el traslado); por el recaudador, su puente.
                    if (x.Funding.SourceAccount is { } origen && x.Funding.SourceDimensions is { } dOrigen && x.Funding.SourceAmounts.Count > 0)
                        Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                            OtraCuenta(origen, x.Funding.SourceHolderPersonPublicId ?? x.HolderPersonPublicId, dOrigen, x.Funding.SourceAmounts),
                            "un CDAT fondeado desde otra cuenta que no cuadra con ella");
                    else
                        Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts,
                            x.Funding.Collector is { } cobro ? Recaudo(x.Dimensions, x.Account, cobro.CollectorClass, cobro.PayerPersonPublicId) : null,
                            "un CDAT fondeado desde otra cuenta sin sus dimensiones");
                    break;
                case InteresesDeCdatPagadosV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts, DestinoDelCdat(x.Dimensions, x.Account, x.Destination, "TermDepositInterest"),
                        "un pago a otra cuenta sin sus dimensiones");
                    break;
                case CdatProrrogadoV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts, null, "la prórroga no cuadra");
                    break;
                case CdatRenovadoV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts, DestinoDeLaRenovacion(x), "un cambio de monto sin su origen o destino");
                    break;
                case CdatRedimidoV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts, DestinoDelCdat(x.Dimensions, x.Account, x.Destination, "TermDepositRedemption"),
                        "una redención a otra cuenta sin sus dimensiones");
                    break;
                case CdatCanceladoV1 x:
                    Operacion(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts, DestinoDelCdat(x.Dimensions, x.Account, x.Destination, "TermDepositRedemption"),
                        "una cancelación a otra cuenta sin sus dimensiones");
                    break;
                case RevalorizacionDeAportesAplicadaV1 x:
                    Lineas(x.Lines, x.Totals, (d, _, _) => Simple(R.FondoDeRevalorizacion, d));
                    RetencionesDeAsamblea(x.Withholdings, R.FondoDeRevalorizacion, retenido: true);
                    break;
                case ExcedentesDistribuidosV1 x:
                    Lineas(x.Lines, x.Totals, (d, _, _) => Simple(R.FondoDeExcedentes, d));
                    // US12 (T203): el pago en dinero no mueve cuentas del asociado: del fondo de excedentes a los retornos por pagar (motivo SurplusPayment).
                    foreach (var pago in x.Payments.Where(p => p.Amount != 0m))
                    {
                        var par = nuevoPar();
                        Agregar(Simple(R.FondoDeExcedentes, pago.Dimensions), debitoSiPositivo: true, pago.Amount, par);
                        Agregar(Devolucion(pago.Dimensions, pago.Account, "SurplusPayment", pago.HolderPersonPublicId)!, debitoSiPositivo: false, pago.Amount, par);
                    }
                    RetencionesDeAsamblea(x.Withholdings, R.FondoDeExcedentes, retenido: true);
                    break;
                case AportesAmortizadosV1 x:
                    Lineas(x.Lines, x.Totals, (d, cuenta, titular) => Devolucion(d, cuenta, "ContributionRefund", titular));
                    break;
                case OperacionDeCuentaReversadaV1 x:
                    Reverso(x);
                    break;
                default:
                    return false;
            }
            return true;
        }

        // ---------------------------------------------------------------------------------------------- por hecho --

        private Lado? DestinoDelRetiro(RetiroDeCuentaAplicadoV1 x)
        {
            if (x.DisbursementOrderPublicId is not null) return Devolucion(x.Dimensions, x.Account, "DepositWithdrawal", _sobre.PersonPublicId);
            var clase = Texto(x.Collector?.CollectorClass) ?? (x.Internal is not null ? ClaseDelPuenteInterno : null);
            return Recaudo(x.Dimensions, x.Account, clase, x.Collector?.PayerPersonPublicId ?? _sobre.PersonPublicId);
        }

        private Lado? DestinoDelCdat(DepositFactDimensionsV1 d, MemberAccountRefV1 cuenta, TermDepositDestinationV1 destino, string motivo)
        {
            if (destino.OrderPublicId is not null || destino.Kind.Contains("Order", StringComparison.OrdinalIgnoreCase))
                return Devolucion(d, cuenta, motivo, _sobre.PersonPublicId);
            // US6 (T174; research E107): el abono en otra cuenta del asociado es su contrapartida, con sus dimensiones y su cambio de saldo.
            return destino.TargetAccount is { } destinoCuenta && destino.TargetDimensions is { } dDestino && destino.TargetAmounts.Count > 0
                ? OtraCuenta(destinoCuenta, destino.TargetHolderPersonPublicId ?? _sobre.PersonPublicId ?? Guid.Empty, dDestino, destino.TargetAmounts)
                : null;
        }

        /// <summary>
        /// US6 (T174): la otra cuenta del asociado como contrapartida: escribe sus saldos y devuelve un lado vacío que no recibe nada si las dos puntas
        /// cuadran; si no cuadran, nulo (el hecho no dice a dónde va lo que sobra).
        /// </summary>
        private Lado? OtraCuenta(MemberAccountRefV1 cuenta, Guid titular, DepositFactDimensionsV1 d, IReadOnlyList<DepositConceptAmountV1> importes)
        {
            _netoDeLaOtra = Saldos(cuenta, titular, d, importes);
            return CuentaQueCuadra;
        }

        /// <summary>El lado que representa «la otra cuenta ya escrita»: la contrapartida sólo la acepta si cuadra con ella.</summary>
        private static readonly Lado CuentaQueCuadra = new("·otra-cuenta·", new ValoresDeCartera(), null, null, null);

        private decimal _netoDeLaOtra;

        private Lado? DestinoDeLaRenovacion(CdatRenovadoV1 x)
        {
            if (x.AmountChange?.Funding?.Collector is { } cobro) return Recaudo(x.Dimensions, x.Account, cobro.CollectorClass, cobro.PayerPersonPublicId);
            if (x.AmountChange?.Funding is { SourceAccount: { } origen, SourceDimensions: { } dOrigen } f && f.SourceAmounts.Count > 0)
                return OtraCuenta(origen, f.SourceHolderPersonPublicId ?? x.HolderPersonPublicId, dOrigen, f.SourceAmounts);
            return x.AmountChange?.Destination is { } destino ? DestinoDelCdat(x.Dimensions, x.Account, destino, "TermDepositRedemption") : null;
        }

        /// <summary>
        /// El traslado de los saldos de la cuenta de una combinación a otra (§3, fila 17): el plazo de emisión, la inactividad (que cambia de rol), el
        /// embargo o la exención. Los roles de captación son de naturaleza crédito: débito a la combinación anterior y crédito a la nueva; las dos puntas
        /// forman un par y, si resuelven la misma cuenta, no dejan líneas (C21).
        /// </summary>
        private void Reclasificacion(CaptacionReclasificadaV1 x)
        {
            foreach (var a in x.Amounts)
            {
                var nueva = SaldoDeCuenta(x.Dimensions, a, x.Account, x.HolderPersonPublicId, null);
                var anterior = SaldoDeCuenta(x.PreviousDimensions, a, x.Account, x.HolderPersonPublicId, null);
                if (nueva is null || anterior is null) continue;
                var par = nuevoPar();
                var credito = R.Buscar(nueva.Rol)!.Naturaleza == AccountNature.Credit;
                Agregar(credito ? anterior : nueva, debitoSiPositivo: true, a.Amount, par);
                Agregar(credito ? nueva : anterior, debitoSiPositivo: false, a.Amount, par);
            }
        }

        /// <summary>
        /// El reverso de una operación de cuenta (§3, fila 31; la 013 §9): el espejo de cada cuenta del original —con las dimensiones vigentes que trae el
        /// reverso— y, si no cuadra entre las cuentas, la contrapartida del original con los datos que el reverso nombra (recaudador, orden, cargo).
        /// </summary>
        private void Reverso(OperacionDeCuentaReversadaV1 x)
        {
            var original = Texto(x.Original?.Type) ?? Texto(x.Mirrors.FirstOrDefault()?.Type) ?? string.Empty;
            // US12 (T203): el reverso entero de una decisión de asamblea trae un espejo por cuenta; cada uno vuelve a su fondo (o a la devolución por
            // pagar de la amortización) con las dimensiones de su cuenta: la sucursal del fondo es la de cada asociado, como en la ida.
            if (original is RevalorizacionDeAportesAplicadaV1.Type or ExcedentesDistribuidosV1.Type or AportesAmortizadosV1.Type && x.Mirrors.Count > 0)
            {
                foreach (var m in x.Mirrors)
                {
                    var dm = m.Dimensions ?? x.Dimensions;
                    var cuentaM = m.Account ?? x.Account;
                    var titularM = m.HolderPersonPublicId ?? x.HolderPersonPublicId;
                    Operacion(cuentaM, titularM, dm, m.Amounts, ContraDeAsamblea(original, dm, cuentaM, titularM, x.OrderKind), $"el reverso de {original} sin su fondo");
                }
                // E260 (respuesta 89 B): lo que la decisión retuvo vuelve de la retención por pagar al fondo.
                if (x.Withholdings.Count > 0)
                    RetencionesDeAsamblea(x.Withholdings, original == RevalorizacionDeAportesAplicadaV1.Type ? R.FondoDeRevalorizacion : R.FondoDeExcedentes, retenido: false);
                return;
            }
            var neto = 0m;
            if (x.Mirrors.Count == 0) neto += Saldos(x.Account, x.HolderPersonPublicId, x.Dimensions, x.Amounts);
            foreach (var m in x.Mirrors)
                neto += Saldos(m.Account ?? x.Account, m.HolderPersonPublicId ?? x.HolderPersonPublicId, m.Dimensions ?? x.Dimensions, m.Amounts);
            if (neto == 0m) return;

            var d = x.Dimensions;
            Lado? contra = original switch
            {
                ConsignacionEnCuentaAplicadaV1.Type or AporteRecibidoV1.Type => Recaudo(d, x.Account, x.CollectorClass, _sobre.PersonPublicId),
                RetiroDeCuentaAplicadoV1.Type => Texto(x.OrderKind) is { } orden
                    ? Devolucion(d, x.Account, orden, _sobre.PersonPublicId)
                    : Recaudo(d, x.Account, x.CollectorClass, _sobre.PersonPublicId),
                CargoDeCuentaCausadoV1.Type or CargoDeCuentaCondonadoV1.Type =>
                    Resultado(R.IngresoPorCargos, d, x.Account, x.HolderPersonPublicId, motivo: x.ReasonCode),
                CruceAlRetiroAplicadoV1.Type => Recaudo(d, x.Account, x.CollectorClass ?? ClaseDelPuenteInterno, x.HolderPersonPublicId),
                PerdidaAplicadaAlRetiroV1.Type => Simple(R.PerdidaAbsorbida, d),
                DevolucionOrdenadaV1.Type => Devolucion(d, x.Account, x.OrderKind, _sobre.PersonPublicId),
                // US12 (T203): la amortización cuyo giro rechazó Tesorería vuelve de la devolución por pagar a los aportes.
                AportesAmortizadosV1.Type => Devolucion(d, x.Account, Texto(x.OrderKind) ?? "ContributionRefund", x.HolderPersonPublicId),
                // US11 (T189): el giro rechazado de una consignación de embargo vuelve a los embargos por consignar, con el juzgado de tercero.
                EmbargoConsignadoV1.Type => new Lado(R.EmbargosPorConsignar, Valores(d), null, null,
                    Persona(x.CounterpartPersonPublicId ?? _sobre.PersonPublicId ?? Guid.Empty), null, Numero(x.Account)),
                _ => null,
            };
            Contrapartida(contra, neto, $"el reverso de {original} sin su contrapartida");
        }

        /// <summary>
        /// E260 (respuesta 89 B): la retención en la fuente de cada asociado sobre lo que reparte la asamblea. Las líneas y los pagos del hecho llevan lo neto, así que
        /// el fondo debe además el valor retenido, que queda por pagar a la administración tributaria: del fondo a la retención de la asamblea (la ida) o a la
        /// inversa (el reverso entero de la decisión).
        /// </summary>
        private void RetencionesDeAsamblea(IReadOnlyList<AssemblyWithholdingV1> retenciones, string fondo, bool retenido)
        {
            foreach (var r in retenciones.Where(r => r.Amount != 0m))
            {
                var par = nuevoPar();
                Agregar(Simple(fondo, r.Dimensions), debitoSiPositivo: retenido, r.Amount, par);
                Agregar(Simple(R.RetencionDeAsambleaPorPagar, r.Dimensions), debitoSiPositivo: !retenido, r.Amount, par);
            }
        }

        /// <summary>US12 (T203): la contrapartida de lo de la asamblea: el fondo de revalorización o de excedentes, o la devolución por pagar de la amortización.</summary>
        private Lado? ContraDeAsamblea(string original, DepositFactDimensionsV1 d, MemberAccountRefV1 cuenta, Guid titular, string? orden) => original switch
        {
            RevalorizacionDeAportesAplicadaV1.Type => Simple(R.FondoDeRevalorizacion, d),
            ExcedentesDistribuidosV1.Type => Simple(R.FondoDeExcedentes, d),
            _ => Devolucion(d, cuenta, Texto(orden) ?? "ContributionRefund", titular),
        };

        // ------------------------------------------------------------------------------------------------ comunes --

        /// <summary>Los saldos de una cuenta y, si no cuadran solos, su contrapartida.</summary>
        private void Operacion(MemberAccountRefV1? cuenta, Guid titular, DepositFactDimensionsV1 d, IReadOnlyList<DepositConceptAmountV1> importes, Lado? contra,
            string faltante, string? claseDelGmf = null) =>
            Contrapartida(contra, Saldos(cuenta, titular, d, importes, claseDelGmf), faltante);

        /// <summary>Las líneas por cuenta de un hecho masivo (o, sin lista por cuenta, sus totales por combinación), cada una con su contrapartida.</summary>
        private void Lineas(IReadOnlyList<DepositFactLineV1> lineas, IReadOnlyList<DepositFactTotalV1> totales,
            Func<DepositFactDimensionsV1, MemberAccountRefV1?, Guid?, Lado?> contra)
        {
            if (lineas.Count > 0)
            {
                foreach (var l in lineas)
                    Operacion(l.Account, l.HolderPersonPublicId, l.Dimensions, l.Amounts, contra(l.Dimensions, l.Account, l.HolderPersonPublicId), "sin contrapartida");
                return;
            }
            foreach (var t in totales)
                Operacion(null, Guid.Empty, t.Dimensions, t.Amounts, contra(t.Dimensions, null, null), "sin contrapartida");
        }

        /// <summary>
        /// Cada concepto de la cuenta a su rol por su lado natural; devuelve el neto con signo de débito (lo que la contrapartida tiene que llevar al
        /// crédito para cuadrar).
        /// </summary>
        private decimal Saldos(MemberAccountRefV1? cuenta, Guid titular, DepositFactDimensionsV1 d, IReadOnlyList<DepositConceptAmountV1> importes,
            string? claseDelGmf = null)
        {
            var neto = 0m;
            foreach (var a in importes)
            {
                if (a.Amount == 0m) continue;
                var lado = SaldoDeCuenta(d, a, cuenta, titular, claseDelGmf);
                if (lado is null) continue;
                var debito = R.Buscar(lado.Rol)!.Naturaleza == AccountNature.Debit;
                Agregar(lado, debito, a.Amount, null);
                neto += debito ? a.Amount : -a.Amount;
            }
            return neto;
        }

        private void Contrapartida(Lado? contra, decimal neto, string faltante)
        {
            if (ReferenceEquals(contra, CuentaQueCuadra))
            {
                // La otra cuenta ya se escribió: lo que queda es la suma de las dos puntas; si no es cero, el hecho no cuadra.
                var resto = neto + _netoDeLaOtra;
                _netoDeLaOtra = 0m;
                if (resto != 0m)
                    fallos.Add(new FalloDeCartera(AccountingErrors.LendingFactCounterpartUnknown(hecho.Tipo, faltante), hecho.Tipo, null, null, null, []));
                return;
            }
            if (neto == 0m) return;
            if (contra is null)
            {
                fallos.Add(new FalloDeCartera(AccountingErrors.LendingFactCounterpartUnknown(hecho.Tipo, faltante), hecho.Tipo, null, null, null, []));
                return;
            }
            Agregar(contra, debitoSiPositivo: true, -neto, null);
        }

        /// <summary>El rol de saldo del concepto de la cuenta (contabilidad.md §2, «de concepto a rol»); nulo si el concepto no es del libro de cuentas.</summary>
        private Lado? SaldoDeCuenta(DepositFactDimensionsV1 d, DepositConceptAmountV1 a, MemberAccountRefV1? cuenta, Guid titular, string? claseDelGmf)
        {
            var rol = R.RolDelConceptoDeCuenta(a.Concept, d.Inactive);
            if (rol is null)
            {
                fallos.Add(new FalloDeCartera(AccountingErrors.LendingFactUnbalanced(a.Amount), hecho.Tipo, null,
                    new Dictionary<string, string> { ["conceptCode"] = a.Concept }, null, Numero(cuenta) is { } n ? [n] : []));
                return null;
            }
            var claseDeAporte = rol == R.AportesSociales ? Texto(a.ContributionClass) ?? R.ClaseDeAporteDelConcepto(a.Concept) : null;
            var claseDeCaptacion = rol == R.GmfPorPagar ? claseDelGmf ?? Texto(d.DepositClass) : Texto(d.DepositClass);
            return new Lado(rol, Valores(d, claseDeAporte: claseDeAporte, claseDeCaptacion: claseDeCaptacion), a.Concept, null, Persona(titular), null,
                Numero(cuenta));
        }

        private Lado Capital(DepositFactDimensionsV1 d, MemberAccountRefV1 cuenta, Guid titular) =>
            new(R.RolDelConceptoDeCuenta(nameof(Domain.Enums.Lending.MemberAccountConcept.Principal), d.Inactive)!, Valores(d),
                nameof(Domain.Enums.Lending.MemberAccountConcept.Principal), null, Persona(titular), null, Numero(cuenta));

        /// <summary>Los no reclamados por pagar de la cuenta (E253): de donde sale el GMF del pago de un saldo no reclamado.</summary>
        private Lado NoReclamado(DepositFactDimensionsV1 d, MemberAccountRefV1 cuenta, Guid titular) =>
            new(R.RolDelConceptoDeCuenta(nameof(Domain.Enums.Lending.MemberAccountConcept.Unclaimed), d.Inactive)!, Valores(d),
                nameof(Domain.Enums.Lending.MemberAccountConcept.Unclaimed), null, Persona(titular), null, Numero(cuenta));

        /// <summary>El puente del recaudador (exige su clase); nulo sin clase: el hecho no dice a dónde fue el dinero.</summary>
        private Lado? Recaudo(DepositFactDimensionsV1 d, MemberAccountRefV1? cuenta, string? clase, Guid? pagador) =>
            Texto(clase) is { } c ? new Lado(R.RecaudoPorAplicar, Valores(d, clase: c), null, null, Persona(pagador), null, Numero(cuenta)) : null;

        /// <summary>El puente de devoluciones con Tesorería (exige la naturaleza y el tipo de orden); nulo sin tipo de orden.</summary>
        private Lado? Devolucion(DepositFactDimensionsV1 d, MemberAccountRefV1? cuenta, string? motivo, Guid? beneficiario) =>
            Texto(motivo) is { } m ? new Lado(R.DevolucionPorPagar, Valores(d, motivo: m), null, null, Persona(beneficiario), null, Numero(cuenta)) : null;

        private Lado Resultado(string rol, DepositFactDimensionsV1 d, MemberAccountRefV1? cuenta, Guid? titular, string? motivo = null) =>
            new(rol, Valores(d, motivo: Texto(motivo)), null, null, Persona(titular), null, Numero(cuenta));

        private Lado Simple(string rol, DepositFactDimensionsV1 d) => new(rol, Valores(d), null, null, null);

        private ValoresDeCartera Valores(DepositFactDimensionsV1 d, string? clase = null, string? motivo = null, string? claseDeAporte = null,
            string? claseDeCaptacion = null) => new(
            CreditLineCode: Texto(d.CreditLineCode), IsEmployee: d.IsEmployee, CollectorClass: clase, ReasonCode: motivo,
            BranchPublicId: d.BranchPublicId != Guid.Empty ? d.BranchPublicId : _sobre.BranchPublicId, LineNature: Texto(d.LineNature),
            DepositClass: claseDeCaptacion ?? Texto(d.DepositClass), IssueTermBand: Texto(d.IssueTermBand), ContributionClass: claseDeAporte,
            AccountFlag: Texto(d.AccountFlag));

        private static string? ClaseGeneradoraDelGmf(GmfRetenidoV1 x) =>
            Texto(x.Gmf.GeneratingDepositClass) is { } clase && R.ClasesDeCaptacion.Contains(clase, StringComparer.Ordinal) ? clase : Texto(x.Dimensions.DepositClass);

        private static Guid? Persona(Guid? persona) => persona is { } p && p != Guid.Empty ? p : null;

        private static string? Numero(MemberAccountRefV1? cuenta) => Texto(cuenta?.AccountNumber);

        private void Agregar(Lado lado, bool debitoSiPositivo, decimal importe, int? par)
        {
            if (importe == 0m) return;
            destino.Add(new Partida(hecho.Tipo, lado.Rol, debitoSiPositivo, importe, lado.Valores, _sobre.Origin.OperationDate, lado.Concepto, null,
                lado.Tercero, _sobre.PersonPublicId, null, par, lado.Cuenta));
        }
    }
}
