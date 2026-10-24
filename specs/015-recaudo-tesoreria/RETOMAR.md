# Retomar la 015 — estado al pausar (2026-10-24)

Rama `015-recaudo-tesoreria`, sólo commits locales (sin push). Último commit de código: `0565cc1b` (fase 37).
El árbol quedó limpio, sin fases a medias.

## Hecho
| Entrega | Fases | Estado |
|---|---|---|
| Diseño | spec, plan, research, data-model, DT, contracts, tasks | 26 historias, FR-001–228, 497 tareas; constitución 2.0.1 (`RCD_`, `TES_`) |
| R0/T0 | 1–21 | construida; falta la fase 22 (verificación, congelamiento, despliegues) |
| R1 | 23–31 | construida |
| T1 | 32–35 | construida |
| R2 | 36–37 | construidas; **38 y 39 sin empezar** (ver la nota «PAUSA 2026-10-24» en tasks.md, entre las fases 38 y 39) |
| T2 | 40–45 | sin empezar |
| Retiro de la Tesorería heredada | 46 | sin empezar (destructiva con guarda, commit propio, el dueño es segundo revisor) |
| Polish | 47 | sin empezar |

## Regla vigente: pruebas al final
Por decisión del dueño (2026-10-23) **no se corre ninguna prueba hasta terminar todo el plan**: se escriben y se compila
por proyecto (`dotnet build -m:1`; `dotnet build-server shutdown` al cerrar cada fase). Todas las pruebas llevan la nota
«prueba escrita, sin correr».

## Orden para retomar
1. Fase 38 (convenios y canales por archivo, C2 = A) y 39 (informes de canales), con lo que el dueño pidió para R2:
   `RCD_PaymentReferences` con dígito de verificación (también en las propuestas de identificación) y tipos de adjunto
   propios de Recaudo (firma del egreso, soporte de la consignación reportada, comprobante de cargue UIAF).
2. T2 (40–45), fase 46 (sólo el código y la migración con guarda; ejecutarla en un ambiente es aparte) y Polish (47).
3. Corrida completa: Domain, Application, Architecture y Shared, uno a la vez
   (`dotnet test <proyecto> -m:1 --no-restore -- xUnit.MaxParallelThreads=4`); luego e2e en PostgreSQL y SQL Server.
4. Puerta T0301 (e2e migradas de Cartera y Captaciones en verde) → T0296, T0297, T0306 (`RetiroDeLosDoblesDePrueba`),
   T0307, T0308, T0310.
5. Fase 22 y los congelamientos de cada entrega (huellas en `LasMigracionesCongeladasNoSeRegeneran`), y las tareas de
   ambiente, con autorización del dueño.

## Migraciones
Pares en orden: `PlataformaParaRecaudoYTesoreria`, `RecaudoNucleo`, `TesoreriaNucleo`,
`IntegracionContableDeRecaudoYTesoreria`, `TesoreriaConciliacionMinima`, `RecaudoYTesoreriaPuestaEnMarcha`,
`RecaudoCompleto`, `CarteraYCaptacionesConPartidaCitada`, `TesoreriaCompleta`, `RecaudoCanales` (el último; para
regenerarlo: borrar sus archivos, restaurar el snapshot de `524a19fe` y volver a generar, porque `ef migrations remove`
intenta conectarse). R0/T0, R1 y T1 ya no se regeneran. Paridad OK (Application 72, Admin 15) y `has-pending-model-changes`
«No changes» en los dos motores con cadenas ficticias `Database__ConnectionStrings__*`.

## Decisiones pendientes del dueño (rige un defecto mientras tanto)
- Umbrales: 3 titulares (pagador tercero frecuente), 2 consignaciones (consignante repetido), 30 días (partida antigua).
- Conciliación: el abono llevado a Recaudo no pesa en la diferencia; alcance de la segregación del conciliador.
- Puesta en marcha: cheques girados en tránsito como línea informativa; saldo al corte = saldo en libros; cajero sin 2FA sólo avisa.
- Cierre de mes con cuadre provisional: negado sólo en el mes de la activación.
- «Gira cheques» deducido de una chequera activa; parámetros de RCD/TES sólo por plantilla (sin pantalla).
- R1: referencia exacta = número completo (dígito en R2); defectos nuevos (egresos varios y aplicar al cargar apagados;
  consignación reportada: verificar primero, 3 y 5 días; GMF RCD 0,004 con el tributarista); anular la devolución de un
  saldo a favor de Cartera se confirma en la corrida; cotejar el layout UIAF antes de enero de 2027.
- T1/R2 (recomendaciones del agente): hecho nuevo de Comercial `RemesaDelPuntoDeVentaDespachada` (enmienda de la 012);
  cheque pasado a cuenta por pagar que se cobra después (pedir anulación en el banco o hecho de cobro tardío en T2);
  hecho nuevo `ChequeRecibidoPagado` (`BancoRecaudo` contra `ChequesEnCanje`) al cruzar el canje; definición de
  «firmante retirado» (usuario inactivo o todas sus fichas retiradas); validar la combinación de firmas en los lotes en T2.
- Datos P1–P19 (tabla de research.md): personal y niveles, SARLAFT, matrices con la contadora, GMF, escenarios de no-go,
  fondo de liquidez, quién aprueba el cuadre, conservación.
