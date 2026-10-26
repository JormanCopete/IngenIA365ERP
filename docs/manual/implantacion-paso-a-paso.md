# Implantación paso a paso: de la cooperativa registrada a la cooperativa en vivo

> Para quien implanta: el **consultor** de IngenIA 365 y el **administrador** de la cooperativa, con la **contadora** en los pasos
> contables y los jefes de cada área en los suyos. Sirve para cualquier cooperativa; los ejemplos son de COOFLOPAL (nómina quincenal,
> cartera de créditos y aportes, sin ahorro, salida en vivo el 01/12/2026).
>
> Cada paso dice **qué** se hace y **dónde**, **quién** lo hace, **qué se necesita antes** y **cómo se verifica**. El detalle de cada
> tema está en el runbook o manual que se cita al final de cada fase; aquí va sólo el orden y lo que no puede faltar.

**Quién es quién en esta guía**

| Rol | Quién es |
|---|---|
| **Maestro** | el administrador de la plataforma (IngenIA 365). Es el único que registra cooperativas y valores nacionales |
| **Consultor** | quien acompaña la implantación por IngenIA 365 |
| **Admin** | el administrador de la cooperativa (el primero lo invita el maestro) |
| **Contadora** | la contadora de la cooperativa |
| **Jefe de área** | jefe de nómina, de cartera, de captaciones, de inventario o comercial, oficial de cumplimiento, según la fase |
| **Consejo** | el órgano que aprueba con acta (consejo de administración o comité) |

**Tres reglas que aplican a toda la guía**

1. **Ninguna plantilla crea personas.** Toda persona (empleado, asociado, deudor, proveedor, entidad) se registra antes en
   Personas. Una plantilla que cita un documento que no existe se rechaza con la fila y la columna.
2. **Las plantillas son todo o nada.** Primero «Revisar» (no guarda nada y dice cada error con fila y columna), después
   «Aplicar». Un archivo con un solo error no guarda nada. Volver a subir el mismo archivo corregido no duplica.
3. **Nada se borra: se corrige con otra versión.** Parámetros, políticas, reglas contables y valores nacionales llevan vigencia;
   lo contabilizado se reversa, no se edita. Planear bien los datos que quedan bloqueados al tener movimientos (las reglas de
   cada cuenta contable, por ejemplo).

---

## Mapa de la implantación

```
 FASE 0  Preparación (decisiones, fechas de corte, equipo)
    │
 FASE 1  Alta de la cooperativa ──────────────┐
    │                                          │   FASE 4  Valores nacionales (maestro)
 FASE 2  Maestros de Core                      │   ── puede empezar el día 1, en paralelo ──
    │                                          │
 FASE 3  Contabilidad (parametrización) ◄──────┘
    │
    ├──────────────┬──────────────────┬───────────────────────┐
 FASE 5         FASE 6            FASE 9                       │
 Asociados      Nómina            Comercial (inventario,       │
    │           (necesita F4)     POS, facturación)            │
    │              │                  │                        │
    └──── FASE 7  Cartera de créditos (necesita F3, F4, F5, F6) │
              │   ■ se detiene en la activación hasta que      │
              │     existan Recaudo y Tesorería                │
          FASE 8  Captaciones y aportes (después de F7)        │
              │                                                │
          FASE 10 Ensayo en QA → paralelo → go/no-go → corte → salida en vivo → primer cierre
```

| Fase | Qué deja lista | Necesita antes | Duración de referencia (COOFLOPAL) |
|---|---|---|---|
| 0 | decisiones, fechas, equipo y levantamiento | — | 1 semana |
| 1 | la cooperativa, su administrador con segundo factor, oficinas, usuarios y roles | 0 | 1–2 días |
| 2 | personas, empresa, sucursales contables, bancos, centros de costo, impuestos, formatos | 1 | 1–3 semanas (personas es lo largo) |
| 3 | plan de cuentas, tipos de comprobante, períodos, ensayo de la apertura | 2 | 5 semanas con la contadora |
| 4 | salario mínimo, UVT, festivos, usura, UVR, topes y anexos replicados; perfil regulatorio | 1 (en paralelo) | continuo; antes de 6 y 7 |
| 5 | asociados con su grupo | 2 | en paralelo con 6 |
| 6 | nómina parametrizada, empleados, saldos de prestaciones, primer período | 2, 3, 4 | 2 semanas + 4 ciclos en paralelo |
| 7 | líneas, matriz contable, gobierno, carga de cartera aplicada y cuadrada | 3, 4, 5, 6 | 2–3 semanas + paralelo |
| 8 | líneas de aportes y captación, carga aplicada | 7 activada | 1–2 semanas |
| 9 | catálogo, bodegas, matriz, POS, facturación | 2, 3 | 5 semanas + paralelo |
| 10 | ensayo, paralelo, corte, salida, primer cierre | todas | paralelo de 4 semanas; corte y cargue en 1–4 días hábiles |

**El orden que no se puede romper el día del corte:** balance del sistema anterior al corte → apertura contable al corte →
saldo inicial de inventario por bodega → carga de cartera al corte → cuadre y acta → activación de créditos el día siguiente al
corte → aportes y captaciones. Ver Fase 10.

---

## Fase 0. Preparación

| # | Qué y dónde | Quién | Antes | Cómo se verifica |
|---|---|---|---|---|
| 0.1 | Designar gestor del proyecto y dos personas por módulo | Gerencia | — | nombres en el plan |
| 0.2 | Fijar la **fecha de corte** (último día en el sistema anterior) y la **fecha de salida** (el día siguiente) por módulo | Gerencia, consultor | 0.1 | fechas en el plan; en COOFLOPAL, corte 30/11 y salida 01/12 |
| 0.3 | Decidir los módulos que entran (¿capta ahorro o sólo aportes? ¿hay POS? ¿nómina quincenal o mensual?) | Gerencia, consultor | 0.1 | decisión escrita |
| 0.4 | Decidir catálogo contable: **CUIF solidario** (vigiladas por la Supersolidaria) o **PUC comercial** | Contadora | 0.3 | decisión escrita |
| 0.5 | Levantar los datos del sistema anterior: personas, plan de cuentas, conceptos de nómina, líneas de crédito, productos, bodegas | Jefes de área | 0.3 | archivos de trabajo por módulo |
| 0.6 | Hacer el **mapa de equivalencias** (cuenta anterior → nueva, concepto anterior → nuevo, línea anterior → nueva) | Contadora, jefes de área | 0.5 | mapa revisado por la contadora |

