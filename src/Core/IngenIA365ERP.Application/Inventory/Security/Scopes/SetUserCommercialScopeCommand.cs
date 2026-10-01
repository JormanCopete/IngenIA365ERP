using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Security.Scopes;

/// <summary>Una bodega pedida en el reemplazo (§16.3). (nuevo)</summary>
public sealed record WarehouseScopeInput(Guid WarehousePublicId, bool IsDefault);

/// <summary>Un punto de venta pedido en el reemplazo (§16.3, desde I3). (nuevo)</summary>
public sealed record PointOfSaleScopeInput(Guid PointOfSalePublicId, bool IsDefault);

/// <summary>
/// Reemplaza el alcance comercial de un usuario (feature 012, T35, T090; contracts/api.md §16.3,
/// <c>PUT /api/inventory/scopes/users/{userPublicId}</c>, <c>Inventory.Scopes.Manage</c>, con <c>Idempotency-Key</c>).
/// Las asignaciones retiradas quedan de baja lógica y todo se audita. <see cref="PointsOfSale"/> nulo no toca los
/// puntos (I3). Única implementación del reemplazo: la pantalla (fase 11) sólo lo llama. (nuevo)
/// </summary>
public sealed record SetUserCommercialScopeCommand(
    Guid UserPublicId,
    IReadOnlyList<WarehouseScopeInput> Warehouses,
    IReadOnlyList<PointOfSaleScopeInput>? PointsOfSale)
    : IRequest<Result<UserCommercialScopeDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

/// <summary>
/// La forma: usuario, lista de bodegas presente, sin identificadores vacíos ni repetidos. Lo que depende de la base
/// (existencia, alcance de quien administra, una sola por defecto) lo responde el handler con su código.
/// </summary>
public sealed class SetUserCommercialScopeCommandValidator : AbstractValidator<SetUserCommercialScopeCommand>
{
    public SetUserCommercialScopeCommandValidator()
    {
        RuleFor(x => x.UserPublicId).NotEmpty();
        RuleFor(x => x.Warehouses).NotNull().WithMessage("Indicá las bodegas (una lista vacía lo deja sin bodegas).");
        RuleForEach(x => x.Warehouses).Must(w => w.WarehousePublicId != Guid.Empty).WithMessage("Cada bodega lleva su identificador.");
        RuleFor(x => x.Warehouses)
            .Must(ws => ws is null || ws.Select(w => w.WarehousePublicId).Distinct().Count() == ws.Count)
            .WithMessage("Una bodega aparece dos veces.");
        RuleForEach(x => x.PointsOfSale).Must(p => p.PointOfSalePublicId != Guid.Empty).WithMessage("Cada punto de venta lleva su identificador.");
        RuleFor(x => x.PointsOfSale)
            .Must(ps => ps is null || ps.Select(p => p.PointOfSalePublicId).Distinct().Count() == ps.Count)
            .WithMessage("Un punto de venta aparece dos veces.");
    }
}

