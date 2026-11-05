# Research — 016 Cargue de archivos planos para la implantación

Decisiones del plan (R1–R16). Cada una dice qué se eligió, por qué y qué se descartó. Las nueve preguntas que eran del dueño
quedaron resueltas el 2026-10-28 (§ Decisiones del dueño, D1–D9).

Punto de partida verificado en `develop` 511e4cc9 (trae 012, 013 y 014):

- `Application/Common/Imports`: `DefinicionDePlantilla`, `EjecutorDeImportacion` (topes: 16 MB, 60.000 filas por hoja, 1.000
  errores, 5.000 cambios), `ContextoDeImportacion`, `FilaDeImportacion`, `ImportResultDto`, `ArchivoDeImportacion` (con SHA-256).
- `ITabularFileReader` (implementación `ClosedXmlTabularFileReader` en la API) devuelve `TablaLeida` con `FilaLeida.Numero`, que
  es el número de fila del archivo. Compara encabezados con `TablaLeida.Normalizar` (sin mayúsculas, tildes, espacios ni `_`).
- **No todas las cargas usan el ejecutor común.** `ImportAccountsCommand` y `ImportOpeningBalancesCommand` (009) reciben
  `(FileName, Content)` y leen por su cuenta con `ITabularFileReader`. `ImportMigrationPartCommand` (013) e
  `ImportDepositMigrationPartCommand` (014) sí usan `IComandoDeImportacion`.
- `IEjecutorEnCooperativa` ejecuta un trabajo de fondo en la base de una cooperativa con un `Actor` que puede ser **la persona que
  lo ordenó**. `ILectorDeParametros` / `COR_ParameterVersions` dan parámetros con vigencia por cooperativa.
- `COR_Positions` (`Position`: `LegacyCode` de 10, `Name`) es el catálogo de cargos que reutiliza la 017.
- Formatos de la cooperativa: `Formato-usuarios-ERP-COOFLOPAL-v2.xlsx` (hojas «Instrucciones», «Usuarios nuevos», «Cargos»,
  «Listas») y `formato-contabilidad-equivalencias-y-depuracion.xlsx` (hoja «Equivalencias»: «Código actual», «Cuenta CUIF (6
  dígitos)», «Auxiliar nuevo (código)», «Tipo de equivalencia», entre otras).

## R1. Dónde entra el perfil: un lector, no una plantilla nueva por carga

**Decisión**: el perfil de lectura se aplica **antes** de la plantilla destino, como otra forma de leer el archivo. Se agrega
`ILectorPorPerfil` (Application, puro) que, con una versión de perfil y los bytes, devuelve la **misma `TablaLeida`** que hoy
devuelve el lector de libros:

- encabezados = los nombres de columna de la plantilla destino (`DefinicionDePlantilla`, o la definición de la parte);
- `FilaLeida.Numero` = **la línea del archivo original**, no un índice nuevo;
- celdas ya convertidas al texto canónico que espera `FilaDeImportacion` (fechas `yyyy-MM-dd`, números con punto decimal, códigos
  ya traducidos por la equivalencia).

Para que la plantilla destino no cambie (FR-009, SC-004), el lector se elige por **un decorador de `ITabularFileReader`**: si la
petición en curso trae un perfil (`LecturaPorPerfilEnCurso`, servicio con alcance de petición que llena un comportamiento de MediatR
para los comandos que implementan `IConPerfilDeLectura`), lee con el perfil; si no, delega en el de siempre. Así los cuatro
caminos —ejecutor común, auxiliares, apertura, partes de 013 y 014— reciben la tabla por el mismo puerto sin tocar su regla.

**Por qué**: es la única forma de que «la regla de negocio sea la misma del cargue con la plantilla Excel» sin duplicar ningún
handler, y de que los errores citen la línea original (FR-010, SC-006) sin un archivo intermedio.

