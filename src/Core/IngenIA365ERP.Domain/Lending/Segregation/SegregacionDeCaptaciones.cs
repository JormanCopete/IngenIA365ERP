using System.Security.Cryptography;
using System.Text;

namespace IngenIA365ERP.Domain.Lending.Segregation;

/// <summary>
/// Un par de segregación de captaciones y aportes (feature 014, US16, T163; FR-005; api.md §1.7): el expediente, el acto de quien registra, el acto de
/// quien aprueba (o levanta), cómo se lee en pantalla y la entrega en que el expediente nace. La comparación es por persona y por usuario
/// (<see cref="ReglasDeSegregacion"/>, nunca el entero del token). (nuevo)
/// </summary>
public sealed record ParDeSegregacionDeCaptaciones(
    string Expediente,
    string Registra,
    string Aprueba,
    string QuienRegistra,
    string QuienAprueba,
    EntregaDeCartera Entrega);

/// <summary>
/// El catálogo de la segregación de captaciones y aportes (feature 014, US16, T163; FR-005; api.md §1.7): qué expedientes hay, qué par de actos se
/// separa en cada uno y la <b>raíz</b> del expediente sobre la que compara <see cref="ReglasDeSegregacion"/>. Puro y sin reloj. (nuevo)
///
/// <list type="bullet">
/// <item>Los pares de FR-005 son todos la regla fija de la 013 —quien radica no decide—: registrar es <see cref="AccionesDeSegregacion.Radicar"/> y
/// aprobar o levantar es <see cref="AccionesDeSegregacion.Decidir"/>. La regla fija no se apaga con el parámetro <c>Segregacion.Incompatibilidades</c>:
/// la cooperativa puede sumar pares, no quitar éstos.</item>
/// <item>La segregación compara por la <b>raíz</b> del expediente, no por su tipo. Dos expedientes que nacen del mismo recurso (la apertura de una cuenta y
/// los cargos de esa cuenta; el retiro del asociado y su devolución) no pueden compartir raíz: quien abrió una cuenta quedaría impedido para condonar
/// cualquier cargo de ella. Por eso los expedientes que cuelgan de un recurso ajeno usan una raíz <b>derivada</b> (<see cref="Raiz"/>), fija para el
/// mismo par (tipo, recurso).</item>
/// <item>Los expedientes de las entregas que todavía no están construidas (embargos K8, tasa negociada K9, fallecimiento y asamblea K10) quedan declarados
/// con su par: la historia que los construya usa estas constantes y la prueba de arquitectura <c>LaSegregacionDeCaptacionesUsaSuCatalogo</c> exige que
/// ningún expediente de captaciones nazca fuera de aquí.</item>
/// </list>
/// </summary>
public static class SegregacionDeCaptaciones
{
    /// <summary>La apertura de una cuenta: quien abre (con o sin excepción) no aprueba la apertura retenida. Raíz: la cuenta.</summary>
    public const string AperturaDeCuenta = "MemberAccount";

    /// <summary>La devolución de aportes sin retiro y la cesión entre asociados: quien la radica no la aprueba ni la niega. Raíz: la solicitud.</summary>
    public const string SolicitudDeAportes = "ContributionRequest";

    /// <summary>El retiro del asociado: quien lo radica no lo aprueba. Raíz: el trámite.</summary>
    public const string RetiroDeAsociado = "MembershipWithdrawal";

    /// <summary>La devolución del retiro (la retenida o la reintegrada): quien liquida no aprueba la devolución. Raíz derivada del trámite.</summary>
    public const string DevolucionDelRetiro = "MembershipWithdrawalRefund";

    /// <summary>Los cargos de una cuenta: quien registra un cargo no lo condona ni exonera los de esa cuenta. Raíz derivada de la cuenta.</summary>
    public const string CargosDeCuenta = "AccountCharge";

    /// <summary>K8 (US11): el embargo. Quien lo registra no lo levanta. Raíz: el embargo.</summary>
    public const string Embargo = "Garnishment";

    /// <summary>K9 (US6, US7): la tasa negociada. Quien la pide no la aprueba. Raíz: la solicitud de tasa.</summary>
    public const string TasaNegociada = "NegotiatedRate";

    /// <summary>K10 (US14): la entrega por fallecimiento del titular. Quien la registra no la aprueba. Raíz: el caso.</summary>
    public const string EntregaPorFallecimiento = "DeceasedHolderCase";

    /// <summary>K10 (US12): la decisión de asamblea (revalorización o distribución). Quien la registra no la aplica. Raíz: la distribución.</summary>
    public const string DecisionDeAsamblea = "AssemblyDistribution";

