using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T001 (decisiones-transversales T13, §2.18; contracts/api.md §2.3), FR-016: toda
/// operación iniciada desde una pantalla o una integración lleva una clave de idempotencia. El
/// comando lo declara implementando <c>IOperacionIdempotente</c>; sin el marcador,
/// <c>IdempotencyBehavior</c> lo deja pasar y un doble clic confirma dos veces.
///
/// <para>
/// Esqueleto del Setup: <see cref="ComandosConRuta"/> empieza vacía y la prueba afirma la regla
/// sobre cada elemento; con la lista vacía pasa porque no hay nada que violar, no por un
/// <c>return</c> temprano. La llena la fase 2 (plataforma, T018) con los comandos de plataforma y
/// el recorrido de <c>Application/Inventory</c>, <c>Application/ElectronicInvoicing</c> y
/// <c>Application/Core/{Taxes,PaymentMeans}</c>, más las consultas por POST exceptuadas.
/// </para>
/// </summary>
public class LosComandosDeInventarioLlevanClave
{
    /// <summary>Nombres de tipo de los comandos con ruta que deben llevar clave. Los agrega la plataforma.</summary>
    private static readonly string[] ComandosConRuta = [];

    [Fact]
    public void Cada_comando_con_ruta_implementa_IOperacionIdempotente()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var comando in ComandosConRuta)
        {
            // La declaración hasta la llave o el punto y coma: ahí van la base y las interfaces.
            var declaracion = new Regex($@"\b(record|class)\s+{Regex.Escape(comando)}\b[^{{;]*", RegexOptions.Compiled);
            var encontrada = fuentes
                .Select(f => (f.Archivo, Match: declaracion.Match(f.Texto)))
                .FirstOrDefault(x => x.Match.Success);

            if (encontrada.Archivo is null)
                infractores.Add($"{comando}: no se encontró su declaración (si se renombró, actualizá ComandosConRuta)");
            else if (!encontrada.Match.Value.Contains("IOperacionIdempotente", StringComparison.Ordinal))
                infractores.Add($"{Path.GetRelativePath(root, encontrada.Archivo)}: {comando} no implementa IOperacionIdempotente");
        }

        Assert.True(infractores.Count == 0,
            "Comandos con ruta sin clave de idempotencia (FR-016):\n  " + string.Join("\n  ", infractores));
    }
}
