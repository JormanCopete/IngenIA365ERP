using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Assembly;

// Feature 014, US12 (T203; contracts/api.md §12; FR-045 a FR-047, D13; research §22): registrar en borrador, descartar, aplicar con el acta cerrada y
// otra persona, y reversar entera la revalorización de aportes, la distribución de excedentes o la amortización de aportes que decide la asamblea. (nuevo)

// ----------------------------------------------------------------------------------------------------------- registrar --

/// <summary>
/// <c>POST /api/lending/assembly-distributions</c> (<c>RegisterAssemblyDistributionCommand</c>): registra en borrador lo decidido —clase, ejercicio, base
/// (sin ella, <c>Asamblea.BaseDeRevalorizacion</c>), porcentaje <b>o</b> valor, fondo, destino de los excedentes (sin él, <c>Asamblea.DestinoDeExcedentes</c>)
/// y la línea de ahorro si van a ahorro—, o la corrección de una reversada (<paramref name="CorrectsPublicId"/>: misma clase y ejercicio, hereda su acta).
/// Quien registra no aplica (segregación). (nuevo)
/// </summary>
public sealed record RegisterAssemblyDistributionCommand(
    AssemblyDistributionKind Kind,
    int Year,
    decimal FundAmount,
    decimal? Percent,
    decimal? Value,
    RevaluationBase? Base,
    SurplusDestination? Destination,
    string? TargetDepositLineCode,
    Guid? CorrectsPublicId,
    string Reason) : IRequest<Result<AssemblyDistributionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterAssemblyDistributionCommandValidator : ValidadorConMotivo<RegisterAssemblyDistributionCommand>
{
    public RegisterAssemblyDistributionCommandValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2999);
        RuleFor(x => x.FundAmount).GreaterThanOrEqualTo(0m).Must(m => RegistroDeCuentas.Decimales(m) <= 2).WithMessage("El fondo lleva a lo sumo dos decimales.");
        RuleFor(x => x).Must(x => x.Percent.HasValue != x.Value.HasValue).WithName("percent").WithMessage("Indique el porcentaje o el valor decidido, uno de los dos.");
        RuleFor(x => x.Percent!.Value).GreaterThan(0m).LessThanOrEqualTo(1m).WithName("percent")
            .Must(p => RegistroDeCuentas.Decimales(p) <= 6).WithMessage("El porcentaje va como fracción (0,05 = 5 %), con hasta seis decimales.")
            .When(x => x.Percent.HasValue);
        RuleFor(x => x.Value!.Value).GreaterThan(0m).WithName("value").Must(v => RegistroDeCuentas.Decimales(v) <= 2)
            .WithMessage("El valor lleva a lo sumo dos decimales.").When(x => x.Value.HasValue);
        RuleFor(x => x.Base).IsInEnum().When(x => x.Base.HasValue);
        RuleFor(x => x.Destination).IsInEnum().When(x => x.Destination.HasValue);
        RuleFor(x => x.Destination).Null().When(x => x.Kind != AssemblyDistributionKind.SurplusDistribution)
            .WithMessage("El destino sólo se elige al distribuir excedentes.");
    }
}

