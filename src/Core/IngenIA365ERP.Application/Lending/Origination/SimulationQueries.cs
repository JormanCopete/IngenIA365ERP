using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Rates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

// Feature 013, US2 (T0389; FR-034, R40, T80; api.md §4.1): el simulador. Simular no registra nada —ni una clave de operación—: es una
// consulta. Una efectiva por encima de la usura no es error: la respuesta lo dice con usury.passes = false y el aviso; radicar, aprobar y
// desembolsar sí bloquean. (nuevo)

/// <summary>La usura de la simulación: la clave de la modalidad, la tasa certificada, la fecha, si pasa y la holgura (api.md §4.1). (nuevo)</summary>
public sealed record ScheduleUsuryDto(string? ModalityKey, decimal? Rate, DateOnly Date, bool Passes, decimal? Headroom, Guid? NationalValuePublicId);

/// <summary>Los totales del plan (api.md §4.1). (nuevo)</summary>
public sealed record ScheduleTotalsDto(decimal Principal, decimal Interest, decimal Insurance, decimal Other, decimal ToPay);

/// <summary>Una cuota del plan (api.md §4.1). (nuevo)</summary>
public sealed record ScheduleInstallmentDto(int Number, DateOnly DueDate, decimal Principal, decimal Interest, decimal Insurance, decimal Other,
    decimal Total, decimal BalanceAfter);

/// <summary>Un tramo de la explicación: período, días, tasa diaria y base (api.md §4.1). (nuevo)</summary>
public sealed record ScheduleExplanationDto(string Segment, int Days, decimal DailyRate, decimal Base);

/// <summary>Un aviso que no impide simular (api.md §1.9): hoy, la usura excedida. (nuevo)</summary>
public sealed record SimulationWarningDto(string Code, string Message);

/// <summary>
/// La simulación (api.md §4.1 <c>ScheduleSimulationDto</c>): versión de reglas, línea y versión, lo pedido, la efectiva pactada, la nominal del
/// período (informativa), la efectiva con cargos, la usura, los totales, el plan cuota a cuota, la explicación (tramos y frases), los avisos y
/// la huella. No lleva número ni código: no es un documento emitido (T80). (nuevo)
/// </summary>
public sealed record ScheduleSimulationDto(
    string RulesVersion,
    Guid CreditLinePublicId,
    string CreditLineCode,
    string CreditLineName,
    Guid CreditLineVersionPublicId,
    decimal Amount,
    int InstallmentCount,
    RepaymentPeriodicity Periodicity,
    AmortizationSystem AmortizationSystem,
    DateOnly DisbursementDate,
    int TermMonths,
    decimal EffectiveAnnualRate,
    decimal NominalPeriodRate,
    decimal EffectiveRateWithCharges,
    ScheduleUsuryDto Usury,
    ScheduleTotalsDto Totals,
    IReadOnlyList<ScheduleInstallmentDto> Installments,
    IReadOnlyList<ScheduleExplanationDto> Explanation,
    IReadOnlyList<string> ExplanationLines,
    IReadOnlyList<SimulationWarningDto> Warnings,
    string SimulationSha256)
{
    /// <summary>
    /// K6b (bloque 2; FR-024): en una línea en UVR, el plan en UVR y la UVR publicada del desembolso con que se valoraron en pesos las cuotas de
    /// arriba (una equivalencia, con su leyenda); nulo en pesos.
    /// </summary>
    public Housing.ScheduleUvrDto? Uvr { get; init; }

    /// <summary>K6b (bloque 2): ¿la línea es de vivienda (Ley 546)? La solicitud pregunta entonces si el inmueble es VIS.</summary>
    public bool IsHousing { get; init; }
}

