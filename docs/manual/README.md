# Manual del sistema: cómo está hecho y cómo se mantiene

> Para quien tenga que corregir una guía, agregar una pantalla nueva al manual o
> ponerle capturas. Lo que ve la persona usuaria está en la propia aplicación:
> **SISTEMA → Manual del sistema** (`/manual`) y el botón **«¿Cómo se hace?»** de la
> barra superior, que abre la guía de la pantalla en la que esté.

Última revisión: 2026-09-04.

---

## 1. Qué es y dónde vive

El manual es parte de la aplicación, no un documento aparte. Se compila con ella y
viaja con cada versión: cada despliegue lleva el manual que describe exactamente
esas pantallas, y abre aunque la API esté caída, porque no depende de ninguna
llamada.

| Pieza | Archivo | Qué hace |
|---|---|---|
| Catálogo | `src/Presentation/IngenIA365ERP.Shared/Services/Manual/ManualCatalogo.cs` | **Todo el contenido.** Un tema por opción del menú: título, módulo, ruta, resumen, requisitos, pasos, palabras clave, temas relacionados. |
| Modelos | `…/Services/Manual/ManualModelos.cs` | `TemaDeManual`, `PasoDeManual`, `TipoDeTema`. |
| Buscador | `…/Services/Manual/ManualBuscador.cs` | Búsqueda en el cliente, sin tildes ni mayúsculas, con puntaje por dónde aparece cada palabra. |
| Índice | `…/Pages/Manual/Index.razor` (`/manual`) | Búsqueda, «Primeros pasos» y el índice por módulo. Acepta `?q=` para llegar buscando. |
| Tema | `…/Pages/Manual/Tema.razor` (`/manual/{slug}`) | La guía: pasos numerados con captura opcional y botón «ir a la pantalla», o **modo guiado** de un paso a la vez. |
| Botón | `…/Components/Shared/BotonAyuda.razor` | En la barra superior. Resuelve la ruta actual contra el catálogo y lleva a su guía; si no hay, al manual con la búsqueda hecha. |
| Estilos | `…/wwwroot/css/componentes.css` (sección «Manual del sistema») | Sólo disposición; colores de las clases y tokens existentes. |

Dos clases de tema:

- **Procesos**, con pasos escritos a mano: entrar, activar el segundo factor,
  registrar una cooperativa, invitar, comprobante contable, solicitud de crédito,
  liquidar nómina… Son los que llevan `TipoDeTema.Proceso`.
- **Maestros, reportes y consultas**, con una guía general cada tipo, generada
  por los ayudantes `Maestro(...)`, `Reporte(...)` y `Consulta(...)` del catálogo.
  Todos los maestros se usan igual —«Nuevo», el lápiz para corregir, la papelera
  para retirar, «Guardar»/«Cancelar», «Buscar»— y escribir ciento veinte veces lo
  mismo sólo garantiza que algunas copias envejezcan mal. Lo particular de cada
  pantalla (qué campos pide, qué valida) lo muestra la propia pantalla.

## 2. Cómo se relaciona cada pantalla con su guía

`ManualCatalogo.ParaRuta(ruta)` decide qué guía abre el botón:

1. Coincidencia exacta con `Ruta` o con alguna de `RutasCubiertas`. Las plantillas
   con parámetros (`/security/users/{PublicId}/roles`) coinciden segmento a
   segmento.
2. Si no, el **prefijo más largo**: `/cartera/creditos/8c2a…` cae en «Cartera de
   créditos».
3. Si tampoco, el botón lleva a `/manual?q=<último tramo de la ruta>`, que casi
   siempre es el nombre de la pantalla.

Por eso, al **agregar una pantalla nueva**, basta con darle un tema (o sumar su
ruta a `RutasCubiertas` de uno existente) para que el botón la reconozca.

## 3. Agregar o corregir una guía

Todo pasa por `ManualCatalogo.Construir()`.

**Un maestro nuevo**, una línea:

```csharp
t.Add(Maestro("/inventario/marcas", "Marcas", Modulos.Inventario, "una marca",
    "Nota opcional que se suma al resumen.", "marcas", "fabricante"));
```

**Un proceso nuevo**, con sus pasos:

