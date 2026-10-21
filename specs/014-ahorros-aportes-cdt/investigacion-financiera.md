# Investigación financiera: captaciones dentro de la Cartera financiera

**Fecha**: 2026-10-14 · **Feature**: 014 · **Para**: el dueño, la contadora y quien haga el plan.

Pedido del dueño (2026-10-14): investigar cómo deben funcionar el CDT, el CDAT y los ahorros, y cómo está
hoy la Cartera financiera, para que ahorros, CDAT y aportes queden como **parte integral de la Cartera
financiera** —con el concepto de línea— pero con su funcionamiento propio de liquidación de intereses,
renovaciones, retención y GMF.

Marcas: **[V]** verificado en la norma o en una fuente fiable; **[A VALIDAR]** pendiente de la contadora o
del área tributaria. Los ejemplos se calcularon a mano y se comprobaron con un cálculo independiente.

---

## 1. Resumen

1. **Hoy el ahorro está mal**: el interés abonado resta del saldo, se liquida sobre el saldo mínimo del
   producto y no sobre el real, repetir la liquidación duplica, la retención y el GMF nunca se calculan, el
   CDT liquida en base 360 y lo cancela en 365, no hay reverso ni estados, no contabiliza y no exige
   permisos. No se puede salir en vivo con ello.
2. **La Cartera nueva (013) ya tiene la mitad del trabajo**: catálogo de líneas con vigencia bitemporal,
   motores decimales de tasas, días, causación y redondeo, ciclo diario por cooperativa, libro inmutable
   de tres fechas, contrato con recaudadores, hechos a Contabilidad por matriz, migración con cuadre,
   documentos verificables, permisos con alcance por oficina y línea. Lo que asume «crédito» es el
   contenido de la versión de la línea (modalidad, usura, mora, amortización, prelación) y el libro, que
   apunta al crédito.
3. **Propuesta**: una sola Cartera financiera con **líneas de tres naturalezas** —colocación (crédito),
   captación (ahorro a la vista, contractual, permanente y CDAT) y aportes—. Comparten la identidad de la
   línea, el alcance, los motores comunes, el ciclo diario, el cierre mensual, la matriz contable, el
   contrato con los recaudadores, la migración y los documentos. Cada naturaleza tiene su propia versión
   de condiciones, su libro y sus motores particulares.
4. **CDAT, no CDT**: lo que emite una cooperativa es un CDAT; el CDT es un título valor de los bancos.

---

## 2. Cómo está hoy

### 2.1 Lo heredado de ahorros, aportes y CDT

| Tema | Estado | Consecuencia |
|---|---|---|
| Modelo | 38 entidades conservadas (`Domain/Entities/Savings`, tablas `LND_*`), banderas de texto, sin invariantes, persona por código de texto; CDT aparte (`CDT_*`) | No hay producto, ni estado de cuenta, ni titularidad confiable |
| Saldo | «Consignaciones menos todo lo demás» | El interés abonado (`IN`) **resta** |
| Cierre de cuenta | Borrado lógico | Se pierde el estado; viola el Principio VII leído con el XI |
| Movimientos | Editables y borrables, sin reverso | Viola el Principio XI |
| Interés de ahorro | Sobre el **saldo mínimo para interés** del producto, no sobre el real; no guarda la última liquidación | Interés falso y **duplicado** al repetir |
| Base de días | 360 en liquidación de ahorro y CDT; 365 en cancelar, renovar y proyectar CDT | El mismo CDT da dos intereses distintos |
| Retención y GMF | Campos sin lógica | Nunca se retiene ni se cobra GMF |
| Aportes | No hay entidad: se graban en la tabla de **promedios para reducción de aportes** | Saldo de aportes no confiable |
| Contabilidad | Las dos liquidaciones devuelven el comprobante vacío (marca pendiente de la 009); cuentas como texto en el producto | Nada llega al libro contable |
| Documentos | Extracto en cero, PDF «no implementado», sin certificados | Incumplimiento tributario anual |
| Seguridad | 52 rutas `/api/savings/*` y 18 `/api/cdt/*` sólo con sesión | Cualquier usuario mueve dinero |
| Retiro del asociado | Marca «retirado»; no devuelve ni cruza | Los aportes no operan como garantía |
| Fechas | UTC en 15 lugares | Movimientos del día equivocado después de las 7 p. m. |

