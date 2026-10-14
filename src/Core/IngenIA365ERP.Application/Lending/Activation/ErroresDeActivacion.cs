using System.Globalization;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>
/// Los errores de la migración y la activación de Cartera (feature 013, T0232, T0233; data-model §10.1 parte 2, C71; api.md §1.9,
/// §19). Los mensajes son los de data-model donde los fija. (nuevo)
/// </summary>
public static class ErroresDeActivacion
{
    public const string CodigoYaActivada = "Lending.Activation.AlreadyActivated";
    public const string CodigoCorteInvalido = "Lending.Activation.CutoffInvalid";
    public const string CodigoFaltanRequisitos = "Lending.Activation.RequirementsMissing";
    public const string CodigoCargaNoEncontrada = "Lending.MigrationLoad.NotFound";
    public const string CodigoCargaNoBorrador = "Lending.Migration.LoadNotDraft";
    public const string CodigoSinUsuario = "Lending.Actor.Unauthorized";
    public const string CodigoObligacionEnCeroImposible = "Lending.Activation.ZeroObligationNotPossible";

    public static Error YaActivada(DateOnly fecha) =>
        new ErrorConDatos(CodigoYaActivada,
            string.Create(CultureInfo.InvariantCulture, $"Cartera ya está activada desde el {fecha:yyyy-MM-dd}."),
            new { activationDate = fecha });

    public static Error CorteInvalido(DateOnly corte) =>
        new ErrorConDatos(CodigoCorteInvalido,
            string.Create(CultureInfo.InvariantCulture,
                $"La fecha de activación debe ser el día siguiente al corte de la carga aplicada ({corte:yyyy-MM-dd})."),
            new { expected = corte.AddDays(1) });

    public static Error FaltanRequisitos(IReadOnlyList<RequisitoDeActivacionDto> faltantes) =>
        new ErrorConDatos(CodigoFaltanRequisitos,
            $"No se puede activar Cartera todavía. Falta: {string.Join("; ", faltantes.Select(f => f.Detail))}. Complete lo que falta y vuelva a intentarlo.",
            new { missing = faltantes.Select(f => new { code = f.Code, detail = f.Detail, whoFixes = f.WhoFixes }).ToList() });

    public static Error CargaNoEncontrada(Guid publicId) =>
        new ErrorConDatos(CodigoCargaNoEncontrada, "La carga de migración no existe.", new { migrationLoadPublicId = publicId });

    public static Error CargaNoBorrador() =>
        new(CodigoCargaNoBorrador, "Esta carga ya fue aplicada o reemplazada y no se puede cambiar. Cree una carga nueva.");

    public static Error SinUsuario() =>
        new(CodigoSinUsuario, "No se pudo identificar a la persona que hace la operación en esta cooperativa.");

    /// <summary>
    /// US8 (T0752, C111): una venta cancelada antes del corte no puede nacer como obligación en cero: no hay línea (ni la de la carga ni una de origen
    /// comercial por defecto para el tipo de deudor) o la venta no nombra a una persona del maestro.
    /// </summary>
    public static Error ObligacionEnCeroImposible(string factura, string motivo) =>
        new ErrorConDatos(CodigoObligacionEnCeroImposible,
            $"La venta {factura} cancelada antes del corte no puede nacer en Cartera como obligación en cero: {motivo}. Corríjalo y vuelva a activar.",
            new { invoice = factura });
}