public sealed class RegisterAssemblyDistributionCommandHandler(
    IApplicationDbContext db,
    DistribucionesDeAsamblea distribuciones,
    IContextoDeLineasDeCaptacion contexto,
    RegistroDeIntervenciones intervenciones,
    NumeradorDeCartera numerador,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RegisterAssemblyDistributionCommand, Result<AssemblyDistributionDto>>
{
    public async Task<Result<AssemblyDistributionDto>> Handle(RegisterAssemblyDistributionCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        if (contexto.Entrega < EntregaDeCartera.K10) return Falla(ErroresDeAportes.EntregaNoDisponible("La decisión de asamblea", nameof(EntregaDeCartera.K10)));
        if (await distribuciones.AlcanceTotalAsync(null, ct) is { IsFailure: true } fuera) return Falla(fuera.Error);
        if ((await ActivacionesDeCaptacion.LeerAsync(db, ct)).Aportes is null) return Falla(ErroresDeAsamblea.SinActivar());
        // Los excedentes son de un ejercicio cerrado (Ley 79 art. 53: el ejercicio cierra el 31 de diciembre).
        if (request.Year >= hoy.Year)
            return Falla(ErroresDeAsamblea.Invalida("year", $"El ejercicio {request.Year} no ha cerrado: la asamblea decide sobre un ejercicio terminado."));

        var p = await distribuciones.ParametrosAsync(hoy, ct);
        var destino = request.Kind == AssemblyDistributionKind.SurplusDistribution ? request.Destination ?? p.Destino : (SurplusDestination?)null;
        int? lineaDestino = null;
        if (destino == SurplusDestination.Savings)
        {
            var codigo = request.TargetDepositLineCode?.Trim();
            if (string.IsNullOrWhiteSpace(codigo))
                return Falla(ErroresDeAsamblea.Invalida("targetDepositLineCode", "Los excedentes a ahorro necesitan la línea de ahorro donde cada asociado los recibe."));
            var linea = await db.CreditLines.AsNoTracking().Where(l => l.Code == codigo && !l.IsDeleted && l.Nature == LineNature.Deposit)
                .Select(l => new { l.Id }).FirstOrDefaultAsync(ct);
            var clase = linea is null ? null : await db.DepositLineVersions.AsNoTracking().Where(v => v.CreditLineId == linea.Id && !v.IsDeleted)
                .OrderBy(v => v.Id).Select(v => (DepositClass?)v.DepositClass).FirstOrDefaultAsync(ct);
            if (linea is null || clase is not (DepositClass.Demand or DepositClass.Permanent))
                return Falla(ErroresDeAsamblea.Invalida("targetDepositLineCode",
                    $"«{codigo}» no es una línea de ahorro a la vista o permanente: los excedentes se abonan en una de ésas."));
            lineaDestino = linea.Id;
        }
        else if (!string.IsNullOrWhiteSpace(request.TargetDepositLineCode))
            return Falla(ErroresDeAsamblea.Invalida("targetDepositLineCode", "La línea de ahorro sólo va con los excedentes a ahorro."));

        AssemblyDistribution? corregida = null;
        if (request.CorrectsPublicId is { } corrige)
        {
            corregida = await db.AssemblyDistributions.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == corrige && !x.IsDeleted, ct);
            if (corregida is null) return Falla(ErroresDeAsamblea.Inexistente());
            if (corregida.Status != AssemblyDistributionStatus.Reversed)
                return Falla(ErroresDeAsamblea.CorreccionInvalida($"La decisión {corregida.Number} no está reversada: sólo se corrige una reversada entera."));
            if (corregida.Kind != request.Kind || corregida.FiscalYear != request.Year)
                return Falla(ErroresDeAsamblea.CorreccionInvalida($"La corrección es de la misma clase y del mismo ejercicio que {corregida.Number}."));
            if (await db.AssemblyDistributions.AsNoTracking().AnyAsync(x => x.CorrectsDistributionId == corregida.Id && !x.IsDeleted
                    && (x.Status == AssemblyDistributionStatus.Simulated || x.Status == AssemblyDistributionStatus.Applied), ct))
                return Falla(ErroresDeAsamblea.CorreccionInvalida($"La decisión {corregida.Number} ya tiene una corrección en borrador o aplicada."));
        }

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var numero = await numerador.NumeroDeProcesoDeCaptacionesAsync(LendingNumberKind.AssemblyDistribution, DistribucionesDeAsamblea.Prefijo, ct);
        var d = new AssemblyDistribution
        {
            Number = numero,
            Kind = request.Kind,
            FiscalYear = (short)request.Year,
            BaseMethod = request.Base ?? p.Base,
            DecidedFraction = request.Percent,
            DecidedValue = request.Value,
            FundAmount = request.FundAmount,
            SurplusDestination = destino,
            DestinationLineId = lineaDestino,
            RoundingUnit = p.UnidadDeRedondeo,
            CorrectsDistributionId = corregida?.Id,
            DecisionSessionItemId = corregida?.DecisionSessionItemId,
            Reason = AssemblyDistribution.Recortar(request.Reason),
            RegisteredByUserId = usuario,
            RegisteredAt = reloj.UtcNow,
        };
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SegregacionDeCaptaciones.DecisionDeAsamblea, d.PublicId),
            AccionesDeSegregacion.Radicar, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        db.AssemblyDistributions.Add(d);
        intervenciones.Registrar(verificada.Value, SegregacionDeCaptaciones.DecisionDeAsamblea, d.PublicId);
        await db.SaveChangesAsync(ct);
        return Result.Success(await distribuciones.DtoAsync(d, conLineas: false, ct));
    }

    private static Result<AssemblyDistributionDto> Falla(Error e) => Result.Failure<AssemblyDistributionDto>(e);
}

// ----------------------------------------------------------------------------------------------------------- descartar --

/// <summary><c>POST /{id}/void</c> (<c>VoidAssemblyDistributionCommand</c>): descarta un borrador con motivo (no la aplicada: ésa se reversa). (nuevo)</summary>
public sealed record VoidAssemblyDistributionCommand(Guid DistributionPublicId, string Reason) : IRequest<Result<AssemblyDistributionDto>>,
    IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidAssemblyDistributionCommandValidator : ValidadorConMotivo<VoidAssemblyDistributionCommand>
{
    public VoidAssemblyDistributionCommandValidator() => RuleFor(x => x.DistributionPublicId).NotEmpty();
}

public sealed class VoidAssemblyDistributionCommandHandler(
    IApplicationDbContext db,
    DistribucionesDeAsamblea distribuciones,
    IDateTimeService reloj) : IRequestHandler<VoidAssemblyDistributionCommand, Result<AssemblyDistributionDto>>
{
    public async Task<Result<AssemblyDistributionDto>> Handle(VoidAssemblyDistributionCommand request, CancellationToken ct)
    {
        var leida = await distribuciones.LeerAsync(request.DistributionPublicId, seguir: true, ct);
        if (leida.IsFailure) return Result.Failure<AssemblyDistributionDto>(leida.Error);
        var d = leida.Value;
        if (d.Status != AssemblyDistributionStatus.Simulated) return Result.Failure<AssemblyDistributionDto>(ErroresDeAsamblea.NoEsBorrador(d.Status));
        d.Descartar(reloj.UtcNow, request.Reason);
        await db.SaveChangesAsync(ct);
        return Result.Success(await distribuciones.DtoAsync(d, conLineas: false, ct));
    }
}

