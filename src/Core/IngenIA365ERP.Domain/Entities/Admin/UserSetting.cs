using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Preferencia de un usuario, en formato clave/valor. Mapea a
/// [dbo].[ADM_UserSettings], en la base Admin.
///
/// <para>
/// Vive en Admin y no en el schema del tenant porque el ajuste es de la
/// PERSONA, no de la cooperativa: quien necesita texto grande lo necesita en
/// todas las cooperativas donde trabaja, y no tendría sentido que el tema
/// cambiara al alternar entre una y otra.
/// </para>
///
/// <para>
/// Hay ajustes que sí dependen de la cooperativa (la pantalla de inicio o los
/// favoritos apuntan a rutas y registros que sólo existen en un tenant). Para
/// eso está <see cref="TenantPublicId"/>.
/// </para>
/// </summary>
public class UserSetting : AuditableEntity
{
    /// <summary>
    /// Usuario central dueño de la preferencia. Es el <c>Id</c> de
    /// ADM_CentralUsers, que por convención de ASP.NET Identity es un Guid y
    /// hace las veces de identificador público.
    /// </summary>
    public Guid CentralUserId { get; set; }

    /// <summary>
    /// Cooperativa a la que aplica el ajuste. <see cref="Guid.Empty"/> significa
    /// "todas".
    /// </summary>
    /// <remarks>
    /// Deliberadamente NO es nulable. Un índice único sobre una columna nulable
    /// no significa lo mismo en los dos motores: SQL Server considera dos NULL
    /// iguales y deja una sola fila, PostgreSQL los considera distintos y
    /// admitiría duplicados. La misma migración daría entonces dos esquemas con
    /// reglas distintas, y el duplicado sólo aparecería en producción si corre
    /// sobre PostgreSQL. Con un centinela no nulable la restricción se comporta
    /// igual en ambos.
    /// </remarks>
    public Guid TenantPublicId { get; set; } = Guid.Empty;

    /// <summary>Clave del ajuste. Ver <see cref="UserSettingKeys"/>.</summary>
    [MaxLength(100)]
    public string SettingKey { get; set; } = string.Empty;

    /// <summary>
    /// Valor, siempre como texto. Los valores compuestos (la lista de
    /// favoritos) se guardan como JSON.
    /// </summary>
    [MaxLength(4000)]
    public string? SettingValue { get; set; }
}

/// <summary>
/// Claves reconocidas. Están acá y no sueltas en las pantallas para que el
/// servidor pueda rechazar cualquier otra: la tabla es escribible por el propio
/// usuario, y sin lista blanca sería un almacén de datos arbitrarios por
/// usuario.
/// </summary>
public static class UserSettingKeys
{
    // --- Apariencia: aplican a todas las cooperativas ---
    public const string Tema = "ui.tema";
    public const string Densidad = "ui.densidad";
    public const string Escala = "ui.escala";
    public const string Contraste = "ui.contraste";

    // --- Navegación: dependen de la cooperativa ---
    public const string PaginaInicio = "ui.pagina-inicio";
    public const string Favoritos = "ui.favoritos";

    /// <summary>Claves cuyo valor NO depende de la cooperativa.</summary>
    public static readonly IReadOnlySet<string> Globales =
        new HashSet<string>(StringComparer.Ordinal) { Tema, Densidad, Escala, Contraste };

    /// <summary>Claves cuyo valor sí depende de la cooperativa.</summary>
    public static readonly IReadOnlySet<string> PorTenant =
        new HashSet<string>(StringComparer.Ordinal) { PaginaInicio, Favoritos };

    public static bool EsValida(string clave) =>
        Globales.Contains(clave) || PorTenant.Contains(clave);

    /// <summary>
    /// Valores admitidos por clave. Sirven de lista blanca en el validador:
    /// estos valores terminan como atributos del elemento raíz del HTML, así
    /// que aceptar texto libre sería inyectarlo en el documento.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ValoresAdmitidos =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [Tema] = new HashSet<string>(StringComparer.Ordinal) { "claro", "oscuro" },
            [Densidad] = new HashSet<string>(StringComparer.Ordinal) { "comoda", "compacta" },
            [Escala] = new HashSet<string>(StringComparer.Ordinal) { "normal", "grande", "muy-grande" },
            [Contraste] = new HashSet<string>(StringComparer.Ordinal) { "normal", "alto" },
        };
}