> **Hoy los datos del sistema anterior entran por las plantillas de importación** que cita cada fase. El cargue directo de los
> archivos planos del sistema anterior (perfiles de lectura y equivalencias) está en especificación y será el mecanismo futuro; ver
> «Lo que todavía no está disponible».

---

## Fase 1. Alta de la cooperativa

| # | Qué y dónde | Quién | Antes | Cómo se verifica |
|---|---|---|---|---|
| 1.1 | Registrar la cooperativa: **Consola SaaS › Registrar Cooperativa** (`/saas/register-tenant`) con razón social, nombre, NIT, nombre interno, correo de contacto y correo del primer administrador | Maestro | NIT y correo del admin | la cooperativa aparece en **Consola SaaS › Cooperativas** (`/admin/tenants`) como aprovisionada; un NIT repetido se rechaza |
| 1.2 | Aceptar la invitación que llega al correo, fijar la contraseña e **inscribir el segundo factor** (app de códigos o llave de acceso) | Admin | 1.1 | el admin entra y ve su cooperativa; **Mi Cuenta › Mis Autenticadores** muestra su factor |
| 1.3 | Inscribir un **segundo factor de respaldo** (otra llave o la app en otro equipo) y guardar los códigos de recuperación | Admin | 1.2 | dos credenciales en **Mis Autenticadores** |
| 1.4 | Definir la **política de segundo factor**: **Mi Cooperativa › Política MFA** (si se exige y qué métodos acepta; si se permite recuperar por correo y con qué demora) | Admin | 1.2 | la política se ve guardada; la recuperación por correo queda apagada salvo decisión expresa |
| 1.5 | Crear las **oficinas**: **Administración › Sucursales** (`/admin/branches`) | Admin | 1.2 | todas las oficinas físicas listadas |
| 1.6 | Revisar los **roles** y crear los propios: **Administración › Roles y Permisos** (`/security/roles`). Hay cuatro roles base (administrador, operador, auditor, sólo lectura); Inventario, Cartera y Captaciones traen perfiles sugeridos que se crean desde sus pantallas (fases 7, 8, 9) | Admin, consultor | 1.2 | cada rol con sus permisos; el operador digita pero no contabiliza |
| 1.7 | Invitar a los usuarios: **Mi Cooperativa › Miembros** («Invitar miembro»); cada uno acepta e inscribe su segundo factor. Asignar roles en **Administración › Usuarios** (`/security/users`) | Admin | 1.6 | todos en estado «Activo» con su rol |
| 1.8 | Nombrar un **segundo administrador** (Miembros › «Promover») | Admin | 1.7 | dos administradores activos: la cooperativa no queda sin administrador si uno pierde su acceso |

> **Cooperativas aprovisionadas antes de las features nuevas**: el retiro de lo heredado (inventario, cartera, captaciones) lo hace
> IngenIA 365 con diagnóstico, respaldo y segundo revisor (runbooks de retiro heredado). **Una cooperativa registrada hoy no tiene
> nada heredado**: no hay que hacer nada.

Referencias: `docs/operaciones/manual-funcional-usuario-final.md` §9–§10.

---

## Fase 2. Maestros de Core

| # | Qué y dónde | Quién | Antes | Cómo se verifica |
|---|---|---|---|---|
| 2.1 | **Empresa**: **Maestros Core › Empresas** (`/maestros/empresas`) con NIT, dígito de verificación y razón social | Admin | Fase 1 | la empresa aparece; la usan la PILA, la dispersión y los documentos |
| 2.2 | **Sucursales contables**: **Maestros Core › Agencias** (`/maestros/agencias`). Crear al menos la principal, con su municipio DANE, y **vincular cada una a su oficina** (columna «Oficina») | Admin | 1.5 | ninguna agencia «Sin vincular»; una oficina sólo se vincula a una agencia |
| 2.3 | Países, departamentos y **ciudades** (`/maestros/ciudades`): revisar que estén los municipios donde operan | Admin | — | los municipios de oficinas, empleados y asociados existen |
| 2.4 | **Bancos** (`/maestros/bancos`) con su **código ACH** (código de transferencia) | Admin, tesorero | — | ningún banco usado por empleados o pagos sin código |
| 2.5 | **Centros de costo contables** (`/maestros/centros-costo`), con el código de la cooperativa | Contadora | — | los centros del mapa de 0.6 creados; sirven a Contabilidad, Nómina y Comercial (no a los asociados) |
| 2.6 | **Grupos del asociado** (`/maestros/grupos-del-asociado`): la clasificación libre de los asociados (zona, empresa, gremio) | Admin, jefe de cartera | — | grupos creados; no hay plantilla, se digitan |
| 2.7 | Catálogos auxiliares que usen: cargos, profesiones, parentescos, motivos de retiro, secciones, comités, convenios (**Maestros Core**) | Admin | — | listas completas para las fichas |
| 2.8 | **Personas**: **Maestros Core › Personas** (`/maestros/personas`). Registrar empleados, asociados, deudores, codeudores, proveedores, clientes, y **las entidades** (EPS, fondos, ARL, cajas, bancos, aseguradoras, pagadurías) como personas | Admin, jefes de área | 2.3 | cada documento del levantamiento existe una sola vez; documento con la longitud correcta; segundo apellido completo |
| 2.9 | **Impuestos y retenciones** (`/maestros/impuestos`): el catálogo tributario viene sembrado; ajustar o cargar con su plantilla (impuestos, retenciones y conceptos) | Contadora | — | la contadora valida tarifas y conceptos |
| 2.10 | **Formatos bancarios** (`/maestros/formatos-bancarios`): el genérico viene sembrado; el del banco pagador se carga campo a campo o pegando su estructura, ligado al banco, con vigencia | Tesorero, consultor | 2.4 | vista previa del archivo con una corrida real (Fase 6) |
| 2.11 | **Parámetros del sistema** (`/admin/parametros`): revisar los que trae cada cooperativa | Admin, consultor | — | revisados y anotados |
| 2.12 | **Medios de pago** (`/maestros/medios-de-pago`) y **resoluciones DIAN** (`/maestros/resoluciones-dian`): sólo si habrá ventas (Fase 9) | Contadora, jefe comercial | 2.9 | ver Fase 9 |

