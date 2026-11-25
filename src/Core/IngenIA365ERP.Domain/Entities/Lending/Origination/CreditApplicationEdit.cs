using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_CreditApplicationEdits</c> (pedido del dueño del 2026-11-25): cada edición de una solicitud ya radicada, sin devolverla. Quién, cuándo,
/// por qué, en qué estado estaba, qué cambió (dato por dato, con los nombres ya resueltos) y el nivel antes y después del reenrutamiento. Hecho de
/// sólo inserción (<see cref="IHechoInmutable"/>): el detalle de la solicitud lo muestra a quien decide. (nuevo)
/// </summary>
public class CreditApplicationEdit : AuditableEntity, IHechoInmutable
{
    public const int LargoDelMotivo = 500;

    public int ApplicationId { get; init; }

    /// <summary>El estado en que estaba al editarla (<c>Submitted</c> o <c>PendingDecision</c>).</summary>
    public CreditApplicationStatus StatusAtEdit { get; init; }

    public int EditedByUserId { get; init; }

    public DateTime EditedAt { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>JSON con cada dato distinto: grupo, dato, antes y después (como lo ve una persona).</summary>
    public string Changes { get; init; } = "[]";

    /// <summary>El nivel de atribución antes y después de volver a enrutarla (pueden ser iguales).</summary>
    public int? LevelBeforeId { get; init; }

    public int? LevelAfterId { get; init; }

    public static CreditApplicationEdit Registrar(int applicationId, CreditApplicationStatus estado, int usuarioId, DateTime instante, string motivo,
        string cambios, int? nivelAntes, int? nivelDespues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ArgumentException.ThrowIfNullOrWhiteSpace(cambios);
        var texto = motivo.Trim();
        return new CreditApplicationEdit
        {
            ApplicationId = applicationId,
            StatusAtEdit = estado,
            EditedByUserId = usuarioId,
            EditedAt = instante,
            Reason = texto.Length > LargoDelMotivo ? texto[..LargoDelMotivo] : texto,
            Changes = cambios,
            LevelBeforeId = nivelAntes,
            LevelAfterId = nivelDespues,
        };
    }
}