### 2.2 La Cartera financiera nueva (013, fases 1–14, rama local sin desplegar)

- **Avance**: 798 de 1.090 tareas; K1 casi cerrada (fases 1, 2 y 4–13 completas; faltan en la 3 el
  diagnóstico y la marca del retiro heredado, y en la 14 registrar respuestas del dueño y congelar las
  migraciones). Nada está en DEV, QA ni producción.
- **Línea de crédito**: identidad estable (`CreditLine`: código, nombre, estado, bloqueo por usura) y
  versiones inmutables con vigencia bitemporal (`CreditLineVersion`), que el crédito sella al
  desembolsar. La versión es de crédito de punta a punta: modalidad y segmento SARC obligatorios, usura,
  tasa de mora, sistemas de amortización, gracia, prelación de conceptos, requisitos del deudor.
- **No hay clase de producto**: nada distingue hoy una línea activa de una pasiva.
- **Motores reutilizables tal cual** (puros y decimales): `EquivalenciaDeTasas` (efectiva ↔ diaria ↔
  periódica ↔ nominal, compuesta o lineal), `ContadorDeDias` (30/360, real/365, real/360),
  `AritmeticaDecimal`, `RedondeoDeCartera`, la aritmética de `Causacion` (acumulado y del día sin error de
  redondeo acumulado), `TasaDelDia.TasaVariable`, `CalendarioHabil` y festivos.
- **Atados al crédito**: reconstrucción y causación por concepto de crédito, plan de pagos, aplicación de
  pagos, mora, suspensión, SARC y deterioro.
- **Libro**: `LoanTransaction` (sólo inserción, tres fechas, reverso espejo, operación con recaudador y
  clave) apunta al crédito y usa los conceptos del crédito; posición diaria versionada con columnas de
  crédito; marca de agua de causación.
- **Procesos**: conductor por cooperativa con clases de corrida y ejecutores enchufables; ciclo diario de
  ocho pasos en tandas; cierre mensual SARC.
- **Contrato con recaudadores**: listar, simular, aplicar, reversar y resolver saldo a favor; síncrono en
  la unidad de trabajo del recaudador; registro de participantes por cooperativa; la clase
  `Contributions` ya existe, con medio «cruce interno» y sin reverso desde Cartera (C37).
- **Contabilidad**: emisor `CAR`, 35 hechos, matriz `ACC_LendingPostingRules` con 26 roles —ninguno de
  depósito, interés por pagar ni gasto por intereses— y 13 dimensiones.
- **Ya previsto para aportes**: el puerto `IConsultaDeAportes` (hoy «no disponible»), la plantilla de
  saldos de aportes (`lending.contribution-balances`, D19), los aportes como garantía y como requisito de
  la línea, y las rutas de pantalla `/cartera/ahorros*`, `/cartera/cdt*`, `/cartera/aportes` reservadas.
- **Fronteras fijadas por pruebas**: `ElEspacioLendingEsSoloCartera` (lo conservado fuera de `Lending`),
  `LaCarteraSoloSeTocaPorSusInterfaces` (cinco puertos), `CarteraNoConoceContabilidad`,
  `ElMotorDeCarteraEsDecimal`, `LaCarteraNoTieneValoresLegalesFijos`. Integrar captaciones exige
  enmendarlas, no saltarlas.

---

## 3. Cómo deben funcionar

### 3.1 CDT frente a CDAT

| | CDT | CDAT |
|---|---|---|
| Emisor | Establecimiento de crédito vigilado por la Superfinanciera | Cooperativa de ahorro y crédito, multiactiva con sección de ahorro, fondo de empleados (Supersolidaria) |
| Naturaleza | Título valor nominativo, negociable por endoso, casi siempre desmaterializado [V] | **No es título valor**: título de legitimación (C. de Co. art. 645), no se endosa; sólo se cede como crédito [V] |
| Plazo mínimo | Un mes [V] | Lo fija el reglamento de la entidad; en el mercado, 30 días [A VALIDAR en la CBJ] |
| Redención anticipada | Prohibida, de orden público; sólo se vende el título [V] | La decide el reglamento; si se admite, con penalidad que reliquida a tasa menor [A VALIDAR] |
| Prórroga | Si no se redime, se prorroga por un término igual; la tasa la pactan las partes, usualmente la vigente para ese plazo el día de la prórroga [V] | Igual en la práctica: la CBJ de la Supersolidaria admite pactar la prórroga en condiciones determinables [V, fuente secundaria] |
| Después del vencimiento sin prórroga | Según reglamento: suele pasar a cuenta por pagar **sin intereses** o a la vista | Igual |
| Seguro | Fogafín | Fogacoop, sólo cooperativas inscritas; fondos de empleados sin seguro [A VALIDAR] |

