# Feature Specification: Cargue de archivos planos para la implantación

**Feature Branch**: `016-cargue-archivos-implantacion`

**Created**: 2026-10-24

**Status**: Aprobada por el dueño el 2026-10-28 con las siete historias; enmendada el 2026-10-30 (D10, utilidad aparte). 015 y 017 ya están en `develop`

**Input**: Pedido del dueño del 2026-10-24. COOFLOPAL sale en vivo el 01/12/2026. La cooperativa ya existe en producción y
ya inició la contabilidad (2.110 cuentas del CUIF), pero faltan sus datos: personas, asociados, auxiliares, saldos de apertura,
créditos con su plan y su mora, y aportes. La feature es el proceso para llevarlos al ERP **desde los archivos planos tal como
salen del sistema anterior**. Informe previo con el inventario, las brechas y las decisiones del dueño:
[informe-previo.md](informe-previo.md).

## Enmienda del 2026-10-30 (D10): utilidad aparte

El dueño aclaró que la 016 «va a ser una utilidad aparte del desarrollo»: **una sesión de Claude** que adapta los archivos del
sistema anterior y los deja listos para subir. Eligió (research D10):

- **En el ERP sólo lo que falta**: las plantillas de personas y terceros (US1), de asociados (US3) y las invitaciones masivas (US7).
- **Los perfiles de lectura, las equivalencias, los totales de control y la traducción de errores** viven en el kit
  `tools/implantacion/`, que corre la sesión de implantación. US2, US4, US5 y US6 se cumplen ahí, en las pantallas de cuadre que ya
  existen y en el runbook, **no** como pantallas, tablas ni tablero del ERP.
- **El equipo sube los libros en las pantallas de siempre** (Revisar → Aplicar); Claude no inicia sesión ni maneja tokens del ERP.
- La plantilla de personas no lleva cuentas bancarias: van por la plantilla de la 015.

Donde las historias de abajo dicen «perfil», «equivalencia», «tablero» o «cargue en segundo plano» dentro del ERP, se leen como
pasos del kit y del runbook. Las reglas de negocio (alta unitaria, todo o nada, errores por fila y columna, nunca borra) no cambian.

## Contexto: lo que ya existe y lo que falta

**Ya existe una mecánica común de plantillas**, construida en la feature 012 y usada por la 009, la 013 y la 014. Funciona así:

- el archivo es un `.xlsx` o un `.csv` con los encabezados exactos de la plantilla en la fila 1;
- hay dos modos: **revisar** (no guarda nada) y **aplicar** (todo o nada);
- los errores se informan por hoja, fila y columna;
- la llave identifica la fila: si existe se actualiza, y la plantilla nunca borra;
- todo queda en la auditoría.

Ya se cargan con esa mecánica:

- los auxiliares del plan de cuentas;
- la apertura contable;
- las 16 plantillas de inventario;
- las plantillas `lending.*` de Cartera, entre ellas la carga de migración en 17 partes;
- la carga de migración de aportes y captaciones.

**Faltan cinco cosas**:

1. **Leer el archivo tal como sale.** Ninguna ruta acepta un TXT de ancho fijo, ni un CSV con otros encabezados, otro orden de columnas u otro separador.
2. **Traducir los códigos.** Los archivos traen los códigos del sistema anterior (cuentas, oficinas, líneas, ciudades, tipos de documento) y ningún cargue los traduce.
3. **Cargar personas y terceros.** No hay plantilla, y ninguna de las demás crea personas: un documento que no existe es un error.
4. **Cargar asociados.** No hay plantilla, y la cuenta de aportes exige un asociado vigente.
5. **Ver el proceso completo y cuadrarlo.** Cada módulo tiene su pantalla; ningún sitio muestra el orden de los cargues, cuál falta, ni cómo se comparan los totales con las cifras de referencia y con el balance de apertura.

## Dependencias

El dueño decidió el 2026-10-28 que **el código de esta feature empieza sólo cuando las features 015 y 017 estén mezcladas en
`develop`**. Hasta entonces se escriben spec, plan y tareas, y nada más.

- **Feature 017 (ingreso por documento e invitaciones ancladas al documento)**, porque cambia la persona sobre la que trabaja
  esta feature:
  - la persona de cada cooperativa gana un campo **usuario** derivado: con cédula de ciudadanía es el número solo; con
    cualquier otro tipo, el prefijo del tipo más el número;
  - el catálogo de tipos de documento de la persona gana el tipo **PPT** (código de persona `PT`);
  - la unicidad de la persona pasa del número solo a ese **usuario**, incluso frente a eliminadas;
  - la invitación con documento, el catálogo de cargos de la cooperativa y las reglas de activación (mismo mensaje en todos los
    casos, tres intentos) son los de la 017. La User Story 7 los usa tal cual; no los reimplementa.
- **Feature 015 (Recaudo y Tesorería)**, PR JormanCopete/IngenIA365ERP#18, rama `015-recaudo-tesoreria`. No es requisito para
  **cargar**, pero lo es para operar después de activar, y su mezcla cambia zonas (personas, Cartera, aportes) que tocan los
  cargues de esta feature.

## Clarifications

### Session 2026-10-28 (respuestas del dueño en chat)

- Q: ¿Se aprueba la spec? → A: **Sí, con las seis historias**, y se agrega una séptima (abajo).
- Q: ¿Hay muestras de los archivos del sistema anterior? → A: **Todavía no.** Se construye con **muestras sintéticas**. Los
  perfiles reales de COOFLOPAL se cargan después **como dato**, sin cambiar código. (Actualiza la última respuesta de la
  sesión del 2026-10-24.)
- Q: ¿Cuándo empieza el código? → A: **Cuando la 017 y la 015 estén mezcladas en `develop`**, porque la 017 cambia la persona:
  usuario derivado (CC el número solo; otro tipo, prefijo + número), tipo nuevo PPT (`PT`) y unicidad por ese usuario. Ver
  «Dependencias».
