using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Lending.Deposits.Lines;

/// <summary>
/// Los errores del catálogo común de líneas y de las versiones de captación y de aportes (feature 014, US1, T065; contracts/api.md §1.8, §3).
/// <c>Lending.Line.NotFound</c> es 404 —también sin permiso o fuera del alcance de líneas, con el mismo mensaje—; los demás, 422 con <c>data</c>;
/// <c>Lending.DepositLine.Invalid</c> y <c>Lending.ContributionLine.Invalid</c> llevan <c>data.errors[{ field, code, message }]</c> para que la
/// pantalla marque el campo. Mensajes en español claro que dicen qué hacer (FR-013). (nuevo)
/// </summary>
public static class ErroresDeLineasDeCaptacion
{
    public const string CodigoLineaInexistente = "Lending.Line.NotFound";
    public const string CodigoCaptacionVersionCruzada = "Lending.DepositLine.VersionOverlaps";
    public const string CodigoAportesVersionCruzada = "Lending.ContributionLine.VersionOverlaps";
    public const string CodigoVersionInexistente = "Lending.DepositLine.VersionNotFound";
    public const string CodigoVersionDeAportesInexistente = "Lending.ContributionLine.VersionNotFound";
    public const string CodigoAportesYaActiva = "Lending.Contributions.LineAlreadyActive";
    public const string CodigoUsarRutaDeCredito = "Lending.Line.UseCreditLinesRoute";
    public const string CodigoLineaReferidaInvalida = "Lending.DepositLine.ReferencedLineInvalid";
    public const string CodigoFiltroInvalido = "Lending.Report.FilterInvalid";

    public static Error LineaInexistente() => new(CodigoLineaInexistente, "La línea no existe.");

    /// <summary>Lo que rechazó una regla de dominio de la línea, como error del contrato (los «Invalid» con su campo en <c>data.errors</c>).</summary>
    public static Error De(ProblemaDeCartera problema)
    {
        ArgumentNullException.ThrowIfNull(problema);
        var campo = problema.Datos is { } d && d.TryGetValue("field", out var f) ? f?.ToString() : null;
        if (campo is null) return ErroresDeGobierno.De(problema);
        var datos = new Dictionary<string, object?>(problema.Datos!, StringComparer.Ordinal)
        {
            ["errors"] = new[] { new ErrorDeCampo(campo, problema.Codigo, problema.Mensaje) },
        };
        return new ErrorConDatos(problema.Codigo, problema.Mensaje, datos);
    }

    public static Error VersionCruzada(LineNature naturaleza, DateOnly existente) => new ErrorConDatos(
        naturaleza == LineNature.Contribution ? CodigoAportesVersionCruzada : CodigoCaptacionVersionCruzada,
        $"La línea ya tiene una versión que rige desde el {Fecha(existente)}: la nueva tiene que empezar ese mismo día o después.",
        new
        {
            existingValidFrom = Fecha(existente),
            errors = new[] { new ErrorDeCampo("validFrom", naturaleza == LineNature.Contribution ? CodigoAportesVersionCruzada : CodigoCaptacionVersionCruzada,
                "Empieza antes de la última versión registrada.") },
        });

    public static Error VersionInexistente(LineNature naturaleza) => naturaleza == LineNature.Contribution
        ? new Error(CodigoVersionDeAportesInexistente, "La versión de la línea de aportes no existe.")
        : new Error(CodigoVersionInexistente, "La versión de la línea de captación no existe.");

    public static Error AportesYaActiva(string codigo, string nombre) => new ErrorConDatos(CodigoAportesYaActiva,
        $"La cooperativa ya tiene una línea de aportes activa ({codigo}, «{nombre}»): cada asociado tiene una sola cuenta de aportes. Ciérrela antes de activar otra.",
        new { existingCode = codigo, existingName = nombre });

    public static Error UsarRutaDeCredito(string codigo) => new ErrorConDatos(CodigoUsarRutaDeCredito,
        $"La línea {codigo} es de crédito: se administra desde «Líneas de crédito».", new { code = codigo, route = "/api/lending/credit-lines" });

    /// <summary>La línea que nombra una versión (la destino a la vista de un CDAT, la del ahorro permanente de los aportes) no sirve.</summary>
    public static Error LineaReferidaInvalida(string campo, string mensaje) => new ErrorConDatos(CodigoLineaReferidaInvalida, mensaje,
        new { errors = new[] { new ErrorDeCampo(campo, CodigoLineaReferidaInvalida, mensaje) } });

    public static Error FiltroInvalido(string mensaje) => new(CodigoFiltroInvalido, mensaje);

    /// <summary>El nombre de la naturaleza para los mensajes.</summary>
    public static string NombreDe(LineNature naturaleza) => naturaleza switch
    {
        LineNature.Deposit => "captación",
        LineNature.Contribution => "aportes",
        _ => "crédito",
    };

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
