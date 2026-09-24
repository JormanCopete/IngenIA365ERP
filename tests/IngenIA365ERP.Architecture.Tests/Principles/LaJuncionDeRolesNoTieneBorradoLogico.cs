using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// <c>SEC_UserRoles</c> es una tabla de unión y <b>no maneja borrado lógico</b>: el mapeo
/// (<c>UserConfiguration.UsingEntity&lt;UserRole&gt;</c>) ignora <c>IsDeleted</c> y las demás columnas
/// de auditoría que <c>UserRole</c> hereda de <c>AuditableEntity</c>, y neutraliza el filtro global
/// con <c>HasQueryFilter(ur =&gt; true)</c>.
///
/// <para>
/// Filtrar una consulta por una de esas propiedades <b>compila</b>, porque en C# existen; EF sólo se
/// entera al traducir, ya en tiempo de ejecución: «Translation of member 'IsDeleted' on entity type
/// 'UserRole' failed». El 2026-08-25 <c>UpdateRoleCommandHandler</c> estrenó así el guardia que impide
/// dejar una cooperativa sin administrador, y desde entonces <b>editar un rol respondía 500</b> en todo
/// ambiente cuyo catálogo tuviera <c>Security.Roles.View</c> y <c>.Update</c> —el guardia sale antes si
/// faltan—. Se descubrió en QA casi un mes después.
/// </para>
///
/// <para>
/// Las pruebas de Application no pueden verlo y no hay que esperar que lo vean: corren sobre
/// <c>TestApplicationDbContext</c> con EF InMemory, que es <b>otro modelo</b> —no aplica ese
/// <c>Ignore</c>— y además evalúa los filtros en cliente. De ahí que el defecto naciera con su prueba
/// en verde. Por eso la barrera vive acá, y toma los nombres prohibidos del <b>modelo real</b> en vez
/// de una lista escrita a mano: si mañana el mapeo deja de ignorar uno, esta prueba lo deja pasar sola.
/// </para>
/// </summary>
public class LaJuncionDeRolesNoTieneBorradoLogico
{
    /// <summary>El parametro del primer lambda de la cadena: `.Where(ur =&gt; ...` -&gt; `ur`.</summary>
    private static readonly Regex Parametro = new(@"\(\s*(\w+)\s*=>", RegexOptions.Compiled);

    /// <summary>Propiedades que <c>UserRole</c> tiene en C# pero el modelo no mapea.</summary>
    private static IReadOnlyList<string> NoMapeadas()
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=modelo-sin-conexion;Database=x")   // construir el modelo no abre conexión
            .Options;
        using var db = new ApplicationDbContext(opciones);
        var tipo = db.Model.FindEntityType(typeof(UserRole))
            ?? throw new InvalidOperationException("UserRole no está en el modelo: cambió el mapeo de SEC_UserRoles.");

        var mapeadas = tipo.GetProperties().Select(p => p.Name)
            .Concat(tipo.GetNavigations().Select(n => n.Name))
            .ToHashSet(StringComparer.Ordinal);

        return typeof(UserRole).GetProperties()
            .Select(p => p.Name)
            .Where(n => !mapeadas.Contains(n))
            .ToList();
    }

    [Fact]
    public void El_mapeo_sigue_sin_mapear_el_borrado_logico()
    {
        // Si esto falla es que la junction cambió: entonces la regla de abajo sobra y hay que retirarla,
        // no ampliarla. Sin este ancla, un mapeo nuevo dejaría la prueba pasando por vacío.
        Assert.Contains("IsDeleted", NoMapeadas());
    }

    [Fact]
    public void Ninguna_consulta_sobre_UserRoles_filtra_por_una_propiedad_que_el_modelo_no_mapea()
    {
        var prohibidas = NoMapeadas();
        var raiz = RepoPath.FindRepoRoot();
        var infractores = new List<string>();

        foreach (var archivo in RepoPath.ProductionCSharpFiles())
        {
            var texto = File.ReadAllText(archivo);
            var desde = 0;
            while ((desde = texto.IndexOf("UserRoles", desde, StringComparison.Ordinal)) >= 0)
            {
                var fin = texto.IndexOf(';', desde);
                var sentencia = fin < 0 ? texto[desde..] : texto[desde..fin];
                desde += "UserRoles".Length;

                // Un alta no es una consulta: escribir de más en un inicializador es inocuo —EF descarta
                // lo no mapeado— y varios sitios ponen CreatedBy/UpdatedBy ahí. Lo que revienta es filtrar.
                if (sentencia.Contains(".Add(", StringComparison.Ordinal)) continue;

                // Sólo el parámetro del lambda ligado a UserRoles. Mirar la sentencia entera daría por
                // infractor cualquier `role.Id` o el `rp.IsDeleted` de un Join contra RolePermissions,
                // que están perfectamente mapeados.
                var lambda = Parametro.Match(sentencia);
                if (!lambda.Success) continue;
                var parametro = lambda.Groups[1].Value;

                foreach (var prohibida in prohibidas.Where(p =>
                             Regex.IsMatch(sentencia, $@"\b{Regex.Escape(parametro)}\.{Regex.Escape(p)}\b")))
                {
                    var linea = texto[..desde].Count(c => c == '\n') + 1;
                    infractores.Add($"{Path.GetRelativePath(raiz, archivo)}:{linea} filtra por UserRole.{prohibida}");
                }
            }
        }

        Assert.True(infractores.Count == 0,
            "SEC_UserRoles no mapea esas columnas, así que la consulta compila y falla al traducir —o sea, ya en el " +
            "ambiente—. Hay que quitar el filtro:\n  " + string.Join("\n  ", infractores));
    }
}
