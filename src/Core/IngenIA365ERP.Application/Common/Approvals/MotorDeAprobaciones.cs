using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals.Transactions;
using IngenIA365ERP.Domain.Enums.Approvals;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Approvals;

/// <summary>
/// El motor de aprobaciones (feature 012, T33, T34, T083). Único escritor de <c>COR_ApprovalRequests</c> y
/// <c>COR_ApprovalDecisions</c>. Decide con <see cref="EvaluadorDePolitica"/>; la identidad de quien decide es
/// <c>SEC_Users.Id</c> de <see cref="IActorActual"/> (o del aprobador presente), nunca el entero del token ni el
/// correo (<c>LaSegregacionNoUsaUserIdDelToken</c>).
/// </summary>
public sealed class MotorDeAprobaciones(
    IApplicationDbContext db,
    IActorActual actorActual,
    IPermissionChecker permisos,
    IAlcanceDeInventario alcanceDeLaPeticion,
    ILimitesPorPermiso limites,
    IAutoridadDeOtroAprobador otroAprobador,
    IAvisosDeAprobacion avisos,
    VistaDeSolicitudes vista,
    IDateTimeService reloj) : IMotorDeAprobaciones
{
    public async Task<Result<EvaluacionConPolitica>> EvaluarAsync(
        string subject,
        Guid? documentTypePublicId,
        DateOnly fechaDeOperacion,
        decimal monto,
        string? permisoLimitado,
        CancellationToken ct)
    {
        if (!ApprovalSubjects.EsValido(subject))
            throw new ArgumentException($"«{subject}» no es un sujeto de aprobación (ApprovalSubjects).", nameof(subject));

        var candidatas = await db.ApprovalPolicies.AsNoTracking()
            .Include(p => p.Levels)
            .Where(p => p.Module == ApprovalPolicy.ModuloInventario && p.Subject == subject
                        && (p.DocumentTypePublicId == null || p.DocumentTypePublicId == documentTypePublicId))
            .ToListAsync(ct);

        var pares = candidatas.Select(p => (Entidad: p, Politica: p.ParaElEvaluador())).ToList();
        var elegida = EvaluadorDePolitica.ElegirPolitica(pares.Select(p => p.Politica), documentTypePublicId, fechaDeOperacion);
        var policyId = elegida is null ? (int?)null : pares.First(p => ReferenceEquals(p.Politica, elegida)).Entidad.Id;

        var maximo = permisoLimitado is null ? null : await limites.MontoMaximoAsync(permisoLimitado, fechaDeOperacion, ct);
        var evaluacion = EvaluadorDePolitica.Evaluar(subject, monto, elegida, maximo);

        if (evaluacion.Resultado == ResultadoDeEvaluacion.ExcedeLimite)
            return Result.Failure<EvaluacionConPolitica>(ErroresDeAprobaciones.ExcedeLimite(monto, maximo!.Value, "COP", permisoLimitado!));

        return Result.Success(new EvaluacionConPolitica(evaluacion, evaluacion.ReglaFija ? null : policyId));
    }

    public async Task<Result<ApprovalRequest?>> SolicitarAsync(SolicitudDeAprobacion solicitud, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } solicitante)
            throw new InvalidOperationException("Pedir una aprobación exige una persona resuelta en SEC_Users (IActorActual.UserId).");

        var evaluada = await EvaluarAsync(solicitud.Subject, solicitud.DocumentTypePublicId, solicitud.OperationDate,
            solicitud.Amount, solicitud.PermisoLimitado, ct);
        if (evaluada.IsFailure) return Result.Failure<ApprovalRequest?>(evaluada.Error);
        if (!evaluada.Value.Evaluacion.RequiereAprobacion) return Result.Success<ApprovalRequest?>(null);

        var niveles = evaluada.Value.Evaluacion.Niveles;
        var nueva = new ApprovalRequest
        {
            Module = ApprovalPolicy.ModuloInventario,
            Subject = solicitud.Subject,
            SourceType = solicitud.SourceType,
            SourcePublicId = solicitud.SourcePublicId,
            SourceLabel = solicitud.SourceLabel,
            ScopeWarehousePublicId = solicitud.ScopeWarehousePublicId,
            ScopePointOfSalePublicId = solicitud.ScopePointOfSalePublicId,
            Amount = solicitud.Amount,
            Currency = solicitud.Currency,
            OperationDate = solicitud.OperationDate,
            PolicyId = evaluada.Value.PolicyId,
            CreatedByUserId = solicitud.CreatedByUserId,
            RequestedByUserId = solicitante,
            Status = ApprovalRequestStatus.Pending,
            CurrentLevel = (byte)niveles[0].Order,
            ContentSha256 = solicitud.ContentSha256,
            RequestedAt = reloj.UtcNow,
        };
        nueva.SellarNiveles(niveles);
        nueva.SellarExcluidos([solicitud.CreatedByUserId, solicitante, .. solicitud.Participantes]);
        db.ApprovalRequests.Add(nueva);

        await avisos.PendienteAsync(nueva, niveles[0].PermissionCode, ct);
        return Result.Success<ApprovalRequest?>(nueva);
    }

    public async Task<Result<DecisionResultDto>> DecidirAsync(DecisionDeAprobacion decision, CancellationToken ct)
    {
        var solicitud = await db.ApprovalRequests
            .Include(r => r.Decisions)
            .FirstOrDefaultAsync(r => r.PublicId == decision.RequestPublicId, ct);
        if (solicitud is null) return Result.Failure<DecisionResultDto>(ErroresDeAprobaciones.SolicitudInexistente());

        var actor = await actorActual.ObtenerAsync(ct);
        int decisor;
        string nombre;
        if (decision.Presente is { } presente)
        {
            decisor = presente.UserId;
            nombre = presente.Name;
        }
        else if (actor.UserId is { } yo)
        {
            decisor = yo;
            nombre = actor.Name;
        }
        else
        {
            return Result.Failure<DecisionResultDto>(ErroresDeAprobaciones.SolicitudInexistente());
        }

        // Sin el permiso del nivel o sin alcance, la solicitud no existe para quien decide (404, §15.2).
        var nivel = VistaDeSolicitudes.NivelActual(solicitud);
        if (nivel is null || !await TienePermisoYAlcanceAsync(solicitud, nivel.PermissionCode, decision.Presente, ct))
            return Result.Failure<DecisionResultDto>(ErroresDeAprobaciones.SolicitudInexistente());

        if (solicitud.Status != ApprovalRequestStatus.Pending)
            return Result.Failure<DecisionResultDto>(ErroresDeAprobaciones.SolicitudNoPendiente(solicitud.Status));

        if (EvaluadorDePolitica.ValidarDecision(decisor, VistaDeSolicitudes.ParticipantesDe(solicitud)) is { } motivo)
            return Result.Failure<DecisionResultDto>(ErroresDeAprobaciones.Autoaprobacion(motivo));

        if (!string.Equals(decision.ExpectedContentSha256, solicitud.ContentSha256, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<DecisionResultDto>(ErroresDeAprobaciones.ContenidoCambiado(solicitud.ContentSha256));

        var metodo = decision.Presente?.Method ?? ApprovalMethod.OwnSession;
        var ahora = reloj.UtcNow;
        var fuente = vista.FuenteDe(solicitud.SourceType);
        var estadoDeFuente = new EstadoDeFuenteDto(solicitud.SourcePublicId, null, null, solicitud.SourceLabel);

        if (decision.Decision == ApprovalDecisionKind.Reject)
        {
            if (string.IsNullOrWhiteSpace(decision.Reason))
                return Result.Failure<DecisionResultDto>(ErroresDeAprobaciones.MotivoRequerido());

            if (fuente is not null)
            {
                var devuelta = await fuente.AlDevolverAsync(solicitud, decision.Reason.Trim(), ct);
                if (devuelta.IsFailure) return Result.Failure<DecisionResultDto>(devuelta.Error);
                estadoDeFuente = devuelta.Value;
            }

            Registrar(solicitud, nivel, decisor, nombre, metodo, decision, ahora);
            solicitud.Status = ApprovalRequestStatus.Rejected;
            solicitud.DecidedAt = ahora;
            await db.SaveChangesAsync(ct);
            return Result.Success(new DecisionResultDto(solicitud.PublicId, solicitud.Status.ToString(), solicitud.CurrentLevel, estadoDeFuente));
        }

        var siguiente = solicitud.NivelesRequeridos().FirstOrDefault(n => n.Order > nivel.Order);
        if (siguiente is not null)
        {
            Registrar(solicitud, nivel, decisor, nombre, metodo, decision, ahora);
            solicitud.Excluir(decisor);
            solicitud.CurrentLevel = (byte)siguiente.Order;
            await avisos.PendienteAsync(solicitud, siguiente.PermissionCode, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(new DecisionResultDto(solicitud.PublicId, solicitud.Status.ToString(), solicitud.CurrentLevel, estadoDeFuente));
        }

        // La última aprobación confirma en la transacción del aprobador. Primero la fuente: si falla, no se registra
        // nada y la solicitud sigue pendiente para que el creador la retire y corrija.
        if (fuente is null)
            throw new InvalidOperationException($"No hay IFuenteDeAprobacion registrada para «{solicitud.SourceType}»: nadie puede confirmar lo aprobado.");

        var confirmada = await fuente.AlAprobarAsync(solicitud, ct);
        if (confirmada.IsFailure) return Result.Failure<DecisionResultDto>(confirmada.Error);

        Registrar(solicitud, nivel, decisor, nombre, metodo, decision, ahora);
        solicitud.Excluir(decisor);
        solicitud.Status = ApprovalRequestStatus.Approved;
        solicitud.DecidedAt = ahora;
        await db.SaveChangesAsync(ct);
        return Result.Success(new DecisionResultDto(solicitud.PublicId, solicitud.Status.ToString(), solicitud.CurrentLevel, confirmada.Value));
    }

    public async Task<IReadOnlyList<ApprovalRequest>> PendientesParaMiAsync(CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } yo) return [];

        var pendientes = await db.ApprovalRequests.AsNoTracking()
            .Include(r => r.Decisions)
            .Where(r => r.Status == ApprovalRequestStatus.Pending && r.Module == ApprovalPolicy.ModuloInventario)
            .OrderBy(r => r.RequestedAt)
            .ToListAsync(ct);

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var permisosVistos = new Dictionary<string, bool>(StringComparer.Ordinal);
        var mias = new List<ApprovalRequest>();
        foreach (var s in pendientes)
        {
            if (EvaluadorDePolitica.ValidarDecision(yo, VistaDeSolicitudes.ParticipantesDe(s)) is not null) continue;
            if (VistaDeSolicitudes.NivelActual(s) is not { } nivel) continue;

            if (!permisosVistos.TryGetValue(nivel.PermissionCode, out var tiene))
                permisosVistos[nivel.PermissionCode] = tiene = await permisos.HasPermissionAsync(nivel.PermissionCode, ct);
            if (!tiene) continue;

            if (await vista.AlAlcanceAsync(s, alcance, ct)) mias.Add(s);
        }

        return mias;
    }

    public async Task<ApprovalRequest?> InvalidarAsync(string sourceType, Guid sourcePublicId, string subject, CancellationToken ct)
    {
        var pendiente = await db.ApprovalRequests.FirstOrDefaultAsync(r => r.SourceType == sourceType && r.SourcePublicId == sourcePublicId
            && r.Subject == subject && r.Status == ApprovalRequestStatus.Pending, ct);
        if (pendiente is null) return null;

        pendiente.Status = ApprovalRequestStatus.Cancelled;
        pendiente.DecidedAt = reloj.UtcNow;
        return pendiente;
    }

    public async Task<Result> RetirarAsync(Guid requestPublicId, string motivo, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var solicitud = await db.ApprovalRequests.FirstOrDefaultAsync(r => r.PublicId == requestPublicId, ct);
        if (solicitud is null || actor.UserId is not { } yo || (yo != solicitud.RequestedByUserId && yo != solicitud.CreatedByUserId))
            return Result.Failure(ErroresDeAprobaciones.SolicitudInexistente());

        if (solicitud.Status != ApprovalRequestStatus.Pending)
            return Result.Failure(ErroresDeAprobaciones.SolicitudNoPendiente(solicitud.Status));

        if (vista.FuenteDe(solicitud.SourceType) is { } fuente)
        {
            var devuelta = await fuente.AlDevolverAsync(solicitud, motivo.Trim(), ct);
            if (devuelta.IsFailure) return Result.Failure(devuelta.Error);
        }

        solicitud.Status = ApprovalRequestStatus.Cancelled;
        solicitud.DecidedAt = reloj.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Permiso del nivel y alcance de quien decide: el de la sesión o el del aprobador presente.</summary>
    private async Task<bool> TienePermisoYAlcanceAsync(ApprovalRequest solicitud, string permiso, AprobadorPresente? presente, CancellationToken ct)
    {
        if (presente is null)
        {
            return await permisos.HasPermissionAsync(permiso, ct)
                && await vista.AlAlcanceAsync(solicitud, await alcanceDeLaPeticion.ObtenerAsync(ct), ct);
        }

        return await otroAprobador.TienePermisoAsync(presente.UserId, permiso, ct)
            && await vista.AlAlcanceAsync(solicitud, await otroAprobador.AlcanceAsync(presente.UserId, ct), ct);
    }

    private void Registrar(ApprovalRequest solicitud, NivelDeAprobacion nivel, int decisor, string nombre,
        ApprovalMethod metodo, DecisionDeAprobacion decision, DateTime ahora)
    {
        var registro = new ApprovalDecision
        {
            RequestId = solicitud.Id,
            Request = solicitud,
            Level = (byte)nivel.Order,
            Decision = decision.Decision,
            DecidedByUserId = decisor,
            DecidedByName = nombre,
            DecidedAt = ahora,
            Method = metodo,
            CredentialPublicId = decision.Presente?.CredentialPublicId,
            PermissionCodeUsed = nivel.PermissionCode,
            ContentSha256 = solicitud.ContentSha256,
            Reason = string.IsNullOrWhiteSpace(decision.Reason) ? null : decision.Reason.Trim(),
        };
        solicitud.Decisions.Add(registro);
        db.ApprovalDecisions.Add(registro);
    }
}
