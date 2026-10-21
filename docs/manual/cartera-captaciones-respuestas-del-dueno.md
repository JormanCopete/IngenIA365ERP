# Captaciones y aportes: lo que cambió con las respuestas del dueño (2026-10-21)

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
  adjunto; la prórroga con «tasa pactada» conserva esa tasa; en la migración, el CDAT con giro puede traer su banco y su cuenta.
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

## Aún no construido

Fecha valor en período liquidado con ajuste en el período abierto, cobro de cargos pendientes al cancelar, recálculo de embargos en cada consignación, retención y retirados en los
excedentes de la asamblea, giro rechazado de excedentes o de fallecimiento, y GMF en la entrega por fallecimiento y en no reclamados.
