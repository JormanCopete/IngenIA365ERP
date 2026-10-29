using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.BankAccounts;

// Feature 015, US10 (T0138; FR-102, FR-103, FR-106, FR-207; contracts/api.md §9; data-model §9.2): las cuentas bancarias propias con su vínculo único a la cuenta
// contable bancaria (por IContabilidadParaTesoreria, en el mismo guardado), sus saldos (lectura sensible auditada) y el alcance por cuenta. Configuración: se
// admite antes de activar Tesorería (no lleva IEscrituraDeTesoreria). El banco tiene que tener código de transferencia; el fondo de liquidez es exclusivo. (nuevo)

/// <summary>Una cuenta bancaria propia como la ve la pantalla, con su vínculo contable resuelto (código, sin la cuenta). (nuevo)</summary>
public sealed record OwnBankAccountDto(
    Guid PublicId,
    string Code,
    string Name,
    Guid BankPublicId,
    string BankName,
    string? BankTransferCode,
    string AccountNumber,
    OwnBankAccountType AccountType,
    Guid BranchPublicId,
    string BranchName,
    BankAccountPurpose Purposes,
    bool IsGmfExempt,
    Guid? BankThirdPartyPersonPublicId,
    TreasuryCatalogStatus Status,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    Guid? LinkedChartAccountPublicId,
    string? LinkedChartAccountCode,
    decimal? LastStatementBalance,
    DateOnly? LastStatementDate);

/// <summary>Los saldos de una cuenta propia (FR-103): en libros (Contabilidad), según el último extracto y el disponible proyectado. (nuevo)</summary>
public sealed record OwnBankAccountBalancesDto(
    Guid PublicId,
    string Code,
    DateOnly Date,
    decimal BookBalance,
    decimal? StatementBalance,
    DateOnly? StatementDate,
    decimal ApprovedNotSent,
    decimal ChecksInTransit,
    decimal RemittancesInTransit,
    decimal ProjectedAvailable,
    decimal ChecksNotInBooks = 0m,
    IReadOnlyList<IngenIA365ERP.Domain.Treasury.Saldos.ChequeEnTransito>? Checks = null,
    IReadOnlyList<IngenIA365ERP.Domain.Treasury.Saldos.RemesaEnTransito>? Remittances = null);

/// <summary>Lo común de las cuentas propias: el DTO, la visible con alcance y las reglas del propósito. (nuevo)</summary>
public static class CuentasPropias
{
    public const string Catalogo = "una cuenta bancaria propia";

    public static async Task<OwnBankAccountDto> DtoAsync(IApplicationDbContext db, IContabilidadParaTesoreria contabilidad, OwnBankAccount c, CancellationToken ct)
    {
        var banco = await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == c.BankId).Select(b => new { b.PublicId, b.Name, b.TransferCode }).FirstAsync(ct);
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == c.BranchId).Select(b => new { b.PublicId, b.Name }).FirstAsync(ct);
        var tercero = c.BankThirdPartyPersonId is { } t
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == t).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var vinculo = await contabilidad.VinculoAsync(c.PublicId, ct);
        return new OwnBankAccountDto(c.PublicId, c.Code, c.Name, banco.PublicId, banco.Name, banco.TransferCode, c.AccountNumber, c.AccountType, oficina.PublicId,
            oficina.Name, c.Purposes, c.IsGmfExempt, tercero, c.Status, c.ValidFrom, c.ValidTo, vinculo?.ChartAccountPublicId, vinculo?.Code, c.LastStatementBalance,
            c.LastStatementDate);
    }

    /// <summary>La cuenta por su <c>PublicId</c> si está al alcance; si no, nula (la ruta responde <c>Generic.NotFound</c>, como ante lo inexistente).</summary>
    public static async Task<OwnBankAccount?> VisibleAsync(IApplicationDbContext db, IAlcanceDeTesoreria alcance, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.OwnBankAccounts : db.OwnBankAccounts.AsNoTracking();
        var cuenta = await consulta.FirstOrDefaultAsync(c => c.PublicId == publicId, ct);
        return cuenta is not null && (await alcance.ObtenerAsync(ct)).IncluyeCuenta(cuenta.Id) ? cuenta : null;
    }

    /// <summary>El fondo de liquidez no admite otro propósito (FR-207); sin propósitos no hay cuenta.</summary>
    public static Result Propositos(BankAccountPurpose propositos)
    {
        if (propositos == BankAccountPurpose.None) return Result.Failure(ErroresDeTesoreria.SinPropositos());
        if (propositos.HasFlag(BankAccountPurpose.LiquidityFund) && propositos != BankAccountPurpose.LiquidityFund)
            return Result.Failure(ErroresDeTesoreria.FondoDeLiquidezExclusivo());
        return Result.Success();
    }
}

