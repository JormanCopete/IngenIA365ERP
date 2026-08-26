# El llavero de DataProtection

> **Antes de desplegar el cambio que mueve el llavero a la base, hay que rescatar
> las claves que ya existen.** Si se despliega sin ese paso, todo lo cifrado hasta
> hoy deja de poder abrirse: los segundos factores y, peor, los archivos adjuntos.
> Es irreversible.

## Qué protege este llavero

Una sola cadena de claves cifra tres cosas distintas:

| Qué | Dónde | Qué pasa si se pierde la clave |
|---|---|---|
| El secreto TOTP de cada persona | `ADM_CentralUsers.MfaSecret` | Todo el mundo con MFA recibe «código inválido» para siempre. Sólo lo arregla un reseteo administrativo por persona |
| La clave de cada archivo adjunto | `AttachmentEncryptionService` | **Los archivos dejan de poder leerse.** No hay reseteo que lo arregle: es pérdida de datos |
| Lo que pase por `EncryptionService` | varios | según el caso |

El segundo es el que manda. Un segundo factor perdido se vuelve a inscribir; un
adjunto cuyo DEK ya no se puede desenvolver, no vuelve.

## Qué estaba mal

`AddDataProtection()` se registraba sin `PersistKeysTo*`. Sin esa llamada, ASP.NET
Core guarda las claves en el perfil del proceso — en un contenedor Linux, dentro de
su capa escribible, que desaparece cuando el pod rota.

En producción eso se traduce en dos fallos simultáneos:

- **Cada despliegue empieza de cero.** Argo CD rota los pods, el llavero se va con
  ellos, y lo cifrado antes queda ilegible.
- **Las dos réplicas no se entienden.** Cada pod genera su propio llavero, así que
  lo que cifra uno el otro no lo abre. Se ve como un fallo intermitente: la misma
  persona inscribe su MFA y al rato el código «no es válido», más o menos la mitad
  de las veces, según a qué pod la mande el balanceador.

Ese segundo síntoma es la señal de que el problema ya está ocurriendo. Si alguien
reportó códigos MFA que fallan de forma intermitente, o adjuntos que a veces no
abren, era esto.

## Qué se cambió

Las claves pasan a la base administrativa, tabla `ADM_DataProtectionKeys`
(migración `LlaveroDeDataProtection`). Es única y compartida por los pods, y entra
en los respaldos que ya existen — se restaura junto con los datos que protege.

**Contrapartida, dicha claro:** quien pueda leer la base administrativa tiene el
llavero y el ciphertext a la vez, así que el cifrado en columna deja de proteger
contra un atacante con acceso a esa base. Sigue protegiendo contra un respaldo
filtrado o un volcado parcial. La alternativa —cifrar el propio llavero con un
certificado— añade una pieza que hay que custodiar y rotar; se puede añadir después
sin migrar nada, y es la decisión pendiente que este documento deja anotada.

`SetApplicationName("IngenIA365ERP")` es el discriminador de aislamiento. **Si
cambia, el llavero deja de reconocerse y equivale a haberlo perdido.** No tocarlo.

---

## Procedimiento de despliegue

### 1. Antes de nada: foto de lo que hay que salvar

```bash
psql -d IngenIA365ERP_Admin -c "SELECT count(*) FILTER (WHERE \"MfaSecret\" IS NOT NULL) AS secretos, count(*) FILTER (WHERE \"TwoFactorEnabled\") AS con_mfa FROM dbo.\"ADM_CentralUsers\";"
```

Y contar los adjuntos cifrados que existan. Si ambos son cero, el rescate de claves
sobra y se puede ir directo al paso 4 — pero **comprobarlo, no suponerlo**.

### 2. Rescatar las claves de cada pod

Hay que hacerlo en **todas** las réplicas: cada una tiene su propio llavero y no se
sabe cuál cifró qué.

```bash
kubectl -n <namespace> get pods -l app=<api>
```

Para cada pod, localizar el directorio de claves y volcarlo:

