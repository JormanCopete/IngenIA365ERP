namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>La clase de lo encontrado: un crédito de Cartera o una cuenta de Captaciones (ahorro, CDAT, aportes). (nuevo)</summary>
public enum ClaseDeObligacionEncontrada
{
    Loan = 1,
    Account = 2,
}

/// <summary>
/// Lo que se busca: el número de un crédito o de una cuenta, como lo dice el asociado o el desprendible. Con <paramref name="SinSeparadores"/> el número se compara
/// sin espacios, guiones, puntos, barras ni guiones bajos de ningún lado: es la base de una referencia de pago (<c>DigitoDeVerificacion.Normalizar</c>), que los quita,
/// y el número guardado los tiene (<c>CR-000001</c>; corrida e2e del 2026-10-26: ningún pago de convenio se identificaba). (nuevo)
/// </summary>
public sealed record BusquedaPorNumero(string Number, bool SinSeparadores = false);

/// <summary>
/// Una obligación encontrada por su número: la clase, su <c>PublicId</c> y su número, y la persona dueña (deudor del crédito o titular de la cuenta) con su
/// documento y nombre. Sin saldos: lo exigible lo dicen los contratos. (nuevo)
/// </summary>
public sealed record ObligacionEncontradaDto(
    ClaseDeObligacionEncontrada Kind,
    Guid ObligationPublicId,
    string Number,
    Guid PersonPublicId,
    string PersonName,
    string? PersonTaxId,
    string Status);

/// <summary>
/// La búsqueda de una obligación por su número (feature 015, decisión del dueño del 2026-10-24 sobre T0113; contracts/interfaces.md, puertos de lectura de
/// Cartera): la ofrecen Cartera y Captaciones —un crédito por su número, una cuenta de captación o de aportes por el suyo— y la usa Recaudo en la caja para llegar
/// a la persona y a lo que puede pagar sin leer tablas <c>LND_</c>. Sólo lee; no aplica el alcance de Cartera (lo autoriza el permiso del módulo que llama, como
/// <c>ProductosDeCaptacionAsync</c>). Un número que no existe responde la lista vacía; uno repetido entre las dos clases, las dos filas. (nuevo)
/// </summary>
public interface IBusquedaDeObligaciones
{
    Task<IReadOnlyList<ObligacionEncontradaDto>> PorNumeroAsync(BusquedaPorNumero busqueda, CancellationToken ct);
}
