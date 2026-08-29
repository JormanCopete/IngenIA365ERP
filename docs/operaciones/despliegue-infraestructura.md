# Despliegue de infraestructura — IngenIA365ERP

> Estado: **PDN en marcha (fase 1 de 3)**. Última actualización: 2026-08-09.
> Dominio: `ingenia365.com` · Orquestador: k3s · GitOps: Argo CD · Borde: Cloudflare · Red admin: Tailscale.

## 0. Bitácora de instalación

### PDN — 85.239.231.94 (2026-08-09)

| # | Paso | Resultado |
|---|---|---|
| 1 | Respaldo previo: BD `panel` (17 tablas), globals, `.env`, `/opt/panel` | ✅ `/root/backups-pre-k8s`, permisos 600, dump verificado |
| 2 | Tailscale 1.102.2 | ✅ `erp-pdn` = **100.104.190.76**, UDP directo (sin relay DERP) |
| 3 | chrony (7 fuentes: Cloudflare, Ubuntu, Google) | ✅ deriva **0.35 µs** |
| 4 | Zona horaria del sistema `Europe/Berlin` → **UTC** | ✅ (conversión a `America/Bogota` solo en presentación) |
| 5 | `unattended-upgrades` solo seguridad, **sin reinicio automático** | ✅ 0 parches de seguridad pendientes |
| 6 | auditd | ✅ activo |
| 7 | CrowdSec 1.7.8 + firewall-bouncer (nftables) | ✅ colecciones sshd/linux/pgsql/auditd; bouncer registrado |
| 8 | k3s **v1.36.3** con `--cluster-init --secrets-encryption --disable servicelb` + reservas de CPU/RAM | ✅ nodo Ready, rol `control-plane,etcd` (crece a HA sin reinstalar) |
| 9 | Traefik: `forwardedHeaders.trustedIPs` + access logs | ✅ conserva la IP real del usuario para auditoría |
| 10 | CloudNativePG **1.30.0** | ✅ operador corriendo |
| 11 | Cluster `erp-db` PostgreSQL **17.7**, 20 Gi, sin superusuario para la app | ✅ *healthy*; contraseña generada en el servidor (nunca expuesta) |
| 12 | Bases `ingenia365erp` + `ingenia365erp_admin` | ✅ creadas |
| 13 | Argo CD (7 pods + 3 CRDs) | ✅ corriendo |

**Panel de producción intacto durante toda la instalación**: verificado HTTP 200 desde internet con TLS válido, antes y después de k3s.

**Consumo tras la instalación**: 2.2 GB RAM de 11 (9.5 libres) · 16 GB disco de 96.

**Accesos publicados**: Argo CD de PDN en `https://erp-pdn.tail72aebc.ts.net` (solo tailnet, TLS válido).

### NONPROD — 169.58.150.185 (2026-08-09)

| # | Paso | Resultado |
|---|---|---|
| 1 | Zona horaria `Europe/Berlin` → **UTC** | ✅ (mismo default del proveedor que en PDN) |
| 2 | chrony (7 fuentes), auditd, `unattended-upgrades` solo seguridad | ✅ |
| 3 | Tailscale 1.102.2 | ⏳ instalado, **pendiente de autorización** |
| 4 | CrowdSec 1.7.8 + firewall-bouncer | ✅ activos |
| 5 | k3s **v1.36.3** `--cluster-init --secrets-encryption` | ✅ nodo Ready |
| 6 | Traefik con `forwardedHeaders` + access logs | ✅ |
| 7 | CloudNativePG 1.30.0 | ✅ |
| 8 | `erp-dev` y `erp-qa`: cluster PG 17.7 (10 Gi c/u) + `ingenia365erp` y `ingenia365erp_admin` | ✅ ambos *healthy* |
| 9 | Argo CD (7 pods, 3 CRDs con server-side apply) | ✅ |
| 10 | **Observabilidad**: kube-prometheus-stack (Prometheus 30 d/20 Gi, Grafana 5 Gi, Alertmanager, kube-state-metrics, node-exporter) | ✅ 6 pods corriendo |
| 11 | PodMonitor de CloudNativePG en dev y qa | ✅ 2 PodMonitors + 9 ServiceMonitors |

