# Contratos de archivos: PILA, dispersión bancaria y consignación de cesantías

**Feature**: 010 | **Date**: 2026-09-20 | **Base**: research R9 (PILA), R11 (dispersión), R2/FR-012
(cesantías) | **Hermano**: `api.md` §3.2, §7, §9

Regla común a los tres: **el layout es dato, no código**. Cada formato vive en un JSON (embebido y
versionado para la PILA; fila de `PAY_BankFileFormats` para bancos y fondos) con `validFrom/validTo`;
un archivo generado guarda el código del layout con que se escribió y no cambia cuando llega una
versión nueva (Principio XI). Los escritores (`PilaWriter`, `FlatFileWriter`) sólo saben poner un
valor en una posición con una alineación y un relleno; lo que va en cada campo lo dice el layout.

## 1. Planilla PILA — archivo tipo 2 de la Resolución 2388 de 2016 (Anexo Técnico 2, v30 del 24-07-2026)

### 1.1 Archivo

- Texto plano ASCII, **ancho fijo**, una línea por registro, fin de línea CRLF, extensión `.txt`,
  mayúsculas, sin tildes ni eñes (se transliteran: Ñ → N).
- Un **registro tipo 1** (encabezado del aportante) y N **registros tipo 2** (uno por cotizante y
  por cada novedad con IBC distinto, R9).
- Campos **N** (numéricos): alineados a la derecha y rellenos con ceros; **A** (alfanuméricos):
  alineados a la izquierda y rellenos con espacios. Fechas `AAAA-MM-DD`; períodos `AAAA-MM`.
- Tarifas con 7 posiciones y decimales con punto (`0.04000`, `0.16000`, `0.00522`); el anexo **no
  fija** cuántos decimales: se cargan como los muestra una planilla ya pagada de la cooperativa
  (falta del dueño 6). Valores en pesos sin decimales; **IBC al peso superior** y **aportes al
  múltiplo de 100 superior** (parámetros `PILA_IBC_REDONDEO` y `PILA_APORTE_REDONDEO_MULTIPLO`).
- Nombre que da el ERP: `PILA_<NIT>_<AAAA-MM>_v<version>.txt` (el operador no exige nombre).
- Aportes en Línea acepta este archivo en Liquidaciones → Adicionar liquidación → Cargar archivo →
  Validar, y clasifica en Error (bloquea), Alerta (deja cargar) y Posible corrección (autocorrige).

### 1.2 Esquema del layout (`Application/Payroll/Pila/Layouts/at2-v30-2026-07-24.json`)

```jsonc
{
  "code": "AT2-v30", "version": "30", "source": "Res. 2388/2016, AT2 consolidado 24-07-2026 (hasta Res. 1529/2026)",
  "validFrom": "2026-08-01", "validTo": null,
  "encoding": "us-ascii", "lineEnding": "CRLF",
  "records": [
    { "type": 1, "length": 359, "fields": [ /* 22 */ ] },
    { "type": 2, "length": 693, "fields": [ /* 98 */ ] }
  ]
}
// field
{ "number": 40, "name": "Salario básico", "start": 192, "length": 9, "kind": "N",
  "required": true,
  "source": "Calculation" | "Profile" | "Settings" | "Constant" | "Blank",
  "path": "Ibc.BasicSalary",          // qué propiedad del modelo puro llena el campo
  "constant": null,
  "format": "Integer" | "Rate7" | "Date" | "Period" | "Flag" | "Text",
  "rules": ["≥ 0", "= 0 cuando SLN"],  // texto, para la explicación por campo
  "verified": true                     // false = posición/longitud por cotejar con el anexo
}
```

`PilaBuilder` (motor puro) produce por línea un `PilaLine` tipado y `PilaWriter` lo proyecta al
layout; la explicación por campo (`api.md` §7) sale del `source`/`path` y del parámetro o política
que se usó. Un layout con `verified = false` en algún campo **no se marca vigente** hasta cotejarlo.

### 1.3 Registro tipo 1 — encabezado (22 campos, 359 posiciones)

> **Verificación**: posiciones reconstruidas de la Res. 2388/2016. La suma de las longitudes de
> abajo da **358**, y el anexo v30 (numeral 2.1.1.1, págs. 26-30) declara **359**: hay un campo
> con una posición de diferencia que se debe cotejar contra el anexo antes de fijar el JSON. Todos
> los campos de este registro van con `verified = false` hasta entonces.