> Personas es el paso más largo y del que depende todo lo demás. En COOFLOPAL se trabajó en paralelo con Contabilidad durante tres
> semanas. Una persona eliminada no se vuelve a crear: se **restaura** desde Personas.

Referencias: `docs/manual/alta-de-persona-desde-modulos.md`, `docs/manual/grupos-del-asociado.md`, `docs/manual/dispersion-bancaria.md`.

---

## Fase 3. Contabilidad

| # | Qué y dónde | Quién | Antes | Cómo se verifica |
|---|---|---|---|---|
| 3.1 | **Validar el catálogo**: **Contabilidad › Catálogos PUC** (`/contabilidad/catalogos`) | Contadora | 0.4 | el catálogo elegido queda marcado como validado |
| 3.2 | **Iniciar la contabilidad**: **Contabilidad › Configuración inicial** (`/contabilidad/configuracion`): catálogo, nivel de movimiento (5 o 6), grupo NIIF, primer ejercicio, sucursal principal, cuatro ojos (sí/no), tolerancias | Contadora, admin | 2.2, 3.1 | el ejercicio aparece con sus doce meses abiertos. **Catálogo y nivel sólo cambian mientras no haya auxiliares** |
| 3.3 | **Auxiliares y cuentas propias en masa**: **Contabilidad › Plan de cuentas** (`/contabilidad/plan-de-cuentas`), «Plantilla Excel» → diligenciar → «Importar cuentas». Donde el catálogo no trae subcuentas (reservas, fondos sociales, provisiones, excedentes) se crean **cuentas propias** en el mismo archivo | Contadora | 3.2, 0.6 | la respuesta dice creadas / actualizadas / sin cambio; ningún error |
| 3.4 | Fijar **las reglas de cada auxiliar** (a qué módulos aplica; si exige tercero, documento cruce, centro de costo, sucursal; si es bancaria o de impuesto) **antes del primer movimiento** | Contadora | 3.3 | revisado cuenta por cuenta; con movimientos sólo cambia el nombre |
| 3.5 | La **cuenta de resultado del ejercicio** (por ejemplo bajo excedentes) en Configuración inicial | Contadora | 3.3 | definida; sin ella no se cierra el año |
| 3.6 | **Tipos de comprobante**: **Contabilidad › Tipos de comprobante** (`/contabilidad/tipos-de-comprobante`); vienen sembrados (manual, nómina, apertura, cierre, cartera…) | Contadora | 3.2 | revisados; consecutivo inicial correcto |
| 3.7 | **Períodos**: **Contabilidad › Períodos** (`/contabilidad/periodos`); el primer ejercicio queda abierto. Si la salida cae en un año nuevo, abrir el siguiente | Contadora | 3.2 | el mes de la salida y el del corte abiertos |
| 3.8 | **Ensayo de la apertura**: **Contabilidad › Saldos de apertura** (`/contabilidad/apertura`), descargar la plantilla, cargar un balance de prueba del sistema anterior y revisar. Mientras sea borrador se corrige, se cambia la fecha o se vuelve a importar | Contadora | 3.3, 2.8 | importa sin errores; **la apertura real se hace al corte** (paso 10.6) |
| 3.9 | **Patrimonio técnico**: **Contabilidad › Indicadores regulatorios** (`/contabilidad/indicadores-regulatorios`) | Contadora | 3.2 | vigente; sin él Cartera no desembolsa ni da cupos |
| 3.10 | **Modo de paso de los módulos** (cómo llega cada operación al libro: **en línea**, **por lotes** o **no pasa**). Inventario lo fija por tipo de documento; Cartera y Captaciones por tipo de hecho en sus parámetros. **Iniciar la contabilidad antes de operar los módulos**: sin contabilidad iniciada todo queda en «no pasa» y luego hay que enviarlo | Contadora, consultor | 3.2 | decisión por tipo anotada; en las fases 7–9 se completa la matriz de cada módulo |
| 3.11 | **Parametrizaciones inválidas**: **Contabilidad › Parametrizaciones inválidas** debe quedar vacía | Contadora | 6.4, 7.2, 9.4 | lista vacía |
| 3.12 | **Comprobante de prueba** por tipo (digitar, contabilizar, reversar) en QA | Contadora | 3.6 | aparece en el balance de prueba y en el libro auxiliar |

Referencias: `docs/operaciones/contabilidad-primer-ejercicio.md`, `docs/manual/contabilidad-contrato-de-contabilizacion.md`.

---

## Fase 4. Valores nacionales y perfil regulatorio

Los valores que fija la ley o publica una autoridad son iguales para todas las cooperativas. Los registra **una vez** el maestro en
la base central y se **replican solos** a cada cooperativa. La cooperativa no los modifica: los revisa.

