using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Application.Lending.Engine;

/// <summary>
/// Las series regulatorias de una modalidad en un rango, tal como las recibe la reconstrucción (feature 013, US4, T0493; T38, T40, T44):
/// la usura de la modalidad certificada que el catálogo vigente asigna (bitemporal, conocida en R), los festivos nacionales replicados, el
/// umbral de suspensión del Anexo 1 (la primera altura de la categoría desde la que se suspende, en su segmento SARC) y la tolerancia de
/// saldo vencido. <see cref="DiasSinUsura"/> son los días del rango sin usura certificada (la causación real se frena en ellos). (nuevo)
/// </summary>
public sealed record SeriesDeModalidad(
    string Modalidad,
    string? Subsegmento,
    string? ClaveDeUsura,
    string? SegmentoSarc,
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<ValorDeUsura> Usura,
    IReadOnlyList<DateOnly> Festivos,
    IReadOnlyList<ValorConVigencia<int>> UmbralesDeSuspension,
    IReadOnlyList<ValorConVigencia<decimal>> Tolerancias,
    IReadOnlyList<DateOnly> DiasSinUsura)
{
    /// <summary>Si la modalidad tiene usura certificada (la vivienda tiene tope propio y no la exige).</summary>
    public bool ExigeUsura => ClaveDeUsura is not null;

    /// <summary>
    /// <c>NationalValues.Missing</c> si <paramref name="fecha"/> no tiene usura certificada para la modalidad (y la exige); éxito si la
    /// tiene o no la exige.
    /// </summary>
    public Result UsuraDe(DateOnly fecha) =>
        ExigeUsura && DiasSinUsura.Contains(fecha)
            ? Result.Failure(ErroresDeValoresNacionales.Faltante(CodigosDeValorNacional.Usura, ClaveDeUsura!, fecha))
            : Result.Success();

    /// <summary>La entrada con estas series (sustituye las que traiga).</summary>
    public EntradaDeReconstruccion Aplicar(EntradaDeReconstruccion entrada) => entrada with
    {
        Usura = Usura,
        Festivos = Festivos,
        UmbralesDeSuspension = UmbralesDeSuspension,
        Tolerancias = Tolerancias,
    };
}

/// <summary>
/// El lector de las series regulatorias que la causación y la liquidación de Cartera necesitan (feature 013, US4, T0493; DT §2.9): se apoya
/// sólo en los lectores únicos de la plataforma —<see cref="IValoresNacionales"/>, <see cref="INormativaVigente"/>, <see cref="ICalendarioHabil"/>
/// y los parámetros de Cartera—; nunca lee <c>COR_NationalValues</c> directamente (<c>LosValoresNacionalesSeLeenEnUnSoloSitio</c>). (nuevo)
/// </summary>
public interface ISeriesRegulatorias
{
    /// <summary>
    /// Las series de <paramref name="modalidad"/> y <paramref name="subsegmento"/> de <paramref name="desde"/> a <paramref name="hasta"/> con lo
    /// que se sabía hasta <paramref name="informacionHasta"/> (nulo = todo lo replicado). El catálogo de modalidades y el Anexo 1 son los
    /// vigentes en <paramref name="hasta"/>. Falla con <c>NationalValues.Missing</c> si no hay catálogo vigente o la modalidad no está en él.
    /// </summary>
    Task<Result<SeriesDeModalidad>> LeerAsync(string modalidad, string? subsegmento, DateOnly desde, DateOnly hasta, DateTime? informacionHasta,
        CancellationToken ct);
}

