using System.Globalization;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Schedules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Payments;

/// <summary>
/// El informativo <c>ABONOCAP</c> al deudor (feature 013, US5, T0554; D24, quickstart §3.12): cada vez que un abono sin interacción usa su preferencia
/// vigente, la mensajería de K1 (<see cref="ColaDeMensajesAlDeudor"/>) le cuenta cuánto se aplicó, cómo (reducir plazo o cuota), la cuota y el plazo
/// nuevos, y que puede cambiar la preferencia. Propósito <c>Informative</c>, por correo. Un aviso nunca tumba el pago: sin plantilla, fuera de
/// ventana o con cualquier falla, queda en el log. (nuevo)
/// </summary>
public sealed class AvisoDeAbonoPorMensajeria(ColaDeMensajesAlDeudor cola, ILogger<AvisoDeAbonoPorMensajeria>? logger = null) : IAvisoDeAbonoConPreferencia
{
    public const string CodigoDePlantilla = "ABONOCAP";

    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public async Task AvisarAsync(Loan credito, LoanOperation pago, decimal aplicado, PrepaymentChoice eleccion, PlanDePagos? plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credito);
        ArgumentNullException.ThrowIfNull(pago);
        try
        {
            var es = new CultureInfo("es-CO");
            var r = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(credito.DebtorPersonId, credito.Id, DebtorMessagePurpose.Informative,
                MessageChannel.Email, CodigoDePlantilla, "LoanOperation", pago.PublicId.ToString("N"), ActorKind.Person, pago.ActorUserId)
            {
                OriginPublicId = pago.PublicId,
                Valores = new Dictionary<string, string?>
                {
                    ["ValorAplicado"] = aplicado.ToString("N2", es),
                    ["ModalidadAplicada"] = eleccion == PrepaymentChoice.ReduceTerm ? "reducir el plazo" : "reducir la cuota",
                    ["NuevaCuota"] = plan?.CuotaNivelada?.ToString("N2", es) ?? plan?.Cuotas.FirstOrDefault(c => c.Vencimiento > pago.ValueDate)?.Total.ToString("N2", es),
                    ["NuevoPlazo"] = plan?.Cuotas.Count.ToString(CultureInfo.InvariantCulture),
                },
            }, ct);
            if (r.IsFailure)
                _log.LogInformation("[Cartera.AvisoDeAbono] No se encoló el aviso del abono {Pago}: {Codigo} {Mensaje}", pago.Number, r.Error.Code, r.Error.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Cartera.AvisoDeAbono] El aviso del abono {Pago} falló.", pago.Number);
        }
    }
}
