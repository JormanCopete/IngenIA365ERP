using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// Los pagos de crédito cuya aprobación se está decidiendo en esta petición (feature 012, I3, T655) (nuevo). La última aprobación de un
/// crédito reentra en el flujo canónico <b>antes</b> de que el motor marque la solicitud <c>Approved</c>: esta marca le dice a
/// <see cref="AprobacionDeCredito"/> que ese pago ya está aprobado. Scoped y sin dependencias, para no cerrar el círculo motor → fuentes.
/// </summary>
public sealed class CreditosAprobadosEnCurso
{
    private readonly HashSet<Guid> _pagos = [];
    private readonly Dictionary<Guid, AprobacionEnCurso> _decisiones = [];

    public void Marcar(Guid pagoPublicId) => _pagos.Add(pagoPublicId);

    public bool Contiene(Guid pagoPublicId) => _pagos.Contains(pagoPublicId);

    /// <summary>La última decisión de un crédito, que el motor registra después de confirmar: la lee el mensaje a Cartera.</summary>
    public void Anotar(Guid pagoPublicId, AprobacionEnCurso decision) => _decisiones[pagoPublicId] = decision;

    public AprobacionEnCurso? DecisionDe(Guid pagoPublicId) => _decisiones.GetValueOrDefault(pagoPublicId);
}

/// <summary>La última aprobación de un crédito mientras se decide (solicitud, aprobador, nivel, método, fecha). (nuevo)</summary>
public sealed record AprobacionEnCurso(Guid RequestPublicId, int ApproverUserId, int Level, ApprovalMethod Method, DateTime DecidedAt);

/// <summary>
/// La aprobación del crédito provisional (feature 012, I3, T655; contracts/api.md §23.2; T32, T33; data-model §27 duda 10) (nuevo). Por
/// cada pago de crédito de la venta pide al motor (<see cref="IMotorDeAprobaciones.SolicitarAsync"/>) una solicitud con
/// <c>Subject = ProvisionalCredit</c>, <c>SourceType = DocumentPayment</c> y el monto financiado:
/// <list type="bullet">
/// <item>sin política registrada rige la regla fija del motor: un nivel con <c>Inventory.Sales.SellOnCredit</c> desde el primer peso
/// (<c>EvaluadorDePolitica.ReglaFija</c>); con política, sus niveles;</item>
/// <item>el nivel con <c>Inventory.Sales.SellOnCredit</c> lo decide quien tenga un monto máximo (<c>SEC_PermissionAmountLimits</c>) igual o
/// mayor que lo financiado (<see cref="FuenteDeAprobacionDeCredito"/>); si nadie en la cooperativa lo tiene —sin contar al cajero, al
/// creador ni a quien cobra—, <c>Inventory.Approval.AmountExceedsLimit</c> con <c>data.maxAmount</c>;</item>
/// <item>se excluyen el cajero de la sesión y quien creó el documento (y el motor excluye a quien pide);</item>
/// <item>la huella es la del pago (medio, monto, condiciones, cliente): si cambia, la pendiente se invalida y se pide otra.</item>
/// </list>
/// El documento queda <c>PendingApproval</c> sin número (lo hace <see cref="ConfirmacionDeDocumento"/>) y la última aprobación lo confirma
/// en la transacción del aprobador. Nunca guarda.
/// </summary>
public sealed class AprobacionDeCredito(IApplicationDbContext db, IMotorDeAprobaciones motor, IActorActual actorActual, CreditosAprobadosEnCurso enCurso)
{
    public const string Permiso = RegistroDePagos.PermisoCredito;

    /// <summary>La huella de lo que se aprueba de un pago de crédito.</summary>
    public static string Huella(InventoryDocument documento, DocumentPayment pago) => HuellaDeOperacion.Calcular("DocumentPayment", new
    {
        Documento = documento.PublicId,
        Pago = pago.PublicId,
        documento.CounterpartyPersonId,
        pago.PaymentMeansId,
        pago.Amount,
        pago.InstallmentCount,
        pago.CreditTermDays,
        pago.InstallmentPeriodDays,
        pago.FirstDueDate,
    });

