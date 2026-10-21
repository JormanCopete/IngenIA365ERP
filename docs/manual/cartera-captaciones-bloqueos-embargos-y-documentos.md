# Captaciones: bloqueos, embargos y documentos al titular

> Feature 014, US11 y US13 (entregas K8 y K9). Para el jefe de captaciones, la contadora y el abogado de la cooperativa. Contrato:
> `specs/014-ahorros-aportes-cdt/contracts/api.md` §8 y `documentos.md`, con las enmiendas de `research.md` §20 y §21. Toda la norma citada aquí es
> **lectura del equipo pendiente de validar** por el abogado y la contadora. Índice y pendientes: [cartera-captaciones.md](cartera-captaciones.md).

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Embargos: lista, registro del oficio, retenido, respuestas, consignación, desembargo | `/cartera/embargos` | `Lending.Accounts.Read` · `Lending.Garnishments.Register` · `.Release` |
| Bloqueos de una cuenta | ficha de la cuenta (`/api/lending/accounts/{id}/holds`) | `Lending.Holds.Manage` |
| Documentos de la cuenta y de la persona (saldo a una fecha, extracto, emisión) | fichas `/cartera/ahorros/{id}`, `/cartera/cdat/{id}`, `/cartera/aportes/{persona}` | `Lending.Accounts.Read` · `Lending.Documents.Issue` |
| Documentos emitidos de Cartera y su verificación | `/cartera/documentos`, `/cartera/verificar` | los de la 013 |
| Informe `garnishments` | `/cartera/informes` | `Lending.Reports.View` |

## 2. Bloqueos

Por pantalla se registran y levantan sólo los motivos **interno**, **garantía de un crédito**, **firma pendiente** y **otro**; los de fallecimiento,
embargo y listas restrictivas los ponen y quitan sus procesos. Un bloqueo es por valor (no supera el disponible) o **total** (deja el disponible en
cero). El de garantía exige un crédito vivo y no se levanta mientras el crédito viva.

## 3. Embargos

- **Rige al registrarse** (CGP art. 593 num. 10: se consuma con el oficio); no espera aprobación. Quien lo registra **no lo levanta**. Registrar
  también pone el bloqueo de deudor que la originación de créditos ya mira.
- El oficio dice autoridad (juzgado, autoridad tributaria, otra entidad pública), número, proceso, demandante, **valor de la medida**, si es **por
  alimentos** y la cuenta de depósitos judiciales. Si nombra aportes, se registra solo el aviso de «aportes inembargables» (los aportes nunca se
  embargan); un oficio que sólo nombra aportes queda rechazado.
- **Qué se retiene**: el motor suma las cuentas de la persona, protege hasta el **límite inembargable** y retiene el exceso hasta el tope pendiente
  de la medida, en el orden de `Embargos.OrdenDeCuentas` (vacío = por antigüedad):
  - **judicial**: el límite `LIMITE_INEMBARGABLE` (valor nacional, sin semilla) sobre **la suma** de las clases protegidas
    (`Embargos.ClasesProtegidas`: a la vista, contractual y permanente; el CDAT no es depósito de ahorro);
  - **coactivo** (DIAN y entidades públicas, ET art. 837-1): `EMBARGO_COACTIVO_SMMLV` (25) × `SMMLV` en la **cuenta más antigua**;
  - **por alimentos**: sin límite (CGP art. 594 num. 2);
  - **persona jurídica**: sin límite (`Embargos.LimiteSoloPersonasNaturales`);
  - las clases de `Embargos.ClasesInembargables` nunca se retienen (en un fondo de empleados, el permanente);
  - el límite es de la **persona**: lo que retiene un embargo anterior no se repite (orden de recibo).
- **Recálculo**: el ciclo diario recalcula cada noche a las personas con embargos activos (subpaso «Embargos», antes de la causación). Un depósito
  posterior al oficio **no** se retiene en línea: se retiene esa noche (pregunta abierta al dueño).
- **Causación**: la cuenta totalmente embargada deja de causar si la línea lo dice y vuelve a causar cuando deja de estarlo. El CDAT sigue su plan
  y, si está embargado al vencer, queda vencido a disposición.
- **Responder al juzgado**: cinco clases (acuse, informe de saldos, aportes inembargables, aviso de consignación, acuse del desembargo), con la
  plantilla de la entidad (`Embargos.PlantillasDeRespuesta`) o la estándar, y el soporte firmado. Cada respuesta es un **documento emitido** con
  código de verificación (prefijo `RJZ-`). El plazo `Embargos.DiasHabilesParaResponder` (3) **avisa y no bloquea**.
- **Consignar**: una operación por cuenta hasta lo retenido, con su orden de giro al juzgado. El GMF de la consignación hoy **no** se cobra.
  Si Tesorería rechaza el giro, el saldo vuelve retenido para el mismo embargo.
