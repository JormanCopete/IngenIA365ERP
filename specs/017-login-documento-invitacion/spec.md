# Feature Specification: Ingreso con correo o usuario de documento e invitaciones ancladas al documento

**Feature Branch**: `claude/017-login-documento-invitacion-253fe0` (rama del worktree; equivale a `017-login-documento-invitacion`)

**Created**: 2026-10-27

**Status**: Aprobada por el dueño el 2026-10-28 (D-01..D-16); siguiente paso `/speckit-plan`

**Input**: borrador del dueño `prompt_login_documento.md` (Downloads): «Más datos al crear/invitar
usuarios, ingreso con correo O usuario derivado del documento de identidad, e invitaciones ancladas
al documento. Regla de oro: ningún usuario existente se ve afectado.»

**Documentos de apoyo**: [analisis-sistema-actual.md](analisis-sistema-actual.md) (cómo funciona hoy
y dónde el borrador supone otra cosa) · [diagnostico-de-datos.md](diagnostico-de-datos.md)
(consultas de sólo lectura que el dueño corre en cada ambiente antes del plan).

## Contexto

Hoy una persona se invita por correo y entra con correo, clave y segundo factor. COOFLOPAL (salida en
vivo el 01/12/2026) tiene personal sin correo corporativo: la identidad tiene que quedar anclada al
**documento de identidad**, con el correo personal como canal de invitación y como forma alternativa
de entrar.

### La tensión que ordena toda la spec

El borrador supone **una sola tabla «Persona» global** con documento, nombres, correo y usuario. El
ERP tiene **una base por cooperativa** con aislamiento físico (constitución, Principio IV) y la
persona de negocio (`COR_People`, Principio V) vive **dentro** de cada cooperativa. Lo único global es
la base administrativa, donde hoy está la identidad de acceso (correo, clave, segundo factor,
membresías, invitaciones).

Por eso la spec separa dos cosas que el borrador junta:

- **Identidad de acceso** (global, una por ser humano, en la base administrativa): con qué entra la
  persona —correo verificado o usuario de documento—, su clave, su segundo factor y a qué
  cooperativas pertenece. Aquí viven el documento de acceso y el usuario derivado.
- **Persona de la cooperativa** (una por cooperativa, la de siempre): los datos de negocio que usan
  nómina, contabilidad, cartera y la DIAN. Se enlaza con la identidad por **id interno**, nunca por
  correo ni por documento.

Ninguna tabla de negocio cambia de llave: los módulos ya referencian a la persona por id interno; el
correo sólo es llave **en la capa de identidad**, y es ahí donde trabaja la feature.

## Decisiones del dueño

Todas confirmadas por el dueño el 2026-10-27/28 (detalle en `## Clarifications`).

| # | Decisión | Opción vigente | Estado |
|---|---|---|---|
| D-01 | Dónde vive la identidad global con documento | En la identidad central de la base administrativa; la persona de cada cooperativa se enlaza por id | **confirmada** |
| D-02 | Forma del usuario derivado | CC sin prefijo; los demás con el prefijo del tipo | **confirmada** |
| D-03 | Correos de cuentas existentes frente a «sólo correo verificado» | Se dan por verificados los de toda cuenta que ya entró o aceptó una invitación | **confirmada** |
| D-04 | Cómo completan el documento los usuarios existentes | Paso no bloqueante en cada ingreso hasta completarlo; cada cooperativa puede fijar una fecha a partir de la cual lo exige | **confirmada** |
| D-05 | Invitar a alguien que ya tiene identidad | La invitación va a su correo verificado; el administrador ve el mismo mensaje en todos los casos | **confirmada** |
| D-06 | Documento que no coincide al activar | Tres intentos (configurable por cooperativa); al tercero la membresía queda en revisión y se avisa al administrador | **confirmada** |
| D-07 | Quién corrige o fusiona documentos | Administrador si la persona sólo está en su cooperativa; maestro si está en varias o hay fusión | **confirmada** |
| D-08 | Si corregir el documento de acceso cambia la persona de cada cooperativa | No la cambia: cada cooperativa recibe el aviso de diferencia y la resuelve en su maestro de personas | **confirmada** |
| D-09 | Cambio de correo | Sólo la persona, con clave **y** segundo factor, código al correo nuevo y aviso al anterior | **confirmada** |
| D-10 | Restablecer clave por el administrador de una cooperativa (hoy existe) | Se reemplaza por «enviar enlace de recuperación» | **confirmada** |
| D-11 | Datos que captura la invitación | Tipo y número (dos veces), nombres y apellidos por separado, correo personal, celular, rol y cargo | **confirmada** |
| D-12 | Catálogo de tipos de documento | **Fijo en el sistema** (lista cerrada, sin pantalla; un tipo nuevo es una versión nueva); cada cooperativa elige cuáles acepta | **confirmada** |
| D-15 | Cargo que se captura al invitar | Catálogo de cargos por cooperativa (código y nombre) | **confirmada** |
| D-16 | Entregas | **Una sola entrega**, antes de la salida en vivo de COOFLOPAL (01/12/2026) | **confirmada** |
| D-13 | CC 123456 y CE 123456 en la misma cooperativa (hoy el índice de personas lo impide) | La persona de cada cooperativa gana el campo **usuario** derivado (misma regla que D-02) y la unicidad pasa a ese campo | **confirmada** |
| D-14 | Fusión de identidades duplicadas | Entra en esta feature, sólo para el maestro | **confirmada** |
| D-17 | Longitudes por tipo de documento | CC 3–10, CE 3–10, PPT 3–15, PEP 3–15, TI 3–11, PA 3–16; letras sólo en PA (research R2) | **confirmada** 2026-10-28 |
| D-18 | Invitar sin documento por la API | **No**: toda invitación nueva, de la cooperativa o del maestro, exige documento (`Invitation.DocumentRequired`); las ya enviadas y pendientes siguen sirviendo | **confirmada** 2026-10-28 |
| D-19 | Quién cambia la política de identidad de la cooperativa | El administrador de la cooperativa o el maestro, igual que la política de segundo factor; sin permiso nuevo | **confirmada** 2026-10-28 |

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Quien ya usa el sistema sigue entrando igual (Priority: P1)

