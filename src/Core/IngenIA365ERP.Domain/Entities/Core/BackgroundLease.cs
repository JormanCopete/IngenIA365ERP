using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Arrendamiento de un trabajo de fondo <b>en la base de cada cooperativa</b> (<c>COR_BackgroundLeases</c>;
/// feature 012, data-model §0.1, decisiones-transversales T10, T47; Principio IV, FR-083). Cada réplica
/// de la API toma el trabajo de esa cooperativa con <c>UPDATE … WHERE LeaseUntil &lt; ahora OR Owner = yo</c>
/// y lo renueva mientras corre, así que un trabajo no corre dos veces a la vez en la misma cooperativa.
///
/// <para>
/// Lo leen y escriben sólo <c>IArrendamientos</c> (<c>Persistence/Services/ArrendamientosEnBase</c>). La
/// fila de cada nombre la siembra la migración y nunca se da de baja; <c>[SinDiffDeAuditoria]</c> porque
/// renovar no es un cambio que auditar. La exactitud de los trabajos <b>nunca</b> depende de esta fila.
/// </para>
/// </summary>
[SinDiffDeAuditoria]
public class BackgroundLease : AuditableEntity
{
    /// <summary>Nombre fijo del trabajo (<c>NombresDeArrendamiento</c>); único.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Réplica que lo tiene (máquina, proceso y un Guid por arranque); nulo = libre.</summary>
    public string? Owner { get; set; }

    /// <summary>Instante UTC en que vence; vencido, cualquiera lo toma.</summary>
    public DateTime LeaseUntil { get; set; }
}
