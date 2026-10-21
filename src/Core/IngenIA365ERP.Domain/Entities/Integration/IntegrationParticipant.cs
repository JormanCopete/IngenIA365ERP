using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Domain.Entities.Integration;

/// <summary>
/// Un participante del registro de integración de la cooperativa (<c>COR_IntegrationParticipants</c>; feature 013, T16, C35,
/// C36; data-model parte 3 §4.6): un recaudador del contrato de obligaciones o un destino de la bandeja, con su vigencia. Lo
/// lee <c>IRegistroDeIntegracion</c> y lo escribe sólo <c>EscritorDelRegistroDeIntegracion</c>, que agrega sin guardar.
///
/// <para>
/// Es un <b>hecho</b>: habilitar, inhabilitar o retirar no cambia la fila, la <b>cierra</b> (<see cref="ValidTo"/>, que se
/// escribe una sola vez) y, si corresponde, abre otra. Así la historia de quién estuvo habilitado y desde cuándo queda.
/// </para>
/// </summary>
public class IntegrationParticipant : AuditableEntity, IHechoInmutable
{
    /// <summary>
    /// Código del participante (C35): <c>CNT</c>, <c>INV</c>, <c>NOM</c>, <c>CAR</c>, <c>TST</c>; Recaudo, Tesorería y Aportes
    /// registran el suyo. Es el <c>CollectorCode</c> de un recaudador.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    public IntegrationParticipantClass Class { get; init; }

    public IntegrationParticipantRole Role { get; init; }

    /// <summary>
    /// El destino de la bandeja que consume (<c>IntegrationDestinations</c>) con <c>MessageDestination</c>; vacío en un
    /// recaudador, para que el único <c>(Code, Role, Destination)</c> compare igual en los dos motores (C36).
    /// </summary>
    public string Destination { get; init; } = string.Empty;

    /// <summary>Doble de prueba (<c>TesoreriaDePrueba</c>, <c>RecaudadorDePrueba</c>): nunca cuenta en producción (T20).</summary>
    public bool IsTestDouble { get; init; }

    /// <summary>Habilitado (C36). Permite sembrar <c>CAR</c> registrado e inhabilitado hasta la activación de Cartera.</summary>
    public bool IsEnabled { get; init; }

    public DateOnly ValidFrom { get; init; }

    /// <summary>Último día de vigencia: lo escribe el retiro o el cambio de habilitación, una sola vez.</summary>
    [EscrituraUnica]
    public DateOnly? ValidTo { get; set; }

    public string Reason { get; init; } = string.Empty;

    public ActorKind RegisteredByKind { get; init; } = ActorKind.Person;

    /// <summary><c>SEC_Users.Id</c> de quien lo registró; nulo en la semilla.</summary>
    public int? RegisteredByUserId { get; init; }

    public string RegisteredByName { get; init; } = string.Empty;

    /// <summary>Los contratos o tipos de mensaje que adopta, con su versión.</summary>
    public ICollection<IntegrationParticipantAdoption> Adoptions { get; init; } = [];

    /// <summary>¿Rige en <paramref name="fecha"/>? (habilitado o no: eso lo mira quien lo lee).</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}
