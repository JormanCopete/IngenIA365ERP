# Feature Specification: Cartera financiera — captaciones: ahorro a la vista, contractual y permanente, CDAT y aportes sociales

**Feature Branch**: `014-ahorros-aportes-cdt`
**Created**: 2026-10-14 · **Revisada**: 2026-10-14 (el dueño pide que las captaciones sean parte integral
de la Cartera financiera)
**Status**: Draft — clarificada con el dueño el 2026-10-14 (Q1 a Q3, P1, P25 y P27, ver Clarifications).
Quedan para su revisión las decisiones por defecto (D1 a D35) y las preguntas abiertas que siguen sin
respuesta. No implementa código.
El fundamento financiero y el diagnóstico están en [investigacion-financiera.md](investigacion-financiera.md).
**Input**: Dos pedidos del dueño del 2026-10-14:
1. Al responder la pregunta A10 de la 013: ahorros, aportes y CDT debieron renovarse junto con Cartera;
   se abordan **después de cerrar K1 de Cartera y sin retrasarla**.
2. Al revisar la primera versión de esta especificación: investigar cómo deben funcionar el CDT, el CDAT
   y los ahorros, y agregarlos **como parte integral de la Cartera financiera** —con el concepto de
   línea, igual que las líneas de crédito— pero con su funcionamiento particular de liquidación de
   intereses, renovaciones, retención en la fuente y GMF. Se decidió escribirla como **extensión de la
   Cartera** en esta feature, sin tocar la especificación 013 mientras se termina K1.

La solicitud pide el mismo nivel de la 013: motores puros con casos dorados, operaciones inmutables con
reverso, contabilidad por hechos y matriz (las captaciones no conocen cuentas), migración desde cero con
plantillas, cuadre y activación, documentos al titular, seguridad por alcance y auditoría, partiendo de
lo que hoy existe y de la norma de la Supersolidaria.

Como en la 013, el módulo sirve a **toda** organización solidaria cliente del ERP —cooperativa de ahorro
y crédito, multiactiva o integral con sección de ahorro y crédito, fondo de empleados de cualquier
categoría, y la que **no capta** ahorro pero sí recibe aportes—. El tipo de entidad es un dato de cada
cooperativa (el perfil regulatorio de FR-018 de la 013) y decide qué líneas puede activar. COOFLOPAL, la
primera en vivo, **no capta ahorro** y sale el 01/12/2026 con créditos y **aportes** (Q1). Aun así, por el
criterio que el dueño fijó para Cartera, todo queda completo y configurable para todos los tipos de
entidad.

Ninguna pantalla, informe, plantilla ni documento de esta feature nombra al sistema anterior: se habla de
«lo heredado» y de «cifras de referencia».

## Contexto

Estado verificado el 2026-10-14 en la rama `013-cartera-financiera` (fases 1 a 14, `c6ba3f2c`), en las
especificaciones 009, 010, 012 y 013 y en la norma publicada. El detalle, con fórmulas y ejemplos, está
en [investigacion-financiera.md](investigacion-financiera.md).

### Lo heredado de ahorros, aportes y CDT

1. **Es un traslado tabla por tabla, sin modelo de producto.** 38 entidades conservadas en
   `Domain/Entities/Savings`, todas con tablas `LND_*`, banderas de texto, sin invariantes, la persona
   enlazada por un código de texto, y mezcladas con caja, chequeras, canje, zonas, actas, biometría,
   enfermedades, bienes, actividades sociales, auxilios y SIPLA. El CDT es un módulo aparte (`CDT_*`,
   18 rutas). No existe una entidad de aportes: se graban en la tabla de **promedios para reducción de
   aportes**.
2. **Los saldos no son confiables.** El saldo es «consignaciones menos todo lo demás», así que el interés
   abonado **resta**; «cerrada» es un borrado lógico; los movimientos se editan y borran; el número de
   cuenta es «el mayor más uno»; las fechas se toman en UTC.
3. **La liquidación es incorrecta.** El ahorro liquida sobre el **saldo mínimo para interés** del
   producto, no sobre el saldo real, y repetir la liquidación la duplica. El CDT liquida en base 360 y lo
   cancela, renueva y proyecta en 365. Retención y GMF son campos sin lógica.
4. **No contabiliza, no documenta, no protege.** Las dos liquidaciones devuelven el comprobante vacío;
   el extracto sale en cero y su PDF «no implementado»; las 70 rutas sólo exigen sesión.
5. **El retiro del asociado no mueve dinero**: no devuelve ni cruza aportes contra la deuda.

### La Cartera financiera nueva (013)

- **Avance**: 798 de 1.090 tareas; K1 casi cerrada; rama local, nada desplegado.
- **Ya construido y reutilizable tal cual**:
  - catálogo de líneas con identidad estable, estado y **versiones inmutables con vigencia bitemporal**;
    alcance de usuario por línea y por oficina, atribuciones por monto;
  - motores puros y decimales de tasas (efectiva, diaria, periódica, nominal; compuesta o lineal), días
    (30/360, real/365, real/360), redondeo, aritmética, causación acumulada sin error de redondeo, tasa
    variable sobre índice, calendario hábil y festivos;
  - conductor de corridas por cooperativa con ejecutores enchufables, ciclo diario en tandas con puesta
    al día, errores de proceso, cierre mensual y su reapertura;
  - libro de sólo inserción con tres fechas, reverso espejo, operación con recaudador y clave, posición
    diaria versionada y marca de agua de causación;
  - contrato con recaudadores (listar, simular, aplicar, reversar, resolver saldo a favor) síncrono en la
    unidad de trabajo del recaudador y registro de participantes por cooperativa;
  - hechos a Contabilidad (emisor `CAR`) con matriz hecho × rol × dimensiones, modo de paso, validación
    previa, conciliación y guarda de cierre;
  - migración con plantillas, cuadre, actas y activación; documentos verificables; centro de informes;
    123 permisos `Lending.*`; auditoría encadenada.
- **Lo que asume crédito**: el contenido de la versión de la línea (modalidad y segmento SARC
  obligatorios, usura, mora, amortización, gracia, prelación, requisitos del deudor), el libro (apunta al
  crédito y a sus conceptos), la posición diaria (columnas de crédito), el cierre mensual (SARC y
  deterioro) y la matriz (26 roles, ninguno de depósito, interés por pagar ni gasto por intereses). **No
  existe una clase o naturaleza de línea.**
- **Ya previsto para aportes**: el puerto `IConsultaDeAportes` (hoy «no disponible»), la plantilla de
  saldos de aportes de D19, la clase de recaudador `Contributions`, los aportes como garantía y como
  requisito de la línea, y las rutas de pantalla `/cartera/ahorros*`, `/cartera/cdt*`, `/cartera/aportes`
  reservadas.

### Quién depende de las captaciones

- **La misma Cartera**: cupo por múltiplo de aportes, mínimo de aportes, excepción de arrastre y
  descuento del deterioro (D19 de la 013 los dejó en espera de un saldo de aportes real).
- **Nómina (010)**: los descuentos de aportes y de ahorro son el recaudo habitual en fondos de empleados
  y cooperativas con libranza; hoy no existen.
- **COOFLOPAL**: sale el 01/12/2026 con créditos y aportes; el cobro de sus cuotas de aportes depende de
  esta feature y de Recaudo.
- **Recaudo y Tesorería** (sin especificar): la caja, los medios de pago y los giros.

### La norma

Resumen; el detalle con fuentes y marcas de validación está en la investigación.
- **Quién capta**: sólo con autorización de la Supersolidaria y, en multiactivas y fondos de empleados,
  sólo con asociados (Ley 454 de 1998, art. 39; Decreto Ley 1481 de 1989 reformado por la Ley 2496 de
  2025). En fondos de empleados la cuota obligatoria de aportes más ahorro permanente no supera el 10 %
  del ingreso y al menos una décima parte va a aportes.
- **CDAT**: no es título valor (título de legitimación, no negociable); plazo mínimo, redención
  anticipada, prórroga y tasa de prórroga los fija el reglamento de cada entidad; la prórroga por un
  término igual es la práctica y puede pactarse. El CDT (bancos) se prorroga por un término igual y no se
  redime antes.
- **Aportes**: garantía de las obligaciones del asociado e inembargables (Ley 79, art. 49);
  revalorización con cargo al fondo (art. 54, Decreto 3081 de 1990); devolución sólo al retiro, por
  exceso de participación o amortización, sin perforar el mínimo irreducible (Carta Circular 02 de
  2019); patrimonio por la excepción NIIF del Decreto 2496 de 2015.
- **Retención en la fuente** (UVT 2026 = 52.374): CDAT 4 % (Decreto 572 de 2025, art. 1, nunca
  suspendido); ahorro a la vista 7 %; en el sector solidario no se retiene si el interés **diario** es
  menor de 0,055 UVT (2.880,57) y, si lo alcanza, se retiene sobre el total (DUR 1625, art. 1.2.4.2.6;
  su aplicación al CDAT tras el Decreto 572, a validar); componente inflacionario 55,43 % para 2025
  (Decreto 898 de 2026), política a validar; se retiene en el pago o abono, antes de capitalizar.
- **GMF**: las vigiladas por la Supersolidaria son agentes retenedores; exención de retiros de ahorro
  hasta 350 UVT al mes (18.330.900 en 2026), por cuenta marcada o por titular según la práctica vigente
  (art. 881-1, a validar); la cancelación de un depósito a término por abono en cuenta está gravada sobre
  capital más intereses, y el traslado entre ahorro y CDAT del mismo titular no está exento según un
  concepto de 2011 (a validar).
- **Fondo de liquidez** (Decreto 1068 de 2015 con el Decreto 976 de 2026): 10 % de los depósitos en
  básicas e intermedias; por bandas de madurez residual en plenas; ahorro permanente al 2 % o 10 % según
  admita retiros parciales.
- **Reportes**: SICSES (formato 18 de captaciones, 169 de saldos diarios, fondo de liquidez); exógena 1019
  y 1020; certificados de retención y tributario anual.
- **Embargos y fallecimiento**: límite inembargable de los depósitos de ahorro (CGP art. 594) y entrega
  sin juicio de sucesión (EOSF art. 127), con alcance discutido para las solidarias (a validar).
- **SARLAFT**: conocimiento del asociado y origen de fondos; su gestión es de su propia feature.

### El calendario

Ninguna fecha queda fijada. Por decisión del dueño (Q1 y Q3):
- esta feature se construye **cuando termine la Cartera financiera** (la 013 completa) y no la retrasa;
- **todos los módulos se terminan antes de que salga COOFLOPAL**: créditos, captaciones y aportes,
  Recaudo y Tesorería. COOFLOPAL no sale con lo heredado ni con soluciones provisionales;
- no se construye ventanilla provisional: sin Recaudo y Tesorería no sale en vivo ninguna línea.

## Clarifications

### Session 2026-10-14

- Q: ¿Dónde se escribe la integración de ahorros, CDAT y aportes en la Cartera financiera?
  → A: **En esta feature, como extensión de la Cartera** (mismo módulo, mismas líneas con naturaleza,
  motores, contrato, matriz y permisos), entregada después de K1. La especificación 013 no se toca
  mientras se termina K1; lo que la 013 debe cambiar va como enmienda E-01 (FR-121).

- Q: **Q1 · Aportes de COOFLOPAL al 01/12/2026.** ¿Con qué maneja sus aportes, si esta feature empieza
  después de la Cartera?
  → A: **Se terminan todos los módulos antes de que salga COOFLOPAL.**
  - COOFLOPAL sale con la Cartera completa, incluidos los aportes de esta feature (K7) y Recaudo y
    Tesorería; no usa el código heredado ni la plantilla de saldos de aportes como solución de salida.
  - La consulta de aportes de la Cartera es real desde su activación (FR-094); la plantilla de saldos de
    D19 queda sólo como insumo de la migración (FR-113).
- Q: **Q2 · Lo conservado que no es ahorro.** ¿Qué se hace con las 38 entidades conservadas y el módulo
  CDT heredado?
  → A: **Se quita lo que sea necesario y se renueva todo lo que se necesite.**
  - Esta feature retira **todo** lo conservado de ahorros, aportes y CDT (las 38 entidades y las tablas
    `CDT_*`), con la guarda destructiva (FR-119).
  - Lo que captaciones y aportes necesitan se **renueva** aquí: cuentas, titulares, firmas y beneficiarios,
    bloqueos y embargos, retiro del asociado y sus estados, tipos de movimiento, parámetros de efectivo y
    origen de fondos, decisiones de asamblea (sobre los órganos y sesiones de la Cartera) y la firma y foto
    del titular (como adjuntos).
  - Lo que reemplazan otros módulos o la plataforma no se renueva aquí: caja, cajero, base de caja,
    chequeras y canje (Recaudo); listas negras (la carga de listas restrictivas); zonas (oficinas y
    DIVIPOLA, D33).
  - Auxilios, subsidios, actividades sociales, enfermedades y bienes de la persona se retiran: si una
    cooperativa los necesita, se renuevan en su propia feature (bienestar social). Si el diagnóstico
    encuentra filas en ellas, la guarda impide el retiro hasta que el dueño decida.
- Q: **Q3 · Recaudo y Tesorería.** ¿Se espera a esas features o se hace una ventanilla provisional?
  → A: **Se espera a que termine la Cartera financiera.**
  - Sin ventanilla provisional; en prueba, el recaudador y la tesorería de prueba de la Cartera.
  - Ninguna línea de captación ni de aportes sale en vivo sin Recaudo y Tesorería (FR-093).
  - El orden es: la 013 completa, luego esta feature (P21 resuelta).
- Q: **P1, P25 y P27 · Valores contables y tributarios.** ¿Cuenta de intereses por pagar, GMF y componente
  inflacionario?
  → A: **Todo configurable, partiendo de la normatividad colombiana.**
  - **P1**: la plantilla de la matriz sugiere las cuentas del CUIF (210595, 211095, 212595, 213095 y las
    de la investigación); la contadora las confirma o cambia.
  - **P25 · GMF**: por defecto, como dice la norma, **una sola cuenta de ahorro exenta por persona**
    (cuenta marcada), y la redención de un CDAT abonada en cuenta y el traslado entre ahorro y CDAT
    gravados. Todo queda **configurable con vigencia** —tarifa, tope de exención, política de exención y
    qué operaciones son hecho generador— incluida la **tarifa cero o la desactivación del impuesto**,
    porque el Gobierno estudia eliminarlo o modificarlo (FR-022, FR-036).
  - **P27 · Componente inflacionario**: por defecto, como dice la norma, se descuenta al retener a las
    personas naturales no obligadas a llevar contabilidad, con el último porcentaje publicado; política y
    porcentaje configurables con vigencia (FR-021).