// ------------------------------------------------------------------------------------------------------------- aplicar --

/// <summary>
/// <c>POST /{id}/apply</c> (<c>ApplyAssemblyDistributionCommand</c>): <b>otra persona</b> (segregación) aplica la decisión con el punto del acta de la
/// asamblea que la aprobó (sesión cerrada, asunto <c>AssemblyDistribution</c> y esta decisión; una corrección ya lo trae), los soportes
/// (<c>LendingAssemblySupport</c>: el acta firmada) y la huella de la simulación que vio (<c>Lending.Savings.PreviewChanged</c> si cambió). Rechaza
/// aplicarla dos veces (<c>AlreadyApplied</c>) y otra aplicada con la misma acta y clase (<c>DuplicateSession</c>, US12-2). En una transacción: una operación
/// por asociado —<c>AssemblyCredit</c> en aportes (revalorización o excedentes) o en su cuenta de ahorro; sin filas con su orden de giro si los excedentes se
/// pagan; <c>ContributionRefund</c> con su orden si se amortiza—, las líneas, un hecho masivo y las órdenes. (nuevo)
/// </summary>
public sealed record ApplyAssemblyDistributionCommand(
    Guid DistributionPublicId,
    Guid? SessionItemPublicId,
    string ExpectedPreviewSha256,
    IReadOnlyList<Guid>? SupportAttachmentPublicIds,
    string Reason) : IRequest<Result<AssemblyDistributionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyAssemblyDistributionCommandValidator : ValidadorConMotivo<ApplyAssemblyDistributionCommand>
{
    public ApplyAssemblyDistributionCommandValidator()
    {
        RuleFor(x => x.DistributionPublicId).NotEmpty();
        RuleFor(x => x.ExpectedPreviewSha256).NotEmpty().Length(64);
    }
}

