using System.Diagnostics;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// Audita todo comando (Principio X). Desde la feature 012 (T36, T37; T059, T060, T062):
///
/// <list type="bullet">
/// <item>el módulo sale de <see cref="ModuloDeAuditoria.Inferir"/>, la misma inferencia del interceptor;</item>
/// <item>el evento lleva actor (persona o proceso), IP, User-Agent, endpoint, canal (<c>web</c>, <c>app</c>,
/// <c>pos</c>, <c>proceso</c>), origen, motivo (<see cref="IConMotivo"/>) y clave de operación en la
/// metadata (<see cref="AuditoriaEncadenada.ContextoAsync"/>);</item>
/// <item>un <c>Result.IsFailure</c> queda como <c>Rejected</c> con su <c>Error.Code</c> (antes quedaba como
/// un éxito con el nombre del comando);</item>
/// <item>en los <b>módulos encadenados</b> el evento no va a Mongo: se agrega a <c>COR_AuditOutbox</c> dentro
/// de la transacción del comando (la de <c>IdempotencyBehavior</c>, o una <see cref="TransaccionExplicita"/>
/// propia), y un rechazo se escribe por un contexto aparte que sobrevive al rollback.</item>
/// </list>
///
/// <para>
/// Los servicios nuevos se piden al contenedor sólo cuando hacen falta: este behavior envuelve también el
/// login, que corre sin cooperativa. Los comandos de <c>Navigation</c> no se auditan aquí: su handler
/// escribe su propia fila (<see cref="AuditoriaEncadenada.RegistrarAsync"/>).
/// </para>
/// </summary>
public class AuditBehavior<TRequest, TResponse>(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<AuditBehavior<TRequest, TResponse>> logger,
    IServiceProvider? servicios = null)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Only audit Commands (writes), not Queries (reads)
    private static readonly bool IsCommand = typeof(TRequest).Name.EndsWith("Command");

    private static readonly string Modulo = ModuloDeAuditoria.Inferir(typeof(TRequest).Namespace);

    /// <summary>Ingresar a una opción registra su propia fila en el handler.</summary>
    private static readonly bool SeRegistraSolo = Modulo == ModuloDeAuditoria.Navigation;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!IsCommand || SeRegistraSolo)
            return await next(cancellationToken);

        var requestName = typeof(TRequest).Name;
        var (action, entityType) = ParseRequestName(requestName);
        var contexto = await AuditoriaEncadenada.ContextoAsync(servicios, currentUserService, request, cancellationToken);

        var flujo = AuditoriaEncadenada.EsEncadenado(Modulo) ? AuditoriaEncadenada.Flujo(contexto.TenantId) : null;
        var db = flujo is null ? null : servicios?.GetService<IApplicationDbContext>();

        return db is null
            ? await PorMongoAsync(request, next, contexto, action, entityType, cancellationToken)
            : await EncadenadoAsync(request, next, contexto, db, flujo!, action, entityType, cancellationToken);
    }

    // ------------------------------------------------------ no encadenados --

    private async Task<TResponse> PorMongoAsync(
        TRequest request, RequestHandlerDelegate<TResponse> next, ContextoDeAuditoria contexto,
        string action, string entityType, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await next(ct);
            sw.Stop();

            await auditService.LogAsync(AuditoriaEncadenada.ParaMongo(contexto,
                Resultado(request, response, action, entityType, sw.ElapsedMilliseconds)), ct);

            logger.LogDebug("Audit: {RequestName} completed in {Duration}ms by user {UserId}",
                requestName, sw.ElapsedMilliseconds, currentUserService.UserId);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            await auditService.LogAsync(AuditoriaEncadenada.ParaMongo(contexto, Excepcion(request, ex, sw.ElapsedMilliseconds)), ct);
            throw;
        }
    }

    // --------------------------------------------------------- encadenados --

    private async Task<TResponse> EncadenadoAsync(
        TRequest request, RequestHandlerDelegate<TResponse> next, ContextoDeAuditoria contexto,
        IApplicationDbContext db, string flujo, string action, string entityType, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        TResponse response;
        try
        {
            response = await TransaccionExplicita.EjecutarAsync(db, async () =>
            {
                var r = await next(ct);
                if (r is Result { IsFailure: true }) return r;

                var evento = Resultado(request, r, action, entityType, sw.ElapsedMilliseconds);
                db.AuditOutbox.Add(AuditoriaEncadenada.Entrada(flujo, AuditoriaEncadenada.Evento(contexto, evento)));
                await db.SaveChangesAsync(ct);
                return r;
            }, ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            await RechazoAsync(flujo, contexto, Excepcion(request, ex, sw.ElapsedMilliseconds), ct);
            throw;
        }

        sw.Stop();
        if (response is Result { IsFailure: true })
            await RechazoAsync(flujo, contexto, Resultado(request, response, action, entityType, sw.ElapsedMilliseconds), ct);

        return response;
    }

    /// <summary>
    /// El rechazo se escribe por otra conexión (T37): la transacción del comando se revierte, y con ella se
    /// iría la fila si fuera por el mismo contexto. Si no se puede, va a Mongo: un rechazo no se pierde.
    /// </summary>
    private async Task RechazoAsync(string flujo, ContextoDeAuditoria contexto, AuditLogCommand evento, CancellationToken ct)
    {
        var fabrica = servicios?.GetService<ITenantDbContextFactory>();
        if (fabrica is not null)
        {
            try
            {
                await using var aparte = fabrica.AbrirLaDelAmbito();
                aparte.Db.AuditOutbox.Add(AuditoriaEncadenada.Entrada(flujo, AuditoriaEncadenada.Evento(contexto, evento)));
                await aparte.Db.SaveChangesAsync(CancellationToken.None);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Auditoria.RechazoSinOutbox] No se pudo escribir el rechazo de {Comando} en COR_AuditOutbox; va directo a Mongo.",
                    typeof(TRequest).Name);
            }
        }

        await auditService.LogAsync(AuditoriaEncadenada.ParaMongo(contexto, evento), ct);
    }

    // ------------------------------------------------------------- eventos --

    /// <summary>Éxito o <c>Rejected</c> con <c>ErrorCode</c>, según el <see cref="Result"/>.</summary>
    private static AuditLogCommand Resultado(TRequest request, TResponse response, string action, string entityType, long duracion)
    {
        if (response is Result { IsFailure: true } fallo)
        {
            return new AuditLogCommand
            {
                Action = AuditEventTypes.CommandRejected,
                EntityType = typeof(TRequest).Name,
                Module = Modulo,
                NewValues = request,
                DurationMs = duracion,
                Metadata = new Dictionary<string, string>
                {
                    ["ErrorCode"] = fallo.Error.Code,
                    ["ErrorMessage"] = fallo.Error.Message.Length > 500 ? fallo.Error.Message[..500] : fallo.Error.Message,
                },
            };
        }

        return new AuditLogCommand
        {
            Action = action,
            EntityType = entityType,
            Module = Modulo,
            NewValues = request,
            DurationMs = duracion,
            HttpStatusCode = 200,
        };
    }

    private static AuditLogCommand Excepcion(TRequest request, Exception ex, long duracion) => new()
    {
        Action = AuditEventTypes.CommandFailed,
        EntityType = typeof(TRequest).Name,
        Module = Modulo,
        NewValues = request,
        DurationMs = duracion,
        HttpStatusCode = 500,
        Metadata = new Dictionary<string, string>
        {
            ["Error"] = ex.Message,
            ["ExceptionType"] = ex.GetType().Name,
        },
    };

    private static (string Action, string EntityType) ParseRequestName(string name)
    {
        // "CreateJournalEntryCommand" → Action="Create", EntityType="JournalEntry"
        // "UpdatePersonCommand" → Action="Update", EntityType="Person"
        // "DeleteLoanPortfolioCommand" → Action="Delete", EntityType="LoanPortfolio"
        // "ProcessPayrollCommand" → Action="Process", EntityType="Payroll"

        var cleanName = name.EndsWith("Command") ? name[..^7] : name;

        if (cleanName.StartsWith("Create")) return ("Create", cleanName[6..]);
        if (cleanName.StartsWith("Update")) return ("Update", cleanName[6..]);
        if (cleanName.StartsWith("Delete")) return ("Delete", cleanName[6..]);
        if (cleanName.StartsWith("Process")) return ("Process", cleanName[7..]);
        if (cleanName.StartsWith("Approve")) return ("Approve", cleanName[7..]);
        if (cleanName.StartsWith("Reject")) return ("Reject", cleanName[6..]);
        if (cleanName.StartsWith("Close")) return ("Close", cleanName[5..]);
        return (cleanName, cleanName);
    }
}