Una persona que hoy entra con su correo, su clave y su segundo factor despliega el lunes y entra
exactamente igual: misma pantalla (el campo dice «Correo o usuario»), misma clave, mismo segundo
factor, mismas cooperativas, mismos permisos; y si tenía la sesión abierta durante el despliegue, la
sesión sigue viva.

**Why this priority**: es la regla de oro del dueño. Si falla, la feature no sale.

**Independent Test**: con una base de antes del despliegue (cuentas con y sin segundo factor, con una y
con varias cooperativas, con sesión viva), aplicar la feature y comprobar ingreso, selector de
cooperativa, permisos y renovación de la sesión viva sin ninguna acción de la persona.

**Acceptance Scenarios**:

1. **Given** una cuenta existente con correo y clave, **When** entra con su correo y su clave,
   **Then** pasa al segundo factor y a su cooperativa como antes, sin pedirle cambiar la clave ni
   verificar el correo.
2. **Given** una sesión abierta antes del despliegue, **When** la pestaña renueva la sesión después
   del despliegue, **Then** la renovación funciona y la persona no es expulsada.
3. **Given** una cuenta existente sin documento, **When** entra, **Then** entra igual y además ve el
   paso para registrar su documento (US5), que puede posponer.
4. **Given** una cuenta existente que escribe su correo con mayúsculas o espacios alrededor,
   **When** entra, **Then** entra (igual que hoy).

---

### User Story 2 — Invitar a una persona con su documento (Priority: P1)

El administrador de una cooperativa invita a alguien registrando tipo y número de documento (el número
dos veces), nombres, apellidos, correo personal, celular y rol. La invitación queda anclada al
documento: la persona se identifica por él aunque cambie de correo.

**Why this priority**: es lo que COOFLOPAL necesita para dar de alta a su personal antes del 01/12.

**Independent Test**: invitar a un documento nuevo y a un documento que ya tiene identidad (de otra
cooperativa), y comprobar a qué correo sale cada invitación y qué ve el administrador.

**Acceptance Scenarios**:

1. **Given** un documento que no existe en Ingenia365, **When** el administrador invita con correo
   personal, **Then** se crea una identidad pendiente con esos datos y la invitación sale a ese correo.
2. **Given** los dos números de documento digitados no coinciden, **When** intenta guardar,
   **Then** el formulario lo rechaza antes de enviar.
3. **Given** el correo digitado ya pertenece a **otra** identidad, **When** intenta guardar,
   **Then** se rechaza con «Correo ya registrado» sin decir de quién es.
4. **Given** un documento que ya tiene identidad con correo verificado, **When** el administrador
   invita, **Then** la invitación sale al correo verificado de esa identidad (no al digitado) y el
   administrador ve el mismo mensaje de «Invitación enviada» que en el caso 1, sin enterarse de que la
   persona pertenece a otras cooperativas.
