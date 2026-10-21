using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;

// Feature 014, US3 (T093, T095; api.md §18.4): el recaudador de prueba TST sobre el contrato de cuentas —listar las cuotas exigibles de una persona,
// consignar y reversar—, sólo fuera de producción y con Lending.TestDoubles.Operate, como sus rutas de obligaciones. Desde K8 (US4, T113) también
// consultar las cuentas de una persona, simular, retirar y trasladar. (nuevo)

/// <summary><c>GET /api/lending/test-doubles/collector/accounts/installments</c>: <c>ListarExigiblesAsync</c> de la persona a una fecha. (nuevo)</summary>
public sealed record ListTestCollectorAccountInstallmentsQuery(Guid PersonPublicId, DateOnly? Date = null)
    : IRequest<Result<RespuestaDelContratoDto<ExigiblesDeCuentasDto>>>;

public sealed class ListTestCollectorAccountInstallmentsQueryHandler(IContratoDeCuentas contrato, IEnumerable<IDestinoDeMensajes> destinos, IDateTimeService reloj)
    : IRequestHandler<ListTestCollectorAccountInstallmentsQuery, Result<RespuestaDelContratoDto<ExigiblesDeCuentasDto>>>
{
    public async Task<Result<RespuestaDelContratoDto<ExigiblesDeCuentasDto>>> Handle(ListTestCollectorAccountInstallmentsQuery request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos))
            return Result.Failure<RespuestaDelContratoDto<ExigiblesDeCuentasDto>>(RecaudadorDePruebaRegistrado.NoExiste());
        return Result.Success(await contrato.ListarExigiblesAsync(
            new SolicitudDeExigibles(RecaudadorDePrueba.Recaudador, [request.PersonPublicId], request.Date ?? reloj.HoyLocal), ct));
    }
}

/// <summary>
/// <c>POST /api/lending/test-doubles/collector/accounts/deposits</c> (con clave de pantalla): un lote de consignaciones y de no descontados por
/// <c>ConsignarAsync</c>, no interactivo; las claves de negocio <c>TST:{guid:N}</c> las propone quien prueba y puede repetirlas. (nuevo)
/// </summary>
/// <remarks>Con <paramref name="AsTreasury"/> (D52, 2026-10-18) consigna como el doble de Tesorería (<c>TES</c>, <c>Treasury</c>): transferencias recibidas.</remarks>
public sealed record ApplyTestCollectorAccountDepositsCommand(IReadOnlyList<ConsignacionAAplicar> Deposits, IReadOnlyList<NoDescontadoDeCuenta>? NotDeducted = null,
    bool AsTreasury = false)
    : IRequest<Result<RespuestaDeConsignacionDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyTestCollectorAccountDepositsCommandValidator : AbstractValidator<ApplyTestCollectorAccountDepositsCommand>
{
    public ApplyTestCollectorAccountDepositsCommandValidator() =>
        RuleFor(x => x).Must(x => (x.Deposits?.Count ?? 0) + (x.NotDeducted?.Count ?? 0) > 0).WithMessage("Agregue al menos una consignación o un no descontado.");
}

public sealed class ApplyTestCollectorAccountDepositsCommandHandler(IApplicationDbContext db, IContratoDeCuentas contrato, IEnumerable<IDestinoDeMensajes> destinos)
    : IRequestHandler<ApplyTestCollectorAccountDepositsCommand, Result<RespuestaDeConsignacionDto>>
{
    public async Task<Result<RespuestaDeConsignacionDto>> Handle(ApplyTestCollectorAccountDepositsCommand request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos)) return Result.Failure<RespuestaDeConsignacionDto>(RecaudadorDePruebaRegistrado.NoExiste());
        var recaudador = request.AsTreasury ? RecaudadorDePrueba.Tesoreria : RecaudadorDePrueba.Recaudador;
        return await TransaccionExplicita.EjecutarAsync(db, async () => Result.Success(await contrato.ConsignarAsync(
            new SolicitudDeConsignacion(recaudador, FormaDeAplicacion.NonInteractive, request.Deposits ?? [], request.NotDeducted ?? []), ct)), ct);
    }
}

/// <summary><c>POST /api/lending/test-doubles/collector/accounts/operations/{key}/reverse</c> (con clave de pantalla): <c>ReversarAsync</c> por la clave. (nuevo)</summary>
public sealed record ReverseTestCollectorAccountOperationCommand(string IdempotencyKey, string Reason, bool AsTreasury = false)
    : IRequest<Result<RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReverseTestCollectorAccountOperationCommandValidator : AbstractValidator<ReverseTestCollectorAccountOperationCommand>
{
    public ReverseTestCollectorAccountOperationCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se reversa la operación.").MaximumLength(500);
    }
}

public sealed class ReverseTestCollectorAccountOperationCommandHandler(IApplicationDbContext db, IContratoDeCuentas contrato,
    IEnumerable<IDestinoDeMensajes> destinos) : IRequestHandler<ReverseTestCollectorAccountOperationCommand, Result<RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>>>
{
    public async Task<Result<RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>>> Handle(ReverseTestCollectorAccountOperationCommand request,
        CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos))
            return Result.Failure<RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>>(RecaudadorDePruebaRegistrado.NoExiste());
        var recaudador = request.AsTreasury ? RecaudadorDePrueba.Tesoreria : RecaudadorDePrueba.Recaudador;
        return await TransaccionExplicita.EjecutarAsync(db, async () => Result.Success(await contrato.ReversarAsync(
            new SolicitudDeReversoDeCuenta(recaudador, request.IdempotencyKey, request.Reason.Trim()), ct)), ct);
    }
}

