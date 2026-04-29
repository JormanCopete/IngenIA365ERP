# PROGRESO-WEB.md - IngenIA365ERP

Tracking de migracion de formularios WinForms a Blazor + SyncFusion.

---

## Sub-grupo P1.1: Maestros Core - 2026-03-30

### Formularios migrados (14 maestros):
| # | Maestro | Tabla Nueva | Form VB Original | Ruta Blazor |
|---|---|---|---|---|
| 1 | Agencias | COR_Branches | sys_agencia | /maestros/agencias |
| 2 | Ciudades | COR_Cities | sys_ciudad57 | /maestros/ciudades |
| 3 | Bancos | COR_Banks | sys_banco03 | /maestros/bancos |
| 4 | Centros de Costo | COR_CostCenters | sys_cencos | /maestros/centros-costo |
| 5 | Secciones | COR_Sections | sys_seccion56 | /maestros/secciones |
| 6 | Profesiones | COR_Professions | sys_profe52 | /maestros/profesiones |
| 7 | Cargos | COR_Positions | sys_cargo55 | /maestros/cargos |
| 8 | Parentescos | COR_Relationships | sys_parent51 | /maestros/parentescos |
| 9 | Motivos de Retiro | COR_WithdrawalReasons | sys_motret | /maestros/motivos-retiro |
| 10 | Enfermedades | COR_Diseases | sys_enfermedades | /maestros/enfermedades |
| 11 | Entidades | COR_Entities | sys_entidad | /maestros/entidades |
| 12 | Deportes | COR_Sports | sys_deport53 | /maestros/deportes |
| 13 | Act. Culturales | COR_CulturalActivities | sys_cultura54 | /maestros/actividades-culturales |
| 14 | Convenios | COR_Agreements | sys_convenio | /maestros/convenios |

### Backend (Application Layer):
- 43 Command files (Create + Update + Delete x 14 entidades + People existente)
- 18 Query files (List + GetById x 14 + People existente)
- 14 Validators (FluentValidation)
- 14 DTOs (records en archivos de Queries)
- 21 DbSets agregados a IApplicationDbContext

### API (Presentation Layer):
- 15 Carter endpoint files (14 maestros + People)
- 75 rutas REST: 5 por maestro (GET list, GET by id, POST, PUT, DELETE)
- Todas bajo /api/core/{recurso} con RequireAuthorization()

### Frontend (Shared):
- 14 paginas Blazor funcionales con SfGrid + SfDialog CRUD
- 1 servicio generico CoreMasterService<T>
- NavMenu actualizado con seccion "Maestros Core" (14 items)
- _Imports.razor con namespace Components.Shared

### Compilacion: 0 errores, 12 advertencias

### Estado: COMPLETADO

---

## Sub-grupo P1.2: Maestros Contabilidad + Cartera - 2026-03-31

### Contabilidad — 13 maestros migrados:
| # | Maestro | Tabla Nueva | Ruta Blazor | API Route |
|---|---|---|---|---|
| 1 | Plan de Cuentas | ACC_ChartOfAccounts | /contabilidad/plan-cuentas | /api/accounting/chart-of-accounts |
| 2 | Tipos Comprobante | ACC_VoucherTypes | /contabilidad/tipos-comprobante | /api/accounting/voucher-types |
| 3 | Periodos Contables | ACC_AccountingPeriods | /contabilidad/periodos | /api/accounting/accounting-periods |
| 4 | Grupos de Cuenta | ACC_AccountGroups | /contabilidad/grupos-cuenta | /api/accounting/account-groups |
| 5 | Subgrupos de Cuenta | ACC_AccountSubgroups | /contabilidad/subgrupos-cuenta | /api/accounting/account-subgroups |
| 6 | Categorias Riesgo | ACC_RiskCategories | /contabilidad/categorias-riesgo | /api/accounting/risk-categories |
| 7 | Lineas IVA | ACC_VatTaxLines | /contabilidad/impuestos/iva | /api/accounting/vat-tax-lines |
| 8 | Lineas Renta | ACC_IncomeTaxLines | /contabilidad/impuestos/renta | /api/accounting/income-tax-lines |
| 9 | Lineas Retefuente | ACC_WithholdingTaxLines | /contabilidad/impuestos/retefuente | /api/accounting/withholding-tax-lines |
| 10 | Lineas ICA | ACC_IcaTaxLines | /contabilidad/impuestos/ica | /api/accounting/ica-tax-lines |
| 11 | Lineas GMF | ACC_GmfTaxLines | /contabilidad/impuestos/gmf | /api/accounting/gmf-tax-lines |
| 12 | Formatos DIAN | ACC_DianReportFormats | /contabilidad/formatos-dian | /api/accounting/dian-report-formats |
| 13 | Codigos Impuestos | ACC_TaxFormCodes | /contabilidad/codigos-impuestos | /api/accounting/tax-form-codes |

