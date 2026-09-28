using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Sales.Cash;

/// <summary>Un medio tal como se arquea: su clase, su método y la tolerancia que se copia al cerrar. (nuevo)</summary>
public sealed record MedioDeArqueo(int PaymentMeansId, string Code, PaymentMeansClass Class, CashCountMethod CountMethod, decimal ToleranceAmount);

/// <summary>
/// Un pago de la sesión (<c>INV_DocumentPayments</c> con su <c>CashSessionId</c>): medio, sentido, valor, datáfono y referencia
/// para el cotejo. (nuevo)
/// </summary>
public sealed record PagoDeLaSesion(
    int DocumentPaymentId,
    int PaymentMeansId,
    PaymentDirection Direction,
    decimal Amount,
    int? CardTerminalId = null,
    string? Reference = null,
    string? DocumentNumber = null);

/// <summary>
/// Un movimiento de caja (clase <c>CashMovement</c> + <c>INV_CashMovementDetails</c>) que toca la sesión como origen o como
/// destino. <see cref="Confirmed"/> falso (borrador o en aprobación) no cuenta en el esperado. (nuevo)
/// </summary>
public sealed record MovimientoDeCaja(
    int DocumentId,
    CashMovementKind Kind,
    bool Confirmed,
    int CashSessionId,
    int? DestinationCashSessionId,
    int SourcePaymentMeansId,
    int? TargetPaymentMeansId,
    decimal Amount,
    int? SourceCardTerminalId = null,
    int? TargetCardTerminalId = null);

/// <summary>
/// La base con que abrió la sesión y el medio al que entra (el efectivo). Con base del día la sesión abre sin base y la base
/// llega por su movimiento <c>BaseIncome</c>: la aplicación no la pasa dos veces. (nuevo)
/// </summary>
public sealed record BaseDeApertura(int PaymentMeansId, decimal Amount);

/// <summary>Lo que se calcula: la sesión, los medios del catálogo, la base, sus pagos y los movimientos que la tocan. (nuevo)</summary>
public sealed record PedidoDeEsperado(
    int CashSessionId,
    IReadOnlyList<MedioDeArqueo> Medios,
    BaseDeApertura? Base,
    IReadOnlyList<PagoDeLaSesion> Pagos,
    IReadOnlyList<MovimientoDeCaja> Movimientos);

/// <summary>El esperado de un datáfono en un medio de tarjeta (lo que debe traer su lote). (nuevo)</summary>
public sealed record EsperadoPorDatafono(int CardTerminalId, decimal Expected, int PaymentsCount);

/// <summary>Un pago recibido que se coteja por referencia en el arqueo. (nuevo)</summary>
public sealed record ReferenciaParaCotejo(int DocumentPaymentId, string? DocumentNumber, string? Reference, decimal Amount);

/// <summary>
/// El esperado de un medio (contracts/api.md §21.2 <c>CashSessionExpectedDto.lines</c>): base + ventas − devoluciones + entradas −
/// salidas + reclasificaciones que entran − las que salen. (nuevo)
/// </summary>
public sealed record EsperadoPorMedio(
    MedioDeArqueo Medio,
    decimal OpeningBase,
    decimal Sales,
    decimal Refunds,
    decimal MovementsIn,
    decimal MovementsOut,
    decimal ReclassificationsIn,
    decimal ReclassificationsOut,
    int PaymentsCount,
    IReadOnlyList<EsperadoPorDatafono> Terminals,
    IReadOnlyList<ReferenciaParaCotejo> References)
{
    public decimal Expected => OpeningBase + Sales - Refunds + MovementsIn - MovementsOut + ReclassificationsIn - ReclassificationsOut;

    /// <summary>La tolerancia del medio al cerrar: se copia a la línea del arqueo.</summary>
    public decimal Tolerance => Medio.ToleranceAmount;
}

/// <summary>El esperado de la sesión: una línea por medio con saldo o actividad, en el orden del catálogo. (nuevo)</summary>
public sealed record EsperadoDeLaSesion(IReadOnlyList<EsperadoPorMedio> Lines)
{
    public decimal Total => Lines.Sum(l => l.Expected);
}

