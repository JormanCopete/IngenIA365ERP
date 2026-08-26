using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// Columnas propias del subtipo TOTP dentro de <c>ADM_MfaCredentials</c>.
///
/// <para>
/// No lleva <c>ToTable</c> —la tabla la declara la raíz—, no lleva
/// <c>HasQueryFilter</c> —EF sólo lo admite en la raíz— y no lleva
/// <c>IsRequired()</c>: cuando aparezca un segundo tipo de credencial, sus filas
/// dejarán esta columna en NULL, y un NOT NULL en la tabla compartida las
/// rompería. Que el secreto sea obligatorio lo garantiza
/// <c>TotpCredential.Inscribir</c>, que es donde se puede dar un mensaje decente.
/// </para>
/// </summary>
public sealed class TotpCredentialConfiguration : IEntityTypeConfiguration<TotpCredential>
{
    public void Configure(EntityTypeBuilder<TotpCredential> builder)
    {
        // Texto, NO binario: es la salida Base64Url de IDataProtector.Protect.
        // 1024 frente a los 512 de ADM_CentralUsers.MfaSecret, para que la copia
        // del traslado no pueda truncar el ciphertext ni por accidente.
        builder.Property(e => e.SecretProtected)
            .HasMaxLength(TotpCredential.LongitudMaxima);
    }
}
