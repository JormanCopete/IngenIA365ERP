using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Origination;

// Feature 013, US2 (bloque 6A; T0394): lo que ValidacionesDeOriginacion necesita de las piezas de listas, cupos y bloqueos, que construye el
// bloque 6B (T0403 ConsultaDeListasDeCartera, T0405 DisponibleDeCupo, T0406 BloqueosDelDeudor). Las validaciones dependen de estas
// interfaces y no de las clases: así la radicación se prueba sola con dobles y 6B implementa cada una en su clase con el nombre que fija
// tasks.md y la registra en DependencyInjection. Hasta entonces, sin registro, la radicación no se resuelve en la API. (nuevo)

/// <summary>A quién y en qué contexto se consultan las listas restrictivas desde Cartera (data-model §2.6). (nuevo)</summary>
public sealed record PedidoDeConsultaDeListas(int PersonId, ScreeningContext Contexto, int? ApplicationId = null, int? CreditLimitId = null)
{
    /// <summary>
    /// Falso en el re-escaneo (C90): sólo los hallazgos quedan como <c>ScreeningResult</c>; una consulta sin coincidencia no deja fila. (bloque 6B)
    /// </summary>
    public bool RegistrarSinHallazgo { get; init; } = true;
}

/// <summary>
/// Lo que dejó la consulta: la fila de <c>LND_ScreeningResults</c> que quedó en la unidad de trabajo (sin guardar), si bloquea (coincidencia
/// sin liberar) y si cuenta como consulta (<c>Unavailable</c> y el simulado en producción no cuentan, T73). (nuevo)
/// </summary>
public sealed record ConsultaDeListasRegistrada(ScreeningResult Resultado, bool Bloquea, bool CuentaComoConsulta);

/// <summary>
/// La consulta de listas de Cartera (T70; la implementa <c>ConsultaDeListasDeCartera</c>, T0403): consulta <c>IListasRestrictivas</c> con el
/// canal vigente sellado, agrega el <c>ScreeningResult</c> a la unidad de trabajo, pone el bloqueo <c>RestrictiveListMatch</c> y alerta
/// <c>Cartera.CoincidenciaEnListas</c> ante una coincidencia. Nunca guarda: guarda quien la llama, en su transacción. (nuevo)
/// </summary>
public interface IConsultaDeListasDeCartera
{
    Task<ConsultaDeListasRegistrada> ConsultarAsync(PedidoDeConsultaDeListas pedido, CancellationToken ct);
}

/// <summary>
/// El disponible de cupo de una persona para una línea a una fecha (FR-058; la implementa <c>DisponibleDeCupo</c>, T0405):
/// <see cref="TieneCupo"/> falso si la persona no tiene cupo que aplique (entonces la validación no aplica); si lo tiene, el cupo, lo
/// consumido y el disponible. Se calcula, nunca se guarda. (nuevo)
/// </summary>
public sealed record DisponibleDeCupoCalculado(bool TieneCupo, decimal Cupo, decimal Consumido, decimal Disponible, Guid? CreditLimitPublicId)
{
    public static DisponibleDeCupoCalculado SinCupo { get; } = new(false, 0m, 0m, 0m, null);
}

/// <summary>El disponible de cupo que la originación consulta (T0405). (nuevo)</summary>
public interface IDisponibleDeCupo
{
    Task<DisponibleDeCupoCalculado> CalcularAsync(int personId, int creditLineId, DateOnly fecha, CancellationToken ct);
}

/// <summary>Los bloqueos vigentes de unas personas (FR-060; la implementa <c>BloqueosDelDeudor</c>, el lector único, T0406). (nuevo)</summary>
public interface IBloqueosDelDeudor
{
    Task<IReadOnlyList<DebtorBlock>> VigentesAsync(IReadOnlyCollection<int> personIds, CancellationToken ct);
}
