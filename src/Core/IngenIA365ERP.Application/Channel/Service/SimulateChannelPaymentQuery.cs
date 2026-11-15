using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Channel;
using MediatR;

namespace IngenIA365ERP.Application.Channel.Service;

// Feature 021, US3 (T079; research R7; contracts/erp-api-de-servicio.md): lo que pasaría si el asociado paga ese valor en ese concepto.
// Envuelve las simulaciones de los contratos —la de obligaciones, con las dos alternativas del abono a capital; la de cuentas— y no
// escribe nada. Sólo simula sobre un concepto publicado de la persona y con una modalidad que el concepto admite.

/// <summary>El cuerpo: el concepto, la modalidad del canal (<c>Due</c>, <c>Partial</c>, <c>Prepayment</c>, <c>Payoff</c>) y el valor.</summary>
public sealed record SimulateChannelPaymentQuery(Guid PersonPublicId, string ConceptKey, string Modality, decimal Amount, string? PrepaymentChoice = null)
    : IRequest<Result<ChannelSimulationDto>>;

public sealed record ChannelSimulationDto(
    string ConceptKey,
    string Modality,
    decimal Amount,
    bool Allowed,
    decimal Surplus,
    IReadOnlyList<DistribucionSimuladaDto> Distribution,
    SaldosSimuladosDto BalancesAfter,
    PlanSimuladoDto? NewPlan,
    string? PrepaymentChoice,
    IReadOnlyList<AlternativaDeAbonoDto> Alternatives,
    IReadOnlyList<string> Notices);

/// <summary>Una parte de la distribución: el concepto por nombre (<c>Principal</c>, <c>CurrentInterest</c>…, o <c>Installment</c> en cuentas).</summary>
public sealed record DistribucionSimuladaDto(string Concept, int? InstallmentNumber, string? Period, decimal Amount);

/// <summary>Los saldos después: en créditos, capital, vencido y lo que cancela; en cuentas, el saldo.</summary>
public sealed record SaldosSimuladosDto(decimal? Principal, decimal? PastDue, decimal? PayoffAmount, decimal? Balance);

public sealed record PlanSimuladoDto(decimal Installment, int RemainingInstallments, DateOnly? NextDueDate);

/// <summary>Una alternativa del abono a capital: reducir plazo o reducir cuota, con su plan.</summary>
public sealed record AlternativaDeAbonoDto(string Choice, PlanSimuladoDto? NewPlan, decimal Surplus);

