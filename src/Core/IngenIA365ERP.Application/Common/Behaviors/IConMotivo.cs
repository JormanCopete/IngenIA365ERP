namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// Marca un comando que exige decir por qué (feature 012, §2.16; contracts/api.md §2.8): anular, descartar,
/// rechazar, reabrir, cambiar un parámetro, aceptar una diferencia, atender una alerta con nota. El motivo es
/// obligatorio, de hasta <see cref="ValidadorConMotivo{T}.LargoMaximo"/> caracteres (validarlo heredando
/// <see cref="ValidadorConMotivo{T}"/>), y la auditoría lo copia al evento del comando.
/// </summary>
public interface IConMotivo
{
    string Reason { get; }
}