**Descartado**: (a) convertir el plano a un `.xlsx` intermedio y subirlo: pierde la línea original y produce el «Excel intermedio
que la persona nunca vio» que prohíbe la US4; (b) una plantilla nueva por cada carga con perfil: duplica reglas.

## R2. Errores de lectura y de equivalencia: se juntan con los de la plantilla

**Decisión**: los errores que nacen al leer (línea corta, fecha imposible, número que no encaja) y al traducir (código sin
equivalencia, «una en varias») los anota el lector en `LecturaPorPerfilEnCurso`, con línea, columna destino y **columna o posición
de origen**. La fila con error de lectura **no pasa** a la tabla de la plantilla, para que no produzca errores derivados. El
comportamiento junta esos errores con los del `ImportResultDto` y:

- en `review`, responde 200 con todo, `valid = false` si hay alguno;
- en `apply`, si hay errores de lectura no llama al handler y responde el 422 `Import.Invalid` con el mismo cuerpo.

Las cargas que no devuelven `ImportResultDto` (auxiliares y apertura de la 009, que devuelven `data.errors[] { row, column }`) se
adaptan igual: el error de lectura se convierte a su forma (`row` = línea original). Códigos nuevos en `Import.Profile.*` y
`Import.Equivalence.*` (contracts/plantillas.md §1.4).

## R3. Archivos grandes: la carga por perfil corre en segundo plano

**Decisión**: revisar o aplicar con perfil **no es una petición síncrona**. El flujo es:

1. la pantalla sube el archivo directo al bucket con la autorización firmada de la feature 011 (dueño nuevo
   `ImplementationLoad` en `AdjuntosDeModulo`, que admite subidas a quien tiene el permiso de revisar cargues);
2. `POST /api/implementation/loads` crea el **cargue de implantación** (`COR_ImplementationLoads`) en `Queued` con el adjunto, el
   perfil, la plantilla destino y el modo;
3. un trabajo por `IEjecutorEnCooperativa`, con `Actor` = **la persona que lo pidió** y sus permisos capturados al pedir, lee el
   objeto, corre la carga destino y va dejando el avance (líneas leídas / total) en el registro;
4. la pantalla consulta el avance cada pocos segundos (`GET /api/implementation/loads/{id}`) y al terminar muestra el
   resultado, que queda guardado en el registro (con los topes de siempre) y el libro de revisión se arma a pedido.

**Por qué**: SC-005 pide 50.000 líneas en menos de 5 minutos con avance visible; el túnel corta peticiones largas a los 100 s y una
revisión larga no puede bloquear la pantalla. Guardar el archivo además da la trazabilidad de qué se cargó.

**Consecuencia**: la mecánica común dice «el archivo no se guarda» (§0.5 de la 012). En un cargue por perfil **sí se guarda**, como
adjunto no borrable por el módulo, porque el trabajo de fondo lo necesita. Queda en Complexity Tracking y la decisión D3
(retención).

**Descartado**: subir por la API (`InputFile`): lo prohíbe `LosAdjuntosNoPasanPorElServidor`. Reducir el archivo a tandas
chicas obligatorias: lo que ya existe en Cartera se conserva, pero no alcanza a personas y aportes.

## R4. Perfiles versionados

**Decisión**: `COR_ReadProfiles` (código único por cooperativa, plantilla destino y parte) con `COR_ReadProfileVersions` y
`COR_ReadProfileMappings`. Una versión se **sella** (`UsedAt`) la primera vez que un cargue `apply` la usa; desde entonces es
inmutable y modificar el perfil crea la versión siguiente (FR-004). Una versión no usada se edita en su sitio. El cargue guarda
`ReadProfileVersionId`. Al guardar se valida contra la definición de la plantilla destino: toda columna obligatoria llena por un
mapeo o una constante, ningún destino dos veces, posiciones que no se cruzan en ancho fijo (FR-003).