    /// <summary>Los expedientes cuya raíz es derivada (cuelgan de un recurso que ya es raíz de otro expediente).</summary>
    public static IReadOnlySet<string> ConRaizDerivada { get; } = new HashSet<string>(StringComparer.Ordinal) { DevolucionDelRetiro, CargosDeCuenta };

    /// <summary>Los pares de FR-005 (api.md §1.7), uno por expediente.</summary>
    public static IReadOnlyList<ParDeSegregacionDeCaptaciones> Pares { get; } =
    [
        new(AperturaDeCuenta, AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Decidir, "quien abre la cuenta", "quien aprueba la apertura excepcional",
            EntregaDeCartera.K7),
        new(SolicitudDeAportes, AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Decidir, "quien radica la devolución o la cesión de aportes",
            "quien la aprueba o la niega", EntregaDeCartera.K7),
        new(RetiroDeAsociado, AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Decidir, "quien radica el retiro del asociado", "quien lo aprueba",
            EntregaDeCartera.K7),
        new(DevolucionDelRetiro, AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Decidir, "quien liquida el retiro y radica su devolución",
            "quien aprueba la devolución", EntregaDeCartera.K7),
        new(CargosDeCuenta, AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Decidir, "quien registra un cargo de la cuenta",
            "quien lo condona o exonera los cargos de la cuenta", EntregaDeCartera.K8),
        new(Embargo, AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Decidir, "quien registra el embargo", "quien lo levanta", EntregaDeCartera.K8),
        new(TasaNegociada, AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Decidir, "quien pide la tasa negociada", "quien la aprueba",
            EntregaDeCartera.K9),
        new(EntregaPorFallecimiento, AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Decidir, "quien registra la entrega por fallecimiento",
            "quien la aprueba", EntregaDeCartera.K10),
        new(DecisionDeAsamblea, AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Decidir, "quien registra la decisión de asamblea",
            "quien la aplica", EntregaDeCartera.K10),
    ];

    /// <summary>Los tipos de expediente de captaciones.</summary>
    public static IReadOnlyList<string> Expedientes { get; } = Pares.Select(p => p.Expediente).ToList();

    /// <summary>¿Es un expediente de captaciones?</summary>
    public static bool Existe(string? expediente) => expediente is not null && Expedientes.Contains(expediente, StringComparer.Ordinal);

    /// <summary>El par del expediente.</summary>
    public static ParDeSegregacionDeCaptaciones ParDe(string expediente) =>
        Pares.FirstOrDefault(p => string.Equals(p.Expediente, expediente, StringComparison.Ordinal))
        ?? throw new ArgumentException($"«{expediente}» no es un expediente de captaciones.", nameof(expediente));

    /// <summary>
    /// La raíz del expediente sobre la que se compara: el mismo recurso si el expediente es dueño de su recurso, o un <c>Guid</c> fijo derivado del tipo y del
    /// recurso (SHA-256 de <c>«tipo:recurso»</c>, los primeros 16 bytes) si el recurso ya es raíz de otro expediente (<see cref="ConRaizDerivada"/>).
    /// </summary>
    public static Guid Raiz(string expediente, Guid recurso)
    {
        _ = ParDe(expediente);
        return ConRaizDerivada.Contains(expediente) ? Derivada(expediente, recurso) : recurso;
    }

    /// <summary>La raíz derivada de un tipo y un recurso (la de la devolución del retiro es la de la 014 desde US10: no cambia).</summary>
    public static Guid Derivada(string expediente, Guid recurso)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expediente);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{expediente}:{recurso:N}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    /// <summary>
    /// Evalúa el acto sobre el expediente con la regla fija y los pares de la cooperativa (<see cref="ReglasDeSegregacion.Evaluar"/>): el acto tiene que ser
    /// uno de los dos del par del expediente y su raíz la de <see cref="Raiz"/>.
    /// </summary>
    public static ConflictoDeSegregacion? Evaluar(string expediente, ActoDeSegregacion acto, IEnumerable<IntervencionPrevia> previas,
        IEnumerable<ParIncompatible> pares)
    {
        ArgumentNullException.ThrowIfNull(acto);
        var par = ParDe(expediente);
        if (!string.Equals(acto.Accion, par.Registra, StringComparison.Ordinal) && !string.Equals(acto.Accion, par.Aprueba, StringComparison.Ordinal))
            throw new ArgumentException($"En «{expediente}» sólo se registra ({par.Registra}) o se aprueba ({par.Aprueba}).", nameof(acto));
        return ReglasDeSegregacion.Evaluar(acto, previas, pares);
    }
}