- Q: ¿Dónde nace el retiro del asociado? → A: **Todo el trámite en la Cartera**: solicitud, aprobación
  por el órgano con acta, liquidación, cruce y devolución; al aprobarse, la Cartera avisa a Asociados para
  cambiar el estado (FR-076, FR-124, D16).
- Q: ¿Las cuotas de aportes y ahorros se cobran también por las pagadurías? → A: **Sí, y todo configurable
  por convenio de pagaduría**: archivos separados por grupo de conceptos o uno solo, agrupación de varias
  líneas (de crédito, captación y aportes) bajo un concepto equivalente de la pagaduría, y formato,
  equivalencias y prelación propios de cada pagaduría (FR-125, E-05).
- Q: ¿Se negocian tasas de CDAT fuera de la tabla de la línea? → A: **Sí, hasta un margen sobre la tabla,
  con aprobación por la matriz de atribuciones**, configurable por línea, antigüedad del asociado, tasa,
  plazo, monto y demás criterios (FR-127).
- Q: ¿Las cuentas de ahorro cobran cuotas de manejo o comisiones? → A: **Catálogo de cargos configurable
  por línea, apagado por defecto**, con exoneraciones, débito a la cuenta y el IVA facturado por Comercial
  cuando el cargo esté gravado (FR-128).
- Q: ¿Cómo se abren las cuentas de menores de edad? → A: **A nombre del menor como titular, manejada por
  su representante legal** con soporte de la representación; a los 18 años el control pasa al titular
  automáticamente, con aviso y actualización de datos (FR-033).

## Conflictos con la constitución y con decisiones vigentes

La especificación no modifica la constitución.

### Con la constitución

- **I-1 · Principio V.** Titulares, cotitulares, firmantes, representantes y beneficiarios son personas
  de `COR_People`, enlazadas por su identificador. El beneficiario que no es persona se da de alta con el
  buscador común; lo heredado que enlaza por código se migra resolviendo la persona o se rechaza por fila.
- **I-2 · Principio XI.** Se lee extendido al libro de captaciones y aportes, como la 013 lo extendió al
  del crédito: no se edita ni se borra; se corrige por reverso.
- **I-3 · Principio VII.** El cierre de una cuenta es un estado, no un borrado.
- **I-4 · Principio X.** Toda operación monetaria queda en la auditoría encadenada; la gestión SARLAFT no
  es de esta feature.
- **I-5 · Prefijo de tablas.** Las tablas nuevas usan el prefijo de la Cartera (`LND_`) sin reutilizar
  ningún nombre heredado; ya no hace falta un prefijo nuevo.

| Principio | Cómo lo cumple |
|---|---|
| I. Spec-first | Esta especificación antes de plan, tareas y código |
| II. Clean Architecture | Motores puros en dominio (FR-020 a FR-026); la Cartera sólo se toca por su lista cerrada de interfaces (FR-008) |
| III. CQRS | Toda operación es comando o consulta con su validador |
| IV. Base por cooperativa | Procesos por cooperativa en el conductor de la Cartera (FR-031); valores nacionales replicados |
| V. Persona maestra | I-1 |
| VI. PublicId | Cuentas, CDAT, transacciones y documentos sólo por identificador público y número |
| VII. Borrado lógico | I-3 |
| VIII. Validación dual | Mensajes en español con código por espacio (FR-013) |
| IX. Errores visibles | Fallas de proceso con alerta y estado conocido (FR-013, FR-031) |
| X. SARLAFT | I-4, FR-003 |
| XI. Inmutabilidad | I-2, FR-001, FR-002 |
| XII. Migraciones | Retiro de lo heredado con diagnóstico, respaldo y segundo revisor (FR-119) |

### Con decisiones vigentes de otras features

- **C1 · 013, frontera `Lending`.** `ElEspacioLendingEsSoloCartera` deja lo conservado fuera de `Lending`.
  **Resuelto**: las captaciones nuevas son Cartera y viven en `Lending`; lo conservado se retira (FR-119)
  y la prueba se enmienda (E-01).
- **C2 · 013, D19.** **Resuelto**: la consulta de aportes la responde la propia Cartera desde la
  activación de aportes en cada cooperativa (FR-094), antes de que salga ninguna cooperativa (Q1); la excepción de arrastre y el descuento de aportes
  en el deterioro se habilitan por parámetro, no automáticamente.
- **C3 · 013, clase `Contributions` y C37.** El cruce de aportes al retiro deja de ser un recaudador
  externo: es una **operación interna** de la Cartera con medio «cruce interno» (FR-077). La clase queda
  sin uso nuevo y C37 pierde objeto.
- **C4 · 013, rutas reservadas.** Las rutas de pantalla `/cartera/ahorros*`, `/cartera/cdt*`,
  `/cartera/aportes` y demás pasan a ser de la Cartera; el CDT se llama CDAT (`/cartera/cdat*`, D1).
- **C5 · 013, C122.** Las rutas conservadas sólo exigen sesión; se retiran sin alias (FR-120).
- **C6 · 010, Nómina.** **Resuelto** con la enmienda E-02: conceptos de descuento de aportes y ahorros
  por el contrato de la Cartera, como `DESC_CARTERA`.
- **C7 · 009, Contabilidad.** **Resuelto** con la enmienda E-03: hechos y roles nuevos en la matriz de la
  Cartera; no hay un tercer emisor.
- **C8 · 012, medios de pago.** La caja y los medios son de Recaudo; no se amplía la caja del punto de
  venta.

## Alcance

### Dentro de esta feature

- **Naturaleza de la línea**: el catálogo de líneas de la Cartera admite tres naturalezas —colocación
  (las líneas de crédito de la 013, sin cambios), **captación** y **aportes**—, con una identidad común y
  una versión de condiciones propia de cada naturaleza.
- **Líneas de captación** por clase fija: ahorro a la vista, ahorro contractual o programado, ahorro
  permanente y CDAT (el ERP actual lo llama «CDT»).
- **Línea de aportes**: cuenta individual con capital mínimo no reducible y aportes temporalmente
  restringidos, cuota periódica, aportes extraordinarios, revalorización y distribución de excedentes
  por decisión de asamblea, amortización y devolución.
- Apertura con titulares, cotitulares, firmantes, representantes de menores y beneficiarios.
- **Operaciones de depósito** en el contrato de la Cartera (consultar, exigibles de cuotas, simular,
  consignar, retirar, trasladar, reversar) para los mismos recaudadores.
- Causación diaria en el ciclo diario de la Cartera y liquidación con retención y GMF.
- CDAT: constitución, plan de intereses (vencido o anticipado), vencimiento, días de gracia, prórroga,
  renovación con condiciones nuevas, redención anticipada con penalidad, cancelación y título no
  negociable.
- Bloqueos, embargos, fallecimiento con entrega a beneficiarios, inactividad y no reclamados.
- Retiro del asociado con cruce interno contra sus créditos y devolución del remanente.
- Débito automático de cuotas de crédito desde una cuenta de ahorro, como operación interna.
- Fondo de liquidez y depósitos asegurados.
- Hechos, roles y dimensiones nuevos en la matriz contable de la Cartera y conciliación.
- Documentos al titular y reportes regulatorios (SICSES, exógena).
- Migración con plantillas, cuadre y activación **por naturaleza**.
- Retiro de todo lo heredado de ahorros, aportes y CDT, y renovación de lo que captaciones y aportes
  necesitan (Q2).
- Enmiendas a la 013 (frontera, contrato, matriz, pruebas), 010, 009 y 008.

### Fuera de esta feature

- **La caja, las ventanillas y los medios de pago**: son de Recaudo; no hay ventanilla provisional (Q3).
- **El giro del dinero**: es de Tesorería; la Cartera registra la orden.
- **La gestión SARLAFT** (segmentación, monitoreo, ROS, UIAF): la Cartera consulta listas, exige la
  declaración de origen de fondos cuando el parámetro lo pide y deja los datos de cada operación.
- **Cuenta corriente, chequeras, canje propio, tarjeta débito, cajeros y canales electrónicos**.
- **Un portal del asociado** (como D1 de la 013).
- **Auxilios, subsidios, actividades sociales y bienestar**: sus tablas heredadas se retiran; si se
  necesitan, se renuevan en su propia feature (Q2).
- **Declarar y pagar** el GMF y la retención: es de Contabilidad y Tesorería con los hechos de la Cartera.
- **Inscripción en Fogacoop y pago de la prima**: sólo se registra si la cooperativa está inscrita.
- **CDT negociables**: la cooperativa emite CDAT; no se modelan endosos ni depósito de valores.

### Entregas sugeridas

Continúan la numeración de la Cartera. Ninguna lleva fecha; todas son de esta feature y se construyen
completas y configurables. Van **después de terminar la 013** (K1 a K6b) y **todas** antes de la salida
de COOFLOPAL (Q1, Q3).

| Entrega | Contenido | Historias |
|---|---|---|
| K7 · Aportes | Naturaleza de la línea y frontera enmendada, línea y cuenta de aportes, cuotas, consulta de aportes real (fin de D19), descuentos por nómina y por pagadurías configurables (FR-125), operaciones de depósito del contrato para aportes, hechos y roles contables, migración y activación de aportes, retiro del asociado con cruce interno, constancias, permisos | US1 (aportes), US2 (aportes), US3, US8, US9 (aportes), US10, US16 |
| K8 · Ahorro a la vista y permanente | Líneas de captación, apertura con titulares y beneficiarios, contrato completo, causación y liquidación con retención y GMF, débito automático, bloqueos y embargos, extractos y certificados, migración y activación de captaciones | US1, US2, US4, US5, US9, US11, US13 (salvo CDAT) |
| K9 · CDAT y contractual | Constitución, plan de intereses, vencimiento, días de gracia, prórroga y renovación, redención anticipada, título; ahorro contractual con bonificación | US6, US7 |
| K10 · Asamblea, inactividad y fallecimiento | Revalorización y excedentes, inactividad, no reclamados, entrega a beneficiarios | US12, US14 |
| K11 · Liquidez e informes | Fondo de liquidez, depósitos asegurados, SICSES, exógena, informes de gestión | US15, US17 |

Reglas de convivencia:
- **Sin K8 no hay captación.** Hasta K8, una cooperativa autorizada a captar no activa líneas de
  captación; la de aportes se activa sola.
- **Ahorro permanente antes de K8.** En los ambientes de prueba, si un fondo de empleados activa K7 antes
  de K8, su ahorro permanente sigue en la plantilla de saldos para la consulta. En producción no se
  activa nada hasta tener K7 a K11 (Q1).
- **Datos migrados de entregas posteriores** (prórrogas pactadas, beneficiarios, embargos, marcas de
  inactividad) se conservan como dato desde la carga.

### Glosario

**Líneas y cuentas**
- **Naturaleza de la línea**: colocación (crédito), captación (dinero que el asociado deposita) o aportes
  (capital social). Decide qué versión de condiciones, qué libro y qué motores usa la línea.
- **Línea de captación**: línea de clase fija (a la vista, contractual, permanente, CDAT) con condiciones
  versionadas.
- **Cuenta**: el contrato de un titular en una línea de captación o de aportes, con número propio y
  estado (en apertura, activa, inactiva, bloqueada, embargada, en cancelación, cancelada).
- **CDAT**: depósito de ahorro a término de una cooperativa; título de legitimación, no negociable.
- **Cuenta de aportes**: la cuenta individual del asociado en el capital social, con **mínimo no
  reducible** y aportes ordinarios, extraordinarios y revalorizados. No causa intereses.
- **Titular, cotitular, firmante, representante y beneficiario**: roles de personas en una cuenta;
  manejo **indistinto** o **conjunto**.

**Movimientos y fechas**
- **Transacción**: movimiento del libro de una cuenta con tipo, concepto, valor y tres fechas (valor,
  contable y registro), igual que en el crédito.
- **Saldo disponible**: saldo menos bloqueos, embargos, canje pendiente y saldo mínimo.
- **Reverso**: transacción contraria enlazada a la original.

**Intereses e impuestos**
- **Método de saldo**: diario (el interés de cada día sobre el saldo de ese día), promedio o mínimo del
  período.
- **Tramos**: escalonados (cada tramo a su tasa) o por saldo total (todo a la tasa del tramo donde cae).
- **Causación** y **liquidación**: reconocimiento diario del interés por pagar y su abono (o pago, en el
  CDAT), neto de retención.
- **Umbral diario de retención**: interés diario mínimo (0,055 UVT en el sector solidario) desde el cual
  se retiene sobre el total.
- **GMF** y su **exención**: 4 por mil sobre las disposiciones; exentos los retiros de ahorro hasta 350
  UVT al mes por cuenta marcada o por titular, según la política vigente.
- **Componente inflacionario**: parte del rendimiento que no es renta para personas naturales no
  obligadas a llevar contabilidad.

**CDAT**
- **Plan de intereses**: fechas, días e interés de cada período, vencido o anticipado.
- **Días de gracia**: días después del vencimiento para redimir o cambiar condiciones sin que la prórroga
  se consolide.
- **Prórroga**: renovación automática por el mismo plazo con la regla de tasa de la línea.
- **Renovación**: título nuevo con condiciones nuevas que cancela el anterior.
- **Plazo de emisión**: rango del plazo pactado (menos de 6 meses, 6 a 12, 12 a 18, 18 o más) que decide
  la cuenta contable del capital.

**Aportes**
- **Revalorización**, **excedentes** y **devolución**: como en la 013 y la norma (ver investigación).

## Interfaces

Las captaciones **son** la Cartera: no tienen interfaces propias entre sí. Lo que cambia es la lista
cerrada de la Cartera (FR-008 de la 013), por enmienda E-01.

**La Cartera ofrece además**

| Interfaz | A quién | Qué hace |
|---|---|---|
| Consulta de aportes y ahorro permanente (`IConsultaDeAportes`), ahora real | La propia Cartera y quien la consulte hoy | FR-094 |
| Consulta de productos de captación y aportes de una persona | Personas, Asociados, Nómina | FR-095 |
| Mensaje «retiro de asociado aprobado» (con fecha y causa) para cambiar el estado | Asociados | FR-076 |
| Operaciones de depósito en el contrato con recaudadores (consultar, exigibles de cuotas, simular, consignar, retirar, trasladar, reversar) | Recaudo, Tesorería, Nómina y, en prueba, el recaudador de prueba | FR-086 a FR-093 |
| Órdenes de giro de retiros por transferencia, devoluciones, entregas a beneficiarios, intereses de CDAT a terceros y consignaciones a juzgados | Tesorería | FR-091 |
| Mensaje «consignación reversada por la Cartera» | El recaudador de la consignación | FR-092 |
| Mensajes «cargo de captación causado» y «cargo reversado» (para facturar el cargo gravado y su nota) | Comercial | FR-128 |
| Hechos de captaciones y aportes (mismo emisor `CAR`) | Contabilidad | FR-096 a FR-100 |

