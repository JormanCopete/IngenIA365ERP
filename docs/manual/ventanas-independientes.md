# Ventanas independientes, como en una aplicación de escritorio

2026-11-17, pedido del dueño. La aplicación web, en el navegador o instalada, abre cualquier opción en una ventana propia. Esa
ventana no tiene pestañas ni barra de direcciones, puede llevarse a otro monitor y trabaja sola: el POS en una ventana y el
estado de cuenta en otra, el libro auxiliar al lado del comprobante. En MAUI no aplica, porque allí no hay ventanas.

## Cómo se abre

- **En el menú**, al pasar el ratón sobre una opción aparece a la derecha el botón de ventana nueva. También funciona en el
  menú del POS.
- **Ctrl+clic, Mayús+clic o clic con la rueda** sobre cualquier enlace de la aplicación abren una ventana con sesión. Antes
  esto abría una pestaña del navegador que pedía entrar otra vez.
- **El botón de la barra superior**, junto a la ayuda, abre la pantalla actual en otra ventana. Así se «duplica» una
  pantalla sin la pestaña del navegador.

Si el navegador bloquea la ventana, aparece un aviso: hay que permitir las ventanas emergentes del sitio con el ícono a la
derecha de la barra de direcciones.

## La sesión

La ventana nueva **hereda la sesión** de la que la abrió, sin volver a entrar, si la cooperativa lo permite con el parámetro
`Security.Session.NewWindowInheritsSession`. Viene encendido y se cambia en Parámetros del sistema. Apagado, la ventana
nueva pide entrar y lleva su propia sesión.

Las ventanas que comparten la sesión forman un **grupo**: las duplicadas con «Duplicar pestaña» y las abiertas así. El grupo
se porta como una sola aplicación:

- **La inactividad se cuenta junta.** Trabajar en cualquiera de las ventanas mantiene vivas a todas. Los 30 minutos corren
  desde la última actividad de cualquiera de ellas.
- **La duración máxima es la misma.** Son 12 horas desde el ingreso; abrir ventanas no las reinicia.
- **«Salir» en una cierra todas.** También cierra todas «Cerrar sesión ahora» del aviso de la sesión. Las demás van al login
  en el siguiente segundo con «Cerraste sesión en otra ventana de la aplicación».
- **Cambiar de cooperativa** en una ventana sólo cambia esa ventana; el grupo se conserva.
- **Un ingreso nuevo abre un grupo nuevo.** Una ventana en la que se entra con otro usuario no queda atada a las anteriores.

Lo que no cambia es que **cerrar la ventana borra su sesión** del equipo. Por eso la aplicación instalada pide entrar cada vez
que se abre desde su ícono.

## Cómo funciona por dentro

- `wwwroot/js/ventanas.js` (Shared) abre la ventana con `window.open(…, 'popup')` y un nombre que dice si lleva la sesión:
  `erp-ventana:con-sesion:…` o `erp-ventana:sin-sesion:…`. También pone el botón flotante del menú y atiende Ctrl, Mayús y
  la rueda. Del lado de C# lo usan `VentanasDeLaAplicacion`, el botón del `MainLayout` y la configuración desde
  `AvisoDeVencimientoDeSesion`.
- Un guion en la cabeza de `App.razor` actúa **antes de que arranque Blazor**:
  - si la ventana debe heredar, copia las claves `ingenia365:` del `sessionStorage` de `window.opener`, sólo del mismo
    origen;
  - si no, las borra;
  - en los dos casos cambia el nombre a `erp-ventana:abierta`, para que un F5 no vuelva a copiar nada.
- El grupo es la clave `session_group` de la sesión. Su estado vive en `localStorage` bajo `ingenia365:grupo:{grupo}`, con la
  última actividad y la marca de cerrada; se poda a las 13 horas. Lo leen y escriben `pestanas.js` y
  `ICoordinacionEntrePestanas` (`LeerGrupoAsync`, `AnunciarActividadAsync`, `AnunciarCierreAsync`).
  - `RenovadorDeSesion` anuncia la actividad cada 5 segundos como mucho.
  - En cada latido, `VigilarAsync` lee el grupo antes de contar la inactividad.
  - `CentralAuthClient.LogoutAsync` anuncia el cierre antes de olvidar la sesión. El «Salir» del `MainLayout` pasa por ahí:
    antes usaba `AuthService`, que sólo borraba los dos tokens.
- El turno de canje y el buzón de sucesores del 2026-11-14 siguen igual. Las ventanas del grupo comparten el refresh y se
  coordinan como las pestañas duplicadas.
- En el manifiesto de la app instalada, `id` fija su identidad y `launch_handler` abre una ventana nueva cada vez que se lanza
  desde el ícono.

Pruebas: `tests/IngenIA365ERP.Shared.Tests/Sesion/VentanasDeUnaSesionTests.cs`. El interop del navegador se prueba a mano,
porque no hay pruebas de navegador en el repositorio.
