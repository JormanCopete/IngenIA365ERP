# Rescate del administrador maestro

El maestro es **la única cuenta que nadie más puede rescatar**. Este documento
existe porque esa frase tiene consecuencias operativas concretas.

---

## Por qué no hay una salida por la aplicación

Las tres vías de recuperación del segundo factor fallan para él, cada una por un
motivo distinto y ninguno arreglable con más código:

| Vía | Por qué no le sirve |
|---|---|
| `POST /api/saas/users/{id}/force-mfa-reset` | Exige `IsGlobalMasterAdmin`. O sea: **sólo puede rescatarse a sí mismo**, que es justo lo que no puede hacer si está fuera. |
| Doble aprobación de dos administradores | Corre sobre `IApplicationDbContext` —la base de UNA cooperativa— y resuelve la persona por `SEC_Users.CentralUserId`. El maestro no tiene membresías por diseño, así que no existe en ninguna `SEC_Users`: no puede ni radicar la solicitud. |
| Recuperación por correo | **Apagada por defecto** para él (`Mfa:RecuperacionPorCorreoParaMaestro`). Es la cuenta que crea cooperativas y borra el segundo factor de cualquiera; abrirle una vía que depende de un buzón es exactamente el riesgo que el diseño acota. |

Le quedan los **códigos de respaldo**, y este documento.

---

## Antes que nada: los códigos de respaldo

Se entregan una sola vez, al inscribir el primer autenticador. Si están, no hace
falta nada más: entrar con uno, ir a `/profile/seguridad/credenciales`, inscribir
un autenticador nuevo, y regenerar los códigos.

**Guardarlos fuera del sistema es parte de la puesta en marcha, no una
recomendación.**

---

## Caso 0 — el despliegue que mueve el llavero (ventana, no incidente)

Éste no ocurre solo: **lo provoca un despliegue concreto**, el que lleva
`LlaveroDeDataProtection`. Va primero porque es el único que se puede evitar
del todo, y sólo antes de desplegar.

El llavero pasa a `ADM_DataProtectionKeys`, que nace **vacía**. ASP.NET Core
genera una clave nueva sin avisar, y el secreto TOTP cifrado con la anterior deja
de descifrarse. El fallo se traga —se captura la excepción y se devuelve
`false`—, así que el maestro ve «Código MFA inválido», indistinguible de teclear
mal, y a los pocos intentos se bloquea.

**Y aquí no hay escape de inscripción.** El login bifurca así:

| Estado del maestro | Respuesta | ¿Salida? |
|---|---|---|
| `TwoFactorEnabled = false` | `MfaEnrollmentRequired` | **Sí** — inscribe y entra (con doble login: el ascenso no da sesión con cero membresías) |
| `TwoFactorEnabled = true` | `MfaRequired` | **No** — pide un código que ya no puede validar |

`Mfa:PlataformaSinRestriccion` **no rescata este caso**: relaja *qué* métodos se
aceptan, nunca *si* se exige segundo factor.

### La consulta que hay que correr antes de desplegar

Producción es PostgreSQL (`appsettings.Production.json`), no SQL Server:

```bash
psql -d IngenIA365ERP_Admin -c 'SELECT "Email", "TwoFactorEnabled", ("MfaSecret" IS NOT NULL) AS tiene_secreto FROM dbo."ADM_CentralUsers" WHERE "IsGlobalMasterAdmin" AND NOT "IsDeleted";'
```

- **`TwoFactorEnabled = false`** → no hay nada que hacer. Cae en la fila con salida.
- **`TwoFactorEnabled = true`** → hace falta **una** de estas dos, antes de desplegar:
  1. Tener en la mano un código de respaldo vigente. **Sobreviven al cambio de
     llavero**: viven en `ADM_CentralUserTokens` y no pasan por DataProtection.
     Los diez van en **una sola fila**, separados por `;`, así que contar filas
     no dice cuántos quedan:
     ```sql
     SELECT u."Email",
            coalesce(array_length(string_to_array(t."Value", ';'), 1), 0) AS codigos_restantes
     FROM dbo."ADM_CentralUsers" u
     LEFT JOIN dbo."ADM_CentralUserTokens" t
            ON t."UserId" = u."Id" AND t."Name" = 'RecoveryCodes'
     WHERE u."IsGlobalMasterAdmin" AND NOT u."IsDeleted";
     ```
     Que el número sea mayor que cero **no basta**: hay que tener el papel. Si no
     aparece por ninguna parte, tratar la opción 2 como obligatoria.
  2. O moverlo a la fila que sí tiene salida, **antes** del despliegue:
     ```sql
     UPDATE dbo."ADM_CentralUsers"
     SET "TwoFactorEnabled" = false, "MfaSecret" = NULL
     WHERE "IsGlobalMasterAdmin" AND NOT "IsDeleted";
     ```
     Queda desprotegido el rato que va entre esto y el primer login posterior al
     despliegue. Es corto y es elegido; quedarse fuera no.

### Justo después de desplegar

