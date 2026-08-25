using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// El rastro de auditoría sin cooperativa va a la base GLOBAL, no a un cubo
/// llamado «default».
///
/// <para>
/// <b>El fallo que fija.</b> <c>AuditDatabaseNames</c> resuelve el nombre con
/// una constante cuando no hay cooperativa, y lo documenta como regla dura. Pero
/// el escritor hacía <c>_tenantService.TenantId ?? "default"</c> ANTES de
/// llamarlo, así que el nombre llegaba ya resuelto y la constante no se aplicaba
/// nunca. Medido en la máquina de desarrollo: 148 documentos en
/// <c>IngenIA365ERP_Audit_default</c> y CERO en <c>IngenIA365ERP_Audit_Global</c>,
/// que es la que lee la consola. Los eventos de identidad se escribían y
/// quedaban invisibles — el mismo síntoma que ya se había corregido una vez.
/// </para>
///
/// <para>
/// El comentario en el código no bastó: estuvo escrito todo el tiempo, en el
/// archivo de al lado, mientras el defecto seguía vivo.
/// </para>
/// </summary>
public class RastroSinCuboDeBasura
{
    private static readonly Regex CuboDeBasura = new(
        @"\?\?\s*""default""", RegexOptions.Compiled);

    [Fact]
    public void El_escritor_de_auditoria_no_inventa_una_cooperativa_llamada_default()
    {
        var raiz = RepoPath.FindRepoRoot();
        var culpables = new List<string>();

        foreach (var archivo in RepoPath.ProductionCSharpFiles())
        {
            // Sólo el ensamblado de auditoría: en otros sitios «default» es una
            // clave de caché y no decide dónde se guarda nada.
            if (!archivo.Contains("IngenIA365ERP.Audit", StringComparison.OrdinalIgnoreCase))
                continue;

            // El propio archivo que documenta la regla la cita en un comentario.
            if (Path.GetFileName(archivo).Equals("AuditDatabaseNames.cs", StringComparison.OrdinalIgnoreCase))
                continue;

            if (CuboDeBasura.IsMatch(File.ReadAllText(archivo)))
                culpables.Add(archivo.Replace(raiz, string.Empty));
        }

        Assert.True(culpables.Count == 0,
            "sin cooperativa el nombre lo resuelve AuditDatabaseNames con su constante Global; " +
            "adelantarse con ?? \"default\" crea una base que nadie lee. Culpables: " +
            string.Join(" | ", culpables));
    }
}
