using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.PersonStatement;

/// <summary>
/// Lo que costaría cancelar un crédito a la fecha simulada (2026-11-16), por concepto: capital, interés corriente (con sus suspensos), interés de mora
/// (con sus suspensos), seguro, otros (gastos de cobranza y demás cargos), el anticipo que se descuenta y el total. Con
/// <c>Pagos.InteresesAlPagarAntesDelVencimiento</c> en «CuotaCompleta», <see cref="InteresNoCausadoDeLaCuotaEnCurso"/> es lo que falta por causar del
/// interés de la cuota en curso y ya va sumado al interés corriente y al total; con «Corrido», nulo. (nuevo)
/// </summary>
public sealed record CreditoDeLaSimulacionDto(Guid LoanPublicId, string Numero, string Linea, decimal Capital, decimal InteresCorriente,
    decimal InteresDeMora, decimal Seguro, decimal Otros, decimal Anticipo, decimal Total, decimal? InteresNoCausadoDeLaCuotaEnCurso,
    IReadOnlyList<string> Avisos);

/// <summary>
/// La simulación de cancelación de unos créditos de la persona a una fecha (2026-11-16): la fecha, cómo se cobró el interés de la cuota en curso
/// (<c>Corrido</c>, <c>CuotaCompleta</c> o <c>Mixto</c> si las líneas de los créditos elegidos no coinciden), cada crédito, el total, el membrete y la
/// persona (para imprimirla). (nuevo)
/// </summary>
public sealed record SimulacionDeCancelacionDto(DateOnly Fecha, string ModoDeIntereses, IReadOnlyList<CreditoDeLaSimulacionDto> Creditos, decimal Total,
    MembreteDto? Membrete, PersonaDelEstadoDeCuentaDto Persona);

/// <summary>
/// <c>POST /api/lending/person-statement/payoff-simulation</c> (2026-11-16, pedido del dueño; <c>Lending.Payoffs.Read</c>): lo que costaría cancelar los
/// créditos elegidos —de la persona como deudora— en una fecha de hoy en adelante. No calcula por su cuenta: cada crédito sale de su liquidación
/// (<see cref="GetLoanSettlementQuery"/>, que ya proyecta a una fecha futura sin pagos y asegura el alcance) y, si
/// <c>Pagos.InteresesAlPagarAntesDelVencimiento</c> (de la línea, con caída al general) es «CuotaCompleta», le suma lo que falta por causar del interés de
/// la cuota en curso: el interés del plan de esa cuota (<see cref="GetLoanStatementQuery"/>) menos lo causado hasta la fecha (los tramos de causación de
/// la liquidación). Simular no escribe ni cambia cómo se aplican los pagos (<c>AplicadorDePagos</c> sigue con el corrido). (nuevo)
/// </summary>
public sealed record SimulatePersonPayoffQuery(Guid PersonPublicId, IReadOnlyList<Guid> Loans, DateOnly Date) : IRequest<Result<SimulacionDeCancelacionDto>>;

public sealed class SimulatePersonPayoffQueryValidator : AbstractValidator<SimulatePersonPayoffQuery>
{
    public SimulatePersonPayoffQueryValidator(IDateTimeService reloj)
    {
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Indique la persona.");
        RuleFor(x => x.Loans).NotEmpty().WithMessage("Elija al menos un crédito para simular.")
            .Must(l => l is null || l.Count <= SimulatePersonPayoffQueryHandler.MaximoDeCreditos)
            .WithMessage($"Se simulan a lo sumo {SimulatePersonPayoffQueryHandler.MaximoDeCreditos} créditos a la vez.");
        RuleFor(x => x.Date).Must(d => d >= reloj.HoyLocal).WithMessage("La fecha de pago es hoy o una fecha futura.");
    }
}