| # | Campo | Long. | Tipo | Pos. | Origen | Valor / regla |
|---|---|---|---|---|---|---|
| 1 | Tipo de registro | 1 | N | 1 | Constant | `1` |
| 2 | Modalidad de la planilla | 1 | N | 2 | Constant | `1` (electrónica) |
| 3 | Secuencia | 4 | N | 3-6 | Constant | `0001` |
| 4 | Nombre o razón social del aportante | 200 | A | 7-206 | Settings | `Company.Name` |
| 5 | Tipo documento del aportante | 2 | A | 207-208 | Settings | `NI` |
| 6 | Número de identificación del aportante | 16 | A | 209-224 | Settings | NIT sin DV |
| 7 | Dígito de verificación | 1 | N | 225 | Settings | DV |
| 8 | Tipo de planilla | 1 | A | 226 | Constant | `E` (empleados; N y A fuera de alcance) |
| 9 | Número de la planilla asociada | 10 | N | 227-236 | Blank | sólo planillas N/A |
| 10 | Fecha de pago de la planilla asociada | 10 | A | 237-246 | Blank | sólo N/A |
| 11 | Forma de presentación | 1 | A | 247 | Settings | `U` única / `S` sucursal |
| 12 | Código de la sucursal | 10 | A | 248-257 | Settings | si `S` |
| 13 | Nombre de la sucursal | 40 | A | 258-297 | Settings | si `S` |
| 14 | Código de la ARL del aportante | 6 | A | 298-303 | Settings | `PilaSettings.ArlPilaCode` |
| 15 | Período de pago sistemas distintos a salud | 7 | A | 304-310 | Calculation | `AAAA-MM` del mes liquidado |
| 16 | Período de pago sistema de salud | 7 | A | 311-317 | Calculation | mes **siguiente** al campo 15 en planilla E (R9) |
| 17 | Número de radicación / planilla integrada | 10 | N | 318-327 | Blank | lo asigna el operador |
| 18 | Fecha de pago | 10 | A | 328-337 | Blank | la pone el operador al pagar |
| 19 | Número total de empleados | 5 | N | 338-342 | Calculation | cotizantes **únicos** (no líneas) |
| 20 | Valor total de la nómina | 12 | N | 343-354 | Calculation | Σ campo 45 (IBC CCF) de todas las líneas |
| 21 | Tipo de aportante | 2 | N | 355-356 | Settings | `1` empleador |
| 22 | Código del operador de información | 2 | N | 357-358 | Settings | Aportes en Línea (código a confirmar con el operador) |

### 1.4 Registro tipo 2 — liquidación por cotizante (98 campos, 693 posiciones)

> **Verificación**: 97 campos / 686 posiciones hasta 2019 y 98 / 693 con el campo 98 de la Res.
> 2520/2024; la suma de abajo cuadra con ambas cifras. Aun así las posiciones se cotejan campo a
> campo contra el numeral 2.1.2.1 del anexo v30 (págs. 81-102) antes de poner `verified = true`;
> las longitudes de identificación (campo 4) llevan además las reglas de la Res. 1529/2026 (CC ≤ 10,
> TI ≤ 11, CE ≤ 7, PA ≤ 16, PE = 15, PT ≤ 8), exigibles desde el 01-10-2026.

