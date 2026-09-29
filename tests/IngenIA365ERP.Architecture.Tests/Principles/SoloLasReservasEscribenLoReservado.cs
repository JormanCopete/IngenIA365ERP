using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, I6, T869 (FR-033, FR-052; data-model §3.2, §3.6, §14; SC-006): lo reservado tiene <b>un solo escritor</b>,
/// <c>Application/Inventory/Sales/Reservas/ReservasDeInventario</c>, que crea las reservas de un pedido, las consume, las libera y las vence en
/// la misma transacción que actualiza <c>INV_StockBalances.Reserved</c> (la fila exclusiva del cerrojo). La reconstrucción
/// (<c>RebuildInventoryProjectionsCommand</c>) recalcula <c>Reserved</c> desde las reservas vivas y la verificación lo compara: otro escritor
/// descuadraría el disponible (<c>Physical − Reserved</c>) sin que nada lo dijera hasta la verificación nocturna.
/// <list type="bullet">
/// <item>fuera de esos dos archivos nadie asigna <c>.Reserved</c> (<c>=</c>, <c>+=</c>, <c>-=</c>) en Application ni en Persistence;</item>
/// <item>fuera de <c>ReservasDeInventario</c> nadie cambia <c>Reservation.Status</c> (<c>Status = ReservationStatus.…</c>), crea una
/// <c>Reservation</c> ni escribe el conjunto <c>Reservations</c> (agregar, adjuntar, actualizar, borrar, <c>ExecuteUpdate</c>).</item>
/// </list>
/// Mismo estilo que <see cref="NadieEscribeElKardexFueraDelRegistro"/>: expresiones sobre la fuente sin comentarios. (nuevo)
/// </summary>
public class SoloLasReservasEscribenLoReservado
{
    /// <summary>El único escritor de las reservas y de lo reservado.</summary>
    public const string EscritorDeLasReservas = "ReservasDeInventario.cs";

    /// <summary>La reconstrucción, que también asigna <c>Reserved</c> (desde las reservas vivas).</summary>
    private const string Reconstruccion = "RebuildInventoryProjectionsCommand.cs";

    private static readonly Regex AsignaReservado = new(@"\.\s*Reserved\s*(\+|-)?=(?!=)", RegexOptions.Compiled);
    private static readonly Regex CambiaEstado = new(@"\bStatus\s*=\s*ReservationStatus\s*\.", RegexOptions.Compiled);
    private static readonly Regex CreaReserva = new(@"\bnew\s+Reservation\s*[({]", RegexOptions.Compiled);
    private static readonly Regex EscribeElConjunto = new(
        @"\.Reservations\s*\.\s*(Add|AddRange|AddAsync|AddRangeAsync|Attach|AttachRange|Update|UpdateRange|Remove|RemoveRange|ExecuteUpdate|ExecuteUpdateAsync|ExecuteDelete|ExecuteDeleteAsync)\b",
        RegexOptions.Compiled);

    private static bool DeLaApp(string archivo) =>
        archivo.Contains($"{Path.DirectorySeparatorChar}IngenIA365ERP.Application{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || archivo.Contains($"{Path.DirectorySeparatorChar}IngenIA365ERP.Persistence{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    [Fact]
    public void Solo_las_reservas_y_la_reconstruccion_asignan_lo_reservado()
    {
        var root = RepoPath.FindRepoRoot();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var infractores = new List<string>();
        foreach (var archivo in RepoPath.ProductionCSharpFiles().Where(DeLaApp))
        {
            var nombre = Path.GetFileName(archivo);
            if (!AsignaReservado.IsMatch(FuenteSinComentarios.Leer(archivo))) continue;
            if (nombre is EscritorDeLasReservas or Reconstruccion) vistos.Add(nombre);
            else infractores.Add($"{Path.GetRelativePath(root, archivo)}: asigna StockBalance.Reserved fuera de ReservasDeInventario");
        }

        Assert.True(vistos.Contains(EscritorDeLasReservas), "ReservasDeInventario escribe Reserved desde T877: si cambió la forma, actualizá la prueba.");
        Assert.True(vistos.Contains(Reconstruccion), "La reconstrucción recalcula Reserved desde T877: si cambió la forma, actualizá la prueba.");
        Assert.True(infractores.Count == 0,
            "Lo reservado escrito fuera de ReservasDeInventario y la reconstrucción (FR-033, T877):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_las_reservas_cambian_el_estado_crean_o_escriben_reservas()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();
        var escritorVisto = false;
        foreach (var archivo in RepoPath.ProductionCSharpFiles())
        {
            var nombre = Path.GetFileName(archivo);
            var texto = FuenteSinComentarios.Leer(archivo);
            var relativo = Path.GetRelativePath(root, archivo);
            if (nombre == EscritorDeLasReservas)
            {
                escritorVisto = CambiaEstado.IsMatch(texto) && EscribeElConjunto.IsMatch(texto);
                continue;
            }
            if (nombre == "Reservation.cs") continue; // la entidad declara su estado inicial
            if (CambiaEstado.IsMatch(texto)) infractores.Add($"{relativo}: cambia Reservation.Status fuera de ReservasDeInventario");
            if (CreaReserva.IsMatch(texto)) infractores.Add($"{relativo}: crea una Reservation fuera de ReservasDeInventario");
            if (EscribeElConjunto.IsMatch(texto)) infractores.Add($"{relativo}: escribe INV_Reservations fuera de ReservasDeInventario");
        }

        Assert.True(escritorVisto, "ReservasDeInventario crea reservas y cambia su estado desde T877: si cambió la forma, actualizá la prueba.");
        Assert.True(infractores.Count == 0,
            "Reservas escritas fuera de ReservasDeInventario (FR-033, FR-052, T877):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void El_escritor_vive_en_la_carpeta_de_reservas_de_ventas()
    {
        var tipo = typeof(IngenIA365ERP.Application.Inventory.Sales.Reservas.ReservasDeInventario);
        Assert.Equal("IngenIA365ERP.Application.Inventory.Sales.Reservas", tipo.Namespace);
        Assert.True(tipo.IsSealed, "El escritor de las reservas es una clase sellada: nadie la extiende para escribir por otro lado.");
    }
}
