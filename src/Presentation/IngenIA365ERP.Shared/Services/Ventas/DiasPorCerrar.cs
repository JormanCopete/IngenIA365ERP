namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>Una sesión de caja abierta que impide cerrar el día: su caja y su cajero, con el enlace a la sesión. (nuevo)</summary>
public sealed record SesionQueImpideElCierre(Guid CashSessionPublicId, string CashRegisterCode, string CashierName, DateTime OpenedAt);

/// <summary>Un día de un punto con sesiones y sin cierre vigente: cuántas sesiones tuvo y cuáles siguen abiertas. (nuevo)</summary>
public sealed record DiaPorCerrar(Guid PointOfSalePublicId, string PointOfSaleCode, string PointOfSaleName, DateOnly OperatingDate, int Sessions,
    IReadOnlyList<SesionQueImpideElCierre> OpenSessions)
{
    public bool SePuedeCerrar => OpenSessions.Count == 0;
}

/// <summary>
/// Los días que faltan por cerrar en <c>/pos/cierre-del-dia</c> (pedido del dueño del 2026-11-11, tras la capacitación de COOFLOPAL: la
/// pantalla sólo listaba los cierres hechos y no decía qué faltaba ni por qué no se podía cerrar). Un día está pendiente si el punto tuvo
/// sesiones con esa fecha operativa y no tiene un cierre vigente (<c>Status = 1</c>; uno reabierto vuelve a quedar pendiente). Puro: recibe
/// lo que la pantalla ya consultó. (nuevo)
/// </summary>
public static class DiasPorCerrar
{
    /// <summary>El estado «cerrado» de <c>DayCloseStatus</c>.</summary>
    public const int CierreVigente = 1;

    public static IReadOnlyList<DiaPorCerrar> Calcular(IEnumerable<SesionDeCajaDto> sesiones, IEnumerable<CierreDelDiaDto> cierres)
    {
        var cerrados = cierres.Where(c => c.Status == CierreVigente)
            .Select(c => (c.PointOfSale.PublicId, c.OperatingDate)).ToHashSet();
        return sesiones
            .GroupBy(s => (s.PointOfSale.PointOfSalePublicId, s.OperatingDate))
            .Where(g => !cerrados.Contains(g.Key))
            .Select(g =>
            {
                var punto = g.First().PointOfSale;
                var abiertas = g.Where(s => s.Status == TextosDeVentas.SesionAbierta).OrderBy(s => s.OpenedAt)
                    .Select(s => new SesionQueImpideElCierre(s.CashSessionPublicId, s.CashRegister.Code, s.Cashier.Name, s.OpenedAt)).ToList();
                return new DiaPorCerrar(punto.PointOfSalePublicId, punto.Code, punto.Name, g.Key.OperatingDate, g.Count(), abiertas);
            })
            .OrderByDescending(d => d.OperatingDate).ThenBy(d => d.PointOfSaleCode, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Por qué «Cerrar el día» no se puede pulsar, o nulo si se puede: falta el punto o la fecha, el día ya está cerrado o tiene sesiones
    /// abiertas (con caja y cajero), o no tuvo sesiones.
    /// </summary>
    public static string? MotivoParaNoCerrar(Guid? punto, DateOnly? fecha, IReadOnlyList<DiaPorCerrar> pendientes, IEnumerable<CierreDelDiaDto> cierres)
    {
        if (punto is null) return "Elija el punto de venta para cerrar su día.";
        if (fecha is null) return "Elija la fecha operativa.";
        if (cierres.Any(c => c.PointOfSale.PublicId == punto && c.OperatingDate == fecha && c.Status == CierreVigente))
            return $"El día {fecha:dd/MM/yyyy} ya está cerrado. Para volver a cerrarlo, reábralo con su motivo.";
        var dia = pendientes.FirstOrDefault(d => d.PointOfSalePublicId == punto && d.OperatingDate == fecha);
        if (dia is null) return $"El {fecha:dd/MM/yyyy} no tuvo sesiones de caja en este punto: no hay nada que cerrar.";
        if (!dia.SePuedeCerrar)
            return "Primero cierre " + (dia.OpenSessions.Count == 1 ? "la sesión abierta: " : "las sesiones abiertas: ")
                   + string.Join("; ", dia.OpenSessions.Select(s => $"caja {s.CashRegisterCode} de {s.CashierName}")) + ".";
        return null;
    }
}
