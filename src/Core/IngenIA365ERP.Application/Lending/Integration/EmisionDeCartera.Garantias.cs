using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Una garantía tal como la emite <c>GarantiaConstituida</c> (mensajes.md §10.24; A3, C15): el movimiento (<c>Constituted</c>, <c>Revalued</c>,
/// <c>Released</c>, <c>Extinguished</c>, <c>Executed</c>), la variación con signo, el valor que queda y el sobre (deudor, sucursal contable de la
/// oficina, línea, crédito si ya existe y fecha contable). (nuevo)
/// </summary>
public sealed record GarantiaAEmitir(
    Collateral Garantia,
    string Movimiento,
    decimal Variacion,
    decimal ValorDespues,
    Guid DeudorPublicId,
    Guid SucursalPublicId,
    string CodigoDeLinea,
    Guid? CreditoPublicId,
    string? NumeroDelCredito,
    DateOnly FechaContable,
    DateTime RegistradoEn);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de una garantía (mensajes.md §3: <c>Collateral</c>).</summary>
    public const string ClaseGarantia = "Collateral";

    /// <summary>Los movimientos de <c>GarantiaConstituida</c> (mensajes.md §10.24).</summary>
    public static class MovimientosDeGarantia
    {
        public const string Constituida = "Constituted";
        public const string Revaluada = "Revalued";
        public const string Liberada = "Released";
        public const string Extinguida = "Extinguished";
        public const string Ejecutada = "Executed";
    }

    /// <summary>El número con que la garantía viaja como origen y sujeto: no tiene numerador propio, así que es su <c>PublicId</c> abreviado.</summary>
    public static string NumeroDeGarantia(Collateral g) => "GAR-" + g.PublicId.ToString("N")[..12].ToUpperInvariant();

    /// <summary>
    /// Con A3 (C15; mensajes.md §3, §10.24; T0450): <c>GarantiaConstituida</c> con origen la garantía (clase <c>Collateral</c>), una línea con la
    /// variación con signo y <c>reasonCode = CollateralReceived</c>, modo del tipo (<c>NoPasa</c> por defecto). La constitución lleva la clave
    /// <c>Confirmation</c>; cada movimiento siguiente sobre la misma garantía (revaluar, extinguir, ejecutar, liberar) lleva
    /// <c>Confirmation:{movimiento:N}</c> con un identificador propio, porque la bandeja admite un solo mensaje por origen, tipo y clave. Sin la
    /// fila del tipo en <c>LND_FactTypes</c> (A3 rechazado o sin sembrar), una variación en cero o el modo sin resolver, no emite. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage?> GarantiaConstituidaAsync(GarantiaAEmitir g, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(g);
        if (g.Variacion == 0m) return null;
        var hayTipo = await db.LendingFactTypes.AsNoTracking().AnyAsync(f => f.Code == GarantiaConstituidaV1.Type && !f.IsDeleted, ct);
        if (!hayTipo) return null;
        var modo = await modoDePaso.ResolverAsync(GarantiaConstituidaV1.Type, g.FechaContable, ct);
        if (modo.IsFailure) return null;

        var numero = NumeroDeGarantia(g.Garantia);
        var contenido = new GarantiaConstituidaV1
        {
            DebtorPersonPublicId = g.DeudorPublicId,
            Loan = g.CreditoPublicId is { } credito ? new LoanRefV1 { LoanPublicId = credito, LoanNumber = g.NumeroDelCredito ?? string.Empty } : null,
            Subject = new ContingentSubjectV1 { Kind = ClaseGarantia, PublicId = g.Garantia.PublicId, Number = numero },
            ValueDate = g.FechaContable,
            RegisteredAt = new DateTimeOffset(g.RegistradoEn, TimeSpan.Zero),
            Dimensions = null,
            CreditLineCode = g.CodigoDeLinea,
            Movement = g.Movimiento,
            Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = g.Variacion, ReasonCode = "CollateralReceived" }],
            BalanceAfter = g.ValorDespues,
        };
        var clave = g.Movimiento == MovimientosDeGarantia.Constituida ? ClavesDeEvento.Confirmacion : ClavesDeEvento.ConfirmacionPor(Guid.NewGuid());
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, g.Garantia.PublicId, ClaseGarantia, "GAR-", numero, g.FechaContable, g.SucursalPublicId,
                PersonPublicId: g.DeudorPublicId),
            clave, [contenido], modo.Value), ct);
        return mensajes[0];
    }
}
