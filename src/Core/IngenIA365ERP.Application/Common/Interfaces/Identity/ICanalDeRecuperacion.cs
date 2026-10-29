using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Feature 017 (US7, T081): por dónde le llega a una persona el enlace para fijar una clave nueva. Hoy hay una
/// sola implementación, el correo verificado; la interfaz existe para que un canal futuro (SMS al celular de
/// la invitación) entre sin tocar a quien lo pide —el olvido de la persona y el enlace que envía el
/// administrador—. Un enlace nuevo deja sin efecto los anteriores sin usar.
/// </summary>
public interface ICanalDeRecuperacion
{
    Task EnviarEnlaceAsync(CentralUser persona, string? ipDeQuienPide, CancellationToken ct);
}
