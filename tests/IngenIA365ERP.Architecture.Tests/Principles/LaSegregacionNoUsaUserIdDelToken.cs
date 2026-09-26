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
/// Llenada por la fase 2 (plataforma, T018) con <c>Core/IngenIA365ERP.Application/Common/Approvals</c> y
/// <c>Core/IngenIA365ERP.Domain/Approvals</c>; la regla se escribe aquí y sólo aquí, y la fase 11 (US12)
/// sólo agrega carpetas. Una carpeta de la lista tiene que existir y tener fuentes: si se movió, la regla
/// dejaría de mirar sin avisar.
/// </para>
/// </summary>
public class LaSegregacionNoUsaUserIdDelToken
{
    /// <summary>Carpetas relativas a <c>src/</c> donde se decide la segregación.</summary>
    private static readonly string[] CarpetasDeSegregacion =
    [
        Path.Combine("Core", "IngenIA365ERP.Application", "Common", "Approvals"),
        Path.Combine("Core", "IngenIA365ERP.Domain", "Approvals"),
        // Fase 11, US12 (T415): los que identifican a una persona —alta, retiro e importación de vendedores
        // (CreateSalespersonCommand, DeleteSalespersonCommand, ImportSalespeopleCommand)— y la revisión diaria de reorden
        // (RevisionDeReorden, que corre como actor proceso).
        Path.Combine("Core", "IngenIA365ERP.Application", "Inventory", "Salespeople"),
        Path.Combine("Core", "IngenIA365ERP.Application", "Inventory", "Replenishment"),
    ];

    private static readonly Regex UserIdOCorreo = new(@"\.(UserId|Email)\b", RegexOptions.Compiled);

    [Fact]
    public void La_segregacion_no_lee_el_UserId_ni_el_correo_del_token()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();

        foreach (var carpeta in CarpetasDeSegregacion)
        {
            var ruta = Path.Combine(root, "src", carpeta);
            Assert.True(Directory.Exists(ruta), $"No existe src/{carpeta}: si se movió, actualizá CarpetasDeSegregacion.");
            var archivos = Directory.EnumerateFiles(ruta, "*.cs", SearchOption.AllDirectories).ToList();
            Assert.True(archivos.Count > 0, $"src/{carpeta} no tiene fuentes: la regla no estaría mirando nada.");

            foreach (var archivo in archivos)
            {
                var texto = FuenteSinComentarios.Leer(archivo);
                if (texto.Contains("ICurrentUserService", StringComparison.Ordinal) && UserIdOCorreo.IsMatch(texto))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: usa ICurrentUserService.UserId o el correo");
            }
        }

        Assert.True(infractores.Count == 0,
            "La segregación de funciones debe comparar el actor de IActorActual:\n  " + string.Join("\n  ", infractores));
    }
}
