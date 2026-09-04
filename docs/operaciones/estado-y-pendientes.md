# Estado de la plataforma y pendientes

> Corte: **2026-09-04**. Actualizar al cerrar cada pendiente.
> Complementa [despliegue-infraestructura.md](despliegue-infraestructura.md) (diseño e
> instalación) y, en el repositorio GitOps, `docs/backups.md` y
> `docs/mongo-replica-set.md`.

---

## 1. Qué está operativo

### Ambientes

| | DEV | QA | PRODUCCIÓN |
|---|---|---|---|
| ERP desplegado | ✅ | ✅ | ✅ |
| Réplicas API / Web | 1 / 1 | 1 / 1 | **2 / 2** |
| PostgreSQL | ✅ | ✅ | ✅ |
| MongoDB (replica set) | ✅ | ✅ | ✅ |
| Sincronización | automática con auto-reparación | automática | **manual** |

Producción responde en **https://app.ingenia365.com** — login verificado en
navegador, sin errores de consola. La IP del servidor no es visible desde
internet: todo entra por el túnel de Cloudflare.

### Respaldos

| Ambiente | PostgreSQL | Retención | MongoDB |
|---|---|---|---|
| PDN | WAL continuo + diario 01:30 COT | 35 d | 02:15 COT |
| DEV | WAL continuo + diario 02:30 COT | 7 d | 02:45 COT |
| QA | WAL continuo + diario 03:00 COT | 14 d | 03:15 COT |

Destino: `s3://ingenia365-erp-backups`, con **Object Lock en modo GOVERNANCE a
40 días**, versionado, cifrado AES256 y credencial de permisos mínimos que **no
puede saltarse el bloqueo ni borrar versiones**.

**Catálogos separados por ambiente** (`erp-db-dev`, `erp-db-qa`, `erp-db-pdn`).
Los tres clústeres se llaman `erp-db`: sin esa separación se sobrescribirían
entre sí y solo se notaría al intentar restaurar.

#### Restauraciones probadas, no supuestas

| Motor | Evidencia |
|---|---|
| PostgreSQL | Respaldo real de producción restaurado en un clúster aparte. Un marcador escrito **18 minutos después** del respaldo base apareció en el restaurado: llegó por **replay de WAL**, lo que valida el PITR y no solo la copia diaria |
| MongoDB | 132 documentos restaurados con índices, conteo idéntico al origen, verificado con aserción automática |

> Un ensayo que solo restaure el respaldo base no distingue *"tengo PITR"* de
> *"tengo copias diarias"*. Esa diferencia son horas de datos perdidos.

### Observabilidad

- Producción emite métricas por la malla Tailscale al Prometheus de nonprod
  (modo **agente**: no evalúa reglas ni almacena, ~200 MB).
- **9 reglas de alerta** sobre métricas verificadas con valores reales.
- **Canal de Telegram funcionando**, probado de extremo a extremo.
- Interruptor de hombre muerto: si producción deja de reportar, eso mismo alerta.

> Alertmanager estaba instalado con `receiver: "null"`: **descartaba todas las
> alertas de todos los ambientes**. Había reglas, paneles y alertas disparándose,
> y nadie se enteraba de nada. Dos alertas reales llevaban días ocultas.

### Seguridad

- Repositorio de la aplicación y paquetes de imágenes en **privado** (verificado
  sin credenciales: 401 anónimo). Los clústeres descargan con `ghcr-pull`.
- SSH público cerrado en ambos servidores; administración solo por Tailscale.
- Claves de firma RS256 distintas por ambiente: un token de DEV no vale en
  producción.

### Correo saliente

- **Servidor**: `mail.notifica365.com:587` con STARTTLS, remitente
  `noresponder.ingenia365erp@notifica365.com`. Envío verificado por el usuario.
- **No es Microsoft 365**, pese a lo que sugiere el nombre: el servidor se
  identifica como Haraka/Poste.io autoalojado. Importa para operar — no hay
  consola de M365 ni contraseñas de aplicación, y la entregabilidad depende de la
  reputación de esa IP, no de la de Microsoft.
- **Credenciales por variable de entorno** (`Smtp__Username`, `Smtp__Password`),
  nunca en el repositorio. En Kubernetes vienen del Secret `erp-smtp`.