| # | Qué y dónde | Quién | Antes | Cómo se verifica |
|---|---|---|---|---|
| 4.1 | Ver «lo que falta» arriba en **Administración › Valores nacionales** (`/admin/valores-nacionales`, sección «Registro central») | Maestro | — | la lista de faltantes de los próximos 31 días |
| 4.2 | Registrar **salario mínimo, UVT y festivos** del año (y del siguiente desde diciembre), con la fuente | Maestro | 4.1 | Nómina los recibe solos; **Nómina no los registra** |
| 4.3 | Registrar la **usura por modalidad** (cada mes), IBR, IPC, los **límites legales** y los documentos normativos (catálogo de modalidades, tipos de organización, Anexo 1, Anexo 2) | Maestro | 4.1 | sin faltantes para las modalidades de las líneas que se usarán |
| 4.4 | Si habrá vivienda: el **tope de tasa de vivienda** y la **UVR día a día** (el período del 16 al 15 de una vez) | Maestro | 4.1 | la UVR de los próximos siete días cargada |
| 4.5 | Si habrá captaciones: componente inflacionario, límite inembargable, tope de entrega sin sucesión, seguro de depósitos, límites de aportes | Maestro | 4.1 | sin faltantes de captaciones |
| 4.6 | Para varios valores, la plantilla de valores nacionales; para llevar lo registrado de un ambiente a otro, «Exportar paquete» e importarlo | Maestro | — | la plantilla revisa antes de aplicar |
| 4.7 | **Revisar** la copia en la cooperativa: «Conforme» u «Objetado» con nota | Contadora | réplica | ningún valor que se use queda «Pendiente de validar» |
| 4.8 | **Perfil regulatorio** de la cooperativa: **Administración › Perfil regulatorio** (`/admin/perfil-regulatorio`): tipo de organización, nivel de supervisión, si capta ahorro, si está obligada a la pérdida esperada | Admin, contadora | 4.3 | perfil vigente |

> En DEV y QA **nunca se registran valores inventados**: la base central es una por ambiente y todas sus cooperativas los copian.

Referencias: `docs/operaciones/valores-nacionales.md`.

---

## Fase 5. Asociados

| # | Qué y dónde | Quién | Antes | Cómo se verifica |
|---|---|---|---|---|
| 5.1 | Registrar asociados: **Asociados › Registro** (`/asociados/registro`). Con persona nueva, persona y asociado se registran **en un solo paso**; con persona existente, se elige y sus datos se ven de sólo lectura («Editar datos de la persona» los corrige en un solo sitio) | Jefe de cartera | 2.6, 2.8 | el asociado aparece con fecha de ingreso y **grupo del asociado** |
| 5.2 | Completar el **100 % de los asociados** vigentes al corte | Jefe de cartera | 5.1 | el conteo cuadra con el del sistema anterior |

> Una cuenta de aportes sólo puede ser de un **asociado vigente al corte**. Los asociados son requisito de las fases 7 y 8.

Referencias: `docs/manual/alta-de-persona-desde-modulos.md`, `docs/manual/grupos-del-asociado.md`.

---

## Fase 6. Nómina

| # | Qué y dónde | Quién | Antes | Cómo se verifica |
|---|---|---|---|---|
| 6.1 | **Plan de nómina**: **Nómina › Planes de nómina** (`/nomina/planes`): periodicidad (mensual, quincenal, decadal, semanal). Cambia sólo mientras el plan no tenga períodos | Jefe de nómina | Fase 1 | el plan por defecto con la periodicidad decidida |
| 6.2 | **Parámetros legales** con vigencia: **Nómina › Parámetros legales** (`/nomina/parametros-legales`); la semilla trae los del año; salario mínimo, UVT y festivos llegan de la Fase 4. Una tabla de retención propia del plan, en **Retención Fuente** | Jefe de nómina, contadora | 4.2 | la pantalla no lista parámetros faltantes este año |
| 6.3 | **Entidades**: EPS, ARL, pensiones, cesantías, cajas (**Nómina › …**), con su **código PILA** y la **persona** que la representa como tercero; clases de riesgo ARL (vienen sembradas) | Jefe de nómina | 2.8 | ninguna entidad usada sin persona ni código PILA |
| 6.4 | **Conceptos y sus cuentas**: **Nómina › Conceptos** (`/nomina/conceptos`): revisar los sembrados, crear los propios y, en «Cuentas», débito y crédito de cada concepto (y por centro de costo si aplica), incluidos los de prima, cesantías, vacaciones y definitiva y los descuentos de Cartera y aportes | Contadora, jefe de nómina | 3.3, 3.4 | ningún concepto sin cuentas; Parametrizaciones inválidas vacía |
| 6.5 | **Políticas de la empresa**: **Nómina › Políticas de la empresa** (`/nomina/politicas`), con vigencia; la **fecha de arranque de la nómina** no tiene defecto y es obligatoria | Jefe de nómina | — | las once políticas con valor; la fecha de arranque registrada |
| 6.6 | **Festivos** (`/nomina/festivos`): los nacionales llegan de la Fase 4; agregar los propios | Jefe de nómina | 4.2 | el año a liquidar tiene sus festivos |
| 6.7 | **Empleados**: **Nómina › Empleados** (`/nomina/empleados`). Con persona nueva, persona y empleado en un paso. Cada ficha: plan, salario, fecha de ingreso, **centro de costo contable**, EPS, pensión, **clase de riesgo ARL**, fondo de cesantías, caja, datos PILA, banco y cuenta de dispersión | Jefe de nómina | 2.5, 6.3 | ninguna ficha con afiliaciones en blanco. Si una cuenta de concepto exige centro de costo, todo empleado lo necesita |
| 6.8 | **Saldos iniciales de prestaciones** a la víspera del arranque (vacaciones pendientes, cesantías, intereses, prima): **Nómina › Saldos iniciales** (`/nomina/saldos-iniciales`) | Jefe de nómina, contadora | 6.5, 6.7 | la lista de «sin saldo» vacía, validada por la contadora |
| 6.9 | **Primer período**: **Nómina › Periodos Pago** (`/nomina/periodos-pago`), y el **mes contable abierto** en Contabilidad | Jefe de nómina | 6.1, 3.7 | el período existe con su mes de imputación |
| 6.10 | **Novedades** fijas y recurrentes: **Nómina › Novedades** | Jefe de nómina | 6.7 | la recurrente aparece al calcular, no al registrarla |
| 6.11 | **Primera liquidación en QA**: **Nómina › Liquidacion**, calcular, revisar y aprobar; el comprobante de nómina sale en Contabilidad | Jefe de nómina, contadora | 6.4–6.10 | el comprobante cuadra; la contadora lo verifica |
| 6.12 | **PILA**: **Nómina › Planilla PILA** (`/nomina/pila`) › «Datos del aportante» (clase, forma de presentación, ARL, operador, municipio, actividad). Validar, generar, revisar el cuadre y cargar el archivo en el operador | Jefe de nómina | 2.1, 6.3, 6.11 | el validador del operador acepta el archivo; marcar «cargada» con el número de planilla |
| 6.13 | **Dispersión**: **Nómina › Dispersion bancaria** (`/nomina/dispersion`): formato del banco (2.10), cuenta bancaria del plan (auxiliar con banco y número), generar y marcar enviado | Jefe de nómina, tesorero | 2.4, 2.10, 3.3 | ningún empleado en «pendientes»; el banco acepta el archivo |

