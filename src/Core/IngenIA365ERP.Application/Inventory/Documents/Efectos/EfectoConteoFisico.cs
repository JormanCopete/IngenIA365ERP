using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>PhysicalCount</c> (feature 012, US11, T398; data-model §8; contracts/api.md §12). El conteo no mueve existencia
/// ni emite: su foto y sus capturas son satélites (<c>INV_CountSnapshotLines</c>, <c>INV_CountCaptures</c>) y lo que ajusta la
/// existencia son sus ajustes aprobados. Por eso su confirmación —el cierre, <c>ClosePhysicalCountCommand</c>, que lo numera— no tiene
/// efecto ni mensajes, y su anulación (<c>POST /counts/{id}/void</c>) crea el <c>Voiding</c> que lo referencia <b>sin kardex ni
/// mensajes</b>. Anular uno con ajustes en curso o vigentes lo rechaza el ciclo común nombrándolos
/// (<c>Inventory.Document.HasDependents</c>: sus ajustes lo tienen como origen <c>CountAdjustmentOf</c>); uno abierto no se anula
/// (<c>Inventory.Document.NotConfirmed</c>): se descarta con motivo y sus productos quedan libres (el bloqueo sólo mira conteos
/// abiertos). (nuevo)
/// </summary>
public sealed class EfectoConteoFisico : EfectoDeClaseBase
{
    public override DocumentClass Clase => DocumentClass.PhysicalCount;

    /// <summary>El conteo no se aprueba por monto: no tiene valor propio.</summary>
    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => 0m;
}