public sealed class SimulateChannelPaymentQueryHandler(
    IApplicationDbContext db,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    IConsultaDeObligaciones consulta,
    IDateTimeService reloj,
    AuditoriaDelCanal auditoria,
    IngenIA365ERP.Application.Common.Integration.Accounting.IContabilidadParaRecaudo? contabilidad = null) : IRequestHandler<SimulateChannelPaymentQuery, Result<ChannelSimulationDto>>
{
    public const string ConceptoNoEncontrado = "Channel.Payable.NotFound";
    public const string ModalidadNoAdmitida = "Channel.Payable.ModalityNotAllowed";
    public const string ValorInvalido = "Channel.Payable.AmountInvalid";
    public const string Rechazada = "Channel.Payable.SimulationRejected";

    public async Task<Result<ChannelSimulationDto>> Handle(SimulateChannelPaymentQuery request, CancellationToken ct)
    {
        var persona = await PersonaDelCanal.HabilitadaAsync(db, request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<ChannelSimulationDto>(PersonaDelCanal.ErrorNoHabilitada);
        var hoy = reloj.HoyLocal;

        // El concepto tiene que ser uno de los que la persona ve hoy: el mismo listado que muestra el canal.
        var (items, _) = await new GetChannelPayablesQueryHandler(db, obligaciones, cuentas, consulta, reloj, auditoria, contabilidad).ListarAsync(persona.PublicId, hoy, ct);
        var concepto = items.FirstOrDefault(i => i.ConceptKey == request.ConceptKey);
        if (concepto is null || ClaveDeConcepto.Leer(request.ConceptKey) is not { } clave)
            return Result.Failure<ChannelSimulationDto>(new Error(ConceptoNoEncontrado, "Ese concepto no está disponible en el canal."));
        if (!ModosDelCanal.Admite(concepto.Modes, request.Modality))
            return Result.Failure<ChannelSimulationDto>(new Error(ModalidadNoAdmitida, "Ese concepto no admite esa forma de pago en el canal."));
        if (request.Amount <= 0m || decimal.Round(request.Amount, 0) != request.Amount)
            return Result.Failure<ChannelSimulationDto>(new Error(ValorInvalido, "El valor debe ser un número entero de pesos mayor que cero."));
        if (request.Modality == ModosDelCanal.Partial && request.Amount < concepto.Modes.Partial!.Minimum)
            return Result.Failure<ChannelSimulationDto>(new ErrorConDatos(ValorInvalido,
                $"El valor mínimo de un pago parcial es {concepto.Modes.Partial.Minimum:N0}.", new { minimum = concepto.Modes.Partial.Minimum }));

        if (clave.EsFactura)
        {
            // Feature 021, C2b: una factura no tiene contrato que simular; se cobra hasta su saldo y lo demás quedaría a favor del asociado.
            var aplicado = Math.Min(request.Amount, concepto.DueToday);
            return Result.Success(new ChannelSimulationDto(request.ConceptKey, request.Modality, request.Amount, true, request.Amount - aplicado,
                [new DistribucionSimuladaDto("Invoice", null, concepto.ProductNumber, aplicado)], new SaldosSimuladosDto(null, null, null, concepto.DueToday - aplicado),
                null, null, [], []));
        }
        return clave.EsCredito
            ? await CreditoAsync(request, clave.Producto, hoy, concepto, ct)
            : await CuentaAsync(request, clave.Producto, hoy, ct);
    }

    private async Task<Result<ChannelSimulationDto>> CreditoAsync(SimulateChannelPaymentQuery request, Guid obligacion, DateOnly hoy, ChannelPayableDto concepto,
        CancellationToken ct)
    {
        EleccionDeAbono? eleccion = null;
        if (request.Modality == ModosDelCanal.Prepayment)
        {
            var pedida = request.PrepaymentChoice ?? concepto.Modes.Prepayment!.DefaultChoice;
            if (!Enum.TryParse<EleccionDeAbono>(pedida, ignoreCase: false, out var e) || !Enum.IsDefined(e))
                return Result.Failure<ChannelSimulationDto>(new Error(ModalidadNoAdmitida, "La opción de abono debe ser ReduceTerm o ReduceInstallment."));
            eleccion = e;
        }
        var r = await obligaciones.SimularAsync(new SolicitudDeSimulacion(ClavesDeRecaudo.Recaudador, obligacion, ModosDelCanal.DeCredito(request.Modality)!.Value,
            request.Amount, hoy, eleccion), ct);
        if (r.Result == ResultadoDelContrato.Rejected || r.Value is null)
            return Result.Failure<ChannelSimulationDto>(new Error(Rechazada, string.IsNullOrWhiteSpace(r.Message) ? "No se pudo simular el pago." : r.Message));
        var s = r.Value;
        return Result.Success(new ChannelSimulationDto(
            request.ConceptKey,
            request.Modality,
            request.Amount,
            true,
            s.Surplus + s.CreditBalanceAmount,
            s.Distribution.Select(d => new DistribucionSimuladaDto(d.Concept.ToString(), d.InstallmentNumber, null, d.Amount)).ToList(),
            new SaldosSimuladosDto(s.BalancesAfter.Principal, s.BalancesAfter.PastDue, s.BalancesAfter.PayoffAmount, null),
            Plan(s.NewPlan, hoy),
            s.Choice?.ToString(),
            s.Alternatives.Where(a => a.Choice is not null)
                .Select(a => new AlternativaDeAbonoDto(a.Choice!.Value.ToString(), Plan(a.NewPlan, hoy), a.Surplus + a.CreditBalanceAmount)).ToList(),
            s.Notices));
    }

    private async Task<Result<ChannelSimulationDto>> CuentaAsync(SimulateChannelPaymentQuery request, Guid cuenta, DateOnly hoy, CancellationToken ct)
    {
        var r = await cuentas.SimularAsync(new SolicitudDeSimulacionDeCuenta(ClavesDeRecaudo.Recaudador, "Deposit", cuenta, request.Amount, hoy,
            ClaseDeMedioDeRecaudo.Transfer, [], null, ModosDelCanal.DeCuenta(request.Modality)), ct);
        if (r.Result == ResultadoDelContrato.Rejected || r.Value is null)
            return Result.Failure<ChannelSimulationDto>(new Error(Rechazada, string.IsNullOrWhiteSpace(r.Message) ? "No se pudo simular el pago." : r.Message));
        var s = r.Value;
        return Result.Success(new ChannelSimulationDto(
            request.ConceptKey,
            request.Modality,
            request.Amount,
            s.Allowed,
            0m,
            s.InstallmentsCovered.Select(d => new DistribucionSimuladaDto("Installment", null, d.Period, d.Amount)).ToList(),
            new SaldosSimuladosDto(null, null, null, s.AvailableAfter.Balance),
            null,
            null,
            [],
            s.Notices));
    }

    /// <summary>El plan después del pago: cuota, cuotas que quedan (las que vencen después de hoy) y próxima fecha.</summary>
    private static PlanSimuladoDto? Plan(PlanResumidoDto? plan, DateOnly hoy) =>
        plan is null ? null : new PlanSimuladoDto(plan.InstallmentAmount, plan.Installments.Count(c => c.DueDate > hoy), plan.NextDueDate);
}