**La Cartera consume además**

| Interfaz | De quién | Para qué |
|---|---|---|
| Descuentos por nómina aplicados en la misma unidad de trabajo | Nómina | FR-089 |
| Resultado del giro | Tesorería | FR-091 |
| Estado de asociado | Asociados | FR-030 |
| Perfil tributario de la persona | Plataforma (012) | FR-063 |
| Valores nacionales nuevos (UVT, componente inflacionario, límite inembargable, tope de entrega sin sucesión, IPC) | Plataforma | FR-015 |

Todo lo demás (personas, listas restrictivas, habeas data, adjuntos, aprobaciones, parámetros, archivos
planos, notificaciones, auditoría, plantillas, ¿es contabilizable?, saldos contables, estado del
período) ya está en la lista de la Cartera.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Parametrizar líneas de captación y de aportes con vigencia (Priority: P1)

El jefe de ahorros configura, sin programar, en el **mismo catálogo de líneas de la Cartera**, las líneas de
naturaleza captación y aportes junto a las de crédito:
- para cada línea de captación, su clase fija (a la vista, contractual, permanente, CDAT), su código de catálogo y
  sus condiciones: tasa (fija o por tramos de saldo o de plazo), método de saldo (diario, promedio o mínimo), saldo
  mínimo para abrir, para mantener y para ganar interés, convención de días, periodicidad de liquidación, retención y GMF, montos máximos
  de retiro y de efectivo, días para inactividad y si admite cotitulares, menores o retiros parciales;
- para la línea de aportes, el capital mínimo no reducible por asociado, la cuota periódica (valor fijo,
  porcentaje del ingreso o fracción del salario mínimo), su periodicidad y su tope.

Cada cambio crea una versión con fecha desde. El tipo de entidad de la cooperativa decide qué clases
puede activar.

**Why this priority**: sin líneas no hay apertura, ni migración, ni consulta para Cartera. Las
condiciones son la base de todo cálculo y deben poder reproducirse a cualquier fecha.

**Independent Test**: crear una línea a la vista con tasa por tramos de saldo y una línea de aportes
con cuota del 3 % del ingreso, cambiar la tasa con fecha futura y comprobar que la consulta a una fecha
anterior devuelve la versión anterior.

**Acceptance Scenarios**:

1. **Given** una cooperativa cuyo perfil regulatorio no la autoriza a captar, **When** el jefe de ahorros
   intenta activar una línea a la vista, **Then** el sistema lo rechaza indicando el perfil vigente y
   que sólo puede ofrecer aportes; no se guarda nada.
2. **Given** una línea de captación activa, **When** el jefe de ahorros cambia su tasa desde el día 15, **Then** se
   crea una versión nueva, la causación de los días anteriores usa la tasa anterior y la de los
   siguientes la nueva, sin tocar lo ya causado.
3. **Given** un fondo de empleados, **When** se configura una cuota obligatoria de aportes más ahorro
   permanente que supera el tope legal del ingreso del asociado, **Then** el sistema lo rechaza con el
   tope vigente y su fuente.
4. **Given** un código de línea que ya existe en el catálogo de la Cartera (de cualquier naturaleza), **When** se crea otro con el mismo
   código, **Then** responde «código duplicado» con el nombre del existente, antes de guardar.

---

### User Story 2 - Abrir cuentas con titulares, firmantes y beneficiarios (Priority: P1)

El asesor abre, para un asociado, una cuenta de aportes o de una línea de captación en tres pasos o
menos:
- busca a la persona con el buscador común (o la da de alta);
- el sistema valida que sea asociado vigente, que tenga al día el conocimiento del asociado y la
  autorización de tratamiento de datos, y la consulta contra listas restrictivas;
- registra cotitulares, firmantes y el tipo de manejo (indistinto o conjunto), el representante si es
  menor de edad, y los beneficiarios con su porcentaje;
- marca, si el titular lo pide, la cuenta exenta de GMF.

La cuenta nace con número propio, estado «activa» (o «en apertura» si exige aprobación o depósito
inicial) y las condiciones vigentes de la línea.

**Why this priority**: es la puerta de todo producto. Sin titularidad clara no hay retiro, embargo ni
entrega por fallecimiento correctos.

**Independent Test**: abrir una cuenta a la vista conjunta con dos titulares y dos beneficiarios al 60 %
y 40 %, marcarla exenta de GMF y comprobar que una segunda cuenta exenta del mismo titular se rechaza.

**Acceptance Scenarios**:

1. **Given** una persona que no es asociada, **When** el asesor intenta abrirle una cuenta de ahorro en
   una cooperativa que sólo capta de asociados, **Then** el sistema lo impide y ofrece vincularla primero.
2. **Given** una coincidencia en listas restrictivas, **When** se intenta abrir, **Then** la apertura
   queda retenida, se avisa al oficial de cumplimiento y sólo él la libera, con motivo.
3. **Given** beneficiarios cuyos porcentajes suman 90 %, **When** se guarda, **Then** se rechaza indicando
   que deben sumar 100 %.
4. **Given** un titular con una cuenta ya marcada exenta de GMF, **When** marca otra, **Then** se rechaza
   con la cuenta marcada vigente, salvo que la cooperativa tenga habilitado el reparto de la exención.
5. **Given** dos asesores que abren cuentas de la misma línea al mismo tiempo, **When** ambas se
   confirman, **Then** reciben números distintos y consecutivos sin hueco por concurrencia.

---

### User Story 3 - Aportes: cuota periódica, recaudo y consulta para Cartera (Priority: P1)

Cada período el sistema genera la cuota exigible de aportes de cada asociado según su línea. Nómina,
Recaudo o el débito automático la pagan por el contrato de la Cartera; el asociado también puede hacer
aportes extraordinarios. El saldo se separa entre mínimo no reducible y el resto. Los motores de
crédito de la misma Cartera (cupo, arrastre, deterioro) leen el saldo de aportes y de ahorro permanente
de una persona a una fecha, y dejan de usar la plantilla de saldos.

**Why this priority**: es lo que COOFLOPAL necesita en vivo (Q1) y lo que libera las tres restricciones
de D19 en Cartera.

**Independent Test**: generar la cuota de enero de un asociado, pagarla por el recaudador de prueba,
consultar el saldo desde la consulta de Cartera y comprobar que coincide con el extracto de aportes.

**Acceptance Scenarios**:

1. **Given** un asociado con cuota mensual de 50.000 sin pagar de dos meses, **When** Recaudo lista los
   exigibles, **Then** recibe dos cuotas con su período y valor, y puede pagar una, las dos o un valor
   mayor que va como aporte extraordinario si la línea lo admite.
2. **Given** un pago por nómina que no alcanza la cuota, **When** Nómina lo aplica, **Then** la cuota
   queda parcialmente pagada y el saldo pendiente sigue exigible.
3. **Given** Aportes activado en la cooperativa, **When** Cartera pide el saldo de aportes de una persona,
   **Then** recibe aportes, mínimo no reducible y ahorro permanente a esa fecha, y la plantilla de saldos
   de aportes deja de consultarse para esa cooperativa.
4. **Given** un asociado en un fondo de empleados, **When** se genera su cuota, **Then** se reparte entre
   aportes y ahorro permanente en la proporción de la línea, respetando la fracción mínima a aportes.

---

### User Story 4 - Consignar, retirar y trasladar por el contrato de la Cartera (Priority: P1)

Recaudo (en ventanilla), Tesorería (transferencias recibidas y giros) y Nómina (descuentos) mueven
dinero en las cuentas sólo por las operaciones de depósito del contrato de la Cartera, el mismo que usan
para pagar créditos; el débito automático de cuotas de crédito es una operación interna:
- **consultar** el saldo disponible y las condiciones;
- **simular** un retiro: valida firmas y manejo, disponible, bloqueos, embargos, montos máximos y
  atribución, y calcula el GMF;
- **consignar**, **retirar** y **trasladar** entre cuentas;
- **reversar** una operación propia el mismo día o dentro de la ventana configurada.

Cada operación es idempotente y deja una transacción con sus tres fechas.

**Why this priority**: sin movimientos no hay producto. Que todo entre por un solo contrato evita las
rutas paralelas que hoy dejan saldos incoherentes.

**Independent Test**: consignar 1.000.000 en efectivo por el recaudador de prueba, retirar 400.000 de una
cuenta exenta y comprobar que el GMF es cero hasta el tope mensual en UVT y se cobra desde el excedente.

**Acceptance Scenarios**:

1. **Given** una cuenta conjunta de manejo conjunto, **When** se simula un retiro con la firma de un solo
   titular, **Then** el contrato lo rechaza indicando las firmas que faltan.
2. **Given** un retiro mayor que el disponible, **When** se simula, **Then** se rechaza mostrando el saldo
   total, los bloqueos y embargos y el disponible.
3. **Given** una cuenta no exenta, **When** se retiran 400.000, **Then** el GMF de 1.600 se calcula, se
   descuenta y queda como transacción propia enlazada al retiro.
4. **Given** una consignación aplicada, **When** el recaudador repite la operación con la misma clave,
   **Then** recibe el comprobante original y no se consigna dos veces.
5. **Given** un retiro que supera la atribución del cajero, **When** se intenta, **Then** queda en espera
   de un aprobador presente con segundo factor (la misma matriz de atribuciones de la Cartera) y no mueve
   saldo hasta aprobarse.
6. **Given** una consignación del día ya usada en un retiro posterior, **When** se intenta reversar,
   **Then** se rechaza si deja la cuenta en negativo, indicando el retiro que lo impide.

---

### User Story 5 - Causar y liquidar intereses con retención en la fuente (Priority: P1)

El ciclo diario de la Cartera causa cada día el interés de cada cuenta según el método de saldo de su
línea y la tasa vigente, sin que nadie lo lance, aunque la cooperativa no haya operado. En la periodicidad
de la línea liquida: abona el
interés neto a la cuenta (o lo paga, en el CDAT), descuenta la retención en la fuente cuando el
interés diario alcanza el umbral (y entonces sobre el total), y emite los hechos. Una liquidación puede simularse a cualquier fecha.

**Why this priority**: es el corazón financiero del producto; hoy liquida mal, duplica y nunca retiene.

**Independent Test**: con los casos dorados de la feature (saldos diarios, cambio de tasa a mitad de mes,
retiro el día 20, base mínima de retención), comprobar que la causación diaria y la liquidación mensual
coinciden al centavo con el valor calculado a mano.

**Acceptance Scenarios**:

1. **Given** una cuenta con saldo variable durante el mes, **When** se liquida, **Then** el interés es la
   suma de los intereses diarios sobre cada saldo diario, con la convención y la equivalencia de tasa de
   la línea (ejemplo de la investigación: 6.884 en el mes).
2. **Given** un saldo diario por debajo del mínimo para interés, **When** se causa ese día, **Then** el
   interés del día es cero y el extracto lo explica.
3. **Given** una liquidación ya hecha para el período, **When** se vuelve a lanzar, **Then** no duplica:
   responde con la liquidación existente.
4. **Given** un interés diario que alcanza el umbral de 0,055 UVT, **When** se liquida, **Then** se
   retiene la tarifa vigente de la clase de línea sobre el **total** gravable (descontando el componente
   inflacionario si la política lo dice), se abona el neto y la retención queda para el certificado; si
   ningún día alcanza el umbral, no se retiene.
5. **Given** un día no causado porque el proceso falló, **When** el proceso vuelve, **Then** se pone al día
   desde el último día causado y alerta al jefe de ahorros del atraso, como en el ciclo de los créditos.

---

### User Story 6 - Constituir, renovar y redimir un CDAT (Priority: P2)

El asesor constituye un CDAT con monto, plazo y la tasa de la línea vigente para ese plazo y monto; el
sistema genera el plan de pago de intereses (vencido o anticipado; periódico o al vencimiento) y el título
no negociable. Al vencer, el titular tiene los días de gracia de la línea para redimir o cambiar
condiciones; pasados, si no dio otra instrucción, se **prorroga** por el mismo plazo con la regla de tasa
de la línea, o se **cancela** y se paga (a una cuenta de ahorro o por orden de giro). La **renovación** con
condiciones nuevas emite un título nuevo. La redención anticipada sólo procede si la línea la admite, con
la penalidad pactada.

**Why this priority**: es el producto de mayor saldo en las cooperativas que captan; COOFLOPAL no lo
usa, por eso es P2.

**Independent Test**: constituir un CDAT de 10.000.000 a 180 días al 10 % EA en base 360 con pago
mensual vencido, comprobar los seis pagos de 79.741 (478.446 en total) y la retención de cada uno según el
umbral, dejarlo vencer sin instrucción y comprobar la prórroga con la tasa nueva.

**Acceptance Scenarios**:

1. **Given** un plazo fuera del rango de la línea, **When** se constituye, **Then** se rechaza con el
   rango vigente.
2. **Given** un CDAT con pago mensual de intereses a una cuenta de ahorro, **When** llega cada fecha de
   pago, **Then** se paga el interés neto de retención en esa cuenta y se emite el hecho.
3. **Given** un CDAT vencido sin instrucción, **When** pasan los días de gracia, **Then** se prorroga con
   la regla de tasa de la línea, capitalizando o no los intereses según lo pactado (la retención va antes
   de capitalizar), queda un título nuevo enlazado al anterior y el titular recibe el aviso.
4. **Given** una línea que no admite redención anticipada, **When** se pide redimir, **Then** se rechaza;
   si la admite, se simula la penalidad y se muestra antes de confirmar.
5. **Given** un CDAT embargado, **When** vence, **Then** no se paga: se prorroga o queda a disposición del
   juzgado según la orden.
6. **Given** un asociado con cinco años de antigüedad que constituye 200.000.000 a 360 días y una regla que
   permite hasta 1,5 puntos sobre la tabla para ese perfil, **When** el asesor pide 1,2 puntos y su
   atribución llega a 0,5, **Then** el CDAT queda en espera de un aprobador con atribución suficiente;
   aprobado, se sella con la tasa de la tabla, el margen, la regla y el aprobador; si pide 2 puntos, se
   rechaza indicando el máximo de la regla.

---

### User Story 7 - Ahorro contractual o programado (Priority: P2)

El asociado pacta una cuota periódica durante un plazo con una tasa (a veces bonificada al cumplir).
Las cuotas se recaudan como los aportes; el incumplimiento según la línea quita la bonificación o
cancela el contrato y devuelve lo ahorrado con la tasa de penalidad.

**Why this priority**: producto habitual pero secundario frente al ahorro a la vista y el CDAT.

**Independent Test**: pactar 12 cuotas mensuales de 200.000 al 6 % EA con bonificación al 8 % EA:
cumpliendo, el valor final es 2.486.777; pagando sólo 10, la liquidación aplica la tasa de incumplimiento
y no la bonificada.

