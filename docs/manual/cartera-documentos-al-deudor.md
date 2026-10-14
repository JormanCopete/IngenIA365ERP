# Cartera: documentos al deudor y su verificación

> Feature 013 (cartera financiera), fase 14 (US13, entrega K1). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §15 (documentos al deudor y verificación),
> [contracts/adaptadores-y-archivos.md](../../specs/013-cartera-financiera/contracts/adaptadores-y-archivos.md) §8 (documentos emitidos con
> código de verificación) y §9 (adjuntos de Cartera), y [data-model.md](../../specs/013-cartera-financiera/data-model.md) §27. Aquellos son el
> contrato; esto es la receta para quien atiende al deudor en la oficina y para quien configura Cartera.

Cartera emite a nombre de una persona tres documentos: el **plan de pagos** de un crédito, el **paz y salvo** (de una obligación o
general) y el **certificado de saldo**. Cada uno sale **en un paso** —se elige, se confirma una vez y queda emitido—, con su
número, un **código de verificación** y un **QR** que lleva a la página de verificación. El PDF se pinta una sola vez, al
emitir, y se guarda con su huella (SHA-256): reimprimir es bajar ese mismo archivo. Un documento emitido no se edita: si algo
está mal se anula y se emite otro.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Buscar documentos (crédito o deudor, clase, estado, fecha de emisión) y ver su detalle con los eventos | Cartera → Documentos (`/cartera/documentos`) | `Lending.Documents.Read` |
| Emitir (plan de pagos, paz y salvo, certificado de saldo) y enviar por correo | botón «Emitir» y «Enviar por correo» del detalle | `Lending.Documents.Issue` |
| Volver a descargar el PDF (reimprimir) | «Descargar PDF» del detalle | `Lending.Documents.Reprint` |
| Anular con motivo | «Anular…» del detalle | `Lending.Documents.Void` |
| Verificar un código | Cartera → Verificar documento (`/cartera/verificar`) y el QR del PDF | `Lending.Documents.Verify` |
| Firmante, logotipo e imagen de la firma | Cartera → Parámetros (pestaña «Parámetros» y pestaña «Documentos») | `Lending.Parameters.View` / `.Manage` |

Desde el expediente de un crédito (`/cartera/creditos/{id}`), el botón **«Documentos»** abre la lista de ese crédito con el
diálogo de emitir ya en él. Un documento se ve sólo si **todos** los créditos que cubre están en el alcance de oficina y línea
de quien consulta; fuera del alcance no existe (el mismo «no encontrado»).

Antes de activar Cartera no se emite nada. El **extracto** y el **certificado tributario** llegan con la entrega K5: hoy la
pantalla no los ofrece y la API responde «disponible en una entrega posterior» (`Lending.Document.KindNotAvailable`).

## 2. Las tres clases y cuándo se niegan

| Clase | Número | Qué certifica | Se niega cuando |
|---|---|---|---|
| Plan de pagos | `PP-000001` | La versión del plan **vigente al emitir**, cuota a cuota (capital, interés, seguros, otros cargos, cuota y saldo), con las condiciones del crédito y las tasas **efectiva anual** y **nominal anual** de su periodicidad. | El crédito no está vigente o no tiene plan. |
| Paz y salvo de una obligación | `PS-000001` | Que el crédito está **cancelado**: su saldo es cero hoy, con todo lo registrado hasta el instante de emitir. | Queda saldo: la negativa dice el crédito y cuánto debe. |
| Paz y salvo general | `PSG-000001` | Que la persona está a paz y salvo por **todas** sus obligaciones de crédito con la cooperativa. Cubre las obligaciones como deudora y, si el parámetro `Documentos.PazYSalvoIncluyeCodeudas` lo dice, también como codeudora (el documento dice si las incluyó). | Alguna obligación tiene saldo (dice cuáles), o hay una **venta a crédito de Comercial en reproceso** para la persona (dice la factura): esa venta todavía puede convertirse en un crédito. |
| Certificado de saldo | `CS-000001` | La liquidación del crédito a una fecha de corte (hoy si no se elige otra) con la información hasta el instante de emitir, por concepto y con lo que cuesta cancelar. Si la fecha es futura, la liquidación se proyecta sin pagos y el documento dice **hasta cuándo vale**. | El crédito no tenía saldo a esa fecha o no se ha desembolsado. |

Cada clase lleva su propio consecutivo. El documento guarda una **copia** del nombre y la identificación del titular tal como
estaban al emitir: si después se corrige la persona en el maestro, el documento no cambia.

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
  motivo dice «Se reversó el pago {número} del crédito {número}.» y la operación causa es el reverso. La vista previa del
  reverso lista los documentos que va a anular y la respuesta los devuelve. Lo emitido **antes** del pago reversado (por ejemplo,
  el plan de pagos que se imprimió al desembolsar) no se toca.

## 8. Configuración

1. **Firmante** (obligatorio): en Cartera → Parámetros, `Documentos.Firmante.Nombre` y `Documentos.Firmante.Cargo`, con
   vigencia desde una fecha y motivo. Sin ellos no se emite nada (`Lending.Document.SignerMissing`). El documento guarda el
   nombre y el cargo vigentes al emitir.
2. **Codeudas en el paz y salvo general**: `Documentos.PazYSalvoIncluyeCodeudas` (por defecto no).
3. **Logotipo e imagen de la firma** (opcionales; pregunta I6 de las decisiones transversales, rige la propuesta por defecto
   mientras el dueño no responda): en Cartera → Parámetros → pestaña **«Documentos»**, «Subir logotipo» y «Subir imagen de la
   firma», en JPEG o PNG. Subir otra **reemplaza** la vigente (la emisión toma siempre la última); la anterior se puede borrar
   desde la lista. Un documento ya emitido conserva su PDF con la imagen que tenía. Sin imágenes, el PDF lleva el membrete con
   el nombre y el NIT de la cooperativa y la línea de firma con el nombre y el cargo. Ver exige `Lending.Documents.Read`;
   subir y borrar, `Lending.Parameters.Manage`. La firma digital certificada queda fuera de esta entrega.
4. Fuera de producción todo PDF lleva **«AMBIENTE DE PRUEBAS – SIN VALIDEZ»** (como leyenda y marca de agua) y el documento
   queda marcado así para siempre.

## 9. Lo que llega después

- **K5**: el extracto del crédito por período y el certificado tributario del año, y la vista de informe «documentos emitidos»
  del centro de informes de Cartera.
- La firma digital certificada del PDF no está en el alcance de la feature.