- Q: ¿Qué más entra? → A: **Una séptima historia: invitaciones masivas de usuarios desde una hoja de cálculo**, con el formato
  `Formato-usuarios-ERP-COOFLOPAL-v2.xlsx` (hojas «Usuarios nuevos», «Cargos» y «Listas»). La revisión valida tipo y número
  de documento, el número digitado dos veces, nombres, correo personal único, celular, un rol entre los cuatro de siempre
  (Administrador de la cooperativa, Operador, Auditor, Solo lectura) y un código de cargo del catálogo de cargos de la
  cooperativa; la hoja «Cargos» puede crear o actualizar ese catálogo. Aplicar envía las invitaciones con **exactamente las
  reglas de la 017**: identidad por documento, el mismo mensaje en todos los casos y tres intentos al activar. Todo o nada,
  errores por hoja, fila y columna, auditado y con permiso propio.
- Q: ¿Qué se decide en las nueve preguntas que dejó el plan? → A: **La opción recomendada en todas** (research D1–D9):
  - una cuenta bancaria por fila en la plantilla de personas;
  - «Oficina / agencia» se muestra y no se guarda;
  - el archivo de un cargue por perfil se guarda hasta que el dueño ordene borrarlo;
  - quien ya es miembro activo queda «sin cambio» con el aviso «ya es miembro», sin invitación;
  - la equivalencia «no se migra» es error de la fila;
  - el paso «usuarios» va en el tablero, informativo;
  - los totales de control son la lista cerrada del data-model;
  - los asociados retirados se crean como estado histórico y un vigente no se retira por archivo;
  - las plantillas de inventario quedan fuera de la lectura por perfil.

### Session 2026-10-24 (formulario al dueño, antes de la spec)

- Q: ¿En qué forma salen los datos de COOFLOPAL? → A: **Archivos planos tal como salen** del sistema anterior (CSV o TXT); se leen con perfiles configurables, sin pasar por Excel.
- Q: ¿Quién ejecuta los cargues? → A: **Ambos, por la misma API**: el equipo de COOFLOPAL desde la pantalla, y soporte de IngenIA por las mismas rutas para los archivos grandes.
- Q: ¿Cómo se ensaya? → A: **En QA, con una copia de la base de producción de COOFLOPAL**, tal cual, con acceso restringido al equipo de implantación, y se borra al terminar dejando constancia.
- Q: ¿Contra qué se valida? → A: **Totales de control contra las cifras de referencia, más el cuadre contra la contabilidad.** Las diferencias se informan y se aceptan con motivo; nunca bloquean.
- Q: ¿Qué alcance? → A: **Personas y terceros, asociados, y archivos planos para lo que ya existe** (apertura, partes de la carga de cartera, carga de aportes). Los empleados ya están creados. Los saldos de prestaciones y los acumulados de nómina quedan fuera.
- Q: ¿Los archivos traen códigos del sistema anterior? → A: **Sí, se traducen al cargar** con tablas de equivalencias por cooperativa.
- Q: ¿Cómo se ve el proceso? → A: **Un tablero de implantación** con el orden, el estado y los totales de cada cargue.
- Q: ¿De dónde salen los perfiles concretos? → A: El dueño deja una **muestra de cada archivo en una carpeta fuera del repositorio**; los perfiles de COOFLOPAL se cargan como dato. *(Actualizada el 2026-10-28: todavía no hay muestras; se construye con muestras sintéticas.)*

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cargar personas y terceros desde el archivo del sistema anterior (Priority: P1)

El equipo de implantación tiene el archivo de terceros exportado del sistema anterior: un TXT de ancho fijo con unos miles de
filas de personas naturales y jurídicas. Hace lo siguiente:

1. Elige el perfil de lectura «Terceros» de su cooperativa.
2. Sube el archivo y lo revisa: el sistema lo lee con el perfil, traduce la ciudad y el tipo de documento con las equivalencias, y aplica a cada fila las mismas reglas del alta unitaria de Maestros › Personas.
3. Recibe los errores por fila y columna: documento repetido, dígito de verificación inválido, ciudad sin equivalencia.
4. Corrige y vuelve a revisar.
5. Aplica: se crean todas las personas o ninguna.

**Why this priority**: todo lo demás depende de las personas. La apertura nombra terceros, Cartera nombra deudores y codeudores,
y aportes nombra asociados, y ninguna de esas cargas crea personas. Sin esta historia no entra nada.

**Independent Test**: en una cooperativa vacía, crear un perfil sobre un TXT de muestra y una tabla de equivalencias de ciudades,
revisar un archivo con 3 filas buenas y 2 malas (errores con fila y columna, nada guardado), corregirlo y aplicar. Las 5 personas
quedan en Maestros › Personas, idénticas a como las dejaría el alta unitaria, con su evento de auditoría.

**Acceptance Scenarios**:

1. **Given** un perfil de ancho fijo y un archivo válido, **When** se revisa, **Then** responde válido con los conteos de lo que crearía y actualizaría, y no guarda nada.
2. **Given** un archivo con un documento que ya existe vivo y con datos distintos, **When** se aplica, **Then** la persona se **actualiza** con los campos que trae el archivo, y el cambio campo a campo aparece en el resultado.
3. **Given** un documento que existe como persona eliminada, **When** se revisa, **Then** la fila da el mismo error del alta unitaria (`Person.TaxIdDeleted`) y señala la columna del documento.
4. **Given** una ciudad sin equivalencia, **When** se revisa, **Then** la fila da un error de equivalencia faltante que nombra la tabla y el código anterior.
5. **Given** un archivo con un error en la fila 4.381, **When** se aplica, **Then** no se guarda ninguna persona y responde con el error y su fila.
6. **Given** filas con la marca de proveedor o de cliente, **When** se aplica, **Then** la persona queda con esa marca; las marcas de asociado, empleado y vendedor no se escriben desde este archivo.