**Acceptance Scenarios**:

1. **Given** un contrato con todas las cuotas al día, **When** vence, **Then** se liquida con la tasa
   bonificada y el saldo se traslada a la cuenta o se gira según la instrucción.
2. **Given** más cuotas atrasadas que las toleradas, **When** corre el proceso diario, **Then** el
   contrato pasa a «incumplido» con el efecto de la línea y avisa al titular.

---

### User Story 8 - Contabilidad recibe cada hecho de captaciones y aportes por la matriz de la Cartera (Priority: P1)

Cada cambio de saldo o de clasificación contable de un saldo (consignación, retiro, GMF, interés
causado, interés liquidado, retención, constitución y pago de CDAT, aporte, revalorización, devolución,
traslado a no reclamados, reverso) sale como hecho de la Cartera (el mismo emisor de los créditos) a Contabilidad, sin cuentas.
Contabilidad lo contabiliza por la **misma matriz de la Cartera**, con roles nuevos (capital del depósito,
interés por pagar, gasto por intereses, retención y GMF por pagar, aportes por clase) y las dimensiones de
naturaleza, clase de línea y plazo de emisión, con comprobante en línea o por
lotes resumido, y concilia sus saldos con los de la Cartera.

**Why this priority**: sin contabilidad no se cierra el mes ni se pasa la revisoría fiscal.

**Independent Test**: con la matriz completa, consignar, causar un mes y liquidar; comprobar los
comprobantes y que la conciliación da cero diferencias. Borrar una regla y comprobar que la pantalla de
la Cartera bloquea la operación indicando la regla faltante.

**Acceptance Scenarios**:

1. **Given** la causación diaria por lotes resumida, **When** termina el día, **Then** Contabilidad genera
   un comprobante por día con una línea por combinación de dimensiones y el tercero cuando la cuenta lo
   exige.
2. **Given** un hecho sin regla en la matriz, **When** se confirma una operación desde una pantalla de
   la Cartera, **Then** se bloquea con la regla faltante; desde el contrato (Recaudo, Nómina) se aplica, se
   sella y se alerta al contador.
3. **Given** un mes en el que la Cartera, con captaciones activadas, no ha cerrado su causación, **When** el contador intenta
   cerrar el período, **Then** Contabilidad lo impide indicando el día pendiente.
4. **Given** un comprobante originado en una captación, **When** alguien intenta reversarlo desde Comprobantes,
   **Then** se rechaza: se corrige con un reverso desde la Cartera.

---

### User Story 9 - Migrar los saldos vigentes y cuadrar antes de activar (Priority: P1)

El jefe de ahorros carga por plantillas, con revisión previa y todo o nada, las cuentas vigentes al
corte: titulares, beneficiarios, condiciones, saldos, interés causado no liquidado, retenciones del año,
acumulado de GMF del mes, CDAT con su plan pendiente, cuentas de aportes con su mínimo no reducible,
cuotas atrasadas, bloqueos y embargos. La Cartera compara los saldos con Contabilidad en las cuentas
mapeadas y no se activa con diferencias sin aceptarlas con motivo y acta. Es la misma mecánica de
migración de los créditos, con plantillas propias, y cada cooperativa activa **aportes** y
**captaciones** por separado de los créditos.

**Why this priority**: toda cooperativa llega con saldos; sin migración no hay salida en vivo.

**Independent Test**: cargar 500 cuentas con un error en la fila 37, ver el error por fila y columna sin
guardar nada, corregir, cargar, cuadrar contra las cifras de referencia y activar.

**Acceptance Scenarios**:

1. **Given** una plantilla con un titular inexistente, **When** se revisa, **Then** se lista fila, columna
   y motivo, y no se guarda ninguna cuenta.
2. **Given** saldos cargados que difieren de Contabilidad en una cuenta, **When** se intenta activar,
   **Then** se muestra la diferencia por línea y cuenta, y sólo se activa aceptándola con motivo y acta.
3. **Given** una carga aplicada no activada, **When** se carga otra con corte nuevo, **Then** la anterior
   queda «reemplazada», no se borra, y sirve de ensayo.
4. **Given** saldos migrados, **When** se activan, **Then** cada cuenta nace con una transacción de saldo
   inicial por concepto fechada al corte, sin hechos contables (Contabilidad ya tiene sus saldos).

---

### User Story 10 - Retiro del asociado: cruce con la deuda y devolución (Priority: P1)

Cuando un asociado se retira (por voluntad, exclusión o fallecimiento), todo el trámite vive en la
Cartera: el analista de aportes radica la solicitud con su causa y soportes, el órgano competente (consejo
o junta, con los órganos y sesiones de la Cartera) la aprueba con acta, y al aprobarse la Cartera avisa a
Asociados para cambiar el estado. Luego liquida el retiro: suma aportes y ahorros, ve sus créditos en la misma Cartera, **cruza** primero contra la deuda
como operación interna (medio «cruce interno») según la prelación de los estatutos, descuenta lo que corresponda por
pérdidas no cubiertas, y genera la devolución del remanente por orden de giro o a otra cuenta. Los CDAT
vigentes siguen hasta su vencimiento salvo que la línea diga otra cosa. Lo no reclamado en el plazo
pasa a «no reclamados».

**Why this priority**: la ley afecta los aportes como garantía de la deuda; hoy el retiro marca al
asociado sin mover dinero.

**Independent Test**: asociado con 3.000.000 en aportes, 1.000.000 en ahorro y un crédito de 2.500.000:
liquidar el retiro, comprobar el cruce en el crédito, la devolución de 1.500.000 y los hechos contables.

**Acceptance Scenarios**:

1. **Given** obligaciones mayores que aportes y ahorros, **When** se liquida el retiro, **Then** se cruza
   todo lo disponible, la devolución es cero y el crédito conserva el saldo restante con sus garantías.
2. **Given** un ejercicio con pérdidas que la reserva no cubre, **When** se liquida el retiro, **Then** se
   descuenta la parte proporcional de las pérdidas según el parámetro y la explicación lo muestra.
3. **Given** retiros que en el mes harían bajar el capital social por debajo del mínimo irreducible de la
   entidad, **When** se liquida uno más, **Then** se rechaza la devolución (no el retiro) y se avisa al
   gerente.
4. **Given** el cruce aplicado, **When** se reversa el pago en el crédito, **Then** el reverso arrastra el
   débito de aportes y ahorros en la misma operación y la liquidación queda «por rehacer».

---

### User Story 11 - Bloqueos y embargos con límite inembargable (Priority: P2)

El analista registra bloqueos (por orden interna, garantía de un crédito, firma pendiente) y embargos
(con oficio, juzgado, demandante, valor y límite). El embargo retiene el saldo embargable: lo que exceda
el límite inembargable vigente sumando todas las cuentas de ahorro de la persona, salvo que el oficio sea
por alimentos. Los aportes no se embargan. La consignación al juzgado sale por orden de giro.

**Why this priority**: es obligación legal frecuente, pero no bloquea la salida en vivo de aportes.

**Independent Test**: persona con dos cuentas que suman más que el límite inembargable; registrar un
embargo y comprobar que sólo se retiene el exceso, repartido según el orden configurado.

**Acceptance Scenarios**:

1. **Given** un embargo por alimentos, **When** se registra, **Then** el límite inembargable no aplica.
2. **Given** un embargo sobre aportes, **When** se intenta registrar, **Then** se rechaza citando la
   inembargabilidad legal y se registra la respuesta al juzgado.
3. **Given** un desembargo, **When** se registra, **Then** se libera el saldo retenido y queda la historia.

---

### User Story 12 - Revalorización de aportes y distribución de excedentes (Priority: P2)

El contador registra la decisión de la asamblea (acta, fondo y porcentaje o valor) y el sistema calcula
la revalorización de cada cuenta de aportes según la base elegida (saldo al cierre o promedio), hasta el
límite legal del índice de precios, y la distribución de excedentes a aportes, ahorros o pago. Se simula,
se aprueba y se aplica en una sola operación con sus hechos.

**Why this priority**: ocurre una vez al año, después de la asamblea.

**Independent Test**: revalorizar al 5 % sobre el saldo promedio con un IPC del 4 % y comprobar que se
limita al IPC y que la suma de abonos coincide con el fondo aprobado.

**Acceptance Scenarios**:

1. **Given** una revalorización que supera el fondo aprobado, **When** se simula, **Then** se prorratea al
   fondo y se informa el ajuste.
2. **Given** una distribución ya aplicada, **When** se intenta aplicar de nuevo con la misma acta,
   **Then** se rechaza; para corregir se reversa entera y se registra otra.

---

### User Story 13 - Documentos al titular (Priority: P2)

El titular recibe en la oficina o por correo: extracto del período, certificado de saldo, certificado
tributario anual (saldos al 31 de diciembre, rendimientos, retenciones y GMF), certificado de retención,
título del CDAT y constancia de aportes. Cada documento lleva número, código de verificación y huella,
se guarda como se emitió y queda anulado si un reverso lo invalida.

**Why this priority**: obligación tributaria anual y pedido constante de los asociados.

**Independent Test**: emitir el certificado tributario 2026 de un titular con dos cuentas y un CDAT,
reversar una retención de diciembre y comprobar que el certificado queda anulado y el nuevo cambia.

**Acceptance Scenarios**:

1. **Given** un extracto emitido, **When** se vuelve a pedir el mismo período sin cambios, **Then** se
   entrega la misma copia guardada.
2. **Given** un usuario de la cooperativa con un código de verificación, **When** lo consulta, **Then** ve
   el documento original y su estado (vigente o anulado).

---

### User Story 14 - Inactividad, no reclamados y fallecimiento del titular (Priority: P2)

Una cuenta sin movimientos del titular durante los días de la línea pasa a **inactiva**: sigue causando
si la línea lo dice, se reactiva con un movimiento del titular y su saldo puede trasladarse a
**no reclamados** según el reglamento. Al fallecimiento del titular, la cuenta se bloquea y el saldo se
entrega a beneficiarios registrados o, sin ellos, por el procedimiento de entrega sin juicio de sucesión
hasta el tope vigente o por sucesión, con aprobación y soporte.

**Why this priority**: obligación de buen manejo; no impide salir en vivo.

**Independent Test**: dejar una cuenta sin movimientos el plazo de la línea, comprobar el cambio a
inactiva y la reactivación con una consignación del titular (no con el abono de intereses).

**Acceptance Scenarios**:

1. **Given** una cuenta inactiva, **When** se abonan intereses, **Then** sigue inactiva: el abono no es
   movimiento del titular.
2. **Given** un titular fallecido con beneficiarios al 50 % y 50 %, **When** se aprueba la entrega,
   **Then** se generan dos órdenes de giro y la cuenta se cancela al pagarse ambas.

---

### User Story 15 - Fondo de liquidez y depósitos asegurados (Priority: P3)

El tesorero consulta, a cualquier fecha, la base del fondo de liquidez por clase de línea y banda de
madurez residual, el requerido según la categoría de la entidad y el régimen vigente (incluida la
transición), y lo compara con el saldo constituido que registra. El informe de depósitos asegurados
suma por ahorrador hasta el tope del seguro cuando la cooperativa está inscrita.

**Why this priority**: es control y reporte; no afecta operaciones.

**Independent Test**: con CDAT de distintos vencimientos y ahorro permanente sólo retirable al
desvincularse, comprobar el requerido por banda contra el cálculo manual.

**Acceptance Scenarios**:

1. **Given** un constituido menor que el requerido, **When** se consulta, **Then** se alerta al tesorero y
   al gerente con la diferencia.

---

### User Story 16 - Seguridad, atribuciones y auditoría verificable (Priority: P1)

Cada acción de captaciones y aportes exige un permiso propio del catálogo de la Cartera; cada usuario
tiene el alcance de la Cartera: oficinas, **líneas** (de cualquier naturaleza) y montos. Quien abre una cuenta no aprueba su apertura excepcional; quien registra un embargo no lo
levanta. Toda acción, incluidas las rechazadas y las lecturas sensibles (consulta de saldos de terceros,
exportaciones, documentos), queda en la auditoría encadenada.

**Why this priority**: es dinero de los asociados; hoy basta con iniciar sesión.

**Independent Test**: con un usuario de la oficina norte, intentar consultar una cuenta de la oficina sur
y recibir «no encontrado»; verificar la cadena de auditoría después de la prueba.

**Acceptance Scenarios**:

1. **Given** un usuario sin permiso de retiro, **When** llama la ruta, **Then** recibe la misma respuesta
   que si no existiera.
2. **Given** la auditoría del día, **When** se verifica la cadena, **Then** cualquier alteración o borrado
   se detecta.

---

### User Story 17 - Informes de gestión y reportes regulatorios (Priority: P3)

El jefe de ahorros y el contador consultan y exportan: saldos por línea y oficina, movimientos,
cuentas por estado, vencimientos de CDAT por plazo de emisión, concentración de depositantes, intereses y retenciones, GMF
retenido, aportes por asociado; y generan los archivos del SICSES (captaciones, saldos diarios, fondo de
liquidez) y de exógena (1019, 1020) que una persona carga en el sistema externo.

**Why this priority**: necesarios, pero sus plazos son mensuales o anuales.

**Independent Test**: generar el formato de captaciones de un mes y comprobar que la suma por línea
coincide con la conciliación contable del mismo corte, en el mismo centro de informes de la Cartera.

**Acceptance Scenarios**:

1. **Given** un formato con un campo cuya estructura no se ha cotejado con el anexo vigente, **When** se
   genera, **Then** sale con la alerta «formato sin cotejar», como el layout PILA de la 010.

---

### Edge Cases

**Fechas y cálculo**
- **Retiro con fecha valor anterior a una liquidación ya hecha.** No se reescribe la liquidación: el
  ajuste de interés de esos días se causa en el período abierto como «ajuste por fecha valor», con
  enlace al movimiento.
- **Cambio de tasa en un día con saldo variable.** Rige la tasa vigente al inicio del día.
- **Año bisiesto y convención real/365.** El 29 de febrero causa con su tasa diaria; los casos dorados lo
  cubren.
- **Festivo o fin de semana.** La causación es diaria calendario; el vencimiento de un CDAT en día no
  hábil se paga el siguiente hábil sin días adicionales de interés, salvo que la línea diga lo contrario;
  los días de gracia se cuentan en días hábiles.
- **Proceso diario caído varios días.** Se pone al día en orden y alerta (FR-031).

**Impuestos**
- **Cuenta exenta de GMF que cambia de marca a mitad de mes.** El acumulado del mes es por persona; la
  nueva cuenta marcada parte del acumulado ya usado. Con la política «por titular» no hay marca: se suman
  todas sus cuentas de ahorro.