// ------------------------------------------------------------------------------------------------------- K8 (US4, T113) --

/// <summary><c>GET /api/lending/test-doubles/collector/accounts</c>: <c>ConsultarAsync</c> de la persona a una fecha. (nuevo)</summary>
public sealed record ListTestCollectorAccountsQuery(Guid PersonPublicId, DateOnly? Date = null, bool IncludeAsSignatory = false)
    : IRequest<Result<RespuestaDelContratoDto<CuentasDePersonaDto>>>;

public sealed class ListTestCollectorAccountsQueryHandler(IContratoDeCuentas contrato, IEnumerable<IDestinoDeMensajes> destinos, IDateTimeService reloj)
    : IRequestHandler<ListTestCollectorAccountsQuery, Result<RespuestaDelContratoDto<CuentasDePersonaDto>>>
{
    public async Task<Result<RespuestaDelContratoDto<CuentasDePersonaDto>>> Handle(ListTestCollectorAccountsQuery request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos))
            return Result.Failure<RespuestaDelContratoDto<CuentasDePersonaDto>>(RecaudadorDePruebaRegistrado.NoExiste());
        return Result.Success(await contrato.ConsultarAsync(
            new SolicitudDeConsultaDeCuentas(RecaudadorDePrueba.Recaudador, request.PersonPublicId, request.Date ?? reloj.HoyLocal, request.IncludeAsSignatory), ct));
    }
}

/// <summary><c>POST /api/lending/test-doubles/collector/accounts/simulations</c> (sin clave): <c>SimularAsync</c> con el recaudador de prueba. (nuevo)</summary>
public sealed record SimulateTestCollectorAccountOperationQuery(SolicitudDeSimulacionDeCuenta Request) : IRequest<Result<RespuestaDelContratoDto<SimulacionDeCuentaDto>>>;

public sealed class SimulateTestCollectorAccountOperationQueryHandler(IContratoDeCuentas contrato, IEnumerable<IDestinoDeMensajes> destinos)
    : IRequestHandler<SimulateTestCollectorAccountOperationQuery, Result<RespuestaDelContratoDto<SimulacionDeCuentaDto>>>
{
    public async Task<Result<RespuestaDelContratoDto<SimulacionDeCuentaDto>>> Handle(SimulateTestCollectorAccountOperationQuery request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos))
            return Result.Failure<RespuestaDelContratoDto<SimulacionDeCuentaDto>>(RecaudadorDePruebaRegistrado.NoExiste());
        return Result.Success(await contrato.SimularAsync(request.Request with { Collector = RecaudadorDePrueba.Recaudador }, ct));
    }
}

/// <summary>
/// <c>POST /api/lending/test-doubles/collector/accounts/withdrawals</c> (con clave de pantalla): <c>RetirarAsync</c> con el recaudador de prueba; la clave de
/// negocio <c>TST:{guid:N}</c> la propone quien prueba. (nuevo)
/// </summary>
public sealed record ApplyTestCollectorAccountWithdrawalCommand(SolicitudDeRetiro Request)
    : IRequest<Result<RespuestaDelContratoDto<ComprobanteDeCuentaDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyTestCollectorAccountWithdrawalCommandHandler(IApplicationDbContext db, IContratoDeCuentas contrato, IEnumerable<IDestinoDeMensajes> destinos)
    : IRequestHandler<ApplyTestCollectorAccountWithdrawalCommand, Result<RespuestaDelContratoDto<ComprobanteDeCuentaDto>>>
{
    public async Task<Result<RespuestaDelContratoDto<ComprobanteDeCuentaDto>>> Handle(ApplyTestCollectorAccountWithdrawalCommand request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos))
            return Result.Failure<RespuestaDelContratoDto<ComprobanteDeCuentaDto>>(RecaudadorDePruebaRegistrado.NoExiste());
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
            Result.Success(await contrato.RetirarAsync(request.Request with { Collector = RecaudadorDePrueba.Recaudador }, ct)), ct);
    }
}

/// <summary><c>POST /api/lending/test-doubles/collector/accounts/transfers</c> (con clave de pantalla): <c>TrasladarAsync</c> con el recaudador de prueba. (nuevo)</summary>
public sealed record ApplyTestCollectorAccountTransferCommand(SolicitudDeTraslado Request)
    : IRequest<Result<RespuestaDelContratoDto<ComprobanteDeCuentaDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyTestCollectorAccountTransferCommandHandler(IApplicationDbContext db, IContratoDeCuentas contrato, IEnumerable<IDestinoDeMensajes> destinos)
    : IRequestHandler<ApplyTestCollectorAccountTransferCommand, Result<RespuestaDelContratoDto<ComprobanteDeCuentaDto>>>
{
    public async Task<Result<RespuestaDelContratoDto<ComprobanteDeCuentaDto>>> Handle(ApplyTestCollectorAccountTransferCommand request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos))
            return Result.Failure<RespuestaDelContratoDto<ComprobanteDeCuentaDto>>(RecaudadorDePruebaRegistrado.NoExiste());
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
            Result.Success(await contrato.TrasladarAsync(request.Request with { Collector = RecaudadorDePrueba.Recaudador }, ct)), ct);
    }
}
