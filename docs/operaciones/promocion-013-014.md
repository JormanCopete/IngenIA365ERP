# Plan de merge y promoción: Cartera (013) y Captaciones (014)

> Preparado el 2026-10-21. Para quien autoriza (el dueño) y para quien despliega. Nada de esto se ha ejecutado: **todo está en commits
> locales, sin push**. Los pasos que tocan producción exigen la **autorización expresa del dueño en el momento** (el clasificador de
> seguridad bloquea los pushes de configuración de producción sin ella).
> Recetas que este plan ordena, sin repetirlas: [cartera-retiro-heredado.md](cartera-retiro-heredado.md),
> [captaciones-retiro-heredado.md](captaciones-retiro-heredado.md), [captaciones-despliegue.md](captaciones-despliegue.md),
> [cartera-despliegue-k4.md](cartera-despliegue-k4.md), [cartera-puesta-en-marcha.md](cartera-puesta-en-marcha.md),
> [captaciones-puesta-en-marcha.md](captaciones-puesta-en-marcha.md), [valores-nacionales.md](valores-nacionales.md).

## 0. Qué se va a mover

| | |
|---|---|
| Rama a integrar | `014-ahorros-aportes-cdt` (worktree `.claude/worktrees/014-ahorros-aportes-cdt`); **contiene toda la 013** (la 013 es su ancestro) |
| Respecto de `origin/develop` | **415 commits por delante y 0 por detrás**: se integra sin conflictos (avance rápido posible) |
| Tamaño | 3.451 archivos, ~3,4 millones de líneas añadidas (código, pruebas, manuales y especificaciones) |
| Migraciones nuevas | **Application: 24** (PostgreSQL y SQL Server, en par) y **Admin: 1** (`ValoresNacionales`) |
| De ellas, destructivas | **2**, con guarda: `RetiroDeLaCarteraHeredada` y `RetiroDeCaptacionesHeredadas`; el resto es aditivo |
| Corrección `fix/e2e-reversa-contable` | **ya está dentro** (`dbc92aa9` está en la 013): el worktree `wt-fix-e2e-contable` queda sin objeto |
| `develop` local | tiene un commit propio sin publicar (`275f42d1`, formatos COOFLOPAL v2): **no mezclarlo** en este merge; decidir aparte si se publica |

Migraciones nuevas, en el orden en que las aplica el migrador: `PlataformaParaCartera`, `NominaRecaudaPorElContrato`, `ComercioConCartera`,
**`RetiroDeLaCarteraHeredada`**, `CarteraNucleo`, `CarteraRiesgo`, `IntegracionContableDeCartera`, `CarteraLibranza`, `CarteraNovedades`,
`CarteraCobranza`, `CarteraCobranzaSalida`, `CarteraInformes`, `CarteraComprobanteDeCargue`, `CarteraPerdidaEsperada`, `CarteraVivienda`,
**`RetiroDeCaptacionesHeredadas`**, `CaptacionesAportes`, `ContractualDestinosAlVencer`, `CaptacionesRespuestasDelDueno`,
`CaptacionesGmfEnEntregas`, `CaptacionesRegiroDeOrdenes`, `CaptacionesAsambleaRetirados`, `CaptacionesRetencionDeAsamblea`,
`CaptacionesBaseDeRetornosPorPlantilla`. En Admin: `ValoresNacionales`.

## 1. Estado de la evidencia (lo que respalda el merge)

- Pruebas sin contenedores, todas verdes al 2026-10-22: Domain 2.652, Application 6.254, Architecture 759, Shared 499.
- e2e completa en PostgreSQL y SQL Server del 2026-10-22: 667 y 668 pasan (19 omitidas, las de rendimiento); las 3 fallas de esa corrida (lista
  de informes y un tropiezo del contenedor) están corregidas en `d13be1c4`. Después se construyeron tres cosas más (E262–E264) con sus e2e
  acotadas en verde.
- Paridad de migraciones PostgreSQL/SQL Server en verde y `has-pending-model-changes` limpio en los dos motores.

## 2. Antes del merge (puertas, en este orden)

1. **Una última corrida e2e completa en los dos motores** sobre el commit exacto que se va a integrar (~3 h; el vigilante de disco
   `scratchpad/e2e-final.sh` detiene la corrida si C: baja de 4 GB). Es la única puerta técnica que falta. Antes, **compactar el disco de Docker
   como administrador** (`wsl --shutdown` y luego `diskpart` con `select vdisk file=…docker_data.vhdx`, `attach vdisk readonly`, `compact vdisk`, `detach vdisk`, en PowerShell como administrador y con Docker Desktop cerrado).
