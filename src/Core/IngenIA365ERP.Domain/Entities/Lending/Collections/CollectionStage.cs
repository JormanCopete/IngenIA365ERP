using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_CollectionStages</c> (feature 013, K4, T0936; data-model §26.4; FR-117, FR-121): una versión de una etapa de cobranza —código de
/// la cooperativa, orden, rango de días de mora (la preventiva admite negativos), si es la jurídica, acciones sugeridas, plantillas y la
/// regla del gasto de cobranza—, vigente desde <see cref="ValidFrom"/> hasta <see cref="ValidTo"/>. Una versión nueva cierra la anterior la
/// víspera (<see cref="CerrarVigencia"/>): sólo cambia <see cref="ValidTo"/>. Entre las vigentes un día no hay cruces ni huecos
/// (<see cref="EtapasDeCobranza.Validar"/>). <c>UK (Code, ValidFrom)</c>. (nuevo)
/// </summary>
public class CollectionStage : AuditableEntity
{
    public const int LargoDelCodigo = 10;

    /// <summary>La vigencia no se puede cerrar antes de empezar.</summary>
    public const string CodigoVigenciaInvalida = "Lending.CollectionStage.ValidityInvalid";

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Order { get; init; }

    public int MinDaysPastDue { get; init; }

    /// <summary>Nulo = sin tope (sólo la última).</summary>
    public int? MaxDaysPastDue { get; init; }

    /// <summary>Etapa jurídica: entrega a abogado (§26.10, FR-124).</summary>
    public bool IsLegal { get; init; }

    public string? SuggestedActionsJson { get; init; }

    public string? TemplateCodesJson { get; init; }

    /// <summary>Base, tarifa, tope y momento del gasto de cobranza, bajo el techo nacional (FR-121).</summary>
    public string? CollectionFeeRuleJson { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateOnly? ValidTo { get; private set; }

    public static CollectionStage Nueva(string codigo, string nombre, int orden, int minimoDias, int? maximoDias, bool juridica, DateOnly desde,
        string? accionesJson = null, string? plantillasJson = null, string? gastoJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        var code = codigo.Trim().ToUpperInvariant();
        if (code.Length > LargoDelCodigo || code.Any(char.IsWhiteSpace))
            throw new ArgumentException($"El código de la etapa es de hasta {LargoDelCodigo} caracteres y sin espacios.", nameof(codigo));
        if (maximoDias is { } max && max < minimoDias)
            throw new ArgumentException("El máximo de días de la etapa no puede ser menor que el mínimo.", nameof(maximoDias));
        return new CollectionStage
        {
            Code = code,
            Name = nombre.Trim(),
            Order = orden,
            MinDaysPastDue = minimoDias,
            MaxDaysPastDue = maximoDias,
            IsLegal = juridica,
            ValidFrom = desde,
            SuggestedActionsJson = accionesJson,
            TemplateCodesJson = plantillasJson,
            CollectionFeeRuleJson = gastoJson,
        };
    }

    public bool VigenteEn(DateOnly dia) => ValidFrom <= dia && (ValidTo is not { } hasta || dia <= hasta);

    public DefinicionDeEtapa Definicion() => new(Code, Order, MinDaysPastDue, MaxDaysPastDue, IsLegal);

    /// <summary>Cierra la vigencia el <paramref name="hasta"/> (la víspera de la versión nueva); no antes de empezar ni dos veces.</summary>
    public void CerrarVigencia(DateOnly hasta)
    {
        if (ValidTo is not null)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoVigenciaInvalida, $"La etapa «{Code}» ya tiene la vigencia cerrada.", "validTo", ValidTo));
        if (hasta < ValidFrom)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoVigenciaInvalida,
                $"La etapa «{Code}» rige desde {ValidFrom:yyyy-MM-dd}: no se cierra antes.", "validFrom", ValidFrom));
        ValidTo = hasta;
    }
}