**Descartado**: guardar el perfil como un JSON en una sola columna: la validación por columna y la exportación con versiones
quedan más claras en tablas, y el JSON pegado se admite igual como forma de editar (como los formatos bancarios de la 010).

## R5. Codificación de caracteres

**Decisión**: el perfil declara `utf-8`, `windows-1252` o `iso-8859-1` (lista cerrada, `CodificacionesDeArchivo`). La lectura usa
`Encoding.GetEncoding` con el proveedor de páginas de código de .NET (`CodePagesEncodingProvider`, ya disponible en el runtime). La
vista previa cuenta los caracteres de reemplazo (`U+FFFD`) y los bytes de control fuera de tabulador y saltos, y avisa
`Import.Profile.EncodingSuspect` con las primeras líneas afectadas (US2 escenario 3). Un BOM UTF-8 se respeta aunque el perfil diga
otra cosa, y se avisa.

## R6. Números, fechas y signos

**Decisión**: cada mapeo declara formato de fecha (patrones .NET exactos, `ParseExact`, cultura invariante; lista de vacíos que
significan «sin fecha», por ejemplo `00/00/0000`) y de número (separador de miles, separador decimal, decimales implícitos, signo
adelante, atrás o entre paréntesis). Lo que no encaja es error de celda; **nunca cero** (borde de la spec). El lector entrega el
texto canónico y la plantilla destino aplica su propia conversión de siempre (`TipoDeValor`).

## R7. Equivalencias

**Decisión**: `COR_CodeEquivalenceTables` (código, nombre, tipo de código) y `COR_CodeEquivalences` (código anterior, código
nuevo, clase `OneToOne`, `ManyToOne`, `OneToMany`, `NotMigrated`, nota). Reglas:

- un código anterior con dos destinos distintos sólo si **todas** sus filas son `OneToMany`; si no, error con la fila de cada uno
  (US2 escenario 4);
- `OneToMany` al cargar es error de la fila que pide decidir la cuenta nueva (US4 escenario 3); `NotMigrated` también es error
  («no se migra»), nunca se omite en silencio;
- `ManyToOne` es normal: las filas se suman en el destino cuando la plantilla lo permite, porque eso lo decide la plantilla (la
  apertura ya agrega por cuenta, tercero y documento cruce);
- el destino que no existe (una cuenta que no está en el plan) es **aviso** al cargar la tabla (US2 escenario 5); el tipo de código
  sabe contra qué catálogo comprobar (`TiposDeCodigo`: cuenta, oficina, sucursal, línea de crédito, ciudad, tipo de documento,
  motivo de retiro, grupo de asociados, banco, otro).

Se cargan con la mecánica común (plantilla `implementation.equivalences`) y con una **lectura del formato de la contadora**
(plantilla `implementation.equivalences-accounting`, hoja «Equivalencias»): código nuevo = «Auxiliar nuevo (código)» si viene, si
no «Cuenta CUIF (6 dígitos)»; «Tipo de equivalencia» se traduce («Una a una», «Varias en una», «Una en varias», «Sin equivalente
(no se migra)»). Las columnas que el formato calcula con fórmula se ignoran.

## R8. Plantilla de personas y terceros sobre la persona de la 017

**Decisión**: plantilla `core.people` en el ejecutor común. Por fila arma un `PersonInput` y pasa por **el mismo validador y la misma
fábrica** del alta unitaria (`PersonFactory`, `AltaConAutorizacion`); para actualizar usa el mismo mapeo de `UpdatePersonCommand` con
la regla «sólo lo que trae valor» (FR-015). La llave es el **usuario derivado** de la 017 (`COR_People.Username`), calculado por el
punto único de la 017 (`UsuarioDeDocumento`); se carga en bloque el diccionario `Username → persona` (vivas y eliminadas) y nunca
se consulta por fila. Las marcas que admite el archivo son `IsSupplier` y `IsCustomer`; las de asociado, empleado y vendedor no
están en la definición. El tipo `PT` (PPT) lo admite porque lo agrega la 017.

