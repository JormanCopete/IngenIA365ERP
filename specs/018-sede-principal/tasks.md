# Tareas: feature 018

- [X] T001 Especificación y plan en `specs/018-sede-principal/`
- [X] T002 [US1] `ISedePrincipalDeCooperativa` y su registro en `src/Core/IngenIA365ERP.Application/DependencyInjection.cs`
- [X] T003 [US1] Llamarla al registrar en `RegisterTenantWithAdminCommandHandler.cs`
- [X] T004 [US2] Llamarla en `ProvisionTenantSchemaCommandHandler.cs` y en `DatabaseInitializerHostedService.cs`
- [X] T005 [US3] Empresa activa para todos en `AlcanceDeSucursales.cs` y en las acciones de oficina
- [X] T006 [US3] `CreateBranchCommand` sin empresa y con `CrearAgencia`; `BranchesModule.cs`
- [X] T007 [US3] Pantallas `Administracion/Branches/Index.razor` y `Edit.razor`; menú «Oficinas»
- [X] T008 Retirar `POST /api/tenants` y `Admin/Tenants/RegisterTenant`
- [X] T009 Pruebas: `SedePrincipalDeCooperativaTests`, ajuste de `OficinasDeOtraCooperativaTests` y `AlcanceDeSucursalesTests`, e2e de `EndToEnd_MasterRegisterTenant`
- [X] T010 Manual: `docs/manual/implantacion-paso-a-paso.md`
