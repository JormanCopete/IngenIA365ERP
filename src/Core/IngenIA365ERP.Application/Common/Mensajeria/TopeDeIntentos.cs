using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Inventory.Parameters;

namespace IngenIA365ERP.Application.Common.Mensajeria;

/// <summary>
/// El tope de intentos transitorios de la cooperativa (feature 023, D3; research R6): <c>Dian.TopeDeIntentos</c> para los documentos
/// electrónicos y su seguimiento, <c>Integracion.TopeDeIntentos</c> para las entregas de la bandeja. Lee a la fecha de hoy por el lector
/// único de parámetros; si no se puede leer, el defecto del catálogo (12). Scoped. (nuevo)
/// </summary>
public sealed class TopeDeIntentos(ILectorDeParametros parametros, IDateTimeService reloj)
{
    public Task<int> DeDocumentosAsync(CancellationToken ct) =>
        LeerAsync(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.TopeDeIntentos,
            ParametrosDeFacturacionElectronica.Definiciones, ct);

    public Task<int> DeIntegracionAsync(CancellationToken ct) =>
        LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.IntegracionTopeDeIntentos, ParametrosDeInventario.Definiciones, ct);

    private async Task<int> LeerAsync(string modulo, string clave, IEnumerable<DefinicionDeParametro> catalogo, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(reloj.UtcNow);
        var leido = await parametros.LeerAsync(modulo, clave, hoy, ct: ct);
        var valor = leido.IsSuccess ? leido.Value.Como<int>() : 0;
        if (valor >= 1) return valor;
        return int.Parse(catalogo.First(d => d.Clave == clave).DefectoSeguro, CultureInfo.InvariantCulture);
    }
}