> El cálculo **no trae ningún valor legal fijo**: si falta un parámetro para la fecha del período, lo dice y no calcula. Se registra
> la vigencia y se vuelve a calcular.

Referencias: `docs/operaciones/nomina-primer-periodo.md`, `docs/operaciones/pila-primera-planilla.md`,
`docs/manual/nomina-centro-de-costo-del-empleado.md`, `docs/manual/liquidaciones-especiales.md`, `docs/manual/dispersion-bancaria.md`.

---

## Fase 7. Cartera de créditos

La cartera se trae **con fecha de corte** en una carga por partes. Aplicar la carga sólo la guarda y la cuadra; los créditos nacen
al **activar**, el día siguiente al corte, todo o nada.

| # | Qué y dónde | Quién | Antes | Cómo se verifica |
|---|---|---|---|---|
| 7.1 | **Líneas de crédito**: **Cartera › Líneas de crédito** (`/cartera/lineas`). **Con un corte en el pasado, cargarlas con la plantilla de líneas** («Importar») con vigencia desde el desembolso más antiguo; la pantalla fecha toda versión desde hoy | Jefe de cartera | 4.3 (usura de la modalidad) | cada línea activa con versión vigente al corte y su concepto tributario si es comercial |
| 7.2 | **Matriz contable de Cartera**: **Contabilidad › Cartera › Matriz contable** (con su plantilla) y **Completitud de la matriz** | Contadora | 3.3, 7.1 | la completitud **vacía**; incluida la regla del recaudo por nómina |
| 7.3 | Decidir qué pasa si falta una regla contable (**Cartera › Parámetros**, `/cartera/parametros`): por defecto **bloquea**; «advertir» deja operar y avisa | Contadora | 7.2 | el valor vigente anotado |
| 7.4 | **Gobierno**: **Cartera › Actas y órganos** (`/cartera/actas`) con sus miembros; **Atribuciones**; **Personas vinculadas** | Jefe de cartera, admin | 2.8 | el consejo existe y puede cerrar sesiones con puntos |
| 7.5 | **Roles sugeridos de Cartera** (desde Activación) y **alcance** de cada usuario por oficina y línea: **Cartera › Alcance de usuarios**. Cada usuario que carga, acepta o decide necesita su **persona** (Administración › Usuarios) | Admin | 1.7, 2.8 | la pantalla de activación no lista usuarios sin alcance |
| 7.6 | **Plantillas de mensaje** al deudor y **etapas de cobranza** (`/cartera/plantillas-de-mensaje`, `/cartera/etapas-de-cobranza`) | Jefe de cartera, oficial de cumplimiento | — | validadas |
| 7.7 | Si hay libranza: **Pagadurías** (`/cartera/pagadurias`) con sus formatos de ida y vuelta | Jefe de cartera | 2.10 | convenios vigentes |
| 7.8 | **Listas restrictivas**: **Cumplimiento › Listas restrictivas** (`/compliance/listas-restrictivas`) con su plantilla | Oficial de cumplimiento | — | lista cargada |
| 7.9 | **Descuento de cuotas por nómina**: cuentas del concepto de descuento de Cartera en Nómina | Contadora | 6.4 | la activación ya no lo avisa |
| 7.10 | **Carga de migración**: **Cartera › Migración** (`/cartera/migracion`) › «Carga nueva» con la fecha de corte. Por parte: plantilla vacía → diligenciar → Revisar → Aplicar. Obligatorias: créditos, cuotas, saldos, mora/calificación/deterioro | Jefe de cartera | 5.2, 7.1, apertura al corte | cada parte aplicada sin errores; «Aplicar la carga» sin errores entre partes |
| 7.11 | **Cuadre** contra Contabilidad y conciliación de ventas a crédito (pestañas «Cuadre» y «Ventas»); **aceptar diferencias** con el punto de un acta cerrada (quien cargó no acepta) | Contadora, jefe de cartera, consejo | 7.10 | cuadre en cero o con todas las diferencias aceptadas por acta |
| 7.12 | **Historial de mora**: si el perfil obliga a la pérdida esperada, completar los meses que falten con la plantilla de historial de mora | Jefe de cartera | 4.8 | la activación no pide historial |
| 7.13 | **Requisitos de activación**: **Cartera › Activación** (`/cartera/activacion`) lista cada uno con quién lo corrige: valores nacionales y Anexo 1, usura de las líneas, oficinas vinculadas, roles, matriz, recaudo por nómina, patrimonio técnico, **un recaudador y una tesorería reales** registrados en **Administración › Participantes de la integración** | Consultor | 7.1–7.12 | **■ PARADA**: hoy el recaudador y la tesorería reales no existen en producción (ver «Lo que todavía no está disponible») |
| 7.14 | **Activar** el día siguiente al corte, dentro del plazo, con el **acta de activación** | Jefe de cartera, consejo | 7.13 completo | créditos nacidos con su número de origen; la liquidación de una muestra al corte es lo cargado |