---

### User Story 2 - Perfiles de lectura y tablas de equivalencias (Priority: P1)

Soporte de IngenIA, o el equipo con permiso, configura dos cosas para su cooperativa:

- **Un perfil de lectura por cada archivo que entrega el sistema anterior.** El perfil dice qué plantilla destino alimenta; qué tipo de archivo es (delimitado con su separador, comillas y encabezado, o de ancho fijo con sus posiciones); la codificación de caracteres; qué filas saltar (títulos, totales, pies); y cómo cada columna o posición llena una columna de la plantilla destino. Esto último incluye el formato de fechas y números, el recorte de espacios, los valores constantes y la equivalencia que se le aplica.
- **Las tablas de equivalencias** (código anterior → código nuevo). Se cargan desde el formato que ya diligencia la contadora, o se editan en pantalla.

Antes de usar un perfil se prueba con un archivo real: la vista previa muestra las primeras filas ya convertidas a las columnas de
la plantilla destino.

**Why this priority**: el dueño eligió cargar los archivos tal como salen. Sin perfiles no hay forma de leerlos, y sin
equivalencias cada archivo tendría que traducirse a mano.

**Independent Test**: definir un perfil CSV con separador «;» y fechas `dd/MM/yyyy`, y otro de ancho fijo. Hacer la vista previa
con una muestra y comprobar que las columnas destino salen bien. Cargar una tabla de equivalencias de cuentas desde el formato de
la contadora y ver que un código sin equivalencia se informa.

**Acceptance Scenarios**:

1. **Given** un perfil de ancho fijo con un campo que se sale del largo de la línea, **When** se prueba, **Then** el error dice la línea y la posición, no un fallo genérico.
2. **Given** un perfil que no llena una columna obligatoria de la plantilla destino (ni con el archivo ni con una constante), **When** se guarda, **Then** se rechaza nombrando la columna faltante.
3. **Given** un archivo en una codificación distinta de la del perfil (tildes y eñes dañadas), **When** se hace la vista previa, **Then** se ve el texto tal como quedaría y aparece un aviso de caracteres no reconocidos.
4. **Given** una tabla de equivalencias con el mismo código anterior dos veces y destinos distintos, **When** se carga, **Then** se rechaza con la fila de cada uno.
5. **Given** una equivalencia que apunta a un código nuevo que no existe (una cuenta que no está en el plan), **When** se carga la tabla, **Then** queda como aviso, y la fila del cargue que la use dará el error del destino inexistente.
6. **Given** un perfil que ya se usó en un cargue aplicado, **When** se modifica, **Then** se guarda como una versión nueva y el cargue anterior sigue diciendo con qué versión se leyó.

---

### User Story 3 - Cargar asociados sobre personas existentes (Priority: P2)

Con las personas ya cargadas, el equipo sube el archivo de asociados del sistema anterior: documento, fecha de afiliación,
oficina (traducida con su equivalencia), grupo, estado y los datos de afiliación del alta de asociado. Revisa y aplica.

**Why this priority**: la carga de aportes exige un asociado vigente, y Cartera usa la condición de asociado. Va después de
personas y antes de aportes.

**Independent Test**: con 10 personas creadas, aplicar un archivo de 10 asociados (uno con un documento inexistente debe impedir
la aplicación). Corregirlo, aplicar, y comprobar que las 10 personas quedan con la marca de asociado y su fila de asociado, como
las deja el alta unitaria.

**Acceptance Scenarios**:

1. **Given** un documento que no existe vivo en Personas, **When** se revisa, **Then** la fila da un error («esta plantilla no crea personas»), igual que en Cartera.
2. **Given** una persona que ya es asociada, **When** se aplica la misma fila con otro grupo, **Then** se actualiza el asociado y no se duplica.
3. **Given** un asociado retirado en el sistema anterior, **When** se aplica, **Then** queda retirado con su fecha y su motivo traducidos, y no cuenta como vigente para aportes.
4. **Given** una fila con una fecha de afiliación futura, **When** se revisa, **Then** la fila da error con la misma regla del alta unitaria.

---

### User Story 4 - Archivos planos para las cargas que ya existen (Priority: P2)

Las cargas que ya existen se alimentan con el archivo tal como sale, por perfil de lectura, en lugar de rehacer cada archivo en
Excel. Son tres:

- la apertura contable: los saldos por cuenta, tercero y documento cruce, con cuentas traducidas por la tabla de la contadora;
- las partes de la carga de migración de Cartera: créditos, cuotas, saldos, mora y las demás;
- la carga de migración de aportes.

Cada una conserva sus propias reglas, su cuadre y su activación. Lo único nuevo es que el archivo de entrada pasa antes por un
perfil y por las equivalencias.

**Why this priority**: es donde están los volúmenes grandes (cuotas, saldos, aportes por asociado) y donde rehacer en Excel es
más caro y más propenso a errores.

**Independent Test**: con un perfil de la parte «créditos» y otro de la apertura, subir los archivos de muestra y comprobar que
el resultado (errores, conteos, cuadre) es idéntico al que se obtiene subiendo el mismo contenido ya convertido en la plantilla
Excel de siempre.

**Acceptance Scenarios**:

1. **Given** un perfil de la parte «cuotas», **When** se aplica un archivo en varias tandas, **Then** cada tanda se comporta exactamente como hoy (un archivo repetido queda «sin cambio», y reemplazar deja las tandas anteriores reemplazadas).
2. **Given** un error en una fila del archivo plano, **When** se informa, **Then** el error dice la línea del archivo original y el campo de la plantilla destino, no una fila de un Excel intermedio que la persona nunca vio.
3. **Given** una cuenta del sistema anterior que la contadora repartió entre varias cuentas nuevas («una en varias»), **When** se carga la apertura, **Then** la fila da un error que pide decidir la cuenta nueva: la equivalencia de uno a muchos no se resuelve sola.
4. **Given** la revisión de una carga por perfil, **When** se pide el libro de revisión, **Then** el libro trae las columnas de la plantilla destino ya convertidas, con el resultado y los errores, para ver qué entendió el perfil.

