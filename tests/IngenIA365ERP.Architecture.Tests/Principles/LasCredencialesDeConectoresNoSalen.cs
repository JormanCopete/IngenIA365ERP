using System.Reflection;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 020 (T150; R16; FR-057): las credenciales de un conector son la única credencial externa que vive en la base —cifrada, en
/// <c>ConnectorCredential.ProtectedPayload</c>— y por eso <b>nunca salen</b>: ningún DTO, exportación, registro ni evento de auditoría la
/// toca. Se fija por la fuente: sólo la entidad, su configuración y el almacén que la cifra y descifra (más las migraciones) nombran el
/// contenido cifrado de un conector; la propiedad lleva <see cref="NoAuditarAttribute"/>, y el comando que las pone no deja ver sus valores a
/// la auditoría (<c>[JsonIgnore]</c>). No hace falta enmendar <c>LasCredencialesDeCanalesExternosNoTocanLaBase</c>: ésa mira las entidades de
/// canales y de Cartera, y <c>ProtectedPayload</c> no tiene nombre de secreto; la excepción del FR-057 queda nombrada aquí.
/// </summary>
public class LasCredencialesDeConectoresNoSalen
{
    /// <summary>Los únicos archivos que pueden nombrar el contenido cifrado (relativos a <c>src/</c>, con barras normales).</summary>
    private static readonly string[] Permitidos =
    [
        "Core/IngenIA365ERP.Domain/Entities/Core/Connectors/Connector.cs",
        "Infrastructure/IngenIA365ERP.Persistence/Configurations/Core/Connectors/ConnectorsConfigurations.cs",
        "Core/IngenIA365ERP.Application/Core/Connectors/AlmacenDeCredencialesDeConector.cs",
    ];

    [Fact]
    public void Solo_la_entidad_su_configuracion_y_el_almacen_nombran_el_contenido_cifrado()
    {
        var src = Path.Combine(RepoPath.FindRepoRoot(), "src");
        var infractores = RepoPath.ProductionCSharpFiles()
            .Select(f => (Archivo: f, Relativo: Path.GetRelativePath(src, f).Replace('\\', '/')))
            .Where(x => !x.Relativo.Contains("Persistence.Migrations", StringComparison.Ordinal))
            .Where(x => !Permitidos.Contains(x.Relativo, StringComparer.Ordinal))
            .Where(x =>
            {
                var fuente = FuenteSinComentarios.Leer(x.Archivo);
                // ProtectedPayload puede ser el nombre en otra entidad cifrada: sólo cuenta si el archivo habla de credenciales de conector.
                return fuente.Contains("ProtectedPayload", StringComparison.Ordinal)
                       && (fuente.Contains("ConnectorCredential", StringComparison.Ordinal) || fuente.Contains("Connectors", StringComparison.Ordinal));
            })
            .Select(x => x.Relativo)
            .ToList();

        Assert.True(infractores.Count == 0,
            "Las credenciales de un conector no salen de su almacén (FR-057):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void El_contenido_cifrado_no_va_a_la_auditoria()
    {
        var propiedad = typeof(Domain.Entities.Core.Connectors.ConnectorCredential)
            .GetProperty(nameof(Domain.Entities.Core.Connectors.ConnectorCredential.ProtectedPayload));

        Assert.NotNull(propiedad);
        Assert.True(propiedad!.IsDefined(typeof(NoAuditarAttribute), inherit: true), "ConnectorCredential.ProtectedPayload debe llevar [NoAuditar].");
    }

    [Fact]
    public void El_comando_de_credenciales_no_deja_ver_los_valores_a_la_auditoria()
    {
        var campos = typeof(Application.Core.Connectors.SetConnectorCredentialsCommand)
            .GetProperty(nameof(Application.Core.Connectors.SetConnectorCredentialsCommand.Fields));

        Assert.NotNull(campos);
        Assert.True(campos!.IsDefined(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), inherit: true),
            "SetConnectorCredentialsCommand.Fields debe llevar [JsonIgnore]: AuditBehavior serializa el comando entero.");
    }

    [Fact]
    public void Ningun_dto_de_conectores_tiene_una_propiedad_de_secreto()
    {
        var tipos = typeof(Application.Core.Connectors.ConectorDeLaCooperativaDto).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, Namespace: not null }
                        && (t.Namespace.StartsWith("IngenIA365ERP.Application.Core.Connectors", StringComparison.Ordinal)
                            || t.Namespace.StartsWith("IngenIA365ERP.Application.Platform.Connectors", StringComparison.Ordinal))
                        && t.Name.EndsWith("Dto", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(tipos);

        string[] sospechosos = ["Password", "Token", "Secret", "Payload", "Credential"];
        var infractores = tipos
            // Sólo las de texto: «Secret» (el campo es secreto) y «HasCredentials» son banderas, no el contenido.
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.PropertyType == typeof(string))
                .Select(p => (Tipo: t.Name, Propiedad: p.Name)))
            .Where(x => sospechosos.Any(s => x.Propiedad.Contains(s, StringComparison.Ordinal)))
            .Select(x => $"{x.Tipo}.{x.Propiedad}")
            .ToList();

        Assert.True(infractores.Count == 0, "Un DTO de conectores expone algo que parece una credencial:\n  " + string.Join("\n  ", infractores));
    }
}