/// <summary>La forma de la simulación (Principio VIII): lo que no necesita la base. (nuevo)</summary>
public sealed class ScheduleSimulationInputValidator : AbstractValidator<ScheduleSimulationInput>
{
    public ScheduleSimulationInputValidator()
    {
        RuleFor(x => x.CreditLinePublicId).NotEmpty().WithMessage("Elija la línea de crédito.");
        RuleFor(x => x.Amount).GreaterThan(0m).PrecisionScale(18, 2, true).WithMessage("El monto es mayor que cero y admite hasta 2 decimales.");
        RuleFor(x => x.Installments).GreaterThan(0).WithMessage("El número de cuotas es de al menos una.");
        RuleFor(x => x.Periodicity).IsInEnum().When(x => x.Periodicity is not null);
        RuleFor(x => x.AmortizationSystem).IsInEnum().When(x => x.AmortizationSystem is not null);
        RuleFor(x => x).Must(x => x.FirstDueDate is null || x.DisbursementDate is null || x.FirstDueDate > x.DisbursementDate)
            .WithName("firstDueDate").WithMessage("El primer vencimiento va después del desembolso.");
        RuleForEach(x => x.Extraordinary).ChildRules(e =>
        {
            e.RuleFor(y => y.Amount).GreaterThan(0m).PrecisionScale(18, 2, true).WithMessage("Una cuota extraordinaria vale más que cero.");
            e.RuleFor(y => y.Months).NotEmpty().WithMessage("Indique en qué meses se paga la cuota extraordinaria.");
            e.RuleForEach(y => y.Months).GreaterThan(0).WithMessage("Los meses de una extraordinaria cuentan desde 1.");
        });
        RuleFor(x => x.Grace!.Kind).IsInEnum().When(x => x.Grace is not null);
        RuleFor(x => x.Grace!.Periods).GreaterThanOrEqualTo(0).When(x => x.Grace is not null);
        RuleForEach(x => x.Insurances).NotEmpty().MaximumLength(20);
    }
}

/// <summary>Lo que comparten la simulación, su PDF y su envío: la línea en alcance, la persona y el simulador. (nuevo)</summary>
public sealed class SimulacionEnAlcance(
    IApplicationDbContext db,
    SimuladorDeCredito simulador,
    IAlcanceDeCartera alcanceDeLaPeticion,
    LendingAuditEmitter auditoria)
{
    /// <summary>Simula con la línea del alcance de quien pide (fuera, el 404 de la línea) y la persona, si viene, para la banda.</summary>
    public async Task<Result<(SimulacionCalculada Calculo, CreditLine Linea, Person? Persona)>> SimularAsync(ScheduleSimulationInput entrada, DateOnly hoy,
        CancellationToken ct)
    {
        var linea = await ConsultasDeLineas.LineaEnAlcanceAsync(db, alcanceDeLaPeticion, auditoria, entrada.CreditLinePublicId, seguir: false, ct);
        if (linea.IsFailure) return Result.Failure<(SimulacionCalculada, CreditLine, Person?)>(linea.Error);

        Person? persona = null;
        var perfil = PerfilParaLaBanda.Ninguno;
        if (entrada.PersonPublicId is { } personaPublica)
        {
            persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == personaPublica && !p.IsDeleted, ct);
            if (persona is null) return Result.Failure<(SimulacionCalculada, CreditLine, Person?)>(ErroresDeGobierno.PersonaInexistente());
            perfil = await PersonaDeOriginacion.PerfilAsync(db, persona, entrada.DisbursementDate ?? hoy, ct);
        }

        var calculo = await simulador.SimularAsync(linea.Value, entrada, perfil, hoy, ct);
        return calculo.IsFailure
            ? Result.Failure<(SimulacionCalculada, CreditLine, Person?)>(calculo.Error)
            : Result.Success((calculo.Value, linea.Value, persona));
    }

    /// <summary>La simulación como la devuelve <c>/simulations/schedule</c>.</summary>
    public static ScheduleSimulationDto Dto(SimulacionCalculada s, CreditLine linea)
    {
        var plan = s.Plan;
        var u = s.Usura;
        var avisos = new List<SimulationWarningDto>();
        if (!u.Pasa)
            avisos.Add(new SimulationWarningDto(ErroresDeOriginacion.CodigoUsuraExcedida,
                $"La tasa efectiva con cargos ({Porcentaje(u.EfectivaConCargos)} EA) supera la usura vigente de {s.Combinacion.ModalityCode} " +
                $"({Porcentaje(u.Usura ?? 0m)} EA). Puede simular, pero así no se radica: ajuste el monto, el plazo o los seguros."));
        return new ScheduleSimulationDto(
            plan.RulesVersion,
            linea.PublicId,
            linea.Code,
            linea.Name,
            s.Version.Version.PublicId,
            s.Entrada.Monto,
            plan.Cuotas.Count,
            s.Entrada.Periodicidad,
            s.Entrada.Sistema,
            s.Fecha,
            s.PlazoMeses,
            s.TasaPactada,
            ErroresDeLineasDeCredito.Tasa(plan.TasaNominalDelPeriodo),
            ErroresDeLineasDeCredito.Tasa(u.EfectivaConCargos),
            new ScheduleUsuryDto(u.ClaveDeUsura, u.Usura, s.Fecha, u.Pasa, u.Holgura is { } h ? ErroresDeLineasDeCredito.Tasa(h) : null, u.UsuraPublicId),
            new ScheduleTotalsDto(plan.TotalCapital, plan.TotalInteres, plan.TotalSeguro, 0m, plan.TotalAPagar),
            plan.Cuotas.Select(c => new ScheduleInstallmentDto(c.Numero, c.Vencimiento, c.Capital, c.Interes, c.Seguro, 0m, c.Total, c.SaldoFinal)).ToList(),
            plan.Explicacion.Tramos
                .Select(t => new ScheduleExplanationDto($"{ExplicacionDeCartera.Fecha(t.Desde)}..{ExplicacionDeCartera.Fecha(t.Hasta)}", t.Dias, t.TasaDiaria, t.Base))
                .ToList(),
            plan.Explicacion.Lineas.Concat(u.Explicacion.Lineas).ToList(),
            avisos,
            s.Huella)
        {
            Uvr = Housing.ScheduleUvrDto.De(s.PlanEnUvr, s.ValorUvr, s.Fecha),
            IsHousing = Housing.ValidacionDeVivienda.EsVivienda(s),
        };
    }

    private static string Porcentaje(decimal fraccion) =>
        (fraccion * 100m).ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("es-CO")) + " %";
}

