namespace IngenIA365ERP.Application.Common.Integration.Lending;

// Feature 015, T2 (fase 43, T0459; enmiendas E-01 d y E-02 f; FR-137, FR-164 d, FR-165 f; contratos-v2.md §2.4 y §3.6): lo que Cartera y Captaciones ofrecen a la
// posición de liquidez de Tesorería: lo programado por vencer por día, sin datos personales. Sin respuesta, la posición se muestra sin ese componente y con el aviso.

/// <summary>Lo programado por cobrar de un día: cuántas cuotas y por cuánto, sin datos personales. (nuevo)</summary>
public sealed record RecaudoEsperadoDelDiaDto(DateOnly Date, int Installments, decimal Amount);

/// <summary>Ofrece Cartera → usa Tesorería (T2; E-01 d): las cuotas de los créditos vivos que vencen entre dos fechas, por día. (nuevo)</summary>
public interface IRecaudosEsperadosDeCartera
{
    Task<IReadOnlyList<RecaudoEsperadoDelDiaDto>> PorFechaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
}

/// <summary>Ofrece Captaciones → usa Tesorería (T2; E-02 f): las cuotas de aportes y ahorros que vencen entre dos fechas, por día. (nuevo)</summary>
public interface IRecaudosEsperadosDeCaptaciones
{
    Task<IReadOnlyList<RecaudoEsperadoDelDiaDto>> PorFechaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
}
