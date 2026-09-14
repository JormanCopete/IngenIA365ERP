# Alta de persona desde los módulos (feature 008)

Cómo un módulo registra a una persona **y** su rol en un solo paso, cómo muestra una persona
que ya existe, y qué hay que hacer para sumar un módulo nuevo (fase 2: Vendedores, Proveedores,
Clientes, Terceros). Está hecho en Personas, Empleados y Asociados; lo demás sigue esta receta.

## Las tres reglas

1. **La persona se escribe en un solo sitio.** Desde la interfaz sólo `PersonaDialog`
   (`Components/Personas`) y los compuestos «con persona» crean o editan una persona. Ningún
   módulo hace `PUT /api/core/people/{id}`. Con una persona **existente**, el módulo muestra sus
   datos **de sólo lectura** y ofrece «Editar datos de la persona», que abre `PersonaDialog`
   (con `Core.People.Update`). La prueba de arquitectura `LaPersonaSeEscribeEnUnSoloSitio` lo
   vigila.
2. **Las banderas con ficha propia las escribe el servidor.** `IsEmployee`, `IsAssociate` e
   `IsSalesperson` son el reflejo de una fila viva en `PAY_Employees`, `COR_Associates` e
   `INV_Salespeople`; las enciende y apaga el handler que crea o retira esa fila
   (`RegisterEmployee*`, `TerminateEmployee`, `RegisterAssociate*`, `CreateSalesperson`,
   `DeleteSalesperson`). No están en `PersonInput`: un cliente que las mande no rompe nada, pero
   tampoco cambia nada. Las otras cinco (cliente, proveedor, asesor, tercero, recibe factura) son
   marcas y se editan en Personas.
3. **Toda ruta de personas, empleados y asociados exige permiso** (`Core.People.*`,
   `Core.Associates.*`, `Payroll.Employees.*`); sin él la API responde el 404 indistinguible. La
   interfaz oculta lo que el usuario no puede hacer con `PermissionGate` sobre
   `PermisosDelUsuario`, que pide los permisos una vez por cooperativa a
   `GET /api/admin/permissions/mine`.

## El flujo en una pantalla de módulo

```
«Nuevo X»  →  PersonSearchPicker (PermitirCrear si tiene Core.People.Create)
   ├─ elige existente → diálogo del módulo: PersonaCampos DESHABILITADOS + pestañas del rol
   │                    + «Editar datos de la persona» (PersonaDialog)
   │                    · si ya tiene el rol → modo edición del rol
   └─ «Crear persona nueva con "…"» → diálogo del módulo: PersonaCampos EDITABLES + pestañas del rol
                        → un «Registrar» → POST /api/<modulo>/<rol>/with-person
                            · 422 Person.TaxIdDuplicate → «Usar esa persona» (GET /by-document?taxId=)
                            · 422 Person.TaxIdDeleted  → «Restaurar persona» (Core.People.Delete) o texto
```

Piezas, todas en `IngenIA365ERP.Shared`:

| Pieza | Qué hace |
|---|---|
| `Services/Core/PersonasClient` + `PersonasDtos` | Toda la conversación con `/api/core/people`: obtener, crear, actualizar, buscar (con `rol`), por documento, restaurar, altas con persona |
| `Services/Core/CatalogosDePersona` | Tipos de documento, persona, género, estado civil, nivel educativo, estado: **un solo sitio** |
| `Models/Personas/PersonaFormularioModelo` | El modelo del formulario: `Nuevo(documento)`, `DesdeDto`, `AInput()`, `Validar()`; derivadas de sólo lectura |
| `Components/Personas/PersonaCampos` | Los campos de una sección (`Identificacion`, `Contacto`, `Demografia`, `Roles`) con `Deshabilitado` |
| `Components/Personas/PersonaFormulario` | Las cuatro secciones en una `SfTab` (lo usa el diálogo de Personas) |
| `Components/Personas/PersonaDialog` | Crear / editar una persona; duplicado → «Abrir esa persona»; eliminada → «Restaurar persona» |
| `Components/Shared/PersonSearchPicker` | El buscador: `PermitirCrear`, `OnCrearPersona`, `FiltroRol`, `TextoSinResultados`; detecta documentos de eliminadas |
| `Components/Shared/PermissionGate` | Oculta según `PermisosDelUsuario`; `MostrarAviso="true"` para una sección entera |

Por qué **`PersonaCampos` por sección** y no una pestaña completa: en Syncfusion un componente
hijo no puede emitir `TabItem` dentro del `TabItems` del padre de forma fiable, así que el módulo
arma su propia `SfTab` y pone una sección por pestaña.

## Receta para sumar un módulo con ficha propia (Vendedores)

Servidor (Application → API), copiando de Asociados:

