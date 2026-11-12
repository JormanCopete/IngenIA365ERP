using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// Lo que exige la mora de un deudor para venderle a crédito (decisión del dueño, 2026-11-12: «tres modos por empresa y línea» y «el bloqueo
/// automático de 30 días sigue mandando»). Viaja en <see cref="EstadoCrediticioDto.ArrearsAction"/>. (nuevo)
/// </summary>
public static class AccionesPorMora
{
    /// <summary>Sin mora, o con días dentro de la tolerancia <c>Cupo.MoraMaximaParaVender</c>: se vende sin más trámite.</summary>
    public const string None = "None";

    /// <summary>Política «Advertir»: se vende sólo si quien cobra confirma, en el panel de pago, que vio los días de mora.</summary>
    public const string Confirm = "Confirm";

    /// <summary>Política «Aprobacion»: se vende sólo con la aprobación de un supervisor presente con su segundo factor.</summary>
    public const string Approve = "Approve";

    /// <summary>Política «Bloquear», o mora desde <c>Bloqueos.DiasDeMoraParaBloquear</c>, o un tope duro (bloqueo, listas, autorización).</summary>
    public const string Blocked = "Blocked";
}

/// <summary>Los valores de <c>Cupo.PoliticaDeMoraParaVender</c> (Cartera, por empresa y por línea). (nuevo)</summary>
public static class PoliticasDeMoraParaVender
{
    public const string Bloquear = "Bloquear";
    public const string Advertir = "Advertir";
    public const string Aprobacion = "Aprobacion";

    public static IReadOnlyList<string> Todas { get; } = [Bloquear, Advertir, Aprobacion];
}

/// <summary>
/// La regla <b>única</b> de la venta a crédito con mora (nuevo). La usan la consulta de Cartera (<c>ConsultasDeCartera.EstadoCrediticioAsync</c>) y la
/// reserva del número (<c>ReservasDeCartera.ReservarAsync</c>): antes cada una repetía su comparación con <c>Cupo.MoraMaximaParaVender</c>. Es pura:
/// recibe lo que ya se leyó (bloqueo vigente, días de mora, tolerancia, política, umbral del bloqueo automático) y la evidencia que trae la venta.
/// <list type="number">
/// <item>un bloqueo vigente del deudor (cualquier motivo, también el automático por mora), una coincidencia en listas o la falta de autorización de
/// datos son <b>topes duros</b>: <see cref="AccionesPorMora.Blocked"/> sea cual sea la política;</item>
/// <item>sin mora, <see cref="AccionesPorMora.None"/>;</item>
/// <item>con días desde <c>Bloqueos.DiasDeMoraParaBloquear</c> (mayor que cero), <see cref="AccionesPorMora.Blocked"/> aunque el ciclo diario todavía no
/// haya creado el bloqueo: el bloqueo automático manda;</item>
/// <item>con días hasta la tolerancia <c>Cupo.MoraMaximaParaVender</c>, <see cref="AccionesPorMora.None"/> (la venta sigue como siempre: «en mora,
/// dentro de lo que Cartera admite»);</item>
/// <item>con más días: la política decide —<c>Bloquear</c> (el defecto) bloquea, <c>Advertir</c> pide la confirmación de quien cobra y
/// <c>Aprobacion</c> la aprobación de un supervisor presente—.</item>
/// </list>
/// El cupo excedido es el otro tope duro: lo comparan quien consulta y la reserva con el disponible, fuera de esta regla.
/// </summary>
public static class ReglaDeMoraParaVender
{
    /// <summary>El método de la huella de la aprobación (<see cref="Huella"/>).</summary>
    public const string MetodoDeLaHuella = "Inventory.Sales.CreditInArrears";

