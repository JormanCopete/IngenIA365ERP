# Cartera — Documentos para firma (pagaré, carta de instrucciones y libranza)

Desde el 2026-11-18 el sistema genera, para cada solicitud de crédito **aprobada y vigente**, un PDF con los documentos que el deudor (y los
codeudores, cuando aplica) deben firmar: el **pagaré**, su **carta de instrucciones**, la **libranza** cuando el crédito se paga por nómina y
cualquier otro documento que la cooperativa configure. Todo sale de **plantillas que cada cooperativa escribe y ajusta**, con campos que el
sistema llena solo y campos propios de la cooperativa.

> Las plantillas base que trae el sistema siguen la norma colombiana citada abajo, pero **no están validadas por un abogado**. El abogado de la
> cooperativa debe revisarlas, ajustarlas y marcarlas como validadas antes de usarlas con asociados.

## 1. Configurar (Cartera › Configuración › Documentos para firma)

Permiso: `Lending.SignatureTemplates.Manage` (el administrador de la cooperativa lo tiene; se da a otros roles en Seguridad › Roles).

1. **Cargar plantillas base.** Agrega el pagaré con espacios en blanco, la carta de instrucciones y la libranza, y tres campos propios:
   `CIUDAD_PAGO`, `DIAS_MORA_LLENADO` y `REGISTRO_RUNEOL`. No pisa nada: sólo agrega lo que la cooperativa no tiene por código, y se puede
   volver a usar si se borra una.
2. **Completar los campos propios fijos.** «Ciudad donde se paga el pagaré», «Días de mora para llenar el pagaré» (lo fija la política de
   cartera) y, si la cooperativa está inscrita como operadora de libranza, su código en el RUNEOL. Un campo vacío se imprime como una raya
   para llenar a mano.
3. **Ajustar los textos** con el abogado y usar **Vista previa** (datos de ejemplo, marca «VISTA PREVIA»).
4. **Marcar validada** cada plantilla, con una nota (quién la revisó). Cambiar el texto después sube la versión y quita la validación.

Cada plantilla define:

| Opción | Para qué |
|---|---|
| Documento | Pagaré, carta de instrucciones, libranza u otro. Ordena la impresión (pagaré, carta, libranza, otros) y, con el pagaré, crea el pagaré físico de la solicitud. |
| Código y nombre | Código único de la cooperativa; el nombre va al pie de cada hoja. |
| Canales de pago | En qué créditos sale: caja, nómina de la cooperativa, libranza con pagaduría, débito automático. Ninguno = siempre. La libranza base sale con nómina propia o con pagaduría. |
| Líneas | Códigos de las líneas donde sale (vacío = todas). |
| Huella | Las firmas llevan el recuadro para la huella. |
| Firman los codeudores | El pagaré y la carta, sí; la libranza la firma sólo el deudor. |
| Activa | Una inactiva no sale. |

### Campos propios

Además de los del sistema, la cooperativa crea los suyos (`CODIGO` en mayúsculas, números y guion bajo):

- **Fijo de la cooperativa**: un valor que se escribe una vez (domicilio, representante legal, registro RUNEOL…).
- **Se digita al generar**: lo pide la solicitud al generar (por ejemplo la placa del vehículo); puede ser obligatorio y tener valor propuesto.

Tipos: texto, texto largo, número, **pesos** (además da `{{CODIGO_LETRAS}}` con el valor en letras), fecha y sí/no (sirve en `{{#SI}}`).
Un campo que usa una plantilla no se puede borrar; su código no cambia.

## 2. Cómo se escribe una plantilla

| Escritura | Resultado |
|---|---|
| `{{CAMPO}}` | El valor. Vacío: una raya para llenar a mano (y la generación lo avisa). |
| `{{CAMPO:MAYUSCULAS}}`, `{{CAMPO:MINUSCULAS}}` | Cambia las letras. |
| `{{BLANCO}}`, `{{BLANCO:40}}` | Un espacio en blanco del largo dicho (3 a 80): el pagaré en blanco. |
| `{{#SI CAMPO}} … {{#SINO}} … {{/SI}}` | Texto que sale si el campo tiene valor (y no es «NO», «0» ni «FALSE»); una lista, si tiene elementos. |
| `{{#NO CAMPO}} … {{/NO}}` | Texto que sale si el campo está vacío o es «NO». |
| `{{#CADA CODEUDORES}} … {{/CADA}}` | Repite el texto por cada codeudor, con `{{CODEUDOR_NOMBRE}}`, `{{CODEUDOR_DOCUMENTO}}`… y `{{ORDEN}}`. |
| `# Título`, `## Subtítulo` | Título centrado en negrita; subtítulo en negrita. |
| `- texto` | Viñeta. |
| `= texto` | Párrafo centrado. |
| `---` (solo en su renglón) | Lo que sigue va en otra hoja. |
| `{{FIRMAS}}` (solo en su renglón) | Las firmas: nombre, documento, calidad y huella si se pidió. |
| `**texto**` | Negrita. |