- **Redención de un CDAT abonada a la cuenta de ahorro del mismo titular.** Causa GMF sobre capital más
  intereses salvo que el parámetro (con su fuente) la declare exenta.
- **Prórroga con capitalización.** La retención se practica antes de capitalizar; el GMF de la
  capitalización sigue el parámetro.
- **CDAT con interés diario bajo el umbral.** Si el parámetro aplica el umbral diario a los CDAT, no hay
  retención; si no, se retiene en cada pago.
- **Tope mensual de exención cruzado en un retiro.** Se cobra GMF sólo sobre el excedente.
- **Traslado entre cuentas del mismo titular.** No causa GMF si la norma vigente lo exime (parámetro con
  fuente).
- **Reverso de un retiro con GMF.** Reversa también el GMF y su acumulado.
- **Retención cuando el titular es persona jurídica, no contribuyente o exenta.** El perfil tributario de
  la persona (012) decide; sin perfil, se retiene con la tarifa general y se alerta.
- **Titular que cumple 18 años con un retiro del representante en espera de aprobación.** El retiro se
  anula con aviso: desde ese día sólo firma el titular.
- **Cambio de representante legal de un menor** (fallecimiento, decisión judicial). Se registra el nuevo
  con su soporte y vigencia; el anterior deja de firmar desde esa fecha.
- **Cotitulares y certificado tributario.** El certificado sale a cada titular por la proporción
  registrada; sin proporción, por partes iguales.

**Cargos**
- **Cuota de manejo sin saldo suficiente.** Queda pendiente y se cobra con la siguiente consignación; la
  cuenta no baja del mínimo ni se vuelve negativa.
- **Cargo gravado cuya factura rechaza la DIAN.** El cargo sigue cobrado; Comercial gestiona la factura y
  la Cartera muestra el estado que Comercial informe.
- **Cuenta inactiva con cargo por inactividad.** El cargo no reactiva la cuenta (no es movimiento del
  titular).

**Aportes**
- **Devolución que perfora el mínimo no reducible del asociado** sin retiro: se rechaza.
- **Asociado que reingresa.** Abre una cuenta de aportes nueva; la anterior queda cancelada con su
  historia.
- **Cuota pagada de más.** Va como aporte extraordinario o como saldo a favor, según la línea.
- **Asociado con aportes por encima del límite individual de participación.** Se rechaza el aporte que lo
  supera, o se devuelve el exceso por decisión registrada.

**Integración**
- **La pagaduría descuenta menos de lo enviado por un concepto equivalente que agrupa varias líneas.** Se
  reparte con la regla del convenio (prelación o proporción); lo no cubierto queda exigible como «no
  descontado por la pagaduría» con la causa equivalente.
- **La pagaduría responde un concepto o un empleado que no está en las equivalencias.** La respuesta se
  carga sin aplicar esa fila y la deja en «no aplicados» con el motivo, para resolverla a mano.
- **Una pagaduría cambia de formato.** Se crea una versión nueva del formato con vigencia; los archivos ya
  generados conservan el suyo.
- **Consignación de Recaudo con un medio que después se rechaza (cheque devuelto).** Recaudo reversa la
  consignación por el contrato; si el saldo ya se usó, la cuenta queda con sobregiro técnico y bloqueo
  hasta cubrirlo, y se alerta.
- **Nómina aplica un descuento y luego se reversa la corrida.** El reverso llega por el contrato con la
  misma clave de origen.
- **Reverso de un pago de crédito hecho por débito automático.** El reverso del pago arrastra el
  reverso del débito en la cuenta de ahorro, en la misma operación.
- **Contabilidad no responde a la validación previa.** Se aplica la política configurada (advertir o
  bloquear), como en la 013.

**Migración**
- **Persona heredada que no existe en el maestro.** Se rechaza la fila; no se crean personas desde la
  plantilla de saldos.
- **Cuenta heredada sin línea equivalente.** Se mapea en la plantilla de líneas antes de cargar.
- **Interés causado no liquidado al corte.** Entra como saldo de interés por pagar de la cuenta y se
  liquida en la primera liquidación sin volver a causarse.
- **Retenciones y GMF del año anteriores al corte.** Se cargan como información para el certificado
  anual, sin hechos.

## Requirements *(mandatory)*

### Functional Requirements

#### Transversales

Rigen además, sin repetirlos, los transversales de la 013 que no se contradicen aquí (FR-001 a FR-019 de
la 013): las captaciones son Cartera.

- **FR-001 · Saldos sólo por transacciones.** El saldo de cada concepto de cada cuenta (capital, interés
  por pagar, retención, GMF, aportes por clase) es siempre la suma de sus transacciones en el **libro de
  captaciones**, con las mismas reglas que el libro del crédito:
  - ninguna pantalla, importación ni proceso cambia un saldo sin una transacción documentada;
  - cada transacción registra tipo, concepto, valor con signo, tres fechas (valor, contable y registro),
    actor, operación (con recaudador y clave, o corrida del proceso), documento y, cuando aplica, motivo;
  - un abono de interés **suma** al saldo; el historial sólo crece.
- **FR-002 · Reverso en vez de borrado.** Una transacción confirmada no se edita ni se borra. Se corrige
  con una transacción espejo enlazada que arrastra sus dependientes (el GMF de un retiro, la retención de
  una liquidación, el débito de un cruce). Un reverso no se reversa.
- **FR-003 · Auditoría.** La auditoría encadenada de la Cartera, con diez años de retención, incluye
  rechazos y lecturas sensibles (saldos y movimientos de un titular, documentos, exportaciones).
- **FR-004 · Permisos y alcance.** Permisos propios de captaciones y aportes dentro del catálogo
  `Lending.*`, sembrados completos desde K7. El alcance de la Cartera (oficinas, **líneas** de cualquier
  naturaleza, matriz de atribuciones por monto) rige igual, falla cerrado y responde como lo inexistente.
  Las interfaces que usan otros módulos se autorizan con el permiso del módulo que llama.
- **FR-005 · Segregación.** Quien registra un embargo, una apertura excepcional, una entrega por
  fallecimiento, una devolución de aportes o una decisión de asamblea no la aprueba. Comparación por
  persona.
- **FR-006 · Habeas data.** La apertura exige la autorización de tratamiento del titular y de los
  cotitulares (la plataforma que usa la Cartera).
- **FR-007 · Parametrización con vigencia.** Las versiones de las líneas de captación y aportes son
  inmutables con vigencia bitemporal, como las de crédito, y sin versiones retroactivas.
  - Las cuentas a la vista, contractuales y permanentes usan en cada día la versión **vigente** de su
    línea, salvo que la línea declare tasa pactada.
  - El CDAT y el ahorro contractual **sellan** sus condiciones al constituirse; una prórroga sella las
    nuevas.
  - Un cambio nunca altera lo causado, liquidado o cerrado.
- **FR-008 · Frontera.** Las captaciones viven en el espacio de la Cartera y se tocan desde otros módulos
  sólo por la lista cerrada de interfaces de la Cartera, ampliada por la enmienda E-01. Las verificaciones
  automáticas de la 013 se enmiendan, no se saltan.
- **FR-009 · Idempotencia.** Toda apertura, operación de depósito, liquidación, prórroga, reverso y
  mensaje lleva clave de origen, con la gramática de claves de la Cartera.
- **FR-010 · Precisión y convenciones.** Decimal exacto con los motores comunes de la Cartera. Son
  condiciones de la línea, selladas donde aplique: convención de días (30/360, real/365, real/360),
  equivalencia de tasas (compuesta por defecto), redondeo (unidad, modo y momento: al peso por
  liquidación por defecto; la causación diaria guarda decimales y la diferencia se absorbe al liquidar).
- **FR-011 · Reproducibilidad.** La liquidación de una cuenta a una fecha D con la información hasta R da
  siempre el mismo resultado, con la explicación día por día.
- **FR-012 · Fechas.** Fecha local de Colombia; fecha valor hacia atrás dentro de los días configurados;
  un mes contable cerrado no se reescribe.
- **FR-013 · Errores y procesos visibles.** Códigos `Lending.Savings.*`, `Lending.TermDeposit.*`,
  `Lending.Contributions.*`; toda falla de proceso queda en los errores de corrida de la Cartera con
  alerta.
- **FR-014 · Sin valores legales en el código; todo configurable desde la norma colombiana.** Tarifas
  de retención por clase de línea, umbral diario en UVT, tope de exención de GMF en UVT, tarifa de GMF
  (que puede ser cero), componente inflacionario, porcentajes del fondo de
  liquidez, límite inembargable, tope de entrega sin sucesión, tope de la cuota obligatoria y límite de
  revalorización viven en parámetros con vigencia y fuente; la verificación de la 013 los vigila; lo
  sembrado sin validar se marca «pendiente de validar».
- **FR-015 · Valores nacionales.** UVT, IPC, componente inflacionario, límite inembargable y tope de
  entrega sin sucesión se suman a los valores nacionales replicados a cada cooperativa.
- **FR-016 · Uso.** Abrir, consignar, retirar y consultar en tres pasos o menos, con indicador de carga
  por zona.
- **FR-017 · El proceso como actor.** Causación, liquidación, cuotas, vencimientos, prórrogas e
  inactividad corren como «Proceso de la cooperativa» y sólo originan los hechos marcados para el proceso.
- **FR-018 · Perfil regulatorio.** El perfil vigente de la cooperativa decide si puede activar líneas de
  captación y cuáles, si capta sólo de asociados, su categoría para el fondo de liquidez, si está inscrita
  en el seguro de depósitos y si es agente retenedor de GMF.
- **FR-019 · Numeración.** Cuentas, CDAT y documentos por consecutivo de la cooperativa por línea o clase,
  sin huecos por concurrencia; el número no se reutiliza.

#### A · Motores de cálculo

Puros y decimales, en el dominio de la Cartera, sobre los motores comunes ya construidos (tasas, días,
redondeo, causación acumulada, calendario).

- **FR-020 · Motor de intereses de captación.** Recibe saldos con fecha valor, versiones de tasa y tramos,
  método de saldo, saldo mínimo para interés, convención y redondeo; devuelve el interés de cada día con
  su explicación.
  - **Métodos de saldo**: diario (`saldo del día × i_d`, con `i_d = (1 + EA)^(1/B) − 1`), promedio y
    mínimo del período.
  - **Tramos**: escalonados o por saldo total.
  - Un día con saldo bajo el mínimo para interés causa cero.
- **FR-021 · Motor de retención.** Recibe el interés de cada día y del período, el perfil tributario del
  titular, la tarifa de la clase de línea, el umbral diario en UVT y la política de componente
  inflacionario; devuelve la retención:
  - si ningún día alcanza el umbral, cero; si alguno lo alcanza, se retiene sobre el **total** del
    período (regla del sector solidario), salvo que el parámetro de la clase diga que el umbral no
    aplica (CDAT, a validar);
  - descontando el componente inflacionario cuando el titular sea persona natural no obligada a llevar
    contabilidad, con el último porcentaje publicado (política por defecto según la norma; P27); la
    política y el porcentaje son configurables con vigencia;
  - cero para no contribuyentes y autorretenedores según el perfil.
- **FR-022 · Motor de GMF.** Recibe la disposición, la política de exención (por defecto **una cuenta
  de ahorro exenta por persona**; alternativa: límite por titular), el acumulado exento del mes, el tope en
  UVT, la tarifa y la tabla de operaciones que son hecho generador (por defecto, según la norma, la
  redención de un CDAT abonada en cuenta y el traslado entre ahorro y CDAT están gravados); devuelve el GMF
  y el nuevo acumulado. Con **tarifa cero o impuesto desactivado** desde una fecha (el Gobierno estudia
  eliminarlo o modificarlo), no calcula GMF desde esa fecha y conserva lo anterior; todo con vigencia.
- **FR-023 · Motor del CDAT.** Plan de intereses (fechas, días e interés de cada período,
  `i_p = (1 + EA)^(n/B) − 1`; vencido o anticipado `i_a = i_v / (1 + i_v)`), causación diaria cuadrada
  contra el plan (la diferencia de redondeo va al último día del período), liquidación al vencimiento,
  prórroga (con o sin capitalización, retención antes de capitalizar), renovación con condiciones nuevas,
  redención anticipada con penalidad (reliquidación a la tasa de penalidad o descuento).
- **FR-024 · Motor del ahorro contractual.** Valor final con cuota periódica, bonificación por
  cumplimiento como diferencia entre la tasa bonificada y la base, tolerancia de atrasos, reliquidación a
  la tasa castigo.
- **FR-025 · Motores de aportes y liquidez.** Revalorización y excedentes (base al cierre o promedio,
  límite legal, prorrateo al fondo); fondo de liquidez (depósitos por clase y madurez residual, intereses
  causados y por causar, categoría y régimen vigente con transición).
- **FR-026 · Casos dorados.** Junto a los de crédito, con valores calculados a mano que coinciden al
  centavo, como mínimo los de la investigación:
  - CDAT de 10.000.000 a 180 días al 10 % EA: 481.244 al vencimiento en base 365; 488.088 en base 360;
    seis pagos de 79.741 (478.446) mensuales vencidos; 79.111 anticipados;
  - ahorro a la vista al 3 % EA con saldos de 2.000.000, 5.000.000 y 1.500.000 diez días cada uno: 6.884
    por saldo diario, 3.649 por saldo mínimo, sin retención por umbral;
  - retención con umbral alcanzado y no alcanzado, con y sin componente inflacionario, persona exenta;
  - GMF con tope mensual cruzado (retiros de 20.000.000 en el mes: 6.676) y redención de CDAT abonada
    (41.952), reverso de retiro con GMF;
  - contractual de 12 cuotas de 200.000: 2.465.306 al 6 % y 2.486.777 al 8 % (bonificación 21.471);
  - prórroga con capitalización neta de retención, redención anticipada, tramos escalonados y por total,
    cambio de tasa a mitad de mes, año bisiesto, revalorización limitada por IPC y prorrateada, fondo de
    liquidez por bandas.

#### B · Líneas de captación y de aportes, y apertura

- **FR-027 · Naturaleza de la línea.** Toda línea del catálogo de la Cartera tiene naturaleza fija:
  colocación, captación o aportes. La identidad (código único en el catálogo, nombre, estado, alcance de
  usuario) es común; la versión de condiciones es propia de cada naturaleza. Las líneas de crédito
  existentes quedan como colocación sin cambiar su comportamiento. A una línea de captación o aportes no
  le aplican modalidad, usura, mora, amortización ni SARC.
