using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 014 (T225, research E232): toda ruta de pantalla escrita como literal en la capa de aplicación de Cartera —el enlace de un
/// requisito de activación, de una alerta o de un aviso— lleva a una página con su <c>@page</c>. El requisito del perfil regulatorio
/// mandaba a <c>/maestros/perfil-regulatorio</c>, que no existe (la pantalla es <c>/admin/perfil-regulatorio</c>), y quien seguía el
/// enlace caía en «Página no encontrada». Mismo espíritu que <c>TodoEnlaceDelMenuTieneSuPagina</c>.
/// </summary>
public class LosEnlacesDeCarteraLlevanASuPagina
{
    private static readonly Regex Ruta = new("\"(/(?:cartera|maestros|admin|contabilidad|nomina|inventario|compras|ventas)(?:/[a-z0-9-]+)*)\"", RegexOptions.Compiled);
    private static readonly Regex Pagina = new("@page \"([^\"{]+)\"", RegexOptions.Compiled);

    [Fact]
    public void Cada_ruta_de_pantalla_de_la_aplicacion_de_Cartera_tiene_su_pagina()
    {
        var root = RepoPath.FindRepoRoot();
        var paginas = Directory.EnumerateFiles(Path.Combine(root, "src", "Presentation", "IngenIA365ERP.Shared", "Pages"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => Pagina.Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value.TrimEnd('/')))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var codigo = ReglaSobreFuente.ArchivosEn(["Core/IngenIA365ERP.Application/Lending"]).ToList();
        Assert.NotEmpty(codigo);

        var sinPagina = codigo
            .SelectMany(a => File.ReadAllLines(a).Select((l, i) => (Archivo: a, Linea: l, Numero: i + 1)))
            .SelectMany(x => Ruta.Matches(x.Linea).Select(m => (x.Archivo, x.Numero, Ruta: m.Groups[1].Value)))
            .Where(x => !paginas.Contains(x.Ruta))
            .Select(x => $"{ReglaSobreFuente.Relativo(root, x.Archivo)}:{x.Numero}: {x.Ruta}")
            .ToList();

        Assert.True(sinPagina.Count == 0, "Rutas de pantalla sin página:\n  " + string.Join("\n  ", sinPagina));
    }
}
