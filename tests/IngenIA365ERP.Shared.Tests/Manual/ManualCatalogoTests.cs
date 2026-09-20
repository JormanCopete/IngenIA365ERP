using IngenIA365ERP.Shared.Services.Manual;
using IngenIA365ERP.Shared.Tests.Helpers;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Manual;

/// <summary>
/// El manual es un catálogo estático que enlaza pantallas y otros temas por texto: un slug mal
/// escrito en <c>Relacionados</c> no da error, <c>Tema.razor</c> lo descarta con <c>OfType</c> y el
/// enlace desaparece en silencio («libro-auxiliar» en vez de «contabilidad-libro-auxiliar», hasta el
/// 2026-09-20); una <c>Ruta</c> sin página tampoco, y el botón «Abrir …» lleva a «Página no
/// encontrada» (los certificados de retención, retirados hasta E4). Aquí se fija que cada enlace
/// del manual llegue a algún lado. Los mensajes traen la lista completa: con un solo elemento
/// se corrige uno y se descubre el siguiente en la corrida que viene.
/// </summary>
public class ManualCatalogoTests
{
    [Fact]
    public void Todo_slug_en_Relacionados_es_un_tema_del_catalogo()
    {
        var rotos = ManualCatalogo.Temas
            .SelectMany(t => t.Relacionados.Select(r => (Tema: t.Slug, Relacionado: r)))
            .Where(x => ManualCatalogo.PorSlug(x.Relacionado) is null)
            .Select(x => $"{x.Tema} → {x.Relacionado}")
            .ToList();

        Vacia(rotos, "Relacionados con un slug que no es de ningún tema (Tema.razor los omite sin avisar):");
    }

    [Fact]
    public void Toda_ruta_de_un_tema_tiene_su_pagina_en_Shared()
    {
        var rutas = Repositorio.RutasConPagina();
        Assert.NotEmpty(rutas);

        var sinPagina = ManualCatalogo.Temas
            .Where(t => !Repositorio.TienePagina(rutas, t.Ruta))
            .Select(t => $"{t.Slug} → {t.Ruta}")
            .ToList();

        Vacia(sinPagina, "Temas cuya Ruta no tiene @page en Shared/Pages (el botón «Abrir …» lleva a «Página no encontrada»):");
    }

    [Fact]
    public void Toda_ruta_cubierta_y_todo_enlace_de_paso_tiene_su_pagina_en_Shared()
    {
        var rutas = Repositorio.RutasConPagina();

        var sinPagina = ManualCatalogo.Temas
            .SelectMany(t => t.RutasCubiertas.Concat(t.Pasos.Select(p => p.Ruta).OfType<string>()).Select(r => (t.Slug, Ruta: r)))
            .Where(x => x.Ruta.StartsWith('/') && !Repositorio.TienePagina(rutas, x.Ruta))
            .Select(x => $"{x.Slug} → {x.Ruta}")
            .Distinct()
            .ToList();

        Vacia(sinPagina, "Pasos con botón o rutas cubiertas sin @page en Shared/Pages:");
    }

    [Fact]
    public void Los_slugs_son_unicos()
    {
        var repetidos = ManualCatalogo.Temas.GroupBy(t => t.Slug, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Vacia(repetidos, "Slugs repetidos (PorSlug devuelve uno solo; el otro queda inalcanzable):");
    }

    private static void Vacia(List<string> lista, string encabezado) =>
        Assert.True(lista.Count == 0, encabezado + "\n  " + string.Join("\n  ", lista));
}