- **FR-028 · Condiciones de una línea de captación.** Clase fija (a la vista, contractual, permanente,
  CDAT), oficinas, si exige asociado, si admite cotitulares, menores y retiros parciales; tasa (fija, por
  tramos de saldo, por plazo y monto en el CDAT; o variable sobre un índice con margen, como en el
  crédito), método de saldo, mínimos para abrir, mantener y ganar interés, convención, equivalencia y
  redondeo, periodicidad de liquidación, retención (clase tributaria), GMF, montos máximos de retiro por
  operación y diarios, efectivo máximo, días para inactividad y si sigue causando inactiva. En el CDAT,
  además: plazo mínimo y máximo, periodicidad de intereses y si son vencidos o anticipados, prórroga
  automática y número máximo de prórrogas, **regla de tasa en la prórroga** (la misma, la vigente para el
  plazo, una pactada), días de gracia y si causan, tratamiento si no se prorroga (deja de causar o pasa a
  la vista), redención anticipada y penalidad, y si la prórroga capitaliza intereses.
- **FR-029 · Condiciones de la línea de aportes.** Mínimo no reducible por asociado, cuota periódica
  (valor fijo, porcentaje del ingreso registrado o fracción del salario mínimo, con redondeo),
  periodicidad, reparto con ahorro permanente, aportes extraordinarios, límite individual de
  participación y plazo estatutario de devolución.
- **FR-030 · Sólo asociados.** Si el perfil o la línea lo exigen, abrir y consignar exige asociado vigente;
  un asociado retirado sólo recibe operaciones del proceso de retiro.
- **FR-031 · Procesos diarios.** Un ejecutor de captaciones dentro del ciclo diario de la Cartera, por
  cooperativa y por tandas, con la marca de agua de causación de cada cuenta: causación, liquidación,
  cuotas exigibles, vencimientos, días de gracia y prórrogas, cumplimiento del contractual e inactividad;
  si falla, se pone al día en orden y alerta.
- **FR-032 · Validaciones de apertura.** Conocimiento del asociado al día, listas restrictivas (la carga de
  la plataforma, con liberación sólo por el oficial de cumplimiento) y declaración de origen de fondos
  sobre el umbral configurado.
- **FR-033 · Titularidad.** Titular, cotitulares con su proporción, manejo indistinto o conjunto,
  firmantes con límite y representante de menores o personas jurídicas; todos personas del maestro.
  - **Menores de edad**: el menor es el **titular** (el saldo, los rendimientos, la retención y el
    certificado tributario son suyos) y la cuenta la maneja su **representante legal**, con el soporte de
    la representación (registro civil o decisión judicial) como adjunto y vigencia; sólo el representante
    firma retiros y traslados mientras el titular sea menor.
  - Si la línea exige asociado, el menor debe estar vinculado como asociado según los estatutos; si no lo
    exige, basta la persona.
  - El proceso diario detecta la **mayoría de edad** (18 años, parámetro con fuente): el control pasa al
    titular, el representante deja de firmar, la cuenta queda con la marca «actualizar datos» hasta que
    el titular actualice su conocimiento y su autorización de datos, y se avisa al titular y al
    representante. No cambia el número, el saldo ni las condiciones.
- **FR-034 · Beneficiarios.** Personas con porcentaje que suma 100 %, editables por el titular con
  constancia; cuenta la versión vigente al fallecimiento.
- **FR-035 · Número de cuenta.** Consecutivo por línea (FR-019).
- **FR-036 · Exención de GMF.** Por defecto, como dice la norma, **una sola cuenta de ahorro exenta por
  persona** en la cooperativa: marcar una segunda se rechaza indicando la vigente; cambiar la marca deja
  vigencia y auditoría. La política es configurable con vigencia (límite por titular sumando sus cuentas,
  o sin GMF).
- **FR-037 · Condiciones selladas.** Ver FR-007.
- **FR-038 · Cambio de condiciones.** Un cambio de tasa de una línea a la vista avisa a los titulares con
  la anticipación del reglamento.
- **FR-039 · Cancelación.** Liquida a la fecha, retiene, cobra GMF y entrega o traslada el saldo; queda
  «cancelada». No se cancela con bloqueos, embargos o canje pendientes.

#### C · Aportes

- **FR-040 · Cuota exigible.** El proceso genera la cuota de cada cuenta de aportes (y su parte de ahorro
  permanente) con su vencimiento; las no pagadas siguen exigibles y se ven en la consulta.
- **FR-041 · Aportes extraordinarios**, si la línea los admite y sin superar el límite de participación.
- **FR-042 · Mínimo no reducible.** Ninguna devolución sin retiro lo perfora.
- **FR-043 · Límite de participación** individual (parámetro con fuente); el exceso se rechaza o se
  devuelve por decisión registrada.
- **FR-044 · Aportes como garantía.** No se devuelven ni se ceden con créditos vivos, salvo en el cruce del
  retiro; la Cartera los ve como garantía de tipo aportes de sus créditos.
- **FR-045 · Revalorización** por decisión de asamblea, limitada por el índice legal y prorrateada al
  fondo; se simula, se aprueba con segundo usuario y se aplica en una operación. Los aportes no causan
  intereses.
- **FR-046 · Distribución de excedentes** a aportes, ahorros o pago, por asociado, con la misma mecánica.
- **FR-047 · Amortización y readquisición** con decisión registrada y en igualdad de condiciones.
- **FR-048 · Cesión entre asociados** sólo si los estatutos la permiten, con aprobación.
- **FR-049 · Tope de cuota en fondos de empleados**: aportes más ahorro permanente no superan el
  porcentaje legal del ingreso, y la fracción legal va a aportes.

#### D · Movimientos, bloqueos y embargos

- **FR-050 · Consignación.** Por el contrato o por traslado; fecha valor; un medio con días de
  disponibilidad deja el valor en canje hasta que el recaudador lo confirme o lo reverse.
- **FR-051 · Retiro.** Valida estado, manejo y firmas, disponible, mínimo, montos máximos, efectivo
  máximo y atribución (matriz de atribuciones de la Cartera); lo que excede espera a un aprobador presente
  con segundo factor.
- **FR-052 · Traslado** entre cuentas del mismo o distinto titular en una operación; entre ahorro y CDAT
  sigue la regla de GMF del parámetro.
- **FR-053 · GMF en la disposición** por el motor (FR-022), como transacción enlazada.
- **FR-054 · Bloqueos** por motivo, valor o total, con vigencia; incluye el bloqueo de una cuenta como
  garantía de un crédito de la misma Cartera.
- **FR-055 · Embargos.** Oficio, juzgado, demandante, valor y si es por alimentos; se retiene lo que excede
  el límite inembargable vigente sumando las cuentas de ahorro de la persona (salvo alimentos), en el
  orden configurado; los aportes no se embargan y se responde al juzgado; la consignación al juzgado sale
  por orden de giro; el desembargo libera.
- **FR-056 · Sin sobregiro de usuario.** Ninguna operación de usuario deja la cuenta bajo cero ni bajo el
  mínimo; el único negativo posible es técnico, por reverso de una consignación ya usada, y bloquea y
  alerta.
- **FR-057 · Saldo disponible** = saldo − bloqueos − embargos − canje − mínimo, siempre desglosado.
- **FR-058 · Movimiento del titular.** Cada tipo de transacción declara si cuenta para la inactividad
  (los abonos de interés y los cargos de proceso no).
- **FR-128 · Cargos y comisiones.** Cada línea de captación puede tener, con vigencia, un catálogo de
  cargos **apagado por defecto**:
  - tipos: cuota de manejo periódica, retiro o consignación adicional sobre los gratuitos del período,
    certificado o extracto adicional, reposición de libreta o de medio, cuenta inactiva, otros que la
    cooperativa defina con su código de catálogo;
  - valor fijo o porcentaje con mínimo y máximo, periodicidad o evento que lo genera, y número de
    operaciones gratuitas;
  - **exoneraciones** combinables: saldo promedio o mínimo, antigüedad, asociado empleado, cuenta exenta,
    titular menor de edad, oficina, o exoneración individual aprobada con motivo;
  - se debita a la cuenta como transacción propia, sin dejarla bajo el mínimo: si no alcanza, queda como
    cargo pendiente que se cobra con la siguiente consignación, salvo que la línea diga que se condona;
  - si el cargo está **gravado con IVA**, la Cartera emite el mensaje «cargo de captación causado» y
    **Comercial factura** electrónicamente el cargo y su IVA (como la decisión D20 de la 013 para los
    intereses gravados); la Cartera no calcula IVA ni emite facturas;
  - un cargo se reversa con su transacción espejo, y el reverso pide a Comercial la nota crédito.
- **FR-059 · Efectivo.** Sobre el umbral configurado exige la declaración de origen de fondos y deja el
  dato para SARLAFT.

#### E · Intereses, retención y GMF

- **FR-060 · Causación diaria** por cuenta con el motor (FR-020), como interés por pagar; queda como parte
  de los hechos de la causación de la Cartera.
- **FR-061 · Liquidación.** En la periodicidad de la línea abona el interés causado no liquidado menos la
  retención; idempotente por cuenta y período; guarda la última fecha liquidada; la diferencia de
  redondeo entre lo causado y lo liquidado se ajusta en la misma operación.
- **FR-062 · Ajuste por fecha valor.** Un movimiento en días ya causados genera un ajuste de causación
  en el día actual, enlazado.
- **FR-063 · Retención** por el motor (FR-021) en cada abono o pago de intereses, **antes** de cualquier
  capitalización; se acumula por titular, año y concepto para los certificados.
- **FR-064 · Simulación** de la liquidación a cualquier fecha, sin efectos.
- **FR-065 · Suspensión de causación** en cuentas embargadas totalmente o de titular fallecido, según la
  línea.
- **FR-066 · GMF del período** acumulado por día y mes y emitido como hecho.

#### F · CDAT y ahorro contractual

- **FR-067 · Constitución.** Monto, plazo en el rango, tasa de la línea vigente para plazo y monto,
  periodicidad y modalidad de intereses, destino de los pagos (cuenta de ahorro u orden de giro) e
  instrucción al vencimiento (prorrogar, renovar con condiciones, cancelar y pagar).
- **FR-127 · Tasa negociada.** Además de la tasa de la tabla, la línea puede admitir una tasa negociada:
  - **reglas con vigencia por línea** que fijan el margen máximo sobre la tabla (en puntos de tasa
    efectiva anual) según criterios combinables: antigüedad del asociado, rango de monto, rango de plazo,
    tasa base de la tabla, saldo total del titular en captaciones y aportes, oficina y canal;
  - el margen permitido se aprueba según la **matriz de atribuciones** de la Cartera (puntos adicionales y
    monto del CDAT): dentro de la atribución del asesor se aplica sola; por encima, espera a un aprobador
    presente con segundo factor o a un órgano con acta; nunca supera el máximo de la regla;
  - queda sellada en el CDAT con la tasa de la tabla, el margen, la regla aplicada, el aprobador y el
    motivo; en la prórroga se aplica la regla de tasa de la línea, que puede conservar o no el margen
    (condición de la línea);
  - los informes muestran los CDAT con tasa negociada y su costo frente a la tabla.

  La misma mecánica aplica, si la línea lo declara, a las cuentas a la vista y contractuales.
- **FR-068 · Título no negociable.** Documento «CDAT, no negociable» con número, código de verificación y
  condiciones; una prórroga o renovación emite un título nuevo enlazado al anterior.
- **FR-069 · Pago de intereses** en cada fecha del plan, neto de retención, al destino pactado.
- **FR-070 · Vencimiento, gracia y prórroga.** Al vencer se abren los días de gracia; pasados sin
  instrucción y con prórroga automática, se prorroga por el mismo plazo con la regla de tasa de la línea,
  hasta el número máximo de prórrogas; si no hay tasa vigente para el plazo o se agotó el máximo, se aplica
  el tratamiento de la línea (deja de causar como «vencido por pagar» o pasa a la vista).
- **FR-071 · Redención anticipada** sólo si la línea la admite, simulada antes de confirmar, con
  aprobación si supera la atribución.
- **FR-072 · CDAT embargado o de titular fallecido**: no se paga al vencimiento; se prorroga o queda a
  disposición.
- **FR-073 · Reclasificación por plazo de emisión.** El capital del CDAT lleva la dimensión de plazo de
  emisión; una prórroga o renovación que la cambia emite el hecho de reclasificación.
- **FR-074 · Ahorro contractual**: cuota, periodicidad, plazo, tasa base, bonificación y tolerancia;
  superada la tolerancia, pasa a «incumplido» con el efecto de la línea.
- **FR-075 · Vencimiento del contractual**: liquida con la tasa que corresponda (bonificada o castigo) y
  traslada o gira según la instrucción; la bonificación se reconoce al cumplirse (o se provisiona según la
  política contable, a validar).

#### G · Retiro del asociado, fallecimiento e inactividad

- **FR-076 · Trámite y liquidación del retiro.** El retiro nace en la Cartera: solicitud con causa
  (voluntario, exclusión, fallecimiento, otra configurada) y soportes; aprobación por el órgano competente
  con acta (órganos, sesiones y votos de la Cartera), sin que quien radica apruebe; al aprobarse, la
  Cartera emite «retiro de asociado aprobado» para que Asociados cambie el estado. Una solicitud se puede
  desistir antes de aprobarse. Aprobado el retiro, el analista de aportes liquida: aportes por clase, ahorros con intereses a la fecha, CDAT (siguen hasta su
  vencimiento salvo que la línea diga otra cosa) y los créditos vivos de la persona en la misma Cartera.
- **FR-077 · Cruce interno.** Aplica aportes y ahorros a los créditos con la aplicación de pagos de la
  Cartera, con medio «cruce interno» y la prelación de los estatutos, en una sola operación: si algo falla,
  nada se aplica; el reverso del pago arrastra el débito.
- **FR-078 · Pérdidas.** Descuento proporcional de pérdidas no cubiertas por la reserva, según parámetro y
  acta.
- **FR-079 · Devolución** del remanente por orden de giro o traslado, en el plazo estatutario, con
  aprobación si supera la atribución.
- **FR-080 · Mínimo irreducible de la entidad.** Si las devoluciones del período lo perforarían, la
  devolución queda pendiente y se avisa al gerente.
- **FR-081 · No reclamados.** Lo devuelto y no cobrado en el plazo, o el saldo inactivo cuando el
  reglamento lo ordene, pasa a «no reclamados» por titular, con su hecho.
- **FR-082 · Fallecimiento.** Bloquea las cuentas del titular; entrega a beneficiarios por su porcentaje;
  sin ellos, entrega sin juicio de sucesión hasta el tope vigente con los soportes del parámetro, o por
  sucesión.
- **FR-083 · Entrega** aprobada con segundo usuario, por orden de giro o traslado.
- **FR-084 · Inactividad.** Sin movimientos del titular en los días de la línea, pasa a «inactiva» (y a su
  dimensión contable); un movimiento del titular la reactiva. El traslado por norma de cuentas abandonadas
  sólo se habilita si la contadora confirma que aplica.
- **FR-085 · Reingreso** con cuentas nuevas; las anteriores conservan su historia.

