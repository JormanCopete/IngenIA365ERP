using System.Reflection;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// La librería de WebAuthn vive SÓLO en Infrastructure.
///
/// <para>
/// No es purismo de capas. Verificar una aserción WebAuthn implica CBOR, COSE,
/// firmas ES256, los flags del authenticator data y el hash del origen: es
/// criptografía de verificación, y hay exactamente un sitio en este proyecto donde
/// puede vivir. Si sus tipos empezaran a aparecer en Application, cambiar de
/// librería —o de versión mayor— dejaría de ser tocar un archivo y pasaría a ser
/// una refactorización que nadie querrá hacer, sobre el camino de entrada al
/// sistema.
/// </para>
///
/// <para>
/// Lo que cruza la frontera es JSON crudo y tipos propios. Es más feo, y es lo que
/// hace que la dependencia sea sustituible.
/// </para>
/// </summary>
public class LaCriptografiaDeWebAuthnNoSeFiltra
{
    private static readonly string[] EnsambladosProhibidos = ["Fido2", "Fido2.Models"];

    [Fact]
    public void Ni_Domain_ni_Application_referencian_la_libreria_de_webauthn()
    {
        var internas = new[]
        {
            typeof(Domain.Common.BaseEntity).Assembly,
            typeof(Application.Common.Interfaces.Identity.IWebAuthnService).Assembly,
        };

        var infractores = internas
            .SelectMany(a => a.GetReferencedAssemblies().Select(r => new { Capa = a.GetName().Name, Ref = r.Name }))
            .Where(x => EnsambladosProhibidos.Contains(x.Ref, StringComparer.OrdinalIgnoreCase))
            .Select(x => $"{x.Capa} referencia {x.Ref}")
            .ToList();

        Assert.True(infractores.Count == 0,
            "La librería de WebAuthn se filtró a una capa interna:\n  " +
            string.Join("\n  ", infractores) +
            "\n\nLo que debe cruzar es JSON crudo y tipos propios de Application.");
    }

    [Fact]
    public void Un_solo_archivo_del_codigo_fuente_nombra_la_libreria()
    {
        // Complementa a la prueba de arriba, que sólo ve referencias compiladas.
        // Ésta caza el `using Fido2NetLib` puesto en un archivo que todavía no
        // compila o que sólo lo usa dentro de un comentario de código.
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => File.ReadAllText(f).Contains("Fido2NetLib", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f))
            .OrderBy(f => f)
            .ToList();

        Assert.True(
            infractores.Count == 1 && infractores[0] == "WebAuthnService.cs",
            "Sólo WebAuthnService.cs puede nombrar Fido2NetLib. Lo nombran:\n  " +
            string.Join("\n  ", infractores));
    }
}
