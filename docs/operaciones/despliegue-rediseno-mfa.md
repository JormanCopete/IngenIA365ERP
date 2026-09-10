# Desplegar el rediseño del segundo factor

> **Desplegado en producción el 2026-09-07** (promoción `develop → release`,
> commit `50ba799`; GitOps `2983420`; sincronización manual de Argo a las 19:09 UTC,
> Job PreSync terminado a las 19:11, pods relevados a las 19:15). Llegó junto con la
> nómina (feature 005) y el aprovisionamiento por cooperativa: 13 migraciones
> administrativas y 7 operativas en una sola ventana.
>
> Las tres preguntas de abajo se respondieron **contra la base**, no por deducción:
> 0 cooperativas y 0 adjuntos (llavero: **opción A**, aceptar la pérdida, nada que
> rescatar); el maestro con `TwoFactorEnabled = false` y sin secreto (**cae en la
> rama con salida**, `MfaEnrollmentRequired`; el Caso 0 no aplicaba); 0 personas con
> segundo factor a las que avisar. Respaldo previo: CNPG `erp-db-pre-nomina-mfa-20260907`
> a S3 más `pg_dump -Fc` de las dos bases en `/root/respaldos/` del nodo.
>
> Verificado tras el relevo: `ADM_DataProtectionKeys` = 1 (el llavero quedó
> persistido); `/api/saas/mfa-policy` y `/api/payroll/*` responden 401 desde
> `app.ingenia365.com` (antes 404); `/appsettings.json` de la Web ya trae el
> `ApiBaseUrl` relativo; ningún `CryptographicException`, `MigrationsPending` ni
> `LecturaHeredada` en los logs; los dos únicos 500 por pod son los sondeos de
> `/health/live` durante el calentamiento. El cierre lo hizo el maestro el
> 2026-09-09, tras resembrar su cuenta por contraseña perdida (Caso 3 de
> [rescate-del-administrador-maestro.md](rescate-del-administrador-maestro.md)):
> inscribió su autenticador, entró dos veces (10:19 y 10:22 UTC, ambas `Success`
> en `ADM_CentralUserLoginAttempts`) y recibió sus diez códigos de respaldo; una
> credencial TOTP en `ADM_MfaCredentials`, `TwoFactorEnabled = true`, y en las 48
> horas siguientes al despliegue ningún `CryptographicException` ni
> `[Mfa.LecturaHeredada]` en los dos pods. **Y la prueba del llavero compartido
> salió sola**: según los logs, `POST /api/profile/mfa/enroll` (que cifra el
> secreto) lo atendió el pod `qmc8t`, `POST /api/profile/mfa/confirm` (que lo
> descifra) lo atendió `7k4h5`, y `POST /api/auth/mfa/verify` volvió a `qmc8t`. Con
> el llavero efímero de antes, la confirmación habría fallado.
>
> Lo que sigue es el procedimiento tal como se pensó, y vale para repetirlo en otro
> ambiente: son ~30 commits y **9 migraciones administrativas**, una de ellas de
> datos, y otra que **mueve el llavero de cifrado**. Es el orden en que hay que
> hacerlo y las tres cosas que hay que decidir antes.

---

## Antes de nada: tres preguntas, tres consultas

Las tres van contra `IngenIA365ERP_Admin`. Producción es **PostgreSQL**
(`appsettings.Production.json`), y los identificadores van entrecomillados porque
EF los crea en PascalCase.

### 1. ¿Hay adjuntos que duelan? — decide el llavero

```bash
psql "$ADMIN_CONN" -c 'SELECT "Name","DatabaseName" FROM dbo."ADM_Tenants" WHERE "DatabaseName" IS NOT NULL;'
```

y contra **cada** base que salga:

```bash
psql -d <DatabaseName> -c 'SELECT count(*) AS adjuntos FROM dbo."COR_Attachments";'
```

Si todas dan **cero**, la decisión es aceptar la pérdida y no hay ningún paso
manual. Ver [llavero-dataprotection.md](llavero-dataprotection.md).

