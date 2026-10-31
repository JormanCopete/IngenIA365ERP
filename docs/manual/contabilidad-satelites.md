# Contabilidad: activos fijos, impuestos y certificados, información exógena

Entrega E4 de la feature 009 (2026-10-30). Este manual es para el contador y para quien parametriza la contabilidad de una
cooperativa. La conciliación bancaria **no** está aquí: es de Tesorería (`/tesoreria/conciliacion`, feature 015).

Todo lo de esta entrega lee el libro por el mismo punto de lectura que los informes (`MovimientosContables`). No hay saldos
guardados, y lo que se contabiliza va por el contrato único (`AccountingPoster`).

**Semillas y ajustes de cada empresa.** Los formularios 350 y 300 y la exógena del año (validados por la contadora el 2026-10-31) se
**siembran solos al iniciar la contabilidad**. Una cooperativa que ya la tenía iniciada los carga con los botones «Cargar formularios
sugeridos» y «Cargar la semilla del año». Desde ahí cada empresa los ajusta a lo suyo de dos maneras:
- **por pantalla**: renglones de cada formulario; formatos nuevos con su disposición (columnas y atributos del XML); conceptos y cuentas
  de cada formato;
- **por plano**: «Bajar plano» trae un Excel lleno con lo configurado; se edita y se vuelve a subir con «Subir plano».

El plano funciona como la carga de auxiliares:
- con un solo error no se guarda nada, y se informa fila, columna y problema;
- cada formulario o formato que viene en el archivo se reemplaza entero, con las mismas reglas que la pantalla;
- los que no vienen no se tocan.

Cada año gravable nuevo trae su semilla; también se puede copiar el año anterior.

---

## 1. Activos fijos y diferidos (`/contabilidad/activos`)

### Qué se registra

Cada activo lleva:
- **Código** de la cooperativa (10 caracteres), nombre y clase: **activo fijo** o **diferido o intangible**.
- **Cuentas**:
  - la del activo;
  - la de depreciación o amortización acumulada (obligatoria en un activo fijo; un diferido puede amortizarse contra su propia cuenta);
  - la de gasto.

  Las tres deben ser de movimiento, estar activas y estar **habilitadas para Activos** (módulo `ACT` en las reglas de la cuenta).
- Sucursal, centro de costo (opcional), proveedor (una persona), fecha y documento de compra.
- Costo, valor residual, vida útil en meses y **primer mes de depreciación**.
- Para los activos que la cooperativa **ya traía** al implantar: la **depreciación acumulada al entrar** y los **meses ya
  depreciados**. Ese saldo llega al libro por la apertura, no por una corrida. En un activo nuevo ambos van en cero.

Registrar no contabiliza nada: la compra ya está en el libro, porque la registró Compras, un comprobante o la apertura.

### Cómo se calcula la cuota

Línea recta **sobre el valor neto en libros**: lo que falta por depreciar (costo − residual − acumulada) repartido en los meses
de vida útil que quedan. Las cuotas que faltan no se guardan: el sistema las calcula cada vez. Eso tiene tres consecuencias:

- **Cambiar la vida útil o el residual** (motivo opcional, queda auditado) sólo mueve las cuotas siguientes. Las contabilizadas
  no cambian.
- **Un mes sin correr se acumula** en la cuota de la corrida siguiente. Por ejemplo, un activo que empezó en un mes que ya estaba
  cerrado.
- **La última cuota se lleva el redondeo**, así el activo queda exactamente en su residual.

Con cuotas contabilizadas ya no se cambian las cuentas, el costo, el primer mes ni el saldo inicial
(`Accounting.Assets.Locked`).

### La corrida del mes

Pestaña «Depreciación del mes»:
1. **Vista previa**: activo por activo, la cuota y cuántos meses cubre.
2. **Correr el mes**: un solo comprobante **DP** fechado el último día del período, con gasto contra acumulada por cada activo.

