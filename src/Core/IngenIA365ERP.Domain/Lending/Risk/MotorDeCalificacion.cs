using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>Un crédito reestructurado: la categoría que tenía al pedir la reestructuración y las cuotas pagadas seguidas desde entonces.</summary>
public sealed record ReestructuracionParaCalificar(RatingCategory CategoriaAlReestructurar, int CuotasPagadasSeguidas);

/// <summary>Un proceso de insolvencia vivo o terminado y la categoría del crédito al iniciarse la negociación (FR-098).</summary>
public sealed record InsolvenciaParaCalificar(InsolvencyProceedingStatus Estado, RatingCategory CategoriaAlIniciar);

/// <summary>Una calificación por análisis o por evaluación: categoría y, en E, subcategoría.</summary>
public sealed record CalificacionImpuesta(RatingCategory Categoria, RatingSubcategory Subcategoria = RatingSubcategory.None);

/// <summary>
/// Lo que el motor necesita de un crédito (lo arma <c>CargadorDeRiesgo</c>, T0699): deudor y segmento SARC (el arrastre es por deudor
/// y segmento), la altura de mora que entrega la reconstrucción (<c>AlturaDeMora</c>, con la fecha exigible del calendario hábil), los
/// meses en E para las subcategorías por tiempo en E, y las marcas de cada regla especial.
/// </summary>
public sealed record CreditoParaCalificar(int LoanId, int DeudorId, string SegmentoSarc, int DiasDeMora)
{
    public int? MesesEnE { get; init; }

    /// <summary>Las personas codeudoras del crédito: el arrastre sólo las mira si la norma dice que alcanza las codeudas.</summary>
    public IReadOnlyList<int> Codeudores { get; init; } = [];

    /// <summary>¿Crédito de pago único de capital al vencimiento?</summary>
    public bool PagoUnico { get; init; }

    /// <summary>¿Nació después del cierre anterior (no tiene línea en él)?</summary>
    public bool Nuevo { get; init; }

    public ReestructuracionParaCalificar? Reestructuracion { get; init; }

    public InsolvenciaParaCalificar? Insolvencia { get; init; }

    /// <summary>La candidata del modelo del segmento (K6a, pérdida esperada homologada); en el Anexo 1, nula.</summary>
    public RatingCategory? CalificacionDelModelo { get; init; }

    /// <summary>La evaluación de cartera aplicada y vigente (K3); en K1, nula.</summary>
    public CalificacionImpuesta? Evaluacion { get; init; }

    /// <summary>La calificación por análisis vigente a R (FR-101); nunca mejora la de las reglas.</summary>
    public CalificacionImpuesta? Analisis { get; init; }
}

/// <summary>Las opciones de una corrida: si hay arrastre (la informativa lo apaga con <c>Riesgo.CalificacionDiariaConArrastre</c>) y los aportes (D19).</summary>
public sealed record OpcionesDeCalificacion(bool ConArrastre, ContextoDeAportes Aportes)
{
    /// <summary>El cierre de K1: con arrastre y D19.</summary>
    public static OpcionesDeCalificacion Cierre { get; } = new(true, ContextoDeAportes.D19SinIndicadores);
}

/// <summary>Una candidata con su causa y en palabras.</summary>
public sealed record CandidataDeCalificacion(RatingCategory Categoria, RatingCause Causa, string Detalle);

/// <summary>
/// La calificación de un crédito (data-model §23.2): la final (la peor candidata) con su subcategoría y causa, la contable, la que dan
/// los días de mora (<c>ArrearsRating</c>), la del modelo, las candidatas y la explicación.
/// </summary>
public sealed record CalificacionDeCredito(
    int LoanId,
    RatingCategory Categoria,
    RatingSubcategory Subcategoria,
    RatingCause Causa,
    RatingCategory Contable,
    RatingCategory CategoriaPorMora,
    RatingCategory? CategoriaDelModelo,
    IReadOnlyList<CandidataDeCalificacion> Candidatas,
    ExplicacionDeCartera Explicacion);