**Para el ERP**: plazo mínimo y máximo, redimible o no y penalidad, prórroga automática y número de
prórrogas, regla de tasa en la prórroga (la misma, la vigente de la tabla, una pactada), días de gracia y
tratamiento si no se prorroga son **condiciones de la línea con vigencia**. El documento se llama «CDAT»
y dice «no negociable».

### 3.2 Liquidación del CDAT

**Fórmulas**
- Tasa del período desde la efectiva anual: `i_p = (1 + EA)^(n/B) − 1`, con n días del período y B la
  base (365 o 360) de la línea.
- Nominal pagadera m veces: `j = m × [(1 + EA)^(1/m) − 1]`.
- Interés anticipado equivalente: `i_a = i_v / (1 + i_v)`.
- Causación diaria: el interés de cada período se reparte por días con la fórmula compuesta por día; la
  diferencia de redondeo contra el valor del plan se absorbe el último día del período.

**Ejemplo: 10.000.000 a 180 días al 10 % EA**

| Modalidad | Cálculo | Interés |
|---|---|---|
| Al vencimiento, base 365 | 10.000.000 × (1,10^(180/365) − 1) | **481.244** |
| Al vencimiento, base 360 | 10.000.000 × (1,10^(0,5) − 1) | **488.088** |
| Mensual vencido, base 360 | i = 1,10^(1/12) − 1 = 0,79741 %; 6 × 79.741 | **478.446** (79.741 por mes) |
| Mensual anticipado | 79.741 / 1,0079741 | 79.111 al inicio de cada mes |

El pago mensual da menos que el pago al vencimiento porque el titular recibe los intereses antes:
1,0079741^6 = 1,0488088, igual que la base 360 al vencimiento.

**Retención** (ver 3.6): al 4 % sobre 488.088 = 19.524, neto 468.564; por mes, 3.190 sobre 79.741, neto
76.551. **Pero** el interés diario del ejemplo es 488.088 / 180 = 2.711,6, por debajo del umbral diario
de 0,055 UVT (2.880,57 en 2026): si ese umbral aplica a los CDAT, **no hay retención** (ver 3.6).

### 3.3 Ahorro a la vista

**Métodos** (parámetro de la línea; Mambu los ofrece igual):
- **Saldo diario** (recomendado): cada día `saldo del cierre × i_d`, con `i_d = (1 + EA)^(1/B) − 1`;
  se causa a diario y se abona (capitaliza) en la periodicidad de la línea.
- **Saldo promedio**: con interés diario simple da lo mismo que el saldo diario.
- **Saldo mínimo del período**: castiga al ahorrador; sólo si el reglamento lo dice.
- **Saldo mínimo para ganar interés**: el día con saldo menor no causa.
- **Tramos de saldo**: escalonados (cada tramo a su tasa) o por saldo total (todo el saldo a la tasa del
  tramo donde cae).

**Ejemplo: mes de 30 días, 3 % EA, base 365** (`i_d` = 0,0080987 %)

| Días | Saldo | Interés del día | Subtotal |
|---|---|---|---|
| 1–10 | 2.000.000 | 161,97 | 1.619,7 |
| 11–20 | 5.000.000 | 404,94 | 4.049,4 |
| 21–30 | 1.500.000 | 121,48 | 1.214,8 |
| | | **Abono del mes** | **6.884** |

Por saldo promedio (2.833.333) da 6.884; por saldo mínimo da 3.649. Ningún interés diario alcanza el
umbral de 2.880,57: este mes **no se retiene**.

### 3.4 Ahorro contractual o programado

Cuota periódica durante un plazo; si cumple, tasa más **bonificación**; si incumple o retira antes,
reliquidación a la tasa castigo y penalidad del reglamento.

**Ejemplo**: 200.000 al final de cada mes durante 12 meses.
- A 6 % EA (i = 0,486755 % mensual): valor final 2.465.306, interés 65.306.
- Con bonificación por cumplimiento a 8 % EA: valor final 2.486.777, interés 86.777.
- La **bonificación es 21.471**; se causa como gasto al cumplirse o se provisiona según la probabilidad de
  cumplimiento [A VALIDAR con la contadora].