**Dependencia**: si la 017 no expone el cálculo del usuario como función pura de Domain reutilizable, la tarea se bloquea (ver
«Dependencias» en plan.md).

## R9. Plantilla de asociados

**Decisión**: plantilla `core.associates` en el ejecutor común. Resuelve la persona viva por usuario derivado (sin persona viva:
`Import.Cell.NotFound` con «esta plantilla no crea personas», el mismo texto de Cartera). Crea con la lógica de
`RegisterAssociateCommand` (que enciende `IsAssociate` por el mismo camino), actualiza con la de `UpdateAssociateCommand`, y el
estado retirado sigue la regla de la 014: **el retiro nace en Cartera** (`Associate.WithdrawalOwnedByLending`). Por eso el archivo
sólo puede **crear** un asociado ya retirado, como estado histórico con su fecha y su motivo traducido; pasar a retirado a un
asociado vigente que ya existe es error de la fila con ese mismo código (decisión D8). Para no ejecutar un comando MediatR por fila (miles de
`SaveChanges`), se extrae de esos handlers el servicio que aplica la regla sobre el contexto sin guardar (`AltaDeAsociado`,
`RetiroDeAsociado`), y los handlers unitarios pasan a usarlo: una sola regla.

## R10. Las cargas que ya existen aceptan perfil

**Decisión**: los comandos `ImportOpeningBalancesCommand`, `ImportAccountsCommand`, `ImportMigrationPartCommand` e
`ImportDepositMigrationPartCommand` implementan `IConPerfilDeLectura` (propiedad opcional `ReadProfileVersionPublicId`). Sus rutas
no cambian; el cargue por perfil las invoca desde el trabajo de fondo con el perfil puesto. Tandas, reemplazo, cuadre, aceptación
de diferencias y activación siguen siendo los suyos. El cargue de implantación guarda la referencia al cargue propio del módulo
(la carga de migración de Cartera o de aportes) cuando lo hay.

## R11. Tablero, cortes, cifras de referencia y diferencias

**Decisión**:

- **Pasos**: catálogo fijo en código `PasosDeImplantacion` (los nueve de la US5 más «usuarios»), con el orden por omisión; cada
  cooperativa puede cambiar orden y habilitación (`COR_ImplementationStepSettings`).
- **Totales de control**: lista cerrada por paso en `TotalesDeControl` (ver data-model). Cada total sabe calcularse sobre la base
  (consulta de lectura). Los de saldo **reusan** los cuadres existentes (`CuadreDeMigracion` de Cartera, `CuadreDeCaptaciones` de
  aportes) y la suma de saldos de la apertura por `MovimientosContables` de la 009; no hay cuadre nuevo.
- **Cortes**: `COR_ImplementationCutoffs` (fecha, nombre, uno vigente). Las cifras y las diferencias son por corte.
- **Cifras de referencia**: `COR_ReferenceFigures` (corte, paso, total, valor, fuente). Se digitan o se cargan por plantilla
  `implementation.reference-figures`.
- **Diferencias aceptadas**: `COR_AcceptedDifferences`, sólo inserción, con el valor calculado, el esperado, la comparación (contra
  cifra o contra contabilidad), el motivo y quién. Una diferencia aceptada sólo vale para esos dos valores: si el total cambia,
  vuelve a mostrarse.
- **Estado del paso**: derivado, nunca guardado: pendiente (sin cargue), revisado, aplicado, cuadrado o con diferencias
  aceptadas. El aviso de orden se calcula comparando con el paso previo habilitado.

Nada de esto bloquea ninguna carga (FR-020, FR-021): el tablero lee, no gobierna.

## R12. Exportar e importar la configuración