/// <summary>
/// Simula un plan de pagos (api.md §4.1, <c>POST /api/lending/simulations/schedule</c>, <c>Lending.Simulations.Run</c>, sin clave): plan cuota a
/// cuota con capital, interés, seguros, otros y saldo, la efectiva, la nominal y la efectiva con cargos, los totales, la usura y la explicación,
/// con la versión de reglas y la huella. No escribe nada. Línea fuera del alcance: el 404 de la línea. (nuevo)
/// </summary>
public sealed record SimulateRepaymentScheduleQuery(ScheduleSimulationInput Simulation) : IRequest<Result<ScheduleSimulationDto>>;

public sealed class SimulateRepaymentScheduleQueryValidator : AbstractValidator<SimulateRepaymentScheduleQuery>
{
    public SimulateRepaymentScheduleQueryValidator()
    {
        RuleFor(x => x.Simulation).NotNull().SetValidator(new ScheduleSimulationInputValidator());
    }
}

public sealed class SimulateRepaymentScheduleQueryHandler(SimulacionEnAlcance simulacion, IDateTimeService reloj)
    : IRequestHandler<SimulateRepaymentScheduleQuery, Result<ScheduleSimulationDto>>
{
    public async Task<Result<ScheduleSimulationDto>> Handle(SimulateRepaymentScheduleQuery request, CancellationToken ct)
    {
        var r = await simulacion.SimularAsync(request.Simulation, reloj.HoyLocal, ct);
        return r.IsFailure
            ? Result.Failure<ScheduleSimulationDto>(r.Error)
            : Result.Success(SimulacionEnAlcance.Dto(r.Value.Calculo, r.Value.Linea));
    }
}

