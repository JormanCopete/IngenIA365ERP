using System.Text.RegularExpressions;

// T138 — Lint que detecta mensajes hardcoded en inglés dentro de strings
// pasados a Result.Failure / WithMessage / IsRequired / .Failure.
//
// Heurística: extrae literales C# que se pasan como segundo argumento de
// Result.Failure(...) y como argumento de WithMessage(...). Si la mayoría
// de las palabras son inglesas (lista negra mínima), reporta el archivo.
//
// Uso: dotnet run --project tools/MessageLintTool/MessageLintTool.csproj -- <repo-root>
//
// Salida: 0 si todo OK, 1 con lista de offenders. CI puede fallar el job.

if (args.Length == 0)
{
    Console.Error.WriteLine("Uso: ingenia-message-lint <repo-root>");
    return 2;
}

var repoRoot = Path.GetFullPath(args[0]);
if (!Directory.Exists(repoRoot))
{
    Console.Error.WriteLine($"Ruta no existe: {repoRoot}");
    return 2;
}

// Solo aplica el lint a las superficies que ven los usuarios: handlers
// Application y razor pages. Identifiers C# en inglés son OK.
var scanRoots = new[]
{
    Path.Combine(repoRoot, "src", "Core", "IngenIA365ERP.Application"),
    Path.Combine(repoRoot, "src", "Presentation", "IngenIA365ERP.Shared", "Pages")
};

// Palabras "tell" de inglés. Si una literal contiene >=2 de estas con boundary
// de palabra, asumimos que está en inglés.
var englishMarkers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "the", "is", "are", "was", "were", "not", "must", "should",
    "cannot", "found", "does", "doesn't", "please", "invalid",
    "missing", "required", "duplicate", "exists", "failed", "wrong"
};

// Patrón: Result.Failure(..., "mensaje") | .WithMessage("mensaje")
var literalRegex = new Regex(
    "(?:Result\\.Failure(?:<[^>]+>)?\\s*\\(\\s*[^,]+,\\s*|\\.WithMessage\\s*\\(\\s*)\"((?:\\\\.|[^\"\\\\])+)\"",
    RegexOptions.Compiled);

var wordBoundary = new Regex(@"\b[a-záéíóúñü']+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

var offenders = new List<string>();
var checkedFiles = 0;

foreach (var root in scanRoots.Where(Directory.Exists))
{
    foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
        .Concat(Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)))
    {
        if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            continue;
        }
        checkedFiles++;

        var content = File.ReadAllText(file);
        foreach (Match m in literalRegex.Matches(content))
        {
            var literal = m.Groups[1].Value;
            if (literal.Length < 15) continue; // mensajes cortos pueden ser comunes a ambos idiomas

            var words = wordBoundary.Matches(literal)
                .Select(w => w.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var englishHits = words.Count(w => englishMarkers.Contains(w));
            if (englishHits >= 2)
            {
                var rel = Path.GetRelativePath(repoRoot, file);
                offenders.Add($"{rel}: \"{Truncate(literal)}\" (hits={englishHits})");
            }
        }
    }
}

Console.WriteLine($"MessageLint: analizados {checkedFiles} archivos.");
if (offenders.Count == 0)
{
    Console.WriteLine("OK — sin mensajes hardcoded en inglés detectados.");
    return 0;
}

Console.Error.WriteLine($"\nDetectados {offenders.Count} mensajes que parecen estar en inglés (FR-048):");
foreach (var line in offenders.Take(50))
{
    Console.Error.WriteLine($"  - {line}");
}
if (offenders.Count > 50)
{
    Console.Error.WriteLine($"  … y {offenders.Count - 50} más");
}
return 1;

static string Truncate(string s) => s.Length <= 100 ? s : s[..97] + "...";