> En DEV y QA el recaudador y la tesorería pueden ser los **dobles de prueba** (`/cartera/dobles-de-prueba`): así se ensaya todo
> el recorrido. En producción no valen.

Referencias: `docs/operaciones/cartera-puesta-en-marcha.md`, `docs/manual/cartera.md`, `docs/manual/cartera-integracion-contable.md`,
`docs/manual/cartera-lineas-de-credito.md`, `docs/manual/cartera-perdida-esperada.md`.

---

## Fase 8. Captaciones y aportes

Hay dos naturalezas que se activan **por separado**: **aportes** y **captaciones** (ahorro a la vista, CDAT, contractual y
permanente, juntos). Una cooperativa que no capta ahorro (como COOFLOPAL) activa sólo aportes. **Los créditos se activan primero.**

| # | Qué y dónde | Quién | Antes | Cómo se verifica |
|---|---|---|---|---|
| 8.1 | **Parámetros** de captaciones en **Cartera › Parámetros**: retención sobre rendimientos, GMF, topes de aportes, retiro del asociado, plazo de activación | Contadora, jefe de captaciones | 4.5 | cada uno con el valor de la cooperativa, no «pendiente de validar» |
| 8.2 | **Líneas**: **Captaciones y aportes › Líneas de aportes** y **Líneas de captación**. **Crearlas a más tardar el día del corte**: no admiten vigencia anterior a hoy y no tienen plantilla. Una sola línea de aportes activa | Jefe de captaciones | 4.8 (perfil que autorice captar) | cada línea con versión vigente al corte |
| 8.3 | **Matriz contable** de las líneas (la misma matriz de Cartera, con las cuentas sugeridas del CUIF) | Contadora | 7.2, 8.2 | completitud vacía |
| 8.4 | Cuentas de los conceptos de descuento de aportes y ahorros en Nómina | Contadora | 6.4 | sin aviso en la activación |
| 8.5 | **Roles sugeridos** de captaciones (jefe, asesor, analista de aportes, tesorero) y alcance de cada asesor; asignar aparte los permisos de activar y aceptar diferencias | Admin | 7.5 | roles creados |
| 8.6 | **Carga de migración**: **Captaciones y aportes › Saldos iniciales** (`/cartera/captaciones/migracion`) › «Carga nueva» con la naturaleza y el corte. Aportes: cuentas de aportes por asociado; captaciones: cuentas, partes, CDAT, embargos, exención de GMF, acumulado tributario | Jefe de captaciones | 5.2, 8.2, apertura al corte | partes aplicadas; la carga aplicada sin errores |
| 8.7 | **Cuadre** y **cifras de referencia** por línea y concepto; aceptar diferencias con acta | Contadora, consejo | 8.6 | cuadre en cero o aceptado |
| 8.8 | **Activar** cada naturaleza: **Captaciones y aportes › Activación**. Con carga, el día siguiente al corte y con acta; sin historia, desde la fecha elegida | Jefe de captaciones | 7.14, 8.7 | **■ PARADA**: igual que 7.13, exige recaudador con el contrato de cuentas y tesorería reales |

Referencias: `docs/operaciones/captaciones-puesta-en-marcha.md`, `docs/manual/cartera-captaciones.md`,
`docs/manual/cartera-captaciones-lineas-y-apertura.md`, `docs/manual/cartera-captaciones-integracion-contable.md`.

---

## Fase 9. Comercial: inventario, compras, ventas, POS y facturación

Cada bodega entra **sola**, con su propia fecha de corte. Mientras una bodega no se activa, la sigue llevando el sistema anterior.

| # | Qué y dónde | Quién | Antes | Cómo se verifica |
|---|---|---|---|---|
| 9.1 | **Plantillas en orden** en **Inventario › Plantillas** (`/inventario/plantillas`): impuestos, grupos contables, unidades, marcas, categorías, productos (con códigos de barras y conversiones), bodegas y ubicaciones, tipos de documento, vendedores | Jefe comercial | 2.8, 2.9 | cada plantilla revisada y aplicada; «con datos» devuelve lo cargado para corregir |
| 9.2 | Revisar **bodegas** (`/inventario/bodegas`): nacen sin activar, con su ubicación general; la primera de cada sucursal trae su bodega de tránsito | Jefe de inventario | 9.1 | todas las bodegas físicas creadas |
| 9.3 | **Tipos de documento** (`/inventario/tipos-de-documento`): prefijo, consecutivo, aprobación y **modo de paso** | Jefe comercial, contadora | 3.10 | uno por clase de documento que se usará |
| 9.4 | **Matriz contable de Inventario**: **Contabilidad › Inventario › Matriz contable**, tipos de comprobante y **Completitud** | Contadora | 3.3, 9.1 | completitud vacía |
| 9.5 | **Parámetros**, **políticas de aprobación** y **alcance comercial** de cada usuario (`/inventario/parametros`, `/inventario/politicas-de-aprobacion`, `/inventario/alcances`); perfiles sugeridos desde Roles | Admin, jefe comercial | 1.6 | cada usuario ve sólo sus bodegas |
| 9.6 | **Ventas**: canales, vendedores, **puntos de venta y cajas**, **listas de precios**, **topes de descuento**, promociones (**Ventas › …**) | Jefe comercial | 9.1 | un punto con su caja y su lista vigente |
| 9.7 | **Medios de pago** (`/maestros/medios-de-pago`: medios, franquicias, adquirentes, datáfonos, denominaciones) y su cuenta en la matriz | Contadora, jefe comercial | 9.4 | cada medio con cuenta; códigos DIAN validados por la contadora |
| 9.8 | **Facturación electrónica**: **Administración › Facturación electrónica** (`/admin/facturacion-electronica`): modo, canal y ambiente con vigencia; **resoluciones DIAN** (factura, documento equivalente POS, documento soporte y las de contingencia) asociadas al canal | Contadora, consultor | 2.12 | la pantalla de preparación sin faltantes. **Hoy sólo existe el canal simulado** (ver «Lo que todavía no está disponible») |
| 9.9 | **Ensayo del POS** (`/pos`): abrir sesión de caja, vender con lector, pago mixto, cerrar el día | Cajeros | 9.6–9.8 | el cierre del día cuadra |
| 9.10 | Por cada bodega, al corte: **conteo físico** en el piso → **cifras de referencia** (`/inventario/cifras-de-referencia`) de todas las bodegas → **saldo inicial** (`/inventario/saldo-inicial`, cantidad contada en unidad base y costo, fecha = víspera de su activación) | Jefe de inventario | 9.2, 3.8 | borrador sin errores; la revisión da el valor por grupo contable |
| 9.11 | **Aprobar** el saldo inicial (**Inventario › Aprobaciones**; otra persona, no quien cargó) | Jefe de inventario | 9.10 | el kardex muestra las entradas en la fecha de corte |
| 9.12 | **Comparar** con las cifras de referencia (**Inventario › Informes**: comparativo de valorizado y de kardex) y explicar cada diferencia por escrito | Jefe de inventario | 9.11 | ninguna diferencia sin causa aprobada |
| 9.13 | **Activar la bodega**: **Inventario › Activación de bodegas** (`/inventario/activacion`), con la comparación contra Contabilidad | Jefe de inventario, contadora | 9.12, apertura contable al corte | la bodega queda activa; desde ahí sólo se opera en el ERP |
| 9.14 | Si hay ventas a crédito de asociados con Cartera activa: fijar en los parámetros de Inventario la fecha desde la que se integra con Cartera y quién registra la cuenta por cobrar, el mismo día de la activación de créditos | Jefe comercial | 7.14 | ventas a crédito nacen como obligación en Cartera |