---

### User Story 5 - Tablero de implantación y cuadre global (Priority: P2)

El gestor de implantación y la contadora abren el tablero de implantación de la cooperativa. Ven los cargues en orden de
dependencia:

1. equivalencias y perfiles;
2. personas;
3. asociados;
4. auxiliares;
5. apertura contable;
6. vinculados y saldos de aportes para el cupo;
7. carga de cartera;
8. carga de aportes;
9. inventario.

Cada cargue muestra su estado (pendiente, revisado, aplicado, cuadrado o con diferencias aceptadas), quién lo hizo y cuándo, y
sus **totales de control**: conteos y sumas, comparados con las **cifras de referencia** que digitó la cooperativa (número de
personas, de asociados vigentes, de créditos, saldo de capital, total de aportes) y con el saldo de las cuentas del balance de
apertura. Si un cargue empieza sin su paso previo, el tablero lo **informa**: no lo impide, porque las cargas propias de cada
módulo tienen sus propias reglas. Una diferencia se acepta con motivo.

**Why this priority**: el dueño eligió validar con totales de control más el cuadre contable, y verlo en un solo sitio. Así se
decide el «sí» o «no» del 28/11.

**Independent Test**: con personas y asociados cargados y cifras de referencia digitadas (una distinta a propósito), el tablero
muestra los dos pasos aplicados, la diferencia en rojo con su valor, y permite aceptarla con motivo. Al aceptarla, el paso queda
«con diferencias aceptadas» y el evento queda en la auditoría.

**Acceptance Scenarios**:

1. **Given** cifras de referencia digitadas para el corte, **When** se aplica un cargue, **Then** sus totales se recalculan con lo que hay en la base y se comparan con la cifra.
2. **Given** una diferencia en el saldo de capital entre la carga de cartera y la cuenta del balance de apertura, **When** se abre el tablero, **Then** muestra las dos cifras, su diferencia y el enlace al cuadre de la carga, que ya existe en Cartera.
3. **Given** una cifra de referencia que no se digitó, **When** se mira el paso, **Then** el total aparece como «sin cifra de referencia» y no como cuadrado.
4. **Given** un cargue de asociados aplicado antes que el de personas, **When** se mira el tablero, **Then** el paso aparece con el aviso de orden.

---

### User Story 6 - Ensayo en QA con copia de producción (Priority: P3)

Antes del corte real, soporte restaura en QA una copia de la base de COOFLOPAL de producción. Le da acceso sólo al equipo de
implantación y corre todos los cargues con los archivos reales, perfiles, equivalencias y cifras de referencia de un corte
anterior. Así mide tiempos, ajusta perfiles y equivalencias, y cuando el tablero cuadra **exporta la configuración** (perfiles,
equivalencias y cifras) para importarla igual en producción. Al terminar, la copia se borra y queda la constancia.

**Why this priority**: es el ensayo que pidió el dueño. Lo que la feature tiene que aportar es la exportación y la importación
de la configuración y el procedimiento; la restauración en sí es operación.

**Independent Test**: exportar la configuración de una cooperativa e importarla en otra. Los perfiles, las tablas y las cifras
deben quedar idénticos, y un mismo archivo debe dar el mismo resultado de revisión en las dos.

**Acceptance Scenarios**:

1. **Given** la configuración exportada de QA, **When** se importa en producción, **Then** los perfiles y equivalencias quedan con sus versiones y la importación queda auditada.
2. **Given** la copia de QA terminada, **When** soporte sigue el procedimiento de cierre del ensayo, **Then** la base se elimina y queda una constancia con la fecha, quién la borró y el respaldo de origen.

---

### User Story 7 - Invitar a los usuarios de la cooperativa desde una hoja de cálculo (Priority: P2)

El administrador de COOFLOPAL recibe de vuelta el formato de usuarios que diligenció la cooperativa. Trae una hoja «Usuarios
nuevos» con una fila por persona: tipo y número de documento, el número otra vez, nombres y apellidos, correo personal, celular,
rol, código de cargo, oficina y observaciones. Trae también una hoja «Cargos» con el catálogo de cargos (código y nombre). Hace lo
siguiente:

1. Sube el libro en Seguridad › Usuarios › «Invitar desde archivo» y lo revisa. El sistema valida cada fila con las mismas reglas
   del formulario de invitación de la 017, y cada cargo con la regla de los códigos de catálogo.
2. Recibe los errores por hoja, fila y columna: números que no coinciden, tipo no aceptado por la cooperativa, correo repetido en
   el archivo, celular inválido, rol desconocido, código de cargo que no está ni en el catálogo ni en la hoja «Cargos».
3. Corrige y vuelve a revisar.
4. Aplica: el catálogo de cargos queda creado o actualizado y salen todas las invitaciones, o ninguna.

Cada persona recibe la invitación que habría recibido si el administrador la hubiera invitado una a una: anclada a su documento,
al correo que dicen las reglas de la 017, y con tres intentos para confirmar el documento al activar.

**Why this priority**: COOFLOPAL tiene que dar de alta a todo su personal antes del 01/12, y el formato ya está en manos de la
cooperativa. Invitar una a una es lento y propenso a errores de digitación. No es P1 porque la invitación unitaria de la 017 ya
resuelve el caso, sólo que más despacio.

**Independent Test**: en una cooperativa con la 017, revisar un libro con 5 filas (una con los dos números distintos, otra con un
cargo que no existe). Se ven los errores con su hoja, fila y columna, y no sale ninguna invitación. Corregirlo (agregando el cargo
en la hoja «Cargos») y aplicar. Quedan 5 invitaciones pendientes, el cargo nuevo en el catálogo, el mismo mensaje en las 5 filas del
resultado y un evento de auditoría de la importación.