/// <summary>
/// El motor puro del esperado de una sesión de caja (feature 012, I3, T580; FR-099, FR-100, T50; data-model §15 «Arqueo»;
/// contracts/api.md §21.2). Por medio: base (sólo el medio de la base, el efectivo) + recibido − reintegrado ± movimientos
/// <b>confirmados</b>: los retiros salen del origen; el retiro a otra caja entra en la sesión destino; el ingreso de base entra;
/// la reclasificación baja el medio de origen y sube el correcto. Las tarjetas llevan su esperado por datáfono (los pagos y las
/// reclasificaciones con datáfono) y los medios por lote o por referencia, la lista de pagos para el cotejo. Lo usan la consulta
/// del esperado, el cierre de la sesión y el cierre del día. Lo fijan los casos dorados de <c>Domain.Tests/Sales/Cash/Casos</c>.
/// (nuevo)
/// </summary>
public static class CalculadoraDeEsperado
{
    public static EsperadoDeLaSesion Calcular(PedidoDeEsperado pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        var acumulados = pedido.Medios.ToDictionary(m => m.PaymentMeansId, m => new Acumulado(m));
        Acumulado De(int medio) => acumulados.TryGetValue(medio, out var a)
            ? a
            : throw new ArgumentException($"El medio {medio} no está en el catálogo que se entregó.", nameof(pedido));

        if (pedido.Base is { } @base)
        {
            var a = De(@base.PaymentMeansId);
            a.Base += @base.Amount;
            a.Activo = true;
        }

        foreach (var pago in pedido.Pagos)
        {
            var a = De(pago.PaymentMeansId);
            a.Activo = true;
            a.Pagos++;
            var signo = pago.Direction == PaymentDirection.Received ? 1 : -1;
            if (signo > 0) a.Ventas += pago.Amount;
            else a.Devoluciones += pago.Amount;
            if (pago.CardTerminalId is { } datafono) a.Datafono(datafono, signo * pago.Amount, cuentaPago: true);
            if (signo > 0 && a.Medio.CountMethod is CashCountMethod.ByReference or CashCountMethod.VoucherTotal)
                a.Referencias.Add(new ReferenciaParaCotejo(pago.DocumentPaymentId, pago.DocumentNumber, pago.Reference, pago.Amount));
        }

        foreach (var m in pedido.Movimientos.Where(m => m.Confirmed))
        {
            var esOrigen = m.CashSessionId == pedido.CashSessionId;
            var esDestino = m.DestinationCashSessionId == pedido.CashSessionId;
            if (!esOrigen && !esDestino) continue;

            switch (m.Kind)
            {
                case CashMovementKind.WithdrawalToSafe:
                case CashMovementKind.WithdrawalForDeposit:
                    if (esOrigen) Salida(De(m.SourcePaymentMeansId), m);
                    break;

                case CashMovementKind.WithdrawalToRegister:
                    if (esOrigen) Salida(De(m.SourcePaymentMeansId), m);
                    if (esDestino) Entrada(De(m.SourcePaymentMeansId), m.Amount, m.SourceCardTerminalId);
                    break;

                case CashMovementKind.BaseIncome:
                    if (esOrigen) Entrada(De(m.SourcePaymentMeansId), m.Amount, null);
                    break;

                case CashMovementKind.ReclassificationBetweenMeans:
                    if (!esOrigen) break;
                    var sale = De(m.SourcePaymentMeansId);
                    sale.Activo = true;
                    sale.ReclasificacionesSalida += m.Amount;
                    if (m.SourceCardTerminalId is { } origen) sale.Datafono(origen, -m.Amount, cuentaPago: false);

                    var entra = De(m.TargetPaymentMeansId
                        ?? throw new ArgumentException($"La reclasificación {m.DocumentId} no trae el medio correcto.", nameof(pedido)));
                    entra.Activo = true;
                    entra.ReclasificacionesEntrada += m.Amount;
                    if (m.TargetCardTerminalId is { } destino) entra.Datafono(destino, m.Amount, cuentaPago: false);
                    break;
            }
        }

        return new EsperadoDeLaSesion(pedido.Medios
            .Select(m => acumulados[m.PaymentMeansId])
            .Where(a => a.Activo)
            .Select(a => a.Linea())
            .ToList());
    }

    private static void Salida(Acumulado a, MovimientoDeCaja m)
    {
        a.Activo = true;
        a.Salidas += m.Amount;
        if (m.SourceCardTerminalId is { } datafono) a.Datafono(datafono, -m.Amount, cuentaPago: false);
    }

    private static void Entrada(Acumulado a, decimal valor, int? datafono)
    {
        a.Activo = true;
        a.Entradas += valor;
        if (datafono is { } d) a.Datafono(d, valor, cuentaPago: false);
    }

    private sealed class Acumulado(MedioDeArqueo medio)
    {
        private readonly SortedDictionary<int, (decimal Esperado, int Pagos)> _datafonos = [];

        public MedioDeArqueo Medio { get; } = medio;
        public bool Activo { get; set; }
        public decimal Base { get; set; }
        public decimal Ventas { get; set; }
        public decimal Devoluciones { get; set; }
        public decimal Entradas { get; set; }
        public decimal Salidas { get; set; }
        public decimal ReclasificacionesEntrada { get; set; }
        public decimal ReclasificacionesSalida { get; set; }
        public int Pagos { get; set; }
        public List<ReferenciaParaCotejo> Referencias { get; } = [];

        public void Datafono(int datafono, decimal valor, bool cuentaPago)
        {
            var (esperado, pagos) = _datafonos.GetValueOrDefault(datafono);
            _datafonos[datafono] = (esperado + valor, pagos + (cuentaPago ? 1 : 0));
        }

        public EsperadoPorMedio Linea() => new(Medio, Base, Ventas, Devoluciones, Entradas, Salidas, ReclasificacionesEntrada,
            ReclasificacionesSalida, Pagos,
            _datafonos.Select(d => new EsperadoPorDatafono(d.Key, d.Value.Esperado, d.Value.Pagos)).ToList(),
            Referencias);
    }
}