Cada renglón es un párrafo justificado; un renglón vacío separa. Una etiqueta de bloque (`{{#SI}}`, `{{/SI}}`, `{{#CADA}}`…) sola en su renglón
no deja renglón vacío. Al guardar, el sistema revisa la plantilla y dice el renglón de cada error (un campo que no existe, un bloque sin cerrar,
`{{FIRMAS}}` acompañado…).

### Campos del sistema

La lista completa, con ejemplos, está en la misma pantalla («Cómo se escribe una plantilla»). Grupos:

- **Cooperativa**: `COOPERATIVA_NOMBRE`, `COOPERATIVA_NIT`, `OFICINA`, `OFICINA_CIUDAD`.
- **Fecha** de generación: `FECHA`, `FECHA_LARGA`, `FECHA_DIA`, `FECHA_DIA_LETRAS`, `FECHA_MES`, `FECHA_ANIO`.
- **Crédito** (con lo **aprobado**): `SOLICITUD_NUMERO`, `LINEA_CODIGO`, `LINEA_NOMBRE`, `DESTINO`, `MONTO`, `MONTO_LETRAS`, `PLAZO_MESES`,
  `NUMERO_CUOTAS`, `PERIODICIDAD`, `SISTEMA_AMORTIZACION`, `VALOR_CUOTA`, `VALOR_CUOTA_LETRAS`, `TASA_EFECTIVA_ANUAL`, `TASA_MENSUAL`,
  `FECHA_PRIMERA_CUOTA`, `FECHA_ULTIMA_CUOTA`, `FECHA_DESEMBOLSO`, `CANAL_DE_PAGO`, `ES_LIBRANZA`, `PAGARE_NUMERO`. La cuota, las tasas y
  las fechas salen de la misma simulación que usa el desembolso.
- **Deudor**: `DEUDOR_NOMBRE`, `DEUDOR_TIPO_DOCUMENTO`, `DEUDOR_TIPO_DOCUMENTO_NOMBRE`, `DEUDOR_DOCUMENTO`, `DEUDOR_EXPEDICION`,
  `DEUDOR_DIRECCION`, `DEUDOR_CIUDAD`, `DEUDOR_TELEFONO`, `DEUDOR_CORREO`.
- **Codeudores**: `HAY_CODEUDORES`, `CODEUDORES_NOMBRES` y la lista `CODEUDORES`.
- **Pagaduría**: `PAGADURIA_NOMBRE`, `PAGADURIA_NIT` (la cooperativa si descuenta su propia nómina; si no, el pagador del convenio de
  libranza), `PAGADURIA_CODIGO_EMPLEADO`.
- **Firmantes**: la lista `FIRMANTES` (deudor y codeudores) para quien quiera armar sus propias firmas.

## 3. Generar en la solicitud

En el detalle de una solicitud **aprobada** aparece «Documentos para firma» (leerlo pide `Lending.Applications.Read`; generar,
`Lending.PromissoryNotes.Manage`):

1. Dice qué documentos aplican y el número del pagaré.
2. Pide los campos propios que se digitan (los obligatorios no dejan generar vacíos).
3. **Generar documentos para firma** arma el PDF y lo descarga. Si la solicitud no tenía pagaré, crea el **pagaré físico** con el número de la
   solicitud; si ya tenía uno físico, usa su número. Con un pagaré desmaterializado no imprime el pagaré ni la carta (se firman en el depósito).
4. Avisa lo que salió con raya (dato que falta en la persona o en la cooperativa) y las plantillas sin validar.
5. Se imprime, se firma (con huella si la plantilla la pide) y **la copia firmada se sube como siempre**: el pagaré y la carta en «Garantías y
   pagaré» (luego «Registrar custodia», que lo deja firmado y permite desembolsar) y la libranza como soporte de la solicitud.

