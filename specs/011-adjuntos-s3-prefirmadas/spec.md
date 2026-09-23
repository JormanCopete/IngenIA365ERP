# Feature Specification: Adjuntos con subida y descarga directas al almacén

**Feature Branch**: `011-adjuntos-s3-prefirmadas`
**Created**: 2026-09-23
**Status**: Draft — decisiones del dueño del 2026-09-23 (ver Clarifications)
**Input**: Solicitud del dueño: (1) que ningún archivo se borre por sí solo —cada cooperativa decide
borrarlos o conservarlos el tiempo que necesite—; (2) optimizar la carga y la descarga para no
consumir memoria de los servicios y aprovechar el almacén de objetos, con **URLs prefirmadas para
subir y para bajar**, el menor costo de AWS posible, buen rendimiento y seguridad. Tras comparar
cifrado propio en flujo (A) con almacenamiento nativo y pases prefirmados (B), el dueño eligió B y,
por costo, su versión «esencial»: sólo los refuerzos que no tienen costo.

## Contexto

Los adjuntos del ERP son los soportes de los comprobantes contables, las planillas PILA, los
archivos de dispersión bancaria y el PDF de la liquidación definitiva. Estado verificado el
2026-09-23:

1. **Todo archivo pasa por el servidor del ERP.** Al subir y al bajar, la API arma el archivo entero
   en memoria unas tres veces (el recibido, el cifrado o descifrado, y el de la verificación). Con el
   tope de 25 MB son ~75 MB por operación simultánea, en servicios de 1 GiB y sin límite de
   concurrencia en las rutas de adjuntos. Diez transferencias grandes a la vez acercan un servicio a
   quedarse sin memoria.
2. **El cifrado propio impide las URLs prefirmadas.** Cada archivo se cifra en la aplicación antes de
   guardarlo, así que el almacén sólo tiene bytes opacos: un enlace directo entregaría basura. Eso
   obliga a que toda descarga pase por el servidor.
3. **Borrar no borra.** Hoy borrar un adjunto marca su fila como eliminada y nada más: el objeto
   queda en el almacén para siempre. Los comentarios del código prometen un «GC programado» que
   nunca se construyó, y la documentación afirma que un adjunto se elimina cuando su dueño lo borra.
   Las dos cosas son falsas.
4. **Un soporte de un comprobante contabilizado se puede borrar por la API.** Sólo la pantalla
   esconde el botón. Los adjuntos que genera un módulo (definitiva, dispersión, PILA) sí están
   protegidos en el servidor.
5. **No hay forma de subir un soporte desde la aplicación.** La pantalla de Comprobantes lista,
   descarga y borra soportes, pero el componente de subida existe sin que ninguna página lo use.
6. **La credencial del ERP ante el almacén es una llave permanente** guardada en el clúster. Dos
   llaves de la cuenta quedaron expuestas en texto plano (la de P1, y la de adjuntos el 2026-09-22).
7. **Estado del almacén:** el bucket de adjuntos existe desde el 2026-09-22, separado del de
   respaldos, con versionado, cifrado en reposo, acceso público bloqueado y una regla que purga a los
   90 días las versiones ya borradas. DEV y QA escriben en él con el formato actual (cifrado propio);
   producción todavía no tiene ningún adjunto ahí.
8. **Se puede colgar un archivo de cualquier documento.** Subir un adjunto no comprueba que el
   documento dueño exista, ni que quien sube tenga permiso sobre él, ni que no sea uno de los tipos que
   genera un módulo. Con el permiso genérico de subir adjuntos se puede agregar un archivo a la PILA o
   a la dispersión de otro, que después nadie puede borrar porque esos tipos son inmutables.
9. **Los soportes de un comprobante se leen sin permiso de contabilidad.** Basta el permiso genérico
   de descargar adjuntos; el permiso de consultar comprobantes no se exige.
