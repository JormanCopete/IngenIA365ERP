using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Integration.Transactions;

/// <summary>
/// Una arista «este mensaje espera a aquél» (<c>COR_IntegrationMessageDependencies</c>; feature 012, T9;
/// data-model §19; contracts/mensajes.md §9). La agrega <c>EmisorDeMensajes</c> hacia el último mensaje de
/// cada cadena de la que depende el nuevo. Siempre <see cref="DependsOnMessageId"/> &lt; <see cref="MessageId"/>:
/// el orden causal es el del <c>Id</c>. Hecho inmutable.
/// </summary>
public class IntegrationMessageDependency : AuditableEntity, IHechoInmutable
{
    public long MessageId { get; init; }

    public IntegrationMessage? Message { get; init; }

    public long DependsOnMessageId { get; init; }

    public IntegrationMessage? DependsOnMessage { get; init; }
}