### 3.5 Ahorro permanente y aportes

- **Ahorro permanente** (fondos de empleados, CUIF 2130): obligatorio, porcentaje del ingreso
  descontado por nómina junto con los aportes; sólo se devuelve al retiro, cruzado antes contra la
  deuda; tasa que fija la junta, liquidada sobre saldo y abonada anual o semestralmente; garantía de
  créditos. Es más «aporte con interés» que depósito.
- **Aportes sociales** (CUIF 31, patrimonio por la excepción NIIF del Decreto 2496 de 2015): garantía
  de las obligaciones del asociado e inembargables (Ley 79 art. 49); mínimo no reducible; revalorización
  con cargo al fondo que decide la asamblea, acreditada en la cuenta individual; devolución sólo al
  retiro, por exceso de participación o por amortización, sin perforar el mínimo irreducible de la
  entidad; lo no reclamado, a cuenta por pagar. **No causan intereses.**
- En los fondos de empleados la cuota obligatoria (aportes más ahorro permanente) no supera el 10 % del
  ingreso y al menos una décima parte va a aportes (Ley 2496 de 2025).

### 3.6 Retención en la fuente sobre rendimientos (2026)

- **UVT 2026 = 52.374** (Resolución DIAN 238 de 2025) [V].
- **Tarifas**: CDT y **CDAT 4 %** (DUR 1625 art. 1.2.4.2.83, con el parágrafo del art. 1 del Decreto
  572 de 2025, vigente desde el 01/06/2025 y **nunca suspendido**) [V]; ahorro a la vista y demás
  rendimientos **7 %** (tarifa residual) [V, fuente secundaria]. Los arts. 2 a 8 del Decreto 572 se
  suspendieron el 07/05/2026 y la suspensión se revocó el 02/06/2026 [V; ejecutoria A VALIDAR]. Por eso
  la tarifa es un **parámetro con vigencia por clase de producto**.
- **Umbral diario del sector solidario** (DUR art. 1.2.4.2.6): no se retiene si el interés **diario** es
  menor de 0,055 UVT (**2.880,57 en 2026**); si lo iguala o supera, se retiene sobre el **valor total**
  [V]. **[A VALIDAR]** si aplica también a los CDAT después del Decreto 572.
- **Componente inflacionario** del año gravable 2025: 55,43 % (Decreto 898 de 2026) [V]. No es renta
  para personas naturales no obligadas a llevar contabilidad; si la política lo descuenta al retener,
  sobre 488.088 la base es 217.541 y la retención 8.702. [A VALIDAR] si se aplica y con qué porcentaje
  (el del año corriente se conoce a mediados del siguiente). Política parametrizable.
- **Momento**: en el pago o abono en cuenta, lo que ocurra primero: cada pago de intereses del CDAT,
  cada abono mensual del ahorro. La causación interna no es abono. La retención se practica **antes** de
  capitalizar en una prórroga.
- **A quién**: personas naturales, declarantes o no, y jurídicas; no a los no contribuyentes (art. 23 ET,
  incluidos los fondos de empleados) ni a los autorretenedores; las cooperativas del art. 19-4 sólo por
  rendimientos de títulos [A VALIDAR]. Lo decide el **perfil tributario de la persona** (012).
- **Cuenta**: 243535, retención por rendimientos financieros.

### 3.7 GMF (4 por mil)

- **Hecho generador** (ET art. 871): disponer de recursos de cuentas de ahorro; incluye la **cancelación
  de depósitos a término mediante abono en cuenta**, con base capital más intereses (Oficio DIAN 2787 de
  2019) [V, fuente secundaria].
- **Exención** (art. 879 num. 1): retiros de cuentas de ahorro en vigiladas por la Supersolidaria hasta
  **350 UVT al mes = 18.330.900 en 2026** [V].
- **Marcación**: desde el 13/12/2024 la Ley 2277 (art. 881-1) dejó de exigir una sola cuenta marcada y el
  límite es **por titular sumando sus cuentas**, aunque la DIAN ha dicho que sin el sistema de
  información sigue la marcación [A VALIDAR la práctica vigente]. El ERP debe admitir las dos políticas.
- **Traslados del mismo titular** (num. 14): exentos; pero el concepto DIAN 082568 de 2011 dice que el
  traslado **entre ahorro y CDAT** no lo está [A VALIDAR que siga vigente].