**Decisión**: un archivo JSON versionado (`implantacion-configuracion`, `schemaVersion: 1`) con perfiles (todas sus versiones),
tablas de equivalencias, ajustes de pasos, cortes y cifras. Importar es todo o nada en una transacción, por **código** (nunca por
Id): un perfil que ya existe con la misma versión y el mismo contenido queda igual; con contenido distinto entra como versión
nueva; las versiones conservan su número de origen en `SourceVersionNumber` para que el cargue de QA y el de producción digan lo
mismo. Exportar con cifras exige el permiso de exportar datos de implantación y queda auditado. No hay cruce entre cooperativas:
viaja un archivo.

## R13. Invitaciones masivas (US7): el mismo camino de la 017

**Decisión**: plantilla `security.user-invitations`, de dos hojas («Usuarios nuevos» y «Cargos»), en el ejecutor común. La
definición usa **los encabezados del formato tal como vienen** («Tipo de documento», «Confirmar número de documento», «Código de
cargo»…). `TablaLeida.Normalizar` gana una sola regla: ignora un asterisco final (`*`). Ningún encabezado vigente lleva asterisco
(una prueba lo vigila), así que ninguna plantilla existente cambia. Hojas desconocidas («Instrucciones», «Listas») se ignoran.

Por fila:

- valida con **el mismo validador** de la invitación de la 017 (`IssueTenantInvitationCommandValidator` o el servicio
  `InvitacionConDocumento`), con la política de la cooperativa (`ADM_TenantIdentityPolicies`: tipos aceptados, intentos);
- rol: diccionario fijo `RolesDelFormato` («Administrador de la cooperativa» → `CompanyAdmin`, «Operador» → `Operator`, «Auditor»
  → `Auditor`, «Solo lectura» → `ReadOnly`, y los nombres del sistema), comparado con `Normalizar`;
- cargo: `COR_Positions.LegacyCode` vivo o fila de la hoja «Cargos», con `CodigoDeCatalogo`;
- unicidad en el archivo: correo normalizado y (tipo, número normalizado);
- «Correo ya registrado» y «ya es miembro» con la **consulta de sólo lectura** que la 017 usa al invitar; nada más se revela
  (FR-032). El resultado de una fila que sale es siempre el mismo texto de la 017.

Al aplicar hay **dos bases**: el catálogo de cargos vive en la de la cooperativa y las invitaciones en la administrativa. No hay
transacción distribuida. Orden:

1. todo se valida primero (review completo en memoria), y con un error no se escribe nada;
2. se guardan los cargos en la base de la cooperativa (crear/actualizar por código), en una transacción que todavía no se confirma;
3. se emiten las invitaciones en **una** transacción de la base administrativa, por el servicio de la 017 sin guardar por fila;
4. después de confirmar, se envían los correos (uno por invitación, por el mismo emisor de la 017); un fallo de envío se anota en
   la fila y no deshace nada.

Para que sea **todo o nada de verdad** sin transacción distribuida, el paso 2 deja la transacción de la cooperativa **abierta sin
confirmar**; el paso 3 confirma la administrativa; después se confirma la de la cooperativa. Si el paso 3 falla, la de la
cooperativa se deshace y no queda nada. Si lo que falla es la confirmación final de la cooperativa (raro: la administrativa ya
confirmó), las invitaciones recién emitidas se **cancelan** por el camino de cancelar de la 017, auditado, y el resultado lo dice
(`Import.Invitations.Compensated`); como los correos sólo salen en el paso 4, nadie recibe nada. Una identidad que la emisión
creó pendiente queda sin invitación vigente, como cuando se cancela una invitación unitaria. Está en Complexity Tracking. Permiso nuevo `Security.Users.ImportInvitations` (CompanyAdmin), que cubre también la hoja «Cargos»: hoy
`/api/core/positions` no pide un permiso propio, sólo sesión. Límite de filas por archivo: parámetro `Invitaciones.MaximoDeFilasPorArchivo` (500).

La importación de invitaciones es **síncrona** (no usa R3): son cientos de filas, no decenas de miles.