| # | Campo | Long. | Tipo | Pos. | Origen | Valor / regla |
|---|---|---|---|---|---|---|
| 1 | Tipo de registro | 1 | N | 1 | Constant | `2` |
| 2 | Secuencia | 5 | N | 2-6 | Calculation | consecutivo desde `00001` |
| 3 | Tipo de documento del cotizante | 2 | A | 7-8 | Profile | `Person.DocumentType` → CC/CE/TI/PA/PE/PT… |
| 4 | Número de identificación del cotizante | 16 | A | 9-24 | Profile | `Person.TaxId`; longitud según tipo (Res. 1529/2026) |
| 5 | Tipo de cotizante | 2 | N | 25-26 | Profile/Rule | `1` dependiente, `19` aprendiz lectiva (Ley 2466/2025), `51` tiempo parcial; derivado de `EmployeeClass` + etapa o fijado en la ficha |
| 6 | Subtipo de cotizante | 2 | N | 27-28 | Profile | `00`; `1` pensionado activo (tarifa pensión 0) |
| 7 | Extranjero no obligado a cotizar a pensiones | 1 | A | 29 | Profile | `X` o blanco |
| 8 | Colombiano en el exterior | 1 | A | 30 | Profile | `X` o blanco |
| 9 | Código del departamento de la ubicación laboral | 2 | A | 31-32 | Profile | DIVIPOLA; default de la sucursal |
| 10 | Código del municipio de la ubicación laboral | 3 | A | 33-35 | Profile | DIVIPOLA |
| 11 | Primer apellido | 20 | A | 36-55 | Profile | |
| 12 | Segundo apellido | 30 | A | 56-85 | Profile | `Person.SecondLastName` (nuevo) |
| 13 | Primer nombre | 20 | A | 86-105 | Profile | |
| 14 | Segundo nombre | 30 | A | 106-135 | Profile | `Person.MiddleName` (nuevo) |
| 15 | ING — ingreso | 1 | A | 136 | Calculation | `X` si ingresó en el mes (+ fecha campo 80); ING y RET del mismo mes en la **misma** línea |
| 16 | RET — retiro | 1 | A | 137 | Calculation | `X` si se retiró en el mes (+ campo 81) |
| 17 | TDE — traslado desde otra EPS | 1 | A | 138 | Blank | fuera de alcance (se digita en el operador) |
| 18 | TAE — traslado a otra EPS | 1 | A | 139 | Blank | |
| 19 | TDP — traslado desde otra AFP | 1 | A | 140 | Blank | |
| 20 | TAP — traslado a otra AFP | 1 | A | 141 | Blank | |
| 21 | VSP — variación permanente de salario | 1 | A | 142 | Calculation | `X` si hubo cambio de salario en el mes (+ campo 82) |
| 22 | Correcciones | 2 | A | 143-144 | Blank | sólo planillas de corrección |
| 23 | VST — variación transitoria del salario | 1 | A | 145 | Calculation | `X` si el IBC del mes superó el salario básico por devengos variables |
| 24 | SLN — suspensión temporal / licencia no remunerada | 1 | A | 146 | Calculation | `X` + fechas 83-84; línea aparte con tarifas sólo del empleador y FSP 0 |
| 25 | IGE — incapacidad general | 1 | A | 147 | Calculation | `X` + fechas 85-86; línea aparte; ARL tarifa 0 |
| 26 | LMA — licencia de maternidad o paternidad | 1 | A | 148 | Calculation | `X` + fechas 87-88; ARL tarifa 0 |
| 27 | VAC-LR — vacaciones / licencia remunerada | 1 | A | 149 | Calculation | `X` + fechas 89-90; línea aparte; ARL según política `CotizaArlEnVacaciones` |
| 28 | AVP — aporte voluntario a pensión | 1 | A | 150 | Calculation | `X` si hay aporte voluntario (campos 48-49) |
| 29 | VCT — variación de centros de trabajo | 1 | A | 151 | Blank | + fechas 91-92 si se usa |
| 30 | IRL — días de incapacidad por riesgo laboral | 2 | N | 152-153 | Calculation | días; + fechas 93-94 |
| 31 | Código de la AFP a la que pertenece | 6 | A | 154-159 | Profile | `PensionProvider.PilaCode` (nuevo) |
| 32 | Código de la AFP a la que se traslada | 6 | A | 160-165 | Blank | |
| 33 | Código de la EPS/EOC a la que pertenece | 6 | A | 166-171 | Profile | `HealthInsuranceProvider.PilaCode` |
| 34 | Código de la EPS a la que se traslada | 6 | A | 172-177 | Blank | |
| 35 | Código de la CCF a la que pertenece | 6 | A | 178-183 | Profile | `FamilyCompensationFund.PilaCode` |
| 36 | Días cotizados a pensión | 2 | N | 184-185 | Calculation | suman 30 entre líneas del cotizante salvo ING/RET |
| 37 | Días cotizados a salud | 2 | N | 186-187 | Calculation | idem |
| 38 | Días cotizados a riesgos laborales | 2 | N | 188-189 | Calculation | idem |
| 39 | Días cotizados a CCF | 2 | N | 190-191 | Calculation | idem |
| 40 | Salario básico | 9 | N | 192-200 | Calculation | salario mensual vigente; integral: el 100 % |
| 41 | Salario integral | 1 | A | 201 | Profile | `X` si `SalaryType` integral |
| 42 | IBC pensión | 9 | N | 202-210 | Calculation | ≥ 1 SMMLV proporcional, ≤ 25 SMMLV, integral 70 %, sin auxilio; al peso superior |
| 43 | IBC salud | 9 | N | 211-219 | Calculation | idem |
| 44 | IBC riesgos laborales | 9 | N | 220-228 | Calculation | idem |
| 45 | IBC CCF | 9 | N | 229-237 | Calculation | salario + novedades del mes (alerta si ≠) |
| 46 | Tarifa de aportes a pensión | 7 | N | 238-244 | Calculation | `PENSION_EMPLEADO_PCT + PENSION_EMPLEADOR_PCT` (0.16000); `0` en subtipo 1 |
| 47 | Cotización obligatoria a pensión | 9 | N | 245-253 | Calculation | al múltiplo de 100 superior |
| 48 | Aporte voluntario del afiliado a pensión obligatoria | 9 | N | 254-262 | Calculation | novedad `AVP_EMPLEADO` o 0 |
| 49 | Aporte voluntario del aportante a pensión obligatoria | 9 | N | 263-271 | Calculation | 0 |
| 50 | Total cotización sistema general de pensiones | 9 | N | 272-280 | Calculation | 47 + 48 + 49 |
| 51 | FSP — subcuenta de solidaridad | 9 | N | 281-289 | Calculation | tabla `FSP_TABLA` vigente (Ley 797 hasta 2027-03-31; Ley 2381 desde 2027-04-01 salvo régimen de transición); v30: **los liquida el operador**, diferencia = alerta |
| 52 | FSP — subcuenta de subsistencia | 9 | N | 290-298 | Calculation | idem |
| 53 | Valor no retenido por aportes voluntarios | 9 | N | 299-307 | Calculation | 0 |
| 54 | Tarifa de aportes a salud | 7 | N | 308-314 | Calculation | `0.12500`; exonerado (art. 114-1) → `0.04000` |
| 55 | Cotización obligatoria a salud | 9 | N | 315-323 | Calculation | al múltiplo de 100 superior |
| 56 | Valor UPC adicional | 9 | N | 324-332 | Calculation | 0 |
| 57 | Nº autorización incapacidad enfermedad general | 15 | A | 333-347 | Calculation | de la novedad IGE si trae número |
| 58 | Valor de la incapacidad por enfermedad general | 9 | N | 348-356 | Calculation | 0 (lo cobra el empleador a la EPS por otro canal) |
| 59 | Nº autorización licencia de maternidad/paternidad | 15 | A | 357-371 | Calculation | |
| 60 | Valor de la licencia de maternidad | 9 | N | 372-380 | Calculation | 0 |
| 61 | Tarifa de aportes a riesgos laborales | 9 | N | 381-389 | Calculation | `ARL_CLASE_<n>_PCT` de la fila `PAY_WorkRiskRates` de la ficha (`0.0052200`); 0 en IGE/LMA |
| 62 | Centro de trabajo CT | 9 | N | 390-398 | Profile | `Employee.WorkCenter` (nuevo) |
| 63 | Cotización obligatoria a riesgos laborales | 9 | N | 399-407 | Calculation | al múltiplo de 100 superior |
| 64 | Tarifa de aportes a CCF | 7 | N | 408-414 | Calculation | `CAJA_PCT` (`0.04000`); completa aun exonerado |
| 65 | Valor aporte CCF | 9 | N | 415-423 | Calculation | |
| 66 | Tarifa de aportes SENA | 7 | N | 424-430 | Calculation | `SENA_PCT` (`0.02000`); exonerado → 0 |
| 67 | Valor aporte SENA | 9 | N | 431-439 | Calculation | |
| 68 | Tarifa de aportes ICBF | 7 | N | 440-446 | Calculation | `ICBF_PCT` (`0.03000`); exonerado → 0 |
| 69 | Valor aporte ICBF | 9 | N | 447-455 | Calculation | |
| 70 | Tarifa aportes ESAP | 7 | N | 456-462 | Constant | 0 (sólo entidades públicas) |
| 71 | Valor aporte ESAP | 9 | N | 463-471 | Constant | 0 |
| 72 | Tarifa aporte MEN | 7 | N | 472-478 | Constant | 0 |
| 73 | Valor aporte MEN | 9 | N | 479-487 | Constant | 0 |
| 74 | Tipo de documento del cotizante principal | 2 | A | 488-489 | Blank | sólo beneficiarios/UPC adicional |
| 75 | Número de identificación del cotizante principal | 16 | A | 490-505 | Blank | |
| 76 | Cotizante exonerado de aporte a salud, SENA e ICBF (Ley 1607) | 1 | A | 506 | Calculation | `S`/`N`: política `Exonerada114_1` vigente **y** IBC < `EXONERACION_PARAFISCALES_TOPE_SMMLV`; coherente con el campo 54 |
| 77 | Código de la ARL a la que pertenece | 6 | A | 507-512 | Profile | `WorkRiskProvider.PilaCode` |
| 78 | Clase de riesgo del afiliado | 1 | N | 513 | Profile | `PAY_WorkRiskRates.Code` (1..5) |
| 79 | Indicador de tarifa especial de pensiones | 1 | A | 514 | Profile | alto riesgo (`Employee.HighRiskPension`) |
| 80 | Fecha de ingreso | 10 | A | 515-524 | Calculation | si ING |
| 81 | Fecha de retiro | 10 | A | 525-534 | Calculation | si RET |
| 82 | Fecha de inicio VSP | 10 | A | 535-544 | Calculation | si VSP |
| 83 | Fecha de inicio SLN | 10 | A | 545-554 | Calculation | |
| 84 | Fecha de fin SLN | 10 | A | 555-564 | Calculation | |
| 85 | Fecha de inicio IGE | 10 | A | 565-574 | Calculation | |
| 86 | Fecha de fin IGE | 10 | A | 575-584 | Calculation | |
| 87 | Fecha de inicio LMA | 10 | A | 585-594 | Calculation | |
| 88 | Fecha de fin LMA | 10 | A | 595-604 | Calculation | |
| 89 | Fecha de inicio VAC-LR | 10 | A | 605-614 | Calculation | |
| 90 | Fecha de fin VAC-LR | 10 | A | 615-624 | Calculation | |
| 91 | Fecha de inicio VCT | 10 | A | 625-634 | Blank | |
| 92 | Fecha de fin VCT | 10 | A | 635-644 | Blank | |
| 93 | Fecha de inicio IRL | 10 | A | 645-654 | Calculation | |
| 94 | Fecha de fin IRL | 10 | A | 655-664 | Calculation | |
| 95 | IBC otros parafiscales distintos a CCF | 9 | N | 665-673 | Calculation | base SENA/ICBF (= 45 salvo exonerado) |
| 96 | Número de horas laboradas | 3 | N | 674-676 | Calculation | días × `HORAS_MES`/30 (parámetro; 210 h desde 2026-07-15) |
| 97 | Fecha de radicación en el exterior | 10 | A | 677-686 | Blank | |
| 98 | Código de actividad económica ARL (Decreto 768/2022; Res. 2520/2024) | 7 | A | 687-693 | Profile/Settings | `Employee.EconomicActivityCode` o el de la empresa |

