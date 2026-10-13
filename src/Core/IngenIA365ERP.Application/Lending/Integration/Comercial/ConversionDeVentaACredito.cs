using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.Comercial;

/// <summary>
/// Una <c>VentaACreditoRegistrada</c> v1 o v2 tal como la lee Cartera (feature 013, US6, T0588; mensajes.md §12.1, §12.2): lo común de las dos
/// versiones y, de la v2, el número reservado y la reserva. Lo arma <see cref="De"/> desde el mensaje recibido; nulo si el contenido no es de una
/// versión conocida. (nuevo)
/// </summary>
public sealed record VentaRecibida(
    MensajeEntrante Mensaje,
    string ThirdPartyKind,
    CreditPaymentV1 Pago,
    CreditTermsV1 Condiciones,
    string? CreditLineCode,
    string? SuggestedCreditLineCode,
    bool PendingValidation,
    string AccountsReceivableRecordedBy,
    string? ConsultationEvidence,
    string? ReservedLoanNumber,
    Guid? ReservationPublicId)
{
    /// <summary>La fecha de la venta (la del sobre): la de origen del crédito (FR-050) y la de su evaluación (FR-051).</summary>
    public DateOnly Fecha => Mensaje.Envelope.Origin.OperationDate;

    /// <summary>La factura (el origen del sobre).</summary>
    public Guid DocumentoPublicId => Mensaje.Envelope.Origin.PublicId;

    public string NumeroDeFactura => Mensaje.Envelope.Origin.Number;

    public static VentaRecibida? De(MensajeEntrante m) => m.Payload switch
    {
        VentaACreditoRegistradaV2 v when m.Version == 2 => new(m, v.ThirdPartyKind, v.CreditPayment, v.Terms, v.CreditLineCode, v.SuggestedCreditLineCode,
            v.PendingValidation, v.AccountsReceivableRecordedBy, v.ConsultationEvidence, v.ReservedLoanNumber, v.ReservationPublicId),
        VentaACreditoRegistradaV1 v when m.Version == 1 => new(m, v.ThirdPartyKind, v.CreditPayment, v.Terms, v.CreditLineCode, v.SuggestedCreditLineCode,
            v.PendingValidation, v.AccountsReceivableRecordedBy, v.ConsultationEvidence, null, null),
        _ => null,
    };
}

/// <summary>
/// Una venta convertible: el comprador vivo, su tipo, la línea resuelta (con la versión vigente a la fecha de la venta), la periodicidad y las
/// cuotas traducidas. (nuevo)
/// </summary>
public sealed record VentaConvertida(
    VentaRecibida Venta,
    Person Persona,
    DebtorKind TipoDeDeudor,
    CreditLine Linea,
    VersionDeLineaLeida Version,
    RepaymentPeriodicity Periodicidad,
    int Cuotas,
    int DiasDelPlazo)
{
    public decimal Monto => Venta.Pago.Amount;
}

/// <summary>
/// La conversión de una venta a crédito en algo que Cartera puede crear (feature 013, US6, T0588; FR-050, FR-053, FR-022; C95; mensajes.md §12.1):
/// <list type="bullet">
/// <item>el comprador vivo por el <c>personPublicId</c> del sobre (<c>Lending.Sale.PersonNotFound</c>);</item>
/// <item>la línea por FR-050: la del mensaje, la sugerida por el medio o la de defecto de origen comercial del tipo de comprador
/// (<c>CommercialDefaultFor</c>), la primera que existe, está activa y, a la fecha de la venta, admite el origen comercial (y, un cliente, los no
/// asociados) (<c>Lending.Sale.CreditLineInvalid</c>);</item>
/// <item>la periodicidad y la unidad de plazo por <c>LND_CommercialTermEquivalences</c> a la fecha de la venta, y la periodicidad admitida por la
/// línea (<c>Lending.Sale.PeriodicityWithoutEquivalence</c>; notas de coordinación PE, contradicción 15).</item>
/// </list>
/// La versión desconocida la responde quien llama (<c>Integration.VersionNotAccepted</c>). Lo no convertible deja el recibo <c>Rejected</c> y
/// reprocesable, sin respuesta a Comercial (C95). No escribe. (nuevo)
/// </summary>
public sealed class ConversionDeVentaACredito(IApplicationDbContext db, VersionVigenteDeLinea versiones, EquivalenciasDeComercio equivalencias)
{
    public const string Asociado = "Associate";
    public const string Cliente = "Customer";