### Cartera Financiera — 9 maestros migrados:
| # | Maestro | Tabla Nueva | Ruta Blazor | API Route |
|---|---|---|---|---|
| 1 | Lineas de Credito | LND_CreditLineParameters | /cartera/lineas-credito | /api/lending/credit-line-parameters |
| 2 | Codigos Movimiento | LND_TransactionCodes | /cartera/codigos-movimiento | /api/lending/transaction-codes |
| 3 | Parametros Ahorro | LND_SavingsParameters | /cartera/parametros-ahorro | /api/lending/savings-parameters |
| 4 | Tasas de Interes | LND_InterestRates | /cartera/tasas-interes | /api/lending/interest-rates |
| 5 | Parametros Provision | LND_ProvisionParameters | /cartera/parametros-provision | /api/lending/provision-parameters |
| 6 | Zonas | LND_Zones | /cartera/zonas | /api/lending/zones |
| 7 | Tipos de Zona | LND_ZoneTypes | /cartera/tipos-zona | /api/lending/zone-types |
| 8 | Parametros Scoring | LND_ScoringParameters | /cartera/scoring | /api/lending/scoring-parameters |
| 9 | Conceptos Descuento | LND_PayrollDeductionConcepts | /cartera/conceptos-descuento | /api/lending/deduction-concepts |

### Backend (Application Layer):
- 52 archivos CQRS Contabilidad (13 entidades x 4 archivos)
- 36 archivos CQRS Cartera (9 entidades x 4 archivos)
- 22 DbSets nuevos agregados a IApplicationDbContext (11 ACC + 11 LND)
- FluentValidation en todos los CreateCommand

### API (Presentation Layer):
- 13 Carter endpoints Contabilidad (65 rutas REST)
- 9 Carter endpoints Cartera (45 rutas REST)

### Frontend (Shared):
- 13 paginas Blazor funcionales Contabilidad
- 9 paginas Blazor funcionales Cartera
- NavMenu actualizado con items nuevos en ambas secciones

### Compilacion: 0 errores

### Estado: COMPLETADO

---

## Sub-grupo P1.3: Maestros Nomina + Inventario + CDT + Debito + Tesoreria + Cartera extras - 2026-03-31

### Nomina — 11 maestros:
| # | Maestro | API Route |
|---|---|---|
| 1 | Conceptos Nomina | /api/payroll/concepts |
| 2 | Periodos Pago | /api/payroll/pay-periods |
| 3 | EPS | /api/payroll/health-insurance |
| 4 | ARL | /api/payroll/work-risk |
| 5 | Tarifas ARL | /api/payroll/work-risk-rates |
| 6 | Pensiones | /api/payroll/pension-providers |
| 7 | Cesantias | /api/payroll/severance-providers |
| 8 | Cuentas Concepto | /api/payroll/concept-accounts |
| 9 | Param. Retencion | /api/payroll/withholding-parameters |
| 10 | Causas Retencion | /api/payroll/withholding-causes |
| 11 | Param. Auto-Aporte | /api/payroll/auto-contribution-params |