## R14. Límites configurables

**Decisión**: parámetros con vigencia por cooperativa (`COR_ParameterVersions`, `ILectorDeParametros`) con su defecto en código,
como el resto de la plataforma:

| Código | Defecto | Uso |
|---|---|---|
| `Implantacion.MaximoDeBytesPorArchivo` | 200 MB | rechazo antes de leer (FR-012) |
| `Implantacion.MaximoDeLineasPorCargue` | 500.000 | ídem |
| `Implantacion.FilasDeVistaPrevia` | 50 | vista previa |
| `Invitaciones.MaximoDeFilasPorArchivo` | 500 | US7 |

El tope de 16 MB y 60.000 filas del ejecutor común sigue para la carga **sin** perfil; con perfil manda el parámetro, porque el
archivo ya no viaja por la petición.

## R15. Pantallas

**Decisión**: módulo «Implantación» en el menú de Maestros (`/implantacion`): tablero, perfiles (lista, editor con vista previa),
equivalencias (tablas y filas), cortes y cifras, cargues (lista y detalle con avance), configuración (exportar e importar). Más
«Invitar desde archivo» en Seguridad › Usuarios y el botón «Cargar con perfil» en las pantallas de apertura, carga de Cartera y
carga de aportes, que abre el mismo componente `CargarConPerfil`. Todas con `IndicadorDeCarga` por zona; el módulo entra a
`ModulosMigrados` de `LasPantallasDicenQueEstanCargando`. Ningún texto nombra al sistema anterior («sistema anterior», «cifras de
referencia»); una prueba de arquitectura busca el nombre en `.razor`, recursos y plantillas.

## R16. Pruebas

**Decisión**: muestras **sintéticas** en `tests/.../Implantacion/Muestras/` (TXT de ancho fijo en windows-1252, CSV con «;» y
fechas `dd/MM/yyyy`, equivalencias en el formato de la contadora, libro de usuarios con la forma del formato v2). Ningún dato real.
Pruebas puras del lector (Domain/Application), de las plantillas con NSubstitute, de arquitectura (un solo lector por perfil, nadie
escribe personas fuera del camino de siempre, la invitación masiva no conoce `ADM_Invitations` salvo por el servicio de la 017) y
e2e en los dos motores **una sola vez al final** (decisión del dueño de las features anteriores): SC-004 compara el mismo contenido
por perfil y por plantilla en cada carga destino.

## Decisiones del dueño (2026-10-28)

El dueño aceptó en chat la opción recomendada de las nueve preguntas. Ya no queda ninguna abierta.

- **D1. Cuentas bancarias por persona.** Una por fila, la que hoy admite el alta unitaria. Una segunda cuenta de la misma persona
  en otra fila es llave repetida.
- **D2. «Oficina / agencia» del formato de usuarios.** Se muestra en la revisión y queda en el resultado de la fila; no se guarda
  (la 017 no tiene dónde ponerla).
- **D3. Retención del archivo de un cargue por perfil.** Se guarda hasta que el dueño ordene borrarlo, como adjunto no borrable
  por el módulo; su borrado es la receta de soporte de la 011.
- **D4. Filas de alguien que ya es miembro activo.** La fila queda «sin cambio» con el aviso «ya es miembro», no recibe
  invitación y completa su documento por el paso de la 017.
- **D5. Equivalencia «Sin equivalente (no se migra)».** Es error de la fila («la cuenta anterior X no se migra»); nunca se omite
  en silencio.
- **D6. Paso «usuarios» en el tablero.** Sí, informativo, después de equivalencias, con invitaciones emitidas y aceptadas y sin
  cifra de referencia por omisión.
- **D7. Totales de control por paso.** La lista cerrada de data-model §3; un total nuevo es una versión del producto, no un dato.
- **D8. Asociados retirados en el archivo.** Se crean ya retirados como estado histórico (sin trámite en Cartera) sólo si la
  persona no tiene fila de asociado; un vigente que el archivo marca retirado es error (`Associate.WithdrawalOwnedByLending`) y se
  retira por Cartera.
