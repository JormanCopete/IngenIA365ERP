# Diagnóstico de calidad de datos — para correr en cada ambiente

**Quién**: el dueño (o soporte con su autorización), en DEV, QA y producción, **antes** de
planear la migración. **Qué**: sólo lecturas (`SELECT`); ninguna consulta modifica datos.
**Por qué**: la feature agrega índices únicos y reglas de verificación; un dato sucio que hoy no
molesta puede hacer fallar la migración o dejar a alguien sin entrar.

Las consultas están en PostgreSQL. En SQL Server cambian `lower/upper` por `LOWER/UPPER`,
`regexp_replace` por `REPLACE` encadenados y las comillas dobles por corchetes. Las de la base
administrativa se corren en `IngenIA365ERP_Admin`; las de cooperativa, **en cada base de
cooperativa por separado** (Principio IV: nunca se cruzan).

Anotar el resultado de cada consulta (cantidad y, si hay filas, la lista) en el reporte de
conflictos de la feature. **No corregir a mano**: las correcciones se deciden caso por caso.

## A. Base administrativa (`IngenIA365ERP_Admin`)

### A1. Correos centrales duplicados ignorando mayúsculas y espacios

```sql
SELECT lower(trim("Email")) AS correo, count(*) AS cuantos,
       string_agg("Id"::text || ' ' || "Status"::text || ' ' || "IsDeleted"::text, ' | ') AS filas
FROM "ADM_CentralUsers"
GROUP BY lower(trim("Email"))
HAVING count(*) > 1;
```

### A2. `NormalizedEmail` inconsistente con `Email`

```sql
SELECT "Id", "Email", "NormalizedEmail"
FROM "ADM_CentralUsers"
WHERE "NormalizedEmail" IS DISTINCT FROM upper(trim("Email"))
   OR "Email" <> trim("Email");
```

### A3. `UserName` distinto del correo (el producto siempre los igualó)

```sql
SELECT "Id", "UserName", "Email"
FROM "ADM_CentralUsers"
WHERE upper(trim("UserName")) IS DISTINCT FROM upper(trim("Email"));
```

### A4. Correos sin verificar en cuentas que hoy entran

Son los que una regla «sólo correo verificado» dejaría por fuera si no se migran.

```sql
SELECT u."Id", u."Email", u."Status", u."LastLoginAt",
       count(m."Id") FILTER (WHERE m."Status" = 1 AND NOT m."IsDeleted") AS membresias_activas
FROM "ADM_CentralUsers" u
LEFT JOIN "ADM_TenantMemberships" m ON m."CentralUserId" = u."Id"
WHERE NOT u."EmailConfirmed" AND NOT u."IsDeleted"
GROUP BY u."Id", u."Email", u."Status", u."LastLoginAt";
```

### A5. Cuentas por estado, con y sin segundo factor, y maestros

```sql
SELECT "Status", "IsGlobalMasterAdmin", "TwoFactorEnabled", "EmailConfirmed", "IsDeleted", count(*)
FROM "ADM_CentralUsers"
GROUP BY 1, 2, 3, 4, 5
ORDER BY 1, 2, 3, 4, 5;
```

### A6. Invitaciones pendientes vivas y su correo frente a cuentas existentes

```sql
SELECT i."PublicId", i."TenantId", i."NormalizedEmail", i."ExpiresAt",
       (SELECT count(*) FROM "ADM_CentralUsers" u
         WHERE u."NormalizedEmail" = i."NormalizedEmail" AND NOT u."IsDeleted") AS cuentas_con_ese_correo
FROM "ADM_Invitations" i
WHERE i."Status" = 0 AND i."ExpiresAt" > now();
```

### A7. Membresías huérfanas (cuenta central borrada o inexistente)

```sql
SELECT m."PublicId", m."TenantId", m."Status"
FROM "ADM_TenantMemberships" m
LEFT JOIN "ADM_CentralUsers" u ON u."Id" = m."CentralUserId" AND NOT u."IsDeleted"
WHERE NOT m."IsDeleted" AND u."Id" IS NULL;
```

### A8. Personas con membresía en más de una cooperativa

Dimensiona cuántas correcciones de documento serán del maestro y no del administrador.

```sql
SELECT "CentralUserId", count(*) AS cooperativas
FROM "ADM_TenantMemberships"
WHERE NOT "IsDeleted" AND "Status" IN (0, 1, 2)
GROUP BY "CentralUserId"
HAVING count(*) > 1;
```

## B. En cada base de cooperativa

### B1. Usuarios sin vínculo con la identidad central

```sql
SELECT "Id", "Username", "Email", "IsActive", "IsDeleted"
FROM "SEC_Users"
WHERE "CentralUserId" IS NULL;
```

### B2. Correos de usuario duplicados en la cooperativa

```sql
SELECT lower(trim("Email")) AS correo, count(*), string_agg("Id"::text, ',')
FROM "SEC_Users"
WHERE NOT "IsDeleted" AND "Email" IS NOT NULL
GROUP BY lower(trim("Email"))
HAVING count(*) > 1;
```

