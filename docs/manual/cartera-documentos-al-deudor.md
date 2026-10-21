# Cartera: documentos al deudor y su verificación

> Feature 013 (cartera financiera), fase 14 (US13, entrega K1) y fase 19 (US13, entrega K5: extracto, certificado tributario y
> comparación del PDF). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §15 (documentos al deudor y verificación),
> [contracts/adaptadores-y-archivos.md](../../specs/013-cartera-financiera/contracts/adaptadores-y-archivos.md) §8 (documentos emitidos con
> código de verificación) y §9 (adjuntos de Cartera), y [data-model.md](../../specs/013-cartera-financiera/data-model.md) §27. Aquellos son el
> contrato; esto es la receta para quien atiende al deudor en la oficina y para quien configura Cartera.

Cartera emite a nombre de una persona cinco documentos: el **plan de pagos** de un crédito, el **paz y salvo** (de una obligación o
general), el **certificado de saldo** y, desde la entrega K5, el **extracto** de un período y el **certificado tributario** de un
año. Cada uno sale **en un paso** —se elige, se confirma una vez y queda emitido—, con su
número, un **código de verificación** y un **QR** que lleva a la página de verificación. El PDF se pinta una sola vez, al
emitir, y se guarda con su huella (SHA-256): reimprimir es bajar ese mismo archivo. Un documento emitido no se edita: si algo
está mal se anula y se emite otro.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Buscar documentos (crédito o deudor, clase, estado, fecha de emisión) y ver su detalle con los eventos | Cartera → Documentos (`/cartera/documentos`) | `Lending.Documents.Read` |
| Emitir (plan de pagos, paz y salvo, certificado de saldo, extracto, certificado tributario) y enviar por correo | botón «Emitir» y «Enviar por correo» del detalle | `Lending.Documents.Issue` |
| Volver a descargar el PDF (reimprimir) | «Descargar PDF» del detalle | `Lending.Documents.Reprint` |
| Anular con motivo | «Anular…» del detalle | `Lending.Documents.Void` |
| Verificar un código y comparar un PDF con el emitido | Cartera → Verificar documento (`/cartera/verificar`) y el QR del PDF | `Lending.Documents.Verify` |
| Firmante, logotipo e imagen de la firma | Cartera → Parámetros (pestaña «Parámetros» y pestaña «Documentos») | `Lending.Parameters.View` / `.Manage` |

Desde el expediente de un crédito (`/cartera/creditos/{id}`), el botón **«Documentos»** abre la lista de ese crédito con el
diálogo de emitir ya en él. Un documento se ve sólo si **todos** los créditos que cubre están en el alcance de oficina y línea
de quien consulta; fuera del alcance no existe (el mismo «no encontrado»).

Antes de activar Cartera no se emite nada. El **extracto** y el **certificado tributario** se emiten desde la entrega K5; una
versión anterior del programa los rechaza con «disponible en una entrega posterior» (`Lending.Document.KindNotAvailable`).

## 2. Las clases y cuándo se niegan

| Clase | Número | Qué certifica | Se niega cuando |
|---|---|---|---|
| Plan de pagos | `PP-000001` | La versión del plan **vigente al emitir**, cuota a cuota (capital, interés, seguros, otros cargos, cuota y saldo), con las condiciones del crédito y las tasas **efectiva anual** y **nominal anual** de su periodicidad. | El crédito no está vigente o no tiene plan. |
| Paz y salvo de una obligación | `PS-000001` | Que el crédito está **cancelado**: su saldo es cero hoy, con todo lo registrado hasta el instante de emitir. | Queda saldo: la negativa dice el crédito y cuánto debe. |
| Paz y salvo general | `PSG-000001` | Que la persona está a paz y salvo por **todas** sus obligaciones de crédito con la cooperativa. Cubre las obligaciones como deudora y, si el parámetro `Documentos.PazYSalvoIncluyeCodeudas` lo dice, también como codeudora (el documento dice si las incluyó). | Alguna obligación tiene saldo (dice cuáles), o hay una **venta a crédito de Comercial en reproceso** para la persona (dice la factura): esa venta todavía puede convertirse en un crédito. |
| Certificado de saldo | `CS-000001` | La liquidación del crédito a una fecha de corte (hoy si no se elige otra) con la información hasta el instante de emitir, por concepto y con lo que cuesta cancelar. Si la fecha es futura, la liquidación se proyecta sin pagos y el documento dice **hasta cuándo vale**. | El crédito no tenía saldo a esa fecha o no se ha desembolsado. |
| Extracto (K5) | `EX-000001` | Un período de un crédito (§2a): saldo inicial por concepto, las operaciones por fecha valor y el saldo final, con la información hasta el instante de emitir. | Sin período, con la fecha inicial después de la final, que termine después de hoy o de más de cinco años («El período del extracto no es válido…», `Lending.Document.PeriodInvalid`); un período sin operaciones **y** sin saldo —por ejemplo, después de cancelado el crédito— no tiene nada que certificar. |
| Certificado tributario (K5) | `CT-000001` | Un año terminado (§2b): por cada crédito en que la persona es **deudora**, los intereses pagados en el año y el saldo al 31 de diciembre. | El año no ha terminado: el de 2026 se emite desde el 1 de enero de 2027 (`Lending.Document.TaxYearNotClosed`); la persona no tuvo créditos ese año. Si no se elige año, es el anterior. |

