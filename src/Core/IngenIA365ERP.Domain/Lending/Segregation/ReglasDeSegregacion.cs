namespace IngenIA365ERP.Domain.Lending.Segregation;

/// <summary>
/// Una intervención ya registrada en <c>LND_CaseParticipations</c>: el expediente, el acto, el <c>SEC_Users.Id</c> y la persona
/// (<c>COR_People.Id</c>) de quien actuó, y el instante. (nuevo)
/// </summary>
public sealed record IntervencionPrevia(Guid Expediente, string Accion, int UsuarioId, int? PersonaId, DateTime En);

/// <summary>El acto que alguien pide hacer sobre un expediente, con su <c>SEC_Users.Id</c> y su persona. (nuevo)</summary>
public sealed record ActoDeSegregacion(Guid Expediente, string Accion, int UsuarioId, int? PersonaId);

/// <summary>Un par de acciones incompatibles del parámetro <c>Segregacion.Incompatibilidades</c>; simétrico. (nuevo)</summary>
public sealed record ParIncompatible(string AccionA, string AccionB, bool Activo)
{
    public bool Junta(string una, string otra) =>
        (string.Equals(AccionA, una, StringComparison.Ordinal) && string.Equals(AccionB, otra, StringComparison.Ordinal))
        || (string.Equals(AccionA, otra, StringComparison.Ordinal) && string.Equals(AccionB, una, StringComparison.Ordinal));
}

/// <summary>
/// El conflicto que encontró <see cref="ReglasDeSegregacion"/>: el acto pedido, la acción previa incompatible con su instante y su
/// usuario, si chocó por persona (dos usuarios de la misma persona) y si fue la regla fija. Lo que lleva
/// <c>Lending.Segregation.Conflict</c>. (nuevo)
/// </summary>
public sealed record ConflictoDeSegregacion(
    string Accion, string AccionPrevia, DateTime InstantePrevio, int UsuarioPrevio, bool PorPersona, bool EsReglaFija);

/// <summary>
/// Motor puro de la segregación de funciones de Cartera (feature 013, T0279; T66, FR-005, R135): compara el acto pedido con las
/// intervenciones previas del <b>mismo expediente</b> y devuelve el conflicto con la acción previa, o ninguno. (nuevo)
///
/// <para>Dos actos son del mismo actor si los hizo el mismo <c>SEC_Users.Id</c> o, cuando los dos tienen persona, la misma persona
/// (una persona con dos usuarios no se escapa; dos usuarios sin persona sólo se comparan por usuario). Nunca se compara el entero
/// del token ni el correo (<c>LaSegregacionNoUsaUserIdDelToken</c>): quien llama resuelve el usuario por <c>IActorActual</c>.</para>
///
/// <para>La regla fija —quien radica no decide ni vota sobre lo suyo— no se apaga, salvo <c>Radicar × Decidir</c> en las solicitudes de crédito
/// de una cooperativa con <c>Segregacion.QuienRadicaDecide</c> (2026-11-17). Los pares configurables muerden sólo si están
/// activos; por defecto, <see cref="ParesPorDefecto"/>. Con varias previas incompatibles se informa la más antigua.</para>
/// </summary>
public static class ReglasDeSegregacion
{
    public const string CodigoDeError = "Lending.Segregation.Conflict";

    /// <summary>
    /// La regla fija (R135): radicar frente a decidir y a votar. K3 (T0863): radicar frente a aplicar —quien radicó una novedad o una condonación no
    /// la aplica—.
    /// </summary>
    public static IReadOnlyList<ParIncompatible> ReglaFija { get; } =
    [
        new(AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Decidir, Activo: true),
        new(AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Votar, Activo: true),
        new(AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Aplicar, Activo: true),
    ];

    /// <summary>El defecto «pendiente de validar» de <c>Segregacion.Incompatibilidades</c> (F6): <c>Decidir × Desembolsar</c> activo.</summary>
    public static IReadOnlyList<ParIncompatible> ParesPorDefecto { get; } =
    [
        new(AccionesDeSegregacion.Decidir, AccionesDeSegregacion.Desembolsar, Activo: true),
    ];

    /// <param name="quienRadicaDecide">
    /// 2026-11-17: el par fijo <c>Radicar × Decidir</c> no rige (<c>Segregacion.QuienRadicaDecide</c>, sólo para las solicitudes de crédito: lo
    /// decide quien llama). Los demás pares fijos siguen.
    /// </param>
    /// <param name="sinReglaFija">
    /// 2026-11-26: la regla fija no rige en ningún par (<c>Segregacion.UnaPersonaHaceTodo</c>); los pares configurables activos siguen.
    /// </param>
    public static ConflictoDeSegregacion? Evaluar(
        ActoDeSegregacion acto, IEnumerable<IntervencionPrevia> previas, IEnumerable<ParIncompatible> pares, bool quienRadicaDecide = false,
        bool sinReglaFija = false)
    {
        ArgumentNullException.ThrowIfNull(acto);
        ExigirAccion(acto.Accion);
        var activos = pares.Where(p =>
        {
            ExigirAccion(p.AccionA);
            ExigirAccion(p.AccionB);
            return p.Activo;
        }).ToList();

        foreach (var previa in previas.Where(p => p.Expediente == acto.Expediente).OrderBy(p => p.En))
        {
            if (!MismoActor(acto, previa, out var porPersona)) continue;

            var fija = !sinReglaFija && ReglaFija.Any(p => p.Junta(acto.Accion, previa.Accion)
                && !(quienRadicaDecide && p.Junta(AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Decidir)));
            if (fija || activos.Any(p => p.Junta(acto.Accion, previa.Accion)))
                return new ConflictoDeSegregacion(acto.Accion, previa.Accion, previa.En, previa.UsuarioId, porPersona, fija);
        }

        return null;
    }

    private static bool MismoActor(ActoDeSegregacion acto, IntervencionPrevia previa, out bool porPersona)
    {
        porPersona = false;
        if (acto.UsuarioId == previa.UsuarioId) return true;
        if (acto.PersonaId is { } persona && previa.PersonaId is { } otra && persona == otra)
        {
            porPersona = true;
            return true;
        }
        return false;
    }

    private static void ExigirAccion(string accion)
    {
        if (!AccionesDeSegregacion.Existe(accion))
            throw new ArgumentException($"«{accion}» no es una acción de segregación de Cartera.", nameof(accion));
    }
}
