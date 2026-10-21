# Captaciones y aportes: líneas, apertura de cuentas, seguridad y puesta en marcha

> Feature 014, US1, US2, US9 y US16 (entregas K7 y K8). Para el jefe de captaciones y la contadora. Contrato:
> `specs/014-ahorros-aportes-cdt/contracts/api.md` §3 a §5 y §16, con las enmiendas de `research.md` §8, §9, §16 y §17. Índice y pendientes:
> [cartera-captaciones.md](cartera-captaciones.md).

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Líneas de captación (catálogo común, filtrado) | `/cartera/lineas?naturaleza=captacion` (menú «Líneas de captación») | `Lending.DepositLines.View` · `.Manage` |
| Línea de aportes | `/cartera/lineas?naturaleza=aportes` (menú «Líneas de aportes») | `Lending.ContributionLines.View` · `.Manage` |
| Cuentas de ahorro y aportes: lista, apertura | `/cartera/ahorros` | `Lending.Accounts.Read` · `Lending.Accounts.Open` |
| Ficha de la cuenta (saldo, movimientos, intereses, contrato, documentos) | `/cartera/ahorros/{id}` | `Lending.Accounts.Read` |
| Titulares, firmantes, representantes y beneficiarios | ficha de la cuenta | `Lending.Accounts.Parties.Manage` |
| Marcar o terminar la cuenta exenta de GMF | ficha de la cuenta (`/api/lending/gmf-exemptions`) | `Lending.GmfExemptions.Manage` |
| Niveles de aprobación de retiros y consignaciones en efectivo | `/cartera/captaciones/aprobaciones` (menú «Aprobación de movimientos») | `Lending.Attributions.View` |
| Saldos iniciales y activación | `/cartera/captaciones/migracion`, `/cartera/captaciones/activacion` | `Lending.Deposits.Migration`, `Lending.Deposits.Activate` |
| Textos de los avisos al titular (`CAMBIOTASA`, `MAYOREDAD`…) | `/cartera/plantillas-de-mensaje` | los de la 013 |

`Read` muestra datos del titular y su lectura queda en la auditoría; `View` no muestra titulares (convención de la 013).

## 2. Las líneas

Toda línea es una **identidad común** (código, nombre, alcance por oficina) con versiones de condiciones **inmutables con vigencia**: una versión
nueva rige desde su fecha y nunca hacia atrás (`Lending.DepositLine.Retroactive`). La clase de una línea de captación (a la vista, contractual,
permanente o CDAT) la fija su primera versión y no cambia. Una línea nace en borrador y se **activa** al darla de alta con «activa» o con
«reabrir»; activarla exige la entrega desplegada (aportes K7, captaciones K8) y, en captaciones, el **perfil regulatorio** autorizado a captar
(`/admin/perfil-regulatorio`: sin autorización la cooperativa sólo ofrece aportes; «sólo de asociados» obliga a exigir asociado en cada versión).
Las tasas se digitan en puntos y viajan como fracción. Hay **una sola línea de aportes activa** por cooperativa.

**Una versión de captación dice**:

- quién puede abrir: exige asociado, admite cotitulares, menores y personas jurídicas, retiros parciales, oficinas;
- la **tasa**: fija o variable (índice, margen, piso, techo, reajuste), con **tramos** por saldo (todo el saldo a la tasa del tramo, defecto D8, o
  escalonado); si declara **tasa pactada** (la sellada al abrir) y si admite **tasa negociada** (CDAT);
- el cálculo: **método de saldo** (diario por defecto, D31; promedio o mínimo), convención de días (real/365 por defecto, D7), equivalencia y
  redondeo, saldo mínimo y saldo mínimo para causar, periodicidad y día de liquidación;
- los topes: retiro por operación y por día, efectivo por operación y por día, depósito mínimo de apertura;
- la tributación: la tarifa de retención que nombra (vacía = la de su clase) y si está sujeta a GMF;
- la vida de la cuenta: días de inactividad, si causa inactiva, si suspende la causación con el titular fallecido o con embargo total, días de
  aviso de un cambio de tasa;
- CDAT y contractual: plazos, periodicidades, prórroga, gracia, redención, cuotas, bonificación e incumplimiento (ver
  [cartera-captaciones-cdat-y-contractual.md](cartera-captaciones-cdat-y-contractual.md));
- el **catálogo de cargos** (apagado por defecto, D35; hoy se registra por API: el editor todavía no lo ofrece);
- en el permanente, si sólo se retira al desvincularse (lo usa el fondo de liquidez).

