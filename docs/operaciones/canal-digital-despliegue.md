# Canal digital del asociado — despliegue (feature 021)

El canal es un **sistema satélite** (constitución v2.1.0): imagen propia, base propia y CI propio
(`.github/workflows/canal.yml`). Este runbook dice qué hace falta en cada clúster. **El cambio en el repositorio
`ingenia365-gitops` lo hace el dueño, o se autoriza aparte** (regla del dueño: nada de producción sin autorización
expresa).

## Imagen

- `ghcr.io/jormancopete/ingenia365erp/canal`: binario Go con la PWA embebida, sobre `distroless/static:nonroot`.
- Construida desde `canal/` con `canal/api/Dockerfile`; el CI la publica en push a `develop` (dev y qa) y `release`
  (pdn) y escribe su **digest** en `workloads/canal/overlays/{dev,qa|pdn}/kustomization.yaml`. Si el overlay todavía
  no existe, el CI avisa y no despliega.

## Workload en GitOps (`workloads/canal/`)

```text
workloads/canal/
├── base/
│   ├── kustomization.yaml
│   ├── deployment.yaml      # 2 réplicas, puerto 8080, sondas a /salud, sin root, recursos explícitos
│   ├── service.yaml         # ClusterIP 8080
│   ├── ingressroute.yaml    # Traefik: sólo /, /api/v1, /webhooks, /.well-known  (NUNCA /internal)
│   └── networkpolicy.yaml   # /internal sólo desde los pods de la API del ERP
└── overlays/{dev,qa,pdn}/
    ├── kustomization.yaml   # images: - name: ghcr.io/jormancopete/ingenia365erp/canal (digest lo escribe el CI)
    └── configuracion.yaml   # ConfigMap: host público, URL interna del ERP, frecuencias
```

- **Namespace**: el mismo del ERP en cada clúster (`erp-dev`, `erp-qa`, `erp-pdn`), para que la NetworkPolicy y la
  URL interna del ERP sean simples.
- **NetworkPolicy**: la ruta `/internal/v1` del canal y la ruta `/api/canal/v1` del ERP sólo se alcanzan entre los
  pods de los dos servicios. Traefik no publica ninguna de las dos.
- `ghcr-pull`: el mismo Secret de las otras imágenes.

## Base de datos

- Base `ingenia365_canal` en el PostgreSQL del clúster (CloudNativePG), con un rol propio sin superusuario y sin
  acceso a ninguna base de cooperativa (regla 1 del satélite).
- Las migraciones (`goose`) las aplica el propio servicio al arrancar, con un candado de asesoría: el canal es una
  sola base, no una por cooperativa.
- Respaldo diario igual que las demás bases, con su prueba de restauración.

## Secrets (los carga el dueño; nunca en el repositorio)

| Secret | Contenido |
|---|---|
| `canal-llaves` | llave privada ES256 del canal (firma hacia el ERP) y llave pública ES256 del ERP (verifica `/internal`) |
| `canal-kek` | llaves de cifrado de llaves por versión (`v1`, `v2`…) para los secretos de pasarela |
| `canal-smtp` | credenciales del relevo de correo (las mismas del ERP o propias) |
| `canal-base` | cadena de conexión a `ingenia365_canal` |

Del lado del ERP, la API necesita:
- `CanalServicio__LlavesPublicas__{kid}`: la llave pública del canal;
- `CanalServicio__LlavePrivadaErp`: la llave privada con la que el ERP firma hacia el canal;
- `CanalServicio__UrlInterna`: la URL del Service del canal.

La rotación está en `docs/operaciones/canal-digital-llaves.md` (fase de pulido).

## Dominio y túnel

- Hosts: `asociados-dev.ingenia365.com`, `asociados-qa.ingenia365.com`, `asociados.ingenia365.com`. Un solo dominio
  para todas las cooperativas: no hay subdominios por cooperativa.
- Rutas en el ConfigMap de `cloudflared` de cada clúster hacia Traefik. Cloudflare **no** debe cachear `/api/*` ni
  `/webhooks/*`; los archivos con huella de la PWA se sirven con `immutable`.
- La URL del webhook que se registra en cada sitio de la pasarela es
  `https://asociados.ingenia365.com/webhooks/placetopay/{sitioPublicId}`. El valor exacto lo muestra la pantalla
  `/canal/destinos` del ERP.

## Orden del primer despliegue en DEV

1. Crear la base y el rol (`ingenia365_canal`).
2. Generar las llaves con `canal/scripts/llaves-dev.sh`, fuera del repositorio, y cargar los Secrets del canal y las variables del ERP.
3. Crear `workloads/canal/` en GitOps con los overlays y su aplicación de Argo CD.
4. Agregar la ruta del túnel.
5. Hacer push a `develop` en `canal/**`: el CI publica la imagen y escribe el digest.
6. Verificar `/salud` y que `GET /api/canal/v1/tenants` del ERP responde al canal y rechaza cualquier otra llamada.

Producción repite los mismos pasos, sólo con autorización expresa del dueño y con sincronización manual en Argo CD.
