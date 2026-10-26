using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Architecture.Tests.Helpers;
using MediatR;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0002 (R-12, R-37; FR-005, FR-172; contracts/api.md §1.2): toda escritura de Recaudo y Tesorería lleva
/// <c>Idempotency-Key</c>. El comando lo declara implementando <c>IOperacionIdempotente</c>; sin el marcador <c>IdempotencyBehavior</c> lo deja
/// pasar y un doble clic en la caja cobra dos veces. La caja reusa la misma clave al reconfirmar tras <c>DistributionChanged</c> o tras una
/// aprobación presente (FR-172). Molde: <see cref="LosComandosDeInventarioLlevanClave"/>, aquí por reflexión sobre los espacios de nombres.
///
/// <para>
/// Árbol que recorre: los <c>*Command</c> de <see cref="RecaudoYTesoreria.NamespacesDeRecaudo"/>, de la Tesorería nueva (sin lo heredado,
/// R-04) y de las piezas de plataforma de la 015 (<see cref="EspaciosDePlataforma"/>: cuentas bancarias de la persona y lista de go/no-go,
/// E-06). <see cref="Exentos"/> nace vacía: un <c>*Command</c> que sea una consulta por POST se nombra ahí con su ruta.
/// </para>
/// </summary>
public class LosComandosDeRecaudoYTesoreriaLlevanClave
{
    /// <summary>Las piezas de plataforma que nacen con la 015 (E-06) y cuyas escrituras también llevan clave.</summary>
    private static readonly string[] EspaciosDePlataforma =
    [
        "IngenIA365ERP.Application.Core.PersonBankAccounts",
        "IngenIA365ERP.Application.Common.GoLive",
    ];

    /// <summary>Comandos que no escriben (consultas por POST), por nombre de tipo, con su ruta en el comentario.</summary>
    private static readonly string[] Exentos =
    [
        // Corrida del 2026-10-26: las descargas son GET (api.md §10, §6): no llevan clave y repetirlas no cambia nada —sólo la primera deja su marca—, como el
        // "/download-link" de Cartera. GET /api/treasury/payment-files/{id}/download y GET /api/receipts/payroll-files/{id}/file.
        "DownloadPaymentFileCommand", "DownloadPayrollDeductionFileCommand",
    ];

    [Fact]
    public void Toda_escritura_de_Recaudo_y_Tesoreria_lleva_clave_de_idempotencia()
    {
        var comandos = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Command", StringComparison.Ordinal))
            .Where(t => RecaudoYTesoreria.EsDeRecaudo(t.Namespace) || RecaudoYTesoreria.EsDeTesoreria(t.Namespace)
                        || EspaciosDePlataforma.Any(e => t.Namespace == e || t.Namespace?.StartsWith(e + ".", StringComparison.Ordinal) == true))
            .Where(EsPeticion)
            .Where(t => !EsConsumidorDeLaBandeja(t))
            .Where(t => !Exentos.Contains(t.Name, StringComparer.Ordinal))
            .ToList();

        var sinClave = comandos.Where(t => !typeof(IOperacionIdempotente).IsAssignableFrom(t)).Select(t => t.FullName!).ToList();
        ReglaSobreFuente.SinInfracciones(sinClave, "Comandos de Recaudo o Tesorería sin IOperacionIdempotente (FR-005, api.md §1.2)");
    }

    private static bool EsPeticion(Type t) =>
        typeof(IBaseRequest).IsAssignableFrom(t);

    /// <summary>
    /// Los consumidores de la bandeja (<c>Consume…Command</c>, que responden <c>Result&lt;ResultadoDeConsumo&gt;</c>) no son escrituras de pantalla: los envía el
    /// destino desde el despachador, sin HTTP ni clave, y su idempotencia es la de la bandeja (el recibo del mensaje; repetir la entrega no hace nada). Corrida del
    /// 2026-10-26: la regla los contaba por llamarse <c>*Command</c>.
    /// </summary>
    private static bool EsConsumidorDeLaBandeja(Type t) =>
        typeof(IRequest<IngenIA365ERP.Application.Common.Models.Result<IngenIA365ERP.Application.Common.Integration.ResultadoDeConsumo>>).IsAssignableFrom(t);
}
