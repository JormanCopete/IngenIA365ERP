# Quickstart — 015 Recaudo y Tesorería (R0 y T0)

**Feature**: 015 | **Fecha**: 2026-10-22

Cómo verificar R0 y T0 de punta a punta: las pruebas automáticas, la preparación de una cooperativa de ensayo y los
recorridos que el ensayo general del 23/11 al 25/11 exige (SC-002). Lo de R1, T1, R2 y T2 se agrega con su entrega.

## 1. Pruebas automáticas

```powershell
# sin contenedores
dotnet test tests/IngenIA365ERP.Domain.Tests --filter "FullyQualifiedName~Receipts|FullyQualifiedName~Treasury"
dotnet test tests/IngenIA365ERP.Application.Tests --filter "FullyQualifiedName~Receipts|FullyQualifiedName~Treasury"
dotnet test tests/IngenIA365ERP.Architecture.Tests

# e2e (Docker Desktop encendido), en los dos motores; al cierre de R0/T0 (decisión del dueño: e2e al final).
# Dos excepciones declaradas: la e2e de humo de la caja al cierre del bloque 2 (T0183c, filtro
# "FullyQualifiedName~IntegrationTests.Receipts.CajaDeHumoTests") y la puerta de FR-224 (T0301, filtro
# "FullyQualifiedName~IntegrationTests.Lending").
$env:DB_PROVIDER="PostgreSql"; dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Receipts|FullyQualifiedName~IntegrationTests.Treasury|FullyQualifiedName~IntegrationTests.Lending"
$env:DB_PROVIDER="SqlServer";  dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "…igual…"

# rendimiento
$env:RUN_PERF_TESTS="1"; dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "Category=Perf&FullyQualifiedName~Receipts|FullyQualifiedName~Treasury"
```

| Grupo | Qué fija | Criterio |
|---|---|---|
| Casos dorados de imputación, arqueo, señales de efectivo, huella bancaria, cruce de la conciliación, transiciones del resultado, GMF | reglas puras | FR-188, FR-031, FR-095, FR-197, FR-216, FR-215, FR-205 |
| `ConfirmarReciboEsTodoONadaTests` (fallo inducido en la tercera aplicación y en el numerador) | todo o nada | SC-006 |
| `ReintentoConLaMismaClaveTests` (tres repeticiones por operación) | idempotencia | SC-007 |
| `DosCajasMismaPersonaTests` (concurrencia créditos + cuentas) | orden de cerrojos | SC-032 |
| `ProcesoNoInteractivoConLineasRechazadasTests` | unidad = línea | SC-030 |
| `ListaVinculanteBloqueaTests` | cero operaciones, cero órdenes en lote | SC-025 |
| `CuentaDestinoCambiadaYArchivoAlteradoTests` | marcas y cotejo | SC-026 |
| `FondoDeLiquidezRestringidoTests` | | SC-028 |
| `CuadreProvisionalYCierreDelMesTests` | | SC-029 |
| Arquitectura: `RecaudoYTesoreriaSeHablanPorSusPuertos`, `RecaudoNoConoceContabilidad`, `TesoreriaNoConoceContabilidad`, `LosHechosDeRecaudoYTesoreriaNoLlevanCuentas`, `RecaudoYTesoreriaNoTienenValoresLegalesFijos`, `NoHayDoblesDePrueba`, `UnSoloConsumidorPorDestino`, `LosEndpointsProtegidosExigenPermiso` | fronteras y valores legales | SC-015, SC-018, SC-020, SC-027 |
| e2e de Cartera y Captaciones migradas a los módulos reales | FR-224 | SC-001, SC-027 |
| Rendimiento: 20 cajeros concurrentes; respuesta de pagaduría de 5.000 líneas; lote de 1.000 órdenes; conciliación de 500 movimientos | | SC-023, SC-010, SC-011, SC-031 |

## 2. Preparar una cooperativa de ensayo (DEV o QA)

1. **Desplegar** la versión con R0 y T0. La migración `RetiroDeLosDoblesDePrueba` cierra (`ValidTo`) las filas `TST` y la
   `TES` del doble; el arranque no encuentra dos consumidores (US9-2). Comprobar en `/admin/participantes-de-integracion` que las
   filas de los dobles aparecen con vigencia cerrada y motivo.
2. **Contabilidad iniciada** con su plan (o sin iniciar: entonces todo hecho queda en «no pasa» y nada se bloquea).
3. **Plataforma**: bancos con código ACH; formatos genéricos sembrados (`EXTRACTO-GENERICO`, `PAGOS-GENERICO`,
   `RESPUESTA-PAGOS-GENERICO`, `RESPUESTA-PAGADURIA-GENERICA`); medios del catálogo común (efectivo, transferencia);
   denominaciones; listas restrictivas cargadas con una persona de prueba en lista vinculante.