    /// <summary>La acción que exige la mora (<see cref="AccionesPorMora"/>).</summary>
    /// <param name="topeDuro">Un bloqueo vigente, una coincidencia en listas o la falta de autorización de datos.</param>
    /// <param name="dias">La mayor altura de mora de los créditos vivos del deudor a la fecha.</param>
    /// <param name="tolerancia"><c>Cupo.MoraMaximaParaVender</c> (de la línea, o el general).</param>
    /// <param name="politica"><c>Cupo.PoliticaDeMoraParaVender</c> (de la línea, o el general); vacía o desconocida = <c>Bloquear</c>.</param>
    /// <param name="diasParaBloquear"><c>Bloqueos.DiasDeMoraParaBloquear</c>; nulo o cero = sin bloqueo automático.</param>
    public static string Accion(bool topeDuro, int dias, int tolerancia, string? politica, int? diasParaBloquear)
    {
        if (topeDuro) return AccionesPorMora.Blocked;
        if (dias <= 0) return AccionesPorMora.None;
        if (diasParaBloquear is { } umbral && umbral > 0 && dias >= umbral) return AccionesPorMora.Blocked;
        if (dias <= Math.Max(0, tolerancia)) return AccionesPorMora.None;
        return politica?.Trim() switch
        {
            PoliticasDeMoraParaVender.Advertir => AccionesPorMora.Confirm,
            PoliticasDeMoraParaVender.Aprobacion => AccionesPorMora.Approve,
            _ => AccionesPorMora.Blocked,
        };
    }

    /// <summary>
    /// ¿La venta procede con la evidencia que trae? <c>None</c> siempre; <c>Confirm</c> si quien cobra confirmó al menos los días de hoy (o hay una
    /// aprobación válida, que pesa más); <c>Approve</c> sólo con la aprobación válida; <c>Blocked</c> nunca.
    /// </summary>
    public static bool Permite(string accion, int dias, int? diasConfirmados, bool aprobada) => accion switch
    {
        AccionesPorMora.None => true,
        AccionesPorMora.Confirm => aprobada || diasConfirmados is { } c && c >= dias,
        AccionesPorMora.Approve => aprobada,
        _ => false,
    };

    /// <summary>
    /// La huella de lo que aprueba el supervisor presente: la persona, la clase del medio, el valor, la línea, la fecha de la operación y los días de
    /// mora. Si cambia cualquiera de ellos (otro valor, otra línea, otra fecha), la aprobación ya no vale y hay que pedir otra.
    /// </summary>
    public static string Huella(Guid persona, PaymentMeansClass clase, decimal monto, string? linea, DateOnly fecha, int dias) =>
        HuellaDeOperacion.Calcular(MetodoDeLaHuella, new
        {
            person = persona,
            meansClass = clase.ToString(),
            amount = decimal.Round(monto, 2),
            line = string.IsNullOrWhiteSpace(linea) ? string.Empty : linea.Trim().ToUpperInvariant(),
            operationDate = fecha,
            daysPastDue = dias,
        });

    /// <summary>
    /// ¿La solicitud <paramref name="solicitud"/> es una aprobación vigente de <b>esta</b> venta con mora? De la fuente
    /// <see cref="ApprovalSourceTypes.SaleCreditInArrears"/>, del sujeto <see cref="ApprovalSubjects.CreditInArrears"/>, aprobada y con la misma huella.
    /// Lo preguntan la confirmación de la venta y la reserva de Cartera: la pantalla no puede saltársela.
    /// </summary>
    public static async Task<bool> AprobacionVigenteAsync(IApplicationDbContext db, Guid? solicitud, string huella, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (solicitud is not { } id || id == Guid.Empty) return false;
        var fila = await db.ApprovalRequests.AsNoTracking()
            .Where(r => r.PublicId == id && r.SourceType == ApprovalSourceTypes.SaleCreditInArrears && r.Subject == ApprovalSubjects.CreditInArrears)
            .Select(r => new { r.Status, r.ContentSha256 })
            .FirstOrDefaultAsync(ct);
        return fila is { Status: ApprovalRequestStatus.Approved } && string.Equals(fila.ContentSha256, huella, StringComparison.OrdinalIgnoreCase);
    }
}
