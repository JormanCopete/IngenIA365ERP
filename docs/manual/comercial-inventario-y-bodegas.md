# Comercial: inventario y bodegas

> Manual funcional del grupo **Inventario** del menú y del submenú **Contabilidad → Inventario** (feature 012, entregas
> I1, I2, I5 e I6). Para el jefe de inventario, el bodeguero, quien aprueba, el administrador y la contadora. El índice
> del módulo está en [comercial.md](comercial.md). Cómo funciona por dentro el documento de inventario (borrador,
> confirmación, número, anulación, kardex) está en [inventario-documentos-y-kardex.md](inventario-documentos-y-kardex.md)
> y no se repite aquí.

## Qué hace

- **Catálogo**: productos (inventariables, servicios, combos, kits y plantillas de variantes), con unidades y empaques,
  códigos de barras, impuestos, imágenes y grupo contable; control de lote, vencimiento y serie; unidades de medida,
  categorías, marcas, grupos contables y causas de ajuste.
- **Bodegas** por sucursal, con ubicaciones, mínimos y máximos, y una **bodega de tránsito** por sucursal para los
  traslados.
- **Existencias y kardex**: cuánto hay, dónde y cómo se movió. La existencia es la suma del kardex, nunca un saldo
  digitado.
- **Movimientos**: ajustes, consumos internos, bajas, ensamble de kits, traslados en dos pasos y conteos físicos.
- **Costeo**: promedio ponderado o PEPS, por cooperativa o por bodega, con ajustes de costo cuando entra un documento con
  fecha anterior.
- **Períodos**: cierre mensual con el valorizado fijado.
- **Puesta en marcha**: saldo inicial, cifras de referencia del sistema anterior y activación de cada bodega con cuadre
  contable.
- **Gobierno**: tipos de documento, parámetros con vigencia, aprobaciones por niveles y montos máximos, alcance por
  bodega, alertas e integridad del kardex.
- **Relación con Contabilidad**: Inventario no escribe asientos ni conoce cuentas; le manda a Contabilidad un mensaje por
  cada operación y la contadora decide las cuentas en la matriz.
- **Tablero e informes**.

## Quién lo hace

| Tarea | Permiso |
|---|---|
| Ver y administrar el catálogo | ver (`Inventory.Catalog.View`), administrar (`Inventory.Catalog.Manage`); cambiar el grupo contable de un producto (`Inventory.Catalog.ReclassifyAccountingGroup`) |
| Bodegas | ver (`Inventory.Warehouses.View`), administrar (`Inventory.Warehouses.Manage`), activar (`Inventory.Warehouses.Activate`), aceptar una diferencia al activar (`Inventory.Warehouses.AcceptActivationDifference`) |
| Tipos de documento | ver (`Inventory.DocumentTypes.View`), cambiar (`Inventory.DocumentTypes.Manage`) |
| Existencias | ver (`Inventory.Stock.View`); costos y valores (`Inventory.Costs.Read`) |
| Ajustes | ver (`Inventory.Adjustments.View`), crear (`Inventory.Adjustments.Create`), digitar el costo de un ajuste positivo (`Inventory.Adjustments.SetUnitCost`) |
| Traslados | ver (`Inventory.Transfers.View`), crear (`Inventory.Transfers.Create`) |
| Conteos | ver (`Inventory.Counts.View`), crear y abrir (`Inventory.Counts.Open`) |
| Períodos | ver (`Inventory.Periods.View`), cerrar (`Inventory.Periods.Close`), reabrir (`Inventory.Periods.Reopen`) |
| Saldo inicial y cifras de referencia | cargar el saldo (`Inventory.OpeningBalance.Load`), aprobarlo (`Inventory.OpeningBalance.Approve`), importar cifras (`Inventory.LegacyFigures.Import`) |
| Parámetros y costeo | ver (`Inventory.Parameters.View`), registrar (`Inventory.Parameters.Manage`), cambiar el método de costeo (`Inventory.Costing.Manage`) |
| Aprobaciones y políticas | bandeja (`Inventory.Approvals.View`) y el permiso de cada nivel; políticas (`Inventory.ApprovalPolicies.View` / `.Manage`) |
| Alcance por bodega | asignar (`Inventory.Scopes.Manage`); ver todas sin asignación (`Inventory.Scope.AllWarehouses`) |
| Alertas | ver (`Inventory.Alerts.View`), atender (`Inventory.Alerts.Attend`), tipos (`Inventory.Alerts.Manage`) |
| Integridad | verificar (`Inventory.Integrity.Verify`), reconstruir (`Inventory.Integrity.Rebuild`) |
| Mensajes a Contabilidad | ver (`Inventory.Messages.View`), reprocesar (`Inventory.Messages.Reprocess`), enviar lo que no aplicaba (`Inventory.Messages.SendNotApplicable`) |
| Conciliación | `Inventory.Reports.View` e `Inventory.Reconciliation.View` |
| Tablero e informes | tablero (`Inventory.Dashboard.View`), informes (`Inventory.Reports.View`), exportar (`Inventory.Reports.Export`) |
| Matriz contable, tipos de comprobante, completitud (contadora) | ver (`Accounting.InventoryRules.View`), cambiar (`Accounting.InventoryRules.Manage`) |
| Lotes de contabilización (contadora) | ver (`Accounting.InventoryBatches.View`), ordenar (`Accounting.InventoryBatches.Run`) |

