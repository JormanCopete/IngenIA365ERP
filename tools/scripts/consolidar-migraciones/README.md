# Consolidar las migraciones del árbol operativo

Guiones para **regenerar** la migración base (`EsquemaBase`) el día que el dueño lo decida, sobre el último develop, y
para **comprobar** que una base creada por ella es igual a una creada por las migraciones que absorbe. La propuesta, la
evidencia del prototipo y los pasos por ambiente están en
[docs/operaciones/consolidacion-de-migraciones.md](../../../docs/operaciones/consolidacion-de-migraciones.md).

> No se corre por partes ni en un ambiente: produce un commit, y ese commit se despliega como cualquier otro.

| Archivo | Qué hace |
|---|---|
| `sqlcompleto.py` | Extrae el SQL de cada `migrationBuilder.Sql(...)` del `Up()` de las migraciones de una carpeta (lo usan los otros dos). |
| `generar_absorbidas.py` | Escribe `EsquemaBase.Absorbidas.cs` en cada motor: la lista de migraciones que la base absorbe, leída de git. |
| `generar_datos.py` | Escribe `EsquemaBase.Complementos.cs` en cada motor: `pg_trgm`, los valores por defecto heredados y las sentencias que sembraban filas fijas, copiadas de git en su orden. |
| `insertar_llamadas.py` | Agrega al `Up()` generado por EF las llamadas a esos complementos (idempotente). |
| `catalogo-pg.sql`, `catalogo-ss.sql` | Esquema de una base en líneas normalizadas (sin depender del orden físico de columnas ni de nombres que inventa el motor). |
| `datos-pg.sql`, `datos-ss.sql` | Las filas de todas las tablas, sin las columnas que cambian en cada corrida (`PublicId`, `CreatedAt`, `UpdatedAt`, `RowVersion`). |
| `medir-compilacion.ps1` | Tiempo y memoria pico de compilar un proyecto en Release (Windows). |

## Regenerar (Windows, raíz del repositorio, rama nueva desde `origin/develop`)

1. Sin migraciones pendientes de integrar en otras ramas, o integradas antes (ver la propuesta, decisión 3).
2. Sacar del árbol las migraciones operativas y la instantánea, en los dos motores:

   ```powershell
   git rm -q src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/Application/*.cs
   git rm -q src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer/Application/*.cs
   ```

3. Generar la base en cada motor (el proyecto de migraciones hace de proyecto de arranque: no hace falta compilar la API):

   ```powershell
   foreach ($t in @(@{P='PostgreSql';V='PostgreSQL'}, @{P='SqlServer';V='SqlServer'})) {
       $proyecto = "src\Infrastructure\IngenIA365ERP.Persistence.Migrations.$($t.P)"
       $env:Database__Provider = $t.V
       dotnet ef migrations add EsquemaBase --context ApplicationDbContext --project $proyecto --startup-project $proyecto --output-dir Application --configuration Release
   }
   ```

4. Comprobar que la instantánea nueva es **idéntica** a la de develop (`git diff --stat` no la lista). Si cambió, el modelo
   tenía cambios sin migración: se para y se resuelve aparte.
5. Escribir los complementos y la lista, y engancharlos:

   ```powershell
   python -I tools/scripts/consolidar-migraciones/generar_absorbidas.py . origin/develop
   python -I tools/scripts/consolidar-migraciones/generar_datos.py . origin/develop
   python -I tools/scripts/consolidar-migraciones/insertar_llamadas.py .
   ```

   `generar_datos.py` tiene dos listas a mano: `SELECCION` (qué sentencias de qué migraciones dejan filas en una base
   nueva) y `POR_DEFECTO_HEREDADOS` (los valores por defecto que el camino viejo dejó y el modelo no declara). Las dos
   salen de la comparación del paso siguiente; si develop sumó migraciones con SQL a mano o columnas con `defaultValue`,
   la comparación lo dice y se agregan.

6. Actualizar las huellas de `LasMigracionesCongeladasNoSeRegeneran` (la base cambia de marca de tiempo).

## Comprobar la equivalencia

Con una base creada por el **DbMigrator de develop** y otra por el **de la rama**, en contenedores desechables
(`postgres:17` y `mcr.microsoft.com/mssql/server:2022-latest`), `dotnet <migrador>\IngenIA365ERP.DbMigrator.dll migrate --scope tenants`
con `Database__Provider` y las dos cadenas por variable de entorno. Después:

```bash
docker exec -i <contenedor-pg> psql -U <usuario> -d <base> -tA -v ON_ERROR_STOP=1 -f - < catalogo-pg.sql > catalogo-<base>.txt
docker exec -i <contenedor-pg> psql -U <usuario> -d <base> -tA -q -v ON_ERROR_STOP=1 -f - < datos-pg.sql > datos-<base>.txt
```

En SQL Server, `sqlcmd -I -y 0 -i catalogo-ss.sql` (y `datos-ss.sql`) dentro del contenedor, con la contraseña en
`SQLCMDPASSWORD`. Las dos salidas de cada par tienen que ser **idénticas**. Lo esperado y aceptado (no sale en estas
comparaciones): el orden físico de las columnas en las tablas que crecieron con `AddColumn`.

Para la base ya migrada por el camino viejo: copiarla (`CREATE DATABASE … TEMPLATE …` / `BACKUP`+`RESTORE`), pasarle el
DbMigrator de la rama (`[Migraciones.BaseConsolidadaMarcada]`, `sin migraciones pendientes`, ningún `CREATE`/`ALTER` en
el registro) y comparar su catálogo con el de la original. Y la vuelta atrás: el DbMigrator de develop sobre una base
nueva y sobre una marcada no ve nada pendiente.