**Acceptance Scenarios**:

1. **Given** un libro válido, **When** se revisa, **Then** responde válido con los conteos de invitaciones que saldrían y de cargos que se crearían o actualizarían, y no guarda ni envía nada.
2. **Given** una fila cuyo «Confirmar número de documento» no coincide con el número, **When** se revisa, **Then** la fila da el error de la 017 (`Identity.Document.Mismatch`) en la columna de confirmación.
3. **Given** dos filas con el mismo correo personal, o con el mismo tipo y número, **When** se revisa, **Then** las dos filas dan error de llave repetida dentro del archivo, cada una con su fila.
4. **Given** un correo que ya pertenece a otra identidad de la plataforma, **When** se revisa, **Then** la fila da «Correo ya registrado» sin decir de quién, igual que la invitación unitaria.
5. **Given** un documento que ya tiene identidad en la plataforma (por ejemplo, de otra cooperativa), **When** se aplica, **Then** la invitación sale al correo de esa identidad y el resultado de la fila dice el **mismo mensaje** que una persona nueva. Ni la revisión ni el resultado permiten distinguir los dos casos.
6. **Given** un código de cargo que no está en el catálogo pero sí en la hoja «Cargos» del mismo libro, **When** se aplica, **Then** el cargo se crea y la invitación lo lleva.
7. **Given** un rol escrito como en la hoja «Listas» («Solo lectura») o por su nombre del sistema («ReadOnly»), **When** se revisa, **Then** se acepta; cualquier otro texto es error de la columna «Rol».
8. **Given** una fila con error, **When** se aplica, **Then** no sale ninguna invitación ni se toca el catálogo de cargos, y responde con los errores.
9. **Given** una persona que ya es miembro activo de la cooperativa, **When** se revisa, **Then** la fila queda «sin cambio» con el aviso «ya es miembro» y no impide aplicar las demás.
10. **Given** una invitación enviada por archivo, **When** la persona digita mal el documento tres veces al activar, **Then** la membresía queda «en revisión» exactamente como en la invitación unitaria.

---

### Edge Cases

- **Archivo enorme** (cuotas: cientos de miles de líneas). Se admite partirlo en tandas, como hoy en Cartera. El límite de tamaño y de filas por archivo es configurable y se informa antes de subir. Una revisión larga muestra su avance y no bloquea la pantalla.
- **Línea mal formada en un archivo de ancho fijo** (más corta, más larga, o con un salto de línea dentro de un campo). Error de esa línea con su longitud real y la esperada; las demás se siguen leyendo.
- **Filas de totales o de pie** que el sistema anterior intercala. Se saltan por la regla del perfil (patrón o posición); una fila que no encaja da error, no se ignora en silencio.
- **Números con separadores locales** («1.234.567,89») o con el signo al final. El perfil lo declara. Un número que no encaja es error de celda, nunca un cero.
- **Fechas imposibles o vacías** («00/00/0000», «31/02/2020»). Error de celda; si el perfil declara que el vacío significa «sin fecha», se toma como sin fecha.
- **Equivalencia «varias en una»** (varias cuentas anteriores a una nueva). Se admite: las filas se suman en el destino cuando la plantilla lo permite (la apertura por cuenta y tercero), y si no, cada fila entra aparte.
- **El mismo número con dos tipos de identificación distintos** en el archivo de personas. Con la 017 son **dos personas**
  distintas (CC 123456 es `123456` y CE 123456 es `CE123456`), así que entran las dos. El mismo tipo y número dos veces es error
  de llave repetida dentro del archivo.
- **Tipo PPT en el archivo de personas.** Se traduce con la equivalencia de tipos de documento al código `PT` de la persona (el
  que agrega la 017). Sin equivalencia, es error de la fila como cualquier otro código.
- **Las filas de ejemplo del formato de usuarios** (amarillas, con datos ficticios) que la cooperativa no borró. Se leen como
  cualquier otra fila: la revisión las muestra y el administrador las quita. El sistema no adivina cuál es un ejemplo.
- **Libro de usuarios con más filas que el límite de invitaciones por archivo.** Se rechaza antes de procesarlo, con el límite en el
  mensaje. El límite es de cada cooperativa.
- **Una invitación cuyo correo no sale** después de aplicar (el servidor de correo rechaza un buzón). La invitación ya existe; el
  resultado lo informa en esa fila y se reenvía desde la lista de invitaciones de la 017. No se deshace el resto.
- **La cooperativa ya tiene datos digitados a mano** (por ejemplo, personas creadas para la nómina en paralelo). El archivo actualiza por documento y no duplica. El resultado de la revisión distingue «crear», «actualizar» y «sin cambio».
- **Personas que el sistema anterior marca como empleados.** El archivo no enciende `IsEmployee`. Los empleados ya existen y sus marcas las escribe su módulo.
- **El perfil cambia a mitad de la implantación.** Los cargues ya aplicados conservan la versión con la que se leyeron.
- **Datos personales en el ensayo de QA.** El acceso es sólo para el equipo de implantación, cada exportación con datos queda auditada, y la copia se borra con constancia.

## Requirements *(mandatory)*

### Functional Requirements

**Perfiles de lectura**

- **FR-001**: El sistema MUST permitir a cada cooperativa definir **perfiles de lectura**. Cada perfil dice:
  - la plantilla destino (o la parte de una carga) que alimenta;
  - el tipo de archivo: delimitado (separador, calificador de texto, si trae encabezado) o de ancho fijo;
  - la codificación de caracteres;
  - las reglas para saltar filas: las N primeras, patrón de fila de total o pie, filas vacías;
  - un mapeo por cada columna de la plantilla destino.
