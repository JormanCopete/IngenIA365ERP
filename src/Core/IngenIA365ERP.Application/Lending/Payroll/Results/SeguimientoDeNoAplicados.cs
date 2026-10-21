using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Payroll.Results;

/// <summary>
/// El seguimiento de un no aplicado (feature 013, K2, T0838, T0841, T0844; FR-112, FR-115, C32; data-model §24.10): el aviso al deudor por sus
/// canales autorizados (informativo <c>NOAPLICPAG</c> por la cola de K1, <see cref="ColaDeMensajesAlDeudor"/>) y la acción —la regla del convenio,
/// automática, o la resolución de una persona con motivo—, cada una una fila de <c>LND_NonDeductionFollowUps</c>. Corre en la unidad de trabajo de
/// quien lo motiva (el consumidor de <c>ResultadoDeDescuentos</c>, <c>AplicarAsync</c> de la nómina propia, la resolución) y guarda dentro de ella:
/// el aviso necesita su Id. Un aviso nunca tumba lo demás: sin plantilla, sin canal autorizado o con Cartera sin activar, queda en el log y el
/// seguimiento sigue sin <c>DebtorNotified</c>. (nuevo)
/// </summary>
public sealed class SeguimientoDeNoAplicados(IApplicationDbContext db, ColaDeMensajesAlDeudor cola, IDateTimeService reloj,
    ILogger<SeguimientoDeNoAplicados>? logger = null)
{
    /// <summary>La plantilla del aviso de no aplicado.</summary>
    public const string PlantillaDeNoAplicado = "NOAPLICPAG";

    /// <summary>El origen de los mensajes de un no aplicado (<c>LND_DebtorMessages.OriginKind</c>).</summary>
    public const string OrigenDelMensaje = "NonDeduction";

    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    /// <summary>
    /// La regla del convenio, automática (FR-115): el aviso al deudor y la acción <paramref name="regla"/>. El no aplicado ya tiene Id (se guardó antes).
    /// Devuelve las filas de seguimiento agregadas (sin guardar la última).
    /// </summary>
    public async Task<IReadOnlyList<NonDeductionFollowUp>> AplicarReglaAsync(InstallmentNonDeduction noAplicado, int deudorId, NonDeductionFollowUpAction regla,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(noAplicado);
        var ahora = reloj.UtcNow;
        var filas = new List<NonDeductionFollowUp>();
        var aviso = await AvisarAsync(noAplicado, deudorId, regla, ActorKind.Process, null, ct);
        if (aviso is not null)
        {
            await db.SaveChangesAsync(ct);
            filas.Add(new NonDeductionFollowUp
            {
                InstallmentNonDeductionId = noAplicado.Id, Action = NonDeductionFollowUpAction.DebtorNotified, IsAutomatic = true, DebtorMessageId = aviso.Id,
                OccurredAt = ahora,
            });
        }
        filas.Add(new NonDeductionFollowUp { InstallmentNonDeductionId = noAplicado.Id, Action = regla, IsAutomatic = true, OccurredAt = ahora });
        db.NonDeductionFollowUps.AddRange(filas);
        return filas;
    }

    /// <summary>
    /// El aviso informativo al deudor por un no aplicado (por correo, con el motivo y la forma de pago que sigue). Nulo si no se pudo encolar; queda en
    /// el log. No guarda.
    /// </summary>
    public async Task<DebtorMessage?> AvisarAsync(InstallmentNonDeduction noAplicado, int deudorId, NonDeductionFollowUpAction accion, ActorKind actor,
        int? usuario, CancellationToken ct)
    {
        try
        {
            var r = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(deudorId, noAplicado.LoanId, DebtorMessagePurpose.Informative, MessageChannel.Email,
                PlantillaDeNoAplicado, OrigenDelMensaje, $"{noAplicado.PublicId:N}:{accion}", actor, actor == ActorKind.Person ? usuario : null)
            {
                OriginPublicId = noAplicado.PublicId,
                InstallmentNonDeductionId = noAplicado.Id,
                Valores = new Dictionary<string, string?>
                {
                    ["MotivoDeNoDescuento"] = Motivo(noAplicado.Reason),
                    ["FormaDePagoNueva"] = FormaDePago(accion),
                },
            }, ct);
            if (r.IsSuccess) return r.Value.Mensaje;
            _log.LogInformation("[Cartera.AvisoDeNoAplicado] No se encoló el aviso del no aplicado {NoAplicado}: {Codigo} {Mensaje}", noAplicado.PublicId,
                r.Error.Code, r.Error.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Cartera.AvisoDeNoAplicado] El aviso del no aplicado {NoAplicado} falló.", noAplicado.PublicId);
        }
        return null;
    }

    /// <summary>¿El no aplicado ya lo resolvió una persona (o quedó cerrado)? (<c>Lending.InstallmentNonDeduction.AlreadyResolved</c>.)</summary>
    public Task<bool> ResueltoAsync(int noAplicadoId, CancellationToken ct) =>
        db.NonDeductionFollowUps.AsNoTracking().AnyAsync(s => s.InstallmentNonDeductionId == noAplicadoId && !s.IsDeleted
            && (!s.IsAutomatic || s.Action == NonDeductionFollowUpAction.Closed), ct);

    /// <summary>El motivo como lo lee el deudor.</summary>
    public static string Motivo(NonDeductionReason motivo) => motivo switch
    {
        NonDeductionReason.DeductionCap => "tope de descuentos",
        NonDeductionReason.InsufficientNet => "salario neto insuficiente",
        NonDeductionReason.DeductionPriority => "prelación de otro descuento",
        NonDeductionReason.Termination => "retiro",
        NonDeductionReason.Leave => "licencia",
        NonDeductionReason.Disability => "incapacidad",
        _ => "otro motivo informado por la pagaduría",
    };

    /// <summary>La forma de pago que sigue para la cuota, según la acción.</summary>
    public static string FormaDePago(NonDeductionFollowUpAction accion) => accion switch
    {
        NonDeductionFollowUpAction.IncludedInNextFile => "el descuento del próximo período de nómina",
        NonDeductionFollowUpAction.SentToCollection => "la gestión de cobranza de la cooperativa",
        NonDeductionFollowUpAction.Closed => "los medios que le indique la cooperativa",
        _ => "caja o los canales de pago de la cooperativa",
    };
}