Volver a generar deja el juego anterior como «Reemplazado» (se conserva y se puede bajar). Cada juego guarda su PDF (adjunto que no se borra),
su huella SHA-256, la versión de cada plantilla y los valores con que salió.

## 4. Normativa en que se apoyan las plantillas base

| Documento | Norma | Qué toma la plantilla |
|---|---|---|
| Pagaré | Código de Comercio, arts. 621 (requisitos comunes de todo título valor: el derecho que incorpora y la firma de quien lo crea; lugar de cumplimiento), 709 (promesa incondicional de pagar una suma determinada, a quién, a la orden o al portador y forma de vencimiento), 710 y 711 (se le aplican las normas de la letra de cambio) | Promesa incondicional, solidaria e indivisible a la orden de la cooperativa; lugar de pago; vencimiento; intereses remuneratorios y de mora sin exceder la usura; cláusula aceleratoria; gastos; renuncia a presentación, aviso de rechazo y protesto. |
| Pagaré con espacios en blanco y carta de instrucciones | Código de Comercio, art. 622: los espacios en blanco se llenan **conforme a las instrucciones** del suscriptor | El pagaré deja en blanco valor, vencimiento y tasa y remite a la carta; la carta identifica el pagaré, dice cómo se llena cada espacio (cuantía = todo lo exigible a la fecha; vencimiento = el día en que se llena; tasa pactada; lugar) y en qué eventos (mora de `DIAS_MORA_LLENADO` días, cláusula aceleratoria, retiro del asociado), y deja constancia de que el deudor recibe copia. La jurisprudencia pone en quien alega el llenado abusivo la carga de probarlo, por eso conviene que la carta sea específica y que el deudor reciba copia. |
| Libranza | Ley 1527 de 2012 (modificada por la Ley 1902 de 2018), art. 3: autorización **expresa e irrevocable** del beneficiario a la entidad pagadora; el descuento no puede dejar al trabajador o pensionado con menos del **50 %** de su salario o pensión neto | Autorización expresa e irrevocable, identificación de la obligación, valor y número de cuotas, tasa; continuidad ante cambio de empleador y descuento del saldo en la liquidación final dentro de los límites legales; declaración del 50 %; constancia de recibo de la pagaduría. |
| Libranza a favor de una cooperativa | Ley 79 de 1988, art. 142 (y 143 y 144): los empleadores deben deducir de lo que paguen a sus trabajadores lo que estos adeuden a la cooperativa cuando la obligación consta en libranza u otro documento firmado por el deudor | Se cita en el encabezado de la libranza. |
| Operador de libranza | Registro Único Nacional de Entidades Operadoras de Libranza (RUNEOL) | Campo propio `REGISTRO_RUNEOL`: si la cooperativa lo diligencia, la libranza lo menciona. |

Puntos para que el abogado decida (no hay respuesta única en la norma): el número de días de mora que habilita llenar el pagaré, si se
mencionan cesantías o prestaciones en la autorización de descuento al retiro (tienen límites legales propios), la tasa de mora (máxima legal o
una pactada menor), si el pagaré se firma ante notario o con huella (el sistema deja el recuadro opcional) y el texto de las cláusulas sobre
pérdida de la calidad de asociado.

Fuentes consultadas: el texto de la Ley 1527 de 2012 en el gestor normativo de la Función Pública
(<https://www.funcionpublica.gov.co/eva/gestornormativo/norma_pdf.php?i=56592>) y su compilación en Colpensiones
(<https://normativa.colpensiones.gov.co/colpens/compilacion/docs/ley_1527_2012.htm>); conceptos y decisiones judiciales sobre pagaré en
blanco y carta de instrucciones publicados por la Rama Judicial y el Tribunal Superior de Medellín.

## 5. Para soporte

- Tablas: `LND_SignatureDocumentTemplates`, `LND_SignatureDocumentFields`, `LND_SignatureDocumentSets` (migración `DocumentosParaFirma`,
  aditiva). El PDF es un adjunto `LendingSignatureDocumentSet` que no se borra.
- Rutas: `GET/POST/PUT/DELETE /api/lending/signature-templates`, `POST …/load-base`, `POST …/{id}/verify`, `POST …/preview` (PDF);
  `POST/PUT/DELETE /api/lending/signature-fields`; `GET/POST /api/lending/applications/{id}/signature-documents`.
- El motor de plantillas es puro (`Domain/Lending/SignatureDocuments/PlantillaDeFirma`); el PDF lo pinta `DocumentosParaFirmaReport`.