Sin el permiso de costos, las pantallas muestran cantidades pero dejan vacíos el costo y el valor, y lo dicen.

## Paso a paso en pantalla

### Catálogo

**Catálogos simples** (Inventario → Unidades de medida, Categorías, Marcas, Grupos contables, Causas de ajuste; Ventas →
Canales de venta): «Nuevo», «Editar», inactivar o reactivar con motivo, y «Plantilla» para cargar por archivo.

- **Unidades**: decimales y el código que exige la factura electrónica. Los decimales sólo bajan si ninguna cantidad
  registrada usa más.
- **Categorías**: árbol de hasta cinco niveles; cambiar el padre mueve la rama entera.
- **Grupos contables**: es lo que Inventario le dice a Contabilidad en lugar de una cuenta. El código no cambia nunca.
- **Causas de ajuste**: las de los ajustes y bajas. Las que usa el sistema (diferencia de conteo, reclamación al
  transportador) no se inactivan.

**Productos** (Inventario → Productos):

1. Filtre o lea el código de barras para ir directo al detalle. «Nuevo producto»: código (hasta 20 caracteres, en
   mayúsculas, único), nombre, clase, unidad base, grupo contable, concepto de retención, IVA y, si aplica, «Controla
   lote», «Controla vencimiento» (exige lote) y «Controla serie».
2. En el detalle, pestañas **Datos** («Se compra» y «Se vende»), **Unidades** (el factor dice cuántas unidades base tiene
   cada empaque), **Códigos de barras** (el de un empaque elige esa unidad al leerlo), **Impuestos**, **Imágenes** y
   **Grupo contable** («Cambiar grupo» con fecha efectiva y motivo; lo ya confirmado conserva su grupo).
3. «Estado»: activo, inactivo (no se ofrece en documentos nuevos) o bloqueado (ningún movimiento, salvo el conteo y la
   recepción de lo que ya viajaba). «Borrar» sólo sirve para un producto que nunca estuvo en un documento.

**Variantes, combos y kits**:

- En una **plantilla de variantes**, pestaña Variantes, «Atributos…» (TALLA, COLOR con sus valores) y la generación de las
  combinaciones con su código y nombre propuestos. Cada variante hereda categoría, marca, unidades, grupo contable,
  impuestos y seguimiento. La plantilla no se vende: se vende una variante.
- En un **combo** o un **kit**, pestaña Componentes: productos y cantidad por unidad, «Guardar componentes». El combo se
  vende como una línea y descuenta sus componentes; el kit se arma con un ajuste de clase **ensamble** («Proponer líneas»):
  salen los componentes y entra el kit al costo de lo consumido.

