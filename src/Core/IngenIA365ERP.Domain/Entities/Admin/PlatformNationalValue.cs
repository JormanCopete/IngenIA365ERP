using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// <c>ADM_NationalValues</c> (feature 013, T37, T38; data-model parte 3 §3.1): un valor nacional escalar del registro
/// central —usura por modalidad certificada, índices, salario mínimo, UVT, festivos y límites legales—. Se registra una
/// vez en la base administrativa y la réplica lo copia sin modificar a cada cooperativa (<c>COR_NationalValues</c>).
/// <b>Ningún cálculo lo lee</b>: sólo el replicador, por <c>IRegistroCentralDeValoresNacionales</c>.
///
/// <para>Es un hecho (Principio XI): sólo se inserta; corregir es una fila nueva con <see cref="SupersedesPublicId"/> y
/// la rechaza <c>GuardaDeInmutabilidad</c> en <c>AdminDbContext.SaveChangesAsync</c> (C68). Lo leen con la regla única
/// <see cref="VigenciaBitemporal"/>, donde R es <see cref="RegisteredAt"/>.</para>
/// </summary>
public class PlatformNationalValue : AuditableEntity, IHechoInmutable, IVersionBitemporal
{
    /// <summary>Código del catálogo cerrado <c>CodigosDeValorNacional</c> (<c>USURA</c>, <c>SMMLV</c>, <c>FESTIVO</c>…).</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Modalidad (<c>USURA</c>), plazo o serie (<c>IBR</c>, <c>IPC</c>), fecha ISO (<c>FESTIVO</c>), código del límite (<c>LIMITE</c>); vacía en los demás.</summary>
    public string Key { get; init; } = string.Empty;

    public DateOnly ValidFrom { get; init; }

    /// <summary>Sólo cuando la norma trae fin (período certificado de la usura; el festivo: igual a <see cref="ValidFrom"/>).</summary>
    public DateOnly? ValidTo { get; init; }

    /// <summary>El período que mide un índice, distinto de cuándo se publica.</summary>
    public DateOnly? ReferenceDate { get; init; }

    /// <summary>Nulo sólo en <c>FESTIVO</c>. Tasas y fracciones como fracción (0,2675 = 26,75 %).</summary>
    public decimal? Value { get; init; }

    /// <summary>Texto de <c>UnidadDeValorNacional</c> (<c>EA</c>, <c>Pesos</c>, <c>Dias</c>…); nulo en <c>FESTIVO</c>.</summary>
    public string? Unit { get; init; }

    /// <summary>Nombre del festivo o descripción del límite.</summary>
    public string? Label { get; init; }

    /// <summary>Norma, resolución o certificación y su fecha.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Obligatorio en una corrección.</summary>
    public string? Reason { get; init; }

    /// <summary>Numeración común con los documentos, tomada de <c>ADM_NationalValueCounters</c>; única.</summary>
    public long Sequence { get; init; }

    /// <summary>Instante UTC del registro: es R en la central.</summary>
    public DateTime RegisteredAt { get; init; }

    /// <summary>El maestro que registró (correo o nombre).</summary>
    public string RegisteredBy { get; init; } = string.Empty;

    /// <summary>Identidad central del maestro (<c>ADM_CentralUsers</c>).</summary>
    public Guid RegisteredByCentralUserId { get; init; }

    /// <summary>La fila que esta corrección reemplaza; una fila se reemplaza a lo sumo una vez (índice único filtrado).</summary>
    public Guid? SupersedesPublicId { get; init; }

    DateTime IVersionBitemporal.ConocidaEn => RegisteredAt;
}