5. **Given** una invitación pendiente, **When** el administrador corrige el documento o la cancela,
   **Then** la anterior deja de servir y, si corrigió, sale una nueva.
6. **Given** la persona ya es miembro activo de esta cooperativa, **When** el administrador la
   invita de nuevo, **Then** se le informa que ya es miembro.

---

### User Story 3 — Activar la invitación confirmando el documento (Priority: P1)

La persona abre el enlace, digita su número de documento, crea su clave (o confirma la que ya tiene si
ya era usuaria en otra cooperativa), pasa por el segundo factor según la política y queda activa. La
pantalla y el correo de bienvenida le dicen: «Puedes ingresar con tu correo o con tu usuario: **XXXX**».

**Why this priority**: sin activación, la invitación no sirve.

**Independent Test**: activar con documento que coincide, con documento que no coincide, y como persona
que ya tenía cuenta.

**Acceptance Scenarios**:

1. **Given** una invitación vigente, **When** la persona abre el enlace, **Then** su correo queda
   verificado.
2. **Given** el número digitado coincide, **When** crea su clave, **Then** la membresía queda activa y
   ve su usuario derivado.
3. **Given** el número no coincide tres veces, **Then** la membresía queda «en revisión», la persona no
   entra a la cooperativa y el administrador recibe un aviso.
4. **Given** la persona ya tenía identidad, **When** confirma con su clave actual, **Then** se agrega
   la membresía sin tocar su clave, su segundo factor ni sus otras cooperativas.
5. **Given** la cooperativa exige segundo factor y la persona no tiene ninguno aceptado, **When**
   activa, **Then** se le pide inscribir uno de los métodos aceptados antes de entrar (igual que hoy).

---

### User Story 4 — Entrar con correo o con usuario de documento (Priority: P1)

Un solo campo «Correo o usuario». Con `@` busca por correo **verificado**; sin `@` busca por usuario,
tolerando puntos, espacios, guiones, minúsculas y el prefijo `CC` escrito de más.

**Why this priority**: es la razón de la feature para quien no tiene correo de uso diario.

**Independent Test**: las combinaciones de la sección de pruebas del borrador (cédula con y sin puntos,
con y sin `CC`; CE, PPT y PA alfanumérico en mayúsculas y minúsculas; CC y CE con el mismo número).

**Acceptance Scenarios**:

1. **Given** cédula 1.234.567, **When** escribe `1234567`, `1.234.567`, `1 234 567` o `CC1234567`,
   **Then** entra con su clave.
2. **Given** pasaporte `AB12345`, **When** escribe `paab12345` o `PA-AB12345`, **Then** entra.
3. **Given** CC 123456 y CE 123456 de personas distintas, **When** cada una escribe su usuario,
   **Then** cada una entra sólo a la suya.
4. **Given** un correo no verificado, **When** intenta entrar con él, **Then** recibe «Credenciales
   inválidas» y la pista fija «Si tu documento no es cédula, antepón el tipo: CE, PPT, PA…».
5. **Given** cualquier identificador inexistente, **When** intenta entrar, **Then** el mensaje y el
   tiempo de respuesta son indistinguibles de una clave errada.
6. **Given** intentos fallidos alternando correo y usuario de la misma persona, **Then** cuentan para
   el mismo bloqueo.

---

### User Story 5 — La persona existente completa su documento (Priority: P2)

Quien entró sin documento ve, después de autenticarse, un paso para registrar tipo y número (dos
veces). Puede posponerlo; reaparece en cada ingreso hasta completarlo, salvo que su cooperativa haya
fijado una fecha desde la cual lo exige.

**Why this priority**: sin esto, los usuarios de hoy nunca tendrían usuario de documento; pero no es
necesario para el alta de COOFLOPAL.

**Acceptance Scenarios**:

1. **Given** una cuenta sin documento, **When** registra un documento libre, **Then** recibe su
   usuario derivado y desde ya puede entrar también con él.
2. **Given** el documento ya pertenece a otra identidad, **Then** no se guarda; se le dice que no se
   pudo registrar y que contacte a su administrador, sin revelar a quién pertenece; se abre un caso
   para el maestro.
3. **Given** la persona pospone, **Then** sigue trabajando en la sesión con normalidad.

---

### User Story 6 — Corregir, actualizar y fusionar documentos (Priority: P2)

El administrador corrige el documento de quien sólo pertenece a su cooperativa; el maestro corrige el
de quien pertenece a varias y fusiona identidades duplicadas. Una actualización legítima (PPT → CE,
extranjero que obtiene CC) sigue el mismo camino con motivo «actualización» y conserva la misma
identidad. Todo queda en el historial de documentos y en la auditoría; la persona recibe su nuevo
usuario por correo y mientras tanto sigue entrando con su correo.