**Lotes, vencimientos y series**: en una entrada la línea pide el lote y el vencimiento; en una salida se propone el lote
que **vence primero** y los vencidos se marcan (venderlos se bloquea o se avisa según el parámetro «Ventas.LoteVencido»).
Cada serie es una unidad: una que ya está en existencia no vuelve a entrar, ni sale una que no está en la bodega.

### Bodegas y ubicaciones

1. **Inventario → Bodegas**, «Nueva bodega»: código, nombre y sucursal. Toda bodega nace **sin activar** (sólo admite su
   saldo inicial) con su ubicación general. La primera bodega operativa de una sucursal trae su **bodega de tránsito**,
   que no se crea a mano.
2. En el detalle: **Datos**, **Ubicaciones** («Agregar ubicación», una «Por defecto»; ninguna con existencia se retira),
   **Mínimos y máximos** por producto (mínimo ≤ punto de reorden ≤ máximo; «Guardar mínimos y máximos») y **Saldo
   inicial y activación**.
3. «Plantilla» (la 7) carga bodegas y ubicaciones.

Una bodega nueva sólo la ve quien tiene alcance total, hasta que se la asignen a alguien.

### Alcance comercial por bodega

**Inventario → Alcance comercial** (también desde Seguridad → Usuarios): elija el usuario, «Editar alcance», marque sus
bodegas (operativas o de tránsito) y a lo sumo una por defecto, y «Guardar alcance». Fuera de su alcance, una bodega no
existe para ese usuario: no aparece en listas, ni en el kardex, ni en los documentos.

### Existencias y kardex

- **Inventario → Existencias**: lea o escriba el producto, o filtre por bodega, «Sólo con existencia» o «En o bajo el
  punto de reorden». Columnas: física, reservada (por pedidos), disponible y en tránsito hacia la bodega; mínimo, punto
  de reorden y posición si hay política. El código abre sus bodegas, ubicaciones y lotes.
- **Inventario → Kardex**: producto, bodega, lote si hace falta y rango (hasta cinco años), «Consultar». La primera fila
  es el saldo al día anterior; cada línea lleva su documento, entrada, salida y saldo. «Mostrar ajustes de costo» agrega
  esas líneas; en PEPS, «Capas consumidas» dice de qué entradas salió cada salida. «Excel» o «PDF».

### Ajustes, consumos y bajas

1. **Inventario → Ajustes**, «Nuevo ajuste»: tipo (positivo, negativo, consumo interno, baja, ensamble…), bodega, fecha,
   causa, centro de costo, motivo y líneas (el lector llena producto y empaque).
2. «Guardar»: el borrador se guarda con sus avisos. Ctrl+S guarda; Ctrl+Enter confirma.
3. Los soportes (actas de destrucción, denuncias) se suben mientras está en borrador o en aprobación.
4. «Confirmar»: si la política o el monto lo piden, queda en aprobación, congelado y sin número.
5. «Descartar» un borrador o «Anular» uno confirmado, con motivo. La anulación es un documento contrario.

### Traslados entre bodegas

1. **Inventario → Traslados**, «Nuevo traslado»: tipo, origen (de su alcance), destino (cualquier bodega operativa y
   activa), fecha, motivo y líneas. «Guardar».
2. «Despachar» (o «Pedir aprobación» si la política lo exige): la mercancía sale del origen y queda **en tránsito**.
3. En el destino, «Recibir»: la cantidad que llegó por línea, la ubicación de llegada y el sobrante declarado. Lo que no
   llega queda como faltante en tránsito.
4. Pestaña «Diferencias pendientes», «Resolver»: salida según el tipo, cantidad, causa y motivo; lo aprueba otra persona.
5. Un despacho no recibido se «Anular» con motivo: la mercancía vuelve del tránsito al origen.

### Conteos físicos

1. **Inventario → Conteos**, «Nuevo conteo»: bodega, alcance (toda la bodega, categorías, ubicaciones, productos o clase
   ABC), si es **ciego** y los contadores. «Crear el conteo».
