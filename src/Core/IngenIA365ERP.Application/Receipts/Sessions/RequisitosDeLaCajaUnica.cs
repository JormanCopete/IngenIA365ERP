using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Sessions;

/// <summary>
/// Lo que la caja única necesita para funcionar (2026-11-25, pedido del dueño: «parámetros simples y seguros»). Lo pregunta la regla del parámetro
/// <c>Caja.SesionUnica</c> antes de encenderlo, para que la caja única no se encienda a medias:
/// <list type="bullet">
/// <item>al menos una caja de Recaudo activa unida a una caja del punto de venta (sin ninguna, encenderla no cambia nada y confunde);</item>
/// <item>la matriz contable de Recaudo con regla para el rol <c>PuenteDelPuntoDeVenta</c> del movimiento de caja, salvo que Contabilidad no esté iniciada,
/// que el módulo no pase a contabilidad o que la política ante la falta de regla sea avisar: sin ella, el ingreso del efectivo del punto de venta a la sesión
/// de Recaudo no se puede contabilizar.</item>
/// </list>
/// (nuevo)
/// </summary>
public static class RequisitosDeLaCajaUnica
{
    public const string SinCajasUnidas =
        "Ninguna caja de Recaudo está unida a una caja del punto de venta: únala en Recaudo → Configuración → Cajas y cajeros, botón «Punto de venta».";

    public const string SinCuentaPuente =
        "La matriz contable de Recaudo no tiene la cuenta del rol «Puente de la caja del punto de venta»: la contadora la asigna en Contabilidad → Recaudo → "
        + "Matriz contable, con la misma cuenta de la caja fuerte en la matriz de Comercial.";

    /// <summary>Lo que falta, en palabras de la pantalla; vacío si la caja única puede encenderse desde <paramref name="fecha"/>.</summary>
    public static async Task<IReadOnlyList<string>> FaltantesAsync(IApplicationDbContext db, IContabilidadParaRecaudo? contabilidad, DateOnly fecha,
        CancellationToken ct)
    {
        var faltan = new List<string>();
        var unidas = await db.CashBoxes.AsNoTracking()
            .AnyAsync(c => c.Status == ReceiptsCatalogStatus.Active && c.PosCashRegisterPublicId != null, ct);
        if (!unidas) faltan.Add(SinCajasUnidas);

        if (contabilidad is not null)
        {
            var evaluacion = await contabilidad.EvaluarAsync([new CombinacionDeHecho(ClavesDeRecaudo.Codigo, MovimientoDeCajaDeRecaudoRegistradoV1.Type,
                new Dictionary<string, string>(StringComparer.Ordinal) { [ConfirmacionDeRecibo.ClaveDelRol] = RolesDeRecaudo.PuenteDelPuntoDeVenta }, fecha)], ct);
            if (!evaluacion.Postable && string.Equals(evaluacion.Policy, PoliticaBloquear, StringComparison.OrdinalIgnoreCase)) faltan.Add(SinCuentaPuente);
        }
        return faltan;
    }

    /// <summary><c>Contabilidad.FaltaDeRegla = Bloquear</c>: la falta de regla detiene la operación (con «Advertir» se aplica y se avisa).</summary>
    private const string PoliticaBloquear = "Bloquear";
}