- **FR-002**: Cada mapeo MUST indicar de dónde sale el valor y cómo se interpreta:
  - el origen: columna por nombre o número, posición inicial y largo, o constante;
  - el formato de fecha y el de número (separador de miles y decimal, signo, decimales implícitos);
  - el recorte de espacios y el relleno;
  - la tabla de equivalencias que se le aplica, si alguna;
  - el valor cuando la celda viene vacía.
- **FR-003**: Al guardar un perfil, el sistema MUST rechazarlo si deja sin llenar una columna obligatoria de la plantilla destino, o si dos mapeos llenan la misma columna.
- **FR-004**: Los perfiles MUST ser **versionados**. Modificar un perfil ya usado en un cargue aplicado crea una versión nueva, y cada cargue guarda con qué versión se leyó.
- **FR-005**: El sistema MUST ofrecer una **vista previa** de un perfil con un archivo real: las primeras filas convertidas a las columnas de la plantilla destino, los errores de lectura por línea y posición, y los avisos de codificación. La vista previa no guarda nada.

**Equivalencias**

- **FR-006**: El sistema MUST mantener **tablas de equivalencias por cooperativa**, cada una de un tipo de código (cuenta, oficina, sucursal, línea de crédito, ciudad, tipo de documento de identidad, motivo de retiro, grupo de asociados u otros que agregue un perfil). Cada fila es un código anterior, su código nuevo y una nota. La equivalencia «una en varias» se marca como tal y no se resuelve sola.
- **FR-007**: Las tablas de equivalencias MUST poder cargarse con la mecánica común de plantillas (revisar, aplicar, todo o nada, errores por fila y columna). Eso incluye una lectura del formato de equivalencias de Contabilidad que ya diligencia la contadora. También MUST poder editarse fila a fila en pantalla.
- **FR-008**: Al cargar un archivo, un código sin equivalencia en la tabla que pide el mapeo MUST ser error de esa fila y esa columna, y nombrar la tabla y el código anterior. No se toma el código tal cual en silencio. Si el perfil declara la columna como «de paso cuando no hay equivalencia», el código se toma tal cual.

**Cargues por perfil**

- **FR-009**: Todo cargue por perfil MUST pasar por la **mecánica común de plantillas** existente:
  - revisar sin guardar y aplicar todo o nada;
  - errores por hoja, fila y columna;
  - llave que crea, actualiza o deja sin cambio, y nunca borra;
  - idempotencia;
  - auditoría con el SHA-256 del archivo y la versión del perfil;
  - libro de revisión con las columnas destino ya convertidas.
  La regla de negocio que se aplica MUST ser la misma del cargue con la plantilla Excel. El archivo plano sólo cambia cómo se lee.
- **FR-010**: Los errores de un cargue por perfil MUST citar la **línea del archivo original** y el nombre de la columna de la plantilla destino. Si el error es de lectura o de equivalencia, MUST citar también la columna o posición de origen.
- **FR-011**: La apertura contable, cada una de las partes de la carga de migración de Cartera y las partes de la carga de migración de aportes MUST aceptar un archivo por perfil, además de su plantilla de siempre, conservando sus tandas, su reemplazo, su cuadre, la aceptación de diferencias y la activación tal como existen.
- **FR-012**: El tamaño máximo de archivo y de filas por cargue MUST ser configurable. Un archivo más grande se rechaza antes de leerlo, con el límite en el mensaje.

**Personas y terceros**

- **FR-013**: El sistema MUST ofrecer una **plantilla de personas y terceros**, naturales y jurídicas. Lleva:
  - documento, tipo y dígito de verificación;
  - nombres y apellidos completos (los dos apellidos y los otros nombres) o razón social;
  - contacto, dirección y ciudad;
  - datos tributarios (responsabilidades, régimen);
  - datos demográficos que admite el alta;
  - cuenta bancaria (banco, tipo y número);
  - las marcas de **proveedor** y **cliente**.
  La llave es el **usuario derivado** de tipo y número que introduce la feature 017 (cédula: el número solo; otro tipo, prefijo
  más número), así que CC y CE con el mismo número son personas distintas. La plantilla aplica las **mismas reglas del alta
  unitaria** de Maestros › Personas, con los mismos códigos de error. El usuario lo calcula el punto único de la 017, nunca la
  plantilla.
- **FR-014**: La plantilla de personas MUST NOT escribir las marcas de asociado, empleado ni vendedor: las escribe sólo el módulo que crea la fila hija. Un documento que existe como persona eliminada MUST responder el error del alta unitaria. No se restaura desde el archivo.
- **FR-015**: Una fila de una persona que ya existe viva MUST actualizar sólo las columnas que el archivo trae con valor. El cambio campo a campo, con su antes y su después, va en el resultado. Una columna vacía no borra un dato existente, salvo que el perfil la declare «vaciar si viene vacía».

**Asociados**

- **FR-016**: El sistema MUST ofrecer una **plantilla de asociados** sobre personas vivas. Lleva documento, fecha de afiliación, oficina, grupo, estado (vigente o retirado, con su fecha y motivo) y los datos de afiliación que admite el alta de asociado. La llave es el usuario derivado de tipo y número (feature 017). Aplica las mismas reglas del alta y del retiro unitarios, y la marca de asociado la escribe el mismo camino que la escribe hoy. Un documento que no existe vivo es error de fila: **esta plantilla no crea personas**.

**Invitaciones masivas de usuarios**

- **FR-027**: El sistema MUST ofrecer una **plantilla de invitaciones de usuarios** con la mecánica común: revisar, aplicar todo o
  nada, errores por hoja, fila y columna, libro de revisión, idempotencia y auditoría con SHA-256. Lee el formato de usuarios que
  diligencia la cooperativa:
  - la hoja **«Usuarios nuevos»**: tipo de documento, número, confirmación del número, primer nombre, otros nombres, primer
    apellido, segundo apellido, correo personal, celular, rol, código de cargo, oficina o agencia, y observaciones;
  - la hoja **«Cargos»**: código y nombre.
  Las hojas «Instrucciones» y «Listas» se ignoran. Los encabezados son los del formato, con o sin el asterisco de obligatorio.