2. «Abrir (tomar la foto)»: congela el teórico a esa hora. Con conteos abiertos con foto el mes no se cierra.
3. Captura: código y Enter; cada lectura suma y se envía por tandas («Enviar lecturas»); una cantidad negativa corrige.
   Con lote, escriba el lote antes de leer. En un conteo ciego quien sólo captura no ve el teórico.
4. «Cerrar el conteo»: compara, exige el reconteo de las líneas que pasan la tolerancia y numera.
5. «Generar ajuste (queda en aprobación)»: lo aprueba alguien que no abrió ni contó. La fecha del ajuste es la de la foto
   o la de la aprobación, según el parámetro «Conteo.FechaDelAjuste».

### Períodos de inventario

**Inventario → Períodos**: los meses desde el corte del primer saldo inicial.

1. «Revisar cierre»: bloquea un mes que no terminó o con conteos abiertos con foto; avisa los borradores, lo que está en
   aprobación, los traslados sin recibir, las remisiones viejas sin facturar y los mensajes pendientes.
2. «Cerrar el mes»: con avisos, hay que reconocerlos. Queda fijado el valorizado por grupo contable y bodega.
3. «Reabrir…»: sólo el último mes cerrado, con motivo y el permiso especial.
4. El **valorizado a cualquier fecha** se consulta en la misma pantalla («Incluir tránsito», «Consultar», «Excel»).

### Puesta en marcha: saldo inicial, cifras de referencia y activación

La puesta en marcha es **bodega por bodega**. La guía completa para quien dirige la implantación está en
[../operaciones/inventario-puesta-en-marcha.md](../operaciones/inventario-puesta-en-marcha.md).

1. **Conteo físico** de la bodega antes de cargar.
2. **Cifras de referencia** (Inventario → Cifras de referencia, plantilla 15): las existencias y valores del sistema
   anterior a una fecha, **sólo para comparar**; nunca mueven el inventario. Un código que no está en el catálogo nuevo
   queda «Sin resolver» con su grupo contable. Importar otra vez la misma fecha y bodega deja el anterior como historia.
   Los comparativos por producto y bodega están en Inventario → Informes.
3. **Saldo inicial** (Inventario → Saldo inicial, plantilla 14): «Revisar» da los errores por fila y columna y el
   resumen por bodega; «Aplicar» crea los borradores (en tramos de 4.000 líneas) fechados en el corte. Cada borrador se
   confirma desde «Acciones» y lo aprueba **otra persona**.
4. **Activación** (Inventario → Activación de bodegas, o la pestaña «Saldo inicial y activación» de la bodega): elija la
   bodega y la fecha de corte. La pantalla compara, por **conjunto de cuentas** contables, el saldo contable al corte con
   el valorizado de esta bodega, el de las bodegas ya activas que comparten esas cuentas y, aparte, el de las que siguen
   fuera del módulo con sus cifras de referencia; muestra la diferencia, los mensajes que la explican y lo que impide
   activar. «Volver a comparar» recalcula.
   - Sin diferencia: «Activar la bodega».
   - Con diferencia: se acepta con motivo, y sólo quien tiene el permiso de aceptar diferencias.
   - Impiden activar: el saldo inicial sin confirmar, una fecha de corte distinta de la del saldo, la bodega ya activa,
     el período cerrado, bodegas fuera del módulo que comparten cuentas y no tienen cifras de referencia al corte, y
     grupos de la bodega sin regla en la matriz contable.
   - **Si la contabilidad de la cooperativa no está iniciada** (o Contabilidad no responde), no hay saldo contable con qué
     comparar y **en producción la bodega no se activa**.

Desde que se activa, la bodega sólo la opera el módulo nuevo; en el sistema anterior se cierra.

### Costeo

El método y el ámbito del costo son parámetros (Inventario → Parámetros, «Costeo.Metodo» y «Costeo.Ambito»):

