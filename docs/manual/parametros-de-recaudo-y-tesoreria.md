# Parámetros de Recaudo y de Tesorería

Desde el 2026-11-25 (pedido del dueño), cada módulo tiene su propia pantalla de parámetros. Hasta ese día no había ninguna. La pantalla de parámetros de
Inventario los rechaza a propósito, porque «tienen su ruta y su permiso», pero esa ruta nunca se construyó. Por eso los parámetros de Recaudo (`RCD`) y
de Tesorería (`TES`) regían siempre con su valor de fábrica.

| Módulo | Pantalla | Ruta de la API | Ver | Cambiar |
|---|---|---|---|---|
| Recaudo | Recaudo → Configuración → Parámetros (`/recaudo/configuracion/parametros`) | `/api/receipts/parameters` | `Receipts.Setup.View` | `Receipts.Setup.Manage` |
| Tesorería | Tesorería → Parámetros (`/tesoreria/parametros`) | `/api/treasury/parameters` | `Treasury.Parameters.View` | `Treasury.Parameters.Manage` |

Para Recaudo se usan los permisos de configuración que ya existían. Los de Tesorería son nuevos y los siembra la API al arrancar. El perfil «Tesorero» trae
el de ver, y el administrador trae los dos.

## Cómo se usa

- Los parámetros se agrupan por tema, que es lo que va antes del primer punto de la clave: `Caja`, `Recibo`, `Giros`, `Cheques`, `Gmf`… En Recaudo, la
  caja va primero. El buscador filtra por la clave y por la descripción.
- Cada parámetro muestra su valor vigente y desde cuándo rige, o «De fábrica» si nadie lo ha cambiado. También muestra lo programado a futuro.
- **Cambiar** no edita el valor en su sitio: registra una **vigencia nueva**, con el valor, la fecha desde la que rige (hoy por defecto) y el motivo. La
  vigencia anterior se cierra la víspera y queda en el **Historial**.
- Las reglas de cada módulo las aplica el servidor, y la pantalla muestra su mensaje tal cual. Por ejemplo:
  - `Caja.SesionUnica` no se enciende sin una caja unida y sin la cuenta puente (ver [caja-unica.md](caja-unica.md)).
  - `Caja.SoloCajerosAsignados` (encendido de fábrica, 2026-11-25) hace que cada caja de Recaudo, del punto de venta y de la caja única la abran sólo
    sus cajeros (ver [mi-caja-y-arqueo.md](mi-caja-y-arqueo.md#quién-abre-cada-caja)).
  - `Recaudo.HoraDeCorte` tiene que caer antes del ciclo diario de Cartera.
- Las cajas leen sus parámetros al momento de cada operación. Por eso cambiar `Caja.ArqueoEstricto` o `Caja.SesionUnica` rige también para las sesiones que
  ya están abiertas.

## Por dentro

- Las dos rutas reenvían a las consultas y al comando de la plataforma (`ListParametersQuery`, `GetParameterHistoryQuery`, `AddParameterVersionCommand`),
  con el módulo fijo por la ruta y sin ámbito: las claves de estos módulos son generales.
- Las reglas propias de cada módulo están en `ReglasDeParametrosDeRecaudo` y `ReglasDeParametrosDeTesoreria`.
- La pantalla es un solo componente, `Components/Comunes/ParametrosDelModulo.razor`.
- El cliente es `ConfiguracionDeRecaudoClient`, con el módulo `RCD` o `TES`.