**Consumo**: 2.9 GB RAM de 11 (8.7 libres) · 9.6 GB disco de 193 (5 %).
Grafana con dashboards en `America/Bogota`; retención de métricas 30 días
(la auditoría SARLAFT de 5 años vive en MongoDB, aparte).

### Cloudflare Tunnel y cierre de puertos (2026-08-10)

| # | Paso | Resultado |
|---|---|---|
| 1 | DNS `ingenia365.com` migrado de GoDaddy a Cloudflare | ✅ sin caídas; correo M365, Teams e Intune intactos (verificado) |
| 2 | SSL/TLS **Full (strict)** + TLS 1.2 mínimo + Always Use HTTPS | ✅ (HSTS deliberadamente apagado hasta estabilizar) |
| 3 | Túnel `erp-pdn` (`d3214473-38cb…`) | ✅ 8 conexiones QUIC en 5 bordes de Cloudflare |
| 4 | `cloudflared` en el clúster, 2 réplicas + PodDisruptionBudget | ✅ credencial copiada del disco al Secret sin intermediarios |
| 5 | `app.ingenia365.com` → Traefik por el túnel | ✅ HTTP 404 (correcto: llega a Traefik, ERP aún sin desplegar) |
| 6 | **IP de producción oculta** | ✅ internet ve `172.67.x.x` (Cloudflare), no `85.239.231.94` |
| 7 | Tailscale SSH desactivado (interceptaba el puerto 22 pidiendo auth por navegador) | ✅ SSH por llave sobre la malla, 6/6 estable |
| 8 | **SSH público cerrado** en ambos servidores | ✅ con interruptor de hombre muerto que reabría a los 10 min si fallaba |
| 9 | Firewall activado en NONPROD (estaba desactivado) | ✅ **cero puertos expuestos a internet** |

**Exposición final a internet**

| Servidor | Puertos abiertos |
|---|---|
| PDN | Solo 80/443 (Caddy del panel; se cierran al migrarlo al clúster) |
| NONPROD | **Ninguno** |

Servicios verificados tras el cierre: panel HTTP 200, túnel respondiendo, Grafana accesible por la malla.

> **Mejora anotada**: Tailscale SSH permite acceso sin llaves, autenticado por
> identidad de la red y con grabación de sesión — atractivo para un producto
> regulado. Requiere configurar la política ACL del tailnet.

### GitOps y cadena de despliegue (2026-08-10)

El despliegue **no usa SSH ni corre desde GitHub Actions**: Argo CD, dentro de
cada clúster, lee el repositorio privado `ingenia365-gitops` y aplica lo que
encuentra. Actions solo compila, prueba y publica imágenes.

| # | Paso | Resultado |
|---|---|---|
| 1 | Repo GitOps privado con Kustomize (`base` + overlays dev/qa/pdn) | ✅ |
| 2 | Llaves de despliegue **generadas en cada servidor** y registradas como *deploy key* de solo lectura | ✅ la clave privada nunca sale del servidor |
| 3 | Aplicaciones `erp-dev` y `erp-qa` | ✅ sincronización automática (dev además con auto-reparación) |
| 4 | Aplicación `erp-pdn` | ✅ **sincronización manual a propósito**: producción no se actualiza sola |
| 5 | Cadena validada de extremo a extremo en `erp-dev` | ✅ PostgreSQL, Redis y MongoDB desplegados por Argo CD |
| 6 | Claves de firma RS256 por ambiente (4096 bits en PDN, 2048 en dev/qa) | ✅ un token de DEV no vale en producción |
| 7 | Imágenes `api`, `web` y `migrator` construidas en el CI | ✅ ver nota abajo |

**Migraciones en producción**: `AutoMigrate=false`. El esquema lo aplica un Job
`PreSync` de Argo CD con la imagen del `DbMigrator` **antes** de rotar los pods,
de modo que ninguna versión de la aplicación arranca contra un esquema viejo.

> **Corrección del CI encontrada aquí**: las imágenes solo se construían *después*
> de mergear, así que un `Dockerfile` roto se descubría tarde. Ahora se construyen
> también en cada PR (sin publicarlas). El primer intento destapó dos fallos
> reales y preexistentes: los `Dockerfile` no copiaban los proyectos de
> migraciones del feature 004, y el nombre de las imágenes llevaba mayúsculas
> (`JormanCopete/IngenIA365ERP`), que un registro Docker rechaza.

### Pendientes que bloquean el avance (acción manual)