Reglas de cuadre que valida `PilaBuilder` antes de escribir (R9, FR-027): campo 20 = Σ campo 45;
campo 19 = cotizantes únicos; días por subsistema suman 30 por cotizante (28/30/31 → 30) salvo
ING/RET; campo 76 coherente con 54 y con la política de la empresa; Σ 47/55/63/65/67/69 vs los
conceptos de aportes de los comprobantes `NM` del mes por subsistema (la diferencia se muestra
antes de descargar). Los códigos de las administradoras (`PilaCode`) son los del listado del
operador, no el `Code` de la cooperativa.

## 2. Dispersión bancaria — formato parametrizable (`PAY_BankFileFormats`)

### 2.1 Definición

```jsonc
{
  "code": "AVVILLAS-PAGOS",                 // CodigoDeCatalogo: mayúsculas, ≤ 20, único
  "name": "Banco AV Villas — pagos a terceros (Empresas)",
  "bankPublicId": "…",                      // COR_Banks: el banco pagador
  "validFrom": "2026-12-01", "validTo": null,
  "kind": "FixedWidth" | "Delimited",
  "delimiter": ";", "quoteText": false,     // sólo Delimited
  "encoding": "us-ascii" | "utf-8" | "windows-1252",
  "lineEnding": "CRLF" | "LF",
  "uppercase": true, "stripAccents": true,
  "amountFormat": "Integer" | "ImplicitCents" | "Point2",   // 1250000 | 125000000 | 1250000.00
  "fileName": "NOM{PaymentDate:yyyyMMdd}{Sequence:000}.txt", // plantilla con los mismos orígenes
  "contentType": "text/plain",
  "records": {
    "header":  { "enabled": true,  "fields": [ /* Field */ ] },
    "detail":  { "enabled": true,  "fields": [ /* Field */ ] },
    "trailer": { "enabled": true,  "fields": [ /* Field */ ] }
  }
}
// Field
{ "order": 1, "name": "Tipo de registro",
  "source": "Constant",                     // ver tabla de orígenes
  "value": "1",                             // sólo Constant
  "length": 1,                              // obligatorio en FixedWidth; máximo en Delimited
  "align": "Left" | "Right", "pad": " " | "0",
  "format": "yyyyMMdd",                     // fechas; en montos manda amountFormat salvo override
  "map": { "1": "S", "2": "D" },            // traduce el valor del ERP al del banco (p. ej. tipo de cuenta)
  "required": true,
  "truncate": true                          // Left + texto: recorta; false → error LineTooLong
}
```

