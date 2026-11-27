# Especificación — 028 Canal de emisión IngenIA Conecta

**Feature**: 028 | **Fecha**: 2026-11-27 | **Estado**: BORRADOR, espera aprobación del dueño |
**Base**: `specs/012-inventario-comercial/contracts/dian.md` (puerto del canal, §3; credenciales, §11; servicio
central, §15; selección del proveedor, §16) | **Hermanos**: `plan.md`, `contracts/canal-conecta.md`,
`tasks.md` | **Repositorio hermano (sólo lectura)**: `Conecta365` (`docs/api/README.md`,
`docs/plan-canales-emision.md`, ADR-017)

## 1. Contexto

Hoy el ERP sólo tiene el canal `SIMULADO` (`CanalSimulado`): sirve para CI, e2e y el ensayo, y la guardia le quita
todo efecto fiscal en producción. La carpeta `Channels/ServicioCentral` está vacía: el «servicio central sin estado» de
la 010 (§15 de `dian.md`) no existe.

IngenIA Conecta es la plataforma de facturación electrónica de IngenIA365 SAS. Corre en los mismos clústeres (QA
`https://conecta-qa.ingenia365.com`, PDN `https://conecta.ingenia365.com`, v0.1.0). Recibe un documento de negocio en
JSON (`POST /v1/invoices`, `/credit-notes`, `/debit-notes`). Ella misma traduce, firma o delega en un proveedor
tecnológico (hoy Factible), transmite, custodia y devuelve estado, CUFE/CUDE, XML firmado, `ApplicationResponse`,
`AttachedDocument` y PDF.

COOFLOPAL (NIT 800121426-5) ya tiene tenant en Conecta PDN (`266f0ee4-…`) con canal Factible en `Habilitation`,
credenciales de calidad en Vault y numeración `SETT` enlazada.

## 2. Objetivo

Sumar al ERP **un adaptador del puerto `ICanalDeEmisionElectronica` que emite por Conecta**. El usuario elige por
cooperativa uno de tres modos:

| Modo | Para qué | Efecto fiscal |
|---|---|---|
| **Simulado** | CI, e2e, ensayo sin red | Ninguno (se conserva tal cual) |
| **Conecta – pruebas** | Habilitación: tenant de Conecta en `Habilitation` (calidad del proveedor o set de pruebas DIAN), en Conecta QA o en Conecta PDN | Ninguno: «SIN VALIDEZ FISCAL» |
| **Conecta – real** | Producción: tenant de Conecta en `Production` | Factura con validez legal |

## 3. Historias de usuario

**US1 — Emitir por Conecta en pruebas (P1).** Como administrador de facturación de una cooperativa, quiero configurar
«Conecta – pruebas» con el tenant de Conecta y verificar la credencial. Las facturas y notas del ERP salen entonces
por Conecta en habilitación, y el ERP muestra el estado, el CUFE/CUDE y los artefactos.
- *Escenario 1*: con la credencial cargada en el Secret, «Verificar credencial» obtiene el token y lee el canal del
  tenant en Conecta. Confirma que el ambiente es `Habilitation` y deja la configuración verificada.
- *Escenario 2*: una factura del POS confirmada se envía a Conecta con su número del ERP. Si Conecta decide dentro de
  la espera del POS, se imprime validada; si no, queda en «pendientes de entrega» y el procesador la consulta.
- *Escenario 3*: la DIAN (o el proveedor) rechaza. El documento queda `Rejected` con las reglas y los mensajes, y se
  corrige por el caso a, b o c con el mismo número.
- *Escenario 4*: una nota crédito total que anula una factura validada sale por Conecta, referida al CUFE de esa
  factura.

**US2 — Pasar a Conecta real con salvaguardas (P1).** Como administrador, quiero activar «Conecta – real» sólo con
confirmación explícita y sólo si todo cuadra, para no emitir con validez legal por error.
- *Escenario 1*: el ERP de DEV o QA nunca ofrece ni acepta «Conecta – real».
- *Escenario 2*: en PDN, activar «real» exige el permiso nuevo, escribir `PRODUCCION` y que la verificación confirme
  que el tenant de Conecta está en `Production`. Hasta entonces la guardia responde `Blocked`.
