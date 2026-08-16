# Estado de la plataforma y pendientes

> Corte: **2026-08-14**. Actualizar al cerrar cada pendiente.
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
