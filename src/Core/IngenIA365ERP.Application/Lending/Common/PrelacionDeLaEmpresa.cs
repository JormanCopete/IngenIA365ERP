using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// La prelación con que se reparte un pago (2026-11-16, pedido del dueño): la de la línea del crédito con el orden de rubros de la empresa
/// (<see cref="Prelacion"/>), y el orden que el pago <b>sella</b> en su decisión (<see cref="Rubros"/>); nulo si mandó la línea. (nuevo)
/// </summary>
public sealed record PrelacionDelPago(Prelacion Prelacion, OrdenDeRubros? Rubros)
{
    /// <summary>La de la línea, sin orden de la empresa (un programa sin el servicio, o lo de siempre).</summary>
    public static PrelacionDelPago DeLaLinea(Prelacion deLaLinea) => new(deLaLinea, null);
}

/// <summary>
/// <b>Un solo sitio</b> que decide con qué orden de rubros reparte un pago (2026-11-16, pedido del dueño; <c>Pagos.PrelacionDeRubros</c>):
/// <list type="number">
/// <item>si la línea del crédito tiene su <b>propia</b> lista de conceptos (<see cref="Prelacion.TieneListaPropia"/>, sólo por la API), manda la línea, que
/// es más específica, y no se sella nada;</item>
/// <item>si no, el parámetro de la empresa a la fecha valor del pago (ámbito general; ilegible o sin vigencia, el de fábrica,
/// <see cref="OrdenDeRubros.DeFabrica"/>), que el pago sella en su decisión.</item>
/// </list>
/// El orden de las cuotas, el suspenso con su concepto, los gastos sin constancia al final y la cuota específica fuera de orden siguen siendo los de la
/// línea (<see cref="Prelacion.ConRubros"/>). <b>Se sella y no se relee</b>: el escritor de parámetros admite vigencias con fecha pasada
/// (<c>AddParameterVersionCommandHandler</c> sólo rechaza que se crucen), así que releerlo al reconstruir cambiaría pagos ya aplicados; la
/// reconstrucción repite lo sellado y un pago sin el orden (anterior al parámetro) se reparte con la de la línea. Lo usan la aplicación de pagos (todo
/// lo que pasa por el contrato: caja, nómina, libranza, canal, recogida, cruce del retiro) y el saldo a favor aplicado como abono. (nuevo)
/// </summary>
public sealed class PrelacionDeLaEmpresa(ReglasDeParametrosDeCartera parametros)
{
    /// <summary>El orden de rubros de la empresa a <paramref name="fecha"/>; ilegible o sin vigencia, el de fábrica.</summary>
    public async Task<OrdenDeRubros> OrdenAsync(DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.PagosPrelacionDeRubros, fecha, ct: ct);
        return leido.IsSuccess && OrdenDeRubros.Leer(leido.Value.Texto) is { } orden ? orden : OrdenDeRubros.DeFabrica;
    }

    /// <summary>La prelación de un pago con fecha valor <paramref name="fecha"/> sobre un crédito cuya línea sella <paramref name="deLaLinea"/>.</summary>
    public async Task<PrelacionDelPago> ParaElPagoAsync(Prelacion deLaLinea, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(deLaLinea);
        if (deLaLinea.TieneListaPropia) return PrelacionDelPago.DeLaLinea(deLaLinea);
        var orden = await OrdenAsync(fecha, ct);
        return new PrelacionDelPago(deLaLinea.ConRubros(orden), orden);
    }
}
