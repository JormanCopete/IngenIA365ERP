using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Feature 027: un pase de ingreso sin segundo factor. Lo concede un administrador de la cooperativa (o el maestro) a una persona que hoy no
/// tiene a mano su app ni sus códigos; viaja sólo en un enlace al correo verificado de esa persona y reemplaza <b>únicamente</b> el código del
/// segundo factor: la contraseña se pide siempre. Vive en <c>ADM_LoginPasses</c>.
///
/// <para>
/// Sirve varias veces mientras está vigente (si la sesión se cierra por inactividad, la persona vuelve a abrir el mismo enlace), dura como
/// mucho 12 horas y no toca los autenticadores: mañana entra con su app como siempre. Se anula al vencer, al entrar con el segundo factor, al
/// concederse otro, cuando lo anula un administrador, al apagar la opción la cooperativa y —comprobado al usarlo— cuando cambian las
/// credenciales de la cuenta, la cuenta deja de estar activa o pierde la membresía.
/// </para>
/// </summary>
public class LoginPass : AuditableEntity
{
    /// <summary>La duración máxima absoluta, en horas: una jornada.</summary>
    public const int HorasMaximas = 12;

    public const int LargoMinimoDelMotivo = 10;
    public const int LargoMaximoDelMotivo = 500;

    public Guid CentralUserId { get; private set; }

    /// <summary>PublicId de la cooperativa que lo concedió: la sesión abierta con el pase sólo entra a ésa.</summary>
    public Guid TenantId { get; private set; }

    public Guid GrantedByUserId { get; private set; }
    public bool GrantedByMaster { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>SHA-256 del secreto del enlace. El secreto en claro existe sólo en el correo.</summary>
    public byte[] TokenHash { get; private set; } = [];

    /// <summary>
    /// El sello de seguridad de la cuenta al conceder. Si al usarlo ya es otro (cambió la clave, se reinició el segundo factor), el pase deja
    /// de servir: quien lo concedió lo hizo para la cuenta tal como estaba.
    /// </summary>
    public string? SecurityStampAtGrant { get; private set; }

    public DateTime GrantedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    public int UseCount { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public string? LastUsedIp { get; private set; }

    public DateTime? VoidedAt { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public string? VoidReason { get; private set; }

    private LoginPass() { }

    public static class Motivos
    {
        public const string RevocadoPorAdministrador = "revocado-por-administrador";
        public const string Reemplazado = "reemplazado";
        public const string EntroConSegundoFactor = "entro-con-segundo-factor";
        public const string OpcionApagada = "opcion-apagada";
        public const string CambioDeCredenciales = "cambio-de-credenciales";
        public const string CuentaInactiva = "cuenta-inactiva";
        public const string SinMembresia = "sin-membresia";
    }

    public static LoginPass Conceder(
        Guid centralUserId, Guid tenantId, Guid grantedByUserId, bool grantedByMaster, string reason,
        byte[] tokenHash, string? securityStamp, DateTime ahora, TimeSpan duracion)
    {
        if (centralUserId == Guid.Empty) throw new ArgumentException("Falta el usuario.", nameof(centralUserId));
        if (tenantId == Guid.Empty) throw new ArgumentException("Falta la cooperativa.", nameof(tenantId));
        if (grantedByUserId == Guid.Empty) throw new ArgumentException("Falta quién lo concede.", nameof(grantedByUserId));
        if (tokenHash is not { Length: 32 }) throw new ArgumentException("La huella debe ser SHA-256.", nameof(tokenHash));
        // La duración no tiene que ser de horas enteras: el reenvío conserva el vencimiento del pase que reemplaza.
        if (duracion <= TimeSpan.Zero || duracion > TimeSpan.FromHours(HorasMaximas))
            throw new ArgumentOutOfRangeException(nameof(duracion), duracion, $"El pase dura como mucho {HorasMaximas} horas.");
        var motivo = (reason ?? string.Empty).Trim();
        if (motivo.Length is < LargoMinimoDelMotivo or > LargoMaximoDelMotivo)
            throw new ArgumentException($"El motivo tiene entre {LargoMinimoDelMotivo} y {LargoMaximoDelMotivo} caracteres.", nameof(reason));

        return new LoginPass
        {
            CentralUserId = centralUserId,
            TenantId = tenantId,
            GrantedByUserId = grantedByUserId,
            GrantedByMaster = grantedByMaster,
            Reason = motivo,
            TokenHash = tokenHash,
            SecurityStampAtGrant = securityStamp,
            GrantedAt = ahora,
            ExpiresAt = ahora.Add(duracion),
        };
    }

    public bool EstaVigente(DateTime ahora) => VoidedAt is null && ExpiresAt > ahora;

    public EstadoDelPase Estado(DateTime ahora) =>
        VoidedAt is not null ? EstadoDelPase.Voided : ExpiresAt > ahora ? EstadoDelPase.Active : EstadoDelPase.Expired;

    public void RegistrarUso(DateTime ahora, string? ip)
    {
        if (!EstaVigente(ahora)) throw new InvalidOperationException("El pase no está vigente.");
        UseCount++;
        LastUsedAt = ahora;
        LastUsedIp = ip is { Length: > 45 } ? ip[..45] : ip;
    }

    /// <summary>Idempotente: anular uno ya anulado o vencido no cambia nada (el primer motivo es el que vale).</summary>
    public void Anular(DateTime ahora, string motivo, Guid? porUsuario)
    {
        if (VoidedAt is not null) return;
        VoidedAt = ahora;
        VoidReason = motivo;
        VoidedByUserId = porUsuario;
    }
}

public enum EstadoDelPase
{
    Active = 1,
    Expired = 2,
    Voided = 3,
}