| # | Acción | Desbloquea |
|---|---|---|
| ✅ M1 | Tailscale en el PC de trabajo | hecho — SSH por la malla verificado |
| ✅ M2 | HTTPS certificates en la tailnet | hecho — Argo CD publicado con TLS |
| ✅ M3 | Autorizar `erp-nonprod` en la tailnet | hecho — Grafana y Argo CD de nonprod publicados |
| ✅ M4 | Migrar nameservers de `ingenia365.com` (GoDaddy → Cloudflare) — ver [guía](migracion-dns-cloudflare.md) | hecho — túnel y dominios operativos |
| ✅ M5 | Mergear el PR a `develop` | hecho — imágenes publicadas y ERP desplegado en DEV y QA |
| ✅ M6 | Repositorio y paquetes de GHCR en privado + Secret `ghcr-pull` | hecho — verificado con prueba de control (sin el secreto la descarga falla con 401) |
| ✅ M7 | Crear bucket S3 `ingenia365-erp-backups` + usuario IAM dedicado | hecho — con Object Lock, versionado, cifrado y permisos mínimos |
| ⏳ M8 | **Desactivar la llave AWS `AKIAQ3EG…`** (quedó expuesta en una conversación) | Ya no la usa el ERP, pero sigue activa. Ver P1 en [estado-y-pendientes.md](estado-y-pendientes.md) |
| ✅ M9 | Respaldo propio de **MongoDB** | hecho — replica set con oplog, volcado diario y **restauración probada** en los tres ambientes |

### Estado de los ambientes (2026-08-14)

| Ambiente | Estado | Sincronización |
|---|---|---|
| DEV | ✅ operativo | automática con auto-reparación |
| QA | ✅ operativo | automática sin auto-reparación |
| PDN | ✅ **operativo** — https://app.ingenia365.com, login verificado en navegador | manual |

> 📋 El estado detallado (respaldos, observabilidad, seguridad) y **la lista de
> pendientes priorizada** viven en [estado-y-pendientes.md](estado-y-pendientes.md).
> Este documento conserva el diseño y la bitácora de instalación.

### Flujo de ramas y ambientes

| Rama | Etiqueta de imagen | Ambiente | Cómo llega |
|---|---|---|---|
| `develop` (por defecto) | `:develop` | DEV y QA | Argo CD sincroniza solo |
| `release` | `:release` + `:latest` | **PRODUCCIÓN** | Argo CD **requiere aprobación manual** |

Producción se despliega **desde `release`**, no desde `main`. Se promociona con un
merge `develop → release`: eso publica las imágenes `:release`, y recién entonces
alguien aprueba la sincronización en Argo CD. Son dos actos deliberados y
separados — publicar una imagen no despliega nada por sí solo.

`main` queda sin uso en este flujo; el CI la sigue construyendo por si se
conserva como rama histórica.

> ⚠️ **Publicar una imagen no basta para desplegarla.** Las etiquetas son
> móviles: republicar `:release` no cambia el manifiesto, así que Kubernetes no
> ve diferencia y no rota los pods. Por eso los Deployments usan
> `imagePullPolicy: Always`. Queda pendiente fijar el **digest** (P3), que es lo
> único que deja registro de qué artefacto está en producción.
>
> La primera promoción se hizo el 2026-08-14, con los respaldos de ambos motores
> ya verificados —incluida la restauración— y las alertas funcionando.

> **M5 usa S3 de AWS** por decisión del usuario (ya disponible), no Backblaze B2
> como se había diseñado. El diseño con Object Lock sigue siendo el objetivo:
> S3 ofrece Object Lock equivalente y conviene activarlo en el bucket nuevo.

## 1. Inventario real de servidores (verificado 2026-08-09)

| | **PDN** | **NONPROD** | **194.163.161.8** |
|---|---|---|---|
| IP | 85.239.231.94 | 169.58.150.185 | 194.163.161.8 |
| SO | Ubuntu 24.04.4 LTS (k6.8) | Ubuntu 24.04.4 LTS (k6.8) | **Ubuntu 20.04.2 (k5.4) — EOL** |
| Recursos | 6 vCPU · 11 GB · 99 GB | 6 vCPU · 11 GB · 193 GB | 4 vCPU · 7.8 GB · 194 GB |
| Libre | 10 GB RAM · 86 GB disco | 10.5 GB RAM · 190 GB disco | **1.1 GB RAM** · 126 GB |
| Ocupado por | Panel .NET (80/443) + PostgreSQL 16.14 | nada (limpio) | **Producción de cuenta365/carteravirtual** |
| Firewall | UFW activo (solo SSH) | **UFW inactivo** | **UFW inactivo** |
| Veredicto | ✅ apto para PDN | ✅ apto para DEV/QA | ❌ **NO usable para monitoreo** |

