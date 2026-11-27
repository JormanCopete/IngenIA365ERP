using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;

namespace IngenIA365ERP.Domain.Entities.Lending.Payoffs;

/// <summary>
/// <c>LND_CreditApplicationPayoffs</c> (feature 028, R2; data-model): un producto que la solicitud recoge con su desembolso, con lo que costaría
/// recogerlo a la fecha proyectada de desembolso (D) con la información hasta R —valor, desglose por concepto, cuota causada y su envío, modo de
/// intereses y huella de la liquidación—. Mientras la solicitud es editable se reescribe entera al guardarla: las filas anteriores quedan de baja
/// lógica y nacen las nuevas (no es un movimiento: el Principio XI no aplica). Al aprobarse queda como referencia de lo autorizado; el desembolso la
/// compara con lo de ese momento (<see cref="DisbursementPayoff"/>). Las filas que copió la migración desde <c>PayoffRequest</c> no tienen proyección:
/// valor cero, desglose vacío y huella vacía (<see cref="TieneProyeccion"/>). (nuevo)
/// </summary>
public class CreditApplicationPayoff : AuditableEntity
{
    /// <summary>«sha256:» y los 64 hexadecimales de la huella de la liquidación.</summary>
    public const int LargoDeLaHuella = 71;

    /// <summary><c>Corrido</c> o <c>CuotaCompleta</c>, los valores de <c>Pagos.InteresesAlPagarAntesDelVencimiento</c>.</summary>
    public const int LargoDelModoDeIntereses = 20;

    public int CreditApplicationId { get; init; }

    public PayoffProductKind ProductKind { get; init; }

    /// <summary>Con <see cref="PayoffProductKind.Loan"/>: el crédito que se recoge.</summary>
    public int? LoanId { get; init; }

    /// <summary>Con <see cref="PayoffProductKind.Account"/>: la cuenta de aportes o ahorros.</summary>
    public int? AccountId { get; init; }

    /// <summary>Con <see cref="PayoffProductKind.CommercialReceivable"/>: el documento cruce de la factura a crédito de Comercial.</summary>
    public string? CommercialReceivableReference { get; init; }

    /// <summary>El número visible del producto, copiado para el informe.</summary>
    public string ProductNumber { get; init; } = string.Empty;

    public PayoffMode Mode { get; init; }

    /// <summary>El valor pedido en una recogida parcial o un abono; nulo en las demás.</summary>
    public decimal? RequestedAmount { get; init; }

    /// <summary>Sólo créditos: el tratamiento de la cuota causada ya resuelto (el parámetro o la elección del asesor con «Preguntar»).</summary>
    public InstallmentTreatment? InstallmentTreatment { get; init; }

    /// <summary>Sólo créditos: el modo de intereses resuelto a D (<c>Corrido</c> o <c>CuotaCompleta</c>); nulo sin proyección.</summary>
    public string? InterestMode { get; init; }

    /// <summary>D: la fecha proyectada de desembolso.</summary>
    public DateOnly ProjectedDate { get; init; }

    /// <summary>R: hasta cuándo se tomó la información (pagos, causación, mora).</summary>
    public DateTime RecordedAsOf { get; init; }

    /// <summary>Lo que se recogería a D con la información hasta R; cero sin proyección.</summary>
    public decimal ProjectedAmount { get; init; }

    /// <summary>JSON de <see cref="DesgloseDeRecogida"/>: lo proyectado por concepto.</summary>
    public string ProjectedBreakdown { get; init; } = "{}";

    /// <summary>Sólo créditos: la cuota causada a D (número y valor).</summary>
    public int? CurrentInstallmentNumber { get; init; }

    public decimal? CurrentInstallmentAmount { get; init; }

    /// <summary>Sólo créditos: en qué va la cuota causada frente a su canal.</summary>
    public SentInstallmentState SentInstallmentState { get; init; }

    /// <summary>El archivo de libranza en que va la cuota causada, si está enviada a una pagaduría (<c>LND_PayrollDeductionFiles</c>).</summary>
    public int? PayrollDeductionFileId { get; init; }

    /// <summary>La huella de la liquidación (<c>sha256:</c>…); vacía sin proyección.</summary>
    public string ProjectionSha256 { get; init; } = string.Empty;

