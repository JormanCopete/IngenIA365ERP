# ANALISIS-FORMULARIOS.md -- IngenIA365ERP (SOLIDO ERP)

> **Fecha:** 2026-03-30
> **Proyecto:** IngenIA365ERP (antes SOLIDO)
> **Fase:** 2D - Analisis de Formularios WinForms para migracion a Blazor/SyncFusion
> **Origen:** D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\SOLIDO\

---

## 1. RESUMEN EJECUTIVO

### Metricas Generales

| Metrica | Valor |
|---|---|
| **Total archivos .vb (sin Designer)** | 520 |
| **Formularios en Formularios/** | 490 |
| **Modulos en Modulos/** | 23 |
| **Archivos en Aplicacion/** | 7 |
| **Total lineas de codigo** | 346,508 |
| **Lineas en Formularios/** | 328,910 |
| **Lineas en Modulos/** | 13,243 |
| **Lineas en Aplicacion/** | 4,355 |
| **Formularios con Crystal Reports** | 61 |
| **Formularios > 1,000 lineas** | 105 |
| **Formularios > 500 lineas** | 213 |
| **Formularios < 100 lineas** | 96 |
| **Formularios < 50 lineas** | 18 |

### Distribucion por Modulo

| Modulo | Prefijo | Archivos | Lineas | % Total |
|---|---|---|---|---|
| Cartera/Prestamos | cop_ | 232 | 180,921 | 55.0% |
| Contabilidad | cnt_ | 62 | 43,514 | 13.2% |
| Sistema | sys_ | 25 | 26,547 | 8.1% |
| Inventario | inv_ | 40 | 15,475 | 4.7% |
| Depositos/Ahorros | dep_ | 13 | 14,582 | 4.4% |
| Nomina | nom_ | 53 | 11,743 | 3.6% |
| Tesoreria | tes_ | 12 | 7,371 | 2.2% |
| CDTs | cdt_ | 6 | 6,971 | 2.1% |
| Servicios | ser_ | 5 | 5,778 | 1.8% |
| Tarjeta Debito | deb_ | 8 | 5,743 | 1.7% |
| Productivo | gpr_ | 17 | 4,466 | 1.4% |
| Tarjeta Credito | cre_ | 5 | 2,790 | 0.8% |
| Clientes | cli_ | 6 | 1,612 | 0.5% |
| Recaudos | rec_ | 6 | 1,397 | 0.4% |
| **Total** | | **490** | **328,910** | **100%** |

### Distribucion por Prioridad

| Prioridad | Descripcion | Formularios | Lineas Est. | % Forms |
|---|---|---|---|---|
| P1 | Maestros simples + Config + Login | 148 | ~30,000 | 30.2% |
| P2 | Contabilidad core | 38 | ~35,000 | 7.8% |
| P3 | Cartera Financiera | 52 | ~75,000 | 10.6% |
| P4 | Inventario + Nomina + CDT + Debito | 68 | ~45,000 | 13.9% |
| P5 | Procesos complejos (cierres, liquid.) | 42 | ~40,000 | 8.6% |
| P6 | Reportes (Crystal + consultas) | 98 | ~55,000 | 20.0% |
| SKIP | Stubs, bases, ya migrados | 44 | ~5,000 | 9.0% |
| **Total** | | **490** | **~285,000** | **100%** |

### Distribucion por Tipo

| Tipo | Formularios | Descripcion |
|---|---|---|
| MAESTRO | 120 | ABM de tablas maestras |
| TRANSACCIONAL | 95 | Movimientos, operaciones financieras |
| CONFIGURACION | 65 | Parametros del sistema por modulo |
| CONSULTA | 48 | Pantallas de consulta sin modificacion |
| REPORTE | 75 | Crystal Reports + listados |
| PROCESO | 45 | Cierres, liquidaciones, batch |
| FORMULARIO | 24 | Formularios auxiliares, dialogos |
| SKIP | 18 | Stubs, clases base, obsoletos |

---

## 2. TABLA COMPLETA DE FORMULARIOS

### Convenciones

- **Tipo:** MAESTRO | TRANSACCIONAL | CONFIGURACION | CONSULTA | REPORTE | PROCESO | FORMULARIO | SKIP
- **Prioridad:** P1 (facil) a P6 (reportes), SKIP (no migrar)
- **Logica Embebida:** Alta (>500 lin), Media (200-500 lin), Baja (<200 lin)
- **Crystal:** Si/No (usa Crystal Reports)

---

### 2.1 cop_ -- Cartera/Prestamos (232 formularios, 180,921 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | cop_fasoma01 | TRANSACCIONAL | Cartera | P3 | 9006 | Alta | cop_maeobli + sys_maenit | LND_LoanPortfolios + COR_People | No | /cartera/asociado-maestro |
| 2 | cop_fcaupr01 | TRANSACCIONAL | Cartera | P3 | 4164 | Alta | cop_caunov + cop_cuopen | LND_AccrualEntries + LND_PendingInstallments | Si | /cartera/causaciones |
| 3 | solicitud_credito | TRANSACCIONAL | Cartera | P3 | 4148 | Alta | cop_solicre | LND_LoanApplications | No | /cartera/solicitudes |
| 4 | cop_fparlc01 | CONFIGURACION | Cartera | P1 | 3893 | Alta | cop_concar12 | LND_CreditLineParameters | No | /cartera/config/lineas-credito |
| 5 | cop_ftcanot01 | TRANSACCIONAL | Cartera | P3 | 3891 | Alta | cop_docmto + cnt_movimto | LND_Documents + ACC_JournalEntries | No | /cartera/notas-contables |
| 6 | cop_fcreas01 | TRANSACCIONAL | Cartera | P3 | 3557 | Alta | sys_maenit + cop_maeobli | COR_People + LND_LoanPortfolios | No | /cartera/crear-asociado |
| 7 | cop_fcrelc01 | TRANSACCIONAL | Cartera | P3 | 3459 | Alta | cop_maeobli + cop_cuopen | LND_LoanPortfolios + LND_PendingInstallments | Si | /cartera/crear-credito |
| 8 | cop_feditdoc01 | TRANSACCIONAL | Cartera | P3 | 3338 | Alta | cop_docmto | LND_Documents | No | /cartera/editar-documento |
| 9 | cop_ftcapa01 | TRANSACCIONAL | Cartera | P3 | 3159 | Alta | cop_cuopen + cop_maeobli | LND_PendingInstallments + LND_LoanPortfolios | No | /cartera/pago-cuotas |
| 10 | cop_fccamc01 | TRANSACCIONAL | Cartera | P5 | 3110 | Alta | cop_cuopen | LND_PendingInstallments | No | /cartera/cambio-condiciones |
| 11 | cop_fccaec01 | TRANSACCIONAL | Cartera | P5 | 2433 | Alta | cop_cuopen | LND_PendingInstallments | No | /cartera/cambio-estado-cuenta |
| 12 | cop_adicredi | TRANSACCIONAL | Cartera | P3 | 2220 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/adicion-credito |
| 13 | cop_fsolauxaprob01 | TRANSACCIONAL | Cartera | P3 | 2203 | Alta | cop_auxilio | LND_Subsidies | No | /cartera/auxilios-aprobacion |
| 14 | cop_fgrafaso01 | REPORTE | Cartera | P6 | 2115 | Alta | sys_maenit + cop_maeobli | COR_People + LND_LoanPortfolios | Si | /reportes/grafico-asociados |
| 15 | cop_fgesmae01 | TRANSACCIONAL | Cartera | P3 | 2073 | Alta | cop_gestioncobro | LND_CollectionManagement | No | /cartera/gestion-cobro |
| 16 | cop_fmarcorre01 | PROCESO | Cartera | P5 | 1992 | Alta | cop_maeobli | LND_LoanPortfolios | Si | /cartera/proceso/marcacion |
| 17 | cop_fcreprocre01 | REPORTE | Cartera | P6 | 1963 | Alta | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/procesados |
| 18 | cop_fcauno01 | TRANSACCIONAL | Cartera | P3 | 1929 | Alta | cop_caunov | LND_AccrualEntries | No | /cartera/causacion-novedades |
| 19 | cop_fclacart01 | PROCESO | Cartera | P5 | 1689 | Alta | cop_copclas | LND_PortfolioClassifications | Si | /cartera/proceso/clasificacion |
| 20 | cop_fsolauxaprocuota | TRANSACCIONAL | Cartera | P3 | 1665 | Alta | cop_auxilio | LND_Subsidies | No | /cartera/auxilios-cuota |
| 21 | cop_factdat01 | REPORTE | Cartera | P6 | 1626 | Alta | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/datos-credito |
| 22 | cop_fasoca01 | TRANSACCIONAL | Cartera | P3 | 1577 | Alta | sys_maenit | COR_People | No | /cartera/asociado-consulta |
| 23 | seleccion | FORMULARIO | Cartera | P3 | 1528 | Alta | cop_solicre | LND_LoanApplications | No | /cartera/seleccion |
| 24 | cop_fparaux01 | CONFIGURACION | Cartera | P1 | 1492 | Alta | cop_auxilio | LND_Subsidies | No | /cartera/config/auxilios |
| 25 | cop_fcrega01 | TRANSACCIONAL | Cartera | P3 | 1490 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/reasignar-agencia |
| 26 | estudio_credito | TRANSACCIONAL | Cartera | P3 | 1485 | Alta | cop_solicre | LND_LoanApplications | No | /cartera/estudio-credito |
| 27 | cop_faprobcred01 | TRANSACCIONAL | Cartera | P3 | 1476 | Alta | cop_solicre | LND_LoanApplications | No | /cartera/aprobar-credito |
| 28 | cop_fcreof01 | TRANSACCIONAL | Cartera | P3 | 1438 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/crear-obligacion |
| 29 | cop_fparem01 | CONFIGURACION | Cartera | P1 | 1419 | Alta | cop_empresa13 | COR_EmployerCompanies | No | /cartera/config/empresas |
| 30 | cop_fcontclasif01 | PROCESO | Cartera | P5 | 1375 | Alta | cop_copclas | LND_PortfolioClassifications | No | /cartera/proceso/contab-clasificacion |
| 31 | cop_fresmov01 | REPORTE | Cartera | P6 | 1312 | Alta | cop_docmto | LND_Documents | Si | /reportes/cartera/resumen-movimientos |
| 32 | cop_fnomcuencobro01 | REPORTE | Cartera | P6 | 1247 | Alta | cop_cuencobro | LND_CollectionAccounts | Si | /reportes/cartera/cuentas-cobro |
| 33 | cop_ftcaac01 | TRANSACCIONAL | Cartera | P3 | 1240 | Alta | cop_cuopen + cop_maeobli | LND_PendingInstallments | No | /cartera/abono-capital |
| 34 | cop_frepitedoc01 | REPORTE | Cartera | P6 | 1224 | Alta | cop_docmto | LND_Documents | No | /reportes/cartera/reimprimir-doc |
| 35 | cop_fcremoint01 | TRANSACCIONAL | Cartera | P3 | 1221 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/mora-intereses |
| 36 | cop_festbenef01 | CONSULTA | Cartera | P3 | 1219 | Alta | cop_benef | COR_Beneficiaries | No | /cartera/beneficiarios |
| 37 | cop_fmoviaso01 | REPORTE | Cartera | P6 | 1212 | Alta | cop_docmto | LND_Documents | Si | /reportes/cartera/movimientos-asociado |
| 38 | cop_fcircobro01 | PROCESO | Cartera | P5 | 1207 | Alta | cop_detcircobro | LND_CollectionNoticeDetails | No | /cartera/proceso/circulares-cobro |
| 39 | cop_fparcf01 | CONFIGURACION | Cartera | P1 | 1201 | Alta | cop_concar12 | LND_CreditLineParameters | No | /cartera/config/condiciones-financieras |
| 40 | cop_fcresegucar01 | TRANSACCIONAL | Cartera | P3 | 1200 | Alta | cop_seguros | LND_InsurancePolicies | No | /cartera/seguros-cartera |
| 41 | cop_fextraccopto01 | REPORTE | Cartera | P6 | 1190 | Alta | cop_maeobli + cop_docmto | LND_LoanPortfolios | No | /reportes/cartera/extracto |
| 42 | cop_fcredf01 | TRANSACCIONAL | Cartera | P3 | 1164 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/desembolso-fondo |
| 43 | cop_fnomim01 | REPORTE | Cartera | P6 | 1156 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/nomina-imprimir |
| 44 | cop_fsolaux01 | TRANSACCIONAL | Cartera | P3 | 1155 | Alta | cop_auxilio | LND_Subsidies | No | /cartera/solicitar-auxilio |
| 45 | cop_ftrasconta01 | PROCESO | Cartera | P5 | 1146 | Alta | cop_docmto + cnt_movimto | LND_Documents + ACC_JournalEntries | No | /cartera/proceso/traslado-contable |
| 46 | cop_fasomcj01 | TRANSACCIONAL | Cartera | P3 | 1136 | Alta | sys_maenit | COR_People | No | /cartera/asociado-menor-cuantia |
| 47 | cop_fprocre01 | PROCESO | Cartera | P5 | 1134 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/proyeccion-credito |
| 48 | cop_fclacorlarpla01 | PROCESO | Cartera | P5 | 1109 | Alta | cop_carteraclp | LND_ShortLongTermPortfolio | No | /cartera/proceso/clasif-corto-largo |
| 49 | cop_fpazysalvo01 | REPORTE | Cartera | P6 | 1081 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/paz-y-salvo |
| 50 | cop_fcaupa01 | TRANSACCIONAL | Cartera | P3 | 1053 | Alta | cop_caunov | LND_AccrualEntries | No | /cartera/causacion-pago |
| 51 | cop_fnomet01 | REPORTE | Cartera | P6 | 1045 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/nomina-extracto |
| 52 | cop_flavacti01 | REPORTE | Cartera | P6 | 1022 | Alta | cop_declavado | LND_MoneyLaunderingDeclarations | Si | /reportes/cartera/lavado-activos |
| 53 | cop_fnomre01 | REPORTE | Cartera | P6 | 1019 | Alta | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/nomina-resumen |
| 54 | cop_fnomep01 | REPORTE | Cartera | P6 | 1016 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/nomina-especial |
| 55 | cop_fnompl01 | REPORTE | Cartera | P6 | 999 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/nomina-plano |
| 56 | cop_fcirclineas | REPORTE | Cartera | P6 | 997 | Alta | cop_concar12 | LND_CreditLineParameters | Si | /reportes/cartera/circular-lineas |
| 57 | cop_fasoplano01 | PROCESO | Cartera | P5 | 997 | Alta | sys_maenit | COR_People | No | /cartera/proceso/plano-asociados |
| 58 | cop_fnomde01 | REPORTE | Cartera | P6 | 993 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/nomina-descuento |
| 59 | cop_fparfactura | CONFIGURACION | Cartera | P1 | 946 | Alta | cop_detallefactura | LND_InvoiceDetails | No | /cartera/config/factura |
| 60 | cop_fnomcosanta | REPORTE | Cartera | P6 | 904 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/cosecha-anterior |
| 61 | cop_finfcuopen01 | REPORTE | Cartera | P6 | 898 | Alta | cop_cuopen | LND_PendingInstallments | Si | /reportes/cartera/cuotas-pendientes |
| 62 | cop_fnomvd01 | REPORTE | Cartera | P6 | 895 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/nomina-varios |
| 63 | recoge_credito | TRANSACCIONAL | Cartera | P3 | 889 | Alta | cop_solicre | LND_LoanApplications | No | /cartera/recoge-credito |
| 64 | cop_fcuencobro01 | TRANSACCIONAL | Cartera | P3 | 884 | Alta | cop_cuencobro | LND_CollectionAccounts | Si | /cartera/cuentas-cobro |
| 65 | ver_movtos | CONSULTA | Cartera | P3 | 882 | Alta | cop_docmto | LND_Documents | No | /cartera/ver-movimientos |
| 66 | cop_paracobra01 | CONFIGURACION | Cartera | P1 | 873 | Alta | cop_gestioncobro | LND_CollectionManagement | No | /cartera/config/cobranza |
| 67 | cop_fgestcobro01 | TRANSACCIONAL | Cartera | P3 | 871 | Alta | cop_gestioncobro | LND_CollectionManagement | No | /cartera/gestion-cobro-detalle |
| 68 | cop_fasora01 | TRANSACCIONAL | Cartera | P3 | 870 | Alta | sys_maenit | COR_People | No | /cartera/asociado-rapido |
| 69 | cop_fcartedades01 | REPORTE | Cartera | P6 | 869 | Alta | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/edades |
| 70 | cop_fparfactura02 | CONFIGURACION | Cartera | P1 | 861 | Alta | cop_detallefactura | LND_InvoiceDetails | No | /cartera/config/factura-2 |
| 71 | consul_cupo | CONSULTA | Cartera | P3 | 854 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/consulta-cupo |
| 72 | cop_fcalcuVacacion01 | TRANSACCIONAL | Cartera | P3 | 827 | Alta | cop_vacaciones | LND_VacationBenefits | No | /cartera/calculo-vacaciones |
| 73 | cop_fextpact01 | REPORTE | Cartera | P6 | 819 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/extracto-pacto |
| 74 | cop_finfret01 | REPORTE | Cartera | P6 | 818 | Alta | sys_maenit | COR_People | Si | /reportes/cartera/informacion-retiro |
| 75 | cop_fgenfact02 | PROCESO | Cartera | P5 | 813 | Alta | cop_detallefactura | LND_InvoiceDetails | Si | /cartera/proceso/generar-factura |
| 76 | cop_fdatbacre01 | TRANSACCIONAL | Cartera | P3 | 808 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/datos-basicos-credito |
| 77 | cop_fnomco01 | REPORTE | Cartera | P6 | 804 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/nomina-conceptos |
| 78 | cop_fpresempciu01 | CONSULTA | Cartera | P3 | 798 | Alta | cop_empresa13 | COR_EmployerCompanies | No | /cartera/consulta-empresas |
| 79 | cop_fpartm01 | CONFIGURACION | Cartera | P1 | 790 | Alta | cop_codmov | LND_TransactionCodes | No | /cartera/config/tipos-movimiento |
| 80 | cop_fcauaju01 | TRANSACCIONAL | Cartera | P3 | 779 | Alta | cop_caunov | LND_AccrualEntries | No | /cartera/causacion-ajuste |
| 81 | cop_fconcuope01 | REPORTE | Cartera | P6 | 761 | Alta | cop_cuopen | LND_PendingInstallments | Si | /reportes/cartera/cuotas-operacion |
| 82 | cop_findrotacion01 | CONSULTA | Cartera | P3 | 759 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/indice-rotacion |
| 83 | cop_flibaso01 | CONSULTA | Cartera | P3 | 754 | Alta | sys_maenit | COR_People | No | /cartera/libro-asociados |
| 84 | cop_flibauxint01 | REPORTE | Cartera | P6 | 749 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/auxiliar-intereses |
| 85 | cop_farcifin01 | PROCESO | Cartera | P5 | 736 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/archivo-cifin |
| 86 | cop_ftcalc01 | REPORTE | Cartera | P6 | 733 | Alta | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/tabla-calculo |
| 87 | cop_fobliultcuo01 | CONSULTA | Cartera | P3 | 728 | Alta | cop_cuopen | LND_PendingInstallments | No | /cartera/ultima-cuota |
| 88 | cop_fcamcuo01 | TRANSACCIONAL | Cartera | P5 | 725 | Alta | cop_cuopen | LND_PendingInstallments | No | /cartera/cambio-cuota |
| 89 | cop_fcrereccuo01 | TRANSACCIONAL | Cartera | P3 | 709 | Alta | cop_cuopen | LND_PendingInstallments | No | /cartera/recalcular-cuotas |
| 90 | cop_proyerecau01 | REPORTE | Cartera | P6 | 706 | Alta | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/proyeccion-recaudo |
| 91 | cop_fcompclacar01 | PROCESO | Cartera | P5 | 701 | Alta | cop_copclas | LND_PortfolioClassifications | No | /cartera/proceso/comp-clasificacion |
| 92 | cop_ftescptos | CONFIGURACION | Cartera | P1 | 700 | Alta | cop_codmov | LND_TransactionCodes | No | /cartera/config/tesoreria-conceptos |
| 93 | cop_fliscau01 | CONSULTA | Cartera | P3 | 686 | Alta | cop_caunov | LND_AccrualEntries | No | /cartera/listado-causaciones |
| 94 | cop_fliqmora01 | PROCESO | Cartera | P5 | 683 | Alta | cop_copmora | LND_DefaultRecords | Si | /cartera/proceso/liquidar-mora |
| 95 | cop_fcremocu01 | TRANSACCIONAL | Cartera | P3 | 681 | Alta | cop_cuopen | LND_PendingInstallments | No | /cartera/modificar-cuota |
| 96 | cop_ftcacc01 | REPORTE | Cartera | P6 | 672 | Alta | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/tabla-amort-cc |
| 97 | cop_fsalconce01 | CONSULTA | Cartera | P3 | 672 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/saldo-conceptos |
| 98 | cop_fparamscoring01 | CONFIGURACION | Cartera | P1 | 668 | Alta | cop_scoring | LND_ScoringParameters | No | /cartera/config/scoring |
| 99 | cop_fnommoddesc01 | REPORTE | Cartera | P6 | 649 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/nomina-mod-descuento |
| 100 | cop_farcsupersoli01 | PROCESO | Cartera | P5 | 648 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/archivo-supersolidaria |
| 101 | cop_fliscaunov01 | CONSULTA | Cartera | P3 | 642 | Alta | cop_caunov | LND_AccrualEntries | No | /cartera/listado-caus-novedades |
| 102 | cop_fparviv01 | CONFIGURACION | Cartera | P1 | 638 | Alta | cop_vivienda | LND_HousingParameters | No | /cartera/config/vivienda |
| 103 | cop_farcdat01 | PROCESO | Cartera | P5 | 624 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/archivo-datos |
| 104 | cop_frepaux01 | REPORTE | Cartera | P6 | 616 | Alta | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/reporte-auxiliar |
| 105 | cop_graba_domto_prestamo | TRANSACCIONAL | Cartera | P3 | 610 | Alta | cop_docmto | LND_Documents | No | /cartera/grabar-documento |
| 106 | cop_repcobra01 | REPORTE | Cartera | P6 | 602 | Alta | cop_gestioncobro | LND_CollectionManagement | Si | /reportes/cartera/reporte-cobranza |
| 107 | cop_fparasesor01 | CONFIGURACION | Cartera | P1 | 578 | Alta | cop_asesores | COR_Advisors | No | /cartera/config/asesores |
| 108 | cop_fparcuap01 | CONFIGURACION | Cartera | P1 | 577 | Alta | cop_cuoant | LND_PreviousInstallments | No | /cartera/config/cuotas-anteriores |
| 109 | cop_fparase01 | CONFIGURACION | Cartera | P1 | 576 | Alta | cop_asesores | COR_Advisors | No | /cartera/config/asesores-ext |
| 110 | cop_fparcircu01 | CONFIGURACION | Cartera | P1 | 559 | Alta | cop_detcircobro | LND_CollectionNoticeDetails | No | /cartera/config/circulares |
| 111 | cop_fbasendeu01 | PROCESO | Cartera | P5 | 554 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/base-endeudamiento |
| 112 | cop_farcovinoc01 | PROCESO | Cartera | P5 | 548 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/archivo-vinculos |
| 113 | cop_fdisfondo01 | PROCESO | Cartera | P5 | 545 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/distribucion-fondo |
| 114 | cop_fconcext01 | REPORTE | Cartera | P6 | 539 | Alta | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/conceptos-extracto |
| 115 | cop_factirecrea01 | TRANSACCIONAL | Cartera | P3 | 535 | Alta | cop_actirecrea | LND_AssociateActivities | No | /cartera/actividades-recreacion |
| 116 | cop_ftrasladocpto01 | TRANSACCIONAL | Cartera | P3 | 532 | Alta | cop_docmto | LND_Documents | No | /cartera/traslado-concepto |
| 117 | cop_fparac01 | CONFIGURACION | Cartera | P1 | 527 | Alta | cop_actirecrea | LND_AssociateActivities | No | /cartera/config/actividades |
| 118 | cop_fparad01 | CONFIGURACION | Cartera | P1 | 526 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/config/descuentos |
| 119 | cop_frepbene01 | REPORTE | Cartera | P6 | 525 | Alta | cop_benef | COR_Beneficiaries | No | /reportes/cartera/beneficiarios |
| 120 | cop_fparasejuri01 | CONFIGURACION | Cartera | P1 | 522 | Alta | cop_asesores | COR_Advisors | No | /cartera/config/asesor-juridico |
| 121 | cop_fsipla | PROCESO | Cartera | P5 | 508 | Alta | cop_Sipla_Novedades | LND_UnusualTransactionEntries | No | /cartera/proceso/sipla |
| 122 | extras | FORMULARIO | Cartera | P3 | 506 | Alta | cop_maeobli | LND_LoanPortfolios | No | /cartera/extras |
| 123 | cop_fparpa01 | CONFIGURACION | Cartera | P1 | 505 | Alta | cop_ahorro58 | LND_SavingsParameters | No | /cartera/config/parametros-ahorro |
| 124 | cop_finfmora01 | CONSULTA | Cartera | P6 | 504 | Alta | cop_copmora | LND_DefaultRecords | No | /reportes/cartera/informe-mora |
| 125 | cop_fparmr01 | CONFIGURACION | Cartera | P1 | 502 | Alta | cop_codmov | LND_TransactionCodes | No | /cartera/config/movimientos-recaudo |
| 126 | cop_fparag01 | CONFIGURACION | Cartera | P1 | 496 | Media | cop_concar12 | LND_CreditLineParameters | No | /cartera/config/garantias |
| 127 | ayuda_credito | FORMULARIO | Cartera | P3 | 491 | Media | cop_solicre | LND_LoanApplications | No | /cartera/ayuda-credito |
| 128 | cop_fconmaes01 | CONSULTA | Cartera | P3 | 489 | Media | cop_maeobli | LND_LoanPortfolios | No | /cartera/consulta-maestro |
| 129 | cop_fnomaplinoapli01 | REPORTE | Cartera | P6 | 486 | Media | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/nomina-aplicados |
| 130 | cop_fparpr01 | CONFIGURACION | Cartera | P1 | 473 | Media | cop_maeobli | LND_LoanPortfolios | No | /cartera/config/prestamos |
| 131 | cop_fcambifecextr01 | TRANSACCIONAL | Cartera | P3 | 460 | Media | cop_maeobli | LND_LoanPortfolios | No | /cartera/cambio-fecha-extracto |
| 132 | cop_fparse01 | CONFIGURACION | Cartera | P1 | 455 | Media | cop_seguros | LND_InsurancePolicies | No | /cartera/config/seguros |
| 133 | ver_liquida | CONSULTA | Cartera | P3 | 454 | Media | cop_maeobli | LND_LoanPortfolios | No | /cartera/ver-liquidacion |
| 134 | activiades_asociado | CONSULTA | Cartera | P3 | 444 | Media | cop_actiaso | LND_AssociateActivities | No | /cartera/actividades-asociado |
| 135 | cop_frelapresapo01 | REPORTE | Cartera | P6 | 442 | Media | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/relacion-aportes |
| 136 | cop_freprefe01 | REPORTE | Cartera | P6 | 437 | Media | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/referencia |
| 137 | cop_frepase01 | REPORTE | Cartera | P6 | 433 | Media | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/reporte-asesores |
| 138 | cop_fparamsipla | CONFIGURACION | Cartera | P1 | 433 | Media | cop_Sipla_Novedades | LND_UnusualTransactionEntries | No | /cartera/config/sipla |
| 139 | cop_fcierre01 | PROCESO | Cartera | P5 | 430 | Media | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/cierre |
| 140 | cop_fauditacuotas | CONSULTA | Cartera | P5 | 425 | Media | cop_cuopen | LND_PendingInstallments | No | /cartera/auditoria-cuotas |
| 141 | cop_fnovactividad01 | TRANSACCIONAL | Cartera | P3 | 413 | Media | cop_actiaso | LND_AssociateActivities | No | /cartera/novedades-actividad |
| 142 | cop_fbenefseg01 | TRANSACCIONAL | Cartera | P3 | 412 | Media | cop_benefseg | LND_InsuranceBeneficiaries | No | /cartera/beneficiarios-seguro |
| 143 | cop_fimpactiv01 | REPORTE | Cartera | P6 | 399 | Media | cop_actiaso | LND_AssociateActivities | Si | /reportes/cartera/imprimir-actividades |
| 144 | cop_freslinea01 | CONSULTA | Cartera | P3 | 382 | Media | cop_concar12 | LND_CreditLineParameters | No | /cartera/resumen-linea |
| 145 | cop_fasocarn01 | REPORTE | Cartera | P6 | 364 | Media | sys_maenit | COR_People | Si | /reportes/cartera/carnets |
| 146 | cop_festretiro01 | PROCESO | Cartera | P5 | 361 | Media | sys_maenit | COR_People | Si | /cartera/proceso/retiro |
| 147 | cop_fimpextracto01 | REPORTE | Cartera | P6 | 354 | Media | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/imprimir-extracto |
| 148 | cop_factcuentas01 | TRANSACCIONAL | Cartera | P3 | 339 | Media | cop_maeobli | LND_LoanPortfolios | No | /cartera/actualizar-cuentas |
| 149 | cop_fmovcart01 | CONSULTA | Cartera | P3 | 335 | Media | cop_docmto | LND_Documents | No | /cartera/movimientos-cartera |
| 150 | cop_finfsoli01 | REPORTE | Cartera | P6 | 319 | Media | cop_solicre | LND_LoanApplications | Si | /reportes/cartera/info-solicitudes |
| 151 | cop_fexporplanopost | PROCESO | Cartera | P5 | 314 | Media | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/exportar-plano |
| 152 | cop_factas01 | TRANSACCIONAL | Cartera | P3 | 303 | Media | cop_acta | LND_Minutes | No | /cartera/actas |
| 153 | cop_finfestjuri01 | CONSULTA | Cartera | P6 | 301 | Media | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/estado-juridico |
| 154 | cop_fresuminte01 | CONSULTA | Cartera | P6 | 295 | Media | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/resumen-intereses |
| 155 | cop_frecreacion01 | TRANSACCIONAL | Cartera | P3 | 265 | Media | cop_actirecrea | LND_AssociateActivities | No | /cartera/recreacion |
| 156 | cop_fasolista01 | CONSULTA | Cartera | P3 | 248 | Media | sys_maenit | COR_People | No | /cartera/listado-asociados |
| 157 | cop_fauxconcep01 | CONSULTA | Cartera | P3 | 245 | Media | cop_auxilio | LND_Subsidies | No | /cartera/auxilios-concepto |
| 158 | cop_fapliredapor01 | REPORTE | Cartera | P6 | 237 | Media | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/aplicar-aportes |
| 159 | cop_fgenfact01 | PROCESO | Cartera | P5 | 229 | Media | cop_detallefactura | LND_InvoiceDetails | No | /cartera/proceso/generar-factura-v1 |
| 160 | cop_fRecDatos01 | TRANSACCIONAL | Cartera | P3 | 219 | Media | sys_maenit | COR_People | No | /cartera/recoger-datos |
| 161 | cop_fparcurso01 | CONFIGURACION | Cartera | P1 | 218 | Media | cop_actirecrea | LND_AssociateActivities | No | /cartera/config/cursos |
| 162 | cop_flistcumple01 | REPORTE | Cartera | P6 | 218 | Media | sys_maenit | COR_People | Si | /reportes/cartera/cumpleanos |
| 163 | cop_festudcred01 | CONSULTA | Cartera | P3 | 218 | Media | cop_solicre | LND_LoanApplications | No | /cartera/estudio-credito-consulta |
| 164 | cop_fcertrent01 | REPORTE | Cartera | P6 | 212 | Media | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/certificado-renta |
| 165 | cop_fsalglob01 | CONSULTA | Cartera | P6 | 207 | Media | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/saldo-global |
| 166 | cop_fimpCircobro01 | REPORTE | Cartera | P6 | 203 | Media | cop_detcircobro | LND_CollectionNoticeDetails | No | /reportes/cartera/imprimir-circular |
| 167 | cop_finfocodeudore01 | REPORTE | Cartera | P6 | 202 | Media | cop_codeudores | LND_CoSigners | Si | /reportes/cartera/codeudores |
| 168 | cop_fimpcertif01 | REPORTE | Cartera | P6 | 193 | Baja | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/imprimir-certificado |
| 169 | cop_finffactura01 | CONSULTA | Cartera | P6 | 192 | Baja | cop_detallefactura | LND_InvoiceDetails | No | /reportes/cartera/info-factura |
| 170 | cop_finfexclu01 | CONSULTA | Cartera | P6 | 190 | Baja | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/exclusiones |
| 171 | cop_fparredaportes01 | CONFIGURACION | Cartera | P1 | 184 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/config/aportes |
| 172 | credi_extras | FORMULARIO | Cartera | P3 | 183 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/credito-extras |
| 173 | cop_fcamempre01 | TRANSACCIONAL | Cartera | P1 | 183 | Baja | cop_empresa13 | COR_EmployerCompanies | No | /cartera/cambio-empresa |
| 174 | cop_finfcuoant01 | CONSULTA | Cartera | P6 | 180 | Baja | cop_cuoant | LND_PreviousInstallments | No | /reportes/cartera/cuotas-anteriores |
| 175 | cop_farcpig01 | PROCESO | Cartera | P5 | 172 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/archivo-pignorado |
| 176 | cop_factcuofijas | TRANSACCIONAL | Cartera | P3 | 172 | Baja | cop_cuopen | LND_PendingInstallments | No | /cartera/actualizar-cuotas-fijas |
| 177 | cop_finfcobranza01 | REPORTE | Cartera | P6 | 169 | Baja | cop_gestioncobro | LND_CollectionManagement | Si | /reportes/cartera/info-cobranza |
| 178 | cop_fsaldprom01 | CONSULTA | Cartera | P6 | 164 | Baja | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/saldo-promedio |
| 179 | cop_faudmaca01 | CONSULTA | Cartera | P1 | 162 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/auditoria-maestro |
| 180 | cop_faudcupe01 | CONSULTA | Cartera | P1 | 157 | Baja | cop_cuopen | LND_PendingInstallments | No | /cartera/auditoria-cuotas-pen |
| 181 | cop_finfrefeaso01 | CONSULTA | Cartera | P6 | 154 | Baja | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/referencia-asociado |
| 182 | cop_fcertiaportes01 | REPORTE | Cartera | P6 | 153 | Baja | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/certificado-aportes |
| 183 | cop_frsiplainusuales | REPORTE | Cartera | P6 | 148 | Baja | cop_Sipla_Novedades | LND_UnusualTransactionEntries | No | /reportes/cartera/sipla-inusuales |
| 184 | cop_fcgbatch01 | PROCESO | Cartera | P5 | 141 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/cg-batch |
| 185 | cop_fcamdocbenef01 | TRANSACCIONAL | Cartera | P1 | 141 | Baja | cop_benef | COR_Beneficiaries | No | /cartera/cambio-doc-beneficiario |
| 186 | cop_fcptosinmovto01 | CONSULTA | Cartera | P6 | 139 | Baja | cop_codmov | LND_TransactionCodes | No | /reportes/cartera/conceptos-sin-movimiento |
| 187 | cop_fzonas01 | MAESTRO | Cartera | P1 | 136 | Baja | cop_Tipozonas | LND_ZoneTypes | No | /cartera/maestros/zonas |
| 188 | frmcopmora | CONSULTA | Cartera | P3 | 135 | Baja | cop_copmora | LND_DefaultRecords | No | /cartera/consulta-mora |
| 189 | cop_finfservenci01 | REPORTE | Cartera | P6 | 133 | Baja | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/servicios-vencidos |
| 190 | cop_fimplistas01 | REPORTE | Cartera | P6 | 123 | Baja | cop_maeobli | LND_LoanPortfolios | No | /reportes/cartera/imprimir-listas |
| 191 | cop_fauxcuotacau01 | CONSULTA | Cartera | P3 | 123 | Baja | cop_caunov | LND_AccrualEntries | No | /cartera/auxiliar-cuota-causacion |
| 192 | cop_fproasosuper01 | PROCESO | Cartera | P5 | 122 | Baja | sys_maenit | COR_People | No | /cartera/proceso/asociados-super |
| 193 | cop_fcauintant01 | CONSULTA | Cartera | P3 | 113 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/causacion-intereses-ant |
| 194 | cop_finfgestion01 | REPORTE | Cartera | P6 | 112 | Baja | cop_gestioncobro | LND_CollectionManagement | Si | /reportes/cartera/info-gestion |
| 195 | cop_fcomite01 | MAESTRO | Cartera | P1 | 112 | Baja | cop_comite | COR_Committees | No | /cartera/maestros/comites |
| 196 | cop_fcumpleparen01 | CONSULTA | Cartera | P6 | 110 | Baja | sys_maenit | COR_People | No | /reportes/cartera/cumpleanos-parentesco |
| 197 | cop_fcomiteasoc | MAESTRO | Cartera | P1 | 105 | Baja | cop_comiteasoc | COR_CommitteeMembers | No | /cartera/maestros/comite-asociados |
| 198 | cop_fplafact01 | PROCESO | Cartera | P5 | 104 | Baja | cop_detallefactura | LND_InvoiceDetails | No | /cartera/proceso/plano-factura |
| 199 | cop_fmovtrascont01 | CONSULTA | Cartera | P3 | 103 | Baja | cop_docmto | LND_Documents | No | /cartera/movimiento-traslado-cont |
| 200 | cop_finfestretiro01 | CONSULTA | Cartera | P6 | 102 | Baja | sys_maenit | COR_People | No | /reportes/cartera/estado-retiro |
| 201 | cop_enfermedad | MAESTRO | Cartera | P1 | 97 | Baja | cop_enfermedadAsoc | LND_AssociateDiseases | No | /cartera/maestros/enfermedades |
| 202 | cop_fprogramaact01 | MAESTRO | Cartera | P1 | 93 | Baja | cop_actirecrea | LND_AssociateActivities | No | /cartera/maestros/programas-actividad |
| 203 | cop_finscoring01 | CONSULTA | Cartera | P3 | 92 | Baja | cop_scoring | LND_ScoringParameters | No | /cartera/info-scoring |
| 204 | cop_impdocu01 | REPORTE | Cartera | P6 | 90 | Baja | cop_docmto | LND_Documents | No | /reportes/cartera/imprimir-documento |
| 205 | cop_fparentidad01 | CONFIGURACION | Cartera | P1 | 90 | Baja | cop_concar12 | LND_CreditLineParameters | No | /cartera/config/entidades |
| 206 | cop_fcaphuella | TRANSACCIONAL | Cartera | P3 | 89 | Baja | sys_maenit | COR_People | No | /cartera/captura-huella |
| 207 | cop_faudmaas01 | CONSULTA | Cartera | P1 | 87 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/auditoria-maestro-asoc |
| 208 | cop_faudlicr01 | CONSULTA | Cartera | P1 | 81 | Baja | cop_concar12 | LND_CreditLineParameters | No | /cartera/auditoria-lineas-credito |
| 209 | cop_factsoli01 | TRANSACCIONAL | Cartera | P3 | 78 | Baja | cop_solicre | LND_LoanApplications | No | /cartera/actualizar-solicitud |
| 210 | cop_fsalxint01 | REPORTE | Cartera | P6 | 76 | Baja | cop_maeobli | LND_LoanPortfolios | Si | /reportes/cartera/saldo-por-intervalo |
| 211 | ver_codeudor | CONSULTA | Cartera | P3 | 72 | Baja | cop_codeudores | LND_CoSigners | No | /cartera/ver-codeudor |
| 212 | cop_ftipozonas01 | MAESTRO | Cartera | P1 | 71 | Baja | cop_Tipozonas | LND_ZoneTypes | No | /cartera/maestros/tipos-zona |
| 213 | cop_fsubzonas01 | MAESTRO | Cartera | P1 | 71 | Baja | cop_Tipozonas | LND_ZoneTypes | No | /cartera/maestros/sub-zonas |
| 214 | cop_factusalario01 | TRANSACCIONAL | Cartera | P1 | 69 | Baja | sys_maenit | COR_People | No | /cartera/actualizar-salario |
| 215 | cop_frecsal01 | CONSULTA | Cartera | P3 | 66 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/recaudo-saldo |
| 216 | frmreproceso | PROCESO | Cartera | P5 | 55 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/reproceso |
| 217 | cop_fcuacarcon01 | CONSULTA | Cartera | P3 | 52 | Baja | cop_copclas | LND_PortfolioClassifications | No | /cartera/cuadre-cartera-contable |
| 218 | Mues_esta_cta | CONSULTA | Cartera | P3 | 47 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/muestra-estado-cuenta |
| 219 | cop_frestru01 | PROCESO | Cartera | P5 | 44 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/proceso/reestructuracion |
| 220 | calcu_presta | CONSULTA | Cartera | P3 | 42 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/calculadora-prestamo |
| 221 | cop_fdocmtocopcnt01 | CONSULTA | Cartera | P3 | 34 | Baja | cop_docmto | LND_Documents | No | /cartera/documento-cop-cnt |
| 222 | cop_fopcionesproyeccion | FORMULARIO | Cartera | P3 | 29 | Baja | cop_maeobli | LND_LoanPortfolios | No | /cartera/opciones-proyeccion |
| 223 | cop_frmbase01 | SKIP | Cartera | SKIP | 10 | Baja | — | — | No | — |

---

### 2.2 cnt_ -- Contabilidad (62 formularios, 43,514 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 224 | cnt_maecu01 | TRANSACCIONAL | Contabilidad | P2 | 3397 | Alta | cnt_maecuen | ACC_ChartOfAccounts | No | /contabilidad/plan-cuentas |
| 225 | cnt_festadosfinan2 | REPORTE | Contabilidad | P6 | 3121 | Alta | cnt_movimto | ACC_JournalEntries | No | /reportes/contabilidad/estados-financieros |
| 226 | cnt_ftramm01 | TRANSACCIONAL | Contabilidad | P2 | 2906 | Alta | cnt_movimto + cnt_docmto | ACC_JournalEntries + ACC_Documents | No | /contabilidad/movimientos |
| 227 | cnt_festafinanci01 | REPORTE | Contabilidad | P6 | 2547 | Alta | cnt_movimto | ACC_JournalEntries | Si | /reportes/contabilidad/estados-fin-crystal |
| 228 | cnt_fcuenamor01 | TRANSACCIONAL | Contabilidad | P2 | 2299 | Alta | cnt_amortiza | ACC_Amortizations | No | /contabilidad/amortizaciones |
| 229 | cnt_maeni01 | MAESTRO | Contabilidad | P2 | 2018 | Alta | cnt_nit + sys_maenit | COR_People | No | /contabilidad/terceros |
| 230 | cnt_fpresupto01 | TRANSACCIONAL | Contabilidad | P2 | 1525 | Alta | cnt_presupto | ACC_Budgets | No | /contabilidad/presupuestos |
| 231 | cnt_frepitedoc01 | REPORTE | Contabilidad | P6 | 1232 | Alta | cnt_docmto | ACC_Documents | No | /reportes/contabilidad/reimprimir-doc |
| 232 | cnt_fanucom01 | PROCESO | Contabilidad | P5 | 1199 | Alta | cnt_docmto | ACC_Documents | No | /contabilidad/proceso/anular-comprobante |
| 233 | cnt_fsalcxc01 | CONSULTA | Contabilidad | P2 | 1139 | Alta | cnt_tercero | ACC_ThirdPartyAccounts | No | /contabilidad/saldos-cxc |
| 234 | cnt_fcertrefte01 | REPORTE | Contabilidad | P6 | 1090 | Alta | cnt_certrefte | *(eliminada)* | No | /reportes/contabilidad/certificado-retefuente |
| 235 | cnt_fforsimpues01 | REPORTE | Contabilidad | P6 | 972 | Alta | cnt_movimto | ACC_JournalEntries | Si | /reportes/contabilidad/formato-impuestos |
| 236 | cnt_frevimpues01 | REPORTE | Contabilidad | P6 | 966 | Alta | cnt_movimto | ACC_JournalEntries | Si | /reportes/contabilidad/revision-impuestos |
| 237 | cnt_fcuendepre01 | TRANSACCIONAL | Contabilidad | P2 | 856 | Alta | cnt_deprecia | ACC_Depreciations | No | /contabilidad/depreciaciones |
| 238 | cnt_fauxterce01 | REPORTE | Contabilidad | P6 | 779 | Alta | cnt_tercero | ACC_ThirdPartyAccounts | Si | /reportes/contabilidad/auxiliar-terceros |
| 239 | cnt_fauxcue01 | REPORTE | Contabilidad | P6 | 757 | Alta | cnt_maecuen | ACC_ChartOfAccounts | Si | /reportes/contabilidad/auxiliar-cuentas |
| 240 | cnt_famopro01 | TRANSACCIONAL | Contabilidad | P2 | 755 | Alta | cnt_amortiza | ACC_Amortizations | No | /contabilidad/amort-programadas |
| 241 | cnt_flibrosofiles01 | REPORTE | Contabilidad | P6 | 642 | Alta | cnt_movimto | ACC_JournalEntries | Si | /reportes/contabilidad/libros-oficiales |
| 242 | cnt_fconciliabanca | TRANSACCIONAL | Contabilidad | P2 | 634 | Alta | cnt_concibanca | ACC_BankReconciliations | Si | /contabilidad/conciliacion |
| 243 | cnt_finfejecupresu01 | REPORTE | Contabilidad | P6 | 601 | Alta | cnt_presupto | ACC_Budgets | Si | /reportes/contabilidad/ejecucion-presupuesto |
| 244 | cnt_fliscomp01 | REPORTE | Contabilidad | P6 | 594 | Alta | cnt_docmto | ACC_Documents | Si | /reportes/contabilidad/listado-comprobantes |
| 245 | cnt_flineretefu01 | CONFIGURACION | Contabilidad | P1 | 585 | Alta | cnt_linretefuente | ACC_WithholdingTaxLines | No | /contabilidad/config/lineas-retefuente |
| 246 | cnt_flineica01 | CONFIGURACION | Contabilidad | P1 | 576 | Alta | cnt_lineaica | ACC_IcaTaxLines | No | /contabilidad/config/lineas-ica |
| 247 | cnt_flinerenta01 | CONFIGURACION | Contabilidad | P1 | 562 | Alta | cnt_linearenta | ACC_IncomeTaxLines | No | /contabilidad/config/lineas-renta |
| 248 | cnt_flineiva01 | CONFIGURACION | Contabilidad | P1 | 562 | Alta | cnt_lineaiva | ACC_VatTaxLines | No | /contabilidad/config/lineas-iva |
| 249 | cnt_fparfordian01 | CONFIGURACION | Contabilidad | P1 | 559 | Alta | cnt_parfordian | ACC_DianReportFormats | No | /contabilidad/config/formatos-dian |
| 250 | cnt_fparalinforimpue | CONFIGURACION | Contabilidad | P1 | 549 | Alta | cnt_parinfmedian | ACC_FinancialReportParams | No | /contabilidad/config/informes-impuestos |
| 251 | cnt_captcom | TRANSACCIONAL | Contabilidad | P2 | 539 | Alta | cnt_docmto | ACC_Documents | No | /contabilidad/capturar-comprobante |
| 252 | cnt_fbalpru01 | REPORTE | Contabilidad | P6 | 551 | Alta | cnt_maecuen | ACC_ChartOfAccounts | Si | /reportes/contabilidad/balance-prueba |
| 253 | cnt_fciecom01 | PROCESO | Contabilidad | P5 | 511 | Alta | cnt_docmto | ACC_Documents | Si | /contabilidad/proceso/cierre |
| 254 | cnt_finfpresupu01 | REPORTE | Contabilidad | P6 | 482 | Media | cnt_presupto | ACC_Budgets | No | /reportes/contabilidad/info-presupuesto |
| 255 | cnt_factarch01 | PROCESO | Contabilidad | P5 | 480 | Media | cnt_docmto | ACC_Documents | No | /contabilidad/proceso/archivar |
| 256 | cnt_fcieanu01 | PROCESO | Contabilidad | P5 | 475 | Media | cnt_docmto | ACC_Documents | No | /contabilidad/proceso/cierre-anual |
| 257 | cnt_fproypresupu01 | REPORTE | Contabilidad | P6 | 474 | Media | cnt_presupto | ACC_Budgets | No | /reportes/contabilidad/proyeccion-presupuesto |
| 258 | cnt_domto_aux | TRANSACCIONAL | Contabilidad | P2 | 472 | Media | cnt_docaux | ACC_AuxiliaryDocuments | No | /contabilidad/doc-auxiliar |
| 259 | cnt_fcencos01 | MAESTRO | Contabilidad | P2 | 468 | Media | cnt_cencos | COR_CostCenters | No | /contabilidad/centros-costo |
| 260 | cnt_fcartecortolargo | CONSULTA | Contabilidad | P2 | 367 | Media | cnt_saldocortolargo | *(eliminada)* | No | /contabilidad/cartera-corto-largo |
| 261 | cnt_fconcifiscal | PROCESO | Contabilidad | P5 | 339 | Media | cnt_movimto | ACC_JournalEntries | No | /contabilidad/proceso/conciliacion-fiscal |
| 262 | cnt_fcarter01 | CONSULTA | Contabilidad | P2 | 381 | Media | cnt_maecuen | ACC_ChartOfAccounts | No | /contabilidad/cartera-cuentas |
| 263 | cnt_fmovcont01 | CONSULTA | Contabilidad | P2 | 320 | Media | cnt_movimto | ACC_JournalEntries | No | /contabilidad/movimientos-contables |
| 264 | cnt_fconsolida01 | PROCESO | Contabilidad | P5 | 316 | Media | cnt_movimto | ACC_JournalEntries | No | /contabilidad/proceso/consolidar |
| 265 | cnt_fmediosdian01 | PROCESO | Contabilidad | P5 | 315 | Media | cnt_movimto | ACC_JournalEntries | No | /contabilidad/proceso/medios-dian |
| 266 | cnt_fdeprepro01 | PROCESO | Contabilidad | P5 | 313 | Media | cnt_deprecia | ACC_Depreciations | No | /contabilidad/proceso/depreciacion |
| 267 | cnt_finflibmay01 | REPORTE | Contabilidad | P6 | 274 | Media | cnt_movimto | ACC_JournalEntries | No | /reportes/contabilidad/libro-mayor |
| 268 | cnt_famolismae01 | CONSULTA | Contabilidad | P2 | 272 | Media | cnt_amortiza | ACC_Amortizations | No | /contabilidad/listado-amortizaciones |
| 269 | cnt_festampilla | CONFIGURACION | Contabilidad | P1 | 255 | Media | cnt_estampilla | ACC_StampTaxes | No | /contabilidad/config/estampillas |
| 270 | cnt_fcatcue01 | REPORTE | Contabilidad | P6 | 254 | Media | cnt_maecuen | ACC_ChartOfAccounts | Si | /reportes/contabilidad/catalogo-cuentas |
| 271 | cnt_verfcuentsaldo01 | CONSULTA | Contabilidad | P2 | 199 | Baja | cnt_maecuen | ACC_ChartOfAccounts | No | /contabilidad/verificar-saldos |
| 272 | cnt_fpargrupocuenta | CONFIGURACION | Contabilidad | P1 | 199 | Baja | cnt_grupocuenta | ACC_AccountGroups | No | /contabilidad/config/grupos-cuenta |
| 273 | cnt_fauxcuendoc01 | CONSULTA | Contabilidad | P2 | 199 | Baja | cnt_docaux | ACC_AuxiliaryDocuments | No | /contabilidad/auxiliar-cuentas-doc |
| 274 | cnt_fsalcue01 | REPORTE | Contabilidad | P6 | 408 | Media | cnt_maecuen | ACC_ChartOfAccounts | Si | /reportes/contabilidad/saldos-cuentas |
| 275 | cnt_frmliquidez | REPORTE | Contabilidad | P6 | 181 | Baja | cnt_movimto | ACC_JournalEntries | Si | /reportes/contabilidad/liquidez |
| 276 | cnt_fplano01 | PROCESO | Contabilidad | P5 | 178 | Baja | cnt_movimto | ACC_JournalEntries | No | /contabilidad/proceso/generar-plano |
| 277 | cnt_fbasereten01 | CONFIGURACION | Contabilidad | P1 | 161 | Baja | cnt_linretefuente | ACC_WithholdingTaxLines | No | /contabilidad/config/base-retencion |
| 278 | cnt_fflujocaja01 | REPORTE | Contabilidad | P6 | 151 | Baja | cnt_tmpflujocaja | *(eliminada)* | No | /reportes/contabilidad/flujo-caja |
| 279 | cnt_fabrcicom01 | PROCESO | Contabilidad | P5 | 142 | Baja | cnt_docmto | ACC_Documents | No | /contabilidad/proceso/abrir-cierre |
| 280 | cnt_fborradoc01 | PROCESO | Contabilidad | P5 | 125 | Baja | cnt_docmto | ACC_Documents | No | /contabilidad/proceso/borrar-documento |
| 281 | cnt_factlot01 | PROCESO | Contabilidad | P5 | 81 | Baja | cnt_docmto | ACC_Documents | No | /contabilidad/proceso/activar-lote |
| 282 | cnt_frmfactura01 | TRANSACCIONAL | Contabilidad | P2 | 80 | Baja | cnt_docmto | ACC_Documents | No | /contabilidad/factura |
| 283 | cnt_fcomdiario01 | CONSULTA | Contabilidad | P2 | 48 | Baja | cnt_docmto | ACC_Documents | No | /contabilidad/comprobante-diario |
| 284 | cnt_creaconcibanca | FORMULARIO | Contabilidad | P2 | 48 | Baja | cnt_maeconcibanca | ACC_BankReconciliationMasters | No | /contabilidad/crear-conciliacion |
| 285 | cnt_fnumfolio01 | CONFIGURACION | Contabilidad | P1 | 34 | Baja | cnt_docmto | ACC_Documents | No | /contabilidad/config/numero-folio |

---

### 2.3 sys_ -- Sistema (25 formularios, 26,547 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 286 | sys_fcia01 | CONFIGURACION | Sistema | P1 | 6448 | Alta | sys_compania | COR_Companies | Si | /admin/parametros |
| 287 | sys_fadmutilidades | PROCESO | Sistema | P5 | 3228 | Alta | cop_maeobli | LND_LoanPortfolios | No | /admin/proceso/utilidades |
| 288 | sys_fimppla01 | PROCESO | Sistema | P5 | 3214 | Alta | sys_plano | COR_FlatFiles | No | /admin/proceso/importar-plano |
| 289 | sys_faudit01 | CONSULTA | Sistema | P1 | 3068 | Alta | sys_auditoria | AUD_SystemAudit | No | /admin/auditoria |
| 290 | sys_fperus01 | MAESTRO | Sistema | P1 | 2066 | Alta | sys_sasusu + sys_maenit | SEC_Users + COR_People | No | /admin/usuarios |
| 291 | sys_fper01 | MAESTRO | Sistema | P1 | 1669 | Alta | sys_permisos | SEC_Permissions | No | /admin/roles |
| 292 | sys_fcom01 | CONFIGURACION | Sistema | P1 | 1538 | Alta | sys_compania | COR_Companies | No | /admin/parametros-compania |
| 293 | sys_fban01 | MAESTRO | Sistema | P1 | 1192 | Alta | sys_bancos | COR_Banks | No | /admin/maestros/bancos |
| 294 | sys_fusu01 | MAESTRO | Sistema | P1 | 923 | Alta | sys_sasusu | SEC_Users | No | /admin/usuarios-detalle |
| 295 | sys_fcco01 | MAESTRO | Sistema | P1 | 558 | Alta | sys_cencos | COR_CostCenters | No | /admin/maestros/centros-costo |
| 296 | sys_fciu01 | MAESTRO | Sistema | P1 | 463 | Media | sys_ciudades | COR_Cities | No | /admin/maestros/ciudades |
| 297 | sys_fenferm01 | MAESTRO | Sistema | P1 | 413 | Media | sys_enfermedades | COR_Diseases | No | /admin/maestros/enfermedades |
| 298 | sys_fexptab01 | PROCESO | Sistema | P1 | 385 | Media | — | — | No | /admin/proceso/exportar-tabla |
| 299 | sys_fparfactura01 | CONFIGURACION | Sistema | P1 | 207 | Media | sys_parametros | COR_SystemParameters | No | /admin/config/factura |
| 300 | sys_fconvtasa | MAESTRO | Sistema | P1 | 173 | Baja | sys_tasas | COR_ExchangeRates | No | /admin/maestros/tasas |
| 301 | sys_fimprcombloq01 | PROCESO | Sistema | P1 | 157 | Baja | sys_sasusu | SEC_Users | No | /admin/proceso/desbloquear |
| 302 | sys_futilidad01 | PROCESO | Sistema | P5 | 147 | Baja | sys_compania | COR_Companies | No | /admin/proceso/utilidad |
| 303 | sys_fctrlcifin01 | CONFIGURACION | Sistema | P1 | 112 | Baja | sys_parametros | COR_SystemParameters | No | /admin/config/cifras-financieras |
| 304 | sys_fpaises01 | MAESTRO | Sistema | P1 | 93 | Baja | sys_paises | COR_Countries | No | /admin/maestros/paises |
| 305 | sys_fcodciu01 | MAESTRO | Sistema | P1 | 93 | Baja | sys_ciudades | COR_Cities | No | /admin/maestros/codigos-ciudad |
| 306 | sys_flista01 | MAESTRO | Sistema | P1 | 129 | Baja | sys_listas | COR_Lists | No | /admin/maestros/listas |
| 307 | sys_finfperfusu01 | CONSULTA | Sistema | P1 | 42 | Baja | sys_sasusu + sys_permisos | SEC_Users + SEC_Permissions | No | /admin/info-perfil-usuario |
| 308 | sys_fpasword | FORMULARIO | Sistema | SKIP | 7 | Baja | sys_sasusu | SEC_Users | No | — |

---

### 2.4 inv_ -- Inventario (40 formularios, 15,475 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 309 | Inv_frmTrasacion01 | TRANSACCIONAL | Inventario | P4 | 1738 | Alta | inv_movimto | INV_StockMovements | No | /inventario/movimientos |
| 310 | inv_faplcotizacion01 | TRANSACCIONAL | Inventario | P4 | 1514 | Alta | inv_cotizacion | INV_Quotations | No | /inventario/cotizaciones |
| 311 | inv_fgespostven | TRANSACCIONAL | Inventario | P4 | 1248 | Alta | inv_postventa | INV_AfterSales | No | /inventario/postventa |
| 312 | Inv_frmTraPos01 | TRANSACCIONAL | Inventario | P4 | 1182 | Alta | inv_movimto | INV_StockMovements | No | /inventario/punto-venta |
| 313 | inv_fliqcom | PROCESO | Inventario | P5 | 1063 | Alta | inv_comisiones | INV_Commissions | No | /inventario/proceso/liquidar-comisiones |
| 314 | inv_fcomisiones | CONFIGURACION | Inventario | P4 | 695 | Alta | inv_comisiones | INV_Commissions | No | /inventario/config/comisiones |
| 315 | inv_frmcuentas01 | CONFIGURACION | Inventario | P4 | 568 | Alta | inv_cuentas | INV_Accounts | No | /inventario/config/cuentas |
| 316 | dep_flibcanje01 | CONSULTA | Inventario | P4 | 563 | Alta | dep_canje | LND_ExchangeNotes | No | /inventario/libro-canje |
| 317 | inv_fdsto01 | TRANSACCIONAL | Inventario | P4 | 532 | Alta | inv_movimto | INV_StockMovements | No | /inventario/descuento |
| 318 | inv_finfmov01 | REPORTE | Inventario | P6 | 525 | Alta | inv_movimto | INV_StockMovements | No | /reportes/inventario/movimientos |
| 319 | inv_fdevolucion01 | TRANSACCIONAL | Inventario | P4 | 444 | Media | inv_movimto | INV_StockMovements | No | /inventario/devoluciones |
| 320 | inv_fcierre01 | PROCESO | Inventario | P5 | 438 | Media | inv_movimto | INV_StockMovements | No | /inventario/proceso/cierre |
| 321 | inv_ftrasbodega01 | TRANSACCIONAL | Inventario | P4 | 437 | Media | inv_movimto | INV_StockMovements | No | /inventario/traslado-bodega |
| 322 | inv_factlot01 | TRANSACCIONAL | Inventario | P4 | 435 | Media | inv_lotes | INV_Lots | No | /inventario/lotes |
| 323 | inv_frmtipomovtos01 | CONFIGURACION | Inventario | P4 | 402 | Media | inv_tipomov | INV_MovementTypes | No | /inventario/config/tipos-movimiento |
| 324 | inv_finfmov02 | REPORTE | Inventario | P6 | 343 | Media | inv_movimto | INV_StockMovements | No | /reportes/inventario/movimientos-2 |
| 325 | inv_finvfisico01 | PROCESO | Inventario | P5 | 307 | Media | inv_maeprod | INV_Products | No | /inventario/proceso/inventario-fisico |
| 326 | inv_fctrlpuntos01 | TRANSACCIONAL | Inventario | P4 | 275 | Media | inv_puntos | INV_Points | No | /inventario/control-puntos |
| 327 | inv_frmfactura01 | TRANSACCIONAL | Inventario | P4 | 251 | Media | inv_factura | INV_Invoices | No | /inventario/facturacion |
| 328 | inv_frmProductos01 | MAESTRO | Inventario | P4 | 235 | Media | inv_maeprod | INV_Products | No | /inventario/productos |
| 329 | inv_fprecios01 | MAESTRO | Inventario | P4 | 165 | Baja | inv_precios | INV_Prices | No | /inventario/precios |
| 330 | inv_finfpreprod01 | REPORTE | Inventario | P6 | 202 | Media | inv_maeprod | INV_Products | No | /reportes/inventario/precios-productos |
| 331 | inv_fcuadreinv01 | PROCESO | Inventario | P5 | 201 | Media | inv_movimto | INV_StockMovements | No | /inventario/proceso/cuadre |
| 332 | inv_fsalprod01 | REPORTE | Inventario | P6 | 161 | Baja | inv_maeprod | INV_Products | No | /reportes/inventario/saldos |
| 333 | inv_frmarqueos01 | PROCESO | Inventario | P5 | 155 | Baja | inv_movimto | INV_StockMovements | No | /inventario/proceso/arqueo |
| 334 | inv_fgrupos01 | MAESTRO | Inventario | P4 | 152 | Baja | inv_grupos | INV_Groups | No | /inventario/maestros/grupos |
| 335 | inv_finfcostper01 | REPORTE | Inventario | P6 | 153 | Baja | inv_maeprod | INV_Products | No | /reportes/inventario/costos-periodo |
| 336 | inv_fgruposecund | MAESTRO | Inventario | P4 | 142 | Baja | inv_grupos | INV_Groups | No | /inventario/maestros/grupos-secundarios |
| 337 | inv_frmstock01 | CONSULTA | Inventario | P4 | 169 | Baja | inv_maeprod | INV_Products | No | /inventario/stock |
| 338 | inv_finfproductos01 | REPORTE | Inventario | P6 | 133 | Baja | inv_maeprod | INV_Products | No | /reportes/inventario/productos |
| 339 | inv_frmbodega01 | MAESTRO | Inventario | P4 | 134 | Baja | inv_bodegas | INV_Warehouses | No | /inventario/maestros/bodegas |
| 340 | inv_fvendedor01 | MAESTRO | Inventario | P4 | 183 | Baja | inv_vendedores | INV_Sellers | No | /inventario/maestros/vendedores |
| 341 | inv_fcostolotes01 | CONSULTA | Inventario | P4 | 84 | Baja | inv_lotes | INV_Lots | No | /inventario/costos-lotes |
| 342 | inv_frmturnos01 | MAESTRO | Inventario | P4 | 111 | Baja | inv_turnos | INV_Shifts | No | /inventario/maestros/turnos |
| 343 | inv_fgrupouno | MAESTRO | Inventario | P4 | 111 | Baja | inv_grupos | INV_Groups | No | /inventario/maestros/grupo-uno |
| 344 | inv_ftipolista01 | MAESTRO | Inventario | P4 | 73 | Baja | inv_tipolista | INV_PriceListTypes | No | /inventario/maestros/tipos-lista |
| 345 | inv_ftipodsto01 | MAESTRO | Inventario | P4 | 72 | Baja | inv_tipodsto | INV_DiscountTypes | No | /inventario/maestros/tipos-descuento |
| 346 | inv_fubicacion01 | MAESTRO | Inventario | P4 | 68 | Baja | inv_ubicacion | INV_Locations | No | /inventario/maestros/ubicaciones |
| 347 | Inv_fanulacion01 | TRANSACCIONAL | Inventario | P4 | 191 | Baja | inv_movimto | INV_StockMovements | No | /inventario/anulaciones |
| 348 | inv_fcuadreinv01 | PROCESO | Inventario | P5 | 201 | Media | inv_movimto | INV_StockMovements | No | /inventario/proceso/cuadre-inventario |

---

### 2.5 dep_ -- Depositos/Ahorros (13 formularios, 14,582 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 349 | dep_fcuah01 | TRANSACCIONAL | Depositos | P3 | 2775 | Alta | dep_maecah | LND_SavingsAccounts | No | /cartera/ahorros |
| 350 | dep_ftransdep01 | TRANSACCIONAL | Depositos | P3 | 2488 | Alta | dep_movdep | LND_SavingsTransactions | No | /cartera/depositos |
| 351 | dep_fnotdep01 | TRANSACCIONAL | Depositos | P3 | 2401 | Alta | dep_movdep | LND_SavingsTransactions | No | /cartera/retiros |
| 352 | dep_fabrcaj01 | PROCESO | Depositos | P5 | 1687 | Alta | dep_movdep | LND_SavingsTransactions | No | /cartera/proceso/apertura-caja |
| 353 | dep_fpaah01 | CONFIGURACION | Depositos | P1 | 1424 | Alta | dep_paramahorro | LND_SavingsParameters | No | /cartera/config/parametros-ahorro |
| 354 | dep_finfcuentas01 | REPORTE | Depositos | P6 | 998 | Alta | dep_maecah | LND_SavingsAccounts | No | /reportes/depositos/info-cuentas |
| 355 | dep_farqueo01 | REPORTE | Depositos | P6 | 699 | Alta | dep_movdep | LND_SavingsTransactions | Si | /reportes/depositos/arqueo |
| 356 | dep_fliquipap01 | PROCESO | Depositos | P5 | 609 | Alta | dep_movdep | LND_SavingsTransactions | No | /cartera/proceso/liquidar-papeletas |
| 357 | dep_frenovpap01 | PROCESO | Depositos | P5 | 189 | Baja | dep_movdep | LND_SavingsTransactions | No | /cartera/proceso/renovar-papeletas |
| 358 | dep_fdebauto01 | TRANSACCIONAL | Depositos | P3 | 308 | Media | dep_movdep | LND_SavingsTransactions | No | /cartera/debito-automatico |
| 359 | dep_fplanom01 | PROCESO | Depositos | P5 | 92 | Baja | dep_movdep | LND_SavingsTransactions | No | /cartera/proceso/plano-nomina |

---

### 2.6 nom_ -- Nomina (53 formularios, 11,743 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 360 | nom_frmprestsoc01 | TRANSACCIONAL | Nomina | P4 | 1184 | Alta | nom_prestsoc | PAY_SocialBenefits | No | /nomina/prestaciones |
| 361 | nom_frmhoja01 | TRANSACCIONAL | Nomina | P4 | 959 | Alta | nom_nomina | PAY_PayrollPeriods | No | /nomina/hoja-trabajo |
| 362 | nom_frmvac01 | TRANSACCIONAL | Nomina | P4 | 815 | Alta | nom_vacaciones | PAY_Vacations | No | /nomina/vacaciones |
| 363 | nom_frminfIndem01 | REPORTE | Nomina | P6 | 755 | Alta | nom_indemniza | PAY_Severances | No | /reportes/nomina/indemnizaciones |
| 364 | nom_frmempresa01 | MAESTRO | Nomina | P4 | 520 | Alta | nom_empresas | COR_EmployerCompanies | No | /nomina/empresas |
| 365 | nom_frminfgen01 | REPORTE | Nomina | P6 | 290 | Media | nom_nomina | PAY_PayrollPeriods | No | /reportes/nomina/info-general |
| 366 | nom_frmcptos01 | MAESTRO | Nomina | P4 | 370 | Media | nom_conceptos | PAY_PayrollConcepts | No | /nomina/conceptos |
| 367 | nom_frmausent01 | TRANSACCIONAL | Nomina | P4 | 367 | Media | nom_ausencias | PAY_Absences | No | /nomina/ausentismos |
| 368 | nom_frminformes01 | REPORTE | Nomina | P6 | 320 | Media | nom_nomina | PAY_PayrollPeriods | No | /reportes/nomina/informes |
| 369 | nom_frmmovtos01 | TRANSACCIONAL | Nomina | P4 | 312 | Media | nom_movimto | PAY_PayrollEntries | No | /nomina/movimientos |
| 370 | nom_frminfpagos01 | REPORTE | Nomina | P6 | 229 | Media | nom_nomina | PAY_PayrollPeriods | No | /reportes/nomina/info-pagos |
| 371 | nom_frmperpagos01 | CONFIGURACION | Nomina | P1 | 226 | Media | nom_perpagos | PAY_PayPeriods | No | /nomina/config/periodos-pago |
| 372 | nom_frmnovliq01 | TRANSACCIONAL | Nomina | P4 | 214 | Media | nom_novliq | PAY_SettlementEntries | No | /nomina/novedades-liquidacion |
| 373 | nom_frmlibranza01 | TRANSACCIONAL | Nomina | P4 | 201 | Media | nom_libranza | PAY_PayrollDeductions | No | /nomina/libranzas |
| 374 | opc_nomi | FORMULARIO | Nomina | P4 | 202 | Media | — | — | No | /nomina/opciones |
| 375 | nom_frmparautapo01 | CONFIGURACION | Nomina | P1 | 187 | Baja | nom_parautapo | PAY_AutoContribParams | No | /nomina/config/aportes-auto |
| 376 | nom_frmintcesant01 | PROCESO | Nomina | P5 | 179 | Baja | nom_cesantias | PAY_Severances | No | /nomina/proceso/intereses-cesantias |
| 377 | nom_frmprima01 | PROCESO | Nomina | P5 | 177 | Baja | nom_nomina | PAY_PayrollPeriods | No | /nomina/proceso/prima |
| 378 | nom_frmcertlab01 | REPORTE | Nomina | P6 | 176 | Baja | nom_empleado | PAY_Employees | No | /reportes/nomina/certificado-laboral |
| 379 | nom_frmconacum01 | CONSULTA | Nomina | P4 | 174 | Baja | nom_acumulados | PAY_Accumulated | No | /nomina/consulta-acumulados |
| 380 | nom_frmcuentas01 | CONFIGURACION | Nomina | P1 | 172 | Baja | nom_cuentas | PAY_Accounts | No | /nomina/config/cuentas |
| 381 | nom_frmfijos01 | TRANSACCIONAL | Nomina | P4 | 171 | Baja | nom_fijos | PAY_FixedEntries | No | /nomina/fijos |
| 382 | nom_frmconflex01 | CONFIGURACION | Nomina | P1 | 170 | Baja | nom_conflex | PAY_FlexConfig | No | /nomina/config/flexible |
| 383 | nom_frmpagman01 | TRANSACCIONAL | Nomina | P4 | 168 | Baja | nom_nomina | PAY_PayrollPeriods | No | /nomina/pago-manual |
| 384 | nom_frminfempl01 | REPORTE | Nomina | P6 | 166 | Baja | nom_empleado | PAY_Employees | No | /reportes/nomina/info-empleados |
| 385 | nom_frmliqretfte01 | PROCESO | Nomina | P5 | 163 | Baja | nom_nomina | PAY_PayrollPeriods | No | /nomina/proceso/liquidar-retefuente |
| 386 | nom_frmplano01 | PROCESO | Nomina | P5 | 160 | Baja | nom_nomina | PAY_PayrollPeriods | No | /nomina/proceso/generar-plano |
| 387 | nom_frmcontpla01 | PROCESO | Nomina | P5 | 155 | Baja | nom_nomina | PAY_PayrollPeriods | No | /nomina/proceso/contabilizar-planilla |
| 388 | nom_frmplanos01 | PROCESO | Nomina | P5 | 123 | Baja | nom_nomina | PAY_PayrollPeriods | No | /nomina/proceso/planos |
| 389 | nom_frmcerting01 | REPORTE | Nomina | P6 | 121 | Baja | nom_empleado | PAY_Employees | No | /reportes/nomina/certificado-ingresos |
| 390 | nom_frminfaut01 | REPORTE | Nomina | P6 | 116 | Baja | nom_nomina | PAY_PayrollPeriods | No | /reportes/nomina/info-autoliquidacion |
| 391 | nom_frmantcesan01 | PROCESO | Nomina | P5 | 112 | Baja | nom_cesantias | PAY_Severances | No | /nomina/proceso/anticipo-cesantias |
| 392 | nom_frmconvac01 | CONSULTA | Nomina | P4 | 105 | Baja | nom_vacaciones | PAY_Vacations | No | /nomina/consulta-vacaciones |
| 393 | nom_frmconcesant01 | CONSULTA | Nomina | P4 | 105 | Baja | nom_cesantias | PAY_Severances | No | /nomina/consulta-cesantias |
| 394 | nom_frmactdepor01 | MAESTRO | Nomina | P4 | 105 | Baja | nom_actividades | PAY_Activities | No | /nomina/maestros/actividades |
| 395 | nom_frmliquida01 | PROCESO | Nomina | P5 | 100 | Baja | nom_nomina | PAY_PayrollPeriods | No | /nomina/proceso/liquidar |
| 396 | nom_frmpension01 | MAESTRO | Nomina | P1 | 88 | Baja | nom_pension | PAY_PensionFunds | No | /nomina/maestros/pensiones |
| 397 | nom_frmparent01 | MAESTRO | Nomina | P1 | 88 | Baja | nom_parentesco | PAY_Relationships | No | /nomina/maestros/parentescos |
| 398 | nom_frmeps01 | MAESTRO | Nomina | P1 | 88 | Baja | nom_eps | PAY_HealthProviders | No | /nomina/maestros/eps |
| 399 | nom_frmcargos01 | MAESTRO | Nomina | P1 | 87 | Baja | nom_cargos | PAY_Positions | No | /nomina/maestros/cargos |
| 400 | nom_frmarp01 | MAESTRO | Nomina | P1 | 87 | Baja | nom_arp | PAY_WorkRiskProviders | No | /nomina/maestros/arl |
| 401 | nom_frmcauret01 | CONFIGURACION | Nomina | P1 | 85 | Baja | nom_cauret | PAY_WithholdingRules | No | /nomina/config/causacion-retencion |
| 402 | nom_frmcesant01 | MAESTRO | Nomina | P1 | 84 | Baja | nom_cesantfondo | PAY_SeveranceFunds | No | /nomina/maestros/fondos-cesantias |
| 403 | nom_frmprofes01 | MAESTRO | Nomina | P1 | 82 | Baja | nom_profesion | PAY_Professions | No | /nomina/maestros/profesiones |
| 404 | nom_frmcencos01 | MAESTRO | Nomina | P1 | 77 | Baja | nom_cencos | COR_CostCenters | No | /nomina/maestros/centros-costo |
| 405 | nom_frmriesgoprof01 | MAESTRO | Nomina | P1 | 76 | Baja | nom_riesgoprof | PAY_ProfessionalRisks | No | /nomina/maestros/riesgos |
| 406 | nom_frmautliq01 | PROCESO | Nomina | P5 | 73 | Baja | nom_nomina | PAY_PayrollPeriods | No | /nomina/proceso/autoliquidar |
| 407 | nom_frmcalprom01 | PROCESO | Nomina | P5 | 66 | Baja | nom_nomina | PAY_PayrollPeriods | No | /nomina/proceso/calcular-promedios |
| 408 | nom_frmlibprest01 | CONSULTA | Nomina | P4 | 52 | Baja | nom_libranza | PAY_PayrollDeductions | No | /nomina/consulta-libranzas |
| 409 | nom_frmotropagos01 | TRANSACCIONAL | Nomina | P4 | 42 | Baja | nom_nomina | PAY_PayrollPeriods | No | /nomina/otros-pagos |
| 410 | nom_frmcierre01 | PROCESO | Nomina | P5 | 37 | Baja | nom_nomina | PAY_PayrollPeriods | No | /nomina/proceso/cierre |
| 411 | nom_frmbenef01 | MAESTRO | Nomina | P1 | 18 | Baja | nom_bene | COR_Beneficiaries | No | /nomina/maestros/beneficiarios |
| 412 | nom_frmbase01 | SKIP | Nomina | SKIP | 10 | Baja | — | — | No | — |

---

### 2.7 tes_ -- Tesoreria (12 formularios, 7,371 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 413 | tes_fpropa01 | TRANSACCIONAL | Tesoreria | P4 | 1652 | Alta | tes_proveedor | TRS_TreasuryPayments | No | /tesoreria/programar-pago |
| 414 | tes_fama01 | TRANSACCIONAL | Tesoreria | P4 | 1129 | Alta | tes_amortiza | TRS_TreasuryAmortizations | No | /tesoreria/amortizacion |
| 415 | tes_fpago01 | TRANSACCIONAL | Tesoreria | P4 | 1073 | Alta | tes_pagos | TRS_TreasuryPayments | No | /tesoreria/pagos |
| 416 | tes_formato_cheque | PROCESO | Tesoreria | P5 | 591 | Alta | tes_cheques | TRS_Checks | No | /tesoreria/proceso/formato-cheque |
| 417 | tes_flispropa01 | CONSULTA | Tesoreria | P4 | 569 | Alta | tes_proveedor | TRS_TreasuryPayments | No | /tesoreria/listado-programados |
| 418 | tes_fescu01 | MAESTRO | Tesoreria | P4 | 517 | Alta | tes_esquema | TRS_PaymentSchemes | No | /tesoreria/esquemas |
| 419 | tes_cptos01 | CONFIGURACION | Tesoreria | P1 | 491 | Media | tes_conceptos | TRS_TreasuryConcepts | No | /tesoreria/config/conceptos |
| 420 | tes_fctrltes01 | CONFIGURACION | Tesoreria | P1 | 501 | Alta | tes_control | TRS_TreasuryControl | No | /tesoreria/config/control |
| 421 | tes_fcomegre01 | REPORTE | Tesoreria | P6 | 322 | Media | tes_pagos | TRS_TreasuryPayments | Si | /reportes/tesoreria/comprobante-egreso |
| 422 | tes_fdisfondo01 | PROCESO | Tesoreria | P5 | 157 | Baja | tes_fondos | TRS_TreasuryFunds | No | /tesoreria/proceso/distribuir-fondos |
| 423 | tes_finfcheque01 | REPORTE | Tesoreria | P6 | 96 | Baja | tes_cheques | TRS_Checks | Si | /reportes/tesoreria/info-cheques |
| 424 | tes_fsalpro01 | REPORTE | Tesoreria | P6 | 273 | Media | tes_proveedor | TRS_TreasuryPayments | Si | /reportes/tesoreria/saldos-proveedores |

---

### 2.8 cdt_ -- CDTs (6 formularios, 6,971 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 425 | cdt_factcdat01 | TRANSACCIONAL | CDT | P4 | 2485 | Alta | cdt_maecdats | CDT_Certificates | No | /cartera/cdt |
| 426 | cdt_fliquicdat | PROCESO | CDT | P5 | 1729 | Alta | cdt_maecdats | CDT_Certificates | No | /cartera/proceso/liquidar-cdt |
| 427 | cdt_finfcdats01 | REPORTE | CDT | P6 | 882 | Alta | cdt_maecdats | CDT_Certificates | Si | /reportes/cdt/info-cdts |
| 428 | cdt_fliqcdats01 | PROCESO | CDT | P5 | 862 | Alta | cdt_maecdats | CDT_Certificates | No | /cartera/proceso/liquidar-cdts-batch |
| 429 | cdt_fparam01 | CONFIGURACION | CDT | P1 | 777 | Alta | cdt_parame58 | CDT_Parameters | No | /cartera/config/cdt |
| 430 | cdt_frenovcdat01 | TRANSACCIONAL | CDT | P4 | 236 | Media | cdt_maecdats | CDT_Certificates | No | /cartera/cdt-renovacion |

---

### 2.9 ser_ -- Servicios (5 formularios, 5,778 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 431 | ser_fexpweb01 | PROCESO | Servicios | P5 | 2167 | Alta | sys_maenit | COR_People | No | /servicios/proceso/exportar-web |
| 432 | ser_fserclien01 | REPORTE | Servicios | P6 | 1783 | Alta | sys_maenit | COR_People | Si | /reportes/servicios/clientes |
| 433 | ser_gensolici01 | TRANSACCIONAL | Servicios | P4 | 1041 | Alta | ser_solicitudes | WEB_ServiceRequests | No | /servicios/solicitudes |
| 434 | ser_gencita | TRANSACCIONAL | Servicios | P4 | 608 | Alta | ser_citas | WEB_Appointments | No | /servicios/citas |
| 435 | ser_fgenclave01 | PROCESO | Servicios | P4 | 179 | Baja | sys_sasusu | SEC_Users | No | /servicios/generar-clave |

---

### 2.10 deb_ -- Tarjeta Debito (8 formularios, 5,743 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 436 | deb_fpargeneral01 | CONFIGURACION | Tarjeta Debito | P4 | 1396 | Alta | deb_parametros | DEB_Parameters | No | /tarjeta-debito/config/general |
| 437 | deb_fasigtarjetas01 | TRANSACCIONAL | Tarjeta Debito | P4 | 1067 | Alta | deb_tarjetas | DEB_DebitCards | No | /tarjeta-debito/asignar |
| 438 | deb_finftarjLibres01 | REPORTE | Tarjeta Debito | P6 | 1021 | Alta | deb_tarjetas | DEB_DebitCards | Si | /reportes/tarjeta-debito/tarjetas-libres |
| 439 | deb_fpardiario01 | CONFIGURACION | Tarjeta Debito | P4 | 946 | Alta | deb_parametros | DEB_Parameters | No | /tarjeta-debito/config/diario |
| 440 | deb_fbloqtarj01 | TRANSACCIONAL | Tarjeta Debito | P4 | 615 | Alta | deb_tarjetas | DEB_DebitCards | No | /tarjeta-debito/bloquear |
| 441 | deb_faplpla01 | PROCESO | Tarjeta Debito | P5 | 349 | Media | deb_movimientos | DEB_DebitTransactions | Si | /tarjeta-debito/proceso/aplicar-plano |
| 442 | deb_fcuoman01 | TRANSACCIONAL | Tarjeta Debito | P4 | 132 | Baja | deb_tarjetas | DEB_DebitCards | No | /tarjeta-debito/cuota-manejo |
| 443 | deb_fexppla01 | PROCESO | Tarjeta Debito | P5 | 67 | Baja | deb_movimientos | DEB_DebitTransactions | No | /tarjeta-debito/proceso/exportar-plano |

---

### 2.11 gpr_ -- Productivo/Grupos (17 formularios, 4,466 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 444 | gpr_empresa | MAESTRO | Productivo | P4 | 593 | Alta | gpr_empresa | COR_ProductiveCompanies | No | /productivo/empresas |
| 445 | gpr_fasigasocapa | TRANSACCIONAL | Productivo | P4 | 499 | Media | gpr_asociados | COR_ProductiveAssociates | No | /productivo/asignar-asociados |
| 446 | gpr_finversion | TRANSACCIONAL | Productivo | P4 | 415 | Media | gpr_inversiones | COR_ProductiveInvestments | No | /productivo/inversiones |
| 447 | gpr_festadogeneral | REPORTE | Productivo | P6 | 403 | Media | gpr_empresa | COR_ProductiveCompanies | No | /reportes/productivo/estado-general |
| 448 | gpr_fbalancegeneral | REPORTE | Productivo | P6 | 357 | Media | gpr_empresa | COR_ProductiveCompanies | No | /reportes/productivo/balance |
| 449 | gpr_fasocempresa | CONSULTA | Productivo | P4 | 347 | Media | gpr_asociados | COR_ProductiveAssociates | No | /productivo/asociados-empresa |
| 450 | gpr_fplannegocio | TRANSACCIONAL | Productivo | P4 | 265 | Media | gpr_plan | COR_ProductiveBusinessPlans | No | /productivo/plan-negocio |
| 451 | gpr_fdepgropprod | TRANSACCIONAL | Productivo | P4 | 227 | Media | gpr_depositos | COR_ProductiveDeposits | No | /productivo/depositos |
| 452 | gpr_freferenciaprod | CONSULTA | Productivo | P4 | 216 | Media | gpr_referencias | COR_ProductiveReferences | No | /productivo/referencias |
| 453 | gpr_fparamcapa | CONFIGURACION | Productivo | P1 | 204 | Media | gpr_parametros | COR_ProductiveParams | No | /productivo/config/capacitacion |
| 454 | gpr_fparamsub | CONFIGURACION | Productivo | P1 | 183 | Baja | gpr_parametros | COR_ProductiveParams | No | /productivo/config/subsidios |
| 455 | grp_fparamintruc | CONFIGURACION | Productivo | P1 | 127 | Baja | gpr_parametros | COR_ProductiveParams | No | /productivo/config/instruccion |
| 456 | gpr_fparamestsoc | CONFIGURACION | Productivo | P1 | 127 | Baja | gpr_parametros | COR_ProductiveParams | No | /productivo/config/estrato |
| 457 | gpr_fparamuni | CONFIGURACION | Productivo | P1 | 126 | Baja | gpr_parametros | COR_ProductiveParams | No | /productivo/config/unidad |
| 458 | gpr_fparamtiposeg | CONFIGURACION | Productivo | P1 | 126 | Baja | gpr_parametros | COR_ProductiveParams | No | /productivo/config/tipo-seguro |
| 459 | gpr_fparamtipemp | CONFIGURACION | Productivo | P1 | 126 | Baja | gpr_parametros | COR_ProductiveParams | No | /productivo/config/tipo-empresa |
| 460 | gpr_fparamtipoinver | CONFIGURACION | Productivo | P1 | 125 | Baja | gpr_parametros | COR_ProductiveParams | No | /productivo/config/tipo-inversion |

---

### 2.12 cre_ -- Tarjeta Credito (5 formularios, 2,790 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 461 | cre_fasigtarj01 | TRANSACCIONAL | Tarj. Credito | P4 | 315 | Media | cre_tarjetas | CDT_CreditCards | No | /tarjeta-credito/asignar |
| 462 | cre_fparam01 | CONFIGURACION | Tarj. Credito | P1 | 286 | Media | cre_parametros | CDT_CreditCardParams | No | /tarjeta-credito/config/parametros |
| 463 | cre_fconsolida01 | PROCESO | Tarj. Credito | P5 | 89 | Baja | cre_tarjetas | CDT_CreditCards | No | /tarjeta-credito/proceso/consolidar |

---

### 2.13 cli_ -- Clientes (6 formularios, 1,612 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 464 | cli_fsalcxc01 | CONSULTA | Clientes | P4 | 940 | Alta | cli_cuentasxcobrar | INV_AccountsReceivable | No | /clientes/saldos-cxc |
| 465 | cli_fmaecli01 | MAESTRO | Clientes | P4 | 258 | Media | cli_maestro | INV_Customers | No | /clientes/maestro |
| 466 | cli_ffactLotes01 | TRANSACCIONAL | Clientes | P4 | 134 | Baja | cli_factura | INV_Invoices | No | /clientes/factura-lotes |
| 467 | cli_fctrlfact01 | CONSULTA | Clientes | P4 | 130 | Baja | cli_factura | INV_Invoices | No | /clientes/control-facturas |
| 468 | cli_fparfact01 | CONFIGURACION | Clientes | P1 | 92 | Baja | cli_parametros | INV_CustomerParams | No | /clientes/config/facturacion |
| 469 | cli_frepfact01 | REPORTE | Clientes | P6 | 58 | Baja | cli_factura | INV_Invoices | No | /reportes/clientes/facturas |

---

### 2.14 rec_ -- Recaudos (6 formularios, 1,397 lineas)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 470 | rec_pagos | TRANSACCIONAL | Recaudos | P4 | 431 | Media | rec_pagos | LND_CollectionPayments | No | /cartera/recaudos |
| 471 | rec_cierrecajero | PROCESO | Recaudos | P5 | 314 | Media | rec_cierre | LND_CashierClosings | No | /cartera/proceso/cierre-cajero |
| 472 | rec_parconvenio | CONFIGURACION | Recaudos | P1 | 229 | Media | rec_convenios | LND_CollectionAgreements | No | /cartera/config/convenios-recaudo |
| 473 | rec_consulflex | CONSULTA | Recaudos | P4 | 191 | Baja | rec_pagos | LND_CollectionPayments | No | /cartera/recaudos-consulta |
| 474 | rec_recdatos | TRANSACCIONAL | Recaudos | P4 | 140 | Baja | rec_pagos | LND_CollectionPayments | No | /cartera/recaudo-datos |
| 475 | rec_formapago | FORMULARIO | Recaudos | P4 | 92 | Baja | rec_formapago | LND_PaymentMethods | No | /cartera/forma-pago |

---

### 2.15 Otros formularios (sin prefijo de modulo)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 476 | selectmaes | FORMULARIO | General | P1 | 351 | Media | — | — | No | (componente) |
| 477 | inf_finfcomisiones01 | REPORTE | General | P6 | 128 | Baja | inv_comisiones | INV_Commissions | No | /reportes/inventario/comisiones |
| 478 | Frmwait | SKIP | General | SKIP | 5 | Baja | — | — | No | — |

---

### 2.16 Aplicacion/ (7 archivos, 4,355 lineas -- SKIP)

| # | Formulario | Tipo | Modulo | Prior. | Lineas | Logica | Tabla Principal | Tabla Nueva | Crystal | Pagina Blazor |
|---|---|---|---|---|---|---|---|---|---|---|
| 479 | principal | SKIP | Aplicacion | SKIP | 1777 | Alta | — | — | No | (layout Blazor) |
| 480 | inicio | SKIP | Aplicacion | SKIP | 946 | Alta | — | — | No | /login |
| 481 | Ayuda | SKIP | Aplicacion | SKIP | 601 | Alta | — | — | No | (componente) |
| 482 | formularios | SKIP | Aplicacion | SKIP | 487 | Media | — | — | No | (layout Blazor) |
| 483 | imprimir | SKIP | Aplicacion | SKIP | 292 | Media | — | — | No | (componente) |
| 484 | num_solicitud | SKIP | Aplicacion | SKIP | 208 | Media | — | — | No | (componente) |
| 485 | bienvenida | SKIP | Aplicacion | SKIP | 44 | Baja | — | — | No | / |

---

### 2.17 Modulos/ (23 archivos, 13,243 lineas -- SKIP, ya migrados a ERP.Core)

| # | Archivo | Tipo | Lineas | Notas |
|---|---|---|---|---|
| 486 | cop_mgrabamovto | SKIP | 1274 | Migrado a ERP.Core (ClsCartera) |
| 487 | copvar | SKIP | 1249 | Migrado a ERP.Core (variables cartera) |
| 488 | contable | SKIP | 835 | Migrado a ERP.Core (ClsContabilidad) |
| 489 | cop_cuadredoc | SKIP | 674 | Migrado a ERP.Core (cuadre documentos) |
| 490 | cop_contable | SKIP | 521 | Migrado a ERP.Core (contabilizacion cartera) |
| 491 | ImportacionCnt | SKIP | 393 | Migrado a ERP.Core (importacion contable) |
| 492 | cnt_impredocumentos | SKIP | 377 | Migrado a ERP.Core (impresion documentos) |
| 493 | VBAVICAP | SKIP | 376 | Migrado a ERP.Core (captura video) |
| 494 | frmCamara | SKIP | 360 | Migrado a ERP.Core (camara) |
| 495 | calculos_financieros | SKIP | 234 | Migrado a ERP.Core (calculos) |
| 496 | cop_impredocumentos | SKIP | 181 | Migrado a ERP.Core (impresion cartera) |
| 497 | crear_documentos | SKIP | 150 | Migrado a ERP.Core (crear documentos) |
| 498 | solido | SKIP | 138 | Migrado a ERP.Core (modulo principal) |
| 499 | Printinfo | SKIP | 113 | Migrado a ERP.Core (info impresion) |
| 500 | varini | SKIP | 96 | Migrado a ERP.Core (VarIni) |
| 501 | VBmemcap | SKIP | 91 | Migrado a ERP.Core (captura memoria) |
| 502 | CrearODBC | SKIP | 60 | Migrado a ERP.Core (conexion ODBC) |
| 503 | cnt_fcnecos01 | SKIP | 39 | Migrado a ERP.Core (centros costo) |
| 504 | AssemblyInfo | SKIP | 32 | Metadata |
| 505 | stilo_visual | SKIP | 22 | Migrado a ERP.Core (estilos) |
| 506 | Numeros_A_Letras | SKIP | 9 | Migrado a ERP.Core (Numeros_A_Letras) |
| 507 | config_report | SKIP | 8 | Migrado a ERP.Core (config reportes) |
| 508 | CtrlClientes | SKIP | 6 | Migrado a ERP.Core (control clientes) |

---

## 3. DETALLE POR GRUPO DE PRIORIDAD

### P1 -- Maestros Simples + Configuracion + Login (148 formularios)

**Descripcion:** Formularios CRUD simples de tablas maestras y pantallas de configuracion/parametros. Son los mas faciles de migrar porque generalmente tienen un grid + formulario de edicion.

**Patron tipico:** SfGrid (listado) + SfDialog (editar/crear) + SfTextBox/SfDropDownList (campos)

**Incluye:**
- Maestros de sistema: paises, ciudades, bancos, centros de costo, listas, usuarios, roles, enfermedades
- Configuraciones de cartera: lineas de credito, auxilios, asesores, seguros, SIPLA, scoring, movimientos
- Configuraciones contables: lineas de impuestos (ICA, IVA, renta, retefuente), formatos DIAN, grupos de cuenta, estampillas
- Configuraciones de nomina: EPS, ARL, fondos pension/cesantias, cargos, profesiones, parentescos, periodos pago
- Configuraciones productivo: parametros de capacitacion, subsidio, instruccion, etc.
- Maestros de cartera: zonas, comites, enfermedades, programas actividad
- Auditorias simples: auditoria maestro, auditoria lineas credito

**Estimacion:** 1-2 dias por formulario, ~200 dias total

---

### P2 -- Contabilidad Core (38 formularios)

**Descripcion:** Formularios del modulo contable que son fundamentales para el ERP: plan de cuentas, comprobantes, movimientos contables, terceros, presupuestos, amortizaciones, depreciaciones, conciliacion bancaria.

**Patron tipico:** SfGrid con edicion inline + validaciones complejas + interaccion con movimientos contables

**Formularios clave:**
- `cnt_maecu01` (3,397 lin) -- Plan de cuentas: arbol jerarquico, validacion contable
- `cnt_ftramm01` (2,906 lin) -- Tramite de movimientos contables: comprobantes, partida doble
- `cnt_festafinanci01` (2,547 lin) -- Estados financieros: calculos complejos
- `cnt_fcuenamor01` (2,299 lin) -- Cuentas de amortizacion
- `cnt_maeni01` (2,018 lin) -- Maestro de terceros/NIT
- `cnt_fpresupto01` (1,525 lin) -- Presupuestos
- `cnt_fconciliabanca` (634 lin) -- Conciliacion bancaria

**Dependencias:** Muchos formularios de otros modulos graban movimientos contables via msgcnt.

**Estimacion:** 3-5 dias por formulario, ~130 dias total

---

### P3 -- Cartera Financiera (52 formularios)

**Descripcion:** El corazon del ERP. Formularios de gestion de asociados, creditos, desembolsos, pagos, cuotas, ahorros, depositos, causaciones. Logica de negocio altamente compleja.

**Patron tipico:** Multiples grids, dialogos modales, calculos financieros en tiempo real, validaciones cruzadas

**Formularios clave:**
- `cop_fasoma01` (9,006 lin) -- Maestro de asociados: el mas grande del sistema
- `solicitud_credito` (4,148 lin) -- Solicitud de credito: workflow complejo
- `cop_fcreas01` (3,557 lin) -- Crear asociado: multiples tabs, validaciones
- `cop_fcrelc01` (3,459 lin) -- Crear credito: tabla amortizacion, proyecciones
- `cop_ftcapa01` (3,159 lin) -- Pago de cuotas: calculo intereses, mora
- `dep_fcuah01` (2,775 lin) -- Cuentas de ahorro
- `dep_ftransdep01` (2,488 lin) -- Transacciones de deposito
- `dep_fnotdep01` (2,401 lin) -- Notas de deposito/retiro

**Dependencias:** Usa msgcnt (contabilidad), msgliqcre (liquidacion), msgcop (cartera), msgdep (depositos)

**Estimacion:** 5-10 dias por formulario, ~360 dias total

---

### P4 -- Inventario + Nomina + CDT + Debito + Otros (68 formularios)

**Descripcion:** Modulos secundarios pero esenciales: inventario con POS, nomina con liquidaciones, CDTs, tarjeta debito, productivo, clientes, servicios, recaudos.

**Formularios clave:**
- `Inv_frmTrasacion01` (1,738 lin) -- Transacciones inventario
- `cdt_factcdat01` (2,485 lin) -- CDTs
- `deb_fpargeneral01` (1,396 lin) -- Parametros tarjeta debito
- `nom_frmprestsoc01` (1,184 lin) -- Prestaciones sociales
- `nom_frmhoja01` (959 lin) -- Hoja de trabajo nomina

**Estimacion:** 2-4 dias por formulario, ~200 dias total

---

### P5 -- Procesos Complejos (42 formularios)

**Descripcion:** Procesos batch, cierres de periodo, liquidaciones, generacion de planos, clasificacion de cartera, exportaciones a entidades regulatorias (Supersolidaria, SIPLA, CIFIN, DIAN).

**Formularios clave:**
- `sys_fadmutilidades` (3,228 lin) -- Administracion de utilidades
- `sys_fimppla01` (3,214 lin) -- Importar planos
- `cop_fmarcorre01` (1,992 lin) -- Marcacion/correo masivo
- `cdt_fliquicdat` (1,729 lin) -- Liquidar CDTs
- `dep_fabrcaj01` (1,687 lin) -- Apertura de caja
- `cop_fclacart01` (1,689 lin) -- Clasificacion de cartera

**Estimacion:** 3-7 dias por formulario, ~180 dias total

---

### P6 -- Reportes (98 formularios)

**Descripcion:** Formularios que generan reportes, la mayoria con Crystal Reports (61 de ellos). Requieren migracion a QuestPDF o Bold Reports.

**Formularios clave:**
- `cop_fgrafaso01` (2,115 lin) -- Graficos de asociados (Crystal)
- `cop_fcreprocre01` (1,963 lin) -- Reporte de creditos procesados (Crystal)
- `cnt_festadosfinan2` (3,121 lin) -- Estados financieros (complejo)
- `cnt_festafinanci01` (2,547 lin) -- Estados financieros Crystal
- `ser_fserclien01` (1,783 lin) -- Reporte servicios (Crystal)

**Estrategia de migracion:**
1. Crystal Reports simples (listados) -> SfGrid con export PDF/Excel
2. Crystal Reports complejos (estados financieros, extractos) -> QuestPDF
3. Graficos -> SfChart / SfAccumulationChart

**Estimacion:** 2-5 dias por formulario, ~300 dias total

---

### SKIP (44 formularios)

**Descripcion:** Formularios que no necesitan migracion directa:
- Clases base (`cop_frmbase01`, `nom_frmbase01`) -- son base classes abstractas
- Stubs vacios (`Frmwait`, `sys_fpasword`)
- Aplicacion/ (7 archivos) -- login, menu principal, ayuda -> ya resuelto en layout Blazor
- Modulos/ (23 archivos) -- ya migrados a ERP.Core en Fase 1
- Tarjeta credito parcial (`cre_fconsolida01` 89 lin) -- modulo no prioritario

---

## 4. TOP 30 FORMULARIOS MAS COMPLEJOS

| # | Formulario | Modulo | Lineas | Descripcion | Complejidad |
|---|---|---|---|---|---|
| 1 | cop_fasoma01 | Cartera | 9,006 | Maestro completo de asociados: datos personales, beneficiarios, referencias, obligaciones, ahorros, aportes. Multiples tabs, busquedas, validaciones. | Extrema |
| 2 | sys_fcia01 | Sistema | 6,448 | Configuracion empresa: parametros globales del sistema, contabilidad, cartera, inventario, nomina. | Muy Alta |
| 3 | cop_fcaupr01 | Cartera | 4,164 | Causacion periodica de intereses y seguros. Proceso batch con contabilizacion automatica. | Muy Alta |
| 4 | solicitud_credito | Cartera | 4,148 | Workflow completo de solicitud: datos asociado, estudio credito, capacidad pago, scoring, aprobacion. | Muy Alta |
| 5 | cop_fparlc01 | Cartera | 3,893 | Parametrizacion de lineas de credito: tasas, plazos, garantias, topes, ~90 campos. | Muy Alta |
| 6 | cop_ftcanot01 | Cartera | 3,891 | Notas contables de cartera: genera comprobantes, cruza con contabilidad. | Muy Alta |
| 7 | cop_fcreas01 | Cartera | 3,557 | Creacion de asociados: formulario de ingreso con multiples validaciones y busquedas. | Muy Alta |
| 8 | cop_fcrelc01 | Cartera | 3,459 | Crear credito: tabla amortizacion, calculo cuotas, proyeccion, desembolso. Crystal Reports. | Muy Alta |
| 9 | cnt_maecu01 | Contabilidad | 3,397 | Plan de cuentas: arbol jerarquico, auxiliares, clasificacion, saldos. | Muy Alta |
| 10 | cop_feditdoc01 | Cartera | 3,338 | Editor de documentos de cartera: comprobantes complejos con multiples lineas. | Muy Alta |
| 11 | sys_fadmutilidades | Sistema | 3,228 | Administracion de utilidades cooperativas: calculo y distribucion de excedentes. | Muy Alta |
| 12 | sys_fimppla01 | Sistema | 3,214 | Importacion masiva de planos: parsing, validacion, insercion batch. | Muy Alta |
| 13 | cop_ftcapa01 | Cartera | 3,159 | Pago de cuotas: calculo intereses corrientes/mora, seguros, aportes, descuentos. | Muy Alta |
| 14 | cnt_festadosfinan2 | Contabilidad | 3,121 | Estados financieros: balance general, estado resultados, flujo efectivo. | Muy Alta |
| 15 | cop_fccamc01 | Cartera | 3,110 | Cambio de condiciones de credito: reestructuracion, cambio tasa/plazo. | Muy Alta |
| 16 | sys_faudit01 | Sistema | 3,068 | Visor de auditoria del sistema: busqueda, filtros, detalle de cambios. | Alta |
| 17 | cnt_ftramm01 | Contabilidad | 2,906 | Tramite de movimientos contables: captura comprobante con partida doble. | Muy Alta |
| 18 | dep_fcuah01 | Depositos | 2,775 | Cuentas de ahorro: apertura, consulta, parametros, movimientos. | Alta |
| 19 | cnt_festafinanci01 | Contabilidad | 2,547 | Estados financieros con Crystal Reports. | Alta |
| 20 | dep_ftransdep01 | Depositos | 2,488 | Transacciones de deposito: consignaciones, retiros, notas. | Alta |
| 21 | cdt_factcdat01 | CDT | 2,485 | CDTs: constitucion, renovacion, liquidacion parcial, cancelacion. | Alta |
| 22 | cop_fccaec01 | Cartera | 2,433 | Cambio estado de cuenta: normalizacion, castigo, recuperacion. | Alta |
| 23 | dep_fnotdep01 | Depositos | 2,401 | Notas debito/credito de deposito. | Alta |
| 24 | cnt_fcuenamor01 | Contabilidad | 2,299 | Cuentas y tablas de amortizacion contable. | Alta |
| 25 | cop_adicredi | Cartera | 2,220 | Adicion a credito existente: recalculo tabla, desembolso adicional. | Alta |
| 26 | cop_fsolauxaprob01 | Cartera | 2,203 | Aprobacion de auxilios: workflow con validaciones de reglamento. | Alta |
| 27 | ser_fexpweb01 | Servicios | 2,167 | Exportacion de datos para portal web: generacion de archivos masivos. | Alta |
| 28 | cop_fgrafaso01 | Cartera | 2,115 | Graficos de asociados: estadisticas, Crystal Reports. | Alta |
| 29 | cop_fgesmae01 | Cartera | 2,073 | Gestion de cobro: asignacion asesores, registro actividades, seguimiento. | Alta |
| 30 | sys_fperus01 | Sistema | 2,066 | Maestro de usuarios: permisos por modulo, sucursal, funciones. | Alta |

---

## 5. COMPONENTES COMPARTIDOS NECESARIOS

### Componentes ya creados (en IngenIA365ERP.Shared):
1. **PersonSearchDialog** -- Busqueda de personas/terceros (reemplaza CargaAyuda de MsgSas)
2. **AccountSearchDialog** -- Busqueda de plan de cuentas
3. **DateRangeSelector** -- Selector de rango de fechas
4. **ConfirmDialog** -- Reemplazo de MessageBox
5. **LoadingOverlay** -- Spinner de carga

### Componentes adicionales necesarios:

| # | Componente | Reemplaza | Usado por | Prioridad |
|---|---|---|---|---|
| 6 | **LoanSearchDialog** | Busqueda de obligaciones/creditos | ~80 forms cop_ | P2 |
| 7 | **SavingsAccountSearch** | Busqueda de cuentas ahorro | ~15 forms dep_ | P2 |
| 8 | **AmortizationTable** | Grid tabla de amortizacion | solicitud_credito, cop_fcrelc01, cop_ftcalc01 | P3 |
| 9 | **PaymentCalculator** | Calculo de cuotas/intereses | cop_ftcapa01, cop_fcaupr01, cop_ftcaac01 | P3 |
| 10 | **DocumentViewer** | Vista previa de comprobantes | cnt_ftramm01, cop_feditdoc01, cnt_captcom | P2 |
| 11 | **AuditTrail** | Grid de auditoria por registro | sys_faudit01, cop_faudmaca01, cop_faudcupe01 | P1 |
| 12 | **ExcelExporter** | Export a Excel (reemplaza COM interop) | ~40 forms con export | P1 |
| 13 | **PdfReportViewer** | SfPdfViewer para reportes QuestPDF | 61 forms Crystal | P6 |
| 14 | **BranchSelector** | Selector de agencia/sucursal | ~30 forms con filtro agencia | P1 |
| 15 | **PeriodSelector** | Selector de periodo (mes/anio) | ~50 forms contables/nomina | P1 |
| 16 | **CurrencyInput** | SfNumericTextBox con formato moneda | ~100 forms con valores monetarios | P1 |
| 17 | **NitInput** | TextBox con validacion NIT colombiano | cop_fcreas01, cnt_maeni01, sys_fperus01 | P1 |
| 18 | **TreeAccountView** | SfTreeView para plan de cuentas | cnt_maecu01, cnt_fbalpru01, cnt_fcatcue01 | P2 |
| 19 | **CollectionNoticeGenerator** | Generacion circulares de cobro | cop_fcircobro01, cop_fnomcuencobro01 | P5 |
| 20 | **FlatFileProcessor** | Procesamiento de planos bancarios | sys_fimppla01, cop_fasoplano01, nom_frmplano01 | P5 |
| 21 | **FinancialChart** | SfChart para graficos financieros | cop_fgrafaso01, gpr_fbalancegeneral | P6 |
| 22 | **PrintPreviewDialog** | Vista previa de impresion QuestPDF | ~30 forms con impresion directa | P2 |
| 23 | **AssociateCard** | Resumen visual de asociado | cop_fasoma01, cop_fasoca01, cop_fasora01 | P3 |
| 24 | **StatusBadge** | Badge de estado (activo/inactivo/mora) | ~60 forms con estados | P1 |
| 25 | **BatchProgressDialog** | Progreso de procesos batch | ~20 forms proceso (cierre, clasificacion) | P5 |

---

## 6. ESTIMACION DE ESFUERZO

### Por Prioridad

| Prioridad | Forms | Dias/Form Prom. | Dias Totales | Desarrolladores | Semanas |
|---|---|---|---|---|---|
| P1 Maestros + Config | 148 | 1.5 | 222 | 3 | 15 |
| P2 Contabilidad | 38 | 4.0 | 152 | 2 | 15 |
| P3 Cartera | 52 | 7.0 | 364 | 3 | 24 |
| P4 Inv+Nom+CDT+Deb | 68 | 3.0 | 204 | 2 | 20 |
| P5 Procesos | 42 | 4.5 | 189 | 2 | 19 |
| P6 Reportes | 98 | 3.0 | 294 | 2 | 29 |
| SKIP | 44 | 0 | 0 | — | — |
| **Componentes compartidos** | 25 | 3.0 | 75 | 2 | 8 |
| **Total** | **490** | — | **1,500** | — | — |

### Por Modulo

| Modulo | Forms | Lineas | Dias Est. | Semanas (2 devs) |
|---|---|---|---|---|
| Cartera (cop_) | 232 | 180,921 | 750 | 75 |
| Contabilidad (cnt_) | 62 | 43,514 | 200 | 20 |
| Sistema (sys_) | 25 | 26,547 | 80 | 8 |
| Inventario (inv_) | 40 | 15,475 | 100 | 10 |
| Depositos (dep_) | 13 | 14,582 | 60 | 6 |
| Nomina (nom_) | 53 | 11,743 | 90 | 9 |
| Tesoreria (tes_) | 12 | 7,371 | 40 | 4 |
| CDTs (cdt_) | 6 | 6,971 | 30 | 3 |
| Servicios (ser_) | 5 | 5,778 | 25 | 3 |
| Tarjeta Debito (deb_) | 8 | 5,743 | 30 | 3 |
| Productivo (gpr_) | 17 | 4,466 | 30 | 3 |
| Tarjeta Credito (cre_) | 5 | 2,790 | 10 | 1 |
| Clientes (cli_) | 6 | 1,612 | 15 | 2 |
| Recaudos (rec_) | 6 | 1,397 | 15 | 2 |
| Componentes | 25 | — | 75 | 8 |
| **Total** | **490+25** | **328,910** | **1,550** | — |

### Cronograma Sugerido (equipo de 4 desarrolladores)

| Fase | Semanas | Contenido |
|---|---|---|
| Fase 2D-1 | 8 | Componentes compartidos (25 componentes) |
| Fase 2D-2 | 15 | P1: Maestros y configuracion (148 forms) |
| Fase 2D-3 | 15 | P2: Contabilidad core (38 forms) |
| Fase 2D-4 | 24 | P3: Cartera financiera (52 forms) |
| Fase 2D-5 | 20 | P4: Inventario + Nomina + CDT + Debito (68 forms) |
| Fase 2D-6 | 19 | P5: Procesos complejos (42 forms) |
| Fase 2D-7 | 29 | P6: Reportes (98 forms) |
| **Total** | **~130 semanas** | **~2.5 anios con 4 devs** |

### Factores de Aceleracion con IA

Con asistencia de IA (Claude Code, Copilot) para generacion de codigo repetitivo:

| Factor | Reduccion |
|---|---|
| P1 (CRUD repetitivos) | -60% (scaffolding automatico) |
| P2 (patrones contables conocidos) | -30% |
| P3 (logica de negocio unica) | -15% |
| P4 (modulos medianos) | -40% |
| P5 (procesos batch) | -25% |
| P6 (reportes con plantilla) | -50% |
| **Promedio ponderado** | **-35%** |

**Estimacion con IA: ~85 semanas (~1.6 anios con 4 devs)**

---

> **Nota:** Este analisis fue generado automaticamente a partir del conteo de lineas de los 490 formularios VB.NET del proyecto SOLIDO. Las estimaciones de esfuerzo son aproximadas y deben ajustarse segun la complejidad real encontrada durante la migracion. Los mapeos de tablas estan basados en MAPEO-BD-VIEJO-NUEVO.md (269 tablas).