### Inventario — 15 maestros:
| # | Maestro | API Route |
|---|---|---|
| 1 | Productos | /api/inventory/products |
| 2 | Grupos Producto | /api/inventory/product-groups |
| 3 | Grupos Primarios | /api/inventory/primary-groups |
| 4 | Grupos Secundarios | /api/inventory/secondary-groups |
| 5 | Tipos Movimiento | /api/inventory/transaction-types |
| 6 | Bodegas | /api/inventory/warehouses |
| 7 | Ubicaciones | /api/inventory/locations |
| 8 | Puntos de Venta | /api/inventory/sales-points |
| 9 | Turnos | /api/inventory/shifts |
| 10 | Vendedores | /api/inventory/salespeople |
| 11 | Tipos Descuento | /api/inventory/discount-types |
| 12 | Tipos Lista Precios | /api/inventory/price-list-types |
| 13 | Cuentas Producto | /api/inventory/product-accounts |
| 14 | Cuentas IVA | /api/inventory/vat-accounts |
| 15 | Param. Comisiones | /api/inventory/commission-parameters |

### CDT — 2 maestros:
| # | Maestro | API Route |
|---|---|---|
| 1 | Parametros CDT | /api/cdt/parameters |
| 2 | Tasas por Plazo CDT | /api/cdt/rates-by-term |

### Debito — 3 maestros:
| # | Maestro | API Route |
|---|---|---|
| 1 | Param. Convenio | /api/debit/agreement-parameters |
| 2 | Datafonos | /api/debit/pos-terminals |
| 3 | Param. Diarios | /api/debit/daily-parameters |

### Tesoreria — 1 maestro:
| # | Maestro | API Route |
|---|---|---|
| 1 | Conceptos | /api/treasury/concepts |

### Cartera extras — 6 maestros:
| # | Maestro | API Route |
|---|---|---|
| 1 | Estados Retiro | /api/lending/withdrawal-statuses |
| 2 | Param. Vivienda | /api/lending/housing-parameters |
| 3 | Param. SIPLA | /api/lending/sipla-parameters |
| 4 | Param. Periodicidad | /api/lending/periodicity-parameters |
| 5 | Tasas por Plazo | /api/lending/term-rates |
| 6 | Cuentas Cartera | /api/lending/portfolio-accounts |

### Compilacion: 0 errores

### Estado: COMPLETADO

---

## Grupo P2: Core Contable Transaccional - 2026-03-31

### Modulos transaccionales implementados:
| # | Modulo | Tipo | API Routes | Blazor Page |
|---|---|---|---|---|
| 1 | Comprobantes Contables | TRANSACCIONAL | POST/GET/void/post /api/accounting/documents | /contabilidad/comprobantes + /lista |
| 2 | Movimientos Contables | CONSULTA | GET /api/accounting/journal-entries | /contabilidad/movimientos |
| 3 | Saldos por Cuenta | CONSULTA | GET /api/accounting/balances | /contabilidad/saldos |
| 4 | Conciliacion Bancaria | TRANSACCIONAL | GET/POST/PUT /api/accounting/bank-reconciliations | /contabilidad/conciliacion |
| 5 | Presupuestos | CRUD+CONSULTA | CRUD /api/accounting/budgets + execution | /contabilidad/presupuestos |

### Backend transaccional implementado:
- CreateDocumentCommand: validacion partida doble, periodo abierto, consecutivo automatico, actualizacion saldos
- VoidDocumentCommand: reversion de saldos, marcado como anulado
- PostDocumentCommand: contabilizacion (borrador → contabilizado)
- CreateBankReconciliationCommand: carga movimientos, conciliacion item por item
- BudgetExecutionQuery: presupuesto vs real con variacion

### Logica de negocio clave:
- Partida doble: SUM(Debitos) == SUM(Creditos) validado en Command Y Validator
- Numeracion consecutiva automatica por tipo de comprobante
- Actualizacion de saldos (ACC_AccountBalances) en cada movimiento
- Reversion de saldos al anular comprobante
- Periodo contable: valida estado abierto antes de permitir operaciones
- Conciliacion: toggle individual de items + calculo de partidas pendientes

