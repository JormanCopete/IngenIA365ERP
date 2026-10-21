using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Sessions;

/// <summary>Una fracción exacta de quórum o de mayoría (numerador / denominador, 0 &lt; n ≤ d). (nuevo)</summary>
public sealed record Fraccion
{
    public Fraccion(int numerador, int denominador)
    {
        if (denominador <= 0 || numerador <= 0 || numerador > denominador)
            throw new ArgumentException($"La fracción {numerador}/{denominador} no es válida: se exige 0 < numerador ≤ denominador.");
        Numerador = numerador;
        Denominador = denominador;
    }

    public int Numerador { get; }
    public int Denominador { get; }

    public override string ToString() => $"{Numerador}/{Denominador}";
}

/// <summary>
/// La regla de votación sellada al abrir la sesión (<c>LND_GoverningBodyRuleVersions</c>, C44): quórum sobre la composición, mayoría
/// ordinaria con su base y, si la hay, la fracción calificada de vinculados (siempre «al menos», sobre la composición). (nuevo)
/// </summary>
public sealed record ReglaDeVotacionSellada(
    Fraccion Quorum,
    FractionComparison ComparacionDeQuorum,
    Fraccion Mayoria,
    FractionComparison ComparacionDeMayoria,
    MajorityBase BaseDeMayoria,
    Fraccion? Vinculados);

/// <summary>Un miembro del órgano vigente a la fecha de la sesión (<c>COR_People.Id</c>); el suplente nombra a su principal. (nuevo)</summary>
public sealed record MiembroDelOrgano(int Persona, bool EsPrincipal, bool TieneVoto, int? SuplenteDe);

/// <summary>La asistencia de una persona a la sesión. (nuevo)</summary>
public sealed record AsistenciaASesion(int Persona, AttendanceStatus Estado);

/// <summary>Un voto registrado; <paramref name="Reemplaza"/> es el voto que éste corrige (C75). (nuevo)</summary>
public sealed record VotoRegistrado(int Voto, int Persona, VoteValue Valor, int? Reemplaza);

/// <summary>Una persona excluida de un punto con su motivo (radicador, solicitante, persona del vínculo). (nuevo)</summary>
public sealed record PersonaExcluida(int Persona, string Motivo);

/// <summary>El punto que se vota: su regla, los excluidos y todos los votos registrados (los reemplazados también). (nuevo)</summary>
public sealed record PuntoAVotar(VotingRule Regla, IReadOnlyList<PersonaExcluida> Excluidos, IReadOnlyList<VotoRegistrado> Votos);

/// <summary>Resultado de un punto: sin quórum no se decide. (nuevo)</summary>
public enum ResultadoDeVotacion
{
    Aprobado = 1,
    NoAprobado = 2,
    SinQuorum = 3,
}

/// <summary>Por qué un voto no cuenta. (nuevo)</summary>
public enum MotivoDeVotoRechazado
{
    NoEsMiembroConVoto = 1,
    NoEstaPresente = 2,
    SuplenteConPrincipalPresente = 3,
    Excluido = 4,
}

/// <summary>Un voto vigente que no cuenta, con su motivo (y el de la exclusión, si fue excluido). (nuevo)</summary>
public sealed record VotoRechazado(int Voto, int Persona, MotivoDeVotoRechazado Motivo, string? Detalle);

/// <summary>El quórum de la sesión: composición (principales con voto), presentes elegibles (sentados) y el mínimo exigido. (nuevo)</summary>
public sealed record QuorumDeSesion(int Composicion, int PresentesElegibles, int Requerido, bool Cumple);

/// <summary>
/// La evaluación de un punto: quórum, elegibles (sentados menos excluidos), base de la mayoría, votos a favor requeridos, conteos de
/// los votos vigentes que cuentan, resultado, votos rechazados y la explicación que se sella en
/// <c>LND_SessionItems.EvaluationExplanation</c>. (nuevo)
/// </summary>
public sealed record EvaluacionDeVotacion(
    QuorumDeSesion Quorum,
    int Elegibles,
    int Base,
    int Requeridos,
    int AFavor,
    int EnContra,
    int Abstenciones,
    ResultadoDeVotacion Resultado,
    IReadOnlyList<VotoRechazado> Rechazados,
    IReadOnlyList<string> Explicacion)
{
    public string TextoDeLaExplicacion => string.Join(Environment.NewLine, Explicacion);
}