/// <summary><c>GET /api/treasury/bank-accounts</c> (<c>Treasury.BankAccounts.View</c>): las cuentas propias al alcance. (nuevo)</summary>
public sealed record ListOwnBankAccountsQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<OwnBankAccountDto>>>;

public sealed class ListOwnBankAccountsQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IContabilidadParaTesoreria contabilidad)
    : IRequestHandler<ListOwnBankAccountsQuery, Result<IReadOnlyList<OwnBankAccountDto>>>
{
    public async Task<Result<IReadOnlyList<OwnBankAccountDto>>> Handle(ListOwnBankAccountsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.OwnBankAccounts.AsNoTracking();
        if (!request.IncludeInactive) consulta = consulta.Where(c => c.Status == TreasuryCatalogStatus.Active);
        var cuentas = (await consulta.OrderBy(c => c.Code).ToListAsync(ct)).Where(c => mio.IncluyeCuenta(c.Id)).ToList();
        var lista = new List<OwnBankAccountDto>(cuentas.Count);
        foreach (var c in cuentas) lista.Add(await CuentasPropias.DtoAsync(db, contabilidad, c, ct));
        return Result.Success<IReadOnlyList<OwnBankAccountDto>>(lista);
    }
}

/// <summary><c>GET /api/treasury/bank-accounts/{id}</c>. (nuevo)</summary>
public sealed record GetOwnBankAccountQuery(Guid PublicId) : IRequest<Result<OwnBankAccountDto>>;

public sealed class GetOwnBankAccountQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IContabilidadParaTesoreria contabilidad)
    : IRequestHandler<GetOwnBankAccountQuery, Result<OwnBankAccountDto>>
{
    public async Task<Result<OwnBankAccountDto>> Handle(GetOwnBankAccountQuery request, CancellationToken ct) =>
        await CuentasPropias.VisibleAsync(db, alcance, request.PublicId, seguir: false, ct) is { } c
            ? Result.Success(await CuentasPropias.DtoAsync(db, contabilidad, c, ct))
            : Result.Failure<OwnBankAccountDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria"));
}

/// <summary>
/// <c>GET /api/treasury/bank-accounts/chart-accounts?bank=</c> (<c>Treasury.BankAccounts.Manage</c>, nuevo): las cuentas bancarias del plan que puede representar
/// una cuenta propia, por <c>IContabilidadParaTesoreria</c> (Tesorería no lee el plan). (nuevo)
/// </summary>
public sealed record ListChartBankAccountsQuery(Guid? BankPublicId) : IRequest<Result<IReadOnlyList<CuentaContableBancariaDto>>>;

public sealed class ListChartBankAccountsQueryHandler(IContabilidadParaTesoreria contabilidad)
    : IRequestHandler<ListChartBankAccountsQuery, Result<IReadOnlyList<CuentaContableBancariaDto>>>
{
    public async Task<Result<IReadOnlyList<CuentaContableBancariaDto>>> Handle(ListChartBankAccountsQuery request, CancellationToken ct) =>
        Result.Success(await contabilidad.CuentasBancariasDelPlanAsync(request.BankPublicId, ct));
}

