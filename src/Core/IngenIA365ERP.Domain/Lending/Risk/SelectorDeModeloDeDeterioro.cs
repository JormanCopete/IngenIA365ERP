using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>El perfil regulatorio vigente que mira el selector: tipo de organización y, si es fondo de empleados, su categoría.</summary>
public sealed record PerfilParaModelo(string? TipoDeOrganizacion, string? CategoriaDeFondo);

/// <summary>La elección voluntaria vigente al cierre (K6a, <c>LND_ImpairmentModelElections</c>): adopción o desistimiento y sus segmentos.</summary>
public sealed record EleccionDeModelo(ImpairmentModelElectionKind Clase, IReadOnlyList<string> Segmentos)
{
    /// <summary>K6a (T1014): el primer cierre al que aplica (<c>ValidFrom</c>); nulo, desde siempre.</summary>
    public DateOnly? Desde { get; init; }
}

/// <summary>K6a (T1014): una versión del perfil regulatorio con su vigencia (<c>COR_RegulatoryProfileVersions.ValidFrom</c>).</summary>
public sealed record VigenciaDePerfil(DateOnly Desde, PerfilParaModelo Perfil);

/// <summary>
/// El modelo de un segmento de pérdida esperada (data-model §23.1 <c>ModelBySegmentJson</c>): modelo, fundamento y la marca
/// «modelo obligatorio no disponible» (D28).
/// </summary>
public sealed record ModeloDelSegmento(string Segmento, ImpairmentModel Modelo, ModelBasis Fundamento, bool ModeloObligatorioNoDisponible);

/// <summary>Lo que el selector decide para un cierre: el modelo de cada segmento y la marca del cierre.</summary>
public sealed record SeleccionDeModelos(IReadOnlyList<ModeloDelSegmento> Segmentos, bool Obligada)
{
    /// <summary>La marca del encabezado del cierre (<c>ObligatoryModelUnavailable</c>).</summary>
    public bool ModeloObligatorioNoDisponible => Segmentos.Any(s => s.ModeloObligatorioNoDisponible);

    /// <summary>
    /// El modelo de un crédito por su segmento de pérdida esperada (del catálogo de modalidades); sin segmento de pérdida esperada
    /// (vivienda, microcrédito, comercial de persona jurídica), el Anexo 1 por no estar obligado ese segmento.
    /// </summary>
    public ModeloDelSegmento ParaCredito(string? segmentoDePerdidaEsperada) =>
        segmentoDePerdidaEsperada is null
            ? new(string.Empty, ImpairmentModel.ArrearsAnnex1, ModelBasis.NotObligated, false)
            : Segmentos.FirstOrDefault(s => s.Segmento == segmentoDePerdidaEsperada)
              ?? new(segmentoDePerdidaEsperada, ImpairmentModel.ArrearsAnnex1, ModelBasis.NotObligated, false);
}

/// <summary>
/// El selector del modelo de deterioro (feature 013, US9, T0695; T50, R58; FR-105, D28; SC-023), por segmento de pérdida esperada:
/// <list type="bullet">
/// <item>Obligada por la <see cref="ReglaDePerdidaEsperada"/> vigente y su perfil, en un segmento cubierto: pérdida esperada
/// <c>Obligated</c> si la entrega desplegada es K6a o posterior; si no, <b>Anexo 1 con la marca</b> «modelo obligatorio no
/// disponible» (D28: sigue cerrando, con alerta).</item>
/// <item>No obligada (o segmento no cubierto) con una adopción voluntaria vigente que lo cubre y K6a desplegada: pérdida esperada
/// <c>Voluntary</c>; un desistimiento o la falta de K6a la dejan en el Anexo 1.</item>
/// <item>Lo demás: Anexo 1 <c>NotObligated</c>.</item>
/// </list>
/// La disponibilidad de K6a la decide <see cref="EntregaDeCartera"/> (<c>CatalogoDeCartera.EntregaVigente</c>), nunca una bandera
/// de ambiente. Puro. (nuevo)
/// </summary>
public static class SelectorDeModeloDeDeterioro
{
    /// <summary>¿La entrega desplegada trae la pérdida esperada?</summary>
    public static bool PerdidaEsperadaDisponible(EntregaDeCartera entrega) => entrega >= EntregaDeCartera.K6a;

    /// <summary>
    /// K6a (T1014; US9-6, US9-7, US9-9): el modelo de cada segmento <b>al cierre</b> de <paramref name="fechaDelCierre"/>: la versión del perfil
    /// vigente a esa fecha (una obligación sobrevenida rige desde el primer cierre dentro de su vigencia) y la última elección voluntaria cuyo
    /// <c>ValidFrom</c> ya llegó (la adopción rige desde su fecha y el desistimiento vuelve al Anexo 1 desde la suya). Nunca mira hacia atrás:
    /// un cierre anterior a una elección se selecciona igual que antes de registrarla.
    /// </summary>
    public static SeleccionDeModelos SeleccionarAl(DateOnly fechaDelCierre, ReglaDePerdidaEsperada? regla, IEnumerable<VigenciaDePerfil> perfiles,
        IEnumerable<EleccionDeModelo> elecciones, EntregaDeCartera entrega)
    {
        ArgumentNullException.ThrowIfNull(perfiles);
        ArgumentNullException.ThrowIfNull(elecciones);
        var perfil = perfiles.Where(p => p.Desde <= fechaDelCierre).OrderByDescending(p => p.Desde).FirstOrDefault()?.Perfil;
        var eleccion = elecciones.Where(e => (e.Desde ?? DateOnly.MinValue) <= fechaDelCierre).OrderByDescending(e => e.Desde ?? DateOnly.MinValue)
            .FirstOrDefault();
        return Seleccionar(regla, perfil, eleccion, entrega);
    }

    public static SeleccionDeModelos Seleccionar(ReglaDePerdidaEsperada? regla, PerfilParaModelo? perfil, EleccionDeModelo? eleccion, EntregaDeCartera entrega)
    {
        var obligada = regla is not null && perfil is not null && regla.Obliga(perfil.TipoDeOrganizacion, perfil.CategoriaDeFondo);
        var disponible = PerdidaEsperadaDisponible(entrega);
        var segmentos = ReglaDePerdidaEsperada.SegmentosConocidos.Select(segmento =>
        {
            if (obligada && regla!.SegmentosCubiertos.Contains(segmento, StringComparer.Ordinal))
                return disponible
                    ? new ModeloDelSegmento(segmento, ImpairmentModel.ExpectedLossAnnex2, ModelBasis.Obligated, false)
                    : new ModeloDelSegmento(segmento, ImpairmentModel.ArrearsAnnex1, ModelBasis.Obligated, true);
            if (disponible && eleccion is { Clase: ImpairmentModelElectionKind.VoluntaryAdoption } e && e.Segmentos.Contains(segmento, StringComparer.Ordinal))
                return new ModeloDelSegmento(segmento, ImpairmentModel.ExpectedLossAnnex2, ModelBasis.Voluntary, false);
            return new ModeloDelSegmento(segmento, ImpairmentModel.ArrearsAnnex1, ModelBasis.NotObligated, false);
        }).ToList();
        return new(segmentos, obligada);
    }
}