    /// <summary>El orden de aplicación, el de la pantalla (desde 1).</summary>
    public int Order { get; init; }

    /// <summary>Si la fila trae la proyección (las copiadas de <c>PayoffRequest</c> no la traen).</summary>
    public bool TieneProyeccion => ProjectionSha256.Length > 0;

    /// <summary>
    /// Una recogida proyectada. El modo, la cuota causada y su envío sólo aplican a créditos; el valor pedido sólo a parciales y abonos, y
    /// positivo.
    /// </summary>
    public static CreditApplicationPayoff Proyectar(int solicitudId, ProductoRecogido producto, PayoffMode modo, decimal? valorPedido, int orden,
        ProyeccionDeRecogida proyeccion)
    {
        ArgumentNullException.ThrowIfNull(producto);
        ArgumentNullException.ThrowIfNull(proyeccion);
        ArgumentException.ThrowIfNullOrWhiteSpace(proyeccion.Huella);
        if (solicitudId <= 0) throw new ArgumentOutOfRangeException(nameof(solicitudId));
        if (orden <= 0) throw new ArgumentOutOfRangeException(nameof(orden));
        if (proyeccion.Huella.Length > LargoDeLaHuella)
            throw new ArgumentOutOfRangeException(nameof(proyeccion), $"La huella pasa de {LargoDeLaHuella} caracteres.");
        if (proyeccion.ModoDeIntereses is { Length: > LargoDelModoDeIntereses })
            throw new ArgumentOutOfRangeException(nameof(proyeccion), $"El modo de intereses pasa de {LargoDelModoDeIntereses} caracteres.");
        ValorPedido(modo, valorPedido);
        var esCredito = producto.Clase == PayoffProductKind.Loan;
        if (!esCredito && (proyeccion.TratamientoDeCuota is not null || proyeccion.CuotaCausada is not null
                           || proyeccion.EstadoDeEnvio != SentInstallmentState.None || proyeccion.ArchivoDeLibranzaId is not null))
            throw new ArgumentException("La cuota causada y su envío sólo aplican a la recogida de un crédito.", nameof(proyeccion));

        return new CreditApplicationPayoff
        {
            CreditApplicationId = solicitudId,
            ProductKind = producto.Clase,
            LoanId = producto.LoanId,
            AccountId = producto.AccountId,
            CommercialReceivableReference = producto.CommercialReceivableReference,
            ProductNumber = producto.Numero,
            Mode = modo,
            RequestedAmount = modo is PayoffMode.Partial or PayoffMode.Deposit ? valorPedido : null,
            InstallmentTreatment = proyeccion.TratamientoDeCuota,
            InterestMode = proyeccion.ModoDeIntereses,
            ProjectedDate = proyeccion.FechaProyectada,
            RecordedAsOf = proyeccion.InformacionHasta,
            ProjectedAmount = proyeccion.Desglose.Total,
            ProjectedBreakdown = proyeccion.Desglose.Serializar(),
            CurrentInstallmentNumber = proyeccion.CuotaCausada?.Numero,
            CurrentInstallmentAmount = proyeccion.CuotaCausada?.Valor,
            SentInstallmentState = proyeccion.EstadoDeEnvio,
            PayrollDeductionFileId = proyeccion.ArchivoDeLibranzaId,
            ProjectionSha256 = proyeccion.Huella,
            Order = orden,
        };
    }

    /// <summary>Lo proyectado por concepto.</summary>
    public DesgloseDeRecogida Desglose() => DesgloseDeRecogida.Leer(ProjectedBreakdown);

    /// <summary>El valor pedido va sólo en un parcial o un abono, y positivo.</summary>
    internal static void ValorPedido(PayoffMode modo, decimal? valor)
    {
        if (!Enum.IsDefined(modo)) throw new ArgumentOutOfRangeException(nameof(modo), modo, "Modo de recogida desconocido.");
        if (modo is PayoffMode.Partial or PayoffMode.Deposit && valor is not > 0m)
            throw new ArgumentOutOfRangeException(nameof(valor), "Una recogida parcial o un abono lleva un valor mayor que cero.");
    }
}