4. **Roles** (plantillas sembradas): un cajero, un supervisor, un analista de recaudo, un tesorero, un aprobador, un
   conciliador y el oficial de cumplimiento, **todos con segundo factor inscrito**, con alcances.
5. **Recaudo** (`/recaudo/configuracion/*`): un punto «PRINCIPAL» con dos cajas (base 500.000, tope de aviso
   3.000.000), la bóveda de la oficina con doble custodia, medios efectivo y transferencia, tipos de documento y
   conceptos sembrados. O cargarlos con `recaudo.puntos-y-cajas` y `recaudo.cajeros-y-alcances`.
6. **Tesorería** (`/tesoreria/cuentas-bancarias`): una cuenta de recaudo y una de pagos, cada una vinculada a su cuenta
   contable bancaria; una chequera de la cuenta de pagos (rango 1001–1050); una cuenta marcada fondo de liquidez.
7. **Matrices** (`/contabilidad/recaudo/matriz`, `/contabilidad/tesoreria/matriz`): completar desde la plantilla hasta
   completitud 100 % (o dejar un hueco a propósito para el recorrido E).
8. **Activar** Recaudo y Tesorería (`/recaudo/activacion`, `/tesoreria/activacion`) con cuadre provisional (motivo y
   fecha límite) y comprobar que el registro tiene `REC` (recaudador y destino) y `TES` (destino) vigentes.
9. **Activar Cartera y Aportes**: la pantalla de activación de cada uno muestra en verde el recaudador y la tesorería
   (SC-001).
10. Crear con Cartera dos créditos desembolsados a un asociado A (uno con orden de giro por transferencia) y la cuota de
    aportes del mes.

## 3. Recorridos de R0 y T0 (los del ensayo general, SC-002)

Cada recorrido termina con: el dinero cuadrado (arqueo o conciliación), los módulos cuadrados (estado del crédito o la
cuenta) y la contabilidad cuadrada (comprobantes y conciliación de puentes en cero).

### A. Caja: abrir, recibo multiproducto, arqueo con faltante, cierre del día (US2, US3, US8; SC-003 a SC-005)

1. El cajero abre la sesión de la caja 1 con 500.000 contados por denominaciones.
2. Busca a A por documento (lector); ve créditos, cuotas de aportes y cuentas (`/recaudo/caja`).
3. Elige la cuota del ciclo del crédito 1, un abono extraordinario al crédito 2 —ve las dos alternativas y no puede
   confirmar sin la elección de A: «reducir plazo»— y la cuota de aportes.
4. Registra 300.000 en efectivo y 500.000 por transferencia (referencia); ve la imputación (efectivo primero en el orden
   de la pantalla).
5. **Confirma** (F9): un recibo con tres aplicaciones, cada una con el comprobante de su módulo, la elección del deudor
   impresa; tirilla con código de verificación.
6. Repite la confirmación con la misma clave (botón de reintento de soporte o `curl` con la misma `Idempotency-Key`):
   devuelve el mismo recibo (SC-007).
7. Recauda 1.200.000 en efectivo más en otros recibos; el supervisor retira 1.000.000 a bóveda con doble custodia.
8. Cierra contando 699.000 en efectivo (arqueo ciego: no ve lo esperado hasta registrar) y las referencias de la
   transferencia. Faltante de 1.000 (sobre la tolerancia cero): queda «cerrada con diferencia por aprobar»; el supervisor
   (otra persona) la aprueba con motivo → hecho de cuenta por cobrar al cajero y aviso al cajero.
9. El supervisor cierra el día del punto; informe por medio. Corre el lote de Recaudo y el cuadre diario: cero o
   explicado.
10. Contabilidad: un comprobante `RR` resumido por día, punto y medio; `DiferenciaDeArqueoAprobada` en línea. La
    conciliación de puentes de Cartera y Captaciones da cero.

### B. Rechazo y «la distribución cambió» (US3-2, US3-3; SC-006)

1. Con A en caja, simular la cuota del crédito 1; antes de confirmar, aplicar desde Nómina (o un segundo cajero) un pago
   al mismo crédito.
2. Confirmar: Cartera responde que la distribución cambió; la pantalla muestra la nueva y pide confirmar otra vez con la
   misma clave; nada quedó registrado entretanto.
3. Un recibo de tres aplicaciones donde la tercera excede el límite de aportes: no queda nada, ni número consumido; el
   cajero ve el motivo por ítem.

### C. Anulación y reverso desde Cartera (US5)

