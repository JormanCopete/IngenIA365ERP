using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0002 (R-12; FR-007, FR-170; CH16; data-model §0.5): las filas de Recaudo y Tesorería se bloquean en <b>un orden fijo</b>, una
/// sentencia por nivel ordenada por <c>Id</c>, y sólo las bloquean <c>CerrojoDeRecaudo</c> y <c>CerrojoDeTesoreria</c> (con su SQL en
/// <c>SqlDelCerrojoDeRecaudo</c> y <c>SqlDelCerrojoDeTesoreria</c>). Recaudo va antes que Tesorería también cuando quien llama es Tesorería:
/// ningún comando de <c>Treasury</c> escribe por <c>IResultadoDeOrdenesDeRecaudo</c> ni por <c>IPartidasDesdeConciliacion</c> sin haber llamado
/// antes a <c>TomarCerrojosAsync</c> del mismo puerto.
///
/// <para>
/// Árbol que recorre: todo el fuente de producción (para el SQL de candados sobre <c>RCD_</c>/<c>TES_</c>) y
/// <see cref="RecaudoYTesoreria.CarpetasDeTesoreria"/> (para la dirección Tesorería → Recaudo). Las listas nacen vacías en el Setup y las
/// llena la fase 2 (T0038): <see cref="QuienesTomanCerrojos"/> con los cuatro archivos, <see cref="NivelesDeRecaudo"/> y
/// <see cref="NivelesDeTesoreria"/> con las tablas de data-model §0.5 en su orden.
/// </para>
/// </summary>
public class LosCerrojosDeRecaudoYTesoreriaVanEnOrden
{
    /// <summary>Los únicos archivos (relativos a <c>src/</c>) que escriben SQL de candados sobre tablas <c>RCD_</c> o <c>TES_</c>.</summary>
    private static readonly string[] QuienesTomanCerrojos =
    [
        // Fase 2 (T0038).
        "Infrastructure/IngenIA365ERP.Persistence/Receipts/CerrojoDeRecaudo.cs",
        "Infrastructure/IngenIA365ERP.Persistence/Receipts/SqlDelCerrojoDeRecaudo.cs",
        "Infrastructure/IngenIA365ERP.Persistence/Treasury/CerrojoDeTesoreria.cs",
        "Infrastructure/IngenIA365ERP.Persistence/Treasury/SqlDelCerrojoDeTesoreria.cs",
    ];

    /// <summary>Las tablas de los niveles de Recaudo que toma el cerrojo, en el orden de data-model §0.5 (sin los de los contratos).</summary>
    private static readonly string[] NivelesDeRecaudo =
    [
        "RCD_Setup", "RCD_CollectionPointDays", "RCD_CashSessions", "RCD_Vaults", "RCD_PayerControls",
        "RCD_PayerCreditBalances", "RCD_UnidentifiedItems", "RCD_PaymasterRemittanceLines", "RCD_ReceivedChecks", "RCD_Receipts", "COR_Sequences",
    ];

    /// <summary>Las tablas de los niveles de Tesorería, en su orden.</summary>
    private static readonly string[] NivelesDeTesoreria =
    [
        "TES_Setup", "TES_BankAccounts", "TES_Checkbooks", "TES_PaymentBatches", "TES_PaymentOrders", "TES_Reconciliations",
    ];

    /// <summary>
    /// Feature 015, US3 (T0107; FR-005, FR-170): en Recaudo, una operación que toca créditos y cuentas llama al contrato de obligaciones (<c>AplicarAsync</c>)
    /// antes que al de cuentas (<c>ConsignarAsync</c>): la persona se bloquea antes que sus cuentas en Cartera, como en todo el orden de data-model §0.5.
    /// </summary>
    [Fact]
    public void Recaudo_llama_al_contrato_de_obligaciones_antes_que_al_de_cuentas()
    {
        var root = RepoPath.FindRepoRoot();
        var infracciones = new List<string>();
        foreach (var archivo in ReglaSobreFuente.ArchivosEn(["Core/IngenIA365ERP.Application/Receipts"]))
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            var obligaciones = texto.IndexOf(".AplicarAsync(", StringComparison.Ordinal);
            var cuentas = texto.IndexOf(".ConsignarAsync(", StringComparison.Ordinal);
            if (cuentas >= 0 && (obligaciones < 0 || obligaciones > cuentas) && texto.Contains("IContratoDeObligaciones", StringComparison.Ordinal))
                infracciones.Add(ReglaSobreFuente.Relativo(root, archivo));
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Recaudo llama a IContratoDeCuentas antes que a IContratoDeObligaciones (FR-170)");
    }

