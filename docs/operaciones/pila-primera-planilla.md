# La primera planilla PILA — runbook

Feature 010, entrega N2 (2026-09-21). Qué tener antes de generar la primera planilla de aportes
(Resolución 2388 de 2016, archivo tipo 2, planilla E para Aportes en Línea), cómo comprobarlo
contra la base de la cooperativa, cómo cotejar el layout y qué hacer con cada inconsistencia.
Complementa `docs/manual/liquidaciones-especiales.md` y `nomina-primer-periodo.md`.

## 1. La idea en un párrafo

La planilla se arma **desde las nóminas aprobadas** imputadas al mes (más vacaciones y definitivas
con corte en el mes): una línea base por cotizante y una adicional por cada novedad con IBC propio
(incapacidad, licencia de maternidad, vacaciones, licencia no remunerada, incapacidad laboral). El
**layout** (registros tipo 1 y 2, campo a campo) es un JSON embebido con vigencia
(`Application/Payroll/Pila/Layouts/at2-v30-2026-07-24.json`); una versión nueva del anexo es un
archivo nuevo, no un despliegue de lógica. **Ningún valor legal vive en el código**: tarifas,
SMMLV, umbrales, redondeos y tabla del fondo de solidaridad son parámetros con vigencia
(`PilaParameterCodes.Required`). Validar no guarda; generar deja una versión inmutable con su
archivo, sus líneas explicadas y sus inconsistencias; marcar cargada anota el radicado del operador.

## 2. Antes de la primera vez (por cooperativa)

| Qué | Dónde se carga | Cómo se comprueba | Si falta |
|---|---|---|---|
| **Empresa** con NIT, dígito y razón social | Maestros › Empresas | `SELECT "TaxId","TaxIdCheckDigit","Name" FROM dbo."COR_Companies"` | `Pila.AportanteSinNit` |
| **Datos del aportante**: clase (A ≥ 200 cotizantes, B < 200), forma de presentación (U/S), código PILA de la ARL del aportante, código del operador (campo 22), DIVIPOLA y actividad económica de la sede | Nómina › Planilla PILA › «Datos del aportante» (`Payroll.Pila.Manage`; `PAY_PilaSettings`) | `GET /api/payroll/pila/settings` → `complete: true` | `Pila.AportanteIncompleto` y los `Pila.AportanteSin*` |
| **Código PILA** de cada EPS, fondo de pensiones (o ACCAI), ARL y caja | Nómina › EPS / Pensiones / ARL / Cajas de compensación, campo «Código PILA» (6 posiciones, el del listado del operador; distinto del código de la cooperativa) | `SELECT "Code","Name","PilaCode" FROM dbo."PAY_HealthInsuranceProviders"` (y `PAY_PensionProviders`, `PAY_WorkRiskProviders`, `PAY_FamilyCompensationFunds`) sin `PilaCode` nulo entre los usados | `Pila.SinCodigoPila` con enlace al catálogo |
| **Ficha de cada empleado**: EPS, fondo, ARL con clase de riesgo, caja; DIVIPOLA y actividad económica si difieren de la sede; tipo/subtipo de cotizante sólo si no se deriva (pensionado activo = subtipo 01; aprendiz lectiva = tipo 19 por la etapa); régimen de transición de la Ley 2381 desde abril de 2027 | Nómina › Empleados › pestaña PILA | relación de la validación | `Pila.SinEps`, `Pila.SinAfp`, `Pila.SinArl`, `Pila.SinCcf`, `Pila.SinClaseRiesgo`, `Pila.SinDivipola`, `Pila.SinActividadEconomica` |
| **Persona**: documento con la longitud de la Res. 1529/2026 (CC ≤ 10, TI ≤ 11, CE ≤ 7, PA ≤ 16, PE = 15, PT ≤ 8) y segundo apellido | Maestros › Personas | validación | `Pila.DocumentoLargo` (bloqueante), `Pila.SegundoApellidoFaltante` (alerta) |
| **Parámetros legales** vigentes al primer día del mes: `FSP_UMBRAL_SMMLV`, `FSP_TABLA`, `IBC_MINIMO_SMMLV`, `IBC_TOPE_SMMLV`, `PILA_IBC_REDONDEO`, `PILA_APORTE_REDONDEO_MULTIPLO`, tarifas de salud/pensión/ARL/CCF/SENA/ICBF, `EXONERACION_PARAFISCALES_TOPE_SMMLV`, `HORAS_MES`, `SMMLV`, `SALARIO_INTEGRAL_BASE_PCT` | Nómina › Parámetros legales (la semilla 2026 los trae) | `GET /api/payroll/legal-parameters/missing?process=Pila&asOf=AAAA-MM-01` vacío | `Payroll.Pila.ParametersMissing` / `Pila.TarifaSinVigencia` |
| **Política `Exonerada114_1`** vigente (la misma de la nómina ordinaria) y `CotizaArlEnVacaciones` | Nómina › Políticas de la empresa | `SELECT "Key","Value","ValidFrom" FROM dbo."PAY_CompanyPolicies"` | aporta completo (no es error) |
| **Permisos** `Payroll.Pila.View/Generate/MarkUploaded/Manage` en los roles | los siembra la API al arrancar; a los roles propios se asignan en Administración › Roles | `SELECT "Code" FROM dbo."SEC_Permissions" WHERE "Code" LIKE 'Payroll.Pila.%'` → 4 | 404 en la pantalla |

