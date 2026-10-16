using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_CollectionActions</c> (feature 013, K4, T0936; data-model §26.7; <c>adaptadores-y-archivos.md</c> §4.8; FR-119, FR-122): una gestión
/// de cobranza —instante, canal, persona contactada (deudor o codeudor; la pagaduría sólo para la gestión ante ella, FR-114), si fue
/// <b>contacto directo</b> con el titular, resultado, notas, gestor y, si la hubo, la decisión de la ventana legal vigente al registrarla—. Un
/// contacto directo fija el canal de la semana (Ley 2300; <see cref="HistorialDeContactos.Componer"/>). Una corrección es otra gestión que
/// anula a ésta (<see cref="VoidsActionId"/> con motivo): la anulada deja de contar. Hecho de sólo inserción (<see cref="IHechoInmutable"/>).
/// (nuevo)
/// </summary>
public class CollectionAction : AuditableEntity, IHechoInmutable
{
    public const int LargoDeLasNotas = 2000;

    public int? LoanId { get; init; }

    public int PersonId { get; init; }

    public ContactRole ContactRole { get; init; }

    public CollectionActionChannel Channel { get; init; }

    /// <summary>UTC.</summary>
    public DateTime OccurredAt { get; init; }

    public bool IsDirectContact { get; init; }

    public CollectionActionResult Result { get; init; }

    public string? Notes { get; init; }

    /// <summary>Quien la registró; nulo en una gestión de sistema (<see cref="CollectionActionChannel.System"/>, K4 bloque D).</summary>
    public int? CollectorUserId { get; init; }

    /// <summary>¿La dejó el sistema y no una persona? (K4, bloque D)</summary>
    public bool EsDeSistema => Channel == CollectionActionChannel.System;

    /// <summary>Si la gestión fue un mensaje.</summary>
    public int? DebtorMessageId { get; init; }

    public int? VoidsActionId { get; init; }

    public string? VoidReason { get; init; }

    /// <summary>
    /// <c>Permitido</c>, <c>Diferido</c> o <c>Excluido</c>: lo que decía la ventana legal al registrarla (§4.8). Nula si el canal no es de
    /// mensaje (teléfono, visita, en persona). <b>Columna que data-model §26.7 no trae</b>: la pide el contrato §4.8 y T0945.
    /// </summary>
    public string? WindowDecision { get; init; }

    /// <summary>Los motivos de esa decisión (<c>ContactWindowReason</c>, separados por coma). Ver <see cref="WindowDecision"/>.</summary>
    public string? WindowReasonCodes { get; init; }

    /// <summary>
    /// La gestión, validada: la pagaduría no es contacto directo con el titular; una corrección lleva motivo; las notas caben. Que la persona
    /// sea deudor o codeudor del crédito lo comprueba el comando (<c>Lending.CollectionAction.ContactNotObligated</c>).
    /// </summary>
    public static CollectionAction Registrar(int? creditoId, int personaId, ContactRole rol, CollectionActionChannel canal, DateTime ocurrioEnUtc,
        bool contactoDirecto, CollectionActionResult resultado, int gestorId, string? notas = null, int? mensajeId = null,
        int? anulaA = null, string? motivoDeAnulacion = null, DecisionDeContacto? decision = null)
    {
        if (!Enum.IsDefined(rol) || !Enum.IsDefined(canal) || !Enum.IsDefined(resultado))
            throw new ArgumentException("Papel, canal o resultado de gestión desconocido.");
        if (canal == CollectionActionChannel.System || resultado == CollectionActionResult.LeftCollection)
            throw new ArgumentException("El canal y el resultado de sistema sólo los escribe el sistema (DeSistema).", nameof(canal));
        if (rol == ContactRole.Paymaster && contactoDirecto)
            throw new ArgumentException("La gestión ante la pagaduría no es contacto directo con el titular.", nameof(contactoDirecto));
        if (anulaA is null != string.IsNullOrWhiteSpace(motivoDeAnulacion))
            throw new ArgumentException("Una gestión que anula otra lleva el motivo, y sólo ella.", nameof(motivoDeAnulacion));
        if (notas is { Length: > LargoDeLasNotas })
            throw new ArgumentException($"Las notas son de hasta {LargoDeLasNotas} caracteres.", nameof(notas));

        string? decisionTexto = decision switch
        {
            null => null,
            Permitido => "Permitido",
            Diferido => "Diferido",
            Excluido => "Excluido",
            _ => throw new ArgumentOutOfRangeException(nameof(decision)),
        };
        string? motivos = decision switch
        {
            Diferido d => string.Join(',', d.Motivos),
            Excluido x => x.Motivo.ToString(),
            _ => null,
        };

        return new CollectionAction
        {
            LoanId = creditoId,
            PersonId = personaId,
            ContactRole = rol,
            Channel = canal,
            OccurredAt = ocurrioEnUtc,
            IsDirectContact = contactoDirecto,
            Result = resultado,
            Notes = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim(),
            CollectorUserId = gestorId,
            DebtorMessageId = mensajeId,
            VoidsActionId = anulaA,
            VoidReason = string.IsNullOrWhiteSpace(motivoDeAnulacion) ? null : motivoDeAnulacion.Trim(),
            WindowDecision = decisionTexto,
            WindowReasonCodes = motivos,
        };
    }

    /// <summary>
    /// Una gestión de sistema (K4, bloque D): la deja el ciclo diario sobre el crédito y su deudor, sin gestor, sin contacto directo —no cuenta en la ventana
    /// legal— y con la observación del motivo (p. ej. «Sale de cobranza: se puso al día el dd/mm/aaaa»). No se anula.
    /// </summary>
    public static CollectionAction DeSistema(int creditoId, int deudorId, DateTime ocurrioEnUtc, CollectionActionResult resultado, string observacion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(observacion);
        if (!Enum.IsDefined(resultado)) throw new ArgumentOutOfRangeException(nameof(resultado), resultado, "Resultado de gestión desconocido.");
        if (observacion.Length > LargoDeLasNotas) throw new ArgumentException($"Las notas son de hasta {LargoDeLasNotas} caracteres.", nameof(observacion));
        return new CollectionAction
        {
            LoanId = creditoId,
            PersonId = deudorId,
            ContactRole = ContactRole.Debtor,
            Channel = CollectionActionChannel.System,
            OccurredAt = ocurrioEnUtc,
            IsDirectContact = false,
            Result = resultado,
            Notes = observacion.Trim(),
        };
    }

    /// <summary>La gestión como la mira el historial de la ventana, con el instante ya pasado a hora local.</summary>
    public GestionParaHistorial ParaHistorial(DateTime enLocal) => new(Id, enLocal, Channel, IsDirectContact, VoidsActionId);
}