Referencias: `docs/operaciones/inventario-puesta-en-marcha.md`, `docs/manual/plantillas-de-importacion.md`,
`docs/manual/inventario-documentos-y-kardex.md`, `docs/manual/cartera-ventas-a-credito.md`.

---

## Fase 10. Ensayo, paralelo, salida en vivo y primer cierre

| # | Qué y dónde | Quién | Antes | Cómo se verifica |
|---|---|---|---|---|
| 10.1 | **Ensayo general en QA**: una cooperativa de QA con la parametrización real, una apertura a un corte pasado y la carga real a ese corte; recorrer todo hasta el **primer cierre mensual con la conciliación en cero** (con los dobles de prueba de Recaudo y Tesorería) | Consultor, todos los jefes | fases 1–9 en QA | acta del ensayo |
| 10.2 | **Paralelo**: una muestra de las transacciones importantes y de todos los casos de la entidad, en el ERP y en el sistema anterior; comparar y registrar diferencias en cada ciclo. En COOFLOPAL: cuatro quincenas de nómina, dos ciclos de contabilidad, comercial y cartera | Jefes de área | 10.1 | cada diferencia cerrada con ajuste o justificación |
| 10.3 | **Comparar con cifras de referencia** una muestra por línea de crédito (saldo por concepto, cuota, días de mora, calificación) y el kardex y valorizado por bodega | Jefe de cartera, jefe de inventario | 10.2 | diferencias explicadas y aprobadas |
| 10.4 | **Respaldo del sistema anterior y plan de reversa** | Consultor, sistemas | — | respaldo verificado |
| 10.5 | **Go/no-go** (lista de abajo) | Gerencia, consultor, contadora | 10.1–10.4 | todos los puntos en «sí» |
| 10.6 | **Al corte**: balance de prueba del sistema anterior → **apertura contable** con fecha del corte (`/contabilidad/apertura`), revisada y contabilizada (cuatro ojos si aplica) | Contadora | 10.5 | el balance del mes siguiente muestra la apertura como saldo inicial; el estado de cuenta del tercero muestra sus pendientes |
| 10.7 | **Al corte**: saldo inicial, aprobación y activación de cada bodega (9.10–9.13) | Jefe de inventario | 10.6 | bodegas activas |
| 10.8 | **Al corte**: carga de cartera aplicada, cuadre aceptado y **activación de créditos** al día siguiente (7.10–7.14); después aportes y captaciones (8.6–8.8) | Jefe de cartera, jefe de captaciones, consejo | 10.6; **Recaudo y Tesorería** | ver «■ PARADA» |
| 10.9 | **Salida en vivo**: desde la fecha de salida, la operación de cada módulo activo se registra **sólo** en el ERP; la nómina del primer período se liquida sólo aquí | Todos | 10.6–10.8 | el sistema anterior queda de consulta |
| 10.10 | **Primera semana**: acompañamiento; revisar a diario las bandejas de mensajes (sin rechazos sin explicar), las corridas diarias de Cartera y la conciliación contable | Consultor, jefes de área | 10.9 | incidencias resueltas o con plan |
| 10.11 | **Primer cierre de mes**: cerrar Inventario y Cartera (Cartera cierra contra el cierre base), luego Contabilidad (no cierra el mes mientras Cartera activa no lo haya cerrado ni con borradores fechados en el mes) | Contadora, jefes de área | 10.10 | conciliaciones en cero; balance y estados financieros revisados por la contadora |

### Lista de comprobación de salida en vivo (go/no-go)

Cada punto en «sí» o el módulo no sale. Marcar sólo los módulos que entran.

**Plataforma y seguridad**
- [ ] Cooperativa aprovisionada, dos administradores, todos los usuarios con segundo factor y su rol.
- [ ] Política de segundo factor definida; usuarios que deciden con persona y alcance.
- [ ] Valores nacionales al día y revisados por la contadora; perfil regulatorio vigente.

