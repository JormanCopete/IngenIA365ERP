using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Lending.PersonStatement;

/// <summary>
/// Lo que costaría cancelar un crédito a la fecha simulada (2026-11-16), por concepto: capital, interés corriente (con sus suspensos), interés de mora
/// (con sus suspensos), seguro, otros (gastos de cobranza y demás cargos), el anticipo que se descuenta y el total. Cuando la cancelación cobra la cuota
/// completa (<c>Pagos.InteresesAlPagarAntesDelVencimiento</c> en «CuotaCompleta» y el crédito fuera del tope legal del prepago, no de vivienda),
/// <see cref="InteresNoCausadoDeLaCuotaEnCurso"/> es lo que falta por causar del interés de la cuota en curso y ya va sumado al interés corriente y al
/// total; con el corrido, nulo. (nuevo)
/// </summary>
public sealed record CreditoDeLaSimulacionDto(Guid LoanPublicId, string Numero, string Linea, decimal Capital, decimal InteresCorriente,
    decimal InteresDeMora, decimal Seguro, decimal Otros, decimal Anticipo, decimal Total, decimal? InteresNoCausadoDeLaCuotaEnCurso,
    IReadOnlyList<string> Avisos)
{
    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 2): la administración, aparte del seguro y de los otros. (nuevo)</summary>
    public decimal Administracion { get; init; }
}

/// <summary>
/// La simulación de cancelación de unos créditos de la persona a una fecha (2026-11-16): la fecha, cómo se cobró el interés de la cuota en curso
/// (<c>Corrido</c>, <c>CuotaCompleta</c> o <c>Mixto</c> si los créditos elegidos no cobran igual), cada crédito, el total, el membrete y la persona (para
/// imprimirla). (nuevo)
/// </summary>
public sealed record SimulacionDeCancelacionDto(DateOnly Fecha, string ModoDeIntereses, IReadOnlyList<CreditoDeLaSimulacionDto> Creditos, decimal Total,
    MembreteDto? Membrete, PersonaDelEstadoDeCuentaDto Persona);

/// <summary>
/// <c>POST /api/lending/person-statement/payoff-simulation</c> (2026-11-16, pedido del dueño; <c>Lending.Payoffs.Read</c>): lo que costaría cancelar los
/// créditos elegidos —de la persona como deudora— en una fecha de hoy en adelante. No calcula por su cuenta: cada crédito sale de su liquidación
/// (<see cref="GetLoanSettlementQuery"/>, que ya proyecta a una fecha futura sin pagos, asegura el alcance y, desde que el motor cobra la cuota completa,
/// dice cómo cobraría una cancelación el interés de la cuota en curso —<c>InteresesAlCancelar</c>: el parámetro de la línea con caída al general, el
/// corrido en vivienda y dentro del tope legal— y cuánto falta por causar de él, con la misma proyección que usa la caja). Así la simulación muestra
/// exactamente lo que cobrará el <c>FullPayoff</c>. Simular no escribe. (nuevo)
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

        var fecha = request.Date;
        var creditos = new List<CreditoDeLaSimulacionDto>();
        var modos = new List<string>();
        foreach (var id in pedidos)
        {
            var credito = propios[id];
            var leida = await sender.Send(new GetLoanSettlementQuery(id, fecha), ct);
            if (leida.IsFailure) return Result.Failure<SimulacionDeCancelacionDto>(leida.Error);
            var s = leida.Value;
            // 2026-11-16: la liquidación ya dice cómo cobra la cancelación la cuota en curso y cuánto falta por causar de ella (lo mismo que la caja).
            modos.Add(s.InterestBeforeDueDate);
            decimal? noCausado = s.InterestBeforeDueDate == ParametrosDeCartera.InteresesDeLaCuotaCompleta ? s.CurrentInstallmentInterestToAccrue ?? 0m : null;

            decimal Concepto(params LoanConcept[] conceptos) => conceptos.Sum(c => s.ByConcept.GetValueOrDefault(c.ToString()));
            creditos.Add(new CreditoDeLaSimulacionDto(id, credito.Number, $"{credito.Line.Code} · {credito.Line.Name}",
                Concepto(LoanConcept.Principal),
                Concepto(LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense) + (noCausado ?? 0m),
                Concepto(LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense),
                Concepto(LoanConcept.Insurance),
                Concepto(LoanConcept.CollectionFee, LoanConcept.OtherCharge),
                s.Credits.GetValueOrDefault(nameof(LoanConcept.Advance)),
                s.ToPayOffWithCurrentInstallment ?? s.ToPayOff,
                noCausado,
                s.Warnings) { Administracion = Concepto(LoanConcept.Administration) });
        }

        var membrete = await LecturasDelEstadoDeCuenta.MembreteAsync(db, store, cifrado, ct);
        var distintos = modos.Distinct(StringComparer.Ordinal).ToList();
        return Result.Success(new SimulacionDeCancelacionDto(fecha, distintos.Count == 1 ? distintos[0] : ModoMixto, creditos, creditos.Sum(c => c.Total),
            membrete, persona));
    }
}
