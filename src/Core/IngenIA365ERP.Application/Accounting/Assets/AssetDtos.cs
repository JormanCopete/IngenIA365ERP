using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Accounting.Assets;

/// <summary>Lo que se registra o se corrige de un activo fijo o diferido (feature 009 E4, US12, FR-076). Las cuentas, la sucursal, el centro y el proveedor van por PublicId.</summary>
public sealed record FixedAssetInput(
    string Code,
    string Name,
    string Kind,
    Guid AssetAccountPublicId,
    Guid? AccumulatedAccountPublicId,
    Guid ExpenseAccountPublicId,
    Guid BranchPublicId,
    Guid? CostCenterPublicId,
    Guid? SupplierPersonPublicId,
    DateOnly PurchaseDate,
    string? PurchaseDocument,
    decimal Cost,
    decimal ResidualValue,
    int LifeMonths,
    DateOnly StartDate,
    decimal OpeningAccumulated = 0m,
    int OpeningElapsedMonths = 0);

public sealed record CuentaDeActivoDto(Guid PublicId, string Code, string Name);

/// <summary>Un activo con su valor en libros a hoy: acumulada (la inicial más las cuotas contabilizadas) y neto.</summary>
public sealed record FixedAssetDto(
    Guid PublicId,
    string Code,
    string Name,
    string Kind,
    string Status,
    CuentaDeActivoDto AssetAccount,
    CuentaDeActivoDto? AccumulatedAccount,
    CuentaDeActivoDto ExpenseAccount,
    Guid BranchPublicId,
    string BranchName,
    Guid? CostCenterPublicId,
    string? CostCenterName,
    Guid? SupplierPersonPublicId,
    string? SupplierName,
    DateOnly PurchaseDate,
    string? PurchaseDocument,
    decimal Cost,
    decimal ResidualValue,
    int LifeMonths,
    DateOnly StartDate,
    decimal OpeningAccumulated,
    int OpeningElapsedMonths,
    decimal Accumulated,
    decimal NetBookValue,
    int RemainingMonths,
    DateOnly? RetiredAt,
    string? RetireReason,
    Guid? RetireDocumentPublicId,
    bool HasPostedInstallments);

/// <summary>Una cuota contabilizada o reversada del activo, con su comprobante.</summary>
public sealed record CuotaDeActivoDto(int Year, int Month, decimal Amount, string Status, Guid? DocumentPublicId, string? DocumentNumber);

/// <summary>La ficha: el activo, sus cuotas contabilizadas o reversadas y la proyección de las que faltan.</summary>
public sealed record FixedAssetDetailDto(FixedAssetDto Asset, IReadOnlyList<CuotaDeActivoDto> Installments, IReadOnlyList<CuotaProyectadaDto> Projection);

public sealed record CuotaProyectadaDto(int Year, int Month, decimal Amount, decimal Accumulated, decimal NetBookValue);

/// <summary>Una corrida mensual: período, comprobante, cuántos activos, total y su reversión si la hubo.</summary>
public sealed record AssetRunDto(
    Guid PublicId,
    int Year,
    int Month,
    string Status,
    Guid DocumentPublicId,
    string DocumentNumber,
    int Assets,
    decimal Total,
    DateTime ExecutedAt,
    string ExecutedBy,
    Guid? ReversalDocumentPublicId,
    string? ReversalDocumentNumber);

/// <summary>Lo que dejaría una corrida del período sin contabilizar nada: activos, cuota y total.</summary>
public sealed record VistaPreviaDeCorridaDto(int Year, int Month, bool AlreadyRun, IReadOnlyList<CuotaDeLaCorridaDto> Lines, decimal Total);

public sealed record CuotaDeLaCorridaDto(Guid AssetPublicId, string Code, string Name, string Kind, decimal Amount, int Months);

