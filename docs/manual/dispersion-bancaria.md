# Dispersión bancaria — archivos planos de pago por banco

Feature 010, entrega N4 (2026-09-21). Cómo la nómina paga por archivo plano en lugar de marcar
empleado por empleado, y cómo se carga la estructura de cada banco **como dato**, sin tocar el
programa. Complementa `liquidaciones-especiales.md` (las relaciones de pago de donde sale el
archivo) y `docs/operaciones/nomina-primer-periodo.md` §2 (los prerrequisitos por cooperativa).

## 1. La idea en un párrafo

Cada banco publica la estructura del archivo que su portal acepta (ancho fijo o delimitado,
codificación, campos de cabecera, detalle y totales). Esa estructura se guarda en
**Maestros › Formatos bancarios** ligada al banco, con vigencia, y un solo motor (`FlatFileWriter`)
la ejecuta sobre la relación de pago de cualquier liquidación aprobada. El archivo generado se
guarda tal cual (con su huella SHA-256), se carga en el portal del banco y, cuando el banco lo
recibe, **«Marcar enviado»** deja pagados a todos los del archivo en una sola acción, con la misma
referencia. Los formatos viven en **Core** (`COR_BankFileFormats`), no en nómina, porque la
consignación de cesantías por fondo ya los usa (`SeveranceDeposit`) y tesorería y contabilidad
pagarán a proveedores con el mismo motor (`SupplierPayments`, reservado).

## 2. Antes de la primera vez (por cooperativa)

| Qué | Dónde | Por qué |
|---|---|---|
| **Código de transferencia (ACH)** de cada banco destino | Maestros › Bancos, campo «Código de transferencia (ACH)» (`COR_Banks.TransferCode`) | Es lo que el archivo escribe como banco destino del empleado (`PayeeBankCode`). SOLIDO lo guardaba en `codtras` y lo usaba en su plano de Colmena; es el mismo dato (D-10). Sin él, el empleado queda en **pendientes** con motivo `BankCodeMissing`. |
| **Cuenta bancaria del plan** del banco pagador | Contabilidad › Plan de cuentas: la auxiliar bajo `111005` con banco y número de cuenta | Es la **cuenta origen** del archivo (`SourceAccountNumber`, `SourceBankCode`). Un formato que la escribe la exige (`SourceAccountRequired`), y la cuenta debe ser del banco del formato (`SourceAccountBankMismatch`). |
| **Empresa** (NIT, dígito, razón social) | Maestros › Empresas | Van en la cabecera (`CompanyNit`, `CompanyNitDv`, `CompanyName`); sin empresa, `CompanyMissing`. |
| **Ficha del empleado**: banco de dispersión, tipo y número de cuenta | Nómina › Empleados, pestaña Banca | Sin los tres, el empleado no va al archivo (`NoBankAccount`) y se paga a mano. |
| **Formato vigente** del banco pagador, o uno genérico | Maestros › Formatos bancarios | `CSV-GENERICO` viene sembrado (delimitado, activo, para cualquier banco); el del banco propio se carga con la estructura que publique (§4). |

## 3. Generar, descargar, marcar enviado

1. En la liquidación aprobada (Nómina › Liquidación, Prima, Cesantías anuales, Vacaciones o
   Liquidación definitiva), en la relación de pago: **«Archivo de dispersión»**
   (`Payroll.Disbursement.Generate`). El diálogo pide cuenta origen, formato (propone el vigente
   del banco de la cuenta a la fecha de pago, o el genérico), fecha de pago y referencia del lote.
2. **Vista previa** (`POST /api/payroll/disbursements/preview`): las primeras líneas tal como
   saldrán y la lista de quién quedaría en **pendientes** con el motivo. No guarda nada; sirve para
   corregir fichas antes de generar.
3. **Generar** (`POST /api/payroll/disbursements`): el archivo queda en Nómina › Dispersión bancaria
   con estado `Generated`, sus líneas (texto exacto de cada registro), los pendientes, el total y
   la huella. Un empleado va a lo sumo a un archivo no anulado de la misma corrida (`AlreadySent`);
   el ya pagado no vuelve a ir (`AlreadyPaid`); el neto cero tampoco (`ZeroNet`).
