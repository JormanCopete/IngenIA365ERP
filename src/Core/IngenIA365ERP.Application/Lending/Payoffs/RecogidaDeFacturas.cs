using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payoffs;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payoffs;

/// <summary>
/// Una factura a crédito de Comercial del solicitante que un crédito nuevo podría recoger (feature 028, HU7, T087; FR-001): su número (documento cruce), el
/// documento contable, la fecha y lo pendiente (lo que informa Contabilidad menos lo que ya cobró Recaudo), como lo da el puerto de Recaudo. (nuevo)
/// </summary>
public sealed record FacturaRecogible(string Numero, Guid? DocumentoContable, DateOnly Fecha, decimal Pendiente)
{
    /// <summary>
    /// El identificador de la factura en la consulta de productos (<c>productPublicId</c>): estable y derivado de su número (una factura no es una fila de
    /// Cartera; en la solicitud y el desembolso se nombra por su número).
    /// </summary>
    public Guid Identificador => IdentificadorDe(Numero);

    public static Guid IdentificadorDe(string numero)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes("LND.Recogida.Factura|" + numero.Trim()));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x30);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    /// <summary>Los modos admitidos por la línea del crédito nuevo (<c>Recogida.ProductosAdmitidos</c>: <c>FacturasComerciales</c>, total o parcial).</summary>
    public IReadOnlyList<PayoffMode> Admitidos(AdmisionDeRecogida admision)
    {
        ArgumentNullException.ThrowIfNull(admision);
        return AdmisionDeRecogidaVigente.Modos(admision, ProductoDeRecogida.FacturasComerciales);
    }

    /// <summary>Lo que se recoge: total, lo pendiente; parcial, el valor pedido topado en lo pendiente (si lo cubre, es un total).</summary>
    public decimal Valor(PayoffMode modo, decimal? pedido) => modo == PayoffMode.Partial && pedido is { } p ? Math.Min(p, Pendiente) : Pendiente;

    /// <summary><c>sha256:</c> + la huella de lo que se recoge de la factura a D (la factura, el modo, el valor y lo pendiente).</summary>
    public string Huella(DateOnly d, PayoffMode? modo = null, decimal? pedido = null) => "sha256:" + new HuellaDeCartera()
        .Texto("invoice", Numero)
        .Fecha("date", d)
        .Enumeracion("mode", modo)
        .Decimal("amount", modo is { } m ? Valor(m, pedido) : null, 2)
        .Decimal("pending", Pendiente, 2)
        .Calcular();
}

/// <summary>Las facturas pendientes del solicitante y, si Recaudo no puede emitir el recibo de la recogida, por qué. (nuevo)</summary>
public sealed record FacturasRecogibles(IReadOnlyList<FacturaRecogible> Facturas, string? SinRecibo)
{
    public static FacturasRecogibles Ninguna { get; } = new([], null);

    public FacturaRecogible? Buscar(string? numero) =>
        string.IsNullOrWhiteSpace(numero) ? null : Facturas.FirstOrDefault(f => string.Equals(f.Numero, numero.Trim(), StringComparison.Ordinal));
}

/// <summary>
/// Una factura que el desembolso recoge, evaluada a la fecha valor (feature 028, HU7, T087): cómo (total o parcial), lo que se le aplica, lo que proyectó la
/// solicitud, lo de ahora (lo pendiente o el parcial pedido), la diferencia con su causa y si se omite porque ya no tiene saldo. (nuevo)
/// </summary>
public sealed record RecogidaDeFacturaEvaluada(string Numero, PayoffMode Modo, decimal AAplicar)
{
    public int? ProyectadaId { get; init; }
    public PayoffMode ModoPedido { get; init; }
    public decimal? ValorPedido { get; init; }
    public bool ConProyeccion { get; init; }
    public decimal Proyectado { get; init; }
    public decimal Actual { get; init; }
    public ResultadoDeDiferencia Diferencia { get; init; } = RecogidaEvaluada.SinDiferencia;