Entrar como maestro y **completar el ciclo entero**: inscribir, salir, volver a
entrar, verificar. **Esperar dos logins** — tras inscribir no se recibe sesión,
porque el ascenso devuelve `null` con cero membresías. Y **guardar los códigos de
respaldo que muestra la pantalla antes de cerrarla**: se enseñan una sola vez.

---

## Caso 1 — la política de plataforma lo dejó fuera

Síntoma: el maestro supera su segundo factor y en vez de entrar recibe
`MfaEnrollmentRequired`, porque `ADM_PlatformMfaPolicy` ya no acepta el método
que tiene inscrito.

Este caso **tiene rescate sin tocar datos**. Poner en la configuración del
despliegue:

```
Mfa__PlataformaSinRestriccion=true
```

y reiniciar. Con eso la política guardada se ignora y se aceptan todos los
métodos; el registro lo dice en cada arranque con un aviso de nivel `Warning`, y
`GET /api/saas/mfa-policy` devuelve `rescateActivo: true` para que nadie crea que
la restricción sigue en vigor.

Después: entrar, inscribir un método que la política sí acepte —o corregir la
política—, y **volver a poner la clave en `false`**.

> El endpoint comprueba antes de guardar que el maestro tenga algún autenticador
> de los que la política nueva aceptaría, y responde
> `Saas.PlatformMfaPolicy.TeDejariaFuera` si no. Este caso sólo debería llegar si
> alguien escribió la fila por SQL directo.

---

## Caso 2 — perdió el autenticador y no tiene códigos

Aquí no hay salida por la aplicación. Hace falta escribir en la base
administrativa, y por tanto:

- [ ] **Backup** de `IngenIA365ERP_Admin` antes de tocar nada.
- [ ] **Segundo par de ojos**, presente mientras se ejecuta.
- [ ] Anotar quién, cuándo y por qué — esta operación **no deja rastro en la
      auditoría**, porque no pasa por la aplicación. Es la única de todo el
      sistema de la que eso es cierto, y por eso el registro manual no es
      burocracia.

Lo que hay que hacer, en este orden. **PostgreSQL**, que es lo que corre en
producción; los identificadores van entre comillas porque EF los crea en
PascalCase:

```sql
-- 1) Identificar la cuenta. Confirmar que es la correcta ANTES de seguir.
SELECT "Id", "Email", "TwoFactorEnabled", "IsGlobalMasterAdmin"
FROM dbo."ADM_CentralUsers"
WHERE "IsGlobalMasterAdmin" AND NOT "IsDeleted";

-- 2) Retirar sus credenciales. Baja LOGICA, no DELETE: el rastro de qué tenía
--    y cuándo dejó de tenerlo es parte de poder reconstruir este incidente.
UPDATE dbo."ADM_MfaCredentials"
SET "IsDeleted" = true,
    "DeletedAt" = now() AT TIME ZONE 'utc',
    "DeletedBy" = 'rescate-manual'
WHERE "CentralUserId" = :id_del_maestro AND NOT "IsDeleted";

-- 3) Apagar la bandera. Sin esto el login le sigue pidiendo un segundo factor
--    que ya no existe, y queda igual de fuera: la rama TwoFactorEnabled=true
--    responde MfaRequired, que NO tiene escape de inscripción.
UPDATE dbo."ADM_CentralUsers"
SET "TwoFactorEnabled" = false,
    "MfaSecret" = NULL
WHERE "Id" = :id_del_maestro;
```

**El sello de seguridad NO se toca en este SQL.** Rotarlo invalidaría todas las
sesiones vivas de todo el sistema, que no es lo que se está arreglando. Si se
sospecha que la cuenta está comprometida —y perder el autenticador sin explicación
es motivo para sospecharlo— entonces sí, y además hay que cambiar la contraseña:

```sql
-- SOLO si se sospecha compromiso.
UPDATE dbo."ADM_CentralUsers"
SET "SecurityStamp" = gen_random_uuid()::text
WHERE "Id" = :id_del_maestro;
```

Después del rescate, el siguiente login del maestro devuelve
`MfaEnrollmentRequired` y le hace inscribir un autenticador nuevo. **Los códigos
de respaldo nuevos se entregan ahí: guardarlos esta vez.**

---

## Caso 3 — perdió la contraseña (no el segundo factor)

Síntoma: `POST /api/auth/login` responde 401 y en `ADM_CentralUserLoginAttempts`
el intento del maestro queda con `Result = InvalidPassword`. No hay bloqueo
(`LockoutEnd` nulo) ni segundo factor de por medio: es la contraseña.

Tampoco aquí hay salida por la aplicación. El restablecimiento por correo exige
SMTP, que producción no tiene, y `MasterAdminSeeder` es idempotente: **si existe
un maestro vivo no hace nada**, aunque el Secret `erp-master-admin` cambie. La
única forma de que vuelva a sembrar es que no encuentre ninguno vivo.

Ocurrió el 2026-09-09: el maestro se había sembrado el 2026-08-12 con la
contraseña que tenía el Secret ese día, y nadie la recordaba.

