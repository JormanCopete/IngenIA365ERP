namespace IngenIA365ERP.Application.Common.Integration.Accounting;

// Feature 015, US8 (T0260; FR-143; contracts/interfaces.md §3): lo que Recaudo y Tesorería ofrecen a Contabilidad para la plantilla y la completitud de sus matrices.
// Contabilidad no lee tablas RCD_ ni TES_: pregunta por estos puertos y recibe códigos y nombres.

/// <summary>Un valor de una dimensión de la matriz, por código: la dimensión (<c>CashBoxCode</c>, <c>MeansCode</c>…), el código, el nombre y su padre (el punto de una caja). (nuevo)</summary>
public sealed record DimensionDeMatrizDto(string Dimension, string Code, string Name, string? ParentCode = null);

/// <summary>Los puntos, cajas, medios, conceptos y oficinas que la configuración real de Recaudo usa (la plantilla sugiere una fila por cada uno). (nuevo)</summary>
public interface IDimensionesDeRecaudo
{
    Task<IReadOnlyList<DimensionDeMatrizDto>> DimensionesAsync(CancellationToken ct);
}

/// <summary>Las cuentas propias, las clases de orden y los orígenes que la configuración real de Tesorería usa. (nuevo)</summary>
public interface IDimensionesDeTesoreria
{
    Task<IReadOnlyList<DimensionDeMatrizDto>> DimensionesAsync(CancellationToken ct);
}