### Frontend transaccional:
- Comprobantes: formulario encabezado+detalle con grid editable inline
  - AccountSearchDialog + PersonSearchDialog en cada linea
  - Totales en tiempo real, indicador de cuadre (verde/rojo)
  - Toolbar: Guardar/Contabilizar/Anular segun estado
- Movimientos: consulta con filtros avanzados + footer aggregates + export Excel
- Saldos: consulta por periodo con saldo anterior/debitos/creditos/nuevo saldo
- Conciliacion: layout 2 paneles (contable vs banco) + checkbox reconciliacion
- Presupuestos: SfTab con 2 pestanas (presupuesto + ejecucion) + 12 columnas mensuales

### Archivos creados: 18 CQRS + 5 Endpoints + 6 Blazor pages = 29 archivos
### 9 DbSets nuevos en IApplicationDbContext
### Compilacion: 0 errores

### Estado: COMPLETADO

---

## Grupo P3.1: Cartera Financiera — Creditos - 2026-03-31

### Modulos transaccionales implementados:
| # | Modulo | Tipo | API Routes | Blazor |
|---|---|---|---|---|
| 1 | Ficha Asociado | TRANSACCIONAL | GET /api/core/people/{id}/detail, search | /asociados/registro/{id} |
| 2 | Solicitudes Credito | TRANSACCIONAL | CRUD+approve+reject+disburse+amortization | /cartera/solicitudes + /nueva |
| 3 | Cartera Creditos | CONSULTA | GET portfolios, detail, statement, installments | /cartera/creditos + /{id} |
| 4 | Recaudos/Pagos | TRANSACCIONAL | POST payments, GET receipt | /cartera/recaudos |
| 5 | Mora y Cobro | PROCESO+CONSULTA | POST calculate, GET defaults+summary | /cartera/mora |

### Logica de negocio clave implementada:
- Solicitud → Aprobacion → Desembolso (flujo completo con validaciones)
- Desembolso: crea LoanPortfolio + genera tabla amortizacion (sistema frances) + comprobante contable
- Pagos: aplicacion en orden de prioridad (mora → intereses → capital)
- Calculo de mora: clasificacion A-E segun regulacion colombiana SFC
- Integracion contable automatica en desembolso y pagos (double-entry)
- Ficha asociado con resumen de cartera (creditos, ahorros, aportes)

### ERP.Core logica disponible para reutilizar (via adapters futuros):
- Clscartera: 25,876 lineas, 80+ metodos (GrabaMovimiento, LiquidaMora, BuscaObligacion)
- ClsLiqcreditos: 6,300+ lineas (GeneraProyeccion, BuscaSolicitud, CalcularCupo)
- Para V2: crear LendingAdapter.cs que delegue a ERP.Core para calculos complejos

### Archivos creados: 12 CQRS + 5 Endpoints + 7 Blazor = 24 archivos
### 7 DbSets nuevos
### Compilacion: 0 errores (4 fixes aplicados post-generacion)

### Estado: COMPLETADO

---

## Grupo P3.2: Cartera Financiera — Ahorros, Aportes, CDT, Retiros - 2026-03-31

### Modulos implementados:
| # | Modulo | Tipo | Endpoints | Blazor |
|---|---|---|---|---|
| 6 | Cuentas de Ahorro | TRANSACCIONAL | 7 rutas (open, deposit, withdraw, close, list, detail, statement) | /cartera/ahorros + /ahorros/{id} + /depositos + /retiros |
| 7 | Aportes Asociados | TRANSACCIONAL | 3 rutas (balance, history, process) | /cartera/aportes |
| 8 | CDT Certificados | TRANSACCIONAL | 6 rutas (create, renew, cancel, list, detail, near-expiry) | /cartera/cdt + /cdt/{id} + /cdt/nuevo |
| 9 | Retiro Asociados | PROCESO | 3 rutas (preview, process, list) | /cartera/retiros-asociado |

