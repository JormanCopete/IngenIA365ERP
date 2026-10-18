using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>
/// Una celda que la política tocó: su ruta (<see cref="PoliticaSarc"/>), lo que dice la norma, lo que propone la política y lo que
/// rige. <see cref="MenosEstricta"/> = la política es más blanda que la norma vigente: rige la norma, y al registrar la política es
/// <c>Lending.RiskPolicy.LessStrict</c> con <c>data.cells[] { path, norm, proposed }</c>.
/// </summary>
public sealed record CeldaDePolitica(string Ruta, decimal Norma, decimal Propuesta, decimal Efectiva)
{
    public bool MenosEstricta => Efectiva != Propuesta;

    public bool GanaLaPolitica => Efectiva == Propuesta && Propuesta != Norma;
}

/// <summary>
/// El resultado de combinar la norma y la política: el <see cref="Anexo1"/> que rige (<see cref="Efectiva"/>), cada celda tocada, las
/// menos estrictas, el deterioro general adicional (que sólo trae la política) y las celdas que la norma vigente no tiene
/// (<see cref="Errores"/>, con su ruta: al registrar son <c>Lending.RiskPolicy.SchemaInvalid</c>; al cerrar, se ignoran y se marcan).
/// </summary>
public sealed record ResultadoDePoliticaEfectiva(
    Anexo1 Efectiva,
    IReadOnlyList<CeldaDePolitica> Celdas,
    decimal DeterioroGeneralAdicional,
    bool AdicionalCompensaIndividual,
    IReadOnlyList<ErrorDeDocumentoNormativo> Errores)
{
    public IReadOnlyList<CeldaDePolitica> MenosEstrictas => Celdas.Where(c => c.MenosEstricta).ToList();
}

/// <summary>
/// K6a (T1016): el resultado de combinar el <see cref="Anexo2"/> vigente con la política: el que rige, cada celda tocada y las que la norma no
/// tiene (con su ruta).
/// </summary>
public sealed record ResultadoDePoliticaEfectivaAnexo2(Anexo2 Efectiva, IReadOnlyList<CeldaDePolitica> Celdas, IReadOnlyList<ErrorDeDocumentoNormativo> Errores)
{
    public IReadOnlyList<CeldaDePolitica> MenosEstrictas => Celdas.Where(c => c.MenosEstricta).ToList();
}

/// <summary>
/// La política SARC efectiva (feature 013, US9, T0692; T45, R54; FR-099, FR-103, FR-104): <b>el máximo por celda</b> entre la norma
/// vigente y la política validada, «máximo» en el sentido de lo más estricto:
/// <list type="bullet">
/// <item>inicio de una categoría: el <b>menor</b> número de días (se llega antes a una categoría peor); el fin de la anterior se corre
/// con él;</item>
/// <item>porcentaje de deterioro y mínimo del general: el <b>mayor</b>;</item>
/// <item>fracción admisible de una garantía: la <b>menor</b>;</item>
/// <item>deterioro general adicional y su uso para compensar el individual: sólo los trae la política.</item>
/// </list>
/// La comparación se hace cada vez contra la norma que rige ese día: si la norma se endurece después de validar la política, gana la
/// norma sin tocar la política. Pura: sin IO, sólo <c>decimal</c>, sin valores legales (todo llega en los dos documentos). (nuevo)
/// </summary>
public static class PoliticaSarcEfectiva
{
    public static ResultadoDePoliticaEfectiva Combinar(Anexo1 norma, PoliticaSarc? politica)
    {
        ArgumentNullException.ThrowIfNull(norma);
        politica ??= PoliticaSarc.Vacia;
        var celdas = new List<CeldaDePolitica>();
        var errores = new List<ErrorDeDocumentoNormativo>();

        foreach (var x in politica.InicioDeCategorias.Where(x => norma.Segmento(x.Segmento) is null)
                     .Select(x => x.Segmento).Concat(politica.Porcentajes.Where(p => norma.Segmento(p.Segmento) is null).Select(p => p.Segmento)).Distinct())
            errores.Add(new($"segments.{x}", $"La norma vigente no trae el segmento {x}."));

        var segmentos = norma.Segmentos.Select(s => CombinarSegmento(s, politica, celdas, errores)).ToList();
        var garantias = norma.Garantias.Select(g => CombinarGarantia(g, politica, celdas)).ToList();
        foreach (var t in politica.Garantias.Where(t => !norma.Garantias.Any(g => g.Naturaleza == t.Naturaleza && g.Tramos.Any(n => n.MesesDesde == t.MesesDesde))))
            errores.Add(new(PoliticaSarc.RutaDeTramo(t.Naturaleza, t.MesesDesde), "La norma vigente no trae ese tramo de garantía."));

        var general = norma.DeterioroGeneral.Select(d =>
        {
            if (politica.DeterioroGeneralMinimo is not { } propuesta) return d;
            var efectiva = Math.Max(d.FraccionMinima, propuesta);
            celdas.Add(new(PoliticaSarc.RutaDelMinimoGeneral + (d.Perfil == Anexo1.PerfilGeneral ? string.Empty : "." + d.Perfil),
                d.FraccionMinima, propuesta, efectiva));
            return d with { FraccionMinima = efectiva };
        }).ToList();
        if (politica.DeterioroGeneralAdicional > 0m)
            celdas.Add(new(PoliticaSarc.RutaDelAdicionalGeneral, 0m, politica.DeterioroGeneralAdicional, politica.DeterioroGeneralAdicional));

        return new(norma.ConTablas(segmentos, garantias, general), celdas, politica.DeterioroGeneralAdicional,
            politica.AdicionalCompensaIndividual, errores);
    }