/// <summary>
/// El motor de calificación (feature 013, US9, T0693; T46, R55; FR-093, FR-098–FR-101; SC-021), en dos fases:
/// <list type="number">
/// <item><b>Por crédito</b>, las candidatas: mora (el rango del segmento que contiene los días; en insolvencia en negociación la
/// reemplaza la categoría al iniciarla, porque «la calificación se mantiene»), modelo (K6a), pago único (la peor regla cuyos días ya
/// pasaron), crédito nuevo, reestructurado (la del momento de reestructurar, nunca mejor que el mínimo de la norma, que mejora una
/// categoría por cada tramo de cuotas pagadas seguidas y vuelve a la inicial si recae en mora), evaluación (K3), análisis (nunca
/// mejora: compite como una más) e insolvencia fracasada (la categoría que fija la norma).</item>
/// <item><b>Arrastre</b> por deudor y segmento SARC (la regla del documento): si el peor crédito del grupo está en la categoría de
/// arrastre o peor, lleva a esa categoría a los demás. El grupo es por <b>deudor</b>, así que no alcanza las codeudas. Sin
/// excepción de aportes mientras rija D19 o no haya indicadores (<see cref="ContextoDeAportes"/>).</item>
/// </list>
/// Gana la peor; en un empate, la causa más específica, en el orden de <see cref="OrdenDeCausas"/> (insolvencia, pago único,
/// reestructurado, crédito nuevo, mora, modelo, evaluación, análisis y, al final, arrastre, que sólo existe si es peor). En E, la
/// subcategoría la da la condición tipada de la norma (días o meses en E); si llegó a E sin cumplir ninguna, la primera; la del
/// análisis, si es el análisis el que gana. La contable sale de <see cref="HomologacionDeCalificacion"/>.
///
/// <para>Recibe la norma <b>efectiva</b> (<see cref="PoliticaSarcEfectiva"/>). Pura: sin IO, sin EF, sin valores legales. (nuevo)</para>
/// </summary>
public static class MotorDeCalificacion
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>El desempate entre candidatas de la misma categoría: la primera de esta lista gana.</summary>
    public static IReadOnlyList<RatingCause> OrdenDeCausas { get; } =
    [
        RatingCause.Insolvency, RatingCause.SingleInstallment, RatingCause.Restructured, RatingCause.NewLoan, RatingCause.Arrears,
        RatingCause.Model, RatingCause.Homologation, RatingCause.Evaluation, RatingCause.Analysis, RatingCause.Drag,
    ];

    /// <summary>Califica una cartera: primero cada crédito, después el arrastre por deudor y segmento.</summary>
    public static IReadOnlyList<CalificacionDeCredito> Calificar(Anexo1 norma, IEnumerable<CreditoParaCalificar> creditos, OpcionesDeCalificacion opciones)
    {
        ArgumentNullException.ThrowIfNull(norma);
        ArgumentNullException.ThrowIfNull(creditos);
        ArgumentNullException.ThrowIfNull(opciones);
        opciones.Aportes.ExigirQueNoApliquen(norma);

        var lista = creditos.ToList();
        if (lista.Select(c => c.LoanId).Distinct().Count() != lista.Count)
            throw new ArgumentException("Un crédito llegó dos veces.", nameof(creditos));

        var primera = lista.Select(c => (Credito: c, Candidatas: CandidatasPropias(norma, c))).ToList();
        var propias = primera.ToDictionary(p => p.Credito.LoanId, p => Peor(p.Candidatas));

        var resultado = new List<CalificacionDeCredito>(lista.Count);
        foreach (var (credito, candidatas) in primera)
        {
            var todas = candidatas.ToList();
            var explicacion = new ExplicacionDeCartera();
            if (opciones.ConArrastre)
                Arrastrar(norma, credito, primera, propias, todas, explicacion, opciones.Aportes);
            else
                explicacion.Linea("Sin arrastre en esta calificación (Riesgo.CalificacionDiariaConArrastre apagado).");
            resultado.Add(Cerrar(norma, credito, todas, explicacion));
        }
        return resultado;
    }

    // ------------------------------------------------------------------------------------------------ fase 1 --

    private static List<CandidataDeCalificacion> CandidatasPropias(Anexo1 norma, CreditoParaCalificar c)
    {
        var segmento = norma.SegmentoObligatorio(c.SegmentoSarc);
        var candidatas = new List<CandidataDeCalificacion>();
        var porMora = norma.CategoriaPorMora(c.SegmentoSarc, c.DiasDeMora);
        var rango = segmento.Categorias.First(r => r.Categoria == porMora);
        var textoRango = $"{rango.DiasDesde.ToString(Inv)}" + (rango.DiasHasta is { } h ? $"–{h.ToString(Inv)}" : " o más");

        if (c.Insolvencia is { Estado: InsolvencyProceedingStatus.Negotiating } negociando)
            candidatas.Add(new(negociando.CategoriaAlIniciar, RatingCause.Insolvency,
                $"insolvencia en negociación: se mantiene {negociando.CategoriaAlIniciar} (la mora daría {porMora}, {c.DiasDeMora.ToString(Inv)} días)"));
        else
            candidatas.Add(new(porMora, RatingCause.Arrears,
                $"mora de {c.DiasDeMora.ToString(Inv)} días: {porMora} en {segmento.Codigo} ({textoRango} días)"));

        if (c.Insolvencia is { Estado: InsolvencyProceedingStatus.Failed } && norma.Insolvencia is { } reglaDeInsolvencia)
            candidatas.Add(new(reglaDeInsolvencia.CategoriaSiFracasa, RatingCause.Insolvency,
                $"la negociación de insolvencia fracasó: {reglaDeInsolvencia.CategoriaSiFracasa} por la norma"));

        if (c.CalificacionDelModelo is { } modelo)
            candidatas.Add(new(modelo, RatingCause.Model, $"modelo del segmento: {modelo}"));

        if (c.PagoUnico)
        {
            var regla = norma.PagoUnico.Where(r => r.DiasDeMoraDesde <= c.DiasDeMora).OrderByDescending(r => r.Categoria).FirstOrDefault();
            if (regla is not null)
                candidatas.Add(new(regla.Categoria, RatingCause.SingleInstallment,
                    $"pago único con {c.DiasDeMora.ToString(Inv)} días de mora: {regla.Categoria} desde {regla.DiasDeMoraDesde.ToString(Inv)} días"));
        }

        if (c.Nuevo && norma.CreditoNuevo is { } nuevo)
            candidatas.Add(new(nuevo.Categoria, RatingCause.NewLoan, $"crédito nuevo: {nuevo.Categoria}"));

        if (c.Reestructuracion is { } r)
            candidatas.Add(Reestructurado(norma.Reestructurados, r, c.DiasDeMora));

        if (c.Evaluacion is { } evaluacion)
            candidatas.Add(new(evaluacion.Categoria, RatingCause.Evaluation, $"evaluación de cartera: {HomologacionDeCalificacion.Texto(evaluacion.Categoria, evaluacion.Subcategoria)}"));

        if (c.Analisis is { } analisis)
            candidatas.Add(new(analisis.Categoria, RatingCause.Analysis, $"calificación por análisis: {HomologacionDeCalificacion.Texto(analisis.Categoria, analisis.Subcategoria)}"));

        return candidatas;
    }

    private static CandidataDeCalificacion Reestructurado(ReglaDeReestructurados regla, ReestructuracionParaCalificar r, int diasDeMora)
    {
        if (r.CuotasPagadasSeguidas < 0) throw new ArgumentOutOfRangeException(nameof(r), r.CuotasPagadasSeguidas, "Las cuotas pagadas no son negativas.");
        var inicial = Max(r.CategoriaAlReestructurar, regla.CategoriaMinima);
        var textoInicial = $"reestructurado: al reestructurar {r.CategoriaAlReestructurar}, mínimo de la norma {regla.CategoriaMinima} → {inicial}";
        if (diasDeMora > 0)
            return new(inicial, RatingCause.Restructured, $"{textoInicial}; volvió a la mora ({diasDeMora.ToString(Inv)} días): regresa a {inicial}");
        var tramos = regla.CuotasPagadasParaMejorar > 0 ? r.CuotasPagadasSeguidas / regla.CuotasPagadasParaMejorar : 0;
        var mejorada = (RatingCategory)Math.Max((int)RatingCategory.A, (int)inicial - tramos);
        return new(mejorada, RatingCause.Restructured,
            $"{textoInicial}; {r.CuotasPagadasSeguidas.ToString(Inv)} cuotas pagadas seguidas = {tramos.ToString(Inv)} tramo(s) de "
            + $"{regla.CuotasPagadasParaMejorar.ToString(Inv)} → {mejorada}");
    }

    // ------------------------------------------------------------------------------------------------ fase 2 --

    private static void Arrastrar(Anexo1 norma, CreditoParaCalificar credito, List<(CreditoParaCalificar Credito, List<CandidataDeCalificacion> Candidatas)> todos,
        Dictionary<int, CandidataDeCalificacion> propias, List<CandidataDeCalificacion> candidatas, ExplicacionDeCartera explicacion, ContextoDeAportes aportes)
    {
        // El grupo: los créditos cuyo deudor es el de éste (y, sólo si la norma alcanza las codeudas, cuyo deudor es uno de sus
        // codeudores); con la regla por segmento, del mismo segmento SARC.
        var regla = norma.Arrastre;
        var personas = new HashSet<int> { credito.DeudorId };
        if (regla.IncluyeCodeudas) personas.UnionWith(credito.Codeudores);
        var grupo = todos.Where(t => personas.Contains(t.Credito.DeudorId)
            && (!regla.DentroDelSegmento || t.Credito.SegmentoSarc == credito.SegmentoSarc)).Select(t => t.Credito).ToList();
        var peor = grupo.Select(g => (g.LoanId, propias[g.LoanId].Categoria)).OrderByDescending(g => g.Categoria).ThenBy(g => g.LoanId).First();
        var suya = propias[credito.LoanId].Categoria;
        var alcance = (regla.DentroDelSegmento ? $"del deudor en {credito.SegmentoSarc}" : "del deudor")
            + (regla.IncluyeCodeudas && credito.Codeudores.Count > 0 ? " o de sus codeudores" : string.Empty);
        if (peor.Categoria < regla.DesdeCategoria || peor.Categoria <= suya)
        {
            explicacion.Linea($"Arrastre: no aplica; el peor crédito {alcance} está en {peor.Categoria} y el arrastre empieza en {regla.DesdeCategoria} "
                + $"(las codeudas no cuentan; excepción de aportes: no, {aportes.MotivoDeQueNoApliquen(norma)}).");
            return;
        }
        candidatas.Add(new(peor.Categoria, RatingCause.Drag,
            $"arrastre: el crédito {peor.LoanId.ToString(Inv)} {alcance} está en {peor.Categoria}"));
        explicacion.Linea($"Arrastre: el crédito {peor.LoanId.ToString(Inv)} {alcance} está en {peor.Categoria} ≥ {regla.DesdeCategoria}; "
            + $"sin excepción de aportes ({aportes.MotivoDeQueNoApliquen(norma)}); las codeudas no cuentan.");
    }

    // ------------------------------------------------------------------------------------------------ cierre --

    private static CalificacionDeCredito Cerrar(Anexo1 norma, CreditoParaCalificar c, List<CandidataDeCalificacion> candidatas, ExplicacionDeCartera explicacion)
    {
        var ganadora = Peor(candidatas);
        var subcategoria = Subcategoria(norma, c, ganadora);
        var contable = HomologacionDeCalificacion.Contable(ganadora.Categoria, subcategoria);
        var final = new ExplicacionDeCartera();
        foreach (var candidata in candidatas) final.Linea($"Candidata {candidata.Categoria} ({candidata.Causa}): {candidata.Detalle}.");
        final.Agregar(explicacion);
        final.Linea($"Calificación {HomologacionDeCalificacion.Texto(ganadora.Categoria, subcategoria)} por {ganadora.Causa} (la peor de las candidatas); "
            + $"contable {contable}.");
        return new(c.LoanId, ganadora.Categoria, subcategoria, ganadora.Causa, contable, norma.CategoriaPorMora(c.SegmentoSarc, c.DiasDeMora),
            c.CalificacionDelModelo, candidatas, final);
    }

    private static RatingSubcategory Subcategoria(Anexo1 norma, CreditoParaCalificar c, CandidataDeCalificacion ganadora)
    {
        if (ganadora.Categoria != RatingCategory.E) return RatingSubcategory.None;
        var impuesta = ganadora.Causa switch
        {
            RatingCause.Analysis => c.Analisis,
            RatingCause.Evaluation => c.Evaluacion,
            _ => null,
        };
        if (impuesta is { Subcategoria: not RatingSubcategory.None } i) return i.Subcategoria;
        var segmento = norma.SegmentoObligatorio(c.SegmentoSarc);
        if (segmento.Porcentajes.All(p => p.Categoria != RatingCategory.E || p.Subcategoria == RatingSubcategory.None)) return RatingSubcategory.None;
        return norma.Porcentaje(c.SegmentoSarc, RatingCategory.E, null, c.DiasDeMora, c.MesesEnE).Subcategoria;
    }

    private static CandidataDeCalificacion Peor(IReadOnlyList<CandidataDeCalificacion> candidatas) =>
        candidatas.OrderByDescending(c => c.Categoria).ThenBy(c => OrdenDe(c.Causa)).First();

    private static int OrdenDe(RatingCause causa)
    {
        for (var i = 0; i < OrdenDeCausas.Count; i++)
            if (OrdenDeCausas[i] == causa) return i;
        return OrdenDeCausas.Count;
    }

    private static RatingCategory Max(RatingCategory a, RatingCategory b) => a >= b ? a : b;
}