- **D9. Plantillas de inventario por perfil.** Fuera por ahora; agregarlas después es una fila del catálogo de destinos.

## D10. La 016 es una utilidad aparte: la sesión de Claude adapta y el ERP sólo gana lo que falta (2026-10-30)

El dueño aclaró en chat que la 016 «va a ser una utilidad aparte del desarrollo»: **una sesión de Claude** desde la que se
interactúa con el aplicativo, se adaptan los archivos y se sube la información. Se le ofrecieron tres alcances y dos formas de
subir, y eligió las recomendadas:

- **Sólo lo que falta en el ERP.** Lo que ninguna carga hace hoy se construye en el ERP con la mecánica común de la 012:
  la plantilla de **personas y terceros** (US1), la de **asociados** (US3) y las **invitaciones masivas** (US7). Los perfiles de
  lectura, las equivalencias, los totales de control y la traducción de errores **salen del ERP**. Pasan a un **kit de adaptación**
  versionado en `tools/implantacion/` (Python 3 + openpyxl), que Claude corre en la sesión de implantación.
  - El kit convierte cada plano en la plantilla **exacta** del ERP.
  - Deja un mapa «fila del libro → línea original» para traducir los errores de la revisión.
  - Calcula los totales de control contra las cifras de referencia.
  - Por eso **no hay tablas `COR_` nuevas, ni migración, ni cargue en segundo plano, ni tablero en el ERP**: US2, US4, US5 y US6
    se cumplen con el kit, las pantallas de cuadre que ya existen (Cartera y aportes) y un runbook.
- **El dueño sube en pantalla.** Claude entrega los `.xlsx` y su informe; el equipo los carga en las pantallas de siempre
  (Revisar → Aplicar). Claude no inicia sesión ni maneja tokens del ERP. Si la revisión trae errores, se le pasa a Claude el libro de
  revisión y el kit los traduce a la línea del plano.

Consecuencias sobre lo ya decidido:

- **R1–R7** (perfil como lector, errores de lectura, segundo plano, versiones, codificación, números, equivalencias) se cumplen en el
  kit con las mismas reglas.
  - El texto canónico es el de contracts/plantillas.md §1.3.
  - Los códigos de error son los de §1.4; el kit los informa por línea.
  - Un número que no encaja nunca se vuelve cero.
  - «No se migra» es error (D5).
  - El perfil y las equivalencias de cada cooperativa son JSON y CSV versionados en git. No llevan datos personales, así que se
    pueden revisar como código.
- Los **archivos planos y los libros generados nunca entran al repositorio**: van a una carpeta de trabajo fuera de él
  (`--trabajo`); el kit se niega a escribir dentro del repositorio.
- **R3 y D3** (el archivo se guarda como adjunto) ya no aplican: el archivo no pasa por el servidor más allá de la importación de
  siempre (16 MB, 60.000 filas). Un plano más grande lo parte el kit en varios libros (`--max-filas`).
- **R11, D6 y D7** (tablero): los totales de control salen del kit, en un informe por archivo. El cuadre contra la contabilidad se
  hace en las pantallas que ya existen (conciliación de Cartera, cuadre de captaciones, informes contables). Las diferencias
  aceptadas, con su motivo, quedan en el informe de implantación del runbook.
- **R12** (exportar e importar la configuración): la configuración **es** la carpeta del kit de la cooperativa; pasar de QA a
  producción es usar los mismos perfiles.
- **D1** (cuenta bancaria por persona): la plantilla de personas **no** lleva columnas bancarias. Las cuentas entran por la
  plantilla `plataforma.cuentas-bancarias-de-personas` de la 015, que ya existe y exige aprobación por otra persona.
- **US7** sigue igual (R13), en el ERP.
