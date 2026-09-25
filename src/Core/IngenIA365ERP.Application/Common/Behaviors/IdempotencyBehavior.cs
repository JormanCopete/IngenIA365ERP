using System.Reflection;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// Una operación de pantalla se ejecuta una sola vez por clave (feature 012, decisiones-transversales T13,
/// T14; FR-016; contracts/api.md §2.3). Sólo actúa sobre los comandos <see cref="IOperacionIdempotente"/>;
/// el resto pasa sin tocar nada.
///
/// <list type="number">
/// <item>Sin clave (<see cref="Guid.Empty"/>): <c>Operation.KeyRequired</c>, sin ejecutar.</item>
/// <item>Clave ya confirmada con la misma huella y el mismo usuario: devuelve el resultado guardado, marca
/// <see cref="EstadoDeLaOperacion"/> (la API agrega <c>Idempotent-Replayed: true</c>) y deja el evento
/// <c>Operation.Replayed</c>. Otra huella u otro usuario: <c>Operation.KeyReused</c> con
/// <c>{ operation, firstUsedAt }</c>.</item>
/// <item>Primera vez: abre <see cref="TransaccionExplicita"/>, inserta la fila de <c>COR_OperationKeys</c>,
/// ejecuta y, sólo si hubo éxito, guarda el resultado serializado en la misma transacción. Un fallo
/// revierte todo y la clave no queda: reintentar con ella vuelve a ejecutar.</item>
/// <item>Un duplicado que llega mientras el primero corre queda esperando en el índice único; cuando el
/// primero confirma, su inserción choca y aquí se lee la fila confirmada y se responde como repetición.</item>
/// </list>
///
/// <para>
/// Va después de Logging y antes de Audit (T14): la auditoría del comando queda dentro de la transacción y
/// una repetición no la vuelve a disparar. Como <c>ReintentoPorConcurrenciaBehavior</c>, pide sus servicios
/// <b>sólo</b> para los comandos marcados: este behavior envuelve también lo que corre sin cooperativa (el
/// login), y resolver la base operativa en el constructor tumbaría esas rutas.
/// </para>
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse>(
    IServiceProvider servicios,
    ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly bool EsIdempotente = typeof(IOperacionIdempotente).IsAssignableFrom(typeof(TRequest));

    private static readonly string Operacion = typeof(TRequest).Name;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!EsIdempotente)
            return await next(cancellationToken);

        var clave = ((IOperacionIdempotente)request).OperationKey;
        if (clave == Guid.Empty)
            return Fallo(ErroresDeOperacion.ClaveRequerida());

        var db = servicios.GetRequiredService<IApplicationDbContext>();
        var actor = await servicios.GetRequiredService<IActorActual>().ObtenerAsync(cancellationToken);
        var huella = HuellaDeOperacion.Calcular(Operacion, request);
        var usuarioCentral = actor.CentralUserId ?? Guid.Empty;

        var previa = await LeerAsync(db, clave, cancellationToken);
        if (previa is not null)
            return await RepetirAsync(previa, huella, usuarioCentral, cancellationToken);

        try
        {
            return await TransaccionExplicita.EjecutarAsync(db, async () =>
            {
                var fila = new OperationKey
                {
                    Key = clave,
                    Operation = Operacion.Length > 120 ? Operacion[..120] : Operacion,
                    RequestSha256 = huella,
                    CentralUserId = usuarioCentral,
                    UserId = actor.UserId,
                    ActorName = actor.Name.Length > 150 ? actor.Name[..150] : actor.Name,
                };
                db.OperationKeys.Add(fila);
                // Aquí espera el duplicado concurrente: el índice único lo retiene hasta que la otra
                // transacción confirma (y entonces choca) o revierte (y entonces sigue).
                await db.SaveChangesAsync(cancellationToken);

                var respuesta = await next(cancellationToken);
                if (respuesta is Result { IsFailure: true })
                    return respuesta;

                fila.ResultJson = Serializar(respuesta);
                await db.SaveChangesAsync(cancellationToken);
                return respuesta;
            }, cancellationToken);
        }
        catch (DbUpdateException ex) when (EsClaveRepetida(ex))
        {
            db.DescartarCambios();
            previa = await LeerAsync(db, clave, cancellationToken);
            if (previa is null) throw;
            logger.LogInformation("[Operacion.Concurrente] La clave {Clave} de {Operacion} llegó dos veces a la vez; se responde con la confirmada.",
                clave, Operacion);
            return await RepetirAsync(previa, huella, usuarioCentral, cancellationToken);
        }
    }

    private static Task<OperationKey?> LeerAsync(IApplicationDbContext db, Guid clave, CancellationToken ct) =>
        db.OperationKeys.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(k => k.Key == clave, ct);

    private async Task<TResponse> RepetirAsync(OperationKey previa, string huella, Guid usuarioCentral, CancellationToken ct)
    {
        if (previa.Operation != Operacion || previa.RequestSha256 != huella || previa.CentralUserId != usuarioCentral || previa.ResultJson is null)
        {
            logger.LogWarning("[Operacion.ClaveReutilizada] La clave {Clave} ya se usó para {OperacionPrevia} y ahora llega con {Operacion} y otro contenido o usuario.",
                previa.Key, previa.Operation, Operacion);
            return Fallo(ErroresDeOperacion.ClaveReutilizada(previa.Operation, previa.CreatedAt));
        }

        servicios.GetService<EstadoDeLaOperacion>()?.MarcarRepeticion(previa.CreatedAt);

        var auditoria = servicios.GetService<IAuditService>();
        if (auditoria is not null)
        {
            await auditoria.LogAsync(new AuditLogCommand
            {
                Action = AuditEventTypes.OperationReplayed,
                EntityType = previa.Operation,
                Module = AuditBehavior<TRequest, TResponse>.InferModuleFromNamespace(typeof(TRequest).Namespace),
                Metadata = new Dictionary<string, string>
                {
                    ["OperationKey"] = previa.Key.ToString(),
                    ["FirstUsedAt"] = DateTime.SpecifyKind(previa.CreatedAt, DateTimeKind.Utc).ToString("O"),
                },
            }, ct);
        }

        return Reconstruir(previa.ResultJson);
    }

    private static bool EsClaveRepetida(DbUpdateException ex)
    {
        for (Exception? actual = ex; actual is not null; actual = actual.InnerException)
        {
            if (actual.Message.Contains(OperationKey.IndiceUnicoDeLaClave, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------ resultado --

    /// <summary>Sólo se guarda un éxito: de <c>Result&lt;T&gt;</c> el valor, de <c>Result</c> nada («null»).</summary>
    private static string Serializar(TResponse respuesta)
    {
        if (respuesta is null || respuesta is Result && !EsResultadoConValor)
            return "null";
        if (EsResultadoConValor)
        {
            var valor = typeof(TResponse).GetProperty(nameof(Result<object>.Value))!.GetValue(respuesta);
            return JsonSerializer.Serialize(valor, TipoDelValor!, Json);
        }
        return JsonSerializer.Serialize(respuesta, typeof(TResponse), Json);
    }

    private static TResponse Reconstruir(string json)
    {
        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Success();
        if (EsResultadoConValor)
        {
            var valor = JsonSerializer.Deserialize(json, TipoDelValor!, Json);
            return (TResponse)ExitoConValor.MakeGenericMethod(TipoDelValor!).Invoke(null, [valor])!;
        }
        return JsonSerializer.Deserialize<TResponse>(json, Json)!;
    }

    private static TResponse Fallo(Error error)
    {
        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(error);
        if (EsResultadoConValor)
            return (TResponse)FalloConValor.MakeGenericMethod(TipoDelValor!).Invoke(null, [error])!;
        throw new InvalidOperationException($"{error.Code}: {error.Message}");
    }

    private static readonly bool EsResultadoConValor =
        typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>);

    private static readonly Type? TipoDelValor = EsResultadoConValor ? typeof(TResponse).GetGenericArguments()[0] : null;

    private static readonly MethodInfo ExitoConValor = typeof(Result)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Result.Success) && m.IsGenericMethodDefinition);

    private static readonly MethodInfo FalloConValor = typeof(Result)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Result.Failure) && m.IsGenericMethodDefinition
            && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Error));
}
