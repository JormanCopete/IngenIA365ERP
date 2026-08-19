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

## Cuántos archivos hay, y por qué esos

```
appsettings.json                     comun a todos los ambientes
appsettings.Development.json         tu maquina  ← aca vive Infraestructura
appsettings.Production.json          desplegado
appsettings.Development.local.json   opcional, por maquina, git lo ignora
```

**Cuatro, y sólo uno es obligatorio tocar.** No hay archivo por sistema
operativo: la elección no depende de si usás Windows o Mac sino de cómo tenés
montado *tu* equipo, y para eso alcanza el `.local.json`. Tampoco hay
`appsettings.QA.json`: QA no declaraba nada que el despliegue no inyecte ya por
variables de entorno.

Orden de precedencia, de menor a mayor:

```
appsettings.json
appsettings.Development.json
appsettings.Development.local.json     ← tu maquina
variables de entorno                   ← siempre ganan
```

## Los ambientes desplegados no leen nada de esto

Ojo con un detalle que no es obvio: **la VPS de DEV corre con
`ASPNETCORE_ENVIRONMENT=Development`**, así que lee el *mismo*
`appsettings.Development.json` que tu portátil, con sus catálogos apuntando a
`localhost`.

Por eso la sección `Infraestructura` **se ignora por completo dentro de un
contenedor**. La guardia mira `DOTNET_RUNNING_IN_CONTAINER`, que fijan las
imágenes base de .NET, y no el nombre del ambiente:

- Fiarse del nombre del ambiente dejaría al pod de DEV resolviendo contra
  `localhost`.
- Fiarse de que las variables de entorno ganen por orden funciona hoy, pero se
  rompe el día que alguien olvide una.

Con la guardia, la sección es inerte en cualquier despliegue, se llame como se
llame el ambiente. En el arranque se ve así:

```
Infraestructura: en contenedor — la configuración llega por variables de entorno.
```

## Valores por defecto

| Servicio | Valor | Por qué |
|---|---|---|
| PostgreSQL | `Local` | Instalado en el sistema, puerto 5432 |
| SQL Server | `Local` | Autenticación de Windows, sin contraseñas |
| Redis | `Docker` | No hay build oficial para Windows |
| MongoDB | `Local` | Servicio de Windows, puerto 27017 |
| SMTP | `Docker` | smtp4dev captura el correo en el 1025 |

## Preparar el PostgreSQL local

El PostgreSQL del puerto 5432 y el del contenedor (5433) son instalaciones
**distintas y sin relación**. Antes de usar el local hay que crear el rol y las
dos bases:

```bash
.\tools\scripts\preparar-postgres-local.ps1
```

Pide dos contraseñas por consola —la del superusuario `postgres` y la que querés
para el rol `ingenia`— y no guarda ninguna: no viajan por la línea de comandos
ni quedan en el historial.

Si usás para el rol la misma contraseña que ya figura en
`appsettings.Development.json`, no hay que tocar nada más. Si usás otra, ponela
en `appsettings.Development.local.json`:

```jsonc
{
  "Infraestructura": {
    "PostgreSQL": {
      "Local": {
        "Operativa": "Host=localhost;Port=5432;Database=ingenia365erp;Username=ingenia;Password=TU_CLAVE",
        "Admin":     "Host=localhost;Port=5432;Database=ingenia365erp_admin;Username=ingenia;Password=TU_CLAVE"
      }
    }
  }
}
```

**Los datos no se copian solos.** `AutoMigrate` crea el esquema y los seeders
cargan los paramétricos, pero el administrador maestro hay que volver a
sembrarlo:

```bash
MASTER_ADMIN_EMAIL="tu.correo@ingenia365.com" MASTER_ADMIN_PASSWORD="tu-clave" dotnet run --project src/Presentation/IngenIA365ERP.API
```

Lo que ya tengas en el contenedor sigue intacto: es otra instalación.

## Verificar contra qué estás corriendo

La API lo dice en cada arranque, y por eso se agregó:

```
Infraestructura (Windows): MongoDB=Local · PostgreSQL=Local · Redis=Docker · Smtp=Docker · SqlServer=Local
Base de datos: proveedor PostgreSQL (origen: appsettings) — operativa host=localhost;port=5432;…
```

Con varios motores encendidos a la vez en la misma máquina, «¿esto es el local o
el del contenedor?» no es una pregunta ociosa.

## Si algo no conecta

El fallo es explícito y con reintentos, no un timeout mudo:

```
Base de datos no disponible aún (host=localhost;port=5432;database=postgres;…)
  — intento 1: 28P01: la autentificación password falló para el usuario «ingenia».
```

Y si el destino está mal escrito, ni siquiera arranca:

```
Infraestructura:Destinos:Redis apunta a 'Kubernetes'. 'Kubernetes' no es un destino
conocido. Los habituales son Local, Docker y Wsl. Definidos para Redis: Docker,
Local, Wsl. Revisá la sección Infraestructura de appsettings.<Ambiente>.json o el
appsettings.<Ambiente>.local.json de esta máquina.
```

Es deliberado que no arranque en vez de seguir con un valor por defecto: un
destino mal escrito que degradara en silencio dejaría la aplicación apuntando a
la base equivocada, y eso es peor que no arrancar.

## Agregar un servicio nuevo al esquema

En `InfraestructuraConfiguracion.Resolver` hay un `switch` por servicio. Cada
rama traduce el destino elegido a **la clave que el consumidor ya leía**. Ése es
el punto del diseño: ni Caching, ni Audit, ni Storage, ni Persistence saben que
esto existe — siguen leyendo `ConnectionStrings:Redis` como siempre.