Orígenes (`source`) que resuelve `FlatFileWriter`; lo que no esté aquí se agrega al enum y al
validador del formato, nunca como código por banco:

| Origen | Registro | Valor |
|---|---|---|
| `Constant` | todos | `value` literal |
| `CompanyNit`, `CompanyNitDv`, `CompanyName` | todos | empresa |
| `SourceAccountNumber`, `SourceAccountType` | todos | cuenta bancaria origen (`ACC_*` bancaria de la 009 elegida al generar) |
| `SourceBankCode` | todos | `COR_Banks.TransferCode` del banco pagador |
| `PaymentDate`, `GenerationDate`, `GenerationTime` | todos | fecha de pago de la relación; hoy |
| `Sequence` | header/trailer | consecutivo diario de archivos de la cooperativa |
| `BatchReference` | todos | referencia del archivo (la que vuelve en `mark-sent` si el banco no da otra) |
| `LineNumber` | detail | 1..N |
| `EmployeeDocumentType`, `EmployeeDocument` | detail | `Person.DocumentType` (con `map` al código del banco), `Person.TaxId` |
| `EmployeeFullName`, `EmployeeFirstNames`, `EmployeeLastNames` | detail | de la persona |
| `EmployeeBankCode` | detail | `COR_Banks.TransferCode` del banco destino (`Employee.PayrollBankId`); **verificar** que el dato traiga el código ACH (R11) |
| `EmployeeAccountType` | detail | `Employee.PayrollBankAccountType` (1 ahorros / 2 corriente) con `map` |
| `EmployeeAccountNumber` | detail | `Employee.PayrollBankAccountNumber` (≤ 25) |
| `NetAmount` | detail | neto a pagar del empleado en la corrida |
| `PaymentConcept` | detail | texto de la corrida («NOMINA 2026-12 Q1», «PRIMA 2026-II») |
| `EmployeeEmail` | detail | si el banco notifica |
| `LineCount`, `TotalAmount` | trailer | totales del detalle |
| `Blank` | todos | espacios/ceros según `pad` |

