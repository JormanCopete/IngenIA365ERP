using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>
/// Los errores del riesgo y los cierres de Cartera (feature 013, US9, bloque 12B; data-model §10.1, C111; api.md §12). Mensajes en español y sin
/// datos del deudor; el <c>data</c> lleva lo que la pantalla necesita para explicar. (nuevo)
/// </summary>
public static class ErroresDeRiesgo
{
    // ------------------------------------------------------------------------------------------------ política --
    public const string CodigoPoliticaMenosEstricta = "Lending.RiskPolicy.LessStrict";
    public const string CodigoPoliticaFueraDeEsquema = "Lending.RiskPolicy.SchemaInvalid";
    public const string CodigoPoliticaRetroactiva = "Lending.RiskPolicy.RetroactiveValidFrom";
    public const string CodigoPoliticaInexistente = "Lending.RiskPolicy.NotFound";
    public const string CodigoPoliticaYaValidada = "Lending.RiskPolicy.AlreadyValidated";

    // ------------------------------------------------------------------------------------------------ análisis --
    public const string CodigoAnalisisNoEmpeora = "Lending.AnalysisRating.NotWorse";
    public const string CodigoAnalisisNoVigente = "Lending.AnalysisRating.NotActive";
    public const string CodigoAnalisisYaVigente = "Lending.AnalysisRating.AlreadyActive";
    public const string CodigoAnalisisInexistente = "Lending.AnalysisRating.NotFound";

    // ------------------------------------------------------------------------------------------------ cierres --
    public const string CodigoCierreAnteriorFaltante = "Lending.MonthlyClose.PreviousMissing";
    public const string CodigoCierreYaHecho = "Lending.MonthlyClose.AlreadyClosed";
    public const string CodigoPeriodoContableCerrado = "Lending.MonthlyClose.AccountingPeriodClosed";
    public const string CodigoCausacionAtrasada = "Lending.MonthlyClose.AccrualBehind";
    public const string CodigoReplicaNoDisponible = "Lending.MonthlyClose.ReplicationUnavailable";
    public const string CodigoNoEsElUltimo = "Lending.MonthlyClose.NotLast";
    public const string CodigoBaseNoSeReabre = "Lending.MonthlyClose.BaseNotReopenable";
    public const string CodigoCierreInexistente = "Lending.MonthlyClose.NotFound";
    public const string CodigoMesNoTerminado = "Lending.MonthlyClose.MonthNotEnded";
    public const string CodigoCierreSinNorma = "Lending.MonthlyClose.RegulationMissing";
    public const string CodigoCierreIncompleto = "Lending.MonthlyClose.LinesIncomplete";
    public const string CodigoBaseYaExiste = "Lending.MonthlyClose.BaseAlreadyExists";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Mes(int anio, int mes) => string.Create(Inv, $"{anio:0000}-{mes:00}");

    // ------------------------------------------------------------------------------------------------ política --

    public static Error MenosEstricta(IReadOnlyList<CeldaDePolitica> celdas) => new ErrorConDatos(CodigoPoliticaMenosEstricta,
        $"La política no puede ser menos estricta que la norma en {string.Join(", ", celdas.Select(c => c.Ruta))}.",
        new { cells = celdas.Select(c => new { path = c.Ruta, norm = c.Norma, proposed = c.Propuesta }).ToList() });

    public static Error FueraDeEsquema(IReadOnlyList<ErrorDeDocumentoNormativo> errores) => new ErrorConDatos(CodigoPoliticaFueraDeEsquema,
        $"La política no cumple el esquema del anexo: {errores.Count.ToString(Inv)} errores.",
        new { errors = errores.Select(e => new { path = e.Ruta, message = e.Mensaje }).ToList() });

    public static Error Retroactiva(DateOnly desde, DateOnly hoy) => new ErrorConDatos(CodigoPoliticaRetroactiva,
        $"La política no puede regir desde el {desde:yyyy-MM-dd}, antes de hoy ({hoy:yyyy-MM-dd}): registre una que rija desde hoy o después.",
        new { validFrom = desde, today = hoy });

    public static Error PoliticaInexistente() => new(CodigoPoliticaInexistente, "La versión de la política SARC no existe.");

    public static Error PoliticaYaValidada(int version) => new ErrorConDatos(CodigoPoliticaYaValidada,
        $"La versión {version.ToString(Inv)} de la política SARC ya está validada.", new { versionNumber = version });

    // ------------------------------------------------------------------------------------------------ análisis --

