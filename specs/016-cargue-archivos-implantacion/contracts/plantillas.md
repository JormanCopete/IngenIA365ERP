# Plantillas — 016

> **D10 (2026-10-30)**: §1 (lectura por perfil) y §4 (equivalencias y cifras) las aplica el kit `tools/implantacion/`, no el ERP; sus
> reglas y códigos de error se conservan tal cual. En el ERP quedan §2, §3 y §5.

Todas siguen la mecánica común de la 012 (`contracts/plantillas.md` §0 de la 012): encabezados en la fila 1, `review` sin guardar y
`apply` todo o nada, `ErrorDeFila { Sheet, Row, Column, Code, Message }`, llave que crea, actualiza o deja igual y nunca borra,
idempotencia, evento `Import.Reviewed` / `Import.Applied` con SHA-256. Las columnas van en `camelCase` español, salvo la de
invitaciones, que usa los encabezados del formato de la cooperativa (§5).

## 1. Lectura por perfil (aplica a toda plantilla destino)

### 1.1 Destinos que aceptan perfil (`CatalogoDeDestinosDePerfil`)

`core.people`, `core.associates`, `accounting.accounts`, `accounting.opening`, cada parte de `lending.migration`, `lending.related-parties`,
`lending.contribution-balances`, cada parte de `deposits.migration`, `implementation.equivalences`, `implementation.reference-figures`.
Las de inventario de la 012 quedan **fuera** (research D9: no hay archivo del sistema anterior para ellas; se agregan como una
fila del catálogo si aparece).

### 1.2 Orden de la lectura de una línea

1. decodificar con la codificación del perfil;
2. saltar: las `skipFirstLines`, las vacías si `skipEmptyLines`, las que cumplen un `skipPattern`;
3. partir: por separador (respetando el calificador) o por posiciones; en ancho fijo, una línea de largo distinto a `lineLength` es
   error de la línea (`Import.Profile.LineLength`, con la real y la esperada) y no se parte;
4. por mapeo: tomar el valor, recortar, quitar relleno, vacío → `defaultWhenEmpty`, convertir fecha o número al texto canónico,
   traducir con la equivalencia;
5. entregar la fila a la plantilla destino con su **número de línea original**.

### 1.3 Texto canónico

Fecha `yyyy-MM-dd`; número con punto decimal y sin miles, signo adelante; sí/no como `si`/`no`; el resto, el texto recortado.

### 1.4 Códigos de error

| Código | Cuándo | Columna / origen |
|---|---|---|
| `Import.Profile.LineLength` | línea de ancho fijo de otro largo | línea, «largo 298, se esperaban 320» |
| `Import.Profile.FieldOutOfLine` | la posición se sale de la línea | línea y posición |
| `Import.Profile.MissingColumn` | delimitado: la columna de origen no existe en la línea | línea y columna de origen |
| `Import.Profile.InvalidDate` | fecha imposible o que no encaja | columna destino y origen |
| `Import.Profile.InvalidNumber` | número que no encaja (nunca cero) | ídem |
| `Import.Profile.UnmatchedSkipRow` | una fila de totales que no cumple el patrón | línea |
| `Import.Profile.EncodingSuspect` | aviso: caracteres no reconocidos | líneas |
| `Import.Equivalence.Missing` | código sin equivalencia | «Tabla CIUDADES, código anterior 05001» |
| `Import.Equivalence.OneToMany` | «una en varias»: hay que decidir | ídem, con los destinos posibles |
| `Import.Equivalence.NotMigrated` | la equivalencia dice «no se migra» | ídem |

## 2. `core.people` — personas y terceros

Una hoja `Datos`. Llave: usuario derivado de (`tipoDocumento`, `documento`) por la regla de la 017.

| Columna | Tipo | Oblig. | Regla |
|---|---|---|---|
| `tipoDocumento` | Código | sí | catálogo de tipos de la persona (`CC`, `CE`, `NIT`, `PT`, …) |
| `documento` | Texto(20) | sí | sin dígito de verificación |
| `digitoVerificacion` | Entero | NIT | el del alta |
| `naturaleza` | Enumeración | sí | `natural` / `juridica` |
| `primerNombre`, `otrosNombres`, `primerApellido`, `segundoApellido` | Texto | natural | |
| `razonSocial` | Texto | jurídica | |
| `correo`, `telefono`, `celular`, `direccion` | Texto | | |
| `ciudad` | Código | | DIVIPOLA (o por equivalencia) |
| `fechaNacimiento`, `genero`, `estadoCivil`, `nivelEducativo` | según el alta | | los códigos del formulario de persona o su nombre |
| `lugarExpedicion`, `fechaExpedicion` | Texto, Fecha | | |
| `telefono2` | Texto | | |
| `responsableIva`, `autorretenedor`, `agenteRetencionIva`, `regimenSimple`, `declaranteRenta`, `obligadoFacturar`, `obligadoContabilidad`, `granContribuyente`, `exentoRetencion`, `exentoReteIca` | SíNoIndiferente | | perfil tributario de la persona (012); vacío = no cambia |
| `ciiu` | Texto | | 4 a 6 dígitos |
| `esProveedor`, `esCliente` | SíNoIndiferente | | vacío = no cambia |