/// <summary>
/// <c>POST /api/treasury/bank-accounts</c> (<c>Treasury.BankAccounts.Manage</c>): da de alta una cuenta propia y, si se indica, su vínculo con la cuenta contable
/// bancaria en el <b>mismo guardado</b> (<c>VincularCuentaBancariaAsync</c>; <c>Accounting.OwnBankAccount.AlreadyLinked</c>). El banco tiene que tener
/// <c>TransferCode</c>; la cuenta no se registra dos veces (UK banco y número); el código sigue <c>CodigoDeCatalogo</c>. (nuevo)
/// </summary>
public sealed record CreateOwnBankAccountCommand(
    string Code,
    string Name,
    Guid BankPublicId,
    string AccountNumber,
    OwnBankAccountType AccountType,
    Guid BranchPublicId,
    BankAccountPurpose Purposes,
    bool IsGmfExempt,
    Guid? BankThirdPartyPersonPublicId,
    Guid? ChartAccountPublicId,
    DateOnly? ValidFrom = null) : IRequest<Result<OwnBankAccountDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateOwnBankAccountCommandValidator : AbstractValidator<CreateOwnBankAccountCommand>
{
    public CreateOwnBankAccountCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Indique el código.").MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre de la cuenta.").MaximumLength(120);
        RuleFor(x => x.BankPublicId).NotEmpty().WithMessage("Indique el banco.");
        RuleFor(x => x.AccountNumber).NotEmpty().WithMessage("Indique el número de la cuenta.").MaximumLength(30).Matches("^[0-9-]+$")
            .WithMessage("El número de la cuenta sólo admite dígitos y guiones.");
        RuleFor(x => x.AccountType).IsInEnum();
        RuleFor(x => x.BranchPublicId).NotEmpty().WithMessage("Indique la oficina.");
    }
}

public sealed class CreateOwnBankAccountCommandHandler(IApplicationDbContext db, IContabilidadParaTesoreria contabilidad, IDateTimeService reloj)
    : IRequestHandler<CreateOwnBankAccountCommand, Result<OwnBankAccountDto>>
{
    public async Task<Result<OwnBankAccountDto>> Handle(CreateOwnBankAccountCommand request, CancellationToken ct)
    {
        var propositos = CuentasPropias.Propositos(request.Purposes);
        if (propositos.IsFailure) return Result.Failure<OwnBankAccountDto>(propositos.Error);
        var banco = await db.Banks.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == request.BankPublicId, ct);
        if (banco is null) return Result.Failure<OwnBankAccountDto>(ErroresDeTesoreria.NoEncontrado("El banco"));
        if (string.IsNullOrWhiteSpace(banco.TransferCode)) return Result.Failure<OwnBankAccountDto>(ErroresDeTesoreria.BancoSinCodigo(banco.Name));
        var oficina = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId, ct);
        if (oficina is null) return Result.Failure<OwnBankAccountDto>(ErroresDeTesoreria.NoEncontrado("La oficina"));
        int? tercero = null;
        if (request.BankThirdPartyPersonPublicId is { } t)
        {
            tercero = await db.People.AsNoTracking().Where(p => p.PublicId == t).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (tercero is null) return Result.Failure<OwnBankAccountDto>(ErroresDeTesoreria.NoEncontrado("La persona del banco"));
        }
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (await db.OwnBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.Code == codigo, ct) is { } mismoCodigo)
            return Result.Failure<OwnBankAccountDto>(CodigoDeCatalogo.Duplicado(CuentasPropias.Catalogo, codigo, mismoCodigo.Name, mismoCodigo.PublicId));
        var numero = request.AccountNumber.Trim();
        if (await db.OwnBankAccounts.AsNoTracking().IgnoreQueryFilters().AnyAsync(c => c.BankId == banco.Id && c.AccountNumber == numero, ct))
            return Result.Failure<OwnBankAccountDto>(ErroresDeTesoreria.CuentaRepetida(numero));

        var desde = request.ValidFrom ?? reloj.HoyLocal;
        var cuenta = new OwnBankAccount
        {
            Code = codigo, Name = request.Name.Trim(), BankId = banco.Id, AccountNumber = numero, AccountType = request.AccountType, BranchId = oficina.Id,
            Purposes = request.Purposes, IsGmfExempt = request.IsGmfExempt, BankThirdPartyPersonId = tercero, ValidFrom = desde,
        };
        db.OwnBankAccounts.Add(cuenta);
        if (request.ChartAccountPublicId is { } contable)
        {
            var vinculo = await contabilidad.VincularCuentaBancariaAsync(cuenta.PublicId, contable, desde, ct);
            if (vinculo.IsFailure) return Result.Failure<OwnBankAccountDto>(vinculo.Error);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await CuentasPropias.DtoAsync(db, contabilidad, cuenta, ct));
    }
}

