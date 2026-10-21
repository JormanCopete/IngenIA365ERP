using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Liquidity;

/// <summary>
/// <c>LND_LiquidityFundSnapshots</c> (feature 014, US15, T215; data-model §6.7; FR-025, FR-109; research R25 y §24): la foto inmutable del fondo de liquidez a una
/// fecha —la categoría del perfil regulatorio, el régimen que rigió (general, por madurez o en transición), la base por clase y banda, el requerido, el
/// constituido de esa fecha y el faltante— y, con ella, la medición del riesgo de liquidez (IRL por bandas y su mínimo). Hecho de sólo inserción: una
/// fecha tiene una sola foto (<c>UK_LND_LiquidityFundSnapshots_Date_Version</c> con <see cref="Version"/> 1); pedirla otra vez devuelve la que ya está. La
/// toma el tesorero (<c>Lending.LiquidityFund.Manage</c>) o el ciclo diario en los días de corte de la cooperativa. (nuevo)
/// </summary>
public class LiquidityFundSnapshot : AuditableEntity, IHechoInmutable
{
    public const int LargoDeLaCategoria = 20;
    public const int LargoDelRegimen = 30;
    public const int LargoDelMotivo = 500;

    public DateOnly SnapshotDate { get; init; }

    public int Version { get; init; } = 1;

    public DateTime RegisteredAt { get; init; }

    /// <summary>La categoría del perfil regulatorio vigente (<c>LiquidityFundCategory</c>).</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary><c>General</c>, <c>PorMadurez</c> o <c>Transicion</c> (rige el general y el nuevo se informa).</summary>
    public string RegimeStage { get; init; } = string.Empty;

    /// <summary>Los renglones de la base (clase, banda, cuentas, capital, intereses, base, porcentaje, requerido) en JSON.</summary>
    public string BaseByClassJson { get; init; } = "[]";

    public decimal RequiredAmount { get; init; }

    /// <summary>En transición: lo que requeriría el régimen nuevo (informativo).</summary>
    public decimal? NewRegimeRequiredAmount { get; init; }

    /// <summary>Σ de los <see cref="LiquidityFundHolding"/> vigentes en la fecha.</summary>
    public decimal ConstitutedAmount { get; init; }

    /// <summary>max(0, requerido − constituido); mayor que cero alerta.</summary>
    public decimal Shortfall { get; init; }

    /// <summary>El IRL acumulado más bajo de las bandas (nulo sin salidas en el horizonte).</summary>
    public decimal? MinimumIrl { get; init; }

    /// <summary>La medición del riesgo de liquidez (activos líquidos netos y bandas) en JSON; nula si no se midió.</summary>
    public string? LiquidityRiskJson { get; init; }

    /// <summary>Los parámetros leídos con su versión y los pisos legales que rigieron.</summary>
    public string ParametersSnapshotJson { get; init; } = "{}";

    /// <summary>La corrida del ciclo que la tomó (nula si la pidió una persona).</summary>
    public int? ProcessRunId { get; init; }

    public int? RequestedByUserId { get; init; }

    public string? Reason { get; init; }

    /// <summary>La alerta que levantó (faltante o IRL bajo), si levantó alguna.</summary>
    public Guid? AlertPublicId { get; init; }

    public string InputsSha256 { get; init; } = string.Empty;
}

/// <summary>
/// <c>LND_LiquidityFundHoldings</c> (feature 014, US15, T215; data-model §6.8; FR-109): lo constituido del fondo de liquidez que registra el tesorero —la clase de
/// inversión, la entidad depositaria (una persona jurídica), el número del título o de la cuenta, el valor, desde cuándo y hasta cuándo, el soporte—. Documento
/// con estado por fechas: vigente desde <see cref="ConstitutedOn"/> hasta la víspera de <see cref="WithdrawnOn"/>; terminarlo es registrar
/// <see cref="WithdrawnOn"/> con su motivo (nunca se borra). La auditoría lleva el antes y el después. (nuevo)
/// </summary>
public class LiquidityFundHolding : AuditableEntity
{
    public const string CodigoTransicionInvalida = "Lending.LiquidityFund.InvalidTransition";
    public const int LargoDeLaReferencia = 60;
    public const int LargoDeLaNota = 500;
    public const int LargoDelTipoDeTitulo = 10;

    public LiquidityHoldingKind Kind { get; set; }

    /// <summary>La entidad depositaria (<c>COR_People</c>).</summary>
    public int InstitutionPersonId { get; set; }

    /// <summary>Número del título, de la cuenta o de la participación.</summary>
    public string Reference { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateOnly ConstitutedOn { get; set; }

    public DateOnly? MaturesOn { get; set; }

    /// <summary>El día en que dejó de contar (el retiro, la redención o el vencimiento sin renovación); nulo mientras sigue.</summary>
    public DateOnly? WithdrawnOn { get; set; }

    public Guid SupportAttachmentPublicId { get; set; }

    /// <summary>Con un CDT o CDAT (<see cref="LiquidityHoldingKind.TermInvestment"/>): <c>CDT</c> o <c>CDAT</c> tal como lo registró el tesorero (respuesta 104 B del dueño, E250).</summary>
    public string? TermTitleKind { get; set; }

    /// <summary>Los intereses causados del título que el 9067 pide por título (respuesta 104 B del dueño, E250); cero si no se informan.</summary>
    public decimal AccruedInterest { get; set; }

    /// <summary>Si los intereses del título se capitalizan (respuesta 104 B del dueño, E250).</summary>
    public bool CapitalizesInterest { get; set; }

    public string Note { get; set; } = string.Empty;

    public int RegisteredByUserId { get; set; }

    public string? EndReason { get; set; }

    public int? EndedByUserId { get; set; }

    /// <summary>¿Cuenta en el constituido del día <paramref name="dia"/>?</summary>
    public bool VigenteEn(DateOnly dia) => !IsDeleted && ConstitutedOn <= dia && (WithdrawnOn is null || dia < WithdrawnOn);

    /// <summary>Lo termina el <paramref name="dia"/> (desde ese día ya no cuenta). Sólo una vez y nunca antes de constituirse.</summary>
    public void Terminar(DateOnly dia, string motivo, int? usuario)
    {
        if (WithdrawnOn is not null)
            throw new ReglaDeCarteraException(new ProblemaDeCartera(CodigoTransicionInvalida, $"La inversión {Reference} ya se terminó el {WithdrawnOn:yyyy-MM-dd}."));
        if (dia < ConstitutedOn)
            throw new ReglaDeCarteraException(new ProblemaDeCartera(CodigoTransicionInvalida, $"La inversión {Reference} se constituyó el {ConstitutedOn:yyyy-MM-dd}: no termina antes."));
        WithdrawnOn = dia;
        EndReason = motivo.Length > LargoDeLaNota ? motivo[..LargoDeLaNota] : motivo;
        EndedByUserId = usuario;
    }
}