    private static readonly Regex Candado = new(@"FOR\s+(UPDATE|SHARE)|UPDLOCK|XLOCK|HOLDLOCK", RegexOptions.Compiled);
    private static readonly Regex TablaDeLosModulos = new(@"\b(RCD|TES)_\w+", RegexOptions.Compiled);

    [Fact]
    public void Solo_los_dos_cerrojos_bloquean_filas_de_Recaudo_y_Tesoreria()
    {
        var root = RepoPath.FindRepoRoot();
        var permitidos = QuienesTomanCerrojos.Select(r => "src/" + r).ToHashSet(StringComparer.Ordinal);
        var infracciones = RepoPath.ProductionCSharpFiles()
            .Where(f => !permitidos.Contains(ReglaSobreFuente.Relativo(root, f)))
            .Where(f => FuenteSinComentarios.Leer(f) is var texto && Candado.IsMatch(texto) && TablaDeLosModulos.IsMatch(texto))
            .Select(f => ReglaSobreFuente.Relativo(root, f))
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "Candados sobre RCD_ o TES_ fuera de CerrojoDeRecaudo y CerrojoDeTesoreria (FR-007, R-12)");
        Assert.Equal(QuienesTomanCerrojos.Length, ReglaSobreFuente.ArchivosNombrados(QuienesTomanCerrojos).Count());
    }

    [Fact]
    public void Los_niveles_de_cada_cerrojo_aparecen_en_el_orden_de_data_model()
    {
        foreach (var (archivo, niveles) in new[] { ("SqlDelCerrojoDeRecaudo.cs", NivelesDeRecaudo), ("SqlDelCerrojoDeTesoreria.cs", NivelesDeTesoreria) })
        {
            if (niveles.Length == 0) continue;
            var fuente = QuienesTomanCerrojos.Single(q => q.EndsWith(archivo, StringComparison.Ordinal));
            var texto = FuenteSinComentarios.Leer(ReglaSobreFuente.ArchivosNombrados([fuente]).Single());
            var posiciones = niveles.Select(t => (Tabla: t, Donde: texto.IndexOf($"\"{t}\"", StringComparison.Ordinal))).ToList();
            Assert.All(posiciones, p => Assert.True(p.Donde >= 0, $"{archivo} no declara el nivel {p.Tabla}"));
            Assert.Equal(niveles, posiciones.OrderBy(p => p.Donde).Select(p => p.Tabla).ToArray());
        }
    }

    /// <summary>
    /// CH16: un archivo de Tesorería que escribe por <c>IResultadoDeOrdenesDeRecaudo.InformarAsync</c> o <c>IPartidasDesdeConciliacion.CrearAsync</c>
    /// llama antes a <c>TomarCerrojosAsync</c>. Sin llamadores todavía, no hay nada que violar.
    /// </summary>
    [Fact]
    public void Tesoreria_toma_los_cerrojos_de_Recaudo_antes_de_escribir_por_sus_puertos()
    {
        var root = RepoPath.FindRepoRoot();
        var escritura = new Regex(@"\.(InformarAsync|CrearAsync)\(", RegexOptions.Compiled);
        var puertos = new Regex(@"\b(IResultadoDeOrdenesDeRecaudo|IPartidasDesdeConciliacion)\b", RegexOptions.Compiled);
        var infracciones = new List<string>();
        foreach (var archivo in RecaudoYTesoreria.ArchivosDeTesoreria())
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            if (!puertos.IsMatch(texto)) continue;
            foreach (Match m in escritura.Matches(texto))
            {
                var antes = texto[..m.Index];
                if (!antes.Contains("TomarCerrojosAsync(", StringComparison.Ordinal))
                    infracciones.Add($"{ReglaSobreFuente.Relativo(root, archivo)}: {m.Groups[1].Value} sin TomarCerrojosAsync antes");
            }
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Tesorería escribe por un puerto de Recaudo sin tomar antes sus cerrojos (CH16, data-model §0.5)");
    }
}