- **Promedio ponderado** o **PEPS**, para toda la cooperativa o por bodega.
- **Cambiar el método o el ámbito** («Cambiar…», «Registrar el cambio»): sólo el primer día de un período abierto, sin
  movimientos desde ese día, con justificación y acta, y con el permiso de costeo. **No se deshace.** El informe
  «Valorizado por cambio de método» muestra el valor por los dos métodos.
- **Fechas anteriores**: si la cooperativa las permite («Costeo.RetroactivosPermitidos» y sus días máximos), un
  documento puede registrarse antes de movimientos ya confirmados. Antes de confirmar, la pantalla avisa y «Ver impacto en
  costos» muestra los documentos afectados con lo que va a inventario y a costo de venta. El costo de esos documentos se
  ajusta sin reescribirlos.

### Tipos de documento

**Inventario → Tipos de documento**: la **clase** la fija el sistema y decide el efecto (entrada, salida, traslado,
venta…); cada tipo de la cooperativa elige su clase al crearse. «Nuevo tipo»: clase, código, nombre, consecutivo y los
campos que exige (tercero, centro de costo, referencia externa, motivo, fecha futura…). La clase y el código no cambian.

- «Ver» muestra el historial de consecutivos, el modo de paso a Contabilidad y la política de aprobación vigentes (estos
  dos se cambian en Parámetros y en Políticas de aprobación).
- «Cambiar prefijo o número»: un consecutivo nuevo con motivo desde una fecha; el número no puede quedar en o bajo uno ya
  emitido. Las clases que numera la DIAN no tienen consecutivo propio.
- «Plantilla» (la 8).

### Parámetros

**Inventario → Parámetros**: cada clave con su valor vigente, si sale de una vigencia o del valor por defecto, y lo
programado. «Historial» muestra todas las vigencias con autor, fecha y motivo. «Nueva vigencia»: valor, ámbito (general,
una bodega o un tipo de documento), fecha desde y motivo. Los documentos de hoy siguen con el valor de hoy.

Algunos que conviene revisar al arrancar: costeo, stock negativo permitido, tolerancias de reconteo y fecha del ajuste
de conteo, tolerancias del cruce de compras, modo de paso a Contabilidad, venta bajo costo, lote vencido, días de la
reserva de pedidos, días máximos de una remisión sin facturar, arqueo ciego y una sesión por cajero.

### Aprobaciones y políticas

- **Inventario → Políticas de aprobación**, pestaña «Políticas»: por sujeto y tipo de documento, «Nueva versión» con sus
  niveles en orden («Agregar nivel»), el umbral y el permiso de quien aprueba cada nivel, fecha desde y motivo.
- Pestaña «Montos máximos», «Fijar monto»: por rol y permiso (compras, ajustes, notas de venta, crédito). Vacío es sin
  límite. Un documento que supera el monto de quien confirma exige al menos el nivel 1; sin política, se rechaza.
- **Inventario → Aprobaciones**: «Por decidir» trae lo pendiente para usted; «Seguimiento», todo lo de su alcance. «Ver»
  muestra los niveles y quién decidió. «Aprobar» o «Rechazar» (con motivo; devuelve el documento a borrador). Quien pidió
  la aprobación la puede retirar con «Retirar la solicitud». Al aprobar el último nivel, el documento se confirma y se
  numera.

### Alertas

**Inventario → Alertas**, pestaña «Bandeja»: las alertas que le llegan (quiebre, reorden, integridad, aprobaciones
pendientes, eventos RADIAN, remisiones sin facturar…) por estado, tipo, severidad y fecha. «Atender…» con una nota de lo
que se hizo. Pestaña «Tipos»: a qué permisos llega cada tipo, por qué canales (la aplicación siempre, el correo
opcional) y sus umbrales; una vigencia nueva pide motivo. «Sin destinatario» es una alerta que se mandó al administrador
porque nadie activo tenía el permiso.

### Integridad del kardex

**Inventario → Integridad del kardex**: «Verificar» comprueba que existencias y costo sean la suma exacta del kardex
(sin filtros, todo lo de su alcance) y lista cada diferencia. «Reconstruir…» recalcula desde el kardex sin tocar un solo
movimiento, con motivo y su permiso; sin filtros puede tardar minutos. La misma verificación corre cada noche y, si
encuentra diferencias, levanta la alerta.

