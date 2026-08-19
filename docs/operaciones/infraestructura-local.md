# Dónde corre cada servicio en tu máquina

Elegir **el motor** y elegir **dónde vive** son dos preguntas distintas, y el
proyecto las separa:

| Pregunta | Clave | Valores |
|---|---|---|
| ¿Qué motor de base de datos? | `Database:Provider` | `PostgreSQL`, `SqlServer` |
| ¿Dónde corre cada servicio? | `Infraestructura:Destinos:<servicio>` | `Local`, `Docker`, `Wsl` |

Son independientes: podés usar PostgreSQL instalado en el sistema, PostgreSQL en
un contenedor o PostgreSQL dentro de WSL sin cambiar una línea de código.

## Por qué es por servicio y no un único interruptor

Porque el caso real no es «todo local» ni «todo en contenedor». En Windows lo
cómodo es tener la base y el correo instalados en el sistema, pero **Redis no
tiene build oficial para Windows** y termina levantándose en WSL o en un
contenedor. Un interruptor global obligaría a mover todo junto.

## Cómo se decide, de menor a mayor prioridad

```
1. appsettings.json                             base común
2. appsettings.Development.json                 el ambiente
3. appsettings.Development.Windows.json         ← tu sistema operativo (versionado)
4. appsettings.Development.local.json           ← tu máquina (NO versionado)
5. Variables de entorno                         ← siempre ganan
```

Los pasos 3 y 4 son los nuevos. El 3 se elige solo según el sistema operativo
donde arranques: `Windows`, `Linux` o `macOS`. El 5 va último a propósito: en un
clúster la configuración llega por variables, y ningún archivo del repositorio
puede pisarla.

## Valores por defecto de cada sistema

| Servicio | Windows | Linux | macOS |
|---|---|---|---|
| PostgreSQL | Docker | Local | Local |
| SQL Server | Local | Docker | Docker |
| Redis | Docker | Local | Local |
| MongoDB | Local | Local | Local |
| SMTP | Docker | Docker | Docker |

Dos elecciones que no son arbitrarias:

- **SQL Server en macOS no puede ser `Local`**: no existe build nativo. En Apple
  Silicon corre en contenedor y no hay alternativa.
- **PostgreSQL en Windows quedó en `Docker`** aunque la preferencia sea local,
  porque es donde están hoy los datos sembrados. Ver más abajo cómo cambiarlo.

## Ajustar tu máquina

Copiá `appsettings.Development.local.json.ejemplo` como
`appsettings.Development.local.json`. Git lo ignora. Sólo hace falta declarar lo
que cambia; el resto se hereda.

```jsonc
{
  "Infraestructura": {
    "Destinos": { "Redis": "Wsl" },
    "Redis": { "Wsl": "172.24.80.1:6379,abortConnect=false" }
  }
}
```

> **La IP de WSL cambia al reiniciar.** Si Redis en WSL responde en
> `localhost:6379` —lo habitual con WSL2— usá esa dirección y te evitás el
> problema. La IP del adaptador `vEthernet (WSL)` sólo hace falta cuando el
> servicio escucha exclusivamente en la interfaz de WSL. Se consulta con
> `wsl hostname -I`.

## Pasar PostgreSQL a local en Windows

El PostgreSQL del puerto 5432 y el del contenedor (5433) son instalaciones
**distintas y sin relación**: las bases del ERP están en el contenedor. Para
mover el desarrollo al local hacen falta tres pasos, en este orden:

1. Crear las bases en el PostgreSQL local:
   ```sql
   CREATE DATABASE ingenia365erp;
   CREATE DATABASE ingenia365erp_admin;
   ```
2. Ajustar usuario y contraseña en `Infraestructura:PostgreSQL:Local` (el
   catálogo trae `ingenia`, que es el del contenedor).
3. Cambiar `Infraestructura:Destinos:PostgreSQL` a `Local`.

Al arrancar, `AutoMigrate` crea el esquema, pero **los datos no se copian**: el
administrador maestro y las cooperativas que hayas creado siguen en el
contenedor. Hay que volver a sembrarlos, o volcar y restaurar.

## Verificar contra qué estás corriendo

La API lo dice en cada arranque, y por eso se agregó:

```
Infraestructura (Windows): MongoDB=Local · PostgreSQL=Docker · Redis=Docker · Smtp=Docker · SqlServer=Local
Base de datos: proveedor PostgreSQL (origen: appsettings) — operativa host=localhost;port=5433;…
```

Con tres motores encendidos a la vez en la misma máquina, «¿esto es el local o
el del contenedor?» no es una pregunta ociosa. Ahora la responde el propio log.

## Si te equivocás en el nombre

Falla al arrancar, con el detalle:

```
Infraestructura:Destinos:Redis apunta a 'Kubernetes'. 'Kubernetes' no es un destino
conocido. Los habituales son Local, Docker y Wsl. Definidos para Redis: Docker,
Local, Wsl. Revisá appsettings.<Ambiente>.Windows.json o el .local.json de esta máquina.
```

Es deliberado que no arranque en vez de seguir con un valor por defecto: un
destino mal escrito que degradara en silencio dejaría la aplicación apuntando a
la base equivocada, y eso es peor que no arrancar.

## Agregar un servicio nuevo al esquema

En `InfraestructuraConfiguracion.Resolver` hay un `switch` por servicio. Cada
rama traduce el destino elegido a **la clave que el consumidor ya leía**. Ése es
el punto del diseño: ni Caching, ni Audit, ni Storage, ni Persistence saben que
esto existe: siguen leyendo `ConnectionStrings:Redis` como siempre. Por eso un
ambiente que no declare la sección `Infraestructura` no cambia en nada.
