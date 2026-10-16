using System.Text.Json;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_CollectorAssignmentRules</c> (feature 013, K4, T0936; data-model §26.6; FR-118): una regla de reparto de cartera entre gestores —la
/// prioridad, los criterios (oficina, etapa, línea, pagaduría, rango de saldo; nulo = cualquiera), la forma (<c>ByCount</c> o
/// <c>ByBalance</c>), si va el deudor completo a un solo gestor y los gestores con su peso (<see cref="CollectorsJson"/>)—, vigente desde
/// <see cref="ValidFrom"/>. Cerrar la vigencia es lo único que cambia. La aplica <see cref="RepartoDeGestores"/>. (nuevo)
/// </summary>
public class CollectorAssignmentRule : AuditableEntity
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string CodigoVigenciaInvalida = "Lending.CollectorAssignmentRule.ValidityInvalid";

    public int Priority { get; init; }

    public Guid? OfficePublicId { get; init; }

    public string? StageCode { get; init; }

    public int? CreditLineId { get; init; }

    /// <summary>La persona de la pagaduría (<c>COR_People</c>).</summary>
    public int? PaymasterPersonId { get; init; }

    public decimal? MinBalance { get; init; }

    public decimal? MaxBalance { get; init; }

    public AssignmentDistribution Distribution { get; init; }

    /// <summary>
    /// Todos los créditos del deudor que casan con la regla van a un solo gestor (FR-118, T0920). <b>Columna que data-model §26.6 no trae</b>:
    /// la pide el caso del reparto «el deudor completo a un solo gestor si la regla lo pide».
    /// </summary>
    public bool AssignWholeDebtor { get; init; }

    /// <summary><c>[{ "userPublicId": "…", "weight": 1 }]</c>: los gestores y su peso.</summary>
    public string CollectorsJson { get; init; } = "[]";

    public DateOnly ValidFrom { get; init; }

    public DateOnly? ValidTo { get; private set; }

    public static string SerializarGestores(IEnumerable<GestorDeRegla> gestores) =>
        JsonSerializer.Serialize(gestores.Select(g => new GestorDeReglaJson(g.UsuarioPublicId, g.Peso)).ToList(), Json);

    public IReadOnlyList<GestorDeRegla> Gestores() =>
        (JsonSerializer.Deserialize<List<GestorDeReglaJson>>(CollectorsJson, Json) ?? []).Select(g => new GestorDeRegla(g.UserPublicId, g.Weight)).ToList();

    /// <summary>Lo que mira el motor; la línea y la pagaduría van por su <c>Id</c> interno, como las trae el crédito.</summary>
    public ReglaDeReparto Regla() => new(PublicId, Priority, Distribution, Gestores())
    {
        Oficina = OfficePublicId,
        Etapa = StageCode,
        Linea = CreditLineId,
        Pagaduria = PaymasterPersonId,
        SaldoMinimo = MinBalance,
        SaldoMaximo = MaxBalance,
        DeudorCompleto = AssignWholeDebtor,
    };

    public static CollectorAssignmentRule Nueva(int prioridad, AssignmentDistribution forma, IReadOnlyList<GestorDeRegla> gestores, DateOnly desde,
        Guid? oficina = null, string? etapa = null, int? lineaId = null, int? pagaduriaId = null, decimal? saldoMinimo = null, decimal? saldoMaximo = null,
        bool deudorCompleto = false)
    {
        ArgumentNullException.ThrowIfNull(gestores);
        var regla = new CollectorAssignmentRule
        {
            Priority = prioridad,
            Distribution = forma,
            CollectorsJson = SerializarGestores(gestores),
            ValidFrom = desde,
            OfficePublicId = oficina,
            StageCode = string.IsNullOrWhiteSpace(etapa) ? null : etapa.Trim().ToUpperInvariant(),
            CreditLineId = lineaId,
            PaymasterPersonId = pagaduriaId,
            MinBalance = saldoMinimo,
            MaxBalance = saldoMaximo,
            AssignWholeDebtor = deudorCompleto,
        };
        ReglaDeCarteraException.SiHay(RepartoDeGestores.Validar(regla.Regla()));
        return regla;
    }

    public bool VigenteEn(DateOnly dia) => ValidFrom <= dia && (ValidTo is not { } hasta || dia <= hasta);

    public void CerrarVigencia(DateOnly hasta)
    {
        if (ValidTo is not null || hasta < ValidFrom.AddDays(-1))
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoVigenciaInvalida,
                "La regla ya está cerrada o se pide cerrarla antes de empezar.", "validFrom", ValidFrom));
        ValidTo = hasta;
    }
}
