# El llavero de DataProtection

> **Antes de desplegar el cambio que mueve el llavero a la base hay que tomar UNA
> decisión, no ejecutar un procedimiento.** Desplegar sin decidir deja ilegible
> todo lo cifrado hasta hoy, y eso es irreversible. Pero la decisión correcta
> puede perfectamente ser «que se pierda»: mientras no haya datos que duelan, es
> la respuesta buena y no cuesta ningún paso manual.
>
> La primera versión de este documento daba el rescate por obligatorio. No lo es
> — y presentarlo así hace que alguien dedique una tarde a salvar claves que
> protegen tres adjuntos de prueba.
>
> **Y si estás corriendo en local, esto no te toca**: saltá a
> [«En desarrollo esto no aplica»](#en-desarrollo-esto-no-aplica) y seguí
> trabajando.

## Qué protege este llavero

Una sola cadena de claves cifra tres cosas distintas:

| Qué | Dónde | Qué pasa si se pierde la clave |
|---|---|---|
| El secreto TOTP de cada persona | `ADM_CentralUsers.MfaSecret` | Todo el mundo con MFA recibe «código inválido» para siempre. Sólo lo arregla un reseteo administrativo por persona |
| La clave de cada archivo adjunto | `AttachmentEncryptionService` | **Los archivos dejan de poder leerse.** No hay reseteo que lo arregle: es pérdida de datos |
| Lo que pase por `EncryptionService` | varios | según el caso |

El segundo es el que manda. Un segundo factor perdido se vuelve a inscribir; un
adjunto cuyo DEK ya no se puede desenvolver, no vuelve.

---

## En desarrollo esto no aplica

**Todo el procedimiento de más abajo es para los ambientes desplegados.** La
ejecución por defecto mientras se desarrolla es **local**, que es lo que dice
`appsettings.Development.json`: `Infraestructura:Destinos` pone cada servicio en
esta máquina y `Database` apunta a la base administrativa local. Un solo
proceso, sin réplicas y sin nada que rote pods — **ninguno de los dos síntomas
de producción puede darse aquí**.

Por eso el defecto duró tanto sin que nadie lo notara. En local el llavero caía
en el perfil del usuario (`%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` en
Windows), que sobrevive a los reinicios y lo usa un único proceso: funcionaba
perfecto. Un registro sin `PersistKeysTo*` **sólo falla donde el sistema de
archivos es efímero o hay más de una instancia**, y corriendo `dotnet run` no
ocurre ninguna de las dos.

### Lo único que cambia en tu máquina

El llavero pasa a `ADM_DataProtectionKeys`, en tu base administrativa local. La
tabla la crea sola la migración `LlaveroDeDataProtection` al arrancar, porque en
desarrollo `Database:AutoMigrate` está en `true`. **Cero pasos manuales, no hay
nada que decidir.**

La contrapartida es real y conviene saberla antes de tropezarla: **ahora el
llavero se va con la base**. Si recreás o borrás la base administrativa local,
los secretos TOTP que hubiera dejan de verificarse y los adjuntos cifrados de
prueba dejan de abrirse. Antes no pasaba, porque el llavero vivía fuera de la
base, en tu perfil de Windows.

No es un problema —se reinscribe el segundo factor y los adjuntos son datos de
prueba— pero explica el síntoma si aparece: **«el código de mi app dejó de
funcionar y no toqué nada»** justo después de recrear la base es esto, no un
fallo del TOTP.

### No busques variables de configuración

No hay. El registro es **el mismo en local y en producción** —una sola llamada
en `IngenIA365ERP.Identity/DependencyInjection.cs`, sin condicionar por
ambiente—, así que lo que cambia entre uno y otro es únicamente a qué base
administrativa apunta la cadena de conexión.

`docs/operaciones/dev-environment.md:74` promete `DataProtection__KeyRingPath` y
`DataProtection__ApplicationName`. **Ninguna línea de código las lee**, ni antes
ni ahora. Ponerlas no hace nada.

---

## Primero: ¿hay algo que rescatar?

> Esta decisión es de los ambientes desplegados. En local ya está resuelta: no
> hay nada que rescatar.

**Los segundos factores no entran en la decisión.** Perderlos cuesta que cada
persona vuelva a inscribir su autenticador, y eso se resuelve solo la próxima vez
que entra. Lo único que decide son los adjuntos, porque son lo único que no vuelve.

Contra la base de **cada cooperativa** —`COR_Attachments` vive ahí, no en la
administrativa—:

```sql
SELECT COUNT(*) AS adjuntos FROM dbo."COR_Attachments";
```

No hace falta filtrar por `EncryptedDek`: `UploadAttachmentCommand` cifra
**siempre**, así que toda fila es un archivo que depende del llavero.

### Si el total es cero en todas → opción A

**Aceptar la pérdida.** No hay nada que salvar. Se despliega y ya: al encontrar
`ADM_DataProtectionKeys` vacía, ASP.NET Core genera la clave nueva por su cuenta
en el primer uso. **Cero pasos manuales, nadie tiene que crear ni pasar ninguna
clave.**

Lo único que hay que avisar: quien tuviera segundo factor inscrito recibirá
«código inválido» y tendrá que volver a inscribirlo. En un sistema sin clientes
son dos o tres personas, y la pantalla de inscripción forzada las guía sola.

Después del despliegue, saltar directo al paso 4.

### Si hay adjuntos que importan → opción B

El rescate de los pasos 2 y 3. Tiene ventana: **hay que hacerlo con los pods
actuales todavía vivos**, porque las claves están en su capa escribible y
desaparecen cuando se reemplazan.

Y conviene saber esto antes de decidir: si ya hubo despliegues desde que existen
esos adjuntos, **parte del daño ya está hecho** — Argo rota los pods en cada
despliegue y con dos réplicas cada una cifró lo suyo. El rescate salva lo que los
pods vivos todavía pueden abrir, no lo anterior.

---

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

> **Sólo ambientes desplegados.** Corriendo en local no hay ninguno de estos
> pasos: se arranca y la tabla se crea sola.

### 1. Decidir (arriba), y dejar constancia

La consulta de adjuntos por cooperativa, y esta otra para saber a cuánta gente hay
que avisar de que va a tener que reinscribir su segundo factor:

```bash
psql -d IngenIA365ERP_Admin -c "SELECT count(*) FILTER (WHERE \"MfaSecret\" IS NOT NULL) AS secretos, count(*) FILTER (WHERE \"TwoFactorEnabled\") AS con_mfa FROM dbo.\"ADM_CentralUsers\";"
```

**Comprobarlo, no suponerlo**, y anotar los números junto con la opción elegida.
Si sale A, ir al paso 4.

### 2. [Sólo opción B] Rescatar las claves de cada pod

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

### 3. [Sólo opción B] Cargarlas en la base

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

No basta con que la API levante.

**Con la opción A**, lo que hay que comprobar es que el llavero nuevo quedó
persistido —si no, el problema original sigue vivo y nadie se entera:

```sql
SELECT count(*) FROM dbo."ADM_DataProtectionKeys";   -- tiene que dar >= 1
```

Después, inscribir un segundo factor y verificarlo **varias veces seguidas**: con
dos réplicas, si el llavero no se compartiera, fallaría más o menos la mitad de
las veces según a qué pod vaya la petición. Eso es justamente el síntoma que este
cambio elimina, y es lo único que lo demuestra desde fuera.

**Con la opción B**, además:

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
  `DataProtection__KeyRingPath` y `DataProtection__ApplicationName`, que **ninguna
  línea de código lee** (ver arriba). O se implementan o se quitan de aquel
  documento; dejarlas ahí hace creer que el llavero se configura por ambiente, y
  no se configura: la llamada es la misma en local y en producción.