- **FR-028**: Cada fila de «Usuarios nuevos» MUST validarse con **las mismas reglas y los mismos códigos de error** de la
  invitación unitaria de la 017: tipo entre los que acepta la cooperativa, número con el formato y la longitud de su tipo,
  confirmación igual al número, primer nombre y primer apellido, correo personal, celular, rol y cargo. Además, dentro del
  archivo, el correo personal y el par tipo-número MUST ser únicos.
- **FR-029**: El rol MUST ser uno de los cuatro roles de siempre de la cooperativa (CompanyAdmin, Operator, Auditor, ReadOnly).
  Se acepta escrito como en la hoja «Listas» («Administrador de la cooperativa», «Operador», «Auditor», «Solo lectura») o por su
  nombre del sistema, sin distinguir mayúsculas ni tildes. Un rol a la medida no entra por el archivo.
- **FR-030**: El código de cargo MUST existir en el catálogo de cargos de la cooperativa o venir en la hoja «Cargos» del mismo
  libro. La hoja «Cargos» **crea o actualiza** el catálogo con la regla de los códigos de catálogo (hasta 10 caracteres,
  mayúsculas y números, sin espacios, único). Nunca borra un cargo.
- **FR-031**: Aplicar MUST emitir cada invitación por **el mismo camino** de la invitación unitaria de la 017, sin reimplementarlo:
  - la identidad se busca por documento y, si no existe, se crea pendiente;
  - la invitación va al correo que dicten esas reglas, con el rol y el cargo de la fila;
  - lleva el número de intentos al activar de la política de la cooperativa (tres por omisión);
  - el token, el vencimiento, el reenvío, la corrección y la cancelación son los de siempre.
- **FR-032**: Ni la revisión ni el resultado de aplicar MUST permitir distinguir si un documento ya tenía identidad, ni si la
  persona pertenece a otras cooperativas: toda fila que sale lleva **el mismo mensaje** de la invitación unitaria. Sólo se
  informa distinto lo que la 017 ya informa al administrador: «Correo ya registrado» y «ya es miembro». Este último va como aviso
  y deja la fila «sin cambio».
- **FR-033**: Todo o nada: con un error en cualquier hoja MUST NOT salir ninguna invitación ni cambiar el catálogo de cargos. Los
  correos salen sólo después de guardar. Un correo que no sale se informa en su fila y se reenvía desde la lista de invitaciones,
  sin deshacer las demás.
- **FR-034**: La importación MUST exigir un **permiso propio** («Invitar usuarios desde archivo», del administrador de la
  cooperativa). Ese mismo permiso cubre la hoja «Cargos»: hoy el catálogo de cargos no tiene permiso propio. Queda en la auditoría con el archivo y su
  SHA-256, y cada invitación deja además su propio evento, como la unitaria. El límite de filas por archivo es de cada cooperativa
  y viene por omisión en 500.
- **FR-035**: La columna «Oficina / agencia» MUST leerse y mostrarse en el libro de revisión, sin efecto en los permisos ni en la
  invitación. «Observaciones» se conserva en el resultado de la fila. Ninguna de las dos se guarda en la identidad.

**Tablero de implantación**

- **FR-017**: El sistema MUST ofrecer un **tablero de implantación** por cooperativa con los pasos en orden de dependencia. El orden es configurable, y viene por omisión el de la User Story 5. Cada paso muestra:
  - su estado: pendiente, revisado, aplicado, cuadrado o con diferencias aceptadas;
  - el último cargue, quién lo hizo, cuándo, con qué archivo y con qué versión de perfil;
  - el enlace a la pantalla del cargue.
- **FR-018**: El sistema MUST permitir digitar **cifras de referencia** para un corte. Son los totales de control del sistema anterior: conteos y sumas por paso (personas, asociados vigentes, créditos, saldo de capital, intereses, aportes, entre otros), con su fuente. MUST permitir también cargarlas por plantilla. Los totales que se pueden comparar los define el sistema por paso. Los valores son de la cooperativa: ningún valor esperado va en el código.
- **FR-019**: El tablero MUST calcular cada total de control con lo que hay en la base al momento y compararlo con su cifra de referencia. En los pasos con saldo, MUST compararlo también con el saldo de las cuentas del balance de apertura. Para eso usa la equivalencia de cuentas o las cuentas que ya cuadra cada módulo (reusa el cuadre de Cartera y de aportes, no lo duplica).
- **FR-020**: Las diferencias MUST ser **informativas**: nunca impiden aplicar ni activar. Se aceptan con un motivo obligatorio, que queda en la auditoría, y el paso pasa a «con diferencias aceptadas». Un paso sin cifra de referencia se muestra como «sin cifra», no como cuadrado.
- **FR-021**: Cuando se aplica un paso cuyo paso previo no está aplicado, el tablero MUST mostrar un **aviso de orden**. No bloquea: las reglas propias de cada carga siguen decidiendo, por ejemplo «esta plantilla no crea personas».

**Ensayo y operación**

- **FR-022**: El sistema MUST permitir **exportar e importar la configuración de implantación** de una cooperativa: perfiles con sus versiones, tablas de equivalencias y cifras de referencia. Así se ensaya en QA y se repite igual en producción. La importación MUST ser todo o nada y quedar auditada.
- **FR-023**: Los cargues MUST quedar disponibles por la **misma API** para la pantalla y para soporte. Una persona de soporte usa las mismas rutas con sus permisos, y su cargue queda en la auditoría como cualquier otro.
- **FR-024**: Los permisos MUST separar:
  - administrar perfiles y equivalencias;
  - revisar;
  - aplicar cada cargue: se reusa el permiso de importar de cada plantilla destino;
  - digitar cifras de referencia;
  - aceptar diferencias.
  Descargar un libro de revisión con datos personales MUST exigir el permiso de exportar datos personales del área.