10. **PILA y dispersión tienen su propia ruta de descarga**, que pasa el archivo por el servidor y
    aplica reglas del módulo: la PILA exige reconocer el descuadre antes de bajar y declara la
    codificación de caracteres que espera el operador. El documento de la liquidación definitiva, en
    cambio, se vuelve a generar en cada descarga y no lee el almacén: queda fuera de esta feature.

## Clarifications

### Session 2026-09-23

- Q: ¿Cifrado propio en flujo (A) o almacenamiento nativo con pases prefirmados (B)? → A: **B**. El
  dueño acepta que la llave de cifrado en reposo la administre el proveedor del almacén, como hace la
  mayor parte de la industria, a cambio de que subir y bajar no pasen por el ERP.
- Q: ¿B completa (bóveda de llaves propia, CDN con enlaces atados a la conexión, antivirus
  gestionado, registro de cada acceso) o sólo lo que no cuesta? → A: **B esencial**: lo que tiene
  costo mensual queda como opcional futuro. Prioridades del dueño: costo mínimo, pases prefirmados
  para subir y bajar, rendimiento y seguridad.
- Q: ¿Qué se borra automáticamente? → A: **Nada.** La única purga automática es la de lo que una
  persona ya borró, pasado el plazo de la papelera.
- Q: ¿Plazo de la papelera? → A: 90 días, lo que ya tiene configurado el almacén. Propuesto por
  defecto y no objetado.
- Q: ¿Supresión definitiva por Habeas Data? → A: procedimiento de soporte con una credencial
  administrativa distinta a la del ERP, no un botón de la aplicación. Propuesto por defecto y no
  objetado.
- Q: ¿Antivirus? → A: fuera de esta feature por costo. Hoy suben archivos sólo empleados de la
  cooperativa; se agregará un antivirus en el propio clúster, sin costo del proveedor, cuando suban
  personas externas (asociados).
- Q: ¿Qué pasa con un archivo que la validación rechaza? → A: **se retira del almacén** a la papelera
  de 90 días (para poder examinarlo) y la fila queda visible como «rechazado» con el motivo. Nunca fue
  aceptado, así que no cuenta como un archivo de la cooperativa que se borra solo (FR-001, FR-014).
  Decisión del dueño, tras el análisis de consistencia.
- Q: ¿Almacén de secretos gestionado del proveedor? → A: no para esta feature; los secretos nuevos
  quedan en el clúster, que los cifra en reposo (verificado en los dos clústeres). Llevar todos los
  secretos de la plataforma a un almacén gestionado es un proyecto aparte.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Subir un soporte a un comprobante sin pasar por el servidor (Priority: P1)

La auxiliar contable abre un comprobante, va a «Soportes», elige el archivo y lo sube. El archivo
viaja directo al almacén; el ERP sólo autoriza la subida y después comprueba lo que llegó. El
soporte aparece en la lista primero como «subiendo» y enseguida como «disponible», o como
«rechazado» con el motivo si su contenido no corresponde a lo que dice ser.

**Why this priority**: hoy no existe ninguna forma de subir un soporte desde la aplicación, y es lo
primero que pide la implantación de COOFLOPAL: adjuntar la factura o el recibo a su comprobante.
Además es el flujo que más memoria consumía en el servidor.

**Independent Test**: en un comprobante en borrador, subir un PDF de 20 MB y comprobar que queda
«disponible», que se descarga idéntico, y que la memoria del servidor no creció con el tamaño del
archivo.

**Acceptance Scenarios**:

1. **Given** un comprobante y una persona con permiso para subir soportes, **When** sube un PDF
   válido dentro del tamaño máximo, **Then** el soporte queda «disponible», con su nombre original,
   y el contenido del archivo nunca pasó por el servidor del ERP.
2. **Given** un archivo ejecutable renombrado como `.pdf`, **When** termina la subida, **Then** el
   soporte queda «rechazado» con el motivo («el contenido no corresponde a un PDF») y no se puede
   descargar.
3. **Given** un archivo más grande que el máximo, **When** se pide autorización para subirlo,
   **Then** el ERP la niega antes de que se transfiera un solo byte y dice cuál es el máximo.
