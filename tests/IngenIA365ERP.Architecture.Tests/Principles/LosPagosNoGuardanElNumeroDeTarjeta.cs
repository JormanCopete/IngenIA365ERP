using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T003 (decisiones-transversales T25, §2.18), FR-096: de un pago con tarjeta se
/// guardan los cuatro últimos dígitos (<c>Last4</c>), la franquicia y la autorización, nunca el
/// número completo. Guardarlo pondría a la cooperativa bajo PCI DSS por una columna que nadie usa.
///
/// <para>
/// Esqueleto del Setup: <see cref="EntidadesDePago"/> (nombres de tipo de las entidades que
/// registran un pago o un medio) empieza vacía y la prueba afirma la regla sobre cada elemento; con
/// la lista vacía pasa porque no hay nada que violar, no por un <c>return</c> temprano. La llena el
/// bloque que crea los pagos del punto de venta y los medios de pago en Core (entrega I3).
/// </para>
/// </summary>
public class LosPagosNoGuardanElNumeroDeTarjeta
{
    /// <summary>Nombres de tipo de las entidades de pago. Los agrega la entrega I3.</summary>
    private static readonly string[] EntidadesDePago = [];

    /// <summary>Una propiedad que guardaría el número de la tarjeta (PAN) completo.</summary>
    private static readonly Regex NumeroDeTarjeta = new(
        @"\bpublic\s+[\w?<>]+\s+(CardNumber|CardPan|Pan|PrimaryAccountNumber|NumeroDeTarjeta|NumeroTarjeta)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void Ninguna_entidad_de_pago_guarda_el_numero_de_tarjeta()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var entidad in EntidadesDePago)
        {
            var declaracion = new Regex($@"\bclass\s+{Regex.Escape(entidad)}\b", RegexOptions.Compiled);
            var (archivo, texto) = fuentes.FirstOrDefault(f => declaracion.IsMatch(f.Texto));

            if (archivo is null)
                infractores.Add($"{entidad}: no se encontró su declaración (si se renombró, actualizá EntidadesDePago)");
            else if (NumeroDeTarjeta.IsMatch(texto))
                infractores.Add($"{Path.GetRelativePath(root, archivo)}: {entidad} guarda el número de la tarjeta");
        }

        Assert.True(infractores.Count == 0,
            "Entidades de pago que guardan el número de la tarjeta (T25; sólo Last4 y autorización):\n  " + string.Join("\n  ", infractores));
    }
}
