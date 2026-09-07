using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Identity.Profile.Preferencias;

/// <summary>
/// Lista blanca estricta de claves y valores.
///
/// <para>
/// Esta tabla la escribe el propio usuario sobre su propia fila, así que la
/// tentación es no validar. Pero el valor de <c>ui.tema</c> termina como
/// atributo del elemento raíz del documento y la página de inicio termina en
/// una navegación: sin lista blanca serían texto arbitrario del usuario puesto
/// en el HTML, y la tabla se convertiría además en un almacén libre por
/// usuario.
/// </para>
/// </summary>
public sealed class SaveMyPreferencesCommandValidator : AbstractValidator<SaveMyPreferencesCommand>
{
    /// <summary>Tope de favoritos. Sin límite, el JSON crece sin control.</summary>
    private const int MaximoFavoritos = 30;

    public SaveMyPreferencesCommandValidator()
    {
        RuleFor(x => x.Preferencias)
            .NotNull().WithMessage("No se recibió ninguna preferencia.")
            .Must(p => p.Count > 0).WithMessage("No se recibió ninguna preferencia.")
            .Must(p => p.Count <= 10).WithMessage("Demasiadas preferencias en una sola llamada.");

        // WithMessage de dos argumentos: en un RuleForEach el de uno recibe el
        // comando entero, no el elemento, y no da acceso a la clave.
        RuleForEach(x => x.Preferencias)
            .Must(par => UserSettingKeys.EsValida(par.Key))
            .WithMessage((_, par) => $"La preferencia '{par.Key}' no existe.")
            .Must(EsValorAdmitido)
            .WithMessage((_, par) => $"El valor de '{par.Key}' no es válido.");
    }

    private static bool EsValorAdmitido(KeyValuePair<string, string?> par)
    {
        // Vacío siempre vale: es la forma de volver al valor por defecto.
        if (string.IsNullOrEmpty(par.Value))
            return true;

        if (UserSettingKeys.ValoresAdmitidos.TryGetValue(par.Key, out var admitidos))
            return admitidos.Contains(par.Value);

        return par.Key switch
        {
            UserSettingKeys.PaginaInicio => EsRutaInterna(par.Value),
            UserSettingKeys.Favoritos => EsListaDeRutasInternas(par.Value),
            _ => false,
        };
    }

    /// <summary>
    /// Sólo rutas relativas de esta aplicación. Rechaza el absoluto y el
    /// protocolo-relativo (<c>//otro-sitio</c>), que el navegador resuelve
    /// contra un host externo: si no, configurar la página de inicio permitiría
    /// dejar a un usuario redirigido fuera del ERP al entrar.
    /// </summary>
    private static bool EsRutaInterna(string valor) =>
        valor.Length <= 200
        && valor.StartsWith('/')
        && !valor.StartsWith("//", StringComparison.Ordinal)
        && !valor.Contains(':', StringComparison.Ordinal)
        && !valor.Contains("..", StringComparison.Ordinal);

    private static bool EsListaDeRutasInternas(string json)
    {
        if (json.Length > 4000)
            return false;

        try
        {
            var rutas = JsonSerializer.Deserialize<List<string>>(json);
            return rutas is not null
                   && rutas.Count <= MaximoFavoritos
                   && rutas.TrueForAll(EsRutaInterna);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
