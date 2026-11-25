namespace IngenIA365ERP.Domain.Lending.Replay;

// 2026-11-24 (feature 025, fase 3, HU1; D1): el interés del período al corte de un crédito cargado. (nuevo)

/// <summary>
/// Un <b>ajuste del corte</b> tal como quedó en el libro (filas <see cref="Enums.Lending.LoanTransactionKind.CutoverInterestAdjustment"/>): el interés
/// corriente que la primera cuota posterior al corte de una carga dejó por fuera —del vencimiento anterior al corte— y que se cobra en la cuota
/// <see cref="Cuota"/> (la primera sin vencer a la fecha de firma), desde el día <see cref="Fecha"/> (la firma), con lo que se registró y cuándo (R). Es
/// la entrada determinística de la reconstrucción, como los reconocimientos de la cuota del período: el monto se calculó una vez al firmar
/// (<see cref="AccrualProcesses.AjusteDelCorteDeCarga"/>) y nunca se recalcula. La reconstrucción lo suma al interés corriente de esa cuota ese día, fuera
/// de la causación: no cambia la base de la causación ni genera mora mientras la cuota no venza. (nuevo)
/// </summary>
public sealed record AjusteDelCorte(DateOnly Fecha, int Cuota, decimal Monto, DateTime RegistradoEn);
