using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// Los errores del gobierno de Cartera —atribuciones, órganos, sesiones y actas, vinculados, alcance y plantillas— (feature 013, US17,
/// bloque D; api.md §1.2–§1.4, §1.9, §3.5–§3.9, §5; data-model §10.1). Los <c>*.NotFound</c> son 404 —también sin permiso o fuera del
/// alcance, con el mismo mensaje—; los demás, 422 con <c>data</c> para corregir. Los mensajes van en español claro, dicen qué hacer y
/// nunca muestran el código (FR-013). Lo que una entidad de gobierno rechaza llega como <see cref="ProblemaDeCartera"/> y se traduce con
/// <see cref="De"/>. (nuevo)
/// </summary>
public static class ErroresDeGobierno
{
    public const string CodigoMatrizInexistente = "Lending.AttributionMatrix.NotFound";
    public const string CodigoOrganoInexistente = "Lending.GoverningBody.NotFound";
    public const string CodigoMiembroInexistente = "Lending.GoverningBody.MemberNotFound";
    public const string CodigoOrganoEnUso = "Lending.GoverningBody.InUse";
    public const string CodigoReglasInvalidas = "Lending.GoverningBody.RulesInvalid";
    public const string CodigoSesionInexistente = "Lending.Session.NotFound";
    public const string CodigoPuntoInexistente = "Lending.Session.ItemNotFound";
    public const string CodigoSujetoNoPendiente = "Lending.Session.SubjectNotPending";
    public const string CodigoSujetoDeOtroOrgano = "Lending.Session.SubjectNotForBody";
    public const string CodigoSujetoNoDisponible = "Lending.Session.SubjectNotSupported";
    public const string CodigoPuntoConVotos = "Lending.Session.ItemHasVotes";
    public const string CodigoVotanteExcluido = "Lending.Session.VoterExcluded";
    public const string CodigoMiembroSinUsuario = "Lending.Session.MemberNotUser";
    public const string CodigoPuntoNoCorresponde = "Lending.Session.ItemMismatch";
    public const string CodigoContenidoCambiado = "Lending.Decision.ContentChanged";
    public const string CodigoPermisoRequerido = "Lending.Permission.Required";
    public const string CodigoVinculadoInexistente = "Lending.RelatedParty.NotFound";
    public const string CodigoGradoFueraDelLimite = "Lending.RelatedParty.KinshipDegreeExceeded";
    public const string CodigoLineaInexistente = "Lending.CreditLine.NotFound";
    public const string CodigoPlantillaInexistente = "Lending.Template.NotFound";
    public const string CodigoPersonaInexistente = "Core.Person.NotFound";
    public const string CodigoSinUsuario = "Lending.Actor.Unauthorized";

    /// <summary>Lo que rechazó una entidad de gobierno, como error del contrato (con su <c>data</c> si la trae).</summary>
    public static Error De(ProblemaDeCartera problema)
    {
        ArgumentNullException.ThrowIfNull(problema);
        return problema.Datos is { Count: > 0 } datos ? new ErrorConDatos(problema.Codigo, problema.Mensaje, datos) : new Error(problema.Codigo, problema.Mensaje);
    }

    public static Error SinUsuario() =>
        new(CodigoSinUsuario, "No se pudo identificar a la persona que hace la operación en esta cooperativa.");

    public static Error MatrizInexistente() => new(CodigoMatrizInexistente, "La matriz de atribuciones no existe.");

    public static Error SinMatrizVigente(DateOnly fecha) => new ErrorConDatos(CodigoMatrizInexistente,
        $"No hay una matriz de atribuciones vigente el {Fecha(fecha)}. Publique una antes de enrutar decisiones.",
        new { date = Fecha(fecha) });

    public static Error OrganoInexistente() => new(CodigoOrganoInexistente, "El órgano de decisión no existe.");

    public static Error OrganoDeNivelInvalido(string nivel) => new ErrorConDatos(CodigoOrganoInexistente,
        $"El órgano del nivel {nivel} no existe, no está activo o no decide crédito.", new { levelCode = nivel });

    public static Error MiembroInexistente() => new(CodigoMiembroInexistente, "El miembro no pertenece a este órgano.");

    public static Error OrganoEnUso(int version) => new ErrorConDatos(CodigoOrganoEnUso,
        $"El órgano decide en un nivel de la matriz de atribuciones versión {version.ToString(CultureInfo.InvariantCulture)}. " +
        "Publique una matriz que no lo nombre antes de desactivarlo.",
        new { matrixVersion = version });

    public static Error ReglasInvalidas(string motivo, string mensaje) => new ErrorConDatos(CodigoReglasInvalidas, mensaje, new { reason = motivo });

    public static Error SesionInexistente() => new(CodigoSesionInexistente, "La sesión no existe.");

    public static Error PuntoInexistente() => new(CodigoPuntoInexistente, "El punto no existe en esta sesión.");