> **Comprobá esto antes de gastar una tarde en el rescate:** el único `IBlobStore`
> es `LocalEncryptedFileStore`, que escribe en `storage/attachments` **relativo al
> directorio de la app**, y ningún `appsettings*.json` declara la sección
> `AttachmentStorage`. Si los pods no montan un volumen ahí, los archivos ya se
> perdían en cada rotación y **rescatar las claves no salva nada**:
> ```bash
> kubectl -n <ns> exec <pod> -- sh -c 'mount | grep -i attach; find . -path "*storage/attachments*" -type f | wc -l'
> ```

### 2. ¿El maestro queda fuera? — decide si podés entrar después

```bash
psql "$ADMIN_CONN" -c 'SELECT "Email","TwoFactorEnabled",("MfaSecret" IS NOT NULL) AS tiene_secreto FROM dbo."ADM_CentralUsers" WHERE "IsGlobalMasterAdmin" AND NOT "IsDeleted";'
```

Si `TwoFactorEnabled` es **true**, el despliegue lo encierra: el llavero cambia,
su secreto TOTP deja de descifrarse, y la rama `MfaRequired` **no tiene escape de
inscripción** (la que sí lo tiene es la de `TwoFactorEnabled = false`). El
procedimiento y las dos salidas están en el **Caso 0** de
[rescate-del-administrador-maestro.md](rescate-del-administrador-maestro.md).

`Mfa__PlataformaSinRestriccion=true` **no rescata esto**: relaja *qué* métodos se
aceptan, nunca *si* se exige segundo factor.

### 3. ¿A cuánta gente hay que avisar?

```bash
psql "$ADMIN_CONN" -c 'SELECT count(*) FILTER (WHERE "MfaSecret" IS NOT NULL) AS con_secreto, count(*) FILTER (WHERE "TwoFactorEnabled") AS con_mfa FROM dbo."ADM_CentralUsers";'
psql "$ADMIN_CONN" -c 'SELECT count(*) FROM dbo."ADM_TenantMfaPolicies" WHERE "IsRequired";'
```

Y una prueba que vale más que las consultas: **pedile hoy a alguien con MFA que
entre**. Si ya recibe «código inválido», el llavero efímero se perdió en algún
reinicio anterior y no hay nada que rescatar — se va directo al plan de
reinscripción.

---

## Configuración: dos variables sin las cuales la API no arranca

`WebAuthnOptions` se registra con `ValidateOnStart` y **no tiene valores por
defecto** a propósito. La sección `WebAuthn` sólo existe en
`appsettings.Production.json`, que está en `.gitignore:118` — **no viaja en la
imagen**. Sin estas dos, el proceso muere antes de servir una petición, el health
check nunca se pone verde y el rollout queda colgado:

```
WebAuthn__RelyingPartyId=ingenia365.com
WebAuthn__OrigenesPermitidos__0=https://app.ingenia365.com
```

> **`RelyingPartyId` es irreversible en el sentido que importa.** Una vez que haya
> passkeys inscritas contra ese dominio, cambiarlo las invalida **todas**, sin
> migración posible. Va el dominio **raíz**, no `app.` — así una llave sigue
> valiendo si el frontend se mueve de subdominio.

**Recomendada, y no es adorno:**

```
IdentityEmail__BaseUrl=https://app.ingenia365.com
```

Sin ella rige el valor cableado `https://localhost:7200`, y con él se construyen
los **dos** enlaces del correo de recuperación — incluido el de **cancelar**. Ese
enlace es la mitigación que hace defendible borrar el segundo factor por correo:
apuntando a localhost, quien reciba el aviso de que alguien le está retirando el
segundo factor no tiene cómo pararlo.

Para que la recuperación por correo funcione: `Smtp__Host`, `Smtp__Port`,
`Smtp__UseStartTls`, `Smtp__FromAddress`, `Smtp__FromName`, y como **secretos**
`Smtp__Username` / `Smtp__Password`. Si faltan, el proceso arranca igual y la
recuperación falla en silencio: la solicitud queda creada con el reloj corriendo
y `CorreoEnviado=false`.