2. **Congelar la rama**: sin más cambios de esquema; cualquier corrección posterior es una migración nueva.
3. **Decidir el contenido de las notas de la promoción** (§8) y revisar `CLAUDE.md`/`docs/INDICE-DOCUMENTACION.md`.
4. **Decisión del dueño sobre la forma del merge** (recomendada: un solo PR de `014-ahorros-aportes-cdt` a `develop` con merge no rápido,
   conservando los 415 commits para poder auditarlos). Alternativa: dos merges (013 y luego 014) si se quiere poder revertir las captaciones
   sin tocar Cartera; no recomendada, porque las dos comparten matriz, plantillas, permisos y documentos.

## 3. El merge a `develop` y la promoción por ambientes

| Paso | Ambiente | Cómo se llega | Qué pasa | Quién autoriza |
|---|---|---|---|---|
| 3.1 | `develop` | PR y merge | La canalización compila una vez, arma imágenes (~9 min) | dueño: el merge |
| 3.2 | **DEV** | automático (Argo CD, selfHeal) | El migrador aplica las 24 + 1; las dos destructivas pasan por su guarda | automático |
| 3.3 | **QA** | automático (sin selfHeal) | Igual; aquí se hace la prueba por rol y la validación con la contadora | automático; el dueño valida |
| 3.4 | **PDN** | manual (release y sincronización de Argo CD) | Sólo con §4 completo | **dueño, expresa y en el momento** |

Antes del 3.2 en **DEV y QA**: diagnóstico de las tablas heredadas en cada base (cooperativa y administrativa) con
[diagnostico-cartera-heredada.sql](../../specs/013-cartera-financiera/diagnostico-cartera-heredada.sql) y
[diagnostico-captaciones-heredadas.sql](../../specs/014-ahorros-aportes-cdt/diagnostico-captaciones-heredadas.sql), y la fila de la guarda
(`LND.RetiroHeredado.Aprobado` y `LND.CaptacionesHeredadas.RetiroAprobado`) en cada base **si hay filas** (con las tablas vacías la guarda pasa
sola; la decisión D15 del dueño pide además la fila en todas las bases de captaciones). Respaldo `pg_dump -Fc` de cada base, aunque DEV y QA
sean de prueba. Anotar los resultados en las tablas de §9 de cada runbook de retiro.

## 4. Producción: lista de verificación antes de tocar nada

1. **Diagnóstico** de las dos listas de tablas heredadas en **cada base** de PDN (administrativa y de cada cooperativa) por el túnel de
   Tailscale (`erp-pdn`, `k3s kubectl -n erp-pdn exec erp-db-1 -c postgres -- psql …`). Si todo da 0 filas, las guardas pasan solas; si hay filas, **no se sigue**:
   se detiene y se decide con el dueño.
2. **Respaldo de cada base** inmediatamente antes: `pg_dump -Fc` a `/root/respaldos/<base>-AAAAMMDD-pre-f013-f014.dump`, con tamaño y cabecera
   `PGDMP` anotados (mismo procedimiento de la 010).
3. **Cabeceras `MIGRACION-DESTRUCTIVA-APROBADA`** de los dos retiros con las líneas `Diagnóstico:`, `Respaldo:` y `Segundo revisor:` (el dueño
   es el segundo revisor, D15 y H7), en un commit que **sólo toca esos comentarios** (ver
   [captaciones-despliegue.md](captaciones-despliegue.md) §2.3: la huella de congelamiento no los incluye).
4. **La fila de la guarda** en cada base de PDN antes de que el migrador corra.
5. **Valores nacionales** en la base administrativa (no hay semilla de usura, UVR ni topes reales): registrarlos desde `/admin/valores-nacionales`
   o por la plantilla `saas.national-values` ([valores-nacionales.md](valores-nacionales.md)). Sin la usura, **no se activa ninguna línea**.
6. **Fecha y hora acordadas**, ventana sin nómina ni cierre contable en curso, y quien despliega con el dueño al lado.
7. **Autorización expresa del dueño** para PDN, en el momento del push de GitOps.

## 5. Después del despliegue en cada ambiente

1. El migrador terminó y la API arrancó (los permisos `Lending.*` y de captaciones los siembra la API al arrancar, no el migrador).
2. Verificaciones de [captaciones-despliegue.md](captaciones-despliegue.md) §7 y [cartera-puesta-en-marcha.md](cartera-puesta-en-marcha.md):
   las tablas heredadas ya no existen, las nuevas sí, `CatalogoDeCartera.EntregaVigente = K11`, el menú muestra «Cartera» y «Ahorros y aportes»,
   `/api/lending/...` responde y las rutas heredadas dan 404.