- **Sólo QA envía correo real** (desde el 2026-09-04): `Smtp__*` en su overlay y
  Secret `erp-smtp` en `erp-qa`. **DEV y PDN no envían**: sin `Smtp__Host` ni
  Secret, y **no hay ningún capturador** (smtp4dev/MailHog) desplegado en el
  clúster —una versión anterior de esta nota decía lo contrario, sobre un commit
  de GitOps que nunca se subió—. Cuando en DEV o PDN falla el envío, el ERP lo
  dice (`CorreoEnviado=false` con el motivo) y la invitación queda para reenviar.
- **Los enlaces del correo ya apuntan a donde deben**. Hasta esta ronda,
  `IdentityEmail:BaseUrl` no estaba declarado en ningún ambiente y regía el valor
  cableado `https://localhost:7200`: toda invitación y todo restablecimiento
  enviados desde las VPS llevaban un enlace muerto.
- Ver `docs/operaciones/correo-saliente.md` para operarlo.

### Interfaz

- **Sistema de diseño único** en
  `src/Presentation/IngenIA365ERP.Shared/wwwroot/css/tokens.css`. Las pantallas
  no declaran colores propios: se migraron 467 literales repartidos en 65
  archivos, más 45 escritos con nombre CSS (`color:red`). Cambiar la identidad
  visual del producto entero es cambiar ese archivo.
- **Tema claro/oscuro, densidad, escala tipográfica y contraste alto**,
  aplicados antes del primer pintado para que no haya destello. Se guardan en la
  cuenta del usuario (`ADM_UserSettings`), no sólo en el navegador.
- **Contraste verificado**: las 44 combinaciones de color y tema cumplen WCAG AA
  (mínimo 4,5:1). La comprobación se hizo midiendo estilos calculados en un
  navegador real, no leyendo el CSS.
- **Login a dos columnas** con panel promocional administrable desde
  `/administracion/promociones`. Si no hay contenido o la consulta falla, el
  panel no se dibuja y el login funciona igual.

- **Hoja de componentes** (`componentes.css`): las 199 pantallas estaban escritas
  contra clases que nunca se declararon —`.page-header` aparecía 129 veces sin
  existir—, y el estilo embebido era el parche. Ya no hay ninguna página con
  bloque `<style>` propio.

| Indicador | Antes | Después |
|---|---:|---:|
| Clases sin definición aplicable | 29 (224 usos) | 12 (12 usos) |
| Clases definidas por varias páginas a la vez | 12 | 0 |
| Páginas con bloque `<style>` embebido | 25 | 0 |
| Declaraciones `style=` embebidas | 924 | 307 |
| Medidas en píxeles literales | 1498 | 256 |
| Hojas de estilo con ámbito | 8 | 49 |

Defectos que la migración sacó a la luz, ya corregidos:

| Defecto | Efecto |
|---|---|
| `.kpi-value` en `#fff` sobre tarjeta blanca | Las cifras del dashboard eran invisibles en modo claro |
| `--ifx-*` y `--sf-grid-header-bg` nunca definidos | Cada `var(--ifx-border, #ccc)` caía siempre al literal |
| Calificación A–E con dos paletas distintas | La misma categoría se pintaba de un color en una pantalla y de otro en otra |
| Paneles azul oscuro incrustados | Restos de un tema anterior dentro de una aplicación clara |
| `auth-card` sin definir en ningún CSS | 14 pantallas del flujo de autenticación sin estilos |
| `.info-card` y `.label` definidas por 14 páginas | La misma pantalla cambiaba de aspecto según de dónde vinieras |
| Reglas de navegación sin `::deep` | Los encabezados de grupo de la barra lateral, centrados en vez de alineados, en todas las pantallas |
| SyncFusion dimensionado por `fluent2.css` | Los filtros se quedaban en 30px con «texto muy grande», justo para quien activó esa opción |

---

## 2. Pendientes

### 🔴 Prioridad alta

#### P1 — Desactivar la llave AWS comprometida

`AKIAQ3EGUSHPYS4PQB5X` circuló en texto plano. **Ya no la usa el ERP** (se
reemplazó por la del usuario `ingenia365-erp-backup`), pero sigue activa.

Consola AWS → IAM → usuario dueño → *Credenciales de seguridad* → **Desactivar**.
Confirmar antes que `polly-carteravirtual` tenga su reemplazo: esa aplicación sí
la estaba usando.

#### P2 — MongoDB sin redundancia de almacenamiento