4. **Given** una autorización de subida emitida, **When** alguien intenta usarla para un archivo de
   otro tamaño, otro tipo u otra ubicación, **Then** el almacén la rechaza.
5. **Given** una autorización de subida que venció sin que se usara, **When** se consulta la lista,
   **Then** el soporte figura como «subida incompleta», con opción de reintentar o de borrarlo.
6. **Given** una persona de la cooperativa A, **When** recibe una autorización de subida, **Then**
   esa autorización no permite escribir en el espacio de ninguna otra cooperativa.
7. **Given** un comprobante ya contabilizado, **When** se sube un soporte nuevo, **Then** se acepta:
   un soporte puede llegar después de contabilizar, y agregarlo no cambia el comprobante.

---

### User Story 2 - Bajar un adjunto directo del almacén (Priority: P1)

Desde cualquier lista de adjuntos —soportes de un comprobante, la planilla PILA, un archivo de
dispersión, el documento de una liquidación definitiva— la persona pulsa «Descargar» y el archivo
baja directo del almacén, con su nombre y su tipo originales. El ERP sólo verifica que tenga permiso
y le entrega un enlace de un minuto.

**Why this priority**: es la otra mitad del consumo de memoria y del ancho de banda del servidor, y
todos los módulos que generan archivos dependen de ella.

**Independent Test**: descargar un adjunto disponible, comparar su huella con la original, y
comprobar que el mismo enlace usado 61 segundos después ya no funciona.

**Acceptance Scenarios**:

1. **Given** un adjunto «disponible» y una persona con permiso de lectura en su módulo, **When**
   pulsa «Descargar», **Then** recibe el archivo idéntico, con su nombre original, sin que el
   contenido pase por el servidor del ERP.
2. **Given** un enlace de descarga emitido, **When** se usa pasados 60 segundos, **Then** el almacén
   lo rechaza.
3. **Given** una persona sin permiso sobre ese módulo o de otra cooperativa, **When** pide descargar
   el adjunto, **Then** recibe la misma respuesta que si el adjunto no existiera.
4. **Given** un adjunto «subiendo», «rechazado», «subida incompleta» o borrado, **When** alguien pide
   descargarlo, **Then** el ERP no emite enlace.
5. **Given** un adjunto de cualquier tipo, **When** se descarga, **Then** el navegador lo guarda como
   archivo: nunca lo abre ni lo interpreta dentro de la página.
6. **Given** un adjunto escrito con el formato anterior (cifrado por la aplicación), **When** se
   descarga, **Then** llega correcto y completo, sin que nadie haya tenido que migrarlo.

---

### User Story 3 - Nada se borra solo; borrar es un acto explícito y recuperable (Priority: P1)

Cada cooperativa decide qué conserva y por cuánto tiempo. Un archivo sólo deja de estar cuando una
persona con permiso lo borra, y aun así queda 90 días en una papelera de donde soporte lo puede
recuperar. Lo que la cooperativa está obligada a conservar —el soporte de un comprobante
contabilizado, un documento que generó un módulo— no se puede borrar por ningún camino.

**Why this priority**: es el requisito explícito del dueño y cierra dos defectos reales: hoy borrar
no elimina nada del almacén, y hoy el soporte de un comprobante contabilizado se puede borrar por la
API.

**Independent Test**: borrar el soporte de un borrador, comprobar que desaparece y que queda
auditado, recuperarlo con la receta de soporte; e intentar borrar el soporte de un comprobante
contabilizado por la API y verificar que se rechaza.

**Acceptance Scenarios**:

1. **Given** un adjunto que nadie toca, **When** pasa cualquier cantidad de tiempo, **Then** sigue
   disponible: ningún proceso lo elimina.
2. **Given** el soporte de un comprobante en borrador y una persona con permiso de borrar, **When**
   lo borra, **Then** desaparece de las listas, deja de poder descargarse, y la auditoría registra
   quién, cuándo, qué archivo y de qué documento.