public sealed class SimulatePersonPayoffQueryHandler(
    IApplicationDbContext db,
    ISender sender,
    ReglasDeParametrosDeCartera parametros,
    IDateTimeService reloj,
    IBlobStore store,
    IAttachmentCipher? cifrado = null) : IRequestHandler<SimulatePersonPayoffQuery, Result<SimulacionDeCancelacionDto>>
{
    public const int MaximoDeCreditos = 50;

    /// <summary>El modo que se informa cuando las líneas de los créditos elegidos no cobran igual.</summary>
    public const string ModoMixto = "Mixto";

    public const string CodigoCreditoAjeno = "Lending.PersonStatement.LoanNotOfPerson";
    public const string CodigoFechaPasada = "Lending.PersonStatement.DateInPast";

    public async Task<Result<SimulacionDeCancelacionDto>> Handle(SimulatePersonPayoffQuery request, CancellationToken ct)
    {
        // Las mismas reglas del validador, por si la consulta llega sin la tubería (una prueba, otro handler).
        if (request.Date < reloj.HoyLocal)
            return Result.Failure<SimulacionDeCancelacionDto>(new Error(CodigoFechaPasada, "La fecha de pago es hoy o una fecha futura."));
        var pedidos = (request.Loans ?? []).Distinct().ToList();
        if (pedidos.Count is 0 or > MaximoDeCreditos)
            return Result.Failure<SimulacionDeCancelacionDto>(new Error("Lending.PersonStatement.LoansInvalid",
                $"Elija entre uno y {MaximoDeCreditos} créditos para simular."));

        var persona = await LecturasDelEstadoDeCuenta.PersonaAsync(db, request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<SimulacionDeCancelacionDto>(LecturasDelEstadoDeCuenta.PersonaInexistente());

        // Sus créditos como deudora, con el alcance de quien simula: uno que no esté (de otra persona, como codeudora o fuera del alcance) no se simula.
        var lista = await sender.Send(new ListLoansQuery(PersonPublicId: request.PersonPublicId, Role: "Debtor", PageSize: 200), ct);
        if (lista.IsFailure) return Result.Failure<SimulacionDeCancelacionDto>(lista.Error);
        var propios = lista.Value.Items.ToDictionary(c => c.PublicId);
        if (pedidos.Any(id => !propios.ContainsKey(id)))
            return Result.Failure<SimulacionDeCancelacionDto>(new Error(CodigoCreditoAjeno,
                $"Uno de los créditos elegidos no es de {persona.Nombre} como deudora: sólo se simulan sus propios créditos."));

        var lineas = await db.Loans.AsNoTracking().Where(l => pedidos.Contains(l.PublicId)).ToDictionaryAsync(l => l.PublicId, l => l.CreditLineId, ct);
        var fecha = request.Date;
        var creditos = new List<CreditoDeLaSimulacionDto>();
        var modos = new List<string>();
        foreach (var id in pedidos)
        {
            var credito = propios[id];
            var leida = await sender.Send(new GetLoanSettlementQuery(id, fecha), ct);
            if (leida.IsFailure) return Result.Failure<SimulacionDeCancelacionDto>(leida.Error);
            var s = leida.Value;
            var modo = await ModoAsync(fecha, lineas.TryGetValue(id, out var linea) ? linea : null, ct);
            modos.Add(modo);

            decimal Concepto(params LoanConcept[] conceptos) => conceptos.Sum(c => s.ByConcept.GetValueOrDefault(c.ToString()));
            var avisos = s.Warnings.ToList();
            decimal? noCausado = null;
            if (modo == ParametrosDeCartera.InteresesDeLaCuotaCompleta)
                noCausado = await InteresNoCausadoAsync(credito, s, fecha, avisos, ct);

            creditos.Add(new CreditoDeLaSimulacionDto(id, credito.Number, $"{credito.Line.Code} · {credito.Line.Name}",
                Concepto(LoanConcept.Principal),
                Concepto(LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense) + (noCausado ?? 0m),
                Concepto(LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense),
                Concepto(LoanConcept.Insurance),
                Concepto(LoanConcept.CollectionFee, LoanConcept.OtherCharge),
                s.Credits.GetValueOrDefault(nameof(LoanConcept.Advance)),
                s.ToPayOff + (noCausado ?? 0m),
                noCausado,
                avisos));
        }

        var membrete = await LecturasDelEstadoDeCuenta.MembreteAsync(db, store, cifrado, ct);
        var distintos = modos.Distinct(StringComparer.Ordinal).ToList();
        return Result.Success(new SimulacionDeCancelacionDto(fecha, distintos.Count == 1 ? distintos[0] : ModoMixto, creditos, creditos.Sum(c => c.Total),
            membrete, persona));
    }

    /// <summary><c>Pagos.InteresesAlPagarAntesDelVencimiento</c> a la fecha: el de la línea si lo tiene, si no el general; ilegible = «Corrido».</summary>
    private async Task<string> ModoAsync(DateOnly fecha, int? linea, CancellationToken ct)
    {
        var leido = linea is { } l
            ? await parametros.LeerAsync(ParametrosDeCartera.PagosInteresesAlPagarAntesDelVencimiento, fecha, null, ParameterScopeKind.CreditLine, l, ct)
            : await parametros.LeerAsync(ParametrosDeCartera.PagosInteresesAlPagarAntesDelVencimiento, fecha, ct: ct);
        return leido.IsSuccess && string.Equals(leido.Value.Texto?.Trim(), ParametrosDeCartera.InteresesDeLaCuotaCompleta, StringComparison.Ordinal)
            ? ParametrosDeCartera.InteresesDeLaCuotaCompleta
            : ParametrosDeCartera.InteresesCorridos;
    }

    /// <summary>
    /// Lo que falta por causar del interés de la cuota en curso a la fecha: la primera cuota por vencer con vencimiento desde la fecha, su interés del plan
    /// menos lo causado hasta la fecha para esa cuota (los tramos de interés corriente de la liquidación), nunca negativo. Sin cuota en curso, cero.
    /// </summary>
    private async Task<decimal> InteresNoCausadoAsync(LoanListItemDto credito, LoanSettlementDto liquidacion, DateOnly fecha, List<string> avisos,
        CancellationToken ct)
    {
        var estado = await sender.Send(new GetLoanStatementQuery(credito.PublicId, fecha), ct);
        if (estado.IsFailure)
        {
            avisos.Add($"No se pudo leer el plan del crédito para cobrar la cuota completa ({estado.Error.Message}): se cobra el interés corrido.");
            return 0m;
        }
        var enCurso = estado.Value.Installments.Where(q => q.DueDate >= fecha && q.Status == EstadosDeCuota.PorVencer).OrderBy(q => q.DueDate)
            .FirstOrDefault();
        if (enCurso is null) return 0m;
        var causado = liquidacion.Explanation
            .Where(t => t.Installment == enCurso.Number
                        && t.Concept is nameof(LoanConcept.CurrentInterest) or nameof(LoanConcept.CurrentInterestInSuspense))
            .Sum(t => t.Amount);
        var falta = decimal.Round(enCurso.Interest - causado, 2, MidpointRounding.AwayFromZero);
        if (falta <= 0m) return 0m;
        avisos.Add($"Se cobra el interés completo de la cuota {enCurso.Number} (vence el {enCurso.DueDate:dd/MM/yyyy}) aunque se pague antes.");
        return falta;
    }
}