### Logica de negocio:
- Apertura cuenta ahorro con deposito inicial + comprobante contable
- Depositos/Retiros con validacion de saldo + comprobante contable
- CDT: calculo tasa por plazo, interes proyectado, renovacion (con/sin capitalizacion), cancelacion anticipada con penalidad 50%
- Retiro asociado: validacion no tiene creditos vigentes, preview completo

### Archivos: 12 CQRS + 4 Endpoints + 9 Blazor = 25 archivos
### 3 DbSets nuevos (ContributionReduction, AssociateWithdrawal, CertificateEntry)
### Compilacion: 0 errores

### Estado: COMPLETADO

---

## P3 COMPLETO: Cartera Financiera

P3.1: Creditos (Solicitudes, Cartera, Recaudos, Mora, Ficha Asociado)
P3.2: Ahorros, Aportes, CDT, Retiros

---

## Grupo P4: Inventario + Nomina + Debito + Tesoreria Transaccional - 2026-03-31

### Modulos implementados:
| # | Modulo | Tipo | Endpoints nuevos | Blazor pages nuevas |
|---|---|---|---|---|
| 1 | Inventario Transaccional | TRANSACCIONAL | 9 (documents CRUD+void, stock, kardex, invoices) | MovimientoInventario, Kardex + 17 masters ya existentes |
| 2 | Nomina Transaccional | TRANSACCIONAL | 7 (register, terminate, entries, process, summary, detail, payslip) | EmpleadoDetalle + Empleados/Liquidacion/Novedades reescritos |
| 3 | Tarjeta Debito | TRANSACCIONAL | 6 (issue, transaction, block, list, detail, transactions) | Tarjetas, TarjetaDetalle, MovimientosTarjeta |
| 4 | Tesoreria | TRANSACCIONAL | 7 (checks CRUD+process, invoices, cash-flow) | Cheques, FacturasTesoreria, FlujoCaja |

### Logica de negocio:
- Inventario: movimientos (E/S/T/A) con validacion de stock + costo promedio + contabilizacion automatica
- Facturacion: calculo IVA/retenciones, generacion numero factura, actualizacion stock
- Nomina: liquidacion masiva (devengados-deducciones-aportes), generacion archivo banco/PILA
- Tarjeta debito: emision, transacciones con validacion saldo cuenta ahorro, bloqueo
- Tesoreria: cheques (emision/cobro/anulacion/devolucion), CxP/CxC, flujo de caja

### Archivos: ~20 CQRS + 7 Endpoints + 12 Blazor + 11 DbSets
### Compilacion: 0 errores (1 fix en InvoiceQueries)

### Estado: COMPLETADO

---

## Grupo P5: Procesos Complejos (Batch) - 2026-03-31

### 7 procesos batch implementados:
| # | Proceso | Tipo | API Routes |
|---|---|---|---|
| 1 | Cierre Periodo Contable | PROCESO | close, reopen, close-preview, trial-balance |
| 2 | Causacion Intereses Cartera | PROCESO | execute, preview |
| 3 | Calificacion Cartera (SFC A-E) | PROCESO | execute, current, history |
| 4 | Liquidacion Intereses CDT | PROCESO | execute, preview |
| 5 | Liquidacion Intereses Ahorro | PROCESO | execute, preview |
| 6 | Descuento por Nomina | PROCESO | execute by period, preview |
| 7 | Extractos + Certificados Retencion | GENERACION | generate, get individual (4 routes) |

### Logica batch implementada:
- Cierre periodo: validacion global partida doble, cierre cuentas resultado (P13), congelamiento
- Causacion: interes corriente + moratorio por credito, comprobante contable masivo
- Calificacion: reglas SFC Colombia (A-E por dias mora + modalidad), calculo provision (1-100%)
- CDT/Ahorro: liquidacion mensual de intereses con abono automatico
- Descuento nomina: aplicacion de pagos masivos desde periodo de pago
- Extractos/Certificados: generacion masiva o consulta individual

