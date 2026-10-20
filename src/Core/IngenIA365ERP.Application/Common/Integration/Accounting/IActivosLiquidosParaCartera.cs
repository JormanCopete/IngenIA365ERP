using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Integration.Accounting;

/// <summary>El saldo contable (débitos menos créditos contabilizados) de las cuentas cuyo código empieza por <see cref="Prefijo"/>.</summary>
public sealed record SaldoPorPrefijoDto(string Prefijo, decimal Saldo);

/// <summary>
/// Lo que Cartera le pregunta a Contabilidad para medir los activos líquidos del indicador de riesgo de liquidez (feature 014, respuesta 99 B del dueño, E250): el
/// saldo por prefijo de código de cuenta que fija la entidad (disponible, inversiones negociables). Vive con su puerto hermano
/// <see cref="IContabilidadParaCartera"/> en el espacio de la plataforma: Cartera no conoce cuentas ni asientos, sólo pide cifras por los códigos que la entidad
/// configura. Sólo lee. (nuevo)
/// </summary>
public interface IActivosLiquidosParaCartera
{
    /// <summary>
    /// El saldo a <paramref name="fecha"/> (movimientos contabilizados hasta ese día) de cada prefijo pedido, en el orden pedido; un prefijo sin cuentas con
    /// movimiento devuelve cero.
    /// </summary>
    Task<Result<IReadOnlyList<SaldoPorPrefijoDto>>> SaldosPorPrefijosAsync(IReadOnlyList<string> prefijos, DateOnly fecha, CancellationToken ct);
}