Requisitos, los mismos del Caso 2: **backup** de `IngenIA365ERP_Admin`, **segundo
par de ojos**, y **anotar quién, cuándo y por qué** —esta operación no pasa por la
aplicación y no deja rastro en la auditoría.

El orden importa:

1. **Primero el Secret, después la baja.** Poner la contraseña nueva con
   `tools/scripts/crear-secreto-maestro.ps1 -Ambiente pdn` (la pide por teclado y
   viaja por STDIN del SSH; no pasa por ningún argumento ni archivo). Si se
   invierte el orden y algo reinicia los pods en medio, el sembrador crea al
   maestro con la contraseña vieja del Secret.
2. **Baja lógica con renombre.** `UserNameIndex` es único sobre
   `NormalizedUserName` y **no filtra por `IsDeleted`**: retirar la fila dejándole
   el mismo nombre hace que el sembrador choque al insertar la nueva, la API nueva
   no arranca y, con el maestro ya retirado, nadie puede entrar. Por eso se
   renombran las cuatro columnas de identidad, no sólo la bandera:

   ```sql
   -- 1) Confirmar que es la cuenta correcta y la única viva.
   SELECT "Id", "Email", "IsGlobalMasterAdmin", "IsDeleted"
   FROM dbo."ADM_CentralUsers" WHERE "IsGlobalMasterAdmin";

   -- 2) Retirarla. Baja LOGICA con renombre; el Id y el historial quedan.
   UPDATE dbo."ADM_CentralUsers"
   SET "IsDeleted" = true,
       "DeletedAt" = now() AT TIME ZONE 'utc',
       "DeletedBy" = 'rescate-manual:contrasena-perdida',
       "UserName"           = "UserName"           || '.retirado-AAAAMMDD',
       "NormalizedUserName" = "NormalizedUserName" || '.RETIRADO-AAAAMMDD',
       "Email"              = "Email"              || '.retirado-AAAAMMDD',
       "NormalizedEmail"    = "NormalizedEmail"    || '.RETIRADO-AAAAMMDD'
   WHERE "Id" = :id_del_maestro AND "IsGlobalMasterAdmin" AND NOT "IsDeleted";
   ```

   Los códigos de respaldo (`ADM_CentralUserTokens`) y las credenciales MFA
   (`ADM_MfaCredentials`) de la fila retirada se quedan con ella; el maestro nuevo
   nace sin nada y el primer login le exige inscribir.
3. **Relevar UN pod de la API** (`kubectl delete pod`, no `rollout restart`: la
   anotación de reinicio deja la aplicación `OutOfSync` en Argo y en producción
   esa señal es la que aprueba despliegues). Con uno basta: el sembrador sólo
   corre al arrancar, y el otro pod lee al maestro de la base en cada petición,
   así que sigue sirviendo y no necesita reinicio. El pod nuevo tarda ~2 min en
   quedar listo y registra `Master admin sembrado: <correo> (Id <guid>)`. Si en
   vez de eso muere con «No existe ningun administrador maestro y no hay
   credenciales para sembrarlo», el Secret no está montado: el pod viejo sigue
   sirviendo, revertir la baja (`IsDeleted = false` y quitar los sufijos) y volver
   al paso 1.
4. **Entrar**: `MfaEnrollmentRequired`, inscribir, dos logins, guardar los códigos.

### Registro de la intervención del 2026-09-09

| | |
|---|---|
| Motivo | Contraseña del maestro desconocida; 2 intentos `InvalidPassword` ese día, ningún bloqueo |
| Respaldo | CNPG `erp-db-pre-resiembra-maestro-20260909` (`20260909T100812`, S3) y `pg_dump -Fc` en `/root/respaldos/ingenia365erp_admin-20260909-pre-resiembra-maestro.dump` |
| Quién | Jorman Copete (Secret nuevo y segundo par de ojos); el asistente ejecutó la baja y el relevo por SSH |
| Fila retirada | `ff6d915d-fb9a-48d1-a4cf-4d447966698e`, sembrada el 2026-08-12, ahora `master@ingenia365.com.retirado-20260909`, `IsDeleted = true` |
| Fila nueva | `7aba2f12-11dd-4698-a8a7-d92457d156dd`, sembrada a las 10:13:35 UTC por el pod `erp-api-69b566b49d-qmc8t`, sin segundo factor |
| Sello de seguridad | No se tocó: no había sesiones vivas del maestro y no se sospecha compromiso |

## Cómo no volver aquí

1. Los códigos de respaldo del maestro, fuera del sistema, verificados.
2. **Dos autenticadores inscritos**, no uno. El tope es de cinco por tipo y el
   ingreso acepta cualquiera: no hay motivo para tener uno solo en la cuenta que
   no puede rescatar nadie.
3. Antes de restringir métodos en `ADM_PlatformMfaPolicy`, comprobar que la
   cuenta ya tiene inscrito uno de los que van a quedar. El endpoint lo comprueba,
   pero un `UPDATE` directo no.