### Componente compartido: ProcessExecutor.razor (generico para todos los procesos)
### Archivos: 15 CQRS + 8 Endpoints + 9 Blazor + 1 Component = 33 archivos
### 6 DbSets nuevos
### Compilacion: 0 errores (5 fixes post-generacion)

### Estado: COMPLETADO

---

## Grupo P6: Reportes PDF (QuestPDF) - 2026-03-31

### 16 reportes PDF implementados:
| # | Reporte | Endpoint PDF |
|---|---|---|
| 1 | Balance General | /api/reports/accounting/balance-sheet/pdf |
| 2 | Estado de Resultados | /api/reports/accounting/income-statement/pdf |
| 3 | Libro Mayor | /api/reports/accounting/general-ledger/pdf |
| 4 | Comprobante Contable | /api/reports/accounting/voucher/{id}/pdf |
| 5 | Certificado Retencion | /api/reports/accounting/withholding-certificate/{personId}/{year}/pdf |
| 6 | Extracto Credito | /api/reports/lending/loan-statement/{id}/pdf |
| 7 | Cartera por Asociado | /api/reports/lending/person-portfolio/{id}/pdf |
| 8 | Cartera por Edades | /api/reports/lending/aging-report/pdf |
| 9 | Calificacion Cartera | /api/reports/lending/classification-report/pdf |
| 10 | Extracto Ahorro | /api/reports/lending/savings-statement/{id}/pdf |
| 11 | Certificado CDT | /api/reports/cdt/certificate/{id}/pdf |
| 12 | Comprobante Nomina | /api/reports/payroll/payslip/{periodId}/{employeeId}/pdf |
| 13 | Resumen Nomina | /api/reports/payroll/summary/{periodId}/pdf |
| 14 | Certificado Laboral | /api/reports/payroll/employment-certificate/{employeeId}/pdf |
| 15 | Inventario Valorizado | /api/reports/inventory/valuation/pdf |
| 16 | Factura Venta | /api/reports/inventory/invoice/{id}/pdf |

### Archivos: 6 Queries + 15 QuestPDF Reports + 5 Endpoints + 8 Blazor = 34 archivos
### QuestPDF configurado con licencia Community
### Compilacion: 0 errores (8 fixes post-generacion)

### Estado: COMPLETADO

---

## ═══════════════════════════════════════
## FASE 2E COMPLETADA - MIGRACION DE FORMULARIOS
## ═══════════════════════════════════════

## TOTALES FINALES P1 + P2 + P3 + P4 + P5 + P6

| Metrica | Gran Total |
|---|---|
| **Archivos Application (.cs)** | **382** |
| **Carter Endpoints** | **113** |
| **QuestPDF Report generators** | **15** |
| **Paginas Blazor** | **136** |
| **Componentes compartidos** | **7** |
| **Layout files** | **4** |
| **DbSets IApplicationDbContext** | **140** |
| **Compilacion** | **0 errores** |

### Resumen por grupo:
| Grupo | Descripcion | Estado |
|---|---|---|
| P1 | 74 Maestros (Core+Contabilidad+Cartera+Nomina+Inventario+CDT+Debito+Tesoreria) | COMPLETADO |
| P2 | Contabilidad transaccional (Comprobantes+Movimientos+Saldos+Conciliacion+Presupuestos) | COMPLETADO |
| P3 | Cartera transaccional (Solicitudes+Creditos+Recaudos+Mora+Ahorros+CDT+Aportes+Retiros) | COMPLETADO |
| P4 | Inventario+Nomina+Debito+Tesoreria transaccional | COMPLETADO |
| P5 | 7 Procesos batch (Cierre+Causacion+Calificacion+Liquidaciones+Descuento+Extractos) | COMPLETADO |
| P6 | 16 Reportes PDF con QuestPDF + 8 paginas de reportes | COMPLETADO |