    public static Error SujetoNoPendiente(string estado) => new ErrorConDatos(CodigoSujetoNoPendiente,
        $"Este asunto ya no está pendiente de decisión (estado: {estado}). No se puede llevar a la sesión.",
        new { subjectStatus = estado });

    public static Error SujetoDeOtroOrgano(string? organo) => new ErrorConDatos(CodigoSujetoDeOtroOrgano,
        "Este asunto lo decide otro órgano según la matriz de atribuciones. Llévelo a una sesión de ese órgano.",
        new { governingBody = organo });

    public static Error SujetoNoDisponible(string sujeto) => new ErrorConDatos(CodigoSujetoNoDisponible,
        "Este asunto todavía no se puede llevar a un acta: llega con su entrega de Cartera.", new { subject = sujeto });

    public static Error PuntoConVotos() => new(CodigoPuntoConVotos,
        "El punto ya tiene votos y no se retira. Si se votó por error, el acta deja constancia y se corrige con otra sesión.");

    /// <summary>US2-6: el radicador, el solicitante, el interesado, quien no es miembro con voto o quien no está presente.</summary>
    public static Error VotanteExcluido(string nombre, string motivo) => new ErrorConDatos(CodigoVotanteExcluido,
        $"{nombre} no puede votar este punto: {DescribirExclusion(motivo)}.", new { reason = motivo });

    public static Error MiembroSinUsuario() => new(CodigoMiembroSinUsuario,
        "El miembro no tiene usuario en esta cooperativa: no puede identificarse con su llave o su código. Registre su voto desde la secretaría.");

    public static Error PuntoNoCorresponde(string esperado, string? sujetoDelPunto, string? resultadoDelPunto) => new ErrorConDatos(CodigoPuntoNoCorresponde,
        "El punto del acta no corresponde: tiene que ser un punto de decisión aprobado, de una sesión cerrada, sobre este mismo asunto.",
        new { expectedSubject = esperado, itemSubject = sujetoDelPunto, itemOutcome = resultadoDelPunto });

    public static Error ContenidoCambiado(string actual) => new ErrorConDatos(CodigoContenidoCambiado,
        "El acta o alguno de sus asuntos cambió desde que la revisó. Vuelva a abrirla y revísela antes de cerrarla.",
        new { currentContentSha256 = actual });

    /// <summary>§1.4: un permiso que depende del cuerpo (votar por otro, una clave con permiso propio): 422, no 404.</summary>
    public static Error PermisoRequerido(params string[] permisos) => new ErrorConDatos(CodigoPermisoRequerido,
        "No tiene el permiso que esta operación necesita. Pídaselo al administrador.", new { permissionCodes = permisos });

    public static Error VinculadoInexistente() => new(CodigoVinculadoInexistente, "El vínculo no existe.");

    public static Error GradoFueraDelLimite(byte grado, decimal limite, string codigo, string fuente) => new ErrorConDatos(CodigoGradoFueraDelLimite,
        $"El grado de parentesco {grado.ToString(CultureInfo.InvariantCulture)} está fuera de los grados que la ley considera vinculados " +
        $"(hasta {limite.ToString("0.##", CultureInfo.InvariantCulture)}, {fuente}). No lo registre como vinculado.",
        new { degree = grado, limitCode = codigo, limitValue = limite, source = fuente });

    public static Error LineaInexistente() => new(CodigoLineaInexistente, "La línea de crédito no existe.");

    public static Error PlantillaInexistente() => new(CodigoPlantillaInexistente, "La plantilla no existe.");

    public static Error PersonaInexistente() => new(CodigoPersonaInexistente, "La persona no existe en esta cooperativa. Créela en Maestros › Personas.");

    /// <summary>Los motivos de exclusión de un punto y de rechazo de un voto (api.md §5, <c>data.reason</c>).</summary>
    public static string DescribirExclusion(string motivo) => motivo switch
    {
        MotivosDeExclusion.Radicador => "radicó o presentó el asunto",
        MotivosDeExclusion.Solicitante => "es el solicitante o un codeudor del asunto",
        MotivosDeExclusion.Interesado => "es la persona por la que existe el vínculo del solicitante",
        MotivosDeExclusion.NoMiembro => "no es miembro con voto del órgano en la fecha de la sesión",
        MotivosDeExclusion.Ausente => "no está registrado como presente en la sesión",
        _ => motivo,
    };

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// Por qué una persona no vota un punto (api.md §5, <c>Lending.Session.VoterExcluded</c>, <c>data.reason</c>): los tres primeros los
/// guarda el punto en <c>ExcludedPersons</c> (US2-6, FR-040); los dos últimos los decide la asistencia. (nuevo)
/// </summary>
public static class MotivosDeExclusion
{
    public const string Solicitante = "Applicant";
    public const string Radicador = "Filer";
    public const string Interesado = "Interested";
    public const string NoMiembro = "NotMember";
    public const string Ausente = "Absent";
}
