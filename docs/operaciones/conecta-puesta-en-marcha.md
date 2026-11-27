# Facturación electrónica por IngenIA Conecta: puesta en marcha

Feature 028 (`specs/028-canal-conecta/`). Receta para emitir las facturas y notas de una cooperativa por IngenIA
Conecta: primero en **pruebas** (`CONECTA-PRUEBAS`), después en **producción** (`CONECTA`). El modo `SIMULADO` sigue
igual.

> **Bloqueo vigente (D1).** Hoy Conecta no acepta el número que asigna el ERP (exige `number = null`). Mientras no
> despliegue la «numeración del cliente» (`specs/028-canal-conecta/pedido-a-conecta.md`), la opción
> `ElectronicInvoicing:Channels:{canal}:ConectaAceptaNumeroDelErp` queda en `false`. Con ella apagada, «Verificar
> credencial» falla con `CONECTA-NUMERACION-DEL-ERP` y la guardia no deja emitir. No la encienda antes: Conecta
> numeraría distinto al ERP.

## 0. Qué es cada modo

| Modo (canal) | Ambiente del ERP | Tenant en Conecta | Extremos admitidos | Dónde existe |
|---|---|---|---|---|
| `SIMULADO` | Habilitación (pruebas) | — | — | Siempre |
| `CONECTA-PRUEBAS` | Habilitación (pruebas) | en `Habilitation` | `QA` (`conecta-qa`) y `PDN` (Conecta producción con el tenant en habilitación) | DEV, QA y PDN |
| `CONECTA` | Producción | en `Production` | `PDN` | **Sólo PDN** (la API no lo registra en otro proceso) |

Las URL las fija GitOps (`ElectronicInvoicing__Channels__{canal con _}__Endpoints__{clave}`, p. ej. `CONECTA_PRUEBAS`); la pantalla sólo elige la
clave. El client id y el secreto viven en el Secret `erp-fe-credenciales`, nunca en la base, en `appsettings` ni en el
repositorio.

## 1. En Conecta (lo hace el administrador de la cooperativa en Conecta)

1. Confirmar que la cooperativa existe en Conecta y anotar su **identificador** (tenant id). COOFLOPAL en Conecta PDN:
   `266f0ee4-20b4-4676-8d76-946ade8e62b4`.
2. Revisar el canal de emisión: `GET /v1/tenants/{id}/emission-channel` debe responder `missing` vacío, en el
   ambiente correcto (`Habilitation` para pruebas, `Production` para real).
3. Revisar que cada prefijo que el ERP va a usar esté registrado y, con proveedor, enlazado (`provider-numberings`).
4. Crear un **cliente API** (`POST /v1/api-clients`, rol `Operator`, scopes `documents:read` y `documents:write`,
   lista blanca de IP si se quiere). Conecta muestra el secreto **una sola vez**: se carga directo en el paso 2.
   Nunca se usa un cliente con `tenants:admin`.

## 2. El Secret del ERP

```powershell
.\tools\scripts\crear-secreto-facturacion-electronica.ps1 -Ambiente pdn -Tenant <publicId de la cooperativa en el ERP> -Canal CONECTA-PRUEBAS -Campos clientId,clientSecret
```

El `-Tenant` es el **PublicId de la cooperativa en el ERP** (`ADM_Tenants`), no el tenant de Conecta. El canal
`CONECTA` sólo se acepta con `-Ambiente pdn`, y pide escribir `PRODUCCION`. El kubelet refresca el archivo y la API lo
relee en a lo sumo 5 minutos, sin reiniciar.

## 3. En el ERP (Administración › Facturación electrónica)

1. **Resoluciones** (Maestros › Resoluciones DIAN):
   - registrar la resolución con su ambiente. Pruebas: `Testing`, p. ej. `SETT` 1–5.000.000;
   - **asociar el prefijo al canal** `CONECTA-PRUEBAS` desde la fecha de arranque;
   - una resolución de pruebas nunca se asocia al canal `CONECTA`, ni al revés: la unicidad (ambiente, prefijo,
     consecutivo) y la guardia lo impiden.
2. **Nueva vigencia**:
   - canal «IngenIA Conecta – pruebas» (el ambiente se fija solo);
   - la cooperativa en Conecta (el identificador del paso 1.1);
   - el servicio (`QA` o `PDN`);
   - correo al comprador (por defecto lo envía el ERP; con «el canal» lo envía Conecta: nunca los dos);
   - el motivo.
3. **Verificar credencial**. El resultado debe decir algo como «Conecta (PDN) · cooperativa … · Habilitation ·
   proveedor factible · prefijos enlazados: SETT». Si falla, el mensaje dice qué: ambiente, NIT distinto, falta algo
   en Conecta o D1.
4. Revisar «Preparación DIAN» en la misma pantalla: debe quedar «Electrónica» para los tipos en uso.

## 4. Primera emisión de prueba

1. Confirmar una factura pequeña (POS u oficina) con la serie asociada.
2. En Ventas › Documentos electrónicos, comprobar:
   - estado «Validado»;
   - CUFE;
   - los tres archivos: XML firmado, respuesta de la DIAN y AttachedDocument;
   - el mismo número que muestra Conecta en su tablero.
3. Emitir una **nota crédito** total sobre esa factura y una **nota débito**, y comprobar lo mismo con CUDE.
4. Si algo queda «Enviado» más de unos minutos, «Consultar» en la bandeja. El procesador también consulta solo.
5. Anotar en la tabla del final: fecha, número, tiempos y observaciones.