```csharp
t.Add(Proceso("cerrar-caja", "Cerrar el turno de caja", Modulos.Inventario, "/inventario/turnos",
    "Qué hace, en una o dos frases.",
    [
        P("Título del paso", "Detalle. Nombres de botones entre «comillas», como se ven en pantalla.", "/inventario/turnos", "Abrir Turnos"),
        P("Otro paso", "El tercer argumento (ruta) es opcional: cuando está, el paso ofrece un botón para ir."),
    ],
    ["palabras", "clave", "para", "buscar"],
    ["Requisito 1.", "Requisito 2."],
    ["slug-de-un-tema-relacionado"],
    ["/otra/ruta/que/este/tema/tambien/cubre"],
    TipoDeTema.Proceso));
```

Reglas que conviene respetar:

- **El slug es la dirección** (`/manual/cerrar-caja`): minúsculas, guiones, sin
  tildes. No lo cambies después de publicado; otros temas y capturas lo referencian.
- **Nombrá los botones como están en la pantalla**, entre comillas angulares:
  «Nuevo», «Guardar», «Reenviar». Si la pantalla cambia el nombre, cambia la guía.
- **No inventes funciones.** Si no sabés qué hace un botón, abrí la pantalla o el
  `.razor` antes de escribir.
- **Voseo**, como el resto de la interfaz: «elegí», «marcá», «revisá».
- Un paso, una acción. Si un paso necesita dos párrafos, son dos pasos.

Las pruebas de arquitectura no cubren el manual; la revisión es leer el tema en
`/manual/<slug>` después de compilar. Es rápido: no hay API detrás.

## 4. Capturas de pantalla

Cada paso puede llevar **una imagen**. Convención:

```
src/Presentation/IngenIA365ERP.Shared/wwwroot/img/manual/<slug>/paso-<n>.png
```

- `<slug>` es el del tema; `<n>` el número del paso, desde 1.
- Se sirven como activo estático en `_content/IngenIA365ERP.Shared/img/manual/…`.
  No hay que registrarlas en ningún sitio: la página arma la ruta y, **si el
  archivo no existe, la imagen se oculta** (`onerror`). Un tema sin capturas sigue
  siendo un tema completo, y una captura vieja se retira borrando el archivo.
- PNG, **1280 px de ancho** como máximo, recortadas a la parte de la pantalla que
  el paso menciona. Sin datos reales de personas: usá la cooperativa de prueba de
  QA y nombres ficticios. Sin la barra de direcciones ni pestañas del navegador.
- Con tema **claro** y densidad normal, para que se vean igual en cualquier
  preferencia del lector.

### Cómo tomarlas

1. Entrá a **QA** (`https://app-qa.ingenia365.com`) con un usuario de la
   cooperativa de prueba, en una ventana de 1280 px de ancho.
2. Abrí la pantalla del paso y dejala en el estado que el paso describe (el
   formulario abierto, el registro buscado).
3. Captura de la región (Windows: `Win + Shift + S`), recorte, y guardar con el
   nombre de la convención.
4. Compilar y abrir `/manual/<slug>`: la imagen debe aparecer bajo el paso.

Hoy **no hay capturas** en el repositorio: la estructura está lista y las guías
funcionan sin ellas. Tomarlas es trabajo de quien conozca las pantallas con datos
de prueba delante; el orden sugerido es el de «Primeros pasos» en `/manual`.

## 5. Qué no hace, a propósito

- **No resalta elementos sobre la pantalla real** (tours con flechas). Eso exige
  acoplar cada guía al marcado de cada pantalla, y se rompe con cualquier cambio de
  diseño sin que ninguna prueba lo note. El modo guiado va paso a paso con botón
  «abrir la pantalla de este paso», que sobrevive a los cambios.
- **No lee del servidor.** Ni contenido ni estadísticas de uso. Si algún día hace
  falta editar el manual sin desplegar, el catálogo puede pasar a JSON en
  `wwwroot` sin tocar las páginas; por ahora, compilarlo es lo que garantiza que
  cada versión lleve su manual.
- **No sustituye a la documentación de operaciones** (`docs/operaciones/`), que es
  para quien administra la plataforma, no para quien la usa.