    /// <summary>La factura ya no tiene saldo (se pagó en la caja u otro camino): no se recoge y el neto sube.</summary>
    public bool Omitida { get; init; }

    public bool SobreLaTolerancia { get; init; }
    public IReadOnlyList<string> Avisos { get; init; } = [];

    /// <summary>El identificador estable de la factura (el de la consulta de productos).</summary>
    public Guid Identificador => FacturaRecogible.IdentificadorDe(Numero);

    /// <summary>Confirmar exige reconocerla: una diferencia u omisión.</summary>
    public bool PorReconocer => Omitida || Diferencia.HayDiferencia;
}

/// <summary>Una factura recogida en el desembolso: la recogida, la clave de su porción y el recibo de Recaudo con lo que se le aplicó. (nuevo)</summary>
public sealed record RecogidaDeFacturaAplicada(RecogidaDeFacturaEvaluada Recogida, string Clave, Guid Recibo, string NumeroDeRecibo, decimal Aplicado);

/// <summary>Una factura recogida viva (sin reversar) de un desembolso: la fila, la factura, el recibo, la clave y lo aplicado. (nuevo)</summary>
public sealed record FacturaDeRecogidaViva(long FilaId, Guid Desembolso, string Numero, Guid Recibo, string Clave, decimal Monto);

/// <summary>
/// <b>La recogida de facturas a crédito de Comercial en el desembolso</b> (2026-11-27, pedido del dueño; feature 028, HU7, T087; FR-026; research R14, E-16): el
/// lado de Cartera. Las facturas son saldo de Contabilidad que sólo se cobra con un recibo de Recaudo; Cartera no lo lee ni lo escribe: todo pasa por
/// <see cref="IRecogidaEnRecaudo"/> (implementado por Recaudo), en la transacción del desembolso:
/// <list type="bullet">
/// <item><b>leer</b> (<see cref="LeerAsync"/>): las facturas pendientes del solicitante y si Recaudo puede emitir el recibo;</item>
/// <item><b>evaluar</b> (<see cref="EvaluarAsync"/>): al desembolsar, lo pendiente de ahora frente a lo proyectado; la factura ya pagada se omite;</item>
/// <item><b>cerrojos</b> (<see cref="TomarCerrojosAsync"/>): los de Recaudo antes que los de Cartera (el orden global de la 015);</item>
/// <item><b>aplicar</b> (<see cref="AplicarAsync"/>): un recibo sin caja por desembolso, una porción por factura con la clave <c>CAR:{desembolso:N}:{factura}</c>; lo
/// aplicado a cada una tiene que ser lo evaluado (la defensa de H4) y si Recaudo no lo emite el desembolso no existe;</item>
/// <item><b>anular</b> (<see cref="ReversarAsync"/>): al anular el crédito nuevo antes del giro, la anulación del recibo (espejo exacto, sin aprobación).</item>
/// </list>
/// Contabilidad: el recibo debita el rol de su medio (<c>RecogidaEnElDesembolso</c>) y el comprobante del desembolso acredita el puente de la recogida propia por la
/// factura (<see cref="ComoDeduccion(RecogidaDeFacturaAplicada, Guid)"/>). (nuevo)
/// </summary>
public sealed class RecogidaDeFacturas(IApplicationDbContext db, IRecogidaEnRecaudo recaudo)
{
    /// <summary>La clase del documento de la factura como la lleva el hecho del desembolso.</summary>
    public const string ClaseDelDocumento = "CommercialReceivable";

    /// <summary>La clave de la porción del recibo que paga <paramref name="factura"/> en el desembolso <paramref name="desembolso"/>: <c>CAR:{desembolso:N}:{factura}</c>.</summary>
    public static string Clave(Guid desembolso, string factura) => $"{ClavesDeRecaudador.Cartera}:{desembolso:N}:{factura.Trim()}";