- *Escenario 3*: un documento de pruebas nunca toma un número de una resolución de producción, ni al revés. Tampoco
  se envía a un tenant de Conecta de otro ambiente.

**US3 — Cambiar de modo sin perder lo pendiente (P2).** Como administrador, quiero cambiar de Simulado a Conecta
pruebas, y de ahí a real, con vigencias. Lo ya numerado sigue por su canal sellado (§10.5 de `dian.md`).

**US4 — Aviso inmediato por webhook (P3, opcional).** Como operador, quiero que la validación llegue apenas Conecta
la conozca, sin esperar la siguiente consulta. El webhook firmado sólo adelanta la consulta; nunca es la fuente de
verdad.

## 4. Requisitos funcionales

- **FR-001** El adaptador cumple las ocho reglas de §3.3 de `dian.md` y pasa la batería `ConformidadDelCanal`.
- **FR-002** Traduce el canónico del ERP al esquema canónico de Conecta para factura (01), nota crédito (91) y nota
  débito (92), según `contracts/canal-conecta.md` §4. No inventa códigos: todos salen del canónico, que ya los resolvió
  con `CatalogoDian`.
- **FR-003** Envía **el número que asignó el ERP**. Un número distinto del enviado, en la respuesta de Conecta, nunca
  se acepta (D1).
- **FR-004** Toda emisión lleva `Idempotency-Key` derivada de la clave del ERP (§6.2 de `dian.md`). Un reintento de
  la misma versión nunca crea un segundo documento en Conecta.
- **FR-005** Consulta el estado por el id de Conecta o, sin él, por prefijo y número. Trae CUFE/CUDE, estado y código
  DIAN, el XML firmado, el `ApplicationResponse` y el `AttachedDocument`. No guarda el PDF de Conecta: la
  representación es la del ERP (§13 de `dian.md`).
- **FR-006** Traduce los errores RFC 9457 de Conecta y sus estados a `ChannelOutcome` (contrato §6 y §7), conservando
  el crudo en `ProviderCode`/`DianStatusCode`.
- **FR-007** Hay dos modos Conecta, cada uno con su código de canal: `CONECTA-PRUEBAS` sólo con `Environment = Testing`
  y `CONECTA` sólo con `Environment = Production` (D4).
- **FR-008** Lo no secreto se guarda en la vigencia de la configuración: tenant de Conecta y extremo elegido de la
  lista blanca. El client id y el client secret viven **sólo** en el Secret `erp-fe-credenciales`, clave
  `{tenantPublicId}.{channelCode}.json`. La URL a la que viaja el secreto nunca sale de la base (contrato §3).
- **FR-009** Autentica por OAuth2 client credentials contra el Keycloak del extremo, con los scopes
  `documents:read documents:write`. El token va en caché por extremo y cliente, y se renueva antes de vencer. Toda
  petición lleva `X-Tenant-Id`.
- **FR-010** «Verificar credencial» obtiene el token y lee `GET /v1/tenants/{id}/emission-channel`. Comprueba el
  tenant, que el ambiente coincida con el modo y lo que falta (`missing`), y muestra el resultado en la pantalla.
- **FR-011** La guardia fiscal bloquea con motivo:
  - `CONECTA` fuera de producción del ERP;
  - `CONECTA` con `Testing`, o `CONECTA-PRUEBAS` con `Production`;
  - credencial no verificada;
  - un tipo que el canal no emite;
  - la contingencia 03 abierta con un canal que no la transmite (D8).
- **FR-012** Activar `CONECTA` exige el permiso `ElectronicInvoicing.Settings.ActivateProduction` y la confirmación
  escrita `PRODUCCION`. Queda auditado.
