using IngenIA365ERP.Domain.Common.Parametros;
using static IngenIA365ERP.Domain.Common.Parametros.CatalogoDeParametros;
using static IngenIA365ERP.Domain.Common.Parametros.EntregaDelComercio;

namespace IngenIA365ERP.Domain.Transport;

/// <summary>
/// Catálogo cerrado de las claves <c>TRN</c> (pedido del dueño del 2026-11-18, puesta en marcha de COOFLOPAL): el interruptor del aviso al
/// sistema externo. Con <see cref="AvisoAlSistemaExterno"/> apagado (el defecto) los cargos de servicios se registran y se pagan igual, pero
/// <b>no nace ningún aviso</b>: lo pagado mientras estuvo apagado no se avisa nunca, ni al encenderlo. Encendido, avisa cada cargo que quede
/// pagado desde ese momento. Lo lee un solo sitio, <c>EmisionDeSolicitudDeConector</c>. (nuevo)
/// </summary>
public static class ParametrosDeTransporte
{
    public const string Modulo = "TRN";

    public const string AvisoAlSistemaExterno = "Transporte.AvisoAlSistemaExterno";

    public static IReadOnlyList<DefinicionDeParametro> Definiciones { get; } =
    [
        SiNo(Modulo, AvisoAlSistemaExterno,
            "Avisar al sistema externo del proveedor (por su conector) cuando el servicio de un vehículo queda pagado. Apagado, no se avisa nada y lo pagado mientras tanto no se avisa después.",
            false, I1),
    ];
}