4. **Descargar** (`GET …/{id}/file`): el archivo con la codificación y el fin de línea del formato
   (`Content-Type: text/plain; charset=us-ascii`, por ejemplo). Cargarlo en el portal del banco.
5. **Marcar enviado** (`POST …/{id}/mark-sent`, `Payroll.Disbursement.MarkSent`) con la fecha de
   envío, la referencia que devolvió el banco y, si difiere, la fecha de pago: todos los del archivo
   quedan pagados por transferencia (`PayrollPayment` con `BankDisbursementFileId`, medio `Transfer`,
   la misma `Reference`) en **una sola transacción**; la corrida ya no se reversa
   (`Payroll.PaymentBlocksReversal`). Quien ya tenía marca vigente se deja como estaba y se avisa
   (`PaymentsAlreadyMarked`).
6. **Pendientes**: se corrige la ficha y se genera otro archivo (sólo entran los que no fueron), o
   se marcan pagados a mano en la relación de pago, como hasta ahora.
7. **Anular** (`POST …/{id}/cancel`, con motivo): sólo un archivo `Generated`; sus empleados vuelven
   a poder ir a otro archivo. Un archivo `Sent` no se anula: cada marca de pago se retira una a una
   desde la relación de pago (`…/payments/{employeeId}/revert`), y el archivo queda como constancia.

El centro de reportes tiene la vista `dispersion` (`GET /api/reports/payroll/dispersion?fileId=`)
con las líneas y los pendientes, exportable a Excel, PDF y Word como las demás.

## 4. Cargar el formato de un banco (Maestros › Formatos bancarios)

Permiso `Core.BankFileFormats.Manage`. Dos maneras: campo a campo en la pantalla, o pegando el JSON
completo en la pestaña **JSON** (`POST /api/core/bank-file-formats`). La definición es la de
`specs/010-nomina-prestaciones-pila-dian/contracts/archivos.md` §2; lo esencial:

```json
{ "code": "AVVILLAS-PAGOS", "name": "AV Villas Empresas", "bankPublicId": "…", "scope": "PayrollDisbursement",
  "validFrom": "2026-12-01", "kind": "FixedWidth", "encoding": "us-ascii", "lineEnding": "CRLF",
  "uppercase": true, "stripAccents": true, "amountFormat": "ImplicitCents",
  "fileName": "PAGO{PaymentDate:yyyyMMdd}.txt",
  "records": {
    "header":  { "enabled": true, "fields": [ { "order": 1, "name": "Tipo", "source": "Constant", "value": "1", "length": 1 }, … ] },
    "detail":  { "enabled": true, "fields": [ { "order": 3, "name": "Documento", "source": "PayeeDocument", "length": 15, "align": "Right", "pad": "0", "required": true }, … ] },
    "trailer": { "enabled": true, "fields": [ { "order": 2, "name": "Registros", "source": "LineCount", "length": 6, "align": "Right", "pad": "0" }, … ] } } }
```

- **Orígenes** (`GET /api/core/bank-file-formats/sources` los lista con el registro donde caben):
  constantes y blancos; de la empresa (`CompanyNit`, `CompanyNitDv`, `CompanyName`); de la cuenta
  origen (`SourceAccountNumber`, `SourceAccountType`, `SourceBankCode`, `SourceAgreementCode`); del
  lote (`PaymentDate`, `GenerationDate`, `GenerationTime`, `Sequence`, `BatchReference`); del
  beneficiario, sólo en el detalle (`LineNumber`, `PayeeDocumentType`, `PayeeDocument`,
  `PayeeFullName`, `PayeeFirstNames`, `PayeeLastNames`, `PayeeBankCode`, `PayeeAccountType`,
  `PayeeAccountNumber`, `Amount`, `Concept`, `PayeeEmail`); de totales, sólo en cabecera o totales
  (`LineCount`, `TotalAmount`); y los de cesantías (`FundNit`, `FundPilaCode`, `SeveranceDays`,
  `SeveranceBaseSalary`, `PayeeHireDate`, `Year`). Los nombres del contrato original
  (`EmployeeDocument`, `EmployeeBankCode`, `NetAmount`, `PaymentConcept`…) siguen valiendo como
  sinónimos.