    /// <summary>
    /// K6a (T1016; data-model §23.10): el <see cref="Anexo2"/> que rige con la política del consejo, <b>el máximo por celda</b> en la PI de cada
    /// calificación, en cada tramo de PDI y en la PDI sin incumplimiento (más alta es más estricta). Una celda que la norma vigente no tiene es un
    /// error con su ruta; una menos estricta deja la de la norma. Ni los coeficientes, ni los cortes, ni la homologación se tocan: son el modelo, no
    /// una tolerancia.
    /// </summary>
    public static ResultadoDePoliticaEfectivaAnexo2 CombinarAnexo2(Anexo2 norma, PoliticaSarc? politica)
    {
        ArgumentNullException.ThrowIfNull(norma);
        politica ??= PoliticaSarc.Vacia;
        var celdas = new List<CeldaDePolitica>();
        var errores = new List<ErrorDeDocumentoNormativo>();

        var pi = norma.ProbabilidadesDeIncumplimiento.Select(t => t with
        {
            PorCalificacion = t.PorCalificacion.Select(p =>
            {
                var propuesta = politica.ProbabilidadesDeIncumplimiento.FirstOrDefault(x => x.Segmento == t.Segmento && x.TipoDeOrganizacion == t.TipoDeOrganizacion
                    && x.Categoria == p.Categoria);
                if (propuesta is null) return p;
                var efectiva = Math.Max(p.Fraccion, propuesta.Fraccion);
                celdas.Add(new(PoliticaSarc.RutaDePi(t.Segmento, t.TipoDeOrganizacion, p.Categoria), p.Fraccion, propuesta.Fraccion, efectiva));
                return p with { Fraccion = efectiva };
            }).ToList(),
        }).ToList();
        foreach (var x in politica.ProbabilidadesDeIncumplimiento.Where(x => norma.TablaDePi(x.Segmento, x.TipoDeOrganizacion) is null))
            errores.Add(new(PoliticaSarc.RutaDePi(x.Segmento, x.TipoDeOrganizacion, x.Categoria), "La norma vigente no trae esa PI."));

        var pdi = norma.PerdidasDadoElIncumplimiento.Select(g => g with
        {
            Tramos = g.Tramos.Select(t =>
            {
                var propuesta = politica.PerdidasDadoElIncumplimiento.FirstOrDefault(x => x.TipoDeGarantia == g.TipoDeGarantia && x.DiasDesde == t.DiasDesde);
                if (propuesta is null) return t;
                var efectiva = Math.Max(t.Fraccion, propuesta.Fraccion);
                celdas.Add(new(PoliticaSarc.RutaDePdi(g.TipoDeGarantia, t.DiasDesde), t.Fraccion, propuesta.Fraccion, efectiva));
                return t with { Fraccion = efectiva };
            }).ToList(),
        }).ToList();
        foreach (var x in politica.PerdidasDadoElIncumplimiento.Where(x => norma.Pdi(x.TipoDeGarantia)?.Tramos.Any(t => t.DiasDesde == x.DiasDesde) != true))
            errores.Add(new(PoliticaSarc.RutaDePdi(x.TipoDeGarantia, x.DiasDesde), "La norma vigente no trae ese tramo de PDI."));

        var segmentos = norma.Segmentos.Select(s => s with
        {
            PdiSinIncumplimiento = s.PdiSinIncumplimiento.Select(p =>
            {
                var propuesta = politica.PerdidasSinIncumplimiento.FirstOrDefault(x => x.Segmento == s.Codigo && x.TipoDeGarantia == p.TipoDeGarantia);
                if (propuesta is null) return p;
                var efectiva = Math.Max(p.Fraccion, propuesta.Fraccion);
                celdas.Add(new(PoliticaSarc.RutaDePdiSinIncumplimiento(s.Codigo, p.TipoDeGarantia), p.Fraccion, propuesta.Fraccion, efectiva));
                return p with { Fraccion = efectiva };
            }).ToList(),
        }).ToList();
        foreach (var x in politica.PerdidasSinIncumplimiento.Where(x =>
                     norma.Segmento(x.Segmento)?.PdiSinIncumplimiento.Any(p => p.TipoDeGarantia == x.TipoDeGarantia) != true))
            errores.Add(new(PoliticaSarc.RutaDePdiSinIncumplimiento(x.Segmento, x.TipoDeGarantia), "La norma vigente no trae esa PDI sin incumplimiento."));

        return new(norma.ConTablas(segmentos, pi, pdi), celdas, errores);
    }

