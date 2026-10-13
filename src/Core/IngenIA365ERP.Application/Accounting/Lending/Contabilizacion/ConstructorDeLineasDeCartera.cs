using System.Text.Json;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;

/// <summary>
/// Un hecho de Cartera tal como lo ve el constructor: el sobre, el contenido tipado <c>…V1</c> y la clase de origen como texto (T10). Sale
/// de lo que entrega la bandeja (<see cref="MensajeEntrante"/>) o de lo que pregunta la validación previa (<see cref="MensajeContableDto"/>):
/// lo evaluado y lo contabilizado se arman con el mismo código. (nuevo)
/// </summary>
public sealed record HechoDeCartera(IntegrationEnvelopeV1 Sobre, object? Contenido, string? ClaseDeOrigen)
{
    public string Tipo => Sobre.Type;

    public static HechoDeCartera De(MensajeEntrante m) => new(m.Envelope, m.Payload, m.OriginDocumentClassText);

    public static HechoDeCartera De(MensajeContableDto m) =>
        new(m.Envelope, ConstructorDeLineasDeCartera.ContenidoTipado(m.Envelope.Type, m.Payload), null);
}

/// <summary>
/// De qué salió cada línea del <see cref="PostingRequest"/> (el mapa de §7.3): los hechos, el rol, el concepto (nulo si la línea suma
/// conceptos distintos) y los créditos que suman ahí, con la cuenta. <see cref="ConservaDetalle"/> dice si la cuenta exige tercero, cruce o
/// base: en un resumido esas líneas no se suman (§10.4). En paralelo con <c>Request.Lines</c>. (nuevo)
/// </summary>
public sealed record LineaDeCartera(
    IReadOnlyList<string> Hechos,
    string Rol,
    string? Concepto,
    IReadOnlyList<string> Creditos,
    string AccountCode,
    string AccountName,
    bool ConservaDetalle);

/// <summary>
/// Un error al armar el comprobante de una unidad (matriz, tipo de comprobante o contenido): el error y de dónde salió —hecho, rol,
/// combinación y créditos—, para que la bandeja y la validación previa digan qué corregir (§13.3). (nuevo)
/// </summary>
public sealed record FalloDeCartera(
    Error Error,
    string FactType,
    string? Role,
    IReadOnlyDictionary<string, string>? Combination,
    string? AccountCode,
    IReadOnlyList<string> Loans);

/// <summary>
/// El comprobante que produce una unidad de Cartera (§7.1). Sin fallos y con líneas, <see cref="Request"/> es lo que se entrega a
/// <c>AccountingPoster</c>; sin líneas, <see cref="SinComprobante"/> dice por qué: <c>ZeroValue</c> (todo en cero) o <c>SameAccounts</c> (las
/// dos puntas de cada traslado resolvieron la misma cuenta, sucursal, tercero y cruce; C21). (nuevo)
/// </summary>
public sealed record ConstruccionDeCartera(
    PostingRequest? Request,
    TipoDeLaUnidadDeCartera? Tipo,
    IReadOnlyList<LineaDeCartera> Mapa,
    IReadOnlyList<FalloDeCartera> Fallos,
    DateOnly Fecha,
    HechoDeCartera Principal,
    string? SinComprobante = null)
{
    public bool EsValida => Fallos.Count == 0 && (Request is not null || SinComprobante is not null);

    public bool SinLineas => EsValida && SinComprobante is not null;
}

/// <summary>
/// Lo que el constructor necesita ya cargado (§10.5): la matriz vigente en el rango de fechas contables de los hechos, los mapeos de tipo de
/// comprobante y las personas de los hechos (<c>PublicId → Id</c>, también las dadas de baja: el contrato de la 009 las rechaza con su código).
/// Se carga una vez por unidad, por grupo o por lote (<see cref="CargarAsync"/>) y el constructor no hace IO. (nuevo)
/// </summary>
public sealed class CatalogosDeCartera(MatrizVigenteDeCartera matriz, MapeosDeCartera mapeos, IReadOnlyDictionary<Guid, int> personas)
{
    public MatrizVigenteDeCartera Matriz { get; } = matriz;

    public MapeosDeCartera Mapeos { get; } = mapeos;

    public IReadOnlyDictionary<Guid, int> Personas { get; } = personas;

    public static async Task<CatalogosDeCartera> CargarAsync(
        IApplicationDbContext db, ResolutorDeReglasDeCartera resolutor, TiposDeComprobanteDeCartera tipos,
        IEnumerable<IReadOnlyList<HechoDeCartera>> unidades, DateOnly hoy, CancellationToken ct)
    {
        var lista = unidades.ToList();
        var fechas = lista.SelectMany(u => u).Select(h => h.Sobre.Origin.OperationDate).ToList();
        var desde = fechas.Count == 0 ? hoy : fechas.Min();
        var hasta = fechas.Count == 0 ? hoy : fechas.Max();
        var matriz = await resolutor.CargarAsync(desde, hasta, ct);
        var mapeos = await tipos.CargarAsync(ct);

        var guids = lista.SelectMany(u => ConstructorDeLineasDeCartera.Desglosar(u).Partidas)
            .SelectMany(p => new[] { p.Tercero, p.TerceroDelSobre })
            .Where(g => g is not null).Select(g => g!.Value).Distinct().ToList();
        var personas = guids.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => guids.Contains(p.PublicId))
                .ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
        return new CatalogosDeCartera(matriz, mapeos, personas);
    }
}

/// <summary>
/// De una unidad de hechos de Cartera a un <see cref="PostingRequest"/> (feature 013, T0646; contracts/contabilidad.md §2, §5, §7.3, §9,
/// §11, §19 y §24). Lo usan el consumidor (<c>PostLendingFactsCommand</c>, el resumido), la validación previa
/// (<c>EvaluateLendingPostingQuery</c>), la vista previa de un lote y la conciliación: lo evaluado y lo contabilizado salen del mismo código.
/// <b>Sin IO</b>: recibe la matriz, los mapeos y las personas ya cargados (<see cref="CatalogosDeCartera"/>).
///
/// <para>
/// Dos pasos. <see cref="Desglosar"/> convierte cada contenido en <b>partidas</b> —un rol, un lado para el importe positivo, un importe con
/// su signo, los valores que busca en la matriz, el tercero natural del rol y el crédito— según la tabla de §2.1 y la de concepto → rol de
/// §2.2. <see cref="Construir"/> resuelve cada partida en la matriz vigente en la fecha contable del hecho (también en los reversos, §9),
/// arma las líneas (un negativo invierte el lado; los ceros se descartan; tercero, cruce y centro sólo donde la cuenta los exige; sucursal de
/// cada punta) y suma las iguales. Cada hecho tiene que cuadrar solo y con dos decimales (<c>Accounting.LendingFact.Unbalanced</c>, defecto
/// del emisor).
/// </para>
///
/// <para>
/// Interpretaciones que el contrato deja abiertas y este código fija (las prueban los casos dorados de <c>Casos/</c>): en los traslados el saldo
/// del deterioro individual viaja con <c>reasonCode = Individual</c>; la disminución del deterioro viaja positiva con su movimiento en
/// <c>reasonCode</c>, y un <c>Transfer</c> es una sola partida con signo sobre el deterioro que se mueve (positivo = el que sube, al crédito;
/// negativo = el que baja, al débito) que cuadra con los demás <c>Transfer</c> del hecho; en el <c>SaldoAFavorReconocido</c> de una corrección
/// de valores nacionales el concepto del ingreso viaja en <c>reasonCode</c>; la capitalización de una novedad (<c>reasonCode =
/// Capitalizacion</c>) lleva lo causado a capital contra su rol de saldo y lo suspendido contra el ingreso diferido, cancelando su orden.
/// </para>
/// (nuevo)
/// </summary>
public static class ConstructorDeLineasDeCartera
{
    /// <summary>El tipo de documento cruce de la factura de Comercial (§5, regla 2; C79): los roles de Comercial cruzan contra ella.</summary>
    public const string CruceDeFactura = "FV";