1. Anular en la sesión abierta un recibo de dos aplicaciones: lo pide el cajero, lo aprueba el supervisor presente con
   segundo factor; dos reversos (Cartera y Captaciones), documento de anulación numerado, esperado de la sesión rebajado,
   hecho espejo.
2. Reversar desde la pantalla de Cartera un pago de Recaudo (error de digitación): llega `PagoReversadoPorCartera`;
   Recaudo registra la aplicación reversada; una persona confirma la causa «dinero recibido» → saldo a favor del pagador
   y alerta al supervisor.
3. Devolver ese saldo a favor por orden de pago a Tesorería (R0) y seguirla en el recorrido G.

### D. Transferencias recibidas: extracto, identificación y partida (US6; SC-009)

1. El analista carga el extracto del día de la cuenta de recaudo con `EXTRACTO-GENERICO`: cinco abonos.
2. Vuelve a cargarlo: «5 omitidos por huella».
3. Identifica cuatro a mano (uno dividido entre dos créditos con suma exacta): recibos de canal banco con la fecha valor
   del movimiento, en forma no interactiva con la modalidad del canal.
4. Deja el quinto como partida por identificar (hecho de reconocimiento); a los diez días lo identifica: en R0 se aplica
   con la fecha más antigua que admite el contrato, el recibo lo dice y se alerta al módulo (FR-071).
5. Un abono que es reintegro de un proveedor se clasifica al concepto con aprobación.

### E. Validación previa contable (US8-1, US8-5)

1. Quitar la regla del medio transferencia en la matriz de Recaudo con la política «bloquear».
2. En caja, elegir transferencia: la pantalla no confirma y dice hecho, rol, combinación y quién debe completarla.
3. Cambiar la política a «advertir» con permiso y motivo: queda auditado y alerta cada día hasta volver a «bloquear».

### F. Pagaduría (US7; SC-010)

1. Cartera genera el archivo de descuentos de una pagaduría (50 líneas, créditos y aportes, v2); llega a
   `/recaudo/pagadurias`.
2. El analista lo descarga y confirma el envío (portal, fecha, radicado) → `EnvioDeDescuentosConfirmado`; confirmar otra
   vez no cambia nada.
3. Carga la respuesta (formato del convenio o `RESPUESTA-PAGADURIA-GENERICA`): 45 descontadas y 5 no; una fila con un
   empleado sin equivalencia queda en «no aplicados». Revisa la vista previa y devuelve el resultado
   (`ResultadoDeDescuentos` v2).
4. Registra el giro (identificado en el extracto, menor que lo descontado) y lo aplica: cada línea es su unidad; las que
   el giro no cubre siguen pendientes; una cuenta bloqueada entretanto queda rechazada como saldo de la pagaduría.
5. Las 5 no descontadas siguen exigibles en Cartera con su motivo.

### G. Giros: lote, aprobación, archivo, resultado y regiro (US10, US11, US26; SC-011 a SC-013)

1. Tesorería recibe tres órdenes: un desembolso (v1), la devolución del retiro de un asociado (v2) y la devolución de
   Recaudo del recorrido C.
2. Cambiar la cuenta destino de un beneficiario: queda pendiente hasta que otra persona la aprueba con la certificación;
   la orden queda marcada «cuenta cambiada».
3. El tesorero arma el lote en la cuenta de pagos (forma archivo, `PAGOS-GENERICO`): vista previa con total, cantidad y
   GMF estimado; la orden marcada exige revisión expresa.
4. El tesorero intenta aprobarlo: se rechaza. El aprobador (atribución suficiente, o dos niveles) lo aprueba.
5. Se genera el archivo (huella); lo descarga otra persona; se marca enviado con quién lo cargó y quién lo autorizó en el
   banco. Las órdenes quedan «enviadas» sin informar nada.
6. Carga la respuesta: dos pagadas y una rechazada por cuenta inexistente; una línea con la cuenta alterada se alerta y no
   se informa hasta revisarla.
7. Resultados: «girada» en dos (hecho `PagoDeOrdenGirado` contra el puente de cada origen) y «rechazada» en la tercera con
   su causal. Cartera reversa el desembolso y emite una orden de regiro enlazada; aparece en la cadena y se paga en un
   archivo nuevo.
8. Un beneficiario en lista vinculante: su orden queda bloqueada fuera del lote y el oficial recibe la alerta (SC-025).

### H. Cheques: devolución del recibido y cheque girado (US15, US19)

1. Con la clase cheque encendida (`Caja.AdmiteCheque`), pagar con un cheque una cuota y un aporte junto con efectivo;
   devolverlo por fondos insuficientes: se reversan sólo las porciones del cheque; el efectivo sigue aplicado; aviso al
   pagador y alerta; informe de cheques devueltos.
