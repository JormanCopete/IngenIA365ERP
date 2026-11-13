# Memoria de la API

Qué pasó el 2026-11-06, qué se midió y qué mirar si vuelve a ocurrir.

## Qué pasó

Las dos réplicas de `erp-api` de producción cayeron por `OOMKilled` (10:20:10 y 10:22:28 UTC) mientras se aplicaba una plantilla de
personas de 11.949 filas, con límite de 1 GiB. Con tandas de 2.000 filas pasó. El reintento llevó la misma carga a la réplica que
seguía viva, por eso cayeron una tras otra.

## La causa

No fue una fuga, ni una carga que retuviera demasiado. Un `dotnet-gcdump` en plena aplicación dio **~200 MB vivos sobre la base**; el
resto del pico era basura sin recoger.

.NET fija por defecto el techo del heap en el 75 % del límite del contenedor (768 MiB en 1 GiB), y da por hecho que lo demás cabe en la
cuarta parte restante. Aquí no cabe. En QA, misma imagen y mismo límite, en reposo:

| `memory.stat` | MB | Qué es |
|---|---:|---|
| `anon` | 514 | heap más memoria nativa |
| `shmem` | 232 | código JIT doblemente mapeado por W^X. Cuenta contra el contenedor, no se reclama, **el GC no la ve** |
| `file` | 234 | casi todo es ese mismo `shmem` |
| total | 755 | pico histórico 803 |

Las cuentas: 768 de heap + 232 de `shmem` = 1.000 MiB, y faltan pilas y nativo. **El kernel mata el contenedor antes de que el GC llegue a
su techo.** Y Server GC, sin presión, deja crecer el heap: una carga que asigna 5 GB en diez segundos llena ese espacio con basura.

## Qué se hizo

| Cambio | Dónde | Efecto |
|---|---|---|
| Techo del heap al 55 % | `System.GC.HeapHardLimitPercent` en `IngenIA365ERP.API.csproj` (viaja en la imagen, vale para todo ambiente) | El GC recoge a tiempo. Lo que de verdad no quepa es un error de esa petición, no la caída del pod |
| Columna resuelta una sola vez por hoja | `HojaDeImportacion` | Cada celda normalizaba todos los encabezados: revisar 12.000 filas asignaba 2 GB en eso. La revisión bajó de 3,1 GB a 1 GB asignado y de 4,4 s a 2,1 s; la aplicación, de 7,2 a 5,1 GB |
| Limitador `cargas` | `LimiteDeCargas`: 2 en curso y 4 en cola por réplica (`Cargas:Concurrencia`), 429 `Import.Busy` | Dos cargas no suman memoria sobre el mismo pod |
| Tope del libro descomprimido | `ClosedXmlTabularFileReader.MaximoDescomprimido`: 128 MB, antes de abrirlo | Los 16 MB del archivo no limitan lo que se carga: un zip se infla |
| Alertas | GitOps, `infrastructure/nonprod/alertas-api.yaml` | Reinicio, `OOMKilled`, menos de 2 réplicas, API caída. Antes no había ninguna |

`TodaCargaMasivaTieneLimite` fija que toda ruta que reciba un archivo lleve limitador.

**Lo que se probó y se descartó:** guardar por tandas dentro de la transacción. Ahorró 190 MB de 1.090 en lo que dejaba crecer el heap, y a cambio
dejaba en la auditoría filas que luego se revierten si un error aparece más abajo: el interceptor las manda a Mongo al guardar, antes del commit.

## Cuánto cuesta una carga (11.949 personas, 30 columnas)

| Etapa | Vivo sobre la base | Asignado | Tiempo |
|---|---:|---:|---:|
| Sólo leer el libro (ClosedXML) | +305 MB | 0,7 GB | 3,3 s |
| Revisión | +376 MB | 1,0 GB | 2,1 s |
| Revisión con Excel de vuelta | +470 MB | 2,0 GB | 3,5 s |
| Aplicación | ~+200 MB en el volcado; sin presión, el heap sube +1.090 MB | 5,1 GB | 10 s |

Medido con `MemoriaDeImportacionTests` (`RUN_PERF_TESTS=1`). El pico de heap bajo Server GC no sirve para esto: sin presión la basura no se recoge y
un cambio real no se nota. Para ver lo vivo: `DOTNET_gcServer=0`, `DOTNET_gcConcurrent=0`, `DOTNET_GCgen0size=1000000`; lo exacto, `dotnet-gcdump` en
plena operación. `DOTNET_GCHeapHardLimit` no sirve en esta prueba: con todo en un proceso, el host no arranca ni con 1.000 MiB.

## Si vuelve a ocurrir

1. `kubectl -n erp-pdn get pods -o custom-columns=N:.metadata.name,R:.status.containerStatuses[*].restartCount,LAST:.status.containerStatuses[*].lastState.terminated.reason`
2. La composición, dentro del pod: `grep -E '^(anon|file|shmem) ' /sys/fs/cgroup/memory.stat`. Si `anon + shmem` se acerca al límite en reposo, hace
   falta subirlo o bajar el porcentaje.
3. Con el 55 %: en 1 GiB el heap topa en 563 MiB y en 2 GiB en 1.126 MiB. Lo que queda es para `shmem` (~230) y nativo.

## Lo que queda abierto

- **Las exportaciones no tienen tope de filas ni limitador** (`ExportadorDeTablas`, `format=xlsx|pdf|docx`): comparten ruta con el json. Con el techo
  no tumban el pod, pero un mayor en Excel de un rango largo responde 500 si no cabe.
- **Un solo nodo con todas las réplicas.** Sigue siendo el punto único de falla: si cae el nodo, cae todo.
- **Límites sobre la capacidad.** La suma de límites de memoria del nodo pasa del 100 %: son topes, no reservas, pero si varios pods los tocan a la vez
  decide el kernel. Con otro cliente en el mismo nodo hay que revisarlo.
- **`RespaldoMongoFallido`** cuenta los Job fallidos que sigan existiendo: el de hace 82 días (`mongo-respaldo-diario-29778195`) la mantendría disparada.