public sealed class ApplyAssemblyDistributionCommandHandler(
    IApplicationDbContext db,
    DistribucionesDeAsamblea distribuciones,
    PuntoDeActaCerrado puntoDeActa,
    RegistroDeIntervenciones intervenciones,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    OrdenesDeGiroDeCuentas ordenes,
    ReglasDeAportes reglas,
    ValidacionPreviaDeCaptaciones validacionPrevia,
    ICerrojoDeCartera cerrojo,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ApplyAssemblyDistributionCommand, Result<AssemblyDistributionDto>>
{
    /// <summary>Los prefijos de las operaciones de cuenta de la asamblea.</summary>
    public const string PrefijoRevalorizacion = "RVA-";
    public const string PrefijoExcedentes = "EXD-";
    public const string PrefijoAmortizacion = "AMZ-";

    public async Task<Result<AssemblyDistributionDto>> Handle(ApplyAssemblyDistributionCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leida = await distribuciones.LeerAsync(request.DistributionPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var d = leida.Value;
        if (d.Status == AssemblyDistributionStatus.Applied) return Falla(ErroresDeAsamblea.YaAplicada(d.Number));
        if (d.Status != AssemblyDistributionStatus.Simulated) return Falla(ErroresDeAsamblea.NoEsBorrador(d.Status));
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SegregacionDeCaptaciones.DecisionDeAsamblea, d.PublicId),
            AccionesDeSegregacion.Decidir, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);

        // El acta: la de la decisión (o la heredada de la reversada que corrige). Una aplicada con la misma acta y clase no convive con otra (US12-2).
        int punto;
        Guid puntoPublico;
        if (d.DecisionSessionItemId is { } heredado)
        {
            punto = heredado;
            puntoPublico = await db.SessionItems.AsNoTracking().Where(i => i.Id == heredado).Select(i => i.PublicId).FirstAsync(ct);
        }
        else
        {
            if (request.SessionItemPublicId is not { } item)
                return Falla(ErroresDeAsamblea.Invalida("sessionItemPublicId", "Indique el punto del acta de la asamblea que aprobó la decisión."));
            var cerrado = await puntoDeActa.ValidarAsync(item, LendingDecisionSubjects.AssemblyDistribution, d.PublicId, ct);
            if (cerrado.IsFailure) return Falla(cerrado.Error);
            (punto, puntoPublico) = (cerrado.Value.ItemId, cerrado.Value.ItemPublicId);
        }
        if (await db.AssemblyDistributions.AsNoTracking().Where(x => x.DecisionSessionItemId == punto && x.Kind == d.Kind && x.Id != d.Id && !x.IsDeleted
                && x.Status == AssemblyDistributionStatus.Applied).Select(x => x.Number).FirstOrDefaultAsync(ct) is { } otra)
            return Falla(ErroresDeAsamblea.ActaDuplicada(otra));

        // El soporte: el acta firmada (adjuntos de la decisión).
        var soportes = (request.SupportAttachmentPublicIds ?? []).Distinct().ToList();
        if (soportes.Count == 0) return Falla(ErroresDeAsamblea.SinSoporte());
        var existentes = await db.Attachments.AsNoTracking()
            .CountAsync(a => soportes.Contains(a.PublicId) && a.OwnerEntityType == AdjuntosDeModulo.SoporteDeAsamblea && a.OwnerEntityPublicId == d.PublicId
                             && !a.IsDeleted, ct);
        if (existentes != soportes.Count) return Falla(ErroresDeAsamblea.SinSoporte());

        var simulada = await distribuciones.SimularAsync(d, hoy, ct);
        if (simulada.IsFailure) return Falla(simulada.Error);
        var s = simulada.Value;
        if (!string.Equals(s.Dto.Sha256, request.ExpectedPreviewSha256, StringComparison.OrdinalIgnoreCase))
            return Falla(ErroresDeAsamblea.VistaPreviaCambio(s.Dto));
        var aplicables = s.Calculo.Lineas.Where(l => l.Aplicado > 0m).ToList();
        if (aplicables.Count == 0) return Falla(ErroresDeAsamblea.SinAsociados());
        // Respuesta 93 B: quien no tiene cuenta de ahorro de destino no bloquea la decisión: su excedente se capitaliza en sus aportes (E257).
        if (d.Kind == AssemblyDistributionKind.Amortization && await reglas.MinimoDeLaEntidadAsync(hoy, ct) is { } minimo and > 0m)
        {
            var capital = await reglas.CapitalSocialAsync(ct);
            if (capital - s.Calculo.TotalAplicado < minimo) return Falla(ErroresDeAportes.MinimoDeLaEntidad(minimo, capital));
        }

        // D23: la pantalla bloquea si lo de la asamblea no se contabilizaría (una pregunta por combinación de dimensiones).
        var validacion = PrevalidationOutcome.NotApplicable;
        var capitalizadas = s.PersonasSinDestino.ToHashSet();
        var retiradas = s.CuentasDeRetirados; // respuesta 91 B (E259): quien se retiró en el ejercicio recibe su parte por giro, nunca en una cuenta
        var cuentasDeLaPregunta = aplicables.Where(l => !capitalizadas.Contains(l.PersonaId) && !retiradas.Contains(l.CuentaId))
            .Select(l => s.Destinos.TryGetValue(l.CuentaId, out var de) ? de.Id : l.CuentaId).Distinct().ToList();
        var preguntas = new List<(HechosRepresentativosDeCaptaciones.CuentaRepresentativa C, SurplusDestination? Destino)>();
        preguntas.AddRange((await validacionPrevia.DimensionesAsync(cuentasDeLaPregunta, hoy, ct)).Values.GroupBy(c => c.D).Select(g => (g.First(), d.SurplusDestination)));
        var aPagarAlRetirado = aplicables.Where(l => retiradas.Contains(l.CuentaId)).Select(l => l.CuentaId).Distinct().ToList();
        if (aPagarAlRetirado.Count > 0)
            preguntas.AddRange((await validacionPrevia.DimensionesAsync(aPagarAlRetirado, hoy, ct)).Values.GroupBy(c => c.D)
                .Select(g => (g.First(), (SurplusDestination?)SurplusDestination.Payment)));
        var aCapitalizar = aplicables.Where(l => capitalizadas.Contains(l.PersonaId)).Select(l => l.CuentaId).Distinct().ToList();
        if (aCapitalizar.Count > 0)
            preguntas.AddRange((await validacionPrevia.DimensionesAsync(aCapitalizar, hoy, ct)).Values.GroupBy(c => c.D)
                .Select(g => (g.First(), (SurplusDestination?)SurplusDestination.Contributions)));
        foreach (var (c, destinoDeLaPregunta) in preguntas)
        {
            var decision = await validacionPrevia.EnPantallaAsync(HechosRepresentativosDeCaptaciones.Unidad(hoy, c,
                HechosRepresentativosDeCaptaciones.Asamblea(c, d.Kind, destinoDeLaPregunta, s.Parametros.ConceptoDeExcedentes)), $"la decisión de asamblea {d.Number}", ct);
            if (decision.Bloqueo is { } bloqueo) return Falla(bloqueo);
            validacion = decision.Outcome;
        }

        var actor = await actorActual.ObtenerAsync(ct);
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var personas = aplicables.Select(l => l.PersonaId).Distinct().Order().ToList();
            var idsCuentas = aplicables.Select(l => l.CuentaId).Concat(s.Destinos.Values.Select(x => x.Id)).Distinct().Order().ToList();
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(personas, ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync(idsCuentas, ModoDeEspera.Esperar, ct);
            var cuentas = await db.MemberAccounts.Where(a => idsCuentas.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
            var controles = await db.MemberAccountControls.Where(c => idsCuentas.Contains(c.MemberAccountId)).ToDictionaryAsync(c => c.MemberAccountId, ct);
            Dictionary<int, Dictionary<MemberAccountConcept, decimal>> saldos = d.Kind == AssemblyDistributionKind.Amortization
                ? await LectorDeCuentas.SaldosAsync(db, idsCuentas, ct)
                : new();
            var ahora = reloj.UtcNow;
            var payload = string.Create(CultureInfo.InvariantCulture,
                $"{{\"distributionPublicId\":\"{d.PublicId:N}\",\"kind\":\"{d.Kind}\",\"year\":{d.FiscalYear}}}");

            // 1. Una operación por asociado (en su cuenta de aportes, o en su cuenta de ahorro si los excedentes van a ahorro).
            var hechas = new List<(LineaDeDistribucion Linea, MemberAccount Cuenta, MemberAccount? Destino, MemberAccountOperation Operacion, HechoDeCuenta Hecho,
                IReadOnlyList<(MemberAccountConcept Concepto, decimal Monto)> Porciones)>();
            foreach (var l in aplicables)
            {
                var aportes = cuentas[l.CuentaId];
                var destino = s.Destinos.TryGetValue(l.CuentaId, out var de) ? cuentas[de.Id] : null;
                var cuenta = destino ?? aportes;
                var hecho = await emision.HechoDeCuentaAsync(cuenta, hoy, ct);
                if (hecho.IsFailure) return Falla(hecho.Error);
                IReadOnlyList<(MemberAccountConcept, decimal)> porciones = d.Kind switch
                {
                    AssemblyDistributionKind.Revaluation => [(MemberAccountConcept.ContributionRevaluation, l.Aplicado)],
                    AssemblyDistributionKind.Amortization => DebitoDeAportes.Repartir(saldos.GetValueOrDefault(aportes.Id) ?? [], l.Aplicado, 0m)
                                                             ?? throw new InvalidOperationException($"La cuenta {aportes.Number} no alcanza para amortizar {l.Aplicado}."),
                    _ when retiradas.Contains(l.CuentaId) => [], // se gira: no entra a ninguna cuenta (E259)
                    _ => d.SurplusDestination switch
                    {
                        SurplusDestination.Savings when destino is null => [(s.Parametros.ConceptoDeExcedentes, l.Aplicado)],
                        SurplusDestination.Savings => [(MemberAccountConcept.Principal, l.Aplicado)],
                        SurplusDestination.Payment => [],
                        _ => [(s.Parametros.ConceptoDeExcedentes, l.Aplicado)],
                    },
                };
                var clase = d.Kind == AssemblyDistributionKind.Amortization ? MemberAccountOperationKind.ContributionRefund : MemberAccountOperationKind.AssemblyCredit;
                var prefijo = d.Kind switch
                {
                    AssemblyDistributionKind.Revaluation => PrefijoRevalorizacion,
                    AssemblyDistributionKind.Amortization => PrefijoAmortizacion,
                    _ => PrefijoExcedentes,
                };
                var numero = await numerador.NumeroDeOperacionDeCuentaAsync(clase, prefijo, ct);
                var operacion = registro.RegistrarOperacion(controles[cuenta.Id], ahora, sello => new MemberAccountOperation
                {
                    MemberAccountId = cuenta.Id,
                    Kind = clase,
                    Number = numero,
                    Sequence = sello.Sequence,
                    ValueDate = hoy,
                    AccountingDate = hecho.Value.FechaContable,
                    RegisteredAt = sello.RegisteredAt,
                    Amount = l.Aplicado,
                    ActorKind = actor.Kind,
                    ActorUserId = actor.UserId,
                    OriginUserCentralId = actor.CentralUserId,
                    AssemblyDistributionId = d.Id,
                    Reason = $"Decisión de asamblea {d.Number}",
                    Payload = payload,
                    InputsSha256 = s.Dto.Sha256,
                    RulesVersion = VersionesDeReglas.Vigente,
                    PrevalidationOutcome = validacion.ToString(),
                });
                hechas.Add((l, aportes, destino, operacion, hecho.Value, porciones));
            }
            await db.SaveChangesAsync(ct);

            // 2. Las órdenes de giro: la amortización y el pago de los excedentes, al beneficiario registrado del asociado.
            var ordenesDeLinea = new Dictionary<long, (AccountPayoutOrder Orden, PayeeV1 Payee, Guid Persona)>();
            var conOrden = d.Kind == AssemblyDistributionKind.Amortization || d.SurplusDestination == SurplusDestination.Payment || retiradas.Count > 0;
            if (conOrden)
            {
                foreach (var h in hechas.Where(x => d.Kind == AssemblyDistributionKind.Amortization || d.SurplusDestination == SurplusDestination.Payment
                                                    || retiradas.Contains(x.Linea.CuentaId)))
                {
                    var titular = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(x => x.Id == h.Cuenta.HolderPersonId, ct);
                    var beneficiario = await ordenes.BeneficiarioAsync(titular.Id, null, null, null, ct);
                    if (beneficiario.IsFailure) return Falla(beneficiario.Error);
                    var b = beneficiario.Value;
                    var plazo = d.Kind == AssemblyDistributionKind.Amortization
                        ? await ordenes.PlazoDeDevolucionAsync(hoy, (await reglas.VersionAsync(h.Cuenta, hoy, ct))?.RefundTermDays, ct)
                        : hoy.AddDays(1);
                    var orden = AccountPayoutOrder.Emitir(h.Cuenta.Id, h.Operacion.Id,
                        d.Kind == AssemblyDistributionKind.Amortization ? PayoutPurpose.ContributionRefund : PayoutPurpose.SurplusPayment, titular.Id,
                        titular.IdType ?? string.Empty, titular.TaxId ?? string.Empty, ReferenciasDeGobierno.Nombre(titular), b.BancoId, b.TipoDeCuenta,
                        b.NumeroDeCuenta, h.Linea.Aplicado, plazo < hoy.AddDays(1) ? hoy.AddDays(1) : plazo, ahora, actor.UserId);
                    await numerador.NumerarOrdenDeCuentaAsync(orden, Disbursements.TramoDeDesembolso.PrefijoDeOrden, ct);
                    db.AccountPayoutOrders.Add(orden);
                    ordenesDeLinea[h.Operacion.Id] = (orden, new PayeeV1
                    {
                        PersonPublicId = titular.PublicId, TaxIdType = titular.IdType ?? string.Empty, TaxId = titular.TaxId ?? string.Empty,
                        Name = ReferenciasDeGobierno.Nombre(titular), BankCode = b.CodigoDeTransferencia, BankAccountNumber = b.NumeroDeCuenta,
                        BankAccountType = b.TipoDeCuenta?.ToString(CultureInfo.InvariantCulture),
                    }, titular.PublicId);
                }
                await db.SaveChangesAsync(ct);
            }

            // 3. El hecho masivo y las órdenes de giro (sin guardar), luego las filas del libro con su mensaje.
            var motivo = d.Kind == AssemblyDistributionKind.Amortization ? nameof(PayoutPurpose.ContributionRefund) : null;
            var lineas = hechas.Where(h => h.Porciones.Count > 0).Select(h => new LineaDeAsambleaAEmitir(h.Hecho, h.Porciones.Select(x => new DepositConceptAmountV1
            {
                Concept = x.Concepto.ToString(), ContributionClass = EmisionDeCartera.ClaseDeAporte(x.Concepto)?.ToString(),
                Amount = d.Kind == AssemblyDistributionKind.Amortization ? -x.Monto : x.Monto, ReasonCode = motivo,
            }).ToList())).ToList();
            var pagos = d.Kind == AssemblyDistributionKind.SurplusDistribution
                ? hechas.Where(h => d.SurplusDestination == SurplusDestination.Payment || retiradas.Contains(h.Linea.CuentaId)).Select(h => new OrdenDeAsambleaAEmitir(h.Hecho, h.Linea.Aplicado, ordenesDeLinea[h.Operacion.Id].Orden, ordenesDeLinea[h.Operacion.Id].Payee,
                    ordenesDeLinea[h.Operacion.Id].Persona)).ToList()
                : [];
            var deAmortizacion = d.Kind == AssemblyDistributionKind.Amortization
                ? hechas.Select(h => new OrdenDeAsambleaAEmitir(h.Hecho, h.Linea.Aplicado, ordenesDeLinea[h.Operacion.Id].Orden, ordenesDeLinea[h.Operacion.Id].Payee,
                    ordenesDeLinea[h.Operacion.Id].Persona)).ToList()
                : [];
            d.AppliedTotal = s.Calculo.TotalAplicado;
            var unidad = await emision.DistribucionAplicadaAsync(new DistribucionAEmitir(d, puntoPublico, hoy, lineas, pagos, deAmortizacion, validacion), ct);
            if (unidad.IsFailure) throw new InvalidOperationException($"{unidad.Error.Code}: {unidad.Error.Message}");
            var mensaje = unidad.Value.De(EmisionDeCartera.TipoDeAsamblea(d.Kind));
            var claseDeFila = d.Kind == AssemblyDistributionKind.Amortization ? MemberAccountTransactionKind.Refund : MemberAccountTransactionKind.AssemblyCredit;
            registro.RegistrarTransacciones(hechas.SelectMany(h => h.Porciones.Select(x => new MemberAccountTransaction
            {
                MemberAccountId = h.Operacion.MemberAccountId,
                Operation = h.Operacion,
                OperationId = h.Operacion.Id,
                Kind = claseDeFila,
                Concept = x.Concepto,
                Amount = d.Kind == AssemblyDistributionKind.Amortization ? -x.Monto : x.Monto,
                ValueDate = hoy,
                AccountingDate = h.Hecho.FechaContable,
                RegisteredAt = h.Operacion.RegisteredAt,
                MessagePublicId = mensaje,
            })).ToList());

            // 4. Las líneas (hecho de sólo inserción) y la decisión aplicada.
            foreach (var l in s.Calculo.Lineas)
            {
                var h = hechas.FirstOrDefault(x => x.Linea.CuentaId == l.CuentaId);
                db.AssemblyDistributionLines.Add(new AssemblyDistributionLine
                {
                    AssemblyDistributionId = d.Id,
                    SimulationVersion = 1,
                    PersonId = l.PersonaId,
                    ContributionAccountId = l.CuentaId,
                    DestinationAccountId = h.Destino?.Id,
                    BaseAmount = l.Base,
                    CalculatedAmount = l.Calculado,
                    ProratedAmount = l.Aplicado,
                    LimitedByMinimum = l.LimitadaPorMinimo,
                    OperationId = h.Operacion?.Id,
                    WithdrawnDuringYear = retiradas.Contains(l.CuentaId),
                    PayoutOrderId = h.Operacion is { } op && ordenesDeLinea.TryGetValue(op.Id, out var o) ? o.Orden.Id : null,
                });
            }
            d.RoundingUnit = s.Parametros.UnidadDeRedondeo;
            d.AppliedMessagePublicId = mensaje;
            d.Aplicar(punto, actor.UserId ?? 0, ahora, hoy, s.Calculo.TotalCalculado, s.Calculo.Factor, s.Calculo.TotalAplicado, s.LimiteLegal, s.FuenteDelLimite,
                s.Dto.Sha256, DistribucionesDeAsamblea.SoportesJson(soportes));
            intervenciones.Registrar(verificada.Value, SegregacionDeCaptaciones.DecisionDeAsamblea, d.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await distribuciones.DtoAsync(d, conLineas: true, ct));
        }, ct);
    }

    private static Result<AssemblyDistributionDto> Falla(Error e) => Result.Failure<AssemblyDistributionDto>(e);
}

