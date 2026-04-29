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