**Acceptance Scenarios**:

1. **Given** una persona sólo en la cooperativa A, **When** el administrador de A corrige con motivo,
   **Then** cambia el documento, se recalcula el usuario, queda el historial y se notifica.
2. **Given** una persona en A y B, **When** el administrador de A intenta corregir, **Then** no puede y
   se le indica que lo solicite al soporte de la plataforma (sin decir que está en B).
3. **Given** dos identidades de la misma persona, **When** el maestro las fusiona, **Then** las
   membresías, el historial y los segundos factores quedan en la correcta, la otra queda inactiva y
   sus sesiones se cierran.
4. **Given** el documento de acceso cambió, **Then** cada cooperativa donde la persona tiene ficha ve
   la diferencia con su persona de negocio y la resuelve allí (D-08).

---

### User Story 7 — Cambiar el correo (Priority: P3)

Sólo la persona cambia su correo desde su perfil. Si perdió el correo sigue entrando con su usuario; si
además olvidó la clave, sólo el maestro cambia el correo, con validación de identidad y auditoría.

**Acceptance Scenarios**:

1. **Given** la persona en su perfil, **When** confirma clave y segundo factor y el código enviado al
   correo nuevo, **Then** el correo cambia y el anterior recibe «Tu correo fue cambiado; si no fuiste tú,
   contacta soporte».
2. **Given** un correo nuevo que ya existe, **Then** se rechaza.
3. **Given** un administrador de cooperativa, **Then** no tiene forma de cambiar el correo de nadie.

---

### User Story 8 — Retirar a alguien de una cooperativa (Priority: P2)

Retirar a una persona de una cooperativa inactiva su **membresía** allí, no su identidad, y cierra sus
sesiones en esa cooperativa; sigue entrando a las demás.

**Acceptance Scenarios**:

1. **Given** una persona en A y B con sesión abierta en A, **When** el administrador de A la retira,
   **Then** la sesión de A se cierra en la siguiente renovación como máximo y la persona sigue
   pudiendo entrar a B.

### Edge Cases

- El usuario derivado de una persona coincide con el prefijo de otro tipo (`PA` + número que empieza
  por letras): el catálogo no admite un prefijo que sea comienzo de otro, y la cédula es sólo dígitos.
- Una persona menor de edad con TI que luego obtiene CC: es una **actualización** (misma identidad).
- Un documento de tipo NIT: no es una persona natural; el catálogo de acceso no lo admite.
- El administrador invita con un correo que es el de la **misma** identidad que el documento: es el
  caso normal, no «Correo ya registrado».
- El documento existe pero la identidad **no** tiene correo verificado (pendiente de otra
  invitación): la invitación sale al correo **de la identidad** (el pendiente), no al digitado, con el
  mismo mensaje al administrador. Así una cooperativa no puede desviar a otro correo la identidad que
  otra creó. Ese correo pendiente sólo lo corrige la invitación que creó la identidad, mientras no se
  haya activado nunca. Las dos invitaciones conviven, cada una con su cooperativa.
- La invitación vence o se cancela mientras la persona tiene el enlace abierto.
- El maestro de plataforma no tiene cooperativa: puede registrar su documento y entrar con usuario,
  sin dejar de exigirle segundo factor.
- Dos administradores invitan el mismo documento a la vez desde cooperativas distintas: una sola
  identidad, dos membresías.
- La persona en revisión intenta usar de nuevo el enlace: se le indica que su administrador debe
  revisar la invitación.
- Una cuenta con correo sin verificar que hoy entra (si el diagnóstico A4 encuentra alguna): la
  migración la da por verificada (D-03) para no dejarla por fuera.

## Requirements *(mandatory)*

### Identidad de acceso

- **FR-001**: La identidad de acceso es **una por ser humano** en toda la plataforma y vive en la
  base administrativa; ninguna base de cooperativa la replica (Principio IV). *(D-01)*
- **FR-002**: La identidad guarda tipo y número de documento (normalizado: sin puntos, espacios ni
  guiones; letras en mayúsculas), nombres y apellidos por separado, celular, correo normalizado y la
  fecha en que el correo se verificó. Documento, celular y fecha de verificación admiten vacío para
  las cuentas existentes.
- **FR-003**: Son únicos en la plataforma: el correo normalizado, el par (tipo, número) y el usuario
  derivado. La unicidad la garantiza la base de datos, no sólo la aplicación; los vacíos no chocan.
