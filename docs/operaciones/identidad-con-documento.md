# Runbook — Identidad con documento (feature 017)

Ingreso con correo **o** usuario de documento, invitaciones ancladas al documento, corrección y fusión de
identidades y cambio de correo por la propia persona. Este runbook es para el despliegue en cada ambiente
(DEV, QA, producción). **Regla de oro**: ninguna persona que hoy entra debe dejar de entrar ni perder su
sesión por el despliegue.

## 1. Antes de desplegar

1. **Diagnóstico de datos** en el ambiente: las consultas de
   `specs/017-login-documento-invitacion/diagnostico-de-datos.md` (secciones A y B; B en **cada** base de
   cooperativa). Son sólo lecturas. Anotar cantidades y filas.
2. Resolver con el dueño, antes de seguir, lo que haga fallar una guarda:
   - **A1** (correos centrales duplicados): la migración `IdentidadConDocumento` se detiene con la lista.
   - **B6** (documentos que chocan al normalizarlos en una cooperativa): `UsuarioDeLaPersona` se detiene
     con la lista en esa base.
3. **Respaldo** de la base administrativa y de cada base de cooperativa (`pg_dump` o el respaldo del
   motor), con nombre `*-AAAAMMDD-pre-f017.dump`. Sin respaldo no se sigue (Principio XII).

## 2. Orden del despliegue

1. **Migración administrativa** `IdentidadConDocumento` (la aplica la API al arrancar con `AutoMigrate`, o
   el DbMigrator). Agrega columnas y tablas, crea los índices únicos de documento y usuario, pasa la
   guarda de correos duplicados y crea el índice único de correo, y marca **verificado** el correo de toda
   cuenta activa con contraseña (es la que ya entraba). No rota sellos: las sesiones abiertas siguen.
2. **Migración de cooperativa** `UsuarioDeLaPersona`, base por base (DbMigrator). Agrega
   `COR_People.Username`, `SEC_Users.PositionId` y `SEC_Users.CentralUsername`, rellena el usuario de cada
   persona, crea `UK_COR_People_Username` y retira `UK_COR_People_TaxId` (desde aquí pueden convivir CC y
   CE con el mismo número).
3. **Arranque de la API**: siembra el permiso nuevo `Security.Users.CorrectDocument` (lo recibe el rol
   `CompanyAdmin` por su `*`). La ruta `POST /api/admin/users/{id}/reset-password` ya no existe (se retiró
   sin alias): el administrador envía el enlace de recuperación.
4. **Tarea de datos** (una vez, tras migrar todas las bases):
   `dotnet run --project tools/IngenIA365ERP.DbMigrator -- identidad-documento --output conflictos-f017.csv`.
   Sube el documento de las cuentas existentes desde la persona enlazada. Lo que no puede decidir queda en
   el CSV; cómo leerlo está en la sección D del diagnóstico. Correrla otra vez da lo mismo.

## 3. Verificación

- Ingresar con una cuenta de antes, con su correo y su clave: mismo segundo factor, mismas cooperativas,
  mismos permisos. Una sesión que estaba abierta antes del despliegue sigue renovando.
- Ingresar con el usuario de documento de una cuenta que la tarea completó (cédula sin prefijo; otros
  tipos con el prefijo: `CE987654`).
- Invitar a una persona nueva con documento y activarla.
- **Consulta de control de SC-004** (debe dar 0 filas). En la base administrativa:

  ```sql
  SELECT "Id", "DocumentType", "DocumentNumber", "DocumentUsername"
  FROM "ADM_CentralUsers"
  WHERE "DocumentUsername" IS NOT NULL
    AND "DocumentUsername" <> (CASE "DocumentType" WHEN 'CC' THEN '' ELSE "DocumentType" END) || "DocumentNumber";
  ```

  Y en cada base de cooperativa, para las personas con tipo de acceso:

  ```sql
  SELECT "Id", "IdType", "TaxId", "Username"
  FROM "COR_People"
  WHERE "Username" IS NOT NULL AND "IdType" IN ('C', 'CC')
    AND "Username" <> upper(regexp_replace("TaxId", '[\s\.\-]', '', 'g'));
  ```

  (La regla completa por tipo es `UsuarioDeDocumento.Derivar`; estas consultas cubren el caso de la cédula,
  que es casi todo. Una fila aquí es un relleno que no coincide con la regla: no se corrige a mano, se
  reporta.)

## 4. Lo que cambia sin aviso, y es aceptado

- **El bloqueo por intentos se libera una vez**: el contador de intentos de contraseña pasa a contarse por
  cuenta (se entra con correo o con usuario) y los contadores viejos por correo dejan de leerse. Quien
  estaba bloqueado en el momento del despliegue queda desbloqueado. Es como máximo una hora, una sola vez
  (decisión del plan).
- La exigencia de documento por fecha **no muerde** hasta que una cooperativa fije «Exigir desde» en su
  política de identidad (por defecto vacía).

## 5. Reversa

1. Bajar la API a la versión anterior.
2. Revertir `UsuarioDeLaPersona` en cada cooperativa. **Límite**: la reversa vuelve a crear
   `UK_COR_People_TaxId` y **falla** si en esa base ya conviven dos personas con el mismo número y distinto
   tipo (por ejemplo CC y CE), lo que sólo puede pasar después del despliegue. En ese caso hay que decidir
   con el dueño cuál de las dos se retira antes de revertir, o restaurar el respaldo de esa base.
3. Revertir `IdentidadConDocumento`. Sus columnas y tablas son nuevas, así que la reversa no toca datos de
   antes del despliegue (lo vigila la prueba `LasMigracionesDe017SonAditivas`). Los documentos registrados,
   el historial y los casos se pierden con ella: si importan, sacar antes una copia de
   `ADM_DocumentHistory` y `ADM_IdentityCases`.
4. Si algo falla en la reversa, restaurar el respaldo del paso 1.3.