- **Por campo**: `length` (obligatorio en ancho fijo), `align` (`Left`/`Right`), `pad` (un carácter),
  `type` (`Text`, `Integer`, `Amount`, `Date`), `format` (fechas, `yyyyMMdd`), `map` (traduce el
  valor del ERP al del banco: `{ "1": "S", "2": "D" }` para tipo de cuenta, `{ "CC": "1", "CE": "2" }`
  para tipo de documento), `required` (vacío → el empleado va a pendientes con `RequiredFieldEmpty`) y
  `truncate` (si no, un valor más largo que su posición responde `LineTooLong` con línea y campo).
- **Montos**: `Integer` (pesos sin decimales), `ImplicitCents` (centavos implícitos, `1750905.00` →
  `175090500`) o `Point2` (`1750905.00`).
- **Vista previa** del formato: escribe las primeras líneas con una liquidación aprobada real sin
  guardar nada (`POST /api/payroll/disbursements/preview` con `definition`).
- **Vigencia y versiones**: dos formatos activos del mismo banco y ámbito no se cruzan en el tiempo
  (`Core.BankFileFormat.Overlaps`; los genéricos —sin banco— tampoco entre sí). Un formato que **ya
  generó archivos no cambia de estructura** (`Core.BankFileFormat.InUse`: sólo nombre, fin de
  vigencia, notas y estado); se cierra su vigencia y se crea la versión nueva con «Copiar como
  formato nuevo». Los archivos anteriores conservan la suya.
- El **AV Villas** real: el seeder deja `AVVILLAS-1` inactivo y sin campos sólo si la cooperativa
  tiene un banco con «VILLAS» en el nombre; hay que cargar la estructura que publique el banco
  (T147, dato del dueño) y activarla, o crear `AVVILLAS-PAGOS` desde cero.

## 5. Qué preguntar al banco

Ancho fijo o delimitado (y el separador); codificación (ASCII, UTF-8, Windows-1252) y fin de
línea; si lleva cabecera y totales y qué campos; tipo de identificación y su codificación; código
del banco destino (ACH de 4 dígitos o propio); tipo de cuenta (ahorros/corriente) y su
codificación; formato del valor (decimales, centavos implícitos); nombre exigido del archivo; y si
exige cuenta origen, convenio o código de empresa en la cabecera.

## 6. Reglas que no se negocian

- **Un solo motor** (`Application/Common/BankFiles/FlatFileWriter`) y **un solo lugar** donde vive
  el formato (`COR_BankFileFormats`). Nómina no sabe escribir archivos: le entrega al motor las
  líneas (`PayrollDisbursementLines`) y el contexto (empresa, cuenta origen, lote). Otro módulo
  hace lo mismo con su propia fuente de líneas y su `scope`.
- El archivo generado es **inmutable** (Principio XI): `PAY_BankDisbursementFiles` y sus líneas no
  se editan ni se borran; se anulan con motivo. Lo que se descarga es el adjunto guardado
  (`COR_Attachments`, `OwnerEntityType = BankDisbursementFile`, no borrable), no una regeneración,
  y la huella lo demuestra.
- Marcar enviado y marcar pagados son **una transacción**: no existe archivo enviado con pagos a
  medias.
- Sin permiso, 404 `Generic.NotFound`, como en todo el ERP.

## 7. Permisos y códigos

`Payroll.Disbursement.View` (ver, descargar, reporte), `.Generate` (vista previa, generar, anular),
`.MarkSent`; `Core.BankFileFormats.View` / `.Manage`. Códigos de error y de exclusión en
`contracts/api.md` §9 y en `Application/Payroll/Dispersion/DisbursementDtos.cs`
(`DisbursementErrors`) y `Application/Core/BankFiles/BankFileFormatCommands.cs`.