Reglas:
- Una sola corrida por mes (`Accounting.Assets.AlreadyRun`).
- Sólo en un período abierto.
- Los meses se corren en orden (`EarlierThanLastRun`).
- **Reversar** pide motivo y sólo aplica a la última corrida, con su período abierto. Deja la reversión del DP y el mes queda
  listo para correrse otra vez.
- El DP no se reversa desde Comprobantes: es de Activos.

### Baja o venta

En la ficha, «Dar de baja», con fecha, motivo y la **cuenta contrapartida del neto** (pérdida en baja, cuenta por cobrar de la
venta…). El sistema genera un DP que cancela el costo y la acumulada. Antes hay que correr la depreciación del mes de la baja.

### Informe

Vista **«Activos fijos y diferidos»** en `/contabilidad/informes` (`assets`): costo, residual, acumulada, neto y meses por
depreciar, por sucursal, exportable.

---

## 2. Impuestos (`/contabilidad/impuestos`)

### La cuenta de impuesto

Se marca en **Plan de cuentas › reglas de la cuenta** (desde E1) con:
- su clase:
  - retención en la fuente;
  - **retención de IVA**;
  - **retención de ICA**;
  - IVA, ICA, GMF o renta;
- el concepto;
- si exige base gravable;
- la tarifa con vigencia.

Desde entonces toda línea en esa cuenta trae base, y su valor debe ser la base × la tarifa, dentro de la tolerancia que fija la
configuración contable.

La retención de IVA y la de ICA son clases aparte (desde E4) para que los certificados no mezclen la retención con el IVA y el ICA
propios. Una cuenta de retención de IVA que estaba marcada como «IVA» hay que **remarcarla**.

En la cuenta se valida, no se calcula: hace falta una auxiliar por tarifa. La retención automática en egresos y pagos que no son
compras (honorarios, arriendos) es de Tesorería ampliada (T2), no de esta entrega.

### Informes

En `/contabilidad/informes`:
- **Retenciones practicadas** (fuente, IVA e ICA);
- **IVA generado y descontable**;
- **ICA**;
- **GMF**.

Cada uno muestra cuenta y tercero con la base y el valor de cada lado, y el neto.

### Formularios

Cada renglón suma, sobre las cuentas que empiezan por sus prefijos, una de estas cosas: el valor neto, la base neta, los
débitos, los créditos, la base de los débitos o la base de los créditos. El signo puede ser + o −. El resumen de cada formulario
activo aparece como un informe más («Formulario 350», «Formulario 300»).

Los sugeridos son el **350** (retención en la fuente, un par base/retención por concepto) y el **300** (IVA: ingresos gravados, IVA
generado, compras gravadas, IVA descontable, saldo). Las cuentas salen del catálogo con que se inició la contabilidad: CUIF
2435xx/244010 o PUC comercial 2365xx/2408. Se siembran al iniciar la contabilidad; si faltan, «Cargar formularios sugeridos».

Sus renglones son los conceptos, no la numeración oficial del formulario. El ICA y el GMF los arma cada cooperativa con los renglones
de su municipio, en pantalla («Nuevo formulario») o con el **plano** (columnas `formulario`, `nombreFormulario`, `activo`, `renglon`,
`descripcion`, `suma`, `prefijos`, `signo`).

---

## 3. Certificados de retención (`/contabilidad/certificados`)

Se elige clase (fuente, IVA o ICA), año y, si se quiere, unos meses, y se pulsa **Generar**:
- Sale **un certificado por tercero** con retenciones en las cuentas de esa clase, con **consecutivo por clase**.
- Generar otra vez no repite los vigentes.
- **Reimprimir** (PDF) no cambia el número.
- **Enviar** lo manda al correo registrado en Personas y anota el envío.

Si el libro cambia después (una reversión, un comprobante nuevo con fecha de ese año), el certificado aparece
**desactualizado**. **Reexpedir** le da número nuevo y deja el anterior anulado, apuntando al nuevo.