- **FR-004**: La persona de cada cooperativa se enlaza con la identidad por id interno. Al activar una
  membresía, la cooperativa busca su persona por tipo y número; si no existe, la crea con los datos de
  la invitación; si existe, la enlaza sin sobrescribir sus datos de negocio.
- **FR-005**: Ninguna tabla de negocio referencia a la persona por correo, documento ni usuario.

### Usuario derivado y catálogo de tipos de documento

- **FR-006**: El usuario **se deriva**, nunca se digita: el número si el tipo es cédula de
  ciudadanía; prefijo del tipo + número en los demás (`CE123456`, `PPT123456`, `PEP123456`,
  `TI123456`, `PAAB123456`). Se calcula en un único punto y se recalcula cada vez que cambia el tipo o
  el número. *(D-02)*
- **FR-007**: El catálogo de tipos de documento de acceso es **fijo en el sistema** (lista cerrada,
  sin pantalla de mantenimiento): CC, CE, PPT, PEP, TI y PA, cada uno con código, nombre, prefijo de
  usuario (vacío sólo para la cédula de ciudadanía), si admite letras, longitud mínima y máxima y su
  equivalencia con el código de persona de la cooperativa y con el código DIAN. Agregar un tipo es una
  versión nueva del producto. Ningún prefijo es el comienzo de otro, y una prueba lo vigila. *(D-12)*
- **FR-008**: Cada cooperativa elige qué tipos del catálogo acepta en sus invitaciones (por defecto:
  todos). Es la única parte parametrizable del catálogo.
- **FR-008a**: Cada cooperativa mantiene su catálogo de **cargos** (código con la regla de los
  catálogos de la cooperativa y nombre); el cargo de la invitación se elige de él y queda en el
  usuario de esa cooperativa como dato informativo, sin efecto en los permisos. *(D-15)*
- **FR-009**: El catálogo de personas de la cooperativa incorpora los tipos que hoy le faltan (PPT, y
  los que el catálogo de acceso tenga) para que la persona de negocio pueda llevar el mismo tipo.
- **FR-010**: La persona de cada cooperativa gana un campo **usuario**, derivado con la misma regla
  de FR-006 (cédula de ciudadanía: sólo el número; otro tipo: prefijo + número; nunca se digita) y
  recalculado al cambiar tipo o número. La unicidad de la persona en la cooperativa pasa del número
  solo a ese usuario —incluso frente a eliminadas, como hoy—, de modo que CC 123456 (`123456`) y
  CE 123456 (`CE123456`) coexisten. El enlace con la identidad de acceso sigue siendo por id interno,
  nunca por este campo. *(D-13)*

### Ingreso

- **FR-011**: El ingreso tiene un solo campo «Correo o usuario» y la clave.
- **FR-012**: Con `@`, se normaliza (minúsculas, sin espacios) y se busca sólo entre correos
  verificados.
- **FR-013**: Sin `@`, se normaliza (mayúsculas; sin espacios, puntos ni guiones; sin el prefijo `CC`
  si lo escribieron) y se busca coincidencia **exacta** con el usuario derivado.
- **FR-014**: Todo fallo responde «Credenciales inválidas» (401, como hoy) con la pista fija «Si tu
  documento no es cédula, antepón el tipo: CE, PPT, PA…». La pista se arma con los prefijos del
  catálogo vigente.
- **FR-015**: El tiempo de respuesta es indistinguible exista o no la identidad (hoy no lo es).
- **FR-016**: El bloqueo progresivo cuenta **por identidad** cuando el identificador resuelve a una, y
  por identificador normalizado cuando no; el límite por IP se conserva. Alternar correo y usuario no
  da intentos extra.
- **FR-017**: Lo que sigue a la clave no cambia: segundo factor obligatorio (incluido el maestro),
  política de métodos por cooperativa, selector de cooperativa sin membresías inactivas, duración e
  inactividad de la sesión.
- **FR-018**: Las sesiones y tokens vivos antes del despliegue siguen siendo válidos; la renovación
  no exige ningún dato nuevo.

### Invitación

- **FR-019**: La invitación captura tipo de documento (entre los aceptados por la cooperativa), número
  digitado dos veces, primer nombre, otros nombres, primer y segundo apellido, correo personal,
  celular, rol de la cooperativa (el que dará los permisos al activar; hoy siempre entra con «sólo
  lectura») y cargo (informativo, distinto del rol de permisos). *(D-11)*
- **FR-020**: Si el correo digitado pertenece a otra identidad, se rechaza con «Correo ya registrado»
  sin revelar a quién.