    /// <summary>
    /// Pide (o conserva) la aprobación de cada pago de crédito que no la tenga. Devuelve la primera solicitud pendiente, o nula si todos
    /// los créditos están aprobados (o la política no exige aprobación).
    /// </summary>
    public async Task<Result<ApprovalRequest?>> SolicitarAsync(InventoryDocument documento, IReadOnlyList<DocumentPayment> pagos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var creditos = pagos.Where(p => p.EsCredito && !p.IsDeleted).ToList();
        if (creditos.Count == 0) return Result.Success<ApprovalRequest?>(null);

        var actor = await actorActual.ObtenerAsync(ct);
        var ids = creditos.Select(p => p.PublicId).ToList();
        var solicitudes = await db.ApprovalRequests
            .Where(r => r.SourceType == ApprovalSourceTypes.DocumentPayment && r.Subject == ApprovalSubjects.ProvisionalCredit && ids.Contains(r.SourcePublicId))
            .ToListAsync(ct);
        solicitudes.AddRange(db.ApprovalRequests.Local.Where(r => r.SourceType == ApprovalSourceTypes.DocumentPayment && ids.Contains(r.SourcePublicId)
            && r.Id == 0 && !solicitudes.Contains(r)));

        var participantes = await ParticipantesAsync(documento, ct);
        var tipo = documento.DocumentType?.PublicId
            ?? await db.InventoryDocumentTypes.AsNoTracking().Where(t => t.Id == documento.DocumentTypeId).Select(t => (Guid?)t.PublicId).FirstOrDefaultAsync(ct);
        var bodega = documento.WarehouseId is int b ? await db.Warehouses.AsNoTracking().Where(w => w.Id == b).Select(w => (Guid?)w.PublicId).FirstOrDefaultAsync(ct) : null;
        var punto = documento.PointOfSaleId is int pv ? await db.PointsOfSale.AsNoTracking().Where(x => x.Id == pv).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct) : null;
        var creador = documento.CreatedByUserId != 0 ? documento.CreatedByUserId : actor.UserId ?? 0;

        ApprovalRequest? primera = null;
        foreach (var pago in creditos)
        {
            if (enCurso.Contiene(pago.PublicId)) continue;
            var huella = Huella(documento, pago);
            var propias = solicitudes.Where(r => r.SourcePublicId == pago.PublicId).ToList();
            if (propias.Any(r => r.Status == ApprovalRequestStatus.Approved && Igual(r.ContentSha256, huella))) continue;
            var pendiente = propias.FirstOrDefault(r => r.Status == ApprovalRequestStatus.Pending);
            if (pendiente is not null && Igual(pendiente.ContentSha256, huella))
            {
                primera ??= pendiente;
                continue;
            }
            if (pendiente is not null) await motor.InvalidarAsync(ApprovalSourceTypes.DocumentPayment, pago.PublicId, ApprovalSubjects.ProvisionalCredit, ct);

            // Alguien, fuera de quienes intervienen, tiene que poder aprobar lo financiado (T34, §23.2).
            var excluidos = participantes.Append(creador).Concat(actor.UserId is int yo ? [yo] : []).Distinct().ToList();
            if (await LimitesDePermisoPorUsuario.MayorDeLaCooperativaAsync(db, Permiso, documento.OperationDate, excluidos, ct) is { } maximo && maximo < pago.Amount)
                return Result.Failure<ApprovalRequest?>(ErroresDeCredito.NadiePuedeAprobar(pago.Amount, maximo));

            var solicitada = await motor.SolicitarAsync(new SolicitudDeAprobacion(
                ApprovalSubjects.ProvisionalCredit,
                ApprovalSourceTypes.DocumentPayment,
                pago.PublicId,
                Etiqueta(documento, pago),
                tipo,
                bodega,
                punto,
                pago.Amount,
                documento.OperationDate,
                creador,
                participantes,
                huella,
                null), ct);
            if (solicitada.IsFailure) return Result.Failure<ApprovalRequest?>(solicitada.Error);
            if (solicitada.Value is { } nueva) primera ??= nueva;
        }
        return Result.Success(primera);
    }

    /// <summary>Quienes intervienen además de quien pide: el cajero de la sesión de la venta (T32, T33).</summary>
    private async Task<IReadOnlyCollection<int>> ParticipantesAsync(InventoryDocument documento, CancellationToken ct)
    {
        if (documento.CashSessionId is not int sesion) return [];
        var cajero = await db.CashSessions.AsNoTracking().Where(s => s.Id == sesion).Select(s => (int?)s.CashierUserId).FirstOrDefaultAsync(ct);
        return cajero is int c ? [c] : [];
    }

    private static bool Igual(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Etiqueta(InventoryDocument documento, DocumentPayment pago)
    {
        var numero = documento.Number is null ? "Borrador" : VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number);
        var texto = $"Crédito {pago.MeansCode} · {numero}";
        return texto.Length > 80 ? texto[..80] : texto;
    }
}

