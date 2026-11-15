# Canal digital del asociado — manual de la cooperativa (feature 021)

El canal digital es la página web (y la app de tiendas) donde el asociado consulta sus productos, paga en línea por la pasarela,
descarga extractos y certificados, recibe avisos y solicita créditos. Es un sistema aparte del ERP que **nunca toca la base de la
cooperativa**: todo lo que muestra o hace lo pide al ERP. Se administra desde el menú **Canal digital** del ERP.

## Puesta en marcha, en orden

1. **Configuración** (`Canal digital › Configuración`, permiso `Channel.Settings.Manage`): nombre que ve el asociado, colores,
   contacto de soporte, minutos de inactividad, vencimiento de la sesión de pago, si se pide código para pagar, política de
   excedentes (anticipo o saldo a favor), días de anticipación del aviso de cuota próxima, si se habilita a personas que no son
   asociadas. Publicar los **textos legales** (términos, política de datos y preguntas frecuentes): sin términos y política
   publicados, el canal no se enciende. Después, **Encender**.
2. **Destinos de pago** (`Channel.Destinations.Manage`): uno por sitio de la pasarela, ligado a un **convenio de recaudo de clase
   pasarela** (de él salen la cuenta bancaria, la comisión y el formato del reporte diario), con los medios (PSE, tarjeta) y la
   **hora de corte de los reversos** (20:00 por defecto). Enviar las **credenciales** del sitio: viajan al canal, que las cifra y
   las verifica; el ERP no las guarda. La URL de notificación que hay que registrar en la pasarela se muestra en la pantalla.
3. **Conceptos publicados**: qué productos ve y paga el asociado —créditos (por línea o todas), aportes, ahorro contractual,
   ahorro permanente o programado y **facturas pendientes** de cuentas por cobrar comerciales—, con sus formas de pago (exigible,
   parcial con mínimo, abono a capital con la opción preseleccionada, cancelación total), si se combinan en un pago, el destino
   (sin destino, sólo consulta) y qué documentos se publican (extracto, certificado, certificado tributario). Las líneas de crédito
   publicadas son también las que el asociado puede **simular y solicitar**.
4. **Habilitaciones** (`Channel.Enablements.*`): quién puede activar su cuenta. Una a una, por carga masiva (plantilla), o todos los
   asociados activos según la política. Inhabilitar pide motivo; el retiro del asociado inhabilita solo.

## Operación diaria

- **Pagos en línea**: cada pago aprobado entra como recibo «en línea» de Recaudo contra el convenio del destino, a nombre del asociado,
  y se aplica a sus productos. Lo que no se pudo aplicar queda a favor del asociado y la alerta **«Pago en línea en revisión»** llega a
  quien resuelve saldos a favor. Un pago aprobado nunca se rechaza.
- **Conciliación**: cargar el reporte diario de la pasarela en Recaudo (Convenios › archivos de pasarela). Ver
  `canal-digital-conciliacion.md`.
- **Reverso** (`Channel.Payments.Reverse`): desde **Atención**, «Reversar» con motivo. Antes del corte va por la pasarela; después, es
  una devolución por Tesorería. Ver `canal-digital-reverso.md`.
- **Atención** (`Canal digital › Atención`, `Channel.Support.*`): buscar por la referencia del pago o por el documento; ver la cuenta
  del canal (activada, último ingreso, bloqueo, dispositivos), cada intento con su estado en la pasarela y lo que hizo el ERP;
  **Desbloquear**, **Cerrar sesiones** o **Reversar**. Sólo se ve lo de esta cooperativa: nunca si la persona está en otra.
- **Indicadores** (`Channel.Metrics.View`): personas habilitadas y activas, ingresos, intentos por estado, valor recaudado, rechazos
  por motivo y lo que el ERP aplicó, dejó en revisión o reversó, en un rango de fechas.
- **Novedades de contacto**: cuando el asociado cambia su correo o celular en el canal, la cooperativa lo ve y decide si actualiza la
  persona en Personas (el canal no la cambia solo).
- **Solicitudes de crédito del canal**: llegan a Cartera como **borrador** con canal de origen «Canal digital»; la oficina las completa
  (codeudores, garantías, documentos) y las radica como cualquier otra.

## Lo que ve el asociado

Sus productos con lo exigible hoy, el pago en línea (con el retorno de la pasarela y el comprobante del ERP), el historial con los
reversados y devueltos, extractos y certificados generados en el momento, los avisos (cuota próxima, pago registrado, pago no
aprobado, pendiente resuelto y seguridad; los apaga en su perfil salvo los de seguridad) y el simulador y la solicitud de crédito.

## Recetas relacionadas

- `docs/manual/canal-digital-conciliacion.md` y `docs/manual/canal-digital-reverso.md`.
- `docs/operaciones/canal-digital-despliegue.md`, `canal-digital-llaves.md`, `canal-digital-app-tiendas.md` y
  `certificacion-pasarela-avalpay.md`.