- **FR-021**: Si el documento no existe, se crea una identidad pendiente y la invitación sale al
  correo digitado. Si existe, se crea sólo la membresía y la invitación sale al correo de la identidad
  (el verificado, o el pendiente si aún no se ha activado), nunca al digitado. El administrador ve el mismo mensaje en los dos casos y nunca se entera de
  otras cooperativas de la persona. *(D-05)*
- **FR-022**: El token es de un solo uso, se guarda como hash y vence según configuración (hoy 7
  días). Se puede reenviar, corregir (antes de aceptar) y cancelar.
- **FR-023**: Invitar a quien ya es miembro activo de la cooperativa se rechaza con un mensaje claro.
- **FR-024**: El alta de una cooperativa con su primer administrador (`with-admin`) captura los mismos
  datos de la persona administradora.

### Activación

- **FR-025**: Abrir el enlace vigente verifica el correo.
- **FR-026**: La persona digita su número; si coincide crea su clave (o confirma la suya si ya tenía
  identidad) y la membresía queda activa. Si no coincide en el número de intentos que fije la
  cooperativa (por defecto 3), la membresía queda «en revisión»,
  no da acceso y se avisa al administrador, que corrige y reenvía o cancela. *(D-06)*
- **FR-027**: Después de activar se aplica la puerta del segundo factor como hoy.
- **FR-028**: La pantalla de activación y el correo de bienvenida dicen «Puedes ingresar con tu correo
  o con tu usuario: **XXXX**».

### Completar el documento (cuentas existentes)

- **FR-029**: Tras un ingreso correcto de una cuenta sin documento se muestra un paso para registrar
  tipo y número (dos veces); se puede posponer y reaparece en cada ingreso. *(D-04)*
- **FR-030**: Cada cooperativa puede fijar una fecha desde la cual el paso deja de ser posponible para
  entrar a **esa** cooperativa; sin fecha, nunca bloquea. La fecha es informativa hasta que llega.
- **FR-031**: Un documento que ya pertenece a otra identidad no se guarda y abre un caso para el
  maestro, sin revelar a quién pertenece.

### Corrección, actualización y fusión

- **FR-032**: Antes de aceptar, el documento se corrige editando o cancelando la invitación.
- **FR-033**: Con la persona activa sólo en una cooperativa, la corrige el administrador de esa
  cooperativa con motivo obligatorio («corrección» o «actualización») y soporte opcional. Con
  membresías en varias, sólo el maestro. *(D-07)*
- **FR-034**: Si el documento correcto ya pertenece a otra identidad, el maestro las **fusiona**:
  membresías, historial, segundos factores y vínculos de cada cooperativa pasan a la correcta; la otra
  queda inactiva y sus sesiones se cierran. *(D-14)*
- **FR-035**: Toda corrección o actualización conserva la identidad, recalcula el usuario, queda en
  el historial de documentos y notifica a la persona su usuario nuevo al correo verificado.
- **FR-036**: Cambiar el documento de acceso no modifica la persona de negocio de ninguna cooperativa;
  cada cooperativa donde no coincida ve la diferencia y la resuelve en su maestro de personas. *(D-08)*

### Correo y clave

- **FR-037**: El correo sólo lo cambia la persona desde su perfil: clave y segundo factor, correo
  nuevo no registrado, código al correo nuevo, aviso al anterior. *(D-09)*
- **FR-038**: Si perdió el correo y la clave, sólo el maestro cambia el correo, con validación de
  identidad y motivo auditado.
- **FR-039**: Ningún administrador de cooperativa cambia la clave ni el correo de nadie; en lugar de
  restablecer la clave (lo que hoy hace), envía un enlace de recuperación al correo verificado.
  *(D-10)*
- **FR-040**: La recuperación de clave acepta correo o usuario y siempre envía al correo verificado;
  responde igual exista o no. El canal SMS/WhatsApp queda previsto como otra vía de envío, sin
  implementarse.
- **FR-041**: La recuperación del segundo factor por correo conserva sus reglas actuales (apagada por
  defecto, demora, cancelar sin pedir nada, no devuelve sesión) y sólo sale a un correo verificado.

### Membresía

- **FR-042**: Los estados de membresía son invitada, en revisión, activa, suspendida y revocada.
- **FR-043**: Retirar de una cooperativa inactiva la membresía, no la identidad, y cierra las
  sesiones de esa cooperativa a más tardar en la siguiente renovación; las demás no se tocan. El
  vínculo con la identidad se resuelve por id, no por correo, y el autor del retiro es quien retira.

### Auditoría

