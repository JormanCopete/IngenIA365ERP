using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements.Collaterals;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes;

// Feature 013, US3 (bloque 7B; T0451; FR-044, FR-045, FR-082; api.md §6.2; adaptadores §3.2, §3.8; data-model §2.9): lo que comparten los comandos
// del pagaré y la tarea cartera.pagares. (nuevo)

/// <summary>Los errores del pagaré (data-model §2.9 y §10.1; api.md §6.2). (nuevo)</summary>
public static class ErroresDePagare
{
    public const string CodigoInexistente = "Lending.PromissoryNote.NotFound";
    public const string CodigoYaExiste = "Lending.PromissoryNote.AlreadyExists";
    public const string CodigoEstadoInvalido = "Lending.PromissoryNote.InvalidState";
    public const string CodigoRechazadoPorElDeposito = "Lending.PromissoryNote.DepositoryRejected";
    public const string CodigoSinDocumento = "Lending.PromissoryNote.DocumentMissing";

    public static Error Inexistente() => new(CodigoInexistente, "El pagaré no existe.");

    public static Error YaExiste(Guid vigente, string estado) => new ErrorConDatos(CodigoYaExiste,
        "Ya hay un pagaré vivo para esta solicitud o crédito. Anúlelo antes de crear otro.", new { promissoryNotePublicId = vigente, status = estado });

    public static Error EstadoInvalido(string estado, string queHacer) => new ErrorConDatos(CodigoEstadoInvalido,
        $"El pagaré está en «{estado}»: {queHacer}", new { status = estado });

    public static Error RechazadoPorElDeposito(string canal, string? detalle) => new ErrorConDatos(CodigoRechazadoPorElDeposito,
        $"El depósito de valores ({canal}) no aceptó la operación. Revise los datos de los firmantes y vuelva a intentar.",
        new { channelCode = canal, detail = detalle });

    public static Error SinDocumento() => new(CodigoSinDocumento,
        "Adjunte primero el pagaré firmado (documento de la garantía) y registre la custodia con ese adjunto.");

    public static Error CanalNoConfigurado() => new ErrorConDatos(ErroresDeDecisionesYCupos.CodigoCanalNoConfigurado,
        "No hay un depósito de valores configurado. Configúrelo en Administración › Canales externos o use pagaré físico.",
        new { kind = nameof(ExternalChannelKind.SecuritiesDepository) });

    public static Error CanalNoDisponible(string canal, string? detalle) => new ErrorConDatos(ErroresDeOriginacion.CodigoCanalNoDisponible,
        $"El depósito de valores ({canal}) no responde. Intente más tarde.", new { channelCode = canal, retryAfter = (string?)null, detail = detalle });
}

