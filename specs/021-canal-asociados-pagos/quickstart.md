# Quickstart — validar la feature 021

Cómo probar cada entrega de punta a punta. Nada de esto usa datos ni credenciales reales: las credenciales de
prueba de la pasarela se cargan por pantalla en DEV, nunca en archivos del repositorio.

## Entorno local

1. ERP como siempre (API + Web, PostgreSQL nativo en 5432), con una cooperativa de prueba que tenga Cartera,
   Captaciones y Recaudo activos y un convenio de recaudo de clase **pasarela** con su cuenta propia.
2. Canal: `canal/api` con su base `ingenia365_canal` (contenedor PostgreSQL de `canal/docker-compose.yml`), pasarela
   **simulada** por defecto (`CANAL_PASARELA=simulada`), llaves ES256 de desarrollo generadas por
   `canal/scripts/llaves-dev.sh` fuera del repositorio, y la llave pública del canal en la configuración local del
   ERP (`CanalServicio:LlavePublica`, por variable de entorno; **no** se crea `appsettings.Development.json`).
3. `canal/web`: `npm run dev` con proxy a `canal/api`.

## C1 · Identidad y consulta

1. ERP `/canal/configuracion`: encender el canal, marca, textos legales; `/canal/conceptos`: publicar una línea de
   crédito y aportes sin destino (sólo consulta).
2. `/canal/habilitaciones`: habilitar a una persona con correo; a otra sin correo ni celular.
3. Canal: activar la primera con su documento → llega el código al correo (bandeja de desarrollo) → crear clave →
   ver el crédito y los aportes con los mismos valores que la caja del ERP a la fecha (SC-005).
4. Activar la segunda → mensaje «acércate a tu entidad» con el contacto de soporte (FR-019).
5. Documento inexistente → misma respuesta y tiempo que uno habilitado (FR-018, SC-006).
6. Habilitar a la primera persona en una segunda cooperativa → el selector muestra las dos; inhabilitarla en una →
   desaparece en la siguiente petición (FR-026).
7. Dejar la sesión quieta 5 min → se cierra con aviso al minuto 4 (FR-027).

## C2 · Pago

1. ERP `/canal/destinos`: destino con el convenio de pasarela; credenciales de la pasarela simulada (o de pruebas de
   Avalpay en DEV) → estado «activa».
2. Publicar el crédito con ese destino y modalidades exigible, parcial (mínimo 20 000) y abono a capital.
3. Canal: pagar el exigible → resumen con total y logo → aceptar términos → pasarela simulada «Aprobada» → resultado
   aprobado con referencia y autorización; en el ERP, recibo de canal en línea, aplicación por prelación, asiento y
   `CDG_PaymentNotices` `Applied`; comprobante descargable (FR-044–FR-048).
4. Repetir el aviso a mano (mismo cuerpo) → misma respuesta, nada nuevo (SC-004).
5. Estado simulado «Pendiente» → aviso de pendiente con referencia; intentar pagar el mismo concepto → bloqueado
   (FR-041); la consulta periódica lo resuelve (FR-040).
6. Abono a capital → elegir reducir plazo o cuota (FR-032a); el plan nuevo en el ERP coincide con la simulación.
7. Pago parcial por debajo del mínimo → rechazado antes de la pasarela.
8. Doble clic en «Pagar» → un solo intento (FR-036).
9. Cancelar la obligación en caja y luego aprobar el pago en línea → saldo a favor del pagador, el asociado ve «en
   revisión» (FR-046).
10. Certificación con Avalpay: recorrer su lista de ítems en el ambiente de pruebas con cada estado de PSE y tarjeta
    (SC-008), con la consulta periódica a 15 min.

## C3 · Operación

1. Cargar en Recaudo el reporte de la pasarela del día (flujo de archivos de la 015) con una línea ya aplicada en
   línea y otra aprobada sin aviso → la primera `AlreadyApplied`, la segunda se aplica (R4); conciliar contra el
   extracto con comisión (FR-050, FR-051).
2. Reverso antes del corte → reversado en la pasarela y en el ERP con asiento espejo; después del corte →
   devolución por Tesorería (FR-052).
3. `/canal/atencion`: buscar por referencia y por documento; desbloquear una cuenta (FR-058); un funcionario de otra
   cooperativa no encuentra nada (FR-059).

## C4 · Documentos y avisos

Extracto de crédito y certificado de aportes desde el canal, iguales a los del ERP; aviso de cuota próxima según
la anticipación configurada; apagar el aviso y verificar que los de seguridad siguen llegando.

## C5 · App

Instalar la app de prueba en Android e iOS, entrar, activar biometría, pagar (la pasarela se abre en Custom Tabs /
`ASWebAuthenticationSession` y vuelve por App Link), recibir el push de pago aplicado.

## C6 · Crédito en línea

Simular con una línea publicada (misma cuota que el simulador del ERP), enviar la solicitud y verla en el ERP con
canal de origen «canal digital».

## Pruebas automáticas

- ERP: Domain, Application (comandos del canal, `RegisterChannelPaymentCommand` con repetición y excedente) y
  Architecture (`SoloElAvisoUsaElActorAsociado`, el esquema `CanalServicio` sólo en `/api/canal/v1`,
  `LasPantallasDicenQueEstanCargando` con el módulo Canal); integración con Testcontainers al final.
- Canal: `go test ./...` con testcontainers (PostgreSQL) y la pasarela simulada; pruebas de contrato contra un ERP
  de prueba; Vitest y Playwright en `canal/web`.