/// <summary>
/// El reemplazo, por los puertos. En orden, para cada clase: a lo sumo una por defecto
/// (<c>Inventory.Scope.DefaultDuplicate</c>), cada pedida existe y está en el <see cref="IAlcanceDeInventario"/> de quien
/// administra (si no, el mismo 404 de la bodega o el punto), y lo que el usuario ya tenía <b>fuera</b> de ese alcance se
/// conserva —quien administra no lo ve, así que no puede estar quitándolo—, sin la marca de por defecto si la lista
/// pedida trae una. Guarda una vez, al final; la idempotencia, la transacción y la auditoría las ponen los behaviors.
/// </summary>
public sealed class SetUserCommercialScopeCommandHandler(
    IApplicationDbContext db,
    IAsignacionesDeBodega bodegas,
    IAsignacionesDePuntoDeVenta puntos,
    IAlcanceDeInventario alcanceDeLaPeticion,
    VistaDeAlcanceComercial vista)
    : IRequestHandler<SetUserCommercialScopeCommand, Result<UserCommercialScopeDto>>
{
    public async Task<Result<UserCommercialScopeDto>> Handle(SetUserCommercialScopeCommand request, CancellationToken ct)
    {
        if (await vista.UsuarioAsync(request.UserPublicId, ct) is not { } usuario)
            return Result.Failure<UserCommercialScopeDto>(Error.NotFound);

        // Antes de I3 no hay tabla de puntos de venta (SinAsignacionesDePuntoDeVenta): pedir puntos es un cuerpo
        // inválido, no un 404 (T408, §16.3 «pointsOfSale desde I3»). Una lista vacía no pide nada.
        if (request.PointsOfSale is { Count: > 0 } && puntos is SinAsignacionesDePuntoDeVenta)
            return Result.Failure<UserCommercialScopeDto>("Validation.Invalid",
                "Los puntos de venta se asignan cuando esté disponible el punto de venta (entrega I3).");

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        var deBodegas = await ReemplazarAsync(
            "warehouse",
            request.Warehouses.Select(w => (w.WarehousePublicId, w.IsDefault)).ToList(),
            alcance.IncluyeBodega,
            ErroresDeAlcance.BodegaInexistente(),
            bodegas.BuscarAsync,
            id => bodegas.BodegasDelUsuarioAsync(id, ct),
            (id, lista) => bodegas.ReemplazarAsync(id, lista, ct),
            usuario.Id,
            ct);
        if (deBodegas.IsFailure) return Result.Failure<UserCommercialScopeDto>(deBodegas.Error);

        if (request.PointsOfSale is { } pedidos)
        {
            var dePuntos = await ReemplazarAsync(
                "pointOfSale",
                pedidos.Select(p => (p.PointOfSalePublicId, p.IsDefault)).ToList(),
                alcance.IncluyePunto,
                ErroresDeAlcance.PuntoInexistente(),
                puntos.BuscarAsync,
                id => puntos.PuntosDelUsuarioAsync(id, ct),
                (id, lista) => puntos.ReemplazarAsync(id, lista, ct),
                usuario.Id,
                ct);
            if (dePuntos.IsFailure) return Result.Failure<UserCommercialScopeDto>(dePuntos.Error);
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(await vista.ArmarAsync(usuario.Id, usuario.Dto, ct));
    }

    private static async Task<Result> ReemplazarAsync(
        string clase,
        IReadOnlyList<(Guid PublicId, bool IsDefault)> pedidas,
        Func<int, bool> enAlcance,
        Error inexistente,
        Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlyDictionary<Guid, ElementoDeAlcance>>> buscar,
        Func<int, Task<AsignacionesDeAlcance>> vigentes,
        Func<int, IReadOnlyList<AsignacionPedida>, Task<Result>> reemplazar,
        int userId,
        CancellationToken ct)
    {
        if (pedidas.Count(p => p.IsDefault) > 1) return Result.Failure(ErroresDeAlcance.PorDefectoRepetido(clase));

        var encontradas = await buscar(pedidas.Select(p => p.PublicId).ToList(), ct);
        var nuevas = new List<AsignacionPedida>(pedidas.Count);
        foreach (var (publicId, porDefecto) in pedidas)
        {
            if (!encontradas.TryGetValue(publicId, out var elemento) || !enAlcance(elemento.Id)) return Result.Failure(inexistente);
            nuevas.Add(new AsignacionPedida(elemento.Id, porDefecto));
        }

        var pedidaPorDefecto = nuevas.Any(n => n.IsDefault);
        var conservadas = (await vigentes(userId)).Items
            .Where(a => !enAlcance(a.Elemento.Id))
            .Select(a => new AsignacionPedida(a.Elemento.Id, a.IsDefault && !pedidaPorDefecto));

        return await reemplazar(userId, [.. conservadas, .. nuevas]);
    }
}