/// <summary>
/// La fuente de aprobación del crédito provisional (feature 012, I3, T655; <c>SourceType = DocumentPayment</c>) (nuevo):
/// <list type="bullet">
/// <item>en el nivel con <c>Inventory.Sales.SellOnCredit</c>, el aprobador necesita un monto máximo igual o mayor que lo financiado
/// (<c>Inventory.Approval.AmountExceedsLimit</c>, y la decisión no queda);</item>
/// <item>la última aprobación anota la solicitud en el pago (<c>ApprovalRequestId</c>) y reentra en el flujo canónico
/// (<see cref="ConfirmacionDeDocumento"/>, <c>PorAprobacion</c>) en la transacción del aprobador: si quedan otros créditos o la
/// política del tipo sin aprobar, el documento sigue <c>PendingApproval</c>;</item>
/// <item>el rechazo o el retiro devuelven la venta a borrador y cancelan las otras solicitudes de crédito pendientes de la misma venta.</item>
/// </list>
/// <see cref="ConfirmacionDeDocumento"/> se resuelve al aprobar, no en el constructor (el círculo motor → fuentes).
/// </summary>
public sealed class FuenteDeAprobacionDeCredito(IApplicationDbContext db, CreditosAprobadosEnCurso enCurso, IDateTimeService reloj, IServiceProvider servicios)
    : IFuenteDeAprobacion, IFuenteConAprobador
{
    public string SourceType => ApprovalSourceTypes.DocumentPayment;

    public async Task<Result> AlAprobarElNivelAsync(ApprovalRequest solicitud, int aprobadorUserId, ApprovalMethod metodo, bool esElUltimo, CancellationToken ct)
    {
        if (VistaDeSolicitudes.NivelActual(solicitud) is { PermissionCode: AprobacionDeCredito.Permiso })
        {
            var suyo = await LimitesDePermisoPorUsuario.DeAsync(db, aprobadorUserId, AprobacionDeCredito.Permiso, solicitud.OperationDate, ct);
            if (suyo is { } maximo && maximo < solicitud.Amount)
                return Result.Failure(ErroresDeCredito.AprobadorSinMonto(solicitud.Amount, maximo));
        }
        if (esElUltimo)
        {
            var pago = await db.DocumentPayments.FirstOrDefaultAsync(p => p.PublicId == solicitud.SourcePublicId, ct);
            if (pago is null) return Result.Failure(Error.NotFound);
            pago.ApprovalRequestId = solicitud.Id;
            enCurso.Anotar(pago.PublicId, new AprobacionEnCurso(solicitud.PublicId, aprobadorUserId, solicitud.CurrentLevel, metodo, reloj.UtcNow));
        }
        return Result.Success();
    }

    public async Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct)
    {
        var pago = await db.DocumentPayments.FirstOrDefaultAsync(p => p.PublicId == solicitud.SourcePublicId, ct);
        if (pago is null) return Result.Failure<EstadoDeFuenteDto>(InventoryErrors.DocumentNotFound());
        pago.ApprovalRequestId = solicitud.Id;
        enCurso.Marcar(pago.PublicId);
        var documento = await db.InventoryDocuments.AsNoTracking().Where(d => d.Id == pago.DocumentId).Select(d => new { d.PublicId, d.Class }).FirstAsync(ct);

        var confirmacion = servicios.GetRequiredService<ConfirmacionDeDocumento>();
        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(documento.PublicId, null, PorAprobacion: true), ct);
        if (confirmada.IsFailure) return Result.Failure<EstadoDeFuenteDto>(confirmada.Error);
        var r = confirmada.Value;
        return Result.Success(new EstadoDeFuenteDto(r.PublicId, documento.Class.ToString(), r.Status.ToString(), r.DisplayNumber));
    }

    public async Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct)
    {
        var pago = await db.DocumentPayments.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == solicitud.SourcePublicId, ct);
        if (pago is null) return Result.Failure<EstadoDeFuenteDto>(InventoryErrors.DocumentNotFound());
        var documento = await db.InventoryDocuments.FirstAsync(d => d.Id == pago.DocumentId, ct);
        if (documento.Status == DocumentStatus.PendingApproval) documento.DevolverABorrador();

        // Las otras solicitudes de crédito de la misma venta ya no tienen qué aprobar: la venta volvió a borrador.
        var hermanos = await db.DocumentPayments.AsNoTracking().Where(p => p.DocumentId == documento.Id && p.PublicId != pago.PublicId).Select(p => p.PublicId).ToListAsync(ct);
        foreach (var otra in await db.ApprovalRequests.Where(r => r.SourceType == ApprovalSourceTypes.DocumentPayment && hermanos.Contains(r.SourcePublicId)
                     && r.Status == ApprovalRequestStatus.Pending).ToListAsync(ct))
        {
            otra.Status = ApprovalRequestStatus.Cancelled;
            otra.DecidedAt = reloj.UtcNow;
        }
        return Result.Success(new EstadoDeFuenteDto(documento.PublicId, documento.Class.ToString(), documento.Status.ToString(),
            VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number)));
    }

    public async Task<bool> EnAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct)
    {
        var documento = await (from p in db.DocumentPayments.AsNoTracking()
                               join d in db.InventoryDocuments.AsNoTracking() on p.DocumentId equals d.Id
                               where p.PublicId == solicitud.SourcePublicId
                               select d).FirstOrDefaultAsync(ct);
        return documento is not null && FiltroDeAlcance.DocumentoVisible(alcance, documento, []);
    }

    public async Task<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>> DescribirAsync(IReadOnlyCollection<Guid> sourcePublicIds, CancellationToken ct)
    {
        var filas = await (from p in db.DocumentPayments.AsNoTracking()
                           join d in db.InventoryDocuments.AsNoTracking() on p.DocumentId equals d.Id
                           where sourcePublicIds.Contains(p.PublicId)
                           select new { p.PublicId, p.Amount, p.MeansCode, p.InstallmentCount, p.CreditTermDays, d.Class, d.Prefix, d.Number, d.OperationDate,
                               d.PointOfSaleId, d.CounterpartyPersonId, DocumentPublicId = d.PublicId })
            .ToListAsync(ct);
        var personaIds = filas.Select(f => f.CounterpartyPersonId).OfType<int>().Distinct().ToList();
        var personas = await db.People.AsNoTracking().Where(p => personaIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var puntoIds = filas.Select(f => f.PointOfSaleId).OfType<int>().Distinct().ToList();
        var puntos = await db.PointsOfSale.AsNoTracking().Where(p => puntoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        return filas.ToDictionary(f => f.PublicId, f => new OrigenDeAprobacionDto(
            f.PublicId,
            f.Class.ToString(),
            null,
            f.Number is null ? null : VistaDeDocumentos.NumeroVisible(f.Prefix, f.Number),
            f.OperationDate,
            null,
            f.PointOfSaleId is int punto ? puntos.GetValueOrDefault(punto) : null,
            $"Crédito provisional de {f.Amount:N2} con {f.MeansCode} a "
                + (f.CounterpartyPersonId is int pid && personas.TryGetValue(pid, out var persona) ? Pos.BorradorDelPos.Nombre(persona) : "—")
                + $" ({f.InstallmentCount ?? 1} cuota(s), {f.CreditTermDays ?? 0} días)",
            $"/ventas/documentos/{f.DocumentPublicId}"));
    }
}