3. **Given** un adjunto borrado hace menos de 90 días, **When** soporte aplica el procedimiento de
   recuperación, **Then** el adjunto vuelve a estar disponible con el mismo contenido.
4. **Given** un adjunto borrado hace más de 90 días, **When** soporte intenta recuperarlo, **Then**
   ya no es posible, y así lo dice el procedimiento.
5. **Given** el soporte de un comprobante contabilizado, **When** alguien intenta borrarlo desde la
   pantalla o directamente por la API, **Then** se rechaza con una explicación.
6. **Given** un adjunto generado por un módulo (definitiva, dispersión, PILA), **When** alguien
   intenta borrarlo, **Then** se rechaza, como hoy.
7. **Given** un borrador con soportes, **When** alguien lo descarta, **Then** el sistema le avisa que
   tiene soportes y sólo lo descarta si la persona confirma borrarlos en el mismo acto (quedan en la
   papelera como cualquier borrado).
8. **Given** una subida que quedó incompleta, **When** pasa cualquier cantidad de tiempo, **Then**
   sigue visible como «subida incompleta» hasta que alguien la reintente o la borre.

---

### User Story 4 - Los archivos que genera el ERP no cargan la memoria del servidor (Priority: P2)

Al generar una planilla PILA, un archivo de dispersión o el documento de una liquidación
definitiva, el ERP guarda el archivo en el almacén tal como lo generó, sin cifrarlo ni copiarlo de
nuevo en memoria, y quedan disponibles de inmediato. La planilla y el archivo de dispersión se
descargan con el mismo enlace directo de la historia 2, conservando las reglas propias del módulo (la
PILA sigue exigiendo reconocer el descuadre antes de bajarla). El documento de la definitiva se sigue
bajando como hoy, por su ruta que lo vuelve a generar.

**Why this priority**: son archivos que el propio ERP produce y hoy son chicos, así que el riesgo
de memoria es menor que en las subidas de usuarios; pero deben quedar en el mismo formato y bajar
por el mismo camino que todo lo demás.

**Independent Test**: generar una PILA y una dispersión en QA, verificar que quedan disponibles, que
se descargan idénticas por enlace directo y que la memoria del servidor no creció con su tamaño.

**Acceptance Scenarios**:

1. **Given** un período con planilla generada, **When** el ERP guarda el archivo, **Then** queda
   «disponible» sin pasar por la validación de contenido de las subidas de usuarios, porque su origen
   es el propio ERP.
2. **Given** ese archivo, **When** alguien con permiso lo descarga, **Then** baja directo del
   almacén, idéntico al generado.

---

### User Story 5 - La credencial del ERP es de vida corta y alcance mínimo (Priority: P2)

El administrador de la plataforma deja de mantener una llave permanente. El ERP obtiene
credenciales que vencen solas en una hora y se renuevan sin reiniciar; esas credenciales no pueden
ver el inventario del almacén, ni borrar versiones anteriores, ni tocar el almacén de respaldos. Si
alguien las roba sin robar también la base de datos, no encuentra nada, y en una hora dejan de
servir.

**Why this priority**: una llave permanente de esta cuenta ya se filtró dos veces. Con pases
prefirmados, la credencial que los firma es la llave de todo; su alcance y su duración son la
seguridad de este diseño.

**Independent Test**: con la credencial del ERP y sin acceso a la base, intentar enumerar el
almacén y descargar un archivo cualquiera; esperar a que venza y reintentar; revocar el certificado
del ERP y comprobar el corte.

**Acceptance Scenarios**:

1. **Given** el ERP en marcha, **When** pasan más de 60 minutos, **Then** sigue operando con una
   credencial renovada sola, sin reinicio ni intervención.
2. **Given** una credencial del ERP en manos de un tercero sin acceso a la base, **When** intenta
   listar el almacén, **Then** se le niega, y no puede obtener ningún archivo sin conocer su nombre
   exacto.
3. **Given** esa misma credencial, **When** intenta borrar una versión anterior o acceder al almacén
   de respaldos, **Then** se le niega.
