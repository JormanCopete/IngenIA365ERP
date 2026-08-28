using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// Mapeo de la jerarquía de credenciales MFA contra <c>ADM_MfaCredentials</c>.
/// Es la PRIMERA jerarquía TPH del repositorio, así que aquí quedan escritas las
/// reglas que en las demás configurations no hacían falta:
///
/// <list type="bullet">
///   <item><c>ToTable</c> y <c>HasQueryFilter</c> van SÓLO en la raíz. EF admite
///         filtro global únicamente en el tipo raíz de una jerarquía.</item>
///   <item>Las columnas de un subtipo son anulables por fuerza: cuando aparezca un
///         segundo tipo, sus filas no llenarán las columnas del primero. Nunca
///         <c>IsRequired()</c> en una derivada — eso pondría NOT NULL en la tabla
///         compartida. Lo obligatorio se defiende en la factory de la entidad.</item>
///   <item>Todo índice único lleva el filtro de soft-delete. Sin él, una
///         credencial revocada bloquearía inscribir otra igual — el mismo error
///         que apareció en <c>UpdateRoleCommandHandler</c> con los permisos.</item>
/// </list>
/// </summary>
public sealed class MfaCredentialConfiguration : IEntityTypeConfiguration<MfaCredential>
{
    /// <summary>
    /// Columna discriminadora. Se referencia por texto en el filtro del índice
    /// único, así que renombrarla obliga a cambiar el filtro Y a migrar datos.
    /// </summary>
    public const string ColumnaDiscriminador = "CredentialType";

    /// <summary>
    /// Valor del discriminador para TOTP. Es un literal corto y NO el nombre de la
    /// clase a propósito: si mañana alguien renombra <c>TotpCredential</c>, los
    /// datos ya escritos siguen valiendo. El SQL del traslado escribe exactamente
    /// esta cadena.
    /// </summary>
    public const string ValorTotp = TiposDeCredencialMfa.Totp;

    /// <summary>
    /// Valor del discriminador para WebAuthn. Literal corto y no el nombre de la
    /// clase, por lo mismo que <see cref="ValorTotp"/>.
    /// </summary>
    public const string ValorWebAuthn = TiposDeCredencialMfa.WebAuthn;

    public void Configure(EntityTypeBuilder<MfaCredential> builder)
    {
        builder.ToTable("ADM_MfaCredentials");
        builder.HasKey(e => e.Id);

        // TPH con dos tipos: el autenticador de códigos y el passkey. Las columnas
        // propias de cada uno son anulables por fuerza, porque comparten tabla.
        builder.HasDiscriminator<string>(ColumnaDiscriminador)
            .HasValue<TotpCredential>(ValorTotp)
            .HasValue<WebAuthnCredential>(ValorWebAuthn);

        builder.Property<string>(ColumnaDiscriminador).HasMaxLength(32);

        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.CentralUserId).IsRequired();
        builder.Property(e => e.Label).HasMaxLength(100);

        // Auditoría (AuditableEntity). Este contexto no tiene interceptores, así
        // que la rellenan las factories de la entidad.
        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Soft-delete: SÓLO aquí. La configuration del subtipo no declara filtro,
        // y la convención transversal salta los tipos derivados.
        builder.HasQueryFilter(e => !e.IsDeleted);

        // «Credenciales activas de esta persona» — la consulta del ingreso.
        builder.HasIndex(e => e.CentralUserId, "IX_ADM_MfaCredentials_CentralUserId")
            .HasDatabaseName("IX_ADM_MfaCredentials_CentralUserId")
            .HasFilter("[IsDeleted] = 0");

        // Aquí hubo un índice único de «una credencial TOTP activa por persona».
        // Se retiró: una persona puede tener varios autenticadores a la vez —el
        // teléfono, el escritorio, uno viejo de respaldo— y el ingreso prueba el
        // código contra todos.
        //
        // No se sustituye por otro índice único. Sobre (CentralUserId, Label) no
        // serviría: Label es opcional, y dos «iPhone» son un problema de quien los
        // nombró, no una violación de integridad. El tope de credenciales por
        // persona se aplica en Application, donde se puede dar un mensaje que
        // explique qué pasa.
    }
}