- **FR-013** El POS espera la decisión de Conecta hasta `Dian.EsperaMaximaPosSegundos`, consultando dentro de la misma
  llamada, sin reenviar (D5).
- **FR-014** (P3) `POST /api/electronic-invoicing/webhooks/conecta/{tenantPublicId}` es anónimo, verifica HMAC-SHA256
  con ventana de 5 minutos y sólo adelanta `NextAttemptAt` del documento correspondiente.

## 5. Criterios de éxito

- **SC-001** Una factura de COOFLOPAL en `CONECTA-PRUEBAS` queda `Validated` en el ERP con CUFE y los tres
  artefactos, con el mismo número en el ERP, en Conecta y en el proveedor.
- **SC-002** Ningún escenario de las pruebas (tiempo agotado, 5xx, corte después de enviar, reintento, caso a, dos
  emisiones concurrentes) produce dos documentos en Conecta para la misma versión.
- **SC-003** Con el ERP en `Development` o `Staging`, no hay forma (API ni pantalla) de configurar o emitir por
  `CONECTA`.
- **SC-004** El p95 entre la confirmación del POS y la impresión validada no empeora frente a la espera configurada:
  lo que no decide en esa espera pasa a pendientes, nunca bloquea la caja.
- **SC-005** Cero secretos en la base, en `appsettings*`, en el repositorio y en los registros. Lo comprueban las
  pruebas de arquitectura y una búsqueda en los registros de la e2e.

## 6. Casos límite

- Conecta responde 202 y luego no se sabe nada: se consulta por id; sin id, por número. Si no aparece (`NotFound`), se
  reenvía la misma versión con la misma clave.
- Conecta acepta, pero los artefactos todavía no están (proveedor Factible los descarga después): el resultado es
  `InProcess` y se vuelve a consultar (regla 5).
- El token vence a mitad de una llamada (401): se renueva una vez y se repite. Esto no es un reintento de la emisión:
  la petición no fue aceptada.
- Conecta en `Contingency` sin CUFE ni XML (el proveedor no responde): no es la contingencia 04 de la DIAN. El
  resultado es `InProcess`, no `DianUnavailable` (contrato §7).
- Totales distintos entre el ERP y Conecta (redondeo): ver D7.
- El tenant de Conecta cambia de canal (Factible ↔ motor propio): para el ERP no cambia nada. Conecta lo resuelve
  detrás del mismo canal `CONECTA`.
- El Secret se rota: `CredencialesEnArchivo` lo ve por la huella del archivo. La caché del token se invalida al
  cambiar la huella del client secret.

## 7. Fuera de alcance (trazado, no se construye aquí)

- Documento soporte (05/95) y documento equivalente POS (20/94): Conecta los tiene (`/v1/support-documents`,
  `/v1/equivalent-documents`), pero sólo con su motor propio, no por proveedor. Quedan para una entrega 028-b cuando el
  tenant emita por motor propio. El adaptador no los declara en `Capacidades`.
- Eventos RADIAN 030/032: Conecta los emite sobre facturas recibidas que primero hay que cargar
  (`POST /v1/received-invoices` con el XML). El ERP no guarda ese XML (E10). Quedan trazados.
- Contingencia 03 transmitida por Conecta (`/v1/invoice-transcriptions`): responde 409 si el tenant emite por
  proveedor. Queda trazada (D8).
- Consulta de rangos y de adquirente: exigen `tenants:admin` o no existen. `ConsultaRangos` y `ConsultaAdquirente`
  quedan en `false`.
- Nómina electrónica (N3 de la 010): Conecta tiene `/v1/payrolls`. La decisión de usarlo es de otra feature.
- **Nada se cambia en el repositorio Conecta365.** Lo que el ERP necesita de Conecta se lista como pedido en
  `plan.md` §3 (D1, D2 y D7) para la sesión o el equipo de Conecta.

## 8. Decisiones del dueño

Ver `plan.md` §3. Las que bloquean son **D1** (numeración) y **D4** (modos como códigos de canal).