Cada clase lleva su propio consecutivo. El documento guarda una **copia** del nombre y la identificación del titular tal como
estaban al emitir: si después se corrige la persona en el maestro, el documento no cambia.

### 2a. El extracto

Se pide desde el diálogo de emitir con la clase **Extracto**, el crédito, el titular y el período (por defecto, el mes anterior).
El documento tiene tres partes:

1. **Saldo inicial**: lo que se debía la víspera del primer día del período, por concepto (capital, interés corriente, interés de
   mora, sus suspensos, seguros, gastos de cobranza, otros cargos, anticipo y saldo a favor).
2. **Operaciones del período**, ordenadas por **fecha valor**: el desembolso, cada pago con lo que aplicó a capital, intereses y
   otros conceptos, los cargos, condonaciones, ajustes y traslados, y la **causación** —los intereses y seguros que el crédito
   fue generando— agregada en un renglón por mes. Los valores van con signo: **+** aumenta la deuda, **−** la disminuye.
3. **Saldo final**: lo que se debía el último día del período, por concepto.

Saldo inicial + operaciones = saldo final, al centavo, en cada concepto: si no cuadrara, el documento no se emite. Un pago
**reversado** ya no hace parte del crédito: el extracto lo informa en su propio renglón («Pago … reversado por …», con su valor y
«sin efecto en el saldo») para que el deudor entienda lo que vio en su recibo. El extracto se arma con lo registrado **al emitir**:
si después se reversa un pago del período, el extracto queda anulado (§7) y se emite otro.

### 2b. El certificado tributario

Se pide con la clase **Certificado tributario**, el titular (el crédito sólo sirve para elegirlo) y el año. Lista cada crédito en
que la persona es **deudora** —sólo como deudora: las codeudas no se certifican, lo confirmó el dueño el 2026-10-16— con:

- **Intereses pagados en el año**: lo que los pagos aplicaron a **interés corriente** y a **interés de mora** —incluidos los que
  estaban en suspenso— con **fecha valor dentro del año**, **neto de los pagos reversados** que la cooperativa conocía al emitir.
  Así, un pago de diciembre reversado en enero, antes de emitir, no cuenta; uno reversado después de emitir anula el certificado
  (§7). Esta definición es nueva y la **valida la contadora** con los casos de ejemplo antes de entregarle el certificado a los
  asociados (quickstart §12). No cuentan los intereses causados y no pagados, ni los seguros ni los gastos de cobranza.
- **Intereses cubiertos con anticipo** (decisión del dueño del 2026-10-16): cuando un anticipo (lo pagado antes de que la cuota
  venza) se aplica a una cuota dentro del año, el interés que cubre **cuenta como pagado sólo si el anticipo ingresó dentro del
  año**, es decir, si la fecha valor del pago que lo dejó cae en el año. Si ingresó antes —por ejemplo, un pago de diciembre que
  adelanta la cuota de enero—, ese interés **no cuenta** en el certificado del año siguiente aunque se aplique en él (y tampoco en
  el del año del pago, porque entonces todavía era anticipo y no interés). Nada se cuenta dos veces: el pago que deja el anticipo
  no suma interés por esa parte, sólo la aplicación. Cuando un anticipo mezcla dinero de dos años, se usa primero el más antiguo,
  y dentro de una cuota el dinero cubre los conceptos en el orden de la prelación. Lo mismo vale para una **cancelación** que usa
  un anticipo: de sus intereses se resta lo que cubrió el anticipo ingresado antes del año. El PDF no separa esa parte; los datos
  del documento la guardan como `interestPaidWithAdvance`.
- **Saldo al 31 de diciembre**, con la información al emitir, de los conceptos que la cooperativa configure (§8): por defecto
  capital, intereses corrientes y de mora (con sus suspensos) y los demás cargos (seguros, gastos de cobranza, otros), menos el
  anticipo. Un concepto que no se incluye sale en cero en su columna.
