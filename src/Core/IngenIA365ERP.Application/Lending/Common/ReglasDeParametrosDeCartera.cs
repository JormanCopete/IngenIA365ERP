using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// Los errores de los parámetros de Cartera (feature 013, T42; data-model parte 3 §10.1; api.md §3.4). Mensajes en español claro que
/// dicen qué hacer (FR-013); los datos van en <c>data</c>. (nuevo)
/// </summary>
public static class ErroresDeParametrosDeCartera
{
    public const string CodigoMenosEstrictoQueLaLey = "Lending.Parameter.BelowLegalFloor";

    /// <summary>
    /// <b>(nuevo; C111, C123)</b>: el valor pide algo que ninguna entrega construye todavía (<c>Contabilidad.DiferirComisiones = true</c>,
    /// FR-017; DT §5 C10: el hecho de amortización de comisiones diferidas no lo construye ninguna entrega de K1 a K6b).
    /// </summary>
    public const string CodigoNoSoportadoTodavia = "Lending.Parameter.NotSupportedYet";

    /// <summary>
    /// <b>(nuevo; C111)</b>: <c>Contabilidad.ModoDePaso</c> y sus hermanas de lote en el ámbito <c>LendingFactType</c> de un acompañante que sólo
    /// viaja con su principal (C01; contabilidad.md §10.1): sella el modo del principal.
    /// </summary>
    public const string CodigoAcompananteSinModo = "Lending.Parameter.CompanionHasNoMode";

    /// <summary>
    /// <b>(nuevo; decisión del dueño del 2026-10-16)</b>: los rangos de mora de <c>Centrales.RangosDeMora</c> tienen la forma pero no la regla —límites
    /// que no crecen, un rango negativo que no existe, un tipo de cartera repetido—.
    /// </summary>
    public const string CodigoRangosDeMoraInvalidos = "Lending.Parameter.InvalidArrearsRanges";

    /// <summary>Los rangos de mora no sirven (422), con el motivo.</summary>
    public static Error RangosDeMoraInvalidos(string valor, string motivo) => new ErrorConDatos(CodigoRangosDeMoraInvalidos, motivo,
        new { key = ParametrosDeCartera.CentralesRangosDeMora, value = valor });

    /// <summary>C01: el acompañante no tiene modo propio (422).</summary>
    public static Error AcompananteSinModo(string clave, string tipoDeHecho, string principal) => new ErrorConDatos(CodigoAcompananteSinModo,
        $"«{tipoDeHecho}» viaja siempre con «{principal}» y pasa a contabilidad como él: configure «{clave}» en el tipo de hecho principal.",
        new { key = clave, factType = tipoDeHecho, principal });

    /// <summary>T42: la vigencia es menos estricta que el límite legal vigente en su <c>ValidFrom</c> (422).</summary>
    public static Error MenosEstrictoQueLaLey(string clave, string valor, string codigoDeLimite, decimal limite, string fuente) =>
        new ErrorConDatos(CodigoMenosEstrictoQueLaLey,
            $"El valor {valor} es menos estricto que el límite legal vigente ({codigoDeLimite} = {limite.ToString(CultureInfo.InvariantCulture)}, {fuente}). "
            + "La cooperativa sólo puede ser más estricta.",
            new { key = clave, value = valor, limitCode = codigoDeLimite, limitValue = limite, source = fuente });

    /// <summary>FR-017: diferir comisiones en «true» no se admite mientras ninguna entrega lo implemente (422).</summary>
    public static Error DiferirComisionesNoSoportado() => new ErrorConDatos(CodigoNoSoportadoTodavia,
        "La cooperativa todavía no puede diferir comisiones y costos de originación: ninguna entrega lo implementa.",
        new { key = ParametrosDeCartera.ContabilidadDiferirComisiones, availableFrom = (string?)null });
}

/// <summary>Un límite legal que se aplicó al leer un parámetro de Cartera, con su valor, su fuente y si fue él el que rigió.</summary>
public sealed record LimiteAplicado(string Codigo, decimal Valor, string Fuente, bool Rige);

/// <summary>
/// Un parámetro de Cartera leído a una fecha con su límite legal aplicado (T42): <see cref="Texto"/> y <see cref="Valor"/> son lo que
/// rige —lo más estricto entre la cooperativa y la ley—; <see cref="DeLaCooperativa"/> es lo que la cooperativa configuró (o el
/// defecto), tal como lo devolvió <see cref="ILectorDeParametros"/>.
/// </summary>
public sealed record ParametroDeCartera(ValorDeParametro DeLaCooperativa, string Texto, object? Valor, IReadOnlyList<LimiteAplicado> Limites)
{
    public T? Como<T>() => Valor is T t ? t : default;

    /// <summary>Verdadero si algún límite legal rigió sobre lo configurado.</summary>
    public bool RigeLaLey => Limites.Any(l => l.Rige);
}

/// <summary>
/// Las reglas de los parámetros de Cartera (feature 013, T42, T43, FR-014, FR-017; data-model parte 3 §4.15, §12.1, §12.5): el
/// <see cref="IReglasDeParametros"/> del módulo <c>LND</c>, que la plataforma corre en el alta (<c>AddParameterVersionCommand</c>, por
/// <see cref="ReglasDeParametrosPorModulo"/>), y la lectura con el límite aplicado. (nuevo)
/// <list type="bullet">
/// <item>Una vigencia <b>menos estricta</b> que un límite legal que la acota (<see cref="DefinicionDeParametro.LimitesLegales"/>,
/// valor nacional <c>LIMITE</c> vigente en su <c>ValidFrom</c>) se rechaza con <c>Lending.Parameter.BelowLegalFloor</c>, el límite y su
/// fuente. Vacía (la clave lo admite) no se compara: significa «rige el límite». Si el límite no está replicado, responde
/// <c>NationalValues.Missing</c> (T40: lo que lo necesita se bloquea).</item>
/// <item><c>Contabilidad.DiferirComisiones = true</c> se rechaza con <c>Lending.Parameter.NotSupportedYet</c>: ninguna entrega de esta
/// feature construye la amortización de comisiones diferidas. La guarda se retira sólo cuando una entrega la implemente.</item>
/// <item>Al leer (<see cref="LeerAsync"/>) rige lo más estricto entre la cooperativa y el límite vigente en la fecha.</item>
/// </list>
/// </summary>
public sealed class ReglasDeParametrosDeCartera(IValoresNacionales valoresNacionales, ILectorDeParametros lector) : IReglasDeParametrosDeModulo
{
    public string Modulo => ParametrosDeCartera.Modulo;

