using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;

/// <summary>
/// <c>LND_AssemblyReturnBases</c> (feature 014, respuesta 90 C del dueño, E261): la base sobre la que la asamblea reparte los retornos de un asociado —el uso de
/// servicios, los ahorros u otra que ella defina—, tal como la cooperativa la subió por la plantilla <c>lending.assembly-return-bases</c>. Una carga es una
/// <b>versión</b> entera (todo o nada) de la decisión en borrador y la última reemplaza a las anteriores, que quedan como constancia: hecho de sólo inserción
/// (<see cref="IHechoInmutable"/>). La decisión que la usa tiene <c>BaseMethod = UploadedBase</c>. (nuevo)
/// </summary>
public class AssemblyReturnBase : AuditableEntity, IHechoInmutable
{
    public const int LargoDelConcepto = 120;

    public int AssemblyDistributionId { get; init; }

    /// <summary>1 en la primera carga de la decisión; sube con cada carga nueva.</summary>
    public int LoadVersion { get; init; }

    public int PersonId { get; init; }

    /// <summary>La base del asociado, en pesos (o en la unidad que defina la asamblea: sólo pesa frente a la de los demás).</summary>
    public decimal BaseAmount { get; init; }

    /// <summary>Sobre qué se reparte (uso de servicios, ahorros…): informativo.</summary>
    public string? Concept { get; init; }
}