- **FR-044**: Todo cambio de documento, usuario, correo, fusión, envío de enlace de recuperación,
  invitación (emitida, reenviada, corregida, cancelada, en revisión) y activación queda auditado con
  valor anterior, valor nuevo, quién, motivo y fecha. Lo que no pertenece a una cooperativa va a la
  auditoría global; lo de una cooperativa, a la suya.
- **FR-045**: El historial de documentos guarda tipo y número anteriores y nuevos, motivo, quién,
  soporte opcional y fecha, y no se borra.

### Despliegue sin afectar a nadie

- **FR-045a**: Toda la feature sale en **una sola entrega**, antes de la salida en vivo de COOFLOPAL
  (01/12/2026). *(D-16)*
- **FR-046**: El despliegue sigue expandir → migrar → contraer. Expandir sólo agrega (columnas,
  tablas, índices que admiten vacío); no renombra ni borra.
- **FR-047**: La migración de datos es idempotente, normaliza correos y documentos, da por verificados
  los correos de las cuentas que ya entraron o aceptaron invitación, toma el documento de la persona
  enlazada cuando todas sus cooperativas coinciden y **reporta sin corregir** duplicados y conflictos.
  *(D-03)*
- **FR-048**: Contraer (p. ej. documento obligatorio) es una entrega posterior con aprobación del
  dueño. Cada migración tiene su reversa (Principio XII).
- **FR-049**: El diagnóstico de `diagnostico-de-datos.md` se corre en cada ambiente antes de la
  migración y su resultado es insumo del plan.

### Key Entities

- **Identidad de acceso** (global): documento de acceso, usuario derivado, nombres, celular, correo y
  su verificación, clave, segundo factor, estado. Es la cuenta central de hoy, ampliada.
- **Tipo de documento de acceso** (lista fija del sistema): código, nombre, prefijo, reglas de formato,
  equivalencias con la persona de cooperativa y con la DIAN.
- **Cargo** (catálogo de cada cooperativa): código y nombre.
- **Tipos aceptados por cooperativa**: qué tipos del catálogo admite cada cooperativa.
- **Membresía**: identidad + cooperativa + estado (incluido «en revisión») + fechas. El rol y el
  cargo quedan en el usuario de la cooperativa.
- **Invitación**: cooperativa, identidad si ya existía, documento, nombres, celular, rol, correo de
  destino, token (hash), vencimiento, estado, quién invita, intentos de documento.
- **Historial de documentos**: identidad, anterior, nuevo, motivo, quién, soporte, fecha.
- **Caso de identidad**: conflicto que resuelve el maestro (documento de otra identidad, fusión
  pendiente).
- **Persona de la cooperativa** (existente): sin cambios de rol; gana el tipo PPT y el campo usuario
  derivado, que pasa a ser su llave única (D-13).

## Success Criteria *(mandatory)*

- **SC-001**: 0 cuentas existentes afectadas: el 100 % de las cuentas que entraban antes del despliegue
  entran después con el mismo correo, la misma clave y el mismo segundo factor, con las mismas
  cooperativas y permisos.
- **SC-002**: 0 sesiones vivas expulsadas por el despliegue.
- **SC-003**: Una persona sin correo corporativo queda invitada, activada y trabajando en menos de 10
  minutos desde que el administrador abre el formulario, usando sólo su correo personal y su documento.
- **SC-004**: El usuario derivado es consistente con tipo y número en el 100 % de las identidades con
  documento, comprobado por una consulta de control.
- **SC-005**: Ninguna respuesta del ingreso, la invitación ni la recuperación permite distinguir si un
  correo, un documento o una membresía en otra cooperativa existen (salvo el «Correo ya registrado»
  que el dueño acepta para el administrador).
- **SC-006**: El 100 % de los cambios de identidad aparecen en la auditoría con anterior, nuevo, autor y
  motivo.
- **SC-007**: Correr la migración de datos dos veces deja exactamente el mismo resultado, y su reversa
  devuelve el esquema anterior.

## Assumptions

- El segundo factor, las passkeys, la política de métodos y la recuperación por correo no cambian de
  reglas; esta feature cambia **con qué** se identifica la persona antes de la clave, no lo que sigue.
- Entrar con passkey sin escribir usuario (ingreso sin clave) queda fuera.
- La etiqueta de la cuenta en la app autenticadora sigue siendo el correo: cambiarla no afecta a nadie
  y no aporta.
- El token central sigue llevando el correo; no se agrega ningún dato obligatorio al token, para que
  las sesiones vivas sigan valiendo.
- El rol que se elige al invitar es uno de los roles de la cooperativa (hoy la invitación siempre da
  «sólo lectura» y el rol se cambia después).