2. Pagar una orden de forma cheque: sale numerado de la chequera (hecho `ChequeGiradoEmitido`); anular el siguiente por
   dañado; entregar el primero con constancia → «girada»; comprobar numeración sin huecos sin explicar.

### I. Remesa y conciliación mínima (US25; SC-031)

1. Remesar desde la bóveda al banco con acta y sello de bolsa: hecho `RemesaDespachada`; queda en tránsito.
2. El conciliador (que no preparó ni aprobó pagos de esa cuenta) carga el extracto del mes; el cruce automático empareja
   recibos de banco, la remesa (pasa a confirmada, hecho `RemesaConfirmada`), los pagos y el cheque cobrado (pasa a
   «cobrado»); los pagos registrados por Contabilidad o por lo heredado aparecen como movimientos contables de la cuenta
   (FR-209).
3. Marca a mano lo que no cruza; un abono sin registro pasa a Recaudo como partida; un cargo no identificado alerta.
4. Cierra: el informe cuadra saldo del extracto, partidas y saldo en libros. Reabrir exige motivo y permiso.
5. En Contabilidad, reabrir el período de ese mes: la conciliación cerrada de Tesorería queda «desactualizada» (E-04, la
   única conciliación bancaria); nada se escribe en las tablas de conciliación de Contabilidad.

### J. Fondo de liquidez y SARLAFT (US14; SC-017, SC-028)

1. Intentar un lote desde la cuenta del fondo: rechazado sin uso aprobado.
2. Con el umbral de declaración en 5.000.000, recibir 6.000.000 en efectivo sin declaración: rechazo; con declaración y
   soporte: el recibo; quien efectúa la entrega queda registrado si no es el pagador.
3. Cuatro pagos de 8.000.000 en efectivo de la misma persona en una semana: alerta de fraccionamiento y de acumulado al
   oficial.
4. La persona en lista vinculante en la ventanilla: no se recibe su dinero; alerta inmediata; sólo el oficial la libera
   si es un falso positivo.

### K. Contingencia (US2-9)

1. Registrar un talonario; simular la caída: emitir a mano el recibo 0001 del talonario.
2. Al volver, digitarlo con su fecha (dentro de la ventana del contrato), marca «contingencia» e imagen; el informe de
   contingencia lo muestra digitado y lista los números sin usar.

### L. Puesta en marcha y cuadre (US13, US24; SC-021, SC-029)

1. Cargar `recaudo.bovedas-al-corte` con un error de fila: no guarda nada y dice fila y columna; corregir y cargar.
2. Cargar cuentas bancarias con saldo, cheques en tránsito y partidas al corte; ningún hecho contable.
3. El cuadre contra Contabilidad muestra una diferencia: activar con cuadre provisional (permiso, motivo, fecha límite);
   alerta diaria; intentar cerrar el mes de la activación en Contabilidad: se niega (`Accounting.Period.OpeningSquarePending`).
4. Registrar el cuadre definitivo: la alerta se cierra y el cierre del mes procede.

## 4. Lista de go/no-go (FR-227)

En `/admin/go-no-go`, la lista de la cooperativa: completitud de las dos matrices al 100 % de lo que no esté en «no pasa»;
un recibo de prueba por medio, punto y concepto; un giro por cada forma y cuenta; segundo factor inscrito de cada
aprobador y supervisor; roles y alcances; requisitos de activación de Cartera y Captaciones en verde; cronograma del
cambio y escenarios de no-go parcial (P15) con su plan de reversa. Una condición en rojo se ve; seguir exige la aprobación
registrada del dueño.

## 5. Paralelo en QA (FR-226)

Copia de la parametrización de COOFLOPAL en una cooperativa de QA; el equipo registra los recaudos del paralelo (11/11 a
27/11) con la versión disponible de R0/T0 y lo que haya de R1/T1; al terminar, el informe compara lo registrado con las cifras de referencia por día,
medio y producto y explica cada diferencia (SC-024). Producción no recibe datos del paralelo: el 28/11 su base está
limpia para la carga definitiva.

## 6. Qué valida cada quien

| Quién | Qué |
|---|---|
| Dueño | P1, P2, P3, P4, P6, P13, P14, P15, P16, P18; la lista de go/no-go |
| Oficial de cumplimiento | P7 (umbrales, suspensión, alcance, listas, estructura del reporte), P17 |
| Contadora | P8, P11, P12 (matrices y tipos de comprobante), P18 |
| Tributarista | P10 (GMF), P19 |
| Equipo de COOFLOPAL | recorridos A a L en el ensayo, manuales de R0 y T0 (FR-225) |
