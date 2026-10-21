using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits;

/// <summary>Lo que se cotiza de un CDAT: el plazo, el monto, la periodicidad y la modalidad, el inicio y el margen pedido con quién y dónde. (nuevo)</summary>
public sealed record PedidoDeCotizacion(
    decimal Monto,
    int PlazoDias,
    RepaymentPeriodicity Periodicidad,
    InterestTiming Modalidad,
    DateOnly Inicio,
    decimal? Margen,
    Person? Titular,
    int? OficinaId,
    string? Canal);

/// <summary>Un CDAT cotizado: la versión de la línea, la tasa de la tabla y su tramo, la negociación evaluada, el plan y la vista previa con su huella. (nuevo)</summary>
public sealed record CdatCotizado(
    DepositLineVersion Version,
    decimal TasaDeTabla,
    int? TramoId,
    NegociacionEvaluada? Negociacion,
    PlanDeCdatCalculado Plan,
    TermDepositSimulationDto Vista)
{
    /// <summary>¿Se negocia y cabe en la regla? (el margen se aplica, sola o con aprobador).</summary>
    public bool Negocia => Negociacion?.Motor.CabeEnLaRegla == true;
}

/// <summary>
/// La cotización de un CDAT (feature 014, US6, T170; api.md §5 <c>POST /term-deposits/simulate</c>; FR-023, FR-067, FR-127): la versión de la línea de CDAT
/// vigente al inicio, el plazo en su rango (<c>Lending.TermDeposit.TermOutOfRange</c> con el rango), la periodicidad y la modalidad admitidas
/// (<c>.PeriodicityNotAllowed</c>), el monto desde el mínimo de apertura (<c>.AmountOutOfRange</c>), la tasa de la tabla (<c>.NoRateForTerm</c>), el margen
/// negociado evaluado (<see cref="NegociacionDeTasa"/>), el plan con sus fechas hábiles (<see cref="PlanDeCdat"/>), la retención estimada de cada período con la
/// política vigente y el perfil del titular (el umbral diario sólo si alcanza al CDAT, D10) y la huella de la vista previa. No escribe. (nuevo)
/// </summary>
public sealed class CotizacionDeCdat(IApplicationDbContext db, LectorDeCdat lector, NegociacionDeTasa negociacion, ReglasDeRetencion retencion)
{
    public async Task<Result<CdatCotizado>> CotizarAsync(CreditLine linea, PedidoDeCotizacion p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(linea);
        ArgumentNullException.ThrowIfNull(p);
        if (linea.Nature != LineNature.Deposit) return Falla(new Error(ErroresDeCdat.CodigoNoEsCdat, "La línea elegida no es de CDAT."));
        var version = await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, linea.Id, p.Inicio, null, ct);
        if (version is null) return Falla(Accounts.ErroresDeCuentas.LineaSinVersion(linea.Code, p.Inicio));
        if (version.DepositClass != DepositClass.TermDeposit) return Falla(new Error(ErroresDeCdat.CodigoNoEsCdat, $"La línea {linea.Code} no es de CDAT."));
        if (PlanDeCdat.ValidarPlazo(p.PlazoDias, version.MinTermDays, version.MaxTermDays) is { } plazo)
            return Falla(new ErrorConDatos(plazo.Codigo, plazo.Mensaje, new { minTermDays = version.MinTermDays, maxTermDays = version.MaxTermDays }));
        if (PlanDeCdat.ValidarPeriodicidad(p.Periodicidad, p.Modalidad, p.PlazoDias, version.LeerPeriodicidadesDeInteres(), version.LeerModalidadesDeInteres())
            is { } periodicidad)
            return Falla(ErroresDeCdat.De(periodicidad));
        if (p.Monto <= 0m || p.Monto < version.MinOpeningAmount) return Falla(ErroresDeCdat.MontoFueraDeRango(p.Monto, version.MinOpeningAmount));
        var tasa = await lector.TasaDeLaTablaAsync(version, p.PlazoDias, p.Monto, p.Inicio, ct);
        if (tasa.IsFailure) return Falla(tasa.Error);
        var (tasaDeTabla, tramo) = tasa.Value;