    /// <summary>El <c>reasonCode</c> de la capitalización de una novedad (mensajes.md §10.20).</summary>
    public const string Capitalizacion = "Capitalizacion";

    private static readonly JsonSerializerOptions Json = OpcionesDeMensajes.Opciones;

    /// <summary>Una partida: un importe con signo sobre un rol, con los valores que busca en la matriz y lo que la línea lleva si su cuenta lo exige.</summary>
    public sealed record Partida(
        string Hecho,
        string Rol,
        bool DebitoSiPositivo,
        decimal Importe,
        ValoresDeCartera Valores,
        DateOnly Fecha,
        string? Concepto,
        string? Credito,
        Guid? Tercero,
        Guid? TerceroDelSobre,
        string? Factura,
        int? Par);

    /// <summary>El resultado del primer paso: las partidas, los fallos del contenido y el hecho principal.</summary>
    public sealed record Desglose(IReadOnlyList<Partida> Partidas, IReadOnlyList<FalloDeCartera> Fallos, HechoDeCartera? Principal);

    // ================================================================================================== utilidades --

    /// <summary>El hecho principal de la unidad (C01): el que tiene a los demás entre sus acompañantes.</summary>
    public static HechoDeCartera? Principal(IReadOnlyList<HechoDeCartera> unidad)
    {
        if (unidad.Count == 0) return null;
        var tipo = OperacionesDeCartera.Principal(unidad.Select(h => h.Tipo).ToList());
        return unidad.FirstOrDefault(h => h.Tipo == tipo) ?? unidad[0];
    }

    /// <summary>¿La unidad es informativa? (sólo recibo, sin comprobante; §7.4).</summary>
    public static bool EsInformativa(IReadOnlyList<HechoDeCartera> unidad) =>
        unidad.Count > 0 && unidad.All(h => h.Sobre.Kind == IntegrationMessageKind.Informational
                                            || OperacionesDeCartera.Buscar(h.Tipo) is { EsInformativo: true });

    /// <summary>¿El tipo es un hecho de Cartera que va a Contabilidad?</summary>
    public static bool VaAContabilidad(string tipo) => OperacionesDeCartera.Buscar(tipo) is not null;

    /// <summary>Un contenido con su record del catálogo de Cartera; los que llegan como JSON se leen con las opciones de los mensajes.</summary>
    public static object? ContenidoTipado(string tipo, object? contenido)
    {
        if (contenido is null) return null;
        var registro = CatalogoDeMensajesDeCarteraV1.HechosAContabilidad.FirstOrDefault(t => t.Type == tipo);
        if (registro is null || registro.Record.IsInstanceOfType(contenido)) return contenido;
        try
        {
            return contenido switch
            {
                JsonElement e => e.Deserialize(registro.Record, Json),
                System.Text.Json.Nodes.JsonNode n => n.Deserialize(registro.Record, Json),
                string s => JsonSerializer.Deserialize(s, registro.Record, Json),
                _ => contenido,
            };
        }
        catch (JsonException)
        {
            return contenido;
        }
    }

    /// <summary>La clase de recaudador de la unidad: la del recaudador del pago o, si no hay, la de las dimensiones del principal (mapeo <c>DN</c>, C79).</summary>
    public static string? ClaseDeRecaudador(IReadOnlyList<HechoDeCartera> unidad)
    {
        foreach (var h in unidad)
        {
            var clase = h.Contenido switch
            {
                PagoAplicadoV1 p => p.Collector.CollectorClass,
                PagoReversadoV1 p => p.Collector.CollectorClass,
                SaldoAFavorResueltoV1 p => p.Collector.CollectorClass,
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(clase)) return clase;
        }
        return unidad.Select(h => Dimensiones(h.Contenido)?.CollectorClass).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
    }

    /// <summary>El crédito del hecho (<c>LoanRefV1</c>), si es de un solo crédito.</summary>
    public static LoanRefV1? Credito(object? contenido) => contenido switch
    {
        CreditoDesembolsadoV1 x => x.Loan,
        DesembolsoReversadoV1 x => x.Loan,
        CreditoAjustadoDesdeComercialV1 x => x.Loan,
        PagoAplicadoV1 x => x.Loan,
        PagoReversadoV1 x => x.Loan,
        AnticipoRecibidoV1 x => x.Loan,
        AnticipoAplicadoV1 x => x.Loan,
        CobroDeInteresEnSuspensoV1 x => x.Loan,
        SaldoAFavorReconocidoV1 x => x.Loan,
        SaldoAFavorResueltoV1 x => x.Loan,
        RecuperacionDeCastigadaV1 x => x.Loan,
        DimensionContableCambiadaV1 x => x.Loan,
        CobroProvisionalIncorporadoV1 x => x.Loan,
        GastoDeCobranzaCausadoV1 x => x.Loan,
        CreditoReestructuradoV1 x => x.Loan,
        CreditoModificadoV1 x => x.Loan,
        CreditoNovadoV1 x => x.Loan,
        CondonacionAprobadaV1 x => x.Loan,
        CreditoCastigadoV1 x => x.Loan,
        CreditoAprobadoSinDesembolsarV1 x => x.Loan,
        CupoRotativoOtorgadoV1 x => x.Loan,
        GarantiaConstituidaV1 x => x.Loan,
        CausacionSuspendidaV1 x => x.Loan,
        CausacionReanudadaV1 x => x.Loan,
        _ => null,
    };

    /// <summary>El hecho original del que depende (<c>RelatedMessagePublicId</c> del recibo, §4.3), si el contenido lo nombra.</summary>
    public static Guid? MensajeOriginal(object? contenido) => contenido switch
    {
        DesembolsoReversadoV1 x => x.Original?.MessageId,
        CreditoAjustadoDesdeComercialV1 x => x.Original?.MessageId,
        PagoReversadoV1 x => x.Original?.MessageId,
        ReversoDeCierreV1 x => x.ReversedPart.MessageId,
        _ => null,
    } is { } g && g != Guid.Empty ? g : null;

    /// <summary>Los créditos de las partes (la lista por crédito, T14).</summary>
    public static IReadOnlyList<LoanFactLineV1> LineasPorCredito(object? contenido) => contenido switch
    {
        InteresCausadoV1 x => x.Lines,
        MoraCausadaV1 x => x.Lines,
        SeguroCausadoV1 x => x.Lines,
        AjusteDeCausacionPorFechaValorV1 x => x.Lines,
        DeterioroCalculadoV1 x => x.Lines,
        CarteraReclasificadaPorCalificacionV1 x => x.Lines,
        ReversoDeCierreV1 x => x.Lines,
        _ => [],
    };