## 3. Cotejar el layout (T094, dato del dueño)

El layout embebido reconstruye las posiciones de la resolución y **todos sus campos llevan
`verified = false`** hasta cotejarlos con el anexo técnico v30 (numerales 2.1.1.1 y 2.1.2.1) y con
una planilla ya pagada de la cooperativa. Mientras sea así, generar deja la alerta
`Pila.LayoutSinCotejar` (hay que reconocerla) y el archivo se prueba en el validador del operador
antes de pagar (D-43). Lo que se coteja:

1. **Registro tipo 1**: los 22 campos suman 358 posiciones y el anexo declara 359 — hay un campo
   con una posición de diferencia. Se corrige el `length` en el JSON y el test
   `PilaLayoutTests` fija el nuevo largo.
2. **Decimales de las tarifas**: el anexo no los fija; el layout escribe 5 (`0.04000`) y 7 en la ARL
   (`0.0052200`). Se ajusta `format` (`Rate7`/`Rate9`) a lo que muestre la planilla pagada.
3. **Código del operador** (campo 22): Aportes en Línea lo confirma.
4. **Fondo de solidaridad** (campos 51-52): el anexo v30 dice que los liquida el operador; el ERP
   los calcula para el descuento al empleado y una diferencia allí es alerta, no error.

Cuando esté cotejado, se pone `verified: true` campo a campo en el JSON, se corre
`dotnet test tests/IngenIA365ERP.Domain.Tests --filter Pila` (los casos dorados se regeneran a
propósito con `PILA_ESCRIBIR_ESPERADO=1`, nunca solos) y la alerta desaparece.

## 4. Validar, generar, cuadrar, cargar

1. **Validar** (`POST /api/payroll/pila/{año}/{mes}/validate`, `Payroll.Pila.Generate`): sin
   guardar. Bloqueante = Error del operador; alerta = deja cargar. Cada hallazgo trae el campo, el
   empleado y la ruta donde se corrige.
2. **Generar** con las bloqueantes en cero y las alertas reconocidas. Con bloqueantes queda una
   generación `Validated` sin archivo (las inconsistencias quedan guardadas y auditadas); regenerar
   crea la versión siguiente y deja la anterior `Superseded` **con su archivo**.
3. **Cuadre (FR-027)**: Σ de pensión, salud, FSP, ARL, CCF, SENA e ICBF del archivo contra los
   conceptos de aportes de las nóminas del mes (`SALUD_EMP + SALUD_EMPLEADOR`, `PENSION_*`, `FSP`,
   `ARL`, `CAJA`, `SENA`, `ICBF`), por subsistema. **Una diferencia por redondeo es normal**: la
   nómina ordinaria redondea según su política (`Payroll.Rounding`, al múltiplo más cercano) y la
   planilla al múltiplo de 100 **superior** (Decreto 780/2016 art. 3.2.1.5). Con diferencia, la
   descarga exige `acknowledgeDifference=true` y la pantalla lo pregunta. Si la diferencia no es de
   redondeo, revisar novedades sin fechas o aportes liquidados sobre otra base.