- **Desembargar** con un valor menor que la medida la reduce; sin valor, la levanta toda.

Ejemplos (casos dorados `embargo-*`, límites de prueba):

| Caso | Resultado |
|---|---|
| Dos cuentas que suman 70.000.000 (40.000.000 a la vista, 30.000.000 permanente), límite 50.000.000, embargo judicial de 15.000.000 | se retienen **15.000.000** de la cuenta a la vista; lo protegido queda en las cuentas que el orden pone al final |
| Las mismas cuentas con un embargo por alimentos de 60.000.000 | el límite no aplica |
| Cobro coactivo con 25 SMMLV de prueba (35.587.500) | protege sólo la más antigua: retiene 4.412.500 de ella y los 30.000.000 de la otra |
| Segundo embargo de la misma persona | no queda nada embargable |
| CDAT con el permanente inembargable de un fondo de empleados | se retiene del CDAT |

Hechos: `CaptacionReclasificada` al poner o quitar la marca de embargo (comprobante `CN`; sin saldo no nace nada; no pasa por la validación previa) y
`EmbargoConsignado` con la orden de giro (`MA`).

## 4. La migración de embargos

La parte «bloqueos y embargos» de la carga trae los embargos vivos con lo retenido **al corte**; al activar nacen activos, sin soporte (se adjunta
después) y sin recálculo hasta la noche siguiente. Los fallecimientos migrados se registran por la pantalla después de activar.

## 5. Documentos al titular

| Documento | Qué certifica | Reglas |
|---|---|---|
| **Extracto** de cuenta | saldo inicial, una línea por operación por fecha valor (lo causado sin operación, una línea por mes), retención, GMF, intereses abonados, saldo final; en aportes, permanente y contractual, las cuotas | pedirlo dos veces sin movimientos posteriores devuelve la **misma copia** sin consumir número |
| **Certificado de saldo** | el saldo a una fecha | no a una fecha futura |
| **Certificado tributario** de captaciones | por cuenta en que la persona fue titular o cotitular: saldo al 31 de diciembre y GMF del año **por su proporción**, rendimientos, componente inflacionario (no constitutivo de renta), base y retención, la bonificación del contractual y lo migrado del año; aportes sólo con `Documentos.CertificadoTributarioIncluyeAportes` (P10) | exige el año cerrado; la suma de los certificados de todos los titulares es exactamente lo de la cuenta |
| **Certificado de retención** (ET art. 381) | por clase de captación y tarifa, con la leyenda `Documentos.LeyendaCertificadoRetencion` | el plazo del último día hábil de marzo es informativo |
| **Título del CDAT** «no negociable» | la versión de condiciones vigente (`TIT-{consecutivo}-{versión}`) | lo emite la operación que sella la versión; ver [cartera-captaciones-cdat-y-contractual.md](cartera-captaciones-cdat-y-contractual.md) §5 |
| **Constancia de aportes** y **paz y salvo** | el saldo de aportes y que no debe cuotas | el paz y salvo se mide por la fecha valor de la última operación |
| **Liquidación del retiro del asociado** | la liquidación aprobada | la emite el proceso al liquidar |
| **Respuesta al juzgado** | lo dicho al juzgado ese día | nunca se anula por un reverso |

Todos llevan código de verificación, se emiten para descargar o enviar por correo (plantilla `ENVIODOC`, editable), y el texto propio de la
cooperativa por clase va en `Documentos.TextosDeCaptaciones`. **Reversar una operación anula** los documentos que la vieron, según la clase: el
extracto si la fecha valor cae hasta el fin del período, el certificado de saldo y la constancia hasta su corte, el tributario hasta el 31 de
diciembre de su año, el de retención sólo por una operación tributaria, el paz y salvo si la operación pagó cuotas.

**Lote**: `POST /api/lending/documents/batches` emite los certificados tributarios o de retención del año (uno por persona con productos) o los
extractos del último período de `Documentos.PeriodicidadDeExtractos` (uno por cuenta), dentro del alcance de quien lo corre; corre dentro de la
petición y todavía no tiene pantalla (pregunta abierta: tarea de fondo).

Lo que el extracto todavía no dice: la exención del GMF que queda en el mes (dice la usada), el estado de la factura de un cargo gravado y la tasa
sellada del CDAT o del contractual (la traen el título y el contrato).

## 6. Pendiente de validar

Límite inembargable y su aplicación a las cooperativas (P5), los 25 SMMLV, clases protegidas e inembargables, sólo personas naturales, textos y
plazo de las respuestas, el GMF de la consignación; contenido del certificado tributario y si lleva aportes (P10), leyenda y ciudad del certificado de
retención, reparto del GMF entre cotitulares, periodicidad de los extractos: [cartera-captaciones.md](cartera-captaciones.md).