    /// <summary>La clave del recibo de la recogida de facturas de un desembolso (uno por desembolso): <c>CAR:{desembolso:N}</c>.</summary>
    public static string ClaveDelRecibo(Guid desembolso) => $"{ClavesDeRecaudador.Cartera}:{desembolso:N}";

    /// <summary>Las facturas pendientes de <paramref name="persona"/> y si Recaudo puede emitir el recibo de la recogida. Sólo lee.</summary>
    public async Task<FacturasRecogibles> LeerAsync(Guid persona, CancellationToken ct)
    {
        var r = await recaudo.PendientesAsync(persona, ct);
        return new FacturasRecogibles(r.Invoices.Where(f => f.Pending > 0m)
            .Select(f => new FacturaRecogible(f.InvoiceNumber, f.AccountingDocumentPublicId, f.Date, f.Pending)).ToList(), r.NotReadyReason);
    }

    /// <summary>
    /// Lo que impide recoger <paramref name="factura"/> en <paramref name="modo"/> con lo que admite la línea del crédito nuevo; nulo si se puede (HU7):
    /// la cooperativa no las recoge (<c>ProductNotAdmitted</c>), un modo que no admite (<c>ModeNotAdmitted</c>), Recaudo sin configurar (<c>NotPayable</c>).
    /// </summary>
    public static Error? Validar(FacturaRecogible factura, PayoffMode modo, decimal? pedido, AdmisionDeRecogida admision, string? sinRecibo)
    {
        ArgumentNullException.ThrowIfNull(factura);
        ArgumentNullException.ThrowIfNull(admision);
        if (!admision.Admite(ProductoDeRecogida.FacturasComerciales)) return ErroresDeRecogida.FacturaNoAdmitida(factura.Numero).ComoError();
        var modos = factura.Admitidos(admision);
        if (!modos.Contains(modo)) return ErroresDeRecogida.ModoDeFacturaNoAdmitido(factura.Numero, modo, modos);
        if (modo == PayoffMode.Partial && pedido is not > 0m)
            return ErroresDeLineasDeCredito.Invalido("payoffs", $"La recogida parcial de la factura {factura.Numero} lleva un valor mayor que cero.");
        if (sinRecibo is not null) return ErroresDeRecogida.RecaudoSinConfigurar(factura.Numero, sinRecibo).ComoError();
        return null;
    }