**Contabilidad**
- [ ] Catálogo validado por la contadora; contabilidad iniciada; auxiliares con sus reglas.
- [ ] Parametrizaciones inválidas vacía; completitud de cada matriz (Inventario, Cartera) vacía.
- [ ] Mes del corte y mes de la salida abiertos; cuenta de resultado definida.
- [ ] Balance de prueba del sistema anterior al corte listo para la apertura.

**Nómina**
- [ ] Ningún concepto sin cuentas; ningún empleado con afiliaciones, clase ARL o centro de costo faltante.
- [ ] Saldos iniciales de prestaciones validados; fecha de arranque registrada.
- [ ] Paralelo cuadrado; un comprobante de nómina verificado por la contadora.
- [ ] Una PILA aceptada por el validador del operador; un archivo de dispersión aceptado por el banco.

**Cartera y captaciones**
- [ ] **Recaudo y Tesorería reales** registrados (sin dobles de prueba). *Hoy no se cumple.*
- [ ] Líneas con versión vigente al corte; usura y Anexo 1 vigentes.
- [ ] Oficinas vinculadas a sus sucursales contables; órganos con miembros.
- [ ] Cuadre en cero o aceptado con acta; acta de activación preparada.
- [ ] Ensayo general con el primer cierre en cero y diferencias aprobadas.

**Comercial**
- [ ] Plantillas aplicadas; cada bodega con su conteo, saldo inicial aprobado y comparativo explicado.
- [ ] Puntos de venta, medios de pago y listas de precios vigentes.
- [ ] Facturación electrónica con canal real y resoluciones reales. *Hoy no se cumple* (sólo hay canal simulado).

**Operación**
- [ ] Respaldo del sistema anterior y plan de reversa.
- [ ] Equipo capacitado; acompañamiento de la primera semana agendado.
- [ ] Pantallas de la app de escritorio y móvil probadas a mano en los equipos de la cooperativa.

---

## Lo que todavía no está disponible

| Qué falta | Efecto en la implantación | Dónde se detiene | Mientras tanto |
|---|---|---|---|
| **Recaudo y Tesorería** (en construcción; no están en producción) | Sin un recaudador y una tesorería reales, la activación de Cartera y la de Captaciones y aportes no procede en producción | **Paso 7.13** (y con él 7.14, 8.8 y 10.8) | Se parametriza, se carga y se cuadra todo; en DEV y QA se ensaya con los dobles de prueba. La nómina se aprueba sin descuentos de Cartera; si hay una carga aplicada sin activar, cada nómina con esos deudores exige reconocer «Cartera por activar». El menú «Tesorería» actual (cheques, facturas, flujo de caja) **no** es la Tesorería que pide la activación |
| **Cargue de archivos planos del sistema anterior** (en especificación) | No hay lectura directa de los archivos del sistema anterior con perfiles y equivalencias | — | Se usan las plantillas de importación de cada módulo, diligenciadas desde los listados del sistema anterior |
| **Facturación electrónica con un proveedor real** | Sólo existe el canal simulado: los documentos salen «sin validez fiscal» | **Paso 9.8** (y la salida del POS y la facturación) | Se ensaya todo con el canal simulado; se necesita el proveedor tecnológico contratado y su adaptador |
| **Nómina electrónica ante la DIAN** | No se transmite la nómina electrónica | Después de 6.11 | Se transmite por el medio actual de la cooperativa |
| **SMS y WhatsApp** reales | Mensajes al deudor y avisos por esos canales son simulados, sin validez legal | Fase 7 (cobranza, aviso previo) | Correo; los avisos previos por canal real esperan proveedor |
| **Formatos sin cotejar** (PILA, SICSES, centrales de riesgo, exógena, Anexo 2) | Cada generación pide reconocer «formato sin cotejar» | 6.12 y los reportes regulatorios | Probar cada archivo en el validador del operador o de la entidad antes de presentarlo |
| **Plantilla de líneas de captación y aportes**, y su vigencia retroactiva | Una línea creada después del corte no sirve para la carga | **Paso 8.2** | Crear las líneas a más tardar el día del corte |
| **Plantilla de grupos del asociado** | Los grupos se digitan uno a uno | 2.6 | Digitar; son pocos |
| **Valores sin semilla** (UVR, tope de vivienda, límite inembargable, tope sin sucesión, límites de participación en aportes, probabilidad de incumplimiento voluntaria) | Lo que dependa de ellos no se calcula hasta registrarlos | Fase 4 | El maestro los registra con su fuente |
| **Parámetros «pendiente de validar»** (retención y GMF de captaciones, vivienda, días de cartera vencida, etapas de cobranza, catálogo PUC) | Los defectos funcionan, pero no están validados | Fases 3, 7, 8 | La contadora los confirma o registra la versión de la cooperativa |
| **Pruebas automáticas de la app** de escritorio y móvil | No hay pruebas de navegador ni de la app | 10.5 | Se prueban a mano en los equipos de la cooperativa |

---

## Dónde está el detalle

| Tema | Documento |
|---|---|
| Contabilidad, primer ejercicio, apertura y cierre | `docs/operaciones/contabilidad-primer-ejercicio.md` |
| Cómo contabiliza cada módulo, matrices y modo de paso | `docs/manual/contabilidad-contrato-de-contabilizacion.md` |
| Nómina, primer período | `docs/operaciones/nomina-primer-periodo.md` |
| PILA | `docs/operaciones/pila-primera-planilla.md` |
| Dispersión | `docs/manual/dispersion-bancaria.md` |
| Valores nacionales | `docs/operaciones/valores-nacionales.md` |
| Cartera, puesta en marcha | `docs/operaciones/cartera-puesta-en-marcha.md` y el índice `docs/manual/cartera.md` |
| Captaciones y aportes, puesta en marcha | `docs/operaciones/captaciones-puesta-en-marcha.md` |
| Inventario, puesta en marcha | `docs/operaciones/inventario-puesta-en-marcha.md` |
| Plantillas de importación | `docs/manual/plantillas-de-importacion.md` |
| Plan de COOFLOPAL (fechas y responsables) | `docs/implementacion/generar-plan.py` |