    public static Error AnalisisNoEmpeora(RatingCategory propuesta, RatingCategory deLasReglas) => new ErrorConDatos(CodigoAnalisisNoEmpeora,
        $"La calificación por análisis ({propuesta}) no es peor que la que dan las reglas ({deLasReglas}): el análisis sólo puede empeorarla.",
        new { ruleCategory = deLasReglas.ToString(), proposed = propuesta.ToString() });

    public static Error AnalisisNoVigente() => new(CodigoAnalisisNoVigente, "La calificación por análisis ya se levantó o no está vigente.");

    public static Error AnalisisYaVigente(Guid vigente) => new ErrorConDatos(CodigoAnalisisYaVigente,
        "El crédito ya tiene una calificación por análisis vigente: levántela antes de poner otra.", new { analysisRatingPublicId = vigente });

    public static Error AnalisisInexistente() => new(CodigoAnalisisInexistente, "La calificación por análisis no existe.");

    // ------------------------------------------------------------------------------------------------ cierres --

    public static Error CierreAnteriorFaltante(int anio, int mes) => new ErrorConDatos(CodigoCierreAnteriorFaltante,
        $"No se puede cerrar {Mes(anio, mes)}: falta el cierre vigente del mes anterior (o el cierre base de la activación). Ciérrelo primero.",
        new { year = anio, month = mes });

    public static Error CierreYaHecho(int anio, int mes, int version, Guid cierre) => new ErrorConDatos(CodigoCierreYaHecho,
        $"{Mes(anio, mes)} ya tiene un cierre vigente (versión {version.ToString(Inv)}). Para volver a cerrarlo, reábralo primero.",
        new { year = anio, month = mes, version, monthlyClosePublicId = cierre });

    public static Error PeriodoContableCerrado(int anio, int mes, string estado) => new ErrorConDatos(CodigoPeriodoContableCerrado,
        $"Contabilidad ya no tiene abierto {Mes(anio, mes)} ({estado}): Cartera no puede cerrar ni reabrir ese mes.",
        new { year = anio, month = mes, accountingState = estado });

    public static Error CausacionAtrasada(int anio, int mes, int creditos, DateOnly? marcaMasVieja) => new ErrorConDatos(CodigoCausacionAtrasada,
        $"Hay {creditos.ToString(Inv)} créditos sin causar hasta el {MonthlyCloseFin(anio, mes):yyyy-MM-dd}: corra el ciclo diario hasta el último "
        + "día del mes antes de cerrarlo.",
        new { year = anio, month = mes, loansBehind = creditos, oldestAccruedDate = marcaMasVieja });

    public static Error ReplicaNoDisponible(string? motivo) => new ErrorConDatos(CodigoReplicaNoDisponible,
        "No se pudo sincronizar los valores nacionales con el registro central: el cierre no se hace sin la réplica al día. Inténtelo más tarde."
        + (string.IsNullOrWhiteSpace(motivo) ? string.Empty : $" ({motivo})"),
        new { reason = motivo });

    public static Error NoEsElUltimo(int anio, int mes) => new ErrorConDatos(CodigoNoEsElUltimo,
        $"Sólo se reabre el último cierre vigente; el de {Mes(anio, mes)} no lo es.", new { year = anio, month = mes });

    public static Error BaseNoSeReabre() => new(CodigoBaseNoSeReabre,
        "El cierre base de la activación no se reabre: es el saldo inicial de Cartera.");

    public static Error CierreInexistente() => new(CodigoCierreInexistente, "El cierre de Cartera no existe.");

    public static Error MesNoTerminado(int anio, int mes, DateOnly hoy) => new ErrorConDatos(CodigoMesNoTerminado,
        $"{Mes(anio, mes)} todavía no termina (hoy es {hoy:yyyy-MM-dd}): se cierra desde el día siguiente a su último día.",
        new { year = anio, month = mes, today = hoy });

    public static Error SinNorma(string detalle) => new ErrorConDatos(CodigoCierreSinNorma,
        $"No se puede calificar ni deteriorar: {detalle}", new { detail = detalle });

    public static Error LineasIncompletas(int faltantes) => new ErrorConDatos(CodigoCierreIncompleto,
        $"{faltantes.ToString(Inv)} créditos no tienen su línea del cierre: reintente la corrida para calcularlos antes de cerrar.",
        new { missingLoans = faltantes });

    public static Error BaseYaExiste() => new(CodigoBaseYaExiste, "La cooperativa ya tiene su cierre base.");

    private static DateOnly MonthlyCloseFin(int anio, int mes) => Domain.Entities.Lending.Risk.MonthlyClose.UltimoDiaDe(anio, mes);
}