La auditoría SARLAFT (retención obligatoria de 5 años) vive en `local-path`, es
decir **disco local del nodo**. Hay respaldo diario a S3, pero si ese disco muere
se pierde todo lo posterior al último volcado (RPO ≤ 24 h).

PostgreSQL no tiene este problema: archiva WAL de forma continua (RPO ≤ 5 min).

Opciones: almacenamiento replicado (Longhorn u otro), un segundo nodo, o llevar
MongoDB a un servicio gestionado.

#### P3 — Producción despliega por etiqueta móvil

Los overlays usan `:release`, una etiqueta que puede reapuntarse. Con
`imagePullPolicy: Always` la imagen coincide con la etiqueta, pero **no queda
registro de qué artefacto está corriendo**.

Para un producto financiero auditable hace falta fijar el **digest** en
`workloads/erp/overlays/pdn/kustomization.yaml`. El propio `docs/backups.md` ya
lo especificaba; nunca se implementó.

> Costó dos despliegues fallidos descubrirlo: Argo reportaba `Synced` y los pods
> seguían corriendo la imagen de 41 horas antes.

#### P13 — El rol de la API no puede crear bases: ninguna cooperativa se aprovisiona

Registrar una cooperativa hace `CREATE DATABASE` con la conexión de la API
(`TenantDatabaseProvisioner`), y el rol `ingenia` de los tres clústeres
CloudNativePG **no tiene `CREATEDB`**: nació como `initdb.owner`, sin más
atributos. Sin el permiso la cooperativa queda registrada con
`ProvisioningState = Failed` y el motivo `permission denied to create database`;
la invitación al primer administrador sale igual y lo lleva a una cooperativa
sin base. Detectado el 2026-09-04 en QA, antes de registrar la primera.

El manifiesto de `erp-db` **no está en GitOps** (sólo el de ensayo de PDN) y
`spec.managed.roles` está vacío, así que el operador ni gestiona ni revierte el
rol: se otorga en vivo, una vez por clúster, como `postgres`:

```bash
ssh -i ~/.ssh/ingenia365_deploy root@100.94.218.42 "k3s kubectl -n erp-qa exec erp-db-1 -c postgres -- psql -U postgres -c 'ALTER ROLE ingenia CREATEDB'"
ssh -i ~/.ssh/ingenia365_deploy root@100.94.218.42 "k3s kubectl -n erp-dev exec erp-db-1 -c postgres -- psql -U postgres -c 'ALTER ROLE ingenia CREATEDB'"
ssh -i ~/.ssh/ingenia365_deploy root@100.104.190.76 "k3s kubectl -n erp-pdn exec erp-db-1 -c postgres -- psql -U postgres -c 'ALTER ROLE ingenia CREATEDB'"
```

Comprobar: `select rolcreatedb from pg_roles where rolname = 'ingenia'` debe dar
`t`. Es reversible con `NOCREATEDB`. Si una cooperativa ya quedó en `Failed`,
`POST /api/saas/tenants/{publicId}/provision` es idempotente y la repara.

| Ambiente | Estado |
|---|---|
| DEV | ✅ 2026-09-04 (`rolcreatedb = t`) |
| QA | ✅ 2026-09-04 — primera cooperativa `coop_prueba` aprovisionada en `Ready`, invitación entregada y aceptada |
| PDN | ⏳ pendiente — hacerlo antes de registrar la primera cooperativa real |

> Al aprovisionar apareció una carrera, una sola vez y sin daño:
> `NotificationEmailDispatcher` recorre `ListActiveAsync()` —que filtra por
> `IsActive` y no por `ProvisioningState`— y abrió `coop_prueba` cuando la base
> ya existía pero sus tablas no (`42P01 dbo.COR_Notifications does not exist`).
> Reintentó a los 15 s y no volvió a fallar. **Arreglado en `e7c96a2`** (2026-09-04):
> `ListActiveAsync` exige `ProvisioningState = Ready`, con prueba unitaria.

### 🟡 Prioridad media

#### P4 — Sellado mensual regulatorio suspendido

El CronJob `mongo-sellado-mensual` (PDN) exporta el mes cerrado en **JSON legible
sin herramientas de Mongo** y lo bloquea con Object Lock en modo **COMPLIANCE**
por 5 años. Está `suspend: true`.

