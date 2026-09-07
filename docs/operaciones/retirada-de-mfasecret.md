# Retirar `ADM_CentralUsers.MfaSecret`

**Estado: pendiente. No se ha ejecutado nada de este documento.**

Es la última pieza del traslado del segundo factor al modelo de credenciales. Se
dejó escrita y sin ejecutar a propósito: el Principio XII exige, antes de una
migración destructiva en producción, **backup completo y revisión de un segundo
desarrollador, ambos documentados**. Eso no lo puede aportar quien escribe esto.

---

## Qué queda por hacer, y en qué orden

El orden importa y es contraintuitivo. Retirar primero la lectura y después la
columna deja el peor estado posible: una columna llena de secretos TOTP vivos que
ya nadie lee — sigue siendo una responsabilidad y ha dejado de ser una red.

1. **Vaciar la columna** (destructivo, este documento).
2. **Retirar el modo compatibilidad de lectura** en
   `AspNetCoreIdentityProvider.VerificarPorCompatibilidadAsync`, y con él
   `IMfaDirectory.HuboAlgunaVezTotpAsync`, que sólo existe para eso.
3. **Retirar la escritura** de `MfaSecret` en el alta de TOTP.
4. Ajustar `EndToEnd_TrasladoMfaNoDejaANadieFuera`, cuyo paso 3 afirma hoy que el
   modo compatibilidad deja entrar antes del traslado.

---

## Qué es esta columna hoy

Guarda el secreto TOTP cifrado del modelo anterior. Desde el traslado
(`TrasladoDeSecretosMfa`) el mismo ciphertext vive además en
`ADM_MfaCredentials.SecretProtected`, que es de donde lee el ingreso.

La columna sigue teniendo dos funciones, y las dos son transitorias:

- **Se lee** cuando alguien con `TwoFactorEnabled` no tiene ninguna credencial
  activa. Es la red por si el traslado no cubrió a alguien.
- **Se escribe** al inscribir un TOTP. Es la red del rollback: si hubiera que
  volver al binario anterior, aquel sólo sabe leer esta columna.

Vaciarla cierra las dos. Por eso no se hace sola.

---

## Antes de ejecutar

- [ ] **Backup completo** de `IngenIA365ERP_Admin`, verificado restaurando en un
      entorno aparte. No basta con que el comando termine sin error.
- [ ] **Segundo revisor** que lea este documento y el SQL, y lo deje firmado por
      escrito.
- [ ] **Comprobar que nadie se queda fuera.** Esta consulta tiene que devolver
      **cero** filas. Si devuelve alguna, esas personas pierden el acceso al
      vaciar la columna, y hay que trasladarlas antes.

```sql
-- Personas con segundo factor activo y secreto heredado, pero SIN credencial
-- en el modelo nuevo. Cada fila aquí es alguien que quedaría fuera.
SELECT u.Id, u.Email
FROM ADM_CentralUsers u
WHERE u.TwoFactorEnabled = 1
  AND u.MfaSecret IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM ADM_MfaCredentials c
      WHERE c.CentralUserId = u.Id AND c.IsDeleted = 0);
```

- [ ] **Anotar cuántas filas se van a tocar**, para poder comparar después:

```sql
SELECT COUNT(*) FROM ADM_CentralUsers WHERE MfaSecret IS NOT NULL;
```

---

## Por qué no se puede deshacer

El `Down` de la migración puede recrear la columna, pero **no su contenido**: el
ciphertext original no está en ningún otro sitio con ese formato. Restaurarlo
exige el backup.

Dicho de otro modo: la reversibilidad de esta migración **es el backup**, no el
`Down`. Por eso el backup no es una recomendación.

---

## Cuándo tiene sentido hacerlo

Cuando se cumplan las tres:

1. El despliegue con el modelo de credenciales lleva el tiempo suficiente como
   para descartar un rollback al binario anterior.
2. La consulta de arriba devuelve cero durante varios días seguidos, no una vez.
3. No hay ningún `[Mfa.LecturaHeredada]` en el registro desde el despliegue. Ese
   aviso significa exactamente «alguien entró por la columna vieja», y mientras
   aparezca, la red sigue haciendo falta.

---

## Después

Ejecutados los cuatro pasos, actualizar:

- `CLAUDE.md` — el párrafo que dice que la columna «sigue escribiéndose como red
  de rollback».
- `specs/002-identidad-central-federada/contracts/profile-and-recovery.md`.
- Este documento, con la fecha, quién lo ejecutó y quién lo revisó.
