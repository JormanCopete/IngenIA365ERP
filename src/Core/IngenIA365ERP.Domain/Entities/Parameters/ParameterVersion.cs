using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Parameters;

namespace IngenIA365ERP.Domain.Entities.Parameters;

/// <summary>
/// Una vigencia de parámetro (<c>COR_ParameterVersions</c>; feature 012, T21, T069; data-model §4.1). El valor se
/// guarda como texto y lo tipa <c>DefinicionDeParametro</c>. Único lector: <c>LectorDeParametros</c>; único
/// escritor: <c>AddParameterVersionCommandHandler</c>. Una vigencia que ya empezó no se edita ni se borra: una nueva
/// la cierra la víspera (<see cref="ValidTo"/>). La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class ParameterVersion : AuditableEntity
{
    /// <summary><c>INV</c>, <c>TAX</c> o <c>EINV</c> (máx. 10).</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Clave de un catálogo cerrado (máx. 80).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary><see cref="ParameterScopeKind.None"/> = general.</summary>
    public ParameterScopeKind ScopeKind { get; set; }

    /// <summary>0 si es general; si no, el Id de la bodega, tipo de documento, punto, caja o tipo de tercero. Sin FK.</summary>
    public int ScopeId { get; set; }

    /// <summary>Texto canónico del valor (máx. 2000).</summary>
    public string Value { get; set; } = string.Empty;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Motivo obligatorio (<c>IConMotivo</c>, máx. 500).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Norma o acta que la respalda (máx. 200).</summary>
    public string? LegalSource { get; set; }

    /// <summary>Vigente a la fecha (inclusive en los dos extremos).</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}