    public async Task<Result<VentaConvertida>> ConvertirAsync(VentaRecibida venta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(venta);
        var personaPublicId = venta.Mensaje.Envelope.PersonPublicId;
        var persona = personaPublicId is { } p
            ? await db.People.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == p && !x.IsDeleted, ct)
            : null;
        if (persona is null) return Result.Failure<VentaConvertida>(ErroresDeVentasACredito.PersonaInexistente());

        var tipo = string.Equals(venta.ThirdPartyKind, Cliente, StringComparison.OrdinalIgnoreCase) ? DebtorKind.Customer
            : string.Equals(venta.ThirdPartyKind, Asociado, StringComparison.OrdinalIgnoreCase) ? DebtorKind.Associate
            : persona.IsAssociate ? DebtorKind.Associate : DebtorKind.Customer;

        var linea = await LineaAsync(venta, tipo, ct);
        if (linea is null) return Result.Failure<VentaConvertida>(ErroresDeVentasACredito.LineaInvalida(venta.CreditLineCode ?? venta.SuggestedCreditLineCode));

        var terminos = venta.Condiciones;
        var periodicidad = await equivalencias.PeriodicidadAsync(terminos.Periodicity, venta.Fecha, ct);
        if (periodicidad is not { } per || !linea.Value.Version.Version.LeerPeriodicidades().Contains(per))
            return Result.Failure<VentaConvertida>(ErroresDeVentasACredito.SinEquivalencia("periodicidad", terminos.Periodicity));
        var dias = await equivalencias.DiasPorUnidadAsync(terminos.TermUnit, venta.Fecha, ct);
        if (dias is not { } d)
            return Result.Failure<VentaConvertida>(ErroresDeVentasACredito.SinEquivalencia("unidad de plazo", terminos.TermUnit));

        var cuotas = per == RepaymentPeriodicity.SinglePayment ? 1 : Math.Max(1, terminos.Installments);
        return Result.Success(new VentaConvertida(venta, persona, tipo, linea.Value.Linea, linea.Value.Version, per, cuotas, Math.Max(0, terminos.Term) * d));
    }

    /// <summary>La línea por FR-050: la del mensaje, la sugerida, la de defecto del tipo de comprador; la primera que sirve a la fecha de la venta.</summary>
    private async Task<(CreditLine Linea, VersionDeLineaLeida Version)?> LineaAsync(VentaRecibida venta, DebtorKind tipo, CancellationToken ct)
    {
        foreach (var codigo in new[] { venta.CreditLineCode, venta.SuggestedCreditLineCode }.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct())
        {
            var normalizado = codigo!.Trim().ToUpperInvariant();
            var linea = await db.CreditLines.AsNoTracking().FirstOrDefaultAsync(l => l.Code == normalizado && !l.IsDeleted, ct);
            if (linea is not null && await SirveAsync(linea, tipo, venta.Fecha, ct) is { } version) return (linea, version);
        }

        var activas = await db.CreditLines.AsNoTracking().Where(l => !l.IsDeleted && l.Status == CreditLineStatus.Active).OrderBy(l => l.Code).ToListAsync(ct);
        if (activas.Count == 0) return null;
        var vigentes = await versiones.LeerVariasAsync(activas.Select(l => l.Id).ToList(), venta.Fecha, null, ct);
        foreach (var linea in activas)
            if (vigentes.TryGetValue(linea.Id, out var v) && v.Version.CommercialDefaultFor == tipo && Admite(v, tipo)) return (linea, v);
        return null;
    }

    private async Task<VersionDeLineaLeida?> SirveAsync(CreditLine linea, DebtorKind tipo, DateOnly fecha, CancellationToken ct)
    {
        if (linea.Status != CreditLineStatus.Active) return null;
        var version = await versiones.LeerAsync(linea.Id, fecha, null, ct);
        return version is not null && Admite(version, tipo) ? version : null;
    }

    private static bool Admite(VersionDeLineaLeida v, DebtorKind tipo) =>
        v.Version.AllowsCommercialOrigin && (tipo == DebtorKind.Associate || v.Version.AllowsNonAssociates);
}