**Aviso de cambio de tasa.** En una línea a la vista sin tasa pactada, si la versión nueva cambia la tasa y hay titulares vivos, se les avisa con
el mensaje `CAMBIOTASA` con la anticipación mayor entre la de la versión y `Captaciones.AnticipacionAvisoCambioDeTasaDias` (vacío). Nunca
impide registrar la versión.

**La versión de aportes dice**: el mínimo no reducible y la cuota (valor fijo, fracción del ingreso o fracción del salario mínimo, con su
redondeo, periodicidad y día), la parte que va a **ahorro permanente** (con su línea), si admite extraordinarios, el límite de participación propio,
qué hacer con el exceso (rechazar o devolver por decisión), si admite cesiones entre asociados, el plazo de devolución y si se descuentan pérdidas
al retiro (D15, apagado por defecto). En un fondo de empleados el tope de la cuota (`Aportes.TopeCuotaFondoEmpleados`, 0,10) y la fracción mínima
a aportes (`Aportes.FraccionMinima`) se controlan en la línea cuando la cuota es fracción del ingreso; con valor fijo o salarios mínimos los
controla la cuota de cada asociado.

## 3. Abrir una cuenta

En `/cartera/ahorros`, «Abrir cuenta». Reglas:

- **Datos mínimos de la persona**: documento, nombres o razón social y, en persona natural, la **fecha de nacimiento** (sin ella no se sabe si es
  menor). Autorización de tratamiento de datos del titular y de cada cotitular.
- **Origen de fondos**: se pide sólo si el depósito de apertura de la línea alcanza `Efectivo.UmbralDeclaracion` (vacío = no se pide).
- **Listas restrictivas**: se consultan al abrir; una coincidencia deja la cuenta **en apertura** con el bloqueo automático y la alerta de la 013,
  que sólo libera el oficial. Con el canal de listas caído no se abre.
- **Segregación**: quien abre no aprueba la apertura excepcional; abrir exige un usuario con persona.
- **Número**: el consecutivo de la línea con su código como prefijo (`AHVISTA-000001`); toda cuenta abierta tiene número.
- Una cuenta que exige depósito de apertura queda **en apertura** hasta que el primer depósito por el contrato alcance el mínimo.
- El CDAT se constituye por su propia pantalla (`/cartera/cdat/nuevo`) y el contractual pide al abrir la **cuota y el plazo** (el contrato se sella
  en la apertura).
- Una persona con el retiro de asociado aprobado, liquidado o por rehacer no abre cuentas; el ahorro permanente es uno por persona.

## 4. Titularidad, menores y beneficiarios

- **Papeles**: titular, cotitular (con su proporción; todas o ninguna, y el titular absorbe el redondeo), firmante (con su límite de firma),
  representante legal (con su fundamento: padre o madre, guardador, orden judicial, representante de persona jurídica) y beneficiario.
- **Manejo** indistinto o conjunto: el conjunto exige al menos dos firmantes.
- **Aportes**: sólo titular y beneficiarios; los aportes de un menor no llevan representante.
- **Beneficiarios**: en porcentaje (hasta cuatro decimales) y suman exactamente 100 %; la **constancia** se adjunta siempre, al abrir y al
  cambiarlos (D34). Cambian en conjunto: el conjunto anterior se cierra la víspera.
- **Menores**: la cuenta es del menor y la maneja su representante con el soporte de la representación. Al cumplir
  `Captaciones.MayoriaDeEdadAnios` (18) el proceso diario cierra la vigencia del representante la víspera del cumpleaños, marca la cuenta
  «actualizar datos», anula los retiros del representante en espera y avisa a los dos con `MAYOREDAD`. La marca se levanta con «actualización de
  datos».
- Los soportes (representación, constancia, firma, foto, origen de fondos) se adjuntan al **titular** y no se borran.
- La ficha de la persona en Maestros cuenta sólo las cuentas donde es **titular**; las de cotitular se ven en una ventana aparte (D20).

## 5. Cuenta exenta de GMF

Con la política `SingleMarkedAccount` (defecto) cada persona tiene **una** cuenta marcada exenta con vigencia: marcar una segunda se rechaza
(`Lending.Gmf.AlreadyMarked`) salvo que se pida reemplazar la vigente en el mismo paso. No se marca ni se termina hacia atrás. Marcar o
terminar la que rige hoy **reclasifica** los saldos de la cuenta (`CaptacionReclasificada`); la que rige desde una fecha futura la reclasifica el
proceso diario ese día. Cómo se calcula el GMF: [cartera-captaciones-movimientos-intereses-retencion-y-gmf.md](cartera-captaciones-movimientos-intereses-retencion-y-gmf.md) §8.