Comprobación en seco antes del rollout:

```bash
docker run --rm --entrypoint ls <imagen-nueva> /app | grep appsettings
```

Si sólo aparece `appsettings.json`, las dos primeras variables son obligatorias
sin discusión.

---

## Las migraciones

Nueve, todas de la base **administrativa**, todas en pareja PostgreSQL/SqlServer:

| Orden | Qué hace |
|---|---|
| `LlaveroDeDataProtection` | Crea `ADM_DataProtectionKeys`. **Es la del llavero.** |
| `CredencialesMfa` | Crea `ADM_MfaCredentials` |
| `TrasladoDeSecretosMfa` | **DML.** Copia el ciphertext literal a la tabla nueva |
| `UltimoUsoDeCredencialMfa` | Columna `LastUsedAt` |
| `VariosAutenticadoresTotpActivos` | Quita el índice único de TOTP activo |
| `CredencialesWebAuthn` | Columnas de passkey |
| `PoliticaDeMetodosMfa` | `AllowedMethodsMask`, **default 3 = acepta todo** |
| `PoliticaMfaDeLaPlataforma` | Crea la tabla de política de plataforma |
| `RecuperacionMfaPorCorreo` | `AllowEmailRecovery=false`, `EmailRecoveryDelayHours=24` |

Los valores por defecto están elegidos para no expulsar a nadie: la máscara llega
en «acepta todo», y `GuardiaDeMetodos` **cortocircuita sin mirar el método**
cuando la máscara no restringe. Sin eso, las sesiones vivas —que no llevan el
claim `mfa_method`— se caerían todas a la vez el día del despliegue.

`TrasladoDeSecretosMfa` es idempotente de verdad (`NOT EXISTS` por persona y
tipo). Pero **da por hecho que el mismo llavero abre después el ciphertext que
copia**, y este despliegue es justo el que mueve el llavero: por eso la decisión
del llavero se cierra **antes**, no en paralelo.

### Cómo aplicarlas

**Sólo el alcance admin.** Nunca `--scope all` ni `--scope tenants` sin
`--tenant`:

```bash
docker run --rm \
  -e DOTNET_ENVIRONMENT=Production \
  -e Database__Provider=PostgreSQL \
  -e Database__AdminConnectionStrings__PostgreSQL="$ADMIN_CONN" \
  <migrador> migrate --scope admin
```

> **Si el Job PreSync falla**, se queda en el namespace con sus logs
> (`hook-delete-policy: HookSucceeded`): leerlos, corregir, y **borrarlo a mano**
> antes de volver a sincronizar, porque con el mismo nombre Argo no lo reemplaza:
> `k3s kubectl -n erp-pdn delete job erp-db-migrate`.
>
> **`--scope` vale `all` si no se pasa**, y ese camino estaba mal: el bucle de
> cooperativas del migrador quedó en el modelo anterior —un esquema por
> cooperativa dentro de una base compartida— cuando el vivo es una base por
> cooperativa (Principio IV). Habría creado las tablas de cada cooperativa como
> esquemas dentro de la plantilla. **Ahora se para con
> `[Migrator.ModeloDeAislamiento]` y código de salida 2** en vez de hacerlo.
> Revisar que el Job PreSync de Argo no invoque el migrador a secas.

Antes de nada, preguntarle a la base en vez de deducirlo de los commits:

```bash
psql "$ADMIN_CONN" -c 'SELECT "MigrationId" FROM dbo."__EFMigrationsHistory" ORDER BY 1;'
```

Y confirmar **PostgreSQL 13 o superior** (`SHOW server_version;`): el traslado usa
`gen_random_uuid()` sin `pgcrypto`.

---

## Orden

1. **Respaldo** de `IngenIA365ERP_Admin`, verificado restaurando. Principio XII:
   hay una migración de datos y un `Down` que destruye el llavero.
2. Cerrar la decisión del **llavero** y, si toca, rescatar las claves **con los
   pods viejos todavía vivos**.