### Relación con Contabilidad

Inventario **no escribe asientos ni conoce cuentas**. Cada operación confirmada deja un **mensaje** para Contabilidad con
el grupo contable, la bodega, el medio de pago, la tarifa o la causa; la **matriz** de la contadora dice la cuenta.

**Modo de paso**: el parámetro «Contabilidad.ModoDePaso» decide, por tipo de documento, si cada operación pasa **en
línea**, **por lotes** o **no pasa**. **Si la contabilidad de la cooperativa no está iniciada**, el valor por defecto es
**«no pasa»**: los documentos se confirman igual y lo que no pasó se envía después con «Enviar lo que no aplicaba…». Un
modo guardado a propósito se respeta. Dejar sin paso un tipo fiscal pide confirmarlo.

Del lado de la contadora (**Contabilidad → Inventario**):

1. **Matriz contable**: una pestaña por familia (inventario y costo, ventas, compras, impuestos y retenciones, medios de
   pago, caja, traslados y puentes). «Nueva regla»: operación, rol, dimensiones, la cuenta (sólo auxiliares habilitadas
   para Inventario), vigencia y motivo. Gana la regla más específica. Una versión nueva cierra la vigente la víspera y no
   puede empezar en o antes de lo ya contabilizado con esa regla. La plantilla 16 carga la matriz completa.
2. **Tipos de comprobante**: qué comprobante y qué documento cruce usa cada operación.
3. **Completitud de la matriz**: a una fecha, lo que falta para que todo lo que Inventario usa se pueda contabilizar.
   Vacía, se puede pasar en línea.
4. **Lotes de contabilización**: los lotes programados, de cierre de turno o de período, manuales y de reproceso. «Lote
   manual»: rango y tipo, «Previsualizar» y ordenar con motivo.

Del lado de Inventario:

- **Inventario → Bandeja de mensajes**: cada mensaje en orden, con su estado (pendiente, en lote, procesado con su
  comprobante, rechazado con el motivo, no aplica). «Ver» muestra el contenido, cada intento y de qué depende. Marque los
  rechazados ya corregidos y «Reprocesar» con motivo: cada uno se contabiliza con su fecha original. «Enviar lo que no
  aplicaba…» manda, con vista previa y motivo, lo de un tipo que antes no pasaba.
- **Inventario → Conciliación con Contabilidad**: fecha y «Conciliar». Compara el inventario valorizado con el saldo
  contable por conjunto de cuentas, con lo pendiente, en lote, rechazado y sin explicar; el detalle por bodega, lo movido
  por tipos que no pasan y las bodegas todavía fuera del módulo con sus cifras. «Excel» o «PDF».

### Tablero e informes

- **Inventario → Tablero**: por sucursal, bodega y fecha de corte, el valor del inventario, rotación y días, margen del
  mes, ventas del día y del mes frente al período anterior, reorden, quiebres, próximos a vencer, mensajes, lotes,
  documentos DIAN, tipos fiscales sin paso a Contabilidad y alertas. El borde de cada ficha dice la severidad; un clic
  abre su detalle.
- **Inventario → Informes**: elija la vista, el rango o el corte, la bodega y los filtros, y «Consultar». Entre otras:
  kardex, existencias, valorizado, documentos, reorden y quiebres, comparativos con las cifras de referencia, diferencias
  de conteo, eventos RADIAN, cruce de compras a tres vías, valorizado por cambio de método, rotación, margen, análisis
  ABC, sin movimiento, próximos a vencer, sugerido de compras, tope de faltantes y mermas, indicios de deterioro,
  documentos electrónicos DIAN, mensajes a otros módulos, lotes contables y conciliación. La fila de un documento abre el
  documento; la de un producto, su kardex. «Excel», «Word» o «PDF».