- **Anticipo recibido** (decisión del dueño del 2026-10-18), en un renglón aparte debajo de la tabla: el anticipo que **ingresó en el
  año y al 31 de diciembre sigue sin aplicar** —el que se aplicará a intereses (y a lo demás de la cuota) el año siguiente—. Es
  informativo y **no suma** a los intereses pagados: como ingresó en este año, el interés que cubra el año siguiente no contará en el
  certificado de ese año (regla de arriba), y así el asociado ve en el de este año lo que pagó por adelantado. Sólo sale si no es cero;
  los datos del documento lo guardan como `advanceReceived` (y el total, `totalAdvanceReceived`).
- **Corrección monetaria UVR** (decisión del dueño del 2026-10-18), también en renglón aparte: en un crédito en UVR, la corrección
  monetaria **causada en el año** sobre lo que se debe (capital, intereses y cargos; no el anticipo ni el saldo a favor). No suma a los
  intereses pagados; en un crédito en pesos no sale. Datos: `monetaryCorrection` y `totalMonetaryCorrection`.

Un crédito **cancelado en el año** aparece con saldo cero y sus intereses pagados; uno desembolsado **después** del año o cancelado
**antes** no aparece. Los créditos de **vivienda** —los de segmento SARC `VIVIENDA` según el catálogo de modalidades vigente al 31
de diciembre— van en su propia sección, «Créditos de vivienda», con su total: es la que el asociado usa para la deducción por
intereses de vivienda. **Hasta la entrega K6b no hay créditos de vivienda**, así que esa sección dice «La persona no tuvo créditos
de vivienda con la cooperativa en el año». El certificado lleva la leyenda «Certifica sólo las obligaciones de crédito de la
cooperativa.» y cómo se cuentan los intereses.

**El paz y salvo general certifica sólo las obligaciones de crédito de Cartera** y lo dice en el PDF. Las ventas a crédito que
la cooperativa aceptó **fuera de Cartera** al ponerla en marcha (las que la migración no trajo y quedaron con su acta) no lo
niegan: el documento las **menciona** con su factura como ventas que no hacen parte de Cartera.

## 3. La firmeza del paz y salvo

Un pago puede devolverse después de aplicado (un cheque sin fondos, una consignación que no llegó). Por eso el paz y salvo de
un crédito recién cancelado sale **sujeto a firmeza**: lleva la leyenda «La cancelación está sujeta a firmeza hasta el
{fecha}», y su estado es «Sujeto a firmeza» hasta esa fecha; desde el día siguiente es «Vigente» sin que nadie haga nada.

La fecha es el día del registro del pago que canceló más los **días hábiles** de su clase de medio de recaudo, según el
parámetro `Pagos.FirmezaDiasPorClaseDeMedio` (por defecto 10 días hábiles; 0 para los cruces internos), con el calendario de
días hábiles de la cooperativa. Si el general cubre varios créditos, manda la firmeza más lejana.

## 4. El código y el QR

El código tiene **16 caracteres** (letras y números, sin I, L, O ni U para que no se confundan) y se muestra en cuatro grupos:
`7K2M-Q9XD-4HTP-B1RW`. No se puede adivinar ni deducir del número del documento. El QR del PDF lleva a
`{dirección de la aplicación}/cartera/verificar?codigo=7K2M-Q9XD-4HTP-B1RW`.

Al digitarlo se admiten minúsculas, espacios y guiones, y se leen la I y la L como 1 y la O como 0.

## 5. Quién verifica

**Sólo usuarios de la cooperativa** con `Lending.Documents.Verify` (decisión D21): no hay verificación anónima. Quien recibe un
documento —un banco, otra cooperativa, un juzgado— pide a la cooperativa que lo verifique, o la cooperativa le da un usuario con
ese permiso. Escanear el QR sin sesión lleva al ingreso y, al entrar, vuelve a la verificación.

La verificación dice si el código corresponde a un documento **de esta cooperativa** (cada cooperativa tiene su base: un
código de otra no existe aquí), su clase, número, fecha, estado —vigente, sujeto a firmeza o **anulado con el motivo y la
fecha**—, el titular con el documento enmascarado (sólo los cuatro últimos dígitos) y los datos esenciales que el documento
certificó. No muestra el expediente. Cada verificación queda en la auditoría (`Lending.Document.Verified`).

### Comparar con un PDF (K5)

Un código auténtico prueba que el documento existe, no que el PDF que alguien presenta sea el mismo: pudo editarse. Con el código
verificado, **«Comparar con un PDF…»** pide el archivo que la persona tiene en su equipo, el **navegador** calcula su huella
(SHA-256) y sólo esa huella se envía; el archivo **no se sube**. La respuesta dice «El PDF es idéntico al emitido» o «El PDF NO es
el emitido: … puede estar alterado o ser de otro documento». La comparación queda en la misma lectura auditada, con su resultado.
Por la API es `GET /api/lending/documents/verify/{código}?sha256={huella en hexadecimal}`, que responde `pdfMatches`.

## 6. Descargar, reimprimir y enviar

