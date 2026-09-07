namespace IngenIA365ERP.Shared.Services.Manual;

/// <summary>Qué clase de pantalla explica un tema. Cambia el tono de la guía, no su forma.</summary>
public enum TipoDeTema
{
    /// <summary>Un flujo con principio y fin: registrar, liquidar, aprobar.</summary>
    Proceso,

    /// <summary>Un catálogo: se consulta, se crea, se corrige, se retira.</summary>
    Maestro,

    /// <summary>Se eligen parámetros y se genera un resultado para leer o imprimir.</summary>
    Reporte,

    /// <summary>Sólo se consulta; no se crea nada desde ahí.</summary>
    Consulta,
}

/// <summary>
/// Un paso de una guía. <see cref="Ruta"/> es opcional: cuando el paso ocurre en
/// otra pantalla (o en la misma, para abrirla), el manual ofrece el botón de ir.
/// </summary>
public sealed record PasoDeManual(
    string Titulo,
    string? Detalle = null,
    string? Ruta = null,
    string? EtiquetaRuta = null);

/// <summary>
/// Un tema del manual: una opción del sistema y cómo se usa. Los datos son
/// estáticos a propósito —viven en <see cref="ManualCatalogo"/>, se compilan con la
/// aplicación y no dependen de ninguna API—: el manual tiene que abrir aun cuando
/// lo que se quiere consultar es «por qué esta pantalla no carga».
/// </summary>
public sealed record TemaDeManual(
    string Slug,
    string Titulo,
    string Modulo,
    string Ruta,
    string Resumen,
    IReadOnlyList<PasoDeManual> Pasos,
    IReadOnlyList<string> PalabrasClave,
    IReadOnlyList<string> Requisitos,
    IReadOnlyList<string> Relacionados,
    IReadOnlyList<string> RutasCubiertas,
    TipoDeTema Tipo)
{
    /// <summary>
    /// Convención de capturas: <c>wwwroot/img/manual/{slug}/paso-{n}.png</c>, con
    /// <c>n</c> desde 1. Si el archivo no existe la página lo oculta, así que un tema
    /// sin capturas sigue siendo un tema completo. Ver <c>docs/manual/README.md</c>.
    /// </summary>
    public string ImagenDelPaso(int numero) =>
        $"_content/IngenIA365ERP.Shared/img/manual/{Slug}/paso-{numero}.png";

    /// <summary>Ruta sin parámetros de plantilla, para el botón «ir a la pantalla».</summary>
    public bool RutaEsNavegable => !Ruta.Contains('{');
}

/// <summary>Un acierto de búsqueda con su puntaje y el fragmento que justifica mostrarlo.</summary>
public sealed record ResultadoDeBusqueda(TemaDeManual Tema, int Puntaje, string Fragmento);
