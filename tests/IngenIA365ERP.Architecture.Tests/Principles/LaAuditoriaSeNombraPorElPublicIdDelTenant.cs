using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// La base de auditoría de cada cooperativa se nombra por el <c>PublicId</c> del tenant
/// (<c>ICurrentTenantService.TenantId</c>), que es lo que la consola consulta. Un emisor que
/// arme un <c>AuditEventDocument</c> con <c>ICurrentUserService.TenantId</c> —el Id entero que
/// el middleware deja en <c>HttpContext.Items</c>— escribe en una base con sufijo numérico que
/// nadie lee: el evento existe, la consola dice que no, y ninguna prueba se pone roja.
///
/// <para>
/// Pasó dos veces el mismo día (2026-09-21): <c>PayrollAuditEmitter</c> lo destapó la e2e de la
/// definitiva; <c>AccountingAuditEmitter</c> —su molde— lo encontró la revisión adversarial de
/// la feature 010. Esta prueba fija que ningún emisor de Application pase el tenant del usuario
/// al documento de auditoría: si el emisor recibe <c>ICurrentUserService</c>, el
/// <c>TenantId</c> que va al documento tiene que salir de <c>ICurrentTenantService</c>.
/// </para>
/// </summary>
public class LaAuditoriaSeNombraPorElPublicIdDelTenant
{
    /// <summary><c>TenantId: currentUser.TenantId</c>, <c>TenantId: _currentUser.TenantId ?? …</c>: el tenant del documento sale del usuario.</summary>
    private static readonly Regex TenantDelUsuario =
        new(@"TenantId\s*:\s*_?[a-zA-Z]*[uU]ser[a-zA-Z]*\s*\.\s*TenantId\b", RegexOptions.Compiled);

    [Fact]
    public void Ningun_emisor_de_Application_escribe_el_tenant_del_usuario_en_el_documento_de_auditoria()
    {
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => f.Contains($"{Path.DirectorySeparatorChar}IngenIA365ERP.Application{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(f =>
            {
                var codigo = File.ReadAllText(f);
                return codigo.Contains("new AuditEventDocument(", StringComparison.Ordinal) && TenantDelUsuario.IsMatch(codigo);
            })
            .Select(f => Path.GetRelativePath(RepoPath.FindRepoRoot(), f))
            .OrderBy(n => n)
            .ToList();

        Assert.True(infractores.Count == 0,
            "Un AuditEventDocument se nombra por el PublicId del tenant (ICurrentTenantService.TenantId), no por " +
            "ICurrentUserService.TenantId, que es el Id interno: con él los eventos caen en una base que la consola " +
            "no lee. Lo hacen:\n  " + string.Join("\n  ", infractores) +
            "\n\nUse `tenant?.TenantId ?? currentUser.TenantId`, como PayrollAuditEmitter y AccountingAuditEmitter.");
    }
}
