# Tablero de inicio y Favoritos

Pedido del dueño del 2026-11-17.

## Para quien usa el ERP

- **Favoritos** (menú, debajo de Dashboard): hasta diez accesos directos a las opciones que más usa. Son los mismos accesos que antes
  estaban en el tablero.
- **Dashboard**: las cifras más importantes para usted al entrar.
  - Arriba van los **Pendientes**: lo que espera por usted según sus permisos (aprobar, contabilizar, cerrar). Solo aparece lo que
    tiene algo; un clic lleva a la pantalla donde se atiende.
  - Cada **tarjeta** dice si muestra **Todo** (lo de toda la cooperativa que usted puede ver) o **Solo lo mío** (lo que usted hizo:
    sus ventas, su caja). Si el indicador lo admite, se cambia con un clic en esa marca. Un clic en la cifra lleva al detalle.
  - **Personalizar**: elija qué cifras ver, en qué orden y con qué alcance. Desde ese momento manda su tablero; «Volver al tablero de
    mi rol» lo borra.
  - Sin tablero propio se ve el **de su rol**; si tiene varios roles, el del primero por nombre que tenga uno; si ninguno lo tiene, el
    **sugerido**.
- Nadie ve una cifra de una pantalla que no puede abrir: cada indicador exige el permiso de la pantalla de la que sale.

## Para el administrador

**Administración › Tablero por rol** (ver: `Security.Roles.View`; cambiar: `Security.Roles.Create`). Elija el rol, agregue y ordene
los indicadores y marque cuáles van con «Solo lo mío». La lista dice el permiso de cada indicador: un usuario del rol sin ese permiso
no lo verá. «Quitar» devuelve el rol al tablero sugerido. No cambia el tablero que un usuario ya se armó.

## Para quien agrega un indicador (receta)

1. Una clase en `src/Core/IngenIA365ERP.Application/Tablero/<Modulo>/` que implemente `IIndicadorDelTablero` (o
   `IPendienteDelTablero`). Se registra sola: no se toca `DependencyInjection.cs` ni el tablero.
2. `Definicion`:
   - `Codigo`: `modulo.cifra` en minúsculas (`ventas.del-dia`). Queda guardado en preferencias y tableros de rol: **no se cambia**.
   - `Permiso`: el de la pantalla de la que sale la cifra (el mismo `RequirePermission` de su ruta de consulta).
   - `AdmiteSoloLoMio`: si tiene sentido «lo que hice yo». Con `contexto.SoloLoMio` se filtra por `contexto.UsuarioId`
     (`SEC_Users.Id`, el de las columnas `…ByUserId`) o por `contexto.UsuarioDeAuditoria` (lo que guarda `CreatedBy`).
   - `Tipo`: `Cifra` (valor y, si se quiere, `Anterior` con su etiqueta: «ayer», «mes anterior») o `Serie` (barras: días, rangos).
   - `Ruta`: la pantalla o el informe que explica la cifra.
   - `PorDefecto`: si entra en el tablero sugerido.
3. `CalcularAsync` **solo lee**, con `AsNoTracking`, y respeta el alcance de sucursal del usuario si la pantalla de origen lo respeta
   (reutilizar la consulta de esa pantalla cuando se pueda). Fechas con `contexto.Hoy` (hora de Colombia).
4. Un pendiente cuenta lo que espera a quien tiene el permiso; si la cuenta es cero no se muestra.
5. La guarda `Cada_indicador_y_pendiente_tiene_codigo_unico_permiso_y_ruta_interna` (`TableroTests`) revisa código, permiso y ruta.

Rutas: `GET /api/dashboard/mine`, `GET /api/dashboard/indicators/{codigo}?soloLoMio=`, `GET /api/dashboard/pending`,
`GET /api/dashboard/roles`, `PUT /api/dashboard/roles/{rol}`. El tablero propio es la preferencia `ui.tablero`; el de cada rol, la
fila `Tablero.Rol.{código del rol}` de `COR_SystemSettings`. Sin migraciones.