### 1.1 Detalle de PDN

- **Panel .NET 10** (`panel-app` + `panel-caddy`, `/opt/panel`) sirviendo `panel.ingenia365.com`. Blazor Server+WASM, Dockerfile multi-stage correcto (evita el workload `wasm-tools` a propósito: sin emscripten/Python y con menos RAM en el build — **reutilizar ese patrón para la imagen del ERP**).
- **PostgreSQL 16.14 nativo**, base `panel` (17 tablas, 8.7 MB), rol `panel_app`. Sin tunear: `shared_buffers 128MB`, `max_connections 100`. TLS con certificado *snakeoil*. `listen_addresses='*'` pero **UFW bloquea 5432 desde internet** (verificado externamente).
- Expuesto a internet: solo 22, 80, 443.

### 1.2 Por qué 194.163.161.8 queda fuera del alcance

Es un **servidor de producción con clientes reales** (nginx sirve `appfemcristar`, `appfetmy`, `appfevima`, `appfonempi`, `appcooflopal`, `apifaempais`, `cooflopal.carteravirtual.co`…), con SQL Server (5 GB de RAM), servidor de correo completo (poste.io, dovecot, rspamd), ClamAV y apps .NET/Node.

Motivos para no instalar la observabilidad del ERP allí:

1. **Sin RAM**: 1.1 GB disponibles; el stack de observabilidad necesita 2–4 GB. Instalarlo pondría en riesgo el correo y el SQL Server de las cooperativas actuales.
2. **SO fuera de soporte**: Ubuntu 20.04 (EOL desde abril 2025), kernel 5.4, **324 actualizaciones pendientes**, 22 semanas sin reiniciar.
3. **Aislamiento**: el sistema que vigila un ERP financiero regulado no puede compartir destino con un servidor multiuso de mayor superficie de ataque.

## 2. 🚨 Riesgos de seguridad detectados (acción independiente de este proyecto)

En **194.163.161.8**, con UFW inactivo y verificado desde internet:

| Riesgo | Detalle | Acción recomendada |
|---|---|---|
| 🔴 **SQL Server abierto a internet** | Puerto **1433 accesible públicamente**; contiene datos de cooperativas | Bindear a `127.0.0.1` o cerrar 1433 con firewall **hoy**. Revisar logs de accesos. |
| 🔴 **FTP en texto plano** | **vsftpd en el puerto 21 público**; credenciales y archivos viajan sin cifrar | Apagar vsftpd o reemplazar por SFTP |
| 🟠 **SO sin parches** | Ubuntu 20.04 EOL + 324 paquetes pendientes + kernel de hace 22 semanas | Plan de actualización a 22.04/24.04 con ventana de mantenimiento |
| 🟠 **Sin firewall** | UFW inactivo: todo puerto que abra un servicio queda público | Activar UFW con allowlist (¡cuidado de no perder SSH!) |

En **169.58.150.185** (DEV/QA): UFW inactivo, pero hoy solo escucha SSH — se resuelve en el hardening inicial.

## 3. Decisiones tomadas