#### H · Operaciones de depósito en el contrato de la Cartera

- **FR-086 · El contrato ampliado.** El contrato con recaudadores de la Cartera suma operaciones de
  depósito: consultar cuentas y disponible, listar exigibles de cuotas, simular, consignar, retirar,
  trasladar y reversar. Mismas reglas: síncrono en la unidad de trabajo del recaudador, registro de
  participantes, clave idempotente, versión del contrato, «no habilitado» antes de la activación de la
  naturaleza.
- **FR-087 · Exigibles.** Cuotas de aportes, permanente y contractual pendientes con período, vencimiento y
  valor; el recaudador paga una, varias o un valor mayor si la línea lo admite.
- **FR-088 · Medios.** Las clases de medio del contrato de la Cartera; un retiro en efectivo lo autoriza la
  Cartera y lo entrega Recaudo.
- **FR-089 · Nómina.** Los descuentos de aportes y ahorros entran por el contrato en la misma unidad de
  trabajo de la aprobación de la corrida; lo no descontado queda exigible con su motivo (E-02).
- **FR-090 · Débito automático.** Con autorización del titular, en la fecha de la cuota de un crédito la
  Cartera debita la cuenta de ahorro y aplica el pago al crédito en una sola operación interna; sin
  disponible, no hay débito parcial salvo que la autorización lo admita.
- **FR-091 · Órdenes de giro** a Tesorería (retiros por transferencia, devoluciones, entregas, intereses
  de CDAT a terceros, consignaciones a juzgados), con el mismo mensaje de orden de giro de los
  desembolsos; una rechazada reintegra el saldo.
- **FR-092 · Reversos.** Un recaudador reversa sus operaciones con su clave; la Cartera reversa una
  consignación sólo enviando «consignación reversada por la Cartera».
- **FR-093 · Antes de activar**, las operaciones de depósito responden «no habilitado» sin efectos.
- **FR-125 · Pagadurías configurables por convenio.** Las cuotas de aportes, ahorro permanente y
  contractual se cobran por las pagadurías con el mismo ciclo de libranza de la Cartera (archivo de
  descuentos, resultado, pendientes de giro, no aplicados), y cada convenio de pagaduría configura, con
  vigencia:
  - **grupos de archivo**: un solo archivo o varios archivos separados por grupo de conceptos (por
    ejemplo, créditos en uno y aportes y ahorros en otro), cada uno con su formato y su calendario;
  - **conceptos equivalentes**: qué líneas (de cualquier naturaleza) y qué conceptos se agrupan bajo cada
    código o concepto de la pagaduría, y cómo se reparte entre ellos lo que la pagaduría informe
    descontado por ese concepto;
  - **formato**: estructura del archivo de envío y del de respuesta como dato versionado, con los
    formatos de archivo plano de la plataforma (campos, longitudes, separadores, codificación, totales),
    sin programar;
  - **prelación** entre conceptos y líneas cuando lo descontado no alcanza, por convenio;
  - **equivalencias** de identificación (código del empleado en la pagaduría, documento) y de causas de
    no descuento a los motivos de la Cartera.

  Una vista previa con datos reales muestra el archivo antes de generarlo; un concepto equivalente sin
  regla de reparto o una línea con cuota sin grupo asignado impide generar y dice cuál.

#### I · Consultas

- **FR-094 · Consulta de aportes real.** La Cartera implementa `IConsultaDeAportes` con aportes por
  clase, mínimo no reducible, ahorro permanente y cuotas en mora a una fecha, desde la activación de
  aportes en la cooperativa; la plantilla de saldos de aportes deja de leerse allí; la excepción de
  arrastre y el descuento en el deterioro se habilitan por parámetro.
- **FR-095 · Productos de una persona.** Cuentas, CDAT y cuenta de aportes con estado y saldo, y si tiene
  productos vivos, para Personas, Asociados y Nómina.

#### J · Integración contable

- **FR-096 · Hechos nuevos de la Cartera.** Mismo emisor, mismas reglas (sin cuentas, en la misma
  transacción, enlace al original): apertura y consignación, retiro, traslado, GMF retenido, interés
  causado y su ajuste, interés liquidado, retención practicada, CDAT constituido, intereses de CDAT
  pagados, CDAT prorrogado, renovado, redimido o cancelado, penalidad, reclasificación por plazo de
  emisión y por inactividad, cargo causado, condonado o reversado, aporte recibido, revalorización, distribución de excedentes, amortización,
  cruce al retiro, devolución ordenada y girada, traslado a no reclamados, embargo consignado y el
  reverso de cada uno.
- **FR-097 · Dimensiones nuevas.** Naturaleza, clase de línea de captación, plazo de emisión, clase de
  aporte, cuenta inactiva, exenta o embargada, además de las de la Cartera que apliquen (línea, oficina,
  empleado, tercero, documento cruce = número de cuenta).
- **FR-098 · Validación previa y modo de paso** como en la Cartera; la causación de captaciones por lotes
  resumida por día por defecto.
- **FR-099 · Matriz.** La matriz de la Cartera suma los **roles** de captación y aportes: capital del
  depósito por clase, interés por pagar, gasto por intereses, retención por pagar, GMF por pagar, depósito
  inactivo, bonificación, aportes por clase, fondo de revalorización, devolución por pagar, no
  reclamados, embargos por consignar, ingreso por cargos y cargo por cobrar. Plantilla con filas sugeridas sin cuenta (con las cuentas CUIF de la
  investigación como sugerencia) y completitud. Lo de captaciones no se reversa desde Comprobantes.
- **FR-100 · Conciliación y cierre.** La conciliación de la Cartera compara también por línea de
  captación, concepto y oficina; el cierre mensual de la Cartera incluye el cierre de causación de
  captaciones (sin SARC), y Contabilidad no cierra un mes que la Cartera no cerró.

#### K · Documentos e informes

- **FR-101 · Documentos** con la mecánica de la Cartera (número, verificación, huella, PDF guardado,
  anulación por reverso), con tipos nuevos.
- **FR-102 · Extracto** por cuenta y período, con saldo inicial, movimientos con fecha valor, intereses,
  retención, GMF, saldo final y disponible.
- **FR-103 · Certificado de saldo** a una fecha.
- **FR-104 · Certificado tributario anual** por titular y año: saldos al 31 de diciembre, rendimientos,
  retenciones, GMF y componente inflacionario, por la proporción de cada cotitular.
- **FR-105 · Certificado de retención** por período.
- **FR-106 · Constancia y paz y salvo de aportes.**
- **FR-107 · Informes de gestión** en el centro de informes de la Cartera: saldos por línea y oficina,
  movimientos, cuentas por estado, vencimientos de CDAT por plazo de emisión, concentración de
  depositantes, intereses y retenciones, GMF, aportes por asociado, cuotas en mora, no reclamados,
  embargos.
- **FR-108 · Exportación** con su permiso y auditada.

#### L · Fondo de liquidez, seguro de depósitos y reportes regulatorios

- **FR-109 · Fondo de liquidez** a cualquier fecha (FR-025) contra el constituido que registra el
  tesorero; alerta si es menor.
- **FR-110 · Depósitos asegurados** por ahorrador hasta el tope, si la cooperativa está inscrita;
  excluye aportes.
- **FR-111 · SICSES** (captaciones, saldos diarios, fondo de liquidez) con estructura versionada como dato
  y alerta «formato sin cotejar».
- **FR-112 · Exógena** 1019 y 1020 con la misma mecánica.

#### M · Migración, activación y retiro de lo heredado

- **FR-113 · Plantillas** con la mecánica de migración de la Cartera: líneas de captación y aportes y su
  equivalencia con lo heredado; cuentas con titulares, firmantes, beneficiarios, condiciones y saldos por
  concepto al corte; interés causado no liquidado; CDAT con plan pendiente, prórrogas hechas e
  instrucción; cuentas de aportes con mínimo no reducible y cuotas atrasadas; bloqueos y embargos;
  exención de GMF y acumulado del mes; retenciones, rendimientos y GMF del año para el certificado. La
  plantilla de saldos de aportes de la 013 sirve de punto de partida.
- **FR-114 · Saldo inicial** por concepto al corte, sin hechos contables.
- **FR-115 · Cuadre** contra Contabilidad por línea, concepto y cuenta; diferencias sólo con motivo y acta.
- **FR-116 · Activación por naturaleza.** Aportes y captaciones tienen cada una su fecha de activación en
  la configuración de la Cartera, independiente de la de los créditos, con la misma regla (día siguiente
  al corte, plazo de fecha valor, mes abierto, carga reemplazable).
- **FR-117 · Cooperativa sin historia** activa sin carga.
- **FR-118 · Cifras de referencia** para cotejar los totales cargados, sin nombrar al sistema de origen.
- **FR-119 · Retiro de lo heredado.** Las 38 tablas conservadas y las tablas `CDT_*` se retiran con
  migración destructiva **con guarda**: diagnóstico de filas por base en local, DEV, QA y producción,
  respaldo, marcador de aprobación y segundo revisor; con filas en alguna, la guarda se niega y el dueño
  decide. Lo que captaciones y aportes necesitan se renueva en esta feature con tablas nuevas, sin
  reutilizar nombres (Q2).
- **FR-120 · Rutas y pantallas.** Las 52 rutas `/api/savings/*`, las 18 `/api/cdt/*` y las 18 pantallas
  heredadas se retiran sin alias; las nuevas viven en la Cartera (`/cartera/ahorros*`, `/cartera/cdat*`,
  `/cartera/aportes*`, rutas API bajo `/api/lending/`) y el menú de la Cartera las agrupa.

#### Enmiendas a otras features

- **FR-121 · E-01 a la 013 (Cartera).** Naturaleza de la línea con identidad común y versiones por
  naturaleza; enmienda de `ElEspacioLendingEsSoloCartera` y de la lista cerrada de interfaces; contrato
  ampliado con operaciones de depósito; ejecutor de captaciones en el ciclo diario y en el cierre mensual;
  hechos, roles y dimensiones nuevos en la matriz; `IConsultaDeAportes` real y fin de D19; cruce al retiro
  y débito automático como operaciones internas (la clase de recaudador `Contributions` queda sin uso
  nuevo y C37 pierde objeto); permisos, documentos, vistas y plantillas nuevos; activación por naturaleza;
  rutas reservadas convertidas en rutas de la Cartera.
- **FR-122 · E-02 a la 010 (Nómina).** Conceptos de descuento de aportes y ahorros calculados con los
  exigibles del contrato y aplicados por él al aprobar; la reversa de la corrida los reversa.
- **FR-123 · E-03 a la 009 (Contabilidad).** Roles, hechos, dimensiones y tipos de comprobante nuevos en
  la matriz de la Cartera; la guarda de cierre incluye la causación de captaciones.
- **FR-124 · E-04 a la 008 (Personas y Asociados).** Asociados deja de registrar retiros por su cuenta:
  cambia el estado del asociado al recibir «retiro de asociado aprobado» de la Cartera; dar de baja a una
  persona consulta sus productos vivos (FR-095).
- **FR-126 · E-05 a la 013 (libranza, K2).** El convenio de pagaduría de K2 deja de ser sólo de créditos:
  admite cuotas de aportes y ahorros, grupos de archivo, conceptos equivalentes con regla de reparto,
  formato de envío y respuesta como dato y prelación configurable (FR-125).

### Key Entities *(include if feature involves data)*

**Configuración**
- **Línea** (existente): identidad común con **naturaleza** (colocación, captación, aportes).
- **Versión de línea de captación**: clase, tasas y tramos, método de saldo, mínimos, convención,
  liquidación, retención, GMF, límites, inactividad y condiciones del CDAT y del contractual.
- **Versión de línea de aportes**: mínimo no reducible, cuota, reparto con permanente, límites, plazo de
  devolución.
- **Parámetros legales**: tarifas, umbral, topes y porcentajes con vigencia y fuente.

**Cuentas**
- **Cuenta de captación**: titular, línea, número, estado, oficina, condiciones selladas cuando aplica.
- **Participante de la cuenta**: persona y rol (cotitular, firmante, representante legal de menor o de
  persona jurídica con su soporte, beneficiario) con vigencia.
- **CDAT**: monto, plazo, tasa pactada (tasa de la tabla, margen negociado, regla y aprobador), plan de
  intereses, instrucción, días de gracia, cadena de prórrogas y renovaciones, plazo de emisión.
- **Regla de tasa negociada**: línea, criterios (antigüedad, monto, plazo, tasa base, saldo del titular,
  oficina, canal), margen máximo y vigencia.
- **Contrato de ahorro contractual**: cuota, plazo, tolerancia, estado de cumplimiento.
- **Cuenta de aportes**: asociado y saldos por clase.
- **Cuota exigible**: período, vencimiento, valor, pagado, estado.
- **Convenio de pagaduría** (de la Cartera, ampliado): grupos de archivo con formato y calendario,
  conceptos equivalentes con sus líneas y regla de reparto, prelación y equivalencias de identificación y
  de causas, con vigencia.

**Libro**
- **Transacción de captación**: tipo, concepto, valor con signo, tres fechas, operación, enlace al
  original.
- **Posición diaria de la cuenta** y **marca de agua de causación**.
- **Liquidación**: período, interés, retención, neto.
- **Acumulado de GMF** por persona y mes.
- **Cargo de la línea** (tipo, valor, evento, gratuitos, exoneraciones, gravado o no, vigencia) y **cargo
  causado** (cuenta, valor, estado: cobrado, pendiente, condonado, reversado; factura de Comercial).

**Restricciones y procesos**
- **Bloqueo**, **Embargo**, **Fallecimiento y entrega**.
- **Liquidación de retiro**, **Decisión de asamblea**, **Carga de migración** por naturaleza.

**Documentos e integración**
- **Documento emitido** (tipos nuevos), **hechos nuevos**, **reglas de matriz con roles nuevos**, **orden de
  giro**.

## Success Criteria *(mandatory)*

### Measurable Outcomes

**Cálculo**
- **SC-001**: Liquidar una misma cuenta a la misma fecha, con la misma información registrada, da un
  resultado idéntico al centavo en el 100 % de las repeticiones.
- **SC-002**: El 100 % de los casos dorados (al menos 40, que cubren todo lo listado en FR-026) coinciden
  al centavo con el valor calculado a mano.
- **SC-003**: Ninguna liquidación lanzada dos veces para el mismo período abona interés dos veces.

**Operación**
- **SC-004**: Un asesor abre una cuenta para un asociado existente en menos de 3 minutos y un cajero
  registra una consignación o un retiro en menos de 1 minuto.
- **SC-005**: Una operación del contrato repetida con la misma clave nunca mueve saldo dos veces (0
  duplicados en la prueba de carga con reintentos).