Validación al guardar (`Payroll.Disbursement.FormatInvalid`): órdenes consecutivos sin huecos,
`length` en todo campo de ancho fijo, `Constant` con `value`, `map` sólo en campos de texto,
`Sequence`/`LineCount`/`TotalAmount` fuera del detalle, ningún origen `Employee*`/`NetAmount` fuera
del detalle, `fileName` con orígenes válidos. `POST /formats/{id}/preview` escribe 5 líneas con
una corrida real sin persistir.

### 2.2 Ejemplo ilustrativo (**no es el formato de AV Villas**)

El formato del archivo plano de pagos de AV Villas Empresas **no se encontró** en fuentes públicas
ni en el repositorio (R11, falta del dueño 1): se carga como fila de datos cuando el dueño lo
aporte y se prueba contra el portal (SC-007). Mientras tanto, la US8 se prueba con este formato
ficticio de ancho fijo, que ejercita cabecera, detalle, totales, mapeo y relleno:

```jsonc
{ "code": "DEMO-ANCHOFIJO", "name": "Demostración ancho fijo", "bankPublicId": "…",
  "validFrom": "2026-12-01", "kind": "FixedWidth", "encoding": "us-ascii", "lineEnding": "CRLF",
  "uppercase": true, "stripAccents": true, "amountFormat": "ImplicitCents",
  "fileName": "DEMO{PaymentDate:yyyyMMdd}.txt",
  "records": {
    "header": { "enabled": true, "fields": [
      { "order": 1, "name": "Tipo", "source": "Constant", "value": "1", "length": 1 },
      { "order": 2, "name": "NIT", "source": "CompanyNit", "length": 10, "align": "Right", "pad": "0" },
      { "order": 3, "name": "Cuenta origen", "source": "SourceAccountNumber", "length": 17, "align": "Right", "pad": "0" },
      { "order": 4, "name": "Fecha de pago", "source": "PaymentDate", "format": "yyyyMMdd", "length": 8 },
      { "order": 5, "name": "Referencia", "source": "BatchReference", "length": 12, "align": "Left", "pad": " ", "truncate": true } ] },
    "detail": { "enabled": true, "fields": [
      { "order": 1, "name": "Tipo", "source": "Constant", "value": "2", "length": 1 },
      { "order": 2, "name": "Tipo doc.", "source": "EmployeeDocumentType", "length": 1, "map": { "CC": "1", "CE": "2", "PA": "4", "PE": "5" } },
      { "order": 3, "name": "Documento", "source": "EmployeeDocument", "length": 15, "align": "Right", "pad": "0" },
      { "order": 4, "name": "Nombre", "source": "EmployeeFullName", "length": 40, "align": "Left", "pad": " ", "truncate": true },
      { "order": 5, "name": "Banco destino", "source": "EmployeeBankCode", "length": 4, "align": "Right", "pad": "0" },
      { "order": 6, "name": "Tipo cuenta", "source": "EmployeeAccountType", "length": 1, "map": { "1": "S", "2": "D" } },
      { "order": 7, "name": "Cuenta", "source": "EmployeeAccountNumber", "length": 17, "align": "Right", "pad": "0" },
      { "order": 8, "name": "Valor", "source": "NetAmount", "length": 15, "align": "Right", "pad": "0" },
      { "order": 9, "name": "Concepto", "source": "PaymentConcept", "length": 20, "align": "Left", "pad": " ", "truncate": true } ] },
    "trailer": { "enabled": true, "fields": [
      { "order": 1, "name": "Tipo", "source": "Constant", "value": "3", "length": 1 },
      { "order": 2, "name": "Registros", "source": "LineCount", "length": 6, "align": "Right", "pad": "0" },
      { "order": 3, "name": "Total", "source": "TotalAmount", "length": 17, "align": "Right", "pad": "0" } ] } } }
```

