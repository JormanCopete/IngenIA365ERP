using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Approvals;

/// <summary>
/// Una versión de política de aprobación (<c>COR_ApprovalPolicies</c>; feature 012, T33, T081; data-model §21): por
/// módulo, sujeto (<see cref="ApprovalSubjects"/>) y tipo de documento (nulo = todos los tipos del sujeto). Cada alta
/// es una versión nueva de la misma <see cref="PolicyKey"/> que cierra la anterior la víspera; una versión sin
/// niveles significa «sin aprobación» desde <see cref="ValidFrom"/>. Único escritor:
/// <c>SaveApprovalPolicyCommandHandler</c>. La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class ApprovalPolicy : AuditableEntity
{
    /// <summary>El módulo de toda política de esta feature.</summary>
    public const string ModuloInventario = "Inventory";

    /// <summary><c>Inventory</c> (máx. 20).</summary>
    public string Module { get; set; } = ModuloInventario;

    /// <summary>Una de <see cref="ApprovalSubjects"/> (máx. 40).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>El tipo de documento, sin FK (la plataforma no apunta al módulo); nulo = todos los tipos del sujeto.</summary>
    public Guid? DocumentTypePublicId { get; set; }

    /// <summary><c>{Module}|{Subject}|{DocumentTypePublicId|*}</c> (máx. 100): la identidad de la serie de versiones.</summary>
    public string PolicyKey { get; set; } = string.Empty;

    /// <summary>1 la primera; cada alta de la misma clave suma uno.</summary>
    public int Version { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Motivo obligatorio (máx. 300).</summary>
    public string Reason { get; set; } = string.Empty;

    public ICollection<ApprovalPolicyLevel> Levels { get; set; } = [];

    /// <summary>La clave de la serie de versiones de un (módulo, sujeto, tipo).</summary>
    public static string ClaveDe(string module, string subject, Guid? documentTypePublicId) =>
        $"{module}|{subject}|{(documentTypePublicId is { } tipo ? tipo.ToString("D") : "*")}";

    /// <summary>Vigente a la fecha (inclusive en los dos extremos).</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);

    /// <summary>Lo que el motor puro necesita de esta versión.</summary>
    public PoliticaDeAprobacion ParaElEvaluador() => new(
        DocumentTypePublicId,
        ValidFrom,
        ValidTo,
        Levels.Where(l => !l.IsDeleted).OrderBy(l => l.Order).Select(l => new NivelDeAprobacion(l.Order, l.Threshold, l.PermissionCode)).ToList());
}