// ------------------------------------------------------------------------------------------------------------- reversar --

/// <summary>
/// <c>POST /{id}/reverse</c> (<c>ReverseAssemblyDistributionCommand</c>): reversa <b>entera</b> una decisión aplicada —el espejo de cada operación en su
/// cuenta, con la fecha valor de la aplicación, y <b>un</b> <c>OperacionDeCuentaReversada</c> que devuelve cada valor a su fondo—; para corregirla se
/// registra otra que la corrige. No se reversa si un asociado ya no tiene lo que recibió (<c>Lending.AssemblyDistribution.ReversalWouldOverdraw</c>), si
/// los excedentes se pagaron en dinero (<c>PaymentNotReversible</c>) ni mientras una orden de giro de la amortización siga viva o ya girada
/// (<c>PayoutsInProgress</c>); las amortizaciones que Tesorería rechazó ya volvieron a los aportes. (nuevo)
/// </summary>
public sealed record ReverseAssemblyDistributionCommand(Guid DistributionPublicId, string Reason) : IRequest<Result<AssemblyDistributionDto>>,
    IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReverseAssemblyDistributionCommandValidator : ValidadorConMotivo<ReverseAssemblyDistributionCommand>
{
    public ReverseAssemblyDistributionCommandValidator() => RuleFor(x => x.DistributionPublicId).NotEmpty();
}