4. **Descargar** el `.txt` (ASCII, CRLF, `PILA_<NIT>_<AAAA-MM>_v<n>.txt`) y cargarlo en Aportes en
   Línea: Liquidaciones › Adicionar liquidación › Cargar archivo › Validar. El operador clasifica en
   Error (bloquea), Alerta (deja cargar) y Posible corrección (autocorrige). La fecha límite de pago
   la propone `PILA_PLAZO_PAGO_POR_NIT` con los dos últimos dígitos del NIT y los días hábiles.
5. **Marcar cargada** (`Payroll.Pila.MarkUploaded`) con el número de planilla del operador y, si se
   sabe, la fecha de pago. Ese período ya no se regenera; una corrección se digita en el operador
   (planilla N). Planillas N y A quedan fuera del ERP por decisión del dueño.

## 5. Qué hacer con cada inconsistencia

| Código | Severidad | Qué hacer |
|---|---|---|
| `Pila.SinEps` / `SinAfp` / `SinArl` / `SinCcf` | bloqueante | asignar la administradora en la ficha (el aprendiz lectiva no necesita fondo ni caja; el pensionado activo ni el extranjero no obligado, fondo) |
| `Pila.SinCodigoPila` | bloqueante | cargar el código en el catálogo (enlace al catálogo) |
| `Pila.SinClaseRiesgo` | bloqueante | clase ARL en la ficha (es la fila de `PAY_WorkRiskRates`) |
| `Pila.SinDivipola` / `SinActividadEconomica` | bloqueante | en la ficha, o el defecto en Datos del aportante |
| `Pila.DocumentoLargo` | bloqueante desde pagos de octubre de 2026 | corregir el documento en Personas |
| `Pila.DiasNoSuman30` | bloqueante | novedades con fechas que se cruzan o exceden el mes: corregir la novedad |
| `Pila.TarifaSinVigencia` | bloqueante | cargar la vigencia del parámetro |
| `Pila.SegundoApellidoFaltante` | alerta | completar la persona; si no lo tiene, reconocer |
| `Pila.RegimenTransicionDesconocido` | alerta (desde 2027-04) | marcar en la ficha si está en régimen de transición de la Ley 2381; sin marca se usa la tabla vigente |
| `Pila.EmpleadoSinNomina` | alerta | empleado activo sin nómina aprobada del mes: aprobar la nómina o retirarlo |
| `Pila.LayoutSinCotejar` | alerta | §3 |

## 6. Abril de 2027 y la reforma pensional

Desde el 2027-04-01 la semilla trae la tabla del fondo de solidaridad de la Ley 2381 de 2024
(`FSP_TABLA` con vigencia nueva; la de la Ley 797 se cierra el 2027-03-31). Quien tenga en la ficha
**régimen de transición = Sí** sigue con la tabla anterior; con **No** usa la nueva; con
**desconocido** usa la nueva y la planilla lo avisa. Cargar esa bandera en las fichas antes de la
planilla de abril de 2027 evita las alertas (caso dorado 02). Las vigencias 2027 de SMMLV y UVT
entran por `Revisiones()` o por pantalla antes de la planilla de enero.

## 7. Cómo se prueba

Casos dorados en `tests/IngenIA365ERP.Domain.Tests/Payroll/Pila/Casos/` (once cotizantes con
ingreso, retiro, incapacidad, vacaciones, integral, aprendiz, pensionada y empresa exonerada; Ley
2381 con transición; no exonerada; ingreso y retiro el mismo mes; integral al tope; aprendices)
con los valores calculados a mano en `derivacion` y el archivo `.esperado.txt` byte a byte;
`PilaLayoutTests`, `PilaValidatorTests`; en Application `PilaHandlersTests`; por HTTP `PilaTests`
(quickstart §3.5). SC-004 —que el operador acepte el archivo— sólo se comprueba con la cuenta del
aportante (dato del dueño): la evidencia va en `docs/operaciones/estado-y-pendientes.md`.
