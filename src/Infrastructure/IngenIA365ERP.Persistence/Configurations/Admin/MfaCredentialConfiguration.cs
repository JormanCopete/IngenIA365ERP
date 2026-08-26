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
    public const string ValorTotp = "Totp";

    public void Configure(EntityTypeBuilder<MfaCredential> builder)
    {
        builder.ToTable("ADM_MfaCredentials");
        builder.HasKey(e => e.Id);

        // TPH. Hoy hay un solo valor; el segundo llega con WebAuthn y sólo añade
        // columnas nulables, que es una operación en línea en ambos motores.
        builder.HasDiscriminator<string>(ColumnaDiscriminador)
            .HasValue<TotpCredential>(ValorTotp);

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

        // UNA credencial TOTP activa por persona.
        //
        // El filtro se escribe en T-SQL canónico y ProviderModelConventions lo
        // traduce: cambia los corchetes por comillas dobles y «[IsDeleted] = 0»
        // por «"IsDeleted" = FALSE». El literal de texto pasa tal cual y es válido
        // en los dos motores.
        //
        // Que incluya IsDeleted no es decorativo: sin eso, revocar una credencial
        // impediría inscribir otra, porque la fila revocada seguiría ocupando el
        // índice.
        builder.HasIndex(e => e.CentralUserId, "UX_ADM_MfaCredentials_TotpActivo")
            .IsUnique()
            .HasDatabaseName("UX_ADM_MfaCredentials_TotpActivo")
            .HasFilter($"[{ColumnaDiscriminador}] = '{ValorTotp}' AND [IsDeleted] = 0");
    }
}