/// <summary>
/// <see cref="ISeriesRegulatorias"/> sobre los lectores únicos (feature 013, US4, T0493). La serie de usura se lee con
/// <see cref="IValoresNacionales.LeerSerieAsync"/> <b>una vez por corrida</b>: este servicio es del ámbito de la tanda o de la consulta y
/// recuerda lo que leyó por modalidad y R; un rango que cabe en uno ya leído no vuelve a la base. Lo vigente cada día (resuelto con
/// <c>VigenciaBitemporal</c> a R por el lector) se entrega como las versiones bitemporales que la reconstrucción vuelve a filtrar por R: el
/// resultado es el mismo. Los festivos son los días no hábiles distintos del domingo del calendario del art. 829 (D2). (nuevo)
/// </summary>
public sealed class SeriesRegulatoriasDeCartera(
    IValoresNacionales valores,
    INormativaVigente normativa,
    ICalendarioHabil calendario,
    ReglasDeParametrosDeCartera parametros) : ISeriesRegulatorias
{
    private readonly Dictionary<(string, string?, DateTime?), SeriesDeModalidad> _leidas = [];

    public async Task<Result<SeriesDeModalidad>> LeerAsync(string modalidad, string? subsegmento, DateOnly desde, DateOnly hasta, DateTime? informacionHasta,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modalidad);
        if (hasta < desde) (desde, hasta) = (hasta, desde);
        var clave = (modalidad, subsegmento, informacionHasta);
        if (_leidas.TryGetValue(clave, out var leida) && leida.Desde <= desde && leida.Hasta >= hasta) return Result.Success(leida);
        if (leida is not null)
        {
            desde = desde < leida.Desde ? desde : leida.Desde;
            hasta = hasta > leida.Hasta ? hasta : leida.Hasta;
        }

        var catalogo = await normativa.CatalogoDeModalidadesAsync(hasta, informacionHasta, ct);
        if (catalogo.IsFailure) return Result.Failure<SeriesDeModalidad>(catalogo.Error);
        var combinacion = catalogo.Value.Documento.Buscar(modalidad, subsegmento);
        if (combinacion is null)
            return Result.Failure<SeriesDeModalidad>(ErroresDeValoresNacionales.Faltante(CodigosDeValorNacional.Usura, modalidad, hasta));

        // Usura: la serie del rango en una consulta; cada valor distinto, una vez.
        var usura = new List<ValorDeUsura>();
        var sinUsura = new List<DateOnly>();
        if (combinacion.UsuryKey is { } claveDeUsura)
        {
            var serie = await valores.LeerSerieAsync(CodigosDeValorNacional.Usura, claveDeUsura, desde, hasta, informacionHasta, ct);
            var vistos = new HashSet<Guid>();
            foreach (var (dia, valor) in serie.Dias.OrderBy(d => d.Key))
            {
                if (valor?.Valor is not { } ea)
                {
                    sinUsura.Add(dia);
                    continue;
                }
                if (vistos.Add(valor.PublicId))
                    usura.Add(new ValorDeUsura(valor.ValidFrom, ea, valor.KnownAt, valor.Sequence, valor.SourcePublicId) { ValidTo = valor.ValidTo });
            }
        }

        // Festivos nacionales: los no hábiles que no son domingo en el calendario del art. 829 (con margen para la fecha exigible).
        var cal = await calendario.ObtenerAsync(desde, hasta.AddDays(DiasDeMargenDeFestivos), DiasDeDescanso.SoloDomingo, informacionHasta, ct);
        var festivos = new List<DateOnly>();
        for (var d = desde; d <= hasta.AddDays(DiasDeMargenDeFestivos); d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Sunday && !cal.EsHabil(d)) festivos.Add(d);

        // Umbral de suspensión: la primera altura de la categoría de suspensión del segmento SARC en el Anexo 1 vigente.
        // (el vigente al empezar el rango rige desde su comienzo; el vigente al terminarlo, desde su propia vigencia si es otro).
        var umbrales = new List<ValorConVigencia<int>>();
        foreach (var fecha in new[] { desde, hasta }.Distinct())
        {
            var anexo = await normativa.LeerAsync(RegulatoryDocumentKind.SarcAnexo1, fecha, Anexo1.Leer, informacionHasta, ct);
            if (anexo.IsFailure || anexo.Value.Documento.Segmentos.FirstOrDefault(s => s.Codigo == combinacion.SarcSegment) is not { } segmento
                || segmento.Categorias.FirstOrDefault(c => c.Categoria == segmento.CategoriaDeSuspension) is not { } categoria) continue;
            var vigenteDesde = umbrales.Count == 0 ? (anexo.Value.ValidFrom < desde ? anexo.Value.ValidFrom : desde) : anexo.Value.ValidFrom;
            if (umbrales.Count == 0 || umbrales[^1].Valor != categoria.DiasDesde) umbrales.Add(new ValorConVigencia<int>(vigenteDesde, categoria.DiasDesde));
        }

        // Tolerancia de saldo vencido: la vigente al empezar el rango y la vigente al terminarlo (si cambió).
        var tolerancias = new List<ValorConVigencia<decimal>>();
        foreach (var fecha in new[] { desde, hasta }.Distinct())
        {
            var t = await parametros.LeerAsync(ParametrosDeCartera.MoraToleranciaDeSaldoVencido, fecha, informacionHasta, ct: ct);
            if (t.IsFailure || t.Value.Valor is not decimal valor) continue;
            if (tolerancias.Count == 0)
                tolerancias.Add(new ValorConVigencia<decimal>(desde, valor));
            else if (tolerancias[^1].Valor != valor)
                tolerancias.Add(new ValorConVigencia<decimal>(t.Value.DeLaCooperativa.Vigencia?.ValidFrom ?? fecha, valor));
        }

        var series = new SeriesDeModalidad(modalidad, subsegmento, combinacion.UsuryKey, combinacion.SarcSegment, desde, hasta, usura, festivos,
            umbrales, tolerancias, sinUsura);
        _leidas[clave] = series;
        return Result.Success(series);
    }

    /// <summary>Días después de <c>hasta</c> cuyos festivos se cargan: una cuota que vence al final del rango se exige el hábil siguiente.</summary>
    private const int DiasDeMargenDeFestivos = 15;
}