1. `Inventory/Salespeople/Contracts/SalespersonInput.cs` con los campos del rol y su validador.
2. `Inventory/Salespeople/Services/SalespersonRegistrar.PrepareAsync(Person, SalespersonInput)`:
   valida, construye la fila con `Person = person`, enciende `person.IsSalesperson`, **no guarda**.
3. `CreateSalespersonCommand : SalespersonInput { Guid PersonPublicId }` sobre el registrar.
4. `RegisterSalespersonWithPersonCommand(PersonInput Person, SalespersonInput Salesperson)`:
   `PersonFactory.PrepareAsync` → registrar → **un** `SaveChangesAsync`, con el `catch` de
   `PersonFactory.EsColisionDeDocumento` (dos usuarios a la vez).
5. `POST /api/inventory/salespeople/with-person` con `.AddEndpointFilter<ErrorEnvelopeFilter>()`
   y **dos** `RequirePermission` (`Inventory.Salespeople.Create` y `Core.People.Create`).
6. Códigos en el catálogo de permisos (seeder del módulo) y reparto en `BuiltInRolesSeeder`.
7. Registrar el `Registrar` en `Application/DependencyInjection.cs`.

Cliente (`Shared`):

8. `PersonasClient.RegistrarVendedorConPersonaAsync` + DTO de request/resultado.
9. En la pantalla: `PersonSearchPicker` con `PermitirCrear`/`OnCrearPersona`; `SfTab` con tres
   `PersonaCampos` (`Deshabilitado="@(!_personaNueva)"`) + las pestañas del rol; banner
   `persona-solo-lectura` con «Editar datos de la persona» dentro de
   `PermissionGate Required="Core.People.Update"`; `PersonaDialog` para ese botón;
   `[SupplyParameterFromQuery(Name = "persona")]` para llegar desde Personas; el aviso
   `aviso-documento` con «Usar esa persona» / «Restaurar persona». `Empleados.razor` es el modelo.
10. En `Personas.razor` → `CalcularRolesRegistrablesAsync`: agregar `"salesperson"` si tiene el
    permiso de crear, y en `RegistrarRolAsync` la ruta `/inventario/vendedores?persona={id}`.
    `PersonaCampos` ya pinta el distintivo y el botón «Registrar como vendedor…».
11. Sumar la carpeta a `ModulosMigrados` en `LasPantallasDicenQueEstanCargando` y quitar la
    pantalla de `BuscadoresAdHocHeredados` en `LaPersonaSeEscribeEnUnSoloSitio` si estaba.

## Receta para un rol que es sólo una marca (Proveedores, Clientes, Terceros)

No hay compuesto: la marca se guarda con la persona. El módulo abre `PersonaDialog` con
`RolSimpleInicial="supplier"` (`customer`, `advisor`, `thirdparty`, `invoice`) y la casilla llega
premarcada; al guardar, `OnGuardado(Guid)` devuelve la persona para seguir con lo del módulo.
`PersonSearchPicker` con `FiltroRol="supplier"` limita la búsqueda a quienes ya tienen la marca.

## Migrar un buscador ad-hoc

Los que quedan (lista en `LaPersonaSeEscribeEnUnSoloSitio.BuscadoresAdHocHeredados`) se cambian
por `<PersonSearchPicker OnPersonSelected="…" />`; el resultado `PersonSearchItem` trae las
ocho banderas y el `PublicId`. Al migrar uno, se saca de la lista (la prueba avisa si se olvida).

## Casos que ya están resueltos y no hay que reinventar

- **Documento de una persona eliminada**: el índice único del documento no distingue eliminadas;
  la API responde `Person.TaxIdDeleted` y sólo quien tiene `Core.People.Delete` restaura
  (`POST /api/core/people/{id}/restore`, misma fila). Nunca se crea una segunda fila.
- **Reingreso de un empleado retirado**: ficha nueva; la retirada es historial;
  `GET /api/payroll/employees/by-person/{id}` devuelve sólo la viva y responde 404 si no hay.
- **Dos usuarios crean la misma persona a la vez**: el segundo recibe `Person.TaxIdDuplicate`
  con el nombre, no un 500 (el handler traduce la violación de `UK_COR_People_TaxId`).
- **Prerender del Web**: `PermisosDelUsuario` no pregunta sin sesión y el gate queda cerrado en
  silencio; sólo avisa si la API falla con sesión.

## Dónde mirar

- Especificación, plan y contratos: `specs/008-alta-persona-un-paso/` (`contracts/api.md` tiene
  cada ruta con su permiso y sus códigos de error).
- Pruebas que fijan las reglas: `Architecture.Tests/Principles/LaPersonaSeEscribeEnUnSoloSitio`,
  `LosMaestrosDePersonaExigenPermiso`; `Application.Tests/Core/People/*`;
  `API.IntegrationTests/Core/AltaDePersonaEnUnPasoTests` (recorre todo por HTTP).