- **Ejemplos**: redimir el CDAT de 3.2 abonándolo a la cuenta del titular: 10.488.088 × 0,004 = 41.952
  si rige el concepto de 2011; retiros de 20.000.000 en un mes de una cuenta exenta: exceso 1.669.100,
  GMF 6.676.
- **Cuenta**: 2430, por producto (243005 ahorro, 243010 CDAT, 243015 contractual, 243020 permanente).

### 3.8 Renovaciones del CDAT

- **Prórroga automática**: mismo plazo, tasa según la regla de la línea, título nuevo enlazado al
  anterior (o nueva versión del mismo número, según el reglamento).
- **Renovación con condiciones nuevas** (plazo, tasa, monto): título nuevo que cancela el anterior, con
  trazabilidad «renovación de».
- **Capitalización**: capital nuevo = capital + intereses − retención; la retención va antes; el GMF puede
  aplicar si hay abono en cuenta [A VALIDAR].
- **Días de gracia**: 1 a 3 días hábiles después del vencimiento para redimir o cambiar condiciones sin
  que la prórroga se consolide; esos días causan a la tasa nueva o no causan, según la línea.
- **Contabilidad**: si el plazo nuevo cambia el rango de emisión, el capital se reclasifica entre 211005
  (menos de 6 meses), 211010 (6 a 12), 211015 (12 a 18) y 211020 (18 o más).

### 3.9 Contabilidad CUIF (verificada en la semilla `puc-solidario.json`)

| Producto | Capital | Intereses por pagar | Gasto |
|---|---|---|---|
| Ahorro a la vista | 2105 (210505 activas, 210510 inactivas) | 210595 | 615005 |
| CDAT | 2110 (211005 … 211020 por plazo de emisión) | 211095 | 615010 |
| Contractual | 2125 (212505 … 212520) | 212595 | 615015 |
| Permanente | 2130 (213005 corto, 213010 largo) | 213095 | 615020 |
| Aportes | 3105 (temporalmente restringidos y mínimos no reducibles) | — | — |

Ciclo de un CDAT: constitución (caja o 2105 contra 211005); causación diaria (615010 contra 211095);
pago de intereses (211095 contra 243535 por la retención y 2105 o caja por el neto); redención (211005
contra 2105 o caja); GMF (2105 del titular contra 243010). La matriz de Cartera necesita **roles nuevos**
para esto: capital del depósito, interés por pagar, gasto por intereses, retención por pagar, GMF por
pagar, depósito inactivo, aportes por clase, revalorización y devolución por pagar.

---

## 4. Cómo se integra en la Cartera financiera

### 4.1 Lo que se comparte (un solo módulo)

| Pieza de Cartera | Cómo la usan las captaciones |
|---|---|
| Catálogo de líneas (identidad, código, estado, vigencia bitemporal, alcance de usuario por línea) | Una línea tiene **naturaleza**: colocación, captación o aportes. La identidad es común; la versión de condiciones es propia de cada naturaleza |
| Motores comunes (tasas, días, redondeo, aritmética, causación acumulada, calendario, tasa variable) | Tal cual |
| Conductor de corridas por cooperativa, ciclo diario, puesta al día, errores de proceso | Un paso o ejecutor nuevo de captaciones dentro del mismo ciclo |
| Cierre mensual | Incluye el cierre de causación de captaciones (sin SARC) |
| Contrato con recaudadores y registro de participantes | Se amplía con operaciones de depósito (consignar, retirar, trasladar, exigibles de cuotas) para los mismos recaudadores |
| Hechos a Contabilidad (emisor `CAR`), matriz, modo de paso, validación previa, conciliación, guarda de cierre | Hechos y roles nuevos en la misma matriz; dimensión de naturaleza y clase de producto |
| Migración (plantillas, cuadre, actas, activación) | Plantillas nuevas; activación **por naturaleza** |
| Documentos verificables, centro de informes, permisos `Lending.*`, alcance, atribuciones, auditoría | Tipos de documento, vistas y permisos nuevos dentro del mismo catálogo |
| `IConsultaDeAportes` y la plantilla de saldos de aportes (D19) | La consulta la responde la misma Cartera; la plantilla deja de usarse al activar aportes |
| Cruce de aportes al retiro | Operación interna de la Cartera con medio «cruce interno», no un recaudador externo |
| Débito automático de cuotas desde ahorro | Operación interna de la Cartera |