| # | Decisión | Justificación |
|---|---|---|
| D1 | **k3s** (no kubeadm) | Kubernetes certificado, ~500 MB de overhead, `--cluster-init` permite crecer a HA con etcd embebido sin migrar |
| D2 | **Cloudflare Tunnel** (sin puertos entrantes) | Resuelve el conflicto con el Caddy del panel, oculta la IP, y elimina 80/443 públicos |
| D3 | **Panel migra al clúster** | Un solo ingress, gestionado por Argo CD junto al ERP |
| D4 | **CloudNativePG** para el ERP | Aislamiento del panel, backups a S3 con PITR, camino declarativo a réplicas/failover |
| D5 | **Web + API en el mismo host bajo `/api`** | Cero CORS, una sola imagen WASM para los 3 ambientes, refuerza FR-007. `api.ingenia365.com` queda como alias para MAUI/integraciones |
| D6 | **Traefik con IngressRoute** (no Gateway API todavía) | Menos conceptos y más ejemplos; migrar a Gateway API es reescribir ~5 manifiestos |
| D7 | **SOPS + age** para secretos (no Infisical self-hosted) | ~20 secretos no justifican una BD + master key + backups propios. Se migra a un gestor central vía External Secrets si compliance lo exige |
| D8 | **Kyverno en modo `Audit` primero** | Un fallo de verificación de firma que bloquea despliegues es opaco a las 2 a.m.; pasar a `Enforce` tras semanas de rodaje |
| D9 | **Argo CD por clúster** (uno en PDN, uno en NONPROD) | Radio de impacto acotado: un incidente en nonprod no alcanza producción |
| D10 | **Nomenclatura plana de dominios** | El Universal SSL gratuito de Cloudflare cubre apex + **un** nivel: `app-qa.` sí, `app.qa.` obligaría a pagar Advanced Certificate Manager |

## 4. Matriz de dominios

Leyenda: 🟠 público vía Cloudflare Tunnel · 🔒 solo Tailscale · 🛡️ Cloudflare Access

| Dominio | Servicio | Ambiente | Exposición |
|---|---|---|---|
| `ingenia365.com` + `www` | **Web institucional (WordPress en hosting administrado externo)** — fuera de la infraestructura del ERP a propósito: WordPress es el software más atacado de internet y no puede compartir nodo con un sistema financiero regulado | — | 🟠 (apunta al hosting, no a las VPS) |
| **`app.ingenia365.com`** | **Web + `/api` — entrada única de todas las cooperativas** | PDN | 🟠 |
| `api.ingenia365.com` | Alias solo `/api` (MAUI, integraciones) | PDN | 🟠 |
| `master.ingenia365.com` | Consola SaaS (`/api/saas/*`) | PDN | 🟠 + 🛡️ |
| `panel.ingenia365.com` | Panel actual (migrado al clúster) | PDN | 🟠 |
| `app-qa.ingenia365.com` | Web + API de QA | NONPROD | 🟠 + 🛡️ |
| `app-dev.ingenia365.com` | Web + API de DEV | NONPROD | 🔒 |
| `argocd-pdn.ingenia365.com` | Argo CD producción | PDN | 🔒 |
| `argocd-np.ingenia365.com` | Argo CD nonprod | NONPROD | 🔒 |
| `grafana.ingenia365.com` | Grafana | MON | 🔒 |
| `alerts.ingenia365.com` | Alertmanager | MON | 🔒 |
| `status.ingenia365.com` | Status page pública | MON | 🟠 |

**Reglas duras:**
- **Cero subdominios por cooperativa** (FR-007): el tenant sale del claim `active_tenant_id`, nunca del host.
- Ningún registro DNS gris apunta a la IP de una VPS. Correo saliente por **relay externo** (SES/Postmark/Brevo), nunca MX propio en estas máquinas.
- `CAA`: `0 issue "letsencrypt.org"` **y** `0 issuewild "letsencrypt.org"` (ambas líneas, o el wildcard falla).

## 5. Matriz de puertos

Leyenda: 🌐 público · 🔒 solo `tailscale0` · 🔗 solo red entre nodos · 🏠 solo localhost · 🐳 solo ClusterIP · ⛔ cerrado

### PDN y NONPROD (nodos k3s)

| Puerto | Servicio | Exposición | Nota |
|---:|---|---|---|
| 22 | SSH | 🔒 | Tras Tailscale, se cierra al público |
| 80 / 443 | Ingress | ⛔ | **Cerrados**: el tráfico entra por Cloudflare Tunnel |
| 41641/udp | Tailscale | 🌐 | Evita relay DERP |
| 6443 | k3s API | 🔒 | **Jamás público** |
| 2379-2380 | etcd | 🔗 | **Jamás público** (solo con HA) |
| 8472/udp | flannel VXLAN | 🔗 | **Jamás público**; con 2+ nodos usar `wireguard-native` |
| 10250 | kubelet | 🔗 | **Jamás público** (equivale a exec en pods) |
| 5432 | PostgreSQL | 🏠 + 🐳 | Nativo (panel) → `127.0.0.1`; ERP → CNPG sin `hostPort` |
| 6379 / 27017 | Redis / MongoDB | 🐳 | Sin `hostPort`, con contraseña |
| 8080 | API .NET | 🐳 | Solo Traefik lo consume |
| 9100 / 9187 | node/postgres exporter | 🔒 | Bind a la IP de Tailscale |
| 7844 | cloudflared | egress | Única vía de entrada de usuarios |