    private static LoanFactDimensionsV1? Dimensiones(object? contenido) => contenido switch
    {
        CreditoDesembolsadoV1 x => x.Dimensions,
        DesembolsoReversadoV1 x => x.Dimensions,
        CreditoAjustadoDesdeComercialV1 x => x.Dimensions,
        PagoAplicadoV1 x => x.Dimensions,
        PagoReversadoV1 x => x.Dimensions,
        AnticipoRecibidoV1 x => x.Dimensions,
        AnticipoAplicadoV1 x => x.Dimensions,
        CobroDeInteresEnSuspensoV1 x => x.Dimensions,
        SaldoAFavorReconocidoV1 x => x.Dimensions,
        SaldoAFavorResueltoV1 x => x.Dimensions,
        RecuperacionDeCastigadaV1 x => x.Dimensions,
        DimensionContableCambiadaV1 x => x.Dimensions,
        CobroProvisionalIncorporadoV1 x => x.Dimensions,
        GastoDeCobranzaCausadoV1 x => x.Dimensions,
        CreditoReestructuradoV1 x => x.Dimensions,
        CreditoModificadoV1 x => x.Dimensions,
        CreditoNovadoV1 x => x.Dimensions,
        CondonacionAprobadaV1 x => x.Dimensions,
        CreditoCastigadoV1 x => x.Dimensions,
        CreditoAprobadoSinDesembolsarV1 x => x.Dimensions,
        CupoRotativoOtorgadoV1 x => x.Dimensions,
        GarantiaConstituidaV1 x => x.Dimensions,
        _ => null,
    };

    // ===================================================================================================== paso 1 --

    /// <summary>Las partidas de la unidad (§2.1, §2.2), los fallos del contenido (que no cuadra o un concepto sin rol) y el principal.</summary>
    public static Desglose Desglosar(IReadOnlyList<HechoDeCartera> unidad)
    {
        ArgumentNullException.ThrowIfNull(unidad);
        var principal = Principal(unidad);
        var partidas = new List<Partida>();
        var fallos = new List<FalloDeCartera>();
        if (principal is null) return new Desglose(partidas, fallos, null);

        var contexto = new ContextoDeUnidad(
            ClaseDeRecaudador(unidad),
            unidad.Select(h => h.Contenido switch
            {
                PagoAplicadoV1 p => (Guid?)p.Collector.PayerPersonPublicId,
                PagoReversadoV1 p => p.Collector.PayerPersonPublicId,
                SaldoAFavorResueltoV1 p => p.Collector.PayerPersonPublicId,
                _ => null,
            }).FirstOrDefault(g => g is { } x && x != Guid.Empty),
            unidad.Select(h => h.Contenido as CreditoAjustadoDesdeComercialV1).FirstOrDefault(c => c is not null));

        var par = 0;
        foreach (var hecho in unidad)
        {
            if (OperacionesDeCartera.Buscar(hecho.Tipo) is not { EsInformativo: false }) continue;
            var propias = new List<Partida>();
            var d = new Desglosador(hecho, contexto, propias, fallos, () => ++par);
            d.Desglosar();

            // Cada hecho cuadra solo y con dos decimales (§2.1, §7.1 paso 7).
            var diferencia = propias.Sum(p => p.DebitoSiPositivo ? p.Importe : -p.Importe);
            var conMasDecimales = propias.FirstOrDefault(p => decimal.Round(p.Importe, 2) != p.Importe);
            if (diferencia != 0m || conMasDecimales is not null)
                fallos.Add(new FalloDeCartera(AccountingErrors.LendingFactUnbalanced(conMasDecimales?.Importe ?? diferencia), hecho.Tipo, null, null, null,
                    propias.Select(p => p.Credito).Where(c => c is not null).Select(c => c!).Distinct().ToList()));
            partidas.AddRange(propias);
        }
        return new Desglose(partidas, fallos, principal);
    }

    private sealed record ContextoDeUnidad(string? ClaseDeRecaudador, Guid? Pagador, CreditoAjustadoDesdeComercialV1? AjusteDeComercial);

    /// <summary>Un lado de un asiento: el rol, los valores que busca, el concepto del importe, el crédito, el tercero natural y la factura.</summary>
    private sealed record Lado(string Rol, ValoresDeCartera Valores, string? Concepto, string? Credito, Guid? Tercero, string? Factura = null);

    private sealed class Desglosador(HechoDeCartera hecho, ContextoDeUnidad u, List<Partida> destino, List<FalloDeCartera> fallos, Func<int> nuevoPar)
    {
        private readonly IntegrationEnvelopeV1 _sobre = hecho.Sobre;
        private bool _invertirTodo;