public sealed class ReverseAssemblyDistributionCommandHandler(
    IApplicationDbContext db,
    DistribucionesDeAsamblea distribuciones,
    RegistroDeIntervenciones intervenciones,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ICerrojoDeCartera cerrojo,
    IActorActual actorActual,
    IDateTimeService reloj,
    Documents.AnulacionDeDocumentosDeCuentas? documentos = null) : IRequestHandler<ReverseAssemblyDistributionCommand, Result<AssemblyDistributionDto>>
{
    public const string CodigoDejariaNegativo = "Lending.AssemblyDistribution.ReversalWouldOverdraw";

    public async Task<Result<AssemblyDistributionDto>> Handle(ReverseAssemblyDistributionCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leida = await distribuciones.LeerAsync(request.DistributionPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var d = leida.Value;
        if (d.Status != AssemblyDistributionStatus.Applied) return Falla(ErroresDeAsamblea.NoAplicada(d.Status));
        if (d.Kind == AssemblyDistributionKind.SurplusDistribution && d.SurplusDestination == SurplusDestination.Payment)
            return Falla(ErroresDeAsamblea.PagoNoReversable());
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SegregacionDeCaptaciones.DecisionDeAsamblea, d.PublicId),
            AccionesDeSegregacion.Decidir, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);

        var lineas = await db.AssemblyDistributionLines.AsNoTracking()
            .Where(l => l.AssemblyDistributionId == d.Id && l.SimulationVersion == d.SimulationVersion && l.OperationId != null).ToListAsync(ct);
        // Respuesta 91 B (E259): lo que se giró a quien se retiró ya salió a Tesorería; la decisión no se reversa entera.
        if (lineas.Any(l => l.WithdrawnDuringYear)) return Falla(ErroresDeAsamblea.PagoNoReversable());
        var idsOps = lineas.Select(l => l.OperationId!.Value).ToList();
        var vivas = await db.AccountPayoutOrders.AsNoTracking()
            .CountAsync(o => idsOps.Contains(o.OperationId) && !o.IsDeleted
                             && (o.Status == DisbursementOrderStatus.Issued || o.Status == DisbursementOrderStatus.Disbursed), ct);
        if (vivas > 0) return Falla(ErroresDeAsamblea.GirosEnCurso(vivas));

        var actor = await actorActual.ObtenerAsync(ct);
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var originales = await db.MemberAccountOperations.Where(o => idsOps.Contains(o.Id) && o.ReversedByOperationId == null).OrderBy(o => o.Id)
                .ToListAsync(ct);
            var idsCuentas = originales.Select(o => o.MemberAccountId).Distinct().Order().ToList();
            var personas = await db.MemberAccounts.AsNoTracking().Where(a => idsCuentas.Contains(a.Id)).Select(a => a.HolderPersonId).Distinct().Order()
                .ToListAsync(ct);
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            if (personas.Count > 0) await cerrojo.BloquearDeudoresAsync(personas, ModoDeEspera.Esperar, ct);
            if (idsCuentas.Count > 0) await cerrojo.BloquearCuentasAsync(idsCuentas, ModoDeEspera.Esperar, ct);
            var ahora = reloj.UtcNow;
            var motivo = AssemblyDistribution.Recortar(request.Reason);

            if (originales.Count > 0)
            {
                var cuentas = await db.MemberAccounts.Where(a => idsCuentas.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
                var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.OperationId != null && idsOps.Contains(t.OperationId.Value) && !t.IsDeleted).OrderBy(t => t.Id)
                    .ToListAsync(ct);
                // Nadie queda en negativo: lo que la asamblea abonó tiene que seguir en la cuenta (un retiro posterior ya lo usó).
                var saldos = await LectorDeCuentas.SaldosAsync(db, idsCuentas, ct);
                var negativas = new List<string>();
                foreach (var g in filas.GroupBy(f => f.MemberAccountId))
                {
                    var cuenta = cuentas[g.Key];
                    var despues = new Dictionary<MemberAccountConcept, decimal>(saldos.GetValueOrDefault(g.Key) ?? []);
                    foreach (var f in g) despues[f.Concept] = despues.GetValueOrDefault(f.Concept) - f.Amount;
                    if (LectorDeCuentas.SaldoDelTitular(cuenta.Nature, despues) < 0m || despues.Any(x => x.Value < 0m && MemberAccountTransaction.ConceptosDelTitular.Contains(x.Key)))
                        negativas.Add(cuenta.Number);
                }
                if (negativas.Count > 0)
                    return Falla(new ErrorConDatos(CodigoDejariaNegativo,
                        $"{negativas.Count} cuenta(s) ya no tienen lo que la asamblea les abonó: el reverso las dejaría en negativo. Corrija con otra decisión.",
                        new { accounts = negativas }));

                var espejos = new List<LineaDeAsambleaAEmitir>();
                var reversos = new List<(MemberAccountOperation Original, MemberAccountOperation Reverso, DateOnly Contable)>();
                foreach (var original in originales)
                {
                    var cuenta = cuentas[original.MemberAccountId];
                    var hecho = await emision.HechoDeCuentaAsync(cuenta, hoy, ct);
                    if (hecho.IsFailure) return Falla(hecho.Error);
                    var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
                    var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Reversal, ReversionDeCuentas.PrefijoReverso, ct);
                    var reverso = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
                    {
                        MemberAccountId = cuenta.Id,
                        Kind = MemberAccountOperationKind.Reversal,
                        Number = numero,
                        Sequence = sello.Sequence,
                        ValueDate = original.ValueDate,
                        AccountingDate = hecho.Value.FechaContable,
                        RegisteredAt = sello.RegisteredAt,
                        Amount = original.Amount,
                        ActorKind = actor.Kind,
                        ActorUserId = actor.UserId,
                        OriginUserCentralId = actor.CentralUserId,
                        AssemblyDistributionId = d.Id,
                        ReversesOperationId = original.Id,
                        Reason = $"Reverso de la decisión de asamblea {d.Number}: {motivo}".Length <= 500
                            ? $"Reverso de la decisión de asamblea {d.Number}: {motivo}"
                            : $"Reverso de la decisión de asamblea {d.Number}",
                        Payload = original.Payload,
                        InputsSha256 = original.InputsSha256,
                        RulesVersion = original.RulesVersion,
                    });
                    reversos.Add((original, reverso, hecho.Value.FechaContable));
                    var valores = EmisionDeCartera.ValoresDeCuenta(filas.Where(f => f.OperationId == original.Id), f => -f.Amount);
                    if (valores.Count > 0) espejos.Add(new LineaDeAsambleaAEmitir(hecho.Value, valores));
                }

                Guid? mensaje = null;
                if (espejos.Count > 0)
                {
                    var usuario = actor.UserId is null ? null : new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name };
                    var unidad = await emision.DistribucionReversadaAsync(new ReversoDeDistribucionAEmitir(d, EmisionDeCartera.TipoDeAsamblea(d.Kind), hoy, espejos,
                        motivo, usuario, ahora), ct);
                    mensaje = unidad.Mensajes.FirstOrDefault()?.PublicId;
                }
                foreach (var (original, reverso, contable) in reversos)
                {
                    var espejo = registro.Reversar(filas.Where(f => f.OperationId == original.Id), reverso, contable, mensaje);
                    if (espejo.IsFailure) return Falla(espejo.Error);
                }
                await db.SaveChangesAsync(ct);
                foreach (var (original, reverso, _) in reversos)
                {
                    RegistroDeCuentas.MarcarReversada(original, reverso);
                    if (documentos is not null) await documentos.AnularAsync(original, reverso, ct);
                }
            }

            d.Reversar(actor.UserId ?? 0, ahora, motivo);
            intervenciones.Registrar(verificada.Value, SegregacionDeCaptaciones.DecisionDeAsamblea, d.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await distribuciones.DtoAsync(d, conLineas: true, ct));
        }, ct);
    }

    private static Result<AssemblyDistributionDto> Falla(Error e) => Result.Failure<AssemblyDistributionDto>(e);
}
