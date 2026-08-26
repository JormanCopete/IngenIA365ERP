using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Admin
{
    /// <summary>
    /// Traslada los secretos TOTP de <c>ADM_CentralUsers.MfaSecret</c> a filas de
    /// <c>ADM_MfaCredentials</c>. NO descifra ni vuelve a cifrar nada: copia el
    /// ciphertext literal.
    ///
    /// <para>
    /// <b>Por qué la copia literal es legal.</b> El texto de <c>MfaSecret</c> es la
    /// salida de <c>IDataProtector.Protect</c> con el purpose
    /// <c>central-identity:mfa-secret</c>. Quien lo descifra sigue siendo el mismo
    /// protector, con el mismo purpose y el mismo llavero: para DataProtection es
    /// el mismo dato en otro renglón. Si el purpose cambiara, <c>Unprotect</c>
    /// lanzaría, el proveedor captura esa excepción y devuelve false, y toda
    /// persona con segundo factor quedaría fuera de su cuenta sin ningún error
    /// visible. Por eso el traslado no toca criptografía.
    /// </para>
    ///
    /// <para>
    /// <b>NO se toca la columna origen.</b> Sigue con su valor: es la red de
    /// rollback y la que lee el modo compatibilidad. Vaciarla es una operación
    /// destructiva y va en su propia pasada, con el marcador
    /// <c>MIGRACION-DESTRUCTIVA-APROBADA</c>, backup y segundo revisor.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente</b>: el INSERT lleva <c>NOT EXISTS</c> y el DELETE del Down
    /// filtra por la marca que este mismo Up escribe. Re-ejecutar cualquiera de los
    /// dos no hace nada.
    /// </para>
    /// </summary>
    public partial class TrasladoDeSecretosMfa : Migration
    {
        /// <summary>
        /// Marca de origen. El Down borra SÓLO lo que este Up creó: lo que escriba
        /// la aplicación lleva otro autor y ConfirmedAt no nulo.
        /// </summary>
        private const string MarcaDeOrigen = "migracion:TrasladoDeSecretosMfa";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se copian TODAS las filas con secreto, incluidas las de personas con
            // TwoFactorEnabled = 0: el secreto existe y perderlo no tendría vuelta.
            // La bandera sigue decidiendo si el login exige segundo factor, así que
            // copiar el secreto no le habilita el MFA a nadie.
            //
            // Se copia también el estado de borrado del usuario: alguien eliminado
            // no debe reaparecer con una credencial activa.
            //
            // NO se nombran Id (identity) ni RowVersion (rowversion no admite
            // INSERT explícito). ConfirmedAt queda NULL a propósito: esa fecha no
            // existe en ninguna parte y ponerle una sería inventar un dato.
            migrationBuilder.Sql($"""
                INSERT INTO [dbo].[ADM_MfaCredentials]
                    ([PublicId], [CredentialType], [CentralUserId], [Label], [SecretProtected],
                     [ConfirmedAt], [IsDeleted], [DeletedAt], [DeletedBy],
                     [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy])
                SELECT
                    NEWID(), N'Totp', u.[Id], NULL, u.[MfaSecret],
                    NULL, u.[IsDeleted], u.[DeletedAt], u.[DeletedBy],
                    SYSUTCDATETIME(), N'{MarcaDeOrigen}',
                    SYSUTCDATETIME(), N'{MarcaDeOrigen}'
                FROM [dbo].[ADM_CentralUsers] u
                WHERE u.[MfaSecret] IS NOT NULL
                  AND NOT EXISTS (
                        SELECT 1
                        FROM [dbo].[ADM_MfaCredentials] c
                        WHERE c.[CentralUserId] = u.[Id]
                          AND c.[CredentialType] = N'Totp');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible sin pérdida: ADM_CentralUsers.MfaSecret sigue intacta, así
            // que borrar las copias no le quita el segundo factor a nadie.
            migrationBuilder.Sql($"""
                DELETE FROM [dbo].[ADM_MfaCredentials]
                WHERE [CredentialType] = N'Totp'
                  AND [CreatedBy] = N'{MarcaDeOrigen}'
                  AND [ConfirmedAt] IS NULL;
                """);
        }
    }
}