/// <summary>
/// <c>PUT /api/treasury/bank-accounts/{id}</c> (<c>Treasury.BankAccounts.Manage</c>): nombre, oficina, propósitos, exención de GMF, el banco como tercero y el
/// vínculo contable (un vínculo nuevo cierra el anterior la víspera). El banco y el número no cambian: es otra cuenta. (nuevo)
/// </summary>
public sealed record UpdateOwnBankAccountCommand(
    Guid PublicId,
    string Name,
    Guid BranchPublicId,
    BankAccountPurpose Purposes,
    bool IsGmfExempt,
    Guid? BankThirdPartyPersonPublicId,
    Guid? ChartAccountPublicId) : IRequest<Result<OwnBankAccountDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateOwnBankAccountCommandValidator : AbstractValidator<UpdateOwnBankAccountCommand>
{
    public UpdateOwnBankAccountCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre de la cuenta.").MaximumLength(120);
        RuleFor(x => x.BranchPublicId).NotEmpty().WithMessage("Indique la oficina.");
    }
}

public sealed class UpdateOwnBankAccountCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IContabilidadParaTesoreria contabilidad,
    IDateTimeService reloj) : IRequestHandler<UpdateOwnBankAccountCommand, Result<OwnBankAccountDto>>
{
    public async Task<Result<OwnBankAccountDto>> Handle(UpdateOwnBankAccountCommand request, CancellationToken ct)
    {
        var cuenta = await CuentasPropias.VisibleAsync(db, alcance, request.PublicId, seguir: true, ct);
        if (cuenta is null) return Result.Failure<OwnBankAccountDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria"));
        var propositos = CuentasPropias.Propositos(request.Purposes);
        if (propositos.IsFailure) return Result.Failure<OwnBankAccountDto>(propositos.Error);
        var oficina = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId, ct);
        if (oficina is null) return Result.Failure<OwnBankAccountDto>(ErroresDeTesoreria.NoEncontrado("La oficina"));
        int? tercero = null;
        if (request.BankThirdPartyPersonPublicId is { } t)
        {
            tercero = await db.People.AsNoTracking().Where(p => p.PublicId == t).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (tercero is null) return Result.Failure<OwnBankAccountDto>(ErroresDeTesoreria.NoEncontrado("La persona del banco"));
        }
        cuenta.Name = request.Name.Trim();
        cuenta.BranchId = oficina.Id;
        cuenta.Purposes = request.Purposes;
        cuenta.IsGmfExempt = request.IsGmfExempt;
        cuenta.BankThirdPartyPersonId = tercero;
        if (request.ChartAccountPublicId is { } contable)
        {
            var vinculo = await contabilidad.VincularCuentaBancariaAsync(cuenta.PublicId, contable, reloj.HoyLocal, ct);
            if (vinculo.IsFailure) return Result.Failure<OwnBankAccountDto>(vinculo.Error);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await CuentasPropias.DtoAsync(db, contabilidad, cuenta, ct));
    }
}

/// <summary><c>POST /api/treasury/bank-accounts/{id}/inactivate</c> (<c>Treasury.BankAccounts.Manage</c>): cierra la vigencia hoy; nunca se borra. (nuevo)</summary>
public sealed record InactivateOwnBankAccountCommand(Guid PublicId) : IRequest<Result<OwnBankAccountDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class InactivateOwnBankAccountCommandValidator : AbstractValidator<InactivateOwnBankAccountCommand>
{
    public InactivateOwnBankAccountCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class InactivateOwnBankAccountCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IContabilidadParaTesoreria contabilidad,
    IDateTimeService reloj) : IRequestHandler<InactivateOwnBankAccountCommand, Result<OwnBankAccountDto>>
{
    public async Task<Result<OwnBankAccountDto>> Handle(InactivateOwnBankAccountCommand request, CancellationToken ct)
    {
        var cuenta = await CuentasPropias.VisibleAsync(db, alcance, request.PublicId, seguir: true, ct);
        if (cuenta is null) return Result.Failure<OwnBankAccountDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria"));
        if (cuenta.Status == TreasuryCatalogStatus.Inactive) return Result.Success(await CuentasPropias.DtoAsync(db, contabilidad, cuenta, ct));
        cuenta.Status = TreasuryCatalogStatus.Inactive;
        cuenta.ValidTo = reloj.HoyLocal;
        await db.SaveChangesAsync(ct);
        return Result.Success(await CuentasPropias.DtoAsync(db, contabilidad, cuenta, ct));
    }
}