/// <summary>
/// El depósito de valores visto desde un pagaré (feature 013, US3, T0451): el canal vigente al crear (<c>COR_ExternalChannelSettings</c>, clase
/// <c>SecuritiesDepository</c>), el contexto de la llamada (cooperativa resuelta, credencial si la pide, el <c>PublicId</c> del pagaré como clave de
/// idempotencia) y el adaptador del canal <b>sellado</b> en el pagaré, por el registro (circuito incluido). Lleva lo que responde el depósito al
/// pagaré sin retroceder su estado: lo usan actualizar, la tarea <c>cartera.pagares</c>, endosar y anular. Scoped. (nuevo)
/// </summary>
public sealed class DepositoDelPagare(
    IApplicationDbContext db,
    IDepositosDeValores depositos,
    ICredencialesDeCanalExterno credenciales,
    ICurrentTenantService tenant,
    IOptions<CanalesExternosOptions> opciones)
{
    /// <summary>El canal vigente del depósito a <paramref name="fecha"/>, o nulo si no hay uno configurado.</summary>
    public async Task<string?> CanalVigenteAsync(DateOnly fecha, CancellationToken ct)
    {
        var configurados = await db.ExternalChannelSettings.AsNoTracking()
            .Where(s => s.Kind == ExternalChannelKind.SecuritiesDepository && !s.IsDeleted).ToListAsync(ct);
        return configurados.Where(s => s.VigenteEn(fecha)).OrderByDescending(s => s.ValidFrom).Select(s => s.ChannelCode).FirstOrDefault();
    }

    /// <summary>El adaptador del canal (sellado o vigente) por el registro.</summary>
    public IDepositoDeValores Resolver(string canal) => depositos.Resolver(canal);

    /// <summary>El contexto de una llamada: la cooperativa resuelta, la credencial del canal (los que no la piden, sin ella) y la clave del pagaré.</summary>
    public async Task<Result<ContextoDeCanalExterno>> ContextoAsync(string canal, Guid pagare, CancellationToken ct)
    {
        CredencialDeCanalExterno? credencial = null;
        if (!CodigosDeCanalExterno.NoPideCredencial(canal))
        {
            var resuelta = await credenciales.ResolverAsync(canal, ct);
            if (resuelta.IsFailure) return Result.Failure<ContextoDeCanalExterno>(resuelta.Error);
            credencial = resuelta.Value;
        }
        return Result.Success(new ContextoDeCanalExterno(CooperativaDelCanal.Resuelta(tenant) ?? Guid.Empty, canal, null, credencial, pagare.ToString("N"),
            opciones.Value.AmbienteDePruebas));
    }

    /// <summary>
    /// Consulta el pagaré desmaterializado <paramref name="p"/> (seguido) al depósito de su canal sellado y lleva el estado (<see cref="Aplicar"/>).
    /// Canal caído (o en proceso sin datos): <c>Lending.Adapter.Unavailable</c> y nada cambia. Sin guardar.
    /// </summary>
    public async Task<Result<ResultadoExterno<EstadoDePagare>>> SincronizarAsync(PromissoryNote p, DateTime ahora, CancellationToken ct)
    {
        if (p.Kind != PromissoryNoteKind.Dematerialized || string.IsNullOrWhiteSpace(p.CustodianChannelCode) || string.IsNullOrWhiteSpace(p.ExternalNoteId))
            return Result.Failure<ResultadoExterno<EstadoDePagare>>(ErroresDePagare.EstadoInvalido(p.Status.ToString(),
                "sólo un pagaré desmaterializado se consulta al depósito."));
        var contexto = await ContextoAsync(p.CustodianChannelCode, p.PublicId, ct);
        if (contexto.IsFailure) return Result.Failure<ResultadoExterno<EstadoDePagare>>(contexto.Error);
        var r = await depositos.Resolver(p.CustodianChannelCode).ConsultarPagareAsync(p.ExternalNoteId, contexto.Value, ct);
        if (r.Valor is null && r.Outcome is ExternalOutcome.ChannelUnavailable or ExternalOutcome.InProcess)
            return Result.Failure<ResultadoExterno<EstadoDePagare>>(ErroresDePagare.CanalNoDisponible(r.ChannelCode, r.Detalle));
        if (r.Valor is { } estado) Aplicar(p, estado, ahora);
        p.LastSyncedAt = ahora;
        return Result.Success(r);
    }

    /// <summary>
    /// Lleva al pagaré lo que informó el depósito, sin retroceder (§3.8): firmado o en custodia anota cuándo; endosado y anulado, igual. Una firma
    /// rechazada lo deja como estaba: sin perfeccionar.
    /// </summary>
    public static void Aplicar(PromissoryNote p, EstadoDePagare estado, DateTime ahora)
    {
        if (estado.FirmaRechazada || (int)estado.Estado <= (int)p.Status) return;
        p.Status = estado.Estado;
        if (estado.Estado is PromissoryNoteStatus.Signed or PromissoryNoteStatus.InCustody) p.SignedAt ??= ahora;
        if (estado.Estado == PromissoryNoteStatus.InCustody) p.InCustodyAt ??= ahora;
        if (estado.Estado == PromissoryNoteStatus.Voided) p.VoidedAt ??= ahora;
    }

    /// <summary>El pagaré vivo por su <c>PublicId</c>, con su dueño en el alcance; fuera, el 404 del pagaré.</summary>
    public static async Task<Result<(PromissoryNote Pagare, DuenoDeGarantia Dueno)>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance,
        LendingAuditEmitter auditoria, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.PromissoryNotes : db.PromissoryNotes.AsNoTracking();
        var p = await consulta.FirstOrDefaultAsync(x => x.PublicId == publicId && !x.IsDeleted, ct);
        if (p is null) return Result.Failure<(PromissoryNote, DuenoDeGarantia)>(ErroresDePagare.Inexistente());
        var dueno = await GarantiasDeCartera.DuenoDeAsync(db, p.LoanId, p.ApplicationId, ct);
        if (dueno.IsFailure) return Result.Failure<(PromissoryNote, DuenoDeGarantia)>(ErroresDePagare.Inexistente());
        var dentro = await alcance.AsegurarAsync(dueno.Value.BranchId, dueno.Value.CreditLineId, ErroresDePagare.Inexistente(), auditoria,
            nameof(PromissoryNote), p.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<(PromissoryNote, DuenoDeGarantia)>(dentro.Error) : Result.Success((p, dueno.Value));
    }

    /// <summary>Los DTO del pagaré: dueño, perfeccionado según la guardia (con el efecto legal del canal) y documentos.</summary>
    public static async Task<IReadOnlyList<PromissoryNoteDto>> DtosAsync(IApplicationDbContext db, GuardiaDeCanalesExternos? guardia,
        IReadOnlyList<PromissoryNote> filas, CancellationToken ct, bool firmaRechazada = false)
    {
        var solicitudes = filas.Where(f => f.ApplicationId is not null).Select(f => f.ApplicationId!.Value).Distinct().ToList();
        var deSolicitud = await db.CreditApplications.AsNoTracking().Where(a => solicitudes.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => (a.PublicId, a.Number), ct);
        var creditos = filas.Where(f => f.LoanId is not null).Select(f => f.LoanId!.Value).Distinct().ToList();
        var deCredito = await db.Loans.AsNoTracking().Where(l => creditos.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => (l.PublicId, (string?)l.Number), ct);
        var ids = filas.Select(f => f.PublicId).ToList();
        var documentos = (await db.Attachments.AsNoTracking()
                .Where(a => a.OwnerEntityType == AdjuntosDeModulo.DocumentoDeGarantia && ids.Contains(a.OwnerEntityPublicId))
                .Select(a => new { a.OwnerEntityPublicId, a.PublicId }).ToListAsync(ct))
            .ToLookup(a => a.OwnerEntityPublicId, a => a.PublicId);

        return filas.Select(f =>
        {
            var dueno = f.LoanId is { } l && deCredito.TryGetValue(l, out var c)
                ? new CollateralOwnerDto(DuenosDeGarantia.Credito, c.PublicId, c.Item2)
                : f.ApplicationId is { } a && deSolicitud.TryGetValue(a, out var s)
                    ? new CollateralOwnerDto(DuenosDeGarantia.Solicitud, s.PublicId, s.Number)
                    : new CollateralOwnerDto(DuenosDeGarantia.Solicitud, Guid.Empty, null);
            var perfeccionado = guardia?.PagarePerfeccionado(f) ?? f.Perfeccionado;
            return new PromissoryNoteDto(f.PublicId, f.Kind.ToString(), f.Status.ToString(), dueno, f.Number, f.CustodianChannelCode, f.ExternalNoteId,
                f.IsSimulated, perfeccionado, f.PhysicalLocation)
            {
                IssuedOn = f.IssuedOn,
                SignedAt = f.SignedAt,
                InCustodyAt = f.InCustodyAt,
                EndorsedAt = f.EndorsedAt,
                EndorseeName = f.EndorseeName,
                VoidedAt = f.VoidedAt,
                VoidReason = f.VoidReason,
                LastSyncedAt = f.LastSyncedAt,
                SignatureRejected = firmaRechazada,
                Documents = documentos[f.PublicId].ToList(),
            };
        }).ToList();
    }
}
