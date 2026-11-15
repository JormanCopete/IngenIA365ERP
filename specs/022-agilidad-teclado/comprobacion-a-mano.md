# Comprobación a mano — feature 022

El repositorio no tiene pruebas de navegador, así que esta lista se recorre en DEV y en QA, en la web y en la app MAUI.

## Toda la aplicación
- [ ] En un formulario (Maestros › Personas), Enter recorre los campos. En el último lleva a «Guardar» y otro Enter guarda.
- [ ] Shift+Enter retrocede.
- [ ] Una lista de Syncfusion abierta elige con Enter y no salta un campo.
- [ ] Un área de texto hace salto de línea con Enter.
- [ ] Los buscadores que ya usaban Enter siguen funcionando: comprobante contable, árbol de cuentas, productos, cuentas de ahorro, verificar documentos, conteo.
- [ ] F1 muestra la ayuda junto al campo; Esc la cierra sin que la pantalla reaccione.
- [ ] Preferencias › Teclado apagado: Enter vuelve a ser el del navegador. Encendido de nuevo, avanza.

## Buscador de personas
- [ ] «perez juan» (minúsculas, sin tilde) encuentra a Juan Pérez.
- [ ] El documento completo + Enter lo elige de una.
- [ ] ↓ con el campo vacío muestra los recientes, y sólo los de esa cooperativa.
- [ ] Alt+N abre el alta con el documento puesto.

## Caja de Recaudo
- [ ] Una cuota y una consignación con efectivo y transferencia, sin ratón.
- [ ] Esc nunca cancela; Ctrl+Alt+X pregunta antes de cancelar.
- [ ] La declaración de origen de fondos adjunta el archivo.
- [ ] Tirilla y carta con Ctrl+P y Ctrl+Shift+P.

## POS
- [ ] Un código desconocido, luego F2: la búsqueda trae el texto y tiene el foco; se ven precio, disponible y empaques.
- [ ] Efectivo con vueltas sin ratón: Alt+1… y Alt+0.
- [ ] Transferencia en un punto con cuentas habilitadas: la cuenta se pide (o se propone la única) y el comprobante contable va a esa cuenta.
- [ ] Cheque al consumidor final: se rechaza.

## Factura de oficina
- [ ] Cinco líneas con Enter, sin pulsar Guardar; el total aparece solo.
- [ ] Pago con tarjeta y confirmación con F10; la pantalla se queda; Ctrl+P imprime; Enter abre otra.
