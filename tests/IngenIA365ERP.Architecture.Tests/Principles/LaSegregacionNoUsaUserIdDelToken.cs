using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T001 (decisiones-transversales T6, T33, §2.18), FR-009 y FR-010: la segregación de
/// funciones (quien crea no aprueba, quien aprobó un nivel no aprueba otro) compara el
/// <c>SEC_Users.Id</c> que resuelve <c>IActorActual</c>, nunca el <c>UserId</c> del token de
/// <c>ICurrentUserService</c> ni el correo: el primero es el identificador central y el segundo cambia.
///
/// <para>
/// Esqueleto del Setup: <see cref="CarpetasDeSegregacion"/> (relativas a <c>src/</c>) empieza vacía
/// y la prueba afirma la regla sobre cada elemento; con la lista vacía pasa porque no hay nada que
/// violar, no por un <c>return</c> temprano. La llena la fase 2 (plataforma, T018) con
/// <c>Core/IngenIA365ERP.Application/Common/Approvals</c> y <c>Core/IngenIA365ERP.Domain/Approvals</c>;
/// la fase 11 (US12) sólo agrega carpetas. Una carpeta que todavía no existe cuenta como vacía.
/// </para>
/// </summary>
public class LaSegregacionNoUsaUserIdDelToken
{
    /// <summary>Carpetas relativas a <c>src/</c> donde se decide la segregación. Las agrega la plataforma.</summary>
    private static readonly string[] CarpetasDeSegregacion = [];

    private static readonly Regex UserIdOCorreo = new(@"\.(UserId|Email)\b", RegexOptions.Compiled);

    [Fact]
    public void La_segregacion_no_lee_el_UserId_ni_el_correo_del_token()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();

        foreach (var carpeta in CarpetasDeSegregacion)
        {
            var ruta = Path.Combine(root, "src", carpeta);
            if (!Directory.Exists(ruta)) continue; // carpeta aún no creada: no tiene nada que violar

            foreach (var archivo in Directory.EnumerateFiles(ruta, "*.cs", SearchOption.AllDirectories))
            {
                var texto = File.ReadAllText(archivo);
                if (texto.Contains("ICurrentUserService", StringComparison.Ordinal) && UserIdOCorreo.IsMatch(texto))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: usa ICurrentUserService.UserId o el correo");
            }
        }

        Assert.True(infractores.Count == 0,
            "La segregación de funciones debe comparar el actor de IActorActual:\n  " + string.Join("\n  ", infractores));
    }
}
