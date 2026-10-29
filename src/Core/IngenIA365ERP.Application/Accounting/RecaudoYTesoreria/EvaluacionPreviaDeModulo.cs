using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;

/// <summary>
/// La validación previa de Recaudo y Tesorería (feature 015, T0259; R-23; FR-143, FR-221; contabilidad.md §6): para cada combinación de hecho × dimensiones, si cada
/// rol que el hecho siempre mueve tiene una regla vigente que podría casar (una dimensión que la pregunta no trae no descarta la regla) y, en los roles por vínculo,
/// si la cuenta propia que viene está vinculada. Sin contabilidad iniciada o con el modo de paso en «no pasa» nada se bloquea. La política la dice
/// <c>Contabilidad.FaltaDeRegla</c> del módulo (<c>Bloquear</c> por defecto); quien pregunta decide con ella: la pantalla no confirma, el proceso avisa. (nuevo)
/// </summary>
public sealed class EvaluacionPreviaDeModulo(ResolutorDeLaMatriz resolutor, ILectorDeParametros lector, ISender sender)
{
    public const string Bloquear = "Bloquear";
    public const string SinContabilidad = "SinContabilidad";
    public const string NoPasa = "NoPasa";

    /// <summary>La clave con que una pregunta acota la evaluación a un rol.</summary>
    public const string ClaveDelRol = "Role";

    /// <summary>La clave con que una pregunta trae la cuenta propia de un rol por vínculo.</summary>
    public const string ClaveDeLaCuentaPropia = "OwnBankAccountPublicId";

    public async Task<EvaluacionContableDto> EvaluarAsync(ModuloDeLaMatriz modulo, IReadOnlyList<CombinacionDeHecho> combinaciones, CancellationToken ct)
    {
        if (combinaciones.Count == 0) return new EvaluacionContableDto(true, Bloquear, []);
        var fecha = combinaciones.Min(c => c.AccountingDate);
        var hasta = combinaciones.Max(c => c.AccountingDate);
        var estado = await sender.Send(new GetAccountingPeriodStatusQuery(fecha), ct);
        if (estado.IsFailure || !estado.Value.AccountingInitialized) return new EvaluacionContableDto(true, SinContabilidad, []);

        var modo = await lector.LeerAsync(modulo.ModuloDeParametros, ParametrosDeRecaudoYTesoreria.ContabilidadModoDePaso, fecha, ct: ct);
        var politica = await PoliticaAsync(modulo, fecha, ct);
        if (modo.IsSuccess && modo.Value.Texto == NoPasa) return new EvaluacionContableDto(true, politica, []);

        var matriz = await resolutor.CargarAsync(modulo, fecha, hasta, ct);
        var faltan = new List<CombinacionSinReglaDto>();
        foreach (var c in combinaciones)
        {
            var hecho = modulo.Hecho(c.FactType);
            if (hecho is null) continue;
            IReadOnlyList<string> roles = c.Dimensions.TryGetValue(ClaveDelRol, out var uno) ? [uno] : hecho.RolesObligatorios;
            var combinacion = Combinacion(c.Dimensions);
            foreach (var codigo in roles)
            {
                var rol = modulo.Rol(codigo);
                if (rol is null) continue;
                if (rol.PorVinculo)
                {
                    if (c.Dimensions.TryGetValue(ClaveDeLaCuentaPropia, out var texto) && Guid.TryParse(texto, out var propia) && !matriz.TieneVinculo(propia, c.AccountingDate))
                        faltan.Add(new CombinacionSinReglaDto(c.FactType, codigo, "La cuenta bancaria propia no está vinculada a su cuenta contable: la vincula Tesorería.",
                            combinacion));
                    continue;
                }
                if (!matriz.HayReglaCompatible(c.FactType, codigo, c.Dimensions, c.AccountingDate))
                    faltan.Add(new CombinacionSinReglaDto(c.FactType, codigo,
                        $"La matriz contable de {modulo.Nombre} no tiene regla para el rol {rol.Nombre} de «{hecho.Nombre}»"
                        + (combinacion is null ? string.Empty : $" con {combinacion}") + $": la completa quien tenga {modulo.PermisoDeLaMatriz}.Manage.", combinacion));
            }
        }
        return new EvaluacionContableDto(faltan.Count == 0, politica, faltan);
    }

    /// <summary>Las dimensiones de la pregunta por código, sin el rol ni la cuenta propia por PublicId; nula si no trae ninguna.</summary>
    private static string? Combinacion(IReadOnlyDictionary<string, string> dimensiones)
    {
        var partes = dimensiones.Where(d => d.Key != ClaveDelRol && d.Key != ClaveDeLaCuentaPropia).Select(d => $"{d.Key}={d.Value}").ToList();
        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }

    /// <summary><c>Contabilidad.FaltaDeRegla</c> del módulo a la fecha; <c>Bloquear</c> si no se puede leer (el defecto seguro).</summary>
    public async Task<string> PoliticaAsync(ModuloDeLaMatriz modulo, DateOnly fecha, CancellationToken ct)
    {
        var leida = await lector.LeerAsync(modulo.ModuloDeParametros, ParametrosDeRecaudoYTesoreria.ContabilidadFaltaDeRegla, fecha, ct: ct);
        return leida.IsSuccess && !string.IsNullOrWhiteSpace(leida.Value.Texto) ? leida.Value.Texto : Bloquear;
    }
}
