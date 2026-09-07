using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// Columnas propias del subtipo WebAuthn dentro de <c>ADM_MfaCredentials</c>.
///
/// <para>
/// Espejo de <see cref="TotpCredentialConfiguration"/> y por las mismas razones:
/// sin <c>ToTable</c> —la tabla la declara la raíz—, sin <c>HasQueryFilter</c>
/// —EF sólo lo admite en la raíz— y <b>sin un solo <c>IsRequired()</c></b>, que
/// pondría NOT NULL en la tabla compartida y rompería todas las filas TOTP, que
/// dejan estas columnas vacías. Lo obligatorio lo defiende la factory.
/// </para>
/// </summary>
public sealed class WebAuthnCredentialConfiguration : IEntityTypeConfiguration<WebAuthnCredential>
{
    /// <summary>
    /// Índice único GLOBAL del credential id: el ingreso resuelve la credencial
    /// por el identificador que devuelve el navegador, sin saber todavía de quién
    /// es.
    /// </summary>
    public const string IndiceCredentialId = "UX_ADM_MfaCredentials_CredentialId";

    public void Configure(EntityTypeBuilder<WebAuthnCredential> builder)
    {
        // varbinary(n) explícito y no byte[] a secas: sin tipo, SQL Server elige
        // varbinary(max), que NO se puede indexar. En PostgreSQL este tipo lo anula
        // ApplyPortableColumnTypes y Npgsql emite bytea.
        //
        // 1023 cabe de sobra en el tope de 1700 bytes de clave de índice no
        // agrupado de SQL Server. Si algún día hiciera falta indexar algo mayor, la
        // salida es indexar un SHA-256 del id y dejar el crudo fuera del índice.
        builder.Property(e => e.CredentialId)
            .HasColumnType($"varbinary({WebAuthnCredential.LongitudMaximaCredentialId})");

        builder.Property(e => e.PublicKeyCose)
            .HasColumnType($"varbinary({WebAuthnCredential.LongitudMaximaClavePublica})");

        builder.Property(e => e.Transports)
            .HasMaxLength(WebAuthnCredential.LongitudMaximaTransportes);

        builder.Property(e => e.AttestationFormat)
            .HasMaxLength(WebAuthnCredential.LongitudMaximaFormatoAtestacion);

        // El filtro tiene DOS mitades y las dos hacen falta.
        //
        // «IS NOT NULL» — sin él, SQL Server trata dos NULL como IGUALES dentro de
        // un índice único y sólo admitiría UNA fila con la columna vacía: la
        // SEGUNDA credencial TOTP de cualquier persona reventaría con violación de
        // clave. PostgreSQL trata los NULL como distintos y lo aceptaría, así que
        // el fallo sería asimétrico y NO aparecería en desarrollo, que corre sobre
        // PostgreSQL. Aparecería en una instalación SQL Server, en producción.
        //
        // «[IsDeleted] = 0» — sin él, una passkey revocada bloquearía re-inscribir
        // la MISMA llave para siempre, y quien la retiró por error no tendría forma
        // de volver a ponerla.
        //
        // Se escribe en T-SQL canónico y ProviderModelConventions lo traduce; el
        // IS NOT NULL pasa intacto en los dos motores.
        builder.HasIndex(e => e.CredentialId, IndiceCredentialId)
            .IsUnique()
            .HasDatabaseName(IndiceCredentialId)
            .HasFilter("[CredentialId] IS NOT NULL AND [IsDeleted] = 0");
    }
}
