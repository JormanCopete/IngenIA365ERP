using System.Text.Json;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Approvals.Transactions;
using IngenIA365ERP.Domain.Enums.Approvals;

namespace IngenIA365ERP.Domain.Entities.Approvals;

/// <summary>
/// Una solicitud de aprobación (<c>COR_ApprovalRequests</c>; feature 012, T33, T081; data-model §21). Lleva
/// <b>sellados</b> la política vigente en la fecha de operación y sus niveles (<see cref="RequiredLevelsJson"/>), el
/// monto, quién creó y quién pidió, los excluidos (<see cref="ExcludedUserIdsJson"/>, por <c>SEC_Users.Id</c>) y la
/// huella de lo que se aprueba (<see cref="ContentSha256"/>): cambiar la política después no altera una solicitud
/// pendiente. Una sola pendiente por (fuente, sujeto). La escribe sólo <c>MotorDeAprobaciones</c>.
/// </summary>
public class ApprovalRequest : AuditableEntity
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary><c>Inventory</c> (máx. 20).</summary>
    public string Module { get; set; } = ApprovalPolicy.ModuloInventario;

    /// <summary>Una de <see cref="ApprovalSubjects"/> (máx. 40).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Una de <see cref="ApprovalSourceTypes"/> (máx. 60).</summary>
    public string SourceType { get; set; } = string.Empty;

    public Guid SourcePublicId { get; set; }

    /// <summary>Lo que la bandeja muestra sin leer el módulo, p. ej. «AJ-000123» (máx. 80).</summary>
    public string SourceLabel { get; set; } = string.Empty;

    /// <summary>La bodega de lo que se aprueba: el aprobador también necesita alcance sobre ella (T35).</summary>
    public Guid? ScopeWarehousePublicId { get; set; }

    /// <summary>El punto de venta de lo que se aprueba (I3).</summary>
    public Guid? ScopePointOfSalePublicId { get; set; }

    /// <summary>El monto que se evaluó (18,2).</summary>
    public decimal Amount { get; set; }

    /// <summary><c>COP</c> (char(3)).</summary>
    public string Currency { get; set; } = "COP";

    /// <summary>La fecha de operación: la política vigente a esta fecha es la que se sella.</summary>
    public DateOnly OperationDate { get; set; }

    /// <summary>La versión de política sellada; nula en las reglas fijas de un nivel.</summary>
    public int? PolicyId { get; set; }

    public ApprovalPolicy? Policy { get; set; }

    /// <summary>Niveles sellados: <c>[{ order, threshold, permission }]</c> (máx. 2000).</summary>
    public string RequiredLevelsJson { get; set; } = "[]";

    /// <summary>El creador del documento (<c>SEC_Users.Id</c>).</summary>
    public int CreatedByUserId { get; set; }

    /// <summary>Quien pidió la aprobación (<c>SEC_Users.Id</c>).</summary>
    public int RequestedByUserId { get; set; }

    /// <summary>
    /// Los excluidos por <c>SEC_Users.Id</c>, en JSON (máx. 400): creador, solicitante y participantes declarados por
    /// el documento; se suman quienes aprueban cada nivel.
    /// </summary>
    public string ExcludedUserIdsJson { get; set; } = "[]";

    public ApprovalRequestStatus Status { get; set; }

    /// <summary>El orden del nivel que falta decidir (tinyint).</summary>
    public byte CurrentLevel { get; set; }

    /// <summary>Huella de lo que se aprueba (char(64), hex en minúsculas).</summary>
    public string ContentSha256 { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; }

    public DateTime? DecidedAt { get; set; }

    public ICollection<ApprovalDecision> Decisions { get; set; } = [];

    /// <summary>Los niveles sellados.</summary>
    public IReadOnlyList<NivelDeAprobacion> NivelesRequeridos() =>
        (JsonSerializer.Deserialize<List<NivelSellado>>(RequiredLevelsJson, Json) ?? [])
            .Select(n => new NivelDeAprobacion(n.Order, n.Threshold, n.Permission))
            .OrderBy(n => n.Order)
            .ToList();

    /// <summary>Sella los niveles en <see cref="RequiredLevelsJson"/> con la forma de data-model §21.</summary>
    public void SellarNiveles(IEnumerable<NivelDeAprobacion> niveles) =>
        RequiredLevelsJson = JsonSerializer.Serialize(
            niveles.OrderBy(n => n.Order).Select(n => new NivelSellado(n.Order, n.Threshold, n.PermissionCode)), Json);

    /// <summary>Todos los excluidos sellados: creador, solicitante, participantes y quienes ya aprobaron un nivel.</summary>
    public IReadOnlyList<int> Excluidos() => JsonSerializer.Deserialize<List<int>>(ExcludedUserIdsJson, Json) ?? [];

    /// <summary>Sella los excluidos (sin repetir, en orden).</summary>
    public void SellarExcluidos(IEnumerable<int> excluidos) =>
        ExcludedUserIdsJson = JsonSerializer.Serialize(excluidos.Distinct().OrderBy(x => x), Json);

    /// <summary>Suma a los excluidos a quien acaba de aprobar un nivel.</summary>
    public void Excluir(int userId) => SellarExcluidos(Excluidos().Append(userId));
}