/// <summary>
/// Lo que se imprime de una simulación (T80): la simulación, la cooperativa, el nombre de la persona si se simuló para alguien, la leyenda
/// «Simulación – no constituye aprobación» y la marca «AMBIENTE DE PRUEBAS – SIN VALIDEZ» fuera de producción. No lleva número de radicado ni
/// código de verificación: no es un documento emitido. (nuevo)
/// </summary>
public sealed record SimulacionParaImprimir(ScheduleSimulationDto Simulacion, string Cooperativa, string? Persona, DateOnly Fecha, bool AmbienteDePruebas)
{
    public const string Leyenda = "Simulación – no constituye aprobación";
    public const string MarcaDePruebas = "AMBIENTE DE PRUEBAS – SIN VALIDEZ";
}

/// <summary>
/// Pinta la simulación en PDF. La implementación con QuestPDF (<c>SimulacionDeCreditoReport</c>) la registra la API, la única capa que conoce
/// la librería. No calcula nada. (nuevo)
/// </summary>
public interface IRenderizadorDeSimulacion
{
    byte[] Render(SimulacionParaImprimir simulacion);
}

/// <summary>El PDF de una simulación: contenido y nombre de archivo (sin número: <c>simulacion-{línea}-{fecha}.pdf</c>). (nuevo)</summary>
public sealed record SimulacionEnPdfDto(byte[] Contenido, string NombreArchivo);

/// <summary>Cómo se arma y se nombra el PDF de una simulación. (nuevo)</summary>
public static class SimulacionesEnPdf
{
    public static string NombreDe(ScheduleSimulationDto s) =>
        $"simulacion-{s.CreditLineCode.ToLowerInvariant()}-{s.DisbursementDate:yyyyMMdd}.pdf";

    public static SimulacionParaImprimir Armar(ScheduleSimulationDto s, Person? persona, ICurrentTenantService? cooperativa,
        IAmbienteDeDespliegue? ambiente, DateOnly hoy) =>
        new(s, cooperativa?.TenantName ?? "Cooperativa", persona is null ? null : ReferenciasDeGobierno.Nombre(persona), hoy, ambiente?.EsProduccion != true);
}

/// <summary>
/// El PDF de la simulación (api.md §4.1, <c>POST /api/lending/simulations/schedule/pdf</c>, <c>Lending.Simulations.Run</c>, sin clave): el mismo
/// cuerpo y las mismas reglas que <see cref="SimulateRepaymentScheduleQuery"/>, pintado por <see cref="IRenderizadorDeSimulacion"/>. No escribe
/// nada. Sin ambiente declarado se marca como de pruebas (el defecto que no da validez a un papel). (nuevo)
/// </summary>
public sealed record RenderScheduleSimulationPdfQuery(ScheduleSimulationInput Simulation) : IRequest<Result<SimulacionEnPdfDto>>;

public sealed class RenderScheduleSimulationPdfQueryValidator : AbstractValidator<RenderScheduleSimulationPdfQuery>
{
    public RenderScheduleSimulationPdfQueryValidator()
    {
        RuleFor(x => x.Simulation).NotNull().SetValidator(new ScheduleSimulationInputValidator());
    }
}

public sealed class RenderScheduleSimulationPdfQueryHandler(
    SimulacionEnAlcance simulacion,
    IRenderizadorDeSimulacion renderizador,
    IDateTimeService reloj,
    ICurrentTenantService? cooperativa = null,
    IAmbienteDeDespliegue? ambiente = null) : IRequestHandler<RenderScheduleSimulationPdfQuery, Result<SimulacionEnPdfDto>>
{
    public async Task<Result<SimulacionEnPdfDto>> Handle(RenderScheduleSimulationPdfQuery request, CancellationToken ct)
    {
        var r = await simulacion.SimularAsync(request.Simulation, reloj.HoyLocal, ct);
        if (r.IsFailure) return Result.Failure<SimulacionEnPdfDto>(r.Error);
        var dto = SimulacionEnAlcance.Dto(r.Value.Calculo, r.Value.Linea);
        var pdf = renderizador.Render(SimulacionesEnPdf.Armar(dto, r.Value.Persona, cooperativa, ambiente, reloj.HoyLocal));
        return Result.Success(new SimulacionEnPdfDto(pdf, SimulacionesEnPdf.NombreDe(dto)));
    }
}
