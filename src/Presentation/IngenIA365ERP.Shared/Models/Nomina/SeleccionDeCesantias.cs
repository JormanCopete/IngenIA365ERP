using IngenIA365ERP.Shared.Services.Nomina;

namespace IngenIA365ERP.Shared.Models.Nomina;

/// <summary>
/// Qué corrida de cesantías queda elegida cuando la lista del año se vuelve a cargar
/// (<c>Pages/Nomina/CesantiasAnuales.razor</c>). El orden es una regla, no un detalle: la recién
/// calculada manda, después la que ya estaba elegida si sigue en la lista, después la del enlace
/// <c>?corrida=</c> —una sola vez, al llegar—, después la vigente (borrador o aprobada) y, si no
/// hay nada de eso, la más reciente. Hasta el 2026-09-21 el parámetro del enlace valía toda la vida
/// de la página y calcular o recalcular habiendo llegado desde el comprobante contable devolvía la
/// selección a la corrida del enlace (la reversada) y pintaba bajo ella los excluidos del borrador nuevo.
/// </summary>
public static class SeleccionDeCesantias
{
    public static LiquidacionCesantiasDto? Elegir(
        IReadOnlyList<LiquidacionCesantiasDto> lista,
        Guid? recienCalculada,
        Guid? elegidaAntes,
        Guid? delEnlace)
    {
        return Buscar(recienCalculada)
            ?? Buscar(elegidaAntes)
            ?? Buscar(delEnlace)
            ?? lista.FirstOrDefault(l => l.EsBorrador || l.EstaAprobada)
            ?? lista.FirstOrDefault();

        LiquidacionCesantiasDto? Buscar(Guid? id) => id is { } c ? lista.FirstOrDefault(l => l.RunPublicId == c) : null;
    }
}