Las cuentas bancarias van por `plataforma.cuentas-bancarias-de-personas` (015), no por esta plantilla (D10). El dígito de verificación
de un NIT que no corresponde al número es **aviso**, no error: el alta unitaria no lo valida.

Errores: los del alta unitaria (`Person.TaxIdDeleted`, `Person.TaxIdDuplicate`, `Person.CheckDigitInvalid`, …), `Import.Key.Duplicate`
dentro del archivo. Resultado por fila: `Create` / `Update` (con `campos` antes/después) / `Unchanged`.

## 3. `core.associates` — asociados

Una hoja `Datos`. Llave: usuario derivado de la persona viva.

| Columna | Tipo | Oblig. | Regla |
|---|---|---|---|
| `tipoDocumento`, `documento` | Código, Texto | sí | persona viva; si no, `Import.Cell.NotFound` («esta plantilla no crea personas») |
| `fechaAfiliacion` | Fecha | sí | no futura (regla del alta, en `AssociateInputValidator`) |
| `oficina` | Sucursal | sí | código o nombre de la agencia; el kit la traduce por equivalencia `Office` |
| `estado` | Enumeración | sí | `vigente` (o `activo`) / `retirado` |
| `grupo` | Código | | código o nombre, `COR_AssociateGroups` activo |
| `clase` | Enumeración | | `asociado`, `afiliado`, `empleado` (`AssociateClass` = `ASOC`, `AFIL`, `EMPL`); las tres dan fila de asociado |
| `fechaRetiro`, `motivoRetiro` | Fecha, Código | si retirado | sólo al **crear** (research R9, D8); motivo por código o nombre de `COR_WithdrawalReasons`; fecha no futura |
| `fechaReingreso` | Fecha | | sólo al **crear**; no futura. En un retirado debe ser anterior al retiro; un vigente con `fechaRetiro` la exige igual o posterior (`Associate.StatusDatesMismatch`) |
| `periodicidadDescuento` | Enumeración | | `mensual`, `quincenal` (`DeductionPeriod` = `M`, `Q`) |
| `formaDescuento` | Enumeración | | `caja`, `nomina` (`DeductionType` = `C`, `N`) |
| `tasaAporte`, `aporteComprometido` | Cantidad, Monto | | en puntos y en pesos, no negativos |
| `comite`, `categoria` | Código, Texto(2) | | datos de afiliación del alta |

Un vigente que el archivo marca retirado, o un retirado que marca vigente, es `Associate.WithdrawalOwnedByLending`. Retiro y reingreso que
difieren de lo guardado dan el aviso `Associate.OnlyOnCreate` y no se aplican.

## 4. Equivalencias y cifras

- `implementation.equivalences`: `codigoAnterior`, `codigoNuevo`, `clase` (`una-a-una`, `varias-en-una`, `una-en-varias`, `no-se-migra`),
  `nota`. Llave (`codigoAnterior`, `codigoNuevo`).
- `implementation.equivalences-accounting`: hoja «Equivalencias» del formato de la contadora (research R7); columnas leídas:
  «Código actual», «Cuenta CUIF (6 dígitos)», «Auxiliar nuevo (código)», «Tipo de equivalencia»; las demás se ignoran.
- `implementation.reference-figures`: `paso`, `total`, `valor`, `fuente`. Llave (`paso`, `total`) del corte de la ruta.

## 5. `security.user-invitations` — invitaciones masivas (US7)

Dos hojas. Encabezados tal como el formato de la cooperativa; el asterisco final se ignora.

### Hoja «Usuarios nuevos»

| Encabezado | Oblig. | Regla (la de la 017 salvo lo dicho) |
|---|---|---|
| Tipo de documento | sí | entre los aceptados por la cooperativa |
| Número de documento | sí | formato y longitud del tipo; se normaliza |
| Confirmar número de documento | sí | igual al número (`Identity.Document.Mismatch`) |
| Primer nombre | sí | |
| Otros nombres | | |
| Primer apellido | sí | |
| Segundo apellido | | |
| Correo personal | sí | único en el archivo; `Invitation.EmailTaken` si es de otra identidad |
| Celular | sí | la regla del formulario de la 017 |
| Rol | sí | «Administrador de la cooperativa», «Operador», «Auditor», «Solo lectura» o el nombre del sistema |
| Código de cargo | sí | catálogo vivo o hoja «Cargos» |
| Oficina / agencia | | se muestra; no se guarda (D2) |
| Observaciones | | se conserva en el resultado |

Llave: (tipo, número normalizado). Resultado: `Create` (sale invitación) o `Unchanged` con aviso `Invitation.AlreadyMember`.

### Hoja «Cargos»

| Encabezado | Oblig. | Regla |
|---|---|---|
| Código | sí | `CodigoDeCatalogo`: hasta 10, mayúsculas y números, sin espacios, único |
| Nombre del cargo | sí | hasta 80 |

Llave: código. Crea o actualiza el nombre; nunca borra. Las columnas sin encabezado (la nota de la columna D) se ignoran.

### Hojas «Instrucciones» y «Listas»

Se ignoran (no son hojas de la definición).