/// <summary>Errores de activos fijos y diferidos (feature 009 E4, US12). Mensajes accionables (Principio VIII).</summary>
public static class AssetErrors
{
    public static readonly Error NotFound = new("Accounting.Assets.NotFound", "El activo no existe.");
    public static readonly Error RunNotFound = new("Accounting.Assets.RunNotFound", "La corrida no existe.");
    public static readonly Error KindUnknown = new("Accounting.Assets.KindUnknown", "La clase debe ser «Asset» (activo fijo) o «Deferred» (diferido o intangible).");
    public static readonly Error AccumulatedRequired = new("Accounting.Assets.AccumulatedRequired",
        "Un activo fijo necesita la cuenta de depreciación acumulada. Sólo un diferido puede amortizarse contra su propia cuenta.");
    public static readonly Error ResidualTooHigh = new("Accounting.Assets.ResidualTooHigh", "El valor residual no puede ser mayor que el costo.");
    public static readonly Error OpeningTooHigh = new("Accounting.Assets.OpeningTooHigh",
        "La depreciación acumulada al entrar no puede superar el costo menos el residual.");
    public static readonly Error OpeningMonthsTooHigh = new("Accounting.Assets.OpeningMonthsTooHigh",
        "Los meses ya depreciados no pueden superar la vida útil.");
    public static readonly Error StartBeforePurchase = new("Accounting.Assets.StartBeforePurchase",
        "La depreciación no puede empezar antes del mes de compra.");
    public static readonly Error Locked = new("Accounting.Assets.Locked",
        "El activo ya tiene cuotas contabilizadas: sus cuentas, costo, fecha de inicio y saldo inicial no se cambian. Vida útil y residual sí, hacia adelante.");
    public static readonly Error LifeTooShort = new("Accounting.Assets.LifeTooShort",
        "La vida útil nueva no puede ser menor que los meses ya depreciados.");
    public static readonly Error ResidualAboveNet = new("Accounting.Assets.ResidualAboveNet",
        "El residual nuevo deja el activo depreciado por encima de su costo: no puede superar el valor neto en libros.");
    public static readonly Error Retired = new("Accounting.Assets.Retired", "El activo está dado de baja.");
    public static readonly Error BranchNotFound = new("Accounting.Assets.BranchNotFound", "La sucursal no existe.");
    public static readonly Error CostCenterNotFound = new("Accounting.Assets.CostCenterNotFound", "El centro de costo no existe.");
    public static readonly Error SupplierNotFound = new("Accounting.Assets.SupplierNotFound", "El proveedor no existe en Personas.");
    public static readonly Error PeriodNotFound = new("Accounting.Assets.PeriodNotFound", "Ese mes no tiene período contable: ábralo en Contabilidad › Períodos.");
    public static readonly Error PeriodClosed = new("Accounting.Assets.PeriodClosed",
        "El período está cerrado: la depreciación de un mes cerrado no se corre ni se reversa. Reabra el período con su motivo si de verdad hace falta.");
    public static readonly Error AlreadyRun = new("Accounting.Assets.AlreadyRun",
        "La depreciación de ese mes ya se corrió. Para correrla otra vez, reverse primero la corrida vigente.");
    public static readonly Error EarlierThanLastRun = new("Accounting.Assets.EarlierThanLastRun",
        "Ya hay una corrida de un mes posterior: los meses se corren en orden (un mes sin correr se acumula en la siguiente corrida).");
    public static readonly Error NotLastRun = new("Accounting.Assets.NotLastRun",
        "Sólo se reversa la última corrida vigente: reverse primero las de los meses posteriores.");
    public static readonly Error RunReversed = new("Accounting.Assets.RunReversed", "La corrida ya está reversada.");
    public static readonly Error NothingToRun = new("Accounting.Assets.NothingToRun",
        "Ningún activo tiene cuota en ese mes: no hay comprobante que generar.");
    public static readonly Error RetireDateBeforeLastInstallment = new("Accounting.Assets.RetireDateBeforeLastInstallment",
        "La baja no puede fecharse en un mes ya depreciado por una corrida posterior: reverse esa corrida o feche la baja después.");
    public static readonly Error AccountingNotInitialized = new("Accounting.NotInitialized", "La contabilidad no está iniciada.");
}