4. **Given** que se revoca el certificado del ERP, **When** vence la credencial en curso, **Then** el
   ERP ya no obtiene credenciales nuevas.
5. **Given** la puesta en producción, **When** se revisa la cuenta, **Then** las dos llaves
   permanentes expuestas están desactivadas y ninguna llave permanente accede al almacén de adjuntos.

---

### Edge Cases

- **La conexión se corta a mitad de una subida grande.** Si la subida empezó antes de que venciera la
  autorización, el almacén la recibe completa; si no llegó, el soporte queda «subida incompleta» y se
  puede reintentar con una autorización nueva.
- **El navegador se cierra antes de avisar que terminó.** El archivo pudo llegar igual. La próxima vez
  que alguien consulte la lista, el sistema comprueba si llegó y completa la validación; sólo si no
  llegó lo marca como «subida incompleta».
- **Nombres con tildes, eñes, espacios o muy largos.** Se conservan exactos al descargar; en el
  almacén el objeto no lleva el nombre original.
- **El mismo archivo se sube dos veces.** Quedan dos soportes; no se deduplica.
- **Tipos sin firma reconocible** (texto plano, CSV). Se validan como texto: sin bytes de control
  binarios. Formatos de Office que comparten firma se distinguen por su estructura interna.
- **El almacén no responde.** Subir y bajar fallan con un mensaje claro; el resto del ERP sigue
  funcionando: una nómina se aprueba y un comprobante se contabiliza aunque el almacén esté caído.
- **A la persona le quitan el permiso después de recibir un enlace.** El enlace sigue sirviendo hasta
  vencer (60 segundos para bajar, pocos minutos para subir). Riesgo residual aceptado.
- **Un enlace de descarga reenviado a otra persona.** Funciona dentro de su minuto. Riesgo residual
  aceptado: atarlo a la conexión de quien lo pidió requiere un servicio con costo que el dueño dejó
  fuera.
- **Un comprobante contabilizado se reversa.** Sus soportes siguen sin poder borrarse; la reversión
  es otro comprobante, con sus propios soportes.
- **Picos de uso.** Si llegan más pedidos a las rutas de adjuntos de los que el límite admite, el
  excedente recibe «intente de nuevo en unos segundos», sin afectar al resto del ERP.
- **Archivos del formato anterior.** Siguen bajando a través del servidor, con el consumo de memoria
  de hoy. Son pocos, están acotados por el tope anterior de 25 MB y sólo existen en DEV y QA.

## Requirements *(mandatory)*

### Functional Requirements

**Conservación y borrado (requisito 1 del dueño)**

- **FR-001**: Ningún proceso del sistema MUST eliminar por su cuenta un archivo **aceptado**. Las
  únicas eliminaciones automáticas permitidas son dos: la purga de lo que una persona ya borró, pasado
  el plazo de la papelera; y el retiro, al confirmarla, de una subida que el sistema rechazó y que por
  eso nunca fue aceptada. Ésa también va a la papelera de 90 días.
- **FR-002**: Borrar un adjunto MUST ser un acto de una persona con permiso de borrar adjuntos de ese
  módulo, y MUST quedar auditado: quién, cuándo, qué archivo, de qué documento y de qué cooperativa.
- **FR-003**: Un adjunto borrado MUST desaparecer de las listas y dejar de poder descargarse, y MUST
  poder recuperarse con un procedimiento de soporte documentado durante 90 días.
- **FR-004**: El sistema MUST rechazar el borrado, por cualquier canal, de los soportes de un
  comprobante contabilizado y de los adjuntos generados por un módulo.
- **FR-005**: Descartar un borrador que tiene soportes MUST exigir la confirmación explícita de
  borrarlos en el mismo acto; sin esa confirmación, el borrador no se descarta.
- **FR-006**: Una subida que no se completó MUST quedar visible como «subida incompleta» hasta que
  una persona la reintente o la borre.