Salida esperada para dos empleados con prima de 1.124.547,50 y 630.404,72 (los ejemplos de R1;
líneas de 48, 114 y 24 posiciones, con los espacios de relleno al final):

```text
108903000010000000123456789020261215PRIMA2026II
21000001234567890ANA MARIA LOPEZ PEREZ                   0052S00000009876543210000000112454750PRIMA 2026-II
21000000987654321CARLOS RUIZ                             0007D00000001234567890000000063040472PRIMA 2026-II
300000200000000175495222
```

El tercer empleado sin cuenta no está en el archivo y sale en `excluded` con `NoBankAccount`
(FR-032). Al marcar enviado, los dos quedan pagados con medio `Transfer`, la fecha y
`bankReference` (FR-033).

### 2.3 Lo que cambia cuando llegue el formato real de AV Villas

Nada en el código: una fila nueva en `PAY_BankFileFormats` con `code = "AVVILLAS-PAGOS"` y la
vigencia; si exige un origen que no está en la tabla de §2.1 (p. ej. un código de oficina), se
agrega al enum de orígenes con su prueba, y esa sí es una tarea. Preguntas que el dueño debe traer
respondidas con la estructura: ancho fijo o delimitado; codificación; si la cuenta origen va en
cabecera o en cada línea; el código de banco destino que usa AV Villas (ACH/Superfinanciera); cómo
codifica el tipo de documento y el tipo de cuenta; si el valor lleva decimales implícitos; si hay
registro de totales; el nombre que exige el portal.

