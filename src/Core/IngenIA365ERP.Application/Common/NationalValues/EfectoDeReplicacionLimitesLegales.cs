using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Application.Common.Parameters;

namespace IngenIA365ERP.Application.Common.NationalValues;

/// <summary>
/// El efecto de la réplica sobre los límites legales (feature 013, T42; data-model parte 3 §4.3, §12.5): cuando llega un
/// <c>LIMITE</c> que acota un parámetro de la cooperativa (<see cref="LimiteLegal.ClaveDeParametro"/>) y la vigencia de ese
/// parámetro queda <b>menos estricta</b> que el límite (<see cref="CatalogoDeLimitesLegales.EsMenosEstricto"/>), avisa
/// <c>Cartera.LimiteLegalMasEstricto</c> (<c>DedupKey …:{límite}</c>) y lo anota en el resumen. No cambia el parámetro: al leer
/// ya rige lo más estricto; la cooperativa decide si registra una vigencia nueva. Los parámetros de Cartera viven en
/// <c>COR_ParameterVersions</c> con el módulo <c>LND</c> (T43); se compara la vigencia de cada ámbito en la fecha en que el límite
/// empieza a regir (o hoy, si ya rige). (nuevo)
/// </summary>
public sealed class EfectoDeReplicacionLimitesLegales : IEfectoDeReplicacion
{
    public const string NombreDelEfecto = "limites-legales";

    /// <summary>El módulo de los parámetros de Cartera en <c>COR_ParameterVersions</c> (T43).</summary>
    public const string ModuloDeParametros = "LND";

    public string Nombre => NombreDelEfecto;

    public async Task<EfectoAplicado> AlReplicarAsync(IApplicationDbContext db, LoteReplicado lote, CancellationToken ct)
    {
        var limites = lote.Valores
            .Where(v => v.Code == CodigosDeValorNacional.Limite && v.Value is not null)
            .Select(v => (Copia: v, Limite: CatalogoDeLimitesLegales.Buscar(v.Key)))
            .Where(x => x.Limite?.ClaveDeParametro is not null)
            .ToList();
        if (limites.Count == 0) return EfectoAplicado.Nada;

        // Las vigencias por el lector único de parámetros (LosParametrosSeLeenEnUnSoloSitio), sobre el contexto de la réplica.
        var claves = limites.Select(l => l.Limite!.ClaveDeParametro!).ToHashSet(StringComparer.Ordinal);
        var parametros = (await new LectorDeParametros(db).VigenciasAsync(ModuloDeParametros, null, ct))
            .Where(p => claves.Contains(p.Key))
            .ToList();

        var alertas = new List<AlertaALevantar>();
        var notas = new List<string>();
        foreach (var (copia, limite) in limites)
        {
            var fecha = copia.ValidFrom > lote.Hoy ? copia.ValidFrom : lote.Hoy;
            var vigentes = parametros.Where(p => p.Key == limite!.ClaveDeParametro && p.VigenteEn(fecha)).ToList();
            foreach (var parametro in vigentes)
            {
                if (!EsMenosEstricto(parametro.Key, parametro.Value, limite!.Codigo, copia.Value!.Value)) continue;

                var nota = $"{limite.Codigo} = {copia.Value.Value.ToString(CultureInfo.InvariantCulture)} desde {copia.ValidFrom:yyyy-MM-dd} "
                    + $"es más estricto que {parametro.Key} = {parametro.Value} de la cooperativa.";
                notas.Add(nota);
                alertas.Add(new AlertaALevantar(
                    AlertasDeValoresNacionales.LimiteLegalMasEstricto,
                    $"Límite legal más estricto: {limite.Codigo}",
                    $"La norma ({copia.Source}) fija {limite.Codigo} en {copia.Value.Value.ToString(CultureInfo.InvariantCulture)} desde el {copia.ValidFrom:yyyy-MM-dd}. "
                    + $"El parámetro {parametro.Key} de la cooperativa vale {parametro.Value}: desde esa fecha rige el límite. "
                    + "Registre una vigencia nueva del parámetro si quiere dejarlo explícito.",
                    EntityType: nameof(NationalValue), EntityPublicId: copia.PublicId,
                    DedupKey: AlertasDeValoresNacionales.ClaveDeLimite(limite.Codigo)));
                break;
            }
        }

        return new EfectoAplicado(new Dictionary<string, int> { ["masEstrictos"] = alertas.Count }, notas, alertas);
    }

    /// <summary>
    /// Feature 013 (T0291): la vigencia se compara con el límite en la unidad del límite y según el tipo de la clave
    /// (<see cref="LimitesDeParametro"/>: un horario por la punta que el límite acota, un sí/no como 1 o 0, un número tal cual); una
    /// vigencia vacía no tiene valor propio (rige el límite) y no alerta. Si la clave no está en el catálogo, como número.
    /// </summary>
    private static bool EsMenosEstricto(string clave, string? valor, string codigoDeLimite, decimal limite)
    {
        var definicion = CatalogoDeParametros.Buscar(ModuloDeParametros, clave);
        if (definicion is not null) return LimitesDeParametro.EsMenosEstricto(definicion, valor, codigoDeLimite, limite);
        return decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero)
            && CatalogoDeLimitesLegales.EsMenosEstricto(codigoDeLimite, numero, limite);
    }
}
