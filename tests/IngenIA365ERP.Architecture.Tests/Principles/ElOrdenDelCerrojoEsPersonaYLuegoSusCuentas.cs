using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 014, respuesta 82 B del dueño (E254): el orden de los cerrojos de Cartera es uno solo para todo camino que toque a la vez a la persona y a sus
/// cuentas —<b>la persona primero (<c>LND_DebtorControls</c>) y después sus cuentas, por Id</b>, igual en los dos motores—. Quien ya tiene una cuenta
/// tomada nunca pide el cerrojo de una persona: dos operaciones sobre la misma persona se interbloquearían (la consignación que recalcula el embargo
/// reescribe los bloqueos de todas las cuentas de su titular). Se comprueba en el fuente, método por método: en ninguno aparece
/// <c>BloquearDeudoresAsync</c> después de <c>BloquearCuentasAsync</c>; y la consignación del contrato de cuentas toma, junto a las suyas, las cuentas de
/// los titulares con embargo vigente.
/// </summary>
public class ElOrdenDelCerrojoEsPersonaYLuegoSusCuentas
{
    private static readonly Regex Miembro = new(@"^    (public|private|internal|protected)\b[^\n]*\(", RegexOptions.Multiline | RegexOptions.Compiled);

    [Fact]
    public void Ningun_metodo_toma_el_cerrojo_de_una_persona_despues_del_de_una_cuenta()
    {
        var raiz = Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application");
        var infracciones = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories))
        {
            var fuente = FuenteSinComentarios.Leer(archivo);
            if (!fuente.Contains("BloquearCuentasAsync(", StringComparison.Ordinal) || !fuente.Contains("BloquearDeudoresAsync(", StringComparison.Ordinal)) continue;
            var inicios = Miembro.Matches(fuente).Select(m => m.Index).Append(fuente.Length).ToList();
            for (var i = 0; i + 1 < inicios.Count; i++)
            {
                var trozo = fuente[inicios[i]..inicios[i + 1]];
                var primeraCuenta = trozo.IndexOf("BloquearCuentasAsync(", StringComparison.Ordinal);
                var ultimaPersona = trozo.LastIndexOf("BloquearDeudoresAsync(", StringComparison.Ordinal);
                if (primeraCuenta >= 0 && ultimaPersona > primeraCuenta)
                    infracciones.Add($"{Path.GetRelativePath(raiz, archivo)}: {trozo.Split('\n')[0].Trim()}");
            }
        }
        Assert.True(infracciones.Count == 0, "Persona primero y luego sus cuentas (ICerrojoDeCartera): " + string.Join("; ", infracciones));
    }

    [Fact]
    public void La_consignacion_del_contrato_toma_tambien_las_cuentas_de_los_titulares_embargados()
    {
        var archivo = Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application", "Lending", "Integration", "ContratoDeCuentasDeCartera.cs");
        var fuente = FuenteSinComentarios.Leer(archivo);
        var inicio = fuente.IndexOf("public async Task<RespuestaDeConsignacionDto> ConsignarAsync", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "no se encontró ConsignarAsync");
        var trozo = fuente[inicio..];
        var persona = trozo.IndexOf("BloquearDeudoresAsync(", StringComparison.Ordinal);
        var embargadas = trozo.IndexOf("CuentasDeLasPersonasEmbargadasAsync(", StringComparison.Ordinal);
        var cuentas = trozo.IndexOf("BloquearCuentasAsync(", StringComparison.Ordinal);
        Assert.True(persona >= 0 && embargadas > persona && cuentas > embargadas,
            "ConsignarAsync debe tomar la persona, saber qué cuentas del titular embargado faltan y bloquearlas todas en una pasada por Id.");
    }
}
