# Cotejo con cifras de referencia

Para la implantación: comparar las cifras del ERP, a un corte, con las que trae la cooperativa en un archivo (el balance, la cartera, los
aportes y ahorros, los asociados). El ERP calcula lo suyo en el momento y dice, partida por partida, qué **cuadra**, qué tiene **diferencia**
y qué está **sólo en el ERP** o **sólo en la referencia**.

Menú **Implantación** (sólo lo ven quienes tienen alguno de sus permisos):

| Opción | Ruta | Permiso | Qué compara |
|---|---|---|---|
| Balance por cuenta | `/implantacion/cotejo/cuentas` | `Implantacion.Cotejo.Contabilidad` | El saldo de cada cuenta, del nivel que traiga el archivo (cada código suma su rama). |
| Saldos por tercero | `/implantacion/cotejo/terceros` | `Implantacion.Cotejo.Contabilidad` | Cuenta × tercero. Las filas repetidas de la misma cuenta y tercero se suman. |
| Cartera por crédito | `/implantacion/cotejo/creditos` | `Implantacion.Cotejo.Cartera` | Capital, intereses, otros cargos, días de mora, calificación y documento del deudor. |
| Aportes y ahorros | `/implantacion/cotejo/cuentas-de-asociados` | `Implantacion.Cotejo.Captaciones` | El saldo de cada cuenta de aportes, ahorro o CDAT y el documento del titular. |
| Asociados | `/implantacion/cotejo/asociados` | `Implantacion.Cotejo.Asociados` | Que exista y su estado (activo o retirado) al corte. |

El inventario se coteja aparte, en **Inventario › Cifras de referencia**.

Los permisos los siembra la API al arrancar. El administrador de la cooperativa los tiene. A los demás roles se asignan a quien hace la
implantación.

## Cómo se usa

1. **Plantilla Excel**: la hoja «Datos» trae los encabezados en la fila 1; la hoja «Instrucciones» explica cada columna. Sólo unas columnas son
   obligatorias; una columna opcional que el archivo no traiga no se compara. Los encabezados se reconocen también con nombres parecidos
   («Código», «Saldo final», «Cédula»…).
2. Llene el archivo con las cifras de referencia **al corte**, elija el corte y pulse **Cotejar**.
3. Sale el **resumen** por grupo (clase de cuenta, línea, estado): cuántas partidas cuadran, cuántas no y las sumas de cada lado. Un clic en
   un grupo abre su **detalle**; **Detalle completo** muestra todo, y **Sólo lo que no cuadra** deja las diferencias.
4. **Exportar a Excel** baja lo que se ve en pantalla (en el detalle, una hoja por grupo además de la completa).

**Nada se guarda.** Cada cotejo vuelve a leer el archivo y a calcular, y deja en la auditoría el evento `Implantacion.Cotejo.Realizado`, con el
nombre y la huella SHA-256 del archivo, el corte y cuántas partidas quedaron en cada estado. Para volver a cotejar, se vuelve a subir.

Si el archivo tiene errores (un importe ilegible, una llave repetida, una columna obligatoria vacía), no se coteja nada: la pantalla los lista
con su fila y columna.

## Reglas

- **Importes**: con o sin separador de miles, con punto o coma decimal, con «$», negativos con «-» o entre paréntesis. Un separador seguido de
  exactamente tres dígitos se toma como miles («150.000» = ciento cincuenta mil). Vacío = cero.
- **Llaves**: números de crédito y de cuenta sin ceros a la izquierda, espacios ni guiones. Documentos además sin puntos ni dígito de verificación.
- **Diferencia** = ERP − referencia. Una partida en cero de un lado y ausente del otro **cuadra**.
- **Tolerancia**: opcional, en pesos (hasta 1.000). Una diferencia de hasta ese valor en un importe se toma como cuadrada.

### Contabilidad

- El libro contabilizado al corte, con el alcance de sucursal de quien coteja. La apertura cuenta como saldo inicial.
- El comprobante de cierre del ejercicio del corte **no** entra: los resultados se ven como antes del cierre. Los cierres de años anteriores sí.
- El signo va por la naturaleza de la cuenta (positivo con su naturaleza). Si el archivo trae débito menos crédito, marque
  «El archivo trae débito menos crédito».
- En el balance por cuenta, del ERP se listan como «sólo en el ERP» las cuentas con saldo **de los mismos niveles** (largos de código) que
  trae el archivo. Un código que no existe en el plan queda «sólo en la referencia», con la observación.
- En los saldos por tercero entran sólo las cuentas que trae el archivo. Conviene cotejar primero el balance por cuenta.

### Cartera

- La cartera viva al corte con el **mismo estado que el estado de cuenta**. Si un crédito no se puede recalcular al corte, sale con su posición
  diaria y la observación lo dice.
- «Interés corriente» e «Interés de mora» incluyen el contingente. «Otros» = seguro + administración + otros cargos.
- La calificación se compara por la letra (A a E).
- El número del crédito es el de la carga, que conserva el de origen.

### Aportes y ahorros

- El saldo es la suma del libro de cuentas con fecha valor hasta el corte. En captación es capital + interés por pagar + bonificación − cargos
  por cobrar; en aportes, la suma de sus cuatro clases.
- La cuenta se empareja por su **número de origen** (el de la carga) o, si no lo tiene, por el del ERP. Las dos columnas se muestran.

### Asociados

- Retirado al corte = estado de retiro con fecha de retiro hasta el corte. Quien ingresó después del corte no entra.
- El estado de la referencia se lee como Activo (Activo, A, Vigente, Hábil) o Retirado (Retirado, R, Inactivo, I, Desvinculado). Lo que no se
  reconoce difiere.
- Si el archivo no trae retirados, los retirados del ERP no se listan como «sólo en el ERP».

## Rutas de la API

- `GET /api/implantacion/cotejo/{opcion}/template.xlsx`
- `POST /api/implantacion/cotejo/{opcion}?asOf=yyyy-MM-dd&level=resumen|detalle&group=&onlyDifferences=&debitMinusCredit=&tolerance=&format=json|xlsx|pdf|docx`.
  Lleva el archivo en `multipart/form-data`, en el campo `archivo` (hasta 10 MB y 200.000 filas). Los errores del archivo responden 422
  `Implantacion.Cotejo.ArchivoInvalido` con `data.errors[] { row, column, code, message }`.

`{opcion}` ∈ `cuentas`, `terceros`, `creditos`, `cuentas-de-asociados`, `asociados`. Sin el permiso de la opción, 404.
