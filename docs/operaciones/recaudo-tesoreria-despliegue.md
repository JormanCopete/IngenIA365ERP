# Desplegar Recaudo y Tesorería (R0/T0 y entregas posteriores)

> Feature 015. Runbook de despliegue por GitOps en DEV, QA y producción. Producción sólo con **respaldo por base** y
> **autorización expresa del dueño** (Principio XII). El orden de las entregas y sus huellas está en
> [tasks.md](../../specs/015-recaudo-tesoreria/tasks.md) (fases 16 y 22) y en data-model §25.

## 1. Antes

1. **Respaldo por base**: `pg_dump` de cada base de cooperativa y de la administrativa, con el nombre `<base>-AAAAMMDD-pre-f015.dump`.
   Verifique que el archivo abre (`pg_restore --list`).
2. **Rama y release**: el PR de R0/T0 lleva en su descripción la enmienda 2.0.1 de la constitución (prefijos `RCD_` y `TES_`) y la
   desviación del Principio III aprobada el 2026-10-23 (Recaudo llama directo a los contratos dentro de su transacción).
3. **Huellas**: `LasMigracionesCongeladasNoSeRegeneran` tiene las de los pares de R0/T0 (T0271 y T0337). Ningún par congelado cambió.

## 2. Las migraciones de R0/T0, en orden

Todas aditivas salvo la de datos de los dobles; se aplican solas al arrancar (`AutoMigrate`, base por base).

| # | Par | Qué |
|---|---|---|
| 1 | `PlataformaParaRecaudoYTesoreria` | lo común: go/no-go, cuentas bancarias de personas, registro de participantes y demás de plataforma |
| 2 | `RecaudoNucleo` | las tablas `RCD_` (caja, recibos, bóveda, bancos, pagadurías, cheques, cumplimiento, activación) y sus semillas |
| 3 | `TesoreriaNucleo` | las tablas `TES_` (cuentas propias, órdenes, lotes, archivos, resultados, chequeras, remesas, extractos, activación) |
| 4 | `IntegracionContableDeRecaudoYTesoreria` | las siete tablas `ACC_` de las matrices, recepciones y vínculos de cuentas propias |
| 5 | `TesoreriaConciliacionMinima` | `TES_Reconciliations`, `TES_ReconciliationMatches`, `TES_ReconcilingItems` |
| 6 | `RecaudoYTesoreriaPuestaEnMarcha` | cuadres de apertura y cifras de referencia (`RCD_`/`TES_`), la FK `OpeningSquareId` de las activaciones |
| 7 | `RetiroDeLosDoblesDePrueba` | **sólo datos**: cierra con `ValidTo` las filas de los dobles en `COR_IntegrationParticipants` y sus adopciones; en producción no hace nada. **Pendiente**: se genera después de la puerta T0301 |

Ninguna toca `TRS_` (Tesorería heredada) ni las tablas de Cartera o Captaciones. Los tipos de comprobante `RR`, `RB`, `MC`, `AR`, `TP` los
siembra `VoucherTypesSeeder`; sus mapeos, `ReceiptsTreasuryVoucherMappingsSeeder` (Order 93). Los permisos `Receipts.*`, `Treasury.*`,
`Platform.GoLive.*`, `Accounting.ReceiptsMatrix.*` y `Accounting.TreasuryMatrix.*` los siembra **la API al arrancar** (no el DbMigrator).

## 3. GitOps

1. DEV: actualizar la imagen en el componente de la API y sincronizar. `Lending:TestDoubles:Enabled` sólo se retira de los manifiestos
   cuando el código de los dobles se borre (T0307, después de la puerta T0301).
2. QA: igual; la cooperativa del paralelo (copia de la parametrización de COOFLOPAL) se activa con la **activación mínima**
   (`IsMinimal = true`); otra cooperativa de QA se activa con la **completa** para verificar el registro de participantes.
3. Producción: con la autorización del dueño, el respaldo hecho y la ventana acordada (runbook de salida en vivo).

## 4. Después: verificar

- `GET /health` responde 200 en cada réplica.
- El arranque encuentra **un solo consumidor por destino** (`Receipts` → `DestinoRecaudo`, `Treasury` → `DestinoTesoreria`,
  `Accounting` → `DestinoContabilidad`); el log lo dice al iniciar.
- Los permisos nuevos existen (`/admin/roles`) y las siete plantillas de rol se pueden aplicar.
- `/admin/participantes-de-integracion` muestra los participantes: en producción no hay dobles.
- Las tareas `recaudo.lote-del-dia`, `recaudo.cuadre-diario`, `recaudo.antiguedad`, `recaudo.sesiones-olvidadas` y
  `tesoreria.vigilancia` aparecen en el programador.

## 5. Entregas posteriores (R1, T1, R2, T2)

Cada una trae su par nuevo y aditivo (`RecaudoCompleto`, `TesoreriaCompleta`, `RecaudoCanales`, `TesoreriaAmpliada`) y sigue este mismo
runbook. El retiro de la Tesorería heredada (`TRS_`, par `RetiroDeLaTesoreriaHeredada`) es destructivo con guarda, irreversible y en commit
propio, después de T2: se despliega **en dos versiones** —primero la que exporta y carga los pendientes, después la que borra— y sólo con la
aprobación del dueño por base ([tesoreria-retiro-heredado.md](tesoreria-retiro-heredado.md)).