**Doble firewall**: panel del proveedor **y** `nftables`/UFW del host. En nodos k3s la `chain forward` va en **`policy accept`** (un `drop` rompe flannel); el filtrado este-oeste se hace con **NetworkPolicy**.

## 6. Orden de instalación (crítico: evita quedarse sin acceso)

> ⚠️ **Tailscale ANTES que el firewall.** Cerrar SSH al público antes de tener la red privada arriba deja el servidor inaccesible.

### Fase 0 — Preparación (manual del usuario)
1. Migrar nameservers de `ingenia365.com` de GoDaddy a Cloudflare.
2. Crear tailnet y cuenta de Cloudflare Zero Trust.
3. Crear bucket de backups (Backblaze B2 con Object Lock).

### Fase 1 — PDN
1. Tailscale (`tag:prod`) + verificar SSH por la malla.
2. Hardening base: SSH solo por llave, `unattended-upgrades`, chrony (≥3 fuentes), auditd, CrowdSec.
3. UFW/nftables **después** de confirmar acceso por Tailscale.
4. Backup de la BD `panel` + `pg_dump` de respaldo.
5. k3s server con `--cluster-init --secrets-encryption --disable traefik,servicelb`.
6. Traefik (Helm) + cert-manager + Cloudflare Tunnel (cloudflared con 2 réplicas).
7. CloudNativePG + cluster de Postgres del ERP + backup a B2.
8. Argo CD + repo GitOps + secretos con SOPS/age.
9. Migrar el panel al clúster (su BD a CNPG) y **recién entonces** apagar el Caddy del host.
10. Desplegar el ERP (namespace `erp-pdn`).

### Fase 2 — NONPROD
Igual que PDN, con dos namespaces (`erp-dev`, `erp-qa`), sin panel, y `AutoMigrate=true`.

### Fase 3 — Observabilidad
Ver §7: pendiente de decidir ubicación.

## 7. Observabilidad — ubicación pendiente

El servidor previsto (194.163.161.8) **no es viable** (§1.2). Opciones:

| Opción | Pros | Contras |
|---|---|---|
| **A. En la VPS NONPROD** (recomendada para arrancar) | Cero costo; tiene 10 GB RAM y 190 GB libres | Si nonprod cae, se pierde visibilidad de PDN → se mitiga con un chequeo externo gratuito (UptimeRobot/Cloudflare) |
| **B. Cuarta VPS dedicada** (~€5/mes) | Aislamiento correcto: el vigilante no comparte destino con lo vigilado | Costo y una máquina más que mantener |
| **C. Grafana Cloud free** | Sin infraestructura; está fuera de tus VPS por diseño | Datos de operación en un tercero; límites del plan free |

Stack (fase 1): VictoriaMetrics + Grafana + Alertmanager. Loki y trazas en fase 2.

## 8. Riesgos abiertos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| PDN es un solo nodo: sin HA real | Backups con PITR + runbook de restauración; segundo nodo cuando el negocio lo justifique |
| Parcheo de kernel requiere reinicio del único nodo | Ventana de mantenimiento anunciada + Ubuntu Livepatch |
| Retención SARLAFT de 5 años | MongoDB **necesita backup propio** (hoy no lo tiene en el plan): `mongodump`/PBM a B2 con Object Lock |
| Hora legal para cierres contables | Contenedores en UTC, `timestamptz` en BD, conversión a `America/Bogota` solo en presentación; alerta si la deriva de chrony supera 250 ms |
| Custodia de secretos raíz | Escrow documentado de: llave age de SOPS, token de k3s, claves JWT históricas (retención 5 años) |
| SignalR (`/hubs/notifications`) con varias réplicas | Backplane de Redis obligatorio antes de escalar la API |
| Límites de recursos | `LimitRange` + `ResourceQuota` por namespace; reservar explícitamente CPU/RAM del sistema |
| Imágenes sin escanear | Trivy en CI con gate por severidad + SBOM CycloneDX como attestation |
