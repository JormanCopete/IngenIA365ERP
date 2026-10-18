using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>Un aviso que no bloquea: un valor de la norma o de la política que sigue pendiente (bloque D). (nuevo)</summary>
public sealed record AvisoDeNormaPendienteDto(string Code, string Message, QuienCorrigeDto? WhoFixes);

/// <summary>Qué avisos de norma pendiente pide quien llama: los del castigo, los de la evaluación de cartera o todos. (nuevo)</summary>
[Flags]
public enum AvisosDeNorma
{
    Castigo = 1,
    Evaluacion = 2,
    Todos = Castigo | Evaluacion,
}

/// <summary>
/// Los valores de la norma y de la política de K3 que la semilla deja vacíos y «pendientes de validar» (feature 013, K3, bloque D; decisión del dueño
/// del 2026-10-15): el plazo del reporte de castigos al supervisor y la periodicidad de la evaluación de cartera del tipo de entidad (datos
/// <c>writeOffReport</c> y <c>portfolioEvaluation</c> del documento <c>TiposDeOrganizacion</c>, según el perfil regulatorio vigente) y la altura mínima
/// de mora para castigar (<c>Castigo.DiasMinimosDeMora</c>). Se quedan vacíos —sin ellos no se exige reporte, no se valida la periodicidad y no se
/// exige altura—, pero el sistema <b>avisa</b> sin bloquear: en los requisitos de la activación de Cartera y al armar un lote de castigo o preparar una
/// evaluación (<c>warnings[]</c> de la respuesta, con su código). Sólo lee. (nuevo)
/// </summary>
public sealed class NormaPendienteDeCartera(ReglasDeParametrosDeCartera parametros, IPerfilRegulatorio perfiles, INormativaVigente normativa)
{
    public const string PlazoDelReporteDeCastigos = "WriteOffReportTermPending";
    public const string PeriodicidadDeLaEvaluacion = "PortfolioEvaluationPeriodicityPending";
    public const string AlturaMinimaDelCastigo = "WriteOffMinimumArrearsPending";

    private static readonly QuienCorrigeDto Norma = new("Administración", "/admin/valores-nacionales", null);
    private static readonly QuienCorrigeDto Parametros = new("Cartera", "/cartera/parametros", "Lending.Parameters.Manage");

    public async Task<IReadOnlyList<AvisoDeNormaPendienteDto>> AvisosAsync(DateOnly fecha, AvisosDeNorma cuales, CancellationToken ct)
    {
        var avisos = new List<AvisoDeNormaPendienteDto>();
        var perfil = await perfiles.VigenteAsync(fecha, null, ct);
        var tipos = perfil is null ? null : await normativa.TiposDeOrganizacionAsync(fecha, null, ct);
        var tipo = perfil is not null && tipos is { IsSuccess: true } ? tipos.Value.Documento.Buscar(perfil.OrganizationTypeCode) : null;
        var deQuien = perfil is null
            ? "La cooperativa no tiene perfil regulatorio vigente, así que no se sabe su tipo de entidad"
            : $"El documento de tipos de organización no trae el dato para el tipo de entidad de la cooperativa ({perfil.OrganizationTypeCode})";

        if (cuales.HasFlag(AvisosDeNorma.Castigo))
        {
            if (tipo?.WriteOffReport is null)
                avisos.Add(new(PlazoDelReporteDeCastigos,
                    $"Falta el plazo del reporte de castigos al supervisor, pendiente de validar: {deQuien}. Mientras falte, al aplicar un lote de castigo no se "
                    + "exige ni se vigila ese reporte.", Norma));
            var dias = await parametros.LeerAsync(ParametrosDeCartera.CastigoDiasMinimosDeMora, fecha, ct: ct);
            if (dias.IsFailure || string.IsNullOrWhiteSpace(dias.Value.Texto))
                avisos.Add(new(AlturaMinimaDelCastigo,
                    "Falta la altura de mora mínima para castigar (días), pendiente de validar: mientras falte, el castigo no exige una altura de mora mínima.",
                    Parametros));
        }
        if (cuales.HasFlag(AvisosDeNorma.Evaluacion) && tipo?.PortfolioEvaluation is null)
            avisos.Add(new(PeriodicidadDeLaEvaluacion,
                $"Falta la periodicidad de la evaluación de cartera (meses de corte y de efecto), pendiente de validar: {deQuien}. Mientras falte, no se valida "
                + "que el corte y el mes de efecto de una evaluación sean los de la norma.", Norma));
        return avisos;
    }
}