### 4.2 Lo que es propio de las captaciones

- Versión de condiciones de captación (métodos de saldo, tramos, mínimos, liquidación, retención, GMF,
  inactividad, condiciones del CDAT y del contractual) y de aportes (mínimo no reducible, cuota, reparto
  con permanente).
- **Libro propio** de la cuenta de captación y de aportes, con las mismas reglas que el del crédito (tres
  fechas, sólo inserción, reverso espejo, operación con clave y recaudador, posición diaria, marca de
  agua), porque el libro del crédito apunta al crédito y a sus conceptos.
- Motores particulares puros con casos dorados: interés por método de saldo y tramos, retención con
  umbral diario y componente inflacionario, GMF con exención mensual, plan y prórroga del CDAT, ahorro
  contractual con bonificación, revalorización y excedentes, fondo de liquidez.
- Estados de la cuenta, titularidad, beneficiarios, bloqueos, embargos, inactividad, fallecimiento.
- Lo que **no** aplica: usura, mora, calificación, deterioro, garantías.

### 4.3 Fronteras que hay que enmendar (sin saltarlas)

- `ElEspacioLendingEsSoloCartera`: lo conservado se retira; las captaciones nuevas viven en `Lending`.
- `LaCarteraSoloSeTocaPorSusInterfaces`: la lista cerrada suma la consulta de aportes y el contrato
  ampliado; Nómina, Recaudo y Tesorería siguen entrando sólo por ahí.
- Enums de Cartera: valores nuevos al final, sin renumerar.
- Tablas: prefijo `LND_` (el del módulo), sin reutilizar ningún nombre heredado
  (`LND_SavingsAccounts`, `LND_DepositAccounts`, `LND_ContributionReductions` siguen hasta su retiro).
- Rutas: las de pantalla reservadas (`/cartera/ahorros*`, `/cartera/cdt*` → `/cartera/cdat*`,
  `/cartera/aportes`) pasan a ser de la Cartera; las API, bajo `/api/lending/...`.

---

## 5. Lo que deben confirmar la contadora y el dueño

**Respuesta del dueño (2026-10-14)**: todo configurable partiendo de la norma colombiana. GMF con una sola
cuenta de ahorro exenta por persona, configurable hasta su eliminación; componente inflacionario según la
norma, configurable. Siguen abiertos los puntos 1, 5, 6, 7 y 8 para la contadora.


1. Si el umbral diario de 0,055 UVT aplica a los CDAT después del Decreto 572 (cambia si el ejemplo de
   3.2 lleva retención).
2. El GMF al redimir o constituir un CDAT contra la cuenta de ahorros del mismo titular.
3. Si se descuenta el componente inflacionario al retener, y con qué porcentaje.
4. Política de exención de GMF: cuenta marcada o límite por titular.
5. Base de días de cada línea (365 o 360) y método de saldo del ahorro.
6. Plazo mínimo, redención anticipada, prórroga, tasa de prórroga y días de gracia del CDAT según el
   reglamento de cada cooperativa.
7. Provisión de la bonificación del ahorro contractual.
8. Artículos vigentes sobre ahorro permanente (Decreto Ley 1481 de 1989, Ley 1391 de 2010, Ley 2496 de
   2025).

## Fuentes

Decreto 572 de 2025 (normograma DIAN); INCP y Accounter sobre su suspensión y revocatoria; Resolución
DIAN 238 de 2025 (UVT 2026); Decreto 898 de 2026 (componente inflacionario); DUR 1625 de 2016 arts.
1.2.4.2.6 y 1.2.4.2.83; Decreto 2715 de 1983; Oficio DIAN 2787 de 2019; concepto DIAN 082568 de 2011;
ET arts. 19-4, 23, 368, 369, 381, 395, 871, 876, 879 y 881-1; SFC, preguntas frecuentes sobre CDT y
compilación de conceptos sobre CDAT; reglamentos de CDT de BBVA y Serfinanza; documentación de Mambu
sobre cálculo de intereses en depósitos; Ley 79 de 1988; Ley 454 de 1998; Decreto Ley 1481 de 1989 y Ley
2496 de 2025; Decreto 1068 de 2015 con el Decreto 976 de 2026; Carta Circular 02 de 2019 de la
Supersolidaria; semilla `puc-solidario.json` del repositorio.