## 5. Paso a real (`CONECTA`)

Lista de chequeo, **todas** antes de empezar:

- [ ] D1 desplegado en Conecta y `ConectaAceptaNumeroDelErp = true` en el overlay de PDN.
- [ ] Pruebas del paso 4 bien en `CONECTA-PRUEBAS` (factura, nota crédito y nota débito), con el contador.
- [ ] Puntos «por cotejar» revisados con el contador (`specs/028-canal-conecta/contracts/canal-conecta.md` §4):
  régimen 48/49, tarifa 0 = exento, escala del descuento, referencia de la nota y correo del adquirente.
- [ ] Tenant de Conecta en **Production** con su canal y prefijos reales enlazados.
- [ ] Resoluciones **reales** registradas en el ERP con ambiente «Producción» y asociadas al canal `CONECTA` desde la
  fecha de paso.
- [ ] Secret `{tenant}.CONECTA.json` cargado (otro cliente API, el de producción).
- [ ] Quien activa tiene `ElectronicInvoicing.Settings.ActivateProduction`.

Luego:

1. Nueva vigencia con canal «IngenIA Conecta – producción», servicio `PDN`, escribiendo `PRODUCCION`.
2. Verificar credencial (debe decir `Production`).
3. Primera factura real, en presencia del contador.

Lo numerado antes en pruebas **no pasa** a producción: sigue por su canal y su ambiente (§10.5 de `dian.md`).

## 6. Volver atrás

- De `CONECTA-PRUEBAS` a `SIMULADO`: nueva vigencia con el simulado. Lo pendiente de Conecta sigue consultándose por
  Conecta.
- De `CONECTA` a otro canal: nueva vigencia. Lo ya numerado sigue por `CONECTA`. Si hay que retirarlo, «Transmitir por
  el canal vigente» sólo aplica a resoluciones asociadas al canal nuevo.
- Contingencia del facturador (tipo 03): `CONECTA*` no la transmite todavía. Si Conecta no responde, las ventas siguen
  numerándose normal y quedan en cola, y la alerta «Canal sin respuesta» avisa. La 03 manual responde
  `ElectronicInvoicing.Contingency.NotSupportedByChannel`.

## 7. Configuración técnica (GitOps, `ingenia365-gitops/workloads/erp`)

- Volumen del Secret `erp-fe-credenciales` (`optional: true`, sin `subPath`) en `/secrets/facturacion-electronica/`,
  componente `facturacion-electronica-credenciales`.
- Variables por overlay:

  ```
  # DEV y QA
  ElectronicInvoicing__Channels__CONECTA_PRUEBAS__Endpoints__QA=https://conecta-qa.ingenia365.com
  # PDN
  ElectronicInvoicing__Channels__CONECTA_PRUEBAS__Endpoints__PDN=https://conecta.ingenia365.com
  ElectronicInvoicing__Channels__CONECTA__Endpoints__PDN=https://conecta.ingenia365.com
  # cuando Conecta despliegue D1 (por canal)
  ElectronicInvoicing__Channels__CONECTA_PRUEBAS__ConectaAceptaNumeroDelErp=true
  ```

  `SyncWaitSeconds` (8) y `PollIntervalMs` (500) son la espera tras el 202. No deben pasar de
  `Dian.EsperaMaximaPosSegundos`.
- La salida del pod de la API hacia `conecta*.ingenia365.com:443` debe estar abierta.

## 8. Diagnóstico

| Mensaje (regla) | Qué significa | Qué hacer |
|---|---|---|
| `CONECTA-EXTREMO` | El servicio elegido no está en la lista de esta instalación | Revisar las variables `Endpoints` del overlay |
| `CONECTA-CREDENCIAL` / `CONECTA-TOKEN` | Falta el archivo del Secret o Conecta no acepta el client id o el secreto | Recargar el Secret (paso 2) |
| `CONECTA-PERMISO` | El cliente no puede emitir para ese tenant (tenant, rol o IP) | Revisar el cliente API en Conecta |
| `CONECTA-NUMERACION` / `CONECTA-CANAL` | Numeración agotada o no enlazada, o canal de Conecta no listo | Revisar en Conecta |
| `CONECTA-NUMERACION-DEL-ERP` | D1 pendiente | Esperar el despliegue de Conecta |
| `CONECTA-NUMERO` | Conecta numeró distinto al ERP | **Detener** y avisar a soporte: no corregir a mano |
| `CONECTA-AMBIENTE` | El tenant de Conecta está en otro ambiente que el modo | Revisar el tenant o la vigencia |
| `CONECTA-TOTALES` (aviso) | Conecta calculó otro total | Revisar el documento; si hace falta, caso b |
| `CONECTA-TRIBUTO-POR-UNIDAD` | Un impuesto por unidad que Conecta no recibe | Mientras tanto, esos productos no se facturan por Conecta |

Los registros del ERP llevan `[FE.Conecta]` con canal, operación, número, HTTP y duración. Nunca llevan el token ni el
secreto. El `traceId` de Conecta queda en el mensaje de la transmisión, para cruzar con su línea de tiempo.

## Resultados de las pruebas manuales

| Fecha | Modo / servicio | Documento | Resultado | Tiempo hasta validar | Observaciones |
|---|---|---|---|---|---|
| | | | | | |