## 3. Relación de consignación de cesantías por fondo (FR-012)

### 3.1 La relación (`TablaExportable`, vista `consignacion-cesantias`)

Encabezado de la cooperativa (empresa, NIT, «Consignación de cesantías año AAAA», corrida y
versión, fecha límite `CESANTIAS_FECHA_LIMITE_CONSIGNACION`, usuario, fecha). Un bloque por fondo,
ordenado por nombre del fondo y luego por apellido:

| Columna | Origen |
|---|---|
| Fondo | `SeveranceProvider.Name`, NIT (de su persona, FR-088 de la 009), `PilaCode` |
| Tipo y número de documento | `Person.DocumentType`, `Person.TaxId` |
| Apellidos y nombres | persona |
| Fecha de ingreso | `Employee.HireDate` |
| Salario base de liquidación | línea `CESANTIAS` de la corrida (`Base`) |
| Días liquidados | línea `CESANTIAS` (`Quantity`) |
| Cesantías a consignar | línea `CESANTIAS` (`Amount`) |
| Intereses (informativo, se pagan al empleado) | línea `INT_CESANTIAS` |
| Estado | `Pendiente` / `Consignado el dd-mm-aaaa, ref. …` (`mark-deposited`) |
| **Total por fondo** | Σ cesantías; número de empleados |
| **Total general** | Σ fondos; debe igualar la cuenta por pagar al fondo del comprobante contable |

Exportable a `xlsx`, `pdf` y `docx` como cualquier vista del centro de reportes; en `xlsx` cada
fondo va además en su propia hoja para entregarla al fondo.

### 3.2 Archivo plano por fondo (opcional)

Los fondos (Porvenir, Protección, Colfondos, FNA) reciben la consignación por su portal con
formatos propios que **no se investigaron** en esta fase (los dos fondos de COOFLOPAL se confirman
con la contadora). Cuando un fondo exija archivo, se define con el **mismo motor de §2**
(`PAY_BankFileFormats` con `scope = "SeveranceDeposit"` y el fondo como «banco») y se descarga por
`GET /api/payroll/settlements/severance/{runId}/deposit-schedule/{fundId}/file?formatId=`. Orígenes
adicionales del ámbito: `FundNit`, `FundPilaCode`, `SeveranceAmount` (en lugar de `NetAmount`),
`SeveranceDays`, `SeveranceBaseSalary`, `EmployeeHireDate`, `Year`. Sin formato vigente para el
fondo, la ruta responde 422 `Payroll.Severance.FundFormatMissing` y la relación de §3.1 sigue siendo
la entrega válida (se digita o se carga en Excel en el portal del fondo, como hoy).

## 4. Lo que queda por verificar antes de codificar los layouts

1. Registro tipo 1 de la PILA: la posición que falta (358 vs 359) y el código de operador de
   Aportes en Línea (campo 22) — anexo v30 págs. 26-30.
2. Registro tipo 2: cotejo campo a campo con el anexo v30 págs. 81-102, formato de las tarifas
   (decimales) y códigos `PilaCode` de las administradoras — ideal una planilla ya pagada de
   COOFLOPAL como patrón (falta del dueño 6 y 7).
3. Formato de AV Villas Empresas (falta del dueño 1) y si `COR_Banks.TransferCode` trae el ACH.
4. Formatos de los fondos de cesantías de COOFLOPAL, si exigen archivo.
5. Redondeos: confirmar con el operador que IBC al peso superior y aportes al múltiplo de 100
   superior es lo que su validador aplica (Decreto 780/2016 art. 3.2.1.5).