3. La **semilla de etapas de cobranza** corre sola al arrancar; la **corrida de etapas migradas** se dispara una vez por cooperativa
   ([cartera-despliegue-k4.md](cartera-despliegue-k4.md) §3).
4. **Ninguna cooperativa activa Cartera ni captaciones todavía**: la activación exige recaudador y tesorería reales (sólo los dobles `TST`/`TES`
   cuentan fuera de producción), la usura registrada, la matriz contable completa y el cuadre de la carga. En producción, hoy, **no hay
   recaudador ni tesorería**: el despliegue es seguro porque nadie podrá activar.
5. QA: prueba a mano por rol (administrador, jefe, analista, gerente, consejo, jefe de captaciones, gestor de cobranza) y recorrido de
   pantallas; **MAUI a mano** (nunca ha tenido pruebas automáticas) y la matriz contable con la contadora (las reglas están **sin cuentas**
   a propósito: con «Bloquear» nada contabiliza hasta que las asigne).

## 6. Vuelta atrás

- Las 22 migraciones aditivas se deshacen con el `Down` o simplemente se dejan (tablas vacías sin uso); el código anterior sigue
  funcionando sobre el esquema nuevo porque nada anterior lee las tablas nuevas.
- Las **dos destructivas no se deshacen con `Down`**: se vuelve desde el **respaldo** de cada base (`pg_restore`), con la pérdida declarada en
  [cartera-retiro-heredado.md](cartera-retiro-heredado.md) §8 y [captaciones-retiro-heredado.md](captaciones-retiro-heredado.md) §7.
  Por eso el respaldo y la verificación de su cabecera son obligatorios.
- Código: revertir el release en GitOps al anterior y sincronizar Argo CD.

## 7. Lo que NO sale con esta promoción (y por qué no bloquea)

| Pendiente | Quién | Efecto en la promoción |
|---|---|---|
| **Recaudo y Tesorería como features propias** (hoy sólo dobles de prueba) | dueño: especificar y construir | **Bloquea la activación**, no el despliegue; es la dependencia crítica de COOFLOPAL (01/12/2026) |
| Cuentas de la matriz contable (castigo, condonación, ingreso diferido, GMF de entregas, cargos, retención de la asamblea…) | contadora | Sin ellas nada contabiliza (por diseño) |
| Retención de la asamblea y GMF en entregas por fallecimiento | tributarista y contadora | Parámetros vacíos o «pendiente de validar» con defectos seguros |
| Cotejo de layouts SICSES y centrales, Anexo 2 (pérdida esperada, PI voluntaria), instructivo del 9026 (T235), UVR y topes de vivienda | dueño / contadora | Se generan con alerta de «sin cotejar»; nadie los envía sin cotejo |
| Proveedor de SMS y WhatsApp | dueño, otra fase | Los avisos salen por correo; SMS/WhatsApp simulados y sin salida en producción |
| QA por rol y MAUI a mano (T1078–T1089, T996–T997) | dueño y equipo | Se hacen en QA, antes de PDN |
| Tipos de entidad, líneas y parámetros de cada cooperativa | implantación | Después del despliegue, con las recetas de puesta en marcha |

## 8. Notas para las cooperativas (texto sugerido)

- El grupo de menú «Cartera Financiera» pasa a llamarse **«Ahorros y aportes»** (mismas pantallas y direcciones `/cartera/...`); la **Cartera**
  de créditos es nueva y reemplaza a la heredada (que se retira).
- Quien integre por API cambia `/api/lending/{familia}` heredado por **`/api/savings/{familia}`** y
  `/api/reports/lending/person-portfolio` por **`/api/reports/savings/person-portfolio`**.
- Las cuentas migradas se inactivan como cualquier otra: **la primera noche tras activar** las de último movimiento antiguo quedarán inactivas y
  recibirán aviso (la revisión previa de la carga dice cuántas son).
- Los avisos al asociado salen por correo; SMS y WhatsApp llegarán en otra fase.

## 9. Orden resumido (lo que haría yo, si el dueño aprueba)

1. Compactar el disco de Docker y correr la **e2e completa final** sobre la rama congelada. 2. PR y merge a `develop` (dueño). 3. DEV y QA se
actualizan solos: diagnóstico, respaldos y guardas antes (§3). 4. QA: prueba por rol, MAUI y matriz contable con la contadora. 5. Valores
nacionales reales en la base administrativa. 6. Cabeceras de aprobación, diagnóstico y respaldo de PDN con el dueño como segundo revisor
(§4). 7. **Autorización expresa** y release a PDN. 8. Verificaciones (§5) y notas a las cooperativas (§8). 9. Mientras tanto, especificar
Recaudo y Tesorería, que son lo que habilita salir en vivo.