**Antes de activarlo**: piloto con retención de **1 día** sobre un prefijo de
prueba. COMPLIANCE no admite marcha atrás **ni para el dueño de la cuenta**; un
error de escala se paga completo durante cinco años.

Incluye `accessLogs` además de `auditLogs`: esa colección tiene TTL de 90 días en
el código, así que sin el sellado la trazabilidad de **quién consultó** datos de
asociados no existiría cuando la pidan.

#### P5 — DEV y QA nunca se abrieron en un navegador

Se validó que los pods quedaran `Ready` y que `/health/ready` respondiera. **Eso
no prueba que la interfaz funcione**: exactamente así sobrevivió hasta producción
el fallo de `blazor.web.js`, con el pod sano y la página en blanco.

Abrirlos por la malla y recorrer el login y algunas pantallas.

#### P6 — Ensayo de restauración periódico

Ambas restauraciones se probaron una vez. Un respaldo que no se restaura hace un
año vuelve a ser una suposición.

Manifiesto listo para repetir: `infrastructure/pdn/ensayo-restauracion.yaml`.
Conviene repetirlo al cambiar de versión de motor. **Trampa documentada**: el
clúster de ensayo **no debe declarar `spec.plugins`**, o archivaría WAL contra el
mismo `serverName` que producción y corrompería su catálogo — el ensayo
destruiría el respaldo que valida.

#### P7 — El CI no verifica que las imágenes arranquen

Comprueba que **construyan**. Hoy aparecieron dos fallos que un CI verde no
detecta: el migrador compilaba pero moría al ejecutarse (`No frameworks were
found`), y la Web arrancaba mostrando una página en blanco.

Se agregó una comprobación puntual en el `Dockerfile` de la Web, pero un paso de
CI que ejecute cada imagen (`--help`, *health check*) cerraría la clase completa.

### 🟢 Prioridad baja

#### P8 — Deuda de pruebas del feature 002

10 pruebas de integración fallan por deuda previa (T128): esperan el contrato de
login anterior al cambio y comparten estado estático entre hosts de prueba.

#### P9 — Tareas abiertas del feature 004

- **T033**: prueba de integración de aprovisionamiento de tenant
- **T034**: verificación manual del reintento con la base caída

#### P10 — Riesgos de seguridad en 194.163.161.8

Documentados durante el relevamiento, fuera del alcance de este proyecto por
decisión explícita. Ver la sección correspondiente en
[despliegue-infraestructura.md](despliegue-infraestructura.md).

#### P11 — Cuota de GitHub Actions

Al pasar el repositorio a privado, las corridas consumen la cuota del plan Free
(2.000 min/mes). Cada corrida completa son ~18 min → alcanza para ~110 mensuales.
Si aprieta, un runner autoalojado en la VPS de nonprod resuelve y además cumple
el objetivo original de no depender de los límites de Actions.

#### P12 — SDK anclado a 10.0.302

`global.json` fija esa versión porque **el SDK 10.0.400 no genera
`blazor.web.js`**. No se investigó si es un cambio intencional que requiere
ajustar el código o una regresión. Revisar al actualizar en vez de quedar
anclados sin saber por qué.

### Pendientes de correo (diferidos por decisión del usuario)

#### C1 — El servidor de correo usa el certificado de fábrica

`mail.notifica365.com` presenta el certificado autofirmado que trae Poste.io de
serie: sujeto = emisor = `O=Poste.io, L=Susice, C=CZ`, sin ningún nombre
alternativo — ni siquiera nombra al servidor. MailKit lo rechaza, así que sin
intervención el ERP no podría enviar.

Como parche se fijó su huella SHA-256 en la configuración: acepta **ese**
certificado y ninguno más. No es lo mismo que desactivar la validación —eso
aceptaría cualquiera, y por esos correos viajan enlaces de invitación y de
restablecimiento de contraseña, que son credenciales de un solo uso.

**Lo correcto es instalar un certificado válido**; Poste.io trae Let's Encrypt
integrado. Al hacerlo hay que **borrar la huella** de los dos sitios donde está:

- `src/Presentation/IngenIA365ERP.API/appsettings.Development.json`, dentro del
  destino `Infraestructura:Smtp:Microsoft365`
- `workloads/erp/base/kustomization.yaml` del repositorio de despliegue