/// <summary>
/// Motor puro de la votación de un acta (feature 013, T0278; T64, R133, F7, C75). Recibe la regla sellada, la composición vigente, la
/// asistencia y el punto, y devuelve quórum, elegibles, conteos, resultado y la explicación. Todo se compara en <b>enteros</b>:
/// <c>votos × denominador</c> frente a <c>numerador × base</c>, sin decimales (2/3 en decimal es 0,666…7 y negaría lo que la norma
/// aprueba). (nuevo)
///
/// <para>Reglas (F7, «pendiente de validar» en sus valores, no en su forma):</para>
/// <list type="bullet">
/// <item>La composición son los principales con voto vigentes. El quórum se mide sobre ella con los <b>sentados</b>: principales con voto
/// presentes, y el suplente presente de un principal que no está (un suplente por principal, el primero de la lista).</item>
/// <item>Elegibles de un punto: los sentados menos los excluidos (US2-6). El voto de un excluido, de quien no está sentado o de un suplente
/// con su principal presente se rechaza y no cuenta.</item>
/// <item>Mayoría ordinaria: sobre los elegibles (<c>PresentEligible</c>) o la composición (<c>Composition</c>), según la regla. La
/// abstención no suma a favor.</item>
/// <item>Calificada de vinculados (FR-040): <c>favorables × d ≥ n × composición</c> con la fracción sellada (piso 4/5 que la cooperativa
/// sólo endurece), aunque haya excluidos (US2-7).</item>
/// <item>Cuenta el voto vigente: el que otro reemplazó queda en la historia y no cuenta (C75).</item>
/// </list>
/// </summary>
public static class EvaluadorDeVotacion
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>El quórum de la sesión (se sella al abrirla y al cerrarla).</summary>
    public static QuorumDeSesion Quorum(
        ReglaDeVotacionSellada regla, IReadOnlyList<MiembroDelOrgano> miembros, IReadOnlyList<AsistenciaASesion> asistencia) =>
        Sentar(regla, miembros, asistencia).Quorum;

    public static EvaluacionDeVotacion Evaluar(
        ReglaDeVotacionSellada regla,
        IReadOnlyList<MiembroDelOrgano> miembros,
        IReadOnlyList<AsistenciaASesion> asistencia,
        PuntoAVotar punto)
    {
        ArgumentNullException.ThrowIfNull(regla);
        ArgumentNullException.ThrowIfNull(punto);
        var explicacion = new List<string>();

        var (quorum, sentados, presentes, miembrosPorPersona) = Sentar(regla, miembros, asistencia);
        explicacion.Add(
            $"Quórum: {quorum.PresentesElegibles} presentes elegibles de una composición de {quorum.Composicion}; se exige " +
            $"{Comparacion(regla.ComparacionDeQuorum)} {regla.Quorum} de la composición: " +
            $"{Comparar(quorum.PresentesElegibles, regla.Quorum, quorum.Composicion, regla.ComparacionDeQuorum, out _)} → " +
            (quorum.Cumple ? "hay quórum." : "no hay quórum."));

        var excluidos = punto.Excluidos.GroupBy(e => e.Persona).ToDictionary(g => g.Key, g => g.First().Motivo);
        var elegibles = sentados.Where(p => !excluidos.ContainsKey(p)).ToHashSet();
        foreach (var (persona, motivo) in excluidos.Where(e => sentados.Contains(e.Key)).OrderBy(e => e.Key))
            explicacion.Add($"Excluido del punto: persona {persona.ToString(Inv)} ({motivo}).");

        // Votos vigentes: los que ningún otro reemplazó (C75).
        var reemplazados = punto.Votos.Where(v => v.Reemplaza is not null).Select(v => v.Reemplaza!.Value).ToHashSet();
        var vigentes = punto.Votos.Where(v => !reemplazados.Contains(v.Voto)).ToList();
        if (reemplazados.Count > 0)
            explicacion.Add($"Votos reemplazados que no cuentan: {reemplazados.Count.ToString(Inv)} (cuenta el vigente de cada persona).");
        var repetido = vigentes.GroupBy(v => v.Persona).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            throw new ArgumentException(
                $"La persona {repetido.Key.ToString(Inv)} tiene más de un voto vigente en el punto: el anterior se corrige con un voto de reemplazo.",
                nameof(punto));

        var rechazados = new List<VotoRechazado>();
        var contados = new List<VotoRegistrado>();
        foreach (var voto in vigentes.OrderBy(v => v.Voto))
        {
            if (elegibles.Contains(voto.Persona))
            {
                contados.Add(voto);
                continue;
            }
            var rechazo = MotivoDelRechazo(voto.Persona, sentados, presentes, miembrosPorPersona, excluidos, out var detalle);
            rechazados.Add(new VotoRechazado(voto.Voto, voto.Persona, rechazo, detalle));
            explicacion.Add($"Voto rechazado de la persona {voto.Persona.ToString(Inv)}: {Describir(rechazo)}" +
                (detalle is null ? "." : $" ({detalle})."));
        }

        var aFavor = contados.Count(v => v.Valor == VoteValue.For);
        var enContra = contados.Count(v => v.Valor == VoteValue.Against);
        var abstenciones = contados.Count(v => v.Valor == VoteValue.Abstain);

        Fraccion fraccion;
        FractionComparison comparacion;
        int @base;
        string nombre;
        if (punto.Regla == VotingRule.RelatedPartyQualified)
        {
            fraccion = regla.Vinculados ?? throw new ArgumentException(
                "El punto pide la mayoría calificada de vinculados y la regla sellada no trae su fracción: con actividad financiera la " +
                "versión de reglas la exige; sin ella, el punto va por la mayoría ordinaria de los estatutos.", nameof(regla));
            comparacion = FractionComparison.AtLeast;
            @base = quorum.Composicion;
            nombre = "Mayoría calificada de vinculados";
        }
        else
        {
            fraccion = regla.Mayoria;
            comparacion = regla.ComparacionDeMayoria;
            @base = regla.BaseDeMayoria == MajorityBase.Composition ? quorum.Composicion : elegibles.Count;
            nombre = "Mayoría ordinaria";
        }

        var requeridos = Minimo(fraccion, @base, comparacion);
        var textoComparacion = Comparar(aFavor, fraccion, @base, comparacion, out var alcanza);
        var aprobado = alcanza && aFavor > 0 && elegibles.Count > 0;
        var resultado = !quorum.Cumple ? ResultadoDeVotacion.SinQuorum
            : aprobado ? ResultadoDeVotacion.Aprobado : ResultadoDeVotacion.NoAprobado;

        var textoBase = punto.Regla == VotingRule.RelatedPartyQualified || regla.BaseDeMayoria == MajorityBase.Composition
            ? "la composición"
            : "los presentes elegibles";
        explicacion.Add(
            $"{nombre}: {Comparacion(comparacion)} {fraccion} sobre {textoBase} ({@base.ToString(Inv)}); elegibles {elegibles.Count.ToString(Inv)}; " +
            $"a favor {aFavor.ToString(Inv)}, en contra {enContra.ToString(Inv)}, abstenciones {abstenciones.ToString(Inv)} (la abstención no suma a favor); " +
            $"se requieren {requeridos.ToString(Inv)} a favor: {textoComparacion}.");
        explicacion.Add(resultado switch
        {
            ResultadoDeVotacion.Aprobado => "Resultado: aprobado.",
            ResultadoDeVotacion.NoAprobado => "Resultado: no aprobado.",
            _ => "Resultado: sin quórum, el punto no se decide.",
        });

        return new EvaluacionDeVotacion(quorum, elegibles.Count, @base, requeridos, aFavor, enContra, abstenciones, resultado,
            rechazados, explicacion);
    }

    private static (QuorumDeSesion Quorum, HashSet<int> Sentados, HashSet<int> Presentes, Dictionary<int, MiembroDelOrgano> Miembros) Sentar(
        ReglaDeVotacionSellada regla, IReadOnlyList<MiembroDelOrgano> miembros, IReadOnlyList<AsistenciaASesion> asistencia)
    {
        ArgumentNullException.ThrowIfNull(miembros);
        ArgumentNullException.ThrowIfNull(asistencia);
        var porPersona = new Dictionary<int, MiembroDelOrgano>();
        foreach (var m in miembros)
            if (!porPersona.TryAdd(m.Persona, m))
                throw new ArgumentException($"La persona {m.Persona.ToString(Inv)} figura dos veces en la composición vigente.", nameof(miembros));

        var presentes = asistencia.Where(a => a.Estado == AttendanceStatus.Present).Select(a => a.Persona).ToHashSet();
        var principales = miembros.Where(m => m.EsPrincipal && m.TieneVoto).ToList();
        var sentados = principales.Where(m => presentes.Contains(m.Persona)).Select(m => m.Persona).ToHashSet();

        // F7: el suplente presente se sienta por un principal ausente; uno por principal, el primero de la lista.
        foreach (var principal in principales.Where(p => !presentes.Contains(p.Persona)))
        {
            var suplente = miembros.FirstOrDefault(m => !m.EsPrincipal && m.TieneVoto && m.SuplenteDe == principal.Persona
                                                        && presentes.Contains(m.Persona) && !sentados.Contains(m.Persona));
            if (suplente is not null) sentados.Add(suplente.Persona);
        }

        var composicion = principales.Count;
        var requerido = Minimo(regla.Quorum, composicion, regla.ComparacionDeQuorum);
        Comparar(sentados.Count, regla.Quorum, composicion, regla.ComparacionDeQuorum, out var cumple);
        return (new QuorumDeSesion(composicion, sentados.Count, requerido, cumple && composicion > 0), sentados, presentes, porPersona);
    }

    private static MotivoDeVotoRechazado MotivoDelRechazo(
        int persona, HashSet<int> sentados, HashSet<int> presentes, Dictionary<int, MiembroDelOrgano> miembros,
        Dictionary<int, string> excluidos, out string? detalle)
    {
        detalle = null;
        if (sentados.Contains(persona) && excluidos.TryGetValue(persona, out var motivo))
        {
            detalle = motivo;
            return MotivoDeVotoRechazado.Excluido;
        }
        if (!miembros.TryGetValue(persona, out var miembro) || !miembro.TieneVoto
            || (!miembro.EsPrincipal && miembro.SuplenteDe is null))
            return MotivoDeVotoRechazado.NoEsMiembroConVoto;
        if (!presentes.Contains(persona)) return MotivoDeVotoRechazado.NoEstaPresente;
        return MotivoDeVotoRechazado.SuplenteConPrincipalPresente;
    }

    private static string Describir(MotivoDeVotoRechazado motivo) => motivo switch
    {
        MotivoDeVotoRechazado.Excluido => "está excluida del punto",
        MotivoDeVotoRechazado.NoEsMiembroConVoto => "no es miembro con voto del órgano",
        MotivoDeVotoRechazado.NoEstaPresente => "no está presente",
        _ => "es suplente y su principal está presente",
    };

    /// <summary>El mínimo de votos que cumple la fracción sobre la base, en enteros.</summary>
    private static int Minimo(Fraccion fraccion, int @base, FractionComparison comparacion)
    {
        long producto = (long)fraccion.Numerador * @base;
        long minimo = comparacion == FractionComparison.AtLeast
            ? (producto + fraccion.Denominador - 1) / fraccion.Denominador
            : producto / fraccion.Denominador + 1;
        return (int)minimo;
    }

    /// <summary>Compara <c>votos × d</c> con <c>n × base</c> y lo escribe («3 × 2 = 6 &gt; 1 × 5 = 5»).</summary>
    private static string Comparar(int votos, Fraccion fraccion, int @base, FractionComparison comparacion, out bool cumple)
    {
        long izquierda = (long)votos * fraccion.Denominador;
        long derecha = (long)fraccion.Numerador * @base;
        cumple = comparacion == FractionComparison.AtLeast ? izquierda >= derecha : izquierda > derecha;
        var operador = comparacion == FractionComparison.AtLeast ? (cumple ? "≥" : "<") : (cumple ? ">" : "≤");
        return $"{votos.ToString(Inv)} × {fraccion.Denominador.ToString(Inv)} = {izquierda.ToString(Inv)} {operador} " +
               $"{fraccion.Numerador.ToString(Inv)} × {@base.ToString(Inv)} = {derecha.ToString(Inv)}";
    }

    private static string Comparacion(FractionComparison comparacion) =>
        comparacion == FractionComparison.AtLeast ? "al menos" : "más de";
}