- **FR-025**: Toda pantalla nueva MUST llevar su indicador de carga por zona. Ningún texto del producto MUST nombrar al sistema anterior: se dice «sistema anterior», «cifras de referencia» o nada.
- **FR-026**: El procedimiento del ensayo en QA con copia de producción MUST quedar documentado como un runbook. Cubre:
  - el respaldo de origen;
  - la restauración;
  - los usuarios sólo del equipo de implantación;
  - la exportación de la configuración;
  - el borrado de la copia y la constancia (fecha, responsable, respaldo de origen).
  El runbook no toca producción sin la autorización expresa del dueño.

### Key Entities *(include if feature involves data)*

- **Perfil de lectura**: pertenece a una cooperativa y tiene versiones. Guarda la plantilla destino (o la parte de una carga), el tipo de archivo y sus opciones, la codificación, las reglas de salto y los mapeos. Cada versión es inmutable una vez usada.
- **Mapeo de columna**: pertenece a una versión de perfil. Guarda la columna destino, el origen (columna, posición y largo, o constante), los formatos, el tratamiento del vacío y la tabla de equivalencias.
- **Tabla de equivalencias**: pertenece a una cooperativa. Tiene un tipo de código y filas con código anterior, código nuevo, tipo de equivalencia (una a una, varias en una, una en varias) y nota.
- **Cargue de implantación**: es el registro de cada revisión o aplicación. Guarda el paso del tablero, la plantilla destino, el archivo (nombre, SHA-256, líneas), la versión del perfil, el modo, el resultado, quién y cuándo. Enlaza con el cargue propio del módulo cuando lo hay: la tanda de Cartera o la carga de aportes.
- **Cifra de referencia**: pertenece a una cooperativa y a un corte. Guarda el paso, el total de control, el valor, la fuente y quién la digitó.
- **Diferencia aceptada**: guarda el paso, el total, el valor calculado, la cifra de referencia o el saldo contable, el motivo, quién y cuándo.
- **Persona** y **Asociado**: son las entidades existentes de Maestros. Esta feature no les agrega campos; la Persona llega con
  el usuario derivado y el tipo PPT de la 017.
- **Cargo**: el catálogo de cargos de la cooperativa (código y nombre), el mismo de la 017 y de la ficha de empleado.
- **Invitación**: la de la 017, sin campos nuevos. La importación sólo la emite por su mismo camino.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: COOFLOPAL carga desde sus archivos tal como salen del sistema anterior el 100 % de sus personas, asociados, auxiliares, saldos de apertura, cartera y aportes, sin rehacer ningún archivo en Excel.
- **SC-002**: En el ensayo de QA, el recorrido completo (de las equivalencias a la carga de aportes cuadrada) se termina en un día hábil de trabajo del equipo, sin contar la depuración de datos.
- **SC-003**: Cada total de control del tablero coincide con su cifra de referencia, o su diferencia queda aceptada con motivo, antes del go/no-go del 28/11/2026.
- **SC-004**: Un mismo contenido da exactamente los mismos errores, conteos y cuadre si entra por perfil o por la plantilla Excel de siempre (verificado con archivos de muestra de cada plantilla destino).
- **SC-005**: Un archivo de 50.000 líneas se revisa y muestra sus errores en menos de 5 minutos, y la persona ve el avance mientras tanto.
- **SC-006**: El 100 % de los errores de un cargue por perfil se pueden ubicar en el archivo original por su número de línea, sin abrir ningún archivo intermedio.
- **SC-007**: La configuración (perfiles, equivalencias y cifras) que cuadró en QA se lleva a producción en una sola importación, sin volver a digitar nada.

- **SC-008**: El personal de COOFLOPAL (todas las filas del formato devuelto) queda invitado con **una** revisión limpia y **una**
  aplicación, sin digitar ninguna invitación a mano.
- **SC-009**: Para un mismo documento, la invitación que sale por archivo es indistinguible de la unitaria (destino, mensaje, rol,
  cargo, vencimiento e intentos al activar), comprobado con un documento nuevo y otro con identidad existente.

## Assumptions

- Los empleados de COOFLOPAL ya existen en producción (nómina en paralelo desde septiembre). Las fichas, los saldos de prestaciones y los acumulados de nómina **quedan fuera**.
- Las plantillas destino de la apertura, de Cartera, de aportes y de inventario no cambian sus columnas ni sus reglas. Esta feature sólo antepone la lectura por perfil.
- Las migraciones congeladas de las features 013 y 014 no se regeneran. Lo nuevo va en migraciones propias, en par PostgreSQL / SQL Server.
- Todavía no hay muestras de los archivos (2026-10-28). Se construye y se prueba con **muestras sintéticas** sin datos reales.
  Cuando el dueño deje las muestras en una carpeta fuera del repositorio, los perfiles de COOFLOPAL se cargan como dato, sin
  cambiar código.
- La invitación con documento, el catálogo de cargos, la política de tipos aceptados y de intentos, y el usuario derivado de la
  persona son los de la feature 017. Esta feature no los cambia.
- El formato de equivalencias de Contabilidad es el que generan los formatos por equipo del plan de implantación (hoja «Equivalencias»).
- COOFLOPAL lleva sólo créditos y aportes. Las partes de captaciones de ahorro quedan cubiertas por el mismo mecanismo, pero no se ensayan.
- Recaudo y Tesorería (feature 015) no es requisito para cargar. Sí lo es para operar después de activar, y lo resuelve esa feature. Aun así, el código de esta
  feature espera su mezcla en `develop` (decisión del dueño del 2026-10-28).
- La restauración de la copia de producción en QA la ejecuta soporte con autorización expresa del dueño en el momento, con respaldo previo.
- Los saldos se cargan con el corte del 27/11 y se ajustan entre el 28 y el 30/11, como dice el plan de implantación. El tablero trabaja por corte y admite más de uno.
