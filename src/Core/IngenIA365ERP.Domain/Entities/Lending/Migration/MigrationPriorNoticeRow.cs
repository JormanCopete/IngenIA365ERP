using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationPriorNotices</c> (feature 013, K1; data-model §29.3, parte <c>prior-notices</c>; plantillas.md §5.3.13): el aviso previo al
/// reporte negativo de <b>un episodio</b> de un crédito, con sus una o dos comunicaciones ya enviadas; llave crédito + episodio. Hecho sin diff de
/// auditoría. Un episodio que no es el vigente según las cuotas es aviso y no se migra. Al activar nace <c>LND_PriorNotices</c> (columnas
/// <c>Migrated*</c>); sin referencia del primer envío, el aviso nace esperando la constancia.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationPriorNoticeRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public string SourceLoanNumber { get; init; } = string.Empty;

    /// <summary>Vencimiento de la primera cuota impaga del episodio (<c>episodio</c>).</summary>
    public DateOnly EpisodeStartDate { get; init; }

    /// <summary>1 o 2 (<c>comunicacionesExigidas</c>).</summary>
    public int RequiredCommunications { get; init; }

    public DateOnly FirstSentOn { get; init; }

    public MessageChannel FirstChannel { get; init; }

    /// <summary>La constancia del primer envío (guía, id del envío).</summary>
    public string? FirstReference { get; init; }

    /// <summary>Con dos comunicaciones; en un día distinto del primero.</summary>
    public DateOnly? SecondSentOn { get; init; }

    public MessageChannel? SecondChannel { get; init; }

    public string? SecondReference { get; init; }
}
