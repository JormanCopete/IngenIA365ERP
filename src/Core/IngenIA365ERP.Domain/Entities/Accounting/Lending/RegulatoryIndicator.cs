using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting.Lending;

/// <summary>
/// Patrimonio técnico o relación de solvencia que registra el contador con su fuente y su soporte (<c>ACC_RegulatoryIndicators</c>;
/// feature 013, T0634, T59; data-model parte 3 §5.4; contracts/contabilidad.md §4.4 y §15; DC-18). Las pérdidas no se registran: se
/// derivan de las cuentas.
///
/// <para>
/// Es un hecho de <b>sólo inserción</b> (<see cref="IHechoInmutable"/>): corregir es registrar otro del mismo corte con
/// <see cref="SupersedesPublicId"/>, que reemplaza al anterior (C121). <c>IContabilidadParaCartera.IndicadoresDeLaEntidadAsync</c>
/// devuelve el último no reemplazado con corte ≤ la fecha, con su corte; la vigencia la juzga Cartera (C33). Las columnas son las de
/// data-model (<c>Kind</c> en vez de <c>IndicatorCode</c>, sin <c>Unit</c>: la unidad la dice la clase; tasks.md, contradicción 8).
/// </para>
/// </summary>
public class RegulatoryIndicator : AuditableEntity, IHechoInmutable
{
    public RegulatoryIndicatorKind Kind { get; init; }

    /// <summary>Fecha de corte del cálculo.</summary>
    public DateOnly CutoffDate { get; init; }

    /// <summary>Pesos en <see cref="RegulatoryIndicatorKind.TechnicalCapital"/>; fracción en <see cref="RegulatoryIndicatorKind.SolvencyRatio"/>.</summary>
    public decimal Value { get; init; }

    /// <summary>Cómo se calculó (norma, formato).</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>El adjunto de soporte, con dueño <c>RegulatoryIndicatorSupport</c>.</summary>
    public Guid? SupportAttachmentPublicId { get; init; }

    /// <summary>El indicador del mismo corte que éste corrige.</summary>
    public Guid? SupersedesPublicId { get; init; }

    public string? Notes { get; init; }

    public int RegisteredByUserId { get; init; }

    public string RegisteredByName { get; init; } = string.Empty;
}
