using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Tablero;

// 2026-11-17 (pedido del dueño): el tablero de inicio muestra a cada usuario las cifras más importantes para él, según su rol o lo
// que él mismo elija. Cada cifra es un IIndicadorDelTablero y cada pendiente un IPendienteDelTablero; los módulos agregan los suyos
// como clases nuevas (se registran solas, DependencyInjection.RegistrarTablero) sin tocar el tablero.

/// <summary>Cómo se muestra el valor de un indicador.</summary>
public enum FormatoDeIndicador
{
    Numero = 1,
    Moneda = 2,
    Porcentaje = 3,
}

/// <summary>Una cifra sola (con su comparación) o una serie que se dibuja como gráfico.</summary>
public enum TipoDeIndicador
{
    Cifra = 1,
    Serie = 2,
}

/// <summary>
/// Lo que el tablero sabe de un indicador sin calcularlo.
/// <list type="bullet">
/// <item><paramref name="Codigo"/>: único, en minúsculas con puntos y guiones (<c>ventas.del-dia</c>); es lo que se guarda en las
/// preferencias del usuario y en el tablero del rol, así que no se cambia una vez publicado.</item>
/// <item><paramref name="Grupo"/>: el módulo como lo lee la persona (Comercial, Caja, Cartera…), para agrupar en el catálogo.</item>
/// <item><paramref name="Permiso"/>: quien no lo tiene no ve el indicador ni lo puede pedir (los permisos ya vienen recortados por
/// los módulos apagados de la cooperativa).</item>
/// <item><paramref name="AdmiteSoloLoMio"/>: si tiene sentido «sólo lo que hice yo» (mis ventas, mi caja). Si no, siempre es de
/// todo lo que el usuario puede ver.</item>
/// <item><paramref name="Ruta"/>: la pantalla o el informe que explica la cifra (un clic en la tarjeta lleva allá).</item>
/// <item><paramref name="PorDefecto"/>: entra en el tablero sugerido de quien no tiene uno propio ni de su rol.</item>
/// </list>
/// </summary>
public sealed record DefinicionDeIndicador(
    string Codigo,
    string Titulo,
    string Grupo,
    string Permiso,
    bool AdmiteSoloLoMio,
    TipoDeIndicador Tipo,
    FormatoDeIndicador Formato,
    string? Ruta,
    string Descripcion,
    bool PorDefecto = false);

/// <summary>
/// A quién y cuándo se le calcula. <paramref name="UsuarioId"/> es <c>SEC_Users.Id</c> (lo que guardan las columnas
/// <c>…ByUserId</c>); <paramref name="UsuarioDeAuditoria"/> es lo que el interceptor escribe en <c>CreatedBy</c>. Con
/// <paramref name="SoloLoMio"/> el indicador filtra por cualquiera de los dos, el que use su tabla.
/// </summary>
public sealed record ContextoDeIndicador(bool SoloLoMio, int? UsuarioId, string? UsuarioDeAuditoria, DateOnly Hoy);

/// <summary>Un punto de una serie: la etiqueta del eje (día, rango, medio) y su valor.</summary>
public sealed record PuntoDeSerie(string Etiqueta, decimal Valor);

/// <summary>
/// Lo que devuelve un indicador. <paramref name="Anterior"/> y <paramref name="EtiquetaDelAnterior"/> son la comparación
/// («ayer», «mes anterior»); <paramref name="Nota"/> una línea corta debajo de la cifra («3 sesiones abiertas»).
/// </summary>
public sealed record ResultadoDeIndicador(
    decimal? Valor,
    decimal? Anterior = null,
    string? EtiquetaDelAnterior = null,
    IReadOnlyList<PuntoDeSerie>? Serie = null,
    string? Nota = null);

/// <summary>Una cifra del tablero. Sólo lee: nunca escribe ni llama fuera del proceso.</summary>
public interface IIndicadorDelTablero
{
    DefinicionDeIndicador Definicion { get; }

    Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct);
}

/// <summary>
/// Un pendiente: algo que espera a quien tiene <paramref name="Permiso"/> (aprobar, contabilizar, cerrar). El tablero lo muestra
/// sólo si la cuenta es mayor que cero, con un clic a <paramref name="Ruta"/>.
/// </summary>
public sealed record DefinicionDePendiente(string Codigo, string Titulo, string Grupo, string Permiso, string Ruta);

/// <summary>Un pendiente del tablero. Sólo cuenta.</summary>
public interface IPendienteDelTablero
{
    DefinicionDePendiente Definicion { get; }

    Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct);
}

/// <summary>Las reglas comunes de los códigos del tablero.</summary>
public static partial class CodigosDelTablero
{
    /// <summary>Tope de tarjetas en un tablero (del usuario o del rol).</summary>
    public const int MaximoDeTarjetas = 24;

    public static bool EsValido(string? codigo) => codigo is not null && Patron().IsMatch(codigo);

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z0-9-]+)+$")]
    private static partial Regex Patron();
}