- **FR-007**: MUST existir un procedimiento de supresión definitiva (Habeas Data), ejecutado por
  soporte con una credencial administrativa distinta a la del ERP, que elimine el archivo y todas sus
  versiones y deje registro de la supresión.
- **FR-008**: La documentación y los comentarios del código MUST describir el borrado tal como
  ocurre, y no prometer purgas automáticas.

**Subida (requisito 2 del dueño)**

- **FR-010**: Antes de autorizar una subida, el sistema MUST registrar el adjunto en estado
  «subiendo», asociado a su documento y a su cooperativa.
- **FR-011**: El sistema MUST entregar una autorización de subida (URL prefirmada) sólo a quien tiene
  permiso de subir adjuntos en ese módulo y esa cooperativa. La autorización MUST servir para un solo
  archivo y una sola ubicación, con su tipo y su tamaño fijados, y MUST vencer en no más de 5 minutos
  (valor configurable).
- **FR-012**: El contenido del archivo MUST viajar del cliente al almacén sin pasar por el servidor
  del ERP.
- **FR-013**: El almacén MUST rechazar lo que no coincida con la autorización (tamaño, tipo o
  ubicación) y MUST verificar la integridad de lo recibido.
- **FR-014**: Tras la subida, el sistema MUST confirmar que el archivo llegó, que su tamaño coincide
  y que su contenido corresponde al tipo declarado (por la firma de sus primeros bytes; los tipos sin
  firma, como texto y CSV, se validan como texto), y MUST marcarlo «disponible» o «rechazado» con el
  motivo. Un archivo rechazado nunca fue aceptado: su objeto se retira del almacén al rechazarlo —queda
  en la papelera de 90 días como cualquier borrado, para poder examinarlo— y la fila sigue visible como
  «rechazado» con el motivo.
- **FR-015**: Si el cliente no avisa que terminó, el sistema MUST completar la confirmación la
  próxima vez que se consulte la lista de adjuntos de ese documento.
- **FR-016**: Los tipos admitidos MUST seguir siendo los de hoy (PDF, imágenes, documentos y hojas
  de Office, texto y CSV). El tamaño máximo MUST ser configurable por la plataforma, con 25 MB como
  valor inicial, y MUST NOT depender de la memoria del servidor.
- **FR-017**: Contabilidad › Comprobantes MUST permitir subir soportes a comprobantes en borrador y
  contabilizados, y MUST mostrar el estado de cada soporte («subiendo», «disponible», «rechazado» con
  su motivo, «subida incompleta»).
- **FR-018**: Subir y bajar MUST funcionar desde el cliente web y desde el cliente móvil.
- **FR-019**: Una persona MUST poder subir adjuntos sólo a documentos que existen en su cooperativa y
  sobre los que tiene permiso de escritura en su módulo, y MUST NOT poder subirlos a los tipos de
  documento que genera un módulo (PILA, dispersión, liquidación definitiva). En esta feature el único
  destino habilitado es el comprobante contable; los demás se habilitan cuando exista su pantalla.

**Descarga**

- **FR-020**: El sistema MUST entregar una autorización de descarga sólo después de verificar la
  cooperativa y el permiso de lectura del módulo dueño del adjunto, y MUST responder lo mismo cuando
  el adjunto no existe que cuando no corresponde a quien lo pide.
- **FR-021**: La autorización de descarga MUST vencer a los 60 segundos (valor configurable), MUST
  servir sólo para ese archivo, y MUST forzar su descarga como archivo con el nombre y el tipo
  originales.
- **FR-022**: El sistema MUST NOT entregar autorizaciones de descarga para adjuntos que no estén
  «disponibles».
- **FR-023**: El contenido del archivo MUST viajar del almacén al cliente sin pasar por el servidor
  del ERP.
- **FR-024**: Los adjuntos escritos con el formato anterior MUST seguir descargándose correctos a
  través del servidor, sin migración y sin diferencia visible para quien los baja.
- **FR-025**: Leer los soportes de un comprobante MUST exigir, además del permiso de adjuntos, el
  permiso de consultar comprobantes.