```bash
kubectl -n <namespace> exec <pod> -- sh -c 'ls -la $HOME/.aspnet/DataProtection-Keys'
kubectl -n <namespace> exec <pod> -- sh -c 'for f in $HOME/.aspnet/DataProtection-Keys/key-*.xml; do echo "=== $f"; cat "$f"; done' > claves-<pod>.txt
```

Si el directorio no existe, probar `/root/.aspnet/DataProtection-Keys` y
`/home/app/.aspnet/DataProtection-Keys`. Si no aparece en ninguna parte, **parar**:
significa que el llavero está en otro sitio y hay que averiguar dónde antes de
seguir, porque el despliegue lo va a dejar atrás.

### 3. Cargarlas en la base

Una fila por archivo. `FriendlyName` es el nombre del archivo sin extensión; `Xml`
es su contenido íntegro, tal cual, sin reformatear.

```sql
INSERT INTO dbo."ADM_DataProtectionKeys" ("FriendlyName", "Xml")
VALUES ('key-3adfabf9-8e12-496f-9bed-a9439661d471', '<key id="..." version="1">...</key>');
```

Cargar las de **todos** los pods. Si dos pods tienen una clave con el mismo id, es
la misma: cargarla una vez.

Comprobar antes de seguir:

```sql
SELECT count(*) FROM dbo."ADM_DataProtectionKeys";
```

### 4. Aplicar el esquema y desplegar

Con `AutoMigrate = false` en producción, la migración va primero y la imagen
después. Si se invierte el orden, la API no arranca (`[Database.MigrationsPending]`).

### 5. Comprobar de verdad, con una cuenta real

No basta con que la API levante. Hay que verificar las dos cosas que el llavero
protege:

- **Segundo factor:** iniciar sesión con una cuenta que tuviera MFA inscrito
  **antes** del despliegue y pasar el desafío. Repetir varias veces seguidas: si el
  rescate quedó incompleto, falla de forma intermitente según el pod que atienda.
- **Adjuntos:** descargar un archivo cifrado subido antes del despliegue y
  comprobar que abre.

Si el MFA falla, **no rotar los pods viejos todavía**: mientras sigan vivos, sus
claves se pueden volver a extraer.

### 6. Vigilar

Buscar `CryptographicException` en los registros durante las 48 horas siguientes.
Un pico ahí significa que faltó alguna clave por cargar.

---

## Verificación automática

Dos pruebas de integración lo cubren, en
`tests/IngenIA365ERP.API.IntegrationTests/Identity/ElLlaveroSobreviveAlProceso.cs`:

- **`Lo_que_cifra_una_instancia_lo_descifra_otra_distinta`** — construye un segundo
  proveedor con su propio contenedor de dependencias sobre la misma base. Es lo más
  parecido a «otro pod» que se puede montar sin levantar otro pod.
- **`El_llavero_quedo_persistido_en_la_base_administrativa`** — comprueba que las
  claves acaban en la tabla y no en memoria.

Las pruebas de MFA que ya existían **no cazaban esto**: cifran y descifran dentro
del mismo proceso, donde un llavero en memoria funciona perfectamente. Por eso el
defecto sobrevivió tanto.

Verificado que ambas detectan: quitando `PersistKeysToDbContext` del registro, las
dos se ponen rojas.

---

## Lo que este documento no resuelve

- **Cifrar el llavero en reposo.** Hoy las claves quedan en claro en la base. La vía
  es `ProtectKeysWithCertificate`, que exige custodiar y rotar un certificado. Es
  una decisión de operación, no de código, y se puede añadir después sin migrar nada.
- **`docs/operaciones/dev-environment.md:74`** promete las variables
  `DataProtection__KeyRingPath` y `DataProtection__ApplicationName`. **Ninguna línea
  de código las lee**, ni antes ni ahora. O se implementan o se quitan del
  documento; dejarlas ahí hace creer que el llavero se configura y no se configura.