- **Inventario → Plantillas**: las dieciséis en su orden de carga, con «Plantilla vacía», «Plantilla con los datos
  actuales», «Revisar» (sin guardar; «Revisión en Excel» devuelve el libro con el resultado por fila) y «Aplicar» (todo o
  nada; una plantilla nunca borra).

## Reglas importantes

- **Todo lo que mueve inventario es un documento** de un tipo de la cooperativa. Lo confirmado no se edita ni se borra:
  se anula con un documento contrario, con su propia fecha y al costo del original.
- **La existencia es la suma del kardex.** Nadie la digita; si algo no cuadra, se verifica la integridad.
- **Sin stock negativo**, salvo que el parámetro lo permita.
- **El borrador y lo que está en aprobación no tienen número**: el número se asigna al confirmar.
- **Quien crea no aprueba**, y quien aprobó un nivel no aprueba otro.
- **Fechas en meses cerrados no se admiten.** Lo retroactivo sólo si el parámetro lo permite, salvo el saldo inicial de
  una bodega sin activar y los ajustes de conteo.
- **Una bodega fuera del alcance no existe para el usuario.**
- **Los parámetros cambian por vigencia** desde una fecha, con motivo; el historial conserva todas.
- **Inventario nunca decide una cuenta**: si falta la regla en la matriz, el mensaje se rechaza en Contabilidad y se
  reprocesa cuando la contadora la crea.
- **En producción ninguna bodega se activa sin cuadre con Contabilidad**; con diferencia, sólo se activa aceptándola con
  motivo y permiso.

## Preguntas frecuentes

**El producto tiene existencia en el kardex pero el POS dice que no hay disponible.**
Puede estar reservado por un pedido o en tránsito. Existencias muestra física, reservada, disponible y en tránsito.

**Confirmé un ajuste con un error.**
Anúlelo con motivo y regístrelo de nuevo. No se edita.

**¿Por qué no me deja cerrar el mes?**
«Revisar cierre» lo dice: el mes no ha terminado o hay un conteo abierto con foto. Los avisos (borradores, aprobaciones,
traslados sin recibir) no bloquean: se reconocen.

**Cerré el mes y me faltó un documento.**
Quien tiene el permiso especial reabre el último mes cerrado, con motivo.

**El mensaje a Contabilidad salió rechazado.**
«Ver» dice el motivo y quién lo corrige (casi siempre falta una regla en la matriz). Corregido, «Reprocesar».

**Todavía no hemos iniciado la contabilidad. ¿Podemos operar inventario?**
Sí: lo confirmado queda «no pasa» y se envía después con «Enviar lo que no aplicaba…». Pero en producción no se puede
activar ninguna bodega hasta que la contabilidad esté iniciada y la matriz tenga las reglas de inventario.

**La activación muestra una diferencia.**
Revise los mensajes que la explican (pendientes, en lote, rechazados), las cifras de referencia de las bodegas que
comparten cuentas y el saldo contable al corte. Si la diferencia es conocida y aceptada, quien tiene el permiso la acepta
con motivo.

**No veo una bodega.**
No está en su alcance. Pídale al administrador que se la asigne en Alcance comercial.

## Lo que todavía no está

- **Valores por confirmar con el dueño o la contadora**: la regla de cantidad del saldo inicial (conteo más movimientos
  hasta el corte, o bodega congelada desde el conteo), las tolerancias de reconteo y la fecha del ajuste de conteo, los
  roles, políticas de aprobación y montos máximos, los umbrales de los informes (clase ABC, días sin movimiento, próximos a
  vencer, tope de faltantes con su norma) y la validación de los tipos de comprobante y de la matriz contable.
- **Extracción de las cifras de referencia** desde la base del sistema anterior: el guion para producir la plantilla 15 no
  está hecho; hoy la plantilla se diligencia a mano o con una extracción propia.
- **Verificación nocturna de la cadena de sellos de la auditoría**: la verificación es manual (Administración →
  Auditoría, pestaña «Integridad»). La verificación nocturna del **kardex** sí corre.
- **Movimientos históricos del sistema anterior**: no se migran; se entra con saldos.
