# El paralelo de Recaudo y Tesorería en QA (COOFLOPAL, del 11/11 al 27/11)

> Feature 015, US24 (FR-226; SC-024). Runbook del **paralelo**: la ventanilla registra en QA lo mismo que registra en su sistema
> de siempre, y al final el informe `parallel-comparison` explica cada diferencia. **Producción no recibe ningún dato del paralelo.**

## 1. Preparar QA

1. Desplegar en QA la versión de R0/T0 disponible (runbook de despliegue) con `Lending:TestDoubles:Enabled = false`.
2. Crear la cooperativa del paralelo con **copia de la parametrización de COOFLOPAL**: oficinas, puntos y cajas, cajeros con su segundo
   factor, medios por punto, conceptos varios, cuentas bancarias propias, matrices contables, parámetros `RCD`/`TES`.
3. Cargar las plantillas de puesta en marcha (`/recaudo/puesta-en-marcha`, `/tesoreria/puesta-en-marcha`) con los saldos al corte del
   inicio del paralelo, registrar un **cuadre provisional** con motivo y fecha límite y activar Recaudo y Tesorería con la
   **activación mínima** (la del paralelo; la completa se ensaya en otra cooperativa de QA).
4. Activar Cartera y Aportes en la cooperativa del paralelo como lo diga su propio runbook.

## 2. Registrar durante el paralelo

Del 11/11 al 27/11, cada día hábil:
- los cajeros registran en `/recaudo/caja` los recaudos de cuotas y aportes que registraron en el sistema anterior;
- el analista carga los extractos del día e identifica los abonos (`/recaudo/extractos`);
- los descuentos de nómina y de pagadurías se registran por su camino (`/recaudo/pagadurias`);
- Tesorería gira las órdenes de prueba del día (desembolsos y devoluciones) y registra sus resultados.

## 3. Cargar las cifras de referencia

Con la plantilla `recaudo.cifras-de-referencia` (en `/recaudo/puesta-en-marcha`), una fila por día y por:
- **medio**: concepto `PARALELO.MEDIO`, dimensión el **código del medio de pago** (EF, TR…), valor recaudado ese día por ese medio;
- **producto**: concepto `PARALELO.PRODUCTO`, dimensión `CREDITOS`, `CUENTAS`, `CONCEPTOS`, `CXC` o `POR-APLICAR`.

La fecha de corte de cada fila es el día. Volver a cargar el mismo día, concepto y dimensión reemplaza la cifra anterior.

## 4. El informe del paralelo

En `/recaudo/informes?vista=parallel-comparison` (o `GET /api/reports/receipts/parallel-comparison?from=&to=&format=xlsx`), por día:
registrado, referencia, diferencia y **explicación**:
- «Sin cifra de referencia del día»: falta cargarla;
- «La referencia lo trae y el paralelo no lo registró»: falta digitar el recaudo;
- «Puede explicarla»: anulaciones, recibos con fecha valor distinta del día de registro o partidas por identificar abiertas ese día;
- «Por explicar»: hay que investigarla con el equipo de COOFLOPAL.

Cada diferencia queda explicada por escrito en el informe que se adjunta al ítem manual `Plataforma.PARALELO` de la lista de go/no-go
(T0327, del dueño).

## 5. Al terminar

Producción se mantiene **limpia**: ningún recibo, extracto ni cifra del paralelo viaja a producción (ítem `PRODUCCION-LIMPIA` del
go/no-go). La carga definitiva en producción se hace el 28/11 sobre la base limpia, con las plantillas de puesta en marcha.