3. Dejar al **maestro** en la rama que tiene salida, o tener su código de respaldo
   en la mano.
4. Añadir las **variables** al despliegue.
5. **Migraciones** (`--scope admin`), antes que la imagen. Con `AutoMigrate=false`,
   si se invierte el orden la API no arranca: `[Database.MigrationsPending]`.
6. **API**.
7. **Web** — hay que **republicarlo sí o sí**. `wwwroot/appsettings.json` es
   estático y viaja dentro de la imagen: el arreglo del `ApiBaseUrl` no llega por
   variable de entorno.

---

## Verificar

No basta con que los health checks se pongan verdes.

- **Maestro**: entrar, inscribir, salir, volver a entrar, verificar. **Esperar dos
  logins** — tras inscribir no se recibe sesión, porque el ascenso devuelve `null`
  con cero membresías. **Guardar los códigos de respaldo antes de cerrar la
  pantalla**: se muestran una sola vez.
- **Alguien con MFA anterior**: que entre, y **varias veces seguidas**. Con dos
  réplicas, si el llavero no se compartiera fallaría más o menos la mitad de las
  veces. Es lo único que demuestra desde fuera que el llavero quedó compartido.
- **El frontend**: abrir la consola del navegador en producción. Tiene que decir
  `ApiBaseUrl=https://app.ingenia365.com/`, **no** `http://localhost:5100`.
- **Adjunto anterior** (si se eligió rescatar): descargarlo y comprobar que abre.
- **La primera cooperativa**: Consola SaaS → Registrar Cooperativa. El alta hace
  `CREATE DATABASE` con el rol de la API, y ese rol (`ingenia`) nació **sin
  `CREATEDB`** en los tres clústeres: sin otorgarlo antes, la cooperativa queda en
  `Failed` y la invitación sale igual, hacia una cooperativa sin base. Ver P13 en
  [estado-y-pendientes.md](estado-y-pendientes.md). El nombre de la base admite
  sólo letras ASCII, dígitos y guion bajo, sin empezar por dígito. Y el correo
  sólo sale en QA ([correo-saliente.md](correo-saliente.md) §2.3): en DEV y PDN la
  pantalla dirá `CorreoEnviado=false` con el motivo, que es lo correcto, y la
  invitación se reenvía desde `/admin/tenants/{tenantId}/invitaciones`.

## Vigilar 48 horas

| Marca en el log | Qué significa |
|---|---|
| `[Mfa.LecturaHeredada]` | El traslado no cubrió a alguien; la red de compatibilidad está trabajando |
| `CryptographicException` | El llavero no abre el ciphertext viejo: la decisión del llavero salió mal |
| `[Database.MigrationsPending]` | La imagen subió antes que el esquema |
| `[Migrator.ModeloDeAislamiento]` | Alguien invocó el migrador sin `--scope admin` |

---

## Rollback

**Volver la imagen, y dejar la base adelantada.** No revertir migraciones.

La imagen vieja **arranca y funciona contra el esquema nuevo**: las columnas
añadidas a tablas que ella también escribe llevan default de servidor
(`AllowedMethodsMask`=3, `AllowEmailRecovery`=false, `EmailRecoveryDelayHours`=24),
y las tablas nuevas simplemente las ignora. La clave de firma JWT es un PEM
montado, no DataProtection, así que las sesiones vivas sobreviven al cambio de
imagen.

**No planificar rollback por `Down`.** El de `VariosAutenticadoresTotpActivos`
falla en cuanto exista un segundo TOTP activo, y los de `LlaveroDeDataProtection`
y `CredencialesWebAuthn` destruyen material criptográfico que no está en ningún
otro sitio. Si hay que volver atrás de verdad, es **restore del respaldo**.

Lo que sí se pierde al volver la imagen: quien haya inscrito una **passkey** o un
**segundo TOTP** después del despliegue no puede entrar con la imagen vieja, que
sólo sabe leer `ADM_CentralUsers.MfaSecret`.
