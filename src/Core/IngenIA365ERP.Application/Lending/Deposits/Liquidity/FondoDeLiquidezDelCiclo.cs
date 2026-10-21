using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Liquidity;

/// <summary>
/// El cierre del día del paso de captaciones para el fondo de liquidez (feature 014, US15, T216; FR-109; research §24): en los días de corte de la entidad
/// (<c>FondoDeLiquidez.Periodicidad</c>: cada día, el domingo o el último día del mes) toma la foto del fondo con la medición del IRL y sus alertas. Corre desde
/// K11 y sólo con captaciones activadas. Nada aquí bloquea: sin categoría en el perfil o con un error, deja el aviso en el registro y el ciclo sigue (los
/// plazos y periodicidades regulatorios son informativos). (nuevo)
/// </summary>
public sealed class FondoDeLiquidezDelCiclo(
    IApplicationDbContext db,
    FotosDelFondoDeLiquidez fotos,
    FondoDeLiquidezDeCartera fondo,
    IContextoDeLineasDeCaptacion contexto,
    ILogger<FondoDeLiquidezDelCiclo>? logger = null) : ICierreDelDiaDeCaptaciones
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public string Nombre => "FondoDeLiquidez";

    public async Task EjecutarAsync(LendingProcessRun corrida, DateOnly d, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (contexto.Entrega < EntregaDeCartera.K11) return;
        if (!(await ActivacionesDeCaptacion.LeerAsync(db, ct)).RigeEn(LineNature.Deposit, d)) return;
        if (!EsDiaDeCorte(await fondo.TextoAsync(ParametrosDeCartera.FondoDeLiquidezPeriodicidad, d, ct), d)) return;
        var r = await fotos.TomarAsync(d, "Corte del ciclo diario", corrida.Id, ct);
        if (r.IsFailure)
            _log.LogWarning("[Cartera.FondoDeLiquidez] La foto del {Fecha} no se tomó: {Codigo} {Mensaje}", d, r.Error.Code, r.Error.Message);
    }

    /// <summary>¿Es <paramref name="d"/> día de corte con la periodicidad dada? (<c>Diaria</c>; <c>Semanal</c>: el domingo; <c>Mensual</c>: el último día del mes.)</summary>
    public static bool EsDiaDeCorte(string periodicidad, DateOnly d) => periodicidad switch
    {
        "Diaria" => true,
        "Semanal" => d.DayOfWeek == DayOfWeek.Sunday,
        _ => d.AddDays(1).Month != d.Month,
    };
}