/// <summary>
/// <c>GET /api/treasury/bank-accounts/{id}/balances?date=</c> (<c>Treasury.BankBalances.View</c>; lectura sensible auditada <c>Treasury.BankBalances.Read</c>,
/// FR-103): el saldo en libros (Contabilidad), el del último extracto y el disponible proyectado = libros − lotes aprobados no enviados − cheques en tránsito +
/// remesas en tránsito. Desde la segunda ronda del 2026-10-27 (D-i) cada cheque y cada remesa va en su línea con su fecha esperada, y un cheque sólo resta si los
/// libros no lo reflejan ya (<see cref="IngenIA365ERP.Domain.Treasury.Saldos.DisponibleProyectado"/>). (nuevo)
/// </summary>
public sealed record GetOwnBankAccountBalancesQuery(Guid PublicId, DateOnly? Date = null) : IRequest<Result<OwnBankAccountBalancesDto>>;

public sealed class GetOwnBankAccountBalancesQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IContabilidadParaTesoreria contabilidad, ParametrosDeTesoreria parametros,
    AuditoriaDeTesoreria auditoria, IDateTimeService reloj) : IRequestHandler<GetOwnBankAccountBalancesQuery, Result<OwnBankAccountBalancesDto>>
{
    public async Task<Result<OwnBankAccountBalancesDto>> Handle(GetOwnBankAccountBalancesQuery request, CancellationToken ct)
    {
        var cuenta = await CuentasPropias.VisibleAsync(db, alcance, request.PublicId, seguir: false, ct);
        if (cuenta is null) return Result.Failure<OwnBankAccountBalancesDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria"));
        var fecha = request.Date ?? reloj.HoyLocal;
        var libros = await contabilidad.SaldoEnLibrosAsync(cuenta.PublicId, fecha, ct);
        var aprobados = await db.PaymentBatches.AsNoTracking()
            .Where(b => b.BankAccountId == cuenta.Id && (b.Status == PaymentBatchState.Approved || b.Status == PaymentBatchState.FileGenerated))
            .SumAsync(b => (decimal?)b.Total, ct) ?? 0m;
        var cheques = await ChequesEnTransitoAsync(cuenta.Id, fecha, ct);
        var remesas = await RemesasEnTransitoAsync(cuenta.Id, fecha, ct);
        var c = IngenIA365ERP.Domain.Treasury.Saldos.DisponibleProyectado.Calcular(libros, aprobados, cheques, remesas);
        await auditoria.SaldosBancariosLeidosAsync([cuenta.PublicId], fecha, ct);
        return Result.Success(new OwnBankAccountBalancesDto(cuenta.PublicId, cuenta.Code, fecha, libros, cuenta.LastStatementBalance, cuenta.LastStatementDate,
            aprobados, c.ChecksInTransit, c.RemittancesInTransit, c.ProjectedAvailable, c.ChecksNotInBooks, cheques, remesas));
    }

    /// <summary>
    /// Segunda ronda del 2026-10-27 (D-i; FR-103): los cheques girados de la cuenta en tránsito a la fecha (emitidos hasta ese día y sin pagar ni anular), cada uno con su
    /// fecha esperada (entrega o emisión + <c>Cheques.DiasSinCobrar</c>) y si los libros ya lo reflejan: sí, si su emisión está contabilizada a la fecha (o es de la carga
    /// de puesta en marcha, cuyo saldo al corte es el de libros); no, si el cierre del mes lo devolvió al banco ese día o ya pasó a cuenta por pagar.
    /// </summary>
    private async Task<IReadOnlyList<IngenIA365ERP.Domain.Treasury.Saldos.ChequeEnTransito>> ChequesEnTransitoAsync(int cuenta, DateOnly fecha, CancellationToken ct)
    {
        var filas = await (from c in db.IssuedChecks.AsNoTracking()
                           join k in db.Checkbooks.AsNoTracking() on c.CheckbookId equals k.Id
                           where k.BankAccountId == cuenta && c.IssuedOn <= fecha && c.Amount > 0m
                                 && (c.Status == IssuedCheckState.Issued || c.Status == IssuedCheckState.Delivered
                                     || (c.Status == IssuedCheckState.Cashed && c.CashedOn > fecha))
                           select c).ToListAsync(ct);
        if (filas.Count == 0) return [];
        var dias = await parametros.EnteroAsync(IngenIA365ERP.Domain.Receipts.Parameters.ParametrosDeRecaudoYTesoreria.ChequesDiasSinCobrar, fecha, ct);
        var emitidos = await contabilidad.ContabilizadosAsync(IngenIA365ERP.Application.Common.Integration.Contracts.Treasury.ChequeGiradoEmitidoV1.Type,
            filas.Where(c => !c.IsOpeningLoad).Select(c => c.PublicId).ToList(), fecha, ct);
        var reclasificados = new HashSet<Guid>();
        foreach (var corrida in await db.MonthEndCheckRuns.AsNoTracking().Where(r => r.BankAccountId == cuenta && r.CutoffDate == fecha).ToListAsync(ct))
            foreach (var l in System.Text.Json.JsonSerializer.Deserialize<List<Checkbooks.MonthEndCheckLineDto>>(corrida.DetailJson) ?? [])
                if (l.Treatment == Checkbooks.CierreDelMesDeCheques.Reclasificado) reclasificados.Add(l.IssuedCheckPublicId);
        return filas.OrderBy(c => c.IssuedOn).ThenBy(c => c.CheckNumber).Select(c =>
        {
            var enLibros = (c.IsOpeningLoad || emitidos.Contains(c.PublicId)) && !reclasificados.Contains(c.PublicId)
                           && !(c.MovedToPayableOn is { } p && p <= fecha);
            var esperado = dias is { } d ? (c.DeliveredOn ?? c.IssuedOn).AddDays(d) : (DateOnly?)null;
            return new IngenIA365ERP.Domain.Treasury.Saldos.ChequeEnTransito(c.PublicId, c.CheckNumber, c.Amount, c.IssuedOn, esperado, enLibros);
        }).ToList();
    }

    /// <summary>Segunda ronda (D-i): las remesas de la cuenta en tránsito a la fecha; de bóveda a banco suman, de banco a bóveda restan.</summary>
    private async Task<IReadOnlyList<IngenIA365ERP.Domain.Treasury.Saldos.RemesaEnTransito>> RemesasEnTransitoAsync(int cuenta, DateOnly fecha, CancellationToken ct)
    {
        var filas = await db.CashRemittances.AsNoTracking()
            .Where(r => r.BankAccountId == cuenta && r.DispatchDate <= fecha
                        && (r.Status == RemittanceState.InTransit || (r.Status == RemittanceState.Confirmed && r.ConfirmedOn > fecha)))
            .OrderBy(r => r.DispatchDate).ThenBy(r => r.Id).ToListAsync(ct);
        if (filas.Count == 0) return [];
        var dias = await parametros.EnteroAsync(IngenIA365ERP.Domain.Receipts.Parameters.ParametrosDeRecaudoYTesoreria.RemesasDiasParaConfirmar, fecha, ct);
        return filas.Select(r => new IngenIA365ERP.Domain.Treasury.Saldos.RemesaEnTransito(r.PublicId, r.Number,
            r.Direction == RemittanceDirection.VaultToBank ? r.Amount : -r.Amount, r.DispatchDate, dias is { } d ? r.DispatchDate.AddDays(d) : null)).ToList();
    }
}
