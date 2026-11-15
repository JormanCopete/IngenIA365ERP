# Transporte y conectores: puesta en marcha

Feature 020. **Producción sólo con autorización expresa del dueño.**

## 1. Respaldos

Antes de migrar, `pg_dump` de la base administrativa y de cada cooperativa (`-pre-f020`). Las dos
migraciones son aditivas, pero el respaldo va igual.

## 2. Migraciones

| Base | Migración | Qué trae |
|---|---|---|
| Administrativa | `ModulosYConectoresDePlataforma` | `ADM_TenantModules`, `ADM_Connectors`, `ADM_ConnectorVersions`, `ADM_TenantConnectors` |
| Cada cooperativa | `Transporte` | 15 tablas `TRN_`, 5 `COR_Connector*` y `COR_People.IsDriver` |

Las aplica `AutoMigrate` al arrancar o el DbMigrator. Son aditivas: no tocan datos existentes.

## 3. Habilitar el módulo

El maestro, en `/saas/modulos`, enciende Transporte en la cooperativa con motivo. En la siguiente pasada
del programador (`modulos.aprovisionar`) quedan los catálogos sembrados y los participantes `TRN` y `CON`.

## 4. Cartera, Comercial y servicios

1. **Línea de Cartera de servicio** (si la cooperativa tiene Cartera): marcada `IsService`, sin interés, que
   admita origen comercial (`AllowsCommercialOrigin`) y no asociados (`AllowsNonAssociates`).
2. **Medios de pago a crédito**: uno de clase crédito de asociado y otro de cliente, los dos con **pago
   parcial** (una factura lleva un pago a crédito por servicio).
3. **Productos** de Comercial de clase servicio, con su precio vigente (radio, app…).
4. **Tipo de documento de venta** de la facturación de servicios y, si factura electrónicamente, su canal
   DIAN configurado.
5. En Transporte › Servicios: cada producto como servicio, con su prioridad, línea, medios y conector.
6. La política de facturación con su vigencia.

## 5. Límite conocido: pagos sin identificar

Un pago que llega por banco o convenio **sin** decir a qué obligación va (`AplicacionNoInteractiva`) no se
reparte solo: quien lo identifica elige las partes. Hasta que se identifique, el servicio no queda pagado
ni se avisa (research R13).

## 6. Conectores

1. El maestro define el conector y publica su versión (`/saas/conectores`), la **simula** y lo habilita en la
   cooperativa.
2. La cooperativa pone sus credenciales y **prueba la conexión** (`/admin/conectores`).
3. Se liga el conector a cada servicio.

`Conectores:PermitirRedesPrivadas` existe sólo para las pruebas: **el arranque falla si vale `true` en
Production**.

## 7. Riesgo del llavero

Las credenciales de los conectores se cifran con DataProtection, cuyo llavero vive en
`ADM_DataProtectionKeys` (ver [llavero-dataprotection.md](llavero-dataprotection.md)). Si el llavero se
pierde, las credenciales no se descifran: los envíos quedan en espera hasta que la cooperativa las vuelva a
poner. No se pierde ningún envío.

## 8. Verificación

- `/saas/modulos` muestra Transporte encendido; el menú aparece en la cooperativa.
- Una factura individual de prueba en un período sin uso; un pago de prueba que la salde.
- El envío aparece en Transporte › Envíos y sale contra el sistema externo (o queda en espera si faltan
  credenciales).