- **SC-006**: La causación diaria de 100.000 cuentas de una cooperativa termina en menos de 30 minutos.
- **SC-007**: El saldo disponible que ve el cajero coincide en el 100 % de los casos con el que valida el
  retiro.

**Integración**
- **SC-008**: Al cierre de cada mes, la conciliación de la Cartera (créditos y captaciones) con
  Contabilidad tiene cero diferencias sin explicar.
- **SC-009**: El 100 % de los cambios de saldo producen exactamente un hecho (o un lote que lo incluye) y
  ninguno lleva cuentas contables.
- **SC-010**: Desde la activación de aportes en una cooperativa, la consulta de aportes que usan los
  motores de crédito devuelve el mismo saldo que el extracto de aportes en el 100 % de las personas
  probadas.
- **SC-019**: Los casos dorados de la investigación financiera (CDAT, ahorro a la vista, contractual,
  retención y GMF) coinciden al centavo, y las líneas de crédito existentes siguen pasando sus 32 casos
  dorados sin cambios tras introducir la naturaleza de la línea.

**Migración**
- **SC-011**: Una carga de 50.000 cuentas se revisa en menos de 10 minutos y, si tiene un error, no guarda
  nada.
- **SC-012**: Ninguna cooperativa se activa con diferencias de cuadre sin acta de aceptación.

**Cumplimiento y seguridad**
- **SC-013**: El 100 % de las rutas de captaciones y aportes exige un permiso propio; un usuario fuera de alcance recibe
  la misma respuesta que ante lo inexistente.
- **SC-014**: La verificación de la cadena de auditoría detecta el 100 % de las alteraciones y borrados
  inducidos en la prueba.
- **SC-015**: Ningún valor legal aparece fijo en el cálculo (verificación automática en verde).
- **SC-016**: El certificado tributario anual de cada titular cuadra con la suma de sus liquidaciones,
  retenciones y GMF del año al centavo.
- **SC-017**: Ninguna devolución de aportes perfora el mínimo no reducible del asociado ni el mínimo
  irreducible de la entidad.

**Uso**
- **SC-018**: El 90 % de los usuarios de oficina completa apertura, consignación y retiro en su primer
  intento sin ayuda, en la prueba con usuarios de la cooperativa piloto.

## Assumptions

- **Para quién es**: toda organización solidaria cliente del ERP; el perfil regulatorio decide qué
  líneas activa. COOFLOPAL no capta ahorro: usará aportes.
- **Un solo módulo**: las captaciones son parte de la Cartera financiera; reutilizan sus motores comunes,
  ciclo diario, contrato, matriz, migración, documentos, informes, permisos y auditoría tal como están al
  cerrar K1.
- **Moneda**: pesos colombianos; sin moneda extranjera ni UVR en las líneas de captación.
- **Volumen**: hasta 100.000 cuentas y 20.000 operaciones diarias por cooperativa, además de los
  créditos.
- **Usuarios**: asesores, cajeros (de Recaudo), analista de aportes, jefe de ahorros, tesorero,
  contador, oficial de cumplimiento, auditor y revisor fiscal.
- **Procesos diarios**: corren en el ciclo diario de la Cartera, después de medianoche, hora de
  Colombia, por cooperativa.
- **Documentos**: PDF guardado; entrega en oficina o por correo; sin portal del asociado.
- **Integraciones externas**: SICSES y DIAN reciben archivos que carga una persona; sin servicios en
  línea.
- **Valores legales**: se siembran con su fuente y «pendiente de validar» hasta que la contadora y el
  abogado los confirmen (ver P1 a P12).
- **Recaudo, Tesorería y SARLAFT** se especifican en sus propias features; hasta entonces, en prueba, un
  recaudador y una tesorería de prueba (Q3).
- **Reglamentos**: cada cooperativa tiene un reglamento de ahorro y unos estatutos que fijan lo que la
  norma deja abierto (plazos de CDAT, redención anticipada, inactividad, prelación del cruce, plazos de
  devolución); el ERP los recibe como parámetros.

### Decisiones por defecto (para revisión del dueño)

Si el dueño no responde, rige la propuesta.

**Alcance y nombres**
- **D1 · CDAT, no CDT.** En la interfaz el producto se llama «CDAT», que es el nombre del sector
  solidario; el CDT es de los bancos. Las pantallas actuales que dicen «CDT» cambian.
- **D2 · Una Cartera, tres naturalezas.** Las líneas de colocación, captación y aportes comparten el
  catálogo y el módulo; cada naturaleza se activa por separado y aportes no depende de que la cooperativa
  capte.
- **D3 · Ahorro permanente es línea de captación** de clase propia, ligado a la cuota de aportes en
  fondos de empleados; no es aporte.
- **D4 · Sin cuenta corriente, chequeras ni canje propio.** El cheque es un medio de Recaudo con días de
  disponibilidad.
- **D5 · Sin portal del asociado** (como D1 de la 013).

**Cálculo**
- **D6 · Causación diaria siempre**, aunque la liquidación sea mensual o trimestral: la piden el formato
  de saldos diarios y el fondo de liquidez.
- **D7 · Convención por defecto real/365 con equivalencia compuesta**; base 360 sólo si la línea la
  declara. Termina la mezcla actual de 360 y 365.
- **D31 · Método de saldo diario** por defecto en el ahorro a la vista; promedio y mínimo sólo si la línea
  los declara.
- **D8 · Tramos por rango total** (todo el saldo a la tasa del tramo en que cae) por defecto; escalonado
  como opción de la línea.
- **D9 · Redondeo al peso** por liquidación, no por día; la causación diaria guarda decimales.
- **D10 · Retención en cada abono o pago**, con el umbral diario de 0,055 UVT evaluado día por día y, si
  algún día lo alcanza, sobre el total; el umbral aplica al ahorro y **no** al CDAT mientras no se valide
  lo contrario (P2), que es la lectura más prudente frente al Decreto 572; descontando el componente
  inflacionario según la norma (P27), todo configurable.
- **D11 · Fecha valor hacia atrás**: hasta 5 días por defecto, configurable.

**Aportes**
- **D12 · La cuota de aportes no es un crédito.** Es un exigible de la línea de aportes que los
  recaudadores pagan por las operaciones de depósito del contrato; no tiene mora, calificación ni
  deterioro.
- **D13 · Revalorización sobre saldo promedio** del año por defecto; la asamblea puede elegir saldo al
  cierre.
- **D14 · Prelación del cruce al retiro**: primero obligaciones vencidas, luego las vigentes de mayor
  tasa, salvo que los estatutos digan otra cosa.
- **D15 · Descuento por pérdidas al retiro** desactivado por defecto; se activa por parámetro con acta.
- **D16 · El retiro nace en la Cartera** (decidido el 2026-10-14): solicitud, aprobación con acta,
  liquidación y devolución; Asociados sólo cambia el estado al recibir el aviso (FR-076, FR-124).

**CDAT y contractual**
- **D35 · Cargos apagados por defecto**; la cooperativa los activa línea por línea. Las operaciones
  gratuitas por período se cuentan por cuenta, no por titular.
- **D34 · Tasa negociada en la prórroga** por defecto **no** conserva el margen: se aplica la tabla y, si
  el titular lo pide, se negocia de nuevo.
- **D17 · Prórroga automática activada** por defecto, por el mismo plazo, a la tasa vigente de la línea
  para ese plazo, **sin** capitalizar intereses (se pagan al destino), con hasta 3 días hábiles de gracia
  sin causación.
- **D18 · Redención anticipada no admitida** por defecto; si la línea la admite, penalidad como
  reliquidación a la tasa de penalidad de la línea.
- **D19 · Vencimiento en día no hábil**: se paga el siguiente hábil sin interés adicional.

**Restricciones**
- **D20 · Límite inembargable aplicado** a las cuentas de ahorro de personas naturales, mientras el
  abogado no diga lo contrario (P5).
- **D21 · Inactividad a los 360 días** sin movimiento del titular, sigue causando, sin traslado a terceros
  (P7).
- **D32 · GMF** (decidido, P25): una cuenta exenta por persona; redención de CDAT abonada y traslado
  entre ahorro y CDAT gravados; tarifa, tope, política y hechos generadores configurables con vigencia,
  incluida la eliminación del impuesto.
- **D33 · Zonas.** Las zonas heredadas no se renuevan: las agrupaciones geográficas salen de las oficinas
  y de DIVIPOLA de la plataforma (Q2).
- **D22 · Entrega sin juicio de sucesión** habilitada hasta el tope vigente, con aprobación de segundo
  usuario (P6).

**Integración**
- **D23 · Validación previa** igual que los créditos (enmienda A8 de la 013): bloquea en pantallas, sella
  y alerta en contrato y procesos.
- **D24 · Causación por lotes resumida** por día por defecto; el resto en línea.
- **D25 · Débito automático y cruce al retiro** como operaciones internas de la Cartera, sin recaudador
  externo (E-01).
- **D26 · Nómina descuenta aportes y ahorros** en conceptos propios, por el contrato (E-02).

**Migración y seguridad**
- **D27 · Activación por naturaleza**: aportes y captaciones, cada una con su carga, su cuadre y su fecha,
  independientes de la de los créditos.
- **D28 · El historial heredado de movimientos no se migra** como transacciones; puede cargarse como
  información para el extracto del primer año.
- **D29 · Auditoría de diez años** y lecturas sensibles auditadas, como los créditos.
- **D30 · Rutas de la Cartera**: `/cartera/ahorros*`, `/cartera/cdat*` y `/cartera/aportes*` en pantalla y
  `/api/lending/...` en la API; el plan elige los nombres definitivos.

### Preguntas abiertas para el dueño

Las resueltas el 2026-10-14 quedan marcadas. Las de norma que siguen abiertas (P2 a P12 y P26) debe validarlas la contadora o el abogado; mientras tanto
rigen como parámetro con fuente y «pendiente de validar».

**Norma y tributario (contadora y abogado)**
- **P1 · Resuelta** (configurable desde la norma; ver Clarifications).
- **P2 · Umbral diario de retención** (0,055 UVT, art. 1.2.4.2.6): ¿aplica también a los CDAT después del
  Decreto 572? Cambia si el CDAT del ejemplo lleva retención.
- **P3 · GMF en fondos de empleados** y si la cooperativa usará el reparto de la exención entre varias
  cuentas (art. 881-1).
- **P4 · Umbrales de transacciones en efectivo** vigentes en 2026 para la declaración de origen de fondos.
- **P5 · Límite inembargable**: ¿aplica a los depósitos en solidarias?
- **P6 · Entrega sin juicio de sucesión**: ¿la permite el reglamento de cada cooperativa y con qué tope?
- **P7 · Cuentas abandonadas**: ¿aplica la Ley 1777 a las solidarias, o sólo la inactividad del
  reglamento?
- **P8 · Estructura de los formatos SICSES** de captaciones, saldos diarios y fondo de liquidez.
- **P9 · Exógena**: campos vigentes de 1019 y 1020 y cómo se reportan aportes y ahorro permanente.
- **P10 · Certificado tributario**: ¿incluye aportes y revalorización?
- **P11 · Límite legal de revalorización** (IPC del año anterior) y su base.
- **P12 · Límite individual de participación** en aportes para personas naturales y jurídicas.
- **P25 · Resuelta** (D32).
- **P26 · Bonificación del ahorro contractual**: ¿se reconoce al cumplirse o se provisiona durante el
  contrato?
- **P27 · Resuelta** (configurable, por defecto según la norma; FR-021).

**Producto (dueño)**
- **P13 · Productos de COOFLOPAL**: ¿sólo aportes, o también ahorro permanente o contractual ligado a
  aportes?
- **P14 · Cuota de aportes de COOFLOPAL**: ¿valor fijo, porcentaje del ingreso o fracción del salario
  mínimo? ¿Se cobra por nómina de las empresas, por caja o ambas?
- **P15 · ¿Se necesita el débito automático** de cuotas de crédito desde cuentas de ahorro en la primera
  cooperativa que capte?
- **P28 · Base de días y método de saldo** de cada línea en la primera cooperativa que capte (365 o 360;
  saldo diario o mínimo).
- **P16 · Prórroga del CDAT**: ¿a la tasa vigente o a la pactada? ¿Capitaliza intereses? ¿Cuántos días de
  gracia y con qué aviso previo? ¿Máximo de prórrogas?
- **P17 · Redención anticipada**: ¿alguna línea la admitirá? ¿Penalidad por reliquidación o
  descuento fijo?
- **P18 · Inactividad**: ¿cuántos días y qué efecto (sigue causando, cobra cuota de manejo, traslado)?
- **P19 · Resuelta** (FR-128): catálogo de cargos configurable por línea, apagado por defecto.
- **P20 · Resuelta** (FR-033): a nombre del menor, manejada por su representante hasta los 18 años.

**Integración y calendario (dueño)**
- **P21 · Resuelta** (Q3): después de la 013 completa; todo antes de la salida de COOFLOPAL.
- **P22 · Resuelta** (D16): el retiro nace en la Cartera.
- **P23 · Bienestar social**: ¿habrá feature para auxilios y actividades? (Sus tablas heredadas se
  retiran igual, Q2.)
- **P24 · Fondo de liquidez**: ¿alguna cooperativa cliente es de categoría plena y necesita las bandas
  de madurez desde el principio?

## Dependencias

- **013 Cartera**: esta feature **es** una extensión de la Cartera; reutiliza todo lo construido y la
  enmienda (E-01). Empieza cuando la 013 esté completa (Q3). Si el plan de la 013 regenera
  migraciones o renumera enums después de K1, esta feature debe esperar a que se congelen.
- **012 Inventario y Comercial**: facturación electrónica de los cargos gravados (FR-128), bandeja de mensajes, idempotencia, parámetros con vigencia, aprobaciones con
  atribuciones, plantillas de importación, medios de pago, perfil tributario de la persona.
- **009 Contabilidad**: destino de los hechos, matriz, comprobantes, cierre y conciliación (enmienda
  E-03).
- **010 Nómina**: descuentos por el contrato (enmienda E-02); formatos de archivo plano.
- **008 Personas**: persona maestra, buscador, alta en un paso, Asociados (enmienda E-04).
- **011 Adjuntos**: soportes de embargos, fallecimientos, actas y declaraciones.
- **Recaudo y Tesorería** (sin especificar): la caja, los medios y los giros. Sin ellas no salen en vivo
  ni las líneas de captación ni las de aportes (Q3); todo se termina antes de la salida de COOFLOPAL (Q1).
- **SARLAFT** (sin especificar): gestión de alertas y reportes a la UIAF con los datos que deja esta
  feature; decide las tablas SIPLA heredadas.
- **Lo que deben validar**: el dueño (D1 a D35, P13 a P20, P22 a P24, P28), la contadora (P2, P3, P8 a
  P12, P26, la matriz y los casos dorados) y el abogado (P4 a P7).