    private static SegmentoSarc CombinarSegmento(SegmentoSarc s, PoliticaSarc politica, List<CeldaDePolitica> celdas, List<ErrorDeDocumentoNormativo> errores)
    {
        // Inicios de categoría: el menor; el fin de la anterior se corre al día antes del inicio siguiente.
        var propios = politica.InicioDeCategorias.Where(x => x.Segmento == s.Codigo).ToList();
        var desde = s.Categorias.Select(c => c.DiasDesde).ToArray();
        var celdasDeInicio = new List<CeldaDePolitica>();
        foreach (var x in propios)
        {
            var i = s.Categorias.ToList().FindIndex(c => c.Categoria == x.Categoria);
            var efectiva = Math.Min(desde[i], x.DiasDesde);
            celdasDeInicio.Add(new(PoliticaSarc.RutaDeInicio(s.Codigo, x.Categoria), desde[i], x.DiasDesde, efectiva));
            desde[i] = efectiva;
        }
        var categorias = s.Categorias;
        if (propios.Count > 0)
        {
            var creciente = desde.Zip(desde.Skip(1)).All(p => p.First < p.Second);
            if (!creciente)
                errores.Add(new($"segments.{s.Codigo}.categories", "Con la política, los inicios de categoría dejan de crecer: rige la norma en ese segmento."));
            else
            {
                celdas.AddRange(celdasDeInicio);
                categorias = s.Categorias.Select((c, i) => new RangoDeCategoria(c.Categoria, desde[i], i + 1 < desde.Length ? desde[i + 1] - 1 : null)).ToList();
            }
        }

        // Porcentajes: el mayor.
        var porcentajes = s.Porcentajes.ToList();
        foreach (var p in politica.Porcentajes.Where(p => p.Segmento == s.Codigo))
        {
            var candidatos = porcentajes.Select((n, i) => (n, i)).Where(t => t.n.Categoria == p.Categoria && t.n.Subcategoria == p.Subcategoria
                && (p.CondicionDesde is null || t.n.Condicion?.Desde == p.CondicionDesde)).ToList();
            var ruta = $"segments.{s.Codigo}.impairmentRates.{p.Categoria}" + (p.Subcategoria == RatingSubcategory.None ? string.Empty : "." + p.Subcategoria);
            if (candidatos.Count != 1)
            {
                errores.Add(new(ruta, candidatos.Count == 0
                    ? "La norma vigente no trae ese porcentaje."
                    : "La norma trae varios porcentajes de esa categoría: la celda dice desde cuándo (conditionFrom)."));
                continue;
            }
            var (norma, indice) = candidatos[0];
            var efectiva = Math.Max(norma.Fraccion, p.Fraccion);
            celdas.Add(new(PoliticaSarc.RutaDePorcentaje(s.Codigo, norma), norma.Fraccion, p.Fraccion, efectiva));
            porcentajes[indice] = norma with { Fraccion = efectiva };
        }

        return s with { Categorias = categorias, Porcentajes = porcentajes };
    }

    private static GarantiaPorMeses CombinarGarantia(GarantiaPorMeses g, PoliticaSarc politica, List<CeldaDePolitica> celdas)
    {
        var tramos = g.Tramos.Select(t =>
        {
            var p = politica.Garantias.FirstOrDefault(x => x.Naturaleza == g.Naturaleza && x.MesesDesde == t.MesesDesde);
            if (p is null) return t;
            var efectiva = Math.Min(t.FraccionAdmisible, p.FraccionAdmisible);
            celdas.Add(new(PoliticaSarc.RutaDeTramo(g.Naturaleza, t.MesesDesde), t.FraccionAdmisible, p.FraccionAdmisible, efectiva));
            return t with { FraccionAdmisible = efectiva };
        }).ToList();
        return g with { Tramos = tramos };
    }
}
