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

Lo que hay que hacer, en este orden:

```sql
-- 1) Identificar la cuenta. Confirmar que es la correcta ANTES de seguir.
SELECT Id, Email, TwoFactorEnabled, IsGlobalMasterAdmin
FROM ADM_CentralUsers
WHERE IsGlobalMasterAdmin = 1;

-- 2) Retirar sus credenciales. Baja LOGICA, no DELETE: el rastro de qué tenía
--    y cuándo dejó de tenerlo es parte de poder reconstruir este incidente.
UPDATE ADM_MfaCredentials
SET IsDeleted = 1,
    DeletedAt = SYSUTCDATETIME(),
    DeletedBy = 'rescate-manual'
WHERE CentralUserId = @IdDelMaestro AND IsDeleted = 0;

-- 3) Apagar la bandera. Sin esto el login le sigue pidiendo un segundo factor
--    que ya no existe, y queda igual de fuera.
UPDATE ADM_CentralUsers
SET TwoFactorEnabled = 0,
    MfaSecret = NULL
WHERE Id = @IdDelMaestro;
```

**El sello de seguridad NO se toca en este SQL.** Rotarlo invalidaría todas las
sesiones vivas de todo el sistema, que no es lo que se está arreglando. Si se
sospecha que la cuenta está comprometida —y perder el autenticador sin explicación
es motivo para sospecharlo— entonces sí, y además hay que cambiar la contraseña:

```sql
-- SOLO si se sospecha compromiso.
UPDATE ADM_CentralUsers SET SecurityStamp = NEWID() WHERE Id = @IdDelMaestro;
```

Después del rescate, el siguiente login del maestro devuelve
`MfaEnrollmentRequired` y le hace inscribir un autenticador nuevo. **Los códigos
de respaldo nuevos se entregan ahí: guardarlos esta vez.**

---

## Cómo no volver aquí

1. Los códigos de respaldo del maestro, fuera del sistema, verificados.
2. **Dos autenticadores inscritos**, no uno. El tope es de cinco por tipo y el
   ingreso acepta cualquiera: no hay motivo para tener uno solo en la cuenta que
   no puede rescatar nadie.
3. Antes de restringir métodos en `ADM_PlatformMfaPolicy`, comprobar que la
   cuenta ya tiene inscrito uno de los que van a quedar. El endpoint lo comprueba,
   pero un `UPDATE` directo no.
