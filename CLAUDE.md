# CLAUDE.md — IngenIA365ERP

## Ubicaciones del Proyecto
- **Proyecto activo**: D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\
- **Documentación completa**: docs/ (ver docs/INDICE-DOCUMENTACION.md)
- **Proyecto original VB.NET**: D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\solido.sln
- **ERP.Core (Fase 1)**: D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\Plugins\PluginsComplete\ERP.Core\ (también copiado a `legacy/ERP.Core/`)
- **Base de datos DDL**: database/schema/
- **Scripts migración datos**: database/migration/

## Visión General
IngenIA365ERP es un ERP financiero SaaS multi-tenant para cooperativas colombianas, migrado desde el sistema desktop SOLIDO (VB.NET WinForms) a una arquitectura web moderna.

## Stack Tecnológico
- **Backend**: .NET 10.0.5, Minimal APIs con Carter, CQRS con MediatR
- **Frontend**: Blazor Hybrid MAUI + Web + WebAssembly, SyncFusion 33.1.44
- **BD**: SQL Server (transaccional) + MongoDB (auditoría) + Redis (caché)
- **Auth**: JWT RS256, 8 roles, 112 permisos
- **Multi-tenancy**: Schema-per-tenant
- **Reportes**: QuestPDF (16 reportes)

## Arquitectura
Clean Architecture en 4 capas:
- `src/Core/` — Domain, Application
- `src/Infrastructure/` — EF Core, repositorios, servicios externos
- `src/Presentation/` — APIs Carter, Blazor Web, Blazor MAUI

## Totales
- 113 endpoints REST
- 136 páginas Blazor funcionales
- 16 reportes PDF
- 382 archivos Application
- 270 entidades, 270 EF Core Configurations, 140 DbSets
- 45 tests automatizados
- 0 errores de compilación

## Cómo Empezar
Ver `README.md` para instrucciones de ejecución y `docs/INDICE-DOCUMENTACION.md` para la documentación completa por fase.