- Al emitir con **descarga**, el navegador baja el PDF de inmediato (`{número}.pdf`).
- **Reimprimir** («Descargar PDF» del detalle) emite un enlace de 60 segundos al **mismo** PDF guardado —idéntico byte a byte,
  con la misma fecha y el mismo código—; cada reimpresión queda en la auditoría (`Lending.Document.Reprinted`). Pedir el enlace
  otra vez da otro enlace, no otro documento.
- Al emitir con **correo**, o con «Enviar por correo» después, el PDF sale como adjunto del mensaje al deudor de propósito
  «Entrega de documento» (plantilla `ENVIODOC`) al correo del titular. Como lo pide el titular, no espera las franjas de
  contacto de la Ley 2300. Hace falta que la persona tenga correo registrado y **autorización de tratamiento de datos general**;
  si falta alguno, no se emite ni se pinta nada y la pantalla dice qué falta. Cuando el correo sale, el documento recibe el
  evento «Entregado por correo».
- Un documento **anulado** no se reimprime ni se envía: se emite otro.

## 7. Anulación manual y automática

- **Manual**: «Anular…» con motivo. El documento y su PDF se conservan; desde ese momento la verificación lo muestra anulado
  con el motivo. No se anula dos veces.
- **Automática**: cuando se **reversa** un pago (desde el expediente del crédito o por el recaudador, con el contrato de
  obligaciones) o un desembolso, en la misma operación se anula **todo documento que cubra ese crédito emitido después del
  registro de lo reversado** —un paz y salvo que se apoyaba en el pago devuelto, un certificado de saldo que lo contaba—. El
  motivo dice «Se reversó el pago {número} del crédito {número}.» y la operación causa es el reverso. Vale igual para el
  **extracto** del período que contenía el pago y para el **certificado tributario** del año: los dos se emitieron después del pago
  reversado y lo contaban; un extracto de un período anterior emitido antes del pago no se anula. La vista previa del
  reverso lista los documentos que va a anular y la respuesta los devuelve. Lo emitido **antes** del pago reversado (por ejemplo,
  el plan de pagos que se imprimió al desembolsar) no se toca.

## 8. Configuración

1. **Firmante** (obligatorio): en Cartera → Parámetros, `Documentos.Firmante.Nombre` y `Documentos.Firmante.Cargo`, con
   vigencia desde una fecha y motivo. Sin ellos no se emite nada (`Lending.Document.SignerMissing`). El documento guarda el
   nombre y el cargo vigentes al emitir.
2. **Codeudas en el paz y salvo general**: `Documentos.PazYSalvoIncluyeCodeudas` (por defecto no). El certificado tributario no
   tiene esta opción: certifica sólo como deudor.
3. **Saldo del certificado tributario** (desde el 2026-10-16): `Documentos.CertificadoTributario.SaldoIncluye`, los conceptos
   separados por coma: `Capital`, `InteresCorriente`, `InteresDeMora`, `OtrosCargos` (seguros, gastos de cobranza y otros cargos)
   y `MenosAnticipo` (resta el anticipo). Por defecto los cinco, que es el saldo de siempre; si nadie lo configura, nada cambia.
   Por ejemplo, `Capital,InteresCorriente` deja fuera la mora, los cargos y el anticipo. Rige el valor vigente **el día de la
   emisión** (al reimprimir un certificado ya emitido no cambia: el PDF guardado es el que vale) y queda en los datos del
   documento como `balanceIncludes`.
4. **Logotipo e imagen de la firma** (opcionales; pregunta I6 de las decisiones transversales, rige la propuesta por defecto
   mientras el dueño no responda): en Cartera → Parámetros → pestaña **«Documentos»**, «Subir logotipo» y «Subir imagen de la
   firma», en JPEG o PNG. Subir otra **reemplaza** la vigente (la emisión toma siempre la última); la anterior se puede borrar
   desde la lista. Un documento ya emitido conserva su PDF con la imagen que tenía. Sin imágenes, el PDF lleva el membrete con
   el nombre y el NIT de la cooperativa y la línea de firma con el nombre y el cargo. Ver exige `Lending.Documents.Read`;
   subir y borrar, `Lending.Parameters.Manage`. La firma digital certificada queda fuera de esta entrega.
5. Fuera de producción todo PDF lleva **«AMBIENTE DE PRUEBAS – SIN VALIDEZ»** (como leyenda y marca de agua) y el documento
   queda marcado así para siempre.

## 9. Lo que llega después

- **K5** (fase 20): la vista de informe «documentos emitidos» del centro de informes de Cartera.
- **K6b**: los créditos de vivienda con la Ley 546 y la UVR; la sección «Créditos de vivienda» del certificado tributario se llena
  sola cuando existan, con el renglón de la corrección monetaria UVR de cada crédito (2026-10-18).
- La firma digital certificada del PDF no está en el alcance de la feature.
