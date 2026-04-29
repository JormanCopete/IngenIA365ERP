# Prompt para Claude Code — Organización Final del Proyecto IngenIA365ERP

Lee CLAUDE.md para contexto completo del proyecto IngenIA365ERP.

El proyecto tiene 3 ubicaciones históricas:

**ORIGINAL (VB.NET):**
  D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\

**FASE 1 (VB.NET → C#):**
  D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\Plugins\PluginsComplete\

**FASE 2+ (IngenIA365ERP - proyecto actual):**
  D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\IngenIA365ERP\

**DESTINO FINAL:**
  D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\

Ejecuta estos pasos:

---

## PASO 1: CREAR ESTRUCTURA EN DESTINO FINAL

Crea la carpeta destino con esta estructura:

```
D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\
├── docs/                          → Toda la documentación del proyecto
│   ├── fase1-consolidacion/       → Docs de la fase VB.NET → C#
│   ├── fase2-rediseno-bd/         → Docs del rediseño de BD
│   ├── fase2-arquitectura/        → Docs de Clean Architecture
│   ├── fase2-migracion/           → Docs de migración de formularios
│   ├── fase3-deployment/          → Docs de Docker, CI/CD
│   └── INDICE-DOCUMENTACION.md    → Índice maestro de todo
│
├── src/                           → Código fuente (copiado de IngenIA365ERP actual)
│   ├── Core/
│   ├── Infrastructure/
│   ├── Presentation/
│   └── IngenIA365ERP.Shared/
│
├── legacy/                        → Referencia al código legacy
│   └── ERP.Core/                  → Copiado de PluginsComplete
│
├── tools/                         → Herramientas de migración y scripts
│   ├── Migration/
│   ├── scripts/
│   ├── IngenIA365ERP.DataMigrator/
│   └── IngenIA365ERP.DbMigrator/
│
├── tests/                         → Tests
│
├── database/                      → Scripts DDL y migración de BD
│   ├── schema/                    → Los 12 archivos DDL
│   ├── migration/                 → Scripts de migración de datos
│   └── seed/                      → Datos semilla
│
├── IngenIA365ERP.sln
├── CLAUDE.md
├── README.md
├── docker-compose.yml
├── .dockerignore
└── .github/
```

---

## PASO 2: COPIAR PROYECTO ACTUAL AL DESTINO

Copia TODO el contenido de:
  `D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\IngenIA365ERP\*`
a:
  `D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\`

Usa robocopy para copiar preservando estructura:

```powershell
robocopy "D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\IngenIA365ERP" "D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP" /E /XD bin obj .vs node_modules Backup Backup1 Backup2 Backup3 Backup4 Backup5 Backup6 Backup7 Backup8 Backup9 Backup10 Backup11 Backup12 Backup13 Backup14 Backup15 Backup16 Backup17 Backup18 Backup19 Backup20 Backup21 Backup22 Backup23 Backup24 Backup25 Backup26 Backup27
```

---

## PASO 3: ORGANIZAR DOCUMENTACIÓN EN docs/

Busca TODOS los archivos .md en estas ubicaciones y cópialos a la carpeta docs/ organizada por fase:

### DESDE `D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\Plugins\PluginsComplete\`:

Copiar a `docs/fase1-consolidacion/`:
- CLAUDE.md (renombrar a CLAUDE-FASE1.md)
- INVENTARIO.md
- CLASIFICACION.md
- DEPENDENCIAS.md
- PROGRESO.md
- CAMBIOS.md
- REPORTE-FINAL.md (renombrar a REPORTE-FINAL-FASE1.md)
- MAPA-DE-MIGRACION.md
- CONTEXTO-SOLUCION-ORIGINAL.md
- ANALISIS-CLUSTER.md

### DESDE `D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\IngenIA365ERP\` (o desde la raíz de SOLIDO si están ahí):

Copiar a `docs/fase2-rediseno-bd/`:
- PROPUESTA-REDISENO-BD.md
- MAPEO-BD-VIEJO-NUEVO.md
- MAPEO-ENTIDADES.md

Copiar a `docs/fase2-arquitectura/`:
- ESTRUCTURA-WEB.md

Copiar a `docs/fase2-migracion/`:
- ANALISIS-FORMULARIOS.md
- PROGRESO-WEB.md
- PLAN-REPORTES.md (si existe)

Copiar a `docs/fase3-deployment/`:
- REPORTE-FINAL-MIGRACION.md

---

## PASO 4: ORGANIZAR SCRIPTS DE BD EN database/

Busca los archivos SQL del DDL nuevo y cópialos a `database/schema/`:
- IngenIA365ERP_Schema_01_COR.sql
- IngenIA365ERP_Schema_02_ACC.sql
- IngenIA365ERP_Schema_03_LND.sql
- IngenIA365ERP_Schema_04_PAY.sql
- IngenIA365ERP_Schema_05_INV.sql
- IngenIA365ERP_Schema_06_CDT_DEB_TRS.sql
- IngenIA365ERP_Schema_07_SEC_AUD_WEB_ADM.sql
- IngenIA365ERP_Schema_08_ForeignKeys.sql
- IngenIA365ERP_Schema_09_Indexes.sql
- IngenIA365ERP_Schema_10_Views.sql
- IngenIA365ERP_Schema_11_Functions.sql
- IngenIA365ERP_Schema_12_SeedData.sql

Copia scripts de migración a `database/migration/`:
- Los 10 scripts 01_ al 10_ de migración de datos

Copia identity_tables.sql a `database/seed/`

También copia DBDefinicion.sql (esquema original) a `database/` como referencia.

---

## PASO 5: CREAR ÍNDICE MAESTRO DE DOCUMENTACIÓN

Crea `docs/INDICE-DOCUMENTACION.md` con este contenido:

```markdown
# IngenIA365ERP — Índice de Documentación

## Proyecto
- **Nombre**: IngenIA365ERP (anteriormente SOLIDO ERP)
- **Tipo**: ERP Financiero SaaS Multi-Tenant para Cooperativas
- **Stack**: .NET 10.0.5, Blazor Hybrid MAUI + Web, SyncFusion 33.1.44, SQL Server, MongoDB, Redis

## Ubicaciones del Proyecto

| Ubicación | Ruta | Descripción |
|---|---|---|
| **PROYECTO ACTUAL** | D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\ | Solución activa de desarrollo |
| Proyecto original VB.NET | D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\ | Solución SOLIDO original (272 formularios WinForms) |
| Fase 1 (VB.NET → C#) | D:\...\SOLIDO\Plugins\PluginsComplete\ | ERP.Core consolidado (231 archivos, 149K líneas) |
| Fase 2 (origen) | D:\...\SOLIDO\IngenIA365ERP\ | Copia de trabajo anterior (ya migrada aquí) |

## Documentación por Fase

### Fase 1: Consolidación VB.NET → C# (COMPLETADA)
| Archivo | Descripción |
|---|---|
| CLAUDE-FASE1.md | Instrucciones y progreso de Claude Code Fase 1 |
| INVENTARIO.md | Inventario de los 33 proyectos originales |
| CLASIFICACION.md | Clasificación de clases por módulo |
| DEPENDENCIAS.md | Grafo de dependencias entre proyectos |
| PROGRESO.md | Log de 8 lotes de migración |
| CAMBIOS.md | Renombramientos y decisiones |
| MAPA-DE-MIGRACION.md | Namespace viejo → nuevo |
| CONTEXTO-SOLUCION-ORIGINAL.md | Arquitectura completa del SOLIDO original |
| ANALISIS-CLUSTER.md | Análisis del cluster circular |
| REPORTE-FINAL-FASE1.md | Resumen ejecutivo Fase 1 |

### Fase 2A: Rediseño de Base de Datos (COMPLETADA)
| Archivo | Descripción |
|---|---|
| PROPUESTA-REDISENO-BD.md | Propuesta de rediseño (270 → 272 tablas) |
| MAPEO-BD-VIEJO-NUEVO.md | Mapeo completo tabla vieja → nueva |
| MAPEO-ENTIDADES.md | Tabla SQL → Entidad C# |

### Fase 2B-2D: Arquitectura y Frontend (COMPLETADA)
| Archivo | Descripción |
|---|---|
| ESTRUCTURA-WEB.md | Diagrama Clean Architecture |

### Fase 2E: Migración de Formularios (COMPLETADA)
| Archivo | Descripción |
|---|---|
| ANALISIS-FORMULARIOS.md | Análisis de 490+ formularios |
| PROGRESO-WEB.md | Progreso P1-P6 |

### Fase 3: Deployment (COMPLETADA)
| Archivo | Descripción |
|---|---|
| REPORTE-FINAL-MIGRACION.md | Resumen completo Fases 1-3 |

## Base de Datos
| Archivo | Ubicación | Descripción |
|---|---|---|
| DBDefinicion.sql | database/ | Esquema original SOLIDO |
| Schema_01 al _12 | database/schema/ | DDL nuevo IngenIA365ERP (272 tablas) |
| Migration_01 al _10 | database/migration/ | Scripts migración de datos |
| identity_tables.sql | database/seed/ | Tablas ASP.NET Identity |

## Totales del Proyecto
- 113 endpoints REST
- 136 páginas Blazor funcionales
- 16 reportes PDF (QuestPDF)
- 382 archivos Application
- 270 entidades de dominio
- 270 EF Core Configurations
- 140 DbSets
- 45 tests automatizados
- 0 errores de compilación
```

---

## PASO 6: ACTUALIZAR CLAUDE.md EN EL DESTINO

Actualiza el CLAUDE.md en la raíz del destino final agregando al inicio esta sección:

```markdown
## Ubicaciones del Proyecto
- **Proyecto activo**: D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\
- **Documentación completa**: docs/ (ver docs/INDICE-DOCUMENTACION.md)
- **Proyecto original VB.NET**: D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\solido.sln
- **ERP.Core (Fase 1)**: D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\Plugins\PluginsComplete\ERP.Core\
- **Base de datos DDL**: database/schema/
- **Scripts migración datos**: database/migration/
```

---

## PASO 7: CREAR ARCHIVO DE REFERENCIA PARA CLAUDE DESKTOP

Crea el archivo `CLAUDE-DESKTOP-INICIO.md` en la raíz del destino con este contenido:

```markdown
# Prompt Inicial para Claude Desktop — IngenIA365ERP

## Contexto
Copia y pega el prompt de abajo cuando inicies una nueva sesión 
en Claude Desktop para trabajar en el proyecto IngenIA365ERP.

---

### PROMPT:

Soy el desarrollador del proyecto IngenIA365ERP, un ERP financiero SaaS 
multi-tenant para cooperativas colombianas. El proyecto está en:

D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\

Para tener el contexto completo del proyecto, lee estos archivos en orden:

1. CLAUDE.md (en la raíz) → Estado actual, stack, convenciones, progreso
2. docs/INDICE-DOCUMENTACION.md → Índice de toda la documentación
3. README.md → Cómo ejecutar el proyecto

El proyecto tiene estas características:
- Backend: .NET 10.0.5, Minimal APIs con Carter, CQRS con MediatR
- Frontend: Blazor Hybrid MAUI + Web + WebAssembly, SyncFusion 33.1.44
- BD: SQL Server (transaccional) + MongoDB (auditoría) + Redis (caché)
- Auth: JWT RS256, 8 roles, 112 permisos
- Multi-tenancy: Schema-per-tenant
- 113 endpoints, 136 páginas Blazor, 16 reportes PDF
- Clean Architecture: Domain, Application, Infrastructure, Presentation

Ubicaciones de referencia:
- Proyecto original VB.NET: D:\...\AplicacionesDesktop\SOLIDO\solido.sln
- ERP.Core (lógica migrada): D:\...\SOLIDO\Plugins\PluginsComplete\ERP.Core\
- DDL nuevo: database/schema/ (12 archivos SQL, 272 tablas)
- Documentación completa: docs/ (organizada por fase)

Confirma que leíste los archivos y dime en qué puedo ayudarte.

---
```

---

## PASO 8: VERIFICAR

1. Verifica que la solución compila en la nueva ubicación:

```powershell
cd "D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP"
dotnet build IngenIA365ERP.sln
```

2. Cuenta los archivos copiados:

```powershell
(Get-ChildItem -Recurse -File).Count
```

3. Verifica que docs/ tiene todos los .md organizados:

```powershell
Get-ChildItem -Path docs -Recurse -Filter "*.md"
```

4. Verifica que database/ tiene los SQL:

```powershell
Get-ChildItem -Path database -Recurse -Filter "*.sql"
```

5. Verifica que no quedaron archivos de Backup:

```powershell
Get-ChildItem -Recurse -Directory -Filter "Backup*"
```

Reporta:
- Archivos copiados (total)
- Documentos .md organizados por fase (lista)
- Scripts SQL en database/ (lista)
- Compilación en nueva ubicación
- Tamaño total de la carpeta destino
