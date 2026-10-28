namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Feature 017 (US6, T069): el paso administrativo de fusionar dos identidades, en una sola transacción de la
/// base administrativa. Pasan a <c>destino</c> las membresías, las credenciales de segundo factor, las
/// invitaciones vivas y el historial de documentos de <c>origen</c>; si el destino no tenía documento se queda
/// con el del origen; el origen queda <c>Disabled</c>, sin documento y con el sello rotado (sus sesiones mueren
/// en la siguiente renovación), y el destino recalcula <c>TwoFactorEnabled</c> con todas sus credenciales.
/// Es idempotente: repetirlo sobre una fusión ya hecha no mueve nada.
/// </summary>
public interface IFusionDeIdentidades
{
    Task<ResultadoDeFusion> TrasladarAsync(Guid origen, Guid destino, DateTime ahora, CancellationToken ct);
}

public sealed record ResultadoDeFusion(int Membresias, int Credenciales, int Invitaciones, int Historial, string? DocumentUsernameDelDestino);