> **Trampa a tener presente**: si el certificado se renueva o se reemplaza y la
> huella no se actualiza ni se borra, el envío deja de funcionar. No falla en
> silencio —queda un error explícito en el registro con las dos huellas, la
> recibida y la esperada— pero nadie lo mira hasta que alguien reporta que no le
> llegan las invitaciones. Con un certificado válido el problema desaparece,
> porque la huella deja de usarse.

Para releer la huella si hiciera falta:

```bash
openssl s_client -starttls smtp -connect mail.notifica365.com:587 -showcerts </dev/null 2>/dev/null | openssl x509 -noout -fingerprint -sha256
```

#### C2 — `ingenia365.com` no tiene DMARC

Cualquiera puede falsificar correos desde ese dominio. Ya estaba señalado como
riesgo crítico en `docs/operaciones/migracion-dns-cloudflare.md`, y ahora pesa
más porque el ERP envía invitaciones y restablecimientos de contraseña.

Conviene revisar SPF, DKIM y DMARC de **`notifica365.com`** también, que es el
dominio que firma los envíos. Con un servidor autoalojado y sin esos registros,
lo esperable es que Hotmail y Gmail manden las invitaciones a spam — y el
síntoma que va a llegar es «no me llegó el correo», el mismo que originó toda
esta ronda.

### Pendientes de la mejora visual

#### V1 — `Microsoft.OpenApi` con vulnerabilidad de gravedad alta

La restauración avisa `NU1903` sobre `Microsoft.OpenApi 2.4.1`
(GHSA-v5pm-xwqc-g5wc). Afecta a `IngenIA365ERP.API`. Subir a la versión
corregida y comprobar que el documento OpenAPI se sigue generando.

#### V2 — MAUI no registra los clientes de `Services/Security`

`MauiProgram.cs` no registra `CentralAuthClient` ni ninguno de los demás, así
que las ~20 pantallas de seguridad —incluidas las nuevas de preferencias y
promociones— fallan al inyectarlos en la app de escritorio y móvil. Es una
condición anterior a esta mejora, pero ahora afecta a más pantallas.

#### V3 — El CI no compila MAUI

Se excluyó porque exige un workload pesado. Un cambio en `Shared` puede romper
la app sin que nadie se entere; hoy se verifica a mano. Evaluar un job aparte,
aunque corra sólo en la rama `release`.

#### V4 — Archivos huérfanos de OneDrive

154 archivos `.fuse_hidden*` (~1 MB) en `src/Presentation`, con el contenido
anterior a la migración de color. No están versionados, no los referencia ningún
proyecto y no compilan, pero conservan los literales que ya no deberían existir y
van a ensuciar cualquier auditoría por `grep`. Conviene borrarlos **desde el
explorador con OneDrive pausado**: la carpeta está sincronizada y el borrado se
propaga a la nube.

#### V5 — Página de inicio y favoritos guardados pero no consumidos

`ui.pagina-inicio` y `ui.favoritos` se guardan y validan en el servidor, pero
todavía ninguna pantalla los lee: falta redirigir tras iniciar sesión y pintar
los favoritos en el menú.

---

## 3. Lecciones operativas

Todos los fallos de la puesta en marcha compartieron una forma: **el sistema
reportaba éxito mientras estaba roto**.

| Fallo | Cómo se veía |
|---|---|
| Alertmanager descartando todo | Reglas activas, paneles poblados, cero avisos |
| Archivado de WAL sin permiso de etiquetado | 102 fallos en 45 min, ninguna alerta |
| La Web sin su script de arranque | Pod `Ready`, HTTP 200, pantalla en blanco |
| Etiqueta móvil con `IfNotPresent` | Argo `Synced`, imagen de 41 h corriendo |
| Cuota sin margen para el excedente | Argo `Synced`, `Progressing` eterno |
| Métrica de respaldo deprecada en cero | Una alerta escrita sobre ella nunca dispararía |

**Lo que funcionó para encontrarlos**: comparar *estado real* contra *estado
esperado* — el digest de la imagen, el contenido del manifiesto, el DOM
renderizado, el objeto en el bucket — en vez de confiar en que un comando dijo
"OK".

Dos reglas que conviene sostener:

1. **Una sonda que pide `/` no distingue una aplicación viva de una página en
   blanco.** Verificar comportamiento, no disponibilidad.
2. **Un proceso que oculta su propio fallo cuesta más de diagnosticar que uno que
   no existe.** Preferir siempre el fallo ruidoso.