    /// <summary>
    /// Una factura autorizada por la solicitud, a la fecha valor del desembolso con lo de ahora (como las cuentas y los créditos):
    /// <list type="number">
    /// <item>la factura sin saldo (se pagó por otro camino) se <b>omite</b>: no se recoge y el neto sube;</item>
    /// <item>Recaudo sin configurar bloquea con el motivo;</item>
    /// <item>lo de ahora: total, lo pendiente; parcial, el valor pedido mientras no lo cubra (si lo cubre, es un total). Se compara con lo proyectado: menos, por pagos;
    /// más, por un recibo anulado o una nota (otros movimientos). <c>MantenerLoProyectado</c> aplica lo proyectado topado en lo de ahora; la tolerancia bloquea
    /// como en un crédito.</item>
    /// </list>
    /// Los bloqueos van a <paramref name="hallados"/> y los avisos generales a <paramref name="avisos"/>.
    /// </summary>
    public static RecogidaDeFacturaEvaluada Evaluar(CreditApplicationPayoff fila, FacturasRecogibles facturas, bool mantenerLoProyectado, decimal? tolerancia,
        List<Error> hallados, List<string> avisos)
    {
        ArgumentNullException.ThrowIfNull(fila);
        ArgumentNullException.ThrowIfNull(facturas);
        var numero = fila.CommercialReceivableReference ?? fila.ProductNumber;
        var proyectado = fila.TieneProyeccion ? fila.Desglose() : DesgloseDeRecogida.Vacio;
        var base_ = new RecogidaDeFacturaEvaluada(numero, fila.Mode, 0m)
        {
            ProyectadaId = fila.Id, ModoPedido = fila.Mode, ValorPedido = fila.RequestedAmount, ConProyeccion = fila.TieneProyeccion, Proyectado = proyectado.Total,
        };
        if (facturas.Buscar(numero) is not { Pendiente: > 0m } factura)
        {
            var motivo = $"La factura {numero} ya no tiene saldo pendiente.";
            var aviso = base_.Proyectado > 0m ? $"{motivo} No se recoge: el neto a girar sube {ErroresDeRecogida.Pesos(base_.Proyectado)}." : $"{motivo} No se recoge.";
            avisos.Add(aviso);
            var cerrada = DiferenciaDeRecogida.Comparar(proyectado, DesgloseDeRecogida.Vacio, productoCerrado: true);
            return base_ with { Omitida = true, Modo = PayoffMode.Total, Diferencia = cerrada with { Causa = PayoffDifferenceCause.ProductClosed }, Avisos = [aviso] };
        }
        if (facturas.SinRecibo is { } sinRecibo) hallados.Add(ErroresDeRecogida.RecaudoSinConfigurar(numero, sinRecibo).ComoError());

        var parcial = fila.Mode == PayoffMode.Partial && fila.RequestedAmount is { } pedido && pedido < factura.Pendiente;
        var actual = parcial ? fila.RequestedAmount!.Value : factura.Pendiente;
        var diferencia = RecogidaEvaluada.SinDiferencia;
        if (fila.TieneProyeccion)
        {
            var d = DiferenciaDeRecogida.Comparar(proyectado, new DesgloseDeRecogida(Capital: actual));
            // Menos que lo proyectado: entraron pagos (un recibo de la caja); más: volvió saldo a la factura (un recibo anulado, una nota).
            if (d.HayDiferencia) diferencia = d with { Causa = actual < proyectado.Total ? PayoffDifferenceCause.Payments : PayoffDifferenceCause.Other };
        }
        var aplicar = mantenerLoProyectado && fila.TieneProyeccion ? Math.Min(proyectado.Total, actual) : actual;
        var avisosDeLaFactura = new List<string>();
        if (diferencia.HayDiferencia)
            avisosDeLaFactura.Add($"Recoger la factura {numero} cuesta {ErroresDeRecogida.Pesos(actual)} y la solicitud proyectó {ErroresDeRecogida.Pesos(proyectado.Total)} "
                                  + $"(diferencia de {ErroresDeRecogida.Pesos(diferencia.Total)}): {RequisitosDelDesembolso.TextoDeCausas(diferencia.Causa)}.");
        if (aplicar < actual)
        {
            var aviso = $"Se mantiene lo proyectado: a la factura {numero} se le aplican {ErroresDeRecogida.Pesos(aplicar)} y queda con un saldo de "
                        + $"{ErroresDeRecogida.Pesos(factura.Pendiente - aplicar)}.";
            avisosDeLaFactura.Add(aviso);
            avisos.Add(aviso);
        }
        var sobre = tolerancia is { } tope && fila.TieneProyeccion && Math.Abs(diferencia.Total) > tope;
        return base_ with
        {
            Modo = aplicar >= factura.Pendiente ? PayoffMode.Total : PayoffMode.Partial,
            AAplicar = aplicar,
            Actual = actual,
            Diferencia = diferencia,
            SobreLaTolerancia = sobre,
            Avisos = avisosDeLaFactura,
        };
    }

    /// <summary>Los cerrojos de Recaudo antes que los de Cartera (el deudor como pagador y, al anular, los recibos).</summary>
    public Task TomarCerrojosAsync(Guid deudor, IReadOnlyList<Guid> recibos, CancellationToken ct) => recaudo.TomarCerrojosAsync(deudor, recibos, ct);