## 6. Seguridad, atribuciones y auditoría

- **Segregación** (regla fija, la cooperativa puede sumar pares pero no quitar éstos): quien registra no aprueba en nueve expedientes —apertura,
  solicitudes de aportes, retiro del asociado, su devolución, cargos de la cuenta, embargo, tasa negociada, entrega por fallecimiento y decisión de
  asamblea—. La comparación es por usuario y por persona.
- **Alcance**: la cuenta tiene que estar en el alcance de quien opera (oficina y línea) y también **la otra cuenta** de una operación (destino de un
  traslado, cuenta que recibe una cesión, crédito que paga un débito automático). Fuera del alcance la respuesta es la misma que «no existe».
  Embargos, fallecimientos, el resumen de aportes y los certificados tributarios exigen el alcance sobre **todas** las cuentas de la persona; la
  asamblea, el fondo de liquidez y los reportes regulatorios exigen **alcance total**.
- **Atribuciones con monto**: un retiro, un traslado o una consignación en efectivo por encima del monto de `Lending.Accounts.Withdraw` o
  `.Deposit` del rol queda **en espera de aprobación** (respuesta 202) y no mueve saldo; se repite con la aprobación concedida. Cada cooperativa
  configura los niveles (umbral y permiso de cada uno) en «Aprobación de movimientos» (D53); sin política rige la regla fija de un nivel con
  aprobador presente y segundo factor. La redención de un CDAT y la liquidación del retiro también son atribuciones con monto.
- **Lecturas auditadas**: el saldo, el detalle, el extracto, el libro, la lista de cuentas de una persona, la vista previa de la cancelación, el
  resumen de aportes, los embargos y los casos de fallecimiento dejan su evento de lectura en la auditoría de Cartera (encadenada, diez años).

## 7. Puesta en marcha: saldos iniciales y activación

Cada naturaleza se activa **aparte** y **después de los créditos** (`Lending.Activation.LoansFirst`): aportes desde K7, captaciones desde K8. El
procedimiento completo está en [captaciones-puesta-en-marcha.md](../operaciones/captaciones-puesta-en-marcha.md); en resumen:

1. **Equivalencias de productos** (código heredado → línea nueva), primero. Las líneas se crean por pantalla: no hay plantilla de importación de
   líneas.
2. **Carga por partes** en `/cartera/captaciones/migracion`: cuentas, titulares, cuentas de aportes, cuotas pendientes, CDAT, bloqueos y
   embargos, cuentas exentas de GMF y el acumulado tributario del año. Revisión previa, **todo o nada**, errores por fila y columna. Una carga
   aplicada reemplaza a la anterior de su naturaleza.
3. **Cuadre** contra las cifras de referencia de la cooperativa por línea y concepto; las diferencias se aceptan con **acta**.
4. **Activar** (`/cartera/captaciones/activacion`): fecha = corte + 1, plazo de fecha valor = corte + `Captaciones.DiasMaximosFechaValor`. Nace el
   saldo inicial por concepto **sin hechos contables** (el único hecho es el informativo `CaptacionesActivadas`).
5. **Requisitos** (en todos los ambientes, D14): créditos activados, roles de captaciones creados, perfil que autoriza las clases, matriz completa
   para las líneas activas, recaudador y Tesorería registrados con su contrato (fuera de producción valen el recaudador `TST` y la tesorería `TES`
   de prueba) y, en producción, K7 a K11 desplegadas (Q1).

Lo que hay que saber de lo migrado: el interés causado al corte se liquida en la primera liquidación sin volver a causarlo; una cuenta inactiva nace
inactiva **desde el corte**; el número migrado se conserva; el CDAT migrado no recibe título al activar (se emite desde su ficha); el contractual
migrado trae cuotas pagadas y atrasadas y, si venía incumplido, nace incumplido desde el corte.

**Advertencia (no bloquea).** Una cuenta migrada activa se inactiva como cualquier otra: la primera noche tras activar, la que tenga su último movimiento
anterior al plazo de inactividad de su línea queda inactiva y su titular recibe el aviso. La revisión previa dice cuántas son
(`Lending.Migration.InactivityOnActivation`); revise la fecha del último movimiento de la carga antes de activar.

## 8. Pendiente de validar

Mayoría de edad, días de fecha valor, umbral de origen de fondos, tope de la cuota de fondos de empleados, fracción mínima a aportes, límites de
participación (P12), aviso de cambio de tasa y textos de los avisos: ver [cartera-captaciones.md](cartera-captaciones.md).