- **FR-026**: Las rutas de descarga propias de un módulo (PILA, dispersión) MUST conservar sus reglas
  —por ejemplo, reconocer el descuadre de la PILA— y MUST entregar el mismo tipo de enlace directo,
  declarando el tipo y la codificación de caracteres del archivo.

**Archivos que genera el ERP**

- **FR-030**: La planilla PILA y el archivo de dispersión MUST guardarse tal como se generaron, sin
  cifrado propio ni copias adicionales del contenido en el servidor, MUST quedar «disponibles» al
  guardarse y MUST descargarse como cualquier otro adjunto (FR-020 a FR-023). El documento de la
  liquidación definitiva MUST guardarse igual, pero **se sigue bajando por su ruta actual**, que lo
  vuelve a generar y no lee el almacén. El archivo existe en memoria mientras se genera —así lo
  producen los generadores de PDF y de planos, y son archivos chicos—; lo que se elimina es todo lo
  que venía después.

**Seguridad y costo**

- **FR-040**: Todo archivo nuevo MUST quedar cifrado en reposo por el almacén. Los archivos nuevos no
  llevan cifrado propio de la aplicación.
- **FR-041**: La credencial del ERP ante el almacén MUST ser temporal (vence sola en no más de una
  hora y se renueva sin reiniciar) y revocable. MUST NOT existir una llave de acceso permanente para
  el almacén de adjuntos.
- **FR-042**: Esa credencial MUST poder sólo leer, escribir y borrar objetos del almacén de adjuntos.
  MUST NOT poder listar su contenido, ni borrar versiones anteriores, ni acceder al almacén de
  respaldos.
- **FR-043**: El nombre de cada objeto en el almacén MUST NOT revelar el nombre original, el tipo ni
  el contenido del archivo, MUST ser imposible de adivinar, y sólo la base de la cooperativa lo
  conoce.
- **FR-044**: Una autorización emitida para una cooperativa MUST NOT permitir leer ni escribir
  objetos de otra.
- **FR-045**: Las rutas de adjuntos MUST tener un límite de concurrencia; lo que lo exceda MUST
  recibir un aviso de reintentar, sin afectar al resto del ERP.
- **FR-046**: Pedir una autorización de subida, confirmar, rechazar, pedir una autorización de
  descarga y borrar MUST quedar auditados y MUST exigir su permiso correspondiente.
- **FR-047**: La solución MUST NOT agregar servicios del proveedor con costo mensual fijo: ni bóveda
  de llaves propia, ni red de distribución, ni antivirus gestionado, ni registro de cada acceso a
  datos. Quedan como opcionales futuros.
- **FR-048**: Antes de llevar la feature a producción, MUST estar desactivadas las dos llaves
  permanentes expuestas.

**Operación**

- **FR-050**: MUST existir una receta, o plantilla, que un administrador de la cuenta aplique una sola
  vez para: permitir subidas desde el navegador únicamente desde los orígenes de los tres ambientes,
  crear las credenciales temporales del ERP, y dejar la política de acceso con el alcance de FR-042.
- **FR-051**: El chequeo de salud del almacén MUST seguir probando que se puede escribir, con la
  credencial temporal y con el espaciado actual (como mucho una escritura cada cinco minutos por
  servicio).

### Key Entities

- **Adjunto**: un archivo asociado a un documento de un módulo (comprobante, planilla, dispersión,
  terminación). Guarda nombre original, tipo declarado, tamaño, huella de integridad, cooperativa,
  documento dueño, formato (anterior, con cifrado propio, o directo), ubicación opaca en el almacén,
  estado, motivo de rechazo, y quién y cuándo lo subió, lo confirmó y lo borró.
- **Estado del adjunto**: «subiendo» → «disponible» | «rechazado»; «subiendo» con autorización vencida
  y sin archivo → «subida incompleta»; cualquiera → «borrado» (papelera de 90 días) por acción de una
  persona.
- **Autorización de subida / de descarga**: permiso de corta vida para un solo archivo. No se guarda;
  su emisión queda en la auditoría.