    /// <summary>
    /// Recoge las facturas del desembolso <paramref name="operacion"/> pagadas por <paramref name="deudor"/> (las omitidas no) con un recibo sin caja de Recaudo, en la
    /// transacción del desembolso y con los cerrojos de Recaudo ya tomados. Si Recaudo no lo emite, el desembolso aborta con su código; lo aplicado a cada factura
    /// tiene que ser lo evaluado (<c>Lending.Payoff.AppliedDiffers</c>).
    /// </summary>
    public async Task<Result<IReadOnlyList<RecogidaDeFacturaAplicada>>> AplicarAsync(LoanOperation operacion, Guid deudor, Guid oficina, string? creditoNuevo,
        IReadOnlyList<RecogidaDeFacturaEvaluada> recogidas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operacion);
        ArgumentNullException.ThrowIfNull(recogidas);
        var aAplicar = recogidas.Where(r => !r.Omitida && r.AAplicar > 0m).ToList();
        if (aAplicar.Count == 0) return Result.Success<IReadOnlyList<RecogidaDeFacturaAplicada>>([]);
        var solicitud = new SolicitudDeRecogidaEnRecaudo(ClaveDelRecibo(operacion.PublicId), deudor, oficina, operacion.ValueDate,
            aAplicar.Select(r => new FacturaARecoger(r.Numero, r.AAplicar, Clave(operacion.PublicId, r.Numero))).ToList(), operacion.Number, creditoNuevo);
        var recibo = await recaudo.AplicarAsync(solicitud, ct);
        if (recibo.IsFailure) return Result.Failure<IReadOnlyList<RecogidaDeFacturaAplicada>>(recibo.Error);