**No entran** las retenciones de los módulos que expiden sus propios certificados: Captaciones y Cartera (`CAR`) certifican la
retención sobre intereses por su lado. Es configurable por cooperativa (`AccountingSetup.CertificatesExcludedModules`, por defecto
`CAR`).

---

## 4. Información exógena (`/contabilidad/exogena`)

### Parametrizar el año

- La **semilla del año** (sembrada al iniciar la contabilidad, o con «Cargar la semilla del año») trae los formatos que usa una
  cooperativa: **1001, 1003, 1005, 1006, 1007, 1008, 1009, 1010 y 1012**, con sus conceptos, columnas, atributos XML y las cuentas
  propuestas para el catálogo de la empresa.
- **Nuevo formato** crea uno que la semilla no trae, con su disposición: elemento de cada fila del XML, atributo del concepto, atributos
  de identificación y hasta diez columnas (campo, rótulo, atributo XML). La disposición de cualquier formato se corrige en el mismo
  diálogo.
- **Plano** de conceptos y cuentas del año: columnas `formato`, `concepto`, `nombreConcepto`, `columna`, `prefijo`, `suma`. Los formatos
  tienen que existir en el año.
- **Copiar del año anterior** trae lo del año pasado.
- Ninguna de las dos pisa lo que ya está.
- Un formato que no aplica se desmarca.
- «Conceptos y cuentas» edita cada formato. Hay una fila por cuenta con el concepto, la columna, el prefijo y qué suma:
  - débitos, créditos o neto del año;
  - base gravable;
  - **saldo al 31 de diciembre**, para las cuentas por cobrar y por pagar, los aportes y los bancos.

La semilla 2026 la validó la contadora el 2026-10-31. Si la DIAN cambia una versión o un atributo, se corrige en la disposición del
formato, sin programa. La **1019 y la 1020** (captaciones) se generan en Cartera › Captaciones. La **2276** (rentas de trabajo) no
está.

### Generar, revisar y exportar

1. **Generar** suma por tercero y concepto:
   - el movimiento del año, sin el cierre del ejercicio;
   - o el saldo al 31/12.

   Quien no llega a la cuantía mínima va a **menores cuantías** (`222222222`, tipo 43), y también lo que no trae tercero, con
   aviso.
2. La generación toma la identificación de cada persona tal como está ese día: tipo DIAN, número, DV, nombres o razón social,
   dirección y municipio. El DV del NIT se calcula si falta, con aviso.
3. **Inconsistencias**:
   - las **bloqueantes** (documento vacío o inválido, tipo sin código DIAN) impiden el XML;
   - las demás (dirección, municipio) avisan.

   «Corregir la persona» abre su ficha. Después se vuelve a generar: la versión de trabajo anterior se reemplaza.
4. **Excel** para revisar.
5. **XML para la DIAN**: ISO-8859-1, nombre `Dmuisca_…` de 33 caracteres. La primera exportación guarda el archivo con la
   generación, que queda **exportada** con fecha y usuario. Las exportadas no se pierden al regenerar.

Antes de presentar, el XML se pasa por el **prevalidador de la DIAN**.

---

## 5. Permisos

| Permiso | Para |
|---|---|
| `Accounting.Assets.View` / `.Manage` / `.Run` | Ver activos / registrar, corregir y dar de baja / correr y reversar el mes |
| `Accounting.Taxes.View` / `.Manage` / `.Certificates` | Ver y bajar el plano / editar formularios y subir el plano / generar, reexpedir y enviar certificados |
| `Accounting.Exogenous.View` / `.Manage` / `.Export` | Ver y bajar el plano / parametrizar, subir el plano y generar / Excel y XML |
| `Accounting.Reports.View` / `.Export` | Los informes (activos, retenciones, IVA, ICA, GMF, formularios) |