### B3. Usuarios sin persona y sin documento

```sql
SELECT "Id", "Username", "Email", "PersonId", "IdentificationNumber"
FROM "SEC_Users"
WHERE NOT "IsDeleted" AND ("PersonId" IS NULL OR "IdentificationNumber" IS NULL OR trim("IdentificationNumber") = '');
```

### B4. Documento del usuario distinto del de su persona

```sql
SELECT u."Id", u."IdentificationNumber", p."IdType", p."TaxId"
FROM "SEC_Users" u
JOIN "COR_People" p ON p."Id" = u."PersonId"
WHERE NOT u."IsDeleted"
  AND u."IdentificationNumber" IS NOT NULL
  AND regexp_replace(upper(u."IdentificationNumber"), '[^0-9A-Z]', '', 'g')
      <> regexp_replace(upper(p."TaxId"), '[^0-9A-Z]', '', 'g');
```

### B5. Documentos de persona con puntos, espacios, guiones o minúsculas

```sql
SELECT "Id", "IdType", "TaxId"
FROM "COR_People"
WHERE "TaxId" ~ '[^0-9A-Z]';
```

### B6. Documentos que chocarían al normalizarlos

Dos personas que hoy son distintas sólo porque una tiene puntos o espacios.

```sql
SELECT regexp_replace(upper("TaxId"), '[^0-9A-Z]', '', 'g') AS normalizado,
       count(*), string_agg("Id"::text || ':' || "IdType" || ':' || "TaxId", ' | ')
FROM "COR_People"
GROUP BY 1
HAVING count(*) > 1;
```

### B7. Tipos de documento en uso

Dimensiona el mapeo entre el catálogo nuevo y los códigos de hoy (`C`, `CE`, `TI`, `RC`, `NI`,
`PA`, `PE`, y lo que haya llegado por migración de datos).

```sql
SELECT "IdType", "PersonType", count(*)
FROM "COR_People"
GROUP BY 1, 2
ORDER BY 3 DESC;
```

### B8. Cédulas con letras o pasaportes sin letras (posibles tipos mal puestos)

```sql
SELECT "Id", "IdType", "TaxId"
FROM "COR_People"
WHERE ("IdType" = 'C' AND "TaxId" ~ '[A-Za-z]')
   OR ("IdType" = 'C' AND length(regexp_replace("TaxId", '[^0-9]', '', 'g')) NOT BETWEEN 3 AND 10);
```

## C. Cruce entre bases (se hace fuera de la base, nunca con un join)

Para saber si una misma persona tiene **documentos distintos en dos cooperativas** hay que
exportar de cada cooperativa `(CentralUserId, IdType, TaxId normalizado)` de los usuarios con
persona (B4 sin el filtro de diferencia) y comparar los archivos. Es el insumo para decidir qué
documento sube a la identidad central en la migración. Lo prepara soporte; no es una consulta.

## D. Cómo leer el reporte de conflictos de la tarea `identidad-documento`

La tarea de una sola vez del DbMigrator (`dotnet run -- identidad-documento --output <archivo.csv>`, T021)
sube a la cuenta central el documento de quien ya tenía cuenta, tomado de la persona enlazada en cada
cooperativa. Lo que no puede decidir sola lo deja en un CSV con cuatro columnas:

| Columna | Qué es |
|---|---|
| `cuenta` | El `Id` de la cuenta central (`ADM_CentralUsers.Id`). Con él se abre en Consola SaaS → Identidades. |
| `correo` | El correo de la cuenta, para reconocer a la persona. |
| `motivo` | Por qué no se registró (ver abajo). |
| `cooperativas` | Las cooperativas que enlazan esa cuenta a una persona, separadas por « \| ». |

Los motivos y qué hacer con cada uno:

- **documentos distintos entre cooperativas**: dos cooperativas tienen a la misma cuenta enlazada a
  personas con documento distinto. Casi siempre una de las dos digitó mal la persona: se corrige la
  persona en esa cooperativa (Personas) y se vuelve a correr la tarea, o se deja que la persona registre
  su documento al entrar y el administrador lo corrija si hace falta.
- **el tipo X no sirve para entrar**: la persona enlazada es NIT o registro civil. No se registra nada; la
  persona completa su documento al entrar (paso posponible).
- **documento inválido: …**: el número no cumple la regla del tipo (longitud, letras). Se corrige la
  persona en su cooperativa.
- **el documento ya es de otra cuenta**: otra cuenta central ya tiene ese usuario. Son dos cuentas de la
  misma persona (dos correos) o un error de digitación. Soporte lo revisa en Identidades y, si son la misma
  persona, las fusiona.

Correr la tarea dos veces da el mismo reporte para lo que no se corrigió: una cuenta que ya quedó con
documento no se vuelve a mirar. Ninguna fila del reporte deja a nadie sin entrar: quien está ahí sigue
entrando con su correo como hasta hoy. El runbook `docs/operaciones/identidad-con-documento.md` dice en qué
paso del despliegue se corre.
