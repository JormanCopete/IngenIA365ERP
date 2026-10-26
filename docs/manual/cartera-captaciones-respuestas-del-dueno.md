# Captaciones y aportes: lo que cambió con las respuestas del dueño (2026-10-21 y 2026-10-22)

Para quien administra Cartera y para la contadora. El detalle técnico está en `specs/014-ahorros-aportes-cdt/research.md` §27.

## Lo que cambia en la operación

- **Archivo de descuentos por pagaduría.** Si una línea de aportes o de ahorro se descuenta por pagaduría y no tiene código de concepto de la pagaduría, el archivo **no se
  genera**: el mensaje dice qué líneas faltan y dónde asignarlas (convenio › «Grupos y equivalencias»).
- **Cargar líneas desde Excel.** Líneas de captación (hojas `Lineas` y `Tramos`) y la línea de aportes tienen su plantilla (descargar vacía, revisar, aplicar), como las líneas de
  crédito. Los cargos y la tasa variable se configuran en pantalla.
- **Tarifas anteriores a la activación.** Mientras las captaciones o los aportes no estén activados, una versión de línea puede empezar en una fecha pasada (para dejar la historia
  de tarifas); activados, empieza hoy o después.
- **Quién activa.** El «jefe de captaciones» no activa captaciones ni acepta diferencias de la migración: lo asigna la gerencia a quien dirige la puesta en marcha.
- **Aprobaciones sobre la atribución.** Quien aprueba debe tener en su alcance la oficina y la línea de la cuenta.
- **CDAT.** Los intereses pueden abonarse a la cuenta de **otra persona** con el permiso «Pagar intereses de CDAT a un tercero» y la instrucción escrita del titular subida como
  adjunto (el sistema lo admite, pero la pantalla de constitución del CDAT todavía sólo ofrece cuentas del titular: **pendiente** en pantalla); la prórroga con «tasa pactada» conserva esa tasa; en la migración, el CDAT con giro puede traer su banco y su cuenta.
- **Ahorro contractual.** Si incumple y la línea dice «cancelar» sin instrucción de destino, el saldo va a la cuenta a la vista del asociado.
- **Embargos.** Cuando hay varios, primero se retiene el de alimentos y después los demás por fecha de recepción del oficio.
- **Retención sobre rendimientos.** En el perfil de la persona hay «Obligado a llevar contabilidad»: a la persona natural obligada no se le descuenta el componente inflacionario.
- **Documentos al titular.** Cada cooperativa puede programar los lotes de extractos y certificados (`Documentos.LotesProgramados`); el lote a mano sigue.
- **Títulos de CDAT migrados.** Se emiten al activar las captaciones.
- **Liquidez.** Los activos líquidos pueden leerse de Contabilidad por prefijos de cuenta (`RiesgoDeLiquidez.ActivosLiquidosContables`); el retiro esperado del ahorro a la vista se estima
  con estadística cuando el libro tiene un año de historia; el seguro de depósitos reparte las cuentas conjuntas en partes iguales; hay una vista «Depósitos y aportes por banda de
  liquidez» (insumo del F29); y el tesorero registra tipo, intereses y capitalización de cada CDT/CDAT del fondo (9067).

## Para la contadora (pendiente de validar)

`Documentos.LotesProgramados`, `SeguroDeDepositos.RepartoDeCotitulares`, `RiesgoDeLiquidez.RetirosALaVistaMetodo`, `.DiasDeHistoriaDelRetiro`, `.ConfianzaDelRetiro` y
`.ActivosLiquidosContables` nacen con un defecto que ella confirma o cambia con vigencia.

## Lo que se construyó después (2026-10-20 a 2026-10-22)

Lo que el 2026-10-21 figuraba como «aún no construido» ya está, junto con las decisiones del 2026-10-22:

- **Fecha valor en un período ya liquidado**: la diferencia de intereses se ajusta en el período abierto (E256).
- **Cancelar con cargos por cobrar**: se cobran del saldo dentro de la misma cancelación (E255).
- **Embargos**: se recalculan en cada consignación posterior al oficio (E254).
- **GMF** en la entrega por fallecimiento y en los saldos no reclamados (E253).
- **Asamblea**: los excedentes de quien no tiene cuenta de ahorro se capitalizan en sus aportes (E257); el asociado retirado durante el ejercicio
  participa a prorrata del tiempo y se le gira lo suyo (E259); se retiene en la fuente sobre la revalorización y los retornos con el concepto, las
  tarifas y la base mínima en UVT que fije cada cooperativa (`Asamblea.Retencion.*`; sin tarifa no se retiene) (E260); la base de los retornos la
  define la asamblea y se sube con la plantilla «Base de los retornos de la asamblea» (E261).
- **Giro rechazado** de un excedente o de una entrega por fallecimiento: se vuelve a girar y el valor sigue por pagar al destinatario (E258).
- **Cuentas migradas**: se inactivan como cualquier otra; la revisión previa de la carga avisa cuántas quedarían inactivas la primera noche (E262).
- **Ahorro contractual**: la instrucción de vencimiento se puede retirar con motivo; sin instrucción, al vencer el contrato queda a disposición del
  titular y no se mueve dinero (E263, decisión del 2026-10-22).
- **Avisos al titular**: salen por **correo electrónico real**; **SMS y WhatsApp esperan proveedor** y, mientras tanto, se registran como simulados
  sin enviarse (E264, decisión del 2026-10-22).

## Pendiente

- Registrar y levantar **bloqueos** de una cuenta no tiene pantalla todavía (la ficha sólo los muestra dentro del disponible).
- Pagar los intereses de un CDAT a la cuenta de un tercero no se ofrece todavía en la pantalla de constitución.
- La pantalla del lote de documentos.