        var aplicadas = new List<RecogidaDeFacturaAplicada>(aAplicar.Count);
        foreach (var r in aAplicar)
        {
            var clave = Clave(operacion.PublicId, r.Numero);
            var aplicado = recibo.Value.Invoices.Where(f => string.Equals(f.Key, clave, StringComparison.Ordinal)).Sum(f => f.Applied);
            if (aplicado != r.AAplicar)
                return Result.Failure<IReadOnlyList<RecogidaDeFacturaAplicada>>(ErroresDeRecogida.FacturaAplicadaDistinta(r.Numero, r.AAplicar, aplicado));
            aplicadas.Add(new RecogidaDeFacturaAplicada(r, clave, recibo.Value.ReceiptPublicId, recibo.Value.Number, aplicado));
        }
        return Result.Success<IReadOnlyList<RecogidaDeFacturaAplicada>>(aplicadas);
    }

    /// <summary>
    /// Una factura recogida como la lleva el hecho del desembolso (<see cref="PayoffDeductionV1"/>, T088): la clase <c>CommercialReceivable</c>, la factura, el
    /// recibo de Recaudo como su operación de aplicación, lo aplicado, la sucursal del recibo (la del crédito nuevo) y el deudor como pagador.
    /// </summary>
    public static PayoffDeductionV1 ComoDeduccion(RecogidaDeFacturaAplicada a, Guid sucursal)
    {
        ArgumentNullException.ThrowIfNull(a);
        return Deduccion(a.Recogida.Numero, a.Recibo, a.Aplicado, sucursal);
    }

    /// <summary>Una factura recogida viva como la lleva el hecho del desembolso (el giro nuevo, H6); la sucursal y el pagador los completa la emisión.</summary>
    public static PayoffDeductionV1 ComoDeduccion(FacturaDeRecogidaViva v)
    {
        ArgumentNullException.ThrowIfNull(v);
        return Deduccion(v.Numero, v.Recibo, v.Monto, null);
    }

    private static PayoffDeductionV1 Deduccion(string numero, Guid recibo, decimal monto, Guid? sucursal) => new()
    {
        ProductKind = PayoffDeductionV1.FacturaComercial,
        ReceivableRef = new ExternalDocumentRefV1 { Module = ModulosDeOrigen.Recaudo, PublicId = recibo, DocumentClass = ClaseDelDocumento, Number = numero },
        OperationPublicId = recibo,
        Amount = monto,
        BranchPublicId = sucursal,
    };

    /// <summary>
    /// Las facturas recogidas <b>vivas</b> (aplicadas y sin reversar) de los desembolsos <paramref name="desembolsos"/>, de sus filas <c>LND_DisbursementPayoffs</c>
    /// (las facturas nacieron con la 028: todas tienen fila). Las leen el giro nuevo (que vuelve a descontarlas, H6), la conciliación y la anulación del crédito.
    /// Sólo lee.
    /// </summary>
    public static async Task<IReadOnlyList<FacturaDeRecogidaViva>> VivasAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> desembolsos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(desembolsos);
        if (desembolsos.Count == 0) return [];
        var publicos = desembolsos.Distinct().ToList();
        var filas = await (from f in db.DisbursementPayoffs.AsNoTracking()
                           join o in db.LoanOperations.AsNoTracking() on f.DisbursementOperationId equals o.Id
                           where publicos.Contains(o.PublicId) && f.ProductKind == PayoffProductKind.CommercialReceivable && !f.Omitted && f.ReversedAt == null
                                 && f.ReceiptPublicId != null && f.AppliedAmount > 0m
                           orderby f.Id
                           select new { f.Id, Desembolso = o.PublicId, f.CommercialReceivableReference, f.ProductNumber, f.ReceiptPublicId, f.IdempotencyKey, f.AppliedAmount })
            .ToListAsync(ct);
        return filas.Select(f => new FacturaDeRecogidaViva(f.Id, f.Desembolso, f.CommercialReceivableReference ?? f.ProductNumber, f.ReceiptPublicId!.Value,
            f.IdempotencyKey, f.AppliedAmount)).ToList();
    }

    /// <summary>
    /// Anula los recibos de las facturas <paramref name="vivas"/> (la anulación del crédito nuevo antes del giro, T087): un recibo por desembolso, sin aprobación y con
    /// el espejo exacto de su asiento; la factura vuelve a quedar con saldo. El primero que no se anula aborta con su código. Devuelve la clave de cada factura y el
    /// documento de anulación de su recibo.
    /// </summary>
    public async Task<Result<IReadOnlyList<(string Clave, Guid Anulacion)>>> ReversarAsync(IReadOnlyList<FacturaDeRecogidaViva> vivas, string motivo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(vivas);
        var hechos = new List<(string, Guid)>();
        foreach (var recibo in vivas.GroupBy(v => v.Recibo))
        {
            var anulado = await recaudo.ReversarAsync(recibo.Key, motivo, ct);
            if (anulado.IsFailure) return Result.Failure<IReadOnlyList<(string, Guid)>>(anulado.Error);
            hechos.AddRange(recibo.Select(v => (v.Clave, anulado.Value.VoidingPublicId)));
        }
        return Result.Success<IReadOnlyList<(string, Guid)>>(hechos);
    }

    /// <summary>La fila <c>LND_DisbursementPayoffs</c> de una factura recogida (también la omitida), con lo aplicado y el recibo.</summary>
    public static DisbursementPayoff Fila(long operacionId, Guid desembolso, RecogidaDeFacturaEvaluada x, RecogidaDeFacturaAplicada? aplicada, string? reconocio,
        DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(x);
        var fila = DisbursementPayoff.Evaluar(operacionId, x.ProyectadaId, ProductoRecogido.Factura(x.Numero, x.Numero), x.Modo, null, null, x.Proyectado, x.Actual,
            x.Diferencia, x.Omitida, Clave(desembolso, x.Numero), x.PorReconocer ? reconocio : null, x.PorReconocer && reconocio is not null ? ahora : null);
        if (aplicada is not null) fila.Aplicar(aplicada.Aplicado, recibo: aplicada.Recibo);
        return fila;
    }
}