    /// <summary>Las claves del modo de paso que un acompañante no tiene (C01): el modo, la granularidad, el disparador y la hora del lote.</summary>
    public static IReadOnlySet<string> ClavesDelModoDePaso { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ParametrosDeCartera.ContabilidadModoDePaso, ParametrosDeCartera.ContabilidadGranularidad,
        ParametrosDeCartera.ContabilidadDisparadorDeLote, ParametrosDeCartera.ContabilidadHoraDeLote,
    };

    public async Task<Result<DecisionDeReglasDeParametro>> EvaluarAsync(AltaDeParametro alta, CancellationToken ct)
    {
        var definicion = alta.Definicion;
        if (alta.ScopeKind == ParameterScopeKind.LendingFactType && ClavesDelModoDePaso.Contains(definicion.Clave)
            && alta.Ambito is { } ambito && Integration.ModoDePasoDeCartera.PrincipalDe(ambito.Code) is { } principal)
            return Result.Failure<DecisionDeReglasDeParametro>(ErroresDeParametrosDeCartera.AcompananteSinModo(definicion.Clave, ambito.Code, principal));
        if (definicion.Clave == ParametrosDeCartera.ContabilidadDiferirComisiones && alta.Valor == "true")
            return Result.Failure<DecisionDeReglasDeParametro>(ErroresDeParametrosDeCartera.DiferirComisionesNoSoportado());
        if (definicion.Clave == ParametrosDeCartera.CentralesRangosDeMora
            && Domain.Lending.Regulatory.RangosDeMoraParaCentrales.Validar(alta.Valor) is { } motivo)
            return Result.Failure<DecisionDeReglasDeParametro>(ErroresDeParametrosDeCartera.RangosDeMoraInvalidos(alta.Valor, motivo));

        foreach (var codigo in definicion.LimitesLegales)
        {
            if (LimitesDeParametro.Comparable(definicion, alta.Valor, codigo) is null) continue;

            var limite = await valoresNacionales.LeerAsync(CodigosDeValorNacional.Limite, codigo, alta.ValidFrom, null, ct);
            if (limite.IsFailure) return Result.Failure<DecisionDeReglasDeParametro>(limite.Error);
            if (limite.Value.Valor is not { } valorDelLimite) continue;

            if (LimitesDeParametro.EsMenosEstricto(definicion, alta.Valor, codigo, valorDelLimite))
                return Result.Failure<DecisionDeReglasDeParametro>(ErroresDeParametrosDeCartera.MenosEstrictoQueLaLey(
                    definicion.Clave, alta.Valor, codigo, valorDelLimite, limite.Value.Fuente));
        }

        return Result.Success(DecisionDeReglasDeParametro.Adelante);
    }

    /// <summary>
    /// El parámetro de Cartera que rige a <paramref name="fecha"/> (con lo que se sabía hasta <paramref name="informacionHasta"/>, si
    /// viene): lo de la cooperativa por <see cref="ILectorDeParametros"/> y, encima, cada límite legal vigente en la fecha; rige lo más
    /// estricto. Un límite que falta es <c>NationalValues.Missing</c> (y su alerta, la levanta el lector de valores nacionales).
    /// </summary>
    public async Task<Result<ParametroDeCartera>> LeerAsync(string clave, DateOnly fecha, DateTime? informacionHasta = null,
        ParameterScopeKind ambito = ParameterScopeKind.None, int ambitoId = 0, CancellationToken ct = default)
    {
        var leido = informacionHasta is null
            ? await lector.LeerAsync(Modulo, clave, fecha, ambito, ambitoId, ct)
            : await lector.LeerAsync(Modulo, clave, fecha, informacionHasta, ambito, ambitoId, ct);
        if (leido.IsFailure) return Result.Failure<ParametroDeCartera>(leido.Error);

        var definicion = leido.Value.Definicion;
        var texto = leido.Value.Texto;
        var aplicados = new List<LimiteAplicado>(definicion.LimitesLegales.Count);
        foreach (var codigo in definicion.LimitesLegales)
        {
            var limite = await valoresNacionales.LeerAsync(CodigosDeValorNacional.Limite, codigo, fecha, informacionHasta, ct);
            if (limite.IsFailure) return Result.Failure<ParametroDeCartera>(limite.Error);
            if (limite.Value.Valor is not { } valorDelLimite) continue;

            var masEstricto = LimitesDeParametro.MasEstricto(definicion, texto, codigo, valorDelLimite);
            aplicados.Add(new LimiteAplicado(codigo, valorDelLimite, limite.Value.Fuente, masEstricto != texto));
            texto = masEstricto;
        }

        var interpretado = definicion.Interpretar(texto);
        return interpretado.Admitido
            ? Result.Success(new ParametroDeCartera(leido.Value, interpretado.Texto!, interpretado.Valor, aplicados))
            : Result.Failure<ParametroDeCartera>(ErroresDeParametros.ValorNoAdmitido(definicion, texto));
    }
}