- **Regla de conservación por dueño**: qué adjuntos no se pueden borrar (soporte de comprobante
  contabilizado, adjunto generado por un módulo) y quién puede leer los de cada módulo.
- **Procedimiento de supresión y de recuperación**: receta de soporte, fuera de la aplicación, con su
  propia credencial administrativa.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El consumo de memoria del servidor por cada subida o descarga directa es el mismo
  para un archivo de 1 MB que para uno del tamaño máximo.
- **SC-002**: Veinte personas subiendo o bajando a la vez archivos del tamaño máximo no cambian el
  tiempo de respuesta de las demás pantallas del ERP.
- **SC-003**: El 95 % de las descargas de archivos de hasta 5 MB empiezan en menos de 2 segundos
  desde el clic.
- **SC-004**: En una revisión de 90 días, ningún archivo falta del almacén sin su registro de borrado
  en la auditoría.
- **SC-005**: El 100 % de los enlaces de descarga usados después de 60 segundos fallan, y el 100 % de
  los intentos de usar una autorización de subida para otro archivo, otro tamaño u otra cooperativa
  son rechazados.
- **SC-006**: Con la credencial del ERP y sin acceso a la base, no se obtiene ningún archivo, y esa
  credencial deja de funcionar sola en una hora como máximo.
- **SC-007**: Para una cooperativa típica (10 GB guardados, 5.000 subidas y 20.000 descargas al mes),
  el costo del almacén no supera USD 1 al mes, sin cargos fijos adicionales.
- **SC-008**: El 100 % de los archivos cuyo contenido no corresponde a su tipo declarado (ejecutable
  renombrado a PDF, imagen renombrada a documento, binario declarado como texto) quedan rechazados en
  las pruebas.
- **SC-009**: El 100 % de los intentos de borrar un soporte de un comprobante contabilizado o un
  adjunto generado por un módulo son rechazados, por la pantalla y por la API.
- **SC-010**: El 100 % de los adjuntos escritos con el formato anterior en DEV y QA se siguen
  descargando idénticos.
- **SC-011**: El 100 % de los intentos de subir un adjunto a un documento inexistente, de otra
  cooperativa, sin permiso de escritura en su módulo o de un tipo que genera un módulo son
  rechazados, y el 100 % de los intentos de leer un soporte contable sin permiso de consultar
  comprobantes reciben la misma respuesta que un adjunto inexistente.

## Assumptions

- Hoy suben archivos sólo empleados de las cooperativas; por eso el antivirus queda fuera de esta
  feature. Se agregará en el propio clúster cuando suban personas externas.
- Se reutilizan los permisos de adjuntos existentes (subir, descargar, borrar) y las reglas de
  lectura por módulo; no hacen falta permisos nuevos.
- Se pueden agregar soportes a un comprobante contabilizado, pero no quitarlos: el soporte suele
  llegar después de contabilizar, y agregarlo no altera el comprobante.
- El almacén es el bucket de adjuntos creado el 2026-09-22 en la región más barata del proveedor
  (us-east-1), con el cifrado en reposo, el versionado y la regla de 90 días que ya tiene.
- Producción no tiene adjuntos en el almacén; DEV y QA sí, con el formato anterior, y no se migran.
- Un administrador de la cuenta del proveedor aplica la receta de operación: el usuario con el que se
  opera hoy no tiene permisos para crear identidades ni credenciales.
- Los secretos nuevos (el certificado del ERP) viven en el clúster, que los cifra en reposo.
- Riesgos residuales aceptados por costo: un enlace de descarga reenviado sirve durante su minuto; un
  enlace ya emitido sigue sirviendo hasta vencer aunque se le quite el permiso a quien lo pidió.
- Fuera de alcance: previsualizar archivos dentro de la aplicación (siempre se descargan), deduplicar
  archivos idénticos, migrar los adjuntos del formato anterior, bóveda de llaves propia, red de
  distribución, antivirus, registro de cada acceso a datos y almacén de secretos gestionado.