        NegociacionEvaluada? negociada = null;
        if (p.Margen is { } margen && margen != 0m)
        {
            if (!version.AllowsNegotiatedRate) return Falla(ErroresDeCdat.LineaNoNegocia(linea.Code));
            var pedido = p.Titular is null
                ? new PedidoDeTasaNegociada(0, p.Monto, p.PlazoDias, tasaDeTabla, 0m, p.OficinaId ?? 0, p.Canal, margen)
                : await negociacion.PedidoAsync(p.Titular, p.OficinaId ?? 0, p.Monto, p.PlazoDias, tasaDeTabla, margen, p.Canal, p.Inicio, ct);
            var evaluada = await negociacion.EvaluarAsync(linea.Id, linea.Code, pedido, p.Inicio, ct);
            if (evaluada.IsFailure) return Falla(evaluada.Error);
            negociada = evaluada.Value;
        }
        var aplicada = tasaDeTabla + (negociada?.Motor.CabeEnLaRegla == true ? p.Margen ?? 0m : 0m);

        var condiciones = new CondicionesDeCdat(p.Monto, aplicada, p.Inicio, p.PlazoDias, p.Periodicidad, p.Modalidad, version.DayCount, version.Redondeo());
        var calendario = await lector.CalendarioAsync(p.Inicio, PlanDeCdat.Vencimiento(p.Inicio, p.PlazoDias), ct);
        var plan = PlanDeCdat.Generar(condiciones, calendario);

        var politica = await retencion.PoliticaAsync(version, p.Inicio, ct);
        if (politica.IsFailure) return Falla(politica.Error);
        var (perfil, natural) = p.Titular is null ? (PerfilDeRetencion.Standard, true) : ReglasDeRetencion.PerfilDe(p.Titular);
        var lineas = plan.Periodos.Select(x =>
        {
            var diarios = Enumerable.Range(1, x.Hasta.DayNumber - x.Desde.DayNumber)
                .Select(i => x.Desde.AddDays(i)).Select(d => new InteresDiarioDeRetencion(d, PlanDeCdat.CausadoDelDia(plan, d))).ToList();
            var r = RetencionSobreRendimientos.Calcular(politica.Value, new EntradaDeRetencion(perfil, natural, DepositClass.TermDeposit, x.Interes, diarios));
            return new TermDepositScheduleLineDto(x.Numero, x.Desde, x.Hasta, x.FechaDePago, x.Dias, x.TasaDelPeriodo, x.Interes, r.Valor, x.Interes - r.Valor);
        }).ToList();
        var huella = new HuellaDeCartera().Identificador("line", linea.PublicId).Decimal("amount", p.Monto, 2).Entero("termDays", p.PlazoDias)
            .Texto("periodicity", p.Periodicidad.ToString()).Texto("timing", p.Modalidad.ToString()).Fecha("start", p.Inicio).Decimal("tableRate", tasaDeTabla, 6)
            .Decimal("rate", aplicada, 6).Decimal("interest", plan.InteresTotal, 2).Calcular();
        var negociacionDto = negociada is null
            ? (version.AllowsNegotiatedRate ? new TermDepositNegotiationDto(true, null, null, null, false, null, "La línea admite tasa negociada.", null) : null)
            : new TermDepositNegotiationDto(negociada.Motor.CabeEnLaRegla, negociada.Regla?.PublicId, negociada.Regla?.Code, negociada.Motor.MargenMaximo,
                negociada.CabeEnAtribucion, negociada.Motor.CodigoDeRechazo, negociada.Motor.Explicacion, negociada.Nivel?.Code);
        var vista = new TermDepositSimulationDto(tasaDeTabla, aplicada, lineas, plan.InteresTotal,
            new TermDepositWithholdingEstimateDto(politica.Value.UmbralAplicaA(DepositClass.TermDeposit), lineas.Sum(l => l.WithholdingEstimate)), p.Inicio,
            plan.Vencimiento, plan.FechaDePagoAlVencimiento, plan.PlazoDeEmision, negociacionDto, huella);
        return Result.Success(new CdatCotizado(version, tasaDeTabla, tramo?.Id, negociada, plan, vista));
    }

    private static Result<CdatCotizado> Falla(Error e) => Result.Failure<CdatCotizado>(e);
}