- El correo «Correo ya registrado» revela al administrador que ese correo existe en la plataforma; el
  borrador del dueño lo acepta a cambio de no crear duplicados.
- El NIT no es documento de acceso: las personas jurídicas no inician sesión.
- Los textos del producto no mencionan el sistema anterior.

## Out of Scope

- Envío por SMS o WhatsApp (sólo queda la interfaz prevista).
- Hacer obligatorio el documento para todos (es la fase «contraer», posterior).
- Cambios en la persona de negocio más allá del catálogo de tipos y la unicidad por tipo + número.
- Ingreso sin clave con passkey.

## Clarifications

### Session 2026-10-27

- Q: ¿Dónde vive la identidad global (documento de acceso, usuario, nombres, celular, correo
  verificado)? → A: en la cuenta central de la base administrativa, ampliada; la persona de cada
  cooperativa se enlaza por id interno. (D-01)
- Q: Hoy en una cooperativa no pueden coexistir CC 123456 y CE 123456. ¿Qué hacemos? → A: se agrega
  a la persona otro campo **usuario**, la concatenación del tipo de documento con el número, salvo la
  cédula de ciudadanía, que no lleva tipo. Ejemplo del dueño: CC 23456 → `23456`; CE 987654 →
  `CE987654`. La unicidad de la persona pasa a ese campo. (D-02, D-13; FR-006, FR-010)
- Q: ¿Qué pasa con las cuentas existentes sin correo marcado como verificado? → A: la migración las da
  por verificadas si ya entraron o aceptaron una invitación. (D-03, FR-047)
- Q: ¿El administrador de cooperativa sigue fijando claves? → A: no; se reemplaza por «Enviar enlace
  de recuperación» al correo verificado. (D-10, FR-039)
- Q: Se invita un documento que ya tiene cuenta. ¿Qué ve el administrador y a dónde va? → A: va al
  correo verificado de esa persona y el administrador ve siempre el mismo mensaje («Invitación
  enviada. Si la persona ya tiene cuenta, le llegará a su correo registrado»). (D-05, FR-021)
- Q: ¿Cuándo pasa a «en revisión» si el documento no coincide al activar? → A: al tercer intento,
  configurable por cooperativa. (D-06, FR-026)
- Q: ¿Quién corrige el documento de una persona activa? → A: según membresías: su administrador si
  sólo está en esa cooperativa; el maestro si está en varias; el administrador sólo ve «sólo lo puede
  hacer soporte». (D-07, FR-033)
- Q: ¿Corregir el documento de acceso cambia la persona de negocio de cada cooperativa? → A: no; cada
  cooperativa ve la diferencia y la resuelve en su maestro de personas. (D-08, FR-036)
- Q: ¿Cómo completan el documento las cuentas existentes? → A: paso posponible tras cada ingreso;
  cada cooperativa puede fijar una fecha desde la cual ya no se pospone para entrar a ella. (D-04,
  FR-029, FR-030)
- Q: ¿Qué se pide para que la persona cambie su correo? → A: clave, segundo factor y código al correo
  nuevo; aviso al anterior. (D-09, FR-037)
- Q: ¿Qué datos captura la invitación? → A: nombres y apellidos separados, celular, rol de la
  cooperativa y cargo, además de documento (dos veces) y correo. (D-11, FR-019)
- Q: ¿La fusión entra en esta feature? → A: sí, sólo para el maestro. (D-14, FR-034)
- Q: ¿Quién administra el catálogo de tipos de documento? → A: es fijo en el sistema, sin pantalla;
  cada cooperativa sigue eligiendo cuáles acepta. (D-12, FR-007, FR-008)
- Q: ¿El cargo es texto libre o catálogo? → A: catálogo por cooperativa. (D-15, FR-008a)
- Q: ¿Cómo se entrega? → A: en una sola entrega, antes del 01/12/2026. (D-16, FR-045a)

### Session 2026-10-28 (tras construir el MVP)

- Q: ¿Se confirman las longitudes por tipo de documento? → A: sí, las de research R2: CC 3–10, CE 3–10,
  PPT 3–15, PEP 3–15, TI 3–11, PA 3–16; letras sólo en PA. (D-17, T001)
- Q: ¿La API sigue aceptando invitaciones sólo por correo, por compatibilidad? → A: no; toda invitación
  nueva exige el documento, también la del maestro. Las invitaciones ya enviadas y pendientes siguen
  sirviendo. (D-18)
- Q: ¿La política de identidad lleva un permiso propio? → A: no; la cambian el administrador de la
  cooperativa o el maestro, igual que la de segundo factor. (D-19)