        public void Desglosar()
        {
            switch (hecho.Contenido)
            {
                case CreditoDesembolsadoV1 x: Desembolso(x); break;
                case DesembolsoReversadoV1 x: DesembolsoReversado(x); break;
                case CreditoAjustadoDesdeComercialV1 x: AjusteDeComercial(x); break;
                case InteresCausadoV1 x: Causaciones(x.Lines); break;
                case MoraCausadaV1 x: Causaciones(x.Lines); break;
                case SeguroCausadoV1 x: Causaciones(x.Lines); break;
                case AjusteDeCausacionPorFechaValorV1 x: Causaciones(x.Lines); break;
                case PagoAplicadoV1 x: Pago(x); break;
                case PagoReversadoV1 x: PagoReversado(x); break;
                case AnticipoRecibidoV1 x: AnticipoRecibido(x); break;
                case AnticipoAplicadoV1 x: AnticipoAplicado(x); break;
                case CobroDeInteresEnSuspensoV1 x: CobroDeSuspenso(x.Dimensions, x.Amounts, x.Loan, x.DebtorPersonPublicId); break;
                case SaldoAFavorReconocidoV1 x: SaldoAFavorReconocido(x); break;
                case SaldoAFavorResueltoV1 x: SaldoAFavorResuelto(x); break;
                case RecuperacionDeCastigadaV1 x: Recuperacion(x.Dimensions, x.Amounts, x.Loan, x.DebtorPersonPublicId); break;
                case DimensionContableCambiadaV1 x:
                    foreach (var a in x.Amounts) Traslado(x.PreviousDimensions, x.Dimensions, a, x.Loan, x.Loan, x.DebtorPersonPublicId);
                    break;
                case CreditoModificadoV1 x: Novedad(x.PreviousDimensions, x.Dimensions, x.Amounts, x.Loan, x.NewLoan ?? x.Loan, x.DebtorPersonPublicId); break;
                case CreditoReestructuradoV1 x: Novedad(x.PreviousDimensions, x.Dimensions, x.Amounts, x.Loan, x.NewLoan ?? x.Loan, x.DebtorPersonPublicId); break;
                case CreditoNovadoV1 x: Novedad(x.PreviousDimensions, x.Dimensions, x.Amounts, x.Loan, x.NewLoan ?? x.Loan, x.DebtorPersonPublicId); break;
                case CobroProvisionalIncorporadoV1 x: CobroProvisional(x); break;
                case DeterioroCalculadoV1 x: Deterioro(x.Lines, x.GeneralImpairment); break;
                case CarteraReclasificadaPorCalificacionV1 x: Reclasificacion(x.Lines); break;
                case ReversoDeCierreV1 x: ReversoDeCierre(x); break;
                case GastoDeCobranzaCausadoV1 x: GastoDeCobranza(x); break;
                case CondonacionAprobadaV1 x:
                    BajaContraDeterioro(x.Dimensions, x.Amounts, x.ImpairmentApplied, "Waiver", x.Loan, x.DebtorPersonPublicId, castigo: false);
                    break;
                case CreditoCastigadoV1 x: Castigo(x); break;
                case CreditoAprobadoSinDesembolsarV1 x: Contingente(x.Dimensions, x.CreditLineCode, x.Amounts, x.Loan, x.DebtorPersonPublicId); break;
                case CupoRotativoOtorgadoV1 x: Contingente(x.Dimensions, x.CreditLineCode, x.Amounts, x.Loan, x.DebtorPersonPublicId); break;
                case GarantiaConstituidaV1 x: Contingente(x.Dimensions, x.CreditLineCode, x.Amounts, x.Loan, x.DebtorPersonPublicId); break;
                default:
                    fallos.Add(new FalloDeCartera(ErroresDeIntegracion.VersionNoAceptada(hecho.Tipo, _sobre.Version), hecho.Tipo, null, null, null, []));
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------- por hecho --

        private void Desembolso(CreditoDesembolsadoV1 x)
        {
            foreach (var a in x.Amounts)
            {
                var saldo = Saldo(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId);
                if (saldo is null) continue;
                Asentar(saldo, ContraDelDesembolso(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId, x.InKind, x.DisbursementForm, x.PayeePersonPublicId), a.Amount);
            }
        }

        private void DesembolsoReversado(DesembolsoReversadoV1 x)
        {
            foreach (var a in x.Amounts)
            {
                if (a.Concept == R.Principal)
                {
                    var saldo = Saldo(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId);
                    if (saldo is null) continue;
                    Asentar(ContraDelDesembolso(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId, x.InKind, x.DisbursementForm, x.PayeePersonPublicId), saldo, a.Amount);
                }
                else
                {
                    // La causación que se anula: el espejo de la causación (§2.1).
                    Causacion(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId, null, invertir: true);
                }
            }
        }

        private void AjusteDeComercial(CreditoAjustadoDesdeComercialV1 x)
        {
            foreach (var a in x.Amounts)
            {
                var comercial = Comercial(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId, x.AccountsReceivableRecordedBy, x.Invoice.Number);
                if (a.Concept == R.Principal)
                {
                    var saldo = Saldo(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId);
                    if (saldo is not null) Asentar(saldo, comercial, a.Amount);
                }
                else if (a.Concept == R.CreditBalance)
                {
                    Asentar(comercial, L(R.SaldoAFavor, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId), a.Amount);
                }
                else
                {
                    // Nota débito: más interés (InteresPorCobrar contra Ingreso); nota crédito o devolución, negativo, lo reversa.
                    Causacion(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId, null);
                }
            }
        }

        private void Causaciones(IReadOnlyList<LoanFactLineV1> lineas)
        {
            foreach (var l in lineas)
                foreach (var a in l.Amounts)
                    Causacion(l.Dimensions, a, l.Loan, l.DebtorPersonPublicId, l.CounterpartyPersonPublicId);
        }

        private void Pago(PagoAplicadoV1 x)
        {
            foreach (var a in x.Amounts)
            {
                if (EsSuspenso(a.Concept))
                {
                    CobroDeSuspenso(x.Dimensions, [a], x.Loan, x.DebtorPersonPublicId);
                    continue;
                }
                var saldo = Saldo(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId);
                if (saldo is not null) Asentar(Recaudo(x.Dimensions, a, x.Loan, x.Collector.CollectorClass, x.Collector.PayerPersonPublicId), saldo, a.Amount);
            }
        }

        private void PagoReversado(PagoReversadoV1 x)
        {
            foreach (var a in x.Amounts)
            {
                var recaudo = Recaudo(x.Dimensions, a, x.Loan, x.Collector.CollectorClass, x.Collector.PayerPersonPublicId);
                if (x.Dimensions.WrittenOff)
                {
                    Asentar(L(R.IngresoPorRecuperacion, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId), recaudo, a.Amount);
                    Asentar(L(R.CastigadaEnOrden, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId), L(R.ContraOrdenControl, x.Dimensions, a, x.Loan, null), a.Amount);
                }
                else if (EsSuspenso(a.Concept))
                {
                    Asentar(L(R.Ingreso, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId), recaudo, a.Amount);
                    Asentar(L(R.SuspensoEnOrden, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId), L(R.ContraOrdenContingente, x.Dimensions, a, x.Loan, null), a.Amount);
                }
                else if (Saldo(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId) is { } saldo)
                {
                    // Restituye con las dimensiones vigentes del reverso (§9): la matriz de su fecha contable.
                    Asentar(saldo, recaudo, a.Amount);
                }
            }
        }

        private void AnticipoRecibido(AnticipoRecibidoV1 x)
        {
            foreach (var a in x.Amounts)
                Asentar(Recaudo(x.Dimensions, a, x.Loan, u.ClaseDeRecaudador ?? x.Dimensions.CollectorClass, u.Pagador),
                    L(R.Anticipo, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId), a.Amount);
        }

        private void AnticipoAplicado(AnticipoAplicadoV1 x)
        {
            foreach (var a in x.Amounts)
                if (Saldo(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId) is { } saldo)
                    Asentar(L(R.Anticipo, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId), saldo, a.Amount);
        }

        private void CobroDeSuspenso(LoanFactDimensionsV1 d, IReadOnlyList<ConceptAmountV1> importes, LoanRefV1 credito, Guid deudor)
        {
            foreach (var a in importes)
            {
                Asentar(Recaudo(d, a, credito, u.ClaseDeRecaudador ?? d.CollectorClass, u.Pagador), L(R.Ingreso, d, a, credito, deudor), a.Amount);
                Asentar(L(R.ContraOrdenContingente, d, a, credito, null), L(R.SuspensoEnOrden, d, a, credito, deudor), a.Amount);
            }
        }

        private void SaldoAFavorReconocido(SaldoAFavorReconocidoV1 x)
        {
            foreach (var a in x.Amounts)
            {
                var haber = L(R.SaldoAFavor, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId);
                var debe = x.Cause switch
                {
                    // C93: la corrección de un interés ya pagado: el concepto del ingreso viaja en reasonCode.
                    "NationalValueCorrection" => L(R.Ingreso, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId, conceptoDeRegla: a.ReasonCode ?? R.CurrentInterest),
                    "CommercialAdjustment" => Comercial(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId,
                        u.AjusteDeComercial?.AccountsReceivableRecordedBy ?? a.ReasonCode, u.AjusteDeComercial?.Invoice.Number),
                    _ => Recaudo(x.Dimensions, a, x.Loan, u.ClaseDeRecaudador ?? x.Dimensions.CollectorClass, u.Pagador),
                };
                Asentar(debe, haber, a.Amount);
            }
        }

        private void SaldoAFavorResuelto(SaldoAFavorResueltoV1 x)
        {
            foreach (var a in x.Amounts)
            {
                var rol = x.BalanceKind == R.Advance || a.Concept == R.Advance ? R.Anticipo : R.SaldoAFavor;
                var debe = L(rol, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId);
                var haber = x.Resolution == "ApplyAsExtraordinaryPrincipal"
                    ? L(R.Capital, x.Dimensions, a with { Concept = R.Principal }, x.TargetLoan ?? x.Loan, x.DebtorPersonPublicId, conceptoDelMapa: a.Concept)
                    : Recaudo(x.Dimensions, a, x.Loan, x.Collector.CollectorClass, x.Collector.PayerPersonPublicId);
                Asentar(debe, haber, a.Amount);
            }
        }

        private void Recuperacion(LoanFactDimensionsV1 d, IReadOnlyList<ConceptAmountV1> importes, LoanRefV1 credito, Guid deudor)
        {
            foreach (var a in importes)
            {
                Asentar(Recaudo(d, a, credito, u.ClaseDeRecaudador ?? d.CollectorClass, u.Pagador), L(R.IngresoPorRecuperacion, d, a, credito, deudor), a.Amount);
                Asentar(L(R.ContraOrdenControl, d, a, credito, null), L(R.CastigadaEnOrden, d, a, credito, deudor), a.Amount);
            }
        }

        private void Novedad(LoanFactDimensionsV1 anteriores, LoanFactDimensionsV1 nuevas, IReadOnlyList<ConceptAmountV1> importes,
            LoanRefV1 creditoAnterior, LoanRefV1 creditoNuevo, Guid deudor)
        {
            foreach (var a in importes)
            {
                if (!string.Equals(a.ReasonCode, Capitalizacion, StringComparison.OrdinalIgnoreCase))
                {
                    Traslado(anteriores, nuevas, a, creditoAnterior, creditoNuevo, deudor);
                    continue;
                }
                var capital = L(R.Capital, nuevas, a, creditoNuevo, deudor);
                if (EsSuspenso(a.Concept))
                {
                    // Lo suspendido no fue ingreso: va al ingreso diferido y su orden se cancela (§2.1, CreditoReestructurado).
                    Asentar(capital, L(R.IngresoDiferido, nuevas, a, creditoNuevo, deudor), a.Amount);
                    Asentar(L(R.ContraOrdenContingente, anteriores, a, creditoAnterior, null), L(R.SuspensoEnOrden, anteriores, a, creditoAnterior, deudor), a.Amount);
                }
                else if (Saldo(anteriores, a, creditoAnterior, deudor) is { } saldo)
                {
                    // Lo causado pasa de su rol de saldo a capital.
                    Asentar(capital, saldo, a.Amount);
                }
            }
        }

        private void CobroProvisional(CobroProvisionalIncorporadoV1 x)
        {
            foreach (var a in x.Amounts)
                if (Saldo(x.Dimensions, a, x.Loan, x.DebtorPersonPublicId) is { } saldo)
                    Asentar(L(R.CuentaPorCobrarComercial, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId, factura: x.Invoice.Number), saldo, a.Amount);
        }

        private void Reclasificacion(IReadOnlyList<LoanFactLineV1> lineas)
        {
            foreach (var l in lineas)
                foreach (var a in l.Amounts)
                    Traslado(l.PreviousDimensions ?? l.Dimensions, l.Dimensions, a, l.Loan, l.Loan, l.DebtorPersonPublicId);
        }

        private void Deterioro(IReadOnlyList<LoanFactLineV1> lineas, IReadOnlyList<GeneralImpairmentLineV1>? general)
        {
            foreach (var l in lineas)
            {
                foreach (var a in l.Amounts)
                {
                    var individual = L(R.DeterioroIndividual, l.Dimensions, a, l.Loan, l.DebtorPersonPublicId);
                    Movimiento(a.ReasonCode, a.Amount, individual,
                        nivel => L(nivel, l.Dimensions, a, l.Loan, null, motivo: R.Individual));
                }
            }
            foreach (var g in general ?? [])
            {
                var valores = new ValoresDeCartera(ModalityCode: Texto(g.ModalityCode), SubsegmentCode: Texto(g.SubsegmentCode), ReasonCode: g.Kind,
                    BranchPublicId: Sucursal(g.BranchPublicId));
                var deterioro = new Lado(R.DeterioroGeneral, valores, null, null, null);
                Movimiento(g.ReasonCode, g.Amount, deterioro, nivel => new Lado(nivel, valores, null, null, null));
            }
        }

        /// <summary>El movimiento del deterioro (§11.3): aumento al gasto; disminución al gasto del mismo ejercicio o a la recuperación; traslado con signo.</summary>
        private void Movimiento(string? movimiento, decimal importe, Lado deterioro, Func<string, Lado> resultado)
        {
            switch (movimiento)
            {
                case "Increase":
                    Asentar(resultado(R.GastoDeterioro), deterioro, importe);
                    break;
                case "DecreaseSameYear":
                    Asentar(deterioro, resultado(R.GastoDeterioro), importe);
                    break;
                case "DecreasePriorYears":
                    Asentar(deterioro, resultado(R.RecuperacionDeterioro), importe);
                    break;
                case "Transfer":
                    // Una sola partida con signo: positivo, el deterioro que sube (crédito); negativo, el que baja. Cuadra con los demás traslados.
                    Suelta(deterioro, debitoSiPositivo: false, importe);
                    break;
                default:
                    fallos.Add(new FalloDeCartera(AccountingErrors.LendingFactUnbalanced(importe), hecho.Tipo, deterioro.Rol, null, null,
                        deterioro.Credito is { } c ? [c] : []));
                    break;
            }
        }

        private void ReversoDeCierre(ReversoDeCierreV1 x)
        {
            // El espejo de la parte del cierre, con las dimensiones vigentes que trae cada línea y la matriz de su fecha (§9, §11.4).
            _invertirTodo = true;
            if (x.ReversedType == CarteraReclasificadaPorCalificacionV1.Type) Reclasificacion(x.Lines);
            else Deterioro(x.Lines, null);
            _invertirTodo = false;
        }

        private void GastoDeCobranza(GastoDeCobranzaCausadoV1 x)
        {
            foreach (var a in x.Amounts)
            {
                if (a.OnBehalfOfAssociate)
                    Asentar(L(R.PagoPorCuentaDelAsociado, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId),
                        L(R.CuentaPorPagarATercero, x.Dimensions, a, x.Loan, x.PayeePersonPublicId), a.Amount);
                else
                    Asentar(L(R.CargoPorCobrar, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId), L(R.Ingreso, x.Dimensions, a, x.Loan, x.DebtorPersonPublicId), a.Amount);
            }
        }

        private void Castigo(CreditoCastigadoV1 x)
        {
            BajaContraDeterioro(x.Dimensions, x.Amounts, x.ImpairmentApplied, "WriteOff", x.Loan, x.DebtorPersonPublicId, castigo: true);
            // El suspenso sale de la orden contingente y entra a control (C81, «pendiente de validar»).
            foreach (var s in x.SuspenseWrittenOff)
            {
                Asentar(L(R.ContraOrdenContingente, x.Dimensions, s, x.Loan, null), L(R.SuspensoEnOrden, x.Dimensions, s, x.Loan, x.DebtorPersonPublicId), s.Amount);
                Asentar(L(R.CastigadaEnOrden, x.Dimensions, s, x.Loan, x.DebtorPersonPublicId), L(R.ContraOrdenControl, x.Dimensions, s, x.Loan, null), s.Amount);
            }
        }

        /// <summary>
        /// Condonación y castigo (§2.1): por concepto, lo condonado o castigado sale de su rol de saldo; al débito, el deterioro individual hasta
        /// lo que Cartera dice que se aplicó y el gasto por el resto. En el castigo, además, el activo entra a la castigada en orden.
        /// </summary>
        private void BajaContraDeterioro(LoanFactDimensionsV1 d, IReadOnlyList<ConceptAmountV1> importes, IReadOnlyList<ConceptAmountV1> aplicado,
            string motivo, LoanRefV1 credito, Guid deudor, bool castigo)
        {
            foreach (var grupo in importes.GroupBy(a => a.Concept))
            {
                var total = grupo.Sum(a => a.Amount);
                var a = grupo.First() with { Amount = total, InstallmentNumber = null };
                if (EsSuspenso(a.Concept))
                {
                    Asentar(L(R.ContraOrdenContingente, d, a, credito, null), L(R.SuspensoEnOrden, d, a, credito, deudor), total);
                    continue;
                }
                var saldo = Saldo(d, a, credito, deudor);
                if (saldo is null) continue;
                var delDeterioro = aplicado.Where(x => x.Concept == a.Concept).Sum(x => x.Amount);
                Suelta(L(R.DeterioroIndividual, d, a, credito, deudor), debitoSiPositivo: true, delDeterioro);
                Suelta(L(R.GastoDeCastigoOCondonacion, d, a, credito, deudor, motivo: motivo), debitoSiPositivo: true, total - delDeterioro);
                Suelta(saldo, debitoSiPositivo: false, total);
                if (castigo)
                    Asentar(L(R.CastigadaEnOrden, d, a, credito, deudor), L(R.ContraOrdenControl, d, a, credito, null), total);
            }
        }

        private void Contingente(LoanFactDimensionsV1? d, string lineaDeCredito, IReadOnlyList<ConceptAmountV1> importes, LoanRefV1? credito, Guid deudor)
        {
            foreach (var a in importes)
            {
                var valores = Valores(d, a, motivo: a.ReasonCode) with { CreditLineCode = Texto(d?.CreditLineCode) ?? Texto(lineaDeCredito) };
                var numero = Texto(credito?.LoanNumber);
                Asentar(new Lado(R.ContraContingenteAcreedora, valores, a.Concept, numero, null),
                    new Lado(R.ContingenteAcreedora, valores, a.Concept, numero, deudor), a.Amount);
            }
        }

        // ------------------------------------------------------------------------------------------ comunes --

        /// <summary>
        /// La causación de un concepto (§2.1): interés por cobrar contra el ingreso; en suspenso, la orden contingente; castigado, la de control;
        /// el seguro contra la aseguradora; un cargo por cuenta del asociado contra el tercero del cargo y uno propio contra el ingreso.
        /// </summary>
        private void Causacion(LoanFactDimensionsV1 d, ConceptAmountV1 a, LoanRefV1 credito, Guid deudor, Guid? contraparte, bool invertir = false)
        {
            var rol = R.RolDelConcepto(a.Concept, a.OnBehalfOfAssociate, d.WrittenOff);
            var debe = rol is null ? null : L(rol, d, a, credito, deudor);
            Lado? haber = rol switch
            {
                R.InteresPorCobrar or R.CargoPorCobrar => L(R.Ingreso, d, a, credito, deudor),
                R.SuspensoEnOrden or R.CastigadaEnOrden => L(R.ContraDeOrden(rol)!, d, a, credito, null),
                R.PagoPorCuentaDelAsociado when a.Concept == R.Insurance => L(R.SeguroPorPagar, d, a, credito, contraparte),
                R.PagoPorCuentaDelAsociado => L(R.CuentaPorPagarATercero, d, a, credito, contraparte),
                _ => null,
            };
            if (debe is null || haber is null)
            {
                ConceptoSinRol(a, credito);
                return;
            }
            if (invertir) Asentar(haber, debe, a.Amount);
            else Asentar(debe, haber, a.Amount);
        }

        /// <summary>
        /// El traslado de un saldo de una combinación a otra (§2.2, «naturaleza crédito»): en los roles de naturaleza débito, débito a la nueva y
        /// crédito a la anterior; en los de naturaleza crédito, al revés. El saldo del deterioro individual viaja con <c>reasonCode =
        /// Individual</c>. Las dos puntas forman un par: si resuelven la misma cuenta, sucursal, tercero y cruce, no dejan líneas (C21).
        /// </summary>
        private void Traslado(LoanFactDimensionsV1 anteriores, LoanFactDimensionsV1 nuevas, ConceptAmountV1 a, LoanRefV1 creditoAnterior, LoanRefV1 creditoNuevo, Guid deudor)
        {
            var rol = string.Equals(a.ReasonCode, R.Individual, StringComparison.Ordinal)
                ? R.DeterioroIndividual
                : R.RolDelConcepto(a.Concept, a.OnBehalfOfAssociate, nuevas.WrittenOff);
            if (rol is null || R.Buscar(rol) is not { } definicion)
            {
                ConceptoSinRol(a, creditoNuevo);
                return;
            }
            var nueva = L(rol, nuevas, a, creditoNuevo, deudor);
            var anterior = L(rol, anteriores, a, creditoAnterior, deudor);
            if (definicion.Naturaleza == AccountNature.Credit) Asentar(anterior, nueva, a.Amount, traslado: true);
            else Asentar(nueva, anterior, a.Amount, traslado: true);
        }

        private Lado ContraDelDesembolso(LoanFactDimensionsV1 d, ConceptAmountV1 a, LoanRefV1 credito, Guid deudor, InKindDisbursementV1? especie,
            string forma, Guid? beneficiario) =>
            especie is { } k
                ? Comercial(d, a, credito, deudor, k.AccountsReceivableRecordedBy, k.Invoice.Number)
                : L(R.DesembolsoPorGirar, d, a, credito, beneficiario ?? deudor, motivo: Texto(a.ReasonCode) ?? Texto(forma));

        /// <summary>El rol de Comercial (§2.1, FR-056): la venta sellada «Contabilidad» (crédito provisional) o «Cartera» (puente); cruza con la factura.</summary>
        private Lado Comercial(LoanFactDimensionsV1 d, ConceptAmountV1 a, LoanRefV1 credito, Guid deudor, string? registradaPor, string? factura) =>
            L(string.Equals(registradaPor, "Contabilidad", StringComparison.OrdinalIgnoreCase) ? R.CuentaPorCobrarComercial : R.PuenteComercial,
                d, a, credito, deudor, factura: Texto(factura));

        private Lado Recaudo(LoanFactDimensionsV1 d, ConceptAmountV1 a, LoanRefV1 credito, string? clase, Guid? pagador) =>
            L(R.RecaudoPorAplicar, d, a, credito, pagador is { } p && p != Guid.Empty ? p : u.Pagador, clase: Texto(clase) ?? Texto(d.CollectorClass));

        /// <summary>El rol de saldo del concepto (§2.2); nulo si el concepto no es de Cartera (y queda el fallo).</summary>
        private Lado? Saldo(LoanFactDimensionsV1 d, ConceptAmountV1 a, LoanRefV1 credito, Guid deudor)
        {
            var rol = R.RolDelConcepto(a.Concept, a.OnBehalfOfAssociate, d.WrittenOff);
            if (rol is null)
            {
                ConceptoSinRol(a, credito);
                return null;
            }
            return L(rol, d, a, credito, deudor);
        }

        private void ConceptoSinRol(ConceptAmountV1 a, LoanRefV1? credito) =>
            fallos.Add(new FalloDeCartera(AccountingErrors.LendingFactUnbalanced(a.Amount), hecho.Tipo, null,
                new Dictionary<string, string> { ["conceptCode"] = a.Concept }, null, Texto(credito?.LoanNumber) is { } n ? [n] : []));

        private Lado L(string rol, LoanFactDimensionsV1? d, ConceptAmountV1 a, LoanRefV1? credito, Guid? tercero,
            string? motivo = null, string? clase = null, string? factura = null, string? conceptoDeRegla = null, string? conceptoDelMapa = null) =>
            new(rol, Valores(d, a, motivo, clase, conceptoDeRegla), conceptoDelMapa ?? a.Concept, Texto(credito?.LoanNumber), tercero is { } t && t != Guid.Empty ? t : null, factura);

        private ValoresDeCartera Valores(LoanFactDimensionsV1? d, ConceptAmountV1 a, string? motivo = null, string? clase = null, string? conceptoDeRegla = null) => new(
            Texto(d?.CreditLineCode), Texto(d?.ModalityCode), Texto(d?.SubsegmentCode), Texto(d?.RiskCategory), conceptoDeRegla ?? a.Concept,
            d?.IsEmployee, d?.InGracePeriod, a.OnBehalfOfAssociate, d?.Restructured, clase, motivo, Sucursal(d?.BranchPublicId));

        private Guid Sucursal(Guid? sucursal) => sucursal is { } s && s != Guid.Empty ? s : _sobre.BranchPublicId;

        private void Asentar(Lado debe, Lado haber, decimal importe, bool traslado = false)
        {
            if (importe == 0m) return;
            int? par = traslado ? nuevoPar() : null;
            Agregar(debe, debitoSiPositivo: true, importe, par);
            Agregar(haber, debitoSiPositivo: false, importe, par);
        }

        private void Suelta(Lado lado, bool debitoSiPositivo, decimal importe)
        {
            if (importe == 0m) return;
            Agregar(lado, debitoSiPositivo, importe, null);
        }

        private void Agregar(Lado lado, bool debitoSiPositivo, decimal importe, int? par) =>
            destino.Add(new Partida(hecho.Tipo, lado.Rol, _invertirTodo ? !debitoSiPositivo : debitoSiPositivo, importe, lado.Valores,
                _sobre.Origin.OperationDate, ConceptoDelMapa(lado), lado.Credito, lado.Tercero, _sobre.PersonPublicId, lado.Factura, par));

        /// <summary>
        /// El concepto que la línea muestra en su mapa: el de la regla si el rol lo usa; en un anticipo o un saldo a favor, el suyo; en el puente
        /// de recaudo, ninguno (lleva el dinero, no un concepto); si no, el del importe.
        /// </summary>
        private static string? ConceptoDelMapa(Lado lado) => lado.Rol switch
        {
            R.RecaudoPorAplicar => null,
            R.Anticipo => R.Advance,
            R.SaldoAFavor => R.CreditBalance,
            _ => R.Buscar(lado.Rol) is { } rol && rol.Admite(DimensionDeCartera.Concept) && lado.Valores.ConceptCode is { } c ? c : lado.Concepto,
        };
    }

    private static bool EsSuspenso(string concepto) => concepto is R.CurrentInterestInSuspense or R.DefaultInterestInSuspense;

    private static string? Texto(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    // ===================================================================================================== paso 2 --

    /// <summary>
    /// El comprobante de la unidad con la matriz y los mapeos cargados (§7.3). <paramref name="origen"/> nulo = el hecho de la unidad
    /// (<c>LendingIssuedDocument</c> con el <c>origin.publicId</c>); en un resumido lo pone quien suma.
    /// </summary>
    public static ConstruccionDeCartera Construir(IReadOnlyList<HechoDeCartera> unidad, CatalogosDeCartera c, AccountingOrigin? origen = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        var desglose = Desglosar(unidad);
        var principal = desglose.Principal ?? throw new ArgumentException("La unidad no trae hechos.", nameof(unidad));
        var fecha = principal.Sobre.Origin.OperationDate;
        var fallos = new List<FalloDeCartera>(desglose.Fallos);

        // El tipo de la unidad lo da el hecho principal (C01), con la clase del recaudador (§4.2).
        TipoDeLaUnidadDeCartera? tipo = null;
        var resuelto = c.Mapeos.Resolver(principal.Tipo, ClaseDeRecaudador(unidad));
        if (resuelto.IsFailure) fallos.Add(new FalloDeCartera(resuelto.Error, principal.Tipo, null, null, null, []));
        else tipo = resuelto.Value;

        var lineas = new List<(PostingLine Linea, Partida Partida, string AccountName, bool ConservaDetalle)>();
        var paresResueltos = new Dictionary<int, List<int>>();
        foreach (var p in desglose.Partidas)
        {
            if (p.Importe == 0m) continue;
            var peticion = new PeticionDeReglaDeCartera(p.Hecho, p.Rol, p.Valores, p.Fecha, p.Importe, p.Credito is { } cr ? [cr] : []);
            var resolucion = c.Matriz.Resolver(peticion);
            if (resolucion.IsFailure)
            {
                fallos.Add(new FalloDeCartera(resolucion.Error, p.Hecho, p.Rol, Combinacion(p), null, peticion.Loans));
                continue;
            }
            var regla = resolucion.Value!;
            var cuenta = regla.CuentaParaReglas;
            var rol = R.Buscar(p.Rol);

            int? persona = null;
            if (cuenta.RequiresThirdParty)
            {
                var guid = rol is { Tercero: not TerceroNaturalDeCartera.Ninguno } ? p.Tercero ?? p.TerceroDelSobre : p.TerceroDelSobre ?? p.Tercero;
                persona = guid is { } g && c.Personas.TryGetValue(g, out var id) ? id : null;
            }

            string? tipoDeCruce = null, numeroDeCruce = null;
            if (cuenta.RequiresCrossDocument)
            {
                if (rol?.Cruce == CruceDeRol.Factura && p.Factura is { } factura)
                {
                    tipoDeCruce = CruceDeFactura;
                    numeroDeCruce = factura;
                }
                else if (p.Credito is { } numero)
                {
                    tipoDeCruce = tipo?.CrossDocumentTypeCode;
                    numeroDeCruce = numero;
                }
            }

            var debito = p.Importe > 0m == p.DebitoSiPositivo;
            var valor = Math.Abs(p.Importe);
            var linea = new PostingLine
            {
                AccountCode = regla.Cuenta.Code,
                Debit = debito ? valor : 0m,
                Credit = debito ? 0m : valor,
                Detail = p.Credito is { } n ? $"{principal.Sobre.Origin.Number} · crédito {n}" : principal.Sobre.Origin.Number,
                PersonId = persona,
                BranchId = regla.BranchId,
                CostCenterId = cuenta.RequiresCostCenter ? regla.CostCenterId : null,
                CrossDocumentType = tipoDeCruce,
                CrossDocumentNumber = numeroDeCruce,
            };
            if (p.Par is { } par)
            {
                if (!paresResueltos.TryGetValue(par, out var indices)) paresResueltos[par] = indices = [];
                indices.Add(lineas.Count);
            }
            lineas.Add((linea, p, regla.Cuenta.Name, cuenta.RequiresThirdParty || cuenta.RequiresCrossDocument || cuenta.RequiresTaxBase));
        }

        if (fallos.Count > 0)
            return new ConstruccionDeCartera(null, tipo, [], fallos, fecha, principal);

        // C21: las dos puntas de un traslado que resolvieron la misma cuenta, sucursal, centro, tercero y cruce no dejan líneas.
        var descartadas = new HashSet<int>();
        foreach (var indices in paresResueltos.Values.Where(i => i.Count == 2))
        {
            var (a, b) = (lineas[indices[0]].Linea, lineas[indices[1]].Linea);
            if (a.AccountCode == b.AccountCode && a.BranchId == b.BranchId && a.CostCenterId == b.CostCenterId && a.PersonId == b.PersonId
                && a.CrossDocumentType == b.CrossDocumentType && a.CrossDocumentNumber == b.CrossDocumentNumber)
                descartadas.UnionWith(indices);
        }
        var vivas = lineas.Where((_, i) => !descartadas.Contains(i)).ToList();
        if (vivas.Count == 0)
            return new ConstruccionDeCartera(null, tipo, [], [], fecha, principal,
                descartadas.Count > 0 ? LendingPosting.SinComprobanteMismasCuentas : LendingPosting.SinComprobanteValorCero);

        var sumadas = SumarIguales(vivas);
        var sobre = principal.Sobre;
        var registradoPor = sobre.OriginUser.CentralUserId is { } central ? new UsuarioDeOrigen(central, sobre.OriginUser.Name) : null;
        var request = new PostingRequest(tipo!.VoucherTypeCode, fecha, Descripcion(principal),
            origen ?? new AccountingOrigin(ModuloContable.Cartera, OrigenesDeCartera.Documento, sobre.Origin.PublicId),
            sumadas.Select(s => s.Linea).ToList(), DocumentKind.Regular, registradoPor);
        return new ConstruccionDeCartera(request, tipo, sumadas.Select(s => s.Mapa).ToList(), [], fecha, principal);
    }

    /// <summary>
    /// Suma las líneas con la misma cuenta, lado, sucursal, centro, tercero y cruce —y, en las cuentas que exigen tercero, cruce o base, el mismo
    /// crédito: una línea por crédito (§10.4) aunque la cuenta no pida cruce; hasta el 2026-10-13 los créditos del mismo deudor de una parte se
    /// juntaban—; el mapa junta hechos y créditos.
    /// </summary>
    private static List<(PostingLine Linea, LineaDeCartera Mapa)> SumarIguales(
        IReadOnlyList<(PostingLine Linea, Partida Partida, string AccountName, bool ConservaDetalle)> lineas)
    {
        var resultado = new List<(PostingLine Linea, List<Partida> Partidas, string AccountName, bool ConservaDetalle)>();
        var indice = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (linea, partida, nombre, conserva) in lineas)
        {
            var clave = string.Join('|', linea.AccountCode, linea.Debit > 0m ? "D" : "C", linea.BranchId, linea.CostCenterId, linea.PersonId,
                linea.CrossDocumentType, linea.CrossDocumentNumber, conserva ? partida.Credito : null);
            if (indice.TryGetValue(clave, out var i))
            {
                var previa = resultado[i];
                previa.Partidas.Add(partida);
                resultado[i] = (previa.Linea with { Debit = previa.Linea.Debit + linea.Debit, Credit = previa.Linea.Credit + linea.Credit },
                    previa.Partidas, previa.AccountName, previa.ConservaDetalle);
                continue;
            }
            indice[clave] = resultado.Count;
            resultado.Add((linea, [partida], nombre, conserva));
        }
        return resultado.Select(r =>
        {
            var conceptos = r.Partidas.Select(p => p.Concepto).Distinct().ToList();
            var mapa = new LineaDeCartera(
                r.Partidas.Select(p => p.Hecho).Distinct(StringComparer.Ordinal).ToList(),
                r.Partidas[0].Rol,
                conceptos.Count == 1 ? conceptos[0] : null,
                r.Partidas.Select(p => p.Credito).Where(x => x is not null).Select(x => x!).Distinct(StringComparer.Ordinal).ToList(),
                r.Linea.AccountCode, r.AccountName, r.ConservaDetalle);
            return (r.Linea, mapa);
        }).ToList();
    }

    /// <summary>La combinación que buscó una partida, para el error y la validación previa (§13.3).</summary>
    public static IReadOnlyDictionary<string, string> Combinacion(Partida p)
    {
        var v = p.Valores;
        var combinacion = new Dictionary<string, string>(StringComparer.Ordinal);
        void Poner(DimensionDeCartera d, object? valor)
        {
            if (valor is null || valor is string { Length: 0 }) return;
            combinacion[R.NombreDe(d)] = valor is bool b ? (b ? "true" : "false") : valor.ToString()!;
        }
        var rol = R.Buscar(p.Rol);
        var (exigidas, opcionales) = rol is null ? ([], []) : R.DimensionesDe(rol, v.ReasonCode);
        bool Usa(DimensionDeCartera d) => exigidas.Contains(d) || opcionales.Contains(d);
        if (Usa(DimensionDeCartera.CreditLine)) Poner(DimensionDeCartera.CreditLine, v.CreditLineCode);
        if (Usa(DimensionDeCartera.Modality)) Poner(DimensionDeCartera.Modality, v.ModalityCode);
        if (Usa(DimensionDeCartera.Subsegment)) Poner(DimensionDeCartera.Subsegment, v.SubsegmentCode);
        if (Usa(DimensionDeCartera.RiskCategory)) Poner(DimensionDeCartera.RiskCategory, v.RiskCategory);
        if (Usa(DimensionDeCartera.Concept)) Poner(DimensionDeCartera.Concept, v.ConceptCode);
        if (Usa(DimensionDeCartera.IsEmployee)) Poner(DimensionDeCartera.IsEmployee, v.IsEmployee);
        if (Usa(DimensionDeCartera.InGracePeriod)) Poner(DimensionDeCartera.InGracePeriod, v.InGracePeriod);
        if (Usa(DimensionDeCartera.OnBehalfOfAssociate)) Poner(DimensionDeCartera.OnBehalfOfAssociate, v.OnBehalfOfAssociate);
        if (Usa(DimensionDeCartera.Restructured)) Poner(DimensionDeCartera.Restructured, v.Restructured);
        if (Usa(DimensionDeCartera.CollectorClass)) Poner(DimensionDeCartera.CollectorClass, v.CollectorClass);
        if (Usa(DimensionDeCartera.Reason)) Poner(DimensionDeCartera.Reason, v.ReasonCode);
        Poner(DimensionDeCartera.Branch, v.BranchPublicId);
        return combinacion;
    }

    /// <summary>«{clase} {número} · crédito {número}» (§7.3); sin crédito único, «{clase} {número}».</summary>
    public static string Descripcion(HechoDeCartera principal)
    {
        var sobre = principal.Sobre;
        var clase = NombreDeLaClase(principal.ClaseDeOrigen);
        var texto = $"{clase} {sobre.Origin.Number}".Trim();
        return Credito(principal.Contenido) is { LoanNumber.Length: > 0 } credito ? $"{texto} · crédito {credito.LoanNumber}" : texto;
    }

    /// <summary>El nombre de la clase del documento de Cartera para la descripción del comprobante.</summary>
    public static string NombreDeLaClase(string? clase) => clase switch
    {
        "LoanDisbursement" => "Desembolso",
        "DisbursementReversal" => "Reverso de desembolso",
        "CommercialAdjustment" => "Ajuste desde Comercial",
        "PaymentApplication" => "Aplicación de pago",
        "PaymentReversal" => "Reverso de pago",
        "CreditBalanceResolution" => "Resolución de saldo a favor",
        "ProvisionalCollection" => "Cobro provisional",
        "AccountingFactPart" => "Parte contable",
        "LoanCharge" => "Cargo",
        "TermsChange" => "Novedad",
        "Waiver" => "Condonación",
        "WriteOff" => "Castigo",
        "CreditApplication" => "Solicitud",
        "CreditLimit" => "Cupo",
        "Collateral" => "Garantía",
        "LendingActivation" => "Activación de Cartera",
        "MonthlyClose" => "Cierre de Cartera",
        null or "" => "Hecho de Cartera",
        _ => clase,
    };
}
