namespace IngenIA365ERP.Domain.Inventory.Tracking;

/// <summary>El valor de <c>Ventas.LoteVencido</c> (FR-026): vender un lote vencido se bloquea o se advierte. No se guarda. (nuevo)</summary>
public enum PoliticaDeLoteVencido { Bloquear = 1, Advertir = 2 }

/// <summary>El veredicto sobre un lote: vigente, vencido y bloqueado, o vencido con aviso. (nuevo)</summary>
public enum VeredictoDeLote { Vigente = 1, Bloqueado = 2, Advertido = 3 }

/// <summary>Un lote con su existencia disponible en la bodega de la salida, en unidad base. (nuevo)</summary>
public sealed record LoteDisponible(int LotId, string Codigo, DateOnly? Vencimiento, decimal Disponible);

/// <summary>
/// Lo que entra al selector: los lotes del producto en la bodega, la cantidad que sale (positiva, en unidad base), la fecha de hoy
/// de la cooperativa (<c>HoyLocal</c>), la política de <c>Ventas.LoteVencido</c> y si la salida admite vencidos sin política (la
/// baja por vencimiento, que existe justamente para sacarlos). (nuevo)
/// </summary>
public sealed record PedidoDeLotes(IReadOnlyList<LoteDisponible> Lotes, decimal Cantidad, DateOnly HoyLocal, PoliticaDeLoteVencido Politica)
{
    public bool AdmiteVencidos { get; init; }
}

/// <summary>Un lote en el orden de salida, con la marca de vencido. (nuevo)</summary>
public sealed record LoteOrdenado(LoteDisponible Lote, bool Vencido);

/// <summary>La parte de la salida que toma un lote. (nuevo)</summary>
public sealed record AsignacionDeLote(LoteDisponible Lote, decimal Cantidad, bool Vencido);

/// <summary>
/// El reparto: los lotes que toma la salida en orden, lo que falta si entre todos no alcanza y los lotes con existencia que la
/// política dejó fuera por vencidos. (nuevo)
/// </summary>
public sealed record ResultadoDeLotes(IReadOnlyList<AsignacionDeLote> Asignaciones, decimal Faltante, IReadOnlyList<LoteDisponible> Excluidos)
{
    /// <summary>El lote que se sugiere primero (el que vence antes); nulo si no hay ninguno elegible.</summary>
    public AsignacionDeLote? Sugerido => Asignaciones.Count > 0 ? Asignaciones[0] : null;

    public bool Completo => Faltante == 0m;

    /// <summary>Con <c>Advertir</c>, la salida toma algún lote vencido: la aplicación lo devuelve en <c>warnings[]</c>.</summary>
    public bool HayVencidos => Asignaciones.Any(a => a.Vencido);
}

/// <summary>
/// El selector de lotes (feature 012, I6, T915; FR-026, US15-4; data-model §1.11): puro y sin IO. Ordena los lotes con existencia
/// por el que <b>vence primero</b> (FEFO), luego por código y por Id, con los lotes sin vencimiento al final; y reparte una salida entre
/// ellos en ese orden cuando uno no alcanza. Un lote está <b>vencido</b> cuando su fecha de vencimiento es anterior a
/// <c>HoyLocal</c>: la fecha de vencimiento es el último día en que sirve. Con <see cref="PoliticaDeLoteVencido.Bloquear"/> los
/// vencidos quedan fuera (y se informan); con <see cref="PoliticaDeLoteVencido.Advertir"/> entran marcados. La política la lee la
/// aplicación y llega por parámetro; <see cref="PoliticaDesde"/> interpreta su texto. Lo usan <c>ReglasDeSeguimiento</c> (T923) y el
/// POS (T929). (nuevo)
/// </summary>
public static class SelectorDeLotes
{
    /// <summary>El código con que la aplicación rechaza vender un lote vencido con <c>Bloquear</c> (T908). (nuevo)</summary>
    public const string CodigoLoteVencido = "Inventory.Lot.Expired";

    public static bool EstaVencido(DateOnly? vencimiento, DateOnly hoyLocal) => vencimiento is { } v && v < hoyLocal;

    /// <summary>El veredicto sobre un lote que la persona eligió a mano.</summary>
    public static VeredictoDeLote Veredicto(DateOnly? vencimiento, DateOnly hoyLocal, PoliticaDeLoteVencido politica) =>
        !EstaVencido(vencimiento, hoyLocal) ? VeredictoDeLote.Vigente
        : politica == PoliticaDeLoteVencido.Bloquear ? VeredictoDeLote.Bloqueado
        : VeredictoDeLote.Advertido;

    /// <summary>Los lotes elegibles —con existencia y, con <c>Bloquear</c>, sin vencer— en el orden en que sale la mercancía.</summary>
    public static IReadOnlyList<LoteOrdenado> Ordenar(IEnumerable<LoteDisponible> lotes, DateOnly hoyLocal, PoliticaDeLoteVencido politica,
        bool admiteVencidos = false)
    {
        ArgumentNullException.ThrowIfNull(lotes);
        return lotes
            .Where(l => l.Disponible > 0m)
            .Select(l => new LoteOrdenado(l, EstaVencido(l.Vencimiento, hoyLocal)))
            .Where(l => !l.Vencido || admiteVencidos || politica == PoliticaDeLoteVencido.Advertir)
            .OrderBy(l => l.Lote.Vencimiento is null)
            .ThenBy(l => l.Lote.Vencimiento)
            .ThenBy(l => l.Lote.Codigo, StringComparer.Ordinal)
            .ThenBy(l => l.Lote.LotId)
            .ToList();
    }

    /// <summary>Reparte la salida entre los lotes elegibles, en orden; lo que no alcanza queda en <see cref="ResultadoDeLotes.Faltante"/>.</summary>
    public static ResultadoDeLotes Repartir(PedidoDeLotes pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        if (pedido.Cantidad <= 0m) throw new ArgumentException("La cantidad que sale es positiva.", nameof(pedido));

        var ordenados = Ordenar(pedido.Lotes, pedido.HoyLocal, pedido.Politica, pedido.AdmiteVencidos);
        var asignaciones = new List<AsignacionDeLote>();
        var restante = pedido.Cantidad;
        foreach (var l in ordenados)
        {
            if (restante == 0m) break;
            var toma = Math.Min(restante, l.Lote.Disponible);
            asignaciones.Add(new AsignacionDeLote(l.Lote, toma, l.Vencido));
            restante -= toma;
        }

        var excluidos = pedido.Lotes
            .Where(l => l.Disponible > 0m && ordenados.All(o => o.Lote.LotId != l.LotId))
            .OrderBy(l => l.Vencimiento).ThenBy(l => l.Codigo, StringComparer.Ordinal)
            .ToList();
        return new ResultadoDeLotes(asignaciones, restante, excluidos);
    }

    /// <summary>El valor del parámetro <c>Ventas.LoteVencido</c> («Bloquear», «Advertir»); cualquier otro es un error visible.</summary>
    public static PoliticaDeLoteVencido PoliticaDesde(string valor) =>
        Enum.TryParse<PoliticaDeLoteVencido>(valor, ignoreCase: true, out var p) && Enum.IsDefined(p)
            ? p
            : throw new ArgumentOutOfRangeException(nameof(valor), valor, "Ventas.LoteVencido admite «Bloquear» o «Advertir».");
}
