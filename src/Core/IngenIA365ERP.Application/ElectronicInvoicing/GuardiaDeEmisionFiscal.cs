using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;

namespace IngenIA365ERP.Application.ElectronicInvoicing;

/// <summary>Los tres veredictos de <see cref="GuardiaDeEmisionFiscal"/> (contracts/dian.md §10.4). I3 sólo produce los dos últimos. (nuevo)</summary>
public enum VeredictoFiscal
{
    /// <summary>Se emite electrónicamente por el canal y con la resolución del veredicto (I4, US8).</summary>
    Electronic = 1,

    /// <summary>La cooperativa no está obligada a la fecha: comprobante no electrónico y su nota.</summary>
    NonElectronic = 2,

    /// <summary>No se confirma ninguna venta fiscal; los motivos dicen qué falta, dónde y con qué permiso.</summary>
    Blocked = 3,
}

/// <summary>Un motivo de <see cref="VeredictoFiscal.Blocked"/>: qué falta, dónde se completa y con qué permiso (nunca sin salida). (nuevo)</summary>
public sealed record MotivoDeBloqueoFiscal(string Code, string Message, string Where, string? Permission);

/// <summary>
/// El veredicto con lo que permite: las clases que confirman y, si está bloqueado, los motivos (<c>data.missing[]</c> de
/// <c>ElectronicInvoicing.NotReady</c>). <see cref="Canal"/> y <see cref="Resolucion"/> los llena I4 con <c>Electronic</c>. (nuevo)
/// </summary>
public sealed record EvaluacionFiscal(
    VeredictoFiscal Veredicto,
    IReadOnlyList<DocumentClass> ClasesAdmitidas,
    IReadOnlyList<MotivoDeBloqueoFiscal> Motivos,
    string? Canal = null,
    string? Resolucion = null)
{
    /// <summary>¿Admite confirmar un documento de <paramref name="clase"/>?</summary>
    public bool Admite(DocumentClass clase) => Veredicto != VeredictoFiscal.Blocked && ClasesAdmitidas.Contains(clase);
}

/// <summary>
/// La decisión única de emitir (feature 012, I3, T609; contracts/dian.md §10.4; patrón de <c>GuardiaDeMetodos.Evaluar</c>): la
/// consultan la confirmación de las ventas (<c>ReglasDeConfirmacionDeVenta</c>) y la apertura de la sesión de caja (aviso temprano,
/// sin bloquear). La firma es la definitiva, <c>Evaluar(fecha, tipoDeDocumento, caja?)</c>; esta versión mínima de I3 da <b>sólo dos
/// veredictos</b>:
/// <list type="bullet">
/// <item><see cref="VeredictoFiscal.NonElectronic"/> cuando <c>Dian.ObligadaAFacturar</c> está apagado a la fecha y la clase del tipo es
/// de venta: confirman el comprobante no electrónico y su nota;</item>
/// <item><see cref="VeredictoFiscal.Blocked"/> con el motivo «la cooperativa está obligada y la entrega I4 no está activa» en cualquier
/// otro caso de venta fiscal.</item>
/// </list>
/// US8 (I4) la amplía con <c>Electronic(canal, resolución)</c> y el resto de motivos <b>sin cambiar la firma</b>. (nuevo)
/// </summary>
public sealed class GuardiaDeEmisionFiscal(ILectorDeParametros parametros)
{
    public const string ObligadaSinI4Code = "ElectronicInvoicing.I4NotActive";

    /// <summary>Las clases del comprobante no electrónico: las únicas que confirma una cooperativa no obligada.</summary>
    public static readonly IReadOnlyList<DocumentClass> ClasesNoElectronicas = [DocumentClass.NonElectronicSalesReceipt, DocumentClass.NonElectronicSalesNote];

    /// <summary>El veredicto para <paramref name="tipo"/> a <paramref name="fecha"/> (y en <paramref name="caja"/>, que I4 usará para el canal).</summary>
    public async Task<EvaluacionFiscal> EvaluarAsync(DateOnly fecha, InventoryDocumentType tipo, CashRegister? caja, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        var leido = await parametros.LeerComoAsync<bool>(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.ObligadaAFacturar, fecha, ct: ct);
        var obligada = leido.IsFailure || leido.Value;
        return Evaluar(obligada, tipo.Class);
    }

    /// <summary>
    /// La regla pura: sin obligación y clase de venta → <see cref="VeredictoFiscal.NonElectronic"/>; en otro caso →
    /// <see cref="VeredictoFiscal.Blocked"/> (la emisión electrónica llega con I4).
    /// </summary>
    public static EvaluacionFiscal Evaluar(bool obligada, DocumentClass clase)
    {
        var deVenta = ClasesDeDocumento.De(clase).Group == DocumentClassGroup.Sales;
        if (!obligada && deVenta) return new EvaluacionFiscal(VeredictoFiscal.NonElectronic, ClasesNoElectronicas, []);
        return new EvaluacionFiscal(VeredictoFiscal.Blocked, [],
        [
            new MotivoDeBloqueoFiscal(ObligadaSinI4Code,
                "La cooperativa está obligada a facturar electrónicamente y la entrega de documentos electrónicos (I4) no está activa.",
                "Parámetros › Facturación electrónica (Dian.ObligadaAFacturar)", ParametrosDeFacturacionElectronica.Permiso),
        ]);
    }
}
